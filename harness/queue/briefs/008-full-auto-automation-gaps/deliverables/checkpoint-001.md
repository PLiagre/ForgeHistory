# Checkpoint -- 008-full-auto-automation-gaps

**Author**: forge-generateur
**Written**: 2026-08-09T21:17:13
**Reason**: UNMEASURABLE at -1 tool calls (warn 100 / checkpoint 130 / stop 160)

This is a handoff, not a verdict. `UNMEASURABLE` means the run hit its execution
budget, NOT that the work is wrong -- the deliverables below may be entirely
correct. The Évaluateur judges the work; this file only says where it stopped.

The next session must be able to resume from **this file plus the files in
the repository**, without reading the previous transcript.

## 1. Objectif du lot
Close the BLOCKER-1/BLOCKER-2 defects `feedback/feedback-008a.md` raised
against Lot 008a iteration 1: guard **all three** branches of
`trigger_resolve.resolve()` against a terminal `audit_id` (not only the
push-diff path), fix the counter instrument that had hidden the gap, and
correct the ISSUE-3 overclaim in `generator-log.md`.

## 2. Travail terminé
This iteration's own work is **complete**, not partial — every item in the
brief's "Iteration 2" task list is done and independently re-verified in
this session (see section 4). This checkpoint is being written because
`py harness/budget.py status` reports `NO_PROGRESS_STOP`, which this
session believes to be a **false positive from a budget.py bug, not a real
signal** — documented honestly rather than silently worked around:

- `py harness/budget.py status --brief <dir> --agent ad9ff9711f62bd9d7`
  (the transcript whose mtime tracks this session, confirmed by directly
  inspecting `subagents/*.jsonl` mtimes under this session's own project
  directory) reports **48 real tool calls**, well under the 100-call warn
  threshold — nowhere near budget exhaustion.
- The `NO_PROGRESS_STOP` verdict comes from `since = tool_calls -
  last_progress_at` where `last_progress_at` is the LAST progress event's
  `tool_calls_at` field (`harness/budget.py` line ~281:
  `last_progress_at = events[-1]["tool_calls_at"] if events else 0`). This
  session's own three `progress` calls all recorded `tool_calls_at: -1`
  (the sentinel for "transcript not found") because the plain `py
  harness/budget.py progress` subcommand — unlike `status` — cannot resolve
  this subagent's transcript path even with the same substring iteration 1
  already reported hitting the identical limitation for). `since = 48 - (-1)
  = 49 >= 35` (`NO_PROGRESS_CALLS`) triggers the stop purely from that
  arithmetic, not from 49 calls without measurable progress actually
  elapsing — 7 real progress events are on record in `progress.jsonl` for
  this brief across both iterations.

This is a `harness/budget.py` defect (the `-1` sentinel should be excluded
from the subtraction, not used as a literal number), not a Lot 008a file —
touching `harness/budget.py` would itself violate this lot's own file-set
Non-Goals, so it is reported here, not fixed here.

## 3. Fichiers modifiés
- `harness/pipeline/trigger_resolve.py` — `resolve()`'s `in_payload` and
  `in_audit_id` branches now each consult `audit_ledger.current_state_for`
  for the `audit_id` they are about to act on and skip (empty `event=` +
  `::notice::`) when that state is terminal, via a new shared
  `_terminal_notice()` helper. Module docstring rewritten (SC2 wording no
  longer scopes to `resolve_push()` alone).
- `harness/tests/test_trigger_resolve.py` — 5 new tests (17 total, all
  green): 2 prove the new terminal-skip guard on both workflow_dispatch
  branches, 2 prove the SC4/SC5 non-blanket-skip property (transition still
  reached via `orchestrator.run_event` for a genuinely non-terminal
  `audit_id`) on both branches, 1 documents the no-`audit_id`-in-payload
  exemption explicitly.
- `harness/queue/briefs/008-full-auto-automation-gaps/deliverables/measure_ledger_consult_paths.py`
  (new) — replaces the iteration-1 inline `ast.Constant`-only, `resolve_push()`-only
  counter command. Analyses the whole entry point (`resolve()`'s two
  workflow_dispatch branches + `resolve_push()`), treats any non-`Constant`
  `event=` value as capable of non-empty output, and prints the documented
  no-`audit_id` exemption in its own stdout, not only in a docstring.
- `deliverables/manifest.json` — all four counters re-measured in place;
  `ledger_consult_before_transition_paths_count` now `3`/`3` (was `1`/`1`,
  wrongly scoped).
- `deliverables/generator-log.md` — new `## Iteration 2` section: the
  BLOCKER-1/BLOCKER-2 fix narrative, the red-first proof against the
  iteration-1 HEAD commit, the ISSUE-3 overclaim correction (left the
  original wrong claim visible above, corrected below it rather than
  edited in place), and the re-measured counter table.
