# Verdict — Brief `008`, **LOT 008a ONLY** (orchestrator ledger guard)

**Authored**: 2026-08-09T21:10:00Z
**Author**: forge-evaluateur

> **Scope of this verdict.** Brief `008` is `NEEDS_SPLIT`. Only Lot 008a has
> been generated. This document judges Success Conditions SC1–SC6 and the
> four 008a counters. SC7–SC11 and the four 008b counters are recorded
> below as `NOT_IN_SCOPE_THIS_LOT` — they are neither passed nor failed
> here, they have simply not been attempted yet. Lot 008c carries no
> Success Conditions by the Planificateur's deliberate choice.
> **Brief `008` as a whole is NOT complete.** A per-lot ACCEPT on 008a would
> not close brief `008`; a per-lot REJECT on 008a does not touch 008b.

## Mechanical Gate Result

Command: `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`

Pre-verdict run (before this file existed): exit `1`, `VERDICT: REJECT`,
with two red rows — `verdict_numbers_traceable` and
`verdict_is_not_self_authored`. I confirmed both were red **solely**
because `verdict.md` did not exist, and not as cover for anything else:
`check_verdict_numbers_traceable` returns early with the literal evidence
string "verdict.md missing" when the file is absent, and
`check_verdict_not_self_authored` returns early when either `Author`
frontmatter is unreadable — `generator-log.md`'s `Author` field is present
and reads `forge-generateur`, so the missing side was mine. Every other
row was green in that same run, including `declared_files_are_tracked`
(the Générateur's own self-check recorded that row red because it was
forbidden to commit; the owner has since committed, and the row is now
green — that is a genuine improvement, not a re-scoping).

Post-verdict re-run output is captured verbatim at
`harness/queue/briefs/008-full-auto-automation-gaps/deliverables/evaluateur-gate-rerun.txt`
(a `.txt`, not a `.log`, so the Execution Contract's "any proof artifact
must be a committed copy under `deliverables/`" rule can hold — `.gitignore`
excludes `*.log`)
(cited by path, not re-typed — hard-won rule `12`).

With this file present, that re-run turns all ten rows green and the gate
now prints `VERDICT: ACCEPT`, exit `0`. **That does not make this lot a
PASS.** The mechanical gate is necessary but not sufficient (hard-won
rule 7, "presence is not function"): it checks that a verdict exists, is
foreign-authored, and cites only traceable numbers — it does not and
cannot check whether the counter those numbers describe was measured over
the scope the brief defined. Rubric row 2 is judged below on
independently reconstructed evidence, and it fails. A green gate is not
an override.

## Per-Rubric-Line Verdict — Lot 008a

