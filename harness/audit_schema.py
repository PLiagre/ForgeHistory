#!/usr/bin/env py
"""
harness/audit_schema.py -- mechanical validation of an audit's frontmatter.

The README states the schema; this enforces it. Run in CI on every PR so the
guarantees an audit's frontmatter makes cannot rot: the three *_authorized
flags really are false (an auditor never self-grants implementation rights),
the target_commit really is a full SHA (freshness can be computed), the id
really matches the filename (the ledger join key is trustworthy).

Deterministic and exit-coded, like verdict_audit.py: 0 if every audit is
valid, 1 if any is not. The logic lives here (testable locally on any OS);
the workflow only invokes it.

Usage:
  py harness/audit_schema.py [--inbox architecture/inbox]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
INBOX = REPO_ROOT / "architecture" / "inbox"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audits as audits_mod  # noqa: E402

REQUIRED = (
    "audit_id",
    "auditor",
    "target_branch",
    "target_commit",
    "created_at",
    "audit_type",
    "status",
    "implementation_authorized",
    "ci_changes_authorized",
    "code_changes_authorized",
)
MUST_BE_FALSE = (
    "implementation_authorized",
    "ci_changes_authorized",
    "code_changes_authorized",
)
SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")
ISO_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")


def validate_audit(path: Path) -> list[str]:
    """Return a list of human-readable errors for one audit file (empty = ok)."""
    path = Path(path)
    errors: list[str] = []
    meta = audits_mod.parse_frontmatter(path.read_text(encoding="utf-8"))

    if not meta:
        return [f"{path.name}: no YAML frontmatter"]

    for field in REQUIRED:
        if field not in meta:
            errors.append(f"{path.name}: missing required field `{field}`")

    for field in MUST_BE_FALSE:
        if field in meta and meta[field] is not False:
            errors.append(
                f"{path.name}: `{field}` must be false (an audit never self-authorises), got {meta[field]!r}"
            )

    status = meta.get("status")
    if status is not None and status != "PROPOSED":
        errors.append(f"{path.name}: `status` must be PROPOSED at entry, got {status!r}")

    commit = meta.get("target_commit")
    if commit is not None and not SHA_RE.match(str(commit)):
        errors.append(f"{path.name}: `target_commit` must be a full 40-hex SHA, got {commit!r}")

    created = meta.get("created_at")
    if created is not None and not ISO_RE.match(str(created)):
        errors.append(f"{path.name}: `created_at` must be ISO 8601 UTC, got {created!r}")

    audit_id = meta.get("audit_id")
    if audit_id is not None and audit_id != path.stem:
        errors.append(
            f"{path.name}: `audit_id` ({audit_id!r}) must equal the filename stem ({path.stem!r})"
        )

    return errors


def validate_inbox(inbox: Path = INBOX) -> dict[str, list[str]]:
    """Map each audit filename to its list of errors."""
    inbox = Path(inbox)
    result: dict[str, list[str]] = {}
    if not inbox.exists():
        return result
    for path in sorted(inbox.glob("CURSOR-*.md")):
        result[path.name] = validate_audit(path)
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate audit frontmatter schema.")
    parser.add_argument("--inbox", default=str(INBOX))
    args = parser.parse_args(argv)

    results = validate_inbox(Path(args.inbox))
    if not results:
        print("No audits to validate.")
        return 0

    all_errors: list[str] = []
    for name, errors in results.items():
        if errors:
            all_errors.extend(errors)
            for e in errors:
                print(f"FAIL {e}")
        else:
            print(f"OK   {name}")

    if all_errors:
        print(f"\n{len(all_errors)} schema error(s) across {len(results)} audit(s).")
        return 1
    print(f"\nAll {len(results)} audit(s) valid.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
