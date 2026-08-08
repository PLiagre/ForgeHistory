# Générateur log — Brief 007, Lot 007a (G3 cells)

**Author**: forge-generateur

Scope: Lot 007a only — `pipeline.py`, `steps/03_cells.py` (marked path
adjustment), `tests/run_proof_g3.py`, `tests/test_qa_red_g3.py`,
`legacy_game_data/cities.json`, `legacy_game_data/city_coordinates.json`,
`README.md` (first edit), `deliverables/**`. Lot 007b (`04_adjacency.py`,
`sea_zones.json`, G4 proof, ADR-0003 artifact check) is explicitly out of
scope for this run.

## 0. Split-check (SC13)

```
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 95
```
→ `advisory: SIZE_OK`. Run before touching any file, as required.

## 1. Pre-port / pre-edit snapshots (SC12) — taken before any edit

- `deliverables/pre-port/03_cells.py.orig` — copy of VictoriaProject's
  pristine `steps/03_cells.py`, taken before the path adjustment.
- `deliverables/pre-edit/pipeline-geo-README.md.orig` — copy of
  `pipeline/geo/README.md` before this lot's edit.

## 2. File copies (SC1, SC2, SC3)

`pipeline.py` (top-level, hard runtime dependency of `03_cells.py`'s
`derive_adjacency()` → `_load_pipeline_module()` → `ROOT / "pipeline.py"` via
`importlib.util.spec_from_file_location`, confirmed by reading
`steps/03_cells.py` lines 131-140 and 1125-1129 before copying):

```
cp "C:\Users\liagr\VictoriaProject\sandbox\geo\pipeline.py" pipeline/geo/pipeline.py
cp "C:\Users\liagr\VictoriaProject\sandbox\geo\steps\03_cells.py" pipeline/geo/steps/03_cells.py
cp "C:\Users\liagr\VictoriaProject\sandbox\geo\tests\run_proof_g3.py" pipeline/geo/tests/run_proof_g3.py
cp "C:\Users\liagr\VictoriaProject\sandbox\geo\tests\test_qa_red_g3.py" pipeline/geo/tests/test_qa_red_g3.py
```