| # | Success Condition | PASS/FAIL | Evidence I personally ran |
|---|---|---|---|
| 1 | Resolution logic lives in a unit-testable Python entry point, invoked by the resolve step | **PASS** | I read `.github/workflows/pipeline-orchestrate.yml` myself: the "Resolve event + payload" step's `run:` block is now `set -euo pipefail`, one `git diff --name-only` for `architecture/reviews/*.md`, and a pipe of those filenames into `trigger_resolve.py resolve` with the three `workflow_dispatch` inputs as flags. No duplicated logic inline. I ran `py -m pytest harness/tests/test_trigger_resolve.py -v`: `12` collected, `12` passed, with no GitHub Actions context — including `test_cli_end_to_end_writes_github_output`, which drives the real subprocess. |
| 2 | Ledger consulted for **every** path before any non-empty `event=`/`payload=` | **FAIL** | See "SC2 — the finding" below. Measured over the SC1 entry point (`resolve()`), gated paths = `1`, capable paths = `3`. The required equality does not hold; it holds only under the Générateur's own re-scoping of the denominator to `resolve_push()`. |
| 3 | Exact incident shape → zero transition attempts, proven by a new regression test | **PASS** | `test_terminal_audit_regression_zero_transition_attempts` genuinely asserts `decide_auto_calls == []` **and** `append_event_calls == []` after monkeypatching `audit_decision.decide_auto` and `audit_ledger.append_event` — it is not merely an empty-`event=` string assertion. I verified it is not vacuous by neutralising the guard from outside the repo (an Évaluateur-only pytest plugin in my scratchpad forcing `trigger_resolve.is_terminal` to return `False`, editing no repo file): exactly the two SC3 tests flip red, with `AssertionError: assert 'review_recorded' == ''`. Red-first is real, independently reproduced, not taken from the log. |
| 4 | Not a blanket skip — non-terminal fixture still dispatches | **PASS** | `test_non_terminal_dispatch_still_reaches_decide_auto` asserts `decide_auto_calls == ["FIXTURE-008a-nonterminal"]` and `result["action"] == "decide_auto"` — the transition **is** attempted, through the real `orchestrator.run_event`. Under my neutralising plugin this test stays green (correctly — the non-terminal path is unaffected), which is what distinguishes it from a fixture that would pass for the wrong reason. |
| 5 | Conservative ambiguous-diff fallback unchanged | **PASS** | I diffed `deliverables/pre-fix/pipeline-orchestrate.yml.orig` against the live workflow. Pre-fix: `else` branch emits `event=` plus a `::notice::` naming the count and "Use workflow_dispatch with an explicit audit_id." Post-fix `resolve_push()` emits the same shape (`event=""`, same `::notice::` wording, same manual-dispatch instruction) for both the zero-candidate and the more-than-one-candidate case. The counting basis changes from "changed files" to "non-terminal candidates" — but SC5's own text defines the fallback as "0, or more than one, *non-terminal* candidate ... after the SC2 exclusion", so that shift is what the brief asked for, not a deviation. Covered by `test_zero_changed_files_falls_back_to_skip` and `test_two_non_terminal_changed_files_falls_back_to_skip`. |
| 6 | `audit_decision.py`'s FSM guard unchanged | **PASS** | Verified against git, not against the log: `git show 'origin/master:harness/audit_decision.py' \| git hash-object --stdin` and `git hash-object harness/audit_decision.py` both yield blob `6d89f113e2a52429d1002699d950be27ea214642`. Byte-identical. `git diff --name-only origin/master...HEAD` does not list the file. |
| — | `must_differ_from` pair | **PASS** | I recomputed both digests myself: pre-fix `d0f474745109642f23fae250e76a2e798a9d381f5704bd94ee49248578e2d23a`, post-fix `49892d194072871175d0ef15a48e41f41d85365b2bdc87308f0ec7d2e52ab305`, differ `True`. I additionally verified the snapshot is an *honest* pre-fix capture: its git blob (`8cb5cc311ef2b38df4f3c91e68d922513f63754f`) is identical to `origin/master`'s version of the workflow. It was not reconstructed after the fact. |
| — | `workflow_inline_bash_decision_logic_remaining_count` == `0` | **PASS** | Re-measured with my own detector, not the Générateur's regex: I extracted the resolve step's `run:` block and looked for any bash line emitting `event=`/`payload=` without going through the entry point (`0` found) and for any branching construct (`1` found: the `\|\| true` guard on `git diff`, which decides nothing about `event=`). Entry-point call present. |

## Reconstructed counters — mine vs. claimed

| counter | claimed | my reconstruction | agree? |
|---|---|---|---|
| `terminal_audit_regression_test_count` | 1 | 1 (`test_terminal_audit_regression_zero_transition_attempts`) | yes |
| `non_terminal_dispatch_still_works_test_count` | 1 | 1 (`test_non_terminal_dispatch_still_reaches_decide_auto`) | yes |
| `ledger_consult_before_transition_paths_count` | 1 gated / 1 capable | **1 gated / 3 capable** over the SC1 entry point | **NO** |
| `workflow_inline_bash_decision_logic_remaining_count` | 0 | 0 | yes |

## SC2 — the finding (why this lot is rejected)

The Générateur scoped `ledger_consult_before_transition_paths_count` to
`resolve_push()` and disclosed that scoping openly, in the manifest's own
command string, in the generator-log, and in a "Scope note" in the module
docstring. The disclosure is honest. **The scoping is not.**

The counter's denominator, as the brief writes it, is "the total count of
code paths in **that function** capable of producing a non-empty
`event=`/`payload=`", where "that function" refers back to "the
new/modified trigger-resolution **entry point**". SC1 names that entry
point as the thing `pipeline-orchestrate.yml`'s resolve step invokes —
which is `resolve()` (via the `resolve` subcommand), not `resolve_push()`.
The eval-rubric's row 2 repeats the requirement with no scoping at all.

My own AST walk of `harness/pipeline/trigger_resolve.py`:

