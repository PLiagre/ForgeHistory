# ADR-0006: Full-auto agent pipeline (derogation to ADR-0005's owner step)

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-05
**Status**: accepted
**Deciders**: project owner

## Context

ADR-0005 gave Cursor a lifecycle (`AUDIT_PROPOSED -> ... -> ARCHIVED`) but
kept one deliberate manual step: the owner decides `CHALLENGED -> APPROVED /
REJECTED` via `/forge-audit-accept` or `/forge-audit-reject`
(`harness/audit_decision.py`). In practice that single human step stalls the
whole loop between sessions: findings sit `PROPOSED` in
`architecture/inbox/`, briefs converted from an audit sit unattended, and
(per brief 006's own postmortem) a single unsupervised Générateur run on
brief 003 burned 1,015 tool calls because nothing bounded the session while
waiting for a human who was not at the keyboard. Brief 006 requires an
**explicit, documented derogation** from the ADR-0005 human decision step,
replacing it with a machine-readable policy — not a second LLM "deciding"
in the owner's place, but a deterministic table of event -> condition ->
action (`harness/pipeline/auto_policy.yaml`).

## Decision

`harness/audit_decision.py` gains a `--policy auto` mode (Lot 006a) that
reads `harness/pipeline/auto_policy.yaml` and the challenged audit's
per-point review verdicts (CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER,
`architecture/reviews/CLAUDE-<id>.md`) and decides deterministically:

- >= 1 CONFIRMED or PARTIAL point -> `AUDIT_APPROVED`, `retained_points` =
  the CONFIRMED ∪ PARTIAL point numbers.
- all points REFUTED -> `AUDIT_REJECTED`.
- NEEDS_OWNER with no CONFIRMED/PARTIAL point -> `AUDIT_REJECTED`, reason
  `"policy: no owner in full_auto"` (machine-generated, never blank).

No LLM judgment happens in this decision; the policy table is the whole
rule set, and `harness/audit_ledger.py` now refuses any `event` that is not
a valid successor of the audit's current FSM state (Lot 006a), independent
of which caller — human `accept`/`reject` or `--policy auto` — tried to
write it. This closes the bypass an earlier Cursor postmortem audit
(`CURSOR-POSTMERGE-42cb054`) flagged: an `AUDIT_APPROVED` event could
previously be appended to the ledger with no prior `AUDIT_CHALLENGED`,
because only `audit_decision.decide()` enforced ordering, not the ledger
itself. `harness/audit_ledger.py:append_event` is now the single
enforcement point, so no future caller (including a Lot 006b
`orchestrator.py` that does not yet exist) can reintroduce that bypass by
calling the ledger directly.

The human `/forge-audit-accept` / `/forge-audit-reject` path (`accept`/
`reject` subcommands) is **kept, unchanged** — `mode: manual` in
`harness/pipeline/config.yaml` remains a fully supported, first-class way
to run the loop; this ADR does not remove it, only adds an alternative.

## Binding auto-merge path constraints (see brief 006's Non-Goals for the source requirement)

- **Auto-merge allowlist.** In `mode: full_auto`, a bot PR is only ever
  auto-merged if every changed path is inside
  `harness/pipeline/config.yaml`'s `auto_merge_allowlist` (currently
  `architecture/inbox/**`, `architecture/reviews/**`,
  `harness/queue/briefs/**/feedback/**`). The allowlist **excludes**
  `.github/workflows/**`, `harness/verdict_audit.py`, and `VISION.md`
  (`auto_merge_denylist` in the same file) — a PR touching any of those
  paths is never auto-merged, in any mode, **without an explicit exception
  listed by the owner in a future revision of this ADR**. No such exception
  exists today; the denylist is unconditional until one is added here by
  name.
- Cursor stays read-only outside `architecture/inbox/**` (ADR-0005,
  unchanged by this ADR).
- `mode: manual` stays available; `full_auto` is opt-in, flipped only after
  Lot 006c's end-to-end demo (see brief 006 "Lots atomiques").

## Alternatives Considered

### Alternative 1: An LLM decides accept/reject instead of a policy table
- **Pros**: Could handle nuance a fixed table misses.
- **Cons**: Reintroduces exactly the "developer judges its own work"
  failure the three-role harness exists to prevent, at one remove — an LLM
  deciding without an owner in the loop is unauditable and non-deterministic
  across runs.
- **Why not**: Brief 006 is explicit — "Règles déterministes — un LLM ne
  décide pas." A policy file can be read, diffed, and tested; an LLM verdict
  cannot.

### Alternative 2: Keep the owner step but add a timeout that defaults to REJECT
- **Pros**: Smaller change; no new policy file.
- **Cons**: Silently discards legitimate findings whenever the owner is
  simply asleep or traveling — the loop still stalls, it just stalls
  destructively (loses work) instead of stalling passively (waits).
- **Why not**: Does not solve the actual problem (throughput between
  sessions); trades one failure mode for a worse one.

## Consequences

### Positive
- The audit -> brief -> code loop can run across sessions with nobody at
  the keyboard, provided `mode: full_auto` is explicitly enabled.
- The FSM bypass identified by the postmortem audit is closed at the single
  choke point (`audit_ledger.append_event`), not per-caller.
- `auto_policy.yaml` is a versioned, diffable, testable artifact — every
  future policy change is a reviewable PR, not a change in an agent's
  judgment.

### Negative
- A wrong policy rule now has no human backstop in `full_auto` mode until
  the next review cycle notices — this is the direct cost of removing the
  owner step, accepted explicitly here rather than left implicit.
- Two more surfaces to keep in sync: the FSM transition map in
  `audit_ledger.py` and the policy table in `auto_policy.yaml` must both be
  updated whenever the lifecycle changes; a change to one without the other
  can silently narrow what the system will do (never widen it past what
  either allows, since both must agree).

### Risks

- **Erroneous automatic decision** (policy misreads a review, e.g. a
  REFUTED-heavy audit still gets approved by a parsing bug). Mitigation:
  `auto_policy.yaml` is versioned and reviewed like code;
  `harness/tests/test_audit_fsm.py` and `harness/tests/test_audit_decision.py`
  cover the decision paths; the FSM in `audit_ledger.py` is a second,
  independent guard that refuses an out-of-order event even if the policy
  logic above it is wrong.
- **Unwanted merge** (a bot PR lands somewhere it should not).
  Mitigation: the allowlist/denylist above, enforced structurally (Lot
  006b's auto-merge workflow reads `auto_merge_allowlist`/
  `auto_merge_denylist` from `config.yaml` and refuses anything outside the
  allowlist or inside the denylist — no exceptions without a named ADR
  update).
- **Runaway cost** (an unsupervised loop keeps calling agents forever,
  repeating brief 003's 1,015-call session). Mitigation: `max_forge_run_
  iterations` in `config.yaml` caps retries per brief; a budget supervisor
  (Lot 006c, not yet built) is required before `full_auto` is safe to leave
  running unattended for real; until it exists, `mode` stays `manual` by
  default in `config.yaml` (see that file's own comment).
- **Bot-only branch discipline lapses**, letting an automated actor push to
  a protected branch outside its lane. Mitigation: bot actions are scoped
  to `cursor/*` and `forge-bot/*` branches (ADR-0005's existing constraint,
  restated here as still binding); branch protection on `master` is a
  waiver-eligible check per the brief's Acceptable Waivers table, not a
  silent assumption.