Legacy game-data — resolved by reading `03_cells.py`'s `CITIES_JSON`/
`CITY_COORDS_JSON` expressions, confirming the real path
`C:\Users\liagr\VictoriaProject\game_unity\Assets\StreamingAssets\data\`:

```
cp "...\game_unity\Assets\StreamingAssets\data\cities.json" pipeline/geo/legacy_game_data/cities.json
cp "...\game_unity\Assets\StreamingAssets\data\city_coordinates.json" pipeline/geo/legacy_game_data/city_coordinates.json
```

Both files' SHA256, computed **before** copying (from the source), matched
the exact target hashes cited in brief.md Success Condition 2 —
`e2052ac8...cebfb` for `cities.json`, `052f7f4b...e00aa` for
`city_coordinates.json` — so no legacy-SHA-mismatch waiver was needed.

**Byte-identical verification** (all 5 files in this lot's subset):

```
pipeline\geo\pipeline.py                                  OK
pipeline\geo\tests\run_proof_g3.py                         OK
pipeline\geo\tests\test_qa_red_g3.py                       OK
pipeline\geo\legacy_game_data\cities.json                  OK
pipeline\geo\legacy_game_data\city_coordinates.json        OK
byte_identical_count(lot 007a subset)= 5 / 5
```

**Existing legacy data re-verified unchanged since brief 002** (not
re-copied):

```
pipeline\geo\legacy_game_data\province_adjacency.json      OK  37e8b5f3...ef89e
pipeline\geo\legacy_game_data\province_coordinates.json    OK  0cc6e120...e7168
existing_legacy_data_unchanged_count= 2 / 2
```

`province_adjacency.json`'s hash also matches brief.md Success Condition 2's
cited target exactly.

No file outside the declared Lot 007a table was created under `pipeline/geo/`
(`git status --porcelain pipeline/geo` confirmed — the only new paths are
the six declared source files plus evidence directories and `README.md`).

## 3. Legacy game-data decision table — all five files (brief.md's table)

| file | decision | this lot |
|---|---|---|
| `cities.json` | copy byte-identical | done |
| `city_coordinates.json` | copy byte-identical | done |
| `sea_zones.json` | copy byte-identical | lot 007b, not this lot |
| `province_adjacency.json` | reuse brief 002's copy, re-verify | re-verified unchanged |
| `province_coordinates.json` | reuse brief 002's copy, re-verify | re-verified unchanged |

Recorded identically in `pipeline/geo/README.md`.

## 4. Marked path adjustment (SC4)

`steps/03_cells.py` lines 71-86 (original, multi-line assignments):

```text
CITIES_JSON = (
    ROOT.parents[1]
    / "game_unity"
    / "Assets"
    / "StreamingAssets"
    / "data"
    / "cities.json"
)
CITY_COORDS_JSON = (
    ROOT.parents[1]
    / "game_unity"
    / "Assets"
    / "StreamingAssets"
    / "data"
    / "city_coordinates.json"
)
```

Replaced with:

```text
CITIES_JSON = ROOT / "legacy_game_data" / "cities.json"  # FORGEHISTORY-PATH-ADJUSTMENT
CITY_COORDS_JSON = ROOT / "legacy_game_data" / "city_coordinates.json"  # FORGEHISTORY-PATH-ADJUSTMENT
```

Verification against `deliverables/pre-port/03_cells.py.orig` via
`difflib.SequenceMatcher`:

```
path_adjustment_marker_count = 2   (rg -c '# FORGEHISTORY-PATH-ADJUSTMENT' steps/03_cells.py)
path_adjustment_unmarked_diff_line_count = 0
```

Both differing lines (the two replacement assignments) end with the literal
marker; no other line differs from the pristine original.

## 5. `game_unity`/`StreamingAssets` audit (SC6) — a real, honest conflict

Whole-tree walk of `pipeline/geo` (excluding `.venv/`, `__pycache__/`) found
8 raw hits. Classified per-hit, same traceability discipline as brief 002's
amended Success Condition 3:

| location | classification |
|---|---|
| `constants.py:561-562` (`FORBIDDEN_GAME_PATH_MARKERS` literals) | pre-existing, brief-002 scope, unchanged by this brief — **counts** per established precedent (brief 002's own manifest recorded this as 2, not 0) |
| `data/divergences_1400.json:1` | named exception (prose, SHA-identical to VictoriaProject original) — excluded |
| `artifacts/divergences_1400.json:1` | named exception (copy from brief 002) — excluded |
| `logs/v1_047_corrections.log:24` | named exception (verbatim quote) — excluded |
| `steps/03_cells.py:108-109` (`RADIUS_FIELD["sources"]` metadata) | **new hit this lot** — pre-existing in the byte-identical VictoriaProject original (confirmed present in `deliverables/pre-port/03_cells.py.orig`); not path-resolution logic; not the marked adjustment — **counts** per SC6's explicit extension ("a hit inside any .py file always counts") |
| `steps/03_cells.py:179` (docstring on `load_cities_readonly`) | same — **counts** |

**Total: 5** (2 + 3), not 0.

This is the same class of unsatisfiable-counter conflict brief 002 hit with
`FORBIDDEN_GAME_PATH_MARKERS` (its "Defect A"). Non-Goals state, without
qualification: *"Must NOT change any logic in steps/03_cells.py ... beyond
the single named path adjustment ... any other diff line, marked or not,
must be reverted."* Editing the `sources` list or the docstring to remove
the substrings would itself be exactly such an unauthorized diff line. There
is no way to simultaneously satisfy "exactly one marked adjustment, nothing
else changed" and "zero references." Measured honestly at 5; not silently
forced to 0 by an out-of-scope edit. Raised for Planificateur/Évaluateur.

## 6. G3 proof run (SC7) — run in this repository, this session

```
cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py
```

**Exit code: 1 (FAIL).** Full console output captured in this session
(reproduced in the tool-call transcript). Key measured facts:

- **Determinism**: two-run SHA256 comparison of 6 artifacts
  (`MANIFEST_g3.json`, `adjacency_g3.json`, `cells_g3.json`,
  `stats_g3.json`, `registry/cell_registry.json`,
  `registry/g6_density_refinement.json`) — **6/6 matched, all non-empty**.
  The algorithm is genuinely deterministic; the port faithfully reproduces
  it run-to-run.
- **QA checks**: 14 entries in `logs/v1_049_qa.json`'s `checks` array.
  **14/14 have a non-empty `red_proof`** (every red-case genuinely fired —
  confirmed in the console's `=== mutations rouges ===` block, all
  `became_red=True`). **Only 9/14 have `passed: true`.** The 5 failing:

  | id | detail | red_proof (fired) |
  |---|---|---|
  | `G3-B` | `part_154:covered_m2=554.3` — one land mass not fully covered | `empty_cells_no_mass_covered` |
  | `G3-D` | `count=401 expected=[150,400]` — 401 is one **above** `G3_SEED_COUNT_MAX=400` | `cell_count_fifty_below_min` |
  | `G3-E` | 8 cells listed with `area>ceil=15000.0` (e.g. cell 1194 at 36115.3 km²) | `cell_area_above_ceil_like_v1_048_giant` |
  | `G3-F` | `max=950145.0 median=2875.1 ratio=330.479 ceil=8.0` | `max_median_ratio_728_like_v1_048` |
  | `G3-G` | 8 cells listed below `compactness_min=0.18` (e.g. cell 1225 at pp=0.081) | `shard_compactness_below_floor` |

- **`stats_g3.json` `cell_count` = 401**, one above
  `G3_SEED_COUNT_MAX=400` (from `pipeline/geo/constants.py`, unchanged,
  cited not re-derived) — **out of the declared [150,400] range.**

### Root-cause investigation — is this a porting defect?

Checked whether this is something introduced by the port (a bad copy, a
library-version mismatch, or a stray edit) versus an already-present state
of the unchanged VictoriaProject source. **Confirmed it is the latter, not
the former**, by three independent checks:

1. **Byte-identity**: `pipeline.py`, `steps/03_cells.py` (beyond the marked
   adjustment) are SHA256-identical to their VictoriaProject originals.
   `constants.py` was ported unchanged by brief 002 and re-verified
   untouched this lot (`G3_SEED_COUNT_MIN=150`, `G3_SEED_COUNT_MAX=400`,
   `G3_AREA_CEIL_KM2=15000.0`, `G3_AREA_MAX_MEDIAN_RATIO=8.0`,
   `G3_COMPACTNESS_MIN=0.18` — identical values in both repos, confirmed by
   `grep`).
2. **Cross-repo output comparison**: VictoriaProject's own currently-recorded
   `artifacts/stats_g3.json` (its own disk state, not touched, not modified
   by this session) already shows `cell_count=401` and
   `max_median_ratio=330.479417` — matching this port's fresh run to 6
   decimal places. This is strong evidence the exact same deterministic
   algorithm, on the exact same corrected-coastline input, produces this
   exact output in both repositories.
3. **Staleness of VictoriaProject's own "green" record**: its
   `logs/v1_049_qa.json` (all `passed: true`) has mtime
   `2026-07-26T12:24:01`, which **predates** its own `constants.py`
   (`2026-07-29T10:49:42`) and `steps/03_cells.py`
   (`2026-07-29T10:14:03`) — both of which predate its own
   `artifacts/stats_g3.json` (`2026-07-29T11:07:40`). VictoriaProject's own
   proof script was never re-run after whatever later change produced the
   current `cell_count=401` state; its recorded "PASS" is stale and does
   not reflect the current, real behavior of its own unchanged source. (All
   three `Get-FileHash`-equivalent `os.path.getmtime` reads were performed
   read-only against VictoriaProject; nothing there was modified or
   executed to write new output.)

**Conclusion**: this is a real, reproducible FAIL of the byte-identical,
unchanged algorithm against its own currently-unchanged thresholds — an
inherited state already present in the read-only source, not something this
port introduced. Non-Goals forbid any further logic change to
`steps/03_cells.py` beyond the one marked path adjustment and forbid
touching `constants.py` at all, so it cannot be resolved within this lot's
scope. No Acceptable Waiver in brief.md covers this scenario (the pip/venv
waiver does not apply — the venv is fully functional: shapely 2.1.2, pyproj
3.7.2, numpy 2.5.1 all installed, the script ran to completion and wrote
every artifact). Recorded as measured fact, not worked around.

## 7. Evidence tracking (SC10)

Checked whether brief 002's own evidence was actually tracked in git before
choosing a mechanism:

```
git ls-files pipeline/geo/logs pipeline/geo/artifacts   → (empty)
git log --diff-filter=A --name-only --all -- 'pipeline/geo/logs/*' 'pipeline/geo/artifacts/*'   → (empty)
```

Brief 002's manifest declared those paths as deliverables but they were
**never actually force-added**; brief 002's own `verdict.md` (`Defect C`)
flagged this exact gap as unresolved. This lot establishes tracking for the
first time, using `git add -f` — the mechanism brief.md names as "most
likely":

```
git add -f pipeline/geo/logs/v1_049_qa.json pipeline/geo/logs/v1_049_cells.log \
  pipeline/geo/artifacts/MANIFEST_g3.json pipeline/geo/artifacts/adjacency_g3.json \
  pipeline/geo/artifacts/cells_g3.json pipeline/geo/artifacts/stats_g3.json \
  pipeline/geo/registry/cell_registry.json pipeline/geo/registry/g6_density_refinement.json \
  pipeline/geo/capture/v1_049_cells_window.png pipeline/geo/capture/v1_049_cells_coast_zoom.png \
  pipeline/geo/capture/v1_049_cells_paris_basin.png pipeline/geo/capture/v1_049_paris_basin_before_after.png