- `resolve_push()` — one `current_state_for` call at line `152`; one
  return capable of a non-empty `event=` at line `167`; that return is
  line-gated by the ledger read. `1` / `1`.
- `resolve()` — **zero** `current_state_for` calls; **two** returns capable
  of a non-empty `event=` at lines `198` and `200`, neither gated by any
  ledger read; plus the delegation to `resolve_push()` at line `201`.

So over the entry point: `1` gated, `3` capable. The equality the brief
requires — "every one of them reads the ledger first — none bypasses it" —
is false.

This is not a definitional quibble. I probed it against the live ledger:

- Push-diff path, `CURSOR-FIXTURE-full-auto-demo` (real current state
  `AUDIT_ARCHIVED`, the *actual* audit from incident run `31085883052`):
  `resolve()` returns `event=''`, `payload=None`. Fixed. Good.
- `workflow_dispatch` with `--audit-id CURSOR-FIXTURE-full-auto-demo`:
  `resolve()` returns `event='review_recorded'`,
  `payload={'audit_id': 'CURSOR-FIXTURE-full-auto-demo'}`, `notices=[]`,
  and writes both lines to `$GITHUB_OUTPUT`. **Not fixed.**
- `workflow_dispatch` with an explicit `--payload` naming the same
  terminal audit: same result. **Not fixed.**

Two of the entry point's three branches will still hand the orchestrator a
transition on an already-terminal audit, with no ledger read, which is the
precise mechanism that produced `TransitionError` / exit `2` / a silently
red job in the origin incident. The workflow's own header comment calls
`workflow_dispatch` "the reliable path", so the surviving hole is on the
route the file itself advertises as primary.

A second, independent narrowing compounds this: the Générateur's detector
only counts returns whose `event=` keyword is an `ast.Constant` truthy
literal. The two `resolve()` branches pass `event=in_event`, an
`ast.Name`. Even if the detector had been pointed at `resolve()`, it would
have reported `1` / `1` and shown nothing. The measurement instrument
cannot see the paths in question.

I record the counter-argument fairly, because it has force: SC2's
subordinate clause says "for every `audit_id` implied by the push's
`architecture/reviews/*.md` diff", the `workflow_dispatch` branches are an
explicit human/CI invocation, SC5 preserves manual `workflow_dispatch` as
the escape hatch, and this behaviour is unchanged from the pre-fix bash
(so it is a *surviving* hole, not a newly introduced one). But SC2's
governing clause is "**Before that entry point ever produces a non-empty
`event=`/`payload=` pair** ... and excludes ... **before constructing any
payload**" — unqualified, over the entry point. The brief's own
World-Terms section states the world-fact as "The trigger that decides
*which* audit to act on does not consult the one ledger that already knows
an audit's status." Half the trigger still doesn't. The rubric is applied
as written (its own preamble insists on that), and as written the equality
fails.

## SC7–SC11 and 008b counters — NOT_IN_SCOPE_THIS_LOT

Recorded so they cannot silently vanish. None of these is judged here;
none of these contributes to the 008a verdict.

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

## Overall Verdict: **LOT_008a: REJECT**

Rubric row 2 fails. The rubric's own Overall Verdict Rule requires rows
1–6 to pass. Five of six pass, and pass well; one does not.

**Brief `008` remains open regardless**: Lot 008b is un-started and Lot 008c
is un-specified.

## Boundary Violations

None. Every boundary I checked is clean, verified against `origin/master`
(`32640da`) rather than against the log's claim:

- `git diff --name-only origin/master...HEAD` restricted to each forbidden
  path returns empty for all of: `pipeline-audit.yml`,
  `pipeline-challenge.yml`, `pipeline-forge-run.yml`,
  `docs/rules/full-auto-pipeline.md`, `harness/audit_convert.py`,
  `harness/pipeline/auto_policy.yaml`, `harness/pipeline/orchestrator.py`,
  `harness/pipeline/config.yaml`, `harness/audit_decision.py`,
  `docs/adr/0006-full-auto-agent-pipeline.md`, `sim/`, `pipeline/geo/`,
  `unity/`.
- The three `TODO(operator` markers still occur once each in the three
  agent-invocation workflows, unchanged in count from `origin/master`.
  The `<<TODO>>` marker in `docs/rules/full-auto-pipeline.md` likewise
  unchanged.
