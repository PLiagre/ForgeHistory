#!/usr/bin/env py
"""
harness/audit_decision.py -- the owner's verdict on a challenged audit.

Transition CHALLENGED -> APPROVED or CHALLENGED -> REJECTED. This is the one
human step: Claude challenges (step 4), the owner decides (here). The module
enforces the ordering and records the rationale; it never decides.

Fail-closed guards, each protecting a way the decision could be meaningless:

  * only a CHALLENGED audit can be decided -- deciding a PROPOSED audit
    would skip Claude's challenge, the whole point of the loop.
  * a reason is required and non-empty -- a verdict with no rationale is
    not a decision, it is a coin flip nobody can audit later.
  * the decision file is never clobbered -- a verdict, once given, is a
    record, not a draft.

The decision is written to architecture/decisions/DECISION-<id>.md AND
appended to the ledger. The file is the human artifact; the ledger is the
timeline. As everywhere in this loop, the ledger is the source of state.

Usage:
  py harness/audit_decision.py accept --audit-id ID --reason "..." [--retain 1,2,4]
  py harness/audit_decision.py reject --audit-id ID --reason "..."
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DECISIONS = REPO_ROOT / "architecture" / "decisions"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402

# verdict -> ledger event
VERDICT_EVENT = {"APPROVED": "AUDIT_APPROVED", "REJECTED": "AUDIT_REJECTED"}


class DecisionError(Exception):
    """A guard refused the operation. Message is user-facing."""


def decision_path(audit_id: str, decisions_dir: Path = DECISIONS) -> Path:
    return Path(decisions_dir) / f"DECISION-{audit_id}.md"


def _find_audit(audit_id: str, inbox: Path | None) -> dict | None:
    for audit in audits_mod.load_audits(inbox or audits_mod.INBOX):
        if audit.get("audit_id") == audit_id:
            return audit
    return None


def _within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _decision_text(audit_id: str, verdict: str, reason: str, retained: list[int] | None) -> str:
    retained_line = (
        ", ".join(str(n) for n in retained) if retained else "tous les points"
    )
    retained_fm = f"[{', '.join(str(n) for n in retained)}]" if retained else "all"
    return f"""---
decision_of: {audit_id}
decided_by: owner
verdict: {verdict}
retained_points: {retained_fm}
---

# Décision sur {audit_id}

**Verdict : {verdict}**

## Raison

{reason}

## Points retenus

{retained_line}
"""


def decide(
    audit_id: str,
    verdict: str,
    reason: str,
    *,
    retained: list[int] | None = None,
    inbox: Path | None = None,
    decisions_dir: Path = DECISIONS,
    ledger_path: Path | None = None,
) -> dict:
    if verdict not in VERDICT_EVENT:
        raise DecisionError(f"verdict must be APPROVED or REJECTED, got {verdict!r}")
    if not reason or not reason.strip():
        raise DecisionError("a non-empty --reason is required; a verdict with no rationale is not a decision")

    ledger_path = ledger_path or audit_ledger.LEDGER_PATH

    if _find_audit(audit_id, inbox) is None:
        raise DecisionError(f"no audit {audit_id!r} in inbox")

    state = audits_mod.current_state(audit_id, audit_ledger.read_events(ledger_path))
    if state != "AUDIT_CHALLENGED":
        raise DecisionError(
            f"audit {audit_id!r} is {state}, not AUDIT_CHALLENGED; only a "
            f"challenged audit can be decided (run /forge-audit-review first)"
        )

    path = decision_path(audit_id, decisions_dir)
    if path.exists():
        raise DecisionError(
            f"{path.as_posix()} already exists; a decision is a record, not a draft"
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(_decision_text(audit_id, verdict, reason.strip(), retained), encoding="utf-8")

    fields: dict = {
        "actor": "owner",
        "decision": path.relative_to(REPO_ROOT).as_posix()
        if _within(path, REPO_ROOT)
        else path.as_posix(),
    }
    if retained:
        fields["retained_points"] = retained
    return audit_ledger.append_event(
        audit_id, VERDICT_EVENT[verdict], ledger_path=ledger_path, **fields
    )


def _parse_retain(value: str | None) -> list[int] | None:
    if not value:
        return None
    out: list[int] = []
    for chunk in value.split(","):
        chunk = chunk.strip()
        if not chunk:
            continue
        try:
            out.append(int(chunk))
        except ValueError:
            raise DecisionError(f"--retain expects comma-separated integers, got {chunk!r}")
    return out or None


# --- CLI ----------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="The owner's verdict on a challenged audit.")
    sub = parser.add_subparsers(dest="cmd", required=True)

    for name in ("accept", "reject"):
        cp = sub.add_parser(name, help=f"{name} a challenged audit")
        cp.add_argument("--audit-id", required=True)
        cp.add_argument("--reason", required=True)
        cp.add_argument("--inbox", default=None)
        cp.add_argument("--decisions", default=str(DECISIONS))
        cp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
        if name == "accept":
            cp.add_argument("--retain", default=None, help="comma-separated point numbers")

    args = parser.parse_args(argv)
    verdict = "APPROVED" if args.cmd == "accept" else "REJECTED"

    try:
        retained = _parse_retain(getattr(args, "retain", None))
        record = decide(
            args.audit_id,
            verdict,
            args.reason,
            retained=retained,
            inbox=Path(args.inbox) if args.inbox else None,
            decisions_dir=Path(args.decisions),
            ledger_path=Path(args.ledger),
        )
    except DecisionError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    print(f"recorded {record['event']} for {args.audit_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
