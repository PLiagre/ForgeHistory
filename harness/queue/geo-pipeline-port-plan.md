# Geo pipeline port plan — VictoriaProject `sandbox/geo/` -> `pipeline/geo/`

**Authored**: 2026-07-29T14:00:00
**Author**: forge-planificateur

Scope of this plan: the 12 step scripts + G11/G12 QA named in
`C:\Users\liagr\VictoriaProject\sandbox\geo\README.md` (`02_coastline` through
`10_id_textures`, plus `qa/run_all.py` (G11) and `qa/crs_coherence.py` (G12)).
Three later scripts present in `sandbox/geo/steps/` —
`11_settlements_proposal.py`, `12_appearance.py`, `13_settlements_geonames.py`
— are exploratory/proposal work not named in `FORGE-HISTORY-BRIEF.md` §3's
inventory or in the README's own step table; they are **out of scope** for
this port plan entirely, not merely deferred. A future Planificateur pass may
open a dedicated brief for them once F1's core map exists, but nothing below
assumes they will be ported.

This plan groups the 12+2 in-scope steps into four sequential briefs. Only
the first (**002**) is fully specified (`002-geo-pipeline-coastline-1400/`).
The other three get a topic/scope paragraph only — their Success Conditions
get written by a future Planificateur pass once the prior brief's real,
on-disk artifacts exist to cite (per hard-won rule: a rubric written before
the cited artifact exists is a rubric written from memory of intent, exactly
the defect §2.7 diagnoses).

## Brief 002 — shared infrastructure + G2 littoral 1400 (coastline)

**Fully specified**: `harness/queue/briefs/002-geo-pipeline-coastline-1400/`.

Ports the five shared modules every later step imports
(`constants.py`, `io_util.py`, `projection.py`, `sources.lock`,
`requirements.txt`) plus the two G2 steps that only depend on that shared
infrastructure and on Natural Earth's `10m_physical.zip`: `02_coastline.py`
(real 1400-window coastline from Natural Earth) and
`02b_corrections_1400.py` (declared, reversible corrections layer — e.g.
lake-to-open-sea reclassification). Does not touch `03_cells.py` onward.
Surfaces and resolves (by copying, not redesigning) a load-bearing porting
problem: `constants.py`'s pilot-window derivation and `02_coastline.py`'s
game-comparison capture both hard-depend, at import/call time, on two
VictoriaProject Unity gameplay data files
(`game_unity/Assets/StreamingAssets/data/province_coordinates.json` and
`.../province_adjacency.json`) that do not exist anywhere in ForgeHistory —
see brief 002 for the exact, minimal, marked path adjustment required.

## Brief 003 — G3 cells + G4 adjacency (the grain-defining step)

