"""
Tests for harness/audit_convert.py -- APPROVED -> CONVERTED, seeding a brief.

Hard-won rule 4: prove red first. Guarantees under test:

  1. Only an APPROVED audit converts -- a PROPOSED/CHALLENGED one is refused,
     so a brief never springs from an undecided audit.
  2. Numbering continues the existing sequence (001..005 -> 006), never
     collides.
  3. The seed is a brief.md carrying provenance (audit id + retained points)
     and Planificateur <<TODO>> placeholders -- it does NOT fabricate the
     spec.
  4. An existing brief directory is never clobbered.
  5. The ledger records CONVERTED with the brief path, and the audit then
     lists as CONVERTED.
"""
import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_convert.py"

sys.path.insert(0, str(HARNESS))
import audit_convert  # noqa: E402
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402


AUDIT_DOC = """---
audit_id: CURSOR-6231186-execution-budgets
auditor: cursor-cloud
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
status: PROPOSED
---
# corps
"""


def _env(tmp_path, *, state="AUDIT_APPROVED", retained=(1, 2, 4)):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-6231186-execution-budgets.md").write_text(AUDIT_DOC, encoding="utf-8")
    briefs = tmp_path / "briefs"
    briefs.mkdir()
    # pretend 001..005 already exist
    for n in range(1, 6):
        (briefs / f"{n:03d}-existing").mkdir()
    ledger = tmp_path / "audit-ledger.jsonl"
    chain = ["AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED"]
    for ev in chain:
        if ev == "AUDIT_APPROVED" and state != "AUDIT_APPROVED":
            break
        fields = {"retained_points": list(retained)} if ev == "AUDIT_APPROVED" else {}
        audit_ledger.append_event("CURSOR-6231186-execution-budgets", ev, ledger_path=ledger, **fields)
        if ev == state:
            break
    return inbox, briefs, ledger


AID = "CURSOR-6231186-execution-budgets"


# --- helpers ------------------------------------------------------------


def test_slug_strips_cursor_and_sha():
    assert audit_convert.slug_from_audit_id(AID) == "execution-budgets"


def test_next_number_continues_sequence(tmp_path):
    briefs = tmp_path / "briefs"
    briefs.mkdir()
    for n in (1, 2, 5):
        (briefs / f"{n:03d}-x").mkdir()
    assert audit_convert.next_brief_number(briefs) == "006"


# --- convert ------------------------------------------------------------


def test_convert_seeds_brief_with_provenance(tmp_path):
    inbox, briefs, ledger = _env(tmp_path)
    rec = audit_convert.convert(AID, inbox=inbox, briefs_dir=briefs, ledger_path=ledger)
    assert rec["event"] == "AUDIT_CONVERTED"
    brief_path = briefs / "006-execution-budgets" / "brief.md"
    assert brief_path.exists()
    text = brief_path.read_text(encoding="utf-8")
    assert AID in text            # provenance names the audit
    assert "1, 2, 4" in text      # retained points recorded
    assert "<<TODO (planificateur)" in text  # spec NOT fabricated
    assert rec["briefs"][0].endswith("006-execution-budgets")
    # rubric + deliverables dir also seeded
    assert (briefs / "006-execution-budgets" / "eval-rubric.md").exists()
    assert (briefs / "006-execution-budgets" / "deliverables").is_dir()


def test_convert_advances_state(tmp_path):
    inbox, briefs, ledger = _env(tmp_path)
    audit_convert.convert(AID, inbox=inbox, briefs_dir=briefs, ledger_path=ledger)
    rows = audits_mod.build_listing(inbox, ledger)
    assert rows[0]["state"] == "AUDIT_CONVERTED"


def test_convert_refuses_when_not_approved(tmp_path):
    inbox, briefs, ledger = _env(tmp_path, state="AUDIT_CHALLENGED")
    with pytest.raises(audit_convert.ConvertError):
        audit_convert.convert(AID, inbox=inbox, briefs_dir=briefs, ledger_path=ledger)
    # no brief dir created
    assert not (briefs / "006-execution-budgets").exists()


def test_convert_refuses_unknown_audit(tmp_path):
    inbox, briefs, ledger = _env(tmp_path)
    with pytest.raises(audit_convert.ConvertError):
        audit_convert.convert("CURSOR-nope", inbox=inbox, briefs_dir=briefs, ledger_path=ledger)


def test_convert_refuses_to_clobber(tmp_path, monkeypatch):
    # Auto-numbering normally makes the target fresh; the no-clobber guard
    # is the backstop if numbering ever returns a colliding value (a race or
    # a future bug). Force that condition and prove the guard fires.
    inbox, briefs, ledger = _env(tmp_path)
    (briefs / "006-execution-budgets").mkdir()
    monkeypatch.setattr(audit_convert, "next_brief_number", lambda *_a, **_k: "006")
    with pytest.raises(audit_convert.ConvertError):
        audit_convert.convert(AID, inbox=inbox, briefs_dir=briefs, ledger_path=ledger)


def test_custom_slug(tmp_path):
    inbox, briefs, ledger = _env(tmp_path)
    audit_convert.convert(AID, slug="budget-supervisor", inbox=inbox, briefs_dir=briefs, ledger_path=ledger)
    assert (briefs / "006-budget-supervisor" / "brief.md").exists()


# --- CLI ----------------------------------------------------------------


def test_cli_convert_exits_zero(tmp_path):
    inbox, briefs, ledger = _env(tmp_path)
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "convert", "--audit-id", AID,
         "--inbox", str(inbox), "--briefs", str(briefs), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_CONVERTED"


def test_cli_convert_not_approved_exits_two(tmp_path):
    inbox, briefs, ledger = _env(tmp_path, state="AUDIT_PROPOSED")
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "convert", "--audit-id", AID,
         "--inbox", str(inbox), "--briefs", str(briefs), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 2
    assert "not AUDIT_APPROVED" in r.stderr
