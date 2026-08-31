# ADR-0005: Cursor Cloud as independent auditor (repositioned from Générateur backend)

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-05
**Status**: accepted
**Deciders**: project owner

## Context

ADR-0002 established Cursor CLI as a pluggable *Générateur* backend — a
second engine that could write code for a brief. In practice the owner wants
Cursor to play a different, more valuable role: an **independent auditor** of
what Claude has built, not a second developer. A single agent that both
writes and blesses its own work is exactly the failure the three-role
harness (ADR-0001) exists to prevent; extending that principle across
*tools*, the developer (Claude) and the auditor (Cursor) must never be the
same actor. Cursor Cloud already produces high-quality audits (two live
examples under `architecture/inbox/`), so the need is to give those audits a
home, a lifecycle, and a mechanical contract — without disturbing the
existing harness or forcing Cursor's use.

## Decision

Cursor is repositioned as an **independent, read-only auditor**. Audits live
under `architecture/` and move through a nine-state lifecycle
(`AUDIT_PROPOSED → CHALLENGED → APPROVED/REJECTED → CONVERTED → IMPLEMENTED →
VERIFIED → ARCHIVED`, plus `STALE`) recorded append-only in
`architecture/audit-ledger.jsonl`. Claude challenges each audit, the owner
decides, and an accepted audit is *converted* into a normal brief — so the
brief remains the single source of instruction. The `run_cursor_generator.sh`
Générateur backend from ADR-0002 is **kept but deprecated** (not removed).
Deterministic checks move to GitHub Actions (`audit-guard`, `harness-ci`,
`security`). Full design: `architecture/README.md`.

## Alternatives Considered

### Alternative 1: Keep Cursor solely as a Générateur backend
- **Pros**: No new structure; ADR-0002 stands unchanged.
- **Cons**: A tool that writes code cannot independently audit it; the
  owner's actual goal (a second, adversarial pair of eyes) goes unmet.
- **Why not**: Conflates developer and auditor — the very separation the
  harness is built on.

### Alternative 2: Let Cursor open code PRs directly from its audits
- **Pros**: Fewer steps from finding to fix.
- **Cons**: Cursor would become a developer again, self-authorizing its own
  recommendations; no human gate between "an audit said so" and "code
  changed."
- **Why not**: An audit must never be an executable instruction. Only the
  owner, via explicit conversion, turns a finding into a brief.

### Alternative 3: Store audits as GitHub issues instead of in-repo files
- **Pros**: Native discussion threads; no repo structure.
- **Cons**: State lives outside the repo, unversioned; no mechanical gate,
  no ledger, no offline reproducibility; couples the loop to GitHub's issue
  API.
- **Why not**: Forge's state must be one versioned source of truth in-repo,
  auditable without a network call.

## Consequences

### Positive
- A genuinely independent auditor, structurally barred from developing.
- The audit → brief loop is closed and fully traceable via the ledger.
- The repo gains its first real CI QA, closing the gap Cursor's own audit
  #6231186 named (`CI_GREEN_INCOMPLETE`).
- Fully additive: if Cursor is never used, `architecture/` stays inert and
  the existing harness is untouched.

### Negative
- More surface: a new directory, nine states, seven commands, three
  workflows to maintain.
- Two ledgers now exist (cost and audit); readers must know which answers
  which question.

### Risks
- **A dead branch of the loop is never archived.** Mitigation: `STALE` state
  plus `/forge-audit-status` make stuck audits visible.
- **The append-only ledgers are not yet concurrency-safe.** Mitigation:
  documented explicitly (`audit_ledger.py`); atomicity is a later,
  separately-tested hardening step, not a silent assumption.
- **CI depends on third-party tooling** (actionlint, gitleaks). Mitigation:
  all actions pinned to full commit SHAs; scanners run from pinned binaries.
