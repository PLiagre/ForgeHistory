# Eval Rubric — Brief 008 (full-auto pipeline reliability gaps, from audit CURSOR-5633ee7-automation-completeness)

**Authored**: 2026-08-08T21:15:00Z
**Author**: forge-planificateur

Rubric written **before** any Générateur work exists for this brief, per
`docs/rules/harness-roles.md`. The Évaluateur applies it as written, never
revised after seeing deliverables. This brief is split into two ready,
independent lots — **008a** and **008b** — each gated and evaluated
separately; a third, **008c**, is out of scope for evaluation because
`brief.md` deliberately carries no Success Conditions for it (see
`brief.md`'s "Lot 008c — deferred" section). An Évaluateur who receives a
008c submission against this brief must reject it outright: there is no
rubric row for it to be judged against, by design.

"Mechanical" = checkable by `harness/verdict_audit.py`, a re-run test
command, or an equivalent scripted check against on-disk artifacts — no
judgment call. "Manual" = requires an Évaluateur reading/reasoning step.

## Lot 008a — orchestrator ledger guard

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| 1 | Resolution logic lives in a unit-testable Python entry point, invoked by `pipeline-orchestrate.yml`'s resolve step | Mechanical + Manual | mechanical: `py -m pytest` can import and call the entry point directly, no GitHub Actions context required (test file exists and passes standalone); manual: Évaluateur opens `pipeline-orchestrate.yml` and confirms the resolve step actually calls this entry point rather than duplicating its logic inline |
| 2 | Ledger consulted for every candidate `audit_id`, terminal ones excluded, before any payload is built | Mechanical | counter `ledger_consult_before_transition_paths_count` == the total count of code paths capable of producing a non-empty `event=`/`payload=` (Required Counters table) |
| 3 | Exact incident shape (1 changed review file, already `AUDIT_ARCHIVED`) produces zero transition attempts, proven by a new regression test | Mechanical | `terminal_audit_regression_test_count` >= 1; harness re-runs that test independently and confirms it asserts zero calls into `audit_decision.decide_auto` / `audit_ledger.append_event` for the fixture `audit_id` — a Générateur-reported "passes" alone is not sufficient, the Évaluateur or gate re-runs it |
| 4 | Fix is not a blanket skip — non-terminal fixture still dispatches | Mechanical | `non_terminal_dispatch_still_works_test_count` >= 1; harness re-runs it and confirms it asserts the transition **is** attempted |
| 5 | Pre-existing ambiguous-diff fallback (0 or >1 non-terminal candidates) unchanged | Manual | Évaluateur diffs `pipeline-orchestrate.yml`'s fallback branch against its pre-fix snapshot (`deliverables/pre-fix/pipeline-orchestrate.yml.orig`) and confirms the `::notice::`-and-skip behavior for that branch is present and unchanged in substance |
| 6 | `audit_decision.py`'s FSM guard itself unchanged | Mechanical | `git diff` (or hash comparison) on `harness/audit_decision.py` shows no change introduced by this lot's commits |
| — | `must_differ_from`: `deliverables/pre-fix/pipeline-orchestrate.yml.orig` vs. `.github/workflows/pipeline-orchestrate.yml` | Mechanical | SHA256 of the two differ; manifest declares the pair explicitly |
| — | `workflow_inline_bash_decision_logic_remaining_count` == 0 | Mechanical | grep of the resolve step for any bash-only branch bypassing the SC1 entry point |

## Lot 008b — pipeline job-failure escalation

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| 7 | New `pipeline_job_failed` rule in `auto_policy.yaml`, action mirrors `three_consecutive_mechanical_rejects` | Mechanical | `pipeline_job_failed_policy_rule_count` == 1; manual spot-check that its `action` value names the same escalation semantics (not a different, weaker action) |
| 8 | `orchestrator.py --event pipeline_job_failed` returns `action == "escalate_pipeline_stuck"` | Mechanical | `pipeline_job_failed_handler_test_count` >= 1; harness re-runs `py -m pytest harness/tests/test_orchestrator.py -q` independently |
| 9 | New/extended `workflow_run` trigger covers all four `pipeline-*.yml` files | Mechanical | `pipeline_workflow_run_trigger_coverage_count` == 4 |
| 10 | Incident-shaped failure (mirroring run 31085883052) resolves to the same escalation action as the 3-REJECT path | Mechanical | `run_31085883052_style_escalation_regression_count` >= 1; harness re-runs the fixture test and confirms the asserted action string matches the existing 3-REJECT fixture's asserted action string exactly (parity, not merely "an" escalation) |
| 11 | `actionlint` passes, or the named substitute is used and documented | Mechanical + Manual | mechanical: `actionlint` exit 0 on the new/edited workflow file, OR (per the Acceptable Waivers row) the substitute check passes; manual: if the substitute path was used, Évaluateur confirms `deliverables/generator-log.md` documents exactly what could not be mechanically checked — a silent skip fails this row |
| — | `must_differ_from`: `deliverables/pre-fix/auto_policy.yaml.orig` vs. `harness/pipeline/auto_policy.yaml` | Mechanical | SHA256 of the two differ |
| — | `must_differ_from`: `deliverables/pre-fix/orchestrator.py.orig` vs. `harness/pipeline/orchestrator.py` | Mechanical | SHA256 of the two differ |

## Non-Goals — disqualifying failures (either lot)

Any one of these is a FAIL regardless of how complete the rest looks:

- Lot 008a's deliverables touch any file in Lot 008b's file set, or vice
  versa (brief.md's Non-Goals, "Independence check").
- Any deliverable edits `pipeline-audit.yml`, `pipeline-challenge.yml`, or
  `pipeline-forge-run.yml`'s agent-invocation step bodies, or
  `docs/rules/full-auto-pipeline.md`'s `<<TODO>>` marker, or
  `harness/audit_convert.py`'s seed text (Lot 008c scope, out of bounds for
  this brief).
- A claim that a real `gh issue create` was implemented for either
  escalation path (brief.md's Non-Goals explicitly forbids overclaiming
  this — parity with the existing log-only `handle_gate_reject` depth is
  the bar, not more).
- `mode: full_auto` value changed in `harness/pipeline/config.yaml`, or
  `docs/adr/0006-full-auto-agent-pipeline.md`'s `Status`/text edited.
- Any counter in `deliverables/manifest.json` with a zero/empty sample
  (gate: `no_empty_sample_pass`).
- A waiver claim (either row in brief.md's Acceptable Waivers table) present
  without both the required command output and the required error/evidence
  attached — a bare prose claim of infeasibility fails this row outright
  (hard-won rule 9).

## Mechanical gate rows (both lots, via `harness/verdict_audit.py`)

| — | All counters in manifest.json have nonzero sample_size | gate: `no_empty_sample_pass` |
| — | Deliverable mtimes after brief `Authored` 2026-08-08T21:15:00Z | gate: `mtime_after_brief` |
| — | Waivers have command + error | gate: `waivers_have_command_and_error` |
| — | No bare `python` in deliverables (must be `py`) | gate: `no_bare_python_alias` |
| — | Verdict numbers traceable to counters | gate: `verdict_numbers_traceable` |
| — | Verdict Author ≠ Générateur Author | gate: `verdict_is_not_self_authored` |
| — | Rubric predates deliverables | gate: `rubric_predates_deliverables` |

## Overall Verdict Rule

**PASS (Lot 008a)** only if rows 1–6 (+ the two unnumbered mechanical rows
in that section) pass **and**
`py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`
exits 0 for the 008a submission.

**PASS (Lot 008b)** only if rows 7–11 (+ its two unnumbered mechanical
rows) pass **and** the same gate command exits 0 for the 008b submission.

Lots 008a and 008b are evaluated independently — neither's PASS/FAIL
depends on the other's status, per their declared independence.

## Évaluateur — minimal replay scenario, per lot

Lot 008a:

```
py -m pytest harness/tests/ -k "trigger or terminal or orchestrat" -q
```

Expected: the SC3 (terminal-audit) test and the SC4 (non-terminal-still-
dispatches) test both present and passing; re-running them independently
of the Générateur's own report is mandatory, not optional.

Lot 008b:

```
py -m pytest harness/tests/test_orchestrator.py -q
```

Expected: a test asserting `--event pipeline_job_failed` yields
`action == "escalate_pipeline_stuck"`, and a second test reproducing the
run-31085883052-shaped failure with the same asserted action string.
