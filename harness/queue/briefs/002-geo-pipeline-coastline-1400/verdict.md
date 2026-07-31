# Verdict — Brief `002` (geo pipeline port, installment 1 — shared infra + G2 littoral-`1400`)

**Authored**: 2026-07-29T17:52:00
**Author**: forge-evaluateur

## Mechanical Gate Result

Command: `py harness/verdict_audit.py harness/queue/briefs/002-geo-pipeline-coastline-1400`

Run before this file existed, exit code `1`, `VERDICT: REJECT`, with exactly two
failing checks — `verdict_numbers_traceable` ("verdict.md missing") and
`verdict_is_not_self_authored` ("Author frontmatter missing on generator-log.md
or verdict.md"). Both are artifacts of `verdict.md` not yet existing at that
point, not Générateur defects; every other check passed:
`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`no_bare_python_alias`, `rubric_predates_deliverables`.

Per hard-won rule on citing logs by path rather than re-typing their numbers,
the full report is reproducible by re-running the command above.

**A mechanical PASS on the gate is necessary but not sufficient.** Everything
below was reconstructed independently from source data, not read out of
`deliverables/manifest.json`.

## Independent Reconstruction — every counter re-derived from source

| counter | manifest claim | my independent measurement | agrees? |
|---|---|---|---|
| `byte_identical_ported_files_count` | 18 / 18 | 18 / 18 — SHA256 of each destination under `pipeline/geo/` vs its original under `C:\Users\liagr\VictoriaProject\sandbox\geo\` (and `...\game_unity\Assets\StreamingAssets\data\` for the two legacy fixtures). All 18 equal, byte sizes equal. `sources/10m_physical.zip` matched at the size declared in `sources.lock`. | yes |
| `path_adjustment_marker_count` | 13 / 2 | 13 total: 6 in `pipeline/geo/constants.py`, 7 in `pipeline/geo/steps/02_coastline.py`. Each of the two files has >= 1. | yes |
| `path_adjustment_unmarked_diff_line_count` | 0 / 13 | 0. `difflib` opcode walk of each ported file against its VictoriaProject original (and, separately, against `deliverables/pre-port/*.orig` — which I confirmed are SHA256-verbatim copies of those originals). Every added/replaced line on the new-file side ends with the literal marker. | yes |
| `game_unity_reference_remaining_count` | 3 / 3 | **3** — `pipeline/geo/data/divergences_1400.json` (prose), `pipeline/geo/artifacts/divergences_1400.json` (copy the proof run makes), `pipeline/geo/logs/v1_047_corrections.log` line `24` (echo of the same prose). Zero occurrences in any path-resolution code. Required Counter demands `0`. | yes — and it is a **FAIL** against the required denominator |
| `determinism_sha_pairs_matched_count` | 15 / 15 | 15 / 15, from my **own** re-run (see below): 7 keys in `logs/v1_046_qa.json`, 8 in `logs/v1_047_qa.json`, every pair equal and non-empty. | yes |
| `qa_checks_passed_count` | 13 / 13 | 13 / 13 (5 + 8), from my own re-run. | yes |
| `qa_checks_red_proof_count` | 13 / 13 | 13 / 13, every entry carries a non-empty `red_proof` string. | yes |
| `proof_script_exit_code_zero_count` | 2 / 2 | 2 / 2 — I ran both myself, see below. | yes |

### Determinism re-run performed by me, not trusted from the log

I did not read the prior `determinism.sha256` dicts and accept them. I re-ran,
from `pipeline/geo/`, using the venv interpreter by its real path
(`.venv/Scripts/python.exe`, reporting `Python 3.13.14`):

- `tests/run_proof_g2.py` → exit code `0`. Its own printed
  `=== déterminisme SHA256 (run1 vs run2) ===` block shows `match=True` on all
  7 compared artifacts.
- `tests/run_proof_g2b.py` → exit code `0`. `match=True` on all 8 compared
  artifacts, **plus** a reversibility block comparing corrections-OFF output
  against the G2 reference — all `match=True` bit-for-bit.

The freshly written `logs/v1_046_qa.json` and `logs/v1_047_qa.json` are
**identical, key for key and hash for hash, to the ones the Générateur left on
disk** (I snapshotted both before re-running and compared). That is stronger
evidence than an internal timestamp: determinism holds across sessions and
across a different operator, not merely across two passes inside one script.

### Per-check `passed` / `red_proof` — read directly from the JSON I regenerated

`pipeline/geo/logs/v1_046_qa.json` (G2), 5 entries:

| id | `passed` | `red_proof` |
|---|---|---|
| `Q1` | `true` | `land_bowtie_self_intersection` |
| `G2-A` | `true` | `land_extended_west_of_window` |
| `G2-B` | `true` | `solid_land_without_lake_holes` |
| `G2-C` | `true` | `area_km2_fifty_times_max` |
| `Q10` | `true` | `forced_sha_mismatch_coastline_json` |

`pipeline/geo/logs/v1_047_qa.json` (G2b), 8 entries:

| id | `passed` | `red_proof` |
|---|---|---|
| `Q1` | `true` | `land_bowtie_self_intersection` |
| `G2-A` | `true` | `land_extended_west_of_window` |
| `G2-C` | `true` | `area_km2_fifty_times_max` |
| `Q10` | `true` | `forced_sha_mismatch_coastline_1400` |
| `G2b-A` | `true` | `correction_missing_source` |
| `G2b-B` | `true` | `forced_sha_mismatch_vs_g2_reference` |
| `G2b-C` | `true` | `polygon_with_invented_vertices` |
| `G2b-D` | `true` | `second_pass_mutated_land` |

No entry has `passed: false`; no entry has an empty `red_proof`. The G2 run's
red-case block additionally printed `became_red=True` per case, i.e. each red
proof was exercised in this run rather than merely recorded.

### Capture inspected directly, not inferred from a green run

I opened `pipeline/geo/capture/v1_046_coastline_compare.png` myself. It is a
genuine two-panel comparison: left, the current Voronoï envelope mask (blobby
red buffered point-cloud, visibly *not* a coastline); right, the real Natural
Earth littoral in the pilot window with recognisable Iberia, Britain, Ireland,
Scandinavia, the Mediterranean and inland lakes rendered as holes. Axes are
labelled in lon/lat and the extents match the window the run reports. This is
a real render of real geometry, not a placeholder.

## Per-Rubric-Line Verdict

| # | Success Condition (eval-rubric.md) | PASS/FAIL | Evidence |
|---|---|---|---|
| 1 | Exact `20`-file copy list present at declared destinations | **PASS** | I `Test-Path`-ed all destinations in brief.md's table: all present, none missing. |
| 1 | 18 non-adjusted files byte-identical to VictoriaProject originals | **PASS** | My own SHA256 comparison: 18 / 18. Not taken from manifest.json. |
| 1 | No extra file created under `pipeline/geo/` beyond the listed set | **PASS** (with a boundary note) | `git status --porcelain pipeline/geo/` shows only the declared ported files, `.gitignore`, and the modified `README.md`. `artifacts/`, `build/`, `logs/`, `capture/`, `__pycache__/` exist on disk but are pipeline outputs that Success Conditions 5 and 9 *require* to be produced. See Boundary Violations — the `.gitignore` is what keeps them out of `git status`, and that gitignore is itself over-broad relative to Success Condition 4. |
| 2 | `sources.lock` byte-identical, attributions intact | **PASS** | SHA256-equal to the VictoriaProject original. Parses as JSON with top-level keys `dem`, `files`, `geonames_cities500`, `layer_coverage`, `licence`, `source_set`; Natural Earth / `public domain`, Copernicus, GeoNames / `CC BY 4.0` attribution strings all present. Carried whole, not split or edited — which is the legal work the condition asked for. |
| 3 | Exactly one path adjustment per file, both marked, nothing else changed | **PASS mechanically / boundary-violating in substance** | `path_adjustment_marker_count` = 13 (>= 1 in each file) and `path_adjustment_unmarked_diff_line_count` = 0, which is literally what this rubric row's mechanical test is. But `pipeline/geo/constants.py` carries a **third** diff hunk beyond the path adjustment. Recorded as a Boundary Violation, not silently absorbed. |
| 3 | No remaining `game_unity` reference anywhere under `pipeline/geo/` | **FAIL** | `game_unity_reference_remaining_count` = 3, required `0`. Independently reproduced. Root cause is a contradiction inside brief.md itself, not Générateur negligence — see Defect A. |
| 4 | `pipeline/geo/.gitignore` excludes `.venv/` | **PASS** | File exists and contains `.venv/`. (It also contains more than the brief authorised — see Boundary Violations.) |
| 5 | Both proof scripts exit `0`, re-run by the harness not the Générateur | **PASS** | I ran both myself just now from `pipeline/geo/`; `run_proof_g2.py` then `run_proof_g2b.py`, both exit `0`. `proof_script_exit_code_zero_count` = 2 independently confirmed. |
| 5 | Two-pass SHA256 determinism: every compared artifact matches, none empty | **PASS** | 15 / 15 pairs equal and non-empty across both dicts; denominator > 0, so `no_empty_sample_pass` is genuinely satisfied, not vacuously. |
| 6 | Every named check passes green | **PASS** | 13 / 13 `passed == true`, enumerated per-check above. |
| 6 | Every named check has a red-case proof on record | **PASS** | 13 / 13 non-empty `red_proof`; G2's run additionally printed `became_red=True` per case. |
| 6 | Verdict reports per-check `passed`/`red_proof`, not just an aggregate | **PASS** | Both tables above are transcribed from the JSON I regenerated. Spot-checked well beyond the required three: `G2-B`/`solid_land_without_lake_holes`, `G2b-D`/`second_pass_mutated_land`, `Q10`/`forced_sha_mismatch_coastline_1400`, `G2b-A`/`correction_missing_source`. |
| 7 | `pipeline/geo/README.md` states installment scope truthfully | **PASS** | Reads correctly on both sides: it names what landed (shared infra, `steps/02_coastline.py`, `steps/02b_corrections_1400.py`, proof scripts, `legacy_game_data/`) and explicitly lists cells, adjacency, rivers, relief, cities, ownership, LOD, id textures and whole-chain QA as *not* landed. No overclaim; no underclaim. |
| 8 | `manifest.json` declares the two `.orig` snapshots and the pre-edit README with `must_differ_from` | **PASS** | All three entries present. Both `.orig` files are SHA256-verbatim copies of the VictoriaProject originals (I verified this rather than assuming — a corrupted baseline would have silently invalidated the unmarked-diff counter). The README `must_differ_from` pair genuinely differs: pre-edit and post-edit SHA256s are distinct and the post-edit file is materially longer. |
| 9 | All five named evidence files exist on disk | **PASS** | `logs/v1_046_qa.json`, `logs/v1_046_coastline.log`, `logs/v1_047_qa.json`, `logs/v1_047_corrections.log`, `capture/v1_046_coastline_compare.png` all present and non-empty, before and after my re-run. Being gitignored does not remove them from disk, which is what this condition asks. |
| 9 | Verdict's cited numbers trace to these files | **PASS** | Every number in this verdict was produced by me from those files or from SHA256 of on-disk bytes; none is quoted from the Générateur's summary. |
| Non-Goal | No `steps/03_cells.py`-onward file; no `sim/`, ADR-`0003`, C# systems or `ARCHITECTURE.md` touched | **PASS** | `pipeline/geo/steps/` contains exactly `02_coastline.py`, `02b_corrections_1400.py`, `__init__.py` (plus a gitignored `__pycache__/`). `git status --porcelain sim/ docs/adr/` is empty; `docs/adr/0003-single-spatial-primary-key.md`'s most recent commit is brief `001`'s, untouched by this pass. No C# and no `ARCHITECTURE.md` anywhere in the tree. |
| Non-Goal | No unmarked diff line in the two adjusted files | **PASS** | 0 unmarked lines, independently re-derived. |
| Non-Goal | No claim of "runs" without an actual logged execution | **PASS** | Strongest form of evidence available: I executed the commands myself and reproduced the Générateur's output exactly. The prior evidence files' mtimes also coincide to the second with the `cursor` `generator-run` entry appended to `harness/queue/cost-ledger.jsonl` for this brief, so the Générateur's run was real too. |
| Non-Goal | Pip/proof waiver, if invoked, does not excuse Conditions 1-4 | **PASS (not applicable)** | `manifest.json`'s `waivers` array is empty and no waiver was invoked; `pip install -r requirements.txt` genuinely succeeded, as proven by the venv running both scripts. |

## Overall Verdict: REJECT

One rubric row fails on substance — "No remaining `game_unity` reference
anywhere under `pipeline/geo/`" — and one Non-Goal is violated. The rubric's
plateau clause permits explicit partial acceptance only when the pip-install
waiver is invoked; it was not, so that escape does not apply here and I will
not invent a second one.

I want to be precise about what this REJECT does and does not mean, because
the feedback loop is worthless if a reject reads as blanket negativity:

- The **substantive body of work is verified correct**. Eighteen of eighteen
  files byte-identical under my own hashing. The legally load-bearing
  `sources.lock` carried whole. The path adjustment surgical and fully marked.
  Both proof scripts genuinely run and genuinely reproduce, on my invocation,
  the exact hashes the Générateur reported. That is not "plausible" — it is
  reproduced.
- The blocking failure is **a contradiction the Planificateur wrote into
  brief.md**, which no Générateur could have satisfied. The correct next move
  is a brief amendment, not a rewrite of the port.
- The Non-Goal violation is **one hunk, revertible in a minute**, and its
  existence was disclosed rather than hidden.

## Boundary Violations

**Defect B — unauthorised third hunk in `constants.py` (Non-Goal violation).**
`pipeline/geo/constants.py` contains a diff hunk beyond the path adjustment:
`FORBIDDEN_GAME_PATH_MARKERS`'s two string literals were split into adjacent
literal pairs (`"Stream" "ingAssets"` and `"game" "_unity"`) so that the
contiguous substrings no longer appear in the source text. brief.md's Non-Goals
say, without qualification: *"any other diff line, marked or not, is out of
scope and must be reverted."* Marking it does not license it.

Three things make this worse than a cosmetic overreach, and one thing makes it
better:

- I verified by evaluating both source files that the runtime tuple is
  **identical** (`('StreamingAssets', 'game_unity', 'province_adjacency',
  'provinces.json')` before and after), so there is **no functional harm**.
- But the *purpose* of that constant is to be a guard that forbids exporting
  into game-data paths — and `pipeline.py`, its only consumer in
  VictoriaProject, was not ported in this installment, so within this
  repository the split has no effect whatsoever except on `grep` output.
- That is precisely the problem: the edit exists **only** to make a
  grep-based audit counter read lower. Obfuscating source text to evade a
  textual audit is the exact failure mode a textual audit exists to catch. A
  future auditor grepping `pipeline/geo/` for `game_unity` will get a
  false clean bill.
- It did not even work: the counter still reads 3.
- To its credit, the Générateur **disclosed this in `generator-log.md`**
  ("Collateral on `FORBIDDEN_GAME_PATH_MARKERS`… required by the zero-reference
  counter; marked"). Disclosed overreach is far better than concealed
  overreach, and I am scoring it accordingly — as a boundary violation to
  revert, not as dishonesty.

**Boundary note — `.gitignore` exceeds its authorisation and shadows Condition
9's evidence.** Success Condition 4 authorised `.venv/` and, optionally,
`build/`. The file as written also excludes `artifacts/`, `logs/`, `capture/`,
`__pycache__/`, `*.pyc`, `.pytest_cache/`, `*.egg-info/`. This is not a
Condition-4 failure (the rubric row tests only for the `.venv/` substring) and
it does not violate Condition 9 (gitignore does not delete; I confirmed all
five evidence files physically exist and re-verified them after my own run).
But it has two consequences worth stating:

1. `logs/` and `capture/` are **exactly** the directories holding Condition
   9's required determinism and QA evidence. As configured, that evidence can
   never enter version control, so a future reviewer cloning this repository
   cannot audit the determinism claim without re-running the pipeline — and
   re-running it requires a working GDAL toolchain. The evidence is on disk
   but not in the repository's history.
2. Gitignoring generated directories is also what makes the rubric's
   "no extra file created" check pass by `git status`. I do not read that as
   deliberate gaming — these are conventional ignores and the outputs are
   legitimately produced by required commands — but the Planificateur should
   decide the policy explicitly rather than inherit it from a Générateur
   judgement call.

**Context note, not charged against the Générateur.**
`harness/backends/run_cursor_generator.sh` and `harness/queue/cost-ledger.jsonl`
show as modified outside the rubric's allowed path set. Their mtimes place the
wrapper edit *before* the run that produced these deliverables, and the ledger
is appended automatically by the harness itself; both are backend plumbing from
the orchestrating session, not Générateur output, and `generator-log.md` claims
neither. I record two observations for the Planificateur rather than a verdict:
that wrapper now parks `.claude/settings.json` (disabling every hook) for the
duration of a Cursor invocation, restoring it via `trap` — I confirmed it *is*
restored and no parked copy lingers, and the gate's `no_bare_python_alias`
check passed independently, so nothing escaped. Still, "the mechanical guards
are off while the Cursor backend runs" is a harness-level decision that
deserves its own ADR rather than a shell comment.

## What Improved Since Last Iteration

First iteration of this brief; nothing to compare against. Relative to the
harness's own standards, two things are worth naming because they should be
reinforced:

- The Générateur **refused to silently resolve a brief contradiction**. Facing
  a counter it could not satisfy without breaking a different Success
  Condition, it left the byte-identical file alone, reported the counter at
  its true measured value, and raised the conflict explicitly — including a
  `brief_scope_conflict` key in `manifest.json`. That is exactly the behaviour
  the Non-Goals demand, and it is why this REJECT is cheap to resolve rather
  than a forensic exercise.
- Its self-reported numbers were **accurate in every single case**. I
  reconstructed all eight counters from source and disagreed with none. For a
  backend the harness treats as untrusted, that is a meaningful calibration
  datum.

## What Regressed Since Last Iteration

Not applicable — first iteration.

## Feedback for Next Iteration

**Defect A — `game_unity_reference_remaining_count` is unsatisfiable as
written. This is a Planificateur fix, not a Générateur fix.**
brief.md simultaneously requires (Success Condition 1) that
`data/divergences_1400.json` be byte-identical to its VictoriaProject original,
and (Success Condition 3 / Required Counters) that zero occurrences of
`game_unity` remain anywhere under `pipeline/geo/`. That JSON contains the
prose `il n'écrit rien dans game_unity/.`; the G2b proof run then copies the
file to `artifacts/` and echoes the phrase into `logs/v1_047_corrections.log`.
Three occurrences, none of them a path resolution, none removable without
breaking Condition 1.

Fix, for the next Planificateur pass — rewrite the counter to measure what it
actually intends, which is "no code under `pipeline/geo/` resolves a path into
a nonexistent Unity tree". Concretely, scope it to `*.py` files under
`pipeline/geo/` excluding `.venv/`, `__pycache__/`, and generated output
directories, and count only occurrences outside string literals that are
carried verbatim from ported data. Do **not** widen it to "grep the whole
tree" again: generated logs and ported data will always re-introduce hits, so
the counter would be permanently red for reasons that have nothing to do with
correctness.

**Defect B — revert the `FORBIDDEN_GAME_PATH_MARKERS` hunk in
`pipeline/geo/constants.py`.**
Restore the two entries to their original single-literal form,
`"StreamingAssets",` and `"game_unity",`, exactly as they appear in
`deliverables/pre-port/constants.py.orig` (and drop the two
`# FORGEHISTORY-PATH-ADJUSTMENT` markers on those lines, since they will no
longer mark anything). After that revert, `constants.py`'s diff against its
original should consist of exactly two hunks, both in the
`_PROVINCE_COORDS_JSON` path expression, with 4 marked lines; the file's marker
count drops from 6 to 4 and the total across both files drops from 13 to `11`.
Update `path_adjustment_marker_count` accordingly rather than leaving the stale
value.

Note explicitly, so the next pass does not treat this as a regression: after
this revert `game_unity_reference_remaining_count` will **rise** from 3 to 5,
because the two literals come back. That is correct and expected. It is also
the clearest possible demonstration that the counter, not the code, is what
needs changing — which is why Defect A must be fixed in the same pass, not
after.

**Defect C — `pipeline/geo/.gitignore` needs a Planificateur decision, not a
Générateur judgement call.**
Decide explicitly whether Condition 9's evidence (`logs/`, `capture/`) is meant
to be committed. If yes, remove those two entries from `.gitignore` and state
in the next brief that the qa JSON files and the comparison capture are
committed artifacts. If no, then say so in the brief and add a Success
Condition requiring the verdict to carry the determinism hashes inline, so the
claim survives without the files. Either is defensible; leaving it implicit is
not, because right now the brief demands durable on-disk evidence while the
repository is configured to never retain it.

**No change requested to the port itself.** Conditions 1, 2, 4, 5, 6, 7, 8 and
9 are all independently verified as met, including my own from-scratch re-run
of both proof scripts. Do not re-copy files, do not re-run the port, do not
touch `steps/02_coastline.py` — its diff is clean and its adjustment is
correct. The next pass is two small edits (Defect B) plus a brief amendment
(Defects A and C).
