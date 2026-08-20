from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys

from .config import CURSOR_EFFORT_REFUSED, load_settings
from .process import PilotError, git, run_command
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
    run_chain,
    lot_preview,
    run_lot,
)


def _path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(prog="forgepilot")
    root.add_argument("--config", type=_path)
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
    iterate.add_argument("--run", action="store_true")

    review = commands.add_parser("review", help="faire relire un diff par Claude Code")
    review.add_argument("plan", type=_path)
    review.add_argument("--repo", type=_path, default=Path.cwd())
    review.add_argument("--base")
    review.add_argument("--model")
    review.add_argument("--effort")
    review.add_argument("--run", action="store_true")

    publish_parser = commands.add_parser("publish", help="ouvrir une draft PR après Cursor")
    publish_parser.add_argument("--repo", type=_path, default=Path.cwd())
    publish_parser.add_argument("--base")
    publish_parser.add_argument("--title", required=True)
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
    enchaine.add_argument("--run", action="store_true")

    lot = commands.add_parser(
        "lot",
        help="si besoin, Claude écrit le brief, puis enchaine — pas de fusion",
    )
    lot.add_argument("source", type=_path)
    lot.add_argument("--repo", type=_path, default=Path.cwd())
    lot.add_argument("--task-name")
    lot.add_argument("--base")
    lot.add_argument("--title")
    lot.add_argument("--model")
    lot.add_argument("--effort")
    lot.add_argument("--run", action="store_true")
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
        settings = load_settings(args.config)
        if args.command == "doctor":
            if os.environ.get("ANTHROPIC_API_KEY"):
                print("REFUS : ANTHROPIC_API_KEY est défini ; le pilote doit utiliser l'abonnement Claude Pro.")
                return 2
            missing = list(missing_binaries(settings))
            branch = git(args.repo, "branch", "--show-current")
            print(f"Projet : {settings.project_id}")
            print(f"Dépôt : {args.repo}")
            print(f"Branche : {branch or '(détachée)'}")
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
                settings, worktree, args.plan, model=args.model
            )
            if not args.run:
                print(format_invocation(invocation))
                return 0
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
            )
            return _run_or_print(invocation, settings, args.repo, args.run)

        if args.command == "publish":
            base_branch = args.base or settings.default_base_branch
            if not args.run:
                print(format_invocation(publish_preview(args.repo, args.title, base_branch)))
                return 0
            print(publish(args.repo, args.title, base_branch))
            return 0

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
                )
                print(json.dumps(payload, indent=2, ensure_ascii=False))
                return 0
            payload = run_chain(
                settings,
                args.repo,
                args.task,
                task_name,
                base_ref=args.base or settings.default_base_ref,
                base_branch=settings.default_base_branch,
                title=args.title or task_name,
                model=args.model,
                effort=args.effort,
            )
            print(json.dumps(payload, indent=2, ensure_ascii=False))
            return 0

        if args.command == "lot":
            if not args.run:
                payload = lot_preview(
                    settings,
                    args.repo,
                    args.source,
                    args.task_name,
                    model=args.model,
                    effort=args.effort,
                )
                print(json.dumps(payload, indent=2, ensure_ascii=False))
                return 0
            payload = run_lot(
                settings,
                args.repo,
                args.source,
                args.task_name,
                base_ref=args.base or settings.default_base_ref,
                base_branch=settings.default_base_branch,
                title=args.title,
                model=args.model,
                effort=args.effort,
            )
            print(json.dumps(payload, indent=2, ensure_ascii=False))
            return 0
    except (OSError, KeyError, ValueError, PilotError) as exc:
        print(f"REFUS : {exc}", file=sys.stderr)
        return 2
    return 2
