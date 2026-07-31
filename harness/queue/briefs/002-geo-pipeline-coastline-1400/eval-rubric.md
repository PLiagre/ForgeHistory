# Eval rubric — Brief 002: geo pipeline port, installment 1 (shared infra + G2 coastline)

**Authored**: 2026-07-29T14:00:00
**Author**: forge-planificateur
**Amended**: 2026-07-29T19:30:00 (iteration 2 — the row for Success Condition
3's `game_unity` check is rewritten to match the amended, narrow-exception
counter in `brief.md`; a new Non-Goal row is added for the `constants.py`
revert. No other row changed.)

One line per Success Condition (brief.md). "Mechanical" = checkable by
`harness/verdict_audit.py` or an equivalent scripted check against on-disk
artifacts/logs, no judgment call. "Manual" = requires an Évaluateur reading
step, cannot be reduced to a single boolean.

| # | Success Condition (brief.md) | Check type | How it is checked |
|---|---|---|---|
| 1 | Exact 20-file copy list present at declared destinations | Mechanical | `Test-Path` on all 20 destination paths in brief.md's table; fails if any is missing |
| 1 | 18 non-adjusted files are byte-identical to their VictoriaProject originals | Mechanical | `byte_identical_ported_files_count` == 18 (Required Counters) |
| 1 | No extra file created under `pipeline/geo/` beyond the 20 listed (plus `.gitignore`, `deliverables/*`, README edit) | Mechanical | diff of `git status --porcelain pipeline/geo/` against the declared file list; any unlisted new file fails this line |
| 2 | `sources.lock` byte-identical, attributions intact | Mechanical | subsumed by `byte_identical_ported_files_count`; additionally confirm the file's `dem`/`geonames_cities500`/Natural Earth licence blocks are present verbatim (substring match against the three attribution texts quoted in `FORGE-HISTORY-BRIEF.md` §3) |
| 3 | Exactly one path adjustment in `constants.py`, exactly one in `steps/02_coastline.py`, both marked, nothing else changed | Mechanical | `path_adjustment_marker_count` >= 2 AND `path_adjustment_unmarked_diff_line_count` == 0 (Required Counters) |
| 3 (AMENDED iteration 2) | No remaining reference to the nonexistent `game_unity` path anywhere under `pipeline/geo/`, except byte-identical pre-existing prose in `data/divergences_1400.json` and its verbatim copy/quotations | Mechanical | Évaluateur greps `pipeline/geo/` for the literal substrings `game_unity` and `StreamingAssets`. For each hit: if the hit's file is NOT one of `data/divergences_1400.json`, `artifacts/divergences_1400.json`, or a `logs/*` file quoting that JSON's content, the hit counts as a violation immediately. If it IS one of those three, compare the hit's exact line byte-for-byte against the corresponding line of `C:\Users\liagr\VictoriaProject\sandbox\geo\data\divergences_1400.json`; a match excludes the hit, a mismatch counts it. `game_unity_reference_remaining_count` (post-exclusion) must equal 0. A hit inside any `.py` file (in particular `constants.py`'s `FORBIDDEN_GAME_PATH_MARKERS`) is never covered by the exception and always counts |
| 4 | `pipeline/geo/.gitignore` excludes `.venv/` | Mechanical | file exists, contains the substring `.venv/` |
| 5 | Proof commands run and both proof scripts exit 0 | Mechanical | `proof_script_exit_code_zero_count` == 2 (Required Counters); the harness (not the Générateur) re-runs the four commands from Success Condition 5 and captures exit codes independently — a Générateur-reported "0" is not itself sufficient, per hard-won rule "celui qui produit ne prononce pas la recevabilité" |
| 5 | Two-pass SHA256 determinism: every compared artifact matches, none empty | Mechanical | `determinism_sha_pairs_matched_count` == total key count in both dicts, AND total key count > 0 (no_empty_sample_pass) |
| 6 | Every named check passes green | Mechanical | `qa_checks_passed_count` == total entries across both `checks` arrays |
| 6 | Every named check has a red-case proof on record | Mechanical | `qa_checks_red_proof_count` == total entries across both `checks` arrays |
| 6 | Verdict reports per-check `passed`/`red_proof`, not just an aggregate claim | Manual | Évaluateur opens `logs/v1_046_qa.json` and `logs/v1_047_qa.json` directly and spot-checks at least 3 individual check entries against what the verdict prose claims about them |
| 7 | `pipeline/geo/README.md` updated, states installment scope truthfully (what landed, what didn't) | Manual | Évaluateur reads the post-edit README; fails if it overclaims (e.g. implies `03_cells.py` or later has landed) or underclaims (fails to mention the G2 cluster now exists) |
| 8 | `deliverables/manifest.json` declares the two pre-port `.orig` snapshots and the pre-edit README snapshot with `must_differ_from` | Mechanical | `manifest.json` parses; contains entries for `deliverables/pre-port/constants.py.orig`, `deliverables/pre-port/02_coastline.py.orig`, `deliverables/pre-edit/pipeline-geo-README.md.orig`; the README entry's `must_differ_from` target's SHA256 differs from the `.orig`'s SHA256 |
| 9 | All five named evidence files exist on disk after the commands run | Mechanical | `Test-Path` on `logs/v1_046_qa.json`, `logs/v1_046_coastline.log`, `logs/v1_047_qa.json`, `logs/v1_047_corrections.log`, `capture/v1_046_coastline_compare.png` |
| 9 | Verdict's cited numbers trace to these files | Manual | Évaluateur cross-checks at least the determinism-match count and the qa-checks-passed count quoted in the verdict against the actual JSON contents — any number in the verdict without a traceable source file/field fails this line |
| Non-Goal | No `steps/03_cells.py`-onward file created; no `sim/`, ADR-0003, C# systems, or `ARCHITECTURE.md` touched | Mechanical | `git status --porcelain` outside `pipeline/geo/{constants.py,steps/02_coastline.py,steps/02b_corrections_1400.py,io_util.py,projection.py,requirements.txt,sources.lock,qa/**,tests/**,data/**,sources/**,legacy_game_data/**,.gitignore,README.md}` and `deliverables/**` must be empty |
| Non-Goal | No unmarked diff line in the two adjusted files | Mechanical | same as Success Condition 3's `path_adjustment_unmarked_diff_line_count` check |
| Non-Goal (NEW, iteration 2) | `constants.py`'s `FORBIDDEN_GAME_PATH_MARKERS` string literals are unsplit and otherwise unmodified, exactly as in `deliverables/pre-port/constants.py.orig` | Manual | Évaluateur reads `pipeline/geo/constants.py`'s diff against `deliverables/pre-port/constants.py.orig` line by line (not just the two Required Counters above, which cannot by themselves distinguish "marked but unauthorized" from "marked and authorized"); the diff must consist of exactly the authorized path-adjustment lines in the `_PROJECT_ROOT` / `_PROVINCE_COORDS_JSON` expression, both ending in `# FORGEHISTORY-PATH-ADJUSTMENT`, and nothing else — any split, re-encoding, reordering, or other alteration of `FORBIDDEN_GAME_PATH_MARKERS` (or of any other line) fails this row regardless of what the two mechanical Required Counters read |
| Non-Goal | No claim of "runs" without an actual logged execution | Manual | Évaluateur checks that Success Condition 9's evidence files' internal timestamps/content correspond to a run performed during this brief's session, not stale/copied-in files |
| Non-Goal | Pip/proof-script waiver, if invoked, does not excuse Success Conditions 1-4 | Mechanical + Manual | if the Acceptable Waivers table's third row is invoked, mechanical check confirms Success Conditions 1-4's file-presence and byte-identity counters are still satisfied regardless; manual check confirms the waiver's required command output is pasted verbatim, not summarized |

## Plateau / waiver notes

- If the pip-install waiver (`brief.md`'s Acceptable Waivers table, row 3) is
  legitimately invoked with its required command output, this brief's
  verdict may still be `accepted` for Success Conditions 1-4 and 7-8 while
  recording Success Conditions 5, 6, and 9 as `blocked-by-environment` —
  the Évaluateur must not silently mark those `passed`, and must not silently
  mark the whole brief `rejected` either; the correct verdict state is
  explicit partial acceptance with the blocking claim carried forward per
  `docs/rules/harness-roles.md`'s plateau/carry-forward convention.
- Any Required Counter above whose denominator evaluates to 0 (e.g. zero
  keys in a determinism dict, zero entries in a `checks` array) is a FAIL on
  that line, never a pass — an empty sample is not a clean result
  (`no_empty_sample_pass`).
- (NEW, iteration 2) The amended `game_unity_reference_remaining_count` row
  is a traceability rule, not a hardcoded tolerance. Its correct value is
  always 0 after exclusions are applied — never "3" or "5" or any other
  fixed number. If a future re-run of the port changes how many hits appear
  in `data/divergences_1400.json`-derived locations, the exclusion logic
  above still applies per-hit; do not shortcut it by hardcoding an expected
  count.