```

`git ls-files` confirms all 12 files now tracked (staged `A`).
`sea_zone_registry.json` does not exist yet (G4/lot 007b scope).

## 8. README update (SC11)

`pipeline/geo/README.md` rewritten to state: G3 code (cells mesh, path
adjustment, legacy data) landed; the proof currently FAILS with full detail
(not glossed over); the five-file legacy-data decision table; the
`game_unity_reference_remaining_count` conflict; G4 adjacency and everything
downstream explicitly still not landed. Pre-edit snapshot
(`deliverables/pre-edit/pipeline-geo-README.md.orig`, SHA
`1cb08512dca13850effe199bbea3c85c0f4b82a6ac0f7355b09f98f6681437db`) differs
from the post-edit file (SHA
`11a0f292647f01d90e7e939788fb10ce1d77ef1aa8fb9a41b8f8f0870f221b61`).

## 9. Non-Goals check (SC14)

`git status --porcelain docs/adr sim/` — empty.
`git diff --stat docs/adr/0003-single-spatial-primary-key.md` — empty (no
change). No `05_rivers.py`-onward file, no `08_ownership.py`, present in
`pipeline/geo/steps/` beyond `02_coastline.py`, `02b_corrections_1400.py`,
`03_cells.py`, `__init__.py`.

## Summary for the Évaluateur (original port iteration)

File copies, byte-identity, the marked path adjustment, evidence tracking,
and the legacy-data decisions are all mechanically clean and verified
in-session. **Two genuine, honestly-measured problems block this lot's full
acceptance and are not workaroundable within scope**: (1) the
`game_unity_reference_remaining_count` counter is unsatisfiable at 0 given
Non-Goals' "no other diff line" rule applied to pre-existing content in the
newly-ported `steps/03_cells.py` (measured at 5); (2) `run_proof_g3.py`
exits 1 and 5/14 QA checks fail, reproducibly, on the byte-identical ported
algorithm against unchanged thresholds — confirmed via VictoriaProject's own
(read-only, unmodified) file mtimes and its own currently-recorded
`stats_g3.json` to be an already-present state of the source, not a porting
defect. Both are reported in full per the hard-won rule against pronouncing
one's own work acceptable; disposition is for the Évaluateur/Planificateur.

---

# Lot 007a-R (repair) — Amendment 007a-R

**Author**: forge-generateur

Scope: repair `steps/03_cells.py`'s seeding/mesh-construction logic so the
G3 Voronoi mesh is genuinely non-degenerate on the current (post-brief-002)
coastline, per Amendment 007a-R (SC4/SC6/SC7 amended, superseding the
original port-only scope). `04_adjacency.py`/lot 007b, `sim/`,
`docs/adr/0003-*.md`, `pipeline.py` untouched. `qa/checks.py`,
`tests/run_proof_g3.py`, `tests/test_qa_red_g3.py`, and all eight named G3
bound constants in `constants.py` are frozen and were not touched (verified
below).

## R0. Split-check re-confirmed

```
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 135
```
→ `advisory: SIZE_OK`.

## R1. Diagnosis (performed before any edit)

Reused the existing `deliverables/pre-port/03_cells.py.orig` snapshot
(SHA256-verified in the original 007a run against VictoriaProject's
pristine file; working tree not reset since, so no re-take was needed).

Reproduced the original port's failure with a fresh `run_cells()` call and
inspected `run['land_xy']`/`_iter_parts(land_xy)` directly (not from the
committed artifact, to rule out staleness):

- `land_xy.area` (the full pilot-window land geometry) = **6,667,146.53
  km²**, valid `MultiPolygon`, 213 parts. Cross-checked against
  VictoriaProject's own current `artifacts/stats_g3.json` (read-only):
  identical `cell_count=401`, identical `area_km2` block to 6 decimal
  places — confirms the port reproduces VictoriaProject's own current
  (non-stale) behavior exactly, not a ForgeHistory-introduced bug.
- Exactly **one** land part has area `<= G3_AREA_EPS_M2` (10,000 m²): part
  index 154, area **554.30 m²** — a coastline-digitization sliver. `G3-B`'s
  own check (`qa/checks.py:g3b_all_land_masses_covered`) requires
  `intersection_area > area_eps` per mass; a whole-fragment cell covering
  exactly this sliver would still measure 554.3 m² < 10,000 m², so `G3-B`
  is **unreachable for this fragment regardless of seeding** unless it is
  excluded from the enumerated masses (it remains part of `land_xy` itself,
  so `Q2`'s coverage tolerance, also `area_eps`, is unaffected — the
  fragment is 18× smaller than that tolerance).
- The 950,145 km² giant cell (matching VictoriaProject's own current
  `stats_g3.json` `area_km2.max` to 6 decimals) traced to land part index
  28 (bounds/centroid confirm this is the North Africa/Maghreb landmass
  within the pilot window). Instrumented `_poisson_variable_radius()`'s
  first two steps directly: **213 mandatory seeds (1/mass, unconditional)
  + up to 198 urban-anchor seeds (unconditional) = up to 411 candidate
  seeds — already at or above `G3_SEED_COUNT_MAX=400` before the
  density-adaptive Bridson step (originally step 3) ever ran.** Confirmed
  `n cities=198`, all 198 successfully snap to land. Bridson's `while
  active and len(seeds) < seed_count_max:` loop condition is false from the
  start whenever steps 1+2 already reached 400, so land masses that
  received only their single mandatory seed (few or no nearby cities, e.g.
  the Maghreb interior) are never subdivided — `build_cells()`'s
  `if len(local_seeds) <= 1 ... : geom = part` fallback then makes the
  *entire* 950,145 km² mass one cell.
- Mathematical check on the ceiling itself:
  `G3_SEED_COUNT_MAX(400) × G3_AREA_CEIL_KM2(15,000) = 6,000,000 km²`,
  **667,146 km² short of the actual land area (6,667,146.53 km²)**. Flagged
  at this stage as a possible hard (pigeonhole) limit independent of
  algorithm quality — confirmed after the repair (§R4 below).

## R2. Repair — two `# FORGEHISTORY-G3-REPAIR`-marked changes, zero constants touched

