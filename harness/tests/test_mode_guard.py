"""
Tests for harness/pipeline/full_auto_mode_guard.py -- brief 009 Lot 009a,
SC1/SC2, counters `mode_full_auto_bare_rejected_test_count` and
`mode_full_auto_accepted_when_forgerun_wired_test_count`.

Both branches of the same guard are proven, per the pairing hard-won rule
008a paid for (a guard exercising only one branch is not proven).

2026-08-12 (ADR-0010): pipeline-forge-run.yml was wired for real (headless
Claude invocation, no stub marker left), which made the original control
case `test_stub_marker_still_present_in_real_forge_run_workflow_control` go
red exactly as its own docstring predicted. The pair is therefore INVERTED,
consciously, in the same commit that rewired the workflow:
  - test_full_auto_accepted_against_real_wired_forge_run: against the REAL,
    on-disk `.github/workflows/pipeline-forge-run.yml`, which no longer
    contains the stub marker.
  - test_bare_full_auto_refused_on_stubbed_fixture (SC1's refusal branch):
    against a FIXTURE copy of that same file with the stub marker put back
    -- proving the guard is not hardcoded to accept `full_auto` forever
    either.

The control case now pins the ABSENCE of the marker in the real file: if a
future edit reintroduced a stub, that control goes red first, loudly, and
`mode: full_auto` in config.yaml would stop being legal.
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


def test_stub_marker_absent_from_real_forge_run_workflow_control():
    """Control case, inverted on 2026-08-12 (ADR-0010): the real workflow
    is wired and carries no stub marker. If this goes red, someone put a
    stub back into pipeline-forge-run.yml -- and `mode: full_auto` in
    config.yaml stops being legal (see test_config_yaml_current_mode below,
    which would go red with it)."""
    text = REAL_FORGE_RUN_WORKFLOW.read_text(encoding="utf-8")
    assert FORGE_RUN_STUB_MARKER not in text


def test_full_auto_accepted_against_real_wired_forge_run():
    """SC2's acceptance branch, now provable against the REAL repository
    state (not a fixture): forge-run carries a real headless invocation,
    so the bare `full_auto` value is legal. Must not raise."""
    validate_mode("full_auto", forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)


def test_bare_full_auto_refused_on_stubbed_fixture(tmp_path):
    """SC1's refusal branch, kept alive on a FIXTURE: a copy of the real
    workflow with the stub marker put back must refuse `full_auto`
    fail-closed -- proving the guard is not hardcoded to accept it forever
    now that the real file is wired."""
    import pytest

    real_text = REAL_FORGE_RUN_WORKFLOW.read_text(encoding="utf-8")
    assert FORGE_RUN_STUB_MARKER not in real_text  # sanity: fixture actually adds something new

    stubbed_fixture = tmp_path / "pipeline-forge-run.yml"
    stubbed_fixture.write_text(
        real_text + f"\n# {FORGE_RUN_STUB_MARKER}, once provisioned): re-stubbed fixture\n",
        encoding="utf-8",
    )
    assert FORGE_RUN_STUB_MARKER in stubbed_fixture.read_text(encoding="utf-8")

    with pytest.raises(ModeGuardError, match="full_auto refused"):
        validate_mode("full_auto", forge_run_workflow=stubbed_fixture)


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


def test_comment_only_structure_markers_refused_full_auto_fail_closed(tmp_path):
    """Iteration-3 fix for feedback-009a-002.md blocker C3, case 1/3.
    Iteration-2's positive-evidence check looked for the substrings
    `jobs:`/`runs-on:` ANYWHERE in the text, including inside a comment.
    A file containing only `# jobs:` / `# runs-on:` as comments (never a
    real YAML key) satisfied that substring search and was silently
    ACCEPTED -- reproduced against the unmodified iteration-2 module, see
    generator-log.md's iteration-3 section for the exact red output. The
    fixed check requires each structure marker to start a real (stripped)
    line, so a commented-out marker no longer counts."""
    import pytest

    comments_only = tmp_path / "pipeline-forge-run.yml"
    comments_only.write_text(
        "# jobs:\n# runs-on:\n# aucune invocation forge-run\n", encoding="utf-8"
    )
    with pytest.raises(ModeGuardError, match="does not look like a complete"):
        validate_mode("full_auto", forge_run_workflow=comments_only)


def test_structure_marker_prefix_garbage_does_not_count_as_yaml_key(tmp_path):
    """Iteration-3 C3 hardening: ``jobs:garbage`` is not the ``jobs:``
    key merely because it shares the same text prefix. The dependency-free
    heuristic accepts only an end-of-line, whitespace, or inline comment
    after a required marker."""
    import pytest

    malformed = tmp_path / "pipeline-forge-run.yml"
    malformed.write_text(
        "name: malformed\njobs:garbage\n  forge:\n    runs-on: ubuntu-latest\n"
        "    steps:\n      - run: echo no-agent\n",
        encoding="utf-8",
    )
    with pytest.raises(ModeGuardError, match="does not look like a complete"):
        validate_mode("full_auto", forge_run_workflow=malformed)


def test_truncated_after_runs_on_before_steps_refused_full_auto_fail_closed(tmp_path):
    """Iteration-3 fix for C3, case 2/3. A file with a REAL, uncommented
    `jobs:`/`runs-on:` pair but truncated before any `steps:` section
    (a partial write cutting off exactly where the real workflow's own
    invocation step would begin) satisfied iteration-2's two-marker
    substring check and was silently ACCEPTED. `steps:` is now a required
    third structural marker (also checked as a real line, not a
    substring), so this truncation is refused."""
    import pytest

    truncated = tmp_path / "pipeline-forge-run.yml"
    truncated.write_text(
        "name: incomplete\njobs:\n  forge:\n    runs-on: ubuntu-latest\n",
        encoding="utf-8",
    )
    with pytest.raises(ModeGuardError, match="does not look like a complete"):
        validate_mode("full_auto", forge_run_workflow=truncated)


def test_structurally_complete_workflow_without_real_invocation_still_accepted_known_heuristic_limit(
    tmp_path,
):
    """Iteration-3, C3, case 3/3 -- the one case that is NOT closed, and is
    documented here rather than silently left uncovered ("la porte se
    rouvrira en silence" -- feedback-009a-002.md). A complete, well-formed
    workflow with real, uncommented `jobs:`/`runs-on:`/`steps:` sections
    whose only step is `run: echo no-agent` still passes this guard: the
    module answers "does this file look like a real, complete GitHub
    Actions workflow, not a corrupted/truncated one", never "does this
    workflow's step actually invoke an agent". Proving the latter is
    exactly Lot 009c SC14's own job (the real headless `claude` CLI
    invocation) -- duplicating that proof here would pre-empt 009c's own
    work, which brief 009's Non-Goals reserve to it. This test pins the
    known, documented limit so a future change cannot silently narrow or
    widen it without this test forcing a conscious update."""
    fake_but_structurally_complete = tmp_path / "pipeline-forge-run.yml"
    fake_but_structurally_complete.write_text(
        "name: fake\njobs:\n  forge:\n    runs-on: ubuntu-latest\n"
        "    steps:\n      - run: echo no-agent\n",
        encoding="utf-8",
    )
    # Must NOT raise -- this is the documented, accepted limitation, not a
    # regression: this module cannot and does not prove semantic wiring.
    validate_mode("full_auto", forge_run_workflow=fake_but_structurally_complete)


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


def test_config_yaml_current_mode_is_now_full_auto():
    """Regression guard, updated 2026-08-12 (ADR-0010): config.yaml now
    declares `full_auto`, and that value must pass the guard against the
    REAL, wired forge-run workflow. If someone re-stubs the workflow, this
    test goes red together with the control case above -- the declaration
    and the wiring can never silently disagree."""
    sys.path.insert(0, str(HARNESS / "pipeline"))
    import policy_loader  # noqa: E402

    config = policy_loader.load_flat_yaml(HARNESS / "pipeline" / "config.yaml")
    assert config.get("mode") == "full_auto"
    validate_mode(config["mode"], forge_run_workflow=REAL_FORGE_RUN_WORKFLOW)
