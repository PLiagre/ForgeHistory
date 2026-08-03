# CLAUDE.md

Guidance for Claude Code (or any agent) working in ForgeHistory.

## Project Overview

ForgeHistory is a living historical simulation engine (1400-1900), successor
to VictoriaProject. Single source of truth: World -> Country -> Province ->
City -> District -> Building -> Family -> Person. Full vision: see
[VISION.md](VISION.md).

## Non-Negotiable Principles

1. One source of truth — views never become parallel databases.
2. The engine reasons in world-terms, never gameplay-terms.
3. The economy is physical — nothing teleports; everything has origin,
   transport, storage, destination.

Full text + the seven diagnosed failure modes and their countermeasures:
see [docs/rules/simulation-principles.md](docs/rules/simulation-principles.md).
Do not paraphrase that file here.

## Hard-Won Rules

Every rule in [docs/rules/hard-won-rules.md](docs/rules/hard-won-rules.md)
was paid for by a real defect in VictoriaProject. Read it before writing any
check, counter, or gate. Do not restate it here — this pointer is
intentional (see "Single Source of Instruction" below).

## The Harness: Three Roles, Never One Agent

Planificateur / Générateur / Évaluateur — never the same agent in the same
pass. See [docs/rules/harness-roles.md](docs/rules/harness-roles.md) and
`.claude/agents/forge-*.md`. The Générateur role is backend-pluggable
(Claude Code by default, or Cursor CLI via `harness/backends/`) — see
[harness/backends/README.md](harness/backends/README.md).

## Architecture

- **sim/** — the simulation engine, testable without Unity. Empty stub (F1+).
- **pipeline/geo/** — geo/map pipeline, incl. sources.lock. Empty stub (F1).
- **harness/** — brief queue, three-role agents' shared contract, the
  mechanical gate (`verdict_audit.py`), pluggable Générateur backends, and
  the fake-brief rejection demo.
- **unity/** — thin render client, zero simulation logic. Empty stub.
- **docs/adr/** — one structural decision = one ADR, dated.
- **docs/rules/** — modular, auto-referenced rules (never paraphrased
  elsewhere).
- **.claude/agents/** — forge-planificateur, forge-generateur, forge-evaluateur.
- **.claude/skills/forge-harness/** — how to run a brief through the harness.
- **.claude/commands/** — `/forge-run`, `/forge-cost-report`,
  `/forge-harness-audit`, `/forge-checkpoint`.
- **.claude/hooks/** — mechanical guards: no bare `python`, no `git push`
  while tests are red, no silent `VISION.md` edits, stale-`HANDOFF.md`
  warning.

## Key Commands

```bash
py harness/verdict_audit.py <brief_dir>          # tier-1 mechanical gate
py -m pytest harness/tests/ -v                    # gate's own test suite (red+green)
py harness/demo/fake_brief_001/run_demo.py        # F0 proof: fake brief is rejected
py harness/verdict_audit.py harness/demo/honest_brief_001   # control: honest brief accepted
bash harness/backends/run_cursor_generator.sh <brief_dir>   # delegate Générateur to Cursor CLI
py harness/backends/ledger.py report              # backend usage (Claude vs Cursor invocation counts)
py harness/backends/ledger.py tokens              # real Claude token cost per role / per brief (reads session transcripts)
py harness/harness_audit.py                       # harness maturity self-audit
py harness/budget.py status --brief <brief_dir>   # execution budget (100 warn / 130 checkpoint / 160 stop)
py harness/budget.py split-check --brief <brief_dir>  # advisory NEEDS_SPLIT pre-flight, before generation
unity/run-unity.ps1 -LogFile <abs> -UnityArgLine '<unity args>'  # Unity batchmode: one call, waits, no polling
```

Slash commands: `/forge-run <brief>` (full loop), `/forge-cost-report`,
`/forge-harness-audit`, `/forge-checkpoint` (rewrite HANDOFF.md from live
state — see `.claude/commands/`).

## Single Source of Instruction

Exactly one document says what an agent must do for a brief: the brief file
itself under `harness/queue/briefs/`. Any other file may point to it; none
may paraphrase it. Enforced by
`harness/tests/test_single_source_of_instruction.py`.

## Status

See [HANDOFF.md](HANDOFF.md) — rewritten at the end of every session.

## Routing

| File(s) | Use |
|---|---|
| `harness/queue/briefs/**` | `.claude/skills/forge-harness/SKILL.md` |
| any `docs/adr/NNNN-*.md` | conventions in `docs/adr/template.md` |
| `sim/**` | not yet populated — see `sim/README.md` (F1+) |
| `pipeline/geo/**` | not yet populated — see `pipeline/geo/README.md` (F1) |
| `harness/backends/**` | `harness/backends/README.md` (pluggable-Générateur contract) |
