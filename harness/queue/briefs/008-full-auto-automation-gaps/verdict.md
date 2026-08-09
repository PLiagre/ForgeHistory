# Verdict — Brief `008`, **LOT 008a ONLY** (orchestrator ledger guard)

**Authored**: 2026-08-09T22:05:00Z
**Author**: forge-evaluateur

> **Scope of this verdict.** Brief `008` is `NEEDS_SPLIT`. Only Lot 008a has
> been generated. This document judges Success Conditions SC1–SC6 and the
> four 008a counters. SC7–SC11 and the four 008b counters are recorded
> below as `NOT_IN_SCOPE_THIS_LOT` — neither passed nor failed here, simply
> not attempted yet. Lot 008c carries no Success Conditions by the
> Planificateur's deliberate choice.
> **Brief `008` as a whole is NOT complete.** An ACCEPT on 008a does not
> close brief `008`.

## Iteration history — kept visible, not overwritten

| iteration | commit | verdict | why |
|---|---|---|---|
| 1 | `6292e16` | **REJECT** | SC2 failed. `ledger_consult_before_transition_paths_count` was scoped to `resolve_push()` alone; measured over the real entry point it was `1` gated / `3` capable. Two of `resolve()`'s three branches emitted a non-empty `event=` on a genuinely `AUDIT_ARCHIVED` audit with no ledger read — I demonstrated this live against the real ledger. |
| 2 | `1beaa6d` | **ACCEPT** | SC2 now genuinely holds across the whole entry point, verified end-to-end through the real CLI subprocess. See below. |

The iteration-1 finding is preserved in full in the "Iteration 1 —
the rejected submission" section at the end. It is not edited away.

## Mechanical Gate Result — iteration 2

Command: `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`

