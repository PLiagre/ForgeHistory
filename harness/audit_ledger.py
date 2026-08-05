#!/usr/bin/env py
"""
harness/audit_ledger.py -- append-only historisation of the audit loop.

Why this exists, and why it is separate from the cost ledger: the cost
ledger (harness/queue/cost-ledger.jsonl) answers "where did spend go, per
backend, per brief." This ledger answers a different question -- "what
happened to each audit, and which brief did an accepted audit become." Two
questions, two domains, two files. Folding audit state transitions into the
cost ledger would overload one file's schema and couple two concerns that
change for different reasons.

What it records: one line per state transition of an audit
(architecture/inbox/CURSOR-*.md), keyed by `audit_id`. The `event` is one
of the AUDIT_* statuses. A CONVERTED event carries `briefs`, closing the
loop audit -> brief -> (via the cost ledger) real cost.

What it does NOT claim: this step's writer is a plain append. It is not
concurrency-safe and does not lock -- Cursor's own audit #6231186 flagged
that no ledger append in this repo is atomic yet. That hardening is a
later, separately-tested step; naming the gap here keeps this honest rather
than pretending a guarantee it does not have.

Usage:
  py harness/audit_ledger.py append --audit-id ID --event AUDIT_PROPOSED \
      [--set key=value ...]
  py harness/audit_ledger.py show [--audit-id ID]
"""
from __future__ import annotations

import argparse
import datetime
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LEDGER_PATH = REPO_ROOT / "architecture" / "audit-ledger.jsonl"

# The nine lifecycle states an audit can transition through. An event
# outside this set is refused: an unrecognised event silently accepted
# would let a typo become a permanent, unqueryable line in the ledger.
VALID_EVENTS = (
    "AUDIT_PROPOSED",
    "AUDIT_CHALLENGED",
    "AUDIT_APPROVED",
    "AUDIT_REJECTED",
    "AUDIT_CONVERTED",
    "AUDIT_IMPLEMENTED",
    "AUDIT_VERIFIED",
    "AUDIT_STALE",
    "AUDIT_ARCHIVED",
)

# The FSM this ledger now enforces (Lot 006a, brief 006 -- closes the
# bypass named by the post-merge audit CURSOR-POSTMERGE-42cb054: an
# AUDIT_APPROVED event used to append with no prior AUDIT_CHALLENGED,
# because only audit_decision.decide() checked ordering, never the ledger
# itself). Key `None` is the state of an audit_id with NO prior events in
# this ledger -- the only legal first event for a brand new audit_id is
# AUDIT_PROPOSED. Every other key is a current state; its value is the set
# of events legally appended next. A state mapped to an empty set is
# terminal -- nothing may follow it.
#
# This map is deliberately wider than the single straight-line happy path
# (PROPOSED -> CHALLENGED -> APPROVED -> CONVERTED -> IMPLEMENTED ->
# VERIFIED -> ARCHIVED) because real callers need it: audit_decision.py can
# also produce CHALLENGED -> REJECTED, and a REJECTED audit still needs to
# reach ARCHIVED (audit_archive.py treats REJECTED and VERIFIED as the two
# archivable terminal states). AUDIT_STALE is reachable from every
# in-flight state (a dead branch of the loop, per ADR-0005) and always
# drains to ARCHIVED, never back into the flow.
#
# `None` (no prior events) allows AUDIT_CHALLENGED as well as
# AUDIT_PROPOSED: per auto_policy.yaml rule audit_pr_merge_ci_green, the
# AUDIT_PROPOSED ledger event is documented as OPTIONAL -- an audit's mere
# presence in architecture/inbox/ is its proposal, and audits.py's own
# DEFAULT_STATE already treats "no events yet" as AUDIT_PROPOSED
# (audit_review.record_challenge relies on exactly this to write the FIRST
# ledger line as AUDIT_CHALLENGED for an audit that was never explicitly
# proposed in the ledger). The FSM mirrors that convention rather than
# fighting it -- it still refuses AUDIT_APPROVED, AUDIT_CONVERTED, etc. as a
# first event, which is the actual disqualifying case this lot must close.
TRANSITIONS: dict[str | None, frozenset[str]] = {
    None: frozenset({"AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_STALE"}),
    "AUDIT_PROPOSED": frozenset({"AUDIT_CHALLENGED", "AUDIT_STALE"}),
    "AUDIT_CHALLENGED": frozenset({"AUDIT_APPROVED", "AUDIT_REJECTED", "AUDIT_STALE"}),
    "AUDIT_APPROVED": frozenset({"AUDIT_CONVERTED", "AUDIT_STALE"}),
    "AUDIT_REJECTED": frozenset({"AUDIT_ARCHIVED"}),
    "AUDIT_CONVERTED": frozenset({"AUDIT_IMPLEMENTED", "AUDIT_STALE"}),
    "AUDIT_IMPLEMENTED": frozenset({"AUDIT_VERIFIED", "AUDIT_STALE"}),
    "AUDIT_VERIFIED": frozenset({"AUDIT_ARCHIVED"}),
    "AUDIT_STALE": frozenset({"AUDIT_ARCHIVED"}),
    "AUDIT_ARCHIVED": frozenset(),  # terminal: nothing follows an archived audit
}


