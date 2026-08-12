"""
Tests for harness/audit_review.py -- the PROPOSED -> CHALLENGED gate.

Hard-won rule 4: prove red first. The gate exists so that the ledger event
AUDIT_CHALLENGED cannot be a lie. Each test attacks one way it could be:

  1. record on an unfilled scaffold (<<TODO>> still present) is refused --
     otherwise an empty template would count as a real review.
  2. record on a review with no verdict token is refused -- a challenge
     with no CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER is not a challenge.
  3. record on an audit that is not PROPOSED is refused -- the state
     machine only allows this one transition here.
  4. record on an unknown audit is refused -- no inbox file, nothing to
     challenge.
  5. scaffold never clobbers a review already in progress.

Only the happy path (a filled review) appends the event, and only then does
the audit list as CHALLENGED.
"""
import subprocess
import sys
from pathlib import Path

import pytest

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audit_review.py"

sys.path.insert(0, str(HARNESS))
import audit_review  # noqa: E402
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402


AUDIT_DOC = """---
audit_id: CURSOR-abc-topic
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
audit_type: architecture-and-qa
status: PROPOSED
---

# corps
"""

FILLED_REVIEW = """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: 623118671dd98543a197b06415a240b9912999af
reviewed_at: 2026-08-05T10:00:00Z
---

# Contre-audit

## Verdicts
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | budget non imposé | CONFIRMED | `budget.py status` exit 2 |
| 2 | split-check | PARTIAL | portee surestimee |
"""


def _env(tmp_path):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-abc-topic.md").write_text(AUDIT_DOC, encoding="utf-8")
    reviews = tmp_path / "reviews"
    ledger = tmp_path / "audit-ledger.jsonl"
    return inbox, reviews, ledger


# --- scaffold -----------------------------------------------------------


def test_scaffold_writes_template_with_placeholders(tmp_path):
    inbox, reviews, _ = _env(tmp_path)
    path = audit_review.write_scaffold("CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews)
    text = path.read_text(encoding="utf-8")
    assert "CURSOR-abc-topic" in text
    assert "<<TODO" in text  # unfilled by construction


def test_scaffold_refuses_unknown_audit(tmp_path):
    inbox, reviews, _ = _env(tmp_path)
    with pytest.raises(audit_review.ReviewError):
        audit_review.write_scaffold("CURSOR-nope", inbox=inbox, reviews_dir=reviews)


def test_scaffold_refuses_to_clobber(tmp_path):
    inbox, reviews, _ = _env(tmp_path)
    audit_review.write_scaffold("CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews)
    with pytest.raises(audit_review.ReviewError):
        audit_review.write_scaffold("CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews)


# --- record: the gate ---------------------------------------------------


def test_record_refuses_unfilled_scaffold(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    audit_review.write_scaffold("CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews)
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )
    assert audit_ledger.read_events(ledger) == []


def test_record_refuses_review_without_verdict(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    reviews.mkdir()
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(
        "# Contre-audit\n\nRien de mesuré, aucun verdict ici.\n", encoding="utf-8"
    )
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )
    assert audit_ledger.read_events(ledger) == []


def test_record_refuses_rows_the_auto_decision_cannot_read(tmp_path):
    """The bb8fe11 shape (2026-08-12): verdict words present, but the rows
    are numbered `§1` / `P1-1` instead of the scaffold's bare `| 1 |`.
    decide_auto could never parse them, so record must refuse NOW -- in
    front of the actor who can fix the table -- instead of logging a
    CHALLENGED the loop chokes on after merge."""
    inbox, reviews, ledger = _env(tmp_path)
    reviews.mkdir()
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(
        """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-12T14:00:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| §1 | classification CI | **PARTIAL** | logique confirmee |
| P1-1 | fusion sans preuve lue | **CONFIRMED** | delai mesure |
""",
        encoding="utf-8",
    )
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )
    assert audit_ledger.read_events(ledger) == []


def test_record_refuses_missing_review(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )


def test_record_refuses_unknown_audit(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-nope", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )


def test_record_refuses_when_not_proposed(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    reviews.mkdir()
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(FILLED_REVIEW, encoding="utf-8")
    # advance the audit past PROPOSED
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_CHALLENGED", ledger_path=ledger)
    with pytest.raises(audit_review.ReviewError):
        audit_review.record_challenge(
            "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
        )


def test_record_happy_path_appends_and_advances_state(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    reviews.mkdir()
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(FILLED_REVIEW, encoding="utf-8")
    record = audit_review.record_challenge(
        "CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger
    )
    assert record["event"] == "AUDIT_CHALLENGED"
    assert record["verdicts"] == {"CONFIRMED": 1, "PARTIAL": 1}
    # the listing now resolves the audit to CHALLENGED
    rows = audits_mod.build_listing(inbox, ledger)
    assert rows[0]["state"] == "AUDIT_CHALLENGED"


# --- CLI ----------------------------------------------------------------


def test_cli_scaffold_then_record(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    r1 = subprocess.run(
        [sys.executable, str(SCRIPT), "scaffold", "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--reviews", str(reviews)],
        capture_output=True, text=True,
    )
    assert r1.returncode == 0, r1.stderr
    # fill it
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(FILLED_REVIEW, encoding="utf-8")
    r2 = subprocess.run(
        [sys.executable, str(SCRIPT), "record", "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--reviews", str(reviews), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r2.returncode == 0, r2.stderr
    assert audit_ledger.read_events(ledger)[0]["event"] == "AUDIT_CHALLENGED"


def test_cli_record_unfilled_exits_nonzero(tmp_path):
    inbox, reviews, ledger = _env(tmp_path)
    subprocess.run(
        [sys.executable, str(SCRIPT), "scaffold", "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--reviews", str(reviews)],
        capture_output=True, text=True,
    )
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "record", "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--reviews", str(reviews), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 2
    assert "placeholder" in r.stderr.lower()
