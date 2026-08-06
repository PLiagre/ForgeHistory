# Eval rubric — Brief 007: geo pipeline port, installment 2 (G3 cells + G4 adjacency)

**Authored**: 2026-08-06T09:14:00
**Author**: forge-planificateur

> **Amendment 007a-R rubric (2026-08-06T12:10:00)**: rows for Success
> Conditions 4, 6, 7 below are superseded for Lot 007a only — see
> "## Amended rows — Amendment 007a-R" near the end of this file. Read that
> section before evaluating Lot 007a. Lot 007b rows are unaffected.

> **Amendment 007a-R2 rubric (2026-08-06T12:55:00)**: the `G3_SEED_COUNT_MAX`
> bound and its counters, rows within Amendment 007a-R's own section, are
> further superseded for Lot 007a only — see "## Amended rows — Amendment
> 007a-R2" at the very end of this file. Read that section before evaluating
> Lot 007a. Lot 007b rows are unaffected.

> **Amendment 007a-R3 rubric (2026-08-06T18:15:00)**: the `G3_AREA_CEIL_KM2`
> bound (frozen by Amendments 007a-R/007a-R2) is un-frozen and re-derived to
> 40,000 km²; `G3-E`'s row and the frozen-constants counters are further
> superseded for Lot 007a only — see "## Amended rows — Amendment 007a-R3"
> at the very end of this file, the last section, superseding all prior
> amendment sections where it names them. Read that section before evaluating
> Lot 007a. Lot 007b rows are unaffected.

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
| 4 | **[SUPERSEDED for 007a — see Amended rows below]** `steps/03_cells.py`: exactly one path adjustment, marked, nothing else changed | 007a | — | see Amended rows |
| 5 | `steps/04_adjacency.py`: exactly one path adjustment (`GAME_DATA`), marked, nothing else changed | 007b | Mechanical | same pattern, vs `deliverables/pre-port/04_adjacency.py.orig` — unaffected by Amendment 007a-R / 007a-R2 / 007a-R3 |
| 6 | **[SUPERSEDED for 007a scope — see Amended rows below]** No remaining `game_unity`/`StreamingAssets` reference outside the brief-002-established exception | 007a, 007b | — | 007b portion (`04_adjacency.py`) unaffected: mechanical, `game_unity_reference_remaining_count` == 0 after exclusions for that file |
| 7 | **[SUPERSEDED for 007a — see Amended rows below, further amended by 007a-R2, and again by 007a-R3]** `run_proof_g3.py` runs, exits 0, all checks green + red-proven, cell count in range | 007a | — | see Amended rows (007a-R3's is the version that must actually reach 14/14) |
| 8 | `run_proof_g4.py` runs, exits 0 | 007b | Mechanical | `proof_script_exit_code_zero_count` includes this script's exit code == 0; harness re-runs independently |
| 8 | G4 two-pass SHA256 determinism, all matched, none empty | 007b | Mechanical | `g4_determinism_sha_pairs_matched_count` == total key count, total > 0 |
| 8 | Every G4 named check green + red-proven, including `G4-B` with the natural (links-off) red-case | 007b | Mechanical | `g4_qa_checks_passed_count` / `g4_qa_checks_red_proof_count` == total entries; `g4_open_sea_reachability_without_links_fails` non-empty specifically for the `G4-B` entry |
| 8 | G4 sea zone count within declared bounds; all four adjacency kinds present | 007b | Mechanical | `g4_sea_zone_count_in_range` true against `[20, 40]`; `g4_adjacency_by_kind_nonzero_count` == 4 |
| 8 | Topology link (Zuiderzee/Afsluitdijk) proven load-bearing, not decorative | 007b | Mechanical + Manual | mechanical: `g4_open_sea_reachability_with_links` true AND `g4_open_sea_reachability_without_links_fails` non-empty (both conditions required together — links-on reachable, links-off not); manual: Évaluateur opens `artifacts/topology_links_g4.json`'s `links_applied` and confirms at least one entry with `applied: true` referencing a Zuiderzee/Lauwerszee-class basin |
| 9 | `artifacts/adjacency_g4.json` carries zero `province` references | 007b | Mechanical | `adjacency_g4_province_field_count` == 0 |
| 9 | Province-comparison output confined to `adjacency_divergence_g4.json`, labeled QA-only, not consumed downstream | 007b | Manual | Évaluateur reads `pipeline/geo/README.md` and `deliverables/manifest.json` for the explicit QA-only label; confirms no other file this brief produces reads `adjacency_divergence_g4.json` as input (grep for the filename across `pipeline/geo/steps/`, `pipeline/geo/pipeline.py`) |
| 10 | New G3/G4 evidence files tracked in git despite `.gitignore`'s wildcard exclusion | 007a, 007b | Mechanical | `evidence_files_git_tracked_count` == full declared count (`git ls-files` intersected with the declared evidence-file list per lot) |
| 10 | Mechanism used (force-add vs. `deliverables/` copy) matches or is consistent with whatever brief 002 actually used | 007a, 007b | Manual | Évaluateur checks `git ls-files pipeline/geo/logs pipeline/geo/artifacts` from before this brief's commits (via `git log`) to see whether 002's evidence was already tracked, and confirms this brief's evidence uses the same mechanism, or explicitly documents why it diverges |
| 11 | `pipeline/geo/README.md` updated truthfully after each lot | 007a, 007b | Manual | Évaluateur reads the post-007a README (must state G3 landed, G4+ not yet, **and — per Amendment 007a-R — that the mesh is genuinely non-degenerate, not merely ported; per Amendment 007a-R2, must truthfully describe the recalibrated `G3_SEED_COUNT_MAX=600` bound; per Amendment 007a-R3, must truthfully describe the recalibrated `G3_AREA_CEIL_KM2=40,000` bound and confirm G3-E through G3-H are all actually green — not merely "reachable"**) and the post-007b README (must state G3+G4 landed, rivers onward not yet); fails on overclaim or underclaim |
| 12 | `deliverables/manifest.json` declares pre-port `.orig` snapshots and pre-edit README snapshots with `must_differ_from`, per lot | 007a, 007b | Mechanical | manifest parses; contains `deliverables/pre-port/03_cells.py.orig` (007a — reused from the original 007a run per Amendment 007a-R, not necessarily re-created) or `deliverables/pre-port/04_adjacency.py.orig` (007b); README snapshot entry's `must_differ_from` target SHA256 differs from the snapshot's own SHA256 |
| 13 | `budget.py split-check` run first, per lot, with that lot's own estimate | 007a, 007b | Mechanical | Générateur's session log/transcript shows this command as an early action; harness re-runs it and confirms it does not report `NEEDS_SPLIT` for the stated per-lot estimate (007a's estimate is **135**, per Amendment 007a-R, superseding the original 95; Amendment 007a-R2's own incremental scope estimate of 20-30 and Amendment 007a-R3's own incremental estimate of 35-50 both stay inside that 135 total for a fresh session, not a new split-check requirement) |
| 14 | Non-Goals hold: no `05_rivers.py` onward, no `08_ownership.py`, no `sim/`, no ADR-0003 edit | 007a, 007b | Mechanical | `git status --porcelain` outside each lot's declared file scope (brief.md's Lots table `Fichiers` row) must be empty; `docs/adr/0003-*.md` unchanged (`git diff --stat` shows no entry) |

## Amended rows — Amendment 007a-R (2026-08-06T12:10:00) — Lot 007a only

| # | Success Condition (brief.md, Amendment 007a-R) | Lot | Check type | How it is checked |
|---|---|---|---|---|
| 4-R | `steps/03_cells.py` carries exactly one `# FORGEHISTORY-PATH-ADJUSTMENT` change plus zero-or-more `# FORGEHISTORY-G3-REPAIR`-marked changes; every diff line vs `.orig` ends in one of the two markers | 007a | Mechanical | `path_adjustment_marker_count` >= 1 AND `g3_unmarked_nonrepair_diff_line_count` == 0 (diff of `steps/03_cells.py` vs `deliverables/pre-port/03_cells.py.orig`, counting lines not ending in either marker) |
| 4-R | Every `# FORGEHISTORY-G3-REPAIR` hunk is explained in the generator-log with the specific failing check ID(s) it targets | 007a | Manual | Évaluateur reads `deliverables/generator-log.md`, matches each repair hunk to a named `G3-*` check, confirms the stated cause-effect is plausible against `qa/checks.py`'s actual logic for that check |
| 4-R | Any changed seeding-parameter constant in `constants.py` passes the mechanical seeding-vs-bound test and is marked + justified | 007a | Mechanical + Manual | mechanical: the changed constant is NOT one of the named frozen bound constants (six, per Amendment 007a-R3 — see below) AND is not read by any `g3*`/`q*` function in `qa/checks.py` (grep); manual: generator-log states before/after value and the target check |
| 6-R | Zero `game_unity`/`StreamingAssets` hits in `.py` files under `pipeline/geo/` for the 007a scope, after excluding the three named pre-existing `03_cells.py` literals (`:108`, `:109`, `:179`) and `constants.py`'s existing two-literal exception | 007a | Mechanical | `game_unity_reference_remaining_count` == 0 after the (five-literal-wide) exclusion set; any hit outside those five named lines counts |
| 6-R | No *new* `game_unity`/`StreamingAssets` literal introduced by a repair edit that happens to touch one of the three named lines | 007a | Manual | Évaluateur diffs the three named lines specifically against `.orig`; if changed, confirms the new text still contains no such literal (the exception does not grow) |
| 7-R | `run_proof_g3.py` exits 0, this session, this repository | 007a | Mechanical | `proof_script_exit_code_zero_count` includes this script's exit code == 0; harness re-runs independently — a Générateur-reported "0" alone is not sufficient |
| 7-R | Determinism preserved: two-run SHA256 comparison all-equal, none empty | 007a | Mechanical | `g3_determinism_sha_pairs_matched_count` == total key count in `logs/v1_049_qa.json`'s `determinism.sha256`, AND total > 0 (`no_empty_sample_pass`) |
| 7-R | Every `checks` entry (`G3-A`..`G3-H`, `q2`, `q3`) green AND red-proven — no carry-forward FAIL admissible | 007a | Mechanical | `g3_qa_checks_passed_count` == total entries AND `g3_qa_checks_red_proof_count` == total entries in `logs/v1_049_qa.json`'s `checks`; any FAIL here is REJECT for Lot 007a, full stop, **unless a currently-valid escalation is on record — per Amendment 007a-R3, only a fresh proof against the current `[G3_SEED_COUNT_MAX=600, G3_AREA_CEIL_KM2=40,000]` pair qualifies; the 445-vs-600 and 645-vs-600 proofs (both computed against the old 15,000 km² ceiling) are moot and do not excuse a sub-14/14 result** |
| 7-R | `cell_count` within `[150, 400]`; every land mass covered; giant-cell / ratio / compactness checks pass | 007a | Mechanical | `g3_cell_count_in_range` == true; `G3-B`/`G3-E`/`G3-F`/`G3-G` entries in `checks` all `passed: true`. **[Superseded by Amendment 007a-R2's row: range is `[150, 600]`. `G3-E`'s bound is further superseded by Amendment 007a-R3's row: checked against `G3_AREA_CEIL_KM2 = 40,000`, not `15,000`.]** |
| 7-R | The three check-definition files (`run_proof_g3.py`, `test_qa_red_g3.py`, `qa/checks.py`) remain byte-identical to VictoriaProject — the quality bar was not weakened | 007a | Mechanical | `g3_check_definitions_byte_identical` == 3 |
| 7-R | The eight named G3 acceptance-bound constants remain unchanged in value | 007a | Mechanical | `g3_bound_constants_unchanged` == 8. **[Superseded by Amendment 007a-R2's row: 7-constant / denominator-7, `G3_SEED_COUNT_MAX` split out. Superseded again by Amendment 007a-R3's row: 6-constant / denominator-6, `G3_AREA_CEIL_KM2` also split out — see that section.]** |
| 7-R | Escalation path used correctly if invoked: "no seeding-parameter-only solution exists" claim is NOT self-granted as a pass | 007a | Manual | if the generator-log claims this, Évaluateur confirms it is recorded as an escalation back to the Planificateur (matching the original 007a FAIL's own escalation pattern), not as an accepted waiver or a silent SC7 pass; absent this claim, ordinary FAIL/iterate rules apply. **This happened twice: first for `G3-E`/`G3-F`/`G3-G` against the `400`-seed cap (`generator-log.md` §R4, resolved by Amendment 007a-R2), then again for `G3-E`/`G3-G` against the `600`-seed cap at the `15,000` km² ceiling (`deliverables/checkpoint-002.md` §2.4, resolved by Amendment 007a-R3). Confirmed correctly escalated both times, not self-granted.** |

## Disqualifying Failures (Amendment 007a-R, Lot 007a only — item 2 superseded by Amendment 007a-R2, then again by Amendment 007a-R3, below)

Any ONE of the following is an automatic REJECT for Lot 007a, regardless of
`run_proof_g3.py`'s exit code or any other green counter:

1. Any byte difference between `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` and their VictoriaProject originals (`g3_check_definitions_byte_identical` < 3).
2. **[SUPERSEDED — see Amendment 007a-R3's Disqualifying Failures below, the
   currently-governing version]** Any of the eight named G3 acceptance-bound
   constants changed in value (`g3_bound_constants_unchanged` < 8).
3. Any `checks` entry with `passed: true` and an empty `red_proof` (a
   weakened or disabled red-case).
4. Any diff line in `steps/03_cells.py` vs `.orig` not ending in
   `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR`
   (`g3_unmarked_nonrepair_diff_line_count` > 0).
5. Any unequal or empty pair in `determinism.sha256` (nondeterminism
   introduced by the repair).

## Plateau / waiver notes

- If the pip/venv waiver (`brief.md` Acceptable Waivers row 1) is invoked
  legitimately with its required output, the affected lot's file-copy and
  path-adjustment Success Conditions (1-6, and 2's decision table) may still
  be `accepted`, while that lot's determinism/QA Success Conditions (7 or 8)
  and evidence condition (10) are recorded as `blocked-by-environment` — not
  silently `passed`, not silently `rejected` (per `docs/rules/harness-roles.md`'s
  plateau/carry-forward convention, same as brief 002). **This does not apply
  to Amendment 007a-R's Disqualifying Failures above — those are never
  `blocked-by-environment`, they are always REJECT.**
- **Per Amendment 007a-R, Lot 007a's Success Condition 7 (now "Amended SC7")
  may NO LONGER be recorded as a `brief-premise` carry-forward FAIL** — that
  disposition was valid only against the original port-only scope and was
  resolved by this amendment. A FAIL on Amended SC7 after this amendment is
  an ordinary Générateur/repair-design defect (iterate) unless the specific
  "no seeding-parameter-only solution exists" claim is escalated per its own
  row above. **Per Amendment 007a-R2, the first such escalation (raised for
  `G3-E`/`G3-F`/`G3-G` against `G3_SEED_COUNT_MAX=400`) was resolved by
  re-deriving `G3_SEED_COUNT_MAX` to 600. Per Amendment 007a-R3, the
  *second* such escalation (raised for `G3-E`/`G3-G` against the pair
  `[G3_SEED_COUNT_MAX=600, G3_AREA_CEIL_KM2=15,000]`) is resolved by
  re-deriving `G3_AREA_CEIL_KM2` to 40,000 instead. A FAIL on `G3-E` or
  `G3-G` in the *next* Lot 007a iteration (post-007a-R3) reverts to an
  ordinary Générateur/design defect, iterated normally — it may not be
  re-escalated as "still unsatisfiable" without a fresh pigeonhole-style
  proof against the new `[600, 40,000]` pair, since both prior proofs are
  specifically invalidated by the bound(s) they were computed against.**
- Any Required Counter whose denominator evaluates to 0 (e.g. zero keys in a
  determinism dict, zero entries in a `checks` array) is a FAIL on that line,
  never a pass — an empty sample is not a clean result (`no_empty_sample_pass`).
- Lot 007b's evaluation must not begin until Lot 007a has an `accepted`
  verdict and `artifacts/cells_g3.json` exists on disk — 007b's own
  determinism proof reads that file as input; evaluating 007b against a
  missing or stale 007a artifact is not a valid run of Success Condition 8.
  **Per Amendment 007a-R, this must be the repaired, all-green `cells_g3.json`
  — not the original 401-cell degenerate artifact. Per Amendment 007a-R2,
  "all-green" was checked against `cell_count` in `[150, 600]`. Per
  Amendment 007a-R3, "all-green" additionally requires the mesh to have been
  built against `G3_AREA_CEIL_KM2 = 40,000`, not `15,000` — a `cells_g3.json`
  produced before this amendment's `constants.py` edit does not qualify,
  even if its own `cell_count` happens to fall in `[150, 600]`.**
- The `g3_cell_count_in_range` and `g4_sea_zone_count_in_range` counters have
  **ranges**, not fixed expected values (`[150, 600]` for G3, unchanged by
  Amendment 007a-R3, and `[20, 40]` for G4, both cited from
  `pipeline/geo/constants.py`) — do not treat any specific number within that
  range as a pass/fail threshold beyond the range itself; the mesh count is a
  measured output of the Voronoi/Poisson process, not a quota (this mirrors
  brief 002 iteration 2's lesson: an amended counter must stay a
  traceability/range rule, not calcify into a hardcoded number). **This
  holds unchanged post-Amendment 007a-R3: the repair must land the count
  somewhere in `[150, 600]`, not at a specific chosen value — by contrast,
  `G3_SEED_COUNT_MAX = 600` AND (as of Amendment 007a-R3) `G3_AREA_CEIL_KM2
  = 40,000` are each specific chosen values with their own dedicated
  counters (`g3_seed_count_max_matches_derivation`,
  `g3_area_ceil_matches_derivation`), because they are re-derived bounds,
  not measured outputs.**
- Cross-repo SHA256 match between this repository's produced
  `artifacts/cells_g3.json` and VictoriaProject's own recorded value
  (`89cd42a41d32aec5b3d6209b47f0a7ad837cfd87f3cefa0b3f77f1d0d91465b2`, from its
  `MANIFEST_g3.json`) is **not** a Success Condition and must not be scored as
  one — library version drift (shapely/pyproj) between the two environments
  could legitimately produce a different but still internally-deterministic
  result. **Post-Amendment 007a-R this is even less meaningful**, since the
  repaired mesh is expected to differ from VictoriaProject's degenerate
  current output by design, and post-Amendment 007a-R3 the two repos' G3
  bound constants are no longer even the same, making a hash match
  structurally impossible and irrelevant. If the Générateur reports this
  cross-repo hash out of curiosity, the Évaluateur treats a mismatch as
  informational, not a defect, provided Amended Success Condition 7's
  intra-repo two-run determinism still holds.

## Amended rows — Amendment 007a-R2 (2026-08-06T12:55:00) — Lot 007a only

Resolves the escalation Amendment 007a-R's "7-R / Escalation path" row
above records: `G3-E`/`G3-F`/`G3-G` were proven, by a closed-form pigeonhole
argument (`400 × 15,000 km² = 6,000,000 km² < 6,667,146.53 km²` actual land
area, given `Q2`'s near-total-coverage requirement), mathematically
unsatisfiable under the frozen `G3_SEED_COUNT_MAX = 400`. This amendment
re-derives that one bound to **600** — see brief.md's own "Amendment
007a-R2" section for the full arithmetic and rationale. All rows below
supersede the corresponding Amendment 007a-R row of the same name; any row
not listed here (e.g. `g3_check_definitions_byte_identical`,
`g3_unmarked_nonrepair_diff_line_count`, `g3_determinism_sha_pairs_matched_count`)
is unchanged from Amendment 007a-R. **Amendment 007a-R3, below, further
supersedes this section's `g3_bound_constants_unchanged` row and
Disqualifying Failure item 2 — read that section for the currently-governing
version of both.**

| # | Success Condition (brief.md, Amendment 007a-R2) | Lot | Check type | How it is checked |
|---|---|---|---|---|
| 7-R2 | `cell_count` within the re-derived `[150, 600]` range (supersedes Amendment 007a-R's `[150, 400]`) | 007a | Mechanical | `g3_cell_count_in_range` computed against `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` read live from `pipeline/geo/constants.py` (not hardcoded `[150,400]` by the harness) — must be true. **Unaffected by Amendment 007a-R3 — this range stays `[150, 600]`.** |
| 7-R2 | `G3_SEED_COUNT_MAX` changed to exactly the derived value **600**, and no other bound constant touched | 007a | Mechanical | `g3_seed_count_max_matches_derivation` == true (this repo's `constants.py` `G3_SEED_COUNT_MAX` == 600) AND `g3_bound_constants_unchanged` == 7 (the seven other named constants). **[Superseded by Amendment 007a-R3's row below: `g3_bound_constants_unchanged` is now a 6-constant counter, since `G3_AREA_CEIL_KM2` is also carved out.]** |
| 7-R2 | The `G3_SEED_COUNT_MAX` change in `constants.py` is a single line, marked `# FORGEHISTORY-G3-REPAIR` | 007a | Mechanical | grep the changed line in `constants.py`; must end in the marker; diff of `constants.py` vs its brief-002-landed state shows exactly one changed value (the `G3_SEED_COUNT_MAX` line). **Amendment 007a-R3 adds a second such line (`G3_AREA_CEIL_KM2`) — both must independently satisfy this pattern.** |
| 7-R2 | `G3-E`/`G3-F`/`G3-G` (and every other `G3-*`/`q*` check) green AND red-proven under the new bound — the raised ceiling alone does not constitute a pass; the mesh must still be re-run and actually satisfy them | 007a | Mechanical | `g3_qa_checks_passed_count` == total entries AND `g3_qa_checks_red_proof_count` == total entries in the *post-007a-R2* `logs/v1_049_qa.json`'s `checks` array — a re-run is required; the pre-007a-R2 run (with `G3-E/F/G` still `false`) does not satisfy this row. **Historical note: the post-007a-R2 re-run reached 12/14 (`G3-D`/`G3-F` green; `G3-E`/`G3-G` still red) — this row was NOT satisfied by Amendment 007a-R2 alone. Amendment 007a-R3's own "7-R3" row, below, is the version that must actually reach 14/14 and is the currently-governing one.** |
| 7-R2 | The prior pigeonhole escalation is not re-raised without a fresh proof against the new bound | 007a | Manual | if the generator-log claims `G3-E`/`G3-F`/`G3-G` remain unsatisfiable after this recalibration, Évaluateur confirms a *new* pigeonhole-style computation is shown against `G3_SEED_COUNT_MAX = 600` (not a restatement of the old `400`-based proof) before treating it as a valid re-escalation, per the plateau note above. **This is exactly what happened: the fresh per-land-part proof in `deliverables/checkpoint-002.md` §2.4 (645-vs-600, not a restatement of the old 445-vs-600 estimate) is what triggered Amendment 007a-R3.** |

### Disqualifying Failures — Amendment 007a-R2 (Lot 007a only; superseded in turn by Amendment 007a-R3 below — kept here for the historical record)

Any ONE of the following was, at the 007a-R2 stage, an automatic REJECT for
Lot 007a, regardless of `run_proof_g3.py`'s exit code or any other green
counter:

1. Any byte difference between `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` and their VictoriaProject originals
   (`g3_check_definitions_byte_identical` < 3). *(unchanged, still governs)*
2. **[SUPERSEDED by Amendment 007a-R3 below — the currently-governing
   version]** Any of the seven then-frozen G3 constants (`G3_SEED_COUNT_MIN`,
   `G3_AREA_FLOOR_KM2`, `G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`,
   `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`) changed in
   value (`g3_bound_constants_unchanged` < 7), **OR** `G3_SEED_COUNT_MAX` set
   to any value other than **600** (`g3_seed_count_max_matches_derivation`
   false). Touching `G3_AREA_CEIL_KM2` (or any of the other six) to "solve"
   the pigeonhole shortfall instead of moving `G3_SEED_COUNT_MAX` was the
   disqualifying move this row existed to catch — **as of Amendment
   007a-R3, `G3_AREA_CEIL_KM2` is legitimately movable to exactly 40,000
   under the owner's own Option 2 decision; this specific prohibition no
   longer applies to that one constant.**
3. Any `checks` entry with `passed: true` and an empty `red_proof`
   (unchanged, still governs).
4. Any diff line in `steps/03_cells.py` vs `.orig`, or in `constants.py`'s
   `G3_SEED_COUNT_MAX` line, not ending in `# FORGEHISTORY-PATH-ADJUSTMENT`
   or `# FORGEHISTORY-G3-REPAIR` (unchanged, still governs — now also
   extended to the `G3_AREA_CEIL_KM2` line per Amendment 007a-R3).
5. Any unequal or empty pair in `determinism.sha256` (unchanged, still
   governs).

## Amended rows — Amendment 007a-R3 (2026-08-06T18:15:00) — Lot 007a only — **currently governing version**

Resolves the second escalation, raised against Amendment 007a-R2's own
`G3_SEED_COUNT_MAX = 600` bound: a fresh, materially stronger per-land-part
pigeonhole proof (`deliverables/checkpoint-002.md` §2.4, `run-report-007a-R2.md`)
showed a genuinely uniform mesh at the frozen `G3_AREA_CEIL_KM2 = 15,000`
km² needs ≥645 cells at the theoretical best case, realistically ≈837-900 —
`600` is short even at the theoretical floor. The owner was escalated four
options and chose **Option 2**: relax `G3_AREA_CEIL_KM2` to **40,000.0**
km² instead of doubling `G3_SEED_COUNT_MAX` toward ~900. See brief.md's own
"Amendment 007a-R3" section for the full real-world-anchor + measured-margin
derivation. All rows below supersede the corresponding row of the same name
in Amendment 007a-R2's section (and, transitively, Amendment 007a-R's);
any row not listed here (e.g. `g3_check_definitions_byte_identical`,
`g3_unmarked_nonrepair_diff_line_count` for `steps/03_cells.py`'s path
adjustment specifically, `g3_determinism_sha_pairs_matched_count`,
`g3_seed_count_max_matches_derivation`) is unchanged.

| # | Success Condition (brief.md, Amendment 007a-R3) | Lot | Check type | How it is checked |
|---|---|---|---|---|
| 7-R3 | `G3_AREA_CEIL_KM2` changed to exactly the derived value **40,000.0**, and none of the six other now-frozen constants (nor `G3_SEED_COUNT_MAX`) touched | 007a | Mechanical | `g3_area_ceil_matches_derivation` == true (this repo's `constants.py` `G3_AREA_CEIL_KM2` == 40,000.0) AND `g3_bound_constants_unchanged` == 6 (`G3_SEED_COUNT_MIN`, `G3_AREA_FLOOR_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`, all unchanged) AND `g3_seed_count_max_matches_derivation` == true (still 600, untouched by this amendment) |
| 7-R3 | The `G3_AREA_CEIL_KM2` change in `constants.py` is a single line (line ~409), marked `# FORGEHISTORY-G3-REPAIR` | 007a | Mechanical | grep the changed line in `constants.py`; must end in the marker; diff of `constants.py` vs its Amendment-007a-R2-landed state shows exactly one changed value (the `G3_AREA_CEIL_KM2` line) |
| 7-R3 | **`G3_COMPACTNESS_MIN` (0.18) and `G3_AREA_MAX_MEDIAN_RATIO` (8.0) — the SHAPE-quality bounds — are byte-value-identical to their pre-007a-R3 state; the fix for `G3-G` comes from seeding logic, never from relaxing either bound** | 007a | Mechanical + Manual | mechanical: both values unchanged in `constants.py`, covered by `g3_bound_constants_unchanged` == 6; manual: Évaluateur confirms `deliverables/generator-log.md`'s `G3-G` fix (if any) is a `# FORGEHISTORY-G3-REPAIR`-marked change to `steps/03_cells.py` seeding/construction logic (e.g. an extra-seed-at-worst-offender pass, Lloyd iteration tuning), not a `constants.py` threshold edit |
| 7-R3 | A **fresh** `run_proof_g3.py` re-run, this session, this repository, against the new `G3_AREA_CEIL_KM2`, reaches **14/14** `checks` entries `passed: true` with non-empty `red_proof` — the currently-governing version of Amended SC7; no carry-forward FAIL, no admissible re-escalation short of a fresh, materially-different pigeonhole proof against the new `[600, 40,000]` pair specifically | 007a | Mechanical | `g3_qa_checks_passed_count` == 14 AND `g3_qa_checks_red_proof_count` == 14 in the *post-007a-R3* `logs/v1_049_qa.json`'s `checks` array; a pre-007a-R3 measurement (12/14 or worse) does not satisfy this row; `proof_script_exit_code_zero_count` includes a 0 exit for this fresh run |
| 7-R3 | `G3-E` specifically checked against 40,000.0 km², not 15,000.0 | 007a | Mechanical | `checks` entry `G3-E`'s own `detail` field (or a direct re-computation from `artifacts/cells_g3.json`'s `area_km2` values) confirms the 40,000.0 bound was the one applied — cross-check against `constants.py`'s live value, not a hardcoded 15,000 assumption in any harness script |
| 7-R3 | `cell_count` still within `[150, 600]` (range itself unchanged by this amendment) | 007a | Mechanical | `g3_cell_count_in_range` == true against `[150, 600]` |
| 7-R3 | `pipeline/geo/README.md` and `deliverables/manifest.json` truthfully state the recalibrated `G3_AREA_CEIL_KM2 = 40,000` and its real-world-anchor rationale (not merely "raised the ceiling") | 007a | Manual | Évaluateur reads both files for an explicit statement of the new value and a one-line rationale consistent with brief.md's Amendment 007a-R3 section (real-world country-scale anchor + measured-margin, not "made the check pass") |

### Disqualifying Failures — Amendment 007a-R3 (Lot 007a only; **currently governing version** — supersedes item 2 of Amendment 007a-R2's list)

Any ONE of the following is an automatic REJECT for Lot 007a, regardless of
`run_proof_g3.py`'s exit code or any other green counter:

1. Any byte difference between `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` and their VictoriaProject originals
   (`g3_check_definitions_byte_identical` < 3). *(unchanged)*
2. **[CURRENTLY GOVERNING]** Any of the six now-frozen G3 constants
   (`G3_SEED_COUNT_MIN`, `G3_AREA_FLOOR_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`,
   `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`) changed in
   value (`g3_bound_constants_unchanged` < 6), **OR** `G3_SEED_COUNT_MAX` set
   to any value other than **600** (`g3_seed_count_max_matches_derivation`
   false), **OR** `G3_AREA_CEIL_KM2` set to any value other than **40,000.0**
   (`g3_area_ceil_matches_derivation` false). **Explicitly includes
   relaxing `G3_COMPACTNESS_MIN` or `G3_AREA_MAX_MEDIAN_RATIO` to force
   `G3-G` or `G3-F` green** — Owner Option 2 relaxes cell SIZE only, never
   cell SHAPE quality; this is precisely the disqualifying move this row
   exists to catch, named explicitly per the Planificateur brief's own
   instruction. Also includes editing any of the three check-definition
   files to weaken or bypass a check (independently covered by item 1, named
   again here for emphasis).
3. Any `checks` entry with `passed: true` and an empty `red_proof`
   (unchanged).
4. Any diff line in `steps/03_cells.py` vs `.orig`, or in `constants.py`'s
   `G3_AREA_CEIL_KM2` or `G3_SEED_COUNT_MAX` lines, not ending in
   `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR`.
5. Any unequal or empty pair in `determinism.sha256` (unchanged).
6. `run_proof_g3.py`'s `checks` array showing anything short of 14/14
   `passed: true` **without** a fresh, materially-different pigeonhole-style
   proof of unsatisfiability specifically against the current
   `[G3_SEED_COUNT_MAX=600, G3_AREA_CEIL_KM2=40,000]` pair — a restatement of
   either the 445-vs-600 (Amendment 007a-R2) or 645-vs-600
   (`checkpoint-002.md` §2.4) argument, both computed against the now-moot
   15,000 km² ceiling, does not qualify as such a proof.
