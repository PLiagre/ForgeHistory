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

4. **Marked path adjustment — `steps/03_cells.py`, exactly one adjustment,
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

6. **No remaining `game_unity`/`StreamingAssets` reference** anywhere under
   `pipeline/geo/` outside the same narrow exception brief 002's amended
   Success Condition 3 already carved out for `data/divergences_1400.json`'s
   own pre-existing prose and `constants.py`'s `FORBIDDEN_GAME_PATH_MARKERS`
   literals (unchanged, not touched by this brief). Extended to the two new
   step files: a hit inside any `.py` file always counts. [Lot 007a, 007b]

7. **G3 determinism + QA proof runs, in this repository, by command.** From
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
- Must NOT change any logic in `steps/03_cells.py` or `steps/04_adjacency.py`
  beyond the single named path adjustment in each (Success Conditions 4-5) —
  any other diff line, marked or not, must be reverted.
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
| path_adjustment_unmarked_diff_line_count | line diff of each adjusted file vs its `deliverables/pre-port/*.orig` snapshot, counting differing lines not ending in the marker | 0 |
| game_unity_reference_remaining_count | grep `game_unity`/`StreamingAssets` across `pipeline/geo/`, same per-hit traceability rule as brief 002's amended Success Condition 3 (exception scope unchanged: `data/divergences_1400.json` + its verbatim copies/quotations + `constants.py`'s untouched `FORBIDDEN_GAME_PATH_MARKERS`) | 0 after exclusions |
| g3_determinism_sha_pairs_matched_count | `logs/v1_049_qa.json`'s `determinism.sha256` dict — matched/total | total key count (must equal numerator; total > 0) |
| g3_qa_checks_passed_count / g3_qa_checks_red_proof_count | `logs/v1_049_qa.json`'s `checks` array | total entries (all green, all red-proven) |
| g3_cell_count_in_range | `artifacts/stats_g3.json`'s `cell_count` vs `[G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX]` in `pipeline/geo/constants.py` | must be within [150, 400] |
| g4_determinism_sha_pairs_matched_count | `logs/v1_050_qa.json`'s `determinism.sha256` dict | total key count (must equal numerator; total > 0) |
| g4_qa_checks_passed_count / g4_qa_checks_red_proof_count | `logs/v1_050_qa.json`'s `checks` array | total entries (all green, all red-proven) |
| g4_sea_zone_count_in_range | `artifacts/stats_g4.json`'s `sea_zone_count` vs `[SEA_ZONE_COUNT_MIN, SEA_ZONE_COUNT_MAX]` in `constants.py` | must be within [20, 40] |
| g4_adjacency_by_kind_nonzero_count | `artifacts/stats_g4.json`'s `by_kind` dict — count of the 4 kinds with value > 0 | 4 (must equal 4 — every kind actually occurs) |
| g4_open_sea_reachability_with_links | `artifacts/topology_links_g4.json`'s `reachability.all_enclosed_reachable` | must be `true` |
| g4_open_sea_reachability_without_links_fails | `logs/v1_050_qa.json`'s `G4-B` check `red_proof` field (natural links-off case) | must be non-empty (proves the topology link is load-bearing, not decorative) |
| adjacency_g4_province_field_count | grep the substring `province` inside `artifacts/adjacency_g4.json` only (not `adjacency_divergence_g4.json`) | 0 (must equal 0) |
| evidence_files_git_tracked_count | `git ls-files` intersected with the declared list of new G3/G4 evidence files (both `logs/v1_04{9,50}_*`, both `artifacts/*g{3,4}*.json`, `registry/cell_registry.json`, `registry/sea_zone_registry.json`, `registry/g6_density_refinement.json`) | must equal the full declared count — none silently gitignored |
| proof_script_exit_code_zero_count | exit codes of `run_proof_g3.py` and `run_proof_g4.py` | 2 (must equal 2) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "`pip install -r requirements.txt` / the existing `.venv` fails in this environment" | `.venv/Scripts/pip.exe install -r requirements.txt` (or `pip show shapely` if venv already exists from brief 002) | actual failure output pasted in full; if invoked, Success Conditions 7, 8, and 10's evidence are excused for **this brief's verdict only** and must be re-raised for the next Planificateur pass — file copies and path adjustments (1-6) are never excused |
| "a legacy game-data file's current VictoriaProject SHA256 no longer matches the target cited in Success Condition 2" | `Get-FileHash 'C:\Users\liagr\VictoriaProject\game_unity\Assets\StreamingAssets\data\<file>.json' -Algorithm SHA256` | actual hash output showing a real mismatch against the cited target — if invoked, re-derive the target from the current file (do not silently keep the stale cited value) and record both hashes |
| "the new evidence files cannot be `git add -f`'d (repo hook rejects, or brief 002's own evidence was never actually tracked)" | `git add -f pipeline/geo/logs/v1_049_qa.json && git status --porcelain pipeline/geo/logs/v1_049_qa.json` | actual command output showing rejection or continued untracked status — if invoked, Success Condition 10 is satisfied instead by copying the evidence files into `deliverables/` (declared, committed copies), and this must be recorded as a real repository constraint, not silently worked around |

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
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 95   # lot 007a
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 85   # lot 007b
```

## Lots atomiques (ordre d'exécution — chaque lot = un `/forge-run` séparé)

### Lot 007a — G3 cells (≤100 appels estimés)

| Champ | Valeur |
|---|---|
| Objectif | `pipeline.py` ported; `steps/03_cells.py` ported with marked path adjustment; `cities.json`/`city_coordinates.json` copied into `legacy_game_data/`; G3 determinism + QA proof green; evidence tracked in git |
| Dépendances | Brief 002 merged (`pipeline/geo/artifacts/coastline_1400.json`, shared infra, `legacy_game_data/province_*.json` already present) |
| Fichiers | `pipeline/geo/pipeline.py`, `pipeline/geo/steps/03_cells.py`, `pipeline/geo/tests/run_proof_g3.py`, `pipeline/geo/tests/test_qa_red_g3.py`, `pipeline/geo/legacy_game_data/cities.json`, `pipeline/geo/legacy_game_data/city_coordinates.json`, `pipeline/geo/README.md` (G3-landed edit), `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/**` |
| Critères | Success Conditions 1, 2 (cities/city_coordinates half), 3 (007a rows), 4, 6 (03_cells.py scope), 7, 10 (G3 evidence), 11 (first edit), 12 (03_cells.py.orig + first README snapshot), 13, 14 |
| Commande de validation | `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py` — exit 0 |
| Définition de terminé | Gate ACCEPT 007a; `artifacts/cells_g3.json` + `artifacts/adjacency_g3.json` + `artifacts/MANIFEST_g3.json` exist, are git-tracked, and are the input `04_adjacency.py` will read in 007b |

### Lot 007b — G4 adjacency (≤90 appels estimés)

| Champ | Valeur |
|---|---|
| Objectif | `steps/04_adjacency.py` ported with marked path adjustment; `sea_zones.json` copied; `province_adjacency.json`/`province_coordinates.json` re-verified unchanged; G4 determinism + QA proof green including the Zuiderzee topology-link red-case; ADR-0003 boundary enforced in the exported artifact; evidence tracked in git |
| Dépendances | 007a merged (`artifacts/cells_g3.json` exists) |
| Fichiers | `pipeline/geo/steps/04_adjacency.py`, `pipeline/geo/tests/run_proof_g4.py`, `pipeline/geo/tests/test_qa_red_g4.py`, `pipeline/geo/legacy_game_data/sea_zones.json`, `pipeline/geo/README.md` (G4-landed edit), `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/**` |
| Critères | Success Conditions 2 (sea_zones + province re-verify), 3 (007b rows), 5, 6 (04_adjacency.py scope), 8, 9, 10 (G4 evidence), 11 (second edit), 12 (04_adjacency.py.orig + second README snapshot), 13, 14 |
| Commande de validation | `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g4.py` — exit 0 |
| Définition de terminé | Gate ACCEPT 007b; `artifacts/adjacency_g4.json`, `artifacts/sea_zones_g4.json`, `artifacts/adjacency_divergence_g4.json`, `artifacts/topology_links_g4.json`, `artifacts/MANIFEST_g4.json` exist, are git-tracked, and `pipeline/geo/README.md` truthfully states G3+G4 landed, rivers onward not yet landed |
