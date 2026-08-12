#!/usr/bin/env py
"""
harness/audit_decision.py -- the verdict on a challenged audit: the owner's
(`accept`/`reject`), or -- Lot 006a, ADR-0006 -- a deterministic policy's
(`auto`).

Transition CHALLENGED -> APPROVED or CHALLENGED -> REJECTED. Claude
challenges (step 4), then either the owner decides (`accept`/`reject`,
unchanged since ADR-0005) or, in `mode: full_auto`
(harness/pipeline/config.yaml), the policy decides (`auto`, per
harness/pipeline/auto_policy.yaml and ADR-0006's documented derogation). The
module enforces the ordering and records the rationale; neither path
"judges" in the human/LLM sense -- `accept`/`reject` records what the owner
already decided elsewhere, `auto` applies a fixed, versioned rule table to
the challenge's own per-point verdicts. No LLM decides inside this module.

Fail-closed guards, each protecting a way the decision could be meaningless:

  * only a CHALLENGED audit can be decided -- deciding a PROPOSED audit
    would skip Claude's challenge, the whole point of the loop.
  * a reason is required and non-empty -- a verdict with no rationale is
    not a decision, it is a coin flip nobody can audit later. `auto`
    generates its reason from the matching auto_policy.yaml rule's `action`
    field (or the literal "policy: no owner in full_auto" for the
    NEEDS_OWNER-without-CONFIRMED/PARTIAL case named in the brief) --
    always non-blank, never a human-typed sentence.
  * the decision file is never clobbered -- a verdict, once given, is a
    record, not a draft.

The decision is written to architecture/decisions/DECISION-<id>.md AND
appended to the ledger. The file is the artifact; the ledger is the
timeline. As everywhere in this loop, the ledger is the source of state.
`decided_by` in the decision file and `actor` in the ledger record are
"owner" for accept/reject, "policy:auto" for `auto` -- so a reader can
always tell which path produced a given decision.

Usage:
  py harness/audit_decision.py accept --audit-id ID --reason "..." [--retain 1,2,4]
  py harness/audit_decision.py reject --audit-id ID --reason "..."
  py harness/audit_decision.py auto   --audit-id ID --policy auto
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DECISIONS = REPO_ROOT / "architecture" / "decisions"
AUTO_POLICY_PATH = REPO_ROOT / "harness" / "pipeline" / "auto_policy.yaml"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402
from pipeline import policy_loader  # noqa: E402

# verdict -> ledger event
VERDICT_EVENT = {"APPROVED": "AUDIT_APPROVED", "REJECTED": "AUDIT_REJECTED"}

# One row per numbered verdict line in a filled CLAUDE-<id>.md review
# table, e.g. "| 1 | budget non impose | CONFIRMED | ... |". Captures the
# point number and the verdict token; anything else on the row is ignored.
#
# The verdict token must be at the START of its cell, but Markdown
# decoration around it (bold `**`, italics, backticks) and free text after
# it within the same cell are tolerated: the first real challenges produced
# headless (runs 31603872434 / 31603909788, 2026-08-12) wrote
# `| **PARTIAL** |`, `| **PARTIAL — non rejouable ici** |` and
# `| **CONFIRMED** (mesurabilité) / **NEEDS_OWNER** (reformulation) |`, and
# the strict pattern refused every row, stalling the whole auto-decision.
# A verdict word buried mid-sentence in a summary/proof cell still does
# NOT match -- only leading decoration is allowed before the token, so this
# stays a parse of the verdict column, not a keyword hunt.
_POINT_VERDICT_ROW = re.compile(
    r"^\|\s*(\d+)\s*\|.*?\|[\s*_`~]*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\b[^|]*\|",
    re.MULTILINE,
)


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


def _decision_text(
    audit_id: str, verdict: str, reason: str, retained: list[int] | None, *, decided_by: str = "owner"
) -> str:
    retained_line = (
        ", ".join(str(n) for n in retained) if retained else "tous les points"
    )
    retained_fm = f"[{', '.join(str(n) for n in retained)}]" if retained else "all"
    return f"""---