**Topic only.** Ports `03_cells.py` (Voronoi cell mesh over the corrected
1400 coastline — this is the ~401-cell grain the project owner has already
decided to keep as-is, not re-derive) and `04_adjacency.py` (sea-zone
carve-up + typed land-land/land-sea/sea-sea adjacency, including the
Zuiderzee/Afsluitdijk topology-link special case). Depends on 002's
`artifacts/coastline_1400.json` and the shared infrastructure. Confronts the
**same class** of legacy-game-data dependency found in 002, at larger scope:
`03_cells.py` reads `game_unity/.../cities.json` and
`.../city_coordinates.json` read-only (city positions bias the Voronoi seed
density), and `04_adjacency.py` additionally reads
`.../sea_zones.json`, `.../province_adjacency.json`,
`.../province_coordinates.json` read-only (named-sea-box attribution and a
legacy-comparison pass). The brief covering this must decide, explicitly,
whether each of these is copied byte-identical into
`pipeline/geo/legacy_game_data/` (matching 002's precedent) or whether any
of them is no longer needed given ForgeHistory has no live `game_unity/`
project to compare against — this is a real decision, not just a copy list,
and must not be silently resolved.

## Brief 004 — G5/G5b/G5c rivers + G6 relief (Copernicus DEM)

**Topic only.** Ports `05_rivers.py` (Natural Earth
`ne_10m_rivers_lake_centerlines`, navigability proxied from `scalerank`),
`05b_navigability_1400.py` (declared navigability overrides for named ports),
`05c_rivers_europe.py` (merges `ne_10m_rivers_europe`, Hausdorff-bounded
dedup against G5), and `06_relief.py` (Copernicus DEM GLO-90 sampling —
elevation/slope/roughness per cell, named historical mountain passes).
Depends on 003's cells + adjacency. Introduces a new, large, legally-cleared
binary dependency not present in 002/003: the Copernicus DEM COG tile set
(179 tiles, ~644 MB total, `sources.lock`'s `dem.tiles` block already carries
every tile's individual SHA256 plus the mandatory ESA/DLR/Airbus attribution
text) — the brief covering this must decide how that volume of binary data
is carried into the new repository (git, git-lfs, or an out-of-repo cache
directory referenced by path) rather than silently assuming plain git add.

## Brief 005 — G7 cities, G8 ownership, G9 LOD, G10 id textures, G11/G12 QA closure

**Topic only.** Ports `07_cities.py` (attaches cities to cells, cross-checks
against geography), `08_ownership.py` (derives a per-cell owner tag —
**flagged conflict with ADR-0003, see below**), `09_lod.py` (topological
arc-simplification LODs 0/1/2), `10_id_textures.py` (rasterized cell-id
identifier textures, the actual artifact the future Unity client reads), and
finally wires up the whole-chain QA: `qa/run_all.py` (G11 — replays 02->10
twice, SHA256-compares every artifact, audits every named check) and
`qa/crs_coherence.py` (G12 — cross-cutting CRS-consistency check). This is
the brief that closes the port: after it, `pipeline/geo/` reproduces
everything `FORGE-HISTORY-BRIEF.md` §3 calls "the pipeline," at full
whole-chain determinism. Depends on 003 (cells/adjacency) and 004 (relief,
for `09_lod`'s and `10_id_textures`'s cell geometry inputs) — `08_ownership`
does not itself depend on relief, so a future Planificateur may choose to
split this into two briefs (G7/G8 vs G9/G10/G11/G12) once 004's artifacts
exist and the true size of this remaining work is visible; that decision is
explicitly deferred, not made here.

### Flag: `08_ownership.py` conflicts with ADR-0003 — must be confronted, not silently ported

Read in full at
`C:\Users\liagr\VictoriaProject\sandbox\geo\steps\08_ownership.py`. Findings:

1. It reads `game_unity/Assets/StreamingAssets/data/provinces.json`
   read-only and takes each entry's `owner_tag`, keyed by a numeric
   `province.id` (1..~50) — this is VictoriaProject's legacy `ProvinceId`,
   the exact identifier ADR-0003 exists to retire as a spatial primary key.
2. It aggregates `cell_id -> province_id` via a **bounded nearest-neighbor**
   rule (`reuse_aggregation`, distance bound 180 km, reusing
   `qa/compare_legacy.py`'s `classify_provinces` / `aggregate_cells`) — cells
   are assigned **to** a pre-existing, fixed enumeration of provinces by
   distance. This is the *opposite* direction from ADR-0003's decision that
   "Province is always a derived aggregation of which cells currently
   compose it" (ADR-0003, `## Decision`): here, province is the fixed input
   and cell membership is derived from it by proximity, not the other way
   around.
2b. A second, independent path (`_city_owner_by_cell`) resolves owner via
   each attached city's own `city["province_id"]` field (also read from
   legacy `game_unity` data) and the two paths are cross-checked for
   concordance (`both_concordant` / `both_divergent`) — i.e. the script's
   own logic already treats "owner via province_id" and "owner via
   city.province_id" as two independently-derived answers to "who owns this
   cell" that can (and in the data, do) disagree. That is failure mode #1's
   shape, reproduced inside the port candidate itself, not merely adjacent
   to it.
3. The output (`artifacts/ownership_1400.json`, `registry/ownership_registry_g8.json`'s
   `cell_to_owner`) is keyed by `cell_id` and carries only an `owner_tag`
   string (no `province_id` persists downstream) — so the *artifact* is
   ADR-0003-compatible on its face (cell-keyed, no stored ProvinceId
   surviving into the output). The conflict is entirely in the **derivation
   path**: the input format (`provinces.json`'s fixed enumeration) and the
   NN-with-bound aggregation method assume province is prior to cell, which
   is the reverse of what ADR-0003 decided.

**What the brief covering G8 must confront explicitly** (not resolve here —
this is a topic flag for a future Planificateur, per this task's scope):
whether "owner tag per cell" for the F1 pilot map can be re-derived through
a method that never treats `provinces.json`'s numeric `id` as spatially
authoritative (e.g., treating "province" purely as a same-owner-cell
aggregate computed *after* ownership is assigned directly per cell by some
other means), or whether the existing NN-bounded method is retained
verbatim as a one-time, clearly-labeled **input transform** whose output
(the `cell_id -> owner_tag` mapping) is the only thing that survives into
`pipeline/geo/`, with the `provinces.json` numeric-id enumeration itself
never copied into ForgeHistory and never exposed as an ID any other part of
the new repository can read or write. Either answer is legitimate; silently
porting the script unchanged, importing legacy `ProvinceId` handling into a
repository whose whole `sim/` tree exists specifically to never have one, is
not.