- Lot 008a's generator commit touched exactly its own declared file set
  plus this brief's `deliverables/` and the cost ledger. No 008b file.
- No claim of a real `gh issue create` anywhere in the deliverables.

## What Improved Since Last Iteration

Genuine improvements, stated so the feedback loop stays calibrated rather
than uniformly negative:

- **Red-first is real and independently reproducible.** I neutralised the
  guard from outside the repo and got exactly the two failures the log
  claimed, with the same assertion text. This is the strongest form of
  hard-won rule 9 compliance I can verify without trusting the author.
- **SC3's test meets the strict reading.** It asserts zero *calls* into
  both `audit_decision.decide_auto` and `audit_ledger.append_event`, not
  merely an empty `event=` string. The earlier `assert outcome.event == ""`
  does short-circuit ahead of the mock assertions, but the mock assertions
  are load-bearing: with the guard neutralised and that line relaxed, the
  workflow-mirroring `if outcome.event:` branch would fire and both lists
  would be non-empty.
- **The pre-fix snapshot is honest.** Its blob is byte-identical to
  `origin/master`'s workflow — captured before the edit, as claimed.
- **SC6 proven structurally, not narratively.** Blob-identical.
- **`is_terminal()` derives terminality from the live
  `audit_ledger.TRANSITIONS` table** rather than hardcoding
  `AUDIT_ARCHIVED`. That is the right instinct (hard-won rule `12`) and it
  is what will keep this correct if a second terminal state appears.
- **No second ledger reader was written.** SC2's explicit "not a second,
  competing ledger-state reconstruction" instruction was respected —
  `audit_ledger.current_state_for` is imported and used directly.
- **`declared_files_are_tracked` is now green**, having been red at
  Générateur handoff for a stated, legitimate reason.

## What Regressed Since Last Iteration

- **`test_single_source_of_instruction.py::test_no_paraphrased_brief_headings_outside_brief_md` is RED at HEAD, and it did not stay fixed.**
  Commit `c07e7f5` fixed the `eval-rubric.md` offender. Commit `e3cc258`
  (the `HANDOFF.md` checkpoint, written *after* that fix) reintroduced the
  same violation in a different file: the offender is now
  `('HANDOFF.md', '<the non-goals heading>')`. I confirmed the culprit with
  `git log -S'<the non-goals heading>' -- HANDOFF.md`, which names `e3cc258` alone.
  This test is **green** on `origin/master`, so it is a branch regression,
  not a pre-existing failure. It is not a Lot 008a deliverable — no file
  in `manifest.json` is the offender — so it does not by itself sink the
  lot, but the branch is red and must not be pushed in this state.

## Feedback for Next Iteration

Each item states how to fix it, specifically.

1. **SC2 / `ledger_consult_before_transition_paths_count` — the blocking
   item.** Move the terminal-state exclusion up into `resolve()` so that
   *all three* branches are gated, not one. Concretely: after the
   `in_payload` branch parses its JSON and after the `in_audit_id` branch
   forms `{"audit_id": ...}`, extract the `audit_id` from the payload and
   run the same `audit_ledger.current_state_for` +
   `is_terminal` check `resolve_push()` already runs; on terminal, return
   `ResolveOutcome(event="", notices=[...])` with the same `::notice::`
   wording naming the `audit_id` and its terminal state. If a payload
   carries no `audit_id` (e.g. the `gate_reject` shape used in
   `test_resolve_prioritises_explicit_payload_over_diff`), that branch has
   no audit to look up and should be documented as structurally incapable
   of the incident — but document it in the brief-facing counter, not only
   in a docstring. Then re-measure the counter over `resolve()` and expect
   `3` gated / `3` capable.
   **If you believe the `workflow_dispatch` branches must stay ungated**
   (a defensible product position — a human explicitly overriding), that
   is a change to the brief's counter definition, which only the
   Planificateur may make. Escalate it as a brief-amendment request; do
   not resolve it by narrowing the denominator in the generator-log.

2. **Fix the measurement instrument, not just the code.** The current AST
   detector only recognises `event=` keywords that are `ast.Constant`
   literals. Extend it to treat any non-`Constant` `event=` value (an
   `ast.Name`, an f-string, a call) as *capable of non-empty* — the
   conservative reading — otherwise the counter will keep reporting a
   clean equality over paths it structurally cannot see. Re-run it over
   `resolve()` and paste both the gated and the capable count.

