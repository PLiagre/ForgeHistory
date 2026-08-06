# Brief 007: Geo pipeline port, installment 2 — G3 cells + G4 adjacency

**Authored**: 2026-08-06T09:14:00
**Author**: forge-planificateur

> **Amendment (2026-08-06, orchestrator, pre-generation)**: the original
> `Authored` value `2026-08-06T15:20:00` was ~6h ahead of the real session
> clock (brief.md true mtime `2026-08-06 09:14:11`), which would have future-
> dated the brief past every deliverable and tripped the gate's
> `mtime_after_brief` / `rubric_predates_deliverables` checks (the exact
> failure mode HANDOFF flagged from brief 004). Corrected to the file's real
> authoring time. No content change. The rubric's `Authored` was corrected
> identically.

> **Amendment 007a-R (2026-08-06T12:10:00)**: Lot 007a's Success Conditions
> 4, 6, 7 below are superseded — see "## Amendment 007a-R (2026-08-06):
> port → repair (owner Option A)" at the end of this file. Read that section
> before starting or evaluating Lot 007a. Lot 007b is unaffected.

> **Amendment 007a-R2 (2026-08-06T12:55:00)**: Lot 007a's `constants.py`
> `G3_SEED_COUNT_MAX` bound (frozen by Amendment 007a-R at 400) is re-derived
> to 600 against the current coastline's real land area — see
> "## Amendment 007a-R2 (2026-08-06): recalibrate G3_SEED_COUNT_MAX to the
> current coastline (owner Option a)" at the end of this file. This resolves
> the pigeonhole escalation the Amendment 007a-R repair run raised on
> G3-E/F/G (see `deliverables/generator-log.md` "Lot 007a-R (repair)" §R4 and
> `deliverables/007a-repair-validation.log`). Read that section before
> resuming or evaluating Lot 007a. Lot 007b is unaffected.

> **Amendment 007a-R3 (2026-08-06T18:15:00)**: Amendment 007a-R2's own
> `G3_SEED_COUNT_MAX = 600` bound has since been shown, by a *fresh*,
> stronger per-land-part pigeonhole proof (`deliverables/checkpoint-002.md`
> §2.4, `run-report-007a-R2.md`), to still be mathematically insufficient at
> the frozen `G3_AREA_CEIL_KM2 = 15,000` km² — a genuine uniform mesh at that
> ceiling needs ≈837-900 cells, roughly double the brief's own stated
> "~150-400 addressable cells" design grain. The owner was escalated four
> options (`run-report-007a-R2.md`'s Options table) and chose **Option 2:
> relax `G3_AREA_CEIL_KM2` instead of doubling the cell count** — see
> "## Amendment 007a-R3 (2026-08-06): relax G3_AREA_CEIL_KM2 (owner Option 2)"
> at the end of this file. `G3_SEED_COUNT_MAX` (600) is **not** touched again
> by this amendment; only `G3_AREA_CEIL_KM2` moves, and only that one bound.
> Read that section before resuming or evaluating Lot 007a. Lot 007b is
> unaffected — it now simply reads whichever cells land within the newly
> re-derived bounds.

## World-Terms Requirement

Stated causally, not as a tooling preference:

Brief 002 gave `pipeline/geo/` a coastline — land exists, bounded, reproducible.
But a coastline alone answers only "where is land"; it cannot answer "how many
distinct places are there" or "which of those places touch which others."
Every later F1 system that reasons about a place — a garrison holding ground,
a merchant caravan choosing a road, a family migrating from a shrinking
province — needs a **grain**: a finite set of cells the rest of the engine can
address, own, tax, and move between. ADR-0003 already decided that grain's
identity (`cell_id` is the single spatial primary key; Province is a derived
aggregation of cells, never the reverse). This brief is what actually produces
that grain and its connectivity: `03_cells.py`'s Voronoi mesh (seeded by
declared urban density, not invented) turns the coastline into ~150-400
addressable cells, and `04_adjacency.py`'s typed adjacency (land-land,
land-sea, sea-sea, strait, plus the declared Zuiderzee/Afsluitdijk topology
link) turns that mesh into a graph a transport route, an army march, or a
migration decision can actually traverse. Until this lands, `sim/`'s eventual
migration/army/trade logic (Principle 3: nothing teleports, everything has a
transport path) has cells to reference but no proven way to know which cell
touches which — every "can this caravan reach that port" question is
unanswerable. This brief is scoped to porting `03_cells.py` and
`04_adjacency.py` unchanged in their determinism and algorithm, exactly as
brief 002 ported `02_coastline.py`/`02b_corrections_1400.py` unchanged — not
to redesigning the mesh or the adjacency typing.

## Load-bearing discovery: `pipeline.py` is a hard runtime dependency of G3

