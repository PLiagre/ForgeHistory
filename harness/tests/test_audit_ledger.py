"""
Tests for harness/audit_ledger.py -- the append-only audit-loop ledger.

Hard-won rule 4: prove red first. Each test breaks a specific guarantee if
the code stops holding it:

  1. An unknown event is refused. A silently-accepted typo would become a
     permanent, unqueryable line -- the ledger's whole value is that every
     `event` is one of the known AUDIT_* states.
  2. A blank audit_id is refused. audit_id is the join key back to the
     brief; a blank one is an orphaned transition no query can attribute.
  3. Append never rewrites history. Two appends leave two lines with the
     first byte-for-byte intact -- a ledger that mutates prior lines is not
     a ledger.
  4. Extra fields (briefs, verdicts, ...) survive the round trip, because
     the CONVERTED->brief link lives entirely in those pass-through fields.

Mixes direct calls into the pure functions with black-box subprocess runs
of the real CLI (the contract other steps and the future CI will invoke).
"""
import json
import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_ledger.py"

sys.path.insert(0, str(HARNESS))
import audit_ledger  # noqa: E402


# --- direct calls -------------------------------------------------------


def test_append_then_read_round_trips(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event(
        "CURSOR-abc-topic", "AUDIT_PROPOSED", ledger_path=ledger
    )
    events = audit_ledger.read_events(ledger)
    assert len(events) == 1
    assert events[0]["audit_id"] == "CURSOR-abc-topic"
    assert events[0]["event"] == "AUDIT_PROPOSED"
    assert events[0]["timestamp"].endswith("Z")


def test_unknown_event_is_refused(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    with pytest.raises(ValueError):
        audit_ledger.append_event("CURSOR-abc", "NOT_A_STATE", ledger_path=ledger)
    # nothing was written
    assert audit_ledger.read_events(ledger) == []


def test_blank_audit_id_is_refused(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    with pytest.raises(ValueError):
        audit_ledger.append_event("   ", "AUDIT_PROPOSED", ledger_path=ledger)
    assert audit_ledger.read_events(ledger) == []


def test_append_is_append_only(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event(
        "CURSOR-abc", "AUDIT_PROPOSED", ledger_path=ledger, timestamp="2026-08-05T10:00:00Z"
    )
    first_bytes = ledger.read_bytes()
    audit_ledger.append_event(
        "CURSOR-abc", "AUDIT_CHALLENGED", ledger_path=ledger, actor="claude"
    )
    events = audit_ledger.read_events(ledger)
    assert [e["event"] for e in events] == ["AUDIT_PROPOSED", "AUDIT_CHALLENGED"]
    # the first line is preserved verbatim as the new file's prefix
    assert ledger.read_bytes().startswith(first_bytes)


def test_extra_fields_survive_round_trip(tmp_path):
    # Lot 006a: append_event now enforces the FSM (see TRANSITIONS), so
    # AUDIT_CONVERTED can no longer be appended as a bare first event --
    # get there via the real legal chain, then check the CONVERTED event's
    # own extra fields round-trip. The assertion this test protects
    # (extra kwargs survive) is unchanged; only the setup is now FSM-legal.
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-abc", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-abc", "AUDIT_CHALLENGED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-abc", "AUDIT_APPROVED", ledger_path=ledger)
    audit_ledger.append_event(
        "CURSOR-abc",
        "AUDIT_CONVERTED",
        ledger_path=ledger,
        actor="owner",
        briefs=["harness/queue/briefs/006-budget-supervisor"],
        retained_points=[1, 2, 4],
    )
    events = audit_ledger.read_events(ledger)
    event = events[-1]
    assert event["event"] == "AUDIT_CONVERTED"
    assert event["briefs"] == ["harness/queue/briefs/006-budget-supervisor"]
    assert event["retained_points"] == [1, 2, 4]


def test_missing_ledger_reads_as_empty_history(tmp_path):
    assert audit_ledger.read_events(tmp_path / "does-not-exist.jsonl") == []


def test_all_nine_states_are_accepted(tmp_path):
    # Lot 006a: append_event now enforces the FSM, so the nine event NAMES
    # can no longer be walked in one arbitrary sequence on a single
    # audit_id (that was never a real transition order, just a check that
    # each name individually passed the VALID_EVENTS charset check). Prove
    # the same thing -- every one of the nine names is accepted, none is
    # spuriously rejected -- by walking three FSM-legal chains that between
    # them touch all nine states at least once.
    ledger = tmp_path / "audit-ledger.jsonl"
    happy_path = [
        "AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED",
        "AUDIT_CONVERTED", "AUDIT_IMPLEMENTED", "AUDIT_VERIFIED", "AUDIT_ARCHIVED",
    ]
    for event in happy_path:
        audit_ledger.append_event("CURSOR-happy", event, ledger_path=ledger)

    rejected_path = ["AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_REJECTED", "AUDIT_ARCHIVED"]
    for event in rejected_path:
        audit_ledger.append_event("CURSOR-rejected", event, ledger_path=ledger)

    stale_path = ["AUDIT_PROPOSED", "AUDIT_STALE", "AUDIT_ARCHIVED"]
    for event in stale_path:
        audit_ledger.append_event("CURSOR-stale", event, ledger_path=ledger)

    seen = {e["event"] for e in audit_ledger.read_events(ledger)}
    assert seen == set(audit_ledger.VALID_EVENTS)
    assert len(audit_ledger.read_events(ledger)) == len(happy_path) + len(rejected_path) + len(stale_path)


# --- CLI contract (subprocess) ------------------------------------------


def _run(*args, ledger):
    return subprocess.run(
        [sys.executable, str(SCRIPT), *args, "--ledger", str(ledger)],
        capture_output=True,
        text=True,
    )


def test_cli_append_exits_zero_and_writes(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    result = _run(
        "append", "--audit-id", "CURSOR-abc", "--event", "AUDIT_PROPOSED", ledger=ledger
    )
    assert result.returncode == 0, result.stderr
    line = json.loads(result.stdout.strip())
    assert line["audit_id"] == "CURSOR-abc"
    assert audit_ledger.read_events(ledger)[0]["event"] == "AUDIT_PROPOSED"


def test_cli_rejects_unknown_event_nonzero(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    result = _run(
        "append", "--audit-id", "CURSOR-abc", "--event", "BOGUS", ledger=ledger
    )
    assert result.returncode == 2
    assert "unknown event" in result.stderr
    assert audit_ledger.read_events(ledger) == []


def test_cli_set_passes_extra_fields(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    result = _run(
        "append",
        "--audit-id",
        "CURSOR-abc",
        "--event",
        "AUDIT_CHALLENGED",
        "--set",
        "actor=claude",
        ledger=ledger,
    )
    assert result.returncode == 0, result.stderr
    assert audit_ledger.read_events(ledger)[0]["actor"] == "claude"


def test_cli_show_filters_by_audit_id(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-a", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-b", "AUDIT_PROPOSED", ledger_path=ledger)
    result = _run("show", "--audit-id", "CURSOR-b", ledger=ledger)
    assert result.returncode == 0, result.stderr
    lines = [json.loads(x) for x in result.stdout.splitlines() if x.strip()]
    assert len(lines) == 1
    assert lines[0]["audit_id"] == "CURSOR-b"
