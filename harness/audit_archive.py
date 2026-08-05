#!/usr/bin/env py
"""
harness/audit_archive.py -- freeze a terminal audit into architecture/archive/.

Transition VERIFIED or REJECTED -> ARCHIVED. Archiving bundles an audit's
three artifacts -- the inbox audit, Claude's review, the owner's decision --
into architecture/archive/<audit_id>/ as a frozen snapshot, and records
AUDIT_ARCHIVED.

Copy, never move: the inbox stays append-only and immutable (README:
"un fichier neuf par nouveau SHA, jamais édité ni supprimé"). The archive is
a convenience bundle of a closed case, not a relocation. Deleting the inbox
record would destroy the provenance the whole loop exists to keep.

Fail-closed guards:
  * only a REJECTED or VERIFIED audit can be archived -- an audit still in
    flight (PROPOSED/CHALLENGED/APPROVED/CONVERTED/IMPLEMENTED) is not a
    closed case;
  * the archive directory is never clobbered.

Usage:
  py harness/audit_archive.py archive --audit-id ID
"""
from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
ARCHITECTURE = REPO_ROOT / "architecture"
INBOX = ARCHITECTURE / "inbox"
REVIEWS = ARCHITECTURE / "reviews"
DECISIONS = ARCHITECTURE / "decisions"
ARCHIVE = ARCHITECTURE / "archive"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402

ARCHIVABLE = {"AUDIT_REJECTED", "AUDIT_VERIFIED"}


class ArchiveError(Exception):
    """A guard refused the operation. Message is user-facing."""


def _find_audit(audit_id: str, inbox: Path) -> dict | None:
    for audit in audits_mod.load_audits(inbox):
        if audit.get("audit_id") == audit_id:
            return audit
    return None


def _within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def archive(
    audit_id: str,
    *,
    inbox: Path = INBOX,
    reviews_dir: Path = REVIEWS,
    decisions_dir: Path = DECISIONS,
    archive_dir: Path = ARCHIVE,
    ledger_path: Path | None = None,
) -> dict:
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH
    inbox, reviews_dir, decisions_dir, archive_dir = map(
        Path, (inbox, reviews_dir, decisions_dir, archive_dir)
    )

    audit = _find_audit(audit_id, inbox)
    if audit is None:
        raise ArchiveError(f"no audit {audit_id!r} in inbox")

    state = audits_mod.current_state(audit_id, audit_ledger.read_events(ledger_path))
    if state not in ARCHIVABLE:
        raise ArchiveError(
            f"audit {audit_id!r} is {state}; only a terminal audit "
            f"({' or '.join(sorted(ARCHIVABLE))}) can be archived"
        )

    dest = archive_dir / audit_id
    if dest.exists():
        raise ArchiveError(f"{dest.as_posix()} already exists; refusing to clobber an archive")
    dest.mkdir(parents=True)

    bundled: list[str] = []
    # the inbox audit (copied, never moved)
    inbox_file = inbox / audit["filename"]
    shutil.copy2(inbox_file, dest / inbox_file.name)
    bundled.append(inbox_file.name)
    # the review, if any
    review = reviews_dir / f"CLAUDE-{audit_id}.md"
    if review.exists():
        shutil.copy2(review, dest / review.name)
        bundled.append(review.name)
    # the decision, if any
    decision = decisions_dir / f"DECISION-{audit_id}.md"
    if decision.exists():
        shutil.copy2(decision, dest / decision.name)
        bundled.append(decision.name)

    rel = dest.relative_to(REPO_ROOT).as_posix() if _within(dest, REPO_ROOT) else dest.as_posix()
    return audit_ledger.append_event(
        audit_id, "AUDIT_ARCHIVED", ledger_path=ledger_path,
        actor="owner", archive=rel, bundled=bundled,
    )


# --- CLI ----------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Freeze a terminal audit into archive/.")
    sub = parser.add_subparsers(dest="cmd", required=True)
    ap = sub.add_parser("archive", help="REJECTED/VERIFIED -> ARCHIVED")
    ap.add_argument("--audit-id", required=True)
    ap.add_argument("--inbox", default=str(INBOX))
    ap.add_argument("--reviews", default=str(REVIEWS))
    ap.add_argument("--decisions", default=str(DECISIONS))
    ap.add_argument("--archive", default=str(ARCHIVE))
    ap.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
    args = parser.parse_args(argv)

    try:
        record = archive(
            args.audit_id,
            inbox=Path(args.inbox),
            reviews_dir=Path(args.reviews),
            decisions_dir=Path(args.decisions),
            archive_dir=Path(args.archive),
            ledger_path=Path(args.ledger),
        )
    except ArchiveError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    print(f"archived {args.audit_id} -> {record['archive']} ({', '.join(record['bundled'])})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
