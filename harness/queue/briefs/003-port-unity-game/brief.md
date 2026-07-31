# Brief 003: Port VictoriaProject's working Unity game into `unity/game_unity/`

**Authored**: 2026-07-31T15:00:00
**Author**: forge-planificateur

## Owner Decision This Brief Implements

Recorded today, 2026-07-31 — this is the arbitration named at
`FORGE-HISTORY-BRIEF.md` §9 ("Unity ou pas"), and it overrides that
document's own §3 default recommendation for the 60 C# simulation systems
("relire, ne pas copier"):

> « Récupérer le code existant fonctionnel de VictoriaProject. Mais les
> harnais et contrôles sont ceux de ForgeHistory. Objectif : un jeu beau et
> fonctionnel à la fin de la session. »

Concretely: `C:\Users\liagr\VictoriaProject\game_unity` is ported **in
bulk** into `D:\ForgeHistory\unity\game_unity\`. VictoriaProject's own
automation layer (`cursor_tasks/`, `automation/run_queue.py`,
`RESULT_TEMPLATE.json`, `runtime_bridge/` locks) is explicitly **not**
ported — ForgeHistory's three-role harness (this brief, `verdict_audit.py`)
replaces it. This is a deliberate, recorded deviation from §3's inventory
verdict for the 60 systems ("relire, ne pas copier"); it is not a silent
substitution — see ADR-0004 (Success Condition 6), which must name this
deviation explicitly, not merely enact it.

## World-Terms Requirement

Stated causally, not as a code-quality preference:

`unity/` has held nothing but a stub since F0: no renderer exists anywhere
in this repository to turn any state — a geo-pipeline artifact, an
ADR-decided cell, a future `sim/` tick — into something a person can look at
and compare against what changed. F1's own definition of done is stated in
world-terms, not code-terms: "je change une possession dans les données et
la carte le montre, capture à l'appui" (`FORGE-HISTORY-BRIEF.md` §8). A
change with no observable consequence is not delivered — this is failure
mode #3 (the terminal variable) applied to the render client itself: every
future brief that produces a world artifact (coastline, cells, ownership)
accumulates JSON nobody can look at unless something reads it and shows a
person the difference.

VictoriaProject already built, measured, and repeatedly re-proved that
reading path: a political map that resolves "where" through one named,
test-guarded translation point between the two coexisting ID systems
(`PilotMapProvider.SimulationProvinceIdOfView`, closing failure mode #1's
gap the day it was found — v1_094), a GPU-backed render path measured at
0.305 ms/frame after the CPU path cost 98 ms/frame (v1_095), and orientation
controls that were rewritten to derive their reference from the measurement
itself instead of naming two hardcoded countries, after the hardcoded
version silently went blind (v1_095b — failure mode #6). None of that
reading path exists in ForgeHistory today. This brief's job is not to write
it: it is to carry the already-working, already-measured version across
whole, so that a person watching this repository can see a world change
instead of only being told, in prose, that one occurred — and to do so
without violating principle 4 (presentation reads simulation state, it does
not re-decide it): the ported renderer must keep reading `EntityManager`
state through the same single translation point it already used, not gain a
second one.

## Success Conditions

1. **Copy scope.** `C:\Users\liagr\VictoriaProject\game_unity\` is copied to
   `D:\ForgeHistory\unity\game_unity\` using `robocopy` (Windows long-path
   safe), **excluding** exactly these eight regenerable directories and
   nothing else:
   `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, `PresentationCache/`,
   `UserSettings/`, `.vs/`. Every other directory — **including
   `Captures/`** — is copied. `Captures/` is named explicitly here because
   it is not obviously regenerable cache: `V1070PoliticalMapTests.cs`'s
   `V1070_Proof_CapturesAndLog` reads `Captures/v1_068/` and writes into
   `Captures/v1_070/` as part of its own red-case proof (17/17 mordant,
   named in `HANDOFF.md`'s v1_070 section) — excluding it would silently
   break a test that already exists and is being carried over unchanged. The
   Générateur must grep the ported `Assets/Tests/` tree for the literal
   substring `Captures` **before** finalizing the exclusion list (see
   Required Counters) — the eight-directory list above is fixed by this
   brief and may not be extended without that grep result being reported.
   `C:\Users\liagr\VictoriaProject\` itself is **read-only** for the
   duration of this brief: nothing under it may be created, modified, or
   deleted (Required Counters: `victoriaproject_source_unmodified_count`).

2. **`unity/game_unity/.gitignore`** is created, covering at minimum the
   eight excluded directories from Success Condition 1 (they must never
   become trackable even after local regeneration during compile/test).

3. **Compilation proof.** Unity 6000.0.43f1 batchmode compiles the ported
   project from its new location, exit code 0, zero `error CS####` lines in
   the compile log:
   ```
   & "C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" `
     -batchmode -quit -nographics -silent-crashes `
     -projectPath unity/game_unity `
     -logFile <ABSOLUTE PATH>\unity\game_unity\Logs\v003_compile.log
   ```
   `-logFile` **must** be an absolute path (a relative one has produced
   empty/truncated logs on this machine before). The first run regenerates
   `Library/` from scratch and has taken 10-40 minutes on comparable Unity
   projects — the Générateur must launch this **in the background** (a
   non-blocking process start, not a synchronous call bound by any tool's
   own default timeout) and poll the log file's growth/tail every ~30-60 s
   until a terminal line appears, documenting the exact polling method used
   in `generator-log.md`. Blocking on a ~10-minute timeout and reporting
   "did not finish" is not an attempt at this condition; it is an abdication
   under hard-won rule 9 unless the log itself shows a real Unity error.

4. **Test proof.** The EditMode suite runs green from the new location:
   ```
   & "C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" `
     -batchmode -runTests -nographics -silent-crashes `
     -projectPath unity/game_unity `
     -testPlatform EditMode `
     -testResults <ABSOLUTE PATH>\unity\game_unity\Logs\v003_test-results.xml `
     -logFile <ABSOLUTE PATH>\unity\game_unity\Logs\v003_tests.log
   ```
   Do **not** add `-quit` to this invocation — VictoriaProject's own
   automation notes (`cursor_tasks/done/sim_001_VictoriaGame.Tests.md`) that
   `-runTests` already exits on completion and combining the two flags has
   caused truncated runs. Pass/fail/total counts are **derived** from the
   written NUnit XML's `<test-run>` root attributes (`total`, `passed`,
   `failed`) — never copied from a number cited in `HANDOFF.md` prose (that
   217/217 figure describes VictoriaProject's own last run, not this port;
   this port's own total may legitimately differ and must be read fresh
   from this run's own XML). `failed` must equal 0 and `passed` must equal
   `total`.

5. **Visual proof — a real before/after pair, from the ported code
   unchanged.** `V1094PilotLiveOwnershipTests.cs` already contains a
   `[TestFixture]`-external batch entry point,
   `VictoriaGame.Tests.V1094BatchRunner.Run`, whose
   `RunAndWriteArtifacts()` call writes `Captures/v1_094/01_avant_conquete.png`
   (before a conquest is applied) and
   `Captures/v1_094/03_apres_conquete_VERT_ecs.png` (after the same
   conquest, rendered through the live ECS path) into the project's own
   `Captures/` directory, using paths derived from `Application.dataPath`
   (so they resolve correctly at the new location with no code change).
   Run:
   ```
   & "C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" `
     -batchmode -quit -nographics -silent-crashes `
     -projectPath unity/game_unity `
     -executeMethod VictoriaGame.Tests.V1094BatchRunner.Run `
     -logFile <ABSOLUTE PATH>\unity\game_unity\Logs\v003_capture.log
   ```
   This produces the `must_differ_from` pair this brief requires (there are
   no other before/after artifacts in scope): `01_avant_conquete.png` and
   `03_apres_conquete_VERT_ecs.png`, declared in `manifest.json` with
   distinct SHA256 (Required Counters:
   `capture_pair_sha256_distinct_count`). The Évaluateur is expected to open
   both PNGs and look, per hard-won rule 11 ("regarder les captures
   soi-même") — a passing SHA256-diff check is necessary, not sufficient.
   This condition does **not** authorize writing any new capture/export
   code; it only authorizes invoking what already exists, unchanged.

6. **ADR-0004** at `docs/adr/0004-<slug>.md` (Générateur chooses `<slug>`),
   matching `docs/adr/template.md`'s structure (`## Context`, `## Decision`,
   `## Alternatives Considered` with >= 2 `### Alternative N:` entries,
   `## Consequences` with `### Positive` / `### Negative` / `### Risks`),
   frontmatter `**Date**`, `**Status**: accepted`, `**Deciders**: project
   owner` (matching ADR-0001/0002/0003's convention). It must, at minimum:
   - Cite and quote the owner decision from this brief's "Owner Decision"
     section, and name the §3/§9 deviation it enacts (bulk port instead of
     "relire, ne pas copier") as one of the `### Alternative` entries
     (rejected: reread-and-rewrite; chosen: bulk port; a real, non-filler
     Why-not for each, specific to this decision — e.g., time budget for
     "un jeu beau et fonctionnel à la fin de la session" versus weeks of
     rewrite, per §9).
   - Name failure mode #1 by number and explain, causally, the debt this
     port **imports rather than resolves**: the ported ECS still carries
     `ProvinceId` (1..~50) alongside the pilot map's `cell_id` (>= 1164);
     the only reason this does not currently desync is the single named
     translation point `PilotMapProvider.SimulationProvinceIdOfView`
     (v1_094) — cited by NAME, per hard-won rule 12, never by re-deriving
     or re-stating its internal logic in the ADR. State explicitly that
     ADR-0003 (cell as the eventual single spatial primary key) remains the
     F1 target this ported code does not yet implement, and that resolving
     the coexistence is out of this brief's scope.
   - State plainly that VictoriaProject's own automation
     (`cursor_tasks/`, `automation/run_queue.py`, `RESULT_TEMPLATE.json`,
     `runtime_bridge/`) is not ported and is replaced by ForgeHistory's
     three-role harness.
   - Any citation of a VictoriaProject determinism/parity fingerprint (if
     one is quoted at all) must name the constant (e.g.
     `ParityAnchors.Expected`), never inline its hex value — hard-won rule
     12; the same rule applies in `generator-log.md`.
   `docs/adr/README.md`'s index gains a row for ADR-0004 (path, title,
   `accepted`, date) — mirroring how brief 001 required this for ADR-0003.

7. **Minimal launcher, not the old automation.** `unity/README.md` is
   updated to state that `game_unity/` has landed, and to document exactly
   one supported way to open it: launching the Unity 6000.0.43f1 editor
   with `-projectPath unity/game_unity -openfile unity/game_unity/Assets/Scenes/Main.unity`.
   A small script (e.g. `unity/open-game.ps1`) wrapping that exact
   invocation is acceptable and encouraged, but `automation/demo.py`,
   `automation/run_queue.py`, and the rest of VictoriaProject's queue/lock
   machinery are **not** ported — see Non-Goals.

8. **Deliverables contract.** `deliverables/manifest.json` (files, counters
   — each with a real, derived, nonzero `sample_size` — and waivers if
   invoked) and `deliverables/generator-log.md` with
   `**Author**: forge-generateur`, narrating what was built and how each
   Required Counter was actually measured. The Générateur never writes
   `verdict.md` and never states or implies the work is "acceptable" or
   "recevable" anywhere in `generator-log.md` — that word is reserved for
   the Évaluateur.

9. **Single-instance discipline.** Before **each** of the three Unity
   invocations in Success Conditions 3/4/5, the Générateur checks that no
   live Unity process holds `unity/game_unity/Temp/UnityLockfile` — a
   lockfile's mere presence does not prove liveness (VictoriaProject's own
   documented finding: a killed batchmode run leaves the file behind), so
   the check must combine `Test-Path` on the lockfile with a process check
   (e.g. `Get-Process Unity -ErrorAction SilentlyContinue`) before
   concluding "busy" and waiting. Each check is logged in
   `generator-log.md` (Required Counters:
   `unity_lockfile_checked_before_invocation_count`).

