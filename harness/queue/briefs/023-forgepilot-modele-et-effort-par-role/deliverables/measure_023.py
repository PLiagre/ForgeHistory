#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 023 (modèle et effort par rôle).

Chaque compteur est imprimé avec **son dénominateur**, dérivé à l'exécution.

Usage, depuis la racine du dépôt :
  python3 …/deliverables/measure_023.py              # imprime seulement
  python3 …/deliverables/measure_023.py --write-manifest  # met à jour manifest.json
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest.mock
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "023-forgepilot-modele-et-effort-par-role"
TEST_FILE = REPO / "control-plane" / "tests" / "test_workflow.py"
BASE_REF = "e6fdd28f901e019280f8b2463cb03b9f1fdcb4f2"
NOT_COMPUTED = -1

D8_ALLOWED = {
    "control-plane/forgepilot/config.py",
    "control-plane/forgepilot/workflow.py",
    "control-plane/forgepilot/cli.py",
    "control-plane/config.toml",
    "control-plane/tests/test_workflow.py",
    "control-plane/README.md",
    "harness/queue/cost-ledger.jsonl",
}
D8_DELIVERABLES_PREFIX = (
    "harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/deliverables/"
)

ORIGINAL_TEST_NAMES = [
    "test_doctor_refuses_anthropic_api_billing",
    "test_claude_code_is_read_only",
    "test_cursor_is_only_executor_and_uses_sandbox",
    "test_dirty_repo_refuses_worktree",
    "test_worktree_branch_is_agent_scoped",
    "test_publish_refuses_non_agent_branch",
    "test_review_keeps_argv_under_system_arg_limit",
    "test_iterate_without_worktree_refuses_naming_execute",
    "test_iterate_reuses_existing_worktree",
    "test_iterate_carries_sandbox_and_does_not_run_without_flag",
    "test_claude_flags_order_unchanged_after_dash_p",
    "test_format_invocation_hides_prompt_keeps_output_format",
]

EXPECTED_CLAUDE_AFTER_P = [
    "--output-format",
    "json",
    "--permission-mode",
    "plan",
    "--tools",
    "Read,Glob,Grep",
    "--disallowedTools",
    "mcp__*",
    "--safe-mode",
    "--disable-slash-commands",
    "--no-chrome",
    "--no-session-persistence",
]

ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True
    )
    if proc.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} a échoué (code {proc.returncode}): "
            f"{(proc.stderr or proc.stdout or '').strip()}"
        )
    return proc.stdout


def _base_toml(*, tools: str = "", roles: str = "") -> str:
    return (
        "[project]\n"
        'id = "measure-023"\n'
        'engine_repository = "owner/engine"\n'
        'city_repository = "owner/city"\n'
        'default_base_ref = "origin/main"\n'
        'default_base_branch = "main"\n'
        "\n"
        "[tools]\n"
        'claude_binary = "claude"\n'
        'cursor_binary = "agent"\n'
        f"{tools}"
        "timeout_seconds = 30\n"
        f"{roles}"
    )


def settings_bare():
    from forgepilot.config import Settings

    return Settings(
        project_id="measure-023",
        engine_repository="owner/engine",
        city_repository="owner/city",
        default_base_ref="origin/main",
        default_base_branch="main",
        claude_binary="claude",
        cursor_binary="agent",
        claude_model="",
        cursor_model="auto",
        timeout_seconds=30,
    )


