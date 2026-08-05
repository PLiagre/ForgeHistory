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

    Fails closed on the two things that would corrupt the ledger's meaning:
    a blank `audit_id` (the join key) or an `event` outside VALID_EVENTS.
    Extra keyword fields (actor, target_commit, review, verdicts,
    retained_points, briefs, ...) are passed through verbatim.
    """
    if not audit_id or not str(audit_id).strip():
        raise ValueError("audit_id is required and must be non-empty")
    if event not in VALID_EVENTS:
        raise ValueError(
            f"unknown event {event!r}; expected one of {', '.join(VALID_EVENTS)}"
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
