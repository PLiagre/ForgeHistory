# ADR-0007: Split `mode: full_auto` into `full_auto_decision_only` and a reserved, fail-closed `full_auto`

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-10
**Status**: accepted
**Deciders**: project owner (2026-08-09 product decision, converted by brief
009 Lot 009a)

## Context

ADR-0006 introduced a single value, `mode: full_auto`, naming ALL of
`harness/pipeline/config.yaml`'s automation at once: Cursor audit, Claude
challenge review, owner-decision fusion, brief conversion, AND
`claude-developer`'s own `/forge-run` invocation. As of brief 009's own
audit of the repository (see brief 009's "World-Terms Requirement"),
`pipeline-challenge.yml`'s real-invocation step and `pipeline-forge-run.yml`'s
own invocation step both still contain a `TODO(operator...)` stub, not
executable code — a fact independently confirmed by a counter-audit
(`architecture/reviews/CLAUDE-CURSOR-5633ee7-automation-completeness.md`,
FINDING-ARCH-002 CONFIRMED). A single config value that names more
automation than a given moment of this repository actually wires is itself
an operational risk: a reader could reasonably, and wrongly, conclude
`forge-run` also fires unattended once `mode: full_auto` is set — it does
not, and would not, until a future brief wires that maillon for real.

## Decision

`harness/pipeline/config.yaml`'s `mode:` key now accepts two values going
forward: `manual` (unchanged, the ADR-0005 human loop) and
`full_auto_decision_only` (new — audit -> challenge -> owner-decision
fusion only, the portion this repository actually runs unattended as of
this ADR). The bare, unqualified `full_auto` is reserved for the future
state where `forge-run`'s own invocation is wired for real; until then it
is refused fail-closed by a new validation module,
`harness/pipeline/full_auto_mode_guard.py`, whenever
`.github/workflows/pipeline-forge-run.yml` still contains the literal
string `TODO(operator`. That guard re-checks the real workflow file on
every call rather than caching a verdict, so the refusal lifts on its own,
with no code change, the moment a future lot removes that stub marker —
proven by a companion test against a fixture copy of the same file with the
marker removed (`harness/tests/test_mode_guard.py`), never the real file.

**This narrows ADR-0006, it does not reverse it** — mirroring how ADR-0006
itself amended ADR-0005 without rewriting it. Every mechanism ADR-0006
introduced (the deterministic policy table, the FSM enforcement in
`audit_ledger.append_event`, the auto-merge allow/deny-list, the budget
supervisor) stays exactly as ADR-0006 describes it; only the single scalar
value naming "how much of it is switched on right now" gains a second,
narrower name so it stops overstating what is wired. `docs/adr/
0006-full-auto-agent-pipeline.md`'s own `Status` and body text are
unchanged by this ADR.

## Alternatives Considered

### Alternative 1: Keep the single `full_auto` value, rely on documentation to clarify scope
- **Pros**: No config/code change; smaller diff.
- **Cons**: Exactly the failure this ADR exists to close — a value can only
  be read one way by a human or a workflow, and prose describing it as
  "audit+challenge only for now" does not stop a future reader (or a future
  workflow author) from wiring `forge-run` under the same flag by mistake.
- **Why not**: The World-Terms Requirement (brief 009) is explicit that a
  config value overstating its own scope is itself an operational risk, not
  merely an inaccuracy.

### Alternative 2: Two entirely separate top-level keys instead of a value split (e.g. `mode_decision: on`, `mode_forge_run: off`)
- **Pros**: Each maillon toggled independently, finer-grained.
- **Cons**: A larger surface change (every reader/workflow of `mode:` today
  would need updating to read two keys instead of one), disproportionate to
  what brief 009 actually wires this pass (one maillon, `claude-challenger`,
  in Lot 009c). The single-key value split covers exactly what is needed
  today without pre-building a multi-key scheme nothing yet exercises.
- **Why not**: Scope discipline — brief 009's own Non-Goals keep this lot to
  the split named by the owner's decision, not a broader redesign.

## Consequences

### Positive
- `mode: full_auto_decision_only` in `config.yaml` now means exactly what
  this repository can do today — no config value overstates its own scope.
- The migration off the bare `full_auto` value is fail-closed and provably
  reversible in one direction only: it becomes valid again automatically
  once forge-run is genuinely wired, never by a silent policy change.
- ADR-0006's own mechanisms are entirely preserved; nothing built for Lot
  006a/006b/006c is rewritten or bypassed.

### Negative
- One more concept (`full_auto` vs. `full_auto_decision_only`) for a future
  reader of `config.yaml` to learn — mitigated by the guard's own error
  message naming the correct value, and by `docs/rules/full-auto-pipeline.md`
  §"How to activate" naming it explicitly.
- Any future brief that wires `forge-run` for real must remember this ADR
  exists and that `full_auto` becomes valid again automatically at that
  point — a silent behavior change from that lot's own perspective if its
  author does not read this ADR first. Mitigated by
  `full_auto_mode_guard.py`'s own docstring naming the exact condition.

### Risks
- **A future lot removes the `TODO(operator` marker from
  `pipeline-forge-run.yml` for a reason unrelated to actually wiring
  forge-run** (e.g. a comment rewrite), silently re-validating `full_auto`
  without forge-run actually being ready. Mitigation: the guard's own test
  suite (`harness/tests/test_mode_guard.py`) includes a control test
  (`test_stub_marker_still_present_in_real_forge_run_workflow_control`)
  that goes red the moment the marker disappears from the real file,
  forcing a reviewer to look at exactly this ADR before that change lands
  quietly.
