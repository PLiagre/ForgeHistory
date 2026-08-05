---
description: Show the full status of one audit — current state, the whole ledger timeline, and its linked review, decision, and briefs.
argument-hint: <audit_id>
allowed-tools: Bash
---

# /forge-audit-status $ARGUMENTS

Read-only. Everything known about one audit in one place.

```bash
py harness/audits.py status --audit-id <audit_id>
```

Prints:

- **state** — resolved from the ledger (the source of truth), not the file.
- **target** — the commit and branch the audit was taken against.
- **review / decision / briefs** — the linked artifacts, drawn from the
  ledger events that recorded them (`(none yet)` when a step hasn't run).
- **timeline** — every ledger event for this audit, in order, with who did
  it.

Writes nothing. Add `--json` for the full structured view.