**Change 1 — `_iter_parts()` excludes sub-`area_eps` fragments** (addresses
`G3-B`): the `MultiPolygon` branch now filters
`polys = [g for g in polys if g.area > G3_AREA_EPS_M2]` before returning.
Single function, used consistently by `build_seeds()`, `build_cells()`,
`run_cells()`, and `run_proof_g3.py`'s own direct call
(`cells_mod._iter_parts(land_xy)`, line 93 — unmodified, frozen file), so
`G3-B`'s `land_parts` input is automatically consistent everywhere. No
geometry surgery (no merge/dissolve): the sliver stays part of `land_xy`
(so `Q2`'s global coverage check is unaffected, verified: 554.3 m² ≪
10,000 m² tolerance); it is simply not enumerated as an addressable "mass"
requiring its own seed/cell — consistent with `G3-B`'s own definition of
"covered" via that same epsilon.

**Change 2 — Bridson reordered before urban anchors, capped by remaining
budget** (addresses `G3-D`/`G3-E`/`G3-F`/`G3-G`): in
`_poisson_variable_radius()`, the density-adaptive Bridson expansion
(formerly step 3) now runs immediately after the mandatory one-per-mass
seeding (step 1), consuming the seed budget according to `r(x)` — which
already encodes urban density via `density_at()`/`city_weight()` — across
*all* land masses, not just those near a named city. Urban-anchor placement
(formerly step 2, unconditional) now runs last, guarded by
`if len(seeds) >= seed_count_max: break`. Rationale: anchors only guarantee
a seed at the *exact* city coordinate; the density field itself (not the
anchor mechanism) is what "seeded by declared urban density" (brief's
World-Terms Requirement) actually requires, and near cities Bridson already
places points densely due to the small local `r(x)`, so most anchors still
land successfully (via `far_enough`) even placed last.

Every line touched by either change ends in `# FORGEHISTORY-G3-REPAIR`
(21 occurrences in `steps/03_cells.py`, `rg -c` verified) or the
pre-existing `# FORGEHISTORY-PATH-ADJUSTMENT` (2, unchanged). `constants.py`
carries **zero** `# FORGEHISTORY-G3-REPAIR` markers — no seeding-parameter
constant was changed; the repair is logic-only.

```
git diff --no-index deliverables/pre-port/03_cells.py.orig pipeline/geo/steps/03_cells.py \
  | grep '^+' | grep -v '^+++' | grep -vc -e 'FORGEHISTORY-PATH-ADJUSTMENT' -e 'FORGEHISTORY-G3-REPAIR'
→ 0   (g3_unmarked_nonrepair_diff_line_count)
```

Verified with a real diff tool (`git diff --no-index`, Myers algorithm) —
not just Python `difflib` — because the reorder relocates the urban-anchor
block to a new position; a naive diff can show the unchanged, relocated
lines as new unmarked insertions (no move-detection). Every relocated line
is explicitly marked, even where its own text is otherwise unchanged, so
the unmarked count is 0 under either diff method.

## R3. Proof run (post-repair) — single blocking call

```
cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py
```
**Exit code: 1** (still non-zero — see §R4). Measured facts:

