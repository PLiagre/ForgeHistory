"""
Tests for harness/pipeline/full_auto_mode_guard.py -- brief 009 Lot 009a,
SC1/SC2, counters `mode_full_auto_bare_rejected_test_count` and
`mode_full_auto_accepted_when_forgerun_wired_test_count`.

Both branches of the same guard are proven, per the pairing hard-won rule
008a paid for (a guard exercising only one branch is not proven):
  - test_bare_full_auto_refused_while_forgerun_unwired (SC1): against the
    REAL, on-disk `.github/workflows/pipeline-forge-run.yml`, which still
    contains the stub marker today.
  - test_full_auto_accepted_once_forgerun_wired (SC2): against a FIXTURE
    copy of that same file with the stub marker removed -- never the real
    file, which brief 009's own Non-Goals forbid rewiring in this lot.

Hard-won rule 4 (prove red first): `test_stub_marker_still_present_in_real_
forge_run_workflow_control` is the control case -- if a future lot wires
forge-run for real and removes the marker, THIS test goes red first,
loudly, rather than SC1's own test silently starting to pass for the wrong
reason (the real file no longer matching what SC1 claims to prove against).
"""
from __future__ import annotations

import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
from pipeline.full_auto_mode_guard import (  # noqa: E402
    FORGE_RUN_STUB_MARKER,
    ModeGuardError,
    validate_mode,
)

REPO_ROOT = HARNESS.parent
REAL_FORGE_RUN_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "pipeline-forge-run.yml"


def test_stub_marker_still_present_in_real_forge_run_workflow_control():
    """Control case for SC1 -- if this goes red, SC1's own refusal test is
    no longer testing the condition it claims to (forge-run would have been
    wired for real, out of this lot's scope)."""
    text = REAL_FORGE_RUN_WORKFLOW.read_text(encoding="utf-8")
    assert FORGE_RUN_STUB_MARKER in text


