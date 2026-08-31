# ADR-0001: Three-role harness (Planificateur/Générateur/Évaluateur) + mechanical gate as F0's core structural decision

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-07-29
**Status**: accepted
**Deciders**: project owner

## Context

VictoriaProject's most expensive failure mode was structural, not technical:
the same agent that wrote code also pronounced its own work acceptable, and
the only gate checked the *format* of a proof, never its *content*. Three
concrete lies passed review this way in a single brief (a stale pipeline
stage cited as the live game state, a PNG file size cited as resident memory
weight, and a false "infeasible in headless" claim contradicted by three
prior briefs). ForgeHistory's F0 exists specifically to make this class of
failure structurally impossible from day one, before any simulation code
exists.

## Decision

Adopt a strict three-role separation — Planificateur (writes the brief and
its rubric, never codes), Générateur (writes code/measurements/logs, never
pronounces acceptability), Évaluateur (writes the verdict against the
pre-written rubric, never modifies code) — never held by the same agent in
the same pass. Back it with a tier-1 mechanical gate (`verdict_audit.py`)
that performs only deterministic, LLM-free checks on a structured brief
directory, so that basic gaming (self-authored verdicts, empty-sample
"measurements," bare-claim waivers) is caught for free before any human or
adversarial review is needed.

## Alternatives Considered

### Alternative 1: Single-agent self-review
- **Pros**: simplest to wire, no coordination overhead.
- **Cons**: reproduces VictoriaProject's failure mode #7 exactly.
- **Why not**: this is the documented root cause being fixed, not a
  candidate design.

### Alternative 2: LLM-judge evaluator only, no mechanical tier
- **Cons**: an LLM judge without grounded, reconciled data can still be
  gamed by a confident-sounding but false verdict — exactly what happened in
  VictoriaProject, where the gate checked proof format, never content.
- **Why not**: violates the "prefer reconciliation over assertion" principle
  (ECC's `skills/loop-design-check`); a judge with no deterministic backstop
  is not meaningfully stronger than self-review.

### Alternative 3: Full 3-tier gate (mechanical + automatic + adversarial) from day one
- **Cons**: tier 3 (adversarial re-review) has no target briefs yet — F0 has
  no simulation code for an adversarial reviewer to interrogate.
- **Why not**: premature; adds cost and complexity with no corresponding
  risk to mitigate yet. Deferred until briefs touch the world/what-the-player
  sees.

## Consequences

### Positive
- The core failure mode (#7) is structurally blocked before any simulation
  code is written, not bolted on later.
- The mechanical gate is free and instant, so it runs on every brief with
  zero marginal cost.

### Negative
- Three-role coordination has overhead (rubric must be written before code,
  feedback must be file-mediated) compared to a single agent just building.

### Risks
- The mechanical gate can itself have false negatives (a forged brief that
  doesn't trip any of the 9 checks). Mitigated by keeping the check list
  reviewable and extensible, and by tier 3 existing as a deliberate future
  backstop once it's needed.
