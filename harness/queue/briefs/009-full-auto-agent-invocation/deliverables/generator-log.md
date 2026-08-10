**Author**: forge-generateur
**Date**: 2026-08-10

# Générateur log -- Brief 009, Lot 009a only (mode split, fail-closed, ADR-0007)

Scope discipline first: this session touched only the files Lot 009a's own
row in "Lots atomiques" names -- `harness/pipeline/config.yaml`,
`harness/pipeline/auto_policy.yaml`, a new validation module under
`harness/pipeline/`, `docs/adr/0007-full-auto-mode-split.md` (new),
`docs/adr/README.md`, `docs/rules/full-auto-pipeline.md`, plus this brief's
own `deliverables/`. `git status --short` (captured before staging) showed
no other path touched: no `.github/workflows/*.yml` file anywhere,
`docs/adr/0006-full-auto-agent-pipeline.md` untouched (`git diff HEAD --
docs/adr/0006-full-auto-agent-pipeline.md` -- 0 lines), and nothing under
`harness/pipeline/ci_budget_guard.py`/`ci-budget-ledger.jsonl` or
`.github/workflows/pipeline-challenge.yml` (Lot 009b's and 009c's own file
sets) was created or edited.

## First action

`py harness/budget.py split-check --brief harness/queue/briefs/009-full-auto-agent-invocation --estimated-calls 100`
returned `advisory: SIZE_OK` -- consistent with the brief's own
mechanically-confirmed 009a estimate (100 calls, `SIZE_OK`). Real output:

```
advisory   : SIZE_OK   (advisory -- the Planificateur decides)
brief      : 009-full-auto-agent-invocation
estimated  : 100

signals (reported, NOT triggers -- see the note in budget.py: on the 5
briefs whose real cost is known, none of these separated cheap from
expensive, and subsystem breadth pointed the wrong way):
  subsystems in Success Conditions : 0  []
  success conditions               : 0
  global-goal phrasing             : 'whole' present

Judge these yourself, they are not counted for you:
  - are the subsystems genuinely INDEPENDENT of each other?
  - could any deliverable be validated on its own?
  - does the brief read as a global goal ('port the whole game')?
```

Pre-fix snapshots (`deliverables/pre-fix/{config.yaml,auto_policy.yaml,
full-auto-pipeline.md}.orig`) were taken immediately after, before any
edit. Verification: this branch (`forge/cursor-audit-loop`) has diverged
from `origin/master` in both directions (15 commits ahead, 8 behind --
`git rev-list --count HEAD ^origin/master` / `... origin/master ^HEAD`), so
a byte comparison against `origin/master` is not a meaningful proof that no
prior-session edit slipped in (this branch's own prior lots, e.g. 008b's
`auto_policy.yaml` rule addition, are ahead of `origin/master` legitimately
and would show as a spurious diff). The comparison that actually proves
"taken before this session's first edit" is against `HEAD` (this branch's
own last commit) instead: `git diff --stat HEAD -- harness/pipeline/
config.yaml harness/pipeline/auto_policy.yaml docs/rules/
full-auto-pipeline.md` printed nothing (exit 0, no output) at snapshot
time, and a direct `diff` of each `.orig` file against the live file
confirmed byte-for-byte identity before the first edit.

## What was built

**A new fail-closed validation module**,
`harness/pipeline/full_auto_mode_guard.py` (`validate_mode(mode,
forge_run_workflow=...)`, raising `ModeGuardError` on refusal, returning
`None` on acceptance -- no boolean a caller could ignore). Two values are
always valid (`manual`, `full_auto_decision_only`); the bare `full_auto` is
valid ONLY if the workflow file passed in (default: the real
`.github/workflows/pipeline-forge-run.yml`) does not contain the literal
`TODO(operator` marker -- re-read on every call, never cached, so a future
lot that genuinely wires forge-run lifts the refusal automatically with no
code change here. Any other value, or an unreadable/missing workflow file,
is refused -- fail-closed applies to I/O errors too, not only to known-bad
literal values.