`harness/queue/geo-pipeline-port-plan.md`'s Brief 003 paragraph names the
legacy-game-data problem but does not mention this: `steps/03_cells.py`'s
`derive_adjacency()` function dynamically loads
`C:\Users\liagr\VictoriaProject\sandbox\geo\pipeline.py` at runtime via
`importlib.util.spec_from_file_location` (see `03_cells.py` lines 1125-1129,
`_load_pipeline_module`) to reuse `pipeline.py`'s `stage_derive()` for a QA-only
untyped adjacency pass that is exported as `artifacts/adjacency_g3.json`.
`pipeline.py` was **not** part of brief 002's copy list (002 only ported
`02_coastline.py` and `02b_corrections_1400.py`). Without it, any run of
`run_cells()` — and therefore `tests/run_proof_g3.py` — fails at that call.
This brief must port `pipeline.py` itself, byte-identical, to
`pipeline/geo/pipeline.py` (top-level, not under `steps/` — `03_cells.py`
resolves it via `ROOT / "pipeline.py"` where `ROOT` is `steps/`'s parent).
Verified safe to import standalone: `pipeline.py`'s module-level code only
imports `constants`/`io_util`/`projection` (already ported) and defines
functions; it does not read `fixtures/` (G1's test fixtures, never ported and
not needed) or any `game_unity` path at import time — only inside function
bodies this brief never calls (`stage_ingest`, `run_pipeline`, `main`).

## Legacy game-data decision (forced explicitly, per file — do not silently resolve)

Per the port-plan, `03_cells.py` and `04_adjacency.py` together read five
legacy `game_unity` files read-only. Each gets an explicit, recorded decision:

| file | read by | decision | reason |
|---|---|---|---|
| `cities.json` | `03_cells.py` (`load_cities_readonly`) | **copy** byte-identical to `legacy_game_data/cities.json` | hard `FileNotFoundError` if absent; city population is the declared input to G3's seed-density field `r(x)` — this is not optional metadata, it is the mesh's own justification for non-uniform density |
| `city_coordinates.json` | `03_cells.py` (`load_cities_readonly`) | **copy** byte-identical to `legacy_game_data/city_coordinates.json` | same — paired with `cities.json`, both required together |
| `sea_zones.json` | `04_adjacency.py` (`_load_attested_sea_names`) | **copy** byte-identical to `legacy_game_data/sea_zones.json` | supplies the attested names ("Manche", "Mer du Nord", ...) G4 assigns to sea zones; without it every zone would be silently unnamed, which is a real behavior change the port must not introduce quietly |
| `province_adjacency.json` | `04_adjacency.py` (`compare_province_adjacency`) | **reuse** brief 002's existing copy at `legacy_game_data/province_adjacency.json`, SHA256 re-verified unchanged, **not** re-copied | already ported in brief 002; used here only for a QA/divergence comparison pass (see Success Condition 9) |
| `province_coordinates.json` | `04_adjacency.py` (`compare_province_adjacency`) + already used by `constants.py`'s pilot-window derivation | **reuse** brief 002's existing copy, SHA256 re-verified unchanged, **not** re-copied | same |

None of these five may be silently skipped, silently re-derived, or silently
assumed absent — a missing legacy input the script hard-requires is a copy
decision, not a design decision, and this table is where that decision is
recorded.

## Success Conditions

1. **`pipeline.py` ported byte-identical** to `pipeline/geo/pipeline.py`
   (top-level). SHA256-equal to
   `C:\Users\liagr\VictoriaProject\sandbox\geo\pipeline.py`. [Lot 007a]

2. **Legacy game-data decision executed** exactly as the table above: three
   new byte-identical copies (`cities.json`, `city_coordinates.json`,
   `sea_zones.json`) with SHA256 equal to the target values recorded in
   VictoriaProject's own `artifacts/MANIFEST_g3.json` / `artifacts/MANIFEST_g4.json`
   `inputs` block (cited exactly, so this is checkable without re-deriving
   anything from memory):
   - `cities.json` → `e2052ac855692316fbda19c9ae4a8d76bb9d7e196ccdf5702b43f672fbbcebfb`
   - `city_coordinates.json` → `052f7f4bfcf0bf16caff2ee9ea6048bd89c9b4200430749918ea48a6e69e00aa`
   - `sea_zones.json` → `1692363b6b292fc2d74e4635c26d0cdcf4feedc1273d74135c3fbf24ea504927`

   and two existing copies (`province_adjacency.json` → `37e8b5f3da0d632cce89c8a9bb0108bea3c59ad885babdbfeac053cd5d0ef89e`,
   `province_coordinates.json`) re-verified unchanged since brief 002, not
   re-copied. [Lot 007a for cities/city_coordinates; Lot 007b for sea_zones +
   re-verification]

3. **Exact file copy list**, mirroring brief 002's table shape:

   | source (under `sandbox/geo/` unless noted) | destination (under `pipeline/geo/`) | byte-identical? | lot |
   |---|---|---|---|---|
   | `pipeline.py` | `pipeline.py` | yes | 007a |
   | `steps/03_cells.py` | `steps/03_cells.py` | no — marked path adjustment (SC4) | 007a |
   | `tests/run_proof_g3.py` | `tests/run_proof_g3.py` | yes | 007a |
   | `tests/test_qa_red_g3.py` | `tests/test_qa_red_g3.py` | yes | 007a |
   | `..\game_unity\...\cities.json` | `legacy_game_data/cities.json` | yes | 007a |
   | `..\game_unity\...\city_coordinates.json` | `legacy_game_data/city_coordinates.json` | yes | 007a |
   | `steps/04_adjacency.py` | `steps/04_adjacency.py` | no — marked path adjustment (SC5) | 007b |
   | `tests/run_proof_g4.py` | `tests/run_proof_g4.py` | yes | 007b |
   | `tests/test_qa_red_g4.py` | `tests/test_qa_red_g4.py` | yes | 007b |
   | `..\game_unity\...\sea_zones.json` | `legacy_game_data/sea_zones.json` | yes | 007b |

   `constants.py`, `io_util.py`, `projection.py`, `qa/checks.py`,
   `requirements.txt`, `sources.lock`, `legacy_game_data/province_*.json` are
   **not** re-copied — they already exist from brief 002 and already carry
   every G3/G4 constant and QA-check function this brief needs (verified: the
   ported `constants.py` already defines `G3_*`/`G4_*`/`SEA_ZONE_*`; the
   ported `qa/checks.py` already defines `run_g3_green`/`run_g4_green` and the
   individual `g3*`/`g4*`/`q*` check functions). No file outside this table
   plus `.gitignore`/`README.md`/`deliverables/**` is created under
   `pipeline/geo/` in this brief.

4. **[AMENDED — see "Amendment 007a-R" below]** Marked path adjustment —
   `steps/03_cells.py`, exactly one adjustment,
   nothing else.** `CITIES_JSON` and `CITY_COORDS_JSON` (lines ~71-86)
   currently resolve to `<repo-root>/game_unity/Assets/StreamingAssets/data/cities.json`
   and `.../city_coordinates.json`. Both must resolve instead to
   `pipeline/geo/legacy_game_data/cities.json` and
   `.../city_coordinates.json`. Every changed line ends with the literal
   marker `# FORGEHISTORY-PATH-ADJUSTMENT`. This is the **only** logic change
   permitted in this file. [Lot 007a]

5. **Marked path adjustment — `steps/04_adjacency.py`, exactly one
   adjustment, nothing else.** `GAME_DATA` (line ~68) currently resolves to
   `<repo-root>/game_unity/Assets/StreamingAssets/data`, and
   `SEA_ZONES_JSON`/`PROVINCE_ADJ_JSON`/`PROVINCE_COORDS_JSON`/`CITIES_JSON`/`CITY_COORDS_JSON`
   are all derived from it. `GAME_DATA` must resolve instead to
   `pipeline/geo/legacy_game_data`. Marked with
   `# FORGEHISTORY-PATH-ADJUSTMENT`. This is the **only** logic change
   permitted in this file. [Lot 007b]

6. **[AMENDED — see "Amendment 007a-R" below]** No remaining
   `game_unity`/`StreamingAssets` reference** anywhere under
   `pipeline/geo/` outside the same narrow exception brief 002's amended
   Success Condition 3 already carved out for `data/divergences_1400.json`'s
   own pre-existing prose and `constants.py`'s `FORBIDDEN_GAME_PATH_MARKERS`
   literals (unchanged, not touched by this brief). Extended to the two new
   step files: a hit inside any `.py` file always counts. [Lot 007a, 007b]

7. **[AMENDED — see "Amendment 007a-R" below]** G3 determinism + QA proof
   runs, in this repository, by command.** From
   `pipeline/geo/`: `.venv/Scripts/python.exe tests/run_proof_g3.py` exits 0.
   Its own two-run comparison (`logs/v1_049_qa.json`'s `determinism.sha256`)
   has every compared artifact's two SHA256 values equal and non-empty; every
   entry in its `checks` array has `passed: true` and a non-empty `red_proof`.
   `artifacts/stats_g3.json`'s `cell_count` is within
   `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX] = [150, 400]` (from
   `pipeline/geo/constants.py` — not re-derived, cited from the file that
   already governs it) and every land mass is covered (the script itself
   raises `RuntimeError` on an uncovered mass — a completed, non-crashing run
   with `passed`-covering checks is the evidence this held). [Lot 007a]
   **[Amendment 007a-R2: the `[150, 400]` range above is superseded to
   `[150, 600]` — see that amendment's section at the end of this file.]**

8. **G4 determinism + QA proof runs, by command.**
   `.venv/Scripts/python.exe tests/run_proof_g4.py` exits 0. Two-run SHA256
   comparison in `logs/v1_050_qa.json` all-equal and non-empty; every `checks`
   entry `passed: true` with a non-empty `red_proof`, **including** `G4-B`
   (open-sea reachability) whose own red-case in `run_proof_g4.py` is the
   *natural* one — running adjacency with topology links disabled and
   observing the Zuiderzee/Lauwerszee basin become genuinely unreachable, not
   a synthetic mutation. `artifacts/stats_g4.json`'s `sea_zone_count` is
   within `[SEA_ZONE_COUNT_MIN, SEA_ZONE_COUNT_MAX] = [20, 40]`. `by_kind`
   shows all four adjacency kinds (`land-land`, `land-sea`, `sea-sea`,
   `strait`) present with count > 0 — this is the actual, measured proof that
   the Pas-de-Calais-class strait detection and the declared Zuiderzee
   topology link both fired on real data, not just that the script exited
   cleanly. [Lot 007b]

9. **ADR-0003 compliance in the exported artifacts, not just in prose.**
   `artifacts/adjacency_g4.json` — the typed-adjacency artifact any later F1
   brief (LOD, id textures, `sim/`) will actually read — contains **zero**
   occurrences of the substring `province`. The province-comparison output
   (`compare_province_adjacency`'s `province_to_cell` map and contradicted/
   missed edge counts) lives **only** in `artifacts/adjacency_divergence_g4.json`,
   which `pipeline/geo/README.md` and `deliverables/manifest.json` must both
   label explicitly as **QA/divergence-only** — a one-time comparison against
   VictoriaProject's legacy 50-province hand-drawn adjacency, never consumed
   by any other artifact, never treated as a spatially authoritative mapping,
   and never read by any code this brief adds outside the QA proof itself.
   This is the port-plan's flagged tension made concrete: the comparison
   method necessarily uses `province_coordinates.json`'s numeric ids to
   locate a province's representative cell, but that mapping must not leak
   into anything downstream calls authoritative. [Lot 007b]

10. **Evidence committed despite `pipeline/geo/.gitignore`'s wildcard
    exclusion.** `pipeline/geo/.gitignore` currently excludes `artifacts/`,
    `logs/`, `capture/`, and `build/` wholesale — meaning every G3/G4 output
    this brief produces is, by default, invisible to `git status` and would
    not survive a fresh clone, exactly the gap `CLAUDE.md`'s Execution
    Contract warns about generically. Before claiming any evidence file
    exists "on disk", the Générateur must check whether brief 002's own
    evidence files (`pipeline/geo/logs/v1_046_qa.json`,
    `pipeline/geo/artifacts/coastline_1400.json`, etc.) are actually tracked
    in git (`git ls-files pipeline/geo/logs pipeline/geo/artifacts`) despite
    the ignore rule, and follow the **same** mechanism for this brief's new
    files (most likely `git add -f`, since 002's `deliverables/manifest.json`
    declares those exact paths as deliverables) — not invent a second,
    inconsistent mechanism. This must be a recorded decision, not silently
    skipped. [Lot 007a, 007b]

11. **`pipeline/geo/README.md` updated**, once after 007a (states G3 cells
    landed; G4 adjacency, rivers, relief, cities, ownership, LOD, id textures
    still not landed) and again after 007b (states G4 adjacency also landed;
    the remainder still not landed). Must not overclaim.

12. **`deliverables/manifest.json`** (one per lot, mirroring 002's structure)
    declares: pre-port snapshots of the two marked-adjustment originals
    (`deliverables/pre-port/03_cells.py.orig` for 007a,
    `deliverables/pre-port/04_adjacency.py.orig` for 007b); a pre-edit
    snapshot of `pipeline/geo/README.md` for each lot's edit, with
    `must_differ_from` pointing at the post-edit README (two
    `must_differ_from` pairs total across the two lots — 007a's pre/post
    README snapshot, and 007b's pre/post README snapshot, each pair distinct
    from the other and from brief 002's own snapshot).

13. **Split-check run first.** Before any file is touched, the Générateur
    runs `py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls <N>` once per lot with that lot's own estimate (see Budget section) — this brief is pre-scoped into lots below precisely so this check is expected to read within-budget per lot, not `NEEDS_SPLIT` again.

14. **Non-Goals hold** (see below) — no file from `05_rivers.py` onward,
    `08_ownership.py`, `sim/`, or `docs/adr/0003-*.md` is touched.

## Non-Goals

- Must NOT create, copy, or modify `steps/05_rivers.py`, `05b_navigability_1400.py`,
  `05c_rivers_europe.py`, `06_relief.py`, `07_cities.py`, `08_ownership.py`,
  `09_lod.py`, `10_id_textures.py`, `qa/run_all.py`, or `qa/crs_coherence.py`
  — those are briefs 004/005 per `harness/queue/geo-pipeline-port-plan.md`.
- Must NOT port `fixtures/` (G1's test-only fixtures) — not needed; `pipeline.py`
  imports cleanly without them (see Load-bearing discovery above), and using
  them for anything beyond the internal `stage_derive` call would exceed this
  brief's scope.
- Must NOT modify `docs/adr/0003-single-spatial-primary-key.md` — this brief
  implements what it already decided; Success Condition 9 is compliance, not
  re-litigation.
- Must NOT modify anything under `sim/`.
- **[NARROWED for `steps/03_cells.py` / Lot 007a only — see "Amendment
  007a-R" below]** Must NOT change any logic in `steps/03_cells.py` or
  `steps/04_adjacency.py`
  beyond the single named path adjustment in each (Success Conditions 4-5) —
  any other diff line, marked or not, must be reverted. For
  `steps/04_adjacency.py` (Lot 007b) this Non-Goal is **unchanged, in full
  force**.
- Must NOT leave an unmarked diff between a "byte-identical" file (the 8
  files marked "yes" across Success Condition 3's table) and its
  VictoriaProject original.
- Must NOT let `artifacts/adjacency_g4.json` (or any artifact other than
  `artifacts/adjacency_divergence_g4.json`) carry a `province`-keyed field —
  Success Condition 9 is a hard boundary, not a style preference.
- Must NOT claim a proof script "ran" without an actual logged execution in
  this repository, this session (hard-won rule: presence is not function).
- Must NOT invoke the pip/venv waiver to justify skipping the file copies or
  path adjustments themselves — only the "prove it runs" conditions
  (7, 8, and 10's evidence) are excused by that waiver, and only with its
  exact command/error produced.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| byte_identical_new_files_count | SHA256 comparison of the 8 files marked "yes" in Success Condition 3's table (pipeline.py, run_proof_g3.py, test_qa_red_g3.py, run_proof_g4.py, test_qa_red_g4.py, cities.json, city_coordinates.json, sea_zones.json) vs their VictoriaProject originals | 8 (must equal 8) |
| legacy_data_sha_target_match_count | SHA256 of the three newly-copied legacy files vs the exact target hashes cited in Success Condition 2, taken from VictoriaProject's own `artifacts/MANIFEST_g3.json`/`MANIFEST_g4.json` `inputs` block | 3 (must equal 3) |
| existing_legacy_data_unchanged_count | SHA256 of `legacy_game_data/province_adjacency.json` and `province_coordinates.json` (from brief 002) vs their VictoriaProject originals, re-verified this session | 2 (must equal 2 — no drift since 002) |
| path_adjustment_marker_count | occurrences of `# FORGEHISTORY-PATH-ADJUSTMENT` across `steps/03_cells.py` and `steps/04_adjacency.py` | >= 1 in each file (>= 2 total) |
| path_adjustment_unmarked_diff_line_count | **[AMENDED for `steps/03_cells.py` / 007a — see "Amendment 007a-R" below, which renames this counter's 007a instance to `g3_unmarked_nonrepair_diff_line_count`. For `steps/04_adjacency.py` / 007b, unchanged.]** line diff of each adjusted file vs its `deliverables/pre-port/*.orig` snapshot, counting differing lines not ending in the marker | 0 |
| game_unity_reference_remaining_count | grep `game_unity`/`StreamingAssets` across `pipeline/geo/`, same per-hit traceability rule as brief 002's amended Success Condition 3 (exception scope unchanged: `data/divergences_1400.json` + its verbatim copies/quotations + `constants.py`'s untouched `FORBIDDEN_GAME_PATH_MARKERS`, **plus, per Amendment 007a-R, the three named pre-existing `03_cells.py` literals**) | 0 after exclusions |
| g3_determinism_sha_pairs_matched_count | `logs/v1_049_qa.json`'s `determinism.sha256` dict — matched/total | total key count (must equal numerator; total > 0) |
| g3_qa_checks_passed_count / g3_qa_checks_red_proof_count | `logs/v1_049_qa.json`'s `checks` array | total entries (all green, all red-proven) — **per Amendment 007a-R, no longer excusable as brief-premise carry-forward for Lot 007a** |
| g3_cell_count_in_range | `artifacts/stats_g3.json`'s `cell_count` vs `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` in `pipeline/geo/constants.py` | must be within [150, 400] — **per Amendment 007a-R, must be true, not carry-forward false. Per Amendment 007a-R2, the range itself is now [150, 600] — see that section.** |
| g4_determinism_sha_pairs_matched_count | `logs/v1_050_qa.json`'s `determinism.sha256` dict | total key count (must equal numerator; total > 0) |
| g4_qa_checks_passed_count / g4_qa_checks_red_proof_count | `logs/v1_050_qa.json`'s `checks` array | total entries (all green, all red-proven) |
| g4_sea_zone_count_in_range | `artifacts/stats_g4.json`'s `sea_zone_count` vs `[SEA_ZONE_COUNT_MIN, SEA_ZONE_COUNT_MAX]` in `constants.py` | must be within [20, 40] |
| g4_adjacency_by_kind_nonzero_count | `artifacts/stats_g4.json`'s `by_kind` dict — count of the 4 kinds with value > 0 | 4 (must equal 4 — every kind actually occurs) |
| g4_open_sea_reachability_with_links | `artifacts/topology_links_g4.json`'s `reachability.all_enclosed_reachable` | must be `true` |
| g4_open_sea_reachability_without_links_fails | `logs/v1_050_qa.json`'s `G4-B` check `red_proof` field (natural links-off case) | must be non-empty (proves the topology link is load-bearing, not decorative) |
| adjacency_g4_province_field_count | grep the substring `province` inside `artifacts/adjacency_g4.json` only (not `adjacency_divergence_g4.json`) | 0 (must equal 0) |
| evidence_files_git_tracked_count | `git ls-files` intersected with the declared list of new G3/G4 evidence files (both `logs/v1_04{9,50}_*`, both `artifacts/*g{3,4}*.json`, `registry/cell_registry.json`, `registry/sea_zone_registry.json`, `registry/g6_density_refinement.json`) | must equal the full declared count — none silently gitignored |
| proof_script_exit_code_zero_count | exit codes of `run_proof_g3.py` and `run_proof_g4.py` | 2 (must equal 2) |

**See "Amendment 007a-R" below for the additional counters
(`g3_repair_marker_count`, `g3_check_definitions_byte_identical`,
`g3_bound_constants_unchanged`) required for Lot 007a post-amendment,
"Amendment 007a-R2" for that same counter's redefinition plus the new
`g3_seed_count_max_matches_derivation` counter, and "Amendment 007a-R3" for
the further redefinition of `g3_bound_constants_unchanged` plus the new
`g3_area_ceil_matches_derivation` counter.**

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "`pip install -r requirements.txt` / the existing `.venv` fails in this environment" | `.venv/Scripts/pip.exe install -r requirements.txt` (or `pip show shapely` if venv already exists from brief 002) | actual failure output pasted in full; if invoked, Success Conditions 7, 8, and 10's evidence are excused for **this brief's verdict only** and must be re-raised for the next Planificateur pass — file copies and path adjustments (1-6) are never excused |
| "a legacy game-data file's current VictoriaProject SHA256 no longer matches the target cited in Success Condition 2" | `Get-FileHash 'C:\Users\liagr\VictoriaProject\game_unity\Assets\StreamingAssets\data\<file>.json' -Algorithm SHA256` | actual hash output showing a real mismatch against the cited target — if invoked, re-derive the target from the current file (do not silently keep the stale cited value) and record both hashes |
| "the new evidence files cannot be `git add -f`'d (repo hook rejects, or brief 002's own evidence was never actually tracked)" | `git add -f pipeline/geo/logs/v1_049_qa.json && git status --porcelain pipeline/geo/logs/v1_049_qa.json` | actual command output showing rejection or continued untracked status — if invoked, Success Condition 10 is satisfied instead by copying the evidence files into `deliverables/` (declared, committed copies), and this must be recorded as a real repository constraint, not silently worked around |

**Note (Amendment 007a-R)**: a claim of the form "no seeding-parameter-only
change can satisfy all eight G3 checks without touching a frozen bound" is
**not** covered by any row above and has no pre-agreed command+error form —
see the amendment's Amended SC7 for how that specific claim must be
escalated instead (evidence of the attempted parameter sweep, back to the
Planificateur — never self-granted as a pass). **Amendment 007a-R2 was the
first resolution of exactly that escalation, for the `G3-E/F/G` pigeonhole
finding raised during the Amendment 007a-R repair run; Amendment 007a-R3 is
the resolution of the *second*, stronger per-land-part escalation raised
against Amendment 007a-R2's own `G3_SEED_COUNT_MAX = 600` bound — see that
amendment's section at the end of this file.**

## Execution Contract

- This is a pure-Python port — no Unity involvement; `unity/run-unity.ps1`
  does not apply to this brief.
- `run_proof_g3.py` and `run_proof_g4.py` each execute the full Voronoi/Poisson
  mesh construction two to three times against real coastline geometry — this
  may take from seconds to a few minutes depending on machine. Run each as a
  single blocking Bash call; only fall back to `run_in_background` if a
  single call would otherwise time out, and then wait for the one completion
  notification — never poll the resulting log or qa.json file repeatedly
  across separate tool calls.
- Every file named in `deliverables/manifest.json` must be under version
  control. Because `pipeline/geo/.gitignore` excludes `artifacts/`, `logs/`,
  `capture/`, and `build/` wholesale (see Success Condition 10), a file
  merely existing on disk in one of those directories is not sufficient —
  confirm it is tracked, or declare a committed copy under `deliverables/`
  instead.

## Budget d'exécution

Estimated **combined** cost if attempted as one monolithic brief: **170-200+
tool calls** — two ~1,400-1,600-line step scripts (vs. brief 002's two
smaller G2 scripts), one newly-discovered shared file (`pipeline.py`), three
new legacy-data copies with target-hash verification, two full
determinism-proof executions, and the `.gitignore`-vs-tracked-evidence
resolution (Success Condition 10) all compound the way brief 002's own
2-iteration amendment cycle already showed this class of brief tends to. This
exceeds the 150-call mechanical threshold. **Split into two lots.** Each lot
is its own `/forge-run`, its own fresh session, resuming only from the
previous lot's merged files and this brief text — never from a prior
transcript.

Before touching any file, run, per lot:
```
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 95   # lot 007a (superseded — see Amendment 007a-R for the post-repair estimate)
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 85   # lot 007b
```

## Lots atomiques (ordre d'exécution — chaque lot = un `/forge-run` séparé)

### Lot 007a — G3 cells (≤100 appels estimés) — **[re-scoped, see Amendment 007a-R below for the repair-scope estimate, Amendment 007a-R2 for the small follow-on recalibration estimate, and Amendment 007a-R3 for the further small area-ceiling recalibration estimate]**

| Champ | Valeur |
|---|---|
| Objectif | `pipeline.py` ported; `steps/03_cells.py` ported with marked path adjustment; `cities.json`/`city_coordinates.json` copied into `legacy_game_data/`; G3 determinism + QA proof green; evidence tracked in git |
| Dépendances | Brief 002 merged (`pipeline/geo/artifacts/coastline_1400.json`, shared infra, `legacy_game_data/province_*.json` already present) |
| Fichiers | `pipeline/geo/pipeline.py`, `pipeline/geo/steps/03_cells.py`, `pipeline/geo/tests/run_proof_g3.py`, `pipeline/geo/tests/test_qa_red_g3.py`, `pipeline/geo/legacy_game_data/cities.json`, `pipeline/geo/legacy_game_data/city_coordinates.json`, `pipeline/geo/README.md` (G3-landed edit), `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/**` |
| Critères | Success Conditions 1, 2 (cities/city_coordinates half), 3 (007a rows), 4 (amended), 6 (amended), 7 (amended, range further amended by 007a-R2, area-ceiling amended by 007a-R3), 10 (G3 evidence), 11 (first edit), 12 (03_cells.py.orig + first README snapshot), 13, 14 |
| Commande de validation | `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py` — exit 0 |
| Définition de terminé | Gate ACCEPT 007a; `artifacts/cells_g3.json` + `artifacts/adjacency_g3.json` + `artifacts/MANIFEST_g3.json` exist, are git-tracked, and are the input `04_adjacency.py` will read in 007b; **per Amendment 007a-R, the mesh is genuinely non-degenerate — all of G3-A..G3-H green with non-empty red_proof, not a carry-forward FAIL; per Amendment 007a-R2, this was checked against `cell_count` in `[150, 600]`; per Amendment 007a-R3, `G3-E` is now checked against the re-derived `G3_AREA_CEIL_KM2 = 40,000` km², and this is the version of Amended SC7 that must actually reach 14/14** |

### Lot 007b — G4 adjacency (≤90 appels estimés)

| Champ | Valeur |
|---|---|
| Objectif | `steps/04_adjacency.py` ported with marked path adjustment; `sea_zones.json` copied; `province_adjacency.json`/`province_coordinates.json` re-verified unchanged; G4 determinism + QA proof green including the Zuiderzee topology-link red-case; ADR-0003 boundary enforced in the exported artifact; evidence tracked in git |
| Dépendances | 007a merged (`artifacts/cells_g3.json` exists) — **per Amendment 007a-R, this must be the repaired, all-green `cells_g3.json`, not the degenerate 401-cell one; per Amendment 007a-R2, "all-green" allowed a `cell_count` anywhere in `[150, 600]`; per Amendment 007a-R3, "all-green" now also requires the mesh to be built against `G3_AREA_CEIL_KM2 = 40,000`, not the original 15,000** |
| Fichiers | `pipeline/geo/steps/04_adjacency.py`, `pipeline/geo/tests/run_proof_g4.py`, `pipeline/geo/tests/test_qa_red_g4.py`, `pipeline/geo/legacy_game_data/sea_zones.json`, `pipeline/geo/README.md` (G4-landed edit), `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/**` |
| Critères | Success Conditions 2 (sea_zones + province re-verify), 3 (007b rows), 5, 6 (04_adjacency.py scope), 8, 9, 10 (G4 evidence), 11 (second edit), 12 (04_adjacency.py.orig + second README snapshot), 13, 14 |
| Commande de validation | `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g4.py` — exit 0 |
| Définition de terminé | Gate ACCEPT 007b; `artifacts/adjacency_g4.json`, `artifacts/sea_zones_g4.json`, `artifacts/adjacency_divergence_g4.json`, `artifacts/topology_links_g4.json`, `artifacts/MANIFEST_g4.json` exist, are git-tracked, and `pipeline/geo/README.md` truthfully states G3+G4 landed, rivers onward not yet landed |

---

## Amendment 007a-R (2026-08-06T12:10:00): port → repair (owner Option A)

**Authored**: 2026-08-06T12:10:00
**Author**: forge-planificateur

**Supersedes** (by explicit reference, Lot 007a only): Success Conditions
**4, 6, 7** above (full replacement text below); the Non-Goals bullet
forbidding any `steps/03_cells.py` logic change beyond the path adjustment
(narrowed to 007a only — the identical bullet for `steps/04_adjacency.py` /
Lot 007b stays in full force); the Required Counters rows
`path_adjustment_unmarked_diff_line_count` (007a instance renamed/redefined
below; the 007b/`04_adjacency.py` instance is unchanged),
`g3_qa_checks_passed_count`/`g3_qa_checks_red_proof_count`, and
`g3_cell_count_in_range` (same cited sources/denominators, but "brief-premise
carry-forward FAIL" is no longer an admissible outcome for Lot 007a).
**Lot 007b is unchanged** by this amendment — it still ports
`steps/04_adjacency.py` byte-identical except its one marked path
adjustment, and now simply reads whichever repaired `artifacts/cells_g3.json`
this amended Lot 007a produces.

### Why (causal chain, world-terms — not a restatement of verdict-007a.md)

The grain this brief exists to produce (see the World-Terms Requirement
above: "a finite set of cells the rest of the engine can address, own, tax,
and move between") is only useful to `sim/`'s later migration/army/trade
logic if the cells it addresses actually cover the land and are shaped like
places a garrison could hold or a caravan could cross — not slivers, not one
cell the size of Iberia. A byte-identical port of `03_cells.py`, fed the
coastline brief 002 already ported (byte-identical to VictoriaProject,
itself refined after VictoriaProject's own G3 mesh was last proven green),
deterministically produces exactly that failure: one cell so large it
absorbs most of a land mass (max/median area ratio ~728, the G3-F ceiling is
8), a land mass no cell actually covers (G3-B), cells below the compactness
floor (G3-G). VictoriaProject's own committed "all-green" proof of G3 is
provably stale against its own current code (see verdict-007a.md's mtime
archaeology: the green log is dated before the coastline refinement that
broke it) — so "port a green source" was never actually available as an
option; the source has not been green since before brief 002 attached this
coastline. The owner's decision (Option A, run-report-007a.md's decision
table) is: repair the seeding/construction logic in ForgeHistory's own copy,
so that the shape `sim/` will later reason about — where a border actually
runs, how far a day's march covers — is a genuine, provable mesh rather than
a faithfully-reproduced defect. VictoriaProject stays read-only; nothing in
this amendment touches it.

### What changes

**The quality bar does not move.** `pipeline/geo/tests/run_proof_g3.py`,
`pipeline/geo/tests/test_qa_red_g3.py`, and `pipeline/geo/qa/checks.py`
(every `g3*`/`q*` function — `G3-A` through `G3-H`, `q2_no_holes_eps`,
`q3_no_overlaps_eps`) stay byte-identical to VictoriaProject: same
functions, same thresholds, same red-cases. `constants.py`'s eight G3
**acceptance-bound** constants stay unchanged in value:
`G3_SEED_COUNT_MIN=150`, `G3_SEED_COUNT_MAX=400`, `G3_AREA_FLOOR_KM2=200.0`,
`G3_AREA_CEIL_KM2=15000.0`, `G3_AREA_MAX_MEDIAN_RATIO=8.0`,
`G3_COMPACTNESS_MIN=0.18`, `G3_AREA_EPS_M2=10000.0`,
`G3_OVERLAP_EPS_M2=10000.0`. Editing any of these eight values, or any line
of the three check-definition files, to make a FAIL disappear is a
**disqualifying failure** (see below) — never a workaround, never a tuning
choice.

**[Amendment 007a-R2 note, inserted for continuity]**: `G3_SEED_COUNT_MAX`'s
value above (400) is superseded to 600 by Amendment 007a-R2, below, once the
repair authorized by this amendment proved 400 mathematically incompatible
with the current land area under `Q2`'s near-total-coverage requirement. The
other seven constants in this list remain exactly as stated here.

**[Amendment 007a-R3 note, inserted for continuity]**: `G3_AREA_CEIL_KM2`'s
value above (15,000.0) is, in turn, superseded by Amendment 007a-R3, below,
once Amendment 007a-R2's own `G3_SEED_COUNT_MAX = 600` bound was itself
shown mathematically insufficient at the frozen 15,000 km² ceiling. The
other six constants in this list (excluding `G3_SEED_COUNT_MAX`, already
superseded, and now `G3_AREA_CEIL_KM2`) remain exactly as stated here.

**What MAY change**: the seeding/mesh-construction logic inside
`pipeline/geo/steps/03_cells.py` — seed placement, the Poisson-disk radius
derivation, coverage/clipping handling for the giant unbounded edge cell,
boundary handling for any currently-uncovered land mass — provided the
result stays deterministic (the same two-run SHA determinism proof required
of every port in this brief family) and every changed line is marked. Two
distinct markers now apply to this one file:

- `# FORGEHISTORY-PATH-ADJUSTMENT` — the original Success Condition 4 path
  resolution change only (unchanged: exactly the `CITIES_JSON`/
  `CITY_COORDS_JSON` reassignment).
- **`# FORGEHISTORY-G3-REPAIR`** — every line touched to repair the mesh.

**Mechanical test for "seeding parameter" (allowed) vs "QA threshold /
acceptance bound" (frozen)**: is the constant read inside `qa/checks.py` by
any `g3*`/`q*` function? If yes, it is a bound, frozen by this amendment. If
no, it is a seeding parameter and may change — marked
`# FORGEHISTORY-G3-REPAIR` in `constants.py` too, with an explicit
before/after value pair in the generator-log tied to the named failing
check(s) it addresses (e.g. "`G3_R_CEIL_M` 95000→X, addresses G3-E/G3-F: caps
the giant-cell radius"). Verified against the current `qa/checks.py`: none of
`G3_MASTER_SEED`, `G3_DENSITY_RADIUS_M`, `G3_BASE_DENSITY`, `G3_R_FLOOR_M`,
`G3_R_CEIL_M`, `G3_LLOYD_ITERATIONS` are read by any check function — these
are the candidate seeding parameters, not an exhaustive or exclusive list;
apply the mechanical test to any other constant the repair touches.

### Amended Success Condition 4 (Lot 007a scope only)

`steps/03_cells.py` carries exactly one `# FORGEHISTORY-PATH-ADJUSTMENT`
change (unchanged from the original Success Condition 4) plus zero or more
`# FORGEHISTORY-G3-REPAIR`-marked changes to seeding/mesh-construction logic
(and, if used, matching marked changes to seeding-parameter constants in
`constants.py`, per the mechanical test above). Every line differing from
`deliverables/pre-port/03_cells.py.orig` must end in one of the two markers
— an unmarked diff line, of either kind, is still a hard FAIL
(`g3_unmarked_nonrepair_diff_line_count` must be 0). The generator-log must
list every `# FORGEHISTORY-G3-REPAIR` hunk with the specific check ID(s) it
targets and why.

### Amended Success Condition 6 (Lot 007a scope only — resolves the SC6
ambiguity flagged in verdict-007a.md as a documented carry-forward,
decided now, not contingent on the repair)

The three pre-existing `game_unity`/`StreamingAssets` literal hits at
`03_cells.py:108`, `:109` (the `RADIUS_FIELD` sources docstring/metadata) and
`:179` (a docstring) — present verbatim in VictoriaProject's original file,
independent of any port or repair — are added permanently to the
traceable-exception scope, alongside `constants.py`'s existing
`FORBIDDEN_GAME_PATH_MARKERS` carve-out (same treatment brief 002 already
established for that class of hit). This decision is unconditional: it does
not depend on whether the repair happens to touch those three lines. If a
repair change coincidentally rewrites one of them (e.g. updating the
docstring to describe new seeding logic), the resulting line must not
introduce a *new* `game_unity`/`StreamingAssets` literal — the exception
covers exactly these three named pre-existing hits, not a growing allowance.
After this amendment, `game_unity_reference_remaining_count` for the
`03_cells.py`/`constants.py` scope is **0** (three named `03_cells.py`
exceptions + two named `constants.py` exceptions, all pre-existing and
excluded; any other `.py` hit still counts).

### Amended Success Condition 7 (Lot 007a scope only — the repair's actual
quality bar; full replacement)

- `run_proof_g3.py` exits 0, this session, this repository, by command.
- **Determinism preserved**: every `logs/v1_049_qa.json`
  `determinism.sha256` pair is equal and non-empty across the script's own
  two-run comparison (`no_empty_sample_pass`). A repair that makes the mesh
  non-deterministic (unordered-set iteration, a wall-clock-seeded RNG, etc.)
  is a FAIL — not waivable, not excusable by any other passing check.
- **Every check green and red-proven, not weakened**: every entry in
  `checks` (`G3-A` through `G3-H` at minimum, plus `q2`/`q3`) has
  `passed: true` **and** a non-empty `red_proof`. The red-proof must still
  genuinely fire — `test_qa_red_g3.py` (byte-identical, untouched) passes its
  own red-case assertions. A repair that happens to make a check
  un-triggerable by construction (e.g. no cell can ever be small enough to
  exercise the floor) is a FAIL on that check's red-proof requirement, not a
  silent pass.
- `artifacts/stats_g3.json`'s `cell_count` within
  `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX] = [150, 400]`. **[Amendment
  007a-R2: this range is superseded to `[150, 600]` — see that section.]**
- Every land mass covered (`G3-B` green; the script's own uncovered-mass
  `RuntimeError` did not fire).
- Giant-cell (`G3-E`), max/median area ratio (`G3-F`), and compactness floor
  (`G3-G`) all green — provable from `stats_g3.json`'s own per-cell area and
  compactness fields, not merely inferred from a clean exit code.
- **"Brief-premise defect, unsatisfiable within scope" is no longer an
  admissible outcome for Lot 007a.** The whole purpose of this amendment is
  that the scope now includes fixing it; a FAIL here is an ordinary
  Générateur/repair-design defect, handled by ordinary iteration
  (feedback → retry) — **except** for one specific claim: that no
  seeding-parameter-only change (per the mechanical test above) can satisfy
  all eight checks simultaneously without touching a frozen bound. That
  claim has no pre-agreed command+error waiver (an exhaustive-search
  negative cannot be reduced to one failing command per hard-won rule 9) and
  must instead be escalated back to the Planificateur with the concrete
  parameter sweep attempted and each attempt's check results — never
  self-granted as a pass, never silently worked around. **This is exactly
  the claim the repair run raised for G3-E/F/G, and Amendment 007a-R2 below
  is its resolution — and Amendment 007a-R3, further below, is the
  resolution of the second such escalation raised against 007a-R2's own
  bound.**

### Non-Goals amended (Lot 007a scope only, except where noted)

- The original bullet "Must NOT change any logic in `steps/03_cells.py` ...
  beyond the single named path adjustment" is **narrowed for Lot 007a only**:
  `steps/03_cells.py` may now also carry `# FORGEHISTORY-G3-REPAIR`-marked
  seeding/construction changes (and matching marked seeding-parameter
  constant changes, per the mechanical test above). The identical bullet for
  `steps/04_adjacency.py` (Lot 007b) is **unchanged, full force** — 007b
  remains a byte-identical-except-path-adjustment port.
- **New, hard Non-Goal**: must NOT edit `pipeline/geo/tests/run_proof_g3.py`,
  `pipeline/geo/tests/test_qa_red_g3.py`, or `pipeline/geo/qa/checks.py` in
  any way. Must NOT change the value of any of the eight named G3
  acceptance-bound constants in `constants.py`. Either is a disqualifying
  failure regardless of how green the resulting proof looks. **[Amendment
  007a-R2 narrows this to seven of the eight — `G3_SEED_COUNT_MAX` is now
  the one re-derivable bound. Amendment 007a-R3 narrows it further to six of
  the (original) eight — `G3_AREA_CEIL_KM2` is now also re-derivable — see
  that section.]**
- **New Non-Goal**: must NOT weaken a check's red-case by construction (e.g.
  repairing the mesh so no cell can ever be tested against a floor/ceiling it
  used to be tested against) — Amended Success Condition 7's red-proof
  requirement exists precisely to catch this.
- **New Non-Goal**: must NOT introduce nondeterminism to reach a green result
  (RNG reseeded from wall clock, dict/set iteration-order dependency, etc.) —
  the determinism clause in Amended Success Condition 7 is absolute, not
  subject to iteration-based relaxation.

### Disqualifying Failures (new — Lot 007a only; any one of these is
REJECT regardless of whether `run_proof_g3.py` exits 0)

1. Any byte difference in `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` vs VictoriaProject's originals.
2. Any of the eight named G3 acceptance-bound constants changed in value.
   **[Amendment 007a-R2: superseded — see that section's amended list, which
   frees `G3_SEED_COUNT_MAX` to move to exactly 600, while freezing the
   remaining seven. Amendment 007a-R3: superseded again — see that section's
   amended list, which additionally frees `G3_AREA_CEIL_KM2` to move to
   exactly 40,000, while freezing the remaining six.]**
3. Any `checks` entry with `passed: true` and an empty `red_proof`.
4. Any diff line in `steps/03_cells.py` vs `.orig` not ending in
   `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR`.
5. `determinism.sha256` containing any unequal or empty pair.

### Required Counters — new / changed for Lot 007a

(the cited sources and denominators of `g3_qa_checks_passed_count`/
`g3_qa_checks_red_proof_count`/`g3_cell_count_in_range` are unchanged from
the main Required Counters table above; only their admissible-outcome
changes — carry-forward FAIL is no longer acceptable for Lot 007a)

| name | sample source | denominator |
|---|---|---|
| g3_repair_marker_count | occurrences of `# FORGEHISTORY-G3-REPAIR` across `steps/03_cells.py` and (if used) `constants.py` | >= 1 if any construction-logic or seeding-parameter change was made; if the Générateur claims zero changes were needed, that claim itself must be justified in the generator-log against verdict-007a.md's established `cell_count=401` finding — a silent zero is suspicious, not a pass |
| g3_unmarked_nonrepair_diff_line_count | diff of `steps/03_cells.py` vs `deliverables/pre-port/03_cells.py.orig` (reused from the original 007a run, not re-created), counting lines that differ and do NOT end in `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR` | 0 (must equal 0) — supersedes the main table's `path_adjustment_unmarked_diff_line_count` for this file only |
| g3_check_definitions_byte_identical | SHA256 of `run_proof_g3.py`, `test_qa_red_g3.py`, `qa/checks.py`, each individually compared vs their VictoriaProject originals | 3 (must equal 3 — all three unchanged) |
| g3_bound_constants_unchanged | value-comparison, one by one, of `G3_SEED_COUNT_MIN`, `G3_SEED_COUNT_MAX`, `G3_AREA_FLOOR_KM2`, `G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2` in this repo's `constants.py` vs VictoriaProject's `constants.py` | 8 (must equal 8 — all eight unchanged in value). **[Amendment 007a-R2: redefined to a 7-constant / denominator-7 counter, with `G3_SEED_COUNT_MAX` moved to a separate `g3_seed_count_max_matches_derivation` counter. Amendment 007a-R3: redefined again to a 6-constant / denominator-6 counter, with `G3_AREA_CEIL_KM2` moved to a separate `g3_area_ceil_matches_derivation` counter — see that section.]** |
| g3_determinism_sha_pairs_matched_count | (unchanged from main table) `logs/v1_049_qa.json`'s `determinism.sha256` dict | total key count (must equal numerator; total > 0) |
| g3_cell_count_in_range | (unchanged source, amended admissible outcome) `artifacts/stats_g3.json`'s `cell_count` vs `[150, 400]` | must be **true** — no carry-forward FAIL. **[Amendment 007a-R2: range widened to `[150, 600]`. Amendment 007a-R3: the range itself is unchanged by 007a-R3 (still `[150, 600]`) — only `G3_AREA_CEIL_KM2` moves this time, not `G3_SEED_COUNT_MAX` — see that section.]** |

### Pre-port evidence — reused, not re-created

The `deliverables/pre-port/03_cells.py.orig` snapshot already delivered and
SHA256-verified against VictoriaProject's pristine file in the FAILed 007a
run satisfies Success Condition 12 for this amendment too — it is exactly
the baseline the repair diffs against. No new `.orig` snapshot is required
unless the working tree was reset since that run, in which case it must be
re-taken and re-verified identically.

### Execution note

This is a genuine design task inside a bounded, checkable space — not a
mechanical port. Expect several iterations of "change a seeding parameter or
a bounded piece of placement/clipping logic → re-run `run_proof_g3.py` → read
`stats_g3.json` / `logs/v1_049_qa.json` → adjust" inside one session. Each
`run_proof_g3.py` execution remains a single blocking Bash call per the
brief's existing Execution Contract — no polling, no repeated re-reads of the
same log across separate tool calls.

### Estimated tool calls for this amendment's scope

**135** (Planificateur estimate; see accompanying report for the reasoning
and for why `py harness/budget.py split-check` could not be executed by this
Planificateur session — no shell tool was available here). The Générateur
must still run `py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 135`
(or its own re-estimate, if materially different, per the original Success
Condition 13) before touching any file.

---

## Amendment 007a-R2 (2026-08-06T12:55:00): recalibrate G3_SEED_COUNT_MAX to the current coastline (owner Option a)

**Authored**: 2026-08-06T12:55:00
**Author**: forge-planificateur

**Timestamp note** (per CLAUDE.md's own recorded lesson, not to repeat the
brief's first future-dating mistake): this amendment must postdate Amendment
007a-R (`12:10:00`) and the repair run it authorized, since it resolves that
run's own escalated finding (`deliverables/generator-log.md`, "Lot 007a-R
(repair)" §R4; `deliverables/007a-repair-validation.log`). `12:55:00` is
chosen as a same-day, non-future timestamp consistent with that ordering —
not an invented future value.

**Supersedes** (Lot 007a only, narrowly — nothing else in Amendment 007a-R
changes): the *value* of `G3_SEED_COUNT_MAX` in `pipeline/geo/constants.py`
(400 → **600**), the `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` range cited
throughout Success Condition 7 / Amended SC7 / the Required Counters table
(now `[150, 600]`, not `[150, 400]`), Amendment 007a-R's
`g3_bound_constants_unchanged` counter (redefined below to a 7-constant,
denominator-7 counter), and Amendment 007a-R's Disqualifying Failure item 2
(redefined below). The two-marker scheme
(`# FORGEHISTORY-PATH-ADJUSTMENT` / `# FORGEHISTORY-G3-REPAIR`), the
mechanical seeding-parameter-vs-bound test, the determinism requirement, the
red-proof requirement, and the escalation-not-self-grant discipline all stay
exactly as Amendment 007a-R defined them. **Lot 007b is untouched.**

**[Amendment 007a-R3 note, inserted for continuity — read the section at the
very end of this file before treating anything below as final]**: this
amendment's own re-derived bound, `G3_SEED_COUNT_MAX = 600`, was
subsequently shown — by a materially stronger, per-land-part pigeonhole
proof, not a repeat of this amendment's flat-average one — to still be
mathematically insufficient at the frozen `G3_AREA_CEIL_KM2 = 15,000` km².
This amendment's arithmetic below is preserved verbatim as the historical
record of the *first* recalibration and remains individually correct as far
as it goes; it is simply not the *whole* story. Amendment 007a-R3 does not
change the number **600** — `G3_SEED_COUNT_MAX` stays exactly as this
amendment left it — it re-derives `G3_AREA_CEIL_KM2` instead, which is the
bound that turned out to still be the true bottleneck.

### Why (the established fact, read from the Générateur's own escalation)

Amendment 007a-R's repair (see `deliverables/generator-log.md` §R1-R4 and
`deliverables/007a-repair-validation.log`, both already in this brief's
`deliverables/`) fixed the two genuinely seeding-logic-fixable defects with
two marked, deterministic, zero-constants-touched changes:

- **`G3-B` (uncovered land mass)** — fixed by excluding sub-`area_eps`
  coastline-digitization slivers from the enumerated "masses requiring their
  own seed", without touching the land geometry itself or `Q2`'s coverage
  check.
- **`G3-D` (cell count out of range) and much of the giant-cell/ratio/
  compactness severity** — fixed by reordering the density-adaptive Bridson
  seed expansion ahead of unconditional urban-anchor placement, so the seed
  budget is spent proportionally to the declared density field across *all*
  land, not exhausted on a fixed anchor set first. `cell_count` moved
  **401→399** (into the then-current `[150,400]` range); the giant cell
  shrank **950,145→215,449 km²**; the max/median ratio improved
  **330→58**.

This is real, measured progress — not a workaround, and not touched again by
this amendment.

What remains (`G3-E`, `G3-F`, `G3-G`) is not a repair-quality defect that
more seeding-parameter tuning can reach. The Générateur proved, by a
closed-form pigeonhole argument independent of any seeding algorithm's
quality, that it is mathematically unsatisfiable under the frozen bounds:

- This repository's own pilot-window land geometry
  (`land_xy.area`, measured directly from the ported, byte-identical
  `steps/03_cells.py`'s own `load_corrected_land()`, cross-checked to 6
  decimal places against VictoriaProject's own currently-recorded
  `stats_g3.json` — confirming this is not a ForgeHistory-introduced
  geometry bug) = **6,667,146.530456 km²**.
- The frozen bounds required covering that land with at most
  `G3_SEED_COUNT_MAX = 400` cells, each at most `G3_AREA_CEIL_KM2 = 15,000`
  km² (both verified unchanged from VictoriaProject at the time this was
  measured, `g3_bound_constants_unchanged = 8/8`) — a hard capacity ceiling
  of `400 × 15,000 = 6,000,000 km²`.
- `Q2` (`q2_no_holes_eps`, frozen, currently green — `passed: true`) requires
  the cells to tile essentially all of the land (tolerance 10,000 m² against
  6.67 million km²) — so the sum of all cell areas is pinned at
  ≈6,667,146.53 km² by a check this amendment does not touch.
- `6,000,000 km² < 6,667,146.53 km²`: a **667,146.53 km² shortfall**, by
  pigeonhole, independent of seed placement, Poisson radius, or Lloyd
  iteration count. At least one cell — in practice several, given the
  shortfall's scale — must exceed the ceiling no matter how seeds are
  placed, as long as the seed-count ceiling and the area ceiling both stay
  at their frozen values against this land geometry.
- Empirical confirmation, not just the closed-form argument: the Générateur
  swept `G3_R_CEIL_M` (a genuine seeding parameter — confirmed, by the
  mechanical test, not read by any `qa/checks.py` check function) at three
  values (95,000 / 60,000 / 40,000 m); all three hit the frozen
  `G3_SEED_COUNT_MAX = 400` cap before relieving the shortage. No
  seeding-parameter-only tuning routes around a hard count ceiling.

This is exactly the scenario Amendment 007a-R's own escalation clause
reserved for the Planificateur ("no seeding-parameter-only change can
satisfy all eight G3 checks without touching a frozen bound... escalated
back to the Planificateur... never self-granted as a pass"). The bounds were
calibrated for a land area that fit `400 × 15,000` — implicitly, ≤6,000,000
km². The v1_064 coastline refinement (brief 002) genuinely widened the
mapped coastline; the land area grew past that capacity. The map-grain
*intent* — "no cell coarser than roughly a small province, ≤15,000 km² at
the coarsest" — did not change; the *count* needed to tile more land at that
same grain necessarily did. This is a calibration correction tied to a real,
dated, documented change in the input geometry (brief 002's coastline
refinement), not a relaxation of what "acceptable" means.

### The decision (owner Option a): re-derive `G3_SEED_COUNT_MAX`, keep every quality bar frozen

**Frozen, unchanged in value** (touching any of these seven remains a
disqualifying failure, same severity as Amendment 007a-R's original list):

| constant | value | why it stays frozen |
|---|---|---|
| `G3_AREA_CEIL_KM2` | 15,000.0 km² | the map-grain intent itself — "no cell coarser than this" — is what stays true; it is not what is inconsistent with the coastline |
| `G3_AREA_FLOOR_KM2` | 200.0 km² | the fine-grain floor near cities; unrelated to a total-land-area shortfall |
| `G3_AREA_MAX_MEDIAN_RATIO` | 8.0 | the G3-F ratio ceiling — a distribution-shape quality bar, not a capacity bar |
| `G3_COMPACTNESS_MIN` | 0.18 | the G3-G sliver-prevention floor — a shape quality bar |
| `G3_SEED_COUNT_MIN` | 150 | the low-end coarseness floor; the repaired mesh already sits at 399, nowhere near 150 — this recalibration is about the *upper* capacity limit only; stays as-is |
| `G3_AREA_EPS_M2` | 10,000.0 m² | coverage tolerance (feeds Q2/G3-B) — a numerical-precision constant, not a design bound |
| `G3_OVERLAP_EPS_M2` | 10,000.0 m² | overlap tolerance (feeds Q3) — same class |

**[Amendment 007a-R3 note]**: the first row of this table
(`G3_AREA_CEIL_KM2`, "stays frozen") is the row Amendment 007a-R3 below
un-freezes. Everything else in this table is unaffected and remains frozen
under 007a-R3 too.

**Re-derived — the one bound this amendment moves:**

```
mathematical_floor = ceil(land_area_km2 / G3_AREA_CEIL_KM2)
                    = ceil(6,667,146.530456 / 15,000.0)
                    = ceil(444.4764...)
                    = 445
```

445 is the absolute minimum — it assumes every cell sits exactly at the
15,000 km² ceiling with zero waste, which no real Voronoi/Poisson mesh under
a density-driven `r(x)` field achieves. The repair's own measured
distribution (`deliverables/007a-repair-validation.log`) is strongly
right-skewed: median 3,688.76 km² (only ~24.6% of the ceiling) against a
p90 of 44,099.33 km² (nearly 3× the ceiling) — most cells sit far below the
ceiling (urban-density-driven, near the floor), while a real tail of
low-density rural/coastal cells sits well above it. Enforcing a hard
per-cell ceiling on that shape requires subdividing the tail into
additional small-to-medium cells, costing materially more total seeds than
the idealized "every cell exactly at capacity" floor accounts for.
Separately, `mandatory_masses: 212` (one seed per land part, unconditional,
confirmed in the repair's own `v1_049_qa.json` output) is a structural seed
cost independent of area entirely — at the new floor of 445, that fixed
cost alone is ~47.6% of the budget, before any ceiling-driven subdivision
runs.

**Headroom factor: 1.35× (35%)**, chosen on that basis — a legible margin
for real-mesh packing inefficiency and the fixed per-mass seed cost, not an
arbitrary round-up:

```
445 * 1.35 = 600.75  →  600  (rounded DOWN to a clean number — conservative:
                              this trims a fraction of a cell from the
                              justified margin, it does not pad it further)
```

**`G3_SEED_COUNT_MAX = 600`** (was 400). `G3_SEED_COUNT_MIN` stays **150**,
unchanged.

This is a bound the repaired seeding logic must still *earn* — raising the
ceiling does not by itself make `G3-E`/`G3-F`/`G3-G` pass; it makes them
*reachable* by legitimately lowering `G3_R_CEIL_M` (or another confirmed
seeding parameter, per Amendment 007a-R's mechanical test) to subdivide the
remaining low-density mass further, without `G3-D` then failing on a count
over 400. The actual resulting `cell_count` remains a measured output of the
Poisson process, checked only against the range `[150, 600]` — per
eval-rubric.md's existing plateau note, "the mesh count is a measured
output... not a quota," which continues to hold unchanged under the new
range.

**[Amendment 007a-R3 note]**: this flat, whole-land-area derivation
(`445 × 1.35 = 600`) turned out to still be insufficient once the actual
576-cell (later 596-cell) mesh was measured against it — it implicitly
assumed land area could be freely repartitioned into 15,000 km² chunks
across land-mass boundaries, which `Q2`/`G3-B` do not allow (every one of
the 212 disjoint land parts needs its own seed, and an oversized part's
shortfall must be rounded up individually, per-part). Amendment 007a-R3's
own section below carries the corrected, per-part arithmetic. `600` is not
retracted as a number — it stays `G3_SEED_COUNT_MAX`'s frozen value — but
the conclusion "this is sufficient" is superseded.

### Guardrails (restated for this scope, consistent with Amendment 007a-R)

- Only the `G3_SEED_COUNT_MAX` line in `pipeline/geo/constants.py` may
  change, marked `# FORGEHISTORY-G3-REPAIR` (the existing repair marker — no
  new marker type is introduced). `G3_SEED_COUNT_MIN` and the six other
  constants listed above stay byte-value-identical to VictoriaProject.
- `pipeline/geo/tests/run_proof_g3.py`, `pipeline/geo/tests/test_qa_red_g3.py`,
  and `pipeline/geo/qa/checks.py` stay byte-identical to VictoriaProject —
  this amendment does not touch check *definitions*, only the one input
  bound one of them reads.
- Determinism still required (two-run SHA256, all pairs equal and
  non-empty). All of `G3-A` through `G3-H` green **and** red-proven — no
  check's red-case may be weakened or made untriggerable.
- The fix that reaches green must come from seeding/construction logic (per
  Amendment 007a-R's existing mechanical test — a further `G3_R_CEIL_M`
  adjustment, refinement of the already-repaired Bridson/mandatory-seed
  ordering, etc.), not from the raised ceiling alone doing the work: raising
  `G3_SEED_COUNT_MAX` to 600 removes the *pigeonhole impossibility*; it does
  not itself shrink any cell or improve any ratio/compactness value.
- Changing `G3_SEED_COUNT_MAX` to any value other than **600** is itself a
  disqualifying failure — this is a documented recalibration to a specific,
  derived, auditable number, not license to pick "whatever number makes the
  run green."

### Required Counters — new / redefined for this amendment (Lot 007a scope)

| name | sample source | denominator |
|---|---|---|
| `g3_bound_constants_unchanged` | **[REDEFINED]** value-comparison of the SEVEN now-frozen constants — `G3_SEED_COUNT_MIN`, `G3_AREA_FLOOR_KM2`, `G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2` — in this repo's `constants.py` vs VictoriaProject's `constants.py` | 7 (must equal 7 — all seven unchanged in value; supersedes Amendment 007a-R's "8" denominator for this counter). **[Amendment 007a-R3: redefined again to a 6-constant / denominator-6 counter — see that section.]** |
| `g3_seed_count_max_matches_derivation` | **[NEW]** this repo's `constants.py` `G3_SEED_COUNT_MAX` value, compared to the derived value cited in this section (600) — cited here, not re-derived by the Évaluateur | must equal 600 exactly. **[Unaffected by Amendment 007a-R3 — this counter and its target value are unchanged.]** |
| `g3_cell_count_in_range` | (unchanged sample source) `artifacts/stats_g3.json`'s `cell_count` vs `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` in `constants.py` | must be within **[150, 600]** (range widened from Amendment 007a-R's `[150, 400]`) — must be true, no carry-forward FAIL, per Amendment 007a-R's existing admissible-outcome rule. **[Unaffected by Amendment 007a-R3 — the range stays `[150, 600]`.]** |
| `g3_repair_marker_count` | (unchanged from Amendment 007a-R) occurrences of `# FORGEHISTORY-G3-REPAIR` across `steps/03_cells.py` and `constants.py` | >= 1 in `steps/03_cells.py` (already true, 21, from the prior repair) and now >= 1 in `constants.py` (the new `G3_SEED_COUNT_MAX` line) |

All other Amendment 007a-R counters (`g3_check_definitions_byte_identical`,
`g3_unmarked_nonrepair_diff_line_count`, `g3_determinism_sha_pairs_matched_count`,
`g3_qa_checks_passed_count`/`g3_qa_checks_red_proof_count`) are unchanged in
source and denominator.

### Disqualifying Failures — amended (Lot 007a only; supersedes item 2 of Amendment 007a-R's list)

Any ONE of the following is an automatic REJECT for Lot 007a, regardless of
`run_proof_g3.py`'s exit code or any other green counter:

1. Any byte difference between `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` and their VictoriaProject originals (unchanged from
   Amendment 007a-R).
2. **[AMENDED]** Any of the seven now-frozen G3 constants (`G3_SEED_COUNT_MIN`,
   `G3_AREA_FLOOR_KM2`, `G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`,
   `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`) changed in
   value (`g3_bound_constants_unchanged` < 7), **OR** `G3_SEED_COUNT_MAX` set
   to any value other than **600** (`g3_seed_count_max_matches_derivation`
   false). Touching `G3_AREA_CEIL_KM2` (or any of the other six) to "solve"
   the pigeonhole shortfall instead of moving `G3_SEED_COUNT_MAX` is
   precisely the disqualifying move this row exists to catch. **[Amendment
   007a-R3: superseded — see that section's amended list, which frees
   `G3_AREA_CEIL_KM2` (and only `G3_AREA_CEIL_KM2`) to move to exactly
   40,000, while freezing the remaining six plus the now-settled
   `G3_SEED_COUNT_MAX = 600`.]**
3. Any `checks` entry with `passed: true` and an empty `red_proof`
   (unchanged).
4. Any diff line in `steps/03_cells.py` vs `.orig` not ending in
   `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR`
   (extended from Amendment 007a-R's item 4 to cover `constants.py`).
5. Any unequal or empty pair in `determinism.sha256` (unchanged).

### Execution note

This is a small, targeted change on top of the already-completed Amendment
007a-R repair session: one constant value, one marked line in
`constants.py`, then re-running the already-repaired seeding logic
(possibly with a further `G3_R_CEIL_M` or equivalent marked adjustment,
still under Amendment 007a-R's existing marked-repair discipline) to
actually earn `G3-E`/`G3-F`/`G3-G` green. Estimated additional tool calls
for this amendment's scope, on top of the completed repair session:
**20-30** (one `constants.py` edit, one-to-a-few `run_proof_g3.py` re-runs
reading `stats_g3.json`/`logs/v1_049_qa.json` between iterations, evidence
re-tracking, README/manifest updates). This stays well inside Lot 007a's
existing `135`-call budget; no new split-check is required beyond
re-confirming the existing `--estimated-calls 135` (or the Générateur's own
updated total, if now materially higher) before resuming.

**[Amendment 007a-R3 note]**: this execution note's plan did not fully play
out — the resulting run reached 12/14 (`G3-D` and `G3-F` newly green;
`G3-E`/`G3-G` still red), and the Générateur's own further, deeper
investigation (`deliverables/checkpoint-002.md`) found this amendment's
`600` target itself insufficient, not merely under-tuned. See Amendment
007a-R3 below for the resolution.

---

## Amendment 007a-R3 (2026-08-06T18:15:00): relax G3_AREA_CEIL_KM2 (owner Option 2)

**Authored**: 2026-08-06T18:15:00
**Author**: forge-planificateur

**Timestamp note**: this amendment must postdate Amendment 007a-R2
(`12:55:00`) and the Générateur session it authorized —
`deliverables/checkpoint-001.md` (`16:30:51`) and
`deliverables/checkpoint-002.md` (`16:55:32`), both real, on-disk, committed
session handoffs from that Générateur run, and `run-report-007a-R2.md`, the
orchestrator's own write-up escalating the finding those checkpoints
recorded. `18:15:00` is chosen as a same-day, non-future timestamp that
postdates all three of those artifacts — not a repeat of the brief's first
future-dating mistake, and not a chronologically-impossible ordering against
work this amendment explicitly responds to.

**Supersedes** (Lot 007a only, narrowly — nothing else in Amendments 007a-R
or 007a-R2 changes): the *value* of `G3_AREA_CEIL_KM2` in
`pipeline/geo/constants.py` (15,000.0 → **40,000.0** km²); Amendment
007a-R2's "Frozen, unchanged in value" table entry for `G3_AREA_CEIL_KM2`
(un-frozen — see the new frozen/movable split below); Amendment 007a-R2's
`g3_bound_constants_unchanged` counter (redefined below to a 6-constant,
denominator-6 counter, with `G3_AREA_CEIL_KM2` moved to its own new
`g3_area_ceil_matches_derivation` counter, mirroring exactly how
`G3_SEED_COUNT_MAX` was carved out of that same counter by Amendment
007a-R2 one level up); and Amendment 007a-R2's Disqualifying Failure item 2
(redefined below). Every other mechanism established so far — the two-marker
scheme (`# FORGEHISTORY-PATH-ADJUSTMENT` / `# FORGEHISTORY-G3-REPAIR`), the
mechanical seeding-parameter-vs-bound test, the determinism requirement, the
red-proof requirement, the escalation-not-self-grant discipline, and
`G3_SEED_COUNT_MAX = 600` itself (untouched, still frozen) — stays exactly
as the prior amendments defined it. **`G3_COMPACTNESS_MIN` (0.18) and
`G3_AREA_MAX_MEDIAN_RATIO` (8.0) — the SHAPE-quality bounds — are explicitly
NOT touched by this amendment; see Disqualifying Failures below.** **Lot
007b is untouched** — it still ports `steps/04_adjacency.py` byte-identical
except its one marked path adjustment, and now simply reads whichever cells
land within this amendment's re-derived bounds.

### Why (causal chain, world-terms — grounded in the Générateur's own re-checked finding, not re-derived from memory)

The grain this brief exists to produce is a set of addressable **places** —
something a garrison can hold, a caravan can cross in a bounded number of
legs, a province can aggregate from (World-Terms Requirement, above). A
place's *area* is not itself a fixed, universal quantity in real historical
geography: a Flemish county and a Saharan-fringe caïdat were never the same
size, because population, cultivable land, and administrative attention were
never uniformly distributed across the pilot window's Western-Europe-to-
Maghreb span. The mesh's own declared seed-density field `r(x)` already
encodes exactly this — cells are small where cities are dense (median cell
size in the current 596-cell mesh is 11,424 km², already far under the old
15,000 km² ceiling, almost entirely in Western/Central/Northern Europe where
urban density drives the seeding) and only grow large in low-density,
low-narrative-interest peripheral land (the Maghreb's Saharan-fringe masses:
one single land part alone measures 4,468,975.5 km², larger than any single
real-world national unit, entirely outside this game's declared area of
historical focus). The `G3_AREA_CEIL_KM2 = 15,000` km² ceiling was set
(VictoriaProject-inherited, unexamined against the current pilot window)
without accounting for how much of that peripheral, low-interest land the
brief-002-refined coastline actually includes.

The orchestrator's own re-check (`run-report-007a-R2.md`, cross-verified
against `deliverables/checkpoint-002.md`'s live measurement of
`artifacts/cells_g3.json`) established, as fact, not estimate:

- The QA check's own violation list **truncates its detail string at 8
  entries** (`qa/checks.py`'s `g3e_area_within_bounds` / `g3g_compactness_floor`,
  both `break` at `len(bad) >= 8`) — "8 cells over ceiling" was always a
  **display artifact** of the check's own detail-string cap, never the true
  violation count. The true count, read directly from
  `artifacts/cells_g3.json`'s own per-cell `area_km2` field at the current
  596-cell mesh: **238 of 596 cells (≈40%) exceed 15,000 km²** — not a small
  margin, a mesh-wide mismatch between the ceiling and the coastline's real
  area distribution.
- A **uniform** mesh that keeps every cell at or under 15,000 km² needs, by
  the Générateur's own rigorous per-land-part pigeonhole bound (each of the
  212 disjoint land parts must receive at least
  `ceil(part_area_km2 / 15,000)` cells, individually, because `Q2`/`G3-B`
  forbid sharing a fractional cell across a mass boundary), **at least 645
  cells at the theoretical zero-packing-waste best case**, realistically
  ≈837-900 with real Voronoi/Bridson packing overhead — roughly **double**
  the brief's own stated "~150-400 addressable cells" design grain.
  `G3_SEED_COUNT_MAX = 600` (Amendment 007a-R2) is 45+ cells short of even
  the theoretical floor, so it cannot reach a clean, uniform 15,000 km²
  mesh no matter how it is tuned.

Doubling the map's cell count (`run-report-007a-R2.md`'s Option 1) was
escalated to the owner rather than auto-applied, because it is a real
design decision about how finely the entire world is subdivided — it
changes compute cost, province-aggregation granularity, and the visual grain
of every later F1 system reading `cell_id`, not a quality-bar relaxation.
**The owner chose Option 2 instead**: keep the ~400-600-cell design grain
(closer to the brief's original intent, and to what the repaired seeding
logic already naturally produces for the densely-seeded core of the map),
and instead accept that a cell in a genuinely low-interest, low-density
periphery — a stretch of Saharan-fringe Maghreb the game's declared
1400-1900 European-history focus does not narratively require at fine grain
— may be as large as a real historical **country**, not merely a province.
This is not a violation of Principle 3 (the economy is physical, nothing
teleports) — travel time and transport routing are downstream, per-edge
properties of `04_adjacency.py`'s graph, entirely independent of a single
cell's raw area; a caravan crossing one large peripheral cell in several
real days is still following an unbroken, physically continuous path, the
same as it would across several smaller cells. What changes is only how
finely that specific, low-interest stretch of land is subdivided for
addressing purposes — exactly the "no cell coarser than roughly a province"
*intent* the ceiling was meant to express, recalibrated to a *country-scale*
grain specifically for the periphery, while Western/Central/Northern Europe
— where the density-driven seeding already produces a median cell an order
of magnitude smaller — is essentially unaffected.

### The decision (owner Option 2): relax `G3_AREA_CEIL_KM2`, keep every SHAPE-quality bar frozen

**Frozen, unchanged in value** (touching any of these six, or
`G3_SEED_COUNT_MAX`, remains a disqualifying failure, same severity as the
prior amendments' lists):

| constant | value | why it stays frozen |
|---|---|---|
| `G3_SEED_COUNT_MIN` | 150 | the low-end coarseness floor; unrelated to a per-cell area bound |
| `G3_SEED_COUNT_MAX` | 600 | **re-derived once already** (Amendment 007a-R2) against the current coastline's per-mass structure; that derivation's `600` number is not what turned out to be wrong — the `G3_AREA_CEIL_KM2` it was paired with was. Re-verified below (Guardrails) that 600 remains comfortably sufficient once the ceiling itself is raised — no reason to move it again |
| `G3_AREA_FLOOR_KM2` | 200.0 km² | the fine-grain floor near cities — a floor, not a ceiling; entirely unrelated to the periphery's oversized-cell problem |
| `G3_AREA_MAX_MEDIAN_RATIO` | 8.0 | **G3-F, a SHAPE-quality bound** — how lopsided the mesh's own internal distribution is allowed to be, relative to itself (a *relative* bound). Owner Option 2 is explicit: relax cell **size**, never cell **shape/quality**. Already GREEN at the measured 596-cell mesh (ratio 3.03, well under 8.0) — raising the absolute ceiling does not threaten this; if anything a coarser mesh (less forced subdivision) keeps this ratio low |
| `G3_COMPACTNESS_MIN` | 0.18 | **G3-G, a SHAPE-quality bound** — the anti-sliver floor. Owner Option 2 does not touch this. The Générateur must close the remaining `G3-G` violations (measured: 43 of 596 cells below 0.18, 16 below 0.10) via seeding cleanup (Lloyd relaxation, targeted extra seeds at low-compactness offenders — the "post-Lloyd repair pass" already drafted in `deliverables/checkpoint-002.md` §2.5 is exactly this class of fix), not by weakening this number |
| `G3_AREA_EPS_M2` | 10,000.0 m² | coverage tolerance (feeds Q2/G3-B) — numerical precision, unrelated |
| `G3_OVERLAP_EPS_M2` | 10,000.0 m² | overlap tolerance (feeds Q3) — same class |

**Re-derived — the one bound this amendment moves: `G3_AREA_CEIL_KM2`.**

Chosen anchor: real-world historical/administrative units at country scale.
Belgium ≈ 30,500 km²; the Netherlands ≈ 41,500 km². A peripheral,
low-interest cell sized "as large as a small-to-mid-sized historical
country" is a legible, defensible real-world description of what the new
ceiling permits — not an arbitrary number chosen to make a run green.

Cross-checked against the actual measured distribution at the current
596-cell mesh (orchestrator-measured `artifacts/cells_g3.json`, cited
verbatim, not re-derived): min 1 km², median 11,424 km², p90 22,902 km²,
p95 25,892 km², p99 29,174 km², **max 34,559 km²**. Cells exceeding
candidate ceilings: >15,000: 238; >20,000: 102; >25,000: 36; >30,000: 3;
**>40,000: 0**.

```
G3_AREA_CEIL_KM2 = 40,000.0 km²   (was 15,000.0 km²)

margin over the currently-measured max = 40,000 - 34,559 = 5,441 km²  (+15.7%)
margin over p99                        = 40,000 - 29,174 = 10,826 km² (+37.1%)
zero cells currently exceed 40,000 km² (measured, not assumed)
```

40,000 km² is chosen, not the bare measured max (34,559 km²) rounded up,
because (a) it sits on a real, nameable, round anchor — essentially the
Netherlands' own area — that a future reader can audit without re-deriving
statistics, and (b) it leaves genuine headroom above the measured max for
the seeding changes this same session must still make to close `G3-G`
(compactness): any re-seeding that adds extra seeds inside currently-over-
compact-floor cells could, in principle, slightly reshuffle a neighboring
cell's boundary and area; a ceiling set at exactly the measured max would
leave zero tolerance for that legitimate follow-on work.

**Load-bearing consequence, verified directly against `steps/03_cells.py`'s
own code (lines ~538, ~543), not asserted from prose**: the existing
densification step's per-mass subdivision target is computed as
`G3_AREA_CEIL_KM2 * 0.5` — currently `15,000 * 0.5 = 7,500` km² per
subdivided sub-cell. Raising the ceiling to 40,000 moves that target to
`40,000 * 0.5 = 20,000` km² — **densification becomes markedly less
aggressive**, meaning the seeding logic will subdivide oversized masses into
*fewer*, larger pieces than before, not more. This is the mechanism by which
raising the ceiling is expected to *reduce* `cell_count`, not increase it —
consistent with the brief's own framing above ("the actual count will
emerge lower — ~500 — under a raised ceiling").

**A further, load-bearing consequence for `G3-E` specifically**: because
`g3e_area_within_bounds` (`qa/checks.py:528`) compares each cell's own
`area_km2` directly against `ceil_km2`, and the currently-measured mesh's
own max (34,559 km²) already sits under the new 40,000 km² ceiling, **`G3-E`
is expected to pass immediately on a re-run of the existing seeding logic,
without requiring any further seeding-parameter change** — this is a
structural property of the measured data, not a guarantee for every possible
future re-seeded mesh (see Guardrails below for what happens if a later
seeding change, made to close `G3-G`, happens to push some cell over
40,000 km²).

### `G3_SEED_COUNT_MAX` re-verified sufficient at the new ceiling (why it is NOT re-derived a second time)

Re-running Amendment 007a-R2's own per-land-part pigeonhole method
(`deliverables/checkpoint-002.md` §2.4's method, at the *new* ceiling
instead of the old one), using the same 9 oversized-land-part area figures
already measured and cited in that checkpoint:

```
212 land parts total.
Parts <= 40,000 km^2 individually -> exactly 1 seed each (mandatory, per Q2):
  203 parts already <= 15,000 km^2 (unchanged)
  + 2 parts between 15,000 and 40,000 km^2 (25,628.1 and 23,860.8 km^2 -- now single-cell, were double-cell under the old ceiling)
  = 205 parts x 1 = 205

7 parts still > 40,000 km^2, each needing ceil(area_i / 40,000) cells:
    4,468,975.5 km^2 -> ceil(111.72) = 112
      950,145.0 km^2 -> ceil( 23.75) =  24
      357,672.8 km^2 -> ceil(  8.94) =   9
      292,309.5 km^2 -> ceil(  7.31) =   8
      218,564.8 km^2 -> ceil(  5.46) =   6
      153,527.2 km^2 -> ceil(  3.84) =   4
       82,682.3 km^2 -> ceil(  2.07) =   3
  sum over the 7 oversized masses = 166

TOTAL zero-packing-waste minimum at the new ceiling = 205 + 166 = 371
G3_SEED_COUNT_MAX (frozen at 600, unchanged by this amendment) = 600
headroom at the theoretical best case = 600 - 371 = 229 cells (1.62x)
```

371 is comfortably below 600 — a genuine, not marginal, headroom (unlike the
prior 445-vs-600 flat estimate, which itself later proved wrong once
per-part granularity was applied at the *old* 15,000 km² ceiling; this
recomputation applies the same corrected per-part method the Générateur
already validated, at the *new* ceiling, and the result is unambiguous).
Separately, the mesh's real, measured behavior is not dominated by this
zero-waste floor at all: the bulk of `cell_count` (399-596 across every run
so far) comes from urban-density-driven seeding across Western/Central/
Northern Europe, which the ceiling never constrained in the first place
(median cell size 11,424 km² even at the old, tighter 15,000 km² ceiling).
Raising the ceiling only removes the *extra*, ceiling-forced subdivision on
top of that density-driven baseline — it cannot plausibly push `cell_count`
*above* what has already been measured (399-596), only at or below it. For
both reasons, **`G3_SEED_COUNT_MAX` is left at 600, unchanged, frozen by
this amendment** — no fresh re-derivation of that particular number is
required or performed.

### Guardrails (restated for this scope, consistent with the prior amendments)

- Only the `G3_AREA_CEIL_KM2` line in `pipeline/geo/constants.py`
  (currently line 409) may change this amendment, marked
  `# FORGEHISTORY-G3-REPAIR` (the existing repair marker — no new marker
  type is introduced), in the same style already used for the
  `G3_SEED_COUNT_MAX` line at line 395 (e.g. `# FORGEHISTORY-G3-REPAIR
  (re-derived, Amendment 007a-R3: 15000 -> 40000)`). No other line in
  `constants.py` may change as part of this amendment.
- `G3_SEED_COUNT_MAX` (600), `G3_SEED_COUNT_MIN` (150),
  `G3_AREA_FLOOR_KM2` (200.0), `G3_AREA_MAX_MEDIAN_RATIO` (8.0),
  `G3_COMPACTNESS_MIN` (0.18), `G3_AREA_EPS_M2`, and `G3_OVERLAP_EPS_M2`
  stay byte-value-identical to their Amendment-007a-R2 state.
- `pipeline/geo/tests/run_proof_g3.py`, `pipeline/geo/tests/test_qa_red_g3.py`,
  and `pipeline/geo/qa/checks.py` stay byte-identical to VictoriaProject —
  this amendment, like the two before it, does not touch check
  *definitions*, only one of the input bounds one of them reads.
- Determinism still required (two-run SHA256, all pairs equal and
  non-empty). All of `G3-A` through `G3-H` (plus `q2`/`q3`) green **and**
  red-proven under the new ceiling — a re-run is required; the pre-007a-R3
  run (with `G3-E`/`G3-G` red) does not satisfy this on its own.
- **The raised ceiling alone is expected to resolve `G3-E`** (see above —
  structural, from the already-measured data), but does **not** by itself
  resolve `G3-G` (compactness — an orthogonal SHAPE property, unaffected by
  an area ceiling). Reaching `G3-G` green still requires a genuine
  seeding/construction-logic fix (Lloyd relaxation tuning, the targeted
  post-Lloyd repair pass already drafted in `steps/03_cells.py`'s "1.6"
  addition per `deliverables/checkpoint-002.md` §2.5, or an equivalent
  marked change), per the existing mechanical seeding-parameter-vs-bound
  test — not by weakening `G3_COMPACTNESS_MIN`.
- If, after this ceiling change, a further seeding-parameter adjustment made
  specifically to close `G3-G` happens to push some cell's area above
  40,000 km² (this amendment's stated margin over the measured max exists
  precisely to make this unlikely but not impossible): this is an ordinary
  Générateur/design-iteration matter — adjust the seeding change to stay
  under the ceiling — **not** grounds to request a third `G3_AREA_CEIL_KM2`
  increase without first showing a fresh pigeonhole-style proof that no
  seeding-parameter-only fix exists at 40,000 km², per the same escalation
  discipline Amendment 007a-R established and Amendment 007a-R2 exercised
  once already. Given the margins shown above (+15.7% over the measured
  max, +37.1% over p99), such an escalation would need a materially
  different, currently unforeseen finding to be admissible — not a
  restatement of this amendment's own numbers.
- Changing `G3_AREA_CEIL_KM2` to any value other than **40,000.0** is
  itself a disqualifying failure — this is a documented recalibration to a
  specific, derived, auditable number (real-world anchor + measured-margin
  rationale, both stated above), not license to pick "whatever number makes
  the run green."

### Amended Success Condition 7 (Lot 007a scope only — supersedes Amendment 007a-R2's cell-count-range-only amendment; the quality bar this session must actually clear)

- `run_proof_g3.py` exits 0, this session, this repository, by command — a
  **fresh** re-run against the new `G3_AREA_CEIL_KM2 = 40,000` and the
  seeding-logic change(s) made to close `G3-G`; the pre-007a-R3 run (12/14,
  `G3-E`/`G3-G` red) does not satisfy this row.
- Determinism preserved: every `logs/v1_049_qa.json` `determinism.sha256`
  pair equal and non-empty across the fresh two-run comparison.
- **Every entry in `checks` (`G3-A` through `G3-H`, `q2`, `q3`) `passed: true`
  and a non-empty `red_proof` — 14/14, no carry-forward FAIL, no further
  admissible escalation short of a fresh, materially different pigeonhole
  proof (see Guardrails above).** This is the version of Amended SC7 that
  must actually reach fully green; Amendment 007a-R's original text and
  Amendment 007a-R2's range widening were each, in turn, a necessary but not
  sufficient step toward this.
- `artifacts/stats_g3.json`'s `cell_count` within `[150, 600]` (unchanged
  from Amendment 007a-R2).
- `G3-E` checked against the re-derived ceiling — every cell's `area_km2`
  <= 40,000.0 km² (not 15,000.0).
- `G3-F` (ratio) and `G3-G` (compactness) checked against their **unchanged**
  frozen values (8.0 and 0.18 respectively) — this amendment does not
  relax either.

### Required Counters — new / redefined for this amendment (Lot 007a scope)

| name | sample source | denominator |
|---|---|---|
| `g3_bound_constants_unchanged` | **[REDEFINED]** value-comparison of the SIX now-frozen constants — `G3_SEED_COUNT_MIN`, `G3_AREA_FLOOR_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2` — in this repo's `constants.py` vs their Amendment-007a-R2 (== VictoriaProject) values | 6 (must equal 6 — all six unchanged in value; supersedes Amendment 007a-R2's "7" denominator, which included `G3_AREA_CEIL_KM2`) |
| `g3_seed_count_max_matches_derivation` | (unchanged from Amendment 007a-R2) this repo's `constants.py` `G3_SEED_COUNT_MAX` value | must equal 600 exactly — this amendment leaves it untouched |
| `g3_area_ceil_matches_derivation` | **[NEW]** this repo's `constants.py` `G3_AREA_CEIL_KM2` value, compared to the derived value cited in this section (40,000.0) — cited here, not re-derived by the Évaluateur | must equal 40,000.0 exactly |
| `g3_cell_count_in_range` | (unchanged source and range) `artifacts/stats_g3.json`'s `cell_count` vs `[150, 600]` | must be within [150, 600] — must be true, no carry-forward FAIL |
| `g3_qa_checks_passed_count` / `g3_qa_checks_red_proof_count` | (unchanged source) `logs/v1_049_qa.json`'s `checks` array, **post-this-amendment fresh re-run only** | 14 / 14 (must equal total entries — this is the counter that must finally reach its full denominator; a pre-007a-R3 measurement does not satisfy it) |
| `g3_repair_marker_count` | (unchanged mechanism) occurrences of `# FORGEHISTORY-G3-REPAIR` across `steps/03_cells.py` and `constants.py` | >= 1 in `steps/03_cells.py` (already true from the prior repair, plus any new "1.6"-class compactness-fix hunk) and now >= 2 in `constants.py` (the existing `G3_SEED_COUNT_MAX` line plus the new `G3_AREA_CEIL_KM2` line) |

All other counters from the main table and Amendments 007a-R/007a-R2
(`g3_check_definitions_byte_identical`, `g3_unmarked_nonrepair_diff_line_count`,
`g3_determinism_sha_pairs_matched_count`, `evidence_files_git_tracked_count`,
`proof_script_exit_code_zero_count`) are unchanged in source and
denominator.

### Disqualifying Failures — amended (Lot 007a only; supersedes item 2 of Amendment 007a-R2's list)

Any ONE of the following is an automatic REJECT for Lot 007a, regardless of
`run_proof_g3.py`'s exit code or any other green counter:

1. Any byte difference between `run_proof_g3.py`, `test_qa_red_g3.py`, or
   `qa/checks.py` and their VictoriaProject originals
   (`g3_check_definitions_byte_identical` < 3). *(unchanged)*
2. **[AMENDED]** Any of the six now-frozen G3 constants (`G3_SEED_COUNT_MIN`,
   `G3_AREA_FLOOR_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`,
   `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`) changed in value
   (`g3_bound_constants_unchanged` < 6), **OR** `G3_SEED_COUNT_MAX` set to
   any value other than **600** (`g3_seed_count_max_matches_derivation`
   false), **OR** `G3_AREA_CEIL_KM2` set to any value other than
   **40,000.0** (`g3_area_ceil_matches_derivation` false). **Explicitly,
   this includes relaxing `G3_COMPACTNESS_MIN` or
   `G3_AREA_MAX_MEDIAN_RATIO` to make `G3-G` or `G3-F` pass instead of
   fixing the seeding logic that actually produces the mesh** — Owner
   Option 2 relaxes cell SIZE only, never cell SHAPE quality, and doing so
   is precisely the disqualifying move this row exists to catch. Also
   includes editing a check *definition* (`qa/checks.py`) to weaken or
   bypass either check — covered independently by item 1 above, but named
   here too for emphasis given this amendment's explicit shape/size
   distinction.
3. Any `checks` entry with `passed: true` and an empty `red_proof`
   (unchanged).
4. Any diff line in `steps/03_cells.py` vs `.orig`, or in `constants.py`'s
   `G3_AREA_CEIL_KM2` or `G3_SEED_COUNT_MAX` lines, not ending in
   `# FORGEHISTORY-PATH-ADJUSTMENT` or `# FORGEHISTORY-G3-REPAIR`.
5. Any unequal or empty pair in `determinism.sha256` (unchanged).
6. **[NEW]** `run_proof_g3.py`'s `checks` array showing anything short of
   14/14 `passed: true` **without** a fresh, materially-different
   pigeonhole-style proof of unsatisfiability specifically against the new
   `G3_AREA_CEIL_KM2 = 40,000` bound (a restatement of Amendment 007a-R2's
   own 445-vs-600 or 645-vs-600 arguments, both computed against the old
   15,000 km² ceiling, does not qualify — those numbers are moot once the
   ceiling itself changes). Ordinary "still iterating, not yet green" is not
   this failure mode; presenting a stale proof as if it still applied, or
   silently landing short of 14/14 without escalating, is.

### Execution note — estimated tool calls for this amendment's scope

This is a small, targeted change on top of the already-completed Amendment
007a-R / 007a-R2 repair sessions, whose own investigative work
(`deliverables/checkpoint-001.md`, `checkpoint-002.md`) already: (a)
confirmed the repaired mesh reproduces deterministically, (b) fixed a
real pre-existing unmarked-diff-line risk, (c) found and diagnosed the true
`G3-E`/`G3-G` violation counts (not the check's own truncated 8-entry
sample), (d) derived the corrected per-land-part pigeonhole proof that
motivated this amendment, and (e) already drafted, on disk, a targeted
post-Lloyd "1.6" repair pass in `build_seeds()` aimed at closing `G3-G`
(currently expected, per that checkpoint's own honest assessment, to reduce
but not eliminate offenders under the *old* ceiling — its effectiveness
against the *new*, less-subdivision-hungry mesh this amendment produces is
not yet measured and must be re-evaluated fresh, not assumed).

For a **fresh session** resuming from this amendment plus the repository's
current files (per the harness's own resume-from-files-not-transcript rule):

1. Edit `constants.py` line 409 (`G3_AREA_CEIL_KM2 = 15_000.0` →
   `40_000.0`), marked `# FORGEHISTORY-G3-REPAIR`.
2. Re-run `run_proof_g3.py` fresh (single blocking call, `run_in_background`
   if the "1.6" preview-pass cost makes it long-running, per the Execution
   Contract — wait for the one completion notification, do not poll).
3. Read the result. Given `G3-E` is expected to pass immediately (structural,
   see above) and `G3-D`/`G3-F` were already green pre-amendment, the
   remaining live question is whether the already-drafted "1.6" pass (or a
   further Lloyd/parameter adjustment) closes `G3-G` under the new, coarser
   mesh. If not fully closed on the first re-run, iterate on that one check
   only — this is now a narrow, single-check design problem, not a
   multi-check pigeonhole.
4. Update `generator-log.md` (a new "Lot 007a-R3" section, same rigor as
   the prior sessions' §R4/§2.x write-ups), `manifest.json` (the new/changed
   counters above, plus a `git add -f` re-track of the 12 evidence files
   since their content will have changed), and `README.md` (truthfully
   state the recalibrated `G3_AREA_CEIL_KM2 = 40,000` and whether 14/14 was
   reached).

Estimated additional tool calls for this amendment's scope, fresh session:
**35-50** (one `constants.py` edit; a handful of `run_proof_g3.py` re-runs
and `stats_g3.json`/`v1_049_qa.json` reads while iterating on `G3-G`
specifically; evidence re-tracking; README/manifest/generator-log updates;
one `budget.py split-check` re-confirmation). This stays well inside Lot
007a's existing `135`-call budget (a fresh session starts its own tool-call
counter at 0 per the harness's per-session budget model). Before touching
any file, the Générateur must still run
`py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 135`
(or its own re-estimate, if materially higher) per Success Condition 13.