- **Determinism preserved**: `logs/v1_049_qa.json`'s `determinism.sha256`
  — **6/6 pairs matched, all non-empty** (`MANIFEST_g3.json`,
  `adjacency_g3.json`, `cells_g3.json`, `stats_g3.json`,
  `registry/cell_registry.json`, `registry/g6_density_refinement.json`).
  `determinism.match = true`. The repair introduced no wall-clock/iteration-
  order nondeterminism.
- **All 14 `checks` entries carry a non-empty `red_proof`** — every
  red-case (byte-identical `test_qa_red_g3.py`, untouched) genuinely fired,
  including on the still-failing checks:

  | id | passed | red_proof |
  |---|---|---|
  | Q1 | true | `cells[0]_bowtie_self_intersection` |
  | Q2 | true | `drop_first_cell_creates_hole` |
  | Q3 | true | `duplicate_geometry_overlap` |
  | Q4 | true | `empty_adjacency_all_isolated` |
  | Q10 | true | `forced_sha_mismatch_cells_g3` |
  | G3-A | true | `cell_geometry_placed_in_sea` |
  | **G3-B** | **true (was false)** | `empty_cells_no_mass_covered` |
  | G3-C | true | `domain_key_rebinding_different_id` |
  | **G3-D** | **true (was false)** | `cell_count_fifty_below_min` |
  | G3-E | false | `cell_area_above_ceil_like_v1_048_giant` |
  | G3-F | false | `max_median_ratio_728_like_v1_048` |
  | G3-G | false | `shard_compactness_below_floor` |
  | G3-H | true | `retired_id_reissued_as_active` |
  | G2b-B | true | `forced_sha_mismatch_vs_g2_reference` |

- `artifacts/stats_g3.json`: `cell_count` **401→399** (now within
  `[150,400]`, `G3-D` green); `area_km2.max` **950,145.03→215,449.18** km²;
  `area_km2.max_median_ratio` **330.48→58.41**; `compactness_polsby_popper`
  unchanged in shape (`min=0.046` remains, but that cell is
  singleton-exempt; the lowest *non-exempt* compactness moved to
  ~0.059-0.096 on several of the same large low-density cells that still
  exceed the area ceiling — same root cause, see §R4).

## R4. G3-E/F/G — proven unsatisfiable by any seeding-parameter-only change (escalated, not self-granted)

**Mathematical proof (pigeonhole, independent of algorithm quality):**

- `land_xy.area` (this repo's own current pilot-window land geometry,
  `pipeline/geo/legacy_game_data` + Natural Earth via brief 002's
  coastline) = **6,667,146.530456 km²** (`land_xy.area/1e6`, directly
  measured, valid `MultiPolygon`).
- `G3_SEED_COUNT_MAX = 400`, `G3_AREA_CEIL_KM2 = 15,000.0` — both frozen,
  both verified unchanged (§R5). `400 × 15,000 = 6,000,000 km²`.
- `Q2` (`q2_no_holes_eps`, frozen, currently `passed: true`) requires
  `land_geom.difference(union_of_cells).area <= area_eps (10,000 m²)` —
  i.e. the cells must tile essentially **all** of the land (global
  tolerance ≈ 1 hectare against 6.67 million km²). Since `Q2` passes, the
  sum of all cell areas ≈ 6,667,146.53 km².
- If every one of the ≤400 cells individually satisfied `area ≤ 15,000
  km²` (`G3-E`), their sum could be at most `6,000,000 km²` — but the sum
  must equal ~6,667,146.53 km² to satisfy `Q2`. **6,000,000 < 6,667,146.53:
  contradiction.** At least one cell (in practice, by the shortfall's
  scale, several) must exceed `G3_AREA_CEIL_KM2` regardless of how seeds
  are placed, as long as `G3_SEED_COUNT_MAX` and `G3_AREA_CEIL_KM2` stay at
  their frozen values and the land geometry stays as-is (both true —
  neither this brief's Non-Goals nor Amendment 007a-R permit touching
  either).
- Real-world grounding, not a coastline artifact: `PILOT_WINDOW_LONLAT =
  (-11.320281, 29.7, 34.820281, 61.5)` — lon -11.3° to 34.8°, lat 29.7° to
  61.5° — genuinely spans Western/Central/Northern Europe plus the Maghreb
  Mediterranean coast (Morocco/Algeria/Tunisia). Land-part-level areas
  (`_iter_parts`) are consistent with real countries at that scale (the
  largest single landmass, part 99, measures 4,468,975 km², in the right
  order of magnitude for "continental Western/Central/Northern Europe
  within this bbox" against known national land areas). This is a real
  land-area/budget mismatch, not a bug in the coastline or in `_iter_parts`.

**Empirical sweep (evidence the repair could not route around the ceiling
by seeding-parameter tuning alone, per Amendment 007a-R's required
escalation form — parameter varied is `G3_R_CEIL_M`, confirmed NOT read by
any `g3*`/`q*` function in `qa/checks.py`, i.e. a legitimate seeding
parameter, not a bound):**

```
_poisson_variable_radius(..., r_ceil=95000.0) -> 400 raw seeds (cap reached)
_poisson_variable_radius(..., r_ceil=60000.0) -> 400 raw seeds (cap reached)
_poisson_variable_radius(..., r_ceil=40000.0) -> 400 raw seeds (cap reached)
```

Lowering `G3_R_CEIL_M` (denser minimum spacing everywhere, including
already-dense urban areas) does not help: Bridson simply hits the frozen
`G3_SEED_COUNT_MAX=400` cap even faster, because the *whole* land area
requires more points at any tighter spacing, not just the sparse regions —
the 400-seed budget is the binding constraint network-wide, not a
parameter-tuning problem local to one land mass. There is no seeding
parameter that increases effective capacity without violating
`G3_SEED_COUNT_MAX`.

**Conclusion — escalated, per Amendment 007a-R's required path (not a
self-granted pass, not a carry-forward FAIL):** `G3-E` (and, as a direct
consequence of the same seed-budget/land-area mismatch, the *distribution*
underlying `G3-F`'s ratio and `G3-G`'s compactness on the few remaining
oversized, coastline-hugging cells) cannot be satisfied without either (a)
raising `G3_SEED_COUNT_MAX` and/or `G3_AREA_CEIL_KM2` — both frozen,
disqualifying to touch — or (b) shrinking the pilot window / land area —
out of `03_cells.py`'s scope (owned by brief 002's `02_coastline.py` /
`constants.py`'s `PILOT_WINDOW_LONLAT` derivation, both untouched Non-Goals
for this lot). This is recorded as a brief-premise/constants-calibration
conflict for the Planificateur, exactly as Amendment 007a-R's escalation
clause anticipates — not resolved by editing a frozen check or threshold,
and not silently forced green.

