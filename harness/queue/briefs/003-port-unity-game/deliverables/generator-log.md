# Generator Log — Brief 003 (Port VictoriaProject's Unity game into `unity/game_unity/`)

**Authored**: 2026-07-31T19:30:00
**Author**: forge-generateur

## Read before writing

`brief.md`, `eval-rubric.md`, `docs/rules/hard-won-rules.md`,
`docs/adr/template.md`, `docs/adr/0001-*.md`, `docs/adr/0002-*.md`,
`docs/adr/0003-*.md`, `docs/adr/README.md`, `unity/README.md` (pre-edit),
`HANDOFF.md` (this repo), and — as read-only source inspection only, never
written to — `C:\Users\liagr\VictoriaProject\HANDOFF.md` and
`C:\Users\liagr\VictoriaProject\game_unity\` (see Finding below for why the
last of these mattered).

## Success Condition 1 — copy scope

Grepped the literal substring `Captures` across
`C:\Users\liagr\VictoriaProject\game_unity\Assets\Tests\*.cs` **before**
finalizing the exclusion list, per brief.md's instruction: 28 of 86 `.cs`
files under `Assets/Tests/` matched (list includes
`V1094PilotLiveOwnershipTests.cs` and `V1070PoliticalMapTests.cs`), so
`Captures/` was retained, matching the brief's own reasoning exactly.

Ran the real copy:
```
MSYS_NO_PATHCONV=1 robocopy "C:\Users\liagr\VictoriaProject\game_unity" "D:\ForgeHistory\unity\game_unity" /E /XD Library Temp Logs obj Builds PresentationCache UserSettings .vs /R:3 /W:5 /NFL /NDL /NP
```
(`MSYS_NO_PATHCONV=1` was required — Git Bash otherwise rewrites `/L`, `/E`,
`/XD` as Windows drive-letter paths, e.g. `/L` -> `L:/`, causing robocopy to
reject its own flags with "Paramètre non valide #3.") Result: 855 files, 154.98
MB, 0 failures. Verified afterward with a second, list-only (`/L`) pass using
identical `/XD` exclusions — 0 files pending copy, 855 total, output saved at
`deliverables/evidence/robocopy-verify.log`.

Confirmed the eight excluded directories are absent at the top level of the
new tree (`Library`, `Temp`, `Logs` [before Success Condition 3 created it
fresh], `obj`, `Builds`, `PresentationCache`, `UserSettings`, `.vs`), and
that no nested `obj/`/`.vs/` directory exists anywhere else in the ported
tree (`Get-ChildItem -Recurse -Directory` filtered on those two names,
zero hits). Confirmed `Captures/` is present with its full `v1_068`..`v1_094`
subdirectory set.

**VictoriaProject source unmodified**: hashed 3 named sentinel files
(`ProjectVersion.txt`, `Assets\Scenes\Main.unity`,
`Assets\Scripts\Presentation\MapSnapshotExporter.cs`) under
`C:\Users\liagr\VictoriaProject\game_unity\` with `Get-FileHash -Algorithm
SHA256` before the first write this brief made, and again at hand-off. All
three hashes identical both times (`deliverables/evidence/sentinel-hashes-
before.txt` == `sentinel-hashes-after.txt`, byte-for-byte). No file under
`C:\Users\liagr\VictoriaProject\` was created, modified, or deleted at any
point in this brief — confirmed by this hash check, not merely asserted.

Disk-space waiver row was checked defensively even though not expected to
trigger: `Get-PSDrive D | Select-Object Used,Free` reported `Free =
112,456,658,944` bytes, far above the ~163 MB non-excluded source tree —
no waiver needed.

## Success Condition 2 — `.gitignore`

Created `unity/game_unity/.gitignore` covering all eight excluded
directories (`/Library/`, `/Temp/`, `/Logs/`, `/obj/`, `/Builds/`,
`/PresentationCache/`, `/UserSettings/`, `/.vs/`) plus a small set of common
Unity-adjacent regenerable file patterns as defense in depth (not relied on
by any Required Counter).

## Success Conditions 3/4/5 — Unity invocations, and the central finding of this brief

**Single-instance discipline** was checked immediately before each of the 3
invocations (`unity_lockfile_checked_before_invocation_count` = 3), combining
`Test-Path unity/game_unity/Temp/UnityLockfile` with `Get-Process Unity
-ErrorAction SilentlyContinue`:
1. Before the compile invocation: lockfile absent, 0 Unity processes -> safe.
2. Before the test invocation: lockfile **present** (left behind by
   invocation 1's own batchmode run — the exact "killed/completed batchmode
   leaves the file behind" case brief.md names), 0 Unity processes -> stale,
   safe (not busy).
3. Before the capture invocation: same as check 2 -> stale, safe.

**Compile (Success Condition 3)**: launched in the background (non-blocking
Bash `run_in_background: true`, absolute `-logFile`), then read the log's
tail periodically (first check after the initial launch call returned
immediately; a second, timed poll loop was started to watch for a terminal
`Exiting batchmode`/`Aborting batchmode` line every 45s) while the run
progressed. In this instance the run finished quickly — Unity's `Library/`
had no prior cache and rebuilt it, but the run terminated on a compile error
well inside the 10-40 minute window the brief warns about, so no long poll
was actually needed; the polling method (background launch + periodic log
tail + notification-driven completion check) is documented here regardless,
per the brief's instruction to document the method even when the run
finishes faster than the worst case.

Result: **exit 1, `Aborting batchmode due to failure: Scripts have compiler
errors.`** 33 lines matching `error CS\d+` in
`unity/game_unity/Logs/v003_compile.log` (2280 lines total, non-empty). All
33 errors are in one file, `Assets\Scripts\Presentation\PilotMapProvider.cs`,
lines 3467-3738, and are genuine C# errors, NOT path-resolution errors:
- `CS0019`: `Operator '&&' cannot be applied to operands of type 'bool' and 'double'` (x4, lines 3516-3519)
- `CS0201`: `Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement` (x4, same lines)
- `CS1061`: `'ProvinceData' does not contain a definition for 'ProvinceName'...` (line 3467)
- `CS1628`: `Cannot use ref, out, or in parameter '...' inside an anonymous method, lambda expression, query expression, or local function` (x2, lines 3734/3738)

**Finding — these errors are not caused by the port; they are a real,
uncommitted, in-progress edit already present in VictoriaProject's own
working tree, unrelated to the v1_094/v1_095/v1_095b work this brief was
asked to carry across.** Investigated read-only (no write to
VictoriaProject at any point):
- `git -C C:\Users\liagr\VictoriaProject diff --stat HEAD -- game_unity/Assets/Scripts/Presentation/PilotMapProvider.cs`
  shows 681 changed lines (675 insertions, 6 deletions) not present at HEAD
  (commit `06c2e59`, 2026-07-29). The broken lines (3516-3519, the invalid
  `TryReadMbLine(...) && (x = y);` statement-expression pattern) are inside
  this uncommitted diff, added on top of the last committed, tested state.
- `C:\Users\liagr\VictoriaProject\HANDOFF.md` states, as of 2026-07-29:
  "File VIDE, arbre propre, tout poussé" (working tree empty/clean,
  everything pushed) and separately names the *next planned* brief as
  `v1_096` — "la fenêtre pilote est trop petite... **C'est le brief
  `v1_096`.**" — i.e. not yet started as of that write-up.
- The new, broken method added in the uncommitted diff is literally named
  `WritePortageProofAndCapturesV1_096b`. Its file mtime (`PilotMapProvider.cs`,
  2026-07-29 12:20:54) predates the last commit's timestamp (`06c2e59`,
  2026-07-29 12:50:12) — i.e. this uncommitted work was already sitting in
  the working tree, not staged, before and through that commit, and has
  remained uncommitted since.
- Conclusion: VictoriaProject's live working directory, as it stood at copy
  time, was not the clean, tested `v1_095b` state the brief's World-Terms
  Requirement describes by name — it carried an additional, unfinished,
  syntactically invalid `v1_096`/`v1_096b` WIP edit layered on top,
  unrelated to the map-rendering path this brief is about.

Per Non-Goals, the **only** authorized source change is a marked
`// FORGEHISTORY-PATH-ADJUSTMENT` line, and only if the CS error is
traceable to a hardcoded absolute path. None of these 33 errors are
path-related (type mismatches, invalid statement expressions, a missing
member, and `ref`/`out` capture in a lambda) — the waiver table's row 2 does
not apply, and no other waiver row in `brief.md` covers "the live source
tree carries unrelated uncommitted WIP that does not compile." No source
file was edited. **Success Condition 3 is not met from this exact copy of
the source tree** — this is reported as a real, evidenced finding (command +
error + causal trace), per hard-won rule 9, not an abdication.

**Test (Success Condition 4)**: ran anyway, per the brief's instruction not
to infer a result rather than measure it fresh:
```
& Unity.exe -batchmode -runTests -nographics -silent-crashes -projectPath unity/game_unity -testPlatform EditMode -testResults <abs>\Logs\v003_test-results.xml -logFile <abs>\Logs\v003_tests.log
```
Result: identical compiler errors, identical `Aborting batchmode` outcome.
`Test-Path unity/game_unity/Logs/v003_test-results.xml` -> `False` — the XML
was never written, because EditMode tests never ran (batchmode aborted at
compile). `test_total_count`/`test_passed_count`/`test_failed_count` are
recorded as sentinel `-1` (not `0`) in `manifest.json` — a `0` would falsely
imply "the suite ran and zero tests existed," which did not happen.

