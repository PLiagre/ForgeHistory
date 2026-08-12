#!/usr/bin/env py
"""
harness/audit_review.py -- Claude's contre-audit ("challenge") of a Cursor
audit, and the PROPOSED -> CHALLENGED transition.

This is the first step that both writes the ledger AND depends on reasoning.
The reasoning is Claude's job at command time; this module is only the
mechanical scaffolding and the fail-closed gate around it:

  scaffold  -- emit reviews/CLAUDE-<audit_id>.md, a template full of
               <<TODO>> placeholders. Never overwrites an existing review.
  record    -- refuse to log CHALLENGED unless a *real* review exists:
               no <<TODO>> left, at least one verdict token, the audit is
               actually in inbox/, and its current state is PROPOSED.

Why the gate matters: the ledger event AUDIT_CHALLENGED asserts "Claude has
produced its contre-audit." Recording it from an empty scaffold would make
that assertion a lie. So `record` proves the review is filled before it
writes -- the same principle as verdict_audit.py refusing a brief that only
claims to be done. A challenge is CONFIRMED / REFUTED / PARTIAL /
NEEDS_OWNER per point; this module never decides those, it only checks that
Claude wrote them.

Usage:
  py harness/audit_review.py scaffold --audit-id ID
  py harness/audit_review.py record   --audit-id ID
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
REVIEWS = REPO_ROOT / "architecture" / "reviews"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_decision  # noqa: E402
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402

VERDICTS = ("CONFIRMED", "REFUTED", "PARTIAL", "NEEDS_OWNER")
PLACEHOLDER = "<<"  # any <<...>> marker means the scaffold is unfilled


class ReviewError(Exception):
    """A guard refused the operation. Message is user-facing."""


def review_path(audit_id: str, reviews_dir: Path = REVIEWS) -> Path:
    return Path(reviews_dir) / f"CLAUDE-{audit_id}.md"


def find_audit(audit_id: str, inbox: Path | None = None) -> dict | None:
    inbox = inbox or audits_mod.INBOX
    for audit in audits_mod.load_audits(inbox):
        if audit.get("audit_id") == audit_id:
            return audit
    return None


def scaffold_text(audit: dict) -> str:
    audit_id = audit.get("audit_id", "UNKNOWN")
    commit = audit.get("target_commit", "<<TODO: target_commit>>")
    return f"""---
review_of: {audit_id}
reviewer: claude-code
target_commit: {commit}
reviewed_at: <<TODO: ISO 8601 UTC, ex. 2026-08-05T10:00:00Z>>
---

# Contre-audit de {audit_id}

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : {commit}
- Le commit existe-t-il dans l'historique de la branche cible ? <<TODO: oui/non + preuve>>
- Mesures de l'audit rejouées ? <<TODO: commande(s) + sortie>>

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | <<TODO: résumé du point>> | <<TODO: CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER>> | <<TODO: commande + sortie, ou contre-preuve>> |
| 2 | <<TODO>> | <<TODO>> | <<TODO>> |

