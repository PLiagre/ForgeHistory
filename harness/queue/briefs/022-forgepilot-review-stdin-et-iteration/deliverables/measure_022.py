#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 022 (stdin + iterate).

Chaque compteur est imprimé avec **son dénominateur**, dérivé à l'exécution.
Amendement 001 : six tests d'origine (pas quatre) ; base de départ de
`tests_ajoutes` = 6.

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables/measure_022.py
  # ou : python3 … (avec forgepilot importable)
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest.mock
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "022-forgepilot-review-stdin-et-iteration"
LOG = BRIEF / "deliverables" / "generator-log.md"
TEST_FILE = REPO / "control-plane" / "tests" / "test_workflow.py"
BASE_REF = "origin/master"
NOT_COMPUTED = -1

# Périmètre D4 (chemins relatifs au dépôt)
D4_ALLOWED = {
    "control-plane/forgepilot/workflow.py",
    "control-plane/forgepilot/cli.py",
    "control-plane/forgepilot/process.py",
    "control-plane/tests/test_workflow.py",
    "control-plane/README.md",
    "harness/queue/cost-ledger.jsonl",
}
D4_DELIVERABLES_PREFIX = (
    "harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables/"
)

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

ORIGINAL_TEST_NAMES = [
    "test_doctor_refuses_anthropic_api_billing",
    "test_claude_code_is_read_only",
    "test_cursor_is_only_executor_and_uses_sandbox",
    "test_dirty_repo_refuses_worktree",
    "test_worktree_branch_is_agent_scoped",
    "test_publish_refuses_non_agent_branch",
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


def settings_fixture():
    from forgepilot.config import Settings

    return Settings(
        project_id="measure-022",
        engine_repository="owner/engine",
        city_repository="owner/city",
        default_base_ref="origin/master",
        default_base_branch="master",
        claude_binary="claude",
        cursor_binary="agent",
        claude_model="",
        cursor_model="auto",
        timeout_seconds=30,
    )


def measure_sc1() -> tuple[int, int, int, int]:
    """longueur_max, octets_diff, prompt_absent, borne."""
    from forgepilot.workflow import review_invocation

    bound = 32 * os.sysconf("SC_PAGESIZE")
    marker = "MEASURE_022_OVERFLOW_MARKER"
    synthetic = marker + ("M" * (bound + 128 - len(marker)))
    octets = len(synthetic.encode())
    settings = settings_fixture()
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        plan = root / "plan.json"
        plan.write_text('{"task":"measure"}', encoding="utf-8")
        with unittest.mock.patch("forgepilot.workflow.git", return_value=synthetic):
            invocation = review_invocation(settings, root, plan, "origin/master")
    longueur = max(len(a.encode()) for a in invocation.argv)
    absent = 1 if all(marker not in a for a in invocation.argv) else 0
    return longueur, octets, absent, bound


def measure_drapeaux() -> int:
    from forgepilot.workflow import plan_invocation

    settings = settings_fixture()
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        task = root / "task.md"
        task.write_text("mesure drapeaux", encoding="utf-8")
        invocation = plan_invocation(settings, root, task)
    p_index = invocation.argv.index("-p")
    got = list(invocation.argv[p_index + 1 : p_index + 1 + len(EXPECTED_CLAUDE_AFTER_P)])
    return 1 if got == EXPECTED_CLAUDE_AFTER_P else 0


def measure_tests_existants_intacts() -> int:
    try:
        diff = git("diff", f"{BASE_REF}...HEAD", "--", "control-plane/tests/test_workflow.py")
    except RuntimeError:
        # working tree not committed yet — compare against BASE_REF with unstaged
        try:
            diff = git("diff", BASE_REF, "--", "control-plane/tests/test_workflow.py")
        except RuntimeError:
            return NOT_COMPUTED
    # Also include unstaged/staged when HEAD == BASE (lot non committé)
    if not diff.strip():
        try:
            diff = git("diff", BASE_REF, "--", "control-plane/tests/test_workflow.py")
        except RuntimeError:
            return NOT_COMPUTED
    # Seules des additions : aucune ligne '-' hors en-têtes @@ / --- / +++
    for line in diff.splitlines():
        if line.startswith("---") or line.startswith("+++") or line.startswith("@@"):
            continue
        if line.startswith("-"):
            return 0
    text = TEST_FILE.read_text(encoding="utf-8")
    for name in ORIGINAL_TEST_NAMES:
        if f"def {name}(" not in text:
            return 0
    return 1


def measure_format_no_leak() -> int:
    from forgepilot.workflow import format_invocation, review_invocation

    marker = "FORMAT_LEAK_022_MARKER"
    settings = settings_fixture()
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        plan = root / "plan.json"
        plan.write_text('{"task":"leak"}', encoding="utf-8")
        with unittest.mock.patch("forgepilot.workflow.git", return_value=marker + "xx"):
            invocation = review_invocation(settings, root, plan, "origin/master")
    formatted = format_invocation(invocation)
    if marker in formatted:
        return 0
    payload = json.loads(formatted)
    if "--output-format" not in payload["argv"]:
        return 0
    of_i = payload["argv"].index("--output-format")
    if payload["argv"][of_i + 1] != "json":
        return 0
    if of_i > 0 and payload["argv"][of_i] == "<prompt>":
        return 0
    return 1


def _init_temp_repo(repo: Path) -> None:
    subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
    subprocess.run(["git", "config", "user.email", "m@example.com"], cwd=repo, check=True)
    subprocess.run(["git", "config", "user.name", "Measure"], cwd=repo, check=True)
    (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
    subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
    subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)


def measure_iterate() -> tuple[int, int, int, int]:
    from forgepilot.cli import main
    from forgepilot.workflow import create_worktree

    reuse = refuse = sandbox = no_run = 0
    with tempfile.TemporaryDirectory() as tmp:
        repo = Path(tmp)
        _init_temp_repo(repo)
        create_worktree(repo, "meas-reuse", "main")
        worktrees = repo / ".forgepilot" / "worktrees"
        before = sorted(p.name for p in worktrees.iterdir())
        plan = repo / "plan.json"
        plan.write_text('{"task":"m"}', encoding="utf-8")
        out = io.StringIO()
        with unittest.mock.patch("sys.stdout", out):
            code = main(
                ["iterate", str(plan), "--task-name", "meas-reuse", "--repo", str(repo)]
            )
        after = sorted(p.name for p in worktrees.iterdir())
        if code == 0 and before == after:
            reuse = 1
        text = out.getvalue()
        brace = text.find("{")
        if brace >= 0:
            payload = json.loads(text[brace:])
            argv = payload.get("argv", [])
            if "--sandbox" in argv and argv[argv.index("--sandbox") + 1] == "enabled":
                sandbox = 1
        with unittest.mock.patch(
            "forgepilot.cli.execute_invocation",
            side_effect=AssertionError("lancé"),
        ):
            with unittest.mock.patch(
                "forgepilot.workflow.run_command",
                side_effect=AssertionError("lancé"),
            ):
                with unittest.mock.patch("sys.stdout", io.StringIO()):
                    code2 = main(
                        [
                            "iterate",
                            str(plan),
                            "--task-name",
                            "meas-reuse",
                            "--repo",
                            str(repo),
                        ]
                    )
        if code2 == 0:
            no_run = 1

    with tempfile.TemporaryDirectory() as tmp:
        repo = Path(tmp)
        plan = repo / "plan.json"
        plan.write_text('{"task":"absent"}', encoding="utf-8")
        err = io.StringIO()
        try:
            with unittest.mock.patch("sys.stderr", err):
                code = main(
                    [
                        "iterate",
                        str(plan),
                        "--task-name",
                        "absent-xyz",
                        "--repo",
                        str(repo),
                    ]
                )
        except SystemExit:
            code = -99
        if code == 2 and "execute" in err.getvalue():
            refuse = 1
    return reuse, refuse, sandbox, no_run


def measure_tests_ajoutes() -> tuple[int, int]:
    text = TEST_FILE.read_text(encoding="utf-8")
    total = len(re.findall(r"^\s*def test_", text, re.MULTILINE))
    added = total - 6
    return added, total


def measure_tests_rouges_avant() -> int:
    if not LOG.is_file():
        return NOT_COMPUTED
    text = LOG.read_text(encoding="utf-8")
    # Deux échecs non négociables recopiés
    has_overflow = "test_review_keeps_argv_under_system_arg_limit" in text and "FAIL" in text
    has_iterate = (
        "test_iterate_without_worktree_refuses_naming_execute" in text
        and ("SystemExit" in text or "FAIL" in text)
    )
    count = (1 if has_overflow else 0) + (1 if has_iterate else 0)
    return count


def measure_suite_verte() -> tuple[int, int]:
    proc = subprocess.run(
        [sys.executable, "-m", "unittest", "discover", "-s", "control-plane/tests"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    match = re.search(r"Ran (\d+) tests?", proc.stderr + proc.stdout)
    ran = int(match.group(1)) if match else NOT_COMPUTED
    ok = 1 if proc.returncode == 0 else 0
    return ok, ran if isinstance(ran, int) else NOT_COMPUTED


def measure_hors_perimetre() -> tuple[int, int]:
    try:
        # Lot non encore committé : comparer l'arbre de travail à origin/master
        committed = git("diff", f"{BASE_REF}...HEAD", "--name-only")
        unstaged = git("diff", BASE_REF, "--name-only")
        staged = git("diff", "--cached", "--name-only")
    except RuntimeError:
        return NOT_COMPUTED, NOT_COMPUTED
    names = set()
    for blob in (committed, unstaged, staged):
        for line in blob.splitlines():
            line = line.strip()
            if line:
                names.add(line)
    # Untracked deliverables
    try:
        untracked = git(
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            "harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables",
            "control-plane",
            "harness/queue/cost-ledger.jsonl",
        )
        for line in untracked.splitlines():
            if line.strip():
                names.add(line.strip())
    except RuntimeError:
        pass
    outside = []
    for path in sorted(names):
        if path in D4_ALLOWED:
            continue
        if path.startswith(D4_DELIVERABLES_PREFIX):
            continue
        outside.append(path)
    return len(outside), len(names)


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesurés du brief 022")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--write-manifest", action="store_true", default=True)
    parser.add_argument("--no-write-manifest", action="store_true")
    args = parser.parse_args()

    # Ensure worktree control-plane is importable
    cp = str(REPO / "control-plane")
    if cp not in sys.path:
        sys.path.insert(0, cp)

    longueur, octets, absent, bound = measure_sc1()
    report(
        "longueur_argv_max_relecture",
        longueur,
        f"borne système 32*SC_PAGESIZE={bound}",
    )
    report("octets_diff_du_test", octets, f"borne système 32*SC_PAGESIZE={bound}")
    report("prompt_absent_de_argv", absent, "1=absent / 0=présent")

    drap = measure_drapeaux()
    report("drapeaux_claude_inchanges", drap, "1=ordre inchangé / 0=dérivé")

    intact = measure_tests_existants_intacts()
    report(
        "tests_existants_intacts",
        intact,
        "1=six tests d'origine intacts (amendement 001) / 0=touchés",
    )

    no_leak = measure_format_no_leak()
    report(
        "format_invocation_ne_fuit_pas_le_prompt",
        no_leak,
        "1=aperçu sans texte / 0=fuite",
    )

    reuse, refuse, sandbox, no_run = measure_iterate()
    report("iterate_reutilise_worktree", reuse, "1=aucun worktree créé / 0=échec")
    report("iterate_refuse_sans_worktree", refuse, "1=code 2 + nomme execute / 0=échec")
    report("iterate_porte_le_sandbox", sandbox, "1=--sandbox enabled / 0=absent")
    report("iterate_sans_run_ne_lance_rien", no_run, "1=rien exécuté / 0=appelé")

    added, total = measure_tests_ajoutes()
    report("tests_ajoutes", added, f"total tests dans test_workflow.py après lot = {total}")

    rouges = measure_tests_rouges_avant()
    report(
        "tests_rouges_avant_correction",
        rouges,
        "2 non négociables (overflow + iterate sans worktree)",
    )

    suite_ok, suite_ran = measure_suite_verte()
    report(
        "suite_control_plane_verte",
        suite_ok,
        f"tests exécutés = {suite_ran}",
    )

    hors, changed = measure_hors_perimetre()
    report(
        "fichiers_hors_perimetre_modifies",
        hors,
        f"fichiers touchés vs {BASE_REF} = {changed}",
    )

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest and not args.no_write_manifest:
        manifest_path = BRIEF / "deliverables" / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        counters = []
        for name, value, denom in ROWS:
            sample = 1
            if name == "octets_diff_du_test":
                sample = int(value) if isinstance(value, int) and value > 0 else NOT_COMPUTED
            elif name == "longueur_argv_max_relecture":
                sample = bound
            elif name == "tests_ajoutes":
                sample = total
            elif name == "suite_control_plane_verte":
                sample = suite_ran if isinstance(suite_ran, int) and suite_ran > 0 else NOT_COMPUTED
            elif name == "fichiers_hors_perimetre_modifies":
                sample = changed if isinstance(changed, int) and changed > 0 else 1
            elif name == "tests_rouges_avant_correction":
                sample = 2
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

    # Sentinel hygiene
    for name, value, _ in ROWS:
        if value == NOT_COMPUTED:
            print(f"# sentinelle -1 pour {name}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