**Both branches of that guard are proven** (`harness/tests/
test_mode_guard.py`, 9 tests):
- `test_bare_full_auto_refused_while_forgerun_unwired` -- against the REAL
  on-disk `pipeline-forge-run.yml` (which still contains the stub marker),
  asserts `ModeGuardError` is raised.
- `test_full_auto_accepted_once_forgerun_wired` -- against a `tmp_path`
  FIXTURE copy of that same file with the marker replaced, asserts
  `validate_mode` does NOT raise. Never touches the real workflow file
  (Lot 009a's own Non-Goal boundary: it must not touch any
  `.github/workflows/*.yml`).
- A control test, `test_stub_marker_still_present_in_real_forge_run_
  workflow_control`, asserts the real file still contains the marker --
  if a future, unrelated lot removes it, this test goes red first and
  loudly, rather than the SC1 refusal test silently starting to pass for
  the wrong reason.
- Five further tests cover `manual`/`full_auto_decision_only` acceptance
  regardless of wiring state, an unknown-literal refusal, a
  missing-workflow-file refusal (I/O fail-closed), an empty-string
  refusal, and a regression guard asserting `config.yaml`'s live `mode:`
  reads `full_auto_decision_only` and independently passes the guard.

**`harness/pipeline/config.yaml`** -- `mode:` rewritten from `full_auto` to
`full_auto_decision_only`; the surrounding comment block rewritten to
describe the split and point at ADR-0007 and the new guard module. No other
line in the file changed (`git diff HEAD -- harness/pipeline/config.yaml`
touches only that one comment block + the `mode:` line).

**`harness/pipeline/auto_policy.yaml`** -- the top-level documentation
scalar (line 15 before this edit) rewritten from `mode: full_auto` to
`mode: full_auto_decision_only`, with its comment corrected to state this
key is documentation only (not parsed), so it cannot silently disagree with
`config.yaml`'s real, live value. No rule under `rules:` touched.

**`docs/adr/0007-full-auto-mode-split.md`** (new) -- records the split, the
fail-closed migration rule, and states explicitly that ADR-0006 is narrowed,
not reversed (mirroring how ADR-0006 itself amended ADR-0005).
`docs/adr/0006-full-auto-agent-pipeline.md` was never opened for writing
this session -- confirmed by `git diff HEAD -- docs/adr/
0006-full-auto-agent-pipeline.md` printing nothing.

**`docs/adr/README.md`** -- two rows added: the pre-existing missing row for
ADR-0006 (found stale, fixed here per the brief's own instruction that
finding it cheap-to-fix in the same lot beats leaving it worse than found),
and the new row for ADR-0007. Zero rows removed or edited.

**`docs/rules/full-auto-pipeline.md`** -- "How to activate" step 3 corrected
to name `full_auto_decision_only` instead of the now-refused bare
`full_auto`, with a short pointer to the guard module and ADR-0007. No
other line in the file changed (`grep -n "full_auto"` after the edit shows
exactly the one corrected line; no other stale mention of the bare value
remains anywhere in the file).

## Prove red first (hard-won rule 4)

`test_config_yaml_current_mode_is_now_full_auto_decision_only` was written
and run BEFORE `config.yaml` was edited:

```
FAILED harness/tests/test_mode_guard.py::test_config_yaml_current_mode_is_now_full_auto_decision_only
AssertionError: assert 'full_auto' == 'full_auto_decision_only'
1 failed, 8 passed in 0.30s
```

After the `config.yaml` edit, the same test (and the full new file) is
green:

```
9 passed in 0.19s
```

The `deliverables/measure_config_mode_transitions.py` script (below) was
also validated against real repository history before being trusted for
its post-commit purpose: pointed at the actual commit that flipped
`config.yaml`'s `mode:` from `manual` to `full_auto` in a single commit
(`0fa54ed5652f82b30fd8e361ea81f3576e544606`, brief 006), range
`8be10d8~1..0fa54ed`, it printed:

```
config_mode_single_commit_transition_count = 2
distinct values seen: ['full_auto', 'manual']
```

-- the exact value a genuine single-commit transition should produce,
confirming the script measures the real diff shape rather than a
coincidence (the two brief-008 lessons this session was warned against: a
detector matching by exact string when the real signal differs, and a
counter that happens to be right for the wrong reason).

