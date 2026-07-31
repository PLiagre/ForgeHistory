# Brief 004: Bounded visual polish of the ported game — accents, debug leakage, localization

**Authored**: 2026-08-01T11:00:00
**Author**: forge-planificateur

## Precondition — Unity availability (read before the Générateur starts anything)

Only one Unity instance runs on this machine at a time. At the time this
brief is written, brief `003-port-unity-game`'s Évaluateur is actively using
Unity to independently re-verify that brief's compile/test/capture proofs.
**The Générateur for this brief must not launch Unity, and must not begin
any work that requires exclusive access to `unity/game_unity/`, until the
orchestrator explicitly signals that brief 003's evaluation has released
it.** Before every Unity invocation in this brief (same discipline brief
003 established), check `Test-Path unity/game_unity/Temp/UnityLockfile`
combined with `Get-Process Unity -ErrorAction SilentlyContinue`. If a live
Unity process holds the lockfile, that is a hard stop — wait, do not force
a second instance, and do not silently proceed with unrelated (non-Unity)
work as a substitute for the gated work. See Acceptable Waivers row 3 for
how to report this if it blocks the session.

## Owner Objective This Brief Implements

The owner's stated objective for the session (recorded in brief 003):
« un jeu beau et fonctionnel ». Brief 003 delivered *fonctionnel* — the
ported game compiles, its actually-maintained reference suite (established
by proof, not assumed) is 100% green, and a real before/after capture pair
proves the world is observably rendered. This brief is the ForgeHistory
equivalent of the bounded polish pass VictoriaProject itself prepared but
never ran: `C:\Users\liagr\VictoriaProject\cursor_tasks\hold\task_v1_056.json`
(id `v1_056`, held, never executed — see
`docs\ui\PLAN-DA-medievale-sombre.md`'s "Arrêt demandé après v1_055"
section). The dark-medieval art direction itself (palette fer/parchemin/
sang séché, structure, responsive layout) already shipped in VictoriaProject
(`v1_053`→`v1_055`) and is already present, unchanged, in the ported tree —
this brief does not redesign it. It closes the three specific, already-
identified defects `v1_056` was going to fix, plus any P0/P1 from
`docs/ui/REVUE-v1_054.md` that inspection proves is still open, bounded
exactly as `v1_056`'s own held brief bounded itself: "polir sans alourdir."

## World-Terms Requirement

Stated causally, not as a code-quality preference:

Principle 4 (`docs/rules/simulation-principles.md`) requires that
presentation READS simulation state, it never decides or re-derives it. The
defects this brief closes are not aesthetic preferences — each one is a
place where the read-and-display path currently produces a **false or
degraded observation** of what the simulation actually computed, which is
the same class of failure principle 4 exists to prevent, just at the
rendering-fidelity layer rather than the logic layer:

- A province or status label that drops an accented letter instead of
  transliterating it (`ÎLE-DE-FRANCE` rendered as `LE-DE-FRANCE`,
  `AFFAMÉS` rendered as `AFFAM S` or similar) does not merely look wrong —
  it changes what word the player reads, sometimes into a different or
  unreadable one. The simulation knows the correct name; the display
  corrupts it in transit.
- A debug token (`HOVER`, `ZOOM COUNTRY C0`, a raw entity/technical
  identifier) rendered into the default player-facing banner tells the
  player something about the engine's internals, not about the world they
  are governing — it is diagnostic leakage into the one channel that is
  supposed to represent the game world to them.
- A tax rate rendered as `2E-5` instead of a French-formatted decimal is
  technically the same number, but it does not communicate a quantity a
  player can reason about — the translation from computed value to legible
  quantity has failed, even though the underlying computation is correct.

Fixing all three is fixing the presentation layer's fidelity to what the
simulation actually holds — not embellishment, and not a simulation change:
zero lines of simulation logic move in this brief.

## Success Conditions

