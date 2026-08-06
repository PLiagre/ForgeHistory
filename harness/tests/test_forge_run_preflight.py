"""
Tests for harness/pipeline/forge_run_preflight.py -- SC15, brief 006 Lot 006c.

Hard-won rule 4: prove red first. `budget.py split-check` on its own is
DELIBERATELY advisory and always exits 0 -- `test_budget_py_itself_stays_
advisory_exit_0_on_needs_split` below proves that is still true (the control
case: this wrapper adds enforcement, it does not depend on budget.py itself
having been made blocking, per the brief's instruction not to rewrite
budget.py destructively). The remaining tests prove the NEW wrapper turns
that same advisory verdict into a blocking exit.
"""
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"
SCRIPT = HARNESS / "pipeline" / "forge_run_preflight.py"
BUDGET_SCRIPT = HARNESS / "budget.py"

BRIEF_MD = """# Brief X: fixture

**Authored**: 2026-08-05T00:00:00Z
**Author**: fixture

## Success Conditions

1. one
2. two
"""


def _make_brief(tmp_path: Path) -> Path:
    brief_dir = tmp_path / "999-fixture"
    brief_dir.mkdir()
    (brief_dir / "brief.md").write_text(BRIEF_MD, encoding="utf-8")
    return brief_dir


def test_budget_py_itself_stays_advisory_exit_0_on_needs_split(tmp_path):
    brief_dir = _make_brief(tmp_path)
    result = subprocess.run(
        [sys.executable, str(BUDGET_SCRIPT), "split-check",
         "--brief", str(brief_dir), "--estimated-calls", "500"],
        capture_output=True, text=True,
    )
    assert result.returncode == 0
    assert "NEEDS_SPLIT" in result.stdout


def test_preflight_blocks_on_needs_split(tmp_path):
    brief_dir = _make_brief(tmp_path)
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--brief", str(brief_dir), "--estimated-calls", "500"],
        capture_output=True, text=True,
    )
    assert result.returncode == 1, result.stdout
    assert "NEEDS_SPLIT" in result.stdout
    assert "BLOCKED" in result.stderr


def test_preflight_allows_size_ok(tmp_path):
    brief_dir = _make_brief(tmp_path)
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--brief", str(brief_dir), "--estimated-calls", "50"],
        capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stdout
    assert "SIZE_OK" in result.stdout
    assert result.stderr == ""


def test_preflight_requires_estimated_calls_flag(tmp_path):
    brief_dir = _make_brief(tmp_path)
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--brief", str(brief_dir)],
        capture_output=True, text=True,
    )
    assert result.returncode not in (0, 1)  # argparse usage error, not a verdict
    assert "--estimated-calls" in result.stderr


def test_preflight_boundary_151_is_blocked_150_is_not(tmp_path):
    brief_dir = _make_brief(tmp_path)
    at_151 = subprocess.run(
        [sys.executable, str(SCRIPT), "--brief", str(brief_dir), "--estimated-calls", "151"],
        capture_output=True, text=True,
    )
    at_150 = subprocess.run(
        [sys.executable, str(SCRIPT), "--brief", str(brief_dir), "--estimated-calls", "150"],
        capture_output=True, text=True,
    )
    assert at_151.returncode == 1
    assert at_150.returncode == 0
