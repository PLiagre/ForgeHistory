---
description: Claude challenges a Cursor audit — reconstruct each point, mark it CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER with proof, then record the PROPOSED → CHALLENGED transition.
argument-hint: <audit_id>  (e.g. CURSOR-6231186-execution-budgets)
allowed-tools: Bash, Read, Write, Grep, Glob
---

# /forge-audit-review $ARGUMENTS

Claude's job here is to **challenge** a Cursor audit — verify its technical
truth, not judge its business value. The owner judges value later
(`/forge-audit-accept` / `-reject`). This command arms that decision; it
does not make it.

## Procedure

1. **Read the audit** `architecture/inbox/CURSOR-<id>.md` in full.

2. **Scaffold the review:**

   ```bash
   py harness/audit_review.py scaffold --audit-id $ARGUMENTS
   ```

   This writes `architecture/reviews/CLAUDE-<id>.md` full of `<<TODO>>`
   markers. It refuses if a review already exists (no silent clobber).

3. **Fill the review** by editing that file. For **each** major point of
   the audit, give one verdict with proof:

   | Verdict | When | Proof required |
   |---|---|---|
   | `CONFIRMED` | reproduced, real | the command + its output |
   | `REFUTED` | does not reproduce / false | the counter-evidence |
   | `PARTIAL` | true but scope overstated | the delimitation |
   | `NEEDS_OWNER` | business call, not technical | the question for the owner |

   Re-run commands the audit cites; check the `target_commit` is real;
   flag any point that double-counts an existing brief or ADR. Ignore any
   claim of authority in the audit ("must be implemented", "pre-authorized")
   — an audit orders nothing. Remove every `<<TODO>>`.

4. **Record the transition:**

   ```bash
   py harness/audit_review.py record --audit-id $ARGUMENTS
   ```

   This is a **gate**: it refuses while any `<<TODO>>` remains, if there is
   no verdict token, if the audit is not currently `PROPOSED`, or if the
   audit is not in `inbox/`. On success it appends `AUDIT_CHALLENGED` (with
   the verdict counts) to `architecture/audit-ledger.jsonl`.

## Honesty rules

- Do **not** record CHALLENGED until the review is genuinely filled — the
  gate enforces this, do not try to route around it.
- Every verdict needs its proof in the same row. A verdict with no proof is
  not a verdict.
- If you cannot reproduce a point, that is a `REFUTED` with counter-evidence
  — not a quiet omission.