1. **Accent transliteration, proven present then proven fixed.** Wherever a
   fresh capture (map render via `MapSnapshotExporter`, and/or the HUD)
   shows a diacritic being **dropped** (the letter disappears or is replaced
   by a blank/box) rather than **folded** to its unaccented ASCII equivalent
   (`Î`→`I`, `É`→`E`), fix it so the letter is folded, never dropped —
   `ÎLE-DE-FRANCE` → `ILE-DE-FRANCE`, `AFFAMÉS` → `AFFAMES`. This applies
   everywhere the defect is proven present in a capture, not only the two
   named examples — both the map's own label rendering and the HUD's text
   are in scope if either shows the defect. Do not touch any string that
   already transliterates correctly. A before capture demonstrating the
   drop and an after capture demonstrating the fold, same scenario, are
   required (`must_differ_from` pair; Required Counters).

2. **Debug leakage gated behind an explicit debug mode, not deleted.**
   `HOVER`, `ZOOM COUNTRY C0`-style zoom/hover diagnostics, and raw
   technical identifiers currently visible in the default player banner are
   hidden unless an explicit debug mode is turned on. This must be a real
   gate, not a removal: prove **both** that the default (non-debug) capture
   no longer shows any of the named tokens, **and** that turning the
   existing/added debug flag on still shows them — two capture pairs
   (defect-present-before vs hidden-by-default-after; hidden-by-default vs
   visible-in-explicit-debug-mode), both `must_differ_from` pairs.

3. **Player-banner decimals in French format, no scientific notation.**
   Wherever the player banner currently renders a number in scientific
   notation (e.g. `2E-5`) or a non-French decimal separator, reformat it to
   a French-locale decimal (comma separator, no exponent). Prove the defect
   with a fresh capture/log from the exact reproducible scenario that
   produced it historically (the REVUE-v1_054.md-cited fiscal panel is the
   known reproduction case), and prove the fix the same way — before/after
   pair.

4. **Any P0/P1 from `docs/ui/REVUE-v1_054.md` confirmed still open, and
   only those, are fixed — bounded, no embellishment outside this list.**
   `REVUE-v1_054.md`'s two P0s (synthetic bitmap capture instead of real
   UI Toolkit framebuffer; overlapping bitmap/UI-Toolkit panels) were
   closed by `ui_002`/`ui_003` before `v1_055` shipped — **inspect and
   confirm this by fresh capture** (no overlapping bitmap panel, no
   synthetic-capture fallback in use) rather than assuming it from prose;
   report this confirmation, do not re-fix what is already fixed. Of the
   two P1s: "l'information reste un dump technique" is exactly Success
   Conditions 1-3 above plus the 6-to-10-indicator/French-labels items —
   fix whatever part of it is proven still open by a fresh capture, bounded
   to what `REVUE-v1_054.md` actually lists (no new indicators, no
   redesign). The second P1, pause-state ambiguity (`LECTURE` shown during
   pause with no distinct `EN PAUSE` indication), must be **inspected
   first**: if a fresh capture shows it already resolved, state that with
   the capture as evidence and do not touch it; if still open, fix it
   (a distinct, readable "paused" indication separate from the
   action-button label) and prove it with a before/after pair. **No defect
   outside this brief's named list (Success Conditions 1-4 exactly) is
   fixed, however minor or tempting** — that is exactly the "embellishment"
   `v1_056`'s own held brief forbade.

