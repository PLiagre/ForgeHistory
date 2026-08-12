"""Brief 010, lot 010b : backend Codex officiel, mesuré et anti-auto-jugé."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys


REPO_ROOT = Path(__file__).resolve().parents[2]
CURSOR_WRAPPER = REPO_ROOT / "harness" / "backends" / "run_cursor_generator.sh"
CODEX_WRAPPER = REPO_ROOT / "harness" / "backends" / "run_codex_generator.sh"
PREFLIGHT = REPO_ROOT / "harness" / "backends" / "codex_preflight.py"
LEDGER = REPO_ROOT / "harness" / "backends" / "ledger.py"
FORGE_RUN = REPO_ROOT / ".claude" / "commands" / "forge-run.md"


def _usage_signature(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    match = re.search(r"^# Usage: bash \S+ (.+)$", text, re.MULTILINE)
    assert match, f"signature Usage absente de {path}"
    return match.group(1)


def _brief(tmp_path: Path, verdict_author: str) -> Path:
    brief = tmp_path / "brief"
    (brief / "deliverables").mkdir(parents=True)
    (brief / "brief.md").write_text(
        "# Fixture\n\n**Authored**: 2026-08-11T10:00:00\n", encoding="utf-8"
    )
    (brief / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2026-08-11T10:01:00\n", encoding="utf-8"
    )
    (brief / "verdict.md").write_text(
        f"**Author**: {verdict_author}\n\n**Verdict**: ACCEPT\n", encoding="utf-8"
    )
    return brief


def test_codex_and_cursor_wrappers_expose_the_same_argument_signature():
    assert _usage_signature(CODEX_WRAPPER) == _usage_signature(CURSOR_WRAPPER)
    assert _usage_signature(CODEX_WRAPPER) == (
        "<brief_dir> [extra_dirs_colon_separated]"
    )


def test_codex_preflight_refuses_existing_same_actor_verdict_without_writing(tmp_path):
    brief = _brief(tmp_path, "forge-evaluateur-codex")
    before = {p.relative_to(brief): p.read_bytes() for p in brief.rglob("*") if p.is_file()}

    completed = subprocess.run(
        [sys.executable, str(PREFLIGHT), str(brief)],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    assert completed.returncode == 2
    assert "REFUSING TO RUN" in completed.stderr
    assert "same actor" in completed.stderr
    after = {p.relative_to(brief): p.read_bytes() for p in brief.rglob("*") if p.is_file()}
    assert after == before


def test_codex_preflight_accepts_cross_actor_verdict(tmp_path):
    brief = _brief(tmp_path, "forge-evaluateur")
    completed = subprocess.run(
        [sys.executable, str(PREFLIGHT), str(brief)],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    assert completed.returncode == 0, completed.stderr
    assert "PREFLIGHT OK" in completed.stdout


def test_wrapper_runs_preflight_before_any_repository_write():
    text = CODEX_WRAPPER.read_text(encoding="utf-8")
    guard = text.index('"$REPO_ROOT/harness/backends/codex_preflight.py"')
    first_write = min(
        text.index('PROMPT_FILE="$(mktemp)"'),
        text.index('mkdir -p "$BRIEF_DIR/deliverables"'),
    )
    assert guard < first_write


def test_forge_run_names_codex_in_hint_option_and_execution_branch():
    lines = FORGE_RUN.read_text(encoding="utf-8").splitlines()
    mentions = [(number, line) for number, line in enumerate(lines, 1) if "codex" in line]
    assert len(mentions) == 3
    assert "argument-hint" in mentions[0][1]
    assert "--backend claude|cursor|codex" in mentions[1][1]
    assert 'backend == "codex"' in mentions[2][1]


def test_ledger_report_counts_a_real_codex_append_in_isolated_ledger(tmp_path):
    ledger_path = tmp_path / "cost-ledger.jsonl"
    env = os.environ.copy()
    env["FORGE_COST_LEDGER"] = str(ledger_path)
    env["PYTHONIOENCODING"] = "utf-8"

    append = subprocess.run(
        [
            sys.executable,
            str(LEDGER),
            "append",
            "--backend",
            "codex",
            "--brief",
            "fixture-010b",
            "--event",
            "generator-run-test",
        ],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    assert append.returncode == 0, append.stderr

    report = subprocess.run(
        [sys.executable, str(LEDGER), "report"],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    assert report.returncode == 0, report.stderr
    assert re.search(r"^\s*1\s+codex$", report.stdout, re.MULTILINE)
    entry = json.loads(ledger_path.read_text(encoding="utf-8").strip())
    assert entry["backend"] == "codex"
