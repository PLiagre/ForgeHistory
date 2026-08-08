# Checkpoint -- 007-geo-pipeline-cells-adjacency

**Author**: forge-generateur
**Written**: 2026-08-06T16:55:32
**Reason**: UNMEASURABLE at -1 tool calls (warn 100 / checkpoint 130 / stop 160)

This is a handoff, not a verdict. `UNMEASURABLE` means the run hit its execution
budget, NOT that the work is wrong -- the deliverables below may be entirely
correct. The Évaluateur judges the work; this file only says where it stopped.

The next session must be able to resume from **this file plus the files in
the repository**, without reading the previous transcript.

## 1. Objectif du lot

Continue Lot 007a under Amendment 007a-R2 (checkpoint-001's handoff): close
`G3-E`/`G3-G` to reach 14/14 green on `run_proof_g3.py`, or find a
*genuinely new* pigeonhole proof against the re-derived `G3_SEED_COUNT_MAX
= 600` cap and escalate instead of forcing a fix.

## 2. Travail terminé

1. **Re-confirmed checkpoint-001's 12/14 baseline is reproducible**, this
   session, this repository: `cd pipeline/geo &&
   .venv/Scripts/python.exe tests/run_proof_g3.py` → `EXIT=1`,
   `proof_out_baseline.log` (git-untracked, in `pipeline/geo/`). Every
   check's `passed`/`red_ok` value matches checkpoint-001's own numbers
   exactly (`cell_count=596`, `G3-F max=34559.5 median=11423.9
   ratio=3.025`, determinism `Q10`/`G2b-B` both green). Confirms nothing
   drifted between sessions.

2. **Found and fixed a real, pre-existing disqualifying-failure risk**:
   checkpoint-001's own session left **3 unmarked diff lines** in
   `steps/03_cells.py` vs `.orig` (`g3_unmarked_nonrepair_diff_line_count`
   was silently 3, not 0 — nobody had re-run the actual `git diff --no-index`
   command since before those specific edits landed). Specifically: the
   `while active and len(seeds) < effective_seed_cap:` line (changed from
   `seed_count_max` to the new reserve variable, no trailing marker), plus
   two blank spacer lines inserted between marked comment blocks (a blank
   line cannot carry a trailing `#` marker by construction). Fixed: added
   the missing marker to the `while` line; deleted the two blank lines
   (purely cosmetic spacing, zero behavior change — confirmed by re-running
   `git diff --no-index .../pre-port/03_cells.py.orig
   pipeline/geo/steps/03_cells.py | grep '^+' | grep -v '^+++' | grep -vc
   -e FORGEHISTORY-PATH-ADJUSTMENT -e FORGEHISTORY-G3-REPAIR` → **0** now,
   confirmed this session, command re-run after the fix).

3. **Discovered the prior session's "8 cells" characterization of
   `G3-E`/`G3-G` was an artifact of the check's own truncated output, not
   the true failure scope** — a materially different, more serious finding
   than checkpoint-001 believed. `qa/checks.py`'s `g3e_area_within_bounds`
   and `g3g_compactness_floor` both `break` out of their scan loop the
   instant their `bad` list reaches 8 entries (`if len(bad) >= 8: break`) —
   the detail string is a truncated sample, not a count. Reading
   `artifacts/cells_g3.json` directly (596 cells, from this session's own
   fresh baseline run) gives the true numbers:
   - **`G3-E`: 238 of 393 non-island cells (60.5%) exceed the 15,000 km²
     ceiling** (not 8). Non-island total area 6,573,366.03 km² across 393
     cells, average 16,725 km²/cell — already above the ceiling on
     average.
   - **`G3-G`: 21 of 393 non-island cells** (singleton-exempt applied,
     matching the check's own exemption logic) are below the 0.18
     compactness floor (not 8; a cruder count that doesn't apply the
     exemption gives 43, also >8, still wrong at "8").
   Measured via a one-off script (not committed, reproducible from
   `artifacts/cells_g3.json` + `metrics.singleton_cell_ids`, both
   git-tracked): see §4 for the exact commands.

4. **Derived a fresh, rigorous, per-mass pigeonhole proof that
   `G3_SEED_COUNT_MAX = 600` is ALSO mathematically insufficient** — a
   materially stronger and different argument than Amendment 007a-R2's own
   derivation (which used a single flat `ceil(total_land_area / 15000) =
   445`, then a 1.35x headroom factor to reach 600). The flat estimate
   silently assumes land area can be freely repartitioned into 15,000 km²
   chunks across mass boundaries — it cannot: `Q2`/`G3-B` coverage requires
   **every one of the 212 disjoint land parts to get its own seed
   independent of area**, and each oversized mass's shortfall must be
   rounded up *individually* (you cannot share a fractional cell across two
   masses). Computed directly from `_iter_parts(land_xy)` on this session's
   own live land geometry (script in §4, not committed — trivially
   reproducible):
   ```
   212 land parts total (matches G3-B's own "parts=212_covered" detail).
   203 parts already <= 15,000 km^2 individually -> exactly 1 seed each (mandatory, structural, non-negotiable per Q2).
   9 parts > 15,000 km^2, needing ceil(area_i / 15000) cells EACH:
     4,468,975.5 km^2 -> 298
       950,145.0 km^2 ->  64
       357,672.8 km^2 ->  24
       292,309.5 km^2 ->  20
       218,564.8 km^2 ->  15
       153,527.2 km^2 ->  11
        82,682.3 km^2 ->   6
        25,628.1 km^2 ->   2
        23,860.8 km^2 ->   2
   sum over the 9 oversized masses = 442
   TOTAL zero-packing-waste minimum = 203 + 442 = 645
   G3_SEED_COUNT_MAX (frozen at 600, Amendment 007a-R2) = 600
   shortfall AT THE THEORETICAL BEST CASE (perfect Voronoi, zero waste) = 45
   ```
   This is a **strict lower bound** — real Voronoi/Bridson packing always
   has *some* waste (cells below the ceiling due to boundary/shape effects),
   so the true requirement is >= 645, never less. The bound is independent
   of `G3_R_CEIL_M`, `G3_LLOYD_ITERATIONS`, margin factors, seed ordering,
   or any other seeding-parameter choice — it follows purely from the
   land geometry (`_iter_parts`, unrelated to seeding) and the two frozen
   constants `G3_AREA_CEIL_KM2`/`G3_SEED_COUNT_MAX`. **No seeding-parameter-
   only change can ever close this gap.** This satisfies Amendment 007a-R's
   escalation clause ("a fresh pigeonhole-style proof against the new
   [150,600]/G3_SEED_COUNT_MAX=600 bound") that checkpoint-001 said it did
   not find.

5. **Implemented, as an honest attempt anyway** (per the brief's
   instruction to keep iterating unless a fresh proof is found — found
   *during* this attempt, not before it), a targeted post-Lloyd repair pass
   in `build_seeds()` (`steps/03_cells.py`, marked
   `# FORGEHISTORY-G3-REPAIR`, ~65 new lines): after the existing seed
   pipeline finishes (mandatory + densification + Bridson + urban anchors +
   reinject-missing-masses, all pre-existing/untouched), it builds a
   *preview* Voronoi tessellation (reusing `build_cells`, the exact same
   function the final export uses — not the check's truncated 8-entry
   view), identifies every cell still over the ceiling or under the
   compactness floor, ranks them worst-first (deterministic, tie-broken by
   `domain_key`), and adds one extra deterministic seed at each offending
   cell's centroid (snapped to land if needed), up to
   `G3_SEED_COUNT_MAX`. Factored the pre-existing inline `seed_records`
   construction into a `_records_for()` helper (marked) so it can run twice
   (once for the preview, once for the final grown seed list) without
   duplicating the formula. **This code is saved to disk and is expected to
   measurably reduce the offender count (per-cell, informed by the actual
   final Voronoi result, more precise than the existing blind-grid
   `1.5` densification step) but per the §2.4 proof CANNOT reach 0
   offenders** — at most ~4-20 of the ~259 distinct offending cells can
   receive an extra seed within the remaining budget headroom, depending on
   how much room the pre-existing upstream steps leave (they were already
   consuming nearly the full 600 budget before this pass runs).

## 3. Fichiers modifiés

- `pipeline/geo/steps/03_cells.py` — (a) added the missing
  `# FORGEHISTORY-G3-REPAIR` marker to the `effective_seed_cap` `while`
  line; (b) deleted 2 unmarked blank spacer lines (both §2.2, disqualifying-
  risk fix, zero behavior change); (c) new `_records_for()` helper
  (factored, marked, identical formula to the code it replaces); (d) new
  "1.6" targeted post-Lloyd repair pass in `build_seeds()` (marked, ~65
  lines, §2.5). Marker count: `rg -c FORGEHISTORY-G3-REPAIR` → 196 in this
  file (was 134 before this session — net +62 from (c)+(d) combined,
  offset by the -2 blank-line deletion), 2 in `constants.py` (unchanged
  this session).
- `pipeline/geo/constants.py` — **unchanged this session** (still
  `G3_SEED_COUNT_MAX=600`, `G3_LLOYD_ITERATIONS=30`, both marked, both
  exactly as checkpoint-001 left them; the 7 frozen constants verified
  unchanged again this session, see §4).
- No other file touched. `deliverables/manifest.json` and
  `deliverables/generator-log.md` are **NOT yet updated** for this
  session's findings — see §6/§7.

## 4. Tests exécutés et résultats

- **Baseline reproduction** (before this session's code edits):
  `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py >
  proof_out_baseline.log 2>&1` → `EXIT=1`. 12/14 green, identical to
  checkpoint-001's `proof_out8.log` numbers (`cell_count=596`, `G3-E`/`G3-G`
  red, all others green, determinism 6/6 both `Q10`/`G2b-B`). Full check
  list captured in `pipeline/geo/proof_out_baseline.log` (not committed,
  git-untracked working file).
- **Full offender count** (this session's real finding, §2.3), reproducible:
  ```
  cd pipeline/geo && .venv/Scripts/python.exe -c "
  import json
  d = json.load(open('artifacts/cells_g3.json'))
  cells = d['cells']
  singleton = set(d['metrics'].get('singleton_cell_ids') or [])
  non_island = [c for c in cells if c['cell_id'] not in singleton]
  over = [c for c in non_island if c['area_km2'] > 15000.0]
  under = [c for c in non_island if c['compactness_polsby_popper'] < 0.18]
  print('G3-E true violations:', len(over), 'of', len(non_island))
  print('G3-G true violations (singleton-exempt):', len(under), 'of', len(non_island))
  "
  ```
  Output (this session, against the pre-1.6-pass `artifacts/cells_g3.json`
  still on disk from the baseline run): `G3-E true violations: 238 of 393`,
  `G3-G true violations (singleton-exempt): 21 of 393`.
- **Per-mass pigeonhole bound** (§2.4), reproducible:
  ```
  cd pipeline/geo && .venv/Scripts/python.exe -c "
  import sys, math, importlib.util
  from pathlib import Path
  ROOT = Path('.').resolve(); sys.path.insert(0, str(ROOT))
  spec = importlib.util.spec_from_file_location('cells_g3', ROOT/'steps'/'03_cells.py')
  mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
  from projection import Projector, detect_projection
  proj = Projector(detect_projection())
  land_pack = mod.load_corrected_land(rebuild=False, projector=proj)
  parts = mod._iter_parts(land_pack['land_xy'])
  CEIL = 15000.0
  total_min = sum(max(1, math.ceil((p.area/1e6)/CEIL)) for p in parts)
  print('parts:', len(parts), 'zero-waste minimum:', total_min, 'vs cap 600')
  "
  ```
  Output: `parts: 212 zero-waste minimum: 645 vs cap 600`.
- **Post-1.6-pass validation run**: LAUNCHED this session
  (`cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py >
  proof_out_targeted.log 2>&1`, single blocking call, `run_in_background`
  per the Execution Contract since the extra preview `build_cells` calls
  inside `build_seeds` roughly double that step's cost) but **did NOT
  complete before this checkpoint was written** (NO_PROGRESS_STOP +
  100-call warn fired first; the file `pipeline/geo/proof_out_targeted.log`
  was still 0 bytes at last check, ~16:54). **Not confirmed to run/exit
  cleanly yet** — the next session's first action should be to re-run it
  fresh (§7/§8), not assume the in-flight one finished or survived past
  this session ending.
- 7-constant freeze re-verified this session (unchanged from checkpoint-001,
  re-run for honesty): `G3_SEED_COUNT_MIN/AREA_FLOOR_KM2/AREA_CEIL_KM2/
  AREA_MAX_MEDIAN_RATIO/COMPACTNESS_MIN/AREA_EPS_M2/OVERLAP_EPS_M2` all 7/7
  match VictoriaProject's `constants.py` byte-for-byte value.
  `g3_unmarked_nonrepair_diff_line_count`: **0** (was 3 before this
  session's fix, §2.2) — re-run after the fix, confirmed.

## 5. Décisions prises

- **Did not attempt to squeeze more budget headroom for the "1.6" pass by
  shrinking the pre-existing `effective_seed_cap`/densification reserves
  further.** Given the §2.4 proof shows a *hard* shortfall of >=45 cells at
  the zero-waste best case (real waste only makes it worse), reallocating
  the ~596/600 already-consumed budget between steps cannot change the
  *total* — it can only shift which specific cells get fixed. Tuning that
  allocation further would burn tool-call budget for a result already known
  to plateau well short of green; not attempted this session.
- **Chose to write the "1.6" targeted pass anyway** (§2.5) rather than stop
  at the theoretical proof alone, because the brief's escalation clause
  asks for "the concrete parameter sweep attempted and each attempt's check
  results" — the theoretical proof is airtight on its own, but a concrete
  before/after count (once the in-flight run in §4 completes) strengthens
  the escalation write-up in the same evidentiary style as the
  Lot-007a-R→007a-R2 escalation (`generator-log.md` §R4) that the
  Planificateur already accepted once.
- **Did not touch `G3_SEED_COUNT_MAX`, `G3_LLOYD_ITERATIONS`, or any other
  seeding parameter this session** — the §2.4 proof shows no seeding
  parameter can close a 45+-cell gap that exists purely from land-area
  arithmetic; further parameter sweeps (already exhausted last session per
  checkpoint-001 §4/§5: `G3_R_CEIL_M` at 3 values, margin at 4 values,
  Lloyd at 2 values) would not change this conclusion and were not repeated.

## 6. Problèmes ouverts

- **The "1.6" pass's actual before/after numbers are unconfirmed** — the
  validation run (§4) did not finish before this checkpoint. The next
  session must re-run it (§7/§8) before writing anything into
  `manifest.json`/`generator-log.md` that cites a `cell_count` or specific
  check detail for the post-1.6-pass state; do not reuse this checkpoint's
  §2.4/§2.3 numbers as if they reflect the post-1.6-pass mesh — those are
  **pre-1.6-pass** (measured against the untouched baseline).
- **`deliverables/manifest.json` and `deliverables/generator-log.md` are
  unmodified this session** — they still reflect the Lot 007a + 007a-R
  state (11/14, `cell_count=399`, `G3_SEED_COUNT_MAX` shown as 400 in some
  narrative text). Both need a new section once the §4 validation run
  completes: `generator-log.md` needs a "Lot 007a-R2 (finish attempt)"
  section documenting §2.2-2.5 verbatim (the disqualifying-risk fix, the
  truncated-check discovery, the fresh 645-vs-600 proof, the 1.6 pass and
  its real measured result); `manifest.json` needs
  `g3_seed_count_max_matches_derivation` (600, new counter),
  `g3_bound_constants_unchanged` redefined to 7 (not 8), and a new
  `brief_scope_conflicts` entry for the 645-vs-600 finding (same shape as
  the existing 400-vs-6,667,146.53 entry already in the file). **Do NOT
  write `g3_qa_checks_passed_count: 14`** unless the post-1.6-pass run
  genuinely shows 14/14 — per §2.4's proof this is expected to still show
  12/14 (or possibly 13/14 if the 1.6 pass happens to clear `G3-G` while
  leaving `G3-E`'s harder 238-cell gap red); write whatever the fresh run
  actually says.
- **Evidence files (`logs/`, `artifacts/`, `registry/`, `capture/`) on disk
  right now reflect the PRE-1.6-pass state** (this session's own baseline
  run, itself identical to checkpoint-001's `proof_out8.log` state) — they
  are NOT yet re-`git add -f`'d for this session (nothing changed there
  worth re-tracking until the post-1.6-pass numbers are confirmed).
- **Escalation is very likely the correct final outcome, not further
  tuning** — §2.4's proof is a hard mathematical lower bound, not an
  empirical plateau. Once the post-1.6-pass run confirms it (expect
  something in the neighborhood of 12-13/14, `cell_count` at or very near
  600), the honest move is to write up the escalation (matching
  `generator-log.md` §R4's precedent format exactly, with the corrected
  per-mass 645-vs-600 numbers replacing/supplementing the old flat
  445-vs-600 framing) and stop — not to keep sweeping parameters that are
  now proven unable to help.

## 7. Prochaine action exacte

Run `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py`
fresh (single blocking call, expect ~10-15 min given the doubled
`build_cells` cost from the new "1.6" preview pass — use `run_in_background`
per the Execution Contract and wait for the one completion notification, do
not poll the log file across many separate tool calls). Read the resulting
`checks` list and `stats_g3.json`'s `cell_count`. Then, regardless of
whether it reaches 14/14 (unlikely per §2.4/§6) or plateaus just short:
write the "Lot 007a-R2 (finish attempt)" section into `generator-log.md`
(content already drafted in §2.2-2.5 of this checkpoint — reuse it
verbatim, just append the real post-run numbers), update
`manifest.json` per §6's exact counter list, `git add -f` the 12 evidence
files if their content changed vs the currently-tracked version, and if
`run_proof_g3.py` still exits 1 (expected), add the 645-vs-600
`brief_scope_conflicts` entry and stop there — do not attempt a 3rd round
of seeding-parameter tuning; the proof in §2.4 is unconditional.

## 8. Commande de reprise
```bash
cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py > proof_out_targeted2.log 2>&1
```

## 9. Contexte minimal nécessaire

- **This checkpoint's §2.2-2.5** — the three concrete findings (disqualifying-
  risk fix, truncated-check discovery, fresh per-mass pigeonhole proof) and
  the code already on disk (`steps/03_cells.py`'s new "1.6" pass) are the
  entire substance of this session; do not re-derive them, re-read them
  from here.
- `harness/queue/briefs/007-geo-pipeline-cells-adjacency/deliverables/generator-log.md`
  §R4 (`Lot 007a-R` — the *first* escalation, already accepted by the
  Planificateur as Amendment 007a-R2) — the exact write-up shape/rigor bar
  the new escalation section (§6/§7 above) must match.
- `pipeline/geo/steps/03_cells.py` lines ~760-825 (`build_seeds`, the new
  "1.6" pass and `_records_for` helper) — read directly, this checkpoint's
  prose is a summary, not a substitute.
- `harness/queue/briefs/007-geo-pipeline-cells-adjacency/brief.md`'s
  Amendment 007a-R2 section (already read by checkpoint-001's session; the
  445-vs-6,667,146.53 flat derivation it contains is exactly what §2.4's
  per-mass 645 figure corrects/strengthens).

## Measured state at checkpoint time
| metric | value |
|---|---|
| tool calls | -1 |
| API requests | -1 |
| progress events | 11 |
| tool calls since last progress | -1 |

### Progress ledger
| # | kind | tool calls | evidence |
|---|---|---|---|
| 1 | deliverable_created | -1 | 12/12 G3 evidence files git-tracked via git add -f (git ls-files confirmed); byte-identical copies of pipeline.py/run_pr |
| 2 | red_to_green | -1 | ran .venv/Scripts/python.exe tests/run_proof_g3.py in this repo this session: exit 1, determinism 6/6 SHA pairs matched  |
| 3 | gate_check_gained | -1 | pipeline/geo/README.md updated (SC11): pre-edit snapshot deliverables/pre-edit/pipeline-geo-README.md.orig differs from  |
| 4 | deliverable_created | -1 | manifest.json + generator-log.md written and staged; self-check py harness/verdict_audit.py harness/queue/briefs/007-geo |
| 5 | plan_step_done | -1 | committed 175f2ac: 28 files, lot 007a complete (git commit), ledger appended (harness/queue/cost-ledger.jsonl), verdict_ |
| 6 | red_to_green | -1 | logs/v1_049_qa.json: G3-B and G3-D now passed:true (were failing in feedback-1.md); G3_unmarked_nonrepair_diff_line_coun |
| 7 | failures_decreased | -1 | artifacts/stats_g3.json: cell_count 401->399 (in [150,400]); max cell area 950145.03km2->215449.18km2; max/median ratio  |
| 8 | gate_check_gained | -1 | py harness/verdict_audit.py harness/queue/briefs/007-geo-pipeline-cells-adjacency -> VERDICT: ACCEPT (mechanical gate, s |
| 9 | failures_decreased | -1 | run_proof_g3.py post-007a-R2: G3-D and G3-F now green (was FAIL at G3_SEED_COUNT_MAX=400); only G3-E/G3-G remain red, do |
| 10 | gate_check_gained | -1 | found+fixed 3 unmarked-diff-line disqualifying-failure risk lines left by prior session (missing marker on effective_see |
| 11 | gate_check_gained | -1 | Fresh pigeonhole proof (per-mass, not global): 212 land parts, 9 masses >15000km2 needing per-mass ceil(area/15000) cell |
