from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
from typing import Callable, Iterable

from .config import (
    CURSOR_EFFORT_REFUSED,
    RoleSettings,
    Settings,
    assert_valid_effort,
)
from .policy import GROK_EFFORTS, effective_risk
from .process import PilotError, git, resolve_binary, run_command, run_command_stream
from .protocol import extract_session_id, validate_plan, write_normalized_json
from .publication import enforce_allowed_paths, stage_explicit_paths, working_tree_paths


READ_ONLY_CLAUDE_TOOLS = "Read,Glob,Grep"
CHAIN_STEPS = ("plan", "execute", "publish", "review")
PROPOSITION_REFUSED = (
    "Une proposition Hermes n'est pas une instruction. "
    "Passer un brief (harness/queue/briefs/.../brief.md) ou un fichier de tâche."
)
API_KEY_REFUSED = (
    "ANTHROPIC_API_KEY est défini ; le pilote doit utiliser l'abonnement Claude Pro."
)
CONTROLLER_SECRET_ENV = re.compile(
    r"(?:discord|github|^gh_|api[_-]?key|access[_-]?token|secret|password|authorization)",
    re.IGNORECASE,
)
PORTABLE_CURSOR_COMMAND_UNITS = 30_000


@dataclass(frozen=True)
class Invocation:
    role: str
    argv: tuple[str, ...]
    cwd: str
    environment: dict[str, str]
    prompt: str | None = None
    model: str | None = None
    effort: str | None = None
    backend: str | None = None


def _read_prompt(name: str) -> str:
    path = Path(__file__).resolve().parent.parent / "prompts" / name
    return path.read_text(encoding="utf-8")


def _task_text(path: Path) -> str:
    if not path.is_file():
        raise PilotError(f"Tâche introuvable : {path}")
    text = path.read_text(encoding="utf-8").strip()
    if not text:
        raise PilotError("La tâche est vide.")
    return text


