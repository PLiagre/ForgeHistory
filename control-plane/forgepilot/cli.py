from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

from .config import CURSOR_EFFORT_REFUSED, load_settings
from .durable import declared_risk, recover_executor_result, register_run, resume_run
from .merge import merge_run
from .process import PilotError, git, run_command
from .protocol import validate_plan
from .review import (
    comment_review_on_pr,
    render_verdict_material,
    validate_verdict_material,
)
from .state import load_state, run_state_path, status_snapshot
from .workflow import (
    chain_preview,
    create_worktree,
    default_task_name,
    execute_invocation,
    executor_invocation,
    existing_worktree,
    format_invocation,
    missing_binaries,
    persist_result,
    plan_invocation,
    publish,
    publish_preview,
    review_invocation,
    witness_invocation,
)


def _path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(prog="forgepilot")
    root.add_argument("--config", type=_path)
    root.add_argument("--policy", type=_path)
    commands = root.add_subparsers(dest="command", required=True)

    doctor = commands.add_parser("doctor", help="vérifier le poste de pilotage")
    doctor.add_argument("--repo", type=_path, default=Path.cwd())
    doctor.add_argument("--check-auth", action="store_true")

    plan = commands.add_parser("plan", help="faire préparer un plan par Claude Code")
    plan.add_argument("task", type=_path)
    plan.add_argument("--repo", type=_path, default=Path.cwd())
    plan.add_argument("--model")
    plan.add_argument("--effort")
    plan.add_argument("--run", action="store_true")

    execute = commands.add_parser("execute", help="faire exécuter un plan par Cursor")
    execute.add_argument("plan", type=_path)
    execute.add_argument("--repo", type=_path, default=Path.cwd())
    execute.add_argument("--base")
    execute.add_argument("--task-name", required=True)
    execute.add_argument("--model")
    execute.add_argument("--effort")
    execute.add_argument("--run", action="store_true")

    iterate = commands.add_parser(
        "iterate",
        help="réexécuter un plan sur le worktree agent existant",
    )
    iterate.add_argument("plan", type=_path)
    iterate.add_argument("--repo", type=_path, default=Path.cwd())
    iterate.add_argument("--task-name", required=True)
    iterate.add_argument("--model")
    iterate.add_argument("--effort")
    iterate.add_argument("--feedback", type=_path)
    iterate.add_argument("--session")
    iterate.add_argument("--run", action="store_true")

    review = commands.add_parser("review", help="faire relire un diff par Claude Code")
    review.add_argument("plan", type=_path)
    review.add_argument("--repo", type=_path, default=Path.cwd())
    review.add_argument("--base")
    review.add_argument("--model")
    review.add_argument("--effort")
    review.add_argument("--bundle", type=_path)
    review.add_argument("--run", action="store_true")

    publish_parser = commands.add_parser("publish", help="ouvrir une draft PR après Cursor")
    publish_parser.add_argument("--repo", type=_path, default=Path.cwd())
    publish_parser.add_argument("--base")
    publish_parser.add_argument("--title", required=True)
    publish_parser.add_argument("--plan", type=_path)
    publish_parser.add_argument("--risk", choices=("R0", "R1", "R2"), default="R1")
    publish_parser.add_argument("--brief")
    publish_parser.add_argument("--run", action="store_true")

    enchaine = commands.add_parser(
        "enchaine",
        help="plan, execute, publish, review — une commande, pas de fusion",
    )
    enchaine.add_argument("task", type=_path)
    enchaine.add_argument("--repo", type=_path, default=Path.cwd())
    enchaine.add_argument("--task-name")
    enchaine.add_argument("--base")
    enchaine.add_argument("--title")
    enchaine.add_argument("--model")
    enchaine.add_argument("--effort")
    enchaine.add_argument("--risk", choices=("R0", "R1", "R2"))
    enchaine.add_argument("--changed-path", action="append", default=[])
    enchaine.add_argument("--run", action="store_true")

    start = commands.add_parser(
        "start",
        help="enregistrer un lot durable et, avec --run, lancer sa première étape",
    )
    start.add_argument("task", type=_path)
    start.add_argument("--repo", type=_path, default=Path.cwd())
    start.add_argument("--task-name")
    start.add_argument("--base")
    start.add_argument("--base-branch")
    start.add_argument("--title")
    start.add_argument("--risk", choices=("R0", "R1", "R2"))
    start.add_argument("--changed-path", action="append", default=[])
    start.add_argument(
        "--allow-heavy",
        action="store_true",
        help="autoriser explicitement les preuves lourdes de certification",
    )
    start.add_argument("--run", action="store_true")

    status = commands.add_parser("status", help="afficher l'état atomique d'un lot")
    status.add_argument("run_id", nargs="?", default="latest")
    status.add_argument("--repo", type=_path, default=Path.cwd())

    resume = commands.add_parser("resume", help="reprendre la première étape incomplète")
    resume.add_argument("run_id", nargs="?", default="latest")
    resume.add_argument("--repo", type=_path, default=Path.cwd())
    resume.add_argument("--allow-heavy", action="store_true")

    recover_executor = commands.add_parser(
        "recover-executor",
        help="archiver un résultat exécuteur retrouvé après un blocage ambigu",
    )
    recover_executor.add_argument("run_id")
    recover_executor.add_argument("--repo", type=_path, default=Path.cwd())
    recover_executor.add_argument("--result", type=_path, required=True)

    verdict = commands.add_parser(
        "verdict",
        help="rendre le matériau de revue lié au SHA sous forme Markdown",
    )
    verdict.add_argument("run_id", nargs="?", default="latest")
    verdict.add_argument("--repo", type=_path, default=Path.cwd())
    verdict.add_argument("--output", type=_path)
    verdict.add_argument("--comment-pr", action="store_true")

    witness = commands.add_parser(
        "witness",
        help="témoin Claude Opus 5 hors chemin quotidien (ADR-0017)",
    )
    witness.add_argument("plan", type=_path)
    witness.add_argument("--repo", type=_path, default=Path.cwd())
    witness.add_argument("--base")
    witness.add_argument("--bundle", type=_path)
    witness.add_argument("--run", action="store_true")

    merge = commands.add_parser(
        "merge",
        help="fusionner si juge PASS et checks verts sur le SHA jugé",
    )
    merge.add_argument("run_id", nargs="?", default="latest")
    merge.add_argument("--repo", type=_path, default=Path.cwd())
    merge.add_argument("--run", action="store_true")
    return root


