---
description: The owner accepts a challenged audit — records APPROVED with a rationale (and optionally which points are retained), transitioning CHALLENGED → APPROVED.
argument-hint: <audit_id> --reason "..." [--retain 1,2,4]
allowed-tools: Bash
---

# /forge-audit-accept $ARGUMENTS

The **owner's** verdict. Only a `CHALLENGED` audit can be accepted — Claude's
challenge (`/forge-audit-review`) must come first. Acceptance means the owner
keeps all, or some, of the audit's points for conversion into briefs.

```bash
py harness/audit_decision.py accept --audit-id <audit_id> --reason "<why>" [--retain 1,2,4]
```

- `--reason` is **required** and must be non-empty: a verdict with no
  rationale is refused. Say why these points are worth a brief.
- `--retain` is optional. Omit it to keep every point; give
  comma-separated numbers to keep a subset (the rest are simply not
  converted later).

Writes `architecture/decisions/DECISION-<id>.md` (never clobbers an existing
one) and appends `AUDIT_APPROVED` to the ledger. Next step in the loop:
`/forge-audit-convert` turns the retained points into briefs.