## Non-Goals

- Must **not** port `automation/` (any file), `cursor_tasks/`,
  `RESULT_TEMPLATE.json`, or `runtime_bridge/` — ForgeHistory's harness
  replaces this layer entirely; ADR-0004 records that, it does not build it.
- Must **not** modify any file under `C:\Users\liagr\VictoriaProject\` —
  read-only for the whole brief (Required Counters:
  `victoriaproject_source_unmodified_count`).
- Must **not** modify any simulation or gameplay C# logic in the ported
  tree. The **only** permitted source change is a minimal, marked,
  path-resolution adjustment if (and only if) Success Condition 3's compile
  fails on a VictoriaProject-hardcoded absolute path — see Acceptable
  Waivers row 2. Any such line must end with the literal comment
  `// FORGEHISTORY-PATH-ADJUSTMENT` (mirroring brief 002's Python
  convention) and be listed individually in `generator-log.md`. No other
  diff line in any `.cs` file is authorized.
- Must **not** attempt to make the PlayMode HUD (`InGameHud`, the real
  1920x1080 framebuffer) capturable — this is a documented structural limit
  (`HANDOFF.md`: "la porte de preuve est EditMode, le HUD n'est capturable
  qu'en PlayMode"), not a defect this brief can or should fix. Success
  Condition 5 uses `MapSnapshotExporter`'s EditMode-reachable path
  specifically because it is the one proven capturable.
- Must **not** resolve the `ProvinceId`/`cell_id` coexistence (failure mode
  #1) — ADR-0004 documents the imported debt; fixing it is separate, future
  work gated on `sim/` and `pipeline/geo/` reaching the point where they can
  actually replace the pilot map's data source.
- Must **not** create, modify, or delete any file under `sim/` or
  `pipeline/geo/`.
- Must **not** run `git commit` (or any equivalent staging/commit action)
  at any point in this brief — a known recurrence risk. All work is left as
  uncommitted working-tree changes for the owner/session to review and
  commit. (Required Counters: `generator_git_commits_count` must be 0.)
- Must **not** report a compile or test result derived from anything other
  than an actual log/XML file produced by an actual invocation run during
  this brief, in this repository, today — presence of the old `217/217`
  (or any other VictoriaProject-session number) in prose is not evidence
  for this port's own counters.
- Must **not** silently extend the eight-directory exclusion list in
  Success Condition 1 past what is named there, and must not exclude
  `Captures/` without first producing the grep evidence Required Counters
  asks for.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| robocopy_files_pending_copy_count | a second, list-only (`/L`) `robocopy` pass comparing `C:\Users\liagr\VictoriaProject\game_unity` against `unity/game_unity` with the same `/XD` exclusions, run *after* the real copy — its summary "Files: Copied" count | must equal 0 (destination already matches source exactly for every non-excluded file) |
| robocopy_files_total_count | same list-only pass's summary "Files: Total" count | must be > 0 |
| victoriaproject_source_unmodified_count | SHA256 (`Get-FileHash`) of >= 3 named sentinel files under `C:\Users\liagr\VictoriaProject\game_unity\` (`ProjectVersion.txt`, `Assets\Scenes\Main.unity`, `Assets\Scripts\Presentation\MapSnapshotExporter.cs`), compared before this brief's first write and again at hand-off | total sentinel files sampled (must equal numerator — all unchanged; must be >= 3) |
| captures_dir_test_reference_count | grep (literal substring `Captures`) across every `.cs` file under `unity/game_unity/Assets/Tests/` after the port | count of matching `.cs` files (must be >= 1 — the evidence that justified not excluding `Captures/`) |
| compile_error_cs_count | full text of `unity/game_unity/Logs/v003_compile.log` | count of lines matching `error CS\d+` (must equal 0); log line count must be > 0 (non-empty log) |
| test_total_count | `<test-run>` root element's `total` attribute in `unity/game_unity/Logs/v003_test-results.xml` | must be > 0 |
| test_passed_count | same XML's `passed` attribute | must equal `test_total_count` |
| test_failed_count | same XML's `failed` attribute | must equal 0 |
| capture_pair_sha256_distinct_count | count of distinct SHA256 values across `Captures/v1_094/01_avant_conquete.png` and `Captures/v1_094/03_apres_conquete_VERT_ecs.png` | must equal 2 (both files present, hashes differ) |
| adr_alternatives_considered_count | `docs/adr/0004-<slug>.md`'s `### Alternative` headings under `## Alternatives Considered` | must be >= 2 |
| adr_index_rows_count | `docs/adr/README.md` table rows, count of `NNNN-*.md` files actually present under `docs/adr/` | must equal 4 (0001-0004) |
| unity_lockfile_checked_before_invocation_count | `generator-log.md`'s documented lockfile-absence checks, one immediately before each Unity invocation | total number of Unity invocations actually run in this brief (must be equal — every invocation preceded by a documented, combined lockfile+process check) |
| generator_git_commits_count | `git log --oneline` commits with a timestamp between `brief.md`'s Authored value (2026-07-31T15:00:00) and `deliverables/manifest.json`'s file mtime | must equal 0 |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "the Unity 6000.0.43f1 editor cannot be located at the declared path" | `Test-Path 'C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe'` | command returns `False` — proving the path is genuinely absent, not merely asserted (already verified present today, 2026-07-31; this row exists only in case the machine state changes between planning and execution) |
| "compilation fails with `error CS####` traceable only to a VictoriaProject-hardcoded absolute path, not to simulation/game logic" | the exact Success Condition 3 compile invocation | the compile log's verbatim `error CS####` line(s), pasted in full — and only that class of error (a path-resolution literal, not a logic error) authorizes the single narrow `// FORGEHISTORY-PATH-ADJUSTMENT` exception named in Non-Goals; Success Condition 3 must still be re-run and pass green after the adjustment, never marked "excused" |
| "insufficient disk space to complete the copy and/or the Library import at the new location" | `Get-PSDrive D \| Select-Object Used,Free` (or equivalent) run immediately before the copy | `Free` (in bytes) is less than the `robocopy` summary's reported source byte total for the non-excluded tree — an actual numeric comparison from real command output, not an estimate |
| "an EditMode test fails for a reason unrelated to the port itself" | the exact Success Condition 4 test invocation | the NUnit XML's specific failing `<test-case>` node's message/stack-trace, **and** a citation of where `HANDOFF.md` already documents that exact test/class as pre-existing-red, environment-dependent, or PlayMode-only-by-mistake in VictoriaProject itself — absent that citation, a test failure is a real port regression and blocks Success Condition 4, full stop |

## Session-Cost Note (informs `eval-rubric.md`)

Success Conditions 3 and 5 are cheap for the Évaluateur to re-run
independently in full (a compile pass and one `-executeMethod` capture
invocation are each single, bounded Unity launches). Success Condition 4 is
not: the full EditMode suite is 200+ cases at measured per-case costs in the
low seconds in VictoriaProject, plus Unity batchmode launch overhead each
time — re-running the *entire* suite twice (once by the Générateur, once
independently by the Évaluateur) is a real, avoidable session-time cost for
a check whose purpose is corroboration, not first discovery. `eval-rubric.md`
accordingly requires the Évaluateur to independently re-run a **named
subset** (three fixtures: `V1094PilotLiveOwnershipTests`,
`V1070PoliticalMapTests`, `V1095GpuMapTests` — chosen because they are the
three systems this port most directly puts at risk: the pilot-map
translation fix, the archived-capture-dependent orientation control, and the
GPU render path) via Unity's `-testFilter`, cross-checked against those same
three fixtures' entries in the Générateur's own full-suite XML, rather than
a second full-suite run.
