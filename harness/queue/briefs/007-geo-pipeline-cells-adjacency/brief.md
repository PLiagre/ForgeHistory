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
| g3_cell_count_in_range | `artifacts/stats_g3.json`'s `cell_count` vs `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` in `pipeline/geo/constants.py` | must be within [150, 400] — **per Amendment 007a-R, must be true, not carry-forward false** |
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
`g3_bound_constants_unchanged`) required for Lot 007a post-amendment.**

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
Planificateur — never self-granted as a pass).

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

### Lot 007a — G3 cells (≤100 appels estimés) — **[re-scoped, see Amendment 007a-R below for the repair-scope estimate]**

| Champ | Valeur |
|---|---|
| Objectif | `pipeline.py` ported; `steps/03_cells.py` ported with marked path adjustment; `cities.json`/`city_coordinates.json` copied into `legacy_game_data/`; G3 determinism + QA proof green; evidence tracked in git |
| Dépendances | Brief 002 merged (`pipeline/geo/artifacts/coastline_1400.json`, shared infra, `legacy_game_data/province_*.json` already present) |
| Fichiers | `pipeline/geo/pipeline.py`, `pipeline/geo/steps/03_cells.py`, `pipeline/geo/tests/run_proof_g3.py`, `pipeline/geo/tests/test_qa_red_g3.py`, `pipeline/geo/legacy_game_data/cities.json`, `pipeline/geo/legacy_game_data/city_coordinates.json`, `pipeline/geo/README.md` (G3-landed edit), `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/**` |
| Critères | Success Conditions 1, 2 (cities/city_coordinates half), 3 (007a rows), 4 (amended), 6 (amended), 7 (amended), 10 (G3 evidence), 11 (first edit), 12 (03_cells.py.orig + first README snapshot), 13, 14 |
| Commande de validation | `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py` — exit 0 |
| Définition de terminé | Gate ACCEPT 007a; `artifacts/cells_g3.json` + `artifacts/adjacency_g3.json` + `artifacts/MANIFEST_g3.json` exist, are git-tracked, and are the input `04_adjacency.py` will read in 007b; **per Amendment 007a-R, the mesh is genuinely non-degenerate — all of G3-A..G3-H green with non-empty red_proof, not a carry-forward FAIL** |

### Lot 007b — G4 adjacency (≤90 appels estimés)

| Champ | Valeur |
|---|---|
| Objectif | `steps/04_adjacency.py` ported with marked path adjustment; `sea_zones.json` copied; `province_adjacency.json`/`province_coordinates.json` re-verified unchanged; G4 determinism + QA proof green including the Zuiderzee topology-link red-case; ADR-0003 boundary enforced in the exported artifact; evidence tracked in git |
| Dépendances | 007a merged (`artifacts/cells_g3.json` exists) — **per Amendment 007a-R, this must be the repaired, all-green `cells_g3.json`, not the degenerate 401-cell one** |
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
  `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX] = [150, 400]`.
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
  self-granted as a pass, never silently worked around.

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
  failure regardless of how green the resulting proof looks.
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
| g3_bound_constants_unchanged | value-comparison, one by one, of `G3_SEED_COUNT_MIN`, `G3_SEED_COUNT_MAX`, `G3_AREA_FLOOR_KM2`, `G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, `G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2` in this repo's `constants.py` vs VictoriaProject's `constants.py` | 8 (must equal 8 — all eight unchanged in value) |
| g3_determinism_sha_pairs_matched_count | (unchanged from main table) `logs/v1_049_qa.json`'s `determinism.sha256` dict | total key count (must equal numerator; total > 0) |
| g3_cell_count_in_range | (unchanged source, amended admissible outcome) `artifacts/stats_g3.json`'s `cell_count` vs `[150, 400]` | must be **true** — no carry-forward FAIL |

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