3. **Close the `HANDOFF.md` single-source regression.** Rename the
   verbatim non-goals heading in `HANDOFF.md` to something that points at
   brief `008` rather than restating its structure (the same remedy
   `c07e7f5` applied to `eval-rubric.md`), then confirm
   `py -m pytest harness/tests/test_single_source_of_instruction.py -q`
   is green before any push. Add this file to whatever check runs at
   checkpoint time — the fix was applied once and immediately undone by
   the next commit, which means the loop is not catching it.

4. **Correct one overclaim in `generator-log.md` (small, but real).** The
   log states that *both* full-suite failures are "pre-existing" and were
   "reproduced via `git stash -u` ... both fail identically on the
   unmodified tree." That is true for
   `test_no_brief_prescribes_polling` — I confirmed it independently, red
   on `origin/master` (`32640da`) with the identical offender
   (`harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/checkpoint-002.md`),
   and it is *not* caused by anything 008a did. It is **false** for
   `test_no_paraphrased_brief_headings_outside_brief_md`: that test is
   green on `origin/master`, and the offender at the time
   (`eval-rubric.md`) was committed in `ed6de66` as part of brief `008`
   itself. `git stash -u` cannot remove an already-committed file, so the
   "unmodified tree" the Générateur tested was still the brief-`008` tree.
   The failure was `008`-introduced (by the Planificateur, not by this
   Générateur) — but it was not pre-existing, and it should have been
   escalated rather than filed under "unrelated". When claiming a failure
   is pre-existing, reproduce it against `origin/master` in a detached
   worktree, not against a stash.

5. **Note, not a failure: the task brief's baseline commit reference is
   stale.** `origin/master` is `32640da` (merge of PR `#11`), not
   `198cfd9` (merge of PR `#10`). Both are red for
   `test_no_brief_prescribes_polling`, so the conclusion holds, but cite
   the current tip next time.

6. **Note, not a failure: the new workflow line introduces one more
   invocation through the bare interpreter alias** that hard-won rule 1
   forbids locally (`py` is the required launcher here). I checked the
   precedent before flagging it: `audit-guard.yml`, `harness-ci.yml`,
   `pipeline-challenge.yml`, `pipeline-forge-run.yml` and this same
   workflow's pre-existing "Run orchestrator" step all already use it, and
   these jobs run on `ubuntu-latest` where the Store-alias hazard does not
   exist. So this is consistent with the repo's own convention and the
   gate does not scan workflow files. Flagged only so the count is
   recorded, not as a defect of this lot.

7. **Optional coverage gap worth one more test.** SC5's new counting basis
   means a push touching two review files, one of them terminal, now
   resolves to `event=review_recorded` where the pre-fix bash would have
   skipped. The brief authorises that broadening explicitly, so it is not
   a defect — but it is the one genuinely new dispatch behaviour this lot
   introduces and it currently has no test. Add a fixture with one
   terminal and one non-terminal changed file asserting
   `event == "review_recorded"` for the non-terminal one.

## Verification commands I ran (for replay)

- `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`
- `py -m pytest harness/tests/ -q`
- `py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q`
- `py -m pytest harness/tests/test_trigger_resolve.py -v`
- `py -m pytest harness/tests/test_trigger_resolve.py -p plugin_prefix -q`
  with a scratchpad-only plugin forcing `trigger_resolve.is_terminal` to
  `False` (no repo file edited)
- `git worktree add --detach <scratchpad> origin/master` then
  `py -m pytest <worktree>/harness/tests/test_run_unity.py::test_no_brief_prescribes_polling <worktree>/harness/tests/test_single_source_of_instruction.py -q`
- `git hash-object` / `git show 'origin/master:<path>' \| git hash-object --stdin`
  for `harness/audit_decision.py` and the workflow snapshot
- `git diff --name-only origin/master...HEAD -- <each Non-Goal path>`
- `git grep -c 'TODO(operator' origin/master -- .github/workflows/` and the
  same at `HEAD`
- my own AST walk of `harness/pipeline/trigger_resolve.py` counting
  `current_state_for` calls and non-empty-`event=`-capable returns per
  function
- direct probes of `trigger_resolve.resolve()` on all three branches
  against the real `architecture/audit-ledger.jsonl`