(Dupliquer une ligne par point majeur de l'audit. Supprimer les lignes
d'exemple non utilisées.)

## 3. Points à porter au propriétaire (NEEDS_OWNER)

<<TODO: les arbitrages métier, hors technique, que le propriétaire doit trancher>>

## 4. Synthèse

<<TODO: ce qui tient, ce qui tombe, recommandation de traitement>>
"""


def write_scaffold(
    audit_id: str,
    *,
    inbox: Path | None = None,
    reviews_dir: Path = REVIEWS,
) -> Path:
    """Create the review file for an audit. Refuses to clobber an existing
    review -- a contre-audit already started is not silently overwritten."""
    audit = find_audit(audit_id, inbox)
    if audit is None:
        raise ReviewError(
            f"no audit {audit_id!r} in inbox; nothing to review"
        )
    path = review_path(audit_id, reviews_dir)
    if path.exists():
        raise ReviewError(
            f"{path.as_posix()} already exists; refusing to overwrite a review in progress"
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(scaffold_text(audit), encoding="utf-8")
    return path


def parse_verdicts(text: str) -> dict:
    """Count occurrences of each verdict token as a whole word."""
    counts: dict = {}
    for token in VERDICTS:
        n = len(re.findall(rf"\b{re.escape(token)}\b", text))
        if n:
            counts[token] = n
    return counts


def record_challenge(
    audit_id: str,
    *,
    inbox: Path | None = None,
    reviews_dir: Path = REVIEWS,
    ledger_path: Path | None = None,
) -> dict:
    """Validate the filled review, then append AUDIT_CHALLENGED.

    Fails closed on every way the assertion "a real review exists and the
    audit was awaiting one" could be false.
    """
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH

    if find_audit(audit_id, inbox) is None:
        raise ReviewError(f"no audit {audit_id!r} in inbox")

    state = audits_mod.current_state(
        audit_id, audit_ledger.read_events(ledger_path)
    )
    if state != "AUDIT_PROPOSED":
        raise ReviewError(
            f"audit {audit_id!r} is {state}, not AUDIT_PROPOSED; only a "
            f"proposed audit can be challenged"
        )

    path = review_path(audit_id, reviews_dir)
    if not path.exists():
        raise ReviewError(
            f"no review at {path.as_posix()}; run `scaffold` and fill it first"
        )
    text = path.read_text(encoding="utf-8")
    if PLACEHOLDER in text:
        raise ReviewError(
            f"{path.as_posix()} still has <<TODO>> placeholders; fill the "
            f"review before recording the challenge"
        )
    verdicts = parse_verdicts(text)
    if not verdicts:
        raise ReviewError(
            f"{path.as_posix()} has no verdict (CONFIRMED/REFUTED/PARTIAL/"
            f"NEEDS_OWNER); a challenge with no verdict is not a challenge"
        )
    # Same parse as audit_decision.decide_auto: the AUDIT_CHALLENGED event
    # promises "the auto-decision can read this review". The first real
    # headless challenge that broke this promise (CLAUDE-CURSOR-bb8fe11-...,
    # 2026-08-12) numbered its rows `§1` / `P1-1` instead of `| 1 |` and
    # stalled the loop AFTER merge, where nobody could fix it -- refusing
    # here, at record time, puts the error in front of the actor (Claude)
    # who can still rewrite the table.
    if not audit_decision.parse_point_verdicts(text):
        raise ReviewError(
            f"{path.as_posix()} has verdict words but no machine-readable "
            f"'| N | ... | VERDICT | ... |' row (N must be a bare number, "
            f"as in the scaffold); --policy auto could never decide it, so "
            f"the challenge is refused now rather than stalling post-merge"
        )

    return audit_ledger.append_event(
        audit_id,
        "AUDIT_CHALLENGED",
        ledger_path=ledger_path,
        actor="claude",
        review=path.relative_to(REPO_ROOT).as_posix()
        if _within(path, REPO_ROOT)
        else path.as_posix(),
        verdicts=verdicts,
    )


def _within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


# --- CLI ----------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Claude's contre-audit of a Cursor audit.")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sp = sub.add_parser("scaffold", help="create the review template")
    sp.add_argument("--audit-id", required=True)
    sp.add_argument("--inbox", default=None)
    sp.add_argument("--reviews", default=str(REVIEWS))

    rp = sub.add_parser("record", help="validate the review and log CHALLENGED")
    rp.add_argument("--audit-id", required=True)
    rp.add_argument("--inbox", default=None)
    rp.add_argument("--reviews", default=str(REVIEWS))
    rp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))

    args = parser.parse_args(argv)
    inbox = Path(args.inbox) if args.inbox else None

    try:
        if args.cmd == "scaffold":
            path = write_scaffold(
                args.audit_id, inbox=inbox, reviews_dir=Path(args.reviews)
            )
            print(f"wrote {path.as_posix()} -- fill every <<TODO>>, then `record`")
            return 0
        if args.cmd == "record":
            record = record_challenge(
                args.audit_id,
                inbox=inbox,
                reviews_dir=Path(args.reviews),
                ledger_path=Path(args.ledger),
            )
            print(f"recorded AUDIT_CHALLENGED for {args.audit_id}: {record['verdicts']}")
            return 0
    except ReviewError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
