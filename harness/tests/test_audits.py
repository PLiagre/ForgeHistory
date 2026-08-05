"""
Tests for harness/audits.py -- the read-only audit listing.

Hard-won rule 4: prove red first. The guarantees under test:

  1. Current state comes from the LEDGER, not the file. An audit whose file
     still says `status: PROPOSED` but whose ledger last says CHALLENGED
     must list as CHALLENGED. This is the whole "single source of truth"
     point -- if state were read from the file, this test would fail.
  2. The default state is PROPOSED for an audit with no ledger events, so a
     freshly-dropped audit is never invisible.
  3. Frontmatter parsing stops at the closing fence and coerces booleans,
     so `implementation_authorized: false` is a bool the CI can check.
  4. A missing inbox is an empty listing, not a crash.
"""
import json
import subprocess
import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
SCRIPT = HARNESS / "audits.py"

sys.path.insert(0, str(HARNESS))
import audits  # noqa: E402
import audit_ledger  # noqa: E402


AUDIT_DOC = """---
audit_id: CURSOR-abc-topic
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
---

# 1. Résumé
target_commit: NOT-THIS-ONE  (this line is body, must be ignored)
"""


def _make_inbox(tmp_path):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-abc-topic.md").write_text(AUDIT_DOC, encoding="utf-8")
    return inbox


# --- frontmatter parsing ------------------------------------------------


def test_frontmatter_stops_at_fence_and_coerces_bool():
    meta = audits.parse_frontmatter(AUDIT_DOC)
    assert meta["audit_id"] == "CURSOR-abc-topic"
    # the body line with the same key must NOT overwrite the real value
    assert meta["target_commit"] == "623118671dd98543a197b06415a240b9912999af"
    assert meta["implementation_authorized"] is False


def test_no_frontmatter_returns_empty():
    assert audits.parse_frontmatter("# just a heading\n") == {}


# --- state resolution ---------------------------------------------------


def test_default_state_is_proposed(tmp_path):
    inbox = _make_inbox(tmp_path)
    ledger = tmp_path / "audit-ledger.jsonl"  # never created
    rows = audits.build_listing(inbox, ledger)
    assert len(rows) == 1
    assert rows[0]["state"] == "AUDIT_PROPOSED"


def test_state_comes_from_ledger_not_file(tmp_path):
    inbox = _make_inbox(tmp_path)
    ledger = tmp_path / "audit-ledger.jsonl"
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_PROPOSED", ledger_path=ledger)
    audit_ledger.append_event("CURSOR-abc-topic", "AUDIT_CHALLENGED", ledger_path=ledger)
    rows = audits.build_listing(inbox, ledger)
    # the file still says status: PROPOSED, but the ledger's last word wins
    assert rows[0]["state"] == "AUDIT_CHALLENGED"


def test_only_latest_event_wins(tmp_path):
    inbox = _make_inbox(tmp_path)
    ledger = tmp_path / "audit-ledger.jsonl"
    for state in ["AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_REJECTED"]:
        audit_ledger.append_event("CURSOR-abc-topic", state, ledger_path=ledger)
    rows = audits.build_listing(inbox, ledger)
    assert rows[0]["state"] == "AUDIT_REJECTED"


def test_missing_inbox_is_empty(tmp_path):
    assert audits.build_listing(tmp_path / "nope", tmp_path / "l.jsonl") == []


# --- CLI contract -------------------------------------------------------


def test_cli_list_text_shows_audit(tmp_path):
    inbox = _make_inbox(tmp_path)
    ledger = tmp_path / "audit-ledger.jsonl"
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "list", "--inbox", str(inbox), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stderr
    assert "CURSOR-abc-topic" in result.stdout
    assert "AUDIT_PROPOSED" in result.stdout


def test_cli_list_json_is_parseable(tmp_path):
    inbox = _make_inbox(tmp_path)
    ledger = tmp_path / "audit-ledger.jsonl"
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "list", "--json", "--inbox", str(inbox), "--ledger", str(ledger)],
        capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stderr
    rows = json.loads(result.stdout)
    assert rows[0]["audit_id"] == "CURSOR-abc-topic"
    assert rows[0]["state"] == "AUDIT_PROPOSED"


def test_cli_empty_inbox_reports_none(tmp_path):
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "list", "--inbox", str(tmp_path / "nope"),
         "--ledger", str(tmp_path / "l.jsonl")],
        capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stderr
    assert "No audits" in result.stdout
