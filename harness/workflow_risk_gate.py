#!/usr/bin/env python3
"""Porte de risque déterministe pour ForgePilot et la CI.

La politique reste autoritaire sous ``control-plane/workflow-policy.toml``.
Ce module n'en maintient aucune copie : il charge uniquement les champs de
classification et les profils de tests nécessaires à une décision mécanique.
Une entrée absente, ambiguë ou inconnue est refusée au lieu d'être devinée.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import os
import re
import subprocess
import tomllib
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Iterable, Mapping, Sequence


POLICY_ENV = "FORGEPILOT_POLICY"
DEFAULT_POLICY = Path("control-plane/workflow-policy.toml")
RISKS = ("R0", "R1", "R2")
RISK_RANK = {risk: rank for rank, risk in enumerate(RISKS)}
TEST_PROFILES = frozenset({"fast", "pr", "certify"})
_DECLARATION_RE = re.compile(
    r"(?im)^\s*(?:Forge-Risk|Forge-Risque)\s*:\s*(R[012])\s*$"
)


class RiskGateError(RuntimeError):
    """Refus lisible de la porte de risque."""


@dataclass(frozen=True)
class WorkflowPolicy:
    """Vue minimale, validée, de la politique autoritaire."""

    path: Path
    version: int
    r0_allowlist: tuple[str, ...]
    r2_paths: tuple[str, ...]
    test_profiles: Mapping[str, str]
    raw: Mapping[str, object]


def _non_empty_string_list(value: object, *, field: str) -> tuple[str, ...]:
    if not isinstance(value, list) or not value:
        raise RiskGateError(f"{field} doit être une liste TOML non vide")
    result: list[str] = []
    for index, item in enumerate(value):
        if not isinstance(item, str) or not item.strip():
            raise RiskGateError(f"{field}[{index}] doit être une chaîne non vide")
        pattern = item.strip().replace("\\", "/")
        if (
            pattern.startswith(("/", "//"))
            or re.match(r"^[A-Za-z]:/", pattern)
            or ".." in PurePosixPath(pattern).parts
        ):
            raise RiskGateError(f"{field}[{index}] sort du dépôt : {item!r}")
        result.append(pattern)
    return tuple(result)


def resolve_policy_path(repo: Path, explicit: Path | str | None = None) -> Path:
    """Résout explicit > environnement > chemin versionné par défaut."""

    candidate: Path | str = explicit or os.environ.get(POLICY_ENV) or DEFAULT_POLICY
    path = Path(candidate).expanduser()
    if not path.is_absolute():
        path = repo / path
    return path.resolve()


def load_policy(
    repo: Path | str,
    policy_path: Path | str | None = None,
) -> WorkflowPolicy:
    """Charge la politique SC1 et refuse tout fragment inutilisable.

    Les tables de rôles, modèles et délais peuvent évoluer sans affecter ce
    consommateur. Les champs dont la porte dépend sont en revanche stricts.
    """

    repo_path = Path(repo).resolve()
    path = resolve_policy_path(repo_path, policy_path)
    try:
        with path.open("rb") as stream:
            raw = tomllib.load(stream)
    except FileNotFoundError as exc:
        raise RiskGateError(f"politique introuvable : {path}") from exc
    except (OSError, tomllib.TOMLDecodeError) as exc:
        raise RiskGateError(f"politique illisible {path} : {exc}") from exc

    policy_table = raw.get("policy")
    if not isinstance(policy_table, dict):
        raise RiskGateError("table [policy] absente")
    version = policy_table.get("version")
    if not isinstance(version, int) or isinstance(version, bool) or version < 1:
        raise RiskGateError("policy.version doit être un entier positif")

    classification = raw.get("classification")
    if not isinstance(classification, dict):
        raise RiskGateError("table [classification] absente")
    r0_allowlist = _non_empty_string_list(
        classification.get("r0_allowlist"), field="classification.r0_allowlist"
    )
    r2_value = classification.get("r2_paths", classification.get("r2_globs"))
    r2_paths = _non_empty_string_list(r2_value, field="classification.r2_paths")

    risks = raw.get("risks")
    if not isinstance(risks, dict):
        raise RiskGateError("table [risks] absente")
    profiles: dict[str, str] = {}
    for risk in RISKS:
        risk_table = risks.get(risk)
        if not isinstance(risk_table, dict):
            raise RiskGateError(f"table [risks.{risk}] absente")
        profile = risk_table.get("test_profile")
        if profile not in TEST_PROFILES:
            allowed = ", ".join(sorted(TEST_PROFILES))
            raise RiskGateError(
                f"risks.{risk}.test_profile inconnu : {profile!r} (attendu : {allowed})"
            )
        profiles[risk] = profile

    return WorkflowPolicy(
        path=path,
        version=version,
        r0_allowlist=r0_allowlist,
        r2_paths=r2_paths,
        test_profiles=profiles,
        raw=raw,
    )


def normalize_path(path: str) -> str:
    """Normalise un chemin Git relatif et refuse les échappements."""

    if not isinstance(path, str) or not path.strip():
        raise RiskGateError("un chemin modifié est vide")
    normalized = path.strip().replace("\\", "/")
    pure = PurePosixPath(normalized)
    if (
        pure.is_absolute()
        or normalized.startswith("//")
        or re.match(r"^[A-Za-z]:/", normalized)
        or ".." in pure.parts
        or normalized.startswith("./../")
    ):
        raise RiskGateError(f"chemin modifié hors dépôt : {path!r}")
    cleaned = pure.as_posix()
    if cleaned in {"", "."}:
        raise RiskGateError(f"chemin modifié invalide : {path!r}")
    return cleaned


def normalize_paths(paths: Iterable[str]) -> tuple[str, ...]:
    result = sorted({normalize_path(path) for path in paths})
    if not result:
        raise RiskGateError("aucun chemin modifié : classement impossible")
    return tuple(result)


def _matches(path: str, patterns: Sequence[str]) -> bool:
    """Applique la même sémantique de glob que le chargeur ForgePilot."""

    for pattern in patterns:
        candidates = {pattern, pattern.replace("/**/", "/")}
        if pattern.startswith("**/"):
            candidates.add(pattern[3:])
        if any(fnmatch.fnmatchcase(path, candidate) for candidate in candidates):
            return True
    return False


def derive_risk(policy: WorkflowPolicy, paths: Iterable[str]) -> str:
    """Dérive R2 si un chemin l'impose, R0 si tous sont permis, sinon R1."""

    normalized = normalize_paths(paths)
    if any(_matches(path, policy.r2_paths) for path in normalized):
        return "R2"
    if all(_matches(path, policy.r0_allowlist) for path in normalized):
        return "R0"
    return "R1"


