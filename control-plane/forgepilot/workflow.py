from __future__ import annotations

from dataclasses import asdict, dataclass, replace
from datetime import datetime, timezone
import json
from pathlib import Path
import re
from typing import Iterable

from .config import CURSOR_EFFORT_REFUSED, RoleSettings, Settings
from .process import PilotError, git, resolve_binary, run_command


READ_ONLY_CLAUDE_TOOLS = "Read,Glob,Grep"


@dataclass(frozen=True)
class Invocation:
    role: str
    argv: tuple[str, ...]
    cwd: str
    environment: dict[str, str]
    prompt: str | None = None
    model: str | None = None
    effort: str | None = None


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


def resolve_role(
    settings: Settings,
    role: str,
    model: str | None = None,
    effort: str | None = None,
) -> RoleSettings:
    """Priorité D3 : drapeau > [roles.*] > [tools] > défaut du binaire."""
    role_cfg = settings.roles.get(role, RoleSettings())
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

    return RoleSettings(model=resolved_model or "", effort=resolved_effort or "")


def _claude_argv(
    settings: Settings,
    role: str,
    model: str | None = None,
    effort: str | None = None,
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
    resolved = resolve_role(settings, role, model=model, effort=effort)
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
) -> Invocation:
    task_body = _task_text(task)
    prompt = _read_prompt("planner.md").replace("{{TASK}}", task_body)
    resolved = resolve_role(settings, "planner", model=model, effort=effort)
    argv = _claude_argv(settings, "planner", model=model, effort=effort)
    return Invocation(
        "planner",
        tuple(argv),
        str(repo),
        {},
        prompt,
        model=resolved.model or None,
        effort=resolved.effort or None,
    )


def review_invocation(
    settings: Settings,
    repo: Path,
    plan: Path,
    base: str,
    *,
    model: str | None = None,
    effort: str | None = None,
) -> Invocation:
    plan_body = _task_text(plan)
    diff = git(repo, "diff", "--no-ext-diff", f"{base}...HEAD")
    if not diff:
        raise PilotError(f"Aucun diff à relire contre {base}.")
    prompt = (
        _read_prompt("reviewer.md")
        .replace("{{PLAN}}", plan_body)
        .replace("{{BASE}}", base)
        .replace("{{DIFF}}", diff)
    )
    resolved = resolve_role(settings, "reviewer", model=model, effort=effort)
    argv = _claude_argv(settings, "reviewer", model=model, effort=effort)
    return Invocation(
        "reviewer",
        tuple(argv),
        str(repo),
        {},
        prompt,
        model=resolved.model or None,
        effort=resolved.effort or None,
    )


def executor_invocation(
    settings: Settings,
    worktree: Path,
    plan: Path,
    *,
    model: str | None = None,
    effort: str | None = None,
) -> Invocation:
    if effort:
        raise PilotError(CURSOR_EFFORT_REFUSED)
    plan_body = _task_text(plan)
    prompt = _read_prompt("executor.md").replace("{{PLAN}}", plan_body)
    resolved = resolve_role(settings, "executor", model=model, effort=None)
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
    return Invocation(
        "executor",
        tuple(argv),
        str(worktree),
        {},
        model=resolved.model or None,
        effort=None,
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


def execute_invocation(invocation: Invocation, settings: Settings, *, stdin: str | None = None) -> object:
    resolve_binary(invocation.argv[0])
    result = run_command(
        invocation.argv,
        cwd=Path(invocation.cwd),
        timeout_seconds=settings.timeout_seconds,
        env=invocation.environment,
        stdin=stdin if stdin is not None else invocation.prompt,
    )
    return result.json()


def persist_result(repo: Path, role: str, invocation: Invocation, result: object) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_dir = repo / ".forgepilot" / "runs" / f"{stamp}-{role}"
    run_dir.mkdir(parents=True, exist_ok=False)
    stored = (
        replace(invocation, prompt="<prompt>")
        if invocation.prompt is not None
        else invocation
    )
    payload = {
        "created_at": datetime.now(timezone.utc).isoformat(),
        "role": role,
        "invocation": asdict(stored),
        "result": result,
    }
    target = run_dir / "result.json"
    target.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return target


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
        "environment": invocation.environment,
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


def publish_preview(repo: Path, title: str, base_branch: str) -> Invocation:
    branch = git(repo, "branch", "--show-current")
    if not branch.startswith("agent/"):
        raise PilotError(f"Publication refusée depuis la branche {branch!r} ; préfixe agent/ requis.")
    if not git(repo, "status", "--porcelain"):
        raise PilotError("Aucun changement à publier.")
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
        "Produit par Cursor dans ForgePilot. Fusion humaine obligatoire.",
    )
    return Invocation("publisher", argv, str(repo), {})


def publish(repo: Path, title: str, base_branch: str) -> str:
    invocation = publish_preview(repo, title, base_branch)
    resolve_binary("gh")
    git(repo, "diff", "--check")
    git(repo, "add", "-A")
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
