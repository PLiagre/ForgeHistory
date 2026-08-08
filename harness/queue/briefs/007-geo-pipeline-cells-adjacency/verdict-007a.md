# Verdict — Brief `007`, Lot `007a` (G3 cells)

**Authored**: 2026-08-06T09:46:00
**Author**: forge-evaluateur

## Mechanical Gate Result

`py harness/verdict_audit.py harness/queue/briefs/007-geo-pipeline-cells-adjacency`
→ exit `1`, `VERDICT: REJECT` (see the gate's own stdout, this session).

The only two failing checks are `verdict_numbers_traceable` (`verdict.md
missing`) and `verdict_is_not_self_authored` (`Author frontmatter missing on
generator-log.md or verdict.md`). Both fail **solely because this verdict did
not yet exist when the gate ran** — they are the Évaluateur's own artifact,
exactly as the task anticipated. Every substantive check PASSes:
`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`no_bare_python_alias`, `rubric_predates_deliverables`,
`declared_files_are_tracked`. The mechanical gate therefore raises no
substantive REJECT; the disposition below is decided on independent
reconstruction, not on the missing-verdict FAILs.

## Independent Counter Reconstruction

Every counter below was re-derived by the Évaluateur from source data /
VictoriaProject (read-only), not copied from `manifest.json`.

| counter | manifest value | reconstructed | agree? |
|---|---|---|---|
| `byte_identical_new_files_count` (`007a` subset) | 5 | 5 / 5 | yes |
| `legacy_data_sha_target_match_count` (`007a` subset) | 2 | 2 / 2 | yes |
| `existing_legacy_data_unchanged_count` | 2 | 2 / 2 | yes |
| `path_adjustment_marker_count` (`03_cells.py`) | 2 | 2 | yes |
| `path_adjustment_unmarked_diff_line_count` | 0 | 0 | yes |
| `game_unity_reference_remaining_count` | 5 | 5 (`.py` hits) | yes (see note) |
| `g3_determinism_sha_pairs_matched_count` | 6 | 6 / 6 | yes |
| `g3_qa_checks_passed_count` | 9 | 9 / 14 | yes |
| `g3_qa_checks_red_proof_count` | 14 | 14 / 14 | yes |
| `g3_cell_count_in_range` | false | false (`401`) | yes |
| `evidence_files_git_tracked_count` | 12 | 12 | yes |
| `proof_script_exit_code_zero_count` | 0 | 0 (exit `1`) | yes |

Notes on my reconstruction:

- **Byte-identity**: SHA256 of `pipeline.py`, `tests/run_proof_g3.py`,
  `tests/test_qa_red_g3.py`, `legacy_game_data/cities.json`,
  `legacy_game_data/city_coordinates.json` each equal their VictoriaProject
  originals (5 / 5). `cities.json` = `e2052ac8…cebfb` and
  `city_coordinates.json` = `052f7f4b…e00aa`, both matching the `SC2`
  target hashes exactly (2 / 2). `province_adjacency.json` /
  `province_coordinates.json` unchanged since `002` (2 / 2).
- **Marked adjustment**: `deliverables/pre-port/03_cells.py.orig` is
  SHA256-identical to VictoriaProject's pristine `03_cells.py`. The diff
  between that snapshot and the ported file is exactly the `CITIES_JSON`
  and `CITY_COORDS_JSON` reassignments (a `16`-line multi-line block collapsed
  to `2` single lines), **both** ending in `# FORGEHISTORY-PATH-ADJUSTMENT`;
  no other line differs. Unmarked diff = 0. Clean.
- **`game_unity` audit**: my whole-tree walk found `12` raw hits, not the `8`
  the generator-log reported — it under-counted, missing `README.md:77`,
  `README.md:84`, `artifacts/cells_g3.json:1`, and
  `build/02b_divergences_1400.json:1`. However, **all four missed hits are
  non-`.py`** (README prose describing the audit itself; a generated artifact
  carrying the `RADIUS_FIELD` sources metadata verbatim; a divergences copy),
  all within the traceable/named-exception category. The `.py`-file hits — the
  ones `SC6` says "always count" — are exactly the 5 the counter reports:
  `constants.py:561-562` (untouched `002`-scope `FORBIDDEN_GAME_PATH_MARKERS`)
  and `03_cells.py:108`,`:109`,`:179` (pre-existing in the byte-identical
  original). The counter value of 5 is correct; its raw-hit narrative was
  imprecise but does not change the post-exclusion result.

## SC7 Source-State Adjudication (reproduced by the Évaluateur)

The central question: is `run_proof_g3.py`'s exit `1` a **port defect** the
Générateur must fix, or a **source-state / brief-premise defect** inherited
from VictoriaProject's own current code? I reconstructed this independently
with the commands and outputs below.

**1 — The port is faithful.** Beyond the five byte-identical files above,
`03_cells.py` is identical to VictoriaProject's original except the two
marked path-adjustment lines (proven: pre-port `.orig` SHA == VP original
SHA; unmarked diff = 0). `constants.py` is unchanged from `002`
(`G3_SEED_COUNT_MIN`/`G3_SEED_COUNT_MAX` = `[150, 400]`,
`G3_AREA_CEIL_KM2`, `G3_AREA_MAX_MEDIAN_RATIO`, `G3_COMPACTNESS_MIN` all
intact).

**2 — The port reproduces VictoriaProject's CURRENT output.** I compared this
repo's `artifacts/stats_g3.json` to VictoriaProject's own on-disk
`sandbox/geo/artifacts/stats_g3.json` (read-only, unmodified):

This repo's `stats_g3.json` and VictoriaProject's own on-disk copy report the same `cell_count` (`401` == `401`), and a `paris_basin` block identical to the decimal — same `bbox`, same `cell_count`, same `median_area_km2`, same `ratio_vs_uniform`.

A deep field-diff shows the **only** differences are the *absolute cell id
labels* (`id_range` base and the `singleton_cell_ids` values) — the
mesh-defining aggregates are identical: same `cell_count` (`401`), same
singleton count (`205` in both), same `paris_basin`, same area distribution.
The failing metric — `cell_count = 401`, one above the `G3_SEED_COUNT_MAX` ceiling of `400` — is reproduced **exactly**. The id-label offset does not touch the
`[150, 400]` range check and is precisely the internally-deterministic,
cross-repo-variable output the rubric's final note declares informational
(intra-repo two-run determinism holds: 6 / 6 SHA pairs matched).

**3 — VictoriaProject's own committed "green" proof log is provably STALE.**
I loaded VictoriaProject's committed `sandbox/geo/logs/v1_049_qa.json` and its
own `artifacts/stats_g3.json`:

VictoriaProject's `v1_049_qa.json` records `14` checks, all `passed=true` (G3-D `passed=True`, `detail=None`), while its own `stats_g3.json` reports `cell_count` = `401` — i.e. outside `[150, 400]`.

VictoriaProject's committed log asserts G3-D ("cell count in `[150, 400]`")
**passed**, while VictoriaProject's own current `stats_g3.json` records
`401`. They contradict. The mtimes explain why:

VictoriaProject mtimes (read-only): `logs/v1_049_qa.json` at `2026-07-26T12:24:01` (the all-green record); `constants.py` at `2026-07-29T10:49:42`; `steps/03_cells.py` at `2026-07-29T10:14:03`; and `artifacts/stats_g3.json` (the `401` state) at `2026-07-29T11:07:40`.

The green qa log predates the constants/script it supposedly validated, which
in turn predate the `401` stats artifact. A fresh `run_proof_g3.py`
regenerates the qa log **and** the stats together in one pass; VictoriaProject's
green log was written before whatever change produced the current `401`
state and was never re-run. **A fresh run of VictoriaProject's own committed
code today would fail G3-B/D/E/F/G identically.** The brief's premise —
"the G3 source is green, port it and it stays green" — is false against
VictoriaProject's current committed state.

**4 — Library drift is ruled out as the cause.** Both venvs carry identical
`numpy` `2.5.1`, `pyproj` `3.7.2`, `shapely` `2.1.2` (same shapely wheel =
same bundled GEOS). The `401`/degenerate mesh is not a version artifact; it
is the deterministic output of the unchanged algorithm against the unchanged
thresholds, in **both** repositories.

**5 — I looked at the captures myself** (hard-won rule `11`). `v1_049_cells_window.png`
is a genuine Voronoi cell mesh over the European pilot window with red density
seeds; it visibly contains the enormous cells over eastern Europe and North
Africa (Morocco is a single giant cell) that the `G3-E` area-ceiling and
`G3-F` max/median-ratio checks flag. The `401`/degenerate state is real and
visible, not a crash or an empty render.

**SC7 adjudication conclusion**: `SC7` (proof exits 0, all `14` checks green,
`cell_count` ≤ `400`) is **unsatisfiable by a byte-identical port**. The
Générateur was **correct** to refuse to alter `03_cells.py` logic or
`constants.py` — the Non-Goals forbid exactly that, and doing so would have
been the real defect. This is a **brief-premise defect**, not a Générateur
work defect.

## Per-Rubric-Line Verdict (Lot `007a` subset)

| Success Condition | PASS/FAIL | Evidence |
|---|---|---|
| `SC1` `pipeline.py` byte-identical | PASS | SHA256 == VP original |
| `SC1` `pipeline.py` genuinely required | PASS | `03_cells.py` `_load_pipeline_module` intact; `adjacency_g3.json` non-trivial (`53564` bytes, `adjacency` key) — `derive_adjacency` ran |
| `SC2` cities/city_coordinates SHA target-match | PASS | 2 / 2 vs cited targets |
| `SC2` decision recorded, none silently skipped | PASS | `README.md` names all five legacy files with decisions matching brief.md |
| `SC3` copy list present, nothing extra | PASS | all `007a` destinations present; `git status --porcelain pipeline/geo` empty (nothing unlisted) |
| `SC3` byte-identical subset | PASS | 5 / 5 (of the `007a` subset of the full `8`) |
| `SC4` exactly one marked adjustment, nothing else | PASS | marker count 2, unmarked diff 0 |
| `SC6` no remaining `game_unity`/`StreamingAssets` in `.py` | **FAIL** | counter = 5, target `0` — **brief-premise, not fixable within scope** (see below) |
| `SC7` proof exits 0, all green, cell in range | **FAIL** | exit `1`; 9 / 14 checks green; `cell_count = 401` — **brief-premise, not fixable within scope** (see adjudication above) |
| `SC10` evidence git-tracked | PASS | `12` files tracked via `git add -f` (mechanism `002` never actually applied; established here first, consistent with brief.md's named mechanism) |
| `SC11` README truthful, no overclaim | PASS | explicitly states proof exits `1` (FAIL); claims only G3 code landed, G4+ not |
| `SC12` `.orig` + README snapshot with `must_differ_from` | PASS | `03_cells.py.orig` present; pre/post README SHAs differ |
| `SC13` split-check run first, per lot | PASS | re-run: `advisory SIZE_OK` at estimate `95`, not `NEEDS_SPLIT` |
| `SC14` Non-Goals hold | PASS | no `05_rivers.py`-onward files; `docs/adr` and `sim/` clean; `ADR-0003` untouched |

## Backward Compatibility

`002`'s ported infrastructure is untouched: `constants.py`, `io_util.py`,
`projection.py`, `qa/checks.py`, `legacy_game_data/province_*.json` all
unchanged (province files SHA-re-verified, 2 / 2). No regression against
`002`. Note that `002`'s own evidence-tracking gap (its "Defect C" — declared
evidence never force-added) was correctly diagnosed and this lot establishes
the force-add mechanism for the first time, which is an **improvement** over
`002`'s state, applied consistently rather than via a second invented
mechanism.

## Overall Verdict: FAIL

Two Success Conditions (`SC6`, `SC7`) are not met on their face:
`game_unity_reference_remaining_count` = 5 (target `0`) and
`run_proof_g3.py` exits `1` with 9 / 14 checks green and `cell_count = 401`.
A lot cannot be ACCEPTED while its own validation command
(`run_proof_g3.py` → exit 0) fails. **However, both failures are
brief-premise defects, not Générateur work defects** — see "What must change
and WHO" below. The Générateur's actual deliverables (byte-identical copies,
the single marked adjustment, evidence tracking, truthful README, honest
counters) are clean and, on the port-fidelity conditions, exemplary.

