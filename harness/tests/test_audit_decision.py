"""
Tests for harness/audit_decision.py -- the owner's CHALLENGED -> APPROVED /
REJECTED verdict.

Hard-won rule 4: prove red first. Each test attacks a way the verdict could
be meaningless or out of order:

  1. Deciding an audit that is not CHALLENGED is refused -- the owner may
     only decide after Claude's challenge.
  2. An empty reason is refused -- a verdict with no rationale is not a
     decision.
  3. A decision is never clobbered -- once given, it is a record.
  4. The happy path writes decisions/DECISION-<id>.md, appends the right
     event, and advances the listed state.
"""
import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_decision.py"

sys.path.insert(0, str(HARNESS))
import audit_decision  # noqa: E402
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402


AUDIT_DOC = """---
audit_id: CURSOR-abc-topic
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
status: PROPOSED
---
# corps
"""


def _env(tmp_path, *, challenged=True):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-abc-topic.md").write_text(AUDIT_DOC, encoding="utf-8")
    decisions = tmp_path / "decisions"
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_PROPOSED", ledger_path=ledger)
    if challenged:
        audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_CHALLENGED", ledger_path=ledger)
    return inbox, decisions, ledger


# --- happy paths --------------------------------------------------------


def test_accept_writes_decision_and_advances(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    rec = audit_decision.decide(
        "CURSOR-abc-topic", "APPROVED", "budget non imposé, à corriger",
        retained=[1, 2, 4], inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
    )
    assert rec["event"] == "AUDIT_APPROVED"
    assert rec["retained_points"] == [1, 2, 4]
    text = audit_decision.decision_path("CURSOR-abc-topic", decisions).read_text(encoding="utf-8")
    assert "APPROVED" in text and "budget non imposé" in text
    rows = audits_mod.build_listing(inbox, ledger)
    assert rows[0]["state"] == "AUDIT_APPROVED"


def test_reject_writes_decision_and_advances(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    rec = audit_decision.decide(
        "CURSOR-abc-topic", "REJECTED", "hors périmètre actuel",
        inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
    )
    assert rec["event"] == "AUDIT_REJECTED"
    rows = audits_mod.build_listing(inbox, ledger)
    assert rows[0]["state"] == "AUDIT_REJECTED"


# --- guards -------------------------------------------------------------


def test_refuses_when_not_challenged(tmp_path):
    inbox, decisions, ledger = _env(tmp_path, challenged=False)  # only PROPOSED
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide(
            "CURSOR-abc-topic", "APPROVED", "raison",
            inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
        )
    assert [e["event"] for e in audit_ledger.read_events(ledger)] == ["AUDIT_PROPOSED"]


def test_refuses_empty_reason(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide(
            "CURSOR-abc-topic", "APPROVED", "   ",
            inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
        )


def test_refuses_unknown_audit(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide(
            "CURSOR-nope", "APPROVED", "raison",
            inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
        )


def test_refuses_to_clobber_decision(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    audit_decision.decide(
        "CURSOR-abc-topic", "APPROVED", "raison 1",
        inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
    )
    # even though state is now APPROVED, prove the file itself is not clobbered:
    # re-challenge path is impossible, so simulate by asserting no-clobber via
    # a fresh CHALLENGED ledger but the existing file.
    ledger2 = tmp_path / "l2.jsonl"
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_PROPOSED", ledger_path=ledger2)
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_CHALLENGED", ledger_path=ledger2)
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide(
            "CURSOR-abc-topic", "REJECTED", "raison 2",
            inbox=inbox, decisions_dir=decisions, ledger_path=ledger2,
        )


def test_invalid_verdict_refused(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide(
            "CURSOR-abc-topic", "MAYBE", "raison",
            inbox=inbox, decisions_dir=decisions, ledger_path=ledger,
        )


# --- CLI ----------------------------------------------------------------


def _run(cmd, tmp_path, ledger, decisions, inbox, *extra):
    return subprocess.run(
        [sys.executable, str(SCRIPT), cmd, "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--decisions", str(decisions), "--ledger", str(ledger), *extra],
        capture_output=True, text=True,
    )


def test_cli_accept_exits_zero(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    r = _run("accept", tmp_path, ledger, decisions, inbox, "--reason", "ok", "--retain", "1,3")
    assert r.returncode == 0, r.stderr
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_APPROVED"
    assert audit_ledger.read_events(ledger)[-1]["retained_points"] == [1, 3]


def test_cli_reject_exits_zero(tmp_path):
    inbox, decisions, ledger = _env(tmp_path)
    r = _run("reject", tmp_path, ledger, decisions, inbox, "--reason", "non")
    assert r.returncode == 0, r.stderr
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_REJECTED"


def test_cli_accept_not_challenged_exits_two(tmp_path):
    inbox, decisions, ledger = _env(tmp_path, challenged=False)
    r = _run("accept", tmp_path, ledger, decisions, inbox, "--reason", "ok")
    assert r.returncode == 2
    assert "not AUDIT_CHALLENGED" in r.stderr
