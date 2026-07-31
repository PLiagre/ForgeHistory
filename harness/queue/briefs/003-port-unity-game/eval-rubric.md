# Eval rubric — Brief 003: port VictoriaProject's working Unity game

**Authored**: 2026-07-31T15:00:01
**Author**: forge-planificateur

This rubric is written before any Générateur work exists for this brief. The
Évaluateur applies it independently; it is not to be revised after seeing
the deliverables. "Mechanical" = checkable by `harness/verdict_audit.py` or
an equivalent scripted check against on-disk artifacts/logs, no judgment
call. "Manual" = requires an Évaluateur reading/looking step.

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| 1 | `unity/game_unity/` exists, copied from VictoriaProject, excluding exactly the eight named directories, `Captures/` retained | Mechanical | `robocopy_files_pending_copy_count` == 0 (list-only verify pass); `Test-Path unity/game_unity/Captures` is true; `Test-Path` false for `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, `PresentationCache/`, `UserSettings/`, `.vs/` under the new tree (until regenerated locally by compile — those regenerated copies are expected and not a violation, only their *ported-from-source* presence would be) |
| 1 | `Captures/` was retained because it is actually referenced by the test suite, not by assumption | Mechanical | `captures_dir_test_reference_count` >= 1 (grep evidence in manifest.json) |
| 1 | `C:\Users\liagr\VictoriaProject\` left untouched | Mechanical | `victoriaproject_source_unmodified_count` == denominator (all >= 3 sampled sentinel file hashes unchanged); Évaluateur independently re-hashes the same 3 named files itself rather than trusting the Générateur's before/after pair alone |
| 2 | `unity/game_unity/.gitignore` covers the eight excluded directories | Mechanical | file exists; contains all eight directory names as patterns |
| 3 | Compilation proof: exit 0, zero `error CS####` | Mechanical + Manual | `compile_error_cs_count` == 0 AND log line count > 0; **the Évaluateur independently re-runs the exact Success Condition 3 invocation itself** (cheap, single bounded launch — see brief's Session-Cost Note) rather than trusting the Générateur's reported exit code alone (hard-won rule: "celui qui produit ne prononce pas la recevabilité") |
| 3 | First-import background launch, no premature timeout-based abandonment | Manual | Évaluateur reads `generator-log.md`'s description of the polling method used; a claim of "timed out, did not finish" without the compile log showing an actual Unity-side error is a FAIL of this row regardless of what else passed |
| 4 | EditMode suite green from the new location, counts derived from NUnit XML | Mechanical | `test_total_count` > 0, `test_passed_count` == `test_total_count`, `test_failed_count` == 0, all three read directly from `unity/game_unity/Logs/v003_test-results.xml`'s `<test-run>` attributes — not from prose |
| 4 | Independent corroboration without a full second run | Mechanical + Manual | Évaluateur runs `-batchmode -runTests -testPlatform EditMode -testFilter <FullyQualifiedName>` (or this Unity version's equivalent working syntax, as documented by the Générateur in `generator-log.md`) against exactly the three named fixtures (`V1094PilotLiveOwnershipTests`, `V1070PoliticalMapTests`, `V1095GpuMapTests`), producing its own fresh NUnit XML; cross-checks each fixture's pass/fail counts against that same fixture's entries in the Générateur's full-suite XML — any mismatch is a FAIL of this row even if both suites report "green" in aggregate |
| 4 | No stale/borrowed numbers (e.g. VictoriaProject's own 217/217) substituted for this port's own measurement | Manual | Évaluateur scans `generator-log.md` and `verdict.md` for any bare count not traceable to this session's own XML/log files |
| 5 | Visual proof — real before/after capture pair from unmodified existing code | Mechanical + Manual | Mechanical: gate's `captures_differ_when_should` (SHA256 of `Captures/v1_094/01_avant_conquete.png` vs `03_apres_conquete_VERT_ecs.png` must differ) plus `capture_pair_sha256_distinct_count` == 2. **Manual, non-waivable (hard-won rule 11): the Évaluateur opens both PNGs and looks** — confirms the "before" shows the pre-conquest owner and the "after" (VERT/ecs variant, not the ROUGE/disk variant) shows a genuinely different owner over the same window, not merely a different SHA256 for an unrelated reason (e.g. a timestamp watermark) |
| 5 | No new capture/export code was written to manufacture this pair | Manual | Évaluateur diffs `Assets/Scripts/Presentation/MapSnapshotExporter.cs` and `Assets/Tests/V1094PilotLiveOwnershipTests.cs` against `C:\Users\liagr\VictoriaProject\game_unity\...` byte-for-byte (`Get-FileHash`) — any difference is a FAIL of this row and of the Non-Goals "no simulation/gameplay logic changes" clause unless it is the one authorized, marked path adjustment |
| 6 | `docs/adr/0004-<slug>.md` exists, matches template structure, `Status: accepted` | Mechanical | heading-list diff against `docs/adr/template.md`, same method as brief 001's rubric row 2 |
| 6 | >= 2 `### Alternative` entries, one of them the §3/§9 "reread, don't copy" deviation, each with real (non-filler) Pros/Cons/Why-not | Mechanical + Manual | `adr_alternatives_considered_count` >= 2 (mechanical); content specificity is a manual judgment call, same standard as brief 001's rubric row 3 |
| 6 | Failure mode #1 named by number; imported debt explained causally; `PilotMapProvider.SimulationProvinceIdOfView` cited by NAME; ADR-0003 named as the still-unmet F1 target | Manual | Évaluateur reads `## Context`/`## Decision`; reject if the explanation is code-quality-only rather than causal (X still desyncs unless Y -> Z), or if it re-derives/re-states the translation logic instead of citing the name |
| 6 | No inline parity/determinism hex value quoted anywhere in the ADR or `generator-log.md` | Mechanical | grep for hex-literal patterns (`0x[0-9A-Fa-f]{8,}`) in both files — any match is a FAIL of this row (hard-won rule 12) |
| 6 | `docs/adr/README.md` gains a row for ADR-0004 | Mechanical | `adr_index_rows_count` == 4 |
| 7 | `unity/README.md` updated; documents exactly the `-projectPath` / `-openfile` invocation; no `automation/`-style queue/lock machinery ported | Manual | Évaluateur reads the post-edit README; FAILS if it overclaims (implies `run_queue.py`/`demo.py` equivalents exist) or is missing the concrete launch command |
| 8 | `deliverables/manifest.json` + `deliverables/generator-log.md` present, `**Author**: forge-generateur`, no "recevable"/"acceptable" language | Mechanical + Manual | mechanical: `verdict_is_not_self_authored`; manual: grep `generator-log.md` for "recevable"/"acceptable"/"accepted" self-claims — any hit is a FAIL of this row |
| 9 | Lockfile+process check documented before each of the 3 Unity invocations | Mechanical | `unity_lockfile_checked_before_invocation_count` == total Unity invocations actually run (must be equal, not merely present) |
| Non-Goal | No file under `automation/`-equivalent, `sim/`, or `pipeline/geo/` created/modified; no `.cs` diff beyond the one authorized path-adjustment exception (if invoked) | Mechanical | `git status --porcelain` outside `unity/**`, `docs/adr/0004-*.md`, `docs/adr/README.md`, and `deliverables/**` must be empty |
| Non-Goal | `C:\Users\liagr\VictoriaProject\` unmodified | Mechanical | same as Success Condition 1's `victoriaproject_source_unmodified_count` row |
| Non-Goal | PlayMode HUD capture not attempted/claimed | Manual | Évaluateur confirms no deliverable claims a PlayMode `InGameHud` framebuffer capture; if the Générateur attempted one anyway and it silently failed, that is itself worth noting in `verdict.md` as scope creep |
| Non-Goal | No git commit created by the Générateur | Mechanical | `generator_git_commits_count` == 0 |
| Non-Goal | Failure mode #1 not silently "fixed" (e.g. by quietly rewriting ECS to use `cell_id`) | Manual | Évaluateur confirms the ported `.cs` tree's ownership/translation logic is byte-identical to VictoriaProject's (same check as Success Condition 5's second row, broadened to the whole `Assets/Scripts/` tree via a directory-level `robocopy /L` diff) |
| — | Every counter in `manifest.json` carries a real, nonzero `sample_size` | Mechanical gate | `no_empty_sample_pass` |
| — | Every declared deliverable postdates `brief.md`'s Authored timestamp (2026-07-31T15:00:00) | Mechanical gate | `mtime_after_brief` |
| — | Any waiver claim carries the exact command + error required by `brief.md`'s Acceptable Waivers table | Mechanical gate | `waivers_have_command_and_error` |
| — | No bare `python` invocation anywhere in deliverables/logs | Mechanical gate | `no_bare_python_alias` |
| — | Any number cited in `verdict.md` traces back to `manifest.json` | Mechanical gate | `verdict_numbers_traceable` |
| — | `verdict.md`'s Author differs from `generator-log.md`'s Author | Mechanical gate | `verdict_is_not_self_authored` |
| — | This rubric's Authored timestamp (2026-07-31T15:00:01) predates every deliverable's mtime | Mechanical gate | `rubric_predates_deliverables` |
| — | Declared `must_differ_from` pair (Success Condition 5) actually differs | Mechanical gate | `captures_differ_when_should` |

## Session-Cost Calibration (why the grid is shaped this way)

Success Conditions 3 and 5 are each a single, bounded Unity launch once
`Library/` is already imported (which it will be, from the Générateur's own
run) — cheap enough that the Évaluateur re-runs them in full rather than
trusting a reported exit code, per "celui qui produit ne prononce pas la
recevabilité." Success Condition 4 (the full EditMode suite) is not cheap
to duplicate in full: per-case costs measured in VictoriaProject were in the
low seconds across 200+ cases, plus Unity's own batchmode startup overhead
on each invocation. Re-running it twice in full would roughly double the
most expensive single step in this brief for a corroboration check, not a
first-discovery one. The rubric therefore requires a **named, fixed subset**
(the same three fixtures named in `brief.md`'s Session-Cost Note) run
independently via `-testFilter` and cross-checked case-by-case against the
Générateur's full-suite XML — this is deliberately a narrower check than
"re-run everything," and that narrowing is declared here, in advance, rather
than being an ad hoc shortcut discovered mid-verdict.

## Plateau / Waiver Notes

- If the Acceptable Waivers table's row 2 (marked path-adjustment) is
  legitimately invoked, Success Condition 3 is not thereby satisfied by the
  waiver itself — the compile must still be re-run and pass green
  afterward, with the adjustment's diff lines reviewed the same way brief
  002's `FORGEHISTORY-PATH-ADJUSTMENT` lines were: exactly the marked lines
  differ from the VictoriaProject original, nothing else.
- If row 4 (a pre-existing-red test) is invoked without a `HANDOFF.md`
  citation backing the "pre-existing" claim, treat it as an unsupported
  claim, not a valid waiver — Success Condition 4 fails outright.
- A Required Counter whose denominator evaluates to 0 (e.g. zero test cases
  in the XML, zero sentinel files hashed) is a FAIL on that line, never a
  pass — an empty sample is not a clean result (`no_empty_sample_pass`).
- Two iterations without improvement on the same blocking row -> STOP,
  escalate to the owner rather than replaying the same prompt a third time
  (`docs/rules/harness-roles.md`).

## Overall Verdict Rule

ACCEPT only if every numbered row passes AND
`py harness/verdict_audit.py harness/queue/briefs/003-port-unity-game` exits
0. A FAIL on the Success Condition 5 "look at the captures yourself" row, or
on the Success Condition 6 "failure mode #1 named causally" row, is
disqualifying regardless of how green the mechanical counters otherwise
read — a SHA256 difference is necessary but was never sufficient in this
codebase's own history (v1_095's tête-bêche capture passed every automated
pixel-diff check it had).
