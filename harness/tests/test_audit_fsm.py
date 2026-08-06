"""
Tests for the FSM transition enforcement added to
harness/audit_ledger.py:append_event (Lot 006a, brief 006).

Hard-won rule 4: prove red first. Before this lot, append_event only
checked `audit_id` non-empty and `event in VALID_EVENTS` -- ANY event was
legal at ANY point in an audit's history, which is exactly the bypass a
post-merge Cursor audit (CURSOR-POSTMERGE-42cb054) flagged: AUDIT_APPROVED
could be appended with no prior AUDIT_CHALLENGED. Every test below failed
before harness/audit_ledger.py grew TRANSITIONS + the FSM check in
append_event (confirmed by running this suite against the pre-006a module:
each adversarial case appended successfully instead of raising). They now
prove the bypass is closed and the legal happy path still works.

Each test uses a tmp_path ledger -- never architecture/audit-ledger.jsonl.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_ledger.py"

sys.path.insert(0, str(HARNESS))
import audit_ledger  # noqa: E402


# --- (a) APPROVED without prior CHALLENGED --------------------------------


def test_approved_without_challenged_raises_from_fresh_audit(tmp_path):
    """The literal bypass named in the brief: a fresh audit_id (zero prior
    events) appending AUDIT_APPROVED directly must raise, not append."""
    ledger = tmp_path / "audit-ledger.jsonl"
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-bypass", "AUDIT_APPROVED", ledger_path=ledger)
    assert audit_ledger.read_events(ledger) == []


def test_approved_without_challenged_raises_from_proposed(tmp_path):
    """Same bypass, one step later: PROPOSED -> APPROVED skips the
    challenge entirely and must also raise."""
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-bypass2", "AUDIT_PROPOSED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-bypass2", "AUDIT_APPROVED", ledger_path=ledger)
    assert [e["event"] for e in audit_ledger.read_events(ledger)] == ["AUDIT_PROPOSED"]


# --- (b) CHALLENGED without prior PROPOSED --------------------------------


def test_challenged_without_prior_state_is_legal_bootstrap(tmp_path):
    """NOT an adversarial case: per auto_policy.yaml (AUDIT_PROPOSED is
    documented optional) and audits.py's DEFAULT_STATE, a fresh audit_id's
    FIRST ledger event may legitimately be AUDIT_CHALLENGED -- this is what
    audit_review.record_challenge relies on for every audit that was never
    explicitly proposed in the ledger. Recorded here so the adversarial
    case below is not mistaken for this one."""
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-bootstrap", "AUDIT_CHALLENGED", ledger_path=ledger)
    assert [e["event"] for e in audit_ledger.read_events(ledger)] == ["AUDIT_CHALLENGED"]


def test_challenged_after_approved_raises(tmp_path):
    """The real adversarial case for (b): CHALLENGED is not a legal
    successor of APPROVED -- an audit cannot be "re-challenged" after the
    owner (or policy) already approved it."""
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-rechallenge", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-rechallenge", "AUDIT_CHALLENGED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-rechallenge", "AUDIT_APPROVED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-rechallenge", "AUDIT_CHALLENGED", ledger_path=ledger)


# --- (c) full happy path succeeds -----------------------------------------


def test_full_happy_path_succeeds(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    chain = [
        "AUDIT_PROPOSED",
        "AUDIT_CHALLENGED",
        "AUDIT_APPROVED",
        "AUDIT_CONVERTED",
        "AUDIT_IMPLEMENTED",
        "AUDIT_VERIFIED",
        "AUDIT_ARCHIVED",
    ]
    for event in chain:
        audit_ledger.append_event("CURSOR-happy", event, ledger_path=ledger)
    events = audit_ledger.read_events(ledger)
    assert [e["event"] for e in events] == chain


# --- (d) event after a terminal state -------------------------------------


def test_event_after_archived_raises(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    chain = ["AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_REJECTED", "AUDIT_ARCHIVED"]
    for event in chain:
        audit_ledger.append_event("CURSOR-terminal", event, ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-terminal", "AUDIT_STALE", ledger_path=ledger)
    assert [e["event"] for e in audit_ledger.read_events(ledger)] == chain


def test_event_after_rejected_without_archive_raises(tmp_path):
    """REJECTED is not itself terminal in this FSM (it must still reach
    ARCHIVED), but nothing except ARCHIVED may follow it -- proves REJECTED
    cannot silently become APPROVED, e.g. after a policy re-run."""
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-rejected-only", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-rejected-only", "AUDIT_CHALLENGED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-rejected-only", "AUDIT_REJECTED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-rejected-only", "AUDIT_APPROVED", ledger_path=ledger)


# --- (e) unknown/typo event still raises -----------------------------------


def test_unknown_event_still_raises_even_with_valid_prior_state(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-typo", "AUDIT_PROPOSED", ledger_path=ledger)
    with pytest.raises(ValueError):
        audit_ledger.append_event("CURSOR-typo", "AUDIT_CHALENGED", ledger_path=ledger)  # typo
    assert [e["event"] for e in audit_ledger.read_events(ledger)] == ["AUDIT_PROPOSED"]


# --- extra adversarial coverage --------------------------------------------


def test_converted_before_approved_raises(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-skip", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-skip", "AUDIT_CHALLENGED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-skip", "AUDIT_CONVERTED", ledger_path=ledger)


def test_verified_before_implemented_raises(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    chain = ["AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED", "AUDIT_CONVERTED"]
    for event in chain:
        audit_ledger.append_event("CURSOR-skip2", event, ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-skip2", "AUDIT_VERIFIED", ledger_path=ledger)


def test_two_different_audit_ids_do_not_interfere(tmp_path):
    """The FSM state is per audit_id -- one audit_id's APPROVED must not
    make a DIFFERENT audit_id's APPROVED legal without its own CHALLENGED."""
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-a", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-a", "AUDIT_CHALLENGED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-a", "AUDIT_APPROVED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event("CURSOR-b", "AUDIT_APPROVED", ledger_path=ledger)


# --- CLI contract: prove the same bypass is closed at the CLI, too --------


def test_cli_append_approved_without_challenged_exits_nonzero(tmp_path):
    ledger = tmp_path / "audit-ledger.jsonl"
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "append", "--audit-id", "CURSOR-cli-bypass",
         "--event", "AUDIT_APPROVED", "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert result.returncode == 2, result.stdout
    assert "invalid transition" in result.stderr
    assert audit_ledger.read_events(ledger) == []
