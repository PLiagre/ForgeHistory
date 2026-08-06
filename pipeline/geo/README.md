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

## Landed (brief 007, lot 007a) — G3 cells, code ported; proof currently FAILS

`pipeline.py` (top-level, byte-identical — hard runtime dependency of
`03_cells.py`'s `derive_adjacency()`, which dynamically loads it via
`importlib.util.spec_from_file_location` to reuse `stage_derive()`), plus:

- `steps/03_cells.py` — Voronoi/Poisson cell mesh, ported with exactly one
  marked path adjustment (`CITIES_JSON`/`CITY_COORDS_JSON` now resolve to
  `legacy_game_data/`, marked `# FORGEHISTORY-PATH-ADJUSTMENT`)
- `tests/run_proof_g3.py`, `tests/test_qa_red_g3.py`
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

**`tests/run_proof_g3.py` was actually executed in this repository, this
session, and exits `1` (FAIL), not `0`.** Two-run SHA256 determinism holds
(6/6 artifact pairs equal, non-empty — the algorithm is genuinely
deterministic and the port faithfully reproduces it) and all 14 QA checks
carry a non-empty `red_proof` (every red-case fires), but only 9 of 14
`checks` entries have `passed: true`. The five that fail — `G3-B`
(one land mass, `part_154`, not fully covered), `G3-D` (`cell_count=401`,
one above `G3_SEED_COUNT_MAX=400`), `G3-E` (several cells above
`G3_AREA_CEIL_KM2`), `G3-F` (`area max/median ratio=330.5` against
`ceil=8.0`), `G3-G` (several cells below `G3_COMPACTNESS_MIN=0.18`) — are
**not a porting defect**: VictoriaProject's own `artifacts/stats_g3.json`
(mtime `2026-07-29T11:07`, i.e. *after* its own `constants.py` and
`03_cells.py` were last touched, `2026-07-29T10:49`/`10:14`) already records
the identical `cell_count=401` and the identical area/compactness
distribution (`max/median=330.479417` to 6 decimals) as this port's fresh
run — proving this is the current, real, already-present output of the
unchanged algorithm against unchanged thresholds. VictoriaProject's own
`logs/v1_049_qa.json` (which shows all-green) predates both of those files
(mtime `2026-07-26T12:24`) and is stale — it was never re-generated after a
later change, so it does not reflect the current state of the source this
brief was directed to port byte-identical. Non-Goals forbid any logic
change to `03_cells.py` beyond the one marked path adjustment and forbid
touching `constants.py` at all, so this cannot be silently "fixed" within
this lot's scope — recorded here as measured fact for the Évaluateur/
Planificateur, not worked around.

`game_unity`/`StreamingAssets` audit: `03_cells.py` (unmodified beyond the
marked adjustment) carries 3 pre-existing string-literal hits — the
`RADIUS_FIELD["sources"]` metadata list (lines ~108-109) and a docstring
comment on `load_cities_readonly` (line ~179) — none of which are
path-resolution logic, all of which Non-Goals forbid touching ("any other
diff line, marked or not, must be reverted"). Combined with the two
pre-existing `constants.py` hits already on record from brief 002
(`FORBIDDEN_GAME_PATH_MARKERS`'s literals), `game_unity_reference_remaining_count`
is measured at **5**, not the required 0 — the same class of unsatisfiable-
counter conflict brief 002 hit, now recurring in the newly-ported file. See
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
