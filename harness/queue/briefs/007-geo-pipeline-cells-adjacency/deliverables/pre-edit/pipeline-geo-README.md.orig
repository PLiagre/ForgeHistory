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

## Not yet landed (brief 003 onward)

Per `harness/queue/geo-pipeline-port-plan.md`, the following are **out of
scope for this installment** and must not be assumed present:

- `steps/03_cells.py` (Voronoi cells)
- `steps/04_adjacency.py` (sea zones + adjacency)
- rivers (`05` / `05b` / `05c`), relief / Copernicus DEM (`06`)
- cities (`07`), ownership (`08`), LOD (`09`), id textures (`10`)
- whole-chain QA (`qa/run_all.py`, `qa/crs_coherence.py`)

ADR-0003 (`docs/adr/0003-single-spatial-primary-key.md`) unblocked writing
here; brief 002 is the first installment that actually lands runnable geo
code.
