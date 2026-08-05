#!/usr/bin/env py
"""
harness/audits.py -- read-only view over the audit loop.

This module is the audit-domain counterpart to the brief queue: it reads
`architecture/inbox/CURSOR-*.md`, parses each audit's frontmatter, and
reports where each audit currently stands in its lifecycle.

Single source of state, on purpose: an audit's *current* state is NOT the
`status:` frozen in its file (that only records how it entered -- always
PROPOSED). It is the last event recorded for that `audit_id` in
`architecture/audit-ledger.jsonl`. The file says how it started; the ledger
says what has happened since. Reading state from the file would create a
second, drifting source of truth -- exactly the failure Forge's
"one source of truth" principle exists to prevent.

Frontmatter is parsed by a tiny flat `key: value` reader rather than a YAML
library: the audit frontmatter is deliberately flat, PyYAML is not a repo
dependency, and pulling one in for ten scalar fields would be a real
dependency for a trivial need.

This step (migration step 3) is READ-ONLY. It never writes the ledger, an
audit, or anything else. Later steps add the writing commands.

Usage:
  py harness/audits.py list [--json]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
INBOX = REPO_ROOT / "architecture" / "inbox"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_ledger  # noqa: E402

# Lifecycle order, used only to group the listing predictably. An audit
# with no ledger events yet is PROPOSED by virtue of sitting in inbox/.
LIFECYCLE = audit_ledger.VALID_EVENTS
DEFAULT_STATE = "AUDIT_PROPOSED"


def parse_frontmatter(text: str) -> dict:
    """Return the flat key/value frontmatter of a markdown document.

    Handles only the leading `---` ... `---` block. Values `true`/`false`
    become booleans; everything else stays a string. Lines without a colon,
    and everything after the closing fence, are ignored.
    """
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return {}
    meta: dict = {}
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        key, value = key.strip(), value.strip()
        if value in ("true", "false"):
            meta[key] = value == "true"
        else:
            meta[key] = value
    return meta


def load_audits(inbox: Path = INBOX) -> list[dict]:
    """Every audit in inbox/, as {frontmatter..., 'path', 'filename'}.

    Sorted by filename for a stable listing. A missing inbox is simply an
    empty list -- a repo that has never been audited, not an error.
    """
    inbox = Path(inbox)
    if not inbox.exists():
        return []
    audits: list[dict] = []
    for path in sorted(inbox.glob("CURSOR-*.md")):
        meta = parse_frontmatter(path.read_text(encoding="utf-8"))
        try:
            meta["path"] = path.relative_to(REPO_ROOT).as_posix()
        except ValueError:
            # inbox outside the repo (e.g. a test tmp dir, or another drive)
            meta["path"] = path.as_posix()
        meta["filename"] = path.name
        audits.append(meta)
    return audits


def current_state(audit_id: str, events: list[dict]) -> str:
    """The last ledger event for this audit, or PROPOSED if none."""
    for event in reversed(events):
        if event.get("audit_id") == audit_id:
            return event.get("event", DEFAULT_STATE)
    return DEFAULT_STATE


def build_listing(inbox: Path = INBOX, ledger_path: Path | None = None) -> list[dict]:
    """Join inbox audits with their current ledger state, richest first."""
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH
    events = audit_ledger.read_events(ledger_path)
    rows: list[dict] = []
    for audit in load_audits(inbox):
        audit_id = audit.get("audit_id") or audit["filename"].removesuffix(".md")
        rows.append({**audit, "state": current_state(audit_id, events)})
    return rows


# --- CLI ----------------------------------------------------------------


def _short(commit: str) -> str:
    return (commit or "")[:7] if commit else "???????"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="List audits by lifecycle state.")
    sub = parser.add_subparsers(dest="cmd", required=True)
    lp = sub.add_parser("list", help="list audits grouped by state")
    lp.add_argument("--inbox", default=str(INBOX))
    lp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
    lp.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)

    rows = build_listing(Path(args.inbox), Path(args.ledger))

    if args.json:
        print(json.dumps(rows, ensure_ascii=False, indent=2))
        return 0

    if not rows:
        print("No audits in architecture/inbox/.")
        return 0

    by_state: dict[str, list[dict]] = {}
    for row in rows:
        by_state.setdefault(row["state"], []).append(row)

    total = len(rows)
    print(f"{total} audit(s) in architecture/inbox/\n")
    for state in LIFECYCLE:
        group = by_state.get(state)
        if not group:
            continue
        print(f"[{state}]  ({len(group)})")
        for row in group:
            print(
                f"  {row.get('audit_id', row['filename'])}"
                f"  |  {_short(row.get('target_commit', ''))}"
                f"  |  {row.get('created_at', '?')}"
                f"  |  {row.get('auditor', '?')}"
            )
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
