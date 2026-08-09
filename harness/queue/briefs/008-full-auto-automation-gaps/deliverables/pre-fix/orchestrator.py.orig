#!/usr/bin/env py
"""
harness/pipeline/orchestrator.py -- the machine that replaces the owner's
accept/reject step in `mode: full_auto` (ADR-0006, brief 006 Lot 006b).

What this module IS: a deterministic dispatcher. It reads one JSON payload
(merge SHA, audit_id, brief_dir, ...), looks up the matching event kind
against `harness/pipeline/auto_policy.yaml`'s rule table, and calls the
ONE existing module that already owns that transition:

    event kind        -> module.function called                -> ledger event(s)
    audit_pr_merge     -> audit_ledger.append_event directly     AUDIT_PROPOSED (optional)
    review_recorded     -> audit_decision.decide_auto            AUDIT_APPROVED or AUDIT_REJECTED
    audit_approved       -> audit_convert.convert                AUDIT_CONVERTED
    brief_seed_created  -> (log only -- claude-planificateur is a separate,
                            deliberately human-shaped invocation, not a
                            ledger transition)
    gate_accept          -> (log only -- launching claude-evaluator is an
                             external agent invocation, not this module's job)
    evaluateur_pass       -> audit_ledger.append_event directly    AUDIT_IMPLEMENTED then AUDIT_VERIFIED
    gate_reject           -> (log only, unless payload proves a 3-in-a-row
                             streak on the SAME brief -- then logs the
                             "pipeline-stuck" escalation the policy requires)
    budget_exhausted      -> (log only -- the real checkpoint is written by
                             harness/budget.py checkpoint, in the Générateur's
                             own process, not here)

What this module is NOT: it never decides business value, never reasons
about an audit's content, and never constructs a ledger line itself.
EVERY write to architecture/audit-ledger.jsonl in this file, direct or via
a called module, funnels through `audit_ledger.append_event` -- the single
choke point the Lot 006a FSM guarantee depends on (see that lot's verdict,
"Feedback for Next Iteration": orchestrator.py must route ALL ledger writes
through append_event, never construct ledger lines directly). This module
never touches a ledger file handle itself -- test_orchestrator.py's
`test_no_direct_ledger_file_write_in_source` proves it mechanically.

A payload naming an event kind with no matching rule in auto_policy.yaml,
or whose required fields are missing, is refused (fail closed) with a
non-zero exit -- this module never guesses a transition the policy table
does not name.

Usage:
  py harness/pipeline/orchestrator.py run --event <kind> --payload '<json>'
  py harness/pipeline/orchestrator.py run --event <kind> --payload-file <path>
  py harness/pipeline/orchestrator.py --help
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"

sys.path.insert(0, str(HARNESS))
import audit_convert  # noqa: E402
import audit_decision  # noqa: E402
import audit_ledger  # noqa: E402
from pipeline import policy_loader  # noqa: E402

AUTO_POLICY_PATH = HARNESS / "pipeline" / "auto_policy.yaml"

# event kind (CLI --event) -> auto_policy.yaml rule id(s) it implements.
# review_recorded covers three rules because audit_decision.decide_auto()
# already picks the right one by reading the review's own per-point
# verdicts -- duplicating that selection here would be a second place that
# could disagree with the first.
EVENT_TO_RULE_IDS = {
    "audit_pr_merge": ["audit_pr_merge_ci_green"],
    "review_recorded": [
        "review_all_refuted",
        "review_has_confirmed_or_partial",
        "review_needs_owner_only",
    ],
    "audit_approved": ["approved_audit_convert"],
    "brief_seed_created": ["brief_seed_created"],
    "gate_accept": ["gate_accept"],
    "evaluateur_pass": ["evaluateur_pass"],
    "gate_reject": ["three_consecutive_mechanical_rejects"],
    "budget_exhausted": ["budget_exhausted"],
}


class OrchestratorError(Exception):
    """A policy guard refused the event. Message is user-facing."""


def _require(payload: dict, *keys: str) -> None:
    missing = [k for k in keys if not payload.get(k)]
    if missing:
        raise OrchestratorError(
            f"payload missing required field(s) {missing}; refusing to guess"
        )


def _matching_rules(policy: dict, event: str) -> list[dict]:
    ids = EVENT_TO_RULE_IDS.get(event)
    if not ids:
        raise OrchestratorError(
            f"no auto_policy.yaml rule maps to event {event!r}; known events: "
            f"{sorted(EVENT_TO_RULE_IDS)}"
        )
    rules = [r for r in policy.get("rules", []) if r.get("id") in ids]
    if not rules:
        raise OrchestratorError(
            f"event {event!r} names rule id(s) {ids} but none are present in "
            f"{AUTO_POLICY_PATH.as_posix()}; policy file and dispatcher have drifted"
        )
    return rules


# --- event handlers -------------------------------------------------------
# Every handler returns a dict describing what happened, for the CLI to
# print. None of them ever opens the ledger file directly.


def handle_audit_pr_merge(payload: dict, *, ledger_path: Path, **_kw) -> dict:
    """Rule `audit_pr_merge_ci_green`: the AUDIT_PROPOSED ledger event is
    documented as OPTIONAL (the audit's mere presence in inbox/ is its
    proposal). If an audit_id is given and it has no prior events, record
    it explicitly; if it already has a state (e.g. the CI-driven bot merge
    already implied PROPOSED, or a human/earlier run recorded it), that is
    not an error -- this rule is optional by design."""
    audit_id = payload.get("audit_id")
    if not audit_id:
        return {"action": "no_op", "reason": "no audit_id in payload; AUDIT_PROPOSED is optional"}
    current = audit_ledger.current_state_for(audit_id, ledger_path)
    if current is not None:
        return {"action": "no_op", "reason": f"{audit_id} already has state {current}; PROPOSED is optional, not re-appended"}
    record = audit_ledger.append_event(audit_id, "AUDIT_PROPOSED", ledger_path=ledger_path, actor="policy:auto")
    return {"action": "ledger_append", "record": record}


def handle_review_recorded(payload: dict, *, ledger_path: Path, inbox: Path | None, decisions_dir: Path, policy_path: Path, **_kw) -> dict:
    _require(payload, "audit_id")
    record = audit_decision.decide_auto(
        payload["audit_id"],
        inbox=inbox,
        decisions_dir=decisions_dir,
        ledger_path=ledger_path,
        policy_path=policy_path,
    )
    return {"action": "decide_auto", "record": record}


def handle_audit_approved(payload: dict, *, ledger_path: Path, inbox: Path | None, briefs_dir: Path, **_kw) -> dict:
    _require(payload, "audit_id")
    record = audit_convert.convert(
        payload["audit_id"],
        slug=payload.get("slug"),
        inbox=inbox,
        briefs_dir=briefs_dir,
        ledger_path=ledger_path,
    )
    return {"action": "audit_convert", "record": record}


def handle_brief_seed_created(payload: dict, **_kw) -> dict:
    _require(payload, "brief_dir")
    # No ledger transition here on purpose: auto_policy.yaml's own rule
    # says "claude_planificateur fills TODO in the SAME pipeline, SEPARATE
    # invocation" -- that invocation is an external agent call the
    # orchestrator enqueues, not a ledger state change it owns.
    return {
        "action": "enqueue_planificateur",
        "reason": f"brief seed at {payload['brief_dir']} needs claude-planificateur "
        f"to fill its <<TODO>> markers before /forge-run can start (separate invocation)",
    }


def handle_gate_accept(payload: dict, **_kw) -> dict:
    _require(payload, "brief_dir")
    return {
        "action": "enqueue_evaluateur",
        "reason": f"gate ACCEPT on {payload['brief_dir']}; launch claude-evaluator "
        f"(architecture/agents/claude-evaluator.md) -- orchestrator does not judge",
    }


def handle_evaluateur_pass(payload: dict, *, ledger_path: Path, **_kw) -> dict:
    _require(payload, "audit_id")
    audit_id = payload["audit_id"]
    implemented = audit_ledger.append_event(audit_id, "AUDIT_IMPLEMENTED", ledger_path=ledger_path, actor="policy:auto")
    verified = audit_ledger.append_event(audit_id, "AUDIT_VERIFIED", ledger_path=ledger_path, actor="policy:auto")
    return {"action": "ledger_append_chain", "records": [implemented, verified]}


def handle_gate_reject(payload: dict, **_kw) -> dict:
    _require(payload, "brief_dir")
    streak = int(payload.get("reject_streak", 0))
    if streak >= 3:
        return {
            "action": "escalate_pipeline_stuck",
            "reason": f"{streak} consecutive mechanical REJECTs on {payload['brief_dir']}; "
            f"policy rule three_consecutive_mechanical_rejects: open bot issue "
            f"'pipeline-stuck', no human wait",
        }
    return {"action": "no_op", "reason": f"reject_streak={streak} < 3; below escalation threshold"}


def handle_budget_exhausted(payload: dict, **_kw) -> dict:
    _require(payload, "brief_dir")
    return {
        "action": "no_op",
        "reason": f"policy rule budget_exhausted is advisory here: the real checkpoint "
        f"is written by `py harness/budget.py checkpoint --brief {payload['brief_dir']}` "
        f"in the Générateur's own process, not by this orchestrator",
    }


HANDLERS = {
    "audit_pr_merge": handle_audit_pr_merge,
    "review_recorded": handle_review_recorded,
    "audit_approved": handle_audit_approved,
    "brief_seed_created": handle_brief_seed_created,
    "gate_accept": handle_gate_accept,
    "evaluateur_pass": handle_evaluateur_pass,
    "gate_reject": handle_gate_reject,
    "budget_exhausted": handle_budget_exhausted,
}


def run_event(
    event: str,
    payload: dict,
    *,
    ledger_path: Path | None = None,
    inbox: Path | None = None,
    decisions_dir: Path | None = None,
    briefs_dir: Path | None = None,
    policy_path: Path = AUTO_POLICY_PATH,
) -> dict:
    """Dispatch one event through its policy rule(s) and handler. Returns a
    dict describing the outcome; raises OrchestratorError on any guard."""
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH
    decisions_dir = decisions_dir or (REPO_ROOT / "architecture" / "decisions")
    briefs_dir = briefs_dir or (REPO_ROOT / "harness" / "queue" / "briefs")

    policy = policy_loader.load_auto_policy(policy_path)
    rules = _matching_rules(policy, event)  # fail closed if event is unknown to the policy

    handler = HANDLERS.get(event)
    if handler is None:  # pragma: no cover -- guarded by EVENT_TO_RULE_IDS/HANDLERS staying in sync
        raise OrchestratorError(f"no handler registered for event {event!r}")

    outcome = handler(
        payload,
        ledger_path=ledger_path,
        inbox=inbox,
        decisions_dir=decisions_dir,
        briefs_dir=briefs_dir,
        policy_path=policy_path,
    )
    outcome["event"] = event
    outcome["matched_rules"] = [r.get("id") for r in rules]
    return outcome


# --- CLI --------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Deterministic dispatcher for the full-auto pipeline (ADR-0006, "
        "brief 006 Lot 006b). Reads auto_policy.yaml, calls the one existing module "
        "that owns each transition, never constructs a ledger line directly."
    )
    sub = parser.add_subparsers(dest="cmd", required=True)

    rp = sub.add_parser("run", help="dispatch one pipeline event")
    rp.add_argument("--event", required=True, choices=sorted(EVENT_TO_RULE_IDS))
    payload_group = rp.add_mutually_exclusive_group(required=True)
    payload_group.add_argument("--payload", default=None, help="inline JSON payload")
    payload_group.add_argument("--payload-file", default=None, help="path to a JSON payload file")
    rp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
    rp.add_argument("--inbox", default=None)
    rp.add_argument("--decisions", default=None)
    rp.add_argument("--briefs-dir", default=None)
    rp.add_argument("--policy-path", default=str(AUTO_POLICY_PATH))

    args = parser.parse_args(argv)

    if args.cmd != "run":  # pragma: no cover -- argparse enforces this already
        return 1

    if args.payload_file:
        payload = json.loads(Path(args.payload_file).read_text(encoding="utf-8"))
    else:
        try:
            payload = json.loads(args.payload)
        except json.JSONDecodeError as exc:
            print(f"error: --payload is not valid JSON: {exc}", file=sys.stderr)
            return 2

    try:
        outcome = run_event(
            args.event,
            payload,
            ledger_path=Path(args.ledger),
            inbox=Path(args.inbox) if args.inbox else None,
            decisions_dir=Path(args.decisions) if args.decisions else None,
            briefs_dir=Path(args.briefs_dir) if args.briefs_dir else None,
            policy_path=Path(args.policy_path),
        )
    except (OrchestratorError, audit_decision.DecisionError, audit_convert.ConvertError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    print(json.dumps(outcome, ensure_ascii=False, default=str))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
