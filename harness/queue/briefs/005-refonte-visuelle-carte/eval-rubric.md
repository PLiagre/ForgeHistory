# Eval rubric — Brief 005: refonte visuelle carte

**Authored**: 2026-08-01T15:10:01
**Author**: forge-planificateur

This rubric is written before any Générateur work exists for this brief.
The Évaluateur applies it independently; it is not to be revised after
seeing the deliverables. "Mechanical" = checkable by
`harness/verdict_audit.py` or an equivalent scripted check against on-disk
artifacts/logs. "Manual" = requires an Évaluateur reading/looking step.
Every Outcome-A/Outcome-B split below follows exactly the shape
`brief.md`'s own Success Conditions section states — this rubric does not
introduce new work, it only states how each stated outcome is checked
(Single Source of Instruction: `CLAUDE.md`).

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| Precondition | Générateur did not launch Unity while a live process held the lockfile | Manual | Évaluateur reads `generator-log.md`'s own lockfile-check timestamps; any invocation timestamped while a held lockfile is logged is a FAIL of this row regardless of what else passed |
| 1 (CPU) | Map orientation, `PresentFrame` path, proven identical to the export path | Mechanical + Manual | `map_orientation_reference_checks_matched_count` == `_total_count`, `_total_count` >= 3 (Required Counters, `no_empty_sample_pass`); declared pair `v005_orientation_cpu` passes `captures_differ_when_should`; manual, non-waivable (hard-won rule 11): Évaluateur opens the after-capture and a fresh export of the same window side by side and independently re-checks every named reference point itself (does not trust the Générateur's own match claim) |
| 1 (GPU) | Map orientation, `PresentRenderTexture` path, proven identical or honestly waived | Mechanical + Manual | if not waived: same method and floor as the CPU row, over pair `v005_orientation_gpu`; if Acceptable Waivers row 1 is invoked: `waivers_have_command_and_error` (mechanical) plus manual confirmation the pasted failure output is a real invocation failure, not a narrated assumption |
| 2 | Initial camera framing on the playable Europe extent | Mechanical + Manual | **Outcome A**: `playable_provinces_outside_initial_window_before_count` > 0 and `after_count` == 0, denominator (`no_empty_sample_pass`) > 0, declared pair `v005_initial_framing` passes `captures_differ_when_should`; manual: Évaluateur opens the after-capture and confirms every named playable province's approximate position is inside the frame, with a visible margin, and that the extent was derived from loaded data (Évaluateur reads the diff to confirm no hard-coded bounding constant was introduced). **Outcome B**: `before_count` == 0 is PASS if and only if (a) the denominator (total playable provinces checked) is real and non-empty and named, (b) `generator-log.md` documents what was run, on what data, with what result, (c) no fictitious defect was manufactured; manual: Évaluateur independently re-derives the playable-province set from the same game data and re-checks the initial window itself |
| 3 | Zoom fluidity, measured in ms | Mechanical + Manual | **Outcome A**: at least one `zoom_transition_ms_measured` before-value exceeds the stated frame budget, every after-value at that same transition does not, over >= 5 transitions (`no_empty_sample_pass`); manual: Évaluateur re-derives the cited `LastWindowRebuildMilliseconds`/`LastGpuBackgroundMilliseconds` values from the same logs the Générateur cites and confirms the stated responsible cost was the one actually fixed (not a different, untested change that happens to also touch zoom). **Outcome B**: every before-value already within the stated budget across all >= 5 transitions, documented with the real numbers (not a favorable subset — Évaluateur spot-checks by re-running the same zoom sequence once itself, or by independently reading the cited log's every line, not only the ones the Générateur highlighted) |
| 4 | Border stroke finesse | Mechanical + Manual | **Outcome A**: `border_stroke_width_px_measured` before-values fail the stated criterion at >= 1 of the 3 zoom levels, all 3 after-values satisfy it, pairs `v005_border_zoom_{min,mid,max}` pass `captures_differ_when_should`; manual: Évaluateur opens all 3 after-captures and independently judges the stroke crisp and non-stair-stepped at each level, against the stated criterion, not merely trusting the reported pixel measurement. **Outcome B**: all 3 before-values already satisfy the stated criterion, documented with real measured widths, unchanged; manual: Évaluateur opens all 3 captures and independently confirms |
| 5 | War-front red — legible and discreet | Mechanical + Manual | `front_rim_legend_reachable_flag` == 1 (or == -1 only with Acceptable Waivers row 2 invoked and `waivers_have_command_and_error` passing); `front_rim_color_change_proof_count` > 0 (`no_empty_sample_pass`); declared pair `v005_front_rim` (or the waived reference-capture equivalent) passes `captures_differ_when_should`/is inspected; manual, non-waivable: Évaluateur (a) reaches the legend/tooltip itself in a fresh capture and confirms it actually explains the marking in terms a player would understand, (b) opens the before/after front-rim captures and independently judges the after version reads as visually secondary/discreet relative to country borders while remaining distinguishable from them — a purely numeric color-distance claim without this visual judgement is not sufficient |
| 6a | `Lois`/`Impôt` panel overlap eliminated | Mechanical + Manual | `panel_overlap_pairs_before_count` > 0 and `after_count` == 0 across the reused fixed gallery scenarios (`no_empty_sample_pass`); pair `v005_panel_overlap` passes `captures_differ_when_should`; manual, non-waivable: Évaluateur opens every after-scenario capture and independently checks for any visible panel-boundary intersection or clipped glyph, not only the specific `Lois`/`Impôt` pair named in the origin grievance |
| 6b | `Investir` block raw dump gated behind debug mode | Mechanical + Manual | `investir_raw_token_default_mode_before_count` > 0, `after_count` == 0, `investir_raw_token_explicit_debug_mode_count` >= 1, over >= 2 distinct provinces/scenarios (`no_empty_sample_pass`); pair `v005_investir_dump` passes `captures_differ_when_should`; manual: Évaluateur opens the default-mode after-capture and the debug-mode capture together and confirms the same behaviour brief 004's `LAWMOD`/`EFF` gate proved — a real gate (French labels visible by default, raw tokens visible in debug), never a plain deletion, never a raw token in default mode |
| 7 | Tick pacing measured, then defensibly set, parity proven unaffected | Mechanical + Manual | `ms_per_tick_measured` sample >= 20 (`no_empty_sample_pass`); `harness_tick_advance_unchanged_flag` == 1 (a 0 is disqualifying for this row regardless of any other value); the Success-Condition-7-isolation reference-suite counters (if separable) or the combined Success Condition 9 run, both 100% green; manual: Évaluateur reads `generator-log.md`'s stated justification for whichever value was chosen (including "unchanged, defensible as-is") against the cited ms measurements, and independently re-reads the harness/capture tick-advance source to confirm it was not touched |
| 8 | Final gallery shows every confirmed Outcome-A fix | Manual (primary), Mechanical (freshness) | mechanical: every gallery file's mtime postdates `brief.md`'s Authored timestamp (`mtime_after_brief`); manual, non-waivable: Évaluateur opens every gallery image and confirms each Success Condition that was Outcome A in this brief is visible in at least one frame; Outcome-B items are checked against the log, not required to be pictured |
| 8 | Standalone-player chain used if buildable, waiver honest if not | Mechanical + Manual | if Acceptable Waivers row 5 is invoked: `waivers_have_command_and_error` plus manual confirmation the pasted failure is real; if not invoked, the gallery includes at least one standalone-chain frame |
| 9 | Reference suite 100% green, fresh, after all changes | Mechanical | `reference_suite_passed_count` == `reference_suite_total_count`, both > 0, fresh XML postdating this brief's last code change; Évaluateur independently re-runs the same reconstructed suite itself rather than trusting the reported counts alone |
| 9 | 7 legacy-attributed test files untouched | Mechanical | `legacy_attributed_test_files_unchanged_count` == 7; Évaluateur independently re-hashes all 7 named files against their brief-003 hand-off SHA256 |
| 10 | Artistic verdict is literally `A_REVOIR_HUMAINEMENT`, never `ADOPTÉ` | Mechanical | grep `generator-log.md` and `manifest.json` for the literal string `A_REVOIR_HUMAINEMENT` (must be present) and for `ADOPTÉ` as a self-declared status (must be absent as a claim of acceptance — the word may appear only when quoting another document's constraint text or negating self-adoption, never as this brief's own verdict) |
| Non-Goal | Zero simulation-logic lines changed, except Success Condition 7's two named pacing constants | Mechanical + Manual | `git status --porcelain` outside `Assets/Scripts/Presentation/**`, `Assets/UI/**`, the two named pacing fields in `Assets/Scripts/Core/Components/TickControl.cs`, `Assets/Tests/**` (captures/logs only), and `deliverables/**` must be empty; manual: Évaluateur reads every changed `.cs` file's diff line by line and confirms every change outside the two named pacing constants stays in the presentation layer, reading, not deciding, simulation state |
| Non-Goal | Harness/capture tick-advance mechanism unchanged | Mechanical + Manual | `harness_tick_advance_unchanged_flag` == 1 (see Success Condition 7 row); manual: Évaluateur diffs the capture/harness entry points that advance ticks against their brief-004 hand-off state |
| Non-Goal | No new screen/panel/view beyond Success Conditions 5/6's explicit scope | Manual | Évaluateur reviews the full file diff for any new top-level UXML screen or `Resources/UI/` asset beyond a legend/tooltip element (Success Condition 5) and the existing debug-mode gate reused (Success Condition 6) |
| Non-Goal | No external asset/package, no new dependency | Manual | Évaluateur reviews the diff and any manifest/package file for additions |
| Non-Goal | No test weakened/deleted/skipped | Mechanical + Manual | Success Condition 9's fresh total has not decreased versus brief 004's hand-off total, minus only whatever legitimate scope difference is explicitly stated (none expected); manual diff review of any `Assets/Tests/**` file confirms no assertion was loosened |
| Non-Goal | 7 legacy-attributed test files untouched | Mechanical | same as Success Condition 9's row (single check, not duplicated in the gate) |
| Non-Goal | No parity/determinism anchor rebased or cited by value | Mechanical | grep for hex-literal patterns (`0x[0-9A-Fa-f]{8,}`) in `generator-log.md`/`manifest.json` — any match is a FAIL (hard-won rule 12) |
| Non-Goal | Nothing fixed beyond Success Conditions 1-7's named list | Manual | Évaluateur reads the full diff against the seven named conditions; any change not traceable to one of them (including the explicitly-named-out-of-scope items: `Promulguer land_tax`, `Sat 0,798`, the `ATK vs BUR` war row) is a FAIL of this row, however minor or clearly-beneficial it looks |
| Non-Goal | No `.meta` file hand-written | Manual | Évaluateur checks any new file's `.meta` was Unity-generated, not authored by hand |
| Non-Goal | No git commit by the Générateur | Mechanical | commit-count query over this brief's own work window must equal 0 |
| — | Every counter in `manifest.json` carries a real, nonzero `sample_size` | Mechanical gate | `no_empty_sample_pass` |
| — | Every declared deliverable postdates `brief.md`'s Authored timestamp (2026-08-01T15:10:00) | Mechanical gate | `mtime_after_brief` |
| — | Any waiver claim carries the exact command + error `brief.md`'s Acceptable Waivers table requires | Mechanical gate | `waivers_have_command_and_error` |
| — | No bare `python` invocation anywhere in deliverables/logs | Mechanical gate | `no_bare_python_alias` |
| — | Any number cited in `verdict.md` traces back to `manifest.json` | Mechanical gate | `verdict_numbers_traceable` |
| — | `verdict.md`'s Author differs from `generator-log.md`'s Author | Mechanical gate | `verdict_is_not_self_authored` |
| — | This rubric's Authored timestamp (2026-08-01T15:10:01) predates every deliverable's mtime | Mechanical gate | `rubric_predates_deliverables` |
| — | Every declared `must_differ_from` pair actually differs | Mechanical gate | `captures_differ_when_should` |

## Session-Cost Calibration

The Évaluateur does not re-derive brief 003's reference-suite *definition*
from scratch (cited by pointer, already done); it re-runs that suite once,
independently, and cross-checks the result. The real cost here is visual
and measurement work, not computation: every declared capture pair (at
most ~8 pairs across Success Conditions 1-6, fewer if some resolve
Outcome B) must be opened and looked at (hard-won rule 11, non-waivable —
this brief's entire subject is what a human sees), and the timing counters
(`zoom_transition_ms_measured`, `border_stroke_width_px_measured`,
`ms_per_tick_measured`) must each be independently re-derivable from a
cited log the Évaluateur actually opens, not merely a number the
Générateur reports.

## Plateau / Waiver Notes

- A Required Counter whose stated denominator evaluates to 0 (no reference
  points checked, no zoom transitions measured, no ticks measured, etc.)
  is a FAIL on that line under either outcome — an empty sample proves
  nothing (`no_empty_sample_pass`).
- Outcome B (defect investigated and proven absent) is a genuine PASS for
  Success Conditions 2, 3, 4, and Success Condition 7's "unchanged,
  defensible as-is" branch, if and only if the sample is real, non-empty,
  and the investigation is documented honestly. It is **not** available for
  Success Condition 1 (both grievances are already independently
  corroborated present — an Outcome-B claim there would contradict the
  Évaluateur's own prior finding and must be treated as a red flag, not
  taken at face value) or for Success Condition 6 (both items are already
  independently corroborated present the same way). It does not apply to
  Success Condition 5, 8, 9, or 10, which are not present/absent-shaped.
- If Acceptable Waivers row 1 (GPU live path unreachable) is invoked,
  Success Condition 1's GPU-path row is satisfied on the CPU path alone,
  with the gap recorded as a carried-forward finding — not grounds for
  rejecting the whole brief by itself.
- If Acceptable Waivers row 2 (no war front reachable) is invoked, Success
  Condition 5 is judged on the existing reference captures instead of a
  fresh live reproduction, with the gap recorded honestly.
- If Acceptable Waivers row 3 (blocked by the presentation/simulation
  boundary) is invoked for any Success Condition 1-7 item, that specific
  item is reported `blocked-by-scope`, not silently dropped and not forced
  through — the rest of the brief is judged on its own merits.
- If Acceptable Waivers row 5 (standalone chain unbuildable) is invoked,
  Success Condition 8 may still be accepted on the `MapSnapshotExporter`
  path alone, with the gap recorded as a carried-forward finding.
- Two iterations without improvement on the same blocking row -> STOP,
  escalate to the owner rather than replaying the same prompt
  (`docs/rules/harness-roles.md`).

## Overall Verdict Rule

ACCEPT only if every numbered row passes AND
`py harness/verdict_audit.py harness/queue/briefs/005-refonte-visuelle-carte`
exits 0. A FAIL on Success Condition 1 (either path, unwaived), Success
Condition 8's "Évaluateur opens every gallery image" row, or Success
Condition 10's artistic-verdict-string row is disqualifying regardless of
how green the mechanical counters otherwise read — this brief exists
because a mechanically-green brief 004 was still rejected by the owner on
sight, so a mechanically green but unlooked-at result here would repeat
exactly the failure this brief was written to correct. **Even a full
ACCEPT on every row above records a rubric-level PASS only — the owner's
own artistic judgement, separately recorded, is the only thing that can
mark this work "beau."** No verdict.md for this brief may declare or imply
`ADOPTÉ`.
