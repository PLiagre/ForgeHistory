---
description: Freeze a terminal audit (REJECTED or VERIFIED) into architecture/archive/<id>/, bundling its audit, review, and decision, and recording ARCHIVED.
argument-hint: <audit_id>
allowed-tools: Bash
---

# /forge-audit-archive $ARGUMENTS

Closes the book on an audit. Only a **terminal** audit — `AUDIT_REJECTED` or
`AUDIT_VERIFIED` — can be archived.

```bash
py harness/audit_archive.py archive --audit-id <audit_id>
```

Bundles the audit's three artifacts into
`architecture/archive/<audit_id>/`:

- the inbox audit `CURSOR-<id>.md`,
- Claude's review `CLAUDE-<id>.md` (if any),
- the owner's decision `DECISION-<id>.md` (if any),

then appends `AUDIT_ARCHIVED` to the ledger.

## It copies, it never moves

The inbox stays append-only and immutable — the original audit is **not**
deleted from `inbox/`. The archive is a frozen bundle of a closed case, not
a relocation. The inbox record is the permanent provenance the whole loop
exists to keep. The command also refuses to clobber an existing archive.