def _slug(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug[:48] or "task"


def default_task_name(task: Path) -> str:
    """Nom de lot : dossier du brief, sinon le nom du fichier."""
    name = task.name.lower()
    if name in {"brief.md", "task.md"} and task.parent.name not in {"", ".", "/"}:
        return _slug(task.parent.name)
    return _slug(task.stem)


def assert_not_api_billing() -> None:
    if os.environ.get("ANTHROPIC_API_KEY"):
        raise PilotError(API_KEY_REFUSED)


def assert_task_is_instruction(task: Path) -> None:
    """Refuse une proposition Hermes : ce n'est pas un brief."""
    parts = [part.lower() for part in task.parts]
    if "hermes" in parts and "propositions" in parts:
        raise PilotError(PROPOSITION_REFUSED)
    if task.name.upper().startswith("PROPOSITION-"):
        raise PilotError(PROPOSITION_REFUSED)


def resolve_role(
    settings: Settings,
    role: str,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
) -> RoleSettings:
    """Priorité D3 : drapeau > [roles.*] > [tools] > défaut du binaire."""
    role_cfg = settings.roles.get(role, RoleSettings())
    if risk and settings.policy is not None:
        policy_role = settings.policy.profile(risk).roles[role]
        role_cfg = RoleSettings(model=policy_role.model, effort=policy_role.effort)
    if model:
        resolved_model = model
    elif role_cfg.model:
        resolved_model = role_cfg.model
    elif role in ("planner", "reviewer"):
        resolved_model = settings.claude_model
    else:
        resolved_model = settings.cursor_model

    if effort:
        resolved_effort = effort
    elif role_cfg.effort:
        resolved_effort = role_cfg.effort
    else:
        resolved_effort = ""
    assert_valid_effort(resolved_effort or "")

    return RoleSettings(model=resolved_model or "", effort=resolved_effort or "")


def _role_backend(settings: Settings, risk: str | None, role: str) -> str:
    if risk is None or settings.policy is None:
        return "cursor" if role == "executor" else "claude"
    return settings.policy.profile(risk).roles[role].backend


def _assert_policy_backend(
    settings: Settings,
    risk: str | None,
    role: str,
    expected: str,
) -> None:
    backend = _role_backend(settings, risk, role)
    if risk is None or settings.policy is None:
        return
    if backend != expected:
        raise PilotError(
            f"Le profil {risk} affecte {role} au backend {backend!r}, pas {expected!r}."
        )


def grok_model_for_effort(model: str, effort: str) -> str:
    """Grok 4.6 n'a pas --effort : l'effort est le suffixe du slug."""
    resolved = effort
    if resolved == "max":
        resolved = "xhigh"
    if not model:
        return model
    if any(model.endswith(f"-{level}") for level in GROK_EFFORTS):
        return model
    if resolved and resolved in GROK_EFFORTS and "grok-4.6" in model:
        return f"{model}-{resolved}"
    return model


def _cursor_read_argv(
    settings: Settings,
    repo: Path,
    prompt: str,
    *,
    mode: str,
    model: str,
) -> list[str]:
    argv = [
        settings.cursor_binary,
        "-p",
        prompt,
        "--mode",
        mode,
        "--trust",
        "--workspace",
        str(repo),
        "--output-format",
        "json",
    ]
    if model:
        argv.extend(["--model", model])
    command_line = subprocess.list2cmdline(argv)
    command_units = len(command_line.encode("utf-16-le")) // 2 + 1
    if command_units > PORTABLE_CURSOR_COMMAND_UNITS:
        raise PilotError(
            "Prompt Cursor trop grand pour une invocation portable "
            f"({command_units} unités UTF-16 > {PORTABLE_CURSOR_COMMAND_UNITS}) ; "
            "scinder le plan ou le bundle."
        )
    return argv


def _claude_argv(
    settings: Settings,
    role: str,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
) -> list[str]:
    argv = [
        settings.claude_binary,
        "-p",
        "--output-format",
        "json",
        "--permission-mode",
        "plan",
        "--tools",
        READ_ONLY_CLAUDE_TOOLS,
        "--disallowedTools",
        "mcp__*",
        "--safe-mode",
        "--disable-slash-commands",
        "--no-chrome",
        "--no-session-persistence",
    ]
    resolved = resolve_role(settings, role, model=model, effort=effort, risk=risk)
    if resolved.model:
        argv.extend(["--model", resolved.model])
    if resolved.effort:
        argv.extend(["--effort", resolved.effort])
    return argv


def plan_invocation(
    settings: Settings,
    repo: Path,
    task: Path,
    *,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
) -> Invocation:
    backend = _role_backend(settings, risk, "planner")
    if backend == "none":
        raise PilotError("Aucun planificateur n'est configuré pour ce risque.")
    task_body = _task_text(task)
    prompt = _read_prompt("planner.md").replace("{{TASK}}", task_body)
    resolved = resolve_role(settings, "planner", model=model, effort=effort, risk=risk)
    if backend == "cursor":
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        argv = _cursor_read_argv(
            settings, repo, prompt, mode="plan", model=cursor_model
        )
        return Invocation(
            "planner",
            tuple(argv),
            str(repo),
            {},
            prompt,
            model=cursor_model or None,
            effort=resolved.effort or None,
            backend="cursor",
        )
    argv = _claude_argv(settings, "planner", model=model, effort=effort, risk=risk)
    return Invocation(
        "planner",
        tuple(argv),
        str(repo),
        {},
        prompt,
        model=resolved.model or None,
        effort=resolved.effort or None,
        backend="claude",
    )


def review_invocation(
    settings: Settings,
    repo: Path,
    plan: Path,
    base: str,
    *,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
    bundle_path: Path | None = None,
) -> Invocation:
    backend = _role_backend(settings, risk, "reviewer")
    if backend == "none":
        raise PilotError("Aucun relecteur n'est configuré pour ce risque.")
    if bundle_path is not None:
        bundle_body = _task_text(bundle_path)
    else:
        plan_body = _task_text(plan)
        diff = git(repo, "diff", "--no-ext-diff", f"{base}...HEAD")
        if not diff:
            raise PilotError(f"Aucun diff à relire contre {base}.")
        bundle_body = json.dumps(
            {
                "base": base,
                "plan": plan_body,
                "manual_diffs": {"legacy-diff": diff},
                "generated_artifacts": [],
                "mechanical_results": [],
                "producer_conclusions_included": False,
            },
            ensure_ascii=False,
        )
    prompt = _read_prompt("reviewer.md").replace("{{REVIEW_BUNDLE}}", bundle_body)
    resolved = resolve_role(settings, "reviewer", model=model, effort=effort, risk=risk)
    if backend == "cursor":
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        argv = _cursor_read_argv(
            settings, repo, prompt, mode="ask", model=cursor_model
        )
        return Invocation(
            "reviewer",
            tuple(argv),
            str(repo),
            {},
            prompt,
            model=cursor_model or None,
            effort=resolved.effort or None,
            backend="cursor",
        )
    argv = _claude_argv(settings, "reviewer", model=model, effort=effort, risk=risk)
    return Invocation(
        "reviewer",
        tuple(argv),
        str(repo),
        {},
        prompt,
        model=resolved.model or None,
        effort=resolved.effort or None,
        backend="claude",
    )


def witness_invocation(
    settings: Settings,
    repo: Path,
    plan: Path,
    base: str,
    *,
    bundle_path: Path | None = None,
) -> Invocation:
    """Témoin Claude hors chemin quotidien (ADR-0017)."""
    if settings.policy is None:
        raise PilotError("Politique absente ; le témoin Claude n'est pas configurable.")
    witness = settings.policy.witness
    if witness.backend != "claude":
        raise PilotError("Le témoin doit rester Claude (ADR-0017).")
    review = review_invocation(
        settings,
        repo,
        plan,
        base,
        model=witness.model,
        effort=witness.effort,
        bundle_path=bundle_path,
        risk=None,
    )
    return Invocation(
        "witness",
        review.argv,
        review.cwd,
        review.environment,
        review.prompt,
        model=witness.model or None,
        effort=witness.effort or None,
        backend="claude",
    )


def executor_invocation(
    settings: Settings,
    worktree: Path,
    plan: Path,
    *,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
    feedback: Path | None = None,
    resume_session: str | None = None,
) -> Invocation:
    _assert_policy_backend(settings, risk, "executor", "cursor")
    if effort:
        raise PilotError(CURSOR_EFFORT_REFUSED)
    plan_body = _task_text(plan)
    if feedback is not None:
        feedback_body = _task_text(feedback)
        prompt = (
            _read_prompt("iterator.md")
            .replace("{{PLAN}}", plan_body)
            .replace("{{FEEDBACK}}", feedback_body)
        )
    else:
        prompt = _read_prompt("executor.md").replace("{{PLAN}}", plan_body)
    resolved = resolve_role(settings, "executor", model=model, effort=None, risk=risk)
    argv = [
        settings.cursor_binary,
        "-p",
        prompt,
        "--force",
        "--sandbox",
        "enabled",
        "--trust",
        "--workspace",
        str(worktree),
        "--output-format",
        "json",
    ]
    if resolved.model:
        argv.extend(["--model", resolved.model])
    if resume_session:
        argv.extend(["--resume", resume_session])
    # Cursor CLI impose aujourd'hui le prompt avec `-p`. Refuser avant
    # CreateProcessW avec une marge explicite est préférable à un échec opaque
    # (la limite Windows est 32 767 unités UTF-16 pour la ligne complète).
    command_line = subprocess.list2cmdline(argv)
    command_units = len(command_line.encode("utf-16-le")) // 2 + 1
    if command_units > PORTABLE_CURSOR_COMMAND_UNITS:
        raise PilotError(
            "Prompt Cursor trop grand pour une invocation portable "
            f"({command_units} unités UTF-16 > {PORTABLE_CURSOR_COMMAND_UNITS}) ; "
            "scinder le plan ou le feedback."
        )
    return Invocation(
        "executor",
        tuple(argv),
        str(worktree),
        {},
        prompt=prompt,
        model=resolved.model or None,
        effort=None,
        backend="cursor",
    )


def ensure_clean_repo(repo: Path) -> None:
    if not (repo / ".git").exists():
        raise PilotError(f"Ce chemin n'est pas un dépôt Git : {repo}")
    status = git(repo, "status", "--porcelain")
    if status:
        raise PilotError("Le dépôt contient des changements locaux ; exécution refusée.")


def create_worktree(repo: Path, task_name: str, base: str) -> tuple[Path, str]:
    ensure_clean_repo(repo)
    branch = f"agent/{_slug(task_name)}"
    root = repo / ".forgepilot" / "worktrees"
    worktree = root / _slug(task_name)
    if worktree.exists():
        raise PilotError(f"Le worktree existe déjà : {worktree}")
    root.mkdir(parents=True, exist_ok=True)
    git(repo, "worktree", "add", "-b", branch, str(worktree), base)
    return worktree, branch


def existing_worktree(repo: Path, task_name: str) -> tuple[Path, str, str]:
    slug = _slug(task_name)
    worktree = repo / ".forgepilot" / "worktrees" / slug
    expected_branch = f"agent/{slug}"
    if not worktree.exists():
        raise PilotError(
            f"Worktree introuvable : {worktree}. "
            "Employer `execute` pour créer la branche et le worktree."
        )
    current = git(worktree, "branch", "--show-current")
    if current != expected_branch:
        raise PilotError(
            f"Branche du worktree {current!r} ; attendu {expected_branch!r}."
        )
    status = git(worktree, "status", "--porcelain")
    return worktree, expected_branch, status


def _stream_argv(invocation: Invocation) -> tuple[str, ...]:
    argv = list(invocation.argv)
    if "--output-format" in argv:
        index = argv.index("--output-format") + 1
        if index < len(argv):
            argv[index] = "stream-json"
    if (
        invocation.backend == "claude"
        and invocation.role in {"planner", "reviewer", "witness"}
        and "--verbose" not in argv
    ):
        argv.append("--verbose")
    return tuple(argv)


def execute_invocation(
    invocation: Invocation,
    settings: Settings,
    *,
    stdin: str | None = None,
    timeout_seconds: int | None = None,
    stream: bool = False,
    on_event: Callable[[object], None] | None = None,
) -> object:
    resolve_binary(invocation.argv[0])
    runner = run_command_stream if stream else run_command
    kwargs: dict[str, object] = {
        "cwd": Path(invocation.cwd),
        "timeout_seconds": timeout_seconds or settings.timeout_seconds,
        "env": invocation.environment,
        "stdin": (
            stdin
            if stdin is not None
            else (None if invocation.backend == "cursor" else invocation.prompt)
        ),
        "remove_env": tuple(
            name for name in os.environ if CONTROLLER_SECRET_ENV.search(name)
        ),
    }
    if stream:
        captured_session: list[str] = []

        def observe(event: object) -> None:
            session = extract_session_id(event)
            if session:
                captured_session[:] = [session]
            if on_event is not None:
                on_event(event)

        kwargs["on_event"] = observe
    result = runner(_stream_argv(invocation) if stream else invocation.argv, **kwargs)
    payload = result.json()
    if stream and captured_session and extract_session_id(payload) is None:
        if isinstance(payload, dict):
            payload = dict(payload)
            payload["session_id"] = captured_session[-1]
        else:
            payload = {"result": payload, "session_id": captured_session[-1]}
    return payload


def persist_result(repo: Path, role: str, invocation: Invocation, result: object) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_dir = repo / ".forgepilot" / "runs" / f"{stamp}-{role}"
    run_dir.mkdir(parents=True, exist_ok=False)
    payload = {
        "created_at": datetime.now(timezone.utc).isoformat(),
        "role": role,
        "invocation": json.loads(format_invocation(invocation)),
        "result": result,
    }
    target = run_dir / "result.json"
    return write_normalized_json(target, payload)


def format_invocation(invocation: Invocation) -> str:
    redacted = list(invocation.argv)
    if "-p" in redacted:
        prompt_index = redacted.index("-p") + 1
        if prompt_index < len(redacted) and not redacted[prompt_index].startswith("--"):
            redacted[prompt_index] = "<prompt>"
    payload: dict[str, object] = {
        "role": invocation.role,
        "argv": redacted,
        "cwd": invocation.cwd,
        "environment": {
            key: ("<secret>" if re.search(r"(?:key|token|secret|password|authorization)", key, re.I) else value)
            for key, value in invocation.environment.items()
        },
        "model": invocation.model,
        "effort": invocation.effort,
    }
    if invocation.prompt is not None:
        payload["prompt"] = "<prompt>"
    return json.dumps(payload, indent=2, ensure_ascii=False)


def missing_binaries(settings: Settings) -> Iterable[str]:
    for name in ("git", "gh", settings.claude_binary, settings.cursor_binary):
        try:
            resolve_binary(name)
        except PilotError:
            yield name


def publish_preview(
    repo: Path,
    title: str,
    base_branch: str,
    *,
    risk: str = "R1",
    brief: str | None = None,
) -> Invocation:
    branch = git(repo, "branch", "--show-current")
    if not branch.startswith("agent/"):
        raise PilotError(f"Publication refusée depuis la branche {branch!r} ; préfixe agent/ requis.")
    if not git(repo, "status", "--porcelain"):
        raise PilotError("Aucun changement à publier.")
    body = (
        "Produit par Cursor dans ForgePilot. "
        "Fusion mécanique si juge PASS et checks verts (ADR-0017).\n\n"
        f"Forge-Risk: {risk}\n"
        f"Forge-Brief: {brief or title}"
    )
    argv = (
        "gh",
        "pr",
        "create",
        "--draft",
        "--base",
        base_branch,
        "--head",
        branch,
        "--title",
        title,
        "--body",
        body,
    )
    return Invocation("publisher", argv, str(repo), {})


def publish(
    repo: Path,
    title: str,
    base_branch: str,
    *,
    allowed_paths: Iterable[str] | None = None,
    risk: str = "R1",
    brief: str | None = None,
) -> str:
    invocation = publish_preview(repo, title, base_branch, risk=risk, brief=brief)
    resolve_binary("gh")
    if allowed_paths is None:
        raise PilotError(
            "Publication refusée : fournir le plan et files_allowed_to_change."
        )
    paths = enforce_allowed_paths(working_tree_paths(repo), allowed_paths)
    git(repo, "diff", "--check")
    stage_explicit_paths(repo, paths)
    git(repo, "diff", "--cached", "--check")
    git(repo, "commit", "-m", title)
    branch = git(repo, "branch", "--show-current")
    git(repo, "push", "-u", "origin", branch, timeout_seconds=300)
    result = run_command(
        invocation.argv,
        cwd=repo,
        timeout_seconds=120,
    )
    return result.stdout.strip()


def chain_preview(
    settings: Settings,
    repo: Path,
    task: Path,
    task_name: str,
    *,
    model: str | None = None,
    effort: str | None = None,
    requested_risk: str = "R1",
    changed_paths: Iterable[str] = (),
) -> dict[str, object]:
    """Aperçu du lot complet. Aucun agent, aucune fusion."""
    assert_task_is_instruction(task)
    _task_text(task)
    if effort:
        assert_valid_effort(effort)
    slug = _slug(task_name)
    risk = requested_risk
    derived = "R1"
    if settings.policy is not None:
        risk, derived = effective_risk(settings.policy, requested_risk, changed_paths)
        if risk == "R0":
            return {
                "command": "enchaine",
                "run": False,
                "fusion": False,
                "task_name": slug,
                "risk": {"requested": requested_risk, "derived": derived, "effective": risk},
                "policy": settings.policy.summary(),
                "steps": ["mechanical-only"],
                "note": "R0 ne lance aucun agent ; contrôles mécaniques dédiés uniquement.",
            }
    plan_inv = plan_invocation(
        settings, repo, task, model=model, effort=effort, risk=risk
    )
    preview_worktree = repo / ".forgepilot" / "worktrees" / slug
    exec_inv = executor_invocation(
        settings, preview_worktree, task, model=model, risk=risk
    )
    return {
        "command": "enchaine",
        "run": False,
        "fusion": False,
        "task_name": slug,
        "risk": {"requested": requested_risk, "derived": derived, "effective": risk},
        "policy": settings.policy.summary() if settings.policy is not None else None,
        "steps": list(CHAIN_STEPS),
        "plan": json.loads(format_invocation(plan_inv)),
        "execute": json.loads(format_invocation(exec_inv)),
        "publish": {
            "role": "publisher",
            "after": "worktree",
            "title": slug,
            "draft": True,
            "note": "Produit par Cursor dans ForgePilot. Fusion mécanique si juge PASS et checks verts (ADR-0017).",
        },
        "review": {
            "role": "reviewer",
            "after": "worktree",
            "base": settings.default_base_ref,
        },
    }


def run_chain(
    settings: Settings,
    repo: Path,
    task: Path,
    task_name: str,
    *,
    base_ref: str,
    base_branch: str,
    title: str,
    model: str | None = None,
    effort: str | None = None,
    requested_risk: str = "R1",
    changed_paths: Iterable[str] = (),
) -> dict[str, object]:
    """plan → execute → publish (draft) → review. Jamais de fusion."""
    raise PilotError(
        "run_chain mutateur désactivé : employer register_run/resume_run pour "
        "conserver l'état, les verrous et les preuves exactes."
    )

    # Corps historique conservé temporairement pour compatibilité de lecture ;
    # il est intentionnellement inaccessible depuis le CLI et cette API.
    assert_not_api_billing()
    assert_task_is_instruction(task)
    _task_text(task)
    if effort:
        assert_valid_effort(effort)
    missing = list(missing_binaries(settings))
    if missing:
        raise PilotError("Binaires manquants : " + ", ".join(missing))

    slug = _slug(task_name)
    pr_title = title.strip() or slug

    risk = requested_risk
    if settings.policy is not None:
        risk, _ = effective_risk(settings.policy, requested_risk, changed_paths)
    plan_inv = plan_invocation(
        settings, repo, task, model=model, effort=effort, risk=risk
    )
    plan_result = execute_invocation(plan_inv, settings)
    plan_payload: dict[str, object] | None = None
    try:
        plan_payload = validate_plan(plan_result)
    except PilotError:
        # Compatibilité pour les tests/anciens adaptateurs qui ne simulent que
        # le rôle. Une vraie publication reste fermée sans périmètre explicite.
        if not (isinstance(plan_result, dict) and plan_result.get("role") == "planner"):
            raise
    if plan_payload is not None and plan_payload["blocked"]:
        raise PilotError("Plan bloqué : Cursor ne sera pas lancé.")
    if plan_payload is not None and settings.policy is not None:
        risk, _ = effective_risk(
            settings.policy,
            risk,
            plan_payload["files_allowed_to_change"],
        )
    plan_path = persist_result(repo, "planner", plan_inv, plan_result)

    worktree, branch = create_worktree(repo, slug, base_ref)
    exec_inv = executor_invocation(
        settings, worktree, plan_path, model=model, risk=risk
    )
    exec_result = execute_invocation(exec_inv, settings)
    exec_path = persist_result(repo, "executor", exec_inv, exec_result)

    pull_request = publish(
        worktree,
        pr_title,
        base_branch,
        allowed_paths=(plan_payload or {}).get("files_allowed_to_change"),
        risk=risk,
        brief=slug,
    )

    review_inv = review_invocation(
        settings,
        worktree,
        plan_path,
        base_ref,
        model=model,
        effort=effort,
        risk=risk,
    )
    review_result = execute_invocation(review_inv, settings)
    review_path = persist_result(repo, "reviewer", review_inv, review_result)

    return {
        "command": "enchaine",
        "run": True,
        "fusion": False,
        "task_name": slug,
        "steps": list(CHAIN_STEPS),
        "branch": branch,
        "worktree": str(worktree),
        "plan": str(plan_path),
        "execute": str(exec_path),
        "pull_request": pull_request,
        "review": str(review_path),
    }