def effective_risk(declared: str, derived: str) -> str:
    if declared not in RISK_RANK:
        raise RiskGateError(f"risque déclaré inconnu : {declared!r}")
    if derived not in RISK_RANK:
        raise RiskGateError(f"risque dérivé inconnu : {derived!r}")
    return declared if RISK_RANK[declared] >= RISK_RANK[derived] else derived


def parse_declared_risk(text: str) -> str:
    """Lit l'unique ligne ``Forge-Risk: Rn`` d'un corps de PR."""

    matches = _DECLARATION_RE.findall(text or "")
    if not matches:
        raise RiskGateError("corps de PR sans ligne unique 'Forge-Risk: R0|R1|R2'")
    if len(matches) != 1:
        raise RiskGateError("corps de PR avec plusieurs déclarations Forge-Risk")
    return matches[0]


def changed_paths(repo: Path | str, base: str, head: str) -> tuple[str, ...]:
    """Retourne tous les chemins changés, y compris les deux côtés d'un renommage."""

    repo_path = Path(repo).resolve()
    if not base or not head:
        raise RiskGateError("base et head Git sont obligatoires")
    if re.fullmatch(r"0{40}", base):
        command = [
            "git",
            "diff-tree",
            "--root",
            "--no-commit-id",
            "--name-status",
            "-z",
            "-r",
            head,
        ]
    else:
        command = [
            "git",
            "diff",
            "--name-status",
            "-z",
            "--find-renames",
            base,
            head,
            "--",
        ]
    completed = subprocess.run(
        command,
        cwd=repo_path,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        detail = (
            completed.stderr
            or completed.stdout
            or "erreur Git sans détail".encode("utf-8")
        ).decode(
            "utf-8", errors="replace"
        ).strip()
        raise RiskGateError(f"git diff impossible : {detail}")

    fields = completed.stdout.split(b"\0")
    if fields and fields[-1] == b"":
        fields.pop()
    paths: list[str] = []
    index = 0
    try:
        while index < len(fields):
            status = fields[index].decode("ascii", errors="strict")
            index += 1
            if not status or status[0] not in "ACDMRTUXB":
                raise RiskGateError(f"statut Git inattendu : {status!r}")
            path_count = 2 if status[0] in {"R", "C"} else 1
            if index + path_count > len(fields):
                raise RiskGateError("sortie Git tronquée pendant le classement")
            for raw_path in fields[index : index + path_count]:
                paths.append(raw_path.decode("utf-8", errors="strict"))
            index += path_count
    except UnicodeDecodeError as exc:
        raise RiskGateError("chemin Git non UTF-8 : classement refusé") from exc
    return normalize_paths(paths)


def evaluate(
    policy: WorkflowPolicy,
    paths: Iterable[str],
    declared: str,
) -> dict[str, object]:
    normalized = normalize_paths(paths)
    derived = derive_risk(policy, normalized)
    effective = effective_risk(declared, derived)
    accepted = RISK_RANK[declared] >= RISK_RANK[derived]
    return {
        "schema_version": 1,
        "accepted": accepted,
        "declared_risk": declared,
        "derived_risk": derived,
        "effective_risk": effective,
        "test_profile": policy.test_profiles[effective],
        "paths": list(normalized),
        "policy": {"path": str(policy.path), "version": policy.version},
        "reason": None
        if accepted
        else f"le risque déclaré {declared} est inférieur au risque dérivé {derived}",
    }


def _read_paths_file(path: Path) -> list[str]:
    try:
        return path.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        raise RiskGateError(f"liste de chemins illisible {path} : {exc}") from exc


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
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--policy", type=Path)
    parser.add_argument("--declared-risk", choices=RISKS)
    parser.add_argument("--pr-body-file", type=Path)
    parser.add_argument("--pr-body-env")
    parser.add_argument("--path", action="append", default=[])
    parser.add_argument("--paths-from", type=Path)
    parser.add_argument("--base")
    parser.add_argument("--head")
    parser.add_argument("--output", type=Path)
    return parser


def _declared_from_args(args: argparse.Namespace) -> str:
    sources = sum(
        value is not None
        for value in (args.declared_risk, args.pr_body_file, args.pr_body_env)
    )
    if sources != 1:
        raise RiskGateError(
            "fournir exactement une source de risque : --declared-risk, "
            "--pr-body-file ou --pr-body-env"
        )
    if args.declared_risk:
        return args.declared_risk
    if args.pr_body_file:
        try:
            return parse_declared_risk(args.pr_body_file.read_text(encoding="utf-8"))
        except OSError as exc:
            raise RiskGateError(f"corps de PR illisible : {exc}") from exc
    assert args.pr_body_env
    if args.pr_body_env not in os.environ:
        raise RiskGateError(f"variable de corps de PR absente : {args.pr_body_env}")
    return parse_declared_risk(os.environ[args.pr_body_env])


def _paths_from_args(args: argparse.Namespace) -> tuple[str, ...]:
    direct = list(args.path)
    if args.paths_from:
        direct.extend(_read_paths_file(args.paths_from))
    has_git_range = args.base is not None or args.head is not None
    if direct and has_git_range:
        raise RiskGateError("choisir les chemins explicites ou la plage Git, pas les deux")
    if has_git_range:
        if not args.base or not args.head:
            raise RiskGateError("--base et --head vont ensemble")
        return changed_paths(args.repo, args.base, args.head)
    return normalize_paths(direct)


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        policy = load_policy(args.repo, args.policy)
        declared = _declared_from_args(args)
        paths = _paths_from_args(args)
        payload = evaluate(policy, paths, declared)
        exit_code = 0 if payload["accepted"] else 2
    except RiskGateError as exc:
        payload = {
            "schema_version": 1,
            "accepted": False,
            "error": str(exc),
        }
        exit_code = 2

    rendered = json.dumps(payload, ensure_ascii=False, sort_keys=True)
    print(rendered)
    if args.output:
        _write_json_atomic(args.output, payload)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
