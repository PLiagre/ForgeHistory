# Brief 008: Full-auto pipeline reliability gaps (from audit CURSOR-5633ee7-automation-completeness)

**Authored**: 2026-08-08T21:15:00Z
**Author**: forge-planificateur

> **Split notice (this pass)**: this brief is `NEEDS_SPLIT`. It is filled
> for two independent, ready-now lots — **008a** and **008b** — and
> deliberately left unfilled for a third, **008c**, which is blocked on an
> owner product decision this Planificateur pass cannot make for them. See
> "## Découpage recommandé" below before reading further.

## Provenance

This brief converts the retained points of audit
`CURSOR-5633ee7-automation-completeness`.

- Audit source: `architecture/inbox/CURSOR-5633ee7-automation-completeness.md`
- Counter-audit: `architecture/reviews/CLAUDE-CURSOR-5633ee7-automation-completeness.md`
  (5/5 CONFIRMED; 1 flagged redundant with a prior audit and not retained)
- Owner decision: `architecture/decisions/DECISION-CURSOR-5633ee7-automation-completeness.md`
  (`verdict: APPROVED`, `retained_points: [1, 2, 3, 4]`)
- Retained points, mapped to this brief:
  - ARCH-001 (P0) → **Lot 008a** below
  - ARCH-002 (P1) → deferred, see **Lot 008c**
  - ARCH-003 (P1) → **Lot 008b** below
  - ARCH-004 (P2) → deferred, see **Lot 008c**
  - (ARCH-005/P2 was explicitly NOT retained by the owner — redundant with
    `CURSOR-6231186-execution-budgets.md` FINDING-ARCH-003, already open.
    Not addressed by this brief.)

An audit instructs nothing. From here, **this brief.md is the SOLE
instruction** (`CLAUDE.md` › Single Source of Instruction). The audit,
counter-audit, and decision above are *provenance*, never orders — every
Success Condition below is this Planificateur's own statement of what must
become true, in world-terms, not a paraphrase of the audit's prose.

## Découpage recommandé (Planificateur judgment — sizing this brief before writing it)

The four retained points are heterogeneous in a way that matters for
sizing, not just for topic:

