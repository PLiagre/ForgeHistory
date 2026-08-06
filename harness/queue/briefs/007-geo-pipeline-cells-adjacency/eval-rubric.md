# Eval rubric — Brief 007: geo pipeline port, installment 2 (G3 cells + G4 adjacency)

**Authored**: 2026-08-06T09:14:00
**Author**: forge-planificateur

One line per Success Condition (`brief.md`). "Mechanical" = checkable by
`harness/verdict_audit.py` or an equivalent scripted check against on-disk
artifacts/logs, no judgment call. "Manual" = requires an Évaluateur reading
step. This brief is split into Lots 007a (G3) and 007b (G4) — each lot gets
its own gate/verdict, evaluated independently and in order (007b cannot be
evaluated ACCEPT before 007a is ACCEPT, since it depends on 007a's artifacts).

| # | Success Condition (brief.md) | Lot | Check type | How it is checked |
|---|---|---|---|---|
| 1 | `pipeline.py` ported byte-identical | 007a | Mechanical | `Get-FileHash` on `pipeline/geo/pipeline.py` vs VictoriaProject original; equal |
| 1 | `pipeline.py` actually required (not a spurious copy) | 007a | Manual | Évaluateur confirms `steps/03_cells.py`'s `_load_pipeline_module` still references it and that `run_proof_g3.py`'s green run exercises `derive_adjacency` (i.e. `artifacts/adjacency_g3.json` exists and is non-trivial) |
| 2 | Three new legacy files copied, SHA256 equal to the cited VictoriaProject-recorded target hashes | 007a (cities/city_coordinates), 007b (sea_zones) | Mechanical | `legacy_data_sha_target_match_count` == 3 (Required Counters) |
| 2 | Two existing legacy files (`province_adjacency.json`, `province_coordinates.json`) re-verified unchanged, not re-copied | 007b | Mechanical | `existing_legacy_data_unchanged_count` == 2; `git log`/diff shows no modification to these two files in this brief's commits |
| 2 | Every one of the five legacy files has an explicit, recorded copy/reuse decision (none silently skipped) | 007a, 007b | Manual | Évaluateur reads `pipeline/geo/README.md`'s legacy-data section and confirms all five files from brief.md's decision table are named with their decision, matching brief.md exactly |
| 3 | Exact file copy list present at declared destinations, nothing extra | 007a, 007b | Mechanical | `Test-Path` on all declared destination paths; `git status --porcelain pipeline/geo/` diffed against the declared file list per lot — any unlisted new file fails |
| 3 | 8 byte-identical files match their VictoriaProject originals | 007a, 007b | Mechanical | `byte_identical_new_files_count` == 8 |
| 4 | `steps/03_cells.py`: exactly one path adjustment (`CITIES_JSON`/`CITY_COORDS_JSON`), marked, nothing else changed | 007a | Mechanical | `path_adjustment_marker_count` >= 1 for this file AND `path_adjustment_unmarked_diff_line_count` == 0 for this file's diff vs `deliverables/pre-port/03_cells.py.orig` |
| 5 | `steps/04_adjacency.py`: exactly one path adjustment (`GAME_DATA`), marked, nothing else changed | 007b | Mechanical | same pattern, vs `deliverables/pre-port/04_adjacency.py.orig` |
| 6 | No remaining `game_unity`/`StreamingAssets` reference outside the brief-002-established exception | 007a, 007b | Mechanical | `game_unity_reference_remaining_count` == 0 after exclusions (same per-hit traceability rule as brief 002's amended Success Condition 3 — a hit in `.py` code, including the two new step files, always counts) |
| 7 | `run_proof_g3.py` runs in this repository, this session, and exits 0 | 007a | Mechanical | `proof_script_exit_code_zero_count` includes this script's exit code == 0; harness re-runs independently, a Générateur-reported "0" alone is not sufficient (hard-won rule: celui qui produit ne prononce pas la recevabilité) |
| 7 | G3 two-pass SHA256 determinism, all matched, none empty | 007a | Mechanical | `g3_determinism_sha_pairs_matched_count` == total key count in `logs/v1_049_qa.json`'s `determinism.sha256`, AND total > 0 (`no_empty_sample_pass`) |
| 7 | Every G3 named check green + red-proven | 007a | Mechanical | `g3_qa_checks_passed_count` == total entries; `g3_qa_checks_red_proof_count` == total entries in `logs/v1_049_qa.json`'s `checks` |
| 7 | G3 cell count within declared bounds, every land mass covered | 007a | Mechanical | `g3_cell_count_in_range` true against `[150, 400]`; script's own uncovered-mass `RuntimeError` did not fire (run completed, exit 0) |
| 8 | `run_proof_g4.py` runs, exits 0 | 007b | Mechanical | `proof_script_exit_code_zero_count` includes this script's exit code == 0; harness re-runs independently |
| 8 | G4 two-pass SHA256 determinism, all matched, none empty | 007b | Mechanical | `g4_determinism_sha_pairs_matched_count` == total key count, total > 0 |
| 8 | Every G4 named check green + red-proven, including `G4-B` with the natural (links-off) red-case | 007b | Mechanical | `g4_qa_checks_passed_count` / `g4_qa_checks_red_proof_count` == total entries; `g4_open_sea_reachability_without_links_fails` non-empty specifically for the `G4-B` entry |
| 8 | G4 sea zone count within declared bounds; all four adjacency kinds present | 007b | Mechanical | `g4_sea_zone_count_in_range` true against `[20, 40]`; `g4_adjacency_by_kind_nonzero_count` == 4 |
| 8 | Topology link (Zuiderzee/Afsluitdijk) proven load-bearing, not decorative | 007b | Mechanical + Manual | mechanical: `g4_open_sea_reachability_with_links` true AND `g4_open_sea_reachability_without_links_fails` non-empty (both conditions required together — links-on reachable, links-off not); manual: Évaluateur opens `artifacts/topology_links_g4.json`'s `links_applied` and confirms at least one entry with `applied: true` referencing a Zuiderzee/Lauwerszee-class basin |
| 9 | `artifacts/adjacency_g4.json` carries zero `province` references | 007b | Mechanical | `adjacency_g4_province_field_count` == 0 |
| 9 | Province-comparison output confined to `adjacency_divergence_g4.json`, labeled QA-only, not consumed downstream | 007b | Manual | Évaluateur reads `pipeline/geo/README.md` and `deliverables/manifest.json` for the explicit QA-only label; confirms no other file this brief produces reads `adjacency_divergence_g4.json` as input (grep for the filename across `pipeline/geo/steps/`, `pipeline/geo/pipeline.py`) |
| 10 | New G3/G4 evidence files tracked in git despite `.gitignore`'s wildcard exclusion | 007a, 007b | Mechanical | `evidence_files_git_tracked_count` == full declared count (`git ls-files` intersected with the declared evidence-file list per lot) |
| 10 | Mechanism used (force-add vs. `deliverables/` copy) matches or is consistent with whatever brief 002 actually used | 007a, 007b | Manual | Évaluateur checks `git ls-files pipeline/geo/logs pipeline/geo/artifacts` from before this brief's commits (via `git log`) to see whether 002's evidence was already tracked, and confirms this brief's evidence uses the same mechanism, or explicitly documents why it diverges |
| 11 | `pipeline/geo/README.md` updated truthfully after each lot | 007a, 007b | Manual | Évaluateur reads the post-007a README (must state G3 landed, G4+ not yet) and the post-007b README (must state G3+G4 landed, rivers onward not yet); fails on overclaim or underclaim |
| 12 | `deliverables/manifest.json` declares pre-port `.orig` snapshots and pre-edit README snapshots with `must_differ_from`, per lot | 007a, 007b | Mechanical | manifest parses; contains `deliverables/pre-port/03_cells.py.orig` (007a) or `deliverables/pre-port/04_adjacency.py.orig` (007b); README snapshot entry's `must_differ_from` target SHA256 differs from the snapshot's own SHA256 |
| 13 | `budget.py split-check` run first, per lot, with that lot's own estimate | 007a, 007b | Mechanical | Générateur's session log/transcript shows this command as an early action; harness re-runs it and confirms it does not report `NEEDS_SPLIT` for the stated per-lot estimate |
| 14 | Non-Goals hold: no `05_rivers.py` onward, no `08_ownership.py`, no `sim/`, no ADR-0003 edit | 007a, 007b | Mechanical | `git status --porcelain` outside each lot's declared file scope (brief.md's Lots table `Fichiers` row) must be empty; `docs/adr/0003-*.md` unchanged (`git diff --stat` shows no entry) |

## Plateau / waiver notes

- If the pip/venv waiver (`brief.md` Acceptable Waivers row 1) is invoked
  legitimately with its required output, the affected lot's file-copy and
  path-adjustment Success Conditions (1-6, and 2's decision table) may still
  be `accepted`, while that lot's determinism/QA Success Conditions (7 or 8)
  and evidence condition (10) are recorded as `blocked-by-environment` — not
  silently `passed`, not silently `rejected` (per `docs/rules/harness-roles.md`'s
  plateau/carry-forward convention, same as brief 002).
- Any Required Counter whose denominator evaluates to 0 (e.g. zero keys in a
  determinism dict, zero entries in a `checks` array) is a FAIL on that line,
  never a pass — an empty sample is not a clean result (`no_empty_sample_pass`).
- Lot 007b's evaluation must not begin until Lot 007a has an `accepted`
  verdict and `artifacts/cells_g3.json` exists on disk — 007b's own
  determinism proof reads that file as input; evaluating 007b against a
  missing or stale 007a artifact is not a valid run of Success Condition 8.
- The `g3_cell_count_in_range` and `g4_sea_zone_count_in_range` counters have
  **ranges**, not fixed expected values (`[150, 400]` and `[20, 40]`,
  respectively, both cited from `pipeline/geo/constants.py`) — do not treat
  any specific number within that range as a pass/fail threshold beyond the
  range itself; the mesh count is a measured output of the Voronoi/Poisson
  process, not a quota (this mirrors brief 002 iteration 2's lesson: an
  amended counter must stay a traceability/range rule, not calcify into a
  hardcoded number).
- Cross-repo SHA256 match between this repository's produced
  `artifacts/cells_g3.json` and VictoriaProject's own recorded value
  (`89cd42a41d32aec5b3d6209b47f0a7ad837cfd87f3cefa0b3f77f1d0d91465b2`, from its
  `MANIFEST_g3.json`) is **not** a Success Condition and must not be scored as
  one — library version drift (shapely/pyproj) between the two environments
  could legitimately produce a different but still internally-deterministic
  result. If the Générateur reports this cross-repo hash out of curiosity,
  the Évaluateur treats a mismatch as informational, not a defect, provided
  Success Condition 7's intra-repo two-run determinism still holds.