## R5. Frozen-scope verification (Disqualifying Failures, all clear)

```
SHA256(pipeline/geo/tests/run_proof_g3.py)   == SHA256(VictoriaProject original)   True
SHA256(pipeline/geo/tests/test_qa_red_g3.py) == SHA256(VictoriaProject original)   True
SHA256(pipeline/geo/qa/checks.py)            == SHA256(VictoriaProject original)   True
→ g3_check_definitions_byte_identical = 3 / 3

G3_SEED_COUNT_MIN=150  G3_SEED_COUNT_MAX=400  G3_AREA_FLOOR_KM2=200.0
G3_AREA_CEIL_KM2=15000.0  G3_AREA_MAX_MEDIAN_RATIO=8.0  G3_COMPACTNESS_MIN=0.18
G3_AREA_EPS_M2=10000.0  G3_OVERLAP_EPS_M2=10000.0
(this repo's constants.py, byte-value comparison against VictoriaProject's constants.py, all 8 equal)
→ g3_bound_constants_unchanged = 8 / 8

g3_repair_marker_count (steps/03_cells.py, rg -c '# FORGEHISTORY-G3-REPAIR') = 21
constants.py '# FORGEHISTORY-G3-REPAIR' marker count = 0 (no constant changed)
g3_unmarked_nonrepair_diff_line_count (git diff --no-index vs .orig) = 0
```

Every Disqualifying Failure condition (Amendment 007a-R) checked and
clear: (1) check-definition files byte-identical — clear; (2) bound
constants unchanged — clear; (3) no `passed:true` entry has an empty
`red_proof` — clear (only `false`-passed entries have any relationship to
the still-open checks, and even those carry non-empty `red_proof`); (4) no
unmarked diff line — clear; (5) determinism — clear (6/6 matched,
non-empty).

## R6. Re-verified counters unaffected by the repair (unchanged from the original 007a run)

`byte_identical_new_files_count` (5/5), `legacy_data_sha_target_match_count`
(2/2), `existing_legacy_data_unchanged_count` (2/2),
`path_adjustment_marker_count` (2, unchanged), all re-verified this session
with the same commands as §2/§4 of the original port log above — no drift,
since `pipeline.py`, `run_proof_g3.py`, `test_qa_red_g3.py`,
`cities.json`, `city_coordinates.json`, and the two path-adjustment lines
were not touched by the repair.

`game_unity_reference_remaining_count`: whole-tree `.py` walk still finds
the same 5 raw hits (`constants.py:561-562`,
`steps/03_cells.py:108,109,179`) — the repair does not touch any of these
five lines. Per Amendment 007a-R's Amended Success Condition 6, all five
are now a permanent named exception (independent of the repair having
happened) → **0** after exclusion.

## R7. Evidence re-tracked

The repair regenerated `logs/v1_049_qa.json`, `logs/v1_049_cells.log`, and
all `artifacts/*g3*.json`/`registry/*.json`/`capture/*.png` in place (same
paths as the original 007a run). Re-applied the same mechanism (`git add
-f`, established in the original 007a run, consistent — not a new
mechanism):

```
git add -f pipeline/geo/logs/v1_049_qa.json pipeline/geo/logs/v1_049_cells.log \
  pipeline/geo/artifacts/MANIFEST_g3.json pipeline/geo/artifacts/adjacency_g3.json \
  pipeline/geo/artifacts/cells_g3.json pipeline/geo/artifacts/stats_g3.json \
  pipeline/geo/registry/cell_registry.json pipeline/geo/registry/g6_density_refinement.json \
  pipeline/geo/capture/v1_049_cells_window.png pipeline/geo/capture/v1_049_cells_coast_zoom.png \
  pipeline/geo/capture/v1_049_cells_paris_basin.png pipeline/geo/capture/v1_049_paris_basin_before_after.png
```

## R8. README update

`pipeline/geo/README.md`'s "Landed (brief 007, lot 007a)" section rewritten
to state the repair explicitly: what changed, why, the before/after
numbers, and the proven-unsatisfiable status of `G3-E`/`G3-F`/`G3-G` — not
"non-degenerate" overclaimed, since 3/14 checks remain red; truthfully
described as "genuinely repaired, materially improved, three checks proven
mathematically blocked by a frozen-constant/land-area mismatch, escalated."

## Summary for the Évaluateur (repair iteration)

