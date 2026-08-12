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
import audit_review  # noqa: E402
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


# --- --policy auto (Lot 006a, ADR-0006) ----------------------------------
#
# Unlike _env() above (which writes the ledger events directly), these
# tests go through audit_review.record_challenge so the AUDIT_CHALLENGED
# event carries a real `review` path -- decide_auto() reads verdicts from
# that file, not from the ledger's own verdict *counts*.


def _auto_env(tmp_path, review_body: str):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-abc-topic.md").write_text(AUDIT_DOC, encoding="utf-8")
    reviews = tmp_path / "reviews"
    decisions = tmp_path / "decisions"
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_review.review_path("CURSOR-abc-topic", reviews).parent.mkdir(parents=True, exist_ok=True)
    audit_review.review_path("CURSOR-abc-topic", reviews).write_text(review_body, encoding="utf-8")
    audit_review.record_challenge("CURSOR-abc-topic", inbox=inbox, reviews_dir=reviews, ledger_path=ledger)
    return inbox, reviews, decisions, ledger


REVIEW_CONFIRMED_AND_PARTIAL = """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-05T10:00:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | budget non impose | CONFIRMED | budget.py status exit 2 |
| 2 | split-check | PARTIAL | portee surestimee |
| 3 | style nit | REFUTED | non reproductible |
"""

REVIEW_ALL_REFUTED = """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-05T10:00:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | style nit | REFUTED | non reproductible |
| 2 | perf claim | REFUTED | benchmark manquant |
"""

REVIEW_NEEDS_OWNER_ONLY = """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-05T10:00:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | choix produit | NEEDS_OWNER | arbitrage metier, hors technique |
"""


def test_decide_auto_approves_on_confirmed_or_partial(tmp_path):
    inbox, reviews, decisions, ledger = _auto_env(tmp_path, REVIEW_CONFIRMED_AND_PARTIAL)
    rec = audit_decision.decide_auto(
        "CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger
    )
    assert rec["event"] == "AUDIT_APPROVED"
    assert rec["actor"] == "policy:auto"
    assert rec["retained_points"] == [1, 2]  # CONFIRMED union PARTIAL, REFUTED point 3 excluded
    assert rec["reason"].startswith("policy:")
    assert rec["reason"].strip()  # never blank


def test_decide_auto_rejects_when_all_refuted(tmp_path):
    inbox, reviews, decisions, ledger = _auto_env(tmp_path, REVIEW_ALL_REFUTED)
    rec = audit_decision.decide_auto(
        "CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger
    )
    assert rec["event"] == "AUDIT_REJECTED"
    assert rec["actor"] == "policy:auto"
    assert "retained_points" not in rec
    assert rec["reason"].strip()


def test_decide_auto_rejects_needs_owner_without_confirmed_or_partial(tmp_path):
    inbox, reviews, decisions, ledger = _auto_env(tmp_path, REVIEW_NEEDS_OWNER_ONLY)
    rec = audit_decision.decide_auto(
        "CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger
    )
    assert rec["event"] == "AUDIT_REJECTED"
    # brief 006 gives this exact phrase verbatim -- must be literally present
    assert "policy: no owner in full_auto" in rec["reason"]


REVIEW_MARKDOWN_DECORATED = """---
review_of: CURSOR-abc-topic
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-12T13:54:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | fenetre de critique de 4 s | **PARTIAL** | preuve reproduite |
| 2 | diagnostic H2 faux | **CONFIRMED** | reproduit independamment |
| 3 | classification CI | **PARTIAL — non rejouable ici** | coherent par deduction |
| 8 | secrets non mesurables | **CONFIRMED** (mesurabilité) / **NEEDS_OWNER** (reformulation) | permissions verifiees |
| 9 | style nit | `REFUTED` | non reproductible |
"""


def test_decide_auto_accepts_markdown_decorated_verdicts(tmp_path):
    """The exact shapes the first real headless challenges produced
    (runs 31603872434 / 31603909788, 2026-08-12): verdicts wrapped in
    Markdown bold/backticks, with free text after the token in the same
    cell, and a composite cell whose LEADING token wins. The strict
    pre-fix pattern matched none of these rows and the auto-decision
    stalled on every merged challenge."""
    inbox, reviews, decisions, ledger = _auto_env(tmp_path, REVIEW_MARKDOWN_DECORATED)
    rec = audit_decision.decide_auto(
        "CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger
    )
    assert rec["event"] == "AUDIT_APPROVED"
    # 1 PARTIAL, 2 CONFIRMED, 3 PARTIAL(+texte), 8 CONFIRMED (token de tete
    # de la cellule composite) ; 9 REFUTED exclu.
    assert rec["retained_points"] == [1, 2, 3, 8]


def test_parse_point_verdicts_is_not_a_keyword_hunt():
    """A verdict word buried mid-sentence in the summary or proof cell must
    NOT count as a verdict row -- only leading decoration is allowed before
    the token in its cell. Fail-closed stays fail-closed."""
    prose_only = (
        "| # | Point | Verdict | Preuve |\n"
        "|---|---|---|---|\n"
        "| 1 | l'audit dit CONFIRMED partout | a trancher | la preuve cite REFUTED |\n"
    )
    assert audit_decision.parse_point_verdicts(prose_only) == []

    bare_still_works = "| 4 | point simple | NEEDS_OWNER | arbitrage |\n"
    assert audit_decision.parse_point_verdicts(bare_still_works) == [(4, "NEEDS_OWNER")]


def test_decide_auto_refuses_when_not_challenged(tmp_path):
    inbox, decisions, ledger = _env(tmp_path, challenged=False)  # only PROPOSED, no review
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide_auto("CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger)


def test_decide_auto_refuses_without_review_file(tmp_path, monkeypatch):
    """A CHALLENGED event whose `review` field points nowhere (or is
    missing) must refuse rather than guess -- an auto decision is only
    ever as good as the verdicts it can actually read."""
    inbox, decisions, ledger = _env(tmp_path, challenged=False)
    inbox.mkdir(exist_ok=True)
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_CHALLENGED", ledger_path=ledger)
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide_auto("CURSOR-abc-topic", inbox=inbox, decisions_dir=decisions, ledger_path=ledger)


def test_cli_auto_shows_policy_flag_in_help():
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "auto", "--help"], capture_output=True, text=True
    )
    assert r.returncode == 0, r.stderr
    assert "--policy" in r.stdout
    assert "auto" in r.stdout


def test_cli_auto_approves_exits_zero(tmp_path):
    inbox, reviews, decisions, ledger = _auto_env(tmp_path, REVIEW_CONFIRMED_AND_PARTIAL)
    r = subprocess.run(
        [sys.executable, str(SCRIPT), "auto", "--audit-id", "CURSOR-abc-topic",
         "--inbox", str(inbox), "--decisions", str(decisions), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_APPROVED"
    assert audit_ledger.read_events(ledger)[-1]["actor"] == "policy:auto"
