# Run Report — 007a, Amendment 007a-R2 (recalibrate G3 bounds → finish)

**Outcome**: **STOPPED — map-grain design decision required (owner)**. Not a
REJECT, not a Générateur defect. Two budget checkpoints + a verified structural
finding that the recalibration target itself was too low.

## What happened

Owner chose "Option a" (amend the G3 bounds to the current coastline, finish
007a). Executed:

1. **Planificateur Amendment 007a-R2**: raised `G3_SEED_COUNT_MAX` 400→600,
   derived as `ceil(6,667,146/15,000)=445 × 1.35 headroom`. All quality bounds
   (`G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN`, …) kept
   frozen. (`checkpoint-001.md`, commit `1807574`.)
2. **Générateur (finish)**: with the raised cap + targeted seeding fixes, moved
   the mesh from 5/14 → **12/14** green, `cell_count=596`, determinism 6/6.
   Budget checkpoint.
3. **Générateur (continuation)**: found the 600 target was itself wrong.
   (`checkpoint-002.md`, commit `766ce39`.)

## The verified structural finding (orchestrator re-checked all of it)

- **The QA violation list truncates at 8** (`qa/checks.py:365,434` — `break` at
  8). "8 cells over ceiling / 8 slivers" was a *display artifact*.
- **True state: 238 of 596 cells exceed the 15,000 km² ceiling** — 40% of the
  mesh, never "small margins".
- **Rigorous lower bound**: subdividing every current cell to ≤15,000 km² needs
  **≥ 837 cells** (realistically ~900 with Voronoi packing waste), because the
  coastline is 6,667,147 km² across ~212 separate land parts (each part needs
  whole cells). The naive `land/ceil = 445` used to derive the 600 cap ignored
  per-part granularity.
- Therefore **`G3_SEED_COUNT_MAX = 600` is still provably insufficient**; the
  earlier "pigeonhole resolved" note was resolved only against the wrong (445)
  figure.

## Why this is now the owner's call, not another auto-recalibration

The refined coastline + the **15,000 km² max-cell quality bound** + the brief's
own stated **"~150-400 addressable cells"** grain are **mutually incompatible**.
A clean uniform mesh needs roughly **double** the designed grain (~850-900
cells). That reshapes the game map and contradicts the stated design intent — a
decision the owner owns, especially after the first auto-derived target (600)
proved wrong. Auto-bumping the cap again would compound a design choice silently.

## Options

| # | Option | Effect | Cost / caveat |
|---|---|---|---|
| 1 | Raise `G3_SEED_COUNT_MAX` to ~900, keep 15,000 km² quality | Clean, uniform-quality mesh | ~2× the designed map grain (finer map, more cells/compute); contradicts "~150-400" |
| 2 | Relax `G3_AREA_CEIL_KM2` (bigger cells allowed) | Keeps ~400-600 grain | Lowers the quality bar — big cells in low-interest regions (Maghreb) |
| 3 | Trim the coastline / exclude low-interest masses | Fewer cells needed | Reverts/changes brief 002's landed coastline; changes geographic scope |
| 4 | Non-uniform grain (fine core, coarse periphery) | Best of both | Needs a QA-check redesign (currently byte-identical to VictoriaProject) |

**Orchestrator lean**: Option 1 if you want a uniform-quality map and are fine
with a finer grain than originally sketched; Option 4 is the "right" long answer
but is a real redesign. Either way it's your map-vision call.

## Repo state (honest, not pushed to master)

007a commits (`1807574`, `766ce39`) sit on `forge/cursor-audit-loop`. Mesh
displays 12/14 but 238 cells are over ceiling; `deliverables/checkpoint-002.md`
is the handoff; `manifest.json` / generator-log "007a-R2" section deliberately
not finalized (would be finalized only at green). Determinism held throughout.
Nothing faked; nothing broken pushed.

## Meta-lesson (recorded)

The brief's premise chain was wrong twice — first a stale committed green log,
then a naive area/ceiling estimate. Bounds must be derived from the real
geometry (per-part cell demand), never from a committed proof artifact or a
back-of-envelope average. Presence is not function.
