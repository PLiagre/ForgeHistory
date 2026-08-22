#!/usr/bin/env python3
"""Route les tests ``fast`` / ``pr`` / ``certify`` sans les lancer par défaut.

Le plan est un JSON reproductible. La sous-commande ``run`` est la seule qui
exécute des processus ; elle les lance sans shell, en série, et exige
``--allow-heavy`` pour une preuve lourde. Un chemin sensible sans règle est un
refus, jamais un plan vide présenté comme vert.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import os
import subprocess
import sys
import time
from contextlib import contextmanager, nullcontext
from pathlib import Path
from typing import Callable, Iterable, Mapping, NamedTuple, Sequence

_HARNESS_DIR = Path(__file__).resolve().parent
if str(_HARNESS_DIR) not in sys.path:
    # ForgePilot charge ce fichier par son chemin depuis un worktree. Dans ce
    # cas, importlib n'ajoute pas automatiquement le dossier frère à sys.path.
    sys.path.insert(0, str(_HARNESS_DIR))

try:
    from workflow_risk_gate import (
        RISK_RANK,
        RISKS,
        TEST_PROFILES,
        RiskGateError,
        changed_paths,
        derive_risk,
        effective_risk,
        load_policy,
        normalize_paths,
    )
except ModuleNotFoundError:  # import depuis ``harness.workflow_test_router``
    from harness.workflow_risk_gate import (  # type: ignore[no-redef]
        RISK_RANK,
        RISKS,
        TEST_PROFILES,
        RiskGateError,
        changed_paths,
        derive_risk,
        effective_risk,
        load_policy,
        normalize_paths,
    )


class TestRouterError(RuntimeError):
    """Refus lisible du routeur de tests."""


class RouteRule(NamedTuple):
    name: str
    patterns: tuple[str, ...]
    target: str


RULES = (
    RouteRule(
        "politique-workflow",
        ("control-plane/workflow-policy.toml",),
        "workflow",
    ),
    RouteRule("forgepilot", ("control-plane/**",), "forgepilot"),
    RouteRule("harness", ("harness/**",), "harness"),
    RouteRule("simulation", ("sim/**",), "sim"),
    RouteRule(
        "g6",
        (
            "pipeline/geo/steps/06_relief.py",
            "pipeline/geo/qa/*g6*.py",
            "pipeline/geo/tests/*g6*.py",
            "pipeline/geo/artifacts/*g6*.json",
            "pipeline/geo/registry/*relief*.json",
            "pipeline/geo/sources.lock",
            "pipeline/geo/sources/*dem*",
            "pipeline/geo/tools/*dem*.py",
        ),
        "geo-g6",
    ),
    RouteRule(
        "c1",
        (
            "pipeline/geo/steps/*climat*.py",
            "pipeline/geo/steps/*climate*.py",
            "pipeline/geo/qa/*c1*.py",
            "pipeline/geo/tests/*c1*.py",
            "pipeline/geo/artifacts/*c1*.json",
        ),
        "geo-c1",
    ),
    RouteRule("geo-doc", ("pipeline/geo/*.md",), "governance"),
    RouteRule(
        "workflows",
        (
            ".github/**",
            ".cursorignore",
            "hermes/crons/**",
        ),
        "workflow",
    ),
    RouteRule(
        "gouvernance",
        (
            ".claude/**",
            ".gitleaks.toml",
            ".gitignore",
            "SECURITY.md",
            "AGENTS.md",
            "CLAUDE.md",
            "VISION.md",
            "ROADMAP.md",
            "HANDOFF.md",
            "docs/adr/**",
            "docs/rules/**",
            "docs/operations/**",
            "hermes/**",
        ),
        "governance",
    ),
)


SENSITIVE_PREFIXES = (
    ".github/",
    ".claude/",
    "control-plane/",
    "docs/adr/",
    "docs/rules/",
    "harness/",
    "hermes/",
    "pipeline/geo/",
    "sim/",
)
SENSITIVE_ROOTS = frozenset(
    {
        ".cursorignore",
        ".gitleaks.toml",
        ".gitignore",
        "AGENTS.md",
        "CLAUDE.md",
        "SECURITY.md",
        "VISION.md",
    }
)
SENSITIVE_SUFFIXES = frozenset(
    {
        ".geojson",
        ".npy",
        ".npz",
        ".parquet",
        ".ps1",
        ".py",
        ".sh",
        ".toml",
        ".yaml",
        ".yml",
    }
)
PROFILE_RANK = {"fast": 0, "pr": 1, "certify": 2}


WORKFLOW_TESTS = (
    "harness/tests/test_workflow_risk_gate.py",
    "harness/tests/test_workflow_test_router.py",
    "harness/tests/test_workflow_ci_contract.py",
    "harness/tests/test_workflow_hermes_watch.py",
)


def _matches(path: str, pattern: str) -> bool:
    if fnmatch.fnmatchcase(path, pattern):
        return True
    if pattern.endswith("/**"):
        directory = pattern[:-3].rstrip("/")
        return path == directory or path.startswith(f"{directory}/")
    return False


def _is_sensitive(path: str) -> bool:
    return (
        path in SENSITIVE_ROOTS
        or path.startswith(SENSITIVE_PREFIXES)
        or Path(path).suffix.lower() in SENSITIVE_SUFFIXES
    )


def route_targets(paths: Iterable[str]) -> tuple[dict[str, object], tuple[str, ...]]:
    """Associe chaque chemin aux cibles ; retourne aussi les chemins refusés."""

    normalized = normalize_paths(paths)
    assignments: dict[str, object] = {}
    refused: list[str] = []
    for path in normalized:
        targets = sorted(
            {rule.target for rule in RULES if any(_matches(path, p) for p in rule.patterns)}
        )
        if not targets and _is_sensitive(path):
            refused.append(path)
        assignments[path] = targets or ["documentation"]
    return assignments, tuple(refused)


def _command(
    command_id: str,
    argv: Sequence[str],
    *,
    proof: str,
    cwd: str = ".",
    heavy: bool = False,
) -> dict[str, object]:
    return {
        "id": command_id,
        "argv": list(argv),
        "cwd": cwd,
        "proof": proof,
        "heavy": heavy,
    }


def _commands_for_target(target: str, profile: str) -> list[dict[str, object]]:
    python = "{python}"
    if target == "forgepilot":
        return [
            _command(
                "forgepilot-tests",
                [python, "-m", "unittest", "discover", "-s", "tests", "-v"],
                cwd="control-plane",
                proof="suite unitaire du pilote et de sa politique",
            )
        ]
    if target == "harness":
        return [
            _command(
                "harness-tests",
                [python, "-m", "pytest", "harness/tests/", "-q"],
                proof="porte mécanique et cas rouges du harnais",
            )
        ]
    if target == "sim":
        return [
            _command(
                "sim-tests",
                [python, "-m", "pytest", "sim/tests/", "-q"],
                proof="non-régression du moteur vivant",
            )
        ]
    if target == "workflow":
        return [
            _command(
                "workflow-contract-tests",
                [python, "-m", "pytest", *WORKFLOW_TESTS, "-q"],
                proof="déclencheurs CI, risque, routage, ignore et veille Hermes",
            )
        ]
    if target == "governance":
        return [
            _command(
                "single-source-tests",
                [python, "-m", "pytest", "harness/tests/test_single_source_of_instruction.py", "-q"],
                proof="le brief demeure l'unique instruction d'exécution",
            )
        ]
    if target == "geo-c1":
        return [
            _command(
                "c1-proof",
                [python, "pipeline/geo/tests/run_proof_c1.py"],
                proof="preuve déterministe climat C1",
            )
        ]
    if target == "geo-g6":
        commands = [
            _command(
                "g6-sentinel",
                [python, "-m", "pytest", "pipeline/geo/tests/test_g6_acceleration.py", "-q"],
                proof="sentinelle G6 sans téléchargement DEM Europe",
            )
        ]
        if profile == "certify":
            commands.append(
                _command(
                    "g6-europe-certification",
                    [python, "pipeline/geo/tests/run_proof_g6.py"],
                    proof="preuve G6 Europe sur le SHA final et cache DEM vérifié",
                    heavy=True,
                )
            )
        return commands
    if target == "documentation":
        return []
    raise TestRouterError(f"cible de tests inconnue : {target}")


def _git_diff_command(base_sha: str | None, head_sha: str | None) -> dict[str, object]:
    argv = ["git", "diff", "--check"]
    if base_sha and head_sha:
        argv.extend([base_sha, head_sha, "--"])
    elif base_sha:
        raise TestRouterError("base_sha exige head_sha")
    return _command(
        "git-diff-check",
        argv,
        proof="absence d'erreur d'espaces dans le diff ciblé",
    )


def build_plan(
    repo: Path | str,
    paths: Iterable[str],
    profile: str | None = None,
    *,
    risk: str | None = None,
    policy_path: Path | str | None = None,
    base_sha: str | None = None,
    head_sha: str | None = None,
) -> dict[str, object]:
    """Construit un plan pur ; aucune commande n'est lancée."""

    repo_path = Path(repo).resolve()
    try:
        normalized = normalize_paths(paths)
    except RiskGateError as exc:
        raise TestRouterError(str(exc)) from exc
    requested_risk = risk
    derived: str | None = None
    required_profile: str | None = None
    proof_timeout_seconds: int | None = None
    if risk is not None:
        if risk not in RISK_RANK:
            raise TestRouterError(f"risque inconnu : {risk!r}")
        try:
            policy = load_policy(repo_path, policy_path)
        except RiskGateError as exc:
            raise TestRouterError(str(exc)) from exc
        derived = derive_risk(policy, normalized)
        risk = effective_risk(risk, derived)
        policy_profile = policy.test_profiles[risk]
        required_profile = policy_profile
        risks_raw = policy.raw.get("risks")
        if isinstance(risks_raw, dict):
            risk_raw = risks_raw.get(risk)
            timeouts_raw = risk_raw.get("timeouts") if isinstance(risk_raw, dict) else None
            proof_timeout = timeouts_raw.get("proof") if isinstance(timeouts_raw, dict) else None
            if proof_timeout is not None:
                if (
                    isinstance(proof_timeout, bool)
                    or not isinstance(proof_timeout, int)
                    or proof_timeout <= 0
                ):
                    raise TestRouterError(
                        f"délai de preuve invalide pour {risk} : {proof_timeout!r}"
                    )
                proof_timeout_seconds = proof_timeout
        if profile is None:
            profile = policy_profile
    if profile not in TEST_PROFILES:
        raise TestRouterError(f"profil de tests inconnu : {profile!r}")
    if profile == "certify" and (not base_sha or not head_sha):
        raise TestRouterError(
            "le profil certify exige la base et le SHA final (--base-sha/--head-sha)"
        )

    try:
        assignments, refused = route_targets(normalized)
    except RiskGateError as exc:
        raise TestRouterError(str(exc)) from exc
    if refused:
        raise TestRouterError(
            "chemin(s) sensible(s) sans règle de tests : " + ", ".join(refused)
        )

    targets = sorted(
        {
            target
            for values in assignments.values()
            for target in values  # type: ignore[union-attr]
        }
    )
    commands = [_git_diff_command(base_sha, head_sha)]
    for target in targets:
        commands.extend(_commands_for_target(target, profile))

    # Une cible commune ne doit pas lancer deux fois la même suite.
    deduplicated: dict[str, dict[str, object]] = {}
    for command in commands:
        command_id = str(command["id"])
        previous = deduplicated.get(command_id)
        if previous is not None and previous != command:
            raise TestRouterError(f"identifiant de commande ambigu : {command_id}")
        deduplicated[command_id] = command

    return {
        "schema_version": 1,
        "status": "planned",
        "profile": profile,
        "required_profile": required_profile,
        "satisfies_policy": (
            None
            if required_profile is None
            else PROFILE_RANK[profile] >= PROFILE_RANK[required_profile]
        ),
        "risk": risk,
        "requested_risk": requested_risk,
        "derived_risk": derived,
        "proof_timeout_seconds": proof_timeout_seconds,
        "base_sha": base_sha,
        "head_sha": head_sha,
        "paths": list(normalized),
        "assignments": assignments,
        "commands": list(deduplicated.values()),
        "heavy_commands": [
            command_id
            for command_id, command in deduplicated.items()
            if command["heavy"]
        ],
        "serial": True,
    }


