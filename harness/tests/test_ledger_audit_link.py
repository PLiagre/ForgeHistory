"""
Tests for the optional `audit_id` field on cost-ledger entries -- SC16,
brief 006 Lot 006c.

Hard-won rule 4: prove red first. Before this field existed, a cost-ledger
entry (`harness/queue/cost-ledger.jsonl`) had no way to say which audit
caused a given brief run -- the only link was a human remembering which
brief number came from which audit. These tests prove the field is genuinely
optional (existing callers with no `--audit-id` are byte-identical to
before) AND that it closes the real chain: `audit_ledger`'s own
`AUDIT_CONVERTED.briefs[]` names a brief, and a cost-ledger entry for that
same brief can now carry the same `audit_id` back -- a reader can join the
two files on that value without fuzzy-matching brief paths.
"""
import json
import subprocess
import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
BACKENDS = HARNESS / "backends"
SCRIPT = BACKENDS / "ledger.py"

sys.path.insert(0, str(BACKENDS))
sys.path.insert(0, str(HARNESS))
import ledger  # noqa: E402
import audit_ledger  # noqa: E402
import audit_convert  # noqa: E402


def _read_jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def test_append_entry_without_audit_id_omits_the_field(tmp_path, monkeypatch):
    """Backward compatibility: an ordinary run (no audit provenance) writes
    exactly the same shape as before this lot -- no `audit_id` key at all,
    not an `audit_id: null`."""
    fake_ledger = tmp_path / "cost-ledger.jsonl"
    monkeypatch.setattr(ledger, "LEDGER_PATH", fake_ledger)
    ledger.append_entry("claude", "harness/queue/briefs/999-x", "generator-run")
    entries = _read_jsonl(fake_ledger)
    assert len(entries) == 1
    assert "audit_id" not in entries[0]


def test_append_entry_with_audit_id_carries_the_field(tmp_path, monkeypatch):
    fake_ledger = tmp_path / "cost-ledger.jsonl"
    monkeypatch.setattr(ledger, "LEDGER_PATH", fake_ledger)
    ledger.append_entry("claude", "harness/queue/briefs/007-conv", "generator-run", audit_id="CURSOR-abc-link")
    entries = _read_jsonl(fake_ledger)
    assert entries[0]["audit_id"] == "CURSOR-abc-link"


def test_cli_append_help_documents_audit_id_flag():
    """CLI-contract check only -- does NOT actually invoke `append` here,
    because the CLI has no `--ledger` override and would write to the real
    repo `harness/queue/cost-ledger.jsonl`; the real end-to-end CLI append
    (with a real, intentional demo entry) is exercised by
    harness/pipeline/demo/run_full_auto_demo.sh instead."""
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "append", "--help"],
        capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stderr
    assert "--audit-id" in result.stdout


def test_full_chain_audit_to_brief_to_cost_entry(tmp_path):
    """The real join this field exists for: an APPROVED audit converts to a
    brief (audit_convert.convert -> AUDIT_CONVERTED with briefs=[...]), and a
    cost-ledger entry for that SAME brief, tagged with the SAME audit_id,
    lets a reader trace audit -> brief -> real invocation cost without
    fuzzy-matching paths."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-abc123-full-chain"
    (inbox / f"{audit_id}.md").write_text(
        f"---\naudit_id: {audit_id}\nauditor: cursor-cloud\n"
        f"target_branch: master\ntarget_commit: 0000000000000000000000000000000000000f\n"
        f"---\n# corps\n",
        encoding="utf-8",
    )
    ledger_path = tmp_path / "audit-ledger.jsonl"
    briefs_dir = tmp_path / "briefs"
    for event in ("AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED"):
        audit_ledger.append_event(audit_id, event, ledger_path=ledger_path)

    converted = audit_convert.convert(
        audit_id, inbox=inbox, briefs_dir=briefs_dir, ledger_path=ledger_path,
    )
    brief_rel = converted["briefs"][0]
    assert brief_rel  # non-empty, per audit_convert's own guarantee

    cost_ledger = tmp_path / "cost-ledger.jsonl"

    # Exercise the real append_entry function against this brief + audit_id.
    original_path = ledger.LEDGER_PATH
    try:
        ledger.LEDGER_PATH = cost_ledger
        ledger.append_entry("claude", brief_rel, "generator-run", audit_id=audit_id)
    finally:
        ledger.LEDGER_PATH = original_path

    cost_entries = _read_jsonl(cost_ledger)
    assert cost_entries[0]["audit_id"] == audit_id
    assert cost_entries[0]["brief"] == brief_rel

    # Join: the audit's own CONVERTED event names the same brief the cost
    # entry names, and both carry the same audit_id -- the actual chain.
    converted_events = [e for e in audit_ledger.read_events(ledger_path)
                        if e.get("audit_id") == audit_id and e.get("event") == "AUDIT_CONVERTED"]
    assert converted_events and brief_rel in converted_events[0]["briefs"]
