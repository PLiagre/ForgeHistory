from __future__ import annotations

from dataclasses import asdict, dataclass, replace
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
from typing import Iterable

from .config import (
    CURSOR_EFFORT_REFUSED,
    RoleSettings,
    Settings,
    assert_valid_effort,
)
from .process import PilotError, git, resolve_binary, run_command


READ_ONLY_CLAUDE_TOOLS = "Read,Glob,Grep"
CHAIN_STEPS = ("plan", "execute", "publish", "review")
LOT_BRIEF_STEPS = ("brief", "plan", "execute", "publish", "review")
PROPOSITION_REFUSED = (
    "Une proposition Hermes n'est pas une instruction. "
    "Passer un brief (harness/queue/briefs/.../brief.md) ou un fichier de tâche."
)
API_KEY_REFUSED = (
    "ANTHROPIC_API_KEY est défini ; le pilote doit utiliser l'abonnement Claude Pro."
)


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
    if is_proposition(task):
        raise PilotError(PROPOSITION_REFUSED)


def is_proposition(task: Path) -> bool:
    parts = [part.lower() for part in task.parts]
    if "hermes" in parts and "propositions" in parts:
        return True
    return task.name.upper().startswith("PROPOSITION-")


def next_brief_number(repo: Path) -> str:
    root = repo / "harness" / "queue" / "briefs"
    numbers: list[int] = []
    if root.is_dir():
        for entry in root.iterdir():
            if entry.is_dir() and len(entry.name) >= 3 and entry.name[:3].isdigit():
                numbers.append(int(entry.name[:3]))
    return f"{max(numbers, default=0) + 1:03d}"


def _coerce_json_dict(value: object) -> dict:
    if isinstance(value, dict):
        inner = value.get("result")
        if isinstance(inner, (dict, str)) and "brief_md" not in value:
            return _coerce_json_dict(inner)
        return value
    if isinstance(value, str):
        text = value.strip()
        if text.startswith("```"):
            lines = text.splitlines()
            if lines and lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip() == "```":
                lines = lines[:-1]
            text = "\n".join(lines).strip()
        try:
            parsed = json.loads(text)
        except json.JSONDecodeError as exc:
            raise PilotError("Le planificateur n'a pas renvoyé un JSON lisible.") from exc
        return _coerce_json_dict(parsed)
    raise PilotError("Le planificateur n'a pas renvoyé un brief JSON.")


def extract_brief_payload(result: object) -> dict[str, str]:
    obj = _coerce_json_dict(result)
    if obj.get("blocked") is True:
        raise PilotError(
            "Le planificateur a bloqué le brief : "
            + str(obj.get("reason") or "raison absente")
        )
    brief_md = obj.get("brief_md")
    rubric = obj.get("eval_rubric_md")
    slug = obj.get("slug")
    if not isinstance(brief_md, str) or not brief_md.strip():
        raise PilotError("Le planificateur n'a pas fourni brief_md.")
    if not isinstance(rubric, str) or not rubric.strip():
        raise PilotError("Le planificateur n'a pas fourni eval_rubric_md.")
    if not isinstance(slug, str) or not _slug(slug):
        raise PilotError("Le planificateur n'a pas fourni un slug utilisable.")
    return {
        "slug": _slug(slug),
        "title": str(obj.get("title") or slug),
        "brief_md": brief_md,
        "eval_rubric_md": rubric,
    }


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
    assert_valid_effort(resolved_effort or "")

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


