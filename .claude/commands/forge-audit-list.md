---
description: List every Cursor audit in architecture/inbox/, grouped by its current lifecycle state (read from the audit ledger, never from the file's frozen status).
argument-hint: (no arguments)
allowed-tools: Bash
---

# /forge-audit-list

Read-only view of the audit loop. Lists each audit under
`architecture/inbox/` with its **current** state.

```bash
py harness/audits.py list
```

## How state is decided (read this before trusting the output)

An audit file's `status:` frontmatter only says how it *entered* the loop —
always `PROPOSED`. The state shown here is the **last event recorded for
that `audit_id`** in `architecture/audit-ledger.jsonl`. An audit sitting in
`inbox/` with no ledger events yet is `AUDIT_PROPOSED`.

This keeps a single source of truth: the ledger is the timeline, the file
is the artifact. If they ever disagree, the ledger wins — never edit an
audit file to "fix" its state.

## Reading the output honestly

- This command **writes nothing**. It cannot change an audit's state; only
  the transition commands (`/forge-audit-review`, `-accept`, `-reject`,
  `-convert`, `-archive`) append to the ledger.
- An empty inbox is reported plainly as "No audits" — never invented.
- `--json` emits the full joined rows (frontmatter + resolved state) for
  scripting.
