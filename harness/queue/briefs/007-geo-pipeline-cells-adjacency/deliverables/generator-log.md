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

## Summary for the Évaluateur

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
