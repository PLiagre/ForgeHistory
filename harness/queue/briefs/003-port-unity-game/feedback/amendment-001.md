# Amendment 001 — Brief 003 (port VictoriaProject's Unity game)

**Authored**: 2026-07-31T20:15:00
**Author**: forge-planificateur
**Context**: `deliverables/generator-log.md` and
`deliverables/evidence/victoriaproject-uncommitted-diffstat.txt` (iteration
1) report a real, evidenced blocker outside the Générateur's authorized
scope — this amendment resolves it by authorizing one narrow additional
action. It does not reopen anything iteration 1 already established as
correct, and it does not edit `brief.md` or `eval-rubric.md` (see "Why this
is a separate file," below) — read this together with both, not instead of
either.

## What iteration 1 found, and why it is not the Générateur's to fix

The copy is byte-exact and complete; VictoriaProject is proven untouched
(hash-identical sentinels before/after). Compilation then failed with 33
`error CS####`, all inside one file,
`Assets/Scripts/Presentation/PilotMapProvider.cs`. The Générateur traced
this — read-only, no write to VictoriaProject at any point — to a real fact
about the source, not about the port: VictoriaProject's own working tree
carried an **uncommitted, unfinished, syntactically invalid** edit
(`WritePortageProofAndCapturesV1_096b`, 675 insertions on top of HEAD) at
the exact moment this brief's copy was taken. `C:\Users\liagr\VictoriaProject\HANDOFF.md`
asserted "arbre propre... tout poussé" as of 2026-07-29 and named the
*next planned* brief as `v1_096` — not yet started. That assertion was false
at copy time. This is the drift between what VictoriaProject's own
`HANDOFF.md` claimed and what its working tree actually held.

**This drift is consigned here as a fact, not corrected.** Nobody —
Générateur, Évaluateur, or this Planificateur pass — modifies
`C:\Users\liagr\VictoriaProject\` to "clean it up." It stays exactly as it
is, uncommitted WIP included; VictoriaProject remains read-only for the
whole of brief 003, as Success Condition 1 and its Non-Goals already
require and as iteration 1 already proved held. The gap is a measured
property of VictoriaProject's own history at this date, recorded here and
in `deliverables/generator-log.md`'s Finding section, and left there.

## The arbitration, and what it authorizes

The owner's decision (this brief's "Owner Decision" section, `brief.md`)
says to port "le code existant **fonctionnel**." Read in light of what
iteration 1 found: the functional code is VictoriaProject's last
**committed** state under `game_unity/` — the state the whole rest of
`brief.md`'s World-Terms Requirement describes by name and by measurement
(v1_094's translation fix, v1_095's GPU path at 0.305 ms/frame, v1_095b's
derived orientation reference, the 217/217 EditMode run `HANDOFF.md`
records at last measurement) — **not** whatever happened to be sitting
uncommitted in the working directory at the moment of copy. A robocopy of
the raw working tree silently included WIP that was never tested, never
green, and never part of the functional baseline this brief exists to
carry over. That is a real gap in Success Condition 1's original copy
method, not a Générateur error: nothing in iteration 1's execution was
wrong given what `brief.md` asked for.

This amendment therefore authorizes exactly one additional action, scoped
narrowly:

### Authorized remediation (adds to Success Condition 1; does not reopen it)

1. **Identify every dirty file under `game_unity/`** in VictoriaProject's
   working tree, read-only:
   ```
   git -C C:\Users\liagr\VictoriaProject diff --name-only HEAD -- game_unity
   git -C C:\Users\liagr\VictoriaProject status --porcelain -- game_unity
   ```
   The first command lists tracked files that differ from HEAD. The second
   additionally surfaces untracked (`??`) files under `game_unity/` — any of
   those that were copied into `unity/game_unity/` by iteration 1's robocopy
   pass are in scope for removal (step 3).

2. **For every tracked dirty file**, replace the copy already sitting at
   `D:\ForgeHistory\unity\game_unity\<same relative path>` with the HEAD
   version, extracted read-only:
   ```
   git -C C:\Users\liagr\VictoriaProject show HEAD:game_unity/<path> > D:\ForgeHistory\unity\game_unity\<path>
   ```
   `git show` reads from VictoriaProject's object database and writes only
   to the destination path given on the right of the redirect — it does not
   touch VictoriaProject's working tree, index, or HEAD. This is the same
   class of operation Success Condition 1 already performed with `robocopy`
   (read from VictoriaProject, write to the port), substituting the source
   selector (HEAD content instead of working-tree content) for exactly the
   files identified in step 1 — nothing else about Success Condition 1
   changes.

3. **For every untracked file from step 1 that was copied into the ported
   tree**, remove it from `unity/game_unity/` and declare the removal (see
   Required Counters below) — it was never part of any committed,
   functional VictoriaProject state and has no HEAD version to restore.

4. **Declare every restored and every removed file explicitly** in
   `deliverables/manifest.json`, each restored file's entry carrying the
   SHA256 of the HEAD version actually written (derived by hashing the
   written file after step 2 — not asserted from the `git show` command
   having been run). Do not fold this into a single aggregate claim; one
   entry per file, so `files_declared_exist` and `mtime_after_brief` can
   check each individually.

5. **Then, and only then, resume Success Conditions 3, 4, and 5** from the
   remediated tree — re-run the exact invocations `brief.md` already
   specifies (compile, `-runTests -testPlatform EditMode`, and
   `-executeMethod VictoriaGame.Tests.V1094BatchRunner.Run`), each preceded
   by the same lockfile+process check Success Condition 9 already requires.
   **Success Condition 5's capture pair must be genuinely regenerated at
   the new location this time** — iteration 1's `01_avant_conquete.png` /
   `03_apres_conquete_VERT_ecs.png` were the pre-existing files `robocopy`
   carried over (VictoriaProject mtime 2026-07-28), which is exactly why the
   gate's `mtime_after_brief` and `rubric_predates_deliverables` checks
   currently fail for that pair — both predate `brief.md`'s Authored
   timestamp (2026-07-31T15:00:00). A successful `-executeMethod` run this
   time overwrites both files with fresh output; their mtimes must postdate
   the brief for the gate to pass, and that freshness is itself evidence the
   run actually executed rather than being asserted.

**Nothing else in `brief.md` changes.** Success Conditions 1 (copy scope
and exclusions, other than the remediation above), 2 (`.gitignore`), 6
(ADR-0004), 7 (launcher/README), 8 (deliverables contract), and 9 (lockfile
discipline) are unaffected — iteration 1 already satisfied all of them and
none of that work needs to be redone or re-verified from scratch. The
Non-Goals list is unchanged and fully in force, including "no `.cs` diff
beyond the one narrow path-adjustment exception" — restoring a file to its
own HEAD content via `git show` is not editing it; a restored file's SHA256
must equal HEAD's, not differ from it, which is the opposite of the kind of
change Non-Goals forbids.

## Required Counters — additions (brief.md's table is not edited; these are additive)

| name | sample source | denominator |
|---|---|---|
| victoriaproject_dirty_tracked_file_count | `git -C C:\Users\liagr\VictoriaProject diff --name-only HEAD -- game_unity` (line count) | same count (must be > 0 — this is what makes the remediation necessary; report honestly even if it turns out to be exactly the 1 file iteration 1 already found, or more) |
| victoriaproject_untracked_copied_file_count | `git -C C:\Users\liagr\VictoriaProject status --porcelain -- game_unity`, `??` entries, filtered to those actually present under `unity/game_unity/` post-copy | same count (may legitimately be 0 if no untracked file was copied — a 0 here is a real measurement, not a sentinel, because the check ran and found none) |
| head_restored_file_count | count of files under `unity/game_unity/` whose SHA256 now equals `git -C C:\Users\liagr\VictoriaProject show HEAD:game_unity/<path>`'s SHA256, among the dirty-tracked set from step 1 | `victoriaproject_dirty_tracked_file_count` (must be equal — every dirty tracked file is fully restored, none partially) |
| untracked_removed_file_count | count of files from the untracked-copied set (previous row) no longer present anywhere under `unity/game_unity/` after remediation | `victoriaproject_untracked_copied_file_count` (must be equal) |

These join, not replace, the Required Counters already in `brief.md`.
`compile_error_cs_count`, `test_total_count`/`test_passed_count`/
`test_failed_count`, and `capture_pair_sha256_distinct_count` must all be
**re-measured from scratch** against the remediated tree per the
Générateur's own standing workflow rule ("don't reuse stale numbers") — the
sentinel `-1` values and the 33-error value from iteration 1 are
iteration-1 facts about the unremediated tree; they do not carry forward.

## Eval-rubric addendum (`eval-rubric.md` is not edited; this section is authoritative alongside it)

Add these rows to the rubric the Évaluateur applies, without renumbering or
touching the existing rows' text:

| # | What | Check type | How it is checked |
|---|---|---|---|
| 1-remediation | Every dirty tracked file identified is fully restored to HEAD content; every copied untracked file is removed | Mechanical | `head_restored_file_count` == `victoriaproject_dirty_tracked_file_count` AND `untracked_removed_file_count` == `victoriaproject_untracked_copied_file_count`; Évaluateur independently re-runs both `git diff --name-only HEAD -- game_unity` and `git status --porcelain -- game_unity` against VictoriaProject itself (read-only) and re-hashes each restored file in the port against a fresh `git show HEAD:game_unity/<path>` — not trusting the Générateur's own SHA256 claim alone |
| 1-remediation | VictoriaProject itself remains untouched by the remediation (git show, not git checkout/reset, was used) | Mechanical + Manual | mechanical: `victoriaproject_source_unmodified_count`'s existing sentinel-file re-hash still holds; manual: Évaluateur runs `git -C C:\Users\liagr\VictoriaProject status --porcelain -- game_unity` once more at verdict time and confirms it is byte-identical to the `status --porcelain` output the Générateur captured during remediation — no new dirt, no cleanup, nothing reset |
| 3/4/5-retry | Compile/test/capture re-attempted against the remediated tree, and pass | Mechanical + Manual | same check methods already defined in `eval-rubric.md`'s rows for Success Conditions 3/4/5 (including the Évaluateur's own independent re-run of the compile step and the three named `-testFilter` fixtures, and looking at the captures by eye) — applied now to the remediated tree's output, not iteration 1's |
| 5-retry | Capture pair is freshly regenerated, not carried over from the original robocopy | Mechanical | both `Captures/v1_094/01_avant_conquete.png` and `03_apres_conquete_VERT_ecs.png` mtimes postdate `brief.md`'s Authored value (2026-07-31T15:00:00) — this is exactly gate check `mtime_after_brief`, now expected to pass where it currently fails |
| Non-Goal | No VictoriaProject file outside the declared dirty/untracked set was touched by remediation | Manual | Évaluateur diffs the full `git status --porcelain -- game_unity` output (VictoriaProject) captured before and after this iteration's work — must be identical; any new dirt is a FAIL of this row regardless of what else passed |

The existing rubric rows for Success Conditions 1, 2, 6, 7, 8, 9 (and the
standing mechanical-gate rows: `no_empty_sample_pass`, `waivers_have_
command_and_error`, `no_bare_python_alias`, `verdict_numbers_traceable`,
`verdict_is_not_self_authored`, `rubric_predates_deliverables`,
`captures_differ_when_should`) are unchanged and apply exactly as written.

## Why this is a separate file, not an edit to `brief.md`/`eval-rubric.md`

Per instruction: `brief.md` and `eval-rubric.md` are not edited for this
amendment, so that already-produced, already-partially-correct deliverables
(the copy, the `.gitignore`, the ADR, the README/launcher) are never put at
risk of a stale-relative-to-brief mtime finding on re-audit. This file is
additive and narrowly scoped — it authorizes one action (restore dirty
files to HEAD; remove untracked strays) that `brief.md`'s original Success
Condition 1 did not anticipate because iteration 1 had not yet discovered
that VictoriaProject's working tree and its own `HANDOFF.md` disagreed. It
does not restate or paraphrase anything `brief.md` already says elsewhere
(single-source-of-instruction discipline, `docs/rules/harness-roles.md`) —
where this file is silent, `brief.md` and `eval-rubric.md` still govern in
full.

## Plateau note

This is iteration 2 for brief 003. If the remediated tree still fails to
compile for a reason distinct from the `v1_096`/`v1_096b` WIP (i.e. a second,
independent defect surfaces), that is a second real finding, not a retry of
this one — report it the same way (command, error, causal trace) rather
than reflexively invoking Acceptable Waivers row 2. If two iterations in a
row fail to make progress on the *same* blocking finding, escalate to the
owner per `docs/rules/harness-roles.md`'s plateau rule instead of a third
attempt.
