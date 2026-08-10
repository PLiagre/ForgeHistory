# Eval Rubric — Brief 009 (wire claude-challenger, split full_auto, CI budget ceiling — converts the owner's 2026-08-09 decision)

**Authored**: 2026-08-10T09:00:00Z
**Author**: forge-planificateur

Rubric written **before** any Générateur work exists for this brief, per
`docs/rules/harness-roles.md`. The Évaluateur applies it as written, never
revised after seeing deliverables. This brief is split into three lots —
**009a**, **009b**, **009c** — each gated and evaluated separately; 009c
depends on the other two and must never be evaluated as complete on its
own if either predecessor's own gate did not ACCEPT.

"Mechanical" = checkable by `harness/verdict_audit.py`, a re-run test
command, or an equivalent scripted check against on-disk artifacts — no
judgment call. "Manual" = requires an Évaluateur reading/reasoning step.
Row numbers below point at brief.md's own numbered items by number; this
document does not restate their text.

## Lot 009a — mode split, fail-closed, ADR-0007

| # | brief.md item | Check type | How it is checked |
|---|---|---|---|
| 1 | bare `full_auto` refused while forge-run unwired | Mechanical | counter `mode_full_auto_bare_rejected_test_count` ≥ 1; harness re-runs the test independently, does not trust a Générateur-reported PASS alone |
| 2 | `full_auto` accepted once the fixture shows forge-run "wired" | Mechanical | counter `mode_full_auto_accepted_when_forgerun_wired_test_count` ≥ 1; confirms both branches of the same guard are exercised, not only the refusal |
| 3 | single-commit transition of `mode:` | Mechanical | counter `config_mode_single_commit_transition_count` == 2; manual spot-check that `git log -p` shows no intermediate bare `full_auto` commit |
| 4 | `auto_policy.yaml`'s documentation scalar updated | Manual | Évaluateur reads the file, confirms line matches `config.yaml`'s new value |
| 5 | ADR-0007 written; ADR-0006 not rewritten | Mechanical + Manual | mechanical: `adr_0007_status_field_present` == 1; manual: Évaluateur diffs `docs/adr/0006-full-auto-agent-pipeline.md` against its pre-brief state (git history) and confirms zero change |
| 5b | `docs/adr/README.md` rows | Mechanical | `adr_readme_rows_added_count` == 2 |
| 6 | activation doc corrected | Mechanical | `must_differ_from`: `deliverables/pre-fix/full-auto-pipeline.md.orig` (009a) vs. `docs/rules/full-auto-pipeline.md`; manual spot-check step 3's literal text names `full_auto_decision_only` |
| 7 | full suite green | Mechanical | `py -m pytest harness/tests/ -q` — full output present in `deliverables/generator-log.md`, zero failures |
| — | `must_differ_from` pairs (config.yaml, auto_policy.yaml, full-auto-pipeline.md) | Mechanical | SHA256 of each pre/post pair differ |

## Lot 009b — CI budget guard module

