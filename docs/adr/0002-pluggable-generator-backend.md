# ADR-0002: Pluggable Générateur backend (Claude Code default, Cursor CLI as second backend)

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-07-29
**Status**: accepted
**Deciders**: project owner

## Context

The project owner holds both a Claude Code subscription and a Cursor
subscription, and wants to spread code-generation cost across both rather
than exhausting only Claude usage. The three-role harness (ADR-0001) already
requires the Générateur's output to be Author-attributed and independently
judged; nothing in that contract requires the Générateur to run on any
particular vendor's model, as long as it produces the same file contract
(`deliverables/manifest.json`, `deliverables/generator-log.md`) that
`verdict_audit.py` reads.

## Decision

Make the Générateur role backend-pluggable: it may run as the native Claude
Code `forge-generateur` agent (default, in-session), or be delegated to
Cursor's CLI (`cursor-agent`, headless) via a wrapper script,
`harness/backends/run_cursor_generator.sh`. Planificateur and Évaluateur stay
on Claude regardless of which backend ran the Générateur, preserving judge
independence. `verdict_audit.py` requires zero changes — it only ever reads
the brief-directory contract, never which backend produced it.

## Alternatives Considered

### Alternative 1: ECC's `ccg-workflow` Codex/Gemini dispatcher (`commands/multi-*.md`)
- **Pros**: already a working cross-vendor subprocess-dispatch pattern
  (Claude -> Codex, Claude -> Gemini) with session resume.
- **Cons**: requires an external, not-installed-by-default `ccg-workflow` npm
  package; supports Codex and Gemini only — no Cursor backend exists in this
  family at all.
- **Why not**: adds a dependency for a capability (Codex/Gemini routing) we
  don't need, while not providing the one capability (Cursor routing) we do.

### Alternative 2: `ecc2`'s Rust `harness_runners` TOML config
- **Pros**: already has a tested example config for `cursor-agent` as a
  pluggable external-CLI runner (cwd/model/permission-mode/task flags, env
  injection, session naming).
- **Cons**: `ecc2` is an alpha, not-yet-GA Rust control-plane scaffold, not
  part of the documented base ECC surface; pulling it in would add a whole
  unfinished toolchain dependency for one wrapper script's worth of
  functionality.
- **Why not**: premature generality; F0 needs exactly one extra backend, not
  a generic multi-runner config system.

### Alternative 3: Bespoke shell wrapper modeled on ECC's `orchestrate-codex-worker.sh`
- **Pros**: minimal, no new dependency, copies a pattern already proven in
  this codebase's own research (spawn CLI subprocess, feed prompt via stdin,
  write handoff/status file).
- **Cons**: has to be adapted by hand since Cursor's CLI flags differ from
  Codex's.
- **Why not rejected**: this is the option chosen — smallest footprint,
  reuses a proven shape, no external dependency.

## Consequences

### Positive
- Spreads generation cost across two subscriptions, as requested.
- A Cursor-authored `generator-log.md` and a Claude-authored `verdict.md`
  are trivially different authors, incidentally strengthening check 8
  (`verdict_is_not_self_authored`) and reducing anchoring bias the same way
  ECC's `santa-loop` pattern uses two differently-vendored reviewers.

### Negative
- One more moving part (an external CLI binary + its own auth/login state)
  the harness depends on when that backend is chosen.

### Risks
- This session cannot install or run `cursor-agent` (needs the project
  owner's own Cursor login). The wrapper is written to match Cursor's
  documented CLI contract, but its first real end-to-end run is a manual
  step for the project owner, tracked in `HANDOFF.md` rather than assumed.
