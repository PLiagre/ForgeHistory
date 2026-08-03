---
description: Report real Claude token usage per role and per brief (from session transcripts), plus Générateur invocation counts by backend.
argument-hint: (no arguments)
allowed-tools: Bash
---

# /forge-cost-report

Two measurements, deliberately kept separate because they have different
evidential standing. See `harness/backends/ledger.py`'s docstring for the
full reasoning.

## 1. Real token usage (measured)

Reads Claude Code's own session transcripts
(`~/.claude/projects/<repo slug>/`), which record a per-request usage block
for the main thread and for every subagent. These are measured token
counts, not estimates.

```bash
py harness/backends/ledger.py tokens
```

Reports total tokens and USD-at-list-price, broken down by role
(Planificateur / Générateur / Évaluateur / main thread), by brief, and by
session — plus the top transcripts by cost with their **mean context per
call**, which is the number that actually explains the bill.

Attribution to a brief comes from the brief path in the agent's own
spawning prompt — direct evidence. An agent whose prompt names no brief is
reported unattributed; never guess one from wall-clock proximity to a
ledger entry.

## 2. Backend split (invocation counts)

```bash
py harness/backends/ledger.py report
```

Counts Générateur runs per backend per brief from
`harness/queue/cost-ledger.jsonl`.

## Reading the output honestly

- **Cursor's token cost is not observable** from this environment. It is
  reported as such and excluded from the dollar total — never folded in,
  never assumed to be zero.
- **The dollar figure is a stated assumption, not a measurement.** The
  transcripts record tokens; the price table in `ledger.py` is published
  list price as of the date it names. A Claude Code subscription is not
  billed per token at all — read the USD column as relative weight between
  roles and briefs, not as an invoice.
- **A model missing from the price table is counted in tokens and listed
  as unpriced**, never silently priced at zero.
- If the transcripts or the ledger are empty or missing, say so plainly —
  do not estimate or fabricate usage numbers.
