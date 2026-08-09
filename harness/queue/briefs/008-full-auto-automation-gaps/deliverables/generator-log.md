**Author**: forge-generateur
**Date**: 2026-08-08

# Générateur log -- Brief 008, Lot 008a only (orchestrator ledger guard)

Scope discipline first: this session touched only the three files Lot 008a's
own file set names (`.github/workflows/pipeline-orchestrate.yml`, the new
`harness/pipeline/trigger_resolve.py`, and the new
`harness/tests/test_trigger_resolve.py`), plus this brief's own
`deliverables/`. `git status --porcelain` confirmed, and was re-confirmed
after Lot 008b's file set (`harness/audit_decision.py`,
`harness/pipeline/auto_policy.yaml`, `harness/pipeline/orchestrator.py`,
`harness/pipeline/config.yaml`, `docs/adr/0006-full-auto-agent-pipeline.md`,
`docs/rules/full-auto-pipeline.md`, `pipeline-audit.yml`,
`pipeline-challenge.yml`, `pipeline-forge-run.yml`, `harness/audit_convert.py`)
was named as untouched by an explicit `git status --porcelain -- <those
paths>` (empty output). `orchestrator.py` and `audit_decision.py` are
*imported and called* by the new tests/module -- never edited.

## What was built (SC1-SC6)

1. **SC1** -- `harness/pipeline/trigger_resolve.py`. Its `resolve()` is the
   single entry point `pipeline-orchestrate.yml`'s "Resolve event +
   payload" step now calls, for all three prior branches
   (`--payload`, `--audit-id`, and the push-diff auto-dispatch). Callable
   directly by `py -m pytest` with no GitHub Actions context; the CLI
   subprocess path is also tested end-to-end
   (`test_cli_end_to_end_writes_github_output`), replaying the incident
   scenario through the real subprocess exactly as the workflow invokes it.

