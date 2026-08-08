# Run Report — 007-geo-pipeline-cells-adjacency, Lot 007a (G3 cells)

**Backend**: claude
**Iterations**: 1
**Score history**: [FAIL]  (mechanical gate structurally ACCEPT once verdict.md exists; substantive Évaluateur verdict FAIL)
**Outcome**: **ESCALATED — brief-premise defect** (not a Générateur defect, not fixable by iteration)

## One-line

The G3 port is *faithful and correct*, but the thing it faithfully reproduces —
VictoriaProject's committed G3 mesh — is itself **degenerate** (401 cells, a
giant uncovered/oversized cell), and VictoriaProject's "all-green" proof log is
**stale**. A byte-identical port (which the brief mandates) therefore cannot
satisfy Success Condition 7. This needs an owner/Planificateur decision, not
another Générateur pass.

## What the Générateur delivered (all correct)

- `pipeline/geo/pipeline.py` — byte-identical to VP (the load-bearing runtime dep of G3).
- `pipeline/geo/steps/03_cells.py` — byte-identical except the single marked
  `# FORGEHISTORY-PATH-ADJUSTMENT` (unmarked-diff = 0).
- `run_proof_g3.py`, `test_qa_red_g3.py`, `cities.json`, `city_coordinates.json`
  — byte-identical; legacy SHA targets matched (2/2); brief-002 province files
  re-verified unchanged (2/2).
- Evidence force-added into git (12 files) — correctly fixing brief 002's own
  unresolved "Defect C" via a consistent mechanism.

## Independently confirmed by the Évaluateur (verdict-007a.md)

1. **Port is faithful**: SHA256-equal copies; only the marked path lines differ.
2. **Port reproduces VP's *current* artifacts exactly**: this repo's
   `stats_g3.json` `cell_count=401`, `paris_basin.cell_count=3`, identical area
   distribution to VP's own on-disk `stats_g3.json`.
3. **VP's green log is provably stale**: VP's `logs/v1_049_qa.json` marks G3-D
   ("cell count in [150,400]") *passed* while VP's `stats_g3.json` reports 401.
   A fresh run regenerates both together, so their disagreement proves the log
   predates the current mesh (log dated 2026-07-26; VP constants/code 2026-07-29).
4. **Libraries identical** (shapely 2.1.2 / numpy 2.5.1 / GEOS 3.13.1) — no
   environment drift.
5. The capture `v1_049_cells_window.png` visibly shows the degenerate mesh.

## Root cause (orchestrator archaeology on VictoriaProject git)

The 5 failing checks are all mesh-quality checks (G3-B coverage, G3-D count,
G3-E cell area, G3-F max/median ratio, G3-G compactness). VP's recent
`constants.py` edits are all P2 city-matching / A12 hillshade — **not** G3 seed
params. The mesh degraded because the **coastline input was refined after
v1_049's green G3 run** (e.g. the v1_064 CRS-coherence fix and correction
layers), while the G3 proof was never re-run/re-committed. Brief 002 faithfully
ported that newer coastline; fed to the current `03_cells.py` it deterministically
yields the degenerate 401-cell mesh.

## Why no iteration-2 was run

SC6 and SC7 are both **blocked pending a decision**, not fixable by a Générateur
without breaching Non-Goals (which forbid any logic change to `03_cells.py` /
`constants.py` beyond the one marked path adjustment). Per harness discipline,
replaying the Générateur on an unsatisfiable premise is exactly what the
plateau/escalation rule exists to prevent.

## Decision required (owner / Planificateur) — options

| # | Option | Effect | Cost |
|---|---|---|---|
| A | **Fix G3 mesh in ForgeHistory** — amend brief 007a from "faithful port" to "port + repair degenerate seeding so the mesh is clean and ≤ its declared bounds" | ForgeHistory gets a *good* map (serves the F1 goal) | Real Voronoi-seeding design work; VP itself hasn't solved it |
| B | **Re-baseline SC6/SC7** to accept VP's current output as ground truth (brief-002-style carry-forward/plateau) | 007a "passes" | Ships a visibly degenerate map (giant/uncovered cells) — the owner judges the map |
| C | **Repair G3 upstream in VictoriaProject first, then re-port** | Clean port later | VP is read-only for this repo; VP's tree is already dirty/non-compiling |
| D | **Port an earlier green VP commit's G3 + its matching coastline** | Green proof | Contradicts "port HEAD" guidance; mixes coastline versions (would also revert brief 002's coastline) |

**Orchestrator recommendation**: Option **A**. The roadmap's purpose is a
beautiful, functional map; faithfully porting a broken mesh (B/D) defeats it, and
C is out of bounds (VP read-only). A turns 007a into a genuine improvement, but
it is a scope change from "port" to "fix", so it needs the owner's go-ahead and a
Planificateur amendment defining the acceptable mesh criteria (coverage, cell
count band, max/median area ratio, compactness floor) — the same checks G3-B..G3-G
already encode.

## Artifacts

- verdict: `verdict.md` / `verdict-007a.md` (Évaluateur, FAIL with full reconstruction)
- feedback: `feedback/feedback-1.md` (states the blocker is a brief amendment, not an iteration)
- deliverables: `deliverables/generator-log.md`, `manifest.json`
- brief: `brief.md` (note the pre-generation timestamp amendment)

## Also note

Brief 007's own SC7 was written by the Planificateur citing VP's green
`v1_049_qa.json` as if it were current — the Planificateur did not verify the
source was *actually* green today. That is the upstream lesson: a rubric must
cite a re-run proof, not a committed proof artifact (hard-won rule — presence is
not function; a stale green log is presence).
