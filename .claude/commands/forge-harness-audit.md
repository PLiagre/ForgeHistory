---
description: Score ForgeHistory's own harness maturity (role coverage, gate/test coverage, hooks wired, docs coverage) — a deterministic checklist, not an LLM judgment.
argument-hint: (no arguments)
allowed-tools: Bash
---

# /forge-harness-audit

Adapted from ECC's `scripts/harness-audit.js` + `agents/harness-optimizer.md`
concept: a points-weighted, deterministic rubric over the harness's own
structure — not over any individual brief (that's `verdict_audit.py`'s job).

```bash
py harness/harness_audit.py
```

Report the score and every FAIL line verbatim. If the score is below the
maximum, do not editorialize about why it's "probably fine" — list exactly
which checks failed and what would need to exist to pass them (hard-won
rule 7: presence is not function; this audit only checks presence, so even
a perfect score does not certify that any of these pieces work correctly —
only `harness/tests/` and the demo pair prove that).