## `config_mode_single_commit_transition_count` -- NOT YET MEASURABLE, disclosed, not fabricated

This is the one counter this lot's own Required Counters row cannot be
computed by the Générateur role: its definition is `git log -p` restricted
to **this lot's own commit range**, and this session, per this repo's
working rule (restated by the orchestrating instruction for this specific
lot, because of the single-commit constraint SC3 imposes), never runs `git
commit` itself -- only `git add` (staging, so the tracked-files check below
can pass), leaving the ONE commit that must contain both the validation
module and the `mode:` rewrite to the orchestrator. Before that commit
exists there is no "this lot's own commit range" to restrict `git log -p`
to, so the counter genuinely cannot be observed yet -- recording a value
now (0, 2, or any other number) would be exactly the fabricated-measurement
class of fault brief 009's own instructions named as the cause of lot
008a's iteration-1 REJECT. It is therefore deliberately **omitted** from
`manifest.json`'s `counters[]` (a `sample_size: -1` entry would fail
`verdict_audit.py`'s own `no_empty_sample_pass` gate for the whole
submission, which is the correct mechanical behaviour for an unmeasured
required counter, not a bug to work around).

**Exact command to run once the orchestrator's single commit for this lot
exists** (script committed at `deliverables/measure_config_mode_
transitions.py`, already validated above against real history):

```
py harness/queue/briefs/009-full-auto-agent-invocation/deliverables/measure_config_mode_transitions.py <base-sha>..<lot-009a-commit-sha>
```

Expected output shape: `config_mode_single_commit_transition_count = 2` /
`distinct values seen: ['full_auto', 'full_auto_decision_only']` -- the old
value removed once, the new value added once, inside that one commit's own
diff of `harness/pipeline/config.yaml`. A count other than 2 means an
intermediate bare-`full_auto` commit crept into the lot's own range, which
SC3 forbids.

## Files declared, staged (not committed)

`git add` was run against exactly the files this lot's own manifest
declares (listed below) -- staging only, no `git commit`, so
`verdict_audit.py`'s `declared_files_are_tracked` check can run
meaningfully (that check reads git's index via `git ls-files`, which sees
staged-but-uncommitted files) without this session performing the single
commit SC3 requires the orchestrator to make. `git status --short` after
staging showed exactly: `A docs/adr/0007-full-auto-mode-split.md`, `M
docs/adr/README.md`, `M docs/rules/full-auto-pipeline.md`, `M
harness/pipeline/auto_policy.yaml`, `M harness/pipeline/config.yaml`, `A
harness/pipeline/full_auto_mode_guard.py`, `A harness/tests/
test_mode_guard.py`, and the eight new files under this brief's own
`deliverables/` -- nothing else.

## Full suite

`py -m pytest harness/tests/ -q` -- full, untruncated output (also saved at
`deliverables/pytest-full-output.txt`):

```
........................................................................ [ 25%]
........................................................................ [ 51%]
........................................................................ [ 77%]
................................................................         [100%]
280 passed in 25.41s
```

Baseline stated by the orchestrating instruction was 271 passed / 0 failed;
this run is 280 passed / 0 failed -- the 9 new tests in
`harness/tests/test_mode_guard.py`, zero pre-existing test broken.

## Deviations / doubts, stated explicitly

- `config_mode_single_commit_transition_count` not measured this session --
  see the dedicated section above. This is the one deviation from the
  brief's Required Counters table this lot's own architecture makes
  unavoidable (the Générateur role does not commit, SC3's own definition
  needs a commit range to exist), not a silent adjustment.
- The `deliverables/pre-fix/*.orig` snapshots are verified byte-identical
  to this branch's own `HEAD`, not to `origin/master` -- see "First
  action" above for why a direct `origin/master` comparison would give a
  false-positive divergence signal on this specific branch today (15
  commits ahead / 8 behind, both directions). If the Évaluateur's own
  process expects an `origin/master` comparison specifically, this is
  flagged here rather than silently substituted.
- No waiver was needed for this lot (both Acceptable Waivers rows in
  brief.md are scoped to Lot 009c only); `manifest.json`'s `waivers` array
  is empty, not omitted.