def _tail(text: str, limit: int = 2000) -> str:
    return text[-limit:] if len(text) > limit else text


def _current_head(repo: Path) -> str:
    completed = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repo,
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        raise TestRouterError("HEAD Git illisible avant certification")
    return completed.stdout.strip()


def _heavy_lock_path(
    repo: Path,
    *,
    environ: Mapping[str, str] | None = None,
) -> Path:
    """Choisit un verrou partagé par les worktrees, ou global au VPS si configuré."""

    environment = os.environ if environ is None else environ
    configured = environment.get("FORGEPILOT_HEAVY_LOCK", "").strip()
    if configured:
        path = Path(configured).expanduser()
        if not path.is_absolute():
            raise TestRouterError("FORGEPILOT_HEAVY_LOCK doit être un chemin absolu")
        return path
    completed = subprocess.run(
        ["git", "rev-parse", "--path-format=absolute", "--git-common-dir"],
        cwd=repo,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        raise TestRouterError("répertoire Git commun illisible pour le verrou lourd")
    return Path(completed.stdout.strip()).resolve() / "forgepilot-heavy-proof.lock"


@contextmanager
def _exclusive_heavy_lock(repo: Path):
    """Refuse une deuxième preuve lourde concurrente sur le même Git/VPS."""

    lock_path = _heavy_lock_path(repo)
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    stream = lock_path.open("a+b")
    acquired = False
    try:
        if stream.tell() == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        try:
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl

                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            acquired = True
        except (BlockingIOError, OSError) as exc:
            raise TestRouterError(
                f"une preuve lourde utilise déjà le verrou {lock_path}"
            ) from exc
        yield str(lock_path)
    finally:
        if acquired:
            if os.name == "nt":
                import msvcrt

                stream.seek(0)
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl

                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)
        stream.close()


