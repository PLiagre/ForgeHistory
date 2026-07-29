---
description: Report Générateur invocation counts by backend (Claude vs Cursor) and by brief, from the local usage ledger.
argument-hint: (no arguments)
allowed-tools: Bash
---

# /forge-cost-report

Reports usage from `harness/queue/cost-ledger.jsonl` — see
`harness/backends/ledger.py`'s docstring for what this does and does not
claim to measure (invocation counts, an honest proxy for backend spend
distribution; not a dollar figure, since Cursor's own cost isn't observable
from this environment).

```bash
py harness/backends/ledger.py report
```

If the ledger is empty or missing, say so plainly — do not estimate or
fabricate usage numbers.
