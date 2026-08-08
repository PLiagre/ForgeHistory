# Checkpoint -- 007-geo-pipeline-cells-adjacency

**Author**: forge-generateur
**Written**: 2026-08-06T16:30:51
**Reason**: UNMEASURABLE at -1 tool calls (warn 100 / checkpoint 130 / stop 160)

This is a handoff, not a verdict. `UNMEASURABLE` means the run hit its execution
budget, NOT that the work is wrong -- the deliverables below may be entirely
correct. The Évaluateur judges the work; this file only says where it stopped.

The next session must be able to resume from **this file plus the files in
the repository**, without reading the previous transcript.

## 1. Objectif du lot

Finish Lot 007a under Amendment 007a-R2: with `G3_SEED_COUNT_MAX` re-derived
400→600, actually earn all-green on `run_proof_g3.py` (G3-A..H, Q1-4, Q10,
G2b-B) by tuning seeding/construction logic — not by the raised ceiling
alone.

## 2. Travail terminé

`G3_SEED_COUNT_MAX` changed 400→600 in `constants.py`, marked
`# FORGEHISTORY-G3-REPAIR`, exactly as Amendment 007a-R2 derives (single
line, no other bound touched). Five additional marked seeding-logic changes
were made to `steps/03_cells.py` (see §3/§5) to actually use that headroom.
**Current best measured state (12/14 checks green + red-proven,
determinism 6/6, count in range):**

```
Q1 True   Q2 True   Q3 True   Q4 True   Q10 True
G3-A True G3-B True G3-C True G3-D True
G3-E False (8 cells, 15062-19134 km2, ceil=15000 -- was 1-2x over ceiling)
G3-F True (max=34559.5 median=11423.9 ratio=3.025, ceil=8.0)
G3-G False (8 cells, pp 0.053-0.174, floor=0.18)
G3-H True G2b-B True
cell_count = 596 (in [150,600])
```

