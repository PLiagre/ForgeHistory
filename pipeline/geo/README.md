# pipeline/geo/

Ports VictoriaProject's `sandbox/geo/` map pipeline into ForgeHistory.
Licenses in `sources.lock` (Natural Earth public domain, GeoNames CC BY 4.0,
Copernicus DEM attribution-required) are carried over unchanged — do not redo
that legal work.

## Landed (brief 002)

Shared infrastructure every later step imports:

- `constants.py`, `io_util.py`, `projection.py`, `requirements.txt`, `sources.lock`
- `qa/checks.py` (shared QA module, ported wholesale)
- `legacy_game_data/` — read-only copies of VictoriaProject province
  coordinates/adjacency fixtures (no Unity project tree in this repository)
- Natural Earth `sources/10m_physical.zip`

G2 littoral-1400 cluster:

- `steps/02_coastline.py` — coastline from Natural Earth in the pilot window
- `steps/02b_corrections_1400.py` — declared, reversible 1400-era corrections
- proof scripts `tests/run_proof_g2.py` / `tests/run_proof_g2b.py` and their
  red-case companions

## Landed (brief 007, lot 007a, Amendment 007a-R) — G3 cells, genuinely repaired mesh

`pipeline.py` (top-level, byte-identical — hard runtime dependency of
`03_cells.py`'s `derive_adjacency()`, which dynamically loads it via
`importlib.util.spec_from_file_location` to reuse `stage_derive()`), plus:

- `steps/03_cells.py` — Voronoi/Poisson cell mesh, ported with exactly one
  marked path adjustment (`CITIES_JSON`/`CITY_COORDS_JSON` now resolve to
  `legacy_game_data/`, marked `# FORGEHISTORY-PATH-ADJUSTMENT`) **plus a
  genuine seeding/construction repair** (marked
  `# FORGEHISTORY-G3-REPAIR`, 21 lines) — this is not merely a byte-identical
  port of a degenerate source; the mesh's seed-placement logic was diagnosed
  and repaired against the current (post-brief-002) coastline. See
  "G3 repair (Amendment 007a-R)" below for the full before/after.
- `tests/run_proof_g3.py`, `tests/test_qa_red_g3.py` — **byte-identical to
  VictoriaProject, untouched**; the quality bar was not weakened
- `legacy_game_data/cities.json`, `legacy_game_data/city_coordinates.json`
  (read-only, SHA256-equal to VictoriaProject's own recorded
  `MANIFEST_g3.json`/`MANIFEST_g4.json` `inputs` target hashes) — the
  declared, non-optional input to G3's seed-density field `r(x)`