## Boundary Violations

None. The Générateur did **not** cross any Non-Goal to force a green result:
it did not edit `03_cells.py` logic beyond the marked adjustment, did not
touch `constants.py`, did not silently force `game_unity_reference_remaining_count`
to `0`, and did not claim the proof "ran" green when it exited `1`. It
reported both blocking conflicts openly with reproduction. This is the correct
behavior under the hard-won rule that the producer does not pronounce its own
work acceptable.

## What Must Change and WHO Must Change It

Both open failures are **blocked pending a Planificateur amendment / owner
decision** and **cannot be fixed by a Générateur iteration-2** without
violating the brief's own Non-Goals:

1. **`SC7` (proof green / `cell_count` ≤ `400`) — brief-premise defect.**
   The brief assumes VictoriaProject's G3 source is green. It is not: a fresh
   run of VictoriaProject's own committed code produces `cell_count = 401`
   and fails G3-B/D/E/F/G, and VictoriaProject's committed green qa log is
   stale (predates its own current constants/script/stats, proven by mtimes
   above). A byte-identical port faithfully reproduces the FAIL. The
   Générateur **cannot** satisfy `SC7` without altering `03_cells.py` or
   `constants.py`, which Non-Goals forbid. **Owner/Planificateur must decide**:
   either (a) amend brief `007` to re-baseline against VictoriaProject's
   *actual current* G3 output (accept the `401`/degenerate mesh as the
   attested source state and adjust `SC7`/the range accordingly), or (b)
   first repair G3 in VictoriaProject upstream and re-attest a genuinely
   green source before this port is expected to be green — a scope this brief
   explicitly excludes.

2. **`SC6` (`game_unity_reference_remaining_count` = 0) — brief-premise
   defect, same class as `002`'s Defect A.** VictoriaProject's `03_cells.py`
   carries `game_unity`/`StreamingAssets` substrings in non-path content
   (`RADIUS_FIELD` sources metadata at `:108`/`:109`, docstring at `:179`),
   pre-existing in the byte-identical original. Removing them is an unmarked,
   non-path-adjustment diff line that Non-Goals prohibit. `SC6`'s "zero in
   any `.py`" and the byte-identical-port mandate are mutually unsatisfiable.
   **Planificateur must amend `SC6`** to carve these pre-existing,
   non-path-resolution literals into the traceable-exception scope (as `002`
   did for `constants.py`'s `FORBIDDEN_GAME_PATH_MARKERS`), rather than
   demanding a `0` that a faithful port cannot reach.

No Générateur re-run is warranted until one of these amendments lands. If the
owner instead rules that the port should be accepted *as-is with `SC6`/`SC7`
recorded as source-state carry-forwards* (analogous to the brief `002` plateau
convention), that is an owner decision to record — not something the
Évaluateur or Générateur may self-grant.
