# Eval rubric — Brief 004: bounded visual polish (accents, debug leakage, localization)

**Authored**: 2026-08-01T11:00:01
**Author**: forge-planificateur

This rubric is written before any Générateur work exists for this brief.
The Évaluateur applies it independently; it is not to be revised after
seeing the deliverables. "Mechanical" = checkable by
`harness/verdict_audit.py` or an equivalent scripted check against on-disk
artifacts/logs. "Manual" = requires an Évaluateur reading/looking step.

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| Precondition | Générateur did not touch Unity/`unity/game_unity/` before the orchestrator's signal that brief 003's evaluation released it | Manual | Évaluateur reads `generator-log.md`'s own timestamps against the orchestrator's recorded signal time; any Unity invocation timestamped before that signal is a FAIL of this row regardless of what else passed |
| 1 | Accent transliteration proven present, then proven fixed, in a real reproducible scenario | Mechanical + Manual | `accent_defect_present_before_count` > 0 and `accent_defect_present_after_count` == 0 (Required Counters); mechanical gate: `captures_differ_when_should` on the declared before/after pair; **manual, non-waivable (hard-won rule 11): Évaluateur opens both images and reads the label itself** — confirms the letter is folded (`ILE-DE-FRANCE`), not merely absent, blank, or replaced by a box glyph |
| 1 | No already-correct transliteration touched | Manual | Évaluateur diffs the relevant source file(s) against brief 003's hand-off state; any change outside the proven-broken cases is a FAIL of this row |
| 2 | Debug leakage hidden by default; still reachable via explicit debug mode | Mechanical + Manual | `debug_leak_default_mode_count` == 0 and `debug_leak_explicit_debug_mode_count` >= 1; both declared pairs pass `captures_differ_when_should`; manual: Évaluateur opens all four images (before/after-default, hidden/explicit-debug) and confirms by eye — a gate that silently always reads "hidden" in every capture regardless of the debug flag would still pass the mechanical counters if the Évaluateur only checked one pair, so both pairs must be inspected together |
| 3 | Player-banner decimals in French format, no scientific notation, proven before/after | Mechanical + Manual | `scientific_notation_before_count` > 0 and `_after_count` == 0; declared pair passes `captures_differ_when_should`; manual: Évaluateur reads the after-capture's fiscal panel directly and confirms a French comma-decimal, no exponent |
| 4 | The two `REVUE-v1_054.md` P0s confirmed still closed (not re-fixed, not silently regressed) | Mechanical + Manual | `p0_regression_check_count` == 2; manual: Évaluateur opens the confirmation captures and independently checks for (a) real UI Toolkit chrome, not a synthetic bitmap composite, (b) no overlapping bitmap diagnostic panel |
| 4 | Pause-ambiguity P1 inspected first; fixed only if confirmed open | Mechanical + Manual | `p1_pause_ambiguity_addressed_flag` is 0 or 1, never silently omitted; if 1, its declared before/after pair passes `captures_differ_when_should` and the Évaluateur confirms a distinct "paused" indication separate from the action-button label; if 0, the Évaluateur confirms the cited capture genuinely already shows the two notions separated |
| 4 | No defect fixed outside the four named Success Conditions | Manual | Évaluateur reads the full diff (`git status --porcelain` scoped to `unity/`) against the four conditions' named files/areas; any change not traceable to Success Conditions 1-4 is a FAIL of this row, regardless of how minor or clearly-beneficial it looks |
| 5 | Final visual gallery, fresh, from the ported location, showing all three fixes | Manual (primary), Mechanical (freshness) | mechanical: every gallery file's mtime postdates `brief.md`'s Authored timestamp (`mtime_after_brief`); manual, non-waivable: Évaluateur opens every image in the gallery and confirms the accent fix, the hidden debug tokens, and the French decimals are each visible in at least one frame — this is the artifact the owner will actually look at, so the Évaluateur's own look here is not skippable regardless of how the mechanical counters read |
| 5 | Standalone-player chain used if buildable, waiver used honestly if not | Mechanical + Manual | if Acceptable Waivers row 1 is invoked: `waivers_have_command_and_error` (mechanical) plus a manual check that the build log's failure is real, not a narrated "assumed infeasible"; if not invoked, the gallery must include at least one standalone-chain frame |
| 6 | Reference suite 100% green, fresh, after this brief's changes | Mechanical | `reference_suite_passed_count` == `reference_suite_total_count`, both > 0, read from a fresh XML whose mtime postdates this brief's code changes; Évaluateur independently re-runs the same reconstructed reference suite (cited by pointer to brief 003, not re-derived) itself rather than trusting the reported counts alone |
| 6 | The 7 legacy-attributed test files from brief 003 are untouched | Mechanical | `legacy_attributed_test_files_unchanged_count` == 7; Évaluateur independently re-hashes all 7 named files against their brief-003 hand-off SHA256, not trusting the Générateur's own claim alone |
| 7 | Artistic verdict is literally `A_REVOIR_HUMAINEMENT`, never `ADOPTÉ` | Mechanical | grep `generator-log.md` and `manifest.json` for the literal string `A_REVOIR_HUMAINEMENT` (must be present) and for `ADOPTÉ` as a self-declared status (must be absent as a claim of acceptance — the word may appear only when quoting `task_v1_056.json`'s own constraint text, not as this brief's own verdict) |
| Non-Goal | No new screen/panel/view; no external asset/package added | Manual | Évaluateur reviews the full file diff for any new `Resources/UI/` asset or new top-level UXML screen |
| Non-Goal | No test weakened/deleted/skipped | Mechanical + Manual | `final_test_total_count` (Success Condition 6's fresh run) has not decreased versus brief 003's own hand-off total, minus only whatever legitimate scope difference exists (none expected — same test tree); manual diff review of any `Assets/Tests/**` file confirms no assertion was loosened |
| Non-Goal | Zero simulation-logic lines changed; zero new sim<->presentation coupling | Mechanical + Manual | `git status --porcelain` outside `Assets/Scripts/Presentation/**`, `Assets/UI/**` (or wherever this brief's fixes actually land), `Assets/Tests/**` (captures/logs only), and `deliverables/**` must be empty; manual: Évaluateur reads every changed `.cs` file's diff and confirms no read/write crosses into simulation (`Assets/Scripts/Core`, `Assets/Scripts/World`, etc.) beyond the existing read-only path |
| Non-Goal | No parity/determinism anchor rebased or cited by value | Mechanical | grep for hex-literal patterns (`0x[0-9A-Fa-f]{8,}`) in `generator-log.md`/`manifest.json` — any match is a FAIL (hard-won rule 12), same check as brief 003 |
| Non-Goal | No `.meta` file hand-written | Manual | Évaluateur checks any new file's `.meta` was Unity-generated (import timestamp/GUID pattern consistent with editor-generated metas), not authored by hand |
| Non-Goal | No git commit by the Générateur | Mechanical | same method as brief 003's `generator_git_commits_count` — a commit-count query over this brief's own work window must equal 0 |
| — | Every counter in `manifest.json` carries a real, nonzero `sample_size` | Mechanical gate | `no_empty_sample_pass` |
| — | Every declared deliverable postdates `brief.md`'s Authored timestamp (2026-08-01T11:00:00) | Mechanical gate | `mtime_after_brief` |
| — | Any waiver claim carries the exact command + error required by `brief.md`'s Acceptable Waivers table | Mechanical gate | `waivers_have_command_and_error` |
| — | No bare `python` invocation anywhere in deliverables/logs | Mechanical gate | `no_bare_python_alias` |
| — | Any number cited in `verdict.md` traces back to `manifest.json` | Mechanical gate | `verdict_numbers_traceable` |
| — | `verdict.md`'s Author differs from `generator-log.md`'s Author | Mechanical gate | `verdict_is_not_self_authored` |
| — | This rubric's Authored timestamp (2026-08-01T11:00:01) predates every deliverable's mtime | Mechanical gate | `rubric_predates_deliverables` |
| — | Every declared `must_differ_from` pair actually differs | Mechanical gate | `captures_differ_when_should` |

## Session-Cost Calibration

The Évaluateur does not re-derive brief 003's reference-suite *definition*
from scratch (that work is already done and cited by pointer) — it re-runs
that already-defined suite once, independently, and cross-checks the
result against the Générateur's own fresh run. The real cost in this brief
is visual, not computational: every declared capture pair must be opened
and looked at (hard-won rule 11 is non-waivable here in particular, since
this brief's entire subject is what the interface shows a human) — that is
a bounded, small number of images (at most ~10-12 across Success Conditions
1-5), not a re-run of the whole 274-case suite a second time.

## Plateau / Waiver Notes

- A Required Counter whose denominator evaluates to 0 (e.g. zero accented
  occurrences found in the reproduction scenario, meaning the "before"
  claim itself has no real defect to point at) is a FAIL on that line, not
  a pass — an empty sample proves nothing (`no_empty_sample_pass`).
- If Acceptable Waivers row 1 (standalone chain unbuildable) is invoked,
  Success Condition 5 may still be `accepted` on the `MapSnapshotExporter`
  path alone, with the standalone-chain gap recorded as a carried-forward
  finding for a future brief — not silently marked fully satisfied and not
  grounds for rejecting the whole brief by itself.
- If Acceptable Waivers row 2 (a fix would require crossing into
  simulation code) is invoked for any of Success Conditions 1-4, that
  specific item is reported `blocked-by-scope`, not silently dropped and
  not forced through — the rest of the brief is judged on its own merits.
- Two iterations without improvement on the same blocking row -> STOP,
  escalate to the owner rather than replaying the same prompt
  (`docs/rules/harness-roles.md`).

## Overall Verdict Rule

ACCEPT only if every numbered row passes AND
`py harness/verdict_audit.py harness/queue/briefs/004-polish-visuel` exits
0. A FAIL on Success Condition 5's "Évaluateur opens every gallery image"
row, or on Success Condition 7's artistic-verdict-string row, is
disqualifying regardless of how green the mechanical counters otherwise
read — this brief's entire purpose is what a human sees, and a mechanically
green but unlooked-at gallery, or a self-declared "beautiful," defeats that
purpose exactly as VictoriaProject's own history (`docs/rules/hard-won-
rules.md` rule 11: four major defects found only by eye) warns against.
