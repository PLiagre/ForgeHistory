# Harness

The mechanism that keeps "celui qui produit ne prononce pas la recevabilité"
true in practice. See `docs/rules/harness-roles.md` for the role contract.

## Layout

- `queue/` — the brief queue. `briefs/NNN-<slug>/` holds one brief's full
  lifecycle: `brief.md`, `eval-rubric.md`, `deliverables/`, `feedback/`,
  `verdict.md`.
- `verdict_audit.py` — the tier-1 mechanical gate. Stdlib-only, zero LLM
  inference. Reads only the brief-directory contract, so it is completely
  agnostic to which backend produced the deliverables.
- `tests/` — the gate's own test suite. Every check has a fixture proving it
  can go red (hard-won rule 4) before trusting it ever goes green.
- `demo/` — `fake_brief_001/` (a deliberately forged brief that must be
  rejected, with a proof script) and `honest_brief_001/` (a clean control
  case that must be accepted — without it, a gate that always rejects would
  trivially "pass" while being useless).
- `backends/` — pluggable Générateur backends beyond the native Claude Code
  agent. Currently: Cursor CLI.

## Design Property Worth Stating Explicitly

`verdict_audit.py` never asks who produced `deliverables/manifest.json` or
`generator-log.md` — it only checks the contract those files must satisfy.
This is what makes the Générateur role backend-pluggable (see ADR-0002)
without touching the gate at all.

## Quick Reference

```bash
py harness/verdict_audit.py <brief_dir>
py -m pytest harness/tests/ -v
py harness/demo/fake_brief_001/run_demo.py
py harness/verdict_audit.py harness/demo/honest_brief_001
bash harness/backends/run_cursor_generator.sh <brief_dir>
```