**Legacy game-data decisions recorded (per brief 007's table), all five
files named explicitly, none silently skipped:**

| file | decision | status this lot |
|---|---|---|
| `cities.json` | copy byte-identical | done, SHA target-matched |
| `city_coordinates.json` | copy byte-identical | done, SHA target-matched |
| `sea_zones.json` | copy byte-identical | **not this lot** — lot 007b |
| `province_adjacency.json` | reuse brief 002's copy, re-verified unchanged | re-verified this session, SHA unchanged since 002 |
| `province_coordinates.json` | reuse brief 002's copy, re-verified unchanged | re-verified this session, SHA unchanged since 002 |

### G3 repair (Amendment 007a-R)

The original port (see `verdict-007a.md`/`feedback-1.md`) faithfully
reproduced VictoriaProject's own current, un-stale `03_cells.py` output on
the post-brief-002 coastline: `cell_count=401`, one land mass uncovered
(`part_154`, a 554 m² coastline-digitization sliver), and one 950,145 km²
single-seed "giant cell" (max/median area ratio 330.5, ceiling 8). Root
cause, diagnosed before any edit: `_poisson_variable_radius()`'s two
unconditional seeding steps — 1 mandatory seed per land mass (213 masses)
plus up to 198 urban-anchor seeds — could together reach or exceed
`G3_SEED_COUNT_MAX=400` **before the density-adaptive Bridson step ever
ran**, starving large, low-city-density land masses (Morocco/Algeria/Tunisia
within the pilot window) of any real subdivision and leaving them as a
single whole-mass cell.

Two `# FORGEHISTORY-G3-REPAIR`-marked changes, no constant touched:

1. `_iter_parts()` excludes fragments whose own area is `<= G3_AREA_EPS_M2`
   (10,000 m²) from the enumerated land masses — such a fragment can never
   pass `G3-B` standalone (even a whole-fragment cell would measure below
   the check's own coverage epsilon) and is geometrically insignificant
   coastline noise, not an addressable place. Fixes `G3-B` and reduces the
   forced-seed count by one (`213→212` masses).
2. `_poisson_variable_radius()` runs the density-adaptive Bridson step
   **before** urban-anchor placement (previously after), so the seed budget
   left after the one-per-mass mandatory pass is spent according to `r(x)`
   (which already encodes urban density via `density_at()`) across *all*
   masses, not consumed by the unconditional anchor pass first. Urban
   anchors are placed last, capped by remaining budget.

Result, same coastline, same master seed, same eight bound constants
(verified unchanged): `cell_count` 401→**399** (now within `[150,400]`),
`G3-B`/`G3-D` now `passed: true`, largest single cell 950,145→**215,449
km²**, max/median area ratio 330.5→**58.4**. `G3-A`, `G3-B`, `G3-C`, `G3-D`,
`G3-H`, `Q1`-`Q4`, `Q10`, `G2b-B` all `passed: true` with non-empty
`red_proof`. Two-run determinism holds (6/6 SHA pairs matched, non-empty).

**`G3-E`/`G3-F`/`G3-G` remain `passed: false` — proven mathematically
unsatisfiable by any seeding-parameter-only change, not an unfixed defect.**
The pilot window's actual land area (`land_xy.area` = 6,667,146.53 km²,
verified) combined with the frozen `G3_SEED_COUNT_MAX=400` and
`G3_AREA_CEIL_KM2=15,000` km² gives a hard ceiling on total coverable area
of `400 × 15,000 = 6,000,000` km² — **667,146 km² short of the real land
area**. Since `Q2` (no-holes, frozen, currently passing) requires the cells
to tile essentially all of the land, the sum of cell areas must equal
~6,667,146 km²; by the pigeonhole principle, at least one cell must exceed
15,000 km² regardless of algorithm. This is not a coastline bug: the
pilot window (`PILOT_WINDOW_LONLAT` = `(-11.32, 29.7, 34.82, 61.5)`,
derived from `province_coordinates.json`) genuinely spans that much real
land (Western/Northern/Central Europe + the Maghreb Mediterranean coast).
`G3_SEED_COUNT_MAX`/`G3_AREA_CEIL_KM2` were not recalibrated when brief
002's coastline refinement widened the window. **Escalated to the
Planificateur** per Amendment 007a-R's required path (evidence: the
pigeonhole computation above, plus an empirical parameter sweep varying
`G3_R_CEIL_M` — a non-bound seeding parameter — which could not relieve
the shortage; see `deliverables/generator-log.md` §"Lot 007a-R" for the
full sweep). This claim is not self-granted as a pass.

`tests/run_proof_g3.py`, `tests/test_qa_red_g3.py`, `qa/checks.py` remain
byte-identical to VictoriaProject (SHA-verified, 3/3); the eight named G3
acceptance-bound constants are unchanged in value (verified, 8/8); no
seeding-parameter constant in `constants.py` was changed (0 lines marked
there — the repair was seeding-*logic* only).

`game_unity`/`StreamingAssets` audit: `03_cells.py` (unmodified beyond the
marked path adjustment and the repair, neither of which touches the
`RADIUS_FIELD["sources"]` metadata list at lines ~108-109 or the docstring
at line ~179) carries the same 3 pre-existing string-literal hits as the
original port. Per Amendment 007a-R's Amended Success Condition 6, these
three lines plus `constants.py`'s two existing `FORBIDDEN_GAME_PATH_MARKERS`
literals are now a **permanent, unconditional exception** (independent of
whether a repair happens to touch them — it did not).
`game_unity_reference_remaining_count` = **0** after this five-literal
exclusion (raw hits = 5, all excluded). See
`harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/manifest.json`
for the full accounting.

## Not yet landed

- `steps/04_adjacency.py` (sea zones + typed adjacency) — brief 007, lot 007b
- rivers (`05` / `05b` / `05c`), relief / Copernicus DEM (`06`)
- cities (`07`), ownership (`08`), LOD (`09`), id textures (`10`)
- whole-chain QA (`qa/run_all.py`, `qa/crs_coherence.py`)

ADR-0003 (`docs/adr/0003-single-spatial-primary-key.md`) unblocked writing
here; brief 002 landed the shared infra + G2 littoral-1400 cluster; brief
007 lot 007a lands the G3 cell-mesh code and its evidence (proof currently
failing per above, not silently marked green).
