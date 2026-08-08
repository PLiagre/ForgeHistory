# Feedback — Brief `007`, Lot `007a`, iteration 1

**Author**: forge-evaluateur
**Authored**: 2026-08-06T09:47:00

Overall verdict: **FAIL**, but read this carefully — **most of the lot is
clean, and the two failing conditions are NOT yours to fix.** Do not open
`03_cells.py` or `constants.py` in an iteration-2. Doing so would violate the
brief's Non-Goals and would be the first real defect in this lot.

## Failing rubric lines, verbatim, with reproduction

### `SC7` — "`run_proof_g3.py` runs in this repository, this session, and exits 0"; "G3 cell count within declared bounds"

Reproduced independently:

```
cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py   → exit 1
logs/v1_049_qa.json : 14 checks, 9 passed, failing = G3-B,G3-D,G3-E,G3-F,G3-G
artifacts/stats_g3.json : cell_count = 401  (G3_SEED_COUNT_MAX = 400)
```

Root cause established by the Évaluateur, not taken on faith:

- Port is byte-identical (pre-port `.orig` SHA == VictoriaProject original;
  unmarked diff 0).
- This repo's `stats_g3.json` matches VictoriaProject's own current
  `stats_g3.json` exactly on `cell_count = 401`, `paris_basin`, and area
  distribution (only absolute cell-id labels differ — informational per the
  rubric).
- VictoriaProject's committed `logs/v1_049_qa.json` claims G3-D passed while
  its own `stats_g3.json` records `401`; its qa log
  (`2026-07-26T12:24:01`) predates its constants.py / `03_cells.py`
  (`2026-07-29`), which predate its `stats_g3.json` (`2026-07-29T11:07:40`).
  Its green log is stale; a fresh run of VictoriaProject's committed code
  would fail identically.
- Both venvs: `numpy 2.5.1`, `pyproj 3.7.2`, `shapely 2.1.2` — no drift.

**Can iteration-2 fix this? NO.** `SC7` is unsatisfiable by a byte-identical
port because VictoriaProject's G3 source is not actually green. **Blocked
pending a Planificateur amendment / owner decision**: either re-baseline
`SC7` against VictoriaProject's real current output, or repair G3 upstream
and re-attest a green source first (out of this brief's scope). Do not
attempt to force exit 0 by editing mesh logic or thresholds.

### `SC6` — "No remaining `game_unity`/`StreamingAssets` reference … `game_unity_reference_remaining_count` == 0 after exclusions"

Reproduced: the `.py`-file hits are 5 —
`constants.py:561-562` (untouched `002`-scope), `03_cells.py:108`,`:109`
(`RADIUS_FIELD` sources metadata), `:179` (docstring). All pre-existing in
the byte-identical VictoriaProject original.

(Minor accuracy note for your log next time: your generator-log said `8` raw
hits; the true raw count is `12` — you missed `README.md:77`, `README.md:84`,
`artifacts/cells_g3.json:1`, `build/02b_divergences_1400.json:1`. All four are
non-`.py` and excludable, so your **counter value of 5 is still correct** —
but state the real raw count.)

**Can iteration-2 fix this? NO.** Removing those literals is an unmarked,
non-path-adjustment diff line the Non-Goals forbid. `SC6`'s `0` target and the
byte-identical-port mandate are mutually unsatisfiable. **Blocked pending a
Planificateur amendment** to carve these pre-existing non-path literals into
the traceable-exception scope (same resolution `002` applied to
`FORBIDDEN_GAME_PATH_MARKERS`).

## What you did right (keep doing this)

- Byte-identical copies and the single marked path adjustment: exemplary,
  fully reproduced (5 / 5, marker 2, unmarked diff 0, target hashes 2 / 2).
- Evidence force-added and tracked (`12` files) after correctly diagnosing
  that `002`'s declared evidence was never actually tracked — consistent
  mechanism, real improvement over `002`.
- Truthful README and honest counters: you reported exit `1` and 9 / 14 green
  openly instead of forcing a false green. That is exactly correct under the
  rule that the producer does not pronounce its own work acceptable.

## Bottom line

No Générateur re-run until an amendment lands. This FAIL is on the brief's
premise, not on your execution.