decision_of: {audit_id}
decided_by: {decided_by}
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
    actor: str = "owner",
) -> dict:
    """Record a verdict (APPROVED/REJECTED) for a CHALLENGED audit.

    `actor` names who/what decided -- "owner" for the human accept/reject
    path (default, unchanged), "policy:auto" when called from decide_auto().
    It is written both into the decision file's `decided_by:` frontmatter
    and the ledger event's `actor` field, so either artifact alone answers
    "was this a human or the policy."
    """
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
    path.write_text(
        _decision_text(audit_id, verdict, reason.strip(), retained, decided_by=actor),
        encoding="utf-8",
    )

    fields: dict = {
        "actor": actor,
        "reason": reason.strip(),
        "decision": path.relative_to(REPO_ROOT).as_posix()
        if _within(path, REPO_ROOT)
        else path.as_posix(),
    }
    if retained:
        fields["retained_points"] = retained
    return audit_ledger.append_event(
        audit_id, VERDICT_EVENT[verdict], ledger_path=ledger_path, **fields
    )


# --- --policy auto (Lot 006a, ADR-0006) ----------------------------------


def parse_point_verdicts(text: str) -> list[tuple[int, str]]:
    """Every `| N | ... | VERDICT | ... |` row in a filled review, as
    (point_number, verdict) pairs. A point can appear once; if the review
    is malformed and repeats a number, both rows are kept -- policy logic
    treats "any row says CONFIRMED/PARTIAL" as sufficient, so duplication
    cannot silently lose a retained point.

    Public on purpose: audit_review.record_challenge uses the SAME parse to
    refuse a review the auto-decision could not read -- one parser, one
    contract, no second place that could disagree with the first."""
    return [(int(n), v) for n, v in _POINT_VERDICT_ROW.findall(text)]


def _review_path_from_ledger(audit_id: str, events: list[dict]) -> Path | None:
    """The review path recorded on this audit_id's AUDIT_CHALLENGED event
    (written by audit_review.record_challenge), resolved to an absolute
    path. None if no such event/field exists."""
    for event in reversed(events):
        if event.get("audit_id") == audit_id and event.get("event") == "AUDIT_CHALLENGED":
            review = event.get("review")
            if not review:
                return None
            p = Path(review)
            return p if p.is_absolute() else REPO_ROOT / p
    return None