def measure_sc1() -> tuple[int, int, int]:
    from forgepilot.config import load_settings
    from forgepilot.workflow import plan_invocation, review_invocation
    from forgepilot.process import PilotError

    distinct = roles_count = unknown = 0
    # SC1b : compter les rôles du config.toml livré, pas d'un fichier jetable.
    delivered = load_settings(REPO / "control-plane" / "config.toml")
    roles_count = len(delivered.roles)

    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        cfg = root / "config.toml"
        cfg.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles=(
                    "\n[roles.planner]\n"
                    'model = "measure-planner-a"\n'
                    'effort = "high"\n'
                    "\n[roles.reviewer]\n"
                    'model = "measure-reviewer-b"\n'
                    'effort = "low"\n'
                    "\n[roles.executor]\n"
                    'model = "measure-executor-c"\n'
                ),
            ),
            encoding="utf-8",
        )
        settings = load_settings(cfg)
        task = root / "task.md"
        task.write_text("mesure", encoding="utf-8")
        plan = root / "plan.json"
        plan.write_text('{"task":"m"}', encoding="utf-8")
        plan_inv = plan_invocation(settings, root, task)
        with unittest.mock.patch("forgepilot.workflow.git", return_value="diff"):
            review_inv = review_invocation(settings, root, plan, "origin/main")
        pm = plan_inv.argv[plan_inv.argv.index("--model") + 1]
        rm = review_inv.argv[review_inv.argv.index("--model") + 1]
        if pm != rm:
            distinct = 1

        bad = root / "bad.toml"
        bad.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles='\n[roles.zorglub]\nmodel = "x"\n',
            ),
            encoding="utf-8",
        )
        try:
            load_settings(bad)
        except PilotError as exc:
            msg = str(exc)
            if "planner" in msg and "reviewer" in msg and "executor" in msg:
                unknown = 1
    return distinct, roles_count, unknown


def measure_sc2() -> tuple[int, int, int]:
    from forgepilot.cli import main
    from forgepilot.config import EFFORT_LEVELS, load_settings
    from forgepilot.process import PilotError
    from forgepilot.workflow import plan_invocation, review_invocation

    transmitted = refuse = levels_ok = 0
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        cfg = root / "config.toml"
        cfg.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles=(
                    "\n[roles.planner]\n"
                    'model = "m-p"\n'
                    'effort = "xhigh"\n'
                    "\n[roles.reviewer]\n"
                    'model = "m-r"\n'
                    'effort = "low"\n'
                ),
            ),
            encoding="utf-8",
        )
        settings = load_settings(cfg)
        task = root / "task.md"
        task.write_text("effort", encoding="utf-8")
        plan = root / "plan.json"
        plan.write_text('{"task":"e"}', encoding="utf-8")
        plan_inv = plan_invocation(settings, root, task)
        with unittest.mock.patch("forgepilot.workflow.git", return_value="d"):
            review_inv = review_invocation(settings, root, plan, "origin/main")
        if (
            "--effort" in plan_inv.argv
            and "--effort" in review_inv.argv
            and plan_inv.argv[plan_inv.argv.index("--effort") + 1] == "xhigh"
            and review_inv.argv[review_inv.argv.index("--effort") + 1] == "low"
        ):
            transmitted = 1

        refuse_parts = 0
        for command in ("execute", "iterate"):
            err = io.StringIO()
            try:
                with unittest.mock.patch("sys.stderr", err):
                    code = main(
                        [
                            command,
                            str(plan),
                            "--task-name",
                            "meas-effort",
                            "--repo",
                            str(root),
                            "--effort",
                            "high",
                        ]
                    )
            except SystemExit:
                code = -99
            msg = err.getvalue().lower()
            if code == 2 and "cuit" in msg and "modèle" in msg:
                refuse_parts += 1
        exec_cfg = root / "exec.toml"
        exec_cfg.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles='\n[roles.executor]\nmodel = "m"\neffort = "high"\n',
            ),
            encoding="utf-8",
        )
        try:
            load_settings(exec_cfg)
        except PilotError as exc:
            msg = str(exc).lower()
            if "cuit" in msg and "modèle" in msg:
                refuse_parts += 1
        if refuse_parts == 3:
            refuse = 1

        accepted = 0
        for level in EFFORT_LEVELS:
            level_cfg = root / f"level-{level}.toml"
            level_cfg.write_text(
                _base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles=f'\n[roles.planner]\nmodel = "m"\neffort = "{level}"\n',
                ),
                encoding="utf-8",
            )
            try:
                s = load_settings(level_cfg)
                if s.roles["planner"].effort == level:
                    accepted += 1
            except PilotError:
                pass
        # Sixième niveau refusé aussi via le drapeau (priorité 1 de D3).
        bad_level = root / "bad-level.toml"
        bad_level.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles='\n[roles.planner]\nmodel = "m"\neffort = "ultra"\n',
            ),
            encoding="utf-8",
        )
        sixth_refused_config = False
        try:
            load_settings(bad_level)
        except PilotError:
            sixth_refused_config = True
        err_cli = io.StringIO()
        sixth_refused_cli = False
        try:
            with unittest.mock.patch("sys.stderr", err_cli):
                code_cli = main(
                    [
                        "plan",
                        str(task),
                        "--repo",
                        str(root),
                        "--config",
                        str(cfg),
                        "--effort",
                        "ultra",
                    ]
                )
        except SystemExit:
            code_cli = -99
        if code_cli == 2 and "ultra" in err_cli.getvalue() and "invalide" in err_cli.getvalue().lower():
            sixth_refused_cli = True
        if accepted == 5 and sixth_refused_config and sixth_refused_cli:
            levels_ok = 5
        else:
            levels_ok = accepted
    return transmitted, refuse, levels_ok


