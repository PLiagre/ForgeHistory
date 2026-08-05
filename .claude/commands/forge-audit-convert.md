---
description: Convert an approved audit into a brief seed under harness/queue/briefs/, transitioning APPROVED → CONVERTED and closing the audit → brief loop.
argument-hint: <audit_id> [--slug custom-slug]
allowed-tools: Bash
---

# /forge-audit-convert $ARGUMENTS

Turns an **approved** audit's retained points into a NEW brief seed, which
then flows through the normal harness (Planificateur → Générateur → gate →
Évaluateur). Only an `AUDIT_APPROVED` audit can be converted.

```bash
py harness/audit_convert.py convert --audit-id <audit_id> [--slug custom-slug]
```

What it creates under `harness/queue/briefs/NNN-<slug>/`:

- `brief.md` — **provenance** (which audit, which decision, which retained
  points) plus `<<TODO (planificateur)>>` placeholders for the actual spec.
- `eval-rubric.md` — a placeholder for the Planificateur to author.
- `deliverables/` — empty, ready for the Générateur.

Then it appends `AUDIT_CONVERTED` (with the new brief path) to the ledger.

## Two invariants this step protects

- **Single source of instruction.** The audit does not instruct anything.
  Once `brief.md` exists, *it* is the one instruction. The audit and the
  decision are recorded inside the brief only as provenance — never as a
  second set of orders. `test_single_source_of_instruction.py` still holds.
- **The converter does not write the spec.** It seeds placeholders; the
  Planificateur fills them before any code exists. A mechanical converter
  never reasoned about the world, so it must not put words in the
  Générateur's mouth.

## After conversion

Fill the `<<TODO>>` placeholders as the Planificateur (or run
`/forge-run <brief_dir>`), then the brief proceeds exactly like any other.
