# ADR-0009: Codex as an official Générateur backend

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-11
**Status**: accepted
**Deciders**: project owner

## Context

The harness already separates the Générateur interface from its backend, but
only the native Claude agent and the Cursor wrapper are documented. The owner
selected Codex as the project's development actor, while ADR-0008 and the
control delivered by brief 010 Lot 010a preserve the rule against an actor
judging its own output.

## Decision

Codex CLI is an official Générateur backend through
`harness/backends/run_codex_generator.sh`. It uses the same brief-directory
interface as the Cursor wrapper, signs its log as `forge-generateur-codex`,
records every attempted invocation in the shared backend ledger, and calls
the Lot 010a actor check before any repository write.

## Alternatives Considered

### Keep Codex as an undocumented interactive convention

- **Pros**: no wrapper to maintain.
- **Cons**: no stable invocation interface, no usage count, and no reusable
  preflight against an existing same-actor verdict.
- **Why not**: an official development actor that is absent from the harness
  contract recreates the observability gap this decision is meant to close.

## Consequences

### Positive

- `/forge-run` can select Claude, Cursor, or Codex explicitly.
- Codex invocation counts appear beside the other backends.
- The wrapper refuses a known same-actor verdict through the shared control.

### Negative

- Codex token cost is not inferred from Claude transcripts; the ledger
  measures invocation counts and reports no invented token total.
- The wrapper depends on a working, authenticated Codex CLI installation.

### Risks

- A local CLI may exist but be blocked by host permissions. The wrapper fails
  loudly and records the attempted invocation; it never falls back to another
  backend without the caller's knowledge.