def measure_sc3() -> tuple[int, int, int, int]:
    from forgepilot.config import load_settings
    from forgepilot.workflow import plan_invocation

    cli_ok = role_ok = fallback = none_flag = 0
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        cfg = root / "config.toml"
        cfg.write_text(
            _base_toml(
                tools='claude_model = "tools-model"\ncursor_model = "auto"\n',
                roles='\n[roles.planner]\nmodel = "roles-model"\neffort = "medium"\n',
            ),
            encoding="utf-8",
        )
        settings = load_settings(cfg)
        task = root / "task.md"
        task.write_text("prio", encoding="utf-8")
        override = "cli-model"
        inv = plan_invocation(settings, root, task, model=override)
        if inv.argv[inv.argv.index("--model") + 1] == override:
            cli_ok = 1
        inv2 = plan_invocation(settings, root, task)
        if inv2.argv[inv2.argv.index("--model") + 1] == "roles-model":
            role_ok = 1

        bare = root / "bare.toml"
        bare.write_text(
            _base_toml(tools='claude_model = "tools-only"\ncursor_model = "auto"\n'),
            encoding="utf-8",
        )
        s_bare = load_settings(bare)
        inv3 = plan_invocation(s_bare, root, task)
        if (
            not s_bare.roles
            and inv3.argv[inv3.argv.index("--model") + 1] == "tools-only"
            and "--effort" not in inv3.argv
        ):
            fallback = 1

        empty = root / "empty.toml"
        empty.write_text(
            _base_toml(tools='claude_model = ""\ncursor_model = "auto"\n'),
            encoding="utf-8",
        )
        s_empty = load_settings(empty)
        inv4 = plan_invocation(s_empty, root, task)
        if "--model" not in inv4.argv:
            none_flag = 1
    return cli_ok, role_ok, fallback, none_flag


