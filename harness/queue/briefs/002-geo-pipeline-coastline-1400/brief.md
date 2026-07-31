# Brief 002: Geo pipeline port, installment 1 — shared infrastructure + G2 littoral 1400 (coastline)

**Authored**: 2026-07-29T14:00:00
**Author**: forge-planificateur
**Amended**: 2026-07-29T19:30:00 (iteration 2 — resolves a self-contradiction
between Success Condition 1 and Success Condition 3 that the Évaluateur
identified in `verdict.md` / `feedback/feedback-002.md`, iteration 1. Only
Success Condition 3 and the `game_unity_reference_remaining_count` Required
Counter are amended below; every other Success Condition, the file-copy
table, and the determinism requirements are unchanged and were independently
confirmed satisfied in iteration 1 — see `feedback/feedback-002-amendment.md`
for the precise diff of what changed and why.)

## World-Terms Requirement

Stated causally, not as a code-quality preference:

Principle 3 (`docs/rules/simulation-principles.md`) requires that the economy
be physical: nothing teleports; every good has an origin, a transport path, a
storage point, a destination. None of that means anything until there is a
world for it to happen in — land that exists, a coastline that bounds it, a
shape a transport route can be drawn across. `pipeline/geo/` is the one place
in this repository that will ever answer "where is land and where is sea" at
the geometric level every higher layer (cells, adjacency, rivers, relief,
cities, ownership) reads from. Today `pipeline/geo/` is an empty stub: F1's
whole causal chain — a province boundary redraw recomputing cell aggregation
(ADR-0003) which migration, tax, and trade logic then read — has no terrain
to redraw, because no terrain has ever been produced or proven reproducible
in this repository. This brief's job is not to invent that terrain: it is to
carry over, unchanged in its determinism and its legal sourcing, the exact
coastline-production step (`02_coastline.py`) and its declared, reversible
1400-era correction layer (`02b_corrections_1400.py`) that VictoriaProject
already built, measured, and SHA256-verified — plus the five shared modules
every later step (cells, adjacency, rivers, relief, cities, ownership, LOD,
textures) will import. Until this lands, every later F1 brief in the geo
pipeline port plan (`harness/queue/geo-pipeline-port-plan.md`) has nothing to
build on.

This brief is scoped to porting that shared infrastructure and those two G2
steps, and to proving — by command, not narration — that they still produce
byte-identical output across two runs in this repository. It is NOT scoped
to `03_cells.py` onward (brief 003), to Copernicus relief (brief 004), to
ownership (brief 005, which separately confronts a real conflict with
ADR-0003 — see `harness/queue/geo-pipeline-port-plan.md`), or to any
simulation code.

## Success Conditions