def decide_auto(
    audit_id: str,
    *,
    inbox: Path | None = None,
    decisions_dir: Path = DECISIONS,
    ledger_path: Path | None = None,
    policy_path: Path = AUTO_POLICY_PATH,
) -> dict:
    """Deterministic CHALLENGED -> APPROVED/REJECTED per
    harness/pipeline/auto_policy.yaml, reading the challenge's own
    per-point verdicts. No LLM decides here -- three fixed rules, checked
    in the order they appear in the policy table (brief 006 "Politique
    auto"):

      1. every point REFUTED               -> REJECTED
      2. >=1 point CONFIRMED or PARTIAL     -> APPROVED, retained = those points
      3. NEEDS_OWNER with none of the above -> REJECTED, "policy: no owner in full_auto"

    A review with no parseable per-point verdict rows refuses (fails
    closed) rather than guessing -- an auto decision must be traceable to
    real table rows, never a default.
    """
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH
    events = audit_ledger.read_events(ledger_path)

    state = audits_mod.current_state(audit_id, events)
    if state != "AUDIT_CHALLENGED":
        raise DecisionError(
            f"audit {audit_id!r} is {state}, not AUDIT_CHALLENGED; only a "
            f"challenged audit can be decided (--policy auto included)"
        )

    policy = policy_loader.load_auto_policy(policy_path)

    review_path = _review_path_from_ledger(audit_id, events)
    if review_path is None or not review_path.exists():
        raise DecisionError(
            f"no review file recorded for {audit_id!r}'s AUDIT_CHALLENGED event; "
            f"--policy auto cannot decide without the per-point verdicts"
        )
    points = parse_point_verdicts(review_path.read_text(encoding="utf-8"))
    if not points:
        raise DecisionError(
            f"{review_path.as_posix()} has no '| N | ... | VERDICT | ... |' rows; "
            f"--policy auto refuses to guess a verdict"
        )

    retained = sorted({n for n, v in points if v in ("CONFIRMED", "PARTIAL")})
    all_refuted = all(v == "REFUTED" for _n, v in points)
    has_needs_owner = any(v == "NEEDS_OWNER" for _n, v in points)

    if all_refuted:
        rule = policy_loader.rule_by_id(policy, "review_all_refuted")
        action = rule["action"] if rule else "all points REFUTED"
        reason = f"policy: {action} (auto_policy.yaml rule review_all_refuted)"
        return decide(
            audit_id, "REJECTED", reason,
            inbox=inbox, decisions_dir=decisions_dir, ledger_path=ledger_path, actor="policy:auto",
        )

    if retained:
        rule = policy_loader.rule_by_id(policy, "review_has_confirmed_or_partial")
        action = rule["action"] if rule else "confirmed or partial points retained"
        reason = f"policy: {action} (auto_policy.yaml rule review_has_confirmed_or_partial)"
        return decide(
            audit_id, "APPROVED", reason, retained=retained,
            inbox=inbox, decisions_dir=decisions_dir, ledger_path=ledger_path, actor="policy:auto",
        )

    if has_needs_owner:
        # Brief 006 gives this exact reason string verbatim; kept literal
        # so it is grep-able, on top of naming its policy rule for
        # traceability back to auto_policy.yaml.
        reason = "policy: no owner in full_auto (auto_policy.yaml rule review_needs_owner_only)"
        return decide(
            audit_id, "REJECTED", reason,
            inbox=inbox, decisions_dir=decisions_dir, ledger_path=ledger_path, actor="policy:auto",
        )

    raise DecisionError(
        f"{review_path.as_posix()} has verdict rows but none are CONFIRMED/PARTIAL/"
        f"NEEDS_OWNER and not all are REFUTED; --policy auto has no matching rule "
        f"and refuses to guess"
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
    parser = argparse.ArgumentParser(
        description="The verdict on a challenged audit: the owner's (accept/reject), "
        "or a deterministic --policy auto (harness/pipeline/auto_policy.yaml, ADR-0006)."
    )
    sub = parser.add_subparsers(dest="cmd", required=True)

    for name in ("accept", "reject"):
        cp = sub.add_parser(name, help=f"{name} a challenged audit (owner)")
        cp.add_argument("--audit-id", required=True)
        cp.add_argument("--reason", required=True)
        cp.add_argument("--inbox", default=None)
        cp.add_argument("--decisions", default=str(DECISIONS))
        cp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
        if name == "accept":
            cp.add_argument("--retain", default=None, help="comma-separated point numbers")

    ap = sub.add_parser(
        "auto",
        help="decide a challenged audit with --policy auto (no human) per auto_policy.yaml",
    )
    ap.add_argument("--audit-id", required=True)
    ap.add_argument(
        "--policy",
        default="auto",
        choices=["auto"],
        help="decision policy; only 'auto' (harness/pipeline/auto_policy.yaml) is implemented",
    )
    ap.add_argument("--inbox", default=None)
    ap.add_argument("--decisions", default=str(DECISIONS))
    ap.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
    ap.add_argument("--policy-path", default=str(AUTO_POLICY_PATH), help="path to auto_policy.yaml")

    args = parser.parse_args(argv)

    try:
        if args.cmd == "auto":
            record = decide_auto(
                args.audit_id,
                inbox=Path(args.inbox) if args.inbox else None,
                decisions_dir=Path(args.decisions),
                ledger_path=Path(args.ledger),
                policy_path=Path(args.policy_path),
            )
        else:
            verdict = "APPROVED" if args.cmd == "accept" else "REJECTED"
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
    print(f"recorded {record['event']} for {args.audit_id} (reason: {record.get('reason', '')})".rstrip())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