- **ARCH-001** is a scoped, well-defined correction to one code path
  (`pipeline-orchestrate.yml`'s trigger-resolution logic) with a real,
  dated, reproducible incident (`gh run view 31085883052`) as its proof.
  It has **zero dependency on any unmade decision** — nothing about how to
  fix it waits on the owner.
- **ARCH-003** is a new, independent policy rule + a new/extended CI
  trigger. It touches `auto_policy.yaml` and `orchestrator.py`'s handler
  table — files ARCH-001 does not touch — and, like ARCH-001, **depends on
  no unmade decision**.
- **ARCH-002** ("the three agent invocations are `echo TODO(operator...)`
  stubs") is explicitly gated, by the audit's own §8 and the counter-audit's
  own §3, on a product decision the owner has not made: which agent/API to
  wire first, whether `mode: full_auto` should be renamed or split
  (`full_auto_decision_only`), and — critically — **what recurring
  per-invocation LLM budget in CI the owner accepts**. Writing Success
  Conditions for this now would mean a Planificateur guessing an answer to
  a question only the owner can answer; the audit's own alternatives table
  says as much ("Alternatives: Aucune; fermer réellement ce maillon dépend
  de la même décision produit").
- **ARCH-004** ("brief-fill after conversion is a documented `<<TODO>>`")
  is, per the audit's own §5, "the same problem as ARCH-002 — real agent
  invocation" wearing a documentation label. It cannot be meaningfully
  separated from that same unmade decision.

**Sizing estimate** (this role's own responsibility, no `py
harness/budget.py split-check` execution available to a Planificateur —
tools are Read/Write/Grep/Glob only, per `.claude/agents/forge-planificateur.md`):
ARCH-001 alone is estimated at **~90 tool calls** (one workflow file, one
new/extended Python entry point, two fixture regression tests, docs
pointer, manifest, generator-log — same order of magnitude as the audit's
own 60–90 estimate). ARCH-003 alone is estimated at **~90 tool calls**
(one policy rule, one orchestrator handler + test, one new/extended
`workflow_run` trigger workflow, one fixture regression test — the audit's
own 70–100 estimate). Attempted as **one** monolithic brief, the combined
cost is not merely additive (180) but higher again, per this role's own
guidance that tool-call cost is quadratic in accumulated context, not
linear — comfortably past the 150-call split threshold either way.

Independence check (the operative word, per this role's own criteria):
Lot 008a's file set (`.github/workflows/pipeline-orchestrate.yml`, a
new/extended trigger-resolution module, its own tests) and Lot 008b's file
set (`harness/pipeline/auto_policy.yaml`, `harness/pipeline/orchestrator.py`'s
handler table, a new/extended `workflow_run` trigger workflow, its own
tests) do not overlap, and neither reads, calls, or depends on the other's
output. Each is independently validatable
(`py -m pytest harness/tests/test_orchestrator.py -q` and its own new test
file) and independently gate-able.

**Decision: `NEEDS_SPLIT`.** Two atomic, fully-specified, ready-now lots —
**008a** and **008b** — below. A third, **008c**, is named but *not*
filled — see its own section — because filling it now would mean writing
Success Conditions for a scope the owner has not yet chosen the shape of.
Lot 008a and Lot 008b have no dependency on each other; either can run
first, in either order, in separate fresh sessions, exactly as the harness
roles require ("Each lot is planned for a fresh session").

## World-Terms Requirement

Stated causally, about the harness's own operational reliability — not a
tooling preference:

ForgeHistory's full-auto loop makes one specific promise:
once `mode: full_auto` is active, an accepted audit becomes a merged fix
with nobody watching the screen (`docs/rules/full-auto-pipeline.md`'s
diagram). That promise has a real, dated counter-example. On the merge
that closed PR #8, the machine built to carry that promise instead broke
it: `pipeline-orchestrate` replayed a state transition on an audit that
had already finished its life (`AUDIT_ARCHIVED`), and the resulting job
died in silence — nothing in GitHub Actions or in the ledger told anyone,
human or bot, that the loop had stopped watching itself (run
`31085883052`, `FAILURE`, exit 2). Two separate world-facts follow, and
they are genuinely two facts, not one:

1. The trigger that decides *which* audit to act on does not consult the
   one ledger that already knows an audit's status. So a push whose diff
   happens, as a side effect of how a merge or squash groups commits, to
   re-surface an already-closed review file can make the orchestrator try
   to re-poke work that is already finished — not because anyone asked it
   to, but because the trigger counts files instead of reading state.
2. Even when that mistake (or any other infra failure inside the four
   `pipeline-*.yml` jobs) produces a visibly red CI run, nothing in the
   policy table that is supposed to stand in for the owner's judgment
   recognizes "the machine itself broke" as an event worth reacting to.
   The same policy already escalates three bad Générateur attempts to a
   human-visible bot issue (`three_consecutive_mechanical_rejects`); it has
   no equivalent line for "the orchestrator's own dispatcher failed."

Until both are fixed, a repository that advertises zero-touch operation
in fact requires a human to notice, by chance, a red tab in the Actions UI
— which is exactly the failure mode `mode: full_auto` was built to remove.

## Success Conditions

### Lot 008a — the orchestrator's trigger must consult the ledger before it acts (fixes ARCH-001 / incident run 31085883052)

1. The event/payload resolution logic that decides which audit to act on
   is moved into a unit-testable Python entry point (not left only as
   inline YAML `run:` bash) invoked by `pipeline-orchestrate.yml`'s
   "Resolve event + payload" step — so the exact scenario of run
   `31085883052` can be replayed by `py -m pytest`, without needing
   GitHub Actions, to prove or disprove the fix.

2. Before that entry point ever produces a non-empty `event=`/`payload=`
   pair, it reads `architecture/audit-ledger.jsonl` (via
   `audit_ledger.current_state_for` or an equivalent already-existing
   reader — not a second, competing ledger-state reconstruction) for every
   `audit_id` implied by the push's `architecture/reviews/*.md` diff, and
   excludes from the candidate set any `audit_id` whose current FSM state
   is terminal per `audit_ledger.TRANSITIONS` (today: `AUDIT_ARCHIVED` is
   the only state mapped to an empty successor set) **before** constructing
   any payload.

3. Reproducing the exact shape of the incident — a diff naming exactly one
   changed review file whose `audit_id` is already `AUDIT_ARCHIVED` — no
   longer produces a non-empty `event=`. The workflow logs a `::notice::`
   skip naming the `audit_id` and its terminal state, and
   `orchestrator.py run` (hence `audit_decision.decide_auto` /
   `audit_ledger.append_event`) is never invoked for that `audit_id`. This
   must be proven by a **new regression test**, not by prose (hard-won
   rule 9): the test constructs a fixture ledger containing an
   `AUDIT_ARCHIVED` line for a fixture `audit_id` and a fixture diff naming
   that same review file, then asserts zero calls reach
   `audit_decision.decide_auto` / `audit_ledger.append_event` for that
   `audit_id`.

4. The fix is not a blanket skip: a second, distinct fixture in the same
   new test file — a diff naming exactly one changed review file whose
   `audit_id` is genuinely non-terminal (e.g. only `AUDIT_CHALLENGED`, no
   prior `AUDIT_ARCHIVED` line) — still resolves to `event=review_recorded`
   and still reaches `orchestrator.py run` for that `audit_id`, exactly as
   before this fix. Proven by a second test function asserting the
   transition **is** attempted.

5. The pre-existing, deliberately conservative fallback for an
   unresolvable diff shape — 0, or more than one, *non-terminal* candidate
   review file remaining after the SC2 exclusion — is unchanged: it still
   skips with a `::notice::` and still requires an explicit manual
   `workflow_dispatch --audit-id`. This lot does not attempt to resolve
   that residual ambiguity.

6. `audit_decision.py`'s own FSM guard — the code that raised
   `TransitionError` / exit 2 during the real incident — is unchanged. The
   audit's own §3 already names this "the correct and wanted behavior of
   the guard-rail"; only the layer deciding *whether to call it at all* is
   corrected by this lot.

### Lot 008b — a `pipeline-*.yml` job failure must earn the same escalation a 3-REJECT streak already earns (fixes ARCH-003)

7. `harness/pipeline/auto_policy.yaml` gains exactly one new rule,
   `pipeline_job_failed`, whose `action` names the same escalation
   semantics as the existing `three_consecutive_mechanical_rejects` rule
   (`open_bot_issue_pipeline_stuck...`) — the policy table now has a line
   for "the machine itself broke," not only "the Générateur's work was bad
   three times running."

8. `harness/pipeline/orchestrator.py` accepts a new `--event
   pipeline_job_failed` whose payload names the failed workflow (name +
   run URL); dispatching it returns the same `action:
   "escalate_pipeline_stuck"` outcome `handle_gate_reject` already returns
   for a 3-in-a-row streak. Proven by a new test in
   `harness/tests/test_orchestrator.py` (or a sibling test file under
   `harness/tests/`), not by prose.

9. A new or extended GitHub Actions workflow triggers on `workflow_run`
   `conclusion: failure` for all four `.github/workflows/pipeline-*.yml`
   files (`pipeline-audit.yml`, `pipeline-challenge.yml`,
   `pipeline-orchestrate.yml`, `pipeline-forge-run.yml`) and calls
   `orchestrator.py run --event pipeline_job_failed` with the failing
   run's name and URL in the payload. Today, **zero** workflow dispatches
   this event under any condition — this is the piece that makes SC7/SC8
   reachable in a real CI run, not only provable inside a test file.

10. Reproducing the exact shape of the incident — a `pipeline-orchestrate`
    run ending `conclusion: failure` (mirroring the real run
    `31085883052`) — is provable by a fixture test asserting the same
    `escalate_pipeline_stuck` action SC8 proves; re-triggering a real
    GitHub Actions failure is out of reach from a local session and is not
    required.

11. `actionlint` passes on the new/edited workflow file (same tool brief
    006 Lot 006b already used for `pipeline-*.yml`), or, if unavailable on
    this runner, the Acceptable Waivers row below names the accepted
    substitute — not a silent skip.

### Lot 008c — deferred, deliberately NOT specified by this pass

ARCH-002 (the three agent invocations are `TODO(operator...)` stubs) and
ARCH-004 (brief-fill after conversion is a documented `<<TODO>>`) both
depend on the same unresolved product decision the counter-audit names as
NEEDS_OWNER: which agent/API to wire first, whether `mode: full_auto`
should be renamed or split (`full_auto_decision_only`), and what recurring
per-invocation LLM budget in CI the owner accepts. This Planificateur pass
does **not** write Success Conditions, Required Counters, waivers, or a
tool-call estimate for a Lot 008c — doing so before that decision exists
would let a Planificateur's guess stand in for the owner's actual choice,
exactly the failure `docs/rules/full-auto-pipeline.md`'s own honest
`<<TODO>>` marker already refuses to hide. **When the owner decides**, a
fresh Planificateur pass converts that decision into a real Lot 008c (or a
separate brief) with its own Success Conditions, counters, and estimate.

## Non-Goals

- **Lot 008c is out of scope for this brief as written** — see above. No
  code in this brief may touch `pipeline-audit.yml`, `pipeline-challenge.yml`,
  or `pipeline-forge-run.yml`'s agent-invocation step bodies (the three
  `TODO(operator...)` lines), and no code in this brief may edit
  `docs/rules/full-auto-pipeline.md`'s `<<TODO>>` marker at line ~40 or
  `harness/audit_convert.py`'s seed-generation text.
- Neither lot implements a real `gh issue create` call. `handle_gate_reject`
  (the existing 3-REJECT escalation this brief's Lot 008b deliberately
  mirrors) is *already* log-only per `orchestrator.py`'s own docstring — no
  actual GitHub issue is opened by it today. Lot 008b matches that same
  wiring depth for symmetry and honesty; it must not silently overclaim a
  notification mechanism that does not exist yet for either escalation
  path. Building real issue-opening for either path is separate, future
  work, not promised here.
- No change to `mode: full_auto`'s value in `harness/pipeline/config.yaml`,
  and no change to `docs/adr/0006-full-auto-agent-pipeline.md`'s `Status`
  or its own text — this brief hardens machinery the ADR already
  authorizes; it does not reopen the ADR's own decision.
- No change to `audit_decision.py`'s FSM guard logic (see SC6).
- No change to `sim/`, `pipeline/geo/`, or `unity/` — this brief is
  entirely `harness/pipeline/**` and `.github/workflows/pipeline-*.yml`.
- Lot 008a must not modify any file Lot 008b's file set names, and vice
  versa (see "Découpage recommandé" § Independence check) — a Générateur
  session on one lot touching the other lot's files fails this brief's
  Non-Goals regardless of what else it gets right.
- This brief does not attempt to resolve the residual ambiguous-diff
  fallback named in SC5 (0 or >1 non-terminal candidates) — that remains
  manual `workflow_dispatch`, unchanged.

## Required Counters

| name | sample source | denominator | Lot |
|---|---|---|---|
| terminal_audit_regression_test_count | new test function(s) in `harness/tests/` added by this lot, asserting the SC3 scenario (fixture ledger `AUDIT_ARCHIVED` + fixture diff naming that review file) | must be >= 1 | 008a |
| non_terminal_dispatch_still_works_test_count | new test function(s) in the same test file, asserting the SC4 scenario (fixture ledger non-terminal + fixture diff) still dispatches | must be >= 1 | 008a |
| ledger_consult_before_transition_paths_count | static count of code paths in the new/modified trigger-resolution entry point where a ledger current-state lookup executes strictly before that path can construct a non-empty `event=`/`payload=` | must equal the total count of code paths in that function capable of producing a non-empty `event=`/`payload=` (every one of them reads the ledger first — none bypasses it) | 008a |
| workflow_inline_bash_decision_logic_remaining_count | grep of `pipeline-orchestrate.yml`'s "Resolve event + payload" step for any remaining bash-only branch that decides `event=`/`payload=` without calling the new SC1 entry point | must equal 0 | 008a |
| pipeline_job_failed_policy_rule_count | `harness/pipeline/auto_policy.yaml` top-level `rules:` entries with `event: pipeline_job_failed` | must equal 1 | 008b |
| pipeline_job_failed_handler_test_count | test function(s) in `harness/tests/test_orchestrator.py` (or a new sibling file) dispatching `--event pipeline_job_failed` and asserting `action == "escalate_pipeline_stuck"` | must be >= 1 | 008b |
| pipeline_workflow_run_trigger_coverage_count | the new/extended workflow's `workflow_run: workflows:` list, checked against the four existing `.github/workflows/pipeline-*.yml` filenames | must equal 4 (all four covered; none silently left out) | 008b |
| run_31085883052_style_escalation_regression_count | fixture test reproducing a `conclusion: failure` payload naming `pipeline-orchestrate`, asserting the same `escalate_pipeline_stuck` action the existing 3-REJECT fixture produces | must be >= 1 | 008b |

### `must_differ_from` pairs (both lots)

| artifact A (pre-fix snapshot) | artifact B (post-fix) | must differ because |
|---|---|---|
| `deliverables/pre-fix/pipeline-orchestrate.yml.orig` (008a, taken before any edit this lot makes) | `.github/workflows/pipeline-orchestrate.yml` (008a, post-fix) | proves the resolve-step logic was actually changed, not merely claimed in the generator-log |
| `deliverables/pre-fix/auto_policy.yaml.orig` (008b, taken before any edit this lot makes) | `harness/pipeline/auto_policy.yaml` (008b, post-fix) | proves the new `pipeline_job_failed` rule was actually added, not merely claimed |
| `deliverables/pre-fix/orchestrator.py.orig` (008b, taken before any edit this lot makes) | `harness/pipeline/orchestrator.py` (008b, post-fix) | proves the new handler/event wiring was actually added, not merely claimed |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "The trigger's ledger-read at resolve-step time can itself be stale — a concurrent push already changed the audit's state after this job's checkout but before its ledger read" (the audit's own named risk for Lot 008a) | reproduce the race **in a fixture test** (e.g. two sequential simulated resolve-step invocations against a ledger mutated between them) — a bare prose claim is not evidence per hard-won rule 9 | the fixture test must demonstrably show a stale read producing an attempted transition on an already-terminal `audit_id`; if the Générateur cannot construct this reproduction, the claim is not accepted as a blocker on SC1–SC4 — document the residual risk in `deliverables/generator-log.md` instead, alongside the failed reproduction attempt, not as a bare assertion |
| "`actionlint` is not installable/runnable on this runner" (Lot 008b, SC11) | `Get-Command actionlint` (or the shell-appropriate equivalent) | command not found / non-zero exit — substitute: `py -c "import yaml, sys; yaml.safe_load(open(sys.argv[1]))"` is **not** available either (repo has no PyYAML dependency, per `auto_policy.yaml`'s own header comment) — substitute is `harness/pipeline/policy_loader.py`'s existing YAML-lite parser run against the new/edited workflow file's frontmatter-shaped sections it can parse, PLUS a manual read-through documented in `deliverables/generator-log.md` naming exactly what could not be mechanically checked; a silent skip is not acceptable |

## Execution Contract

- No Unity batchmode steps in this brief — N/A (this brief is entirely
  `harness/pipeline/**` + `.github/workflows/pipeline-*.yml`).
- Estimated tool calls: **Lot 008a ≈ 90**, **Lot 008b ≈ 90** — each
  independently well under the 160-call stop and the 150-call split
  threshold; run `py harness/budget.py split-check --brief
  harness/queue/briefs/008-full-auto-automation-gaps --estimated-calls 90`
  as the first action of **each** lot's own session (per-lot, not once for
  the whole brief — the two lots are separate fresh sessions).
- Every file named in each lot's `deliverables/manifest.json` must be under
  version control. `.gitignore` excludes `*.log` and
  `unity/game_unity/Logs/` (not relevant here, but the general rule
  applies) — any proof artifact must be a committed copy under
  `deliverables/`, not a log left outside version control.
- Lot 008a and Lot 008b run as two separate `/forge-run` invocations, each
  a fresh session; neither resumes from the other's transcript. Either
  order is acceptable — see Non-Goals for the file-overlap boundary that
  keeps them independent.

## Lots atomiques (summary)

| id | objectif | dépendances | fichiers / sous-systèmes | critères d'acceptation | commande de validation | définition de terminé |
|---|---|---|---|---|---|---|
| 008a-orchestrator-ledger-guard | The orchestrator's trigger cannot re-act on an already-terminal audit, reproducing and closing incident run 31085883052 | Aucune | `.github/workflows/pipeline-orchestrate.yml`; new/extended Python trigger-resolution entry point under `harness/pipeline/`; `harness/tests/` | Success Conditions 1–6; counters `terminal_audit_regression_test_count`, `non_terminal_dispatch_still_works_test_count`, `ledger_consult_before_transition_paths_count`, `workflow_inline_bash_decision_logic_remaining_count` | `py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q` | Gate ACCEPT 008a; the SC3 fixture (exact incident shape) produces zero transition attempts, proven by a passing test, not prose |
| 008b-pipeline-failure-escalation | A `pipeline-*.yml` job failure earns the same machine escalation a 3-REJECT streak already earns | Aucune (indépendant de 008a) | `harness/pipeline/auto_policy.yaml`; `harness/pipeline/orchestrator.py`; new/extended `.github/workflows/*.yml` `workflow_run` trigger; `harness/tests/` | Success Conditions 7–11; counters `pipeline_job_failed_policy_rule_count`, `pipeline_job_failed_handler_test_count`, `pipeline_workflow_run_trigger_coverage_count`, `run_31085883052_style_escalation_regression_count` | `py -m pytest harness/tests/test_orchestrator.py -q` | Gate ACCEPT 008b; the SC10 fixture (incident-shaped failure) resolves to the same `escalate_pipeline_stuck` action the existing 3-REJECT path produces |
| 008c-full-auto-agent-invocation | (deferred) wire at least one real agent invocation (ARCH-002) and/or the brief-fill step (ARCH-004) | Owner decision: agent/API choice, `mode: full_auto` naming, recurring CI LLM budget | Not yet named — depends on the decision | Not yet written | Not yet written | **BLOCKED** — not a Générateur target until a fresh Planificateur pass follows the owner's decision |