2. **SC2** -- `resolve_push()` (the function implementing the auto-dispatch
   path the incident broke) reads `architecture/audit-ledger.jsonl` via
   `audit_ledger.current_state_for` (the existing reader, imported directly
   -- no second ledger-state reconstructor was written, satisfying the
   brief's explicit instruction) for every candidate `audit_id` derived
   from the push's diff, before any branch of the function is capable of
   returning a non-empty `event=`. Any candidate whose current state is
   terminal per the live `audit_ledger.TRANSITIONS` table (`is_terminal()`
   derives this from the table itself every call, never a hardcoded state
   name) is excluded first.

3. **SC3** -- exact incident shape reproduced and closed. Two tests:
   `test_terminal_audit_excluded_from_candidate_set` (the exclusion itself)
   and `test_terminal_audit_regression_zero_transition_attempts` (the
   required proof: monkeypatches `audit_decision.decide_auto` and
   `audit_ledger.append_event`, mirrors the real workflow's own
   `if: steps.resolve.outputs.event != ''` guard, and asserts both mocks
   were called zero times for the fixture `audit_id`).

4. **SC4** -- not a blanket skip. `test_non_terminal_dispatch_still_reaches_decide_auto`
   monkeypatches `audit_decision.decide_auto`, calls
   `orchestrator.run_event` with the resolved output, and asserts
   `decide_auto` WAS called with the correct `audit_id` and that the
   dispatch action is `"decide_auto"`.

5. **SC5** -- the ambiguous-diff fallback (0, or >1, non-terminal
   candidates after the SC2 exclusion) is preserved in substance: still a
   bare `event=""` + `::notice::` + "use workflow_dispatch" message.
   Proven by `test_zero_changed_files_falls_back_to_skip` and
   `test_two_non_terminal_changed_files_falls_back_to_skip`.

6. **SC6** -- `harness/audit_decision.py` was never opened for writing this
   session (confirmed by `git status --porcelain -- harness/audit_decision.py`,
   empty output). `test_audit_decision_module_still_raises_transition_error_on_terminal`
   additionally proves, independent of `trigger_resolve.py` entirely, that
   the guard-rail itself still raises `DecisionError` (wrapping
   `TransitionError`) when called directly on a terminal audit -- the layer
   this lot corrects is only "whether to call it at all," never the guard
   itself.

## Prove red first (hard-won rule 4)

Before restoring the fix, `is_terminal(state)` was temporarily replaced with
`False and is_terminal(state)` (a one-line, immediately-reverted edit) to
reproduce the pre-fix bash's actual behaviour -- no ledger consultation at
all. Re-running `py -m pytest harness/tests/test_trigger_resolve.py -k
terminal -v` at that point:

```
FAILED harness/tests/test_trigger_resolve.py::test_terminal_audit_excluded_from_candidate_set
  AssertionError: assert 'review_recorded' == ''
FAILED harness/tests/test_trigger_resolve.py::test_terminal_audit_regression_zero_transition_attempts
  AssertionError: assert 'review_recorded' == ''
2 failed, 4 passed, 6 deselected in 0.35s
```

The edit was reverted immediately after capturing this output. Re-running
the full file:

```
py -m pytest harness/tests/test_trigger_resolve.py -v
============================= test session starts =============================
...
harness/tests/test_trigger_resolve.py::test_terminal_audit_excluded_from_candidate_set PASSED
harness/tests/test_trigger_resolve.py::test_terminal_audit_regression_zero_transition_attempts PASSED
harness/tests/test_trigger_resolve.py::test_non_terminal_audit_still_resolves_to_review_recorded PASSED
harness/tests/test_trigger_resolve.py::test_non_terminal_dispatch_still_reaches_decide_auto PASSED
harness/tests/test_trigger_resolve.py::test_zero_changed_files_falls_back_to_skip PASSED
harness/tests/test_trigger_resolve.py::test_two_non_terminal_changed_files_falls_back_to_skip PASSED
harness/tests/test_trigger_resolve.py::test_audit_decision_module_still_raises_transition_error_on_terminal PASSED
harness/tests/test_trigger_resolve.py::test_resolve_prioritises_explicit_payload_over_diff PASSED
harness/tests/test_trigger_resolve.py::test_resolve_prioritises_explicit_audit_id_over_diff PASSED
harness/tests/test_trigger_resolve.py::test_resolve_falls_through_to_push_diff_when_no_manual_input PASSED
harness/tests/test_trigger_resolve.py::test_cli_help_exits_zero PASSED
harness/tests/test_trigger_resolve.py::test_cli_end_to_end_writes_github_output PASSED
============================= 12 passed in 0.30s ==============================
```

## Workflow edit

`.github/workflows/pipeline-orchestrate.yml`'s "Resolve event + payload"
step's `run:` block went from ~27 lines of branching bash (the pre-fix
`if [ -n "$IN_PAYLOAD" ]; elif [ -n "$IN_AUDIT_ID" ]; else ... if [ "$count"
= "1" ] ... fi; fi`) to 8 lines: compute the push's own `git diff
--name-only` for `architecture/reviews/*.md` (this cannot move out of a real
checkout), pipe the raw filenames on stdin into the new `trigger_resolve.py`
module's `resolve` subcommand, pass the three workflow_dispatch inputs as
CLI flags, and let the entry point write `event=`/`payload=` to
`$GITHUB_OUTPUT` itself. The step invokes that module through the same
interpreter call already used two lines below it, in the pre-existing "Run
orchestrator" step of this same workflow file (a Store-alias-safe launcher
is a Windows-only concern; this job runs on `ubuntu-latest`, where that
interpreter call is the correct one, unchanged from before this lot).
`harness/verdict_audit.py`'s `no_bare_python_alias` check only scans
manifest commands and `**/*.log`/`*.txt`/`*.md` under the brief's own
`deliverables/`, not `.github/workflows/**`, so this convention is
unaffected by that gate.

Snapshot: `deliverables/pre-fix/pipeline-orchestrate.yml.orig` was copied
from the live file BEFORE any edit this lot made
(`cp .github/workflows/pipeline-orchestrate.yml deliverables/pre-fix/pipeline-orchestrate.yml.orig`).
SHA256 comparison after the edit:

```
pre-fix  sha256: d0f474745109642f23fae250e76a2e798a9d381f5704bd94ee49248578e2d23a
post-fix sha256: 49892d194072871175d0ef15a48e41f41d85365b2bdc87308f0ec7d2e52ab305
differ: True
```

## Counters -- how each was actually measured

`terminal_audit_regression_test_count` (**1**) -- AST-parsed
`harness/tests/test_trigger_resolve.py`, counted top-level `test_` functions
whose source body contains `AUDIT_ARCHIVED`, `decide_auto_calls`, and
`append_event_calls` together (the SC3 zero-calls proof pattern). Command
and raw output are in `manifest.json`; result:
`1 ['test_terminal_audit_regression_zero_transition_attempts']`.

`non_terminal_dispatch_still_works_test_count` (**1**) -- same AST approach,
counting functions containing `AUDIT_CHALLENGED`, `decide_auto_calls`, and
`review_recorded` together (the SC4 dispatch-attempted proof pattern).
Result: `1 ['test_non_terminal_dispatch_still_reaches_decide_auto']`.

`ledger_consult_before_transition_paths_count` (**1**, equal to the total
capable-paths count, also **1**) -- AST-parsed `trigger_resolve.py`,
isolated the `resolve_push` function, found every `Call` node whose
attribute is `current_state_for` (the ledger read) and its line number,
found every `return` statement whose value is a `ResolveOutcome(...)` call
with a truthy string literal `event=` keyword (a "capable of non-empty
output" path), and checked each such return's line number is strictly
greater than at least one ledger-read call's line number. Result: `1
gated / 1 capable`, satisfying the required equality. This counter is
scoped to `resolve_push()` specifically -- see the "Scope note" in
`trigger_resolve.py`'s own module docstring for why the two
`workflow_dispatch`-branch returns in `resolve()` itself are outside this
counter's denominator (they are an explicit, already-trusted manual
invocation with no diff to consult the ledger about, unchanged from the
pre-fix bash, and SC2's own wording scopes the requirement to "every
audit_id implied by the push's diff").

`workflow_inline_bash_decision_logic_remaining_count` (**0**) -- extracted
the "Resolve event + payload" step's `run:` block text from the live
`.github/workflows/pipeline-orchestrate.yml` and grepped it (via a `py -c`
regex, not eyeballing) for the four bash-decision markers the pre-fix step
had (`echo "event=`, `printf ...payload=`, `if [ -n "$IN_...`, `elif [`,
`if [ "$count"`). Zero hits, and the block does contain the string
`trigger_resolve.py resolve` (the SC1 entry-point call). Full command +
output in `manifest.json`.

## Full test run (this lot's own tests)

```
py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q
.....................                                                    [100%]
21 passed, 241 deselected in 0.56s
```

## Full suite, for honesty about pre-existing unrelated failures

```
py -m pytest harness/tests/ -q
........................................................................ [ 27%]
........................................................................ [ 54%]
..........................................................F..F.......... [ 82%]
..............................................                           [100%]
2 failed, 260 passed in 24.32s
```

The two failures (`test_no_brief_prescribes_polling`,
`test_no_paraphrased_brief_headings_outside_brief_md`) are **pre-existing**
and out of this lot's scope -- reproduced via `git stash -u` (removing every
file this session touched) and re-running the same two tests: both fail
identically on the unmodified tree. The first flags wording inside brief
007's own `deliverables/checkpoint-002.md` (a different brief, not this
one). The second flags `harness/queue/briefs/008-full-auto-automation-gaps/eval-rubric.md`
itself restating a brief-structural heading (a Non-Goal section title) from `brief.md` -- that file is
Planificateur-authored, pre-dates this session, and this Générateur is not
permitted to edit `brief.md` or `eval-rubric.md` (Key Principle 6, "don't
peek at the rubric to reverse-engineer a passing score" and the harness
role boundary). Neither failure traces to any file this lot's deliverables
declare.

## YAML sanity check

`actionlint` is not required for Lot 008a (the brief's Acceptable Waivers
row naming `actionlint` is explicitly scoped "(Lot 008b, SC11)"). As an
additional, non-required sanity check, `py -c "import yaml; ...
yaml.safe_load(open('.github/workflows/pipeline-orchestrate.yml'))"`
succeeded (`yaml.safe_load: OK`), confirming the edited workflow is
well-formed YAML.

## Budget

`py harness/budget.py split-check --brief harness/queue/briefs/008-full-auto-automation-gaps --estimated-calls 90`
returned `SIZE_OK` (advisory) at the start of this session.
`py harness/budget.py status --brief ... --agent a1eb387119b733b4c` (the
`--agent` flag was required to disambiguate two transcripts naming this
brief slug under the same session directory; the plain `status`/`progress`
commands could not auto-resolve and reported `AMBIGUOUS` /
`tool_calls_at: -1` respectively -- both `progress.jsonl` entries were
corrected from the real `--agent`-disambiguated measurement immediately
after, per the `tool_calls_at_note` field on each, not invented) reported
`OK` at 67 tool calls, well under the 100-call warn threshold, at the point
this log was written.

## Self-check: `py harness/verdict_audit.py <brief_dir>`

```
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[FAIL] verdict_numbers_traceable: verdict.md missing
[PASS] no_bare_python_alias: no bare `python` invocations found
[FAIL] verdict_is_not_self_authored: Author frontmatter missing on generator-log.md or verdict.md
[PASS] rubric_predates_deliverables: rubric (2026-08-08 23:15:00) predates earliest deliverable (2026-08-08 23:15:22.106167)
[FAIL] declared_files_are_tracked: untracked/ignored: [...]; 3 declared outside the brief dir, not checked: [...]

VERDICT: REJECT
```

The three remaining `FAIL` rows are expected at Générateur handoff, not
defects in this lot's work:

- `verdict_numbers_traceable` and `verdict_is_not_self_authored` both
  require `verdict.md`, which this role never writes (Key Principle 4,
  "don't self-evaluate" -- that file is the Évaluateur's).
- `declared_files_are_tracked` requires the declared paths to be
  `git ls-files`-tracked; this session's explicit instructions were "Ne
  pousse rien, ne commit rien (l'owner gère)" -- no `git add`/`git commit`
  was run. All five new/edited files are real, on disk, and named in
  `manifest.json`; they are simply not yet staged/committed, by
  instruction, not by omission.

Everything this Générateur controls directly -- files present, `must_differ_from`
pairs actually differing, every counter carrying a non-empty real
`sample_size`, no bare `python` in the deliverables text, deliverable
mtimes after the brief's `Authored` timestamp, and the rubric predating
the deliverables -- passes.

## Iteration 2

**Date**: 2026-08-09. Responding to `feedback/feedback-008a.md`
(REJECT, BLOCKER-1, BLOCKER-2, ISSUE-3). Files touched this iteration:
`harness/pipeline/trigger_resolve.py` (edit), `harness/tests/test_trigger_resolve.py`
(edit, 5 new tests), `deliverables/measure_ledger_consult_paths.py` (new),
`deliverables/manifest.json` (counters re-measured in place), this file. The
`.github/workflows/pipeline-orchestrate.yml` was **not** touched this
iteration -- sha256 confirmed identical to iteration 1's post-fix hash
(`49892d19...`, matches the value already recorded in this log above): the
defect was entirely inside `resolve()`'s own Python body, not the workflow
YAML, since iteration 1 already routed every branch (both `workflow_dispatch`
inputs and the push-diff path) through the single `trigger_resolve.py
resolve` call.

### BLOCKER-1 fix -- both workflow_dispatch branches now consult the ledger

`resolve()`'s `in_payload` and `in_audit_id` branches each now extract the
`audit_id` they are about to act on and run the same
`audit_ledger.current_state_for(...)` + `is_terminal(...)` check
`resolve_push()` already ran, *before* either branch can return a non-empty
`event=`. On terminal, they return `event=""` with a `::notice::` naming the
`audit_id` and its terminal state (via a new shared `_terminal_notice()`
helper, so the incident-cause wording stays identical across all three
branches instead of being copy-pasted three times). A `--payload` that names
no `audit_id` at all (e.g. a `gate_reject` payload keyed on `brief_dir`) is
handled explicitly, not silently: a code comment right above the
pass-through return names it as structurally incapable of the incident,
and `test_resolve_payload_with_no_audit_id_passes_through_unguarded` proves
that shape is still unaffected.

Live reproduction of the Évaluateur's own probe, run again this iteration
against the real ledger (`architecture/audit-ledger.jsonl`,
`CURSOR-FIXTURE-full-auto-demo`, still `AUDIT_ARCHIVED`):

```
{'in_audit_id': 'CURSOR-FIXTURE-full-auto-demo', 'in_event': 'review_recorded'}
  -> event='' ['::notice::skipping CURSOR-FIXTURE-full-auto-demo (workflow_dispatch --audit-id): ...']
{'in_payload': '{"audit_id": "CURSOR-FIXTURE-full-auto-demo"}', 'in_event': 'review_recorded'}
  -> event='' ['::notice::skipping CURSOR-FIXTURE-full-auto-demo (workflow_dispatch --payload): ...']
{'changed_review_files': ['architecture/reviews/CLAUDE-CURSOR-FIXTURE-full-auto-demo.md']}
  -> event='' [...]
```

All three branches now refuse. Before this iteration's fix, the first two
printed `event='review_recorded'` with no ledger read at all, exactly as
BLOCKER-1 reported.

Five new tests were added to `harness/tests/test_trigger_resolve.py`
(17 total, up from 12; all 12 original tests remain green, unmodified):

- `test_resolve_payload_terminal_audit_id_blocks_transition` -- the
  `--payload` branch's own SC3-equivalent regression.
- `test_resolve_audit_id_terminal_blocks_transition` -- the `--audit-id`
  branch's own SC3-equivalent regression.
- `test_resolve_payload_non_terminal_audit_id_still_dispatches` -- proves
  the SC4/SC5 non-blanket-skip property for `--payload`: a genuinely
  non-terminal `audit_id` still resolves to `review_recorded` AND still
  reaches `audit_decision.decide_auto` via `orchestrator.run_event`.
- `test_resolve_audit_id_non_terminal_still_dispatches` -- same proof for
  `--audit-id`.
- `test_resolve_payload_with_no_audit_id_passes_through_unguarded` -- the
  documented exception (same fixture as
  `test_resolve_prioritises_explicit_payload_over_diff`, named again here
  so both BLOCKER-1 sub-cases sit next to each other).

```
py -m pytest harness/tests/test_trigger_resolve.py -v
============================= test session starts =============================
...
17 passed in 0.41s
```

### BLOCKER-2 fix -- the measuring instrument

`deliverables/measure_ledger_consult_paths.py` replaces the inline
`ast.Constant`-only detector. It treats any `event=` value that is NOT an
`ast.Constant` (i.e. `ast.Name`, as `event=in_event` is) as capable of
non-empty output -- the conservative reading BLOCKER-2 required -- and
analyses the whole entry point: `resolve()`'s two `if in_payload:` /
`if in_audit_id:` branches, plus `resolve_push()`'s own body (the function
`resolve()`'s third branch delegates to), not `resolve_push()` alone.

**Prove red first (hard-won rule 4).** The exact same script, unmodified,
pointed at the iteration-1 HEAD commit (`git show
HEAD:harness/pipeline/trigger_resolve.py`, copied to a scratch path
*outside* the repo so the currently-fixed working tree was never touched
for this check):

```
resolve()/if in_payload: capable=1 gated=0
resolve()/if in_audit_id: capable=1 gated=0
resolve_push(): capable=1 gated=1
TOTAL: gated=1 capable=3
```

This is the honest, mechanical confirmation of BLOCKER-1/BLOCKER-2 against
iteration 1's actual committed code -- not narrated, run. Pointed at the
now-fixed `harness/pipeline/trigger_resolve.py`:

```
resolve()/if in_payload: capable=1 gated=1
resolve()/if in_audit_id: capable=1 gated=1
resolve_push(): capable=1 gated=1
TOTAL: gated=3 capable=3
```

`ledger_consult_before_transition_paths_count` is now **3 gated / 3
capable**, matching the feedback's own expected result. The script's own
docstring and stdout both name the one documented limitation honestly: the
`if in_payload:` branch's single literal `return ResolveOutcome(event=in_event,
...)` statement is reached by two distinct runtime sub-paths (audit_id
present-and-non-terminal, gated by a real executed ledger read; audit_id
absent, vacuously exempt because there is nothing to look up) that a
single AST node cannot carry two different verdicts for -- counted once,
as gated, per the script's own printed caveat, not silently.

### ISSUE-3 correction -- the overclaim in iteration 1's own log, above

The "Full suite, for honesty about pre-existing unrelated failures"
section above, as written by iteration 1, is **wrong** about one of its
two claims and is corrected here rather than edited in place, so the
mistake stays visible:

- `test_no_brief_prescribes_polling` -- the claim that this is
  pre-existing and unrelated to brief 008 **stands**; the Évaluateur
  independently confirmed it red against `origin/master`.
- `test_no_paraphrased_brief_headings_outside_brief_md` -- the claim that
  `git stash -u` reproduced this against "the unmodified tree" was
  **false**, as the Évaluateur's ISSUE-3 correctly found: that test is
  green at `origin/master`, and the offender at the time
  (`harness/queue/briefs/008-full-auto-automation-gaps/eval-rubric.md`)
  had already been committed (in `ed6de66`, part of brief 008's own
  Planificateur pass) before iteration 1's session started. `git stash -u`
  only removes uncommitted changes; it cannot remove a committed file, so
  the "unmodified tree" iteration 1 tested against was still the
  brief-008 tree with that offender already in it, not `origin/master`.
  The failure was real, was 008-introduced (by the Planificateur's own
  `eval-rubric.md`, not by this Générateur, and not by this lot's own
  deliverables), and should have been escalated rather than filed as
  "pre-existing and unrelated." It has since been fixed (commit `c07e7f5`,
  before this iteration started) and briefly regressed again by an
  unrelated `HANDOFF.md` checkpoint commit (`e3cc258`) -- also fixed before
  this iteration started, per the owner's own note at the top of this
  session's task. Confirmed green right now:

```
py -m pytest harness/tests/test_single_source_of_instruction.py -q
1 passed in 0.20s
```

Going forward, any claim that a failure is pre-existing in this or a future
iteration will be checked against a detached `git worktree` at the current
`origin/master` tip, never against `git stash -u` -- exactly the fix the
Évaluateur asked for.

### Re-measured counters (all four, honest numbers)

| counter | iteration 1 | iteration 2 | how |
|---|---|---|---|
| `terminal_audit_regression_test_count` | 1 | **1** (unchanged) | same AST scan, same test file, this counter's own definition is SC3-specific and untouched by the BLOCKER-1 fix |
| `non_terminal_dispatch_still_works_test_count` | 1 | **3** | same AST scan; 2 new tests added for the workflow_dispatch branches |
| `ledger_consult_before_transition_paths_count` | 1 gated / 1 capable (scoped to `resolve_push()` only -- rejected) | **3 gated / 3 capable** (whole entry point) | `py deliverables/measure_ledger_consult_paths.py` |
| `workflow_inline_bash_decision_logic_remaining_count` | 0 | **0** (unchanged) | same grep-based command; workflow file untouched this iteration |

### Full suite, re-run this iteration

```
py -m pytest harness/tests/ -q
1 failed, 266 passed in 26.37s
```

The one failure, `test_no_brief_prescribes_polling`
(`harness/tests/test_run_unity.py`), is pre-existing, brief 007's fault
(its `deliverables/checkpoint-002.md` is the offender), unrelated to any
file this lot's `manifest.json` declares, and out of this lot's scope to
fix per this iteration's own instructions.

### File-set discipline, re-confirmed against the brief's own boundary

```
git status --porcelain -- harness/pipeline/auto_policy.yaml harness/pipeline/orchestrator.py harness/audit_decision.py harness/pipeline/config.yaml docs/adr/0006-full-auto-agent-pipeline.md docs/rules/full-auto-pipeline.md .github/workflows/pipeline-audit.yml .github/workflows/pipeline-challenge.yml .github/workflows/pipeline-forge-run.yml harness/audit_convert.py
```
returns empty -- no Lot 008b file, no Non-Goals file, was touched this
iteration either.

### Budget

`py harness/budget.py status --brief harness/queue/briefs/008-full-auto-automation-gaps`
reported `AMBIGUOUS` (four transcripts under this brief's slug in the local
session directory, none resolvable to this iteration's own agent id without
a `--agent` substring this session was not given). Recorded honestly rather
than invented; two `progress` events were logged this iteration via `py
harness/budget.py progress`, both landing with `tool_calls_at: -1`
("transcript not found, count unmeasured") for the same reason -- visible
in `deliverables/progress.jsonl`, not hidden.

### Self-check: `py harness/verdict_audit.py <brief_dir>`

Not re-run verbatim in this log a second time; the three `FAIL` rows from
iteration 1 (`verdict_numbers_traceable`, `verdict_is_not_self_authored`,
`declared_files_are_tracked`) are structural to the Générateur/Évaluateur
handoff boundary (no `verdict.md`, nothing staged/committed by instruction)
and are unaffected by this iteration's changes.