| # | brief.md item | Check type | How it is checked |
|---|---|---|---|
| 8 | module exposes precheck + record, reuses the price table | Manual + Mechanical | manual: Évaluateur confirms the module imports `harness/backends/ledger.py`'s price table rather than re-typing it; mechanical: `grep` for a duplicated price literal (e.g. `5.0` mapped to a model name) outside `harness/backends/ledger.py` fails this row if found |
| 9 | monthly precheck fails closed ≥ $200, proceeds < $200 | Mechanical | counters `monthly_precheck_refuses_test_count` ≥ 1 and `monthly_precheck_proceeds_test_count` ≥ 1; harness re-runs both independently |
| 10 | refusal flips `mode:` to `manual`, byte-scoped | Mechanical | counter `mode_flip_byte_scoped_test_count` ≥ 1; the test itself asserts a byte diff touching only the `mode:` line — Évaluateur re-runs it, does not trust the Générateur's own diff summary |
| 11 | over-cap anomaly flag, not a silent accept, reusable across cap values | Mechanical | counter `over_cap_anomaly_flag_test_count` ≥ 2 (one per distinct cap value, e.g. $5 and $50) |
| 12 | prior-month entries excluded from cumulative | Mechanical | counter `monthly_boundary_reset_test_count` ≥ 1 |
| 13 | ledger not gitignored | Mechanical | counter `ci_budget_ledger_not_gitignored_check_count` == 1, real command output present in `deliverables/generator-log.md` |
| — | file-set boundary (no touch to `config.yaml`'s value, any ADR, any workflow file) | Manual | Évaluateur checks the commit's own file list against brief.md's Non-Goals for this lot |

## Lot 009c — real invocation, mode-gated, budget-gated

| # | brief.md item | Check type | How it is checked |
|---|---|---|---|
| 14 | `TODO(operator` stub removed, real call constructed | Mechanical | counter `challenge_todo_stub_remaining_count` == 0; manual spot-check the replacement step actually shells out to `claude`, not a second narration |
| 15 | mode-gate: `manual` blocks, `full_auto_decision_only` proceeds | Mechanical | counters `mode_gate_manual_blocks_test_count` ≥ 1 and `mode_gate_full_auto_decision_only_proceeds_test_count` ≥ 1; both re-run independently |
| 16 | budget-precheck called before invocation, documented skip on refusal | Manual + Mechanical | manual: Évaluateur reads the step order in `pipeline-challenge.yml`, confirms the 009b precheck call precedes the real invocation call; mechanical: a refusal path produces a `::warning::`-shaped log line, not a job failure |
| 17 | budget-record called after invocation, real USD | Manual | Évaluateur confirms the post-invocation step calls 009b's record function with a USD value derived from `harness/backends/ledger.py`'s own scan logic, not a hardcoded number |
| 18 | stubbed-CLI end-to-end proof reaches `AUDIT_CHALLENGED` | Mechanical | counter `stubbed_cli_end_to_end_test_count` ≥ 1; harness re-runs it and independently confirms the ledger read inside the test shows `AUDIT_CHALLENGED` for the fixture `audit_id` — a Générateur-reported pass alone is insufficient |
| 19 | `mechanical-scaffold-smoke` unweakened | Mechanical | counter `mechanical_scaffold_smoke_unchanged_check` shows zero removed/weakened steps or triggers |
| 20 | activation doc's `[claude-challenger]` text updated, no overclaim on the other two maillons | Manual | Évaluateur reads the diagram text, confirms `cursor-auditor`/`forge-run` are still described as stubs |
| 21 | full suite green | Mechanical | `py -m pytest harness/tests/ -q` — full output present, zero failures |
| — | `must_differ_from` pairs (pipeline-challenge.yml, full-auto-pipeline.md re-diffed post-009a) | Mechanical | SHA256 of each pre/post pair differ; the pre-fix snapshot for `full-auto-pipeline.md` in this lot must itself already reflect 009a's edit (Évaluateur confirms the snapshot is NOT byte-identical to the pristine pre-brief file) |

## Disqualifying failures — brief.md Non-Goal violations (any lot)

Any one of these is a FAIL regardless of how complete the rest looks:

- 009a touches `.github/workflows/pipeline-challenge.yml`, or 009b touches
  `harness/pipeline/config.yaml`'s value, any ADR, or any workflow file,
  or 009c re-implements mode-validation/budget-ledger logic instead of
  calling 009a's/009b's own modules.
- Any deliverable edits `pipeline-audit.yml`'s or `pipeline-forge-run.yml`'s
  own agent-invocation step bodies.
- A claim that a real `gh issue create` was implemented anywhere in this
  brief.
- `docs/adr/0006-full-auto-agent-pipeline.md`'s `Status` or body text
  edited by any lot.
- PyYAML imported by any file under `harness/pipeline/*.py` or
  `harness/*.py` outside a test/verification-only file.
- A claim that a single in-flight invocation's dollar cost was
  pre-emptively capped mid-execution (only the monthly cumulative is a
  true pre-invocation block — brief.md "points jugés sous-spécifiés" (a)).
- A claim that the $50/forge-run cap was exercised end-to-end against a
  real invocation (brief.md "points jugés sous-spécifiés" (b) — recorded
  only, not wired, in this brief).
- Any counter in a lot's `deliverables/manifest.json` with a zero/empty
  sample (gate: `no_empty_sample_pass`).
- A waiver claim (either row in brief.md's Acceptable Waivers table)
  present without both the required command output and the required
  error/evidence attached (hard-won rule 9).
- 009c evaluated or merged before both 009a's and 009b's own gates
  ACCEPTed.

## Mechanical gate rows (all three lots, via `harness/verdict_audit.py`)

| — | All counters in manifest.json have nonzero sample_size | gate: `no_empty_sample_pass` |
| — | Deliverable mtimes after brief `Authored` 2026-08-10T09:00:00Z | gate: `mtime_after_brief` |
| — | Waivers have command + error | gate: `waivers_have_command_and_error` |
| — | No bare `python` in deliverables (must be `py`) | gate: `no_bare_python_alias` |
| — | Verdict numbers traceable to counters | gate: `verdict_numbers_traceable` |
| — | Verdict Author ≠ Générateur Author | gate: `verdict_is_not_self_authored` |
| — | Rubric predates deliverables | gate: `rubric_predates_deliverables` |

## Overall Verdict Rule

**PASS (Lot 009a)** only if rows 1-7 (+ its unnumbered mechanical rows)
pass **and** `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`
exits 0 for the 009a submission.

**PASS (Lot 009b)** only if rows 8-13 (+ its unnumbered row) pass **and**
the same gate command exits 0 for the 009b submission. Independent of
009a's status.

**PASS (Lot 009c)** only if rows 14-21 (+ its unnumbered rows) pass, **and**
the same gate command exits 0 for the 009c submission, **and** both 009a
and 009b are already ACCEPTed (dependency, not independence, unlike
008a/008b).

## Évaluateur — minimal replay scenario, per lot

Lot 009a:

```
py -m pytest harness/tests/ -k "mode_guard or mode_split or full_auto" -q
```

Expected: the SC1 (bare-`full_auto`-refused) test and the SC2
(accepted-once-wired) test both present and passing, exercised
independently of the Générateur's own report.

Lot 009b:

```
py -m pytest harness/tests/ -k "ci_budget or budget_guard" -q
```

Expected: precheck refuse/proceed pair, the byte-scoped flip test, the
≥2-cap-value over-cap test, and the monthly-boundary test all present and
passing.

Lot 009c:

```
py -m pytest harness/tests/ -k "challenge or invoke" -q
```

Expected: the mode-gate refuse/proceed pair and the stubbed-CLI
end-to-end test all present and passing; independently re-run, the
stubbed-CLI test's own ledger read must show `AUDIT_CHALLENGED` for its
fixture `audit_id`.
