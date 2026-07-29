# pipeline/geo/

Will hold what's ported from VictoriaProject's `sandbox/geo/` pipeline
(16 steps, 35 tests, deterministic, rejayable, SHA256 two-pass QA), including
its `sources.lock` and attributions (Natural Earth, GeoNames, Copernicus
DEM — licenses already vetted, do not redo that legal work). F1 scope —
empty stub for F0. F1 begins with an ADR deciding the single spatial primary
key (`docs/rules/simulation-principles.md` failure mode #1, recommendation:
the geographic cell is the key, the province is an aggregation of cells)
before any code lands here.