Diagnosed and fixed the two genuinely seeding-logic-fixable defects
(`G3-B` uncovered sliver, `G3-D` seed-budget starvation causing both the
401>400 overshoot and the giant-cell/ratio/compactness degeneracy) with two
marked, minimal, deterministic changes — zero constants touched, zero
frozen files touched, determinism preserved, every red-case still fires.
Measured, large improvement: cell_count in range, giant cell 950k→215k km²,
ratio 330→58. The three remaining failures (`G3-E`, `G3-F`, `G3-G`) are not
an unfixed defect: proven via a closed-form pigeonhole argument (total
seed-count cap × area ceiling < actual land area, given `Q2`'s near-total
coverage requirement) that **no** seeding-parameter-or-logic-only change can
satisfy them without touching a frozen bound constant or the land geometry
itself — both out of this lot's scope. Backed by an empirical parameter
sweep (`G3_R_CEIL_M` at three values, all hit the frozen seed cap without
relieving the shortage). Escalated to the Planificateur per Amendment
007a-R's required path, not self-granted as a pass, not silently forced
green, not recorded as a plain carry-forward FAIL.

## Lot 007a-R3 (Amendment 007a-R3: relax G3_AREA_CEIL_KM2, owner Option 2)

**Author**: forge-generateur

### R3.0 Split-check

```
py harness/budget.py split-check --brief harness/queue/briefs/007-geo-pipeline-cells-adjacency --estimated-calls 60
```
Result: `advisory: SIZE_OK`. Run before touching any file, as the amendment requires.

### R3.1 The single authorized bound change

`pipeline/geo/constants.py` line 409: `G3_AREA_CEIL_KM2 = 15_000.0` to
`40_000.0`, marked `# FORGEHISTORY-G3-REPAIR (re-derived, Amendment
007a-R3: 15000 -> 40000)`. `git diff pipeline/geo/constants.py` shows
exactly this one changed value-line, nothing else — confirmed both at the
start and end of this session.

### R3.2 First re-run — confirms the amendment's own structural prediction

`cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py` (single
blocking call, `run_in_background` + wait for completion per the Execution
Contract, roughly 4-8 minutes per run given the doubled `build_cells` cost
from the existing "1.6" preview pass):

- `EXIT=1`. `cell_count=596` (within `[150,600]`).
- `G3-E` newly `passed: true` — `max=37217.8` km², comfortably under
  the new 40,000 km² ceiling. This is exactly the "structural, no seeding
  change required" outcome the amendment's own derivation predicted.
- `G3-F` remains `passed: true` — `ratio=3.838`, well under 8.0.
- `G3-G` still `passed: false` — the check's own truncated 8-entry detail
  string; true count (via `artifacts/cells_g3.json` plus singleton
  exemption, same method as checkpoint-002) is 21 of 393 non-island cells
  below the 0.18 floor — unchanged from the pre-amendment measurement
  (same 21, since the ceiling change alone does not touch the seeding
  logic that produces compactness).
- Determinism: 6/6 SHA pairs matched, non-empty.
- 13/14 — up from 12/14 pre-amendment. Only `G3-G` remains.

### R3.3 Extensive seeding-repair experimentation (all reverted — documented honestly)

Per the amendment's explicit instruction (close the remaining sub-floor
slivers via seeding; do not relax `G3_COMPACTNESS_MIN`), this session
attempted 7 distinct, measured seeding-only repair strategies before
concluding none improves on the 21-violation baseline without a worse
trade-off. Every one of these was implemented, run, measured, and then
reverted — the final `steps/03_cells.py` on disk has zero net behavior
change versus the pre-session committed `HEAD` (confirmed: re-running the
proof after reverting reproduces R3.2's exact `cell_count=596` and
byte-identical determinism SHAs).

1. Widened the post-Lloyd "1.6" repair pass's seed reserve from 8 to 60
   (`effective_seed_cap = seed_count_max - 60`, giving the compactness-fix
   pass far more headroom). Measured result: `cell_count` 596 to 544,
   `G3-G` violations 21 to 23, violation rate 5.3% to 6.7%. Worse, not
   better — fewer cells everywhere (Bridson's own density-adaptive step
   starved of budget) means larger, less compact cells on average along
   the fractal coasts, more than offsetting the extra targeted fixes.
2. Reduced `G3_R_CEIL_M` (95,000 to 60,000 m), a genuine non-bound
   seeding parameter, to densify low-urban-density peripheral zones
   globally. Measured result: `G3-E` broke (max cell 48,884 km², over
   40,000) — network-wide tighter spacing competed for the same shared
   600-seed budget via Bridson's active list, starving a different,
   previously-adequately-served mass. Reverted; `G3_R_CEIL_M` stays 95,000
   (untouched, matches VictoriaProject).
3. Reserve 35 plus iterative multi-round repair (rebuild preview, find
   worst offenders, add seeds, repeat up to 8 rounds instead of one static
   pass) — discovered and fixed a real bug in the process: the round-level
   "did this round regress G3-E" guard only checked at the start of the
   next round, so a regression introduced by the very last round (the one
   that hit the seed cap) went unverified. Fixed by checking immediately
   after each round's own additions. Even after the fix, measured result:
   `G3-E` protected (0 area violations, confirmed), but `G3-G` reached 29
   violations — worse than baseline.
4. "Farthest point from existing seeds" candidate placement (9x9 grid
   search inside the offending cell's own geometry, maximizing distance to
   all current seeds) replacing plain centroid placement, hypothesizing
   this would out-perform centroid for compactness fixes. Measured: closed
   `G3-E` fully at reserve 15 and 35 (0 area violations, robust), but
   `G3-G` raw violation counts (46-49, before singleton exemption) were
   worse than the centroid-only baseline (about 24 raw) — the farthest
   point is not always the best split point for an irregular coastal
   shape.
5. Hybrid: try centroid first, fall back to farthest-point only if
   centroid is too close to an existing seed, plus area-priority severity
   ordering (area-ceiling offenders always processed before compactness
   ones within a round), plus reduced minimum spacing specifically for
   area offenders (0.15 times `r_floor` instead of 0.5 times `r_floor`,
   since a genuinely oversized cell must reach 0 violations, unlike a
   compactness sliver). This combination did successfully close `G3-E` to
   0 violations at reserve 35 (`max=37,662.6` km², confirmed via a direct
   `build_cells` check on the returned seeds, independent of the full
   proof script) — the best-performing area-fix strategy found this
   session. But `G3-G` still measured 29 true (singleton-exempt)
   violations at that configuration — again worse than the reserve 8
   baseline's 21.