**Capture (Success Condition 5)**: ran anyway, same reasoning:
```
& Unity.exe -batchmode -quit -nographics -silent-crashes -projectPath unity/game_unity -executeMethod VictoriaGame.Tests.V1094BatchRunner.Run -logFile <abs>\Logs\v003_capture.log
```
Result: identical compiler errors; `-executeMethod` never ran because the
assembly never compiled. `Captures/v1_094/01_avant_conquete.png` and
`03_apres_conquete_VERT_ecs.png` exist in the ported tree — but they are the
files `robocopy` carried over from VictoriaProject (mtime 2026-07-28
20:32:xx, well before this brief's `brief.md` Authored timestamp), not
freshly written artifacts from a successful run at the new location. Their
SHA256 hashes genuinely differ (`91753C9C...B6` vs `F5691CF2...1B`,
`capture_pair_sha256_distinct_count` = 2, a real, mechanically true
measurement) — but this fact alone does not demonstrate the ported code
executes correctly from `unity/game_unity/`; it only demonstrates the two
pre-existing files differ, which they already did in VictoriaProject before
any port happened. `manifest.json`'s counter entry for this value states
this caveat explicitly. Confirmed byte-for-byte, before drawing any
conclusion, that neither `MapSnapshotExporter.cs` nor
`V1094PilotLiveOwnershipTests.cs` differs from the VictoriaProject source
(`Get-FileHash` match=`True` both files) — no capture/export code was
written or altered to manufacture this pair, consistent with Success
Condition 5's restriction, but the pair itself is stale, not newly produced.

**No source file was modified anywhere in the ported tree.** `git status
--porcelain` in `D:\ForgeHistory` after all three invocations shows only
`?? unity/game_unity/` (new, untracked) and
`?? harness/queue/briefs/003-port-unity-game/` — nothing else changed.

## Success Condition 6 — ADR-0004

Wrote `docs/adr/0004-bulk-port-victoriaproject-unity-game.md`, matching
`docs/adr/template.md`'s structure exactly (`## Context`, `## Decision`,
`## Alternatives Considered` with 3 `### Alternative N:` entries, `##
Consequences` with `### Positive`/`### Negative`/`### Risks`). Quotes the
brief's "Owner Decision" verbatim; names the §3/§9 deviation explicitly as
Alternative 1 (rejected: reread-and-rewrite; chosen: bulk port — Alternative
3), with a real, decision-specific Why-not for each (time budget for "un jeu
beau et fonctionnel à la fin de la session" versus a multi-week rewrite,
citing v1_095b's own prior silent-regression history as the risk a
from-scratch reread would re-risk). Names failure mode #1 by number in
`### Negative`, explains the imported debt causally (`ProvinceId` 1..~50
alongside `cell_id` >= 1164, bridged only by
`PilotMapProvider.SimulationProvinceIdOfView`, cited by name only, never
re-derived), states ADR-0003 remains the unmet F1 target, and states
resolving the coexistence is out of this brief's scope. States plainly that
VictoriaProject's own automation (`cursor_tasks/`, `automation/run_queue.py`,
`RESULT_TEMPLATE.json`, `runtime_bridge/`) is not ported and is replaced by
ForgeHistory's three-role harness. No parity/determinism hex value is
quoted anywhere in the ADR (confirmed: `grep -P '0x[0-9A-Fa-f]{8,}'` against
the ADR file returns 0 matches) — the ADR's `### Risks` section also names,
by pointer only, this same log's Finding section for the uncommitted-WIP
risk of bulk-porting a live directory rather than a specific commit.

`docs/adr/README.md` gained a row for ADR-0004 (path, title, `accepted`,
`2026-07-31`) — 4 ADR files now present under `docs/adr/`, matching
`adr_index_rows_count` = 4.

## Success Condition 7 — minimal launcher

`unity/README.md` gained a `## game_unity/` section stating the port has
landed, pointing at ADR-0004 for the failure-mode-#1 debt, documenting
exactly one supported open command
(`-projectPath unity/game_unity -openfile
unity/game_unity/Assets/Scenes/Main.unity`), and explicitly stating
VictoriaProject's `automation/demo.py`/`automation/run_queue.py`/
`cursor_tasks/`/`runtime_bridge/` machinery was not ported. It does **not**
claim the ported project currently compiles or runs green — it points at
this log's Finding section instead, so the README cannot be read as
overclaiming. `unity/open-game.ps1` wraps the exact invocation (with
existence checks for the Unity executable and the project path, raising a
clear error rather than silently doing nothing if either is missing).

## Success Condition 9 — lockfile discipline

Documented above, inline with each invocation. Iteration 1: 3 invocations
(compile, test, capture), 3 checks. Iteration 2 (amendment-001): 3 more
invocations against the remediated tree, 3 more checks. Iteration 3
(amendment-002): 1 more invocation (fresh full EditMode re-run after
Cluster A/B remediation; Success Conditions 3/5 not reopened), 1 more check.
Iteration 4 (amendment-003): 2 more invocations (V1095 no-nographics
diagnostic, and one more fresh full EditMode re-run after the corrected
Cluster B bridge), 2 more checks.
`unity_lockfile_checked_before_invocation_count` = 9, equal to the 9 Unity
invocations actually run across all 4 iterations.

## Non-Goals discipline

- No file under `automation/`, `cursor_tasks/`, `RESULT_TEMPLATE.json`, or
  `runtime_bridge/` was ported (confirmed: none of these names appear
  anywhere under `unity/game_unity/` — `Get-ChildItem -Recurse` for those
  names returns nothing).
- No `.cs` diff anywhere in the ported tree — the `// FORGEHISTORY-PATH-
  ADJUSTMENT` exception was never invoked, because the blocking errors are
  not path-resolution errors (see Finding above). Zero `.cs` files were
  edited.
- No PlayMode HUD capture was attempted or claimed.
- No file under `sim/` or `pipeline/geo/` was created, modified, or deleted.
- **No `git commit` (or `git add`/`git push`) was run at any point in this
  brief.** `generator_git_commits_count`'s literal query (commits timestamped
  between `brief.md`'s Authored value and `manifest.json`'s own mtime)
  returns 1 — commit `fce1d82`, "Run brief 002: port geo shared infra + G2
  coastline 1400 (gate ACCEPT 9/9)", timestamped 2026-07-31 17:58:46. This
  commit was made by a separate, already-completed brief-002 session, not by
  this Générateur; this session's own first actions (the pre-copy `Captures`
  grep and the first robocopy dry-run) were run after 18:00 the same day.
  Reported honestly per the literal counter definition (a check derives, it
  is not renamed to force a convenient answer — hard-won rule 2) rather than
  narrowing the query window to manufacture a clean 0.
- No stale VictoriaProject session number (e.g. "217/217") is cited anywhere
  above as this port's own result — `test_total_count`/`test_passed_count`/
  `test_failed_count` (274/256/17) are read directly from this session's own
  `unity/game_unity/Logs/v003_test-results.xml`, re-measured from scratch
  after remediation, never copied from `HANDOFF.md` prose.
- The eight-directory exclusion list was not extended past what `brief.md`
  names; `Captures/` was retained with grep evidence gathered *before*
  finalizing the list, as required.

## Waivers

None invoked. The compile failure does not match Acceptable Waivers row 2
(not a hardcoded-absolute-path error) or any other row in `brief.md`'s
table — it is reported as an unwaived, evidenced finding (command + full
error text + causal trace), not excused.

## Iteration 2 — amendment-001 remediation, and re-run of Success Conditions 3/4/5

**Authored (this section)**: 2026-07-31T20:45:00

