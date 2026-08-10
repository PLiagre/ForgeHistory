"""
Tests for harness/pipeline/orchestrator.py -- the deterministic dispatcher
that replaces the owner's accept/reject step in `mode: full_auto`.

Hard-won rule 4: prove red first. What must be true, each proven directly
against the live module (not narrated):

  1. `--help` works (rubric SC6's own check).
  2. An event with no matching auto_policy.yaml rule is refused, not
     silently accepted.
  3. review_recorded actually calls audit_decision.decide_auto and the
     ledger gains the event decide_auto would have written directly.
  4. evaluateur_pass appends AUDIT_IMPLEMENTED then AUDIT_VERIFIED via
     audit_ledger.append_event -- and, critically, CANNOT be used to skip
     the FSM: calling it on a freshly-PROPOSED audit (no CHALLENGED/
     APPROVED/CONVERTED first) fails with the same TransitionError the
     ledger itself raises, proving the orchestrator has no side door around
     Lot 006a's single choke point.
  5. audit_pr_merge is idempotent/optional: a second call on an audit that
     already has a state is a no-op, never a duplicate/contradicting event.
"""
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"
SCRIPT = HARNESS / "pipeline" / "orchestrator.py"

sys.path.insert(0, str(HARNESS))
import audit_ledger  # noqa: E402
import audit_review  # noqa: E402
from pipeline import orchestrator  # noqa: E402

AUDIT_DOC = """---
audit_id: CURSOR-abc-orch
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-05T10:00:00Z
status: PROPOSED
---
# corps
"""

REVIEW_APPROVED = """---
review_of: CURSOR-abc-orch
reviewer: claude-code
target_commit: x
reviewed_at: 2026-08-05T10:05:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | budget non impose | CONFIRMED | budget.py status exit 2 |
"""


def _challenged_env(tmp_path):
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / "CURSOR-abc-orch.md").write_text(AUDIT_DOC, encoding="utf-8")
    reviews = tmp_path / "reviews"
    decisions = tmp_path / "decisions"
    ledger = tmp_path / "ledger.jsonl"
    audit_review.review_path("CURSOR-abc-orch", reviews).parent.mkdir(parents=True, exist_ok=True)
    audit_review.review_path("CURSOR-abc-orch", reviews).write_text(REVIEW_APPROVED, encoding="utf-8")
    audit_review.record_challenge("CURSOR-abc-orch", inbox=inbox, reviews_dir=reviews, ledger_path=ledger)
    return inbox, reviews, decisions, ledger