def _run_or_print(invocation, settings, repo: Path, should_run: bool) -> int:
    if not should_run:
        print(format_invocation(invocation))
        return 0
    result = execute_invocation(invocation, settings)
    target = persist_result(repo, invocation.role, invocation, result)
    print(target)
    return 0


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        settings = load_settings(args.config, args.policy)
        if args.command == "doctor":
            if os.environ.get("ANTHROPIC_API_KEY"):
                print("REFUS : ANTHROPIC_API_KEY est défini ; le pilote doit utiliser l'abonnement Claude Pro.")
                return 2
            missing = list(missing_binaries(settings))
            branch = git(args.repo, "branch", "--show-current")
            print(f"Projet : {settings.project_id}")
            print(f"Dépôt : {args.repo}")
            print(f"Branche : {branch or '(détachée)'}")
            if settings.policy is None:
                print("Politique : ABSENTE")
                return 2
            print("Politique effective :")
            print(json.dumps(settings.policy.summary(), indent=2, ensure_ascii=False))
            if missing:
                print("Binaires manquants : " + ", ".join(missing))
                return 1
            print("Binaires : OK")
            if args.check_auth:
                for command in (
                    [settings.claude_binary, "auth", "status"],
                    [settings.cursor_binary, "status"],
                    ["gh", "auth", "status"],
                ):
                    run_command(command, cwd=args.repo, timeout_seconds=60)
                print("Authentifications Claude Code, Cursor et GitHub : OK")
            return 0

        if args.command == "plan":
            invocation = plan_invocation(
                settings,
                args.repo,
                args.task,
                model=args.model,
                effort=args.effort,
            )
            return _run_or_print(invocation, settings, args.repo, args.run)

        if args.command == "execute":
            if args.effort:
                raise PilotError(CURSOR_EFFORT_REFUSED)
            base = args.base or settings.default_base_ref
            if not args.run:
                preview_worktree = args.repo / ".forgepilot" / "worktrees" / args.task_name
                invocation = executor_invocation(
                    settings, preview_worktree, args.plan, model=args.model
                )
                print(format_invocation(invocation))
                return 0
            worktree, branch = create_worktree(args.repo, args.task_name, base)
            invocation = executor_invocation(
                settings, worktree, args.plan, model=args.model
            )
            result = execute_invocation(invocation, settings)
            target = persist_result(args.repo, invocation.role, invocation, result)
            print(f"Branche : {branch}")
            print(f"Worktree : {worktree}")
            print(f"Résultat : {target}")
            return 0

        if args.command == "iterate":
            if args.effort:
                raise PilotError(CURSOR_EFFORT_REFUSED)
            worktree, branch, status = existing_worktree(args.repo, args.task_name)
            print(f"Branche : {branch}")
            print(f"Worktree : {worktree}")
            print(f"État git :\n{status}" if status else "État git : (propre)")
            invocation = executor_invocation(
                settings,
                worktree,
                args.plan,
                model=args.model,
                feedback=args.feedback,
                resume_session=args.session,
            )
            if not args.run:
                print(format_invocation(invocation))
                return 0
            if args.feedback is None:
                raise PilotError(
                    "Feedback structuré absent ; fournir --feedback pour une itération réelle."
                )
            result = execute_invocation(invocation, settings)
            target = persist_result(args.repo, "executor", invocation, result)
            print(f"Résultat : {target}")
            return 0

        if args.command == "review":
            base = args.base or settings.default_base_ref
            invocation = review_invocation(
                settings,
                args.repo,
                args.plan,
                base,
                model=args.model,
                effort=args.effort,
                bundle_path=args.bundle,
            )
            return _run_or_print(invocation, settings, args.repo, args.run)

        if args.command == "publish":
            base_branch = args.base or settings.default_base_branch
            if not args.run:
                print(
                    format_invocation(
                        publish_preview(
                            args.repo,
                            args.title,
                            base_branch,
                            risk=args.risk,
                            brief=args.brief,
                        )
                    )
                )
                return 0
            raise PilotError(
                "Publication directe désactivée : employer `start --run` puis `resume` "
                "afin de conserver les preuves exactes du candidat."
            )

        if args.command == "enchaine":
            task_name = args.task_name or default_task_name(args.task)
            if not args.run:
                payload = chain_preview(
                    settings,
                    args.repo,
                    args.task,
                    task_name,
                    model=args.model,
                    effort=args.effort,
                    requested_risk=args.risk or declared_risk(args.task) or "R1",
                    changed_paths=args.changed_path,
                )
                print(json.dumps(payload, indent=2, ensure_ascii=False))
                return 0
            state_path, state = register_run(
                settings,
                args.repo,
                args.task,
                task_name,
                requested_risk=args.risk,
                changed_paths=args.changed_path,
                base_ref=args.base,
                base_branch=settings.default_base_branch,
                title=args.title or task_name,
            )
            state = resume_run(settings, args.repo, str(state["run_id"]))
            print(
                json.dumps(
                    {"state_path": str(state_path), "state": state},
                    indent=2,
                    ensure_ascii=False,
                )
            )
            return 0

        if args.command == "start":
            task_name = args.task_name or default_task_name(args.task)
            state_path, state = register_run(
                settings,
                args.repo,
                args.task,
                task_name,
                requested_risk=args.risk,
                changed_paths=args.changed_path,
                base_ref=args.base,
                base_branch=args.base_branch,
                title=args.title,
                allow_heavy=args.allow_heavy,
            )
            if args.run:
                print(
                    f"RUN {state['run_id']} enregistré ; suivi : "
                    f"forgepilot status {state['run_id']} --repo {args.repo}",
                    file=sys.stderr,
                    flush=True,
                )
                state = resume_run(
                    settings,
                    args.repo,
                    str(state["run_id"]),
                    allow_heavy=args.allow_heavy,
                )
            print(json.dumps({"state_path": str(state_path), "state": state}, indent=2, ensure_ascii=False))
            return 0

        if args.command == "status":
            state_path = run_state_path(args.repo, args.run_id)
            print(
                json.dumps(
                    {"state_path": str(state_path), "state": status_snapshot(load_state(state_path))},
                    indent=2,
                    ensure_ascii=False,
                )
            )
            return 0

        if args.command == "resume":
            print(
                f"RUN {args.run_id} : reprise de la première étape incomplète",
                file=sys.stderr,
                flush=True,
            )
            state = resume_run(
                settings,
                args.repo,
                args.run_id,
                allow_heavy=args.allow_heavy,
            )
            print(json.dumps(state, indent=2, ensure_ascii=False))
            return 0

        if args.command == "recover-executor":
            state = recover_executor_result(args.repo, args.run_id, args.result)
            print(json.dumps(state, indent=2, ensure_ascii=False))
            return 0

        if args.command == "verdict":
            state_path = run_state_path(args.repo, args.run_id)
            state = load_state(state_path)
            artifacts = state.get("artifacts")
            material_value = artifacts.get("review_material") if isinstance(artifacts, dict) else None
            if not isinstance(material_value, str):
                raise PilotError("Aucun matériau de revue archivé pour ce lot.")
            material_path = Path(material_value)
            if not material_path.is_absolute():
                material_path = state_path.parent / material_path
            validate_verdict_material(args.repo, state, material_path)
            output = args.output or state_path.parent / "verdict-material.md"
            render_verdict_material(material_path, output)
            if args.comment_pr:
                pull_request = state.get("pull_request")
                if not isinstance(pull_request, str) or not pull_request:
                    raise PilotError("PR absente ; commentaire de revue impossible.")
                worktree = Path(str(state.get("worktree") or args.repo))
                comment_review_on_pr(worktree, pull_request, output)
            print(output)
            return 0

        if args.command == "witness":
            base = args.base or settings.default_base_ref
            invocation = witness_invocation(
                settings,
                args.repo,
                args.plan,
                base,
                bundle_path=args.bundle,
            )
            return _run_or_print(invocation, settings, args.repo, args.run)

        if args.command == "merge":
            payload = merge_run(args.repo, args.run_id, apply=args.run)
            print(json.dumps(payload, indent=2, ensure_ascii=False))
            return 0
    except (OSError, KeyError, ValueError, PilotError) as exc:
        print(f"REFUS : {exc}", file=sys.stderr)
        return 2
    return 2