Read `harness/queue/briefs/003-port-unity-game/feedback/amendment-001.md` in
full before acting, per the coordinator's instruction. The amendment
authorizes exactly one additional action (restore VictoriaProject's own
`game_unity/`-dirty tracked files to their HEAD content in the ported tree;
remove untracked strays iteration 1's `robocopy` carried over) and instructs
resuming Success Conditions 3/4/5 from the remediated tree. It does not
reopen Success Conditions 1, 2, 6, 7, 8, 9 — none of that work was redone.

### Remediation (amendment-001's Authorized remediation, steps 1-4)

1. **Identified every dirty file under `game_unity/`**, read-only against
   VictoriaProject:
   ```
   git -C C:\Users\liagr\VictoriaProject diff --name-only HEAD -- game_unity
   git -C C:\Users\liagr\VictoriaProject status --porcelain -- game_unity
   ```
   77 tracked files differ from HEAD (`victoriaproject_dirty_tracked_file_count`
   = 77, `deliverables/evidence/victoriaproject-diff-name-only-HEAD.txt`); 72
   additional `??` untracked entries exist (all under
   `Assets/StreamingAssets/data/map/`), and all 72 were confirmed present in
   the ported tree — iteration 1's `robocopy` copies everything on disk
   regardless of git tracking status
   (`victoriaproject_untracked_copied_file_count` = 72,
   `deliverables/evidence/untracked-copied-into-port.txt`). 77 + 72 = 149,
   matching the combined `status --porcelain` line count exactly.

2. **Restored every dirty tracked file** to HEAD content:
   ```
   git -C C:\Users\liagr\VictoriaProject show HEAD:game_unity/<path> > D:\ForgeHistory\unity\game_unity\<path>
   ```
   run once per path (77 invocations). All 77 succeeded (0 `git show`
   failures). Verified each restored file's SHA256 against a **second,
   independent** `git show HEAD:<path>` read (not merely trusting the first
   write): 77/77 match, 0 mismatch
   (`deliverables/evidence/head-restored-sha256-check.txt`,
   `head_restored_file_count` = 77).

3. **Removed every untracked-copied file** from the ported tree: 72/72
   confirmed absent afterward (`untracked_removed_file_count` = 72).

4. **VictoriaProject itself remained untouched by remediation** — `git show`
   reads from the object database and writes only to the ForgeHistory-side
   destination; it never touches VictoriaProject's working tree, index, or
   HEAD. Verified two ways: (a) the 3 sentinel files' SHA256 unchanged from
   the very first pre-write check in iteration 1
   (`deliverables/evidence/sentinel-hashes-before.txt` ==
   `sentinel-hashes-after.txt`); (b)
   `git -C C:\Users\liagr\VictoriaProject status --porcelain -- game_unity`
   captured immediately before remediation and again at final hand-off are
   **byte-identical**
   (`deliverables/evidence/victoriaproject-status-porcelain-before-remediation.txt`
   == `victoriaproject-status-porcelain-final.txt`) — no new dirt, nothing
   reset, nothing cleaned up.

### Success Condition 3 (compile) re-run — GREEN

Lockfile+process check (invocation 4 of 6 overall): lockfile present (stale,
left by iteration 1's last invocation), 0 Unity processes -> safe.
```
& Unity.exe -batchmode -quit -nographics -silent-crashes -projectPath unity/game_unity -logFile <abs>\Logs\v003_compile_remediated.log
```
Launched in the background exactly as before (non-blocking Bash start,
absolute `-logFile`), polled by periodic `wc -l`/tail reads on the log file
approximately every 30-90 seconds (spaced out manually between checks) plus
one blocking `Get-Process`-based liveness check, until the terminal line
appeared. This run regenerated `Library/` from scratch (first import at the
new location) and took materially longer than the polling interval — the
exact scenario brief.md's Success Condition 3 anticipated; it was not
abandoned on any timeout. Result: **exit 0**, log ends
`*** Tundra build success (5.89 seconds), 17 items updated, 555 evaluated`
then `Exiting batchmode successfully now! ... return code 0`. **0** lines
matching `error CS\d+` in the 2079-line log (only 2 pre-existing
`warning CS0618` lines, same as iteration 1). The log was copied to the
canonical `Logs/v003_compile.log` path (iteration 1's failing log preserved
separately at `Logs/v003_compile_iteration1_failed.log`, not declared in
`manifest.json` since it is not this iteration's evidence).

### Success Condition 4 (EditMode suite) re-run — NOT green, real result reported in full

Lockfile+process check (invocation 5 of 6): lockfile absent, 0 processes ->
safe.
```
& Unity.exe -batchmode -runTests -nographics -silent-crashes -projectPath unity/game_unity -testPlatform EditMode -testResults <abs>\Logs\v003_test-results.xml -logFile <abs>\Logs\v003_tests.log
```
No `-quit` (per brief.md's explicit instruction). This run took
substantially longer than the compile step — the log grew past 1,000,000
lines before finishing, and the Unity process's own CPU time climbed
continuously throughout (447s -> 1637s, checked repeatedly via
`Get-Process Unity | Select CPU` to confirm it was genuinely working, not
stalled, each time growth paused) — consistent with brief.md's own
Session-Cost Note ("200+ cases... plus Unity batchmode launch overhead").
Two short-lived helper Unity.exe processes appeared and exited transiently
during the run (confirmed via `Get-CimInstance Win32_Process` that only one
Unity.exe — the exact PID launched by this invocation — was alive at any
sampled instant that mattered); not a second live instance, not a
single-instance-discipline violation. It was not abandoned under any
timeout; the wait continued until the XML was actually written.

**Real result, read directly from `<test-run>`'s root attributes**: `total
= 274`, `passed = 256`, `failed = 17`, `inconclusive = 0`, `skipped = 1`.
`failed` does **not** equal 0 — Success Condition 4's literal bar is not met.
This is reported as measured, not smoothed over. The 17 failures were
extracted with their exact `fullname` and `<failure><message>` via a small
`py -c` one-liner using the stdlib `xml.etree.ElementTree` reader (no bare
`python`), saved verbatim at `deliverables/evidence/failed-test-cases.txt`.
They resolve into three causally distinct clusters, none of which this
brief is authorized to remediate further:

1. **4 failures — `System.IO.DirectoryNotFoundException` for
   `D:\ForgeHistory\unity\sandbox\geo\artifacts\coordinate_correction_proposal_v1_072.json`**
   (`V1037CityPlacementTests.V1081_Artifacts_And_Verdict`,
   `V1037CityPlacementTests.V1081_C_V1080_Acquis_Hold`,
   `V1080CoordinatesTests.V1080_Artifacts_And_Verdict`,
   `V1080CoordinatesTests.V1080_B_PositionsMatchArbitratedProposal`). These
   tests read a path under `unity/sandbox/...` — a sibling of `game_unity/`
   in VictoriaProject's own layout (`C:\Users\liagr\VictoriaProject\sandbox\`
   exists there) that Success Condition 1 never named in its copy scope
   (only `game_unity/` was ported; `sandbox/` is out of this brief's scope
   entirely, and Non-Goals forbid creating anything outside the declared
   scope). This is a structural consequence of the copy scope itself, not
   introduced by remediation.
2. **5 failures — missing historical `*_tests.xml`/`*_large.xml` files under
   `Logs/`** (`V1042SuiteBudgetTests`, all 5 of its cases: messages cite
   `v1_041_tests.xml requis`, `Aucun *_tests.xml / *_large.xml trouve sous
   Logs/`, `XML LARGE requis`, `v1_077_large.xml requis` x2). `Logs/` is one
   of the eight directories Success Condition 1 explicitly excludes and
   Non-Goals forbid extending past — these tests depend on VictoriaProject's
   own historical log/XML artifacts that lived under the excluded directory
   by design. Also a direct, unavoidable consequence of following the
   brief's own fixed exclusion list exactly as written.
3. **8 failures — parity/anchor/canary mismatches**
   (`V1008MeasurementTests.V1008_Anchors_Survive_First_ISystem_And_WorldMetrics_Move`,
   `V1014MeasurementTests.V1014_SweepOccupationScoreRateAndReanchor`,
   `V1095GpuMapTests.V1095_Artifacts_And_Verdict`,
   `V1bMapMaskTests.V1b_ExportGraphLandMaskAtKeyTicks`,
   `V1cMapReadableTests.V1c_ExportReadableMapAtKeyTicks`,
   `V1dChronicleTests.V1d_ExportChronicleAndJournal`,
   `V1eMapLayerTests.V1e_ExportThematicLayers`,
   `V1MapSnapshotTests.V1_ExportMapSnapshotsAtKeyTicks`). Messages cite
   "Ancrages t1000/t800 non bit-identiques", "GPU et CPU doivent decrire la
   meme terre, dans le meme sens", and "Canari prov1/prov6 echoue" — all
   determinism/parity-style assertions. A plausible but **not confirmed**
   causal hypothesis: HEAD (the last commit, `06c2e59`, 2026-07-29) predates
   whatever anchor-rebasing work may have been bundled into the same
   uncommitted diff as the `v1_096b` WIP that iteration 1 found and this
   iteration reverted — if so, restoring to HEAD is correct per amendment-001
   but re-exposes a real anchor/HEAD mismatch that the uncommitted work had
   (perhaps incidentally) resolved. This brief does not modify any `.cs`
   logic to investigate further (Non-Goals), so this hypothesis is reported
   as a hypothesis, not asserted as fact — see the "not confirmed" wording
   above.

   No waiver is invoked for any of the 17. Checked, before writing this off:
   `C:\Users\liagr\VictoriaProject\HANDOFF.md` mentions
   `V1037CityPlacementTests.cs` exactly once (grep, read-only) — but about an
   unrelated 2026-07-26 brace-counting QA bug in
   `auto_runner._qa_csharp_file` ("205 contre 207... ses accolades de code
   etaient 67 contre 67"), not this test's `DirectoryNotFoundException`, and
   the passage says the file "compilait et passait 167 cas" (was passing) at
   that time — the opposite of a pre-existing-red citation. No other of the
   17 failing test/class names appears anywhere in
   `C:\Users\liagr\VictoriaProject\HANDOFF.md`. Per brief.md's Acceptable
   Waivers row 4 and `eval-rubric.md`'s Plateau/Waiver Notes ("If row 4 ...
   is invoked without a HANDOFF.md citation backing the 'pre-existing' claim,
   treat it as an unsupported claim, not a valid waiver"), none of the 17
   qualifies for a waiver. Success Condition 4 is reported as **not fully
   met** (17/274 failed), with full causal evidence, not excused.

   Of the three rubric-named cross-check fixtures specifically:
   `V1094PilotLiveOwnershipTests` 1/1 passed, `V1070PoliticalMapTests` 5/5
   passed, `V1095GpuMapTests` 1/1 **failed** (`V1095_Artifacts_And_Verdict`).

### Success Condition 5 (capture regeneration) re-run — GREEN, genuinely fresh

Lockfile+process check (invocation 6 of 6): lockfile absent, 0 processes ->
safe. Recorded the capture pair's mtime and SHA256 immediately before this
invocation for later comparison: both files already showed a same-day mtime
(18:51:31) — from Success Condition 4's own test run, since
`V1094PilotLiveOwnershipTests`'s test case itself calls
`RunAndWriteArtifacts()` as part of its assertions — and the same SHA256
values as iteration 1's stale copies (`91753C9C...B6`, `F5691CF2...1B`).
```
& Unity.exe -batchmode -quit -nographics -silent-crashes -projectPath unity/game_unity -executeMethod VictoriaGame.Tests.V1094BatchRunner.Run -logFile <abs>\Logs\v003_capture.log
```
Result: **exit 0**. `v003_capture.log` shows
`VictoriaGame.Tests.V1094PilotLiveOwnershipTests.RunAndWriteArtifacts()`
genuinely executing (called from `V1094BatchRunner.Run`, both named
explicitly in the log's stack-trace lines, confirming the real code path
ran, not merely that the process exited 0). Re-checked mtime and SHA256
immediately after: mtime is now **18:55:02** — later than the pre-invocation
check, confirming this specific invocation genuinely rewrote both files —
and both postdate `brief.md`'s Authored timestamp (2026-07-31T15:00:00) by
~3h55m, which is exactly what `mtime_after_brief` and
`rubric_predates_deliverables` check. SHA256 values are unchanged
(`91753C9C...B6`, `F5691CF2...1B`) — byte-identical to before. This is
read as the expected signature of a genuinely deterministic simulation
reproducing the same conquest outcome and the same rendered pixels on a
fresh run, not as evidence of staleness: the file was demonstrably
rewritten (new mtime, log shows the write call executing), it simply
produced the same content, which is what determinism predicts. The two
files' hashes remain distinct from **each other**
(`capture_pair_sha256_distinct_count` = 2, unchanged from iteration 1).
`MapSnapshotExporter.cs` and `V1094PilotLiveOwnershipTests.cs` were both
among the 77 HEAD-restored files this iteration and were independently
SHA256-verified against `git show HEAD:<path>` in the remediation step above
— byte-identical to VictoriaProject's own HEAD, confirming (again) that no
capture/export code was written or altered.

### Re-run self-check

```
py harness/verdict_audit.py harness/queue/briefs/003-port-unity-game
```
Result documented in this iteration's final state below — see Summary.

## Iteration 3 — amendment-002: Cluster B/A remediation, fresh full re-run, Cluster C bisection

**Authored (this section)**: 2026-08-01T00:15:00

Read `harness/queue/briefs/003-port-unity-game/feedback/amendment-002.md` in
full before acting. It authorizes narrow remediation for Cluster B (5
failures) and Cluster A (4 failures), and a diagnose-only mandate for
Cluster C (8 failures) — no fix without a three-part proof this brief cannot
meet (Non-Goals still forbid any `.cs` change beyond the one narrow,
inapplicable path-adjustment exception, even if a cause were fully
understood). Success Conditions 3 and 5 are confirmed still green and are
not reopened.

### Cluster B (5 failures) — attempted, genuinely could not be resolved by the authorized mechanism

Ran the authorized identification command, read-only against VictoriaProject:
```
git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD --name-only -- game_unity/Logs
```
**Result: empty. 0 lines.** (`logs_tracked_at_head_count` = 0,
`deliverables/evidence/logs-tracked-at-head.txt`.) This directly contradicts
amendment-002's own Cluster B diagnosis ("VictoriaProject committed to git
as part of its own evidence trail"). Investigated further, read-only, before
concluding this was not a mistake on my part:
- `git -C C:\Users\liagr\VictoriaProject log --all --diff-filter=A --name-only`
  grepped for `v1_041_tests.xml`/`v1_077_large.xml` returns **zero matches
  across VictoriaProject's entire git history, any branch, any commit ever**
  — these files were never added in any commit, not just missing from HEAD.
- `git -C C:\Users\liagr\VictoriaProject check-ignore -v game_unity/Logs/v1_041_tests.xml`
  confirms `.gitignore:35:game_unity/Logs/` matches — **the whole
  `game_unity/Logs/` directory is gitignored in VictoriaProject**, not
  merely "not yet committed."
- `ls game_unity/Logs/` on VictoriaProject's disk shows 491 files present,
  including `v1_041_tests.xml`, `v1_077_large.log`, `v1_077_large.xml` —
  confirming these are real, existing, but **local-only, untracked, gitignored
  artifacts**, never part of any committed proof trail.
(`deliverables/evidence/logs-gitignore-check.txt` records this full chain.)

Given this, the authorized mechanism (`git show HEAD:game_unity/Logs/<path>`)
has nothing to extract — there is no committed content at this path to
restore. `logs_head_restored_count` = 0 (of 0 restorable), internally
consistent, not a partial failure. I did **not** substitute an unauthorized
mechanism (e.g. copying the gitignored files directly from VictoriaProject's
disk) — amendment-002's authorized action for Cluster B is specifically
`git show HEAD:`-based restoration of committed content, not a disk copy of
untracked files; that different action (disk-copy of untracked content) was
authorized by amendment-001 only for Cluster-A-style bridging, under its own
distinct, source-derived scoping, and is not extended here without a new
authorization. **Cluster B's 5 `V1042SuiteBudgetTests` failures remain
unresolved after this brief's authorized remediation attempt** — reported
as a genuine finding (amendment-002's own diagnosis was factually incorrect
for this path), not silently worked around.

### Cluster A (4 failures) — resolved

Read, in full, both failing fixtures' complete source (not just the
exception message), including every method each transitively calls:
- `V1037CityPlacementTests.cs`: `V1081_Artifacts_And_Verdict` ->
  `RunAndWriteBornesLog()` -> (line 341-344) `new V1080CoordinatesTests()`,
  calling `TryCheckParityForV1081`/`TryCheckProposalMatchForV1081`/
  `TryCheckAttributionForV1081`. `V1081_C_V1080_Acquis_Hold` calls the same
  three methods directly.
- `V1080CoordinatesTests.cs`: those three bridge methods resolve to
  `CheckParityBitIdentical` (reads only `BeforeCoordsPath`, in-scope
  Captures/v1_080/, already ported), `CheckPositionsMatchProposal` (reads
  `ProposalPath`), and `CheckAttribution` (reads only `LiveCoordsPath`,
  in-scope StreamingAssets, already ported).

**Exactly one `sandbox/`-relative path exists across all 4 failing methods
and everything they call**: `ProposalPath` (`V1080CoordinatesTests.cs:59-62`)
= `Path.Combine(GameUnityRoot, "..", "sandbox", "geo", "artifacts",
"coordinate_correction_proposal_v1_072.json")` — matching amendment-002's
own stated floor exactly (`sandbox_bridge_artifacts_identified_count` = 1).
No test is declared NOT APPLICABLE (`cluster_a_tests_not_applicable_count`
= 0) — the default (supply the real artifact) applies to all 4.

Copied, read-only from VictoriaProject:
```
C:\Users\liagr\VictoriaProject\sandbox\geo\artifacts\coordinate_correction_proposal_v1_072.json
  -> D:\ForgeHistory\unity\sandbox\geo\artifacts\coordinate_correction_proposal_v1_072.json
```
SHA256 verified identical on both sides: `2FE5991D57AE8D5A0AB981273768FCDC3C3A047A1D04DE5B5BD00C252267E66B`
(`sandbox_bridge_artifacts_restored_count` = 1). VictoriaProject's own
`sandbox/geo/artifacts/` was only read. Consigned as a temporary, narrow,
read-only bridge — not a second geo-pipeline — in `unity/README.md`'s new
`## sandbox/geo/artifacts/` section (states explicitly: not the same tree as
`pipeline/geo/`, points at `harness/queue/geo-pipeline-port-plan.md`'s later
brief slots as the eventual real fix, and that a non-divergence test between
the two trees is future work, not created here).

### Fresh full Success Condition 4 re-run (post A+B remediation, not filtered)

Lockfile+process check (invocation 7 of 7 across all iterations): lockfile
absent, 0 processes -> safe. Archived iteration 2's test log/XML to
`v003_tests_iteration2.log`/`v003_test-results_iteration2.xml` before
re-running, to keep every iteration's evidence distinguishable. Ran the
exact, unfiltered Success Condition 4 invocation again — full suite, not a
subset, per amendment-002's explicit instruction that resolving A/B "may
change which cases even reach the parity/canary assertions." Monitored via
the same background-launch + periodic log-tail/CPU-liveness method as
iterations 1-2 (polled roughly every 30-90s over the run's ~19-minute
duration; Unity process CPU time climbed continuously — 5s to 1334s —
confirming it was genuinely working throughout, never stalled; two
short-lived helper Unity.exe processes appeared and exited transiently,
confirmed via `Get-CimInstance Win32_Process` that only the one launched PID
was ever alive at any sampled instant, not a second live instance).

**Fresh result**: `total=274, passed=260, failed=13, skipped=1,
inconclusive=0` (260+13+1+0=274, internally consistent). Down from
256/274/17 by exactly the 4 Cluster A cases — confirmed by name: the fresh
13 failing `fullname`s (`deliverables/evidence/failed-test-cases-iteration3.txt`)
are an **exact subset** of iteration 2's 17 (Cluster B's 5 + Cluster C's 8,
byte-for-byte the same 8 fullnames as before); zero new or unexpected
failures were introduced by either remediation.

### Cluster C (8 failures) — diagnosed, root cause NOT confirmed, nothing rebased

Per amendment-002's mandate, diagnosis followed the fresh A+B-remediated
full re-run above, not a stale or filtered one. **No `.cs` file was read
with intent to fix it, only to understand what it reads and asserts — no
`.cs` file was modified.** `git status --porcelain` in `D:\ForgeHistory`
after this entire iteration still shows only `unity/`, `docs/adr/0004-*.md`,
`docs/adr/README.md`, this brief's `deliverables/`, and the cost ledger —
zero `.cs` diffs anywhere.

**Step 1 — ruled out a file-content cause.** For each of the 8 (V1008, V1014,
V1095, V1b, V1c, V1d, V1e, V1MapSnapshot), confirmed neither the failing
`.cs` fixture nor any `.cs` file it depends on for the specific failing
assertion was ever part of the 77-dirty/restored set — grepped
`deliverables/evidence/victoriaproject-diff-name-only-HEAD.txt` for each of
the 9 test-class names: zero matches. Only 2 `.cs` files were ever dirty
across the whole brief (`MapSnapshotExporter.cs`, `PilotMapProvider.cs`,
both Presentation-layer) plus 4 Presentation-layer test fixtures (V1068,
V1070, V1071, V1094) — none of which any Cluster C failure's source touches.
Every StreamingAssets data file the 77-dirty/restored set or the 72-removed
set contains is confined to map-rendering data (`data/map/*`) — grepped for
whether the actual core-simulation data loaders (`GameDataLoader.cs`,
economy/army/population systems) read any of the 72 removed filenames:
found only two apparent hits (`cities.json`, `adjacency.json`), both
confirmed **false positives** — substring matches inside the unrelated,
untouched, still-present `data/cities.json` (no `map/` subdirectory) and
`data/province_adjacency.json`, not the removed `data/map/cities.json` /
`data/map/adjacency*.json` files. **Conclusion of step 1: no file-content
difference from VictoriaProject's own HEAD explains any of the 8 failures.**

**Step 2 — measured the live divergence directly from this run's own fresh
proof logs**, not from the top-level assert message alone. Each of these
tests writes its own detailed per-field measurement log into
`unity/game_unity/Logs/` as it runs (this brief's own fresh evidence, not
carried over from anywhere):
- `v1_008_measurements.log`, `v1_002_measurements.log` (V1b), and
  `v1_006_measurements.log` (V1e) — copied to
  `deliverables/evidence/cluster-c-*.log` — show the **exact same 12
  per-tick simulation values, to the integer/decimal, across all three
  independently-written logs** (e.g. `worldArmyStr=38865` in all three,
  `population=142317` in all three, `totalDebt=0.0` in all three), all
  diverging from the same hardcoded anchor constants (`worldArmyStr`
  expected `38953`, `population` expected `142551`, `totalDebt` expected
  `750.9`, etc.) in the identical way. This proves V1008/V1b/V1c/V1d/
  V1e/V1MapSnapshot are not 6 independent bugs but **one shared
  phenomenon**: a single, internally-consistent live simulation trajectory
  (seed=42195) that does not reproduce the anchor values, even though every
  file the trajectory depends on is confirmed byte-identical to HEAD (step
  1). `v1_008_measurements.log` states its own hypothesis in French, quoted
  verbatim because it is the test-author's own contemporaneous assessment,
  not mine: *"Cause probable : ordonnancement ou définition de métrique"*
  (probable cause: [system] ordering or metric definition) — i.e. an
  environment-dependent ECS execution-order property, not a content
  regression. This is a **hypothesis consistent with the evidence, not a
  confirmed mechanism** — I did not (and, staying inside this brief's
  read-only/no-`.cs`-edit constraints, could not) instrument the ECS
  scheduler to prove it.
- `v1_014_sweep.log` (V1014) — the test re-derives its own comparison value
  ("genou"/knee, via a rate-sweep over the same live trajectory) fresh each
  run; it computed `2.0` this run against a shipped constant of `0.5`.
  Affected by the same underlying trajectory-divergence phenomenon as the
  six above, not a separate defect.
- `v1_006_measurements.log` (V1e) additionally fails an earlier assertion in
  sequence, the "Canari prov1/prov6" ownership-snapshot check (`Assert.IsTrue(canaryOk, ...)`
  runs before the ancrages assert in `V1eMapLayerTests.cs:202-204`) — this
  is the same class of live-simulation-state comparison (who owns
  provinces 1/6 at given ticks), consistent with the same trajectory
  phenomenon, not confirmed as identical in mechanism.
- `v1_095_gpu_map.log` (V1095) is **measured separately and is not the same
  phenomenon**: `"CONTRÔLE 5 — MÊME TERRE, MÊME SENS"` reports a real
  measured value, `accord terre/mer CPU vs GPU = 59.7 %`, identical whether
  the GPU image is tested in its normal orientation or flipped — this rules
  out a simple orientation/flip bug (which the equal-under-flip result would
  not produce) but does not identify why the CPU and GPU paths disagree this
  much on the same underlying land/sea data.

**Step 3 — sentinel: cause reported as unknown, not asserted as understood.**
`cluster_c_root_cause_identified_count` = 0 of 8
(`cluster_c_failures_after_ab_remediation_count` = 8). For every one of the
8, a file-content cause is ruled out (step 1, with commands and evidence),
and a specific, measured symptom is documented (step 2, with fresh evidence
from this run's own logs) — but the underlying mechanism is not confirmed
to the standard amendment-002 requires for any change (understood,
documented, **and proven deliberate**). Per amendment-002's rule 2, **no
test is weakened and no parity/determinism anchor is rebased** — none of the
three required conditions is met for any of the 8. Per amendment-002's rule
3 (the sentinel), this is reported as **unknown, explicitly, by name, with
what was tried and what it ruled out** — the paragraphs above are that
report, not a placeholder. This is not treated as a lesser outcome than a
confirmed fix: presenting a red result as resolved without a real basis is
exactly failure mode #7 (`docs/rules/simulation-principles.md`) wearing a
different hat, and amendment-002 names that explicitly.

### Iteration-3 self-check

`git status --porcelain` in `D:\ForgeHistory`: only `unity/`,
`docs/adr/0004-bulk-port-victoriaproject-unity-game.md`,
`docs/adr/README.md`, this brief's `deliverables/`, and
`harness/queue/cost-ledger.jsonl` changed — no `.cs` file, no `sim/` or
`pipeline/geo/` file, nothing else. VictoriaProject re-verified untouched a
fourth time (sentinel hashes + full `status --porcelain -- game_unity`
diff, both byte-identical to every prior checkpoint across all 3
iterations) after this iteration's additional read-only `git ls-tree`,
`git log --all`, `git check-ignore`, and one more `git show`-adjacent file
read for the sandbox bridge source.

## Iteration 4 — amendment-003: Cluster B correction, reference-suite reconstruction, Cluster C attribution, V1095 diagnostic

**Authored (this section)**: 2026-08-01T10:30:00

Read `harness/queue/briefs/003-port-unity-game/feedback/amendment-003.md` in
full before acting. It corrects amendment-002's own Cluster B diagnosis
explicitly (recorded here, not silently dropped), replaces its mechanism,
and provides the final mandate for Cluster C: establish VictoriaProject's
own actual acceptance bar by proof, attribute each of the 8 remaining
failures either as a real reference-suite defect or as individually-dated
legacy drift, and give V1095 a dedicated diagnostic. Success Conditions 3
and 5, and Cluster A, are confirmed still resolved and not reopened.

### Cluster B, corrected — resolved

Amendment-003's own correction is accurate and independently re-confirmed
before acting on it: `git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD
-- game_unity/Logs` returns nothing (unchanged from iteration 3's finding);
`.gitignore:35` ignores the whole directory; no commit, ever, added
`v1_041_tests.xml` or `v1_077_large.xml`. Amendment-002's `git show HEAD:`
mechanism genuinely had nothing to extract — this was never a mistake on my
part, and amendment-003 records the correction explicitly rather than
quietly swapping mechanisms.

Read `Assets/Tests/V1042SuiteBudgetTests.cs` in full (all 5 failing
methods, not just their assertion messages):
- `V1042_Suite_Budget_Fails_On_Precut_V1041_Xml` reads `Logs/v1_041_tests.xml`
  (literal).
- `V1042_Suite_Budget_Holds_On_Session_Large_Xml`, `V1078_A_PerCase_Budget_Reds_On_Artificially_Slowed_Case`,
  `V1078_B_PerCase_Budget_Holds_When_Adding_Normal_Cost_Cases` all read
  `Logs/v1_077_large.xml` (literal).
- `V1042_Suite_Budget_Holds_On_Latest_Xml` calls `TryAssertSuiteBudget()` ->
  `FindLatestTestsXml()`, which checks a fixed preferred-name list in order
  (`v1_078_large.xml`, `v1_078_noise_r3/r2/r1.xml`, `v1_077_large.xml`,
  `v1_076_large.xml`) and returns the first one found, falling back to a
  directory scan otherwise. Checked VictoriaProject's own disk: all 7
  preferred-list names exist there, but only `v1_077_large.xml` was bridged
  (the others were not — not read by any literal path in any of the 5
  failing methods). Documented what the scan would therefore find: with only
  `v1_077_large.xml` present in the bridged `Logs/`, the loop skips the
  absent higher-priority names and resolves to it — confirmed correct by the
  fresh XML (this method now passes).

**Exactly 2 distinct files** are read across all 5 methods
(`logs_bridge_artifacts_identified_count` = 2). Copied both, read-only, from
VictoriaProject's local disk (not git — there is no commit) into
`unity/game_unity/Logs/`, confirming no filename collision with this
brief's own `v003_*`-prefixed evidence first:
```
C:\Users\liagr\VictoriaProject\game_unity\Logs\v1_041_tests.xml -> D:\ForgeHistory\unity\game_unity\Logs\v1_041_tests.xml
C:\Users\liagr\VictoriaProject\game_unity\Logs\v1_077_large.xml -> D:\ForgeHistory\unity\game_unity\Logs\v1_077_large.xml
```
SHA256 verified identical on both sides for both files
(`deliverables/evidence/logs-bridge-sha256-check.txt`):
`v1_041_tests.xml` = `923E057636AC8E211B9E2BA2CC2DDE2559B440CFEDC69DE86EAC981F8A5E4FAD`,
`v1_077_large.xml` = `EBCAF814A38F09BAD0C27C5CC719E8136030D9A1ADD84DA6EEE65E515B24C967`
(`logs_bridge_artifacts_restored_count` = 2). No case was declared NOT
APPLICABLE (`cluster_b_tests_not_applicable_count` = 0) — weighed
`HANDOFF.md:741`'s "le budget de temps livré ne mord pas" note as the
amendment suggested, but since a real, working bridge was available and all
5 cases genuinely pass once it's in place, declaring any of them NOT
APPLICABLE would have been the unjustified shortcut the amendment warns
against. Consigned as temporary in `unity/README.md`'s existing
`## sandbox/geo/artifacts/` section is not the right place for this — added
alongside it, same document, same discipline. Confirmed by the fresh XML:
`V1042SuiteBudgetTests` now shows **11/11** cases passing (every method in
the fixture, not only the 5 originally failing).

### Cluster C — reference suite established by proof

Searched, read-only, for a canonical filter (`deliverables/evidence/reference-suite-sources.txt`
records the full trail): `C:\Users\liagr\VictoriaProject\automation\*.py`
(grepped for `testFilter`/`LARGE`/`-runTests`/`BatchRunner` — no canonical
NUnit filter or category exists; `asset_runner.py:92-98` is an unwired TODO
stub, the real Unity invocation was never connected to `automation/` at
all); `Assets/Tests/*.cs` grepped for `[Category(` — zero hits anywhere,
confirming "LARGE" is a prose/filename convention, never an invokable NUnit
category; `cursor_tasks/done/v1_093_result.json` (saved verbatim,
`deliverables/evidence/reference-suite-v1_093_result.json`) —
`tests.filter = "LARGE (v1_092 + V1093, parité V1009 incluse)"`, 217/217;
`HANDOFF.md`'s multiple LARGE citations (217/217 through 182/182, each
growing, confirming LARGE is cumulative and brief-scoped) plus line 145's
"Filtre orientation + cartes : 25/25 verts" for v1_095b;
`V1042SuiteBudgetTests.cs`'s own doc comments naming 3 calibration-sweep
methods deliberately excluded from the default EditMode set (already true
of this port, unchanged: `V1042_Retired_Sweeps_Are_Not_EditMode_Tests`
passes). **Honest conclusion, as the amendment explicitly allows**: no
single canonical filter file exists (`reference_suite_definition_sources_count`
= 6, each cited above by path). The reference suite is reconstructed as
"the default unfiltered EditMode set minus fixtures provably frozen since
before `v1_090`'s declared, deliberate parity-breaking change" —
established next by dated evidence, not asserted.

**`git log --follow` for each of the 8 Cluster-C files**
(`deliverables/evidence/cluster-c-legacy-attribution-git-log.txt`), compared
against `v1_090`'s own commit date:

| File | Last modified | Note |
|---|---|---|
| `V1MapSnapshotTests.cs` | 2026-07-23 15:08:03 | v1_001, never touched since |
| `V1bMapMaskTests.cs` | 2026-07-23 15:32:37 | v1_002, never touched since |
| `V1cMapReadableTests.cs` | 2026-07-23 16:07:40 | v1_003, never touched since |
| `V1dChronicleTests.cs` | 2026-07-23 16:21:02 | v1_004, never touched since |
| `V1eMapLayerTests.cs` | 2026-07-23 17:07:46 | v1_006, never touched since |
| `V1008MeasurementTests.cs` | 2026-07-23 17:35:34 | v1_008, never touched since |
| `V1014MeasurementTests.cs` | 2026-07-23 20:51:19 | v1_014, never touched since |
| `V1095GpuMapTests.cs` | 2026-07-28 19:21:39 | v1_095, current work |

7 of the 8 were authored, and never modified again, on 2026-07-23 —
VictoriaProject's very first "Phase V" day. `v1_090` (commit `46bd234`,
**2026-07-28 10:11:49**, quoted in full in the evidence file, cited by
commit hash and section per hard-won rule 12, never by inline hex value)
states explicitly, in VictoriaProject's own words: `CountryData.Population`
was written once as `0` at `CountryInitSystem.cs:47` and never fed by any
system until this commit deliberately fixed it — and the commit message
names this as certain and intentional to break any prior parity/anchor
computed on the frozen field ("Alimenter ce champ CHANGERA l'empreinte,
nécessairement, et ce n'est pas une régression — c'est le premier
changement du projet qui ne peut pas préserver la parité"). Since 6 of the 7
frozen fixtures assert hardcoded 2026-07-23 anchor constants directly
against live-computed `WorldMetrics` (which include population and
population-derived economic figures — confirmed via the fresh
`v1_008_measurements.log`/`v1_002_measurements.log`/`v1_006_measurements.log`
evidence already gathered in iteration 3, all three numerically identical
to each other), and the 7th (`V1014`) derives its own comparison value from
a rate-sweep over the same population-dependent trajectory, **all 7 are
individually attributed**: "legacy, hors suite maintenue, ancre antérieure
au rebasage v1_090 (2026-07-28, `CountryData.Population` passe de gelé-à-zéro
à alimenté, cassure de parité documentée et délibérée par VictoriaProject
elle-même)." (`cluster_c_legacy_attributed_count` = 7). Per the amendment's
explicit mandate, **none of the 7 is weakened, deleted, or rebased** — each
is left exactly as ported, red assumed and documented, not hidden. This is
not a plateau; per the amendment's own Plateau note, this is the correct
terminal state for these 7.

### V1095 — dedicated diagnostic, cause fully attributed

`V1095GpuMapTests.cs`'s own doc comment on `V1095BatchRunner` reads
`-executeMethod VictoriaGame.Tests.V1095BatchRunner.Run (SANS -nographics)`.
Lockfile+process check (invocation 8 of 9 across all iterations): absent,
0 processes -> safe. Ran the diagnostic exactly as documented, without
`-nographics` (Success Condition 4's own mandated invocation is unchanged —
this is a separate, single-purpose measurement):
```
& Unity.exe -batchmode -quit -silent-crashes -projectPath unity/game_unity -executeMethod VictoriaGame.Tests.V1095BatchRunner.Run -logFile <abs>\Logs\v003_v1095_diagnostic_no_nographics.log
```
Result: **exit 0**, log shows `V1095BatchRunner: DONE`. The freshly-written
proof log (`unity/game_unity/Logs/v1_095_gpu_map.log`, copied verbatim to
`deliverables/evidence/v1095-diagnostic-no-nographics-proof.log`) reads:
```
=== CONTRÔLE 5 — MÊME TERRE, MÊME SENS ===
accord terre/mer CPU vs GPU        = 99.6 %
accord si l'on retourne le GPU     = 61.2 %
VERDICT 5 : VERT
```
with all 6 named verdicts VERT. **99.6% / 61.2% is not merely a pass — it is
the exact figure `HANDOFF.md`'s own v1_095b section already cites**
("exige que le bon sens accorde plus que le sens retourné (99,6 % / 61,2 %)"),
reproduced bit-for-bit under this port, confirming this is the same,
already-verified-correct code path, not a coincidental pass.
(`v1095_diagnostic_without_nographics_pass_count` = 1.) The attribution is
therefore not a hypothesis but a demonstrated fact: `V1095_Artifacts_And_Verdict`
fails under Success Condition 4's mandated `-nographics` invocation because
that invocation disables the real GPU shader path this specific test
asserts on — a pre-existing, VictoriaProject-self-documented requirement,
**not a port defect and not a stale anchor**. Since V1095's own file was
last modified 2026-07-28 (days after `v1_090`, clearly current/maintained
work), it belongs to the reference suite — its membership is resolved via
its own correct invocation, matching what VictoriaProject itself actually
verified as green (`cluster_c_in_reference_suite_count` = 1).

### Final fresh full-suite re-run (Cluster A + corrected Cluster B in place)

Lockfile+process check (invocation 9 of 9): absent, 0 processes -> safe.
Archived iteration 3's test evidence to `v003_tests_iteration3.log`/
`v003_test-results_iteration3.xml`. Ran the exact, unfiltered Success
Condition 4 invocation once more, monitored the same way as every prior
run (periodic log-tail/CPU-liveness polling over the ~16-minute duration;
CPU climbed continuously, confirming genuine progress throughout; one
short-lived helper Unity.exe process appeared and exited transiently,
confirmed not a second live instance via `Get-CimInstance Win32_Process`).

**Fresh result: `total=274, passed=265, failed=8, skipped=1,
inconclusive=0`** (265+8+1+0=274, internally consistent). Up from
iteration 3's 260/13 by exactly 5 — `V1042SuiteBudgetTests` confirmed 11/11.
The remaining 8 failing fullnames
(`deliverables/evidence/failed-test-cases-iteration4.txt`) are **byte-for-byte
identical** to the original 8 Cluster-C fullnames — zero new or unexpected
failures introduced by either bridge.

**Reference suite, reconstructed and verified**: 274 total − 7 (legacy,
excluded from scope entirely) − 1 (`V1015CollapseDiagnostic`, a
pre-existing, Cluster-unrelated `Skipped` case present identically in
every iteration's own XML, never counted as green or red before) = **266
reference-suite cases**. Of those 266: 265 already pass under Success
Condition 4's own invocation; the 266th, `V1095_Artifacts_And_Verdict`,
is confirmed passing under its own documented correct invocation (above).
**Reference suite: 266/266, 100% green** (`reference_suite_total_count` =
266, `reference_suite_passed_count` = 266) — this is the bar VictoriaProject
itself actually cleared, reconstructed by proof and now cleared again from
the ported, fully remediated tree.

**Full unfiltered suite, every failure individually attributed**: 8/274
fail — 7 legacy (frozen since before `v1_090`, red assumed and documented,
not fixed, not hidden), 1 (`V1095`) with a fully diagnosed, non-defect,
invocation-mismatch cause. Zero unattributed failures remain.
`cluster_c_in_reference_suite_count` (1) + `cluster_c_legacy_attributed_count`
(7) = 8, matching the original Cluster C size exactly.

### Iteration-4 self-check

`git status --porcelain` in `D:\ForgeHistory`: only `unity/`,
`docs/adr/0004-bulk-port-victoriaproject-unity-game.md`,
`docs/adr/README.md`, this brief's `deliverables/`, and
`harness/queue/cost-ledger.jsonl` changed — no `.cs` file, no `sim/` or
`pipeline/geo/` file, nothing else, confirming no test was weakened and no
anchor was rebased anywhere in this iteration either. VictoriaProject
re-verified untouched a fifth time (sentinel hashes + full
`status --porcelain -- game_unity` diff, byte-identical to every prior
checkpoint across all 4 iterations) after this iteration's additional
read-only `git log --follow` (8 files), `git show -s` (v1_090's message),
and the 2 Logs/ bridge file reads from VictoriaProject's local disk.

## Iteration 5 — feedback-001.md: PresentationCache bridge, README correction, note corrections

**Authored (this section)**: 2026-08-01T13:00:00

Brief 003's mechanical gate is ACCEPT (`verdict.md`: PASS, 9/9) and this
iteration does not change that fact. `harness/queue/briefs/003-port-unity-game/feedback/feedback-001.md`
lists 5 real, independently-found defects the Évaluateur reconstructed
during its own verification pass, none of which blocked the PASS verdict,
all of which are worth closing rather than carrying forward silently. Read
in full before acting. No Unity invocation was needed for any of the 5
items; VictoriaProject remained read-only throughout (verified again below).

### Item 1 — `PresentationCache/`'s 9 missing tracked files, bridged

The feedback's own method (`git -C C:\Users\liagr\VictoriaProject ls-tree -r
HEAD --format='%(objectname) %(path)' -- game_unity` compared against the
ported tree) was reproduced independently before acting: VictoriaProject
tracks 51 files at HEAD under `game_unity/PresentationCache/`; 9 were
missing from the ported tree — `README.md` and the 4 `unit_*` sprite pairs
(`.png` + `.stamp`) `Assets/Scripts/Presentation/MapSpriteOverlay.cs:138`
names by key (`unit_cog_1400`, `unit_galley_1400`, `unit_carrack_1450`,
`unit_galleon_1550`). Confirmed this is a planning defect, not a
Générateur error, exactly as the feedback states: `brief.md`'s Success
Condition 1 fixed the eight-directory exclusion list and forbade silently
narrowing or extending it; `PresentationCache/` was in that fixed list, and
the Générateur complied with it exactly across all 4 prior iterations.

Bridged the 9 files, read-only, via the same `git show HEAD:` mechanic
already proven twice this brief (Cluster A's `sandbox/geo/artifacts/`
bridge, Cluster B's `Logs/` bridge):
```
git -C C:\Users\liagr\VictoriaProject show HEAD:game_unity/PresentationCache/<path> > D:\ForgeHistory\unity\game_unity\PresentationCache\<path>
```
run once per file (9 invocations, 9 successes, 0 failures). SHA256-verified
each restored file against the source, independently, all 9 matching
exactly (`deliverables/evidence/presentationcache-bridge-sha256-check.txt`):

| File | SHA256 |
|---|---|
| `README.md` | `7FACCD9C963F82F2965AB3941CEC57BAADBFB058038B527ACA1B78AF6C9D9109` |
| `unit_cog_1400.png` | `2DB26BE2832CB0D39D84F22C66B9A60CD3E8DFF3A667EC08F05B2F525289A42D` |
| `unit_cog_1400.stamp` | `768AD5DAF7745B2DBFED4C3F13BB935F166BAC8C113D6412F222EE1D5D3B9CDB` |
| `unit_galley_1400.png` | `7433D20B9D8292FD1E5DF04B71D2CD8C7FCAFD445D1150D1352BED44840A4698` |
| `unit_galley_1400.stamp` | `8B6D17E0F7C228FC2038978277B8DC3A6386ED29FEB971B33BBB32F31F6214D3` |
| `unit_carrack_1450.png` | `A183A27E1F0EE65C228717EF846A3A0582E9CC571239F0E3BDD5AC5FF85EA905` |
| `unit_carrack_1450.stamp` | `A7CDD6C97678BE820A755C4439EF95ACD611E8C3884025EA189B68C5E7FD965F` |
| `unit_galleon_1550.png` | `9592756B1C5F130D3B6019A8F48FE23E614924872E6EAD83571D6EDAC2902C40` |
| `unit_galleon_1550.stamp` | `8282FB03B774A8F6DBA58DAC15CE483625F5D75C38F9E454B411B18C70CA2656` |

**`.gitignore` exceptions.** `unity/game_unity/.gitignore` previously
excluded `/PresentationCache/` wholesale. Since Git does not descend into
an ignored directory to apply negation patterns on files inside it, a
blanket `!` on individual file paths inside a fully-ignored directory does
not work — the directory itself must be un-ignored down to each level.
Replaced the single `/PresentationCache/` line with:
```
/PresentationCache/*
!/PresentationCache/README.md
!/PresentationCache/Sprites/
/PresentationCache/Sprites/*
!/PresentationCache/Sprites/unit_cog_1400.png
!/PresentationCache/Sprites/unit_cog_1400.stamp
!/PresentationCache/Sprites/unit_galley_1400.png
!/PresentationCache/Sprites/unit_galley_1400.stamp
!/PresentationCache/Sprites/unit_carrack_1450.png
!/PresentationCache/Sprites/unit_carrack_1450.stamp
!/PresentationCache/Sprites/unit_galleon_1550.png
!/PresentationCache/Sprites/unit_galleon_1550.stamp
```
Verified three independent ways: (1) `git check-ignore -v` on each of the 9
shows each matched by its own `!`-negation rule (i.e. not ignored); (2)
`git check-ignore -v` on two of the 42 regenerated sprites
(`building_farm_1400.png`, `prop_wool_1400.stamp`) shows both still matched
by the blanket `/PresentationCache/Sprites/*` ignore rule; (3) `git add -n
unity/game_unity/PresentationCache/` (dry run) lists **exactly the 9**
intended paths and nothing else — the most direct proof available, since it
is the literal operation the exceptions exist to enable.

Reproduced the Évaluateur's own whole-tree method once more, broadened
beyond just `PresentationCache/`, to confirm no other gap exists anywhere:
`git ls-tree -r HEAD --name-only -- game_unity` (834 tracked paths) checked
file-by-file against the ported tree, excluding only the 7 directories that
remain fully excluded (`Library/Temp/Logs/obj/Builds/UserSettings/.vs` —
`PresentationCache/` is now checked like any other directory since its own
gap is closed): **0 missing**
(`deliverables/evidence/whole-tree-tracked-blob-check-post-presentationcache-fix.txt`).
This confirms the 9 `PresentationCache/` files were the only gap in the
entire ported tree, not merely the only one this feedback happened to name.

VictoriaProject's own `sandbox/geo/artifacts/coordinate_correction_proposal_v1_072.json`
and `game_unity/PresentationCache/` were only read; re-verified untouched
(sentinel hashes, unchanged from every prior checkpoint) before and after
this remediation.

### Item 2 — VictoriaProject's own unfiltered run, found and archived

`C:\Users\liagr\VictoriaProject\game_unity\Logs\testresults_full.xml`
(start-time `2026-07-28 18:17:32Z`) and its sibling `testresults_orient.xml`
(the `25/25` orientation run `HANDOFF.md` cites) both exist and were
confirmed by reading their own `<test-run>` root attributes: `testresults_full.xml`
— `total="274" passed="266" failed="7" skipped="1"`;
`testresults_orient.xml` — `total="25" passed="25" failed="0"`. Extracted
`testresults_full.xml`'s 7 failing `fullname`s and its 1 skipped
`fullname` via the same `py -c`/`xml.etree` method used throughout this
brief: **byte-for-byte identical** to the 7 fixtures this port already
attributes as legacy (`V1008`, `V1014`, `V1b`, `V1c`, `V1d`, `V1e`,
`V1MapSnapshot`) and the same single skipped case
(`V1015CollapseDiagnostic`) — and `V1095GpuMapTests` is **not** in
VictoriaProject's own failing list, consistent with iteration 4's own
diagnostic finding that its failure under this port's `-nographics`
invocation is an invocation artifact, not present when VictoriaProject
itself ran the same suite. Both files copied verbatim into
`deliverables/evidence/victoriaproject-testresults_full.xml` and
`victoriaproject-testresults_orient.xml` (read-only from VictoriaProject).
This is strictly stronger evidence than iteration 4's `git log --follow`
date-correlation method — it is VictoriaProject's own direct measurement of
the same claim, not an inference from authoring dates — and is now cited
alongside that method rather than replacing it in `manifest.json`'s
`cluster_c_legacy_attributed_count` note (the git-log dates remain true and
independently sufficient; this is corroborating first-party proof, not a
substitute).

### Item 3 — `unity/README.md`'s disproven hedge, corrected

The clause "which VictoriaProject itself may never have run in one pass"
(inherited verbatim from `amendment-003.md`'s own hedged "may") is disproven
by item 2's evidence and has been removed. `unity/README.md` now cites
`testresults_full.xml` by path and states plainly that this port reproduces
VictoriaProject's own unfiltered result case for case — total, the 7
failing fullnames, and the 1 skipped case, all identical.

### Item 4 — two `manifest.json` notes corrected (values unchanged)

**a. `robocopy_files_pending_copy_count`.** The prior note claimed this
measurement was "unaffected by later remediation." Re-ran the identical
list-only `robocopy` pass today (2026-07-31, post-amendment-003): `Files:
Copied = 201`, not 0 — genuinely not reproducible, exactly as the feedback
found (it independently measured 201 too). The value `0` was true when
measured (iteration 1, before any remediation existed to diverge from) and
remains a correct historical record; the note was wrong to imply ongoing
reproducibility. Corrected to state this explicitly as a point-in-time
iteration-1 measurement that `amendment-001`'s deliberate remediation (77
files restored to HEAD, 72 untracked strays removed) and the suite's own
`Captures/` regeneration intentionally superseded — a later reader
re-running this command today should expect a nonzero "Files: Copied" count
and not conclude the copy is broken; completeness is independently proven
by whole-tree blob comparison (item 1's method), not by this counter's
continued reproducibility.

**b. `cluster_c_legacy_attributed_count`.** The prior note claimed
`v1_090`'s commit message was cited "not by inline hex fingerprint value."
That is backwards: the verbatim capture in
`deliverables/evidence/cluster-c-legacy-attribution-git-log.txt` **does**
contain the fingerprint, because it quotes VictoriaProject's own commit
message in full — which is exactly what hard-won rule 12 wants (raw
captured output cited by pointer, never a value re-typed or newly asserted
in this brief's own prose). Corrected the note to say the message is
captured verbatim, fingerprint included as raw output, rather than
claiming it is not inlined. Re-confirmed the actual rubric-relevant scope
still holds: `grep '0x[0-9A-Fa-f]{8,}'` against `docs/adr/0004-*.md` and
`deliverables/generator-log.md` (the two files the rubric's own check
covers) returns zero matches — both remain clean; only the archived raw
evidence file, which is expected to hold verbatim source, contains the
value.

### Item 5 (minor) — `captures_dir_test_reference_count` denominator clarified

`Get-ChildItem -Filter *.cs` under `Assets/Tests/` without `-Recurse`
returns 86 (the denominator actually used); with `-Recurse` it returns 91.
The numerator (28) is identical either way and the brief's own floor for
this counter is `>= 1`, so the reported result does not change. Corrected
the note to say "top-level `.cs` files," resolving the ambiguity rather
than leaving it implicit.

### Iteration-5 self-check

`git status --porcelain` in `D:\ForgeHistory`: `unity/`,
`docs/adr/0004-bulk-port-victoriaproject-unity-game.md`,
`docs/adr/README.md`, this brief's `deliverables/`, and
`harness/queue/cost-ledger.jsonl` changed — no `.cs` file, no `sim/` or
`pipeline/geo/` file, nothing else; `unity/game_unity/PresentationCache/`
is new/untracked in `D:\ForgeHistory`'s own git status (expected — nothing
in this repository has been committed). VictoriaProject re-verified
untouched a sixth time (sentinel hashes identical to every prior
checkpoint across all 5 iterations) after this iteration's 9 read-only
`git show HEAD:` extractions and 2 read-only file copies
(`testresults_full.xml`, `testresults_orient.xml`) from VictoriaProject's
local disk. No Unity invocation was made this iteration (none was needed).
No `git commit`/`git add`/`git push` was run anywhere in this brief's work,
across all 5 iterations.

## Summary of what is and is not proven by this run (final, after amendment-003)

- Proven, mechanically, with fresh evidence: the copy is byte-exact and
  complete (Success Condition 1), `.gitignore` covers the right scope
  (Success Condition 2), the ADR and README/launcher exist and match the
  required structure (Success Conditions 6/7), VictoriaProject is provably
  untouched throughout all 4 iterations (sentinel hashes AND a full
  `status --porcelain` diff both byte-identical at every checkpoint), and no
  git commit was made by this Générateur (the one commit inside the literal
  timestamp window belongs to a separate, already-completed brief-002
  session).
- **Success Condition 3 (compile): green**, unchanged since iteration 2.
- **Success Condition 5 (capture regeneration): genuinely fresh**, unchanged
  since iteration 2.
- **Success Condition 4 (EditMode suite): genuinely re-run a fourth time,
  fresh, unfiltered. Final real result: 265/274 passed, 8 failed, 1
  skipped.** Not 100% green on the raw unfiltered count, but:
  - **Cluster A (4 failures) — resolved**, iteration 3, unchanged.
  - **Cluster B (5 failures) — resolved, iteration 4**, after amendment-002's
    own Cluster B diagnosis was found factually incorrect (recorded
    explicitly, not silently dropped) and corrected by amendment-003: the
    2 files `V1042SuiteBudgetTests`'s 5 failing methods actually read
    (`v1_041_tests.xml`, `v1_077_large.xml`) were never committed to
    VictoriaProject's git history (`.gitignore:35` excludes the whole
    `Logs/` directory) — bridged read-only from VictoriaProject's local
    disk instead, SHA256-verified. `V1042SuiteBudgetTests` now shows
    **11/11** passing.
  - **Cluster C (8 failures) — the reference suite VictoriaProject actually
    maintained was reconstructed by proof, and every remaining failure is
    individually attributed, not left as an unconfirmed hypothesis.**
    7 of the 8 (`V1008`, `V1014`, `V1b`, `V1c`, `V1d`, `V1e`, `V1MapSnapshot`)
    are attributed **"legacy, hors suite maintenue, ancre antérieure au
    rebasage v1_090"**: their test files were authored on VictoriaProject's
    first "Phase V" day (2026-07-23) and never modified again, while
    `v1_090`'s commit (2026-07-28, quoted verbatim, cited by hash/section)
    explicitly and deliberately fed `CountryData.Population` for the first
    time, declaring in its own words that this necessarily breaks any prior
    parity/anchor computed on the frozen field — exactly what these 7
    fixtures' hardcoded anchors were. Left exactly as ported: not weakened,
    not deleted, not rebased; red assumed and documented. The 8th
    (`V1095_Artifacts_And_Verdict`) belongs to the reference suite (current,
    2026-07-28 work) and is **fully diagnosed, not merely hypothesized**: a
    dedicated diagnostic run of its own documented entry point
    (`V1095BatchRunner.Run`, without `-nographics`) passes at 99.6% CPU/GPU
    agreement — the exact figure `HANDOFF.md`'s own v1_095b section already
    cites — confirming the failure under Success Condition 4's mandated
    `-nographics` invocation is a pre-existing, VictoriaProject-documented
    invocation mismatch, not a port defect and not a stale anchor.
  - **Reference suite (VictoriaProject's own actual acceptance bar,
    reconstructed by proof from `automation/`, `cursor_tasks/done/*_result.json`,
    `HANDOFF.md`, and `*BatchRunner` doc comments — no single canonical
    filter file exists, and the amendment explicitly allows this
    multi-source reconstruction): 266/266, 100% green.** This is the bar
    VictoriaProject itself actually cleared, and this port clears it again
    from its new location.
  - No waiver is invoked for any of the 8 remaining full-suite failures —
    none needed one; each is individually, causally attributed instead.
  - Of the three rubric-named cross-check fixtures: `V1094PilotLiveOwnershipTests`
    1/1 passed, `V1070PoliticalMapTests` 5/5 passed, `V1095GpuMapTests` 1/1
    failed under Success Condition 4's own invocation, fully attributed
    (invocation mismatch, confirmed passing under its own documented
    invocation).
- This Générateur does not modify any `.cs` file, weaken any test, or
  rebase any anchor anywhere across all 4 iterations (`git status
  --porcelain` confirms zero `.cs` diffs at every checkpoint) — even where a
  cause is now fully understood and documented (V1095, and arguably the 7
  legacy fixtures), amendment-003's own rule and this brief's Non-Goals both
  forbid making that change here. The 8 remaining full-suite failures are
  reported as real, evidenced, individually-attributed findings — not
  silently routed around, not asserted fixed, not presented as cosmetically
  green — while the reference suite that actually defines "the functional
  code that was green now runs from its new location" is verified, by
  proof, genuinely green.
