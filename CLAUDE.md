# CLAUDE.md

Guidance for Claude Code (or any agent) working in ForgeHistory.

## Project Overview

ForgeHistory is a living historical simulation engine (1400-1900), successor
to VictoriaProject. Single source of truth: World -> Country -> Province ->
City -> District -> Building -> Family -> Person. Full vision: see
[VISION.md](VISION.md).

## Langue et clarté

- Toute communication avec l'utilisateur, et tout compte-rendu écrit
  (`generator-log.md`, `verdict.md`, `checkpoint-NNN.md`, messages de
  commit, mises à jour de `HANDOFF.md`), est rédigé en **français clair**.
- Éviter le jargon technique non expliqué ; si un terme technique est
  nécessaire, l'expliquer en une phrase simple la première fois qu'il
  apparaît.
- Préférer des phrases courtes et concrètes — ce qui a été fait, pourquoi,
  ce qui reste à faire — à une narration savante ou un vocabulaire
  inutilement complexe.
- S'applique aux trois rôles du harnais (Planificateur, Générateur,
  Évaluateur) et à tout travail interactif en dehors du harnais.

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

## The Harness: Optional Three Roles

Planificateur / Générateur / Évaluateur — never the same agent in the same
pass **when the harness runs**. See
[docs/rules/harness-roles.md](docs/rules/harness-roles.md). Since ADR-0018
the default product path is Hermes (Sol 5.6, grandes étapes) then Cursor
(brief large, sous-tâches en parallèle). The harness remains an optional
proof archive ; `verdict_audit.py` still tells the truth when invoked. The
producer never merges their own work. The living product is `sim/`
(`python -m sim`) ; Unity is asleep.

## Architecture

- **ROADMAP.md** — the game/project roadmap, owned by Hermes (project
 lead, ADR-0010). Claude (CTO) reads it to plan briefs; evolutions enter
 through `hermes/requests/`.
- **hermes/** — chef de projet : propositions, rapports, demandes, crons
  de lecture, skill. Jamais le code produit, un brief ou un verdict. Voir
  `hermes/README.md` et ADR-0016.
- **control-plane/** — ForgePilot : chemin durable optionnel. Cursor
  Cloud peut livrer une PR directement (ADR-0018).
- **sim/** — **produit vivant** : moteur Python, sans Unity. Lancer
  `python -m sim`. Couche 1 commencée (011–018, snapshot `v0a-1`).
- **pipeline/geo/** — G3, G4, G5, C1, G6 livré non consommé par `sim/`.
  Suite : ressources (026), climat observé.
- **viewer/** — regard mince sur un snapshot. Pas une seconde simulation.
- **harness/** — file de briefs, gate `verdict_audit.py`, backends
  Générateur, démo faux brief.
- **unity/** — client visuel **en veille** (ADR-0016). Référence gelée,
  pas une seconde simulation.
- **architecture/** — boucle d'audit historique (jalons). Ne pas ouvrir
  au boot Hermes. Full-auto GitHub en `mode: manual`.
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
py -m sim                                 # produit vivant, sans Unity
py -m sim --ticks 0 --json                # fumée : le monde s'amorce
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
py hermes/dashboard.py                            # regenerate hermes/DASHBOARD.md
bash hermes/crons/quotidien.sh                    # veille quotidienne (mesure, pas de fusion)
cd control-plane && python3 -m unittest discover -s tests -v  # ForgePilot
.venv/bin/forgepilot enchaine <brief.md> --repo .            # aperçu du lot
.venv/bin/forgepilot enchaine <brief.md> --repo . --run      # plan→execute→draft PR→review (pas de fusion)
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
| `sim/**` | `sim/README.md` — produit vivant, sans Unity |
| `viewer/**` | `viewer/README.md` — regard mince |
| `pipeline/geo/**` | `pipeline/geo/README.md` |
| `unity/**` | en veille, ADR-0016 |
| `harness/backends/**` | `harness/backends/README.md` (pluggable-Générateur contract) |
| `architecture/**` | sur demande explicite seulement — `architecture/README.md` |
| `harness/pipeline/**` | `docs/rules/full-auto-pipeline.md` + ADR-0006 (archive, `mode: manual`) |
| `ROADMAP.md`, `hermes/**` | `hermes/README.md` + ADR-0016 + ADR-0018 |