All ten rows green, `VERDICT: ACCEPT`, exit `0`. Output captured verbatim at
`harness/queue/briefs/008-full-auto-automation-gaps/deliverables/evaluateur-gate-rerun.txt`
(cited by path, not re-typed — hard-won rule `12`; a `.txt` not a `.log`, so
the Execution Contract's "any proof artifact must be a committed copy under
`deliverables/`" rule can hold, since `.gitignore` excludes `*.log`).

**The gate remains necessary but not sufficient** (hard-won rule 7,
"presence is not function"). It was *also* green on iteration 1, which I
rejected. Everything below is independently reconstructed from source, not
read off the manifest.

## Per-Rubric-Line Verdict — Lot 008a, iteration 2

| # | Success Condition | PASS/FAIL | Evidence I personally ran |
|---|---|---|---|
| 1 | Resolution logic lives in a unit-testable Python entry point, invoked by the resolve step | **PASS** | `.github/workflows/pipeline-orchestrate.yml` is byte-identical to iteration 1 (blob `9c9dfb4fdda1285aa8df32e1f4ce7bd171194c77` at both `HEAD` and `HEAD~1`), and I verified its resolve step calls `trigger_resolve.py resolve` with no duplicated inline logic. `py -m pytest harness/tests/test_trigger_resolve.py -v`: `17` collected, `17` passed, no GitHub Actions context. |
| 2 | Ledger consulted for **every** path before any non-empty `event=`/`payload=` | **PASS** | The iteration-1 defect is closed. My own whole-module AST scan finds exactly `3` capable non-empty-`event=` returns (lines `180`, `245`, `253`) and exactly `3` `current_state_for` calls (lines `165`, `233`, `247`), each preceding its branch's capable return. Confirmed end-to-end through the real CLI subprocess against the live ledger, not only through the Python function — see the probe table below. The one declared exception is a genuine structural exemption, proved so by exhaustive probe, not a re-scoping — see the ruling below. |
| 3 | Exact incident shape → zero transition attempts, proven by a new regression test | **PASS** | `test_terminal_audit_regression_zero_transition_attempts` still asserts `decide_auto_calls == []` **and** `append_event_calls == []` against monkeypatched `audit_decision.decide_auto` / `audit_ledger.append_event`. Unmodified since iteration 1 (`git diff HEAD~1 HEAD` on the test file shows zero deleted lines). Re-proved red by neutralising the guard from outside the repo. |
| 4 | Not a blanket skip — non-terminal fixture still dispatches | **PASS** | Now proven for all three branches, not just the push path. `test_non_terminal_dispatch_still_reaches_decide_auto`, `test_resolve_payload_non_terminal_audit_id_still_dispatches`, `test_resolve_audit_id_non_terminal_still_dispatches` each assert `decide_auto` **was** called with the right `audit_id` through the real `orchestrator.run_event`. Confirmed live: a `workflow_dispatch --audit-id` naming the non-terminal `CURSOR-5633ee7-automation-completeness` still emits `event=review_recorded`. |
| 5 | Conservative ambiguous-diff fallback unchanged | **PASS** | `resolve_push()`'s fallback body is unchanged from iteration 1, which I had already diffed against the pre-fix snapshot and found equivalent in substance to the pre-fix bash `else` branch. The new `workflow_dispatch` guards do **not** damage SC5's escape hatch: I verified live that every *non-terminal* `audit_id` manually dispatched still resolves and still dispatches. The only capability removed is manually replaying a transition on an audit whose life is already over — which never worked anyway (it produced the `TransitionError` / exit `2` of the origin incident). Nothing that previously succeeded now fails. |
| 6 | `audit_decision.py`'s FSM guard unchanged | **PASS** | Verified against git, not the log: blob `6d89f113e2a52429d1002699d950be27ea214642` at `origin/master`, at `HEAD`, and in the worktree. Byte-identical across all three. |
| — | `must_differ_from` pair | **PASS** | Recomputed: pre-fix snapshot and live workflow differ. The snapshot remains an honest pre-fix capture — its blob `8cb5cc311ef2b38df4f3c91e68d922513f63754f` equals `origin/master`'s version of the workflow. |
| — | `workflow_inline_bash_decision_logic_remaining_count` == `0` | **PASS** | Re-measured with my own detector, not the Générateur's regex: `0` bash lines emit `event=`/`payload=` outside the entry point; the sole branching construct is a `\|\| true` guard on `git diff`, which decides nothing. |

## Reconstructed counters — mine vs. claimed (iteration 2)

Each re-derived by my own command, not by re-running theirs.

| counter | claimed | my reconstruction | agree? |
|---|---|---|---|
| `terminal_audit_regression_test_count` | 1 | 1 (`test_terminal_audit_regression_zero_transition_attempts`) | yes |
| `non_terminal_dispatch_still_works_test_count` | 3 | 3 (push-diff + both `workflow_dispatch` branches) | yes |
| `ledger_consult_before_transition_paths_count` | 3 gated / 3 capable | 3 gated / 3 capable | yes |
| `workflow_inline_bash_decision_logic_remaining_count` | 0 | 0 | yes |

For contrast, iteration 1's third counter claimed 1 gated / 1 capable and I
reconstructed 1 gated / 3 capable. That disagreement is what the REJECT
rested on; it is now resolved by fixing the code, not the measurement.

## Ruling 1 — the declared exception: **honest structural exemption**, not iteration 1's narrowing in a new coat

You were right to flag this as the move I rejected last time. It is not the
same move, and I can say why in terms of what the code can actually do.

**Iteration 1's narrowing** excluded from the denominator two branches that
carried a real `audit_id` and that I demonstrated, live, producing
`event=review_recorded` on a genuinely `AUDIT_ARCHIVED` audit. The excluded
paths were *incident-capable*. Excluding them hid a live defect.

**Iteration 2's exemption** covers the complementary sub-path: a `--payload`
that carries no `audit_id` at all. To decide whether that is incident-capable
I did not take the docstring's word for it — I probed the orchestrator
directly, dispatching every one of the `8` event kinds it accepts against
`4` adversarial payload shapes (no `audit_id` key; a terminal id smuggled
into other keys such as `audit`/`id`/`name`; empty-string `audit_id`; null
`audit_id`), with `decide_auto` and `append_event` both instrumented.

**Result: `0` transitions reached, across all `32` dispatches.** A control
dispatch with a truthy terminal `audit_id` did reach `decide_auto`, proving
the probe detects a transition when one occurs.

The reason is structural and I read it in `orchestrator.py`: every handler
capable of a ledger transition (`handle_review_recorded`,
`handle_audit_approved`, `handle_evaluateur_pass`) opens with
`_require(payload, "audit_id")`, and `_require` tests `not payload.get(k)` —
so absent, empty-string and null all fail closed. `handle_audit_pr_merge`
returns `no_op` on a falsy `audit_id`, and when truthy only appends if
`current_state_for` returns `None`, which is never true of a terminal audit.
The remaining four handlers never touch the ledger.

So the exempted sub-path names no audit, and cannot be made to name one.
The guard's domain is `audit_id`s; this sub-path has none. There is nothing
to bypass. **Honest exemption.** It is also disclosed in three places
(script docstring, script stdout, counter command string) rather than
buried, and it does not change the counter's value under either counting
convention — even counting runtime sub-paths (`4` capable, `3` executing a
read) the fourth is provably incapable of the outcome SC2 exists to prevent.

One qualification I record rather than waive: the Générateur asserted this
structural incapability in prose and docstrings; it did **not** prove it
with a test. `test_resolve_payload_with_no_audit_id_passes_through_unguarded`
only asserts the pass-through, never that no transition is reachable. I
proved the claim; they did not. The claim is true, so this is not a FAIL —
but per hard-won rule 9 the proof should live in the suite, not in my
verdict. See feedback item 1.

## Ruling 2 — red-first integrity of the committed detector: **sound, no special-casing**

The detector is now a committed deliverable and therefore gameable, so I
checked it three ways.

- **Reproduced the red/green split myself.** I extracted iteration 1's
  `trigger_resolve.py` (`git show 'HEAD~1:...'`) and iteration 2's
  (`git show 'HEAD:...'`) into two scratch trees outside the repo and ran
  the *committed, unmodified* script against each. Iteration 1:
  `TOTAL: gated=1 capable=3`. Iteration 2: `TOTAL: gated=3 capable=3`. The
  claim holds exactly, and `1`/`3` matches the number I derived by hand in
  iteration 1 before this script existed.
- **Read it for special-casing: none.** `is_capable_return` and
  `is_ledger_call` are generic AST predicates. No function name, variable
  name, or line number is special-cased to force a result. The BLOCKER-2
  rule is implemented conservatively as required: a `Constant` `event=` is
  capable iff truthy, and **any** non-`Constant` value is treated as
  capable.
- **Cross-checked its coverage with my own independent scan.** I walked the
  whole module for capable returns and confirmed the script's three
  hardcoded scopes cover all `3` of them, with none missed. The sole
  non-covered return in `resolve()`'s top-level body is
  `return resolve_push(...)`, correctly not counted as capable because it
  delegates to a scope analysed separately.

One real fragility, not a defect today: the script locates its scopes by
matching `ast.unparse(s.test) == "in_payload"` / `"in_audit_id"`. A future
fourth branch in `resolve()`, or a renamed parameter, would be silently
skipped rather than flagged. See feedback item 2.

## Ruling 3 — does the guard hold end-to-end through the real CLI? **Yes**

Not only through the Python function. I drove the actual subprocess the
workflow invokes — stdin for the diff, `--in-event`/`--in-audit-id`/`--in-payload`
flags, `--ledger architecture/audit-ledger.jsonl`, `--github-output` to a
file — against the **live** ledger, where `CURSOR-FIXTURE-full-auto-demo`
is genuinely `AUDIT_ARCHIVED` (the actual audit from the origin incident)
and `CURSOR-5633ee7-automation-completeness` is `AUDIT_CONVERTED`.

| probe | `$GITHUB_OUTPUT` written | notice? |
|---|---|---|
| push-diff naming the terminal audit (exact incident shape) | `event=` only | yes, names the audit and `AUDIT_ARCHIVED` |
| `--audit-id` naming the terminal audit | `event=` only | yes, names the branch as `workflow_dispatch --audit-id` |
| `--payload` naming the terminal audit | `event=` only | yes, names the branch as `workflow_dispatch --payload` |
| `--audit-id` naming a non-terminal audit | `event=review_recorded` + payload | none — dispatches, as it must |
| `--payload` with no `audit_id` (the exemption) | `event=gate_reject` + payload | none — pass-through, proved harmless above |

All three branches that were reachable-and-dangerous in iteration 1 now
write a bare `event=` and a `::notice::`. The downstream "Run orchestrator"
step is gated on `steps.resolve.outputs.event != ''`, so it does not run.
The incident is closed on every route into the workflow, not just the one
the incident happened to arrive by.

I also probed one adversarial shape the tests do not cover: a `--payload`
of `{"audit_id": ""}` passes through with a non-empty `event=`. It cannot
cause the incident (`_require` fails closed on the empty string, exit `2`,
no transition), and this is pre-existing orchestrator behaviour untouched
by this lot — recorded for completeness, not as a finding.

## Suite state — confirmed for the record

- `py -m pytest harness/tests/ -q` → **`1` failed, `266` passed**, exactly
  as you stated.
- The single red is `test_run_unity.py::test_no_brief_prescribes_polling`,
  offender `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/checkpoint-002.md`.
  I verified last iteration that this is red on `origin/master` in a
  detached worktree, with the identical offender. Pre-existing, brief `007`'s,
  not 008a's.
- `py -m pytest harness/tests/test_single_source_of_instruction.py -q` →
  **green**. The `HANDOFF.md` offender I found is fixed, and the two
  offenders my own `verdict.md` and `feedback-008a.md` had introduced are
  fixed too. I re-read the rewording: it renames headings and rephrases
  mentions; no finding, number, or judgment of mine was removed. Noted
  without complaint — my own artifacts caused two of the three, which is a
  fair catch against me.
- Rubric replay `py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q`
  → `26` passed.

## Red-first, independently reproduced (hard-won rule 9)

I did not accept "`17` passed" as proof the new tests are load-bearing. Two
neutralisation probes, both from outside the repo, editing no repo file:

- **Guard removed entirely** (`is_terminal` forced to `False`): `4` failed,
  `13` passed — the `2` original SC3 tests plus the `2` new
  terminal-blocking tests.
- **Iteration 1's `resolve()` restored, everything else at iteration 2**:
  **exactly** the `2` new terminal-blocking tests fail, with
  `AssertionError: assert 'review_recorded' == ''`. `2` failed, `15` passed.

The second probe is the decisive one: it isolates the new tests to the
BLOCKER-1 fix specifically. The two "still dispatches" tests correctly stay
green (they do not depend on the guard), and the exemption test correctly
stays green. The new tests are neither vacuous nor over-broad.

## SC7–SC11 and 008b counters — NOT_IN_SCOPE_THIS_LOT

Recorded so they cannot silently vanish. None is judged here; none
contributes to the 008a verdict.

| # / counter | status |
|---|---|
| SC7 — `pipeline_job_failed` rule in `auto_policy.yaml` | `NOT_IN_SCOPE_THIS_LOT` |
| SC8 — `orchestrator.py --event pipeline_job_failed` → `escalate_pipeline_stuck` | `NOT_IN_SCOPE_THIS_LOT` |
| SC9 — `workflow_run` trigger covering all four `pipeline-*.yml` | `NOT_IN_SCOPE_THIS_LOT` |
| SC10 — incident-shaped failure fixture → same escalation action | `NOT_IN_SCOPE_THIS_LOT` |
| SC11 — `actionlint` (or the named substitute) on the new workflow | `NOT_IN_SCOPE_THIS_LOT` |
| `pipeline_job_failed_policy_rule_count` | `NOT_IN_SCOPE_THIS_LOT` |
| `pipeline_job_failed_handler_test_count` | `NOT_IN_SCOPE_THIS_LOT` |
| `pipeline_workflow_run_trigger_coverage_count` | `NOT_IN_SCOPE_THIS_LOT` |
| `run_31085883052_style_escalation_regression_count` | `NOT_IN_SCOPE_THIS_LOT` |
| `must_differ_from`: `auto_policy.yaml.orig`, `orchestrator.py.orig` | `NOT_IN_SCOPE_THIS_LOT` (neither snapshot exists yet, correctly) |
| Lot 008c (`ARCH-002` / `ARCH-004`) | Blocked on an owner product decision; no Success Conditions exist by design |

## Overall Verdict: **LOT_008a: ACCEPT**

Without hedging. SC1–SC6 all pass on evidence I reconstructed myself, the
four 008a counters all reconcile with my own independent measurements, the
mechanical gate is green, the origin incident is closed on every route into
the workflow rather than only the one it arrived by, and the fix is proven
by tests I verified are load-bearing by breaking the code from outside and
watching precisely the right ones fail.

The single blocking finding from iteration 1 is genuinely fixed — fixed in
the code, not fixed in the description of the code, which is the
distinction that mattered.

**Brief `008` remains open**: Lot 008b is un-started, Lot 008c
un-specified. This ACCEPT closes Lot 008a only.

## Boundary Violations

None. Verified against `origin/master` (`32640da`), path by path, not
against the log's claim.

- `git diff --name-only origin/master...HEAD` restricted to each forbidden
  path is empty for all of: `pipeline-audit.yml`, `pipeline-challenge.yml`,
  `pipeline-forge-run.yml`, `docs/rules/full-auto-pipeline.md`,
  `harness/audit_convert.py`, `harness/pipeline/auto_policy.yaml`,
  `harness/pipeline/orchestrator.py`, `harness/pipeline/config.yaml`,
  `harness/audit_decision.py`, `docs/adr/0006-full-auto-agent-pipeline.md`,
  `sim/`, `pipeline/geo/`, `unity/`.
- `orchestrator.py` and `audit_decision.py` are *imported and called* by
  the new tests and by my own probes — never edited. Blob-verified.
- No claim of a real `gh issue create` anywhere in the deliverables.
- The workflow YAML was not touched this iteration at all.

## What Improved Since Iteration 1

- **The blocking defect was fixed in the code, not argued away.** All three
  `resolve()` branches now consult the ledger. This is the response I asked
  for and the one I would not have accepted a rationalisation in place of.
- **The escalation path was respected.** I told them that if they believed
  the `workflow_dispatch` branches should stay ungated, that was a
  Planificateur amendment and not theirs to make. They chose the other
  branch of that instruction — fix the code — rather than filing a
  counter-argument. Either was legitimate; this one is better.
- **The measuring instrument was rebuilt and committed**, so the counter is
  now reproducible by anyone rather than living in a one-off `py -c` string,
  and its red-first behaviour is verifiable against history. I verified it.
- **The overclaim was corrected additively.** The wrong iteration-1 claim is
  still on the page, marked wrong, with the reasoning; it was not edited
  into looking correct. That is the honest way to carry a mistake forward.
- **The `git stash -u` methodology error was named and replaced** with the
  detached-worktree method, as a stated forward commitment.
- **All `12` original tests are unmodified** — `git diff HEAD~1 HEAD` on the
  test file shows zero deletions. The suite grew by addition, never by
  loosening an existing assertion to make room.
- **The shared `_terminal_notice()` helper** keeps the incident-cause
  wording identical across all three branches instead of three drifting
  copies.
- **Budget ambiguity was reported honestly** (`AMBIGUOUS`, `tool_calls_at:
  -1`) rather than invented.

## What Regressed Since Iteration 1

Nothing. The workflow is byte-identical, `audit_decision.py` is
byte-identical, no original test was weakened, no Non-Goal was crossed, and
the suite improved from `2` red to `1` red (the remaining one pre-existing
and not this lot's).

## Feedback for Next Iteration

Not blocking. Lot 008a is accepted; these are for whoever touches this code
next, most likely the 008b session.

1. **Put the exemption's justification in the suite, not in a docstring.**
   The claim "a `--payload` with no `audit_id` is structurally incapable of
   the incident" is load-bearing for the counter and is currently proved
   only in prose. I proved it with a `32`-dispatch probe; that probe should
   be a test. Add one that dispatches a no-`audit_id` payload through every
   `orchestrator.EVENT_TO_RULE_IDS` key with `decide_auto` and
   `append_event` instrumented, asserting zero calls — and include a control
   dispatch with a truthy terminal `audit_id` that *does* reach
   `decide_auto`, so the test cannot pass vacuously.

2. **Pin the invariant the exemption depends on.** The exemption is true
   only because every transition-capable handler in `orchestrator.py` opens
   with `_require(payload, "audit_id")`. Lot 008b edits that file's handler
   table. If a future handler derives an `audit_id` by any other route, the
   exemption silently becomes false and no test will notice. Add an
   assertion that every handler reachable from a ledger transition requires
   a truthy `audit_id`.

3. **Harden the detector's scope discovery.** It finds its scopes by
   `ast.unparse(s.test) == "in_payload"` / `"in_audit_id"`. Make it
   enumerate every capable return in the module first and assert that its
   analysed scopes cover all of them — failing loudly if one is
   unaccounted for — rather than analysing three hardcoded branches. That
   is the check I had to run by hand to trust the number.

4. **Consider a fixture for the mixed-diff case.** A push touching two
   review files, one terminal and one not, now resolves to
   `event=review_recorded` where the pre-fix bash skipped. SC5 explicitly
   authorises this, so it is not a defect, but it is the one genuinely new
   dispatch behaviour this lot introduced and it still has no test. Carried
   over from iteration 1's feedback, still open, still optional.

5. **Note, unchanged from iteration 1:** the resolve step launches the
   module with the bare interpreter alias hard-won rule 1 forbids locally
   (`py` is the required launcher here). This matches the pre-existing
   convention in `audit-guard.yml`, `harness-ci.yml`,
   `pipeline-challenge.yml`, `pipeline-forge-run.yml` and this same file's
   "Run orchestrator" step, and these jobs run on `ubuntu-latest` where the
   Store-alias hazard does not exist. Consistent, not a defect. Recorded so
   the count stays on the books.

---

# Iteration 1 — the rejected submission (preserved)

Commit `6292e16`. **Verdict at the time: LOT_008a REJECT.** Retained in
full so the loop's history is not sanitised into a clean-looking pass.

**The blocking finding.** `ledger_consult_before_transition_paths_count`
was measured over `resolve_push()` and reported 1 gated / 1 capable. The
brief's denominator is "code paths in **that function**", where "that
function" refers to "the new/modified trigger-resolution **entry point**" —
which SC1 defines as what the workflow invokes, i.e. `resolve()`. Measured
there: **1 gated / 3 capable**. `resolve()` made zero `current_state_for`
calls; its two `workflow_dispatch` returns were ungated.

**Demonstrated live, not argued.** Against the real ledger with
`CURSOR-FIXTURE-full-auto-demo` at `AUDIT_ARCHIVED` — the actual audit from
incident run `31085883052`:

- push-diff path → `event=''` (fixed at iteration 1)
- `--audit-id CURSOR-FIXTURE-full-auto-demo` → `event='review_recorded'`,
  payload built, no ledger read, no notice — **not fixed**
- `--payload` naming the same terminal audit → same — **not fixed**

Two of three branches still handed the orchestrator a transition on an
already-terminal audit, on the route the workflow's own header calls "the
reliable path".

**Compounding defect (BLOCKER-2).** The iteration-1 detector only
recognised `event=` keywords that were `ast.Constant` literals; the two
bypass branches pass `event=in_event`, an `ast.Name`. Pointed at
`resolve()` unchanged it would still have printed 1/1. The instrument could
not see the paths in question.

**What passed at iteration 1** (and still passes): SC1, SC3, SC4, SC5, SC6,
the `must_differ_from` pair, and
`workflow_inline_bash_decision_logic_remaining_count` == 0. The pre-fix
snapshot was verified an honest capture (blob-identical to `origin/master`),
`audit_decision.py` blob-identical, red-first independently reproduced, and
every Non-Goal boundary clean.

**ISSUE-3, the overclaim.** The iteration-1 log claimed both full-suite
failures were "pre-existing", reproduced via `git stash -u`. True for
`test_no_brief_prescribes_polling` (I confirmed it red on `origin/master`).
False for `test_no_paraphrased_brief_headings_outside_brief_md`: green on
`origin/master`, and its offender (`eval-rubric.md`) was committed in
`ed6de66` as part of brief `008` itself — `git stash -u` cannot remove a
committed file, so the "unmodified tree" tested was still the brief-`008`
tree. Corrected additively by iteration 2.

**ISSUE-4, a regression I found outside the deliverables.**
`test_single_source_of_instruction.py` was red at `e3cc258`: commit
`c07e7f5` fixed the `eval-rubric.md` offender and the very next commit
reintroduced the same violation in `HANDOFF.md`. Fixed since; green now.
Two further offenders in that same class were introduced by my own
iteration-1 `verdict.md` and `feedback-008a.md`, and were fixed by the
owner. Recorded against myself as well as against the loop.

## Verification commands I ran (iteration 2, for replay)

- `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`
- `py -m pytest harness/tests/ -q`
- `py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q`
- `py -m pytest harness/tests/test_trigger_resolve.py -v`
- `py -m pytest harness/tests/test_single_source_of_instruction.py -q`
- `py -m pytest harness/tests/test_trigger_resolve.py -p plugin_prefix -q`
  (scratchpad plugin forcing `trigger_resolve.is_terminal` to `False`)
- `py -m pytest harness/tests/test_trigger_resolve.py -p plugin_it1 -q`
  (scratchpad plugin restoring iteration 1's `resolve()` body only)
- the committed `deliverables/measure_ledger_consult_paths.py`, run
  unmodified against `git show 'HEAD~1:harness/pipeline/trigger_resolve.py'`
  and `git show 'HEAD:...'` in two scratch trees outside the repo
- my own whole-module AST scan of `trigger_resolve.py`, cross-checking the
  detector's scope coverage against every capable return
- direct CLI-subprocess probes of all `5` branch shapes against the live
  `architecture/audit-ledger.jsonl`
- an exhaustive `orchestrator.run_event` probe: every event kind × `4`
  adversarial no/falsy-`audit_id` payloads, with `decide_auto` and
  `append_event` instrumented, plus a positive control
- `git hash-object` / `git show '<rev>:<path>' \| git hash-object --stdin`
  for `harness/audit_decision.py` and the workflow, across
  `origin/master`, `HEAD~1`, `HEAD`
- `git diff --name-only origin/master...HEAD -- <each Non-Goal path>`
- `git diff HEAD~1 HEAD -- harness/tests/test_trigger_resolve.py` to confirm
  zero deletions from the original `12` tests