1. **Exact file copy list.** The following files are copied from
   `C:\Users\liagr\VictoriaProject\sandbox\geo\` (and, for the two legacy
   game-data files, from `C:\Users\liagr\VictoriaProject\game_unity\...`)
   into `pipeline/geo/` at the destination paths shown. Eighteen of the
   twenty must be **byte-identical** (SHA256-equal) to their VictoriaProject
   original; the remaining two (`constants.py`, `steps/02_coastline.py`)
   require exactly one path adjustment each (Success Condition 3) and must
   be byte-identical **except** for lines marked per that condition.

   | source (under `sandbox/geo/` unless noted) | destination (under `pipeline/geo/`) | byte-identical? |
   |---|---|---|
   | `constants.py` | `constants.py` | no — marked path adjustment (SC3) |
   | `io_util.py` | `io_util.py` | yes |
   | `projection.py` | `projection.py` | yes |
   | `requirements.txt` | `requirements.txt` | yes |
   | `sources.lock` | `sources.lock` | yes |
   | `steps/__init__.py` | `steps/__init__.py` | yes |
   | `steps/02_coastline.py` | `steps/02_coastline.py` | no — marked path adjustment (SC3) |
   | `steps/02b_corrections_1400.py` | `steps/02b_corrections_1400.py` | yes |
   | `qa/__init__.py` | `qa/__init__.py` | yes |
   | `qa/checks.py` | `qa/checks.py` | yes (shared module spanning later steps too — ported wholesale as one unit, not trimmed) |
   | `tests/__init__.py` | `tests/__init__.py` | yes |
   | `tests/run_proof_g2.py` | `tests/run_proof_g2.py` | yes |
   | `tests/test_qa_red_g2.py` | `tests/test_qa_red_g2.py` | yes |
   | `tests/run_proof_g2b.py` | `tests/run_proof_g2b.py` | yes |
   | `tests/test_qa_red_g2b.py` | `tests/test_qa_red_g2b.py` | yes |
   | `data/corrections_1400.json` | `data/corrections_1400.json` | yes |
   | `data/divergences_1400.json` | `data/divergences_1400.json` | yes |
   | `sources/10m_physical.zip` | `sources/10m_physical.zip` | yes (52,422,954 bytes per `sources.lock`) |
   | `..\game_unity\Assets\StreamingAssets\data\province_coordinates.json` | `legacy_game_data/province_coordinates.json` | yes |
   | `..\game_unity\Assets\StreamingAssets\data\province_adjacency.json` | `legacy_game_data/province_adjacency.json` | yes |

   No file outside this list is created under `pipeline/geo/` in this brief
   (Non-Goals reiterates this for `steps/03_cells.py` onward).

2. **`sources.lock` and its attributions are carried over byte-for-byte** —
   this is legally load-bearing (Natural Earth public domain, Copernicus DEM
   attribution-required, GeoNames CC BY 4.0 — the DEM and GeoNames entries
   are not consumed by this installment, but the file is one legally-vetted
   unit and must not be split or edited). No re-verification of the
   licenses is required or wanted; carrying the file unchanged **is** the
   legal work.

3. **The legacy-game-data path adjustment — exactly two files, exactly one
   adjustment each, and nothing else.** `constants.py`'s
   `_load_province_coordinates()` (and the `_PROJECT_ROOT` /
   `_PROVINCE_COORDS_JSON` path expression it depends on) and
   `steps/02_coastline.py`'s `build_current_game_landmask()` (and its
   `repo` / `coords_path` / `adj_path` expressions) currently resolve to
   `<repo-root>/game_unity/Assets/StreamingAssets/data/...`. ForgeHistory has
   no `game_unity/` tree. Both must be changed to resolve instead to the
   copied files at `pipeline/geo/legacy_game_data/province_coordinates.json`
   and `pipeline/geo/legacy_game_data/province_adjacency.json`. This is the
   **only** logic change permitted in either file. Every changed line must
   end with the literal marker comment `# FORGEHISTORY-PATH-ADJUSTMENT` so
   the change is greppable and auditable — an unmarked diff line is a
   silent, unauthorized change (Non-Goals).

   **Amended scope of the zero-occurrence requirement (iteration 2).**
   Outside that one path-resolution change, no occurrence of the substrings
   `game_unity` or `StreamingAssets` may remain in any `.py` file, or in any
   other path-resolution logic, anywhere under `pipeline/geo/` (Required
   Counters). There is exactly one narrow, named exception, because it is
   unavoidable rather than desired: `data/divergences_1400.json` — copied
   byte-identical per Success Condition 1 — contains pre-existing French
   prose that mentions `game_unity` as plain text, not as a path (e.g. "il
   n'écrit rien dans `game_unity`/."). That prose is not introduced by this
   port; it is VictoriaProject's own original content, carried over because
   Success Condition 1 requires the file be byte-identical. The exception
   therefore covers exactly three things and nothing broader:
   - `pipeline/geo/data/divergences_1400.json` itself (the byte-identical
     original);
   - `pipeline/geo/artifacts/divergences_1400.json` (the copy Success
     Condition 5's proof run produces from that same file); and
   - any log line under `pipeline/geo/logs/` (e.g.
     `v1_047_corrections.log`) that quotes that file's content verbatim as
     part of the proof run's own recorded output.

   An occurrence in one of those three locations is excluded from the
   Required Counter **only if** it is traceable, line-for-line, to
   `data/divergences_1400.json`'s own pre-existing content (see the Required
   Counters table below for the exact comparison). Any occurrence anywhere
   else — in particular in any `.py` file, in any path-resolution
   expression, or introduced by any means other than a byte-identical copy
   or verbatim quotation of that one named source file — is not covered by
   this exception, still counts, and must be zero.

   **This condition does not authorize touching `FORBIDDEN_GAME_PATH_MARKERS`
   in `constants.py`.** In iteration 1, that constant's two string literals
   (`"StreamingAssets"`, `"game_unity"`) were split into adjacent literal
   pairs (`"Stream" "ingAssets"`, `"game" "_unity"`) for the sole purpose of
   making a grep-based count of this Required Counter read lower. That is a
   second, unauthorized logic change to `constants.py` beyond the single
   path adjustment this condition permits — forbidden by this condition's
   "nothing else" clause and by the Non-Goals list below, regardless of
   whether the changed lines carried the `# FORGEHISTORY-PATH-ADJUSTMENT`
   marker (the marker denotes the path adjustment; it does not license
   whatever line it is attached to). It must be reverted before this brief
   is re-evaluated: restore both entries to their original single-literal
   form exactly as they appear in `deliverables/pre-port/constants.py.orig`
   — `"StreamingAssets",` and `"game_unity",` — and remove the two now-
   meaningless `# FORGEHISTORY-PATH-ADJUSTMENT` markers from those lines,
   since they do not mark the path adjustment. After the revert, the only
   authorized difference between `pipeline/geo/constants.py` and
   `deliverables/pre-port/constants.py.orig` remains the single path
   adjustment already covered by its existing marked lines in the
   `_PROJECT_ROOT` / `_PROVINCE_COORDS_JSON` expression; nothing else in
   that file may differ from its `.orig`. (Expected, non-regressive side
   effect: reverting this will raise the raw grep count of `game_unity` /
   `StreamingAssets` hits in `constants.py` back to two — those two hits are
   in a `.py` file and are not copies of `divergences_1400.json`'s prose, so
   they are not covered by the exception above and must still be reduced to
   zero the only legitimate way: by the path adjustment itself resolving to
   `legacy_game_data/...`, not by re-splitting the literals.)

4. **`pipeline/geo/.gitignore` excludes `.venv/`** (and may additionally
   exclude `build/` if the Générateur judges that consistent with existing
   ForgeHistory conventions) — the virtual environment itself is never a
   ported or committed artifact, matching `sandbox/geo/.venv/.gitignore`'s
   own exclusion.

5. **The port actually runs, in this repository, and proves determinism by
   command — not by narration.** From `pipeline/geo/`:
   ```
   py -m venv .venv
   .venv/Scripts/pip.exe install -r requirements.txt
   .venv/Scripts/python.exe tests/run_proof_g2.py
   .venv/Scripts/python.exe tests/run_proof_g2b.py
   ```
   Both proof scripts must exit code 0. Each writes its own two-pass
   SHA256 comparison (`logs/v1_046_qa.json`'s `determinism.sha256` for G2,
   `logs/v1_047_qa.json`'s equivalent for G2b) — every compared artifact
   path's two SHA256 values must be equal and non-empty. `run_proof_g2b.py`
   depends on `logs/v1_046_qa.json` already existing, so `run_proof_g2.py`
   must run first.

6. **Every named check in both proof scripts passes green, and every one of
   those checks has an on-record proof that it can go red.** This is not a
   new check to invent — `qa/checks.py` and `tests/test_qa_red_g2.py` /
   `tests/test_qa_red_g2b.py` already implement both the green checks and
   their red-case mutations; porting them unchanged (Success Condition 1)
   and running them (Success Condition 5) is what produces this. The
   verdict must report the `passed` and `red_proof` fields of every entry in
   both qa.json `checks` arrays, not just an aggregate "all green" claim.

7. **`pipeline/geo/README.md` is updated** to state that shared
   infrastructure and the G2 littoral-1400 cluster (coastline + corrections)
   have landed, listing what has NOT yet landed (cells, adjacency, rivers,
   relief, cities, ownership, LOD, id textures — i.e. everything from
   `03_cells.py` onward, per `harness/queue/geo-pipeline-port-plan.md`). It
   must not claim more than this installment delivered.

8. **`deliverables/manifest.json`** declares:
   - a pre-port snapshot of both marked-adjustment originals,
     `deliverables/pre-port/constants.py.orig` and
     `deliverables/pre-port/02_coastline.py.orig` (verbatim copies of the
     VictoriaProject originals, for diffing per Success Condition 3 /
     Required Counters);
   - a pre-edit snapshot of `pipeline/geo/README.md`,
     `deliverables/pre-edit/pipeline-geo-README.md.orig`, with
     `must_differ_from` pointing at the post-edit
     `pipeline/geo/README.md` — this is **the** `must_differ_from` pair this
     brief requires; there are no other before/after artifacts in scope.

9. **The determinism and QA evidence lives on disk, not only in the
   verdict.** `pipeline/geo/logs/v1_046_qa.json`, `v1_046_coastline.log`,
   `v1_047_qa.json`, `v1_047_corrections.log`, and the comparison capture
   `pipeline/geo/capture/v1_046_coastline_compare.png` must all exist after
   Success Condition 5's commands run, and the verdict's cited numbers
   (SHA256 match counts, checks-passed counts) must be traceable to these
   files, not asserted from memory of what the scripts were supposed to do.

## Non-Goals

- Must NOT create, copy, or modify `pipeline/geo/steps/03_cells.py` or any
  step from `04_adjacency.py` onward — that is brief 003 onward's scope
  (`harness/queue/geo-pipeline-port-plan.md`).
- Must NOT port the 60 C# simulation systems or `ARCHITECTURE.md` — both are
  explicitly excluded per `FORGE-HISTORY-BRIEF.md` §3.
- Must NOT create or modify any file under `sim/` — that tree's F1 scope is
  separate work.
- Must NOT modify `docs/adr/0003-single-spatial-primary-key.md` — this brief
  implements what that ADR already unblocked; it does not re-litigate it.
- Must NOT change any logic in `constants.py` or `steps/02_coastline.py`
  beyond the single path adjustment named in Success Condition 3 — any other
  diff line, marked or not, is out of scope and must be reverted. This
  explicitly includes reverting any splitting, re-encoding, or obfuscation
  of `FORBIDDEN_GAME_PATH_MARKERS`'s string literals — such a change is not
  a path adjustment and is not authorized by any Required Counter, including
  the amended `game_unity_reference_remaining_count` (see Success Condition
  3).
- Must NOT leave an unmarked diff between a "byte-identical" file (all 18
  in Success Condition 1's table besides the two adjusted ones) and its
  VictoriaProject original — if a difference is discovered and judged
  necessary, it must be raised as a brief-scope question, not silently
  introduced.
- Must NOT claim "the pipeline runs" from having read the scripts — the
  claim must correspond to an actual, logged execution of Success
  Condition 5's commands in this repository, on this machine, today (hard-won
  rule: presence is not function).
- Must NOT invoke the pip-install / proof-script waiver (Acceptable Waivers
  table) to justify skipping the file copy itself — the file copy and path
  adjustment happen regardless of whether the venv can be built in this
  environment; only the "prove it runs" conditions (5, 6, 9) are excused by
  that specific waiver, and only if its exact command/error is produced.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| byte_identical_ported_files_count | SHA256 (`Get-FileHash`) comparison between each of the 18 non-adjusted files listed in Success Condition 1's table (as ported under `pipeline/geo/`) and its corresponding original under `C:\Users\liagr\VictoriaProject\sandbox\geo\` or `...\game_unity\...` | 18 (must equal 18 — every non-adjusted file matches) |
| path_adjustment_marker_count | occurrences of the literal string `# FORGEHISTORY-PATH-ADJUSTMENT` across `pipeline/geo/constants.py` and `pipeline/geo/steps/02_coastline.py` | must be >= 1 in each of the two files (>= 2 total) |
| path_adjustment_unmarked_diff_line_count | line-by-line diff between `pipeline/geo/constants.py` vs `deliverables/pre-port/constants.py.orig`, and `pipeline/geo/steps/02_coastline.py` vs `deliverables/pre-port/02_coastline.py.orig`, counting only differing lines that do NOT end with `# FORGEHISTORY-PATH-ADJUSTMENT` | total differing lines across both file-pairs (must equal 0) |
| game_unity_reference_remaining_count (AMENDED iteration 2) | grep for the literal substrings `game_unity` and `StreamingAssets` across every file under `pipeline/geo/` after the port. For **each** hit found: (a) determine whether the hit's file is `pipeline/geo/data/divergences_1400.json`, `pipeline/geo/artifacts/divergences_1400.json`, or a log file under `pipeline/geo/logs/` that quotes that file's content; if not, the hit counts. (b) If it is one of those three, compare the hit's exact line, byte-for-byte, against the corresponding line in the VictoriaProject original at `C:\Users\liagr\VictoriaProject\sandbox\geo\data\divergences_1400.json` — if the line matches (i.e. the hit is that source file's own pre-existing content, not a modification or a new insertion), it is excluded and does not count; if it does not match, it is an unexplained occurrence and counts. This is a traceability rule, not a fixed tolerance — the excluded count is derived per-hit from comparison against the named source file, never assumed | must equal 0 after step (b)'s exclusions are applied — every hit not traceable to `data/divergences_1400.json`'s own pre-existing content, including any hit inside a `.py` file or a path-resolution expression, is a real violation |
| determinism_sha_pairs_matched_count | `pipeline/geo/logs/v1_046_qa.json`'s `determinism.sha256` dict + `pipeline/geo/logs/v1_047_qa.json`'s equivalent dict, combined — count of entries where the two SHA256 values are equal and non-empty | total number of keys across both dicts combined (must be equal to the numerator, i.e. 100% match; total key count must be > 0) |
| qa_checks_passed_count | `checks` array in `pipeline/geo/logs/v1_046_qa.json` + `pipeline/geo/logs/v1_047_qa.json`, count of entries with `passed == true` | total entries across both `checks` arrays (must be equal, i.e. all green) |
| qa_checks_red_proof_count | same two `checks` arrays, count of entries with a non-empty `red_proof` string | total entries across both `checks` arrays (must be equal, i.e. every check has a red proof on record) |
| proof_script_exit_code_zero_count | exit codes of `.venv/Scripts/python.exe tests/run_proof_g2.py` and `.venv/Scripts/python.exe tests/run_proof_g2b.py`, run from `pipeline/geo/` | 2 (must equal 2 — both exit 0) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "`10m_physical.zip` cannot be committed to this repository at its current size/location" | `git add pipeline/geo/sources/10m_physical.zip && git commit -m test` (or the repository's configured equivalent) | actual command output showing a real rejection (e.g. a configured size-limit hook or remote rejection) — not a prose estimate of the file's size |
| "`province_coordinates.json` or `province_adjacency.json` cannot be located at the stated VictoriaProject path" | `Get-Item 'C:\Users\liagr\VictoriaProject\game_unity\Assets\StreamingAssets\data\province_coordinates.json'` (or the `province_adjacency.json` equivalent) | command errors with a file-not-found result, proving the path is genuinely absent, not merely asserted |
| "`pip install -r requirements.txt` fails in this environment (e.g. a GDAL-dependent wheel such as `rasterio` or `geopandas` has no compatible build here)" | `.venv/Scripts/pip.exe install -r requirements.txt` | actual pip failure output pasted in full; if invoked, Success Conditions 5, 6, and 9 are excused for **this brief's verdict only** and must be re-raised as a blocking environment issue for the next Planificateur pass — the file copy and path adjustment (Success Conditions 1-4) are never excused by this waiver |