def brief_invocation(
    settings: Settings,
    repo: Path,
    source: Path,
    number: str,
    *,
    model: str | None = None,
    effort: str | None = None,
) -> Invocation:
    """Claude rédige le brief ; il n'écrit aucun fichier (lecture seule)."""
    task_body = _task_text(source)
    prompt = (
        _read_prompt("brief.md")
        .replace("{{TASK}}", task_body)
        .replace("{{NUMBER}}", number)
    )
    resolved = resolve_role(settings, "planner", model=model, effort=effort)
    argv = _claude_argv(settings, "planner", model=model, effort=effort)
    return Invocation(
        "brief",
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


def chain_preview(
    settings: Settings,
    repo: Path,
    task: Path,
    task_name: str,
    *,
    model: str | None = None,
    effort: str | None = None,
) -> dict[str, object]:
    """Aperçu du lot complet. Aucun agent, aucune fusion."""
    assert_task_is_instruction(task)
    _task_text(task)
    if effort:
        assert_valid_effort(effort)
    slug = _slug(task_name)
    plan_inv = plan_invocation(
        settings, repo, task, model=model, effort=effort
    )
    preview_worktree = repo / ".forgepilot" / "worktrees" / slug
    exec_inv = executor_invocation(
        settings, preview_worktree, task, model=model
    )
    return {
        "command": "enchaine",
        "run": False,
        "fusion": False,
        "task_name": slug,
        "steps": list(CHAIN_STEPS),
        "plan": json.loads(format_invocation(plan_inv)),
        "execute": json.loads(format_invocation(exec_inv)),
        "publish": {
            "role": "publisher",
            "after": "worktree",
            "title": slug,
            "draft": True,
            "note": "Produit par Cursor dans ForgePilot. Fusion humaine obligatoire.",
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
) -> dict[str, object]:
    """plan → execute → publish (draft) → review. Jamais de fusion."""
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

    plan_inv = plan_invocation(
        settings, repo, task, model=model, effort=effort
    )
    plan_result = execute_invocation(plan_inv, settings)
    plan_path = persist_result(repo, "planner", plan_inv, plan_result)

    worktree, branch = create_worktree(repo, slug, base_ref)
    exec_inv = executor_invocation(
        settings, worktree, plan_path, model=model
    )
    exec_result = execute_invocation(exec_inv, settings)
    exec_path = persist_result(repo, "executor", exec_inv, exec_result)

    pull_request = publish(worktree, pr_title, base_branch)

    review_inv = review_invocation(
        settings,
        worktree,
        plan_path,
        base_ref,
        model=model,
        effort=effort,
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


def write_brief_in_worktree(worktree: Path, number: str, payload: dict[str, str]) -> Path:
    """Écrit brief + rubrique dans le worktree. Claude n'a rien écrit sur disque."""
    dirname = f"{number}-{payload['slug']}"
    target = worktree / "harness" / "queue" / "briefs" / dirname
    if target.exists():
        raise PilotError(f"Le dossier de brief existe déjà : {target}")
    target.mkdir(parents=True, exist_ok=False)
    (target / "brief.md").write_text(payload["brief_md"].rstrip() + "\n", encoding="utf-8")
    (target / "eval-rubric.md").write_text(
        payload["eval_rubric_md"].rstrip() + "\n", encoding="utf-8"
    )
    git(worktree, "add", str(target.relative_to(worktree)))
    git(worktree, "commit", "-m", f"planificateur: brief {dirname}")
    return target / "brief.md"


def lot_preview(
    settings: Settings,
    repo: Path,
    source: Path,
    task_name: str | None,
    *,
    model: str | None = None,
    effort: str | None = None,
) -> dict[str, object]:
    """Aperçu : brief Claude si proposition, puis enchaine. Aucun agent."""
    _task_text(source)
    if effort:
        assert_valid_effort(effort)
    needs_brief = is_proposition(source)
    if needs_brief:
        number = next_brief_number(repo)
        brief_inv = brief_invocation(
            settings, repo, source, number, model=model, effort=effort
        )
        slug = _slug(task_name or f"{number}-lot")
        return {
            "command": "lot",
            "run": False,
            "fusion": False,
            "needs_brief": True,
            "brief_number": number,
            "task_name": slug,
            "steps": list(LOT_BRIEF_STEPS),
            "brief": json.loads(format_invocation(brief_inv)),
            "note": (
                "Claude rédige le brief (lecture seule). "
                "ForgePilot l'écrit dans le worktree, puis enchaîne. "
                "Pas de fusion."
            ),
        }
    name = task_name or default_task_name(source)
    payload = chain_preview(
        settings, repo, source, name, model=model, effort=effort
    )
    payload["command"] = "lot"
    payload["needs_brief"] = False
    return payload


def run_lot(
    settings: Settings,
    repo: Path,
    source: Path,
    task_name: str | None,
    *,
    base_ref: str,
    base_branch: str,
    title: str | None,
    model: str | None = None,
    effort: str | None = None,
) -> dict[str, object]:
    """Si proposition : Claude écrit le brief, puis plan→execute→draft PR→review."""
    if not is_proposition(source):
        name = task_name or default_task_name(source)
        result = run_chain(
            settings,
            repo,
            source,
            name,
            base_ref=base_ref,
            base_branch=base_branch,
            title=title or name,
            model=model,
            effort=effort,
        )
        result["command"] = "lot"
        result["needs_brief"] = False
        return result

    assert_not_api_billing()
    _task_text(source)
    if effort:
        assert_valid_effort(effort)
    missing = list(missing_binaries(settings))
    if missing:
        raise PilotError("Binaires manquants : " + ", ".join(missing))

    number = next_brief_number(repo)
    brief_inv = brief_invocation(
        settings, repo, source, number, model=model, effort=effort
    )
    brief_raw = execute_invocation(brief_inv, settings)
    persist_result(repo, "brief", brief_inv, brief_raw)
    payload = extract_brief_payload(brief_raw)

    slug = _slug(task_name or f"{number}-{payload['slug']}")
    worktree, branch = create_worktree(repo, slug, base_ref)
    brief_path = write_brief_in_worktree(worktree, number, payload)

    plan_inv = plan_invocation(
        settings, repo, brief_path, model=model, effort=effort
    )
    plan_result = execute_invocation(plan_inv, settings)
    plan_path = persist_result(repo, "planner", plan_inv, plan_result)

    exec_inv = executor_invocation(
        settings, worktree, plan_path, model=model
    )
    exec_result = execute_invocation(exec_inv, settings)
    exec_path = persist_result(repo, "executor", exec_inv, exec_result)

    pr_title = (title or payload["title"] or slug).strip()
    pull_request = publish(worktree, pr_title, base_branch)

    review_inv = review_invocation(
        settings,
        worktree,
        plan_path,
        base_ref,
        model=model,
        effort=effort,
    )
    review_result = execute_invocation(review_inv, settings)
    review_path = persist_result(repo, "reviewer", review_inv, review_result)

    return {
        "command": "lot",
        "run": True,
        "fusion": False,
        "needs_brief": True,
        "brief": str(brief_path),
        "task_name": slug,
        "steps": list(LOT_BRIEF_STEPS),
        "branch": branch,
        "worktree": str(worktree),
        "plan": str(plan_path),
        "execute": str(exec_path),
        "pull_request": pull_request,
        "review": str(review_path),
    }