This is a large, real, measured improvement over the Amendment-007a-R
baseline (5/14 failing, `G3-D`/`G3-E`/`G3-F` all red, giant cell up to
950,145→215,449 km² after the first repair, max/median ratio 330→58) — now
only `G3-E` and `G3-G` remain, both by a modest margin (not the pigeonhole-
scale impossibility the original 400-cap escalation raised — that
escalation is resolved; this is now ordinary seeding-quality tuning, per
the eval-rubric's plateau note that a fresh iteration reverts to "ordinary
Générateur/design defect").

**Run 8 full `run_proof_g3.py` executions this session** (each a single
blocking call, several via `run_in_background` per the Execution Contract
since Lloyd=20/30 pushed wall time past 5 min), sweeping: `G3_R_CEIL_M`
(95k/60k — reverted, see §5), an area-driven coverage-densification step
(margin factor 0.3/0.4/0.42/0.45/0.5 — settled on 0.5), `G3_LLOYD_ITERATIONS`
(10/20/30 — settled on 20, kept in constants.py at 20; the 30-run's log is
`proof_out8.log` but the committed state is Lloyd=20, see §6), an
`effective_seed_cap` reserve for the pre-existing unmarked "reinject missing
mass" step, an ascending-vs-descending mass-processing order, and a
per-mass minimum-radius override for both the mandatory seed and the
densification candidates of any oversized mass (this was the fix for a
specific reproducible bug: a small oversized mass's own subdivision was
blocked by a *neighbouring* mass's mandatory seed still claiming a ~93km
low-density exclusion radius — diagnosed via a standalone `build_seeds()`
debug script, root-caused, fixed, verified gone).

**Not yet done**: G3-E and G3-G are not green. `run_proof_g3.py` still
exits 1. Evidence files from this session's runs are NOT yet re-tracked via
`git add -f` (the committed `logs/v1_049_qa.json` etc. reflect the PRIOR
(007a-R) repair state, not this session's runs) — do this only once the
final green (or final-attempted) config is settled, to avoid re-tracking
throwaway intermediate states. `deliverables/manifest.json` and
`generator-log.md`'s "Lot 007a-R2" section are NOT yet written.

## 3. Fichiers modifiés

- `pipeline/geo/constants.py` — `G3_SEED_COUNT_MAX` 400→600 (marked,
  Amendment 007a-R2's exact derived value); `G3_LLOYD_ITERATIONS` 10→30
  (marked, seeding param, not read by `qa/checks.py`, verified — **current
  on-disk value confirmed 30, not 20**; the checkpoint text below discusses
  both 20 and 30 runs since both were tested this session; 30 is what is
  currently committed to disk and is the slightly-better of the two). No
  other line changed; `G3_R_CEIL_M` was tried at 60000 then explicitly
  reverted back to the original `95_000.0` (unmarked, matches
  VictoriaProject) — confirmed current on-disk value.
- `pipeline/geo/steps/03_cells.py` — six marked (`# FORGEHISTORY-G3-REPAIR`)
  logic additions on top of the Amendment-007a-R repair (which is already
  in place and unchanged): (a) `try_add()` gained an optional `r_override`
  param; (b) the mandatory-seed loop (step 1) now passes
  `r_override=r_floor` for any part whose area exceeds
  `G3_AREA_CEIL_KM2` (prevents an oversized mass's own mandatory seed from
  claiming a huge low-density exclusion radius that can block a
  *neighbouring* oversized mass's subdivision); (c) a new step "1.5"
  between mandatory seeding and Bridson: for every oversized mass, computes
  `target_cells = ceil(area_km2 / (G3_AREA_CEIL_KM2 * 0.5))`, lays a
  deterministic grid over the mass's bbox at the matching step size, and
  forces coverage seeds (`r_override=r_floor`) up to
  `seed_count_max - 5`; (d) that step processes masses in **ascending**
  target-cell-cost order (cheapest fix first), not descending area — see
  §5 for why; (e) a `refine_tries` fallback (halve the grid step up to 6x
  if fewer than `min(target_cells,2)` land candidates are found) — turned
  out not to be the real fix (see §5) but is harmless and left in; (f)
  Bridson's `while` loop and the urban-anchor loop both changed their
  budget check from `seed_count_max` to a new local `effective_seed_cap =
  seed_count_max - 8`, to leave headroom for the pre-existing (unmarked,
  untouched) "reinject missing masses after Lloyd" step, which has no
  budget check of its own and was observed pushing `cell_count` to 601
  (over the 600 ceiling) without this reserve.

## 4. Tests exécutés et résultats

All via `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py`,
this session, this repository. Key runs (full console captured; latest two
are the ones that matter):

- **`proof_out6.log`** (Lloyd=20, margin=0.5, ascending order, both
  r_override fixes applied) — **EXIT=1, 12/14 green.** `cell_count=596`,
  `G3-F max=36168.8 ratio=3.148` (green), `G3-E` 8 cells 16430-19106 km²,
  `G3-G` 8 cells pp 0.053-0.177. Determinism 6/6 matched.
- **`proof_out8.log`** (same but Lloyd=30) — **EXIT=1, 12/14 green.**
  `cell_count=596`, `G3-F max=34559.5 ratio=3.025`, `G3-E` 8 cells
  16636-19134, `G3-G` 8 cells pp 0.052-0.174 (marginally better than
  Lloyd=20 but not decisively). **This IS the currently-committed on-disk
  state** (`G3_LLOYD_ITERATIONS = 30` in `constants.py`, confirmed via
  `grep` at checkpoint time) — re-running `run_proof_g3.py` right now
  should reproduce `proof_out8.log`, not `proof_out6.log`.
- **`proof_out7.log`** (margin=0.42, everything else same as proof_out6) —
  regressed to 11/14 (`G3-F` newly failed, `max=84322.1 ratio=9.079`) —
  tightening the margin below 0.5 starves a different, more expensive mass.
  **Do not retry margin < 0.5 without re-solving the budget-allocation
  problem, not just the margin.**
- Determinism (`determinism.sha256`, 6 artifacts) was 6/6 matched and
  non-empty in **every** run this session — the repair logic is
  deterministic throughout all experiments.
- `py harness/verdict_audit.py` was **not** re-run this session (the gate
  would currently return REJECT/FAIL since `run_proof_g3.py` still exits 1
  and Amended SC7 requires all-green) — no point running it until closer to
  green or until the checkpoint's next session decides to stop and
  escalate instead.

## 5. Décisions prises

- **`G3_R_CEIL_M` reverted to 95000 (unmarked)**: lowering it globally
  (tried 60000) made the giant-cell problem *worse* (126398→170436 km² max)
  because Bridson's active-list is dominated numerically by
  high-density/urban regions (many more active points → higher random-draw
  probability), so a smaller `r_ceil` just raises the *demand* for points
  in low-density masses without changing their *share* of the budget —
  confirmed empirically, documented in code comments. The real fix ended up
  being the explicit, deterministic coverage-densification step (c) above,
  not a global density-field parameter.
- **Coverage-densification step processes masses by ascending
  target-cell-cost, not descending area**: descending-area order
  (try-the-biggest-first) starves the *cheapest* oversized masses (which
  need as few as 2-4 extra seeds) because they're processed last and the
  budget is already gone by then — this was empirically root-caused via a
  standalone `build_seeds()` debug script (mass index 66, area 23860.8 km²,
  was *never* subdivided in any config until this fix, reproducibly
  identical across 5+ different runs). Ascending order fixed it completely
  (that specific 23860.8 km² cell no longer appears in `proof_out6.log`
  onward).
- **`r_override=r_floor` needed on *both* the mandatory seed AND the
  densification candidates for oversized masses**: initially only the
  densification candidates got the override, which was insufficient — a
  *neighbouring* mass's own mandatory seed (not the mass being subdivided)
  was the actual blocker, since `far_enough`'s exclusion radius is
  `0.5*(candidate_r + existing_seed_r)` and the neighbour's stored r stays
  at the full density-derived value (~93km) regardless of what radius our
  new candidate requests. Fixed by also overriding the *mandatory* seed's
  radius for any oversized part.
- **Effective seed cap reserve (8) for Bridson/urban, separate reserve (5)
  inside the densification loop**: the pre-existing (byte-identical to
  VictoriaProject, unmarked, NOT part of this repair's scope to rewrite)
  "reinject masses Lloyd/dedup may have dropped" step has no budget check
  of its own. Once densification+Bridson+urban routinely fill the seed
  list to exactly `G3_SEED_COUNT_MAX`, that reinjection step can push
  `cell_count` to 601 (over the ceiling), failing `G3-D`. Reserving budget
  upstream (not touching the reinjection step itself, which stays
  unmarked/original) avoids this without weakening `G3-B`'s coverage
  guarantee.
- **Margin factor kept at 0.5** (target cell area = half the declared
  ceiling, i.e. 7,500 km² per forced seed) — both tighter (0.3, 0.4, 0.42,
  0.45) and the untried-looser direction were evaluated; 0.5 is the only
  value tested that keeps G3-F green while still meaningfully reducing
  G3-E's overshoot. This is an empirical finding, not a principled derived
  value — a fresh session could still explore a smarter *per-mass adaptive*
  margin (e.g., tighter for cheap masses, looser for the one genuinely
  unsatisfiable-within-budget continental mass) rather than one global
  constant.

## 6. Problèmes ouverts

- **G3-E and G3-G are still red.** G3-E: 8 cells at 15,061-19,134 km²
  (ceil 15,000) — modest overshoot (1.0x-1.3x), likely Voronoi-tessellation
  slack around the deterministic coverage grid rather than a fundamental
  capacity problem (unlike the original 400-cap pigeonhole, which IS
  resolved: total demand fits comfortably under 600 now). G3-G: 8 cells at
  pp 0.053-0.177 (floor 0.18) — several are very close (0.174-0.177);
  likely boundary/shard artifacts from grid-seeded cells adjacent to
  irregular coastline. A boundary-hugging seed pass was tried and tested
  WORSE (regressed to 10/14, a giant 153,527 km² cell reappeared elsewhere
  because it consumed budget from a different oversized mass) — this
  specific idea was reverted; do not re-attempt it unmodified.
- **`G3_LLOYD_ITERATIONS` at 20 vs 30**: marginal difference observed (30
  slightly better on G3-G's closest offender: 0.174 vs 0.177,
  still both red). Not conclusively worth the extra ~50% wall time per run
  without further evidence; committed value is 20. A fresh session could
  try 40-50, or investigate why compactness plateaus rather than blindly
  raising iterations further.
- **Evidence files not yet re-tracked**: `git status` will show
  `pipeline/geo/constants.py` and `pipeline/geo/steps/03_cells.py` as
  modified (uncommitted), but `logs/`, `artifacts/`, `registry/`,
  `capture/` under `pipeline/geo/` still reflect the PRIOR (007a-R,
  pre-007a-R2) run's output on disk from the last `git add -f` — this
  session's repeated runs overwrote those files on disk each time but
  never re-staged them. **Do not `git add -f` until the final config is
  settled** (avoids polluting history with 8 throwaway intermediate
  states).
- **`deliverables/manifest.json` and `generator-log.md`'s "Lot 007a-R2"
  section are not written.** The counters `g3_seed_count_max_matches_
  derivation`, `g3_bound_constants_unchanged` (7), `g3_repair_marker_count`,
  `g3_cell_count_in_range` etc. all need fresh measurement once
  `run_proof_g3.py` is green (or once a decision is made to stop and
  escalate — see below).
- **Escalation consideration**: per the eval-rubric's plateau note, a FAIL
  on G3-E/F/G now is an *ordinary* design defect (iterate), not a
  re-escalatable pigeonhole claim, UNLESS a *fresh* pigeonhole-style proof
  is shown against the new `[150,600]`/`G3_SEED_COUNT_MAX=600` bound. No
  such proof was found or attempted this session — the remaining gap looks
  like an ordinary seeding-quality tuning problem (overshoot is 1.0-1.3x on
  area, not the old 44x; compactness offenders are close to the 0.18
  floor, several within 0.01-0.03), not a hard mathematical impossibility.
  A fresh session should keep iterating on seeding logic, not escalate,
  unless it can show a genuine new capacity proof.

## 7. Prochaine action exacte

Re-run `cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py`
once (single blocking call) to confirm the current on-disk state still
reproduces `proof_out8.log`'s 12/14-green result (constants.py currently
shows `G3_SEED_COUNT_MAX = 600` and `G3_LLOYD_ITERATIONS = 30`). Then
attack G3-G specifically (the closer-to-passing check): try increasing the
densification margin factor's *shape* rather than its overall value — e.g.,
after Lloyd relaxation, identify remaining sub-0.18 cells directly from
`stats_g3.json` and add one extra seed at each such cell's centroid in a
second, smaller-scope pass (this targets the actual failing cells instead
of blindly tuning a global parameter that had diminishing/non-monotonic
returns in this session's sweep). Once both G3-E and G3-G are green (or a
genuine new pigeonhole proof is found), re-run `git add -f` on the 12
evidence files (§7 of `generator-log.md`'s existing "Lot 007a-R (repair)"
section has the exact file list), then write
`deliverables/manifest.json` + append "Lot 007a-R2 (finish)" to
`generator-log.md` with every counter from the brief's Deliverables
contract section, each measured freshly (not carried over from
`007a-repair-validation.log`, which reflects the pre-007a-R2 state).

## 8. Commande de reprise
```bash
cd pipeline/geo && .venv/Scripts/python.exe tests/run_proof_g3.py
```

## 9. Contexte minimal nécessaire

- `harness/queue/briefs/007-geo-pipeline-cells-adjacency/brief.md` —
  Amendment 007a-R (repair rules, markers, frozen bounds) and Amendment
  007a-R2 (the `G3_SEED_COUNT_MAX=600` derivation, its own 5 counters, the
  narrowed 7-constant freeze list) at the end of the file — read both in
  full before touching anything.
- `harness/queue/briefs/007-geo-pipeline-cells-adjacency/eval-rubric.md` —
  the two "Amended rows" sections at the end (007a-R, 007a-R2) for the
  exact check/counter mapping this checkpoint's §7 next action must
  satisfy.
- `pipeline/geo/steps/03_cells.py` lines ~449-585 (`try_add`, the mandatory
  seed loop, the new "1.5" densification step, Bridson's `effective_seed_cap`)
  — this is where 100% of this session's logic changes live; read it
  directly rather than trusting this checkpoint's prose summary before
  making further edits.
- `pipeline/geo/deliverables/generator-log.md`'s "Lot 007a-R (repair)"
  section (already committed, prior session) — explains the *prior*
  repair's two changes (`_iter_parts` filtering, Bridson-before-anchors
  reorder) that this session built on top of; do not re-derive that
  reasoning, it is already recorded.

## Measured state at checkpoint time
| metric | value |
|---|---|
| tool calls | -1 |
| API requests | -1 |
| progress events | 9 |
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
