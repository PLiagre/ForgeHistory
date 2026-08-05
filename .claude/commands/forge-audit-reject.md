---
description: The owner rejects a challenged audit — records REJECTED with a rationale, transitioning CHALLENGED → REJECTED.
argument-hint: <audit_id> --reason "..."
allowed-tools: Bash
---

# /forge-audit-reject $ARGUMENTS

The **owner's** verdict to set an audit aside. Only a `CHALLENGED` audit can
be rejected — Claude's challenge (`/forge-audit-review`) must come first.

```bash
py harness/audit_decision.py reject --audit-id <audit_id> --reason "<why>"
```

- `--reason` is **required** and must be non-empty: an audit set aside
  without a recorded motive is refused. Say why it is not worth a brief
  (out of scope, already covered, refuted by the challenge, ...).

Writes `architecture/decisions/DECISION-<id>.md` (never clobbers an existing
one) and appends `AUDIT_REJECTED` to the ledger. A rejected audit goes to
`archive/` at the final step (`/forge-audit-archive`); it is never edited or
deleted from `inbox/`.