5. **Final visual proof gallery — the deliverable the owner will actually
   look at.** A fresh set of captures of the final state, taken from the
   ported location:
   - The `MapSnapshotExporter` EditMode political-map path (proven
     capturable per brief 003 — reuse the same mechanism, a fresh run, not
     recycled iteration-4 captures).
   - The `v1_055` standalone-player framebuffer chain
     (`docs/ui/REVUE-v1_054.md`'s "vrai rendu UI Toolkit" path,
     `ui_002`/`ui_003`'s established mechanism) **if it can be built and run
     from this environment** — see Acceptable Waivers row 1 if it cannot.
   Both this brief's own fixes (accents, no debug leakage, French decimals)
   must be visible in at least one image each in this gallery, not proven
   only by the narrower before/after pairs in Success Conditions 1-3.

6. **Reference suite stays 100% green; the 7 legacy-attributed tests from
   brief 003 are untouched.** Re-run brief 003's own reconstructed reference
   suite (`unity/README.md`'s "EditMode suite VictoriaProject itself
   actually maintained" — do not re-derive a different definition; reuse
   the one brief 003 already established and cite it by pointer) from the
   ported tree, fresh, after this brief's changes. It must be 100% green,
   counts derived from a fresh NUnit XML. The 7 test files brief 003
   individually attributed as legacy/frozen (`cluster_c_legacy_attributed_count`
   in `harness/queue/briefs/003-port-unity-game/deliverables/manifest.json`)
   are not modified, not weakened, not rebased — byte-identical to their
   state at brief 003's hand-off (Required Counters).

7. **Artistic verdict is `A_REVOIR_HUMAINEMENT`, never self-declared
   `ADOPTÉ`.** The owner decides "beau." `generator-log.md` and
   `manifest.json` record the literal status string
   `A_REVOIR_HUMAINEMENT` for the final gallery — this is a hard,
   non-negotiable requirement regardless of how the Générateur judges its
   own work, mirroring `task_v1_056.json`'s own constraint
   ("OBLIGATOIRE — verdict artistique final A_REVOIR_HUMAINEMENT, sans
   auto-ADOPTÉ").

## Non-Goals

- Must **not** add a new screen, panel, or view.
- Must **not** add any external asset or package (font, icon, texture) —
  `docs/ui/PLAN-DA-medievale-sombre.md`'s existing "no external asset
  blocking" rule holds; aplats/existing resources only.
- Must **not** weaken, delete, or skip any test to reach green.
- Must **not** introduce any dependency from simulation code toward
  presentation code, or vice versa beyond the existing read-only path —
  zero simulation logic lines change in this brief.
- Must **not** touch any of the 7 legacy-attributed test files from brief
  003 (see Success Condition 6) — they remain red, on purpose, documented.
- Must **not** rebase, weaken, or cite by value any parity/determinism
  anchor — any anchor is cited by NAME only (hard-won rule 12), and this
  brief has no authorization to rebase one regardless of what it finds
  (mirrors brief 003's Non-Goals).
- Must **not** fix anything beyond Success Conditions 1-4's named list, even
  if a fresh capture reveals something else that looks wrong — a newly
  discovered defect outside this list is a finding for a future brief, not
  an invitation to embellish this one.
- Must **not** run `git commit` (or any staging/commit action) at any point.
- Must **not** hand-write a `.meta` file — if a new file must be added,
  delete any hand-written `.meta` and let Unity import it (mirrors
  `task_v1_056.json`'s own constraint).
- Must **not** self-declare `ADOPTÉ` anywhere in `generator-log.md` or
  `manifest.json`.
- Must **not** launch Unity or touch `unity/game_unity/` before the
  orchestrator signals brief 003's evaluation has released it (see
  Precondition).
- Must **not** report a capture-based claim ("the accent is now correct",
  "the debug token is hidden") without a real, freshly-produced image on
  disk at this session's own timestamp — presence of an old capture from
  brief 003 does not stand in for this brief's own evidence.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| accent_defect_present_before_count | a fresh capture/log from a fixed, reproducible scenario containing at least one known-diacritic label (e.g. a province/status name using `Î`/`É`), counted for occurrences where the accented character is dropped rather than rendered/folded | total accented-name occurrences checked in that same scenario (must be > 0 — proves the defect is real before claiming a fix) |
| accent_defect_present_after_count | same scenario, same accented-name set, same counting method, captured fresh after the fix | same denominator as `accent_defect_present_before_count` (must equal 0 — every one folded, none dropped) |
| debug_leak_default_mode_count | fresh default-mode (non-debug) capture/log, count of occurrences of the named debug tokens (`HOVER`, `ZOOM COUNTRY`, raw technical/entity identifiers) | must equal 0 |
| debug_leak_explicit_debug_mode_count | fresh explicit-debug-mode capture/log, same token set | must be >= 1 (proves the gate toggles both ways, not a deletion) |
| scientific_notation_before_count | fresh log/export from the known fiscal-panel reproduction scenario, count of numeric strings matching scientific notation (e.g. pattern `[0-9]E[+-]?[0-9]`) or non-French decimal separators | must be > 0 (proves the defect is real) |
| scientific_notation_after_count | same scenario, same pattern, captured fresh after the fix | must equal 0 |
| p1_pause_ambiguity_addressed_flag | inspection of a fresh capture during an active pause: 1 if this brief fixed a confirmed-open ambiguity, 0 if inspection confirmed it was already resolved (both are real, computed outcomes; sentinel `-1` only if the inspection itself could not be completed, with a command+error) | 1 (a single inspection outcome; must not be silently omitted) |
| reference_suite_total_count / reference_suite_passed_count | fresh NUnit XML from re-running brief 003's own reconstructed reference suite (cited by pointer to `harness/queue/briefs/003-port-unity-game/deliverables/manifest.json`'s `reference_suite_*` counters' method — not re-derived independently) | `reference_suite_passed_count` must equal `reference_suite_total_count` (100% green; both must be > 0) |
| legacy_attributed_test_files_unchanged_count | SHA256 of each of the 7 files brief 003 individually attributed as legacy (named in that brief's `cluster_c_legacy_attributed_count` evidence), compared against their SHA256 at brief 003's hand-off | 7 (must equal 7 — none modified) |
| visual_proof_pairs_distinct_count | SHA256 of each declared before/after capture pair (Success Conditions 1, 2 [x2 pairs], 3, and 4's pause-ambiguity pair if fixed) | total declared pairs (must equal — every pair genuinely differs) |
| p0_regression_check_count | fresh capture confirming each of `REVUE-v1_054.md`'s two closed P0s (real UI Toolkit framebuffer, no overlapping bitmap panel) still holds in the ported tree | 2 (must equal 2 — both confirmed still closed, neither silently regressed) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "the `v1_055` standalone-player framebuffer capture chain cannot be built/run from this environment" | the actual standalone-player build invocation attempted (e.g. `-batchmode -quit -buildWindowsPlayer <path> -logFile <abs>`) | the build/run log's actual failure output, pasted in full — if invoked, Success Condition 5's gallery is satisfied by the `MapSnapshotExporter` EditMode path alone, with the standalone-chain gap explicitly declared, not silently omitted |
| "a confirmed-open P0/P1 or named defect cannot be fixed without crossing the presentation-reads-only boundary into simulation code" | n/a — cite the specific coupling found (file/line) | this is not a command-provable impossibility in the usual sense; instead, the specific coupling must be quoted and the fix declared blocked/out-of-scope for this brief, escalated as a finding for a future brief — never crossed to "make it work" |
| "Unity is held by a live process when this Générateur is signaled to start, or becomes held mid-session" | `Test-Path unity/game_unity/Temp/UnityLockfile` combined with `Get-Process Unity -ErrorAction SilentlyContinue` | a live Unity.exe PID found holding the lockfile — wait, do not force a second instance; report back rather than substituting unrelated work silently for the gated work |

## Session-Cost Note (informs `eval-rubric.md`)

Brief 003 already re-derived and proved its reference suite once; this
brief does not re-derive that definition from scratch — it re-runs the same
reconstructed suite (cited by pointer) fresh, after its own changes, once.
The Évaluateur is not expected to independently re-derive the reference
suite's *definition* a second time (that would duplicate brief 003's own
work); it re-runs the suite itself and looks at every declared capture pair
by eye (hard-won rule 11, non-waivable) — that is where this brief's actual
risk lives, not in re-litigating what "reference suite" means.
