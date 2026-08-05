"""
Tests for harness/audit_archive.py -- REJECTED/VERIFIED -> ARCHIVED.

Hard-won rule 4: prove red first. Guarantees under test:

  1. Only a terminal audit (REJECTED or VERIFIED) archives -- an in-flight
     one is refused.
  2. Archiving copies, never moves: the inbox file still exists afterwards
     (inbox is immutable provenance).
  3. The bundle gathers whatever artifacts exist (audit + review + decision).
  4. An existing archive is never clobbered.
"""
import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_archive.py"

sys.path.insert(0, str(HARNESS))
import audit_archive  # noqa: E402
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402

AID = "CURSOR-abc-topic"
AUDIT_DOC = f"""---
audit_id: {AID}
auditor: cursor-cloud
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
status: PROPOSED
---
# corps
"""


def _env(tmp_path, *, final="AUDIT_REJECTED"):
    arch = tmp_path / "architecture"
    inbox = arch / "inbox"; inbox.mkdir(parents=True)
    (inbox / f"{AID}.md").write_text(AUDIT_DOC, encoding="utf-8")
    reviews = arch / "reviews"; reviews.mkdir()
    (reviews / f"CLAUDE-{AID}.md").write_text("# review\n| x | CONFIRMED | y |\n", encoding="utf-8")
    decisions = arch / "decisions"; decisions.mkdir()
    (decisions / f"DECISION-{AID}.md").write_text("# decision\nREJECTED\n", encoding="utf-8")
    archive = arch / "archive"
    ledger = tmp_path / "audit-ledger.jsonl"
    chain = ["AUDIT_PROPOSED", "AUDIT_CHALLENGED"]
    chain += [final] if final in ("AUDIT_REJECTED",) else ["AUDIT_APPROVED", "AUDIT_CONVERTED",
                                                            "AUDIT_IMPLEMENTED", "AUDIT_VERIFIED"]
    for ev in chain:
        audit_ledger.append_event(AID, ev, ledger_path=ledger)
    return inbox, reviews, decisions, archive, ledger


def _archive(inbox, reviews, decisions, archive, ledger):
    return audit_archive.archive(
        AID, inbox=inbox, reviews_dir=reviews, decisions_dir=decisions,
        archive_dir=archive, ledger_path=ledger,
    )


def test_archive_rejected_bundles_all_three(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path, final="AUDIT_REJECTED")
    rec = _archive(inbox, reviews, decisions, archive, ledger)
    assert rec["event"] == "AUDIT_ARCHIVED"
    dest = archive / AID
    assert (dest / f"{AID}.md").exists()
    assert (dest / f"CLAUDE-{AID}.md").exists()
    assert (dest / f"DECISION-{AID}.md").exists()
    assert set(rec["bundled"]) == {f"{AID}.md", f"CLAUDE-{AID}.md", f"DECISION-{AID}.md"}


def test_archive_copies_not_moves(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path)
    _archive(inbox, reviews, decisions, archive, ledger)
    # inbox original still present -- immutable provenance
    assert (inbox / f"{AID}.md").exists()


def test_archive_verified_ok(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path, final="AUDIT_VERIFIED")
    rec = _archive(inbox, reviews, decisions, archive, ledger)
    assert rec["event"] == "AUDIT_ARCHIVED"


def test_archive_refuses_in_flight(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path, final="AUDIT_REJECTED")
    # override: leave it CHALLENGED only
    ledger2 = tmp_path / "l2.jsonl"
    audit_ledger.append_event(AID, "AUDIT_PROPOSED", ledger_path=ledger2)
    audit_ledger.append_event(AID, "AUDIT_CHALLENGED", ledger_path=ledger2)
    with pytest.raises(audit_archive.ArchiveError):
        audit_archive.archive(AID, inbox=inbox, reviews_dir=reviews, decisions_dir=decisions,
                              archive_dir=archive, ledger_path=ledger2)
    assert not (archive / AID).exists()


def test_archive_refuses_clobber(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path)
    _archive(inbox, reviews, decisions, archive, ledger)
    with pytest.raises(audit_archive.ArchiveError):
        _archive(inbox, reviews, decisions, archive, ledger)


def test_archive_advances_state(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path)
    _archive(inbox, reviews, decisions, archive, ledger)
    assert audits_mod.current_state(AID, audit_ledger.read_events(ledger)) == "AUDIT_ARCHIVED"


def test_cli_archive_exits_zero(tmp_path):
    inbox, reviews, decisions, archive, ledger = _env(tmp_path)
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "archive", "--audit-id", AID,
         "--inbox", str(inbox), "--reviews", str(reviews), "--decisions", str(decisions),
         "--archive", str(archive), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_ARCHIVED"


def test_cli_archive_in_flight_exits_two(tmp_path):
    arch = tmp_path / "architecture"
    inbox = arch / "inbox"; inbox.mkdir(parents=True)
    (inbox / f"{AID}.md").write_text(AUDIT_DOC, encoding="utf-8")
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event(AID, "AUDIT_PROPOSED", ledger_path=ledger)
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "archive", "--audit-id", AID,
         "--inbox", str(inbox), "--archive", str(arch / "archive"), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 2
    assert "only a terminal audit" in r.stderr