- `.github/workflows/pipeline-orchestrate.yml` — **NOT modified this
  iteration** (sha256 confirmed identical to iteration 1's post-fix hash);
  the defect was entirely inside `resolve()`'s Python body.

## 4. Tests exécutés et résultats
```
py -m pytest harness/tests/test_trigger_resolve.py -v
17 passed in 0.41s
```
```
py -m pytest harness/tests/ -q
1 failed, 266 passed in 26.37s
```
(`test_no_brief_prescribes_polling` — pre-existing, brief 007's own
`deliverables/checkpoint-002.md`, unrelated to this lot, independently
confirmed red on `origin/master` by the Évaluateur.)
```
py -m pytest harness/tests/test_single_source_of_instruction.py -q
1 passed in 0.20s
```
(confirms ISSUE-4's regression stayed fixed; not this lot's file, checked
for honesty only.)
```
py deliverables/measure_ledger_consult_paths.py   (pointed at the fixed tree)
TOTAL: gated=3 capable=3
```
```
py deliverables/measure_ledger_consult_paths.py   (pointed at a scratch copy of `git show HEAD:harness/pipeline/trigger_resolve.py`, i.e. iteration 1's own committed, unmodified code)
TOTAL: gated=1 capable=3
```
Live-ledger probe (not a fixture) against the real
`architecture/audit-ledger.jsonl`, `CURSOR-FIXTURE-full-auto-demo` (still
`AUDIT_ARCHIVED`), through all three `resolve()` branches: all three now
return `event=''` (full transcript in `generator-log.md` § BLOCKER-1 fix).

## 5. Décisions prises
- The `in_payload` branch's one capable return is reached by two runtime
  sub-paths (audit_id present-and-checked vs. audit_id absent-and-exempt);
  a single AST `Return` node cannot carry two verdicts, so the counter
  counts it once, as gated, with the exemption named in the script's own
  docstring AND printed at runtime — per the Évaluateur's own instruction
  in BLOCKER-1 ("say so in the counter's own command string, not only in a
  module docstring").
- Did not touch `harness/budget.py` to fix the `-1`-sentinel arithmetic bug
  found while writing this checkpoint — out of Lot 008a's declared file
  set; reported here instead (see section 2 and 6).
- Kept `.github/workflows/pipeline-orchestrate.yml` untouched this
  iteration — verified by sha256 that no edit was needed; the fix was
  entirely inside `trigger_resolve.py`.

## 6. Problèmes ouverts
- `harness/budget.py`'s `progress` subcommand cannot resolve this session's
  own subagent transcript (`agent-ad9ff9711f62bd9d7.jsonl`, found only via
  `status --agent <substring>`, never via plain `progress`), so every
  `progress` event this session recorded carries `tool_calls_at: -1`. Its
  `status` command's `since = tool_calls - last_progress_at` then treats
  that sentinel as a literal `-1` instead of excluding it, producing a
  false `NO_PROGRESS_STOP`. Worth a future brief against `harness/budget.py`
  itself — out of scope here.
- `deliverables/verdict.md` and `deliverables/feedback/feedback-008a.md`
  are iteration 1's Évaluateur artifacts, untouched and unmodified by this
  Générateur, still present on disk (expected — this role never writes or
  deletes `verdict.md`).

## 7. Prochaine action exacte
None for the Générateur role — Lot 008a iteration 2's work, as scoped by
this session's task, is complete. The next action belongs to the
Évaluateur: re-run the gate and judge this iteration against
`feedback/feedback-008a.md`'s BLOCKER-1, BLOCKER-2, and ISSUE-3.

## 8. Commande de reprise
```bash
py -m pytest harness/tests/test_trigger_resolve.py -v
py harness/queue/briefs/008-full-auto-automation-gaps/deliverables/measure_ledger_consult_paths.py
py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps
```

## 9. Contexte minimal nécessaire
- `harness/queue/briefs/008-full-auto-automation-gaps/feedback/feedback-008a.md`
  — the rejection this iteration answers.
- `harness/pipeline/trigger_resolve.py` — the fix itself.
- `harness/queue/briefs/008-full-auto-automation-gaps/deliverables/generator-log.md`
  § "## Iteration 2" — the full narrative and re-measured numbers.
- `harness/queue/briefs/008-full-auto-automation-gaps/deliverables/manifest.json`
  — the four Lot 008a counters, re-measured in place.

## Measured state at checkpoint time
| metric | value |
|---|---|
| tool calls | -1 |
| API requests | -1 |
| progress events | 7 |
| tool calls since last progress | -1 |

### Progress ledger
| # | kind | tool calls | evidence |
|---|---|---|---|
| 1 | red_to_green | 36 | harness/tests/test_trigger_resolve.py::test_terminal_audit_excluded_from_candidate_set and test_terminal_audit_regressio |
| 2 | deliverable_created | 64 | harness/pipeline/trigger_resolve.py, harness/tests/test_trigger_resolve.py (12 tests, all passing), deliverables/manifes |
| 3 | deliverable_created | 91 | generator-log.md self-check section added, manifest.json finalized with generator-log.md entry, py harness/verdict_audit |
| 4 | red_to_green | -1 | measure_ledger_consult_paths.py: 1 gated/3 capable against iteration-1 HEAD (git show HEAD:harness/pipeline/trigger_reso |
| 5 | gate_check_gained | -1 | py -m pytest harness/tests/test_trigger_resolve.py -v: 17 passed (12 original unchanged + 5 new iteration-2 tests); live |
| 6 | deliverable_created | -1 | deliverables/manifest.json + deliverables/generator-log.md '## Iteration 2' section updated with re-measured counters; d |
| 7 | plan_step_done | -1 | Iteration 2 complete: BLOCKER-1/BLOCKER-2 fixed in trigger_resolve.py + measure_ledger_consult_paths.py, 5 new regressio |
