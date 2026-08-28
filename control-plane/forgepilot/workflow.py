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
from .exchange import stage_exchange
from .policy import GROK_EFFORTS, effective_risk
from .process import (
    PilotError,
    ReviewProtocolError,
    git,
    resolve_binary,
    run_command,
    run_command_stream,
)
from .protocol import (
    BRIEF_REVIEW_JSON_SCHEMA,
    BRIEF_REVIEW_SCHEMA_RETRY_HINT,
    REVIEW_JSON_SCHEMA,
    REVIEW_SCHEMA_RETRY_HINT,
    extract_session_id,
    validate_plan,
    write_normalized_json,
)
from .publication import enforce_allowed_paths, stage_explicit_paths, working_tree_paths


CHAIN_STEPS = ("plan", "execute", "publish", "review")
PROPOSITION_REFUSED = (
    "Une proposition Hermes n'est pas une instruction. "
    "Passer un brief (harness/queue/briefs/.../brief.md) ou un fichier de tâche."
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


def _role_backend(
    settings: Settings,
    risk: str | None,
    role: str,
) -> str:
    if settings.policy is not None and risk is None:
        # Une politique chargée et aucun risque ne doit jamais contourner
        # l'affectation explicite des rôles. Refuser est la seule réponse
        # honnête : le risque se déclare (`--risk`) ou se dérive du brief.
        raise PilotError(
            f"Aucun risque déclaré pour le rôle {role} alors qu'une politique "
            f"est chargée ({settings.policy.path}). Passer --risk R0|R1|R2, "
            "ou déclarer `Risque : R…` dans le brief."
        )
    if risk is None or settings.policy is None:
        return "cursor"
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
    add_dir: Path | None = None,
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
    if add_dir is not None:
        argv.extend(["--add-dir", str(add_dir)])
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
    resolved = resolve_role(settings, "planner", model=model, effort=effort, risk=risk)
    if backend == "cursor":
        try:
            task_reference = task.resolve().relative_to(repo.resolve()).as_posix()
        except ValueError as exc:
            raise PilotError(
                "Le brief Cursor doit vivre dans le dépôt pour être lu par chemin."
            ) from exc
        authoritative_task = (
            f"Lis intégralement `{task_reference}` dans le dépôt. Ce fichier est "
            "l'unique tâche autoritaire : ne le résume pas avant de construire le plan."
        )
        prompt = _read_prompt("planner.md").replace("{{TASK}}", authoritative_task)
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        argv = _cursor_read_argv(
            settings, repo, prompt, mode="ask", model=cursor_model
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
    raise PilotError(f"Backend de planification automatique interdit : {backend!r}.")


def brief_review_invocation(
    settings: Settings,
    repo: Path,
    brief: Path,
    *,
    model: str | None = None,
    effort: str | None = None,
    risk: str | None = None,
    schema_retry: bool = False,
) -> Invocation:
    """
    Fait relire le BRIEF avant qu'un exécutant démarre.

    Le relecteur de PR arrive après que l'exécutant a travaillé — jusqu'à
    deux heures au profil R2. Un brief qui contient deux lots, un critère
    invérifiable ou une demande de modifier un test existant coûte alors un
    aller-retour complet. Relu d'abord, il coûte le budget du relecteur.

    Même backend et même effort que le relecteur de PR : c'est le même
    travail de lecture adverse, sur un objet plus petit. Le brief est passé
    par RÉFÉRENCE, jamais recopié dans la ligne de commande.

    Ce n'est pas un jugement de lot : personne n'a encore produit quoi que ce
    soit. La règle « celui qui produit ne prononce pas la recevabilité de son
    propre travail » est respectée : l'auteur manuel du brief n'est jamais
    son relecteur automatique.
    """
    backend = _role_backend(settings, risk, "reviewer")
    if backend == "none":
        raise PilotError(
            "Aucun relecteur n'est configuré pour ce risque : un lot R0 ne "
            "mobilise aucun agent, la relecture de brief n'a pas lieu d'être."
        )
    resolved = resolve_role(settings, "reviewer", model=model, effort=effort, risk=risk)

    if backend == "cursor":
        try:
            reference = brief.resolve().relative_to(repo.resolve()).as_posix()
        except ValueError as exc:
            raise PilotError(
                "Le brief doit vivre dans le dépôt pour être lu par chemin."
            ) from exc
        corps = (
            f"Lis intégralement `{reference}` dans le dépôt. Ce fichier est "
            "l'unique brief à relire ; ne le résume pas avant de juger."
        )
        prompt = _read_prompt("brief-reviewer.md").replace("{{BRIEF}}", corps)
        prompt = (
            f"{prompt.rstrip()}\n\n## Schéma JSON fermé\n\n"
            f"{json.dumps(BRIEF_REVIEW_JSON_SCHEMA, ensure_ascii=False, sort_keys=True)}\n"
        )
        if schema_retry:
            prompt = f"{prompt.rstrip()}\n\n{BRIEF_REVIEW_SCHEMA_RETRY_HINT}\n"
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        argv = _cursor_read_argv(settings, repo, prompt, mode="ask", model=cursor_model)
        return Invocation(
            "brief-reviewer",
            tuple(argv),
            str(repo),
            {},
            prompt,
            model=cursor_model or None,
            effort=resolved.effort or None,
            backend="cursor",
        )

    raise PilotError(f"Backend de relecture automatique interdit : {backend!r}.")


def _stage_review_schema(repo: Path, *, near: Path | None = None) -> str:
    """Dépose le schéma dans le canal d'échange de la PR #138, pas un second tuyau."""

    source = (near.parent if near is not None else repo / ".forgepilot") / "review-schema.json"
    source.parent.mkdir(parents=True, exist_ok=True)
    source.write_text(
        json.dumps(REVIEW_JSON_SCHEMA, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    return stage_exchange(repo, source, "review-schema")


def _review_prompt(
    repo: Path,
    bundle_body_or_reference: str,
    *,
    schema_near: Path | None = None,
    schema_retry: bool = False,
) -> str:
    schema_path = _stage_review_schema(repo, near=schema_near)
    prompt = (
        _read_prompt("reviewer.md")
        .replace("{{REVIEW_BUNDLE}}", bundle_body_or_reference)
        .replace("{{REVIEW_SCHEMA}}", schema_path)
    )
    if schema_retry:
        prompt = f"{prompt.rstrip()}\n\n{REVIEW_SCHEMA_RETRY_HINT}\n"
    return prompt


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
    schema_retry: bool = False,
) -> Invocation:
    backend = _role_backend(settings, risk, "reviewer")
    if backend == "none":
        raise PilotError("Aucun relecteur n'est configuré pour ce risque.")
    resolved = resolve_role(settings, "reviewer", model=model, effort=effort, risk=risk)
    argv: list[str]
    schema_near = bundle_path if bundle_path is not None else None
    if backend == "cursor" and bundle_path is not None:
        if not bundle_path.is_file():
            raise PilotError(f"Bundle de revue introuvable : {bundle_path}")
        # Le bundle vit à côté de `state.json`, dans le dossier du run. L'y
        # laisser imposait un `--add-dir` sur ce dossier : le relecteur voyait
        # alors l'état interne du lot — verdicts antérieurs, compteur
        # d'itérations, conclusions du producteur — que le bundle exclut
        # justement (`producer_conclusions_included: false`). Une copie dans
        # le canal d'échange lui donne son matériel, et rien d'autre.
        bundle_reference_path = stage_exchange(repo, bundle_path, "review-bundle")
        bundle_reference = (
            f"Lis intégralement le bundle de revue `{bundle_reference_path}` "
            "dans ton espace de travail. Ce fichier est l'unique bundle "
            "autoritaire ; rends ensuite le JSON de revue fermé. Si ce fichier "
            "est illisible, rends `verdict` `BLOCKED` avec "
            "`blocked_reason` `material_unreadable` : une panne de transport "
            "n'est pas un jugement sur le produit."
        )
        prompt = _review_prompt(
            repo,
            bundle_reference,
            schema_near=schema_near,
            schema_retry=schema_retry,
        )
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        # Sandbox activé : le relecteur lit le bundle depuis forge-exchange/
        # dans le workspace, au lieu de le recevoir dans le prompt. Sans
        # sandbox, l'invocation portable est limitée à 30K unités UTF-16,
        # ce qu'un plan détaillé (acceptance_criteria + checks + scope)
        # dépasse régulièrement.
        argv = [
            settings.cursor_binary,
            "-p",
            prompt,
            "--force",
            "--sandbox",
            "enabled",
            "--trust",
            "--workspace",
            str(repo),
            "--output-format",
            "json",
        ]
        if cursor_model:
            argv.extend(["--model", cursor_model])
    else:
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
        prompt = _review_prompt(
            repo,
            bundle_body,
            schema_near=schema_near,
            schema_retry=schema_retry,
        )
    if backend == "cursor":
        cursor_model = grok_model_for_effort(resolved.model, resolved.effort)
        if bundle_path is None:
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
    raise PilotError(f"Backend de revue automatique interdit : {backend!r}.")


def _stage_reference(worktree: Path, source: Path, nom: str) -> str:
    """
    Dépose une copie du corps dans le canal d'échange DU worktree et rend son
    chemin relatif, pour que le prompt le cite au lieu de le recopier.

    Pourquoi dans le worktree, et pas un `--add-dir` sur le dossier du run :
    l'exécutant tourne avec `--force`, et lui ouvrir le dossier du run lui
    donnerait accès à `state.json`. Ici il ne voit qu'une copie. Le relecteur
    passe désormais par la même porte, pour la même raison.

    Pourquoi `.forge-exchange/` et plus `.forgepilot/` : les deux sont
    git-ignorés, donc invisibles à `working_tree_paths()` — mais `.forgepilot/`
    est aussi cursor-ignoré, ce qui rendait la copie illisible à celui à qui
    on la tendait. Voir `exchange.py` et `tests/test_exchange_channel.py`.
    """
    return stage_exchange(worktree, source, nom)


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
    # Le plan et le feedback arrivent par RÉFÉRENCE, jamais recopiés dans le
    # prompt — donc jamais dans argv. Cursor n'accepte le prompt que par `-p`,
    # et Windows borne la ligne complète à 32 767 unités UTF-16 : tant que les
    # corps étaient inline, la taille du lot décidait de sa faisabilité.
    # Le planificateur et le relecteur passaient déjà par une référence ;
    # l'exécutant était le seul à ne pas l'avoir.
    plan_reference = _stage_reference(worktree, plan, "plan")
    if feedback is not None:
        feedback_reference = _stage_reference(worktree, feedback, "feedback")
        prompt = (
            _read_prompt("iterator.md")
            .replace(
                "{{PLAN}}",
                f"Lis intégralement `{plan_reference}` dans ton worktree. "
                "Ce fichier est l'unique plan autoritaire.",
            )
            .replace(
                "{{FEEDBACK}}",
                f"Lis intégralement `{feedback_reference}` dans ton worktree. "
                "Ce fichier est l'unique feedback de la revue indépendante.",
            )
        )
    else:
        prompt = _read_prompt("executor.md").replace(
            "{{PLAN}}",
            f"Lis intégralement `{plan_reference}` dans ton worktree. "
            "Ce fichier est l'unique plan autoritaire.",
        )
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
    corrective_branch = re.fullmatch(rf"{re.escape(expected_branch)}-fix-[1-9][0-9]*", current)
    if current != expected_branch and corrective_branch is None:
        raise PilotError(
            f"Branche du worktree {current!r} ; attendu {expected_branch!r} "
            "ou une branche corrective suffixée -fix-N."
        )
    status = git(worktree, "status", "--porcelain")
    return worktree, current, status


def _stream_argv(invocation: Invocation) -> tuple[str, ...]:
    argv = list(invocation.argv)
    if "--output-format" in argv:
        index = argv.index("--output-format") + 1
        if index < len(argv):
            argv[index] = "stream-json"
    return tuple(argv)


def persist_failure_trace(
    trace_dir: Path,
    role: str,
    invocation: Invocation,
    error: PilotError,
    *,
    head_sha: str | None = None,
) -> Path | None:
    """Archive la sortie brute d'une invocation refusée, prompt caviardé.

    Livré par la PR #138 pour les erreurs levées pendant `execute_invocation()`.
    Étendu ensuite aux refus de `validate_review()` / `validate_executor()` :
    l'invocation a réussi, le contrat JSON n'a pas tenu. Même dossier
    `traces/`, même caviardage, même message d'erreur stable. Ce n'est pas
    un second canal : le transport reste `.forge-exchange/`.

    Le prompt est remplacé par `<prompt>` : un fournisseur peut le recopier
    dans sa réponse, et le dépôt n'archive jamais un prompt.
    """
    raw = getattr(error, "raw", None)
    if not raw:
        return None
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    dossier = Path(trace_dir) / "traces"
    dossier.mkdir(parents=True, exist_ok=True)
    corps = str(raw)
    if invocation.prompt:
        corps = corps.replace(invocation.prompt, "<prompt>")
    cible = dossier / f"{stamp}-{role}-raw.txt"
    cible.write_text(corps, encoding="utf-8")
    envelope: dict[str, object] = {
        "role": role,
        "backend": invocation.backend,
        "model": invocation.model,
        "effort": invocation.effort,
        "error": str(error),
        "raw_chars": len(corps),
        "raw_path": cible.name,
        "invocation": json.loads(format_invocation(invocation)),
    }
    if head_sha:
        envelope["head_sha"] = head_sha
    write_normalized_json(
        dossier / f"{stamp}-{role}-envelope.json",
        envelope,
    )
    return cible
    raw = getattr(error, "raw", None)
    if not raw:
        return None
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    dossier = Path(trace_dir) / "traces"
    dossier.mkdir(parents=True, exist_ok=True)
    corps = str(raw)
    if invocation.prompt:
        corps = corps.replace(invocation.prompt, "<prompt>")
    cible = dossier / f"{stamp}-{role}-raw.txt"
    cible.write_text(corps, encoding="utf-8")
    write_normalized_json(
        dossier / f"{stamp}-{role}-envelope.json",
        {
            "role": role,
            "backend": invocation.backend,
            "model": invocation.model,
            "effort": invocation.effort,
            "error": str(error),
            "raw_chars": len(corps),
            "raw_path": cible.name,
            "invocation": json.loads(format_invocation(invocation)),
        },
    )
    return cible


USAGE_SCALARS = (bool, int, float, str)
USAGE_TEXT_MAX = 200


def _usage_scalars(source: object) -> dict[str, object]:
    """Ne retient d'une enveloppe que ce qui se compte ou s'identifie."""
    if not isinstance(source, dict):
        return {}
    retenu: dict[str, object] = {}
    for key, value in source.items():
        if isinstance(value, USAGE_SCALARS):
            if isinstance(value, str) and len(value) > USAGE_TEXT_MAX:
                continue
            retenu[str(key)] = value
        elif isinstance(value, dict):
            imbrique = {
                str(sous_cle): sous_valeur
                for sous_cle, sous_valeur in value.items()
                if isinstance(sous_valeur, USAGE_SCALARS)
                and not (isinstance(sous_valeur, str) and len(sous_valeur) > USAGE_TEXT_MAX)
            }
            if imbrique:
                retenu[str(key)] = imbrique
    return retenu


def persist_usage(
    usage_dir: Path,
    invocation: Invocation,
    envelope: object,
) -> Path | None:
    """Conserve la comptabilité d'une invocation agent, sans son produit.

    L'enveloppe `type: "result"` de Cursor porte la durée, l'identifiant de
    requête et ce que le fournisseur dit de sa consommation. Elle était
    dépouillée pour n'en garder que le JSON métier : le coût d'un lot n'était
    donc mesurable nulle part. Le rapport du 26 août 2026 n'a pu chiffrer
    l'incident 035 qu'a posteriori, et seulement parce que l'autre fournisseur
    tenait ses propres journaux ; ForgePilot n'en tenait aucun.

    `result` est retiré — `persist_result` l'archive déjà. Le reste est gardé
    par filtrage de forme, pas par liste de noms : ce que le CLI ajoutera
    demain sera conservé sans que ce code ait à le connaître. Une écriture
    ratée ne casse jamais l'étape : la mesure n'est pas le produit.
    """
    comptable = _usage_scalars(
        {key: value for key, value in envelope.items() if key != "result"}
        if isinstance(envelope, dict)
        else {}
    )
    if not comptable:
        return None
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    payload = {
        "created_at": datetime.now(timezone.utc).isoformat(),
        "role": invocation.role,
        "backend": invocation.backend,
        "model": invocation.model,
        "effort": invocation.effort,
        "envelope": comptable,
    }
    try:
        dossier = Path(usage_dir) / "usage"
        dossier.mkdir(parents=True, exist_ok=True)
        cible = dossier / f"{stamp}-{invocation.role}.json"
        cible.write_text(
            json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    except OSError:
        return None
    return cible


def usage_summary(run_dir: Path) -> dict[str, object]:
    """Ce que le lot a coûté jusqu'ici, par rôle, d'après les enveloppes gardées.

    Les compteurs sont ceux que le fournisseur a nommés : ForgePilot les
    additionne sans les interpréter. Un champ absent reste absent — un coût
    inconnu n'est pas un coût nul.
    """
    dossier = Path(run_dir) / "usage"
    invocations: list[dict[str, object]] = []
    for fichier in sorted(dossier.glob("*.json")) if dossier.is_dir() else ():
        try:
            invocations.append(json.loads(fichier.read_text(encoding="utf-8")))
        except (OSError, json.JSONDecodeError):
            continue
    par_role: dict[str, dict[str, object]] = {}
    for entree in invocations:
        role = str(entree.get("role", "?"))
        compte = par_role.setdefault(role, {"invocations": 0})
        compte["invocations"] = int(compte["invocations"]) + 1  # type: ignore[arg-type]
        enveloppe = entree.get("envelope")
        if not isinstance(enveloppe, dict):
            continue
        for cle, valeur in enveloppe.items():
            if isinstance(valeur, bool) or not isinstance(valeur, (int, float)):
                continue
            compte[cle] = (compte.get(cle) or 0) + valeur  # type: ignore[operator]
        usage = enveloppe.get("usage")
        if isinstance(usage, dict):
            for cle, valeur in usage.items():
                if isinstance(valeur, bool) or not isinstance(valeur, (int, float)):
                    continue
                compte[f"usage.{cle}"] = (compte.get(f"usage.{cle}") or 0) + valeur  # type: ignore[operator]
    return {"invocations": len(invocations), "par_role": par_role}


def execute_invocation(
    invocation: Invocation,
    settings: Settings,
    *,
    stdin: str | None = None,
    timeout_seconds: int | None = None,
    stream: bool = False,
    on_event: Callable[[object], None] | None = None,
    trace_dir: Path | None = None,
    usage_dir: Path | None = None,
) -> object:
    if trace_dir is not None:
        try:
            return execute_invocation(
                invocation,
                settings,
                stdin=stdin,
                timeout_seconds=timeout_seconds,
                stream=stream,
                on_event=on_event,
                usage_dir=usage_dir,
            )
        except PilotError as exc:
            trace = persist_failure_trace(trace_dir, invocation.role, invocation, exc)
            if trace is not None:
                # Le message reste STABLE : `_record_step_failure` en dérive la
                # signature qui compte les échecs identiques. Y glisser le nom
                # horodaté de la trace rendrait chaque échec unique, et le
                # garde-fou « trois fois la même panne » ne se déclencherait
                # plus jamais. Le dossier suffit à retrouver le fichier.
                message = f"{exc} Sortie brute conservée sous {trace.parent.name}/."
                if isinstance(exc, ReviewProtocolError):
                    raise ReviewProtocolError(message, route=exc.route) from exc
                raise PilotError(message) from exc
            raise
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
    captured_session: list[str] = []
    if stream:

        def observe(event: object) -> None:
            session = extract_session_id(event)
            if session:
                captured_session[:] = [session]
            if on_event is not None:
                on_event(event)

        kwargs["on_event"] = observe
    result = runner(_stream_argv(invocation) if stream else invocation.argv, **kwargs)
    payload = result.json()
    if (
        invocation.backend == "cursor"
        and isinstance(payload, dict)
        and payload.get("type") == "result"
    ):
        if usage_dir is not None:
            # AVANT le dépouillement : passé cette ligne, `payload` n'est plus
            # l'enveloppe du fournisseur mais le JSON métier de l'agent.
            persist_usage(usage_dir, invocation, payload)
        if payload.get("is_error"):
            raise PilotError(f"Cursor a rendu une erreur : {payload.get('result', 'aucun détail')}")
        cursor_result = payload.get("result")
        if isinstance(cursor_result, str):
            candidate = cursor_result.strip()
            if candidate.endswith("\n\n[REDACTED]"):
                candidate = candidate[: -len("\n\n[REDACTED]")].rstrip()
            fence = "```json\n"
            fence_count = candidate.count(fence)
            if fence_count >= 1:
                parts = candidate.split(fence, maxsplit=1)
                after_fence = parts[1]
                end_marker = after_fence.rfind("\n```")
                if end_marker >= 0:
                    candidate = after_fence[:end_marker].strip()
                else:
                    candidate = after_fence.strip()
            try:
                payload = json.loads(candidate)
            except json.JSONDecodeError as exc:
                error_type = (
                    ReviewProtocolError
                    if invocation.role in {"reviewer", "brief-reviewer"}
                    else PilotError
                )
                raise error_type(
                    "Cursor a réussi sans rendre le JSON métier attendu.",
                    raw=candidate,
                ) from exc
        else:
            payload = cursor_result
    if (
        stream
        and invocation.role == "executor"
        and captured_session
        and extract_session_id(payload) is None
    ):
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
    for name in ("git", "gh", settings.cursor_binary):
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