6. Root-cause analysis of why more seeding does not help `G3-G`:
   `seed_lon`/`seed_lat` of every one of the 21 baseline offenders (and
   every variant's offender set) clusters in genuinely fractal coastal
   geography — Norwegian fjords (lon roughly 6-9, lat roughly 59-61),
   Scottish west-coast Highlands (lon roughly -9 to -5, lat roughly
   52-58), Aegean islands (lon roughly 22-28, lat roughly 37-38).
   Splitting an offending cell in these zones with one more seed
   frequently produces a new small sliver-shaped neighbor with comparably
   poor compactness — a real property of this specific coastline's
   fractal detail at this seed density, not a placement-algorithm gap.
   This is consistent with `constants.py`'s own pre-existing comment
   (coastal cells naturally sink toward the floor near the fractal NE
   coastline).
7. Given (1) through (6), reverted `steps/03_cells.py` to the exact
   pre-session committed `HEAD` content (copied from `git show HEAD:...`,
   not `git checkout` — destructive git operations are gated in this
   session) rather than land any of the experimentally-worse
   configurations. Re-ran the proof to confirm this reproduces R3.2's
   exact 13/14 result (identical `cell_count=596`, identical determinism
   SHAs) — it does.

This is recorded as an open finding for the Planificateur, not a formal
pigeonhole proof and not self-granted as a pass — see
`deliverables/manifest.json`'s `brief_scope_conflicts` entry for the
precise, evidence-cited claim.

### R3.4 `G3_SEED_COUNT_MAX` re-verified sufficient at the new ceiling

Not re-derived this session — the amendment's own brief.md section already
re-derives this (371 minimum at the new 40,000 km² ceiling versus 600
available, 1.62x headroom) and explicitly states no fresh re-derivation is
required. Cross-checked directly against this session's own measured
`cell_count=596` (well under 600, consistent with the amendment's
prediction that the raised ceiling would not push the count above what was
already measured at the old, tighter ceiling).

### R3.5 Frozen-scope verification (Disqualifying Failures, all clear)

All six now-frozen G3 bound constants (`G3_SEED_COUNT_MIN`,
`G3_AREA_FLOOR_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`,
`G3_AREA_EPS_M2`, `G3_OVERLAP_EPS_M2`) verified byte-value-identical to
VictoriaProject's `constants.py` (6/6). `G3_SEED_COUNT_MAX=600` unchanged,
matches Amendment 007a-R2's derivation. `G3_AREA_CEIL_KM2=40000.0`, the one
authorized change. `run_proof_g3.py` / `test_qa_red_g3.py` / `qa/checks.py`
verified 3/3 byte-identical to VictoriaProject. Neither
`G3_COMPACTNESS_MIN` nor `G3_AREA_MAX_MEDIAN_RATIO` was touched at any
point this session, including during the reverted experiments in R3.3 —
every experiment operated exclusively on `steps/03_cells.py` seeding logic
(never on a threshold), consistent with Amendment 007a-R3's explicit
prohibition.

### R3.6 A real, pre-existing disqualifying-failure risk found and fixed

Re-running the unmarked-diff-line check against the freshly-restored
`HEAD` content (R3.3 step 7) gave 2, not 0 — the same 2 unmarked blank
spacer lines checkpoint-002 had previously found and fixed in an
uncommitted session that was never actually landed in the `175f2ac`
commit. Re-applied the identical fix this session (deleted both blank
lines between marked comment blocks, zero behavior change — confirmed by
re-running the full proof: identical `cell_count=596` and byte-identical
determinism SHAs before and after the deletion).
`g3_unmarked_nonrepair_diff_line_count` re-confirmed 0 after the fix.

### R3.7 Evidence re-tracked

`git add -f` re-run on all 12 evidence files plus `constants.py` plus
`steps/03_cells.py`, reflecting the final post-Amendment-007a-R3 state
(`G3-E`/`G3-F` green, `G3-G` red, `cell_count=596`, determinism 6/6).
`deliverables/007a-r3-validation.log` holds the full final proof-run
output.

### Summary for the Évaluateur (Amendment 007a-R3)

- `G3_AREA_CEIL_KM2` = 40,000.0 exactly, the one authorized change,
  marked, single-line diff. Confirmed structurally: `G3-E` is now
  genuinely `passed: true` with real margin (`max=37,217.8` km², 6.9%
  under ceiling) — exactly the outcome the amendment's own derivation
  predicted.
- `G3-F` remains green (`ratio=3.838` versus `ceil=8.0`).
- `G3-G` remains red — 21 of 393 non-island cells below the 0.18
  compactness floor. Extensive, honestly-documented seeding-only repair
  experimentation (7 distinct configurations, real before/after numbers)
  found no configuration that improves on this baseline without either
  worsening `G3-G` itself or risking `G3-E` — a genuinely new finding
  beyond Amendment 007a-R2's own area-only pigeonhole proof, recorded as
  an open finding for the Planificateur, not self-granted as a pass.
- 13/14, not 14/14. Determinism preserved (6/6). `cell_count=596` within
  `[150,600]`. All 6 remaining frozen bounds byte-identical (6/6). Check
  definitions byte-identical (3/3).
  `g3_unmarked_nonrepair_diff_line_count=0`.
- Session's net change to the repository: `constants.py`'s one authorized
  line, plus a 2-line cosmetic (zero-behavior) fix to `steps/03_cells.py`
  that re-applies a prior session's own already-described fix that had
  not actually been committed.