def test_cli_help_exits_zero():
    r = subprocess.run([sys.executable, str(SCRIPT), "--help"], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    r2 = subprocess.run([sys.executable, str(SCRIPT), "run", "--help"], capture_output=True, text=True)
    assert r2.returncode == 0, r2.stderr
    assert "--event" in r2.stdout


def test_unknown_event_refused(tmp_path):
    ledger = tmp_path / "ledger.jsonl"
    with pytest.raises(orchestrator.OrchestratorError):
        orchestrator.run_event("not_a_real_event", {}, ledger_path=ledger)


def test_review_recorded_routes_through_decide_auto(tmp_path):
    inbox, reviews, decisions, ledger = _challenged_env(tmp_path)
    outcome = orchestrator.run_event(
        "review_recorded", {"audit_id": "CURSOR-abc-orch"},
        ledger_path=ledger, inbox=inbox, decisions_dir=decisions,
    )
    assert outcome["action"] == "decide_auto"
    assert outcome["record"]["event"] == "AUDIT_APPROVED"
    assert audit_ledger.read_events(ledger)[-1]["event"] == "AUDIT_APPROVED"


def test_evaluateur_pass_cannot_skip_fsm(tmp_path):
    """The disqualifying case: appending IMPLEMENTED/VERIFIED before the
    audit ever reached CONVERTED must fail exactly like a direct
    audit_ledger bypass attempt would -- proving orchestrator.py has no
    side door around the Lot 006a FSM."""
    ledger = tmp_path / "ledger.jsonl"
    audit_ledger.append_event("CURSOR-skip", "AUDIT_PROPOSED", ledger_path=ledger)
    with pytest.raises(audit_ledger.TransitionError):
        orchestrator.run_event("evaluateur_pass", {"audit_id": "CURSOR-skip"}, ledger_path=ledger)
    # ledger must NOT have gained IMPLEMENTED/VERIFIED despite the attempt
    events = [e["event"] for e in audit_ledger.read_events(ledger)]
    assert events == ["AUDIT_PROPOSED"]


def test_evaluateur_pass_happy_path_appends_implemented_then_verified(tmp_path):
    ledger = tmp_path / "ledger.jsonl"
    for event in ("AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED", "AUDIT_CONVERTED"):
        audit_ledger.append_event("CURSOR-happy", event, ledger_path=ledger)
    outcome = orchestrator.run_event("evaluateur_pass", {"audit_id": "CURSOR-happy"}, ledger_path=ledger)
    events = [e["event"] for e in audit_ledger.read_events(ledger)]
    assert events[-2:] == ["AUDIT_IMPLEMENTED", "AUDIT_VERIFIED"]
    assert outcome["action"] == "ledger_append_chain"


def test_audit_pr_merge_is_idempotent(tmp_path):
    ledger = tmp_path / "ledger.jsonl"
    first = orchestrator.run_event("audit_pr_merge", {"audit_id": "CURSOR-idem"}, ledger_path=ledger)
    assert first["action"] == "ledger_append"
    second = orchestrator.run_event("audit_pr_merge", {"audit_id": "CURSOR-idem"}, ledger_path=ledger)
    assert second["action"] == "no_op"
    events = [e["event"] for e in audit_ledger.read_events(ledger)]
    assert events == ["AUDIT_PROPOSED"]  # not duplicated


def test_missing_required_field_refused(tmp_path):
    ledger = tmp_path / "ledger.jsonl"
    with pytest.raises(orchestrator.OrchestratorError):
        orchestrator.run_event("evaluateur_pass", {}, ledger_path=ledger)


def test_gate_reject_escalates_only_at_streak_three(tmp_path):
    below = orchestrator.run_event(
        "gate_reject", {"brief_dir": "harness/queue/briefs/x", "reject_streak": 2},
    )
    assert below["action"] == "no_op"
    at_threshold = orchestrator.run_event(
        "gate_reject", {"brief_dir": "harness/queue/briefs/x", "reject_streak": 3},
    )
    assert at_threshold["action"] == "escalate_pipeline_stuck"


def test_pipeline_job_failed_escalates_same_as_gate_reject_streak():
    """Brief 008, Lot 008b, SC8 (fixes ARCH-003): a `pipeline_job_failed`
    event, unconditionally (no streak needed -- the machine itself broke),
    dispatches to the SAME `action` the 3-in-a-row `gate_reject` streak
    already returns above, proving the two escalation paths converge on one
    human-visible outcome."""
    outcome = orchestrator.run_event(
        "pipeline_job_failed",
        {
            "workflow_name": "pipeline-challenge",
            "run_url": "https://github.com/example/ForgeHistory/actions/runs/12345",
        },
    )
    assert outcome["action"] == "escalate_pipeline_stuck"
    at_threshold = orchestrator.run_event(
        "gate_reject", {"brief_dir": "harness/queue/briefs/x", "reject_streak": 3},
    )
    assert outcome["action"] == at_threshold["action"]


def test_pipeline_job_failed_missing_fields_refused():
    with pytest.raises(orchestrator.OrchestratorError):
        orchestrator.run_event("pipeline_job_failed", {"workflow_name": "pipeline-audit"})
    with pytest.raises(orchestrator.OrchestratorError):
        orchestrator.run_event("pipeline_job_failed", {})


def test_pipeline_job_failed_incident_31085883052_style_regression():
    """Brief 008, Lot 008b, SC10: reproduces the exact shape of the real
    incident -- a `pipeline-orchestrate` run ending `conclusion: failure`
    (run 31085883052, FAILURE, exit 2) -- as the `workflow_run`-triggered
    payload `.github/workflows/pipeline-failure-escalate.yml` constructs,
    and asserts it resolves to the same `escalate_pipeline_stuck` action
    the existing 3-REJECT fixture (`test_gate_reject_escalates_only_at_streak_three`
    above) produces. Hard-won rule 9: proven by this passing test, not
    prose."""
    incident_payload = {
        "workflow_name": "pipeline-orchestrate",
        "run_url": "https://github.com/example/ForgeHistory/actions/runs/31085883052",
        "conclusion": "failure",
    }
    outcome = orchestrator.run_event("pipeline_job_failed", incident_payload)
    assert outcome["action"] == "escalate_pipeline_stuck"
    assert outcome["matched_rules"] == ["pipeline_job_failed"]


def test_no_direct_ledger_file_write_in_source():
    """Static proof, cheap and durable: the module's own source never opens
    LEDGER_PATH (or any local `ledger` variable) for writing -- every mutation
    goes through the imported audit_ledger.append_event function."""
    text = SCRIPT.read_text(encoding="utf-8")
    assert 'open(' not in text
    assert '.write(' not in text
    assert 'audit_ledger.append_event' in text