def test_bare_full_auto_refused_while_forgerun_unwired():
    """SC1 -- against the REAL repository state (not a fixture), the bare
    `full_auto` value is refused fail-closed: a raised exception, not a
    silent pass-through."""
    import pytest

    with pytest.raises(ModeGuardError, match="full_auto refused"):
        validate_mode("full_auto", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_full_auto_accepted_once_forgerun_wired(tmp_path):
    """SC2 -- companion test proving the SAME guard is not hardcoded to
    refuse `full_auto` forever: a fixture copy of pipeline-forge-run.yml
    with the stub marker removed makes `full_auto` valid again, with no
    code change to the guard itself."""
    real_text = REAL_FORGE_RUN_WORKFLOW.read_text(encoding="utf-8")
    assert FORGE_RUN_STUB_MARKER in real_text  # sanity: fixture actually removes something real

    wired_fixture = tmp_path / "pipeline-forge-run.yml"
    wired_fixture.write_text(real_text.replace(FORGE_RUN_STUB_MARKER, "DONE(operator"), encoding="utf-8")
    assert FORGE_RUN_STUB_MARKER not in wired_fixture.read_text(encoding="utf-8")

    # Must not raise.
    validate_mode("full_auto", forge_run_workflow=wired_fixture)


def test_manual_always_valid():
    validate_mode("manual", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_full_auto_decision_only_always_valid_even_while_forgerun_unwired():
    """The whole point of the split: the new value never depends on
    forge-run's wiring state."""
    validate_mode("full_auto_decision_only", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_unknown_mode_value_refused_fail_closed():
    import pytest

    with pytest.raises(ModeGuardError, match="not a recognised value"):
        validate_mode("full_auto_typo", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_missing_workflow_file_refuses_full_auto_fail_closed(tmp_path):
    """An unreadable/missing forge-run workflow must never be silently
    treated as 'must be fine, allow it' -- fail-closed applies to I/O
    errors too, not only to known-bad values."""
    import pytest

    missing = tmp_path / "does-not-exist.yml"
    with pytest.raises(ModeGuardError, match="could not read"):
        validate_mode("full_auto", forge_run_workflow=missing)


def test_empty_forge_run_workflow_refuses_full_auto_fail_closed(tmp_path):
    """Iteration-2 fix for feedback-009a.md blocker B2: a zero-byte
    `pipeline-forge-run.yml` must NOT be silently accepted as proof
    forge-run is wired. Absence of the stub marker is not the same as
    presence of proof the file is a real, complete workflow -- an empty
    file proves nothing and must refuse in the same "cannot prove
    forge-run is wired" family as the I/O refusal above, not resolve to
    the opposite (permissive) outcome the two failure shapes previously
    disagreed on. Proved red first (see generator-log.md) against the
    iteration-1 module: this assertion failed with `Failed: DID NOT RAISE
    ModeGuardError` because the pre-fix code returned None on an empty
    file."""
    import pytest

    empty = tmp_path / "pipeline-forge-run.yml"
    empty.write_text("", encoding="utf-8")
    with pytest.raises(ModeGuardError, match="empty or has no content"):
        validate_mode("full_auto", forge_run_workflow=empty)


def test_whitespace_only_forge_run_workflow_refuses_full_auto_fail_closed(tmp_path):
    """Same defect, second shape: a workflow file reduced to whitespace
    only (blank lines, spaces, tabs) carries no content either -- it must
    refuse for the same reason an empty file refuses, not be treated as
    a different case. Proved red first against the iteration-1 module:
    `Failed: DID NOT RAISE ModeGuardError`."""
    import pytest

    ws_only = tmp_path / "pipeline-forge-run.yml"
    ws_only.write_text("   \n\n\t  \n", encoding="utf-8")
    with pytest.raises(ModeGuardError, match="empty or has no content"):
        validate_mode("full_auto", forge_run_workflow=ws_only)


def test_truncated_forge_run_workflow_before_jobs_section_refuses_full_auto_fail_closed(tmp_path):
    """Third shape named for iteration 2: a workflow file truncated before
    its `jobs:` section (a partial write, a bad checkout, a disk-full
    truncation cutting off the tail as these failures typically do) is
    non-empty but still does not prove forge-run is wired -- the ORIGINAL
    code accepted any non-empty file lacking the stub marker because it
    only checked for the marker's ABSENCE, never for positive evidence the
    file is a real, complete GitHub Actions workflow. This fixture is (a)
    non-empty/non-whitespace, so it does not merely re-exercise the two
    tests above; (b) does not contain the `TODO(operator` marker, so the
    pre-fix module's absence-only check would have accepted it; and (c)
    deliberately cut before `jobs:`/`runs-on:`/`steps:` all appear, so the
    post-fix positive-evidence check has something concrete to fail on.
    Proved red first against the iteration-1 module: `Failed: DID NOT
    RAISE ModeGuardError`."""
    import pytest

    real_text = REAL_FORGE_RUN_WORKFLOW.read_text(encoding="utf-8")
    jobs_index = real_text.index("jobs:")
    truncated_text = real_text[: jobs_index - 20]
    assert truncated_text.strip(), "truncated fixture must be non-empty for this test to prove anything"
    assert FORGE_RUN_STUB_MARKER not in truncated_text, (
        "truncated fixture must not itself carry the marker -- otherwise "
        "this is indistinguishable from SC1's own refusal case"
    )
    assert "jobs:" not in truncated_text and "runs-on:" not in truncated_text, (
        "truncated fixture must genuinely lack the structural markers the "
        "positive-evidence check looks for, otherwise this test would pass "
        "for the wrong reason"
    )

    truncated = tmp_path / "pipeline-forge-run.yml"
    truncated.write_text(truncated_text, encoding="utf-8")
    with pytest.raises(ModeGuardError, match="does not look like a complete"):
        validate_mode("full_auto", forge_run_workflow=truncated)


def test_non_utf8_forge_run_workflow_raises_mode_guard_error_not_uncaught_exception(tmp_path):
    """Secondary, non-blocking defect named in feedback-009a.md B2 point 3:
    a workflow file that is not valid UTF-8 must refuse via the module's
    OWN published contract (`ModeGuardError`), not leak a raw
    `UnicodeDecodeError` that escapes the documented `except OSError`
    handler. The outcome was already a refusal (uncaught exception is
    non-zero exit, not permissive) -- this test only proves the CONTRACT
    now holds for a caller that catches `ModeGuardError` specifically, per
    the module's own docstring."""
    import pytest

    bad_encoding = tmp_path / "pipeline-forge-run.yml"
    bad_encoding.write_bytes(b"\xff\xfe not valid utf-8 TODO(operator")
    with pytest.raises(ModeGuardError):
        validate_mode("full_auto", forge_run_workflow=bad_encoding)


def test_empty_mode_refused():
    import pytest

    with pytest.raises(ModeGuardError):
        validate_mode("", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_config_yaml_current_mode_is_now_full_auto_decision_only():
    """Regression guard for SC3 -- once this lot rewrites config.yaml, its
    mode must both be the new value AND pass the guard unconditionally."""
    sys.path.insert(0, str(HARNESS / "pipeline"))
    import policy_loader  # noqa: E402

    config = policy_loader.load_flat_yaml(HARNESS / "pipeline" / "config.yaml")
    assert config.get("mode") == "full_auto_decision_only"
    validate_mode(config["mode"], forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)