class TransitionError(ValueError):
    """Raised by append_event when `event` is not a legal successor of the
    audit's current FSM state. A ValueError subclass on purpose -- every
    existing caller (audit_decision.py, audit_review.py, audit_convert.py,
    audit_archive.py, the CLI's `except ValueError`) already catches
    ValueError, so this is additive, not a breaking signature change."""


def current_state_for(audit_id: str, ledger_path: Path) -> str | None:
    """The audit's current FSM state, or None if it has no prior events in
    this ledger yet (the only state from which AUDIT_PROPOSED is legal).

    Delegates to audits.current_state for the actual reconstruction so
    there is exactly one place that knows "last event for this audit_id
    wins" -- imported lazily to avoid a load-order cycle (audits.py imports
    this module at its own top level)."""
    events = read_events(ledger_path)
    own_events = [e for e in events if e.get("audit_id") == audit_id]
    if not own_events:
        return None
    import audits as audits_mod  # noqa: WPS433 -- deliberate lazy import, see docstring

    return audits_mod.current_state(audit_id, events)


def _utc_now_iso() -> str:
    """ISO 8601, UTC, second precision -- same shape Cursor stamps in its
    audit frontmatter (`created_at`), so the two are directly comparable."""
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def append_event(
    audit_id: str,
    event: str,
    *,
    ledger_path: Path = LEDGER_PATH,
    timestamp: str | None = None,
    **fields: object,
) -> dict:
    """Append one transition to the ledger and return the record written.

    Fails closed on three things that would corrupt the ledger's meaning:
    a blank `audit_id` (the join key), an `event` outside VALID_EVENTS, or
    -- Lot 006a -- an `event` that is not a legal FSM successor of this
    audit_id's current state (see TRANSITIONS). The disqualifying case this
    exists to close: appending AUDIT_APPROVED when the audit is not
    AUDIT_CHALLENGED now raises TransitionError (a ValueError) instead of
    silently appending, no matter which caller tries it -- the human
    accept/reject path, a future --policy auto, or a direct CLI/API call.
    Extra keyword fields (actor, target_commit, review, verdicts,
    retained_points, briefs, ...) are passed through verbatim.
    """
    if not audit_id or not str(audit_id).strip():
        raise ValueError("audit_id is required and must be non-empty")
    if event not in VALID_EVENTS:
        raise ValueError(
            f"unknown event {event!r}; expected one of {', '.join(VALID_EVENTS)}"
        )

    ledger_path = Path(ledger_path)
    current = current_state_for(audit_id, ledger_path)
    allowed = TRANSITIONS.get(current, frozenset())
    if event not in allowed:
        state_desc = current or "NONE (no prior event for this audit_id)"
        allowed_desc = ", ".join(sorted(allowed)) if allowed else "none -- terminal state"
        raise TransitionError(
            f"invalid transition for {audit_id!r}: {state_desc} -> {event} is not "
            f"allowed; legal next event(s) from {state_desc}: {allowed_desc}"
        )

    record: dict = {
        "timestamp": timestamp or _utc_now_iso(),
        "audit_id": audit_id,
        "event": event,
    }
    record.update(fields)

    ledger_path = Path(ledger_path)
    ledger_path.parent.mkdir(parents=True, exist_ok=True)
    with ledger_path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(record, ensure_ascii=False) + "\n")
    return record


def read_events(ledger_path: Path = LEDGER_PATH) -> list[dict]:
    """Return every ledger line as a dict, in file order. Missing file is
    an empty history, not an error -- a fresh repo simply has no events."""
    ledger_path = Path(ledger_path)
    if not ledger_path.exists():
        return []
    events: list[dict] = []
    for line in ledger_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            events.append(json.loads(line))
    return events


# --- CLI ----------------------------------------------------------------


def _parse_set(pairs: list[str]) -> dict:
    out: dict = {}
    for pair in pairs or []:
        if "=" not in pair:
            raise SystemExit(f"--set expects key=value, got {pair!r}")
        key, value = pair.split("=", 1)
        out[key] = value
    return out


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="cmd", required=True)

    ap = sub.add_parser("append", help="append one transition")
    ap.add_argument("--audit-id", required=True)
    ap.add_argument("--event", required=True)
    ap.add_argument(
        "--set",
        action="append",
        default=[],
        metavar="key=value",
        help="extra field, repeatable",
    )
    ap.add_argument("--ledger", default=str(LEDGER_PATH))

    sp = sub.add_parser("show", help="print the ledger, optionally filtered")
    sp.add_argument("--audit-id", default=None)
    sp.add_argument("--ledger", default=str(LEDGER_PATH))

    args = parser.parse_args(argv)

    if args.cmd == "append":
        try:
            record = append_event(
                args.audit_id,
                args.event,
                ledger_path=Path(args.ledger),
                **_parse_set(args.set),
            )
        except ValueError as exc:
            print(f"error: {exc}", file=sys.stderr)
            return 2
        print(json.dumps(record, ensure_ascii=False))
        return 0

    if args.cmd == "show":
        for event in read_events(Path(args.ledger)):
            if args.audit_id and event.get("audit_id") != args.audit_id:
                continue
            print(json.dumps(event, ensure_ascii=False))
        return 0

    return 1


if __name__ == "__main__":
    raise SystemExit(main())