def run_plan(
    plan: Mapping[str, object],
    repo: Path | str,
    *,
    allow_heavy: bool = False,
    runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> dict[str, object]:
    """Exécute le plan en série et retourne codes, durées et preuves."""

    repo_path = Path(repo).resolve()
    profile = plan.get("profile")
    if profile not in TEST_PROFILES:
        raise TestRouterError("plan sans profil reconnu")
    head_sha = plan.get("head_sha")
    if profile == "certify":
        if not isinstance(head_sha, str) or not head_sha:
            raise TestRouterError("plan certify sans SHA final")
        if _current_head(repo_path) != head_sha:
            raise TestRouterError("le SHA final du plan ne correspond plus à HEAD")

    commands = plan.get("commands")
    if not isinstance(commands, list) or not commands:
        raise TestRouterError("plan sans commande")
    has_heavy = any(isinstance(item, dict) and item.get("heavy") for item in commands)
    if has_heavy and not allow_heavy:
        raise TestRouterError(
            "le plan contient une preuve lourde ; relancer explicitement avec --allow-heavy"
        )

    started = time.monotonic()
    results: list[dict[str, object]] = []
    overall_code = 0
    timeout_value = plan.get("proof_timeout_seconds")
    if timeout_value is not None and (
        isinstance(timeout_value, bool)
        or not isinstance(timeout_value, int)
        or timeout_value <= 0
    ):
        raise TestRouterError("plan avec délai de preuve invalide")
    lock_context = _exclusive_heavy_lock(repo_path) if has_heavy else nullcontext(None)
    with lock_context as heavy_lock:
        for item in commands:
            if not isinstance(item, dict):
                raise TestRouterError("commande de plan invalide")
            argv_value = item.get("argv")
            cwd_value = item.get("cwd")
            if not isinstance(argv_value, list) or not all(isinstance(x, str) for x in argv_value):
                raise TestRouterError("argv de plan invalide")
            if not isinstance(cwd_value, str):
                raise TestRouterError("cwd de plan invalide")
            argv = [sys.executable if arg == "{python}" else arg for arg in argv_value]
            cwd = (repo_path / cwd_value).resolve()
            try:
                cwd.relative_to(repo_path)
            except ValueError as exc:
                raise TestRouterError(f"cwd hors dépôt : {cwd_value}") from exc

            command_started = time.monotonic()
            failure_kind: str | None = None
            try:
                completed = runner(
                    argv,
                    cwd=cwd,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    capture_output=True,
                    check=False,
                    timeout=timeout_value,
                )
                code = completed.returncode
                stdout = completed.stdout or ""
                stderr = completed.stderr or ""
            except subprocess.TimeoutExpired as exc:
                code = 124
                failure_kind = "timeout"
                stdout = exc.stdout or ""
                stderr = exc.stderr or f"délai de preuve dépassé ({timeout_value} s)"
            except OSError as exc:
                code = 127
                failure_kind = "launch-error"
                stdout = ""
                stderr = str(exc)
            if isinstance(stdout, bytes):
                stdout = stdout.decode("utf-8", errors="replace")
            if isinstance(stderr, bytes):
                stderr = stderr.decode("utf-8", errors="replace")
            duration = round(time.monotonic() - command_started, 6)
            result = {
                "id": item.get("id"),
                "code": code,
                "duration_seconds": duration,
                "proof": item.get("proof"),
                "heavy": bool(item.get("heavy")),
                "failure_kind": failure_kind,
                "stdout_tail": _tail(stdout),
                "stderr_tail": _tail(stderr),
            }
            results.append(result)
            if code != 0:
                overall_code = code or 1
                break

    return {
        "schema_version": 1,
        "status": "passed" if overall_code == 0 else "failed",
        "code": overall_code,
        "profile": profile,
        "head_sha": head_sha,
        "heavy_lock": heavy_lock,
        "proof_timeout_seconds": timeout_value,
        "duration_seconds": round(time.monotonic() - started, 6),
        "results": results,
    }


def _write_json_atomic(path: Path, payload: Mapping[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.replace(temporary, path)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("plan", "run"))
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--policy", type=Path)
    parser.add_argument("--profile", choices=sorted(TEST_PROFILES))
    parser.add_argument("--risk", choices=RISKS)
    parser.add_argument("--path", action="append", default=[])
    parser.add_argument("--paths-from", type=Path)
    parser.add_argument("--base-sha")
    parser.add_argument("--head-sha")
    parser.add_argument("--allow-heavy", action="store_true")
    parser.add_argument("--output", type=Path)
    return parser


def _paths_from_args(args: argparse.Namespace) -> list[str]:
    paths = list(args.path)
    if args.paths_from:
        try:
            paths.extend(args.paths_from.read_text(encoding="utf-8").splitlines())
        except OSError as exc:
            raise TestRouterError(f"liste de chemins illisible : {exc}") from exc
    if not paths and args.base_sha and args.head_sha:
        try:
            paths.extend(changed_paths(args.repo, args.base_sha, args.head_sha))
        except RiskGateError as exc:
            raise TestRouterError(str(exc)) from exc
    return paths


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        plan = build_plan(
            args.repo,
            _paths_from_args(args),
            args.profile,
            risk=args.risk,
            policy_path=args.policy,
            base_sha=args.base_sha,
            head_sha=args.head_sha,
        )
        payload = (
            plan
            if args.mode == "plan"
            else run_plan(plan, args.repo, allow_heavy=args.allow_heavy)
        )
        exit_code = 0 if payload.get("code", 0) == 0 else int(payload["code"])
    except (RiskGateError, TestRouterError) as exc:
        payload = {"schema_version": 1, "status": "refused", "error": str(exc)}
        exit_code = 2
    print(json.dumps(payload, ensure_ascii=False, sort_keys=True))
    if args.output:
        _write_json_atomic(args.output, payload)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
