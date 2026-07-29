"""
Tests for harness/verdict_audit.py.

Hard-won rule 4: prove red first. Every check below has a fixture that trips
it into FAIL before we trust the honest fixture to pass it. Invokes the
script as a real subprocess (black-box, via `py`), not by importing
internals, so this also exercises the actual CLI contract.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "verdict_audit.py"


def run_audit(brief_dir: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(brief_dir)],
        capture_output=True, text=True,
    )


def build_honest_brief(tmp_path: Path) -> Path:
    bd = tmp_path / "brief_dir"
    (bd / "deliverables").mkdir(parents=True)

    (bd / "brief.md").write_text(
        "# Brief\n\n**Authored**: 2020-01-01T00:00:00\n**Author**: forge-planificateur\n",
        encoding="utf-8",
    )
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2020-01-01T00:00:01\n",
        encoding="utf-8",
    )
    (bd / "deliverables" / "before.txt").write_text("before-state", encoding="utf-8")
    (bd / "deliverables" / "after.txt").write_text("after-state-different", encoding="utf-8")
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n\n"
        "Built the thing, measured 12 provinces from a loaded 12-province "
        "test world via `py province_count.py test-world-12.json`.\n",
        encoding="utf-8",
    )

    manifest = {
        "files": [
            {"path": "deliverables/before.txt"},
            {"path": "deliverables/after.txt", "must_differ_from": "deliverables/before.txt"},
        ],
        "counters": [
            {"name": "province_count", "value": 12, "sample_size": 12,
             "command": "py province_count.py test-world-12.json"},
        ],
        "waivers": [],
    }
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur\n\n"
        "Measured province_count = 12 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )
    return bd


def load_manifest(bd: Path) -> dict:
    return json.loads((bd / "deliverables" / "manifest.json").read_text(encoding="utf-8"))


def save_manifest(bd: Path, manifest: dict) -> None:
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8",
    )


# --- The honest control case: proves the gate isn't a blanket-rejector ---

def test_accept_honest_brief(tmp_path):
    bd = build_honest_brief(tmp_path)
    result = run_audit(bd)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "VERDICT: ACCEPT" in result.stdout


# --- Nine red-case tests, one per check ---

def test_reject_missing_declared_file(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["files"].append({"path": "deliverables/missing.txt"})
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] files_declared_exist" in result.stdout
    assert "VERDICT: REJECT" in result.stdout


def test_reject_stale_deliverable(tmp_path):
    bd = build_honest_brief(tmp_path)
    stale_ts = 1546300800  # 2019-01-01, before brief.md's 2020-01-01 Authored date
    target = bd / "deliverables" / "before.txt"
    os.utime(target, (stale_ts, stale_ts))

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] mtime_after_brief" in result.stdout


def test_reject_captures_identical_when_should_differ(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "deliverables" / "after.txt").write_text("before-state", encoding="utf-8")  # now identical

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] captures_differ_when_should" in result.stdout


def test_reject_waiver_missing_command_or_error(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["waivers"].append({"claim": "cannot measure X in this environment", "command": None, "error": None})
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] waivers_have_command_and_error" in result.stdout


def test_reject_empty_sample_zero(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["sample_size"] = 0
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_empty_sample_pass" in result.stdout


def test_reject_empty_sample_sentinel(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["sample_size"] = -1
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_empty_sample_pass" in result.stdout


def test_reject_untraceable_verdict_number(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur\n\n"
        "Measured province_count = 99 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] verdict_numbers_traceable" in result.stdout


def test_reject_bare_python_alias(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["command"] = "python province_count.py test-world-12.json"
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_bare_python_alias" in result.stdout


def test_reject_self_authored_verdict(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-generateur\n\n"
        "Measured province_count = 12 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout


def test_reject_rubric_written_after_deliverables(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2099-01-01T00:00:00\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] rubric_predates_deliverables" in result.stdout


# --- Internal-error path: never treated as a pass ---

def test_nonexistent_directory_is_internal_error_not_pass(tmp_path):
    result = run_audit(tmp_path / "does-not-exist")
    assert result.returncode == 2
    assert "VERDICT: ACCEPT" not in result.stdout