def measure_sc4() -> tuple[int, int, int, int, int]:
    from forgepilot.cli import main
    from forgepilot.config import Settings
    from forgepilot.workflow import (
        create_worktree,
        executor_invocation,
        plan_invocation,
        review_invocation,
    )

    flags = sandbox = prompt_absent = retro = intact = 0
    settings = settings_bare()
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        task = root / "task.md"
        task.write_text("flags", encoding="utf-8")
        plan = root / "plan.json"
        plan.write_text('{"task":"f"}', encoding="utf-8")
        plan_inv = plan_invocation(settings, root, task)
        with unittest.mock.patch("forgepilot.workflow.git", return_value="d"):
            review_inv = review_invocation(settings, root, plan, "origin/main")
        ok_roles = 0
        for inv in (plan_inv, review_inv):
            p_index = inv.argv.index("-p")
            got = list(inv.argv[p_index + 1 : p_index + 1 + len(EXPECTED_CLAUDE_AFTER_P)])
            if got == EXPECTED_CLAUDE_AFTER_P:
                ok_roles += 1
        if ok_roles == 2:
            flags = 1

        # execute : aperçu sans --run (pas besoin de worktree réel).
        exec_ok = False
        out_exec = io.StringIO()
        with unittest.mock.patch("sys.stdout", out_exec):
            code_exec = main(
                [
                    "execute",
                    str(plan),
                    "--task-name",
                    "meas-sandbox",
                    "--repo",
                    str(root),
                ]
            )
        if code_exec == 0:
            text = out_exec.getvalue()
            brace = text.find("{")
            if brace >= 0:
                payload = json.loads(text[brace:])
                argv = payload["argv"]
                if "--sandbox" in argv and argv[argv.index("--sandbox") + 1] == "enabled":
                    exec_ok = True

        # iterate : sous-commande réelle via main (worktree agent préparé).
        # Repo git propre dédié : plan.json doit être suivi pour avoid dirty-tree.
        iter_ok = False
        iter_repo = root / "iter-repo"
        iter_repo.mkdir()
        subprocess.run(
            ["git", "init", "-b", "main"],
            cwd=iter_repo,
            check=True,
            capture_output=True,
        )
        subprocess.run(
            ["git", "config", "user.email", "measure@example.com"],
            cwd=iter_repo,
            check=True,
            capture_output=True,
        )
        subprocess.run(
            ["git", "config", "user.name", "Measure"],
            cwd=iter_repo,
            check=True,
            capture_output=True,
        )
        (iter_repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
        iter_plan = iter_repo / "plan.json"
        iter_plan.write_text('{"task":"f"}', encoding="utf-8")
        subprocess.run(
            ["git", "add", "tracked.txt", "plan.json"],
            cwd=iter_repo,
            check=True,
            capture_output=True,
        )
        subprocess.run(
            ["git", "commit", "-m", "initial"],
            cwd=iter_repo,
            check=True,
            capture_output=True,
        )
        create_worktree(iter_repo, "meas-sandbox", "main")
        out_iter = io.StringIO()
        with unittest.mock.patch(
            "forgepilot.cli.execute_invocation",
            side_effect=AssertionError("execute_invocation appelé"),
        ):
            with unittest.mock.patch("sys.stdout", out_iter):
                code_iter = main(
                    [
                        "iterate",
                        str(iter_plan),
                        "--task-name",
                        "meas-sandbox",
                        "--repo",
                        str(iter_repo),
                    ]
                )
        if code_iter == 0:
            text = out_iter.getvalue()
            brace = text.find("{")
            if brace >= 0:
                payload = json.loads(text[brace:])
                argv = payload["argv"]
                if "--sandbox" in argv and argv[argv.index("--sandbox") + 1] == "enabled":
                    iter_ok = True

        if exec_ok and iter_ok:
            sandbox = 1
        # garde secondaire : executor_invocation direct (ne remplace pas iterate).
        _ = executor_invocation(settings, root, plan)

        bound = 32 * os.sysconf("SC_PAGESIZE")
        marker = "MEASURE_023_OVERFLOW_MARKER"
        synthetic = marker + ("M" * (bound + 128 - len(marker)))
        with unittest.mock.patch("forgepilot.workflow.git", return_value=synthetic):
            big = review_invocation(settings, root, plan, "origin/main")
        if all(marker not in a for a in big.argv):
            prompt_absent = 1

    try:
        Settings("t", "o/e", "o/c", "origin/main", "main", "claude", "agent", "", "auto", 30)
        retro = 1
    except TypeError:
        retro = 0

    try:
        diff = git("diff", BASE_REF, "--", "control-plane/tests/test_workflow.py")
    except RuntimeError:
        return flags, sandbox, prompt_absent, retro, NOT_COMPUTED
    for line in diff.splitlines():
        if line.startswith("---") or line.startswith("+++") or line.startswith("@@"):
            continue
        if line.startswith("-"):
            return flags, sandbox, prompt_absent, retro, 0
    text = TEST_FILE.read_text(encoding="utf-8")
    if all(f"def {name}(" in text for name in ORIGINAL_TEST_NAMES):
        intact = 1
    return flags, sandbox, prompt_absent, retro, intact


def measure_sc5() -> tuple[int, int]:
    from forgepilot.config import load_settings
    from forgepilot.workflow import format_invocation, plan_invocation

    shows = no_leak = 0
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        cfg = root / "config.toml"
        cfg.write_text(
            _base_toml(
                tools='claude_model = ""\ncursor_model = "auto"\n',
                roles='\n[roles.planner]\nmodel = "preview-m"\neffort = "high"\n',
            ),
            encoding="utf-8",
        )
        settings = load_settings(cfg)
        marker = "APERCU_LEAK_023_MARKER"
        task = root / "task.md"
        task.write_text(f"tâche {marker}", encoding="utf-8")
        inv = plan_invocation(settings, root, task)
        formatted = format_invocation(inv)
        payload = json.loads(formatted)
        if payload.get("model") == "preview-m" and payload.get("effort") == "high":
            shows = 1
        if marker not in formatted and payload.get("prompt") == "<prompt>":
            no_leak = 1
    return shows, no_leak


def measure_tests_ajoutes() -> tuple[int, int]:
    text = TEST_FILE.read_text(encoding="utf-8")
    total = len(re.findall(r"^\s*def test_", text, re.MULTILINE))
    added = total - 12
    return added, total


def measure_tests_rouges_avant() -> tuple[int, int]:
    """Rejoue les tests neufs contre config/workflow/cli de e6fdd28.

    Copie jetable hors du dépôt. Retourne
    (échecs parmi les tests ajoutés, total des tests ajoutés).
    """
    added, _total = measure_tests_ajoutes()
    if added <= 0:
        return NOT_COMPUTED, NOT_COMPUTED

    original_names = set(ORIGINAL_TEST_NAMES)
    with tempfile.TemporaryDirectory(prefix="measure023-red-", dir="/tmp") as tmp:
        staging = Path(tmp) / "control-plane"
        shutil.copytree(
            REPO / "control-plane",
            staging,
            ignore=shutil.ignore_patterns(
                "__pycache__", "*.pyc", "*.pyo", ".venv", "*.egg-info", "dist", "build"
            ),
        )
        for rel in (
            "forgepilot/config.py",
            "forgepilot/workflow.py",
            "forgepilot/cli.py",
        ):
            try:
                blob = git("show", f"{BASE_REF}:control-plane/{rel}")
            except RuntimeError:
                return NOT_COMPUTED, added
            (staging / rel).write_text(blob, encoding="utf-8")

        env = os.environ.copy()
        env["PYTHONPATH"] = str(staging)
        cmd = [sys.executable, "-m", "unittest", "discover", "-s", "tests", "-v"]
        proc = subprocess.run(
            cmd,
            cwd=staging,
            capture_output=True,
            text=True,
            env=env,
        )
        combined = proc.stderr + proc.stdout
        failed_added: set[str] = set()
        for match in re.finditer(
            r"^(FAIL|ERROR): (test_\w+)",
            combined,
            re.MULTILINE,
        ):
            name = match.group(2)
            if name not in original_names:
                failed_added.add(name)
        return len(failed_added), added


def measure_suite_verte() -> tuple[int, int, str]:
    cp = str(REPO / "control-plane")
    env = os.environ.copy()
    env["PYTHONPATH"] = cp
    cmd = [sys.executable, "-m", "unittest", "discover", "-s", "tests"]
    proc = subprocess.run(
        cmd,
        cwd=cp,
        capture_output=True,
        text=True,
        env=env,
    )
    cmd_repr = (
        f"PYTHONPATH={cp} {sys.executable} -m unittest discover -s tests "
        f"(cwd={cp})"
    )
    match = re.search(r"Ran (\d+) tests?", proc.stderr + proc.stdout)
    ran = int(match.group(1)) if match else NOT_COMPUTED
    ok = 1 if proc.returncode == 0 else 0
    return ok, ran if isinstance(ran, int) else NOT_COMPUTED, cmd_repr


def measure_hors_perimetre() -> tuple[int, int]:
    try:
        committed = git("diff", f"{BASE_REF}...HEAD", "--name-only")
        unstaged = git("diff", BASE_REF, "--name-only")
        staged = git("diff", "--cached", "--name-only")
        untracked = git("ls-files", "--others", "--exclude-standard")
    except RuntimeError:
        return NOT_COMPUTED, NOT_COMPUTED
    names: set[str] = set()
    for blob in (committed, unstaged, staged, untracked):
        for line in blob.splitlines():
            line = line.strip()
            if line:
                names.add(line)
    outside = []
    for path in sorted(names):
        if path in D8_ALLOWED:
            continue
        if path.startswith(D8_DELIVERABLES_PREFIX):
            continue
        outside.append(path)
    return len(outside), len(names)


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesurés du brief 023")
    parser.add_argument("--json", action="store_true")
    parser.add_argument(
        "--write-manifest",
        action="store_true",
        default=False,
        help="écrit deliverables/manifest.json (sinon : impression seule)",
    )
    args = parser.parse_args()

    cp = str(REPO / "control-plane")
    if cp not in sys.path:
        sys.path.insert(0, cp)

    distinct, roles_count, unknown = measure_sc1()
    report("modeles_par_role_distincts", distinct, "1=deux --model distincts / 0=échec")
    report("roles_declares", roles_count, "3=rôles connus du pilote")
    report("role_inconnu_refuse", unknown, "1=refus nommant les trois rôles / 0=échec")

    transmitted, refuse, levels = measure_sc2()
    report("effort_transmis_claude", transmitted, "1=--effort sur planner et reviewer / 0=échec")
    report(
        "effort_refuse_sur_cursor",
        refuse,
        "1=execute+iterate+roles.executor refusés / 0=échec",
    )
    report("niveaux_effort_acceptes", levels, "5=low,medium,high,xhigh,max (+1 invalide refusé)")

    cli_ok, role_ok, fallback, none_flag = measure_sc3()
    report("priorite_cli_sur_role", cli_ok, "1=drapeau > [roles.*] / 0=échec")
    report("priorite_role_sur_tools", role_ok, "1=[roles.*] > [tools] / 0=échec")
    report("repli_sur_tools", fallback, "1=sans [roles.*] comportement d'avant / 0=échec")
    report(
        "aucun_drapeau_si_rien_declare",
        none_flag,
        "1=pas de --model si rien déclaré / 0=échec",
    )

    flags, sandbox, prompt_absent, retro, intact = measure_sc4()
    report(
        "drapeaux_lecture_seule_intacts",
        flags,
        "1=ordre après -p intact sur les deux rôles Claude / 0=échec",
    )
    report("sandbox_intact", sandbox, "1=--sandbox enabled sur execute et iterate / 0=échec")
    report("prompt_absent_de_argv", prompt_absent, "1=marqueur hors argv / 0=présent")
    report(
        "settings_retrocompatible",
        retro,
        "1=Settings constructible avec dix champs / 0=TypeError",
    )
    report(
        "tests_existants_intacts",
        intact,
        "1=douze tests d'origine intacts (diff additions only) / 0=touchés",
    )

    shows, no_leak = measure_sc5()
    report(
        "apercu_montre_modele_et_effort",
        shows,
        "1=aperçu porte model et effort / 0=échec",
    )
    report(
        "apercu_ne_fuit_pas_le_prompt",
        no_leak,
        "1=prompt masqué / 0=fuite",
    )

    added, total = measure_tests_ajoutes()
    report("tests_ajoutes", added, f"total tests dans test_workflow.py après lot = {total}")

    rouges, rouges_denom = measure_tests_rouges_avant()
    report(
        "tests_rouges_avant_correction",
        rouges,
        f"tests ajoutés = {rouges_denom}",
    )

    suite_ok, suite_ran, suite_cmd = measure_suite_verte()
    report(
        "suite_control_plane_verte",
        suite_ok,
        f"tests exécutés = {suite_ran}",
    )
    print(f"# commande suite_control_plane_verte : {suite_cmd}", file=sys.stderr)

    hors, changed = measure_hors_perimetre()
    report(
        "fichiers_hors_perimetre_modifies",
        hors,
        f"fichiers touchés vs {BASE_REF[:7]} = {changed}",
    )

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = BRIEF / "deliverables" / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        counters = []
        for name, value, denom in ROWS:
            sample = 1
            if name == "roles_declares":
                sample = 3
            elif name == "niveaux_effort_acceptes":
                sample = 5
            elif name == "tests_ajoutes":
                sample = total
            elif name == "suite_control_plane_verte":
                sample = suite_ran if isinstance(suite_ran, int) and suite_ran > 0 else NOT_COMPUTED
            elif name == "fichiers_hors_perimetre_modifies":
                sample = changed if isinstance(changed, int) and changed > 0 else 1
            elif name == "tests_rouges_avant_correction":
                sample = (
                    rouges_denom
                    if isinstance(rouges_denom, int) and rouges_denom > 0
                    else NOT_COMPUTED
                )
            else:
                sample = 1
            if sample == 0:
                sample = NOT_COMPUTED
            counters.append(
                {
                    "name": name,
                    "value": value,
                    "sample_size": sample,
                    "denominator": denom,
                }
            )
        manifest["counters"] = counters
        manifest_path.write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        print(f"# manifest mis à jour : {manifest_path.relative_to(REPO)}", file=sys.stderr)

    for name, value, _ in ROWS:
        if value == NOT_COMPUTED:
            print(f"# sentinelle -1 pour {name}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
