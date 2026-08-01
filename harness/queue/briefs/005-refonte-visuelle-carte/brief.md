# Brief 005: Refonte visuelle carte — orientation, cadrage, fluidité, traits, front, UI, pacing

**Authored**: 2026-08-01T15:10:00
**Author**: forge-planificateur

## Precondition — Unity availability (read before the Générateur starts anything)

Before every Unity invocation in this brief (same discipline briefs 003/004
established), check `Test-Path unity/game_unity/Temp/UnityLockfile` combined
with `Get-Process Unity -ErrorAction SilentlyContinue`. If a live Unity
process holds the lockfile, that is a hard stop — wait, do not force a
second instance, and do not silently substitute unrelated work for the
gated work. See Acceptable Waivers row 4 for how to report this if it
blocks the session.

## Origin — this brief exists because the owner rejected brief 004's result

`harness/queue/briefs/004-polish-visuel/owner-verdict-2026-08-01.md` records
the owner's verbatim verdict on the ported game, after brief 004's four
Success Conditions were mechanically ACCEPTed: **"visuellement c'est pas
encore ça"** (not adopted). Two of the owner's eight grievances are
independently corroborated by the Évaluateur's own inspection of brief
004's own gallery (`verdict.md`, closing section): the live map is
presented vertically flipped with mirror-inverted labels, and the `Lois`
panel overlaps the `Impôt` panel closely enough to clip a heading's
circumflex. The other six grievances are the owner's word alone, not yet
independently measured — this brief's job is to measure each one honestly,
fix what is proven open, and report proven-absent where it is not.

This brief is **exclusively** about the presentation layer's fidelity to
what the simulation already computes and to what the owner asked for. It
does not redesign the dark-medieval art direction (`docs/ui/PLAN-DA-
medievale-sombre.md`), which already shipped and is not in question here.

## World-Terms Requirement

Stated causally, not as a code-quality preference (principle 4,
`docs/rules/simulation-principles.md`: presentation READS simulation state,
it never decides or re-derives it — a rendering-fidelity failure is the
same class of defect as a logic failure, one layer up):

- The simulation computes one geography — a province and its neighbours sit
  at fixed coordinates, England north of Iberia, Bourgogne east of
  Champagne. When the export path (`MapSnapshotExporter.WriteMapBufferPng`)
  and the live path (`InGameHud.PresentFrame` / `PresentRenderTexture`)
  disagree about which row is north, the player is not looking at a
  stylistic choice — they are looking at a **false observation of the
  world's own geometry**, one that contradicts the same engine's own other
  output. A single documented row-origin convention, held identically by
  every consumer, is what principle 4 requires; two consumers each claiming
  "row 0 is north" while producing opposite results on screen is exactly
  the coexisting-convention failure principle 4 exists to prevent.
- The camera's initial framing is a read of where the *playable* world
  actually is (the loaded provinces with data, owners, populations — not
  the full unpopulated map buffer). Framing the whole world buffer when
  only Europe carries game state shows the player mostly empty ocean and
  un-simulated land — a degraded observation of what is actually there to
  govern, derived from the wrong extent.
- A border stroke's on-screen thickness is a read of the province/country
  adjacency the simulation already computed; nothing about the simulation
  changes when the same adjacency renders 1px or 6px — but a stroke whose
  apparent width scales with zoom because it is measured in a resource the
  camera moves through (texture texels) rather than what the eye actually
  measures (screen pixels) is a rendering-fidelity bug, not a variant.
- The red front-rim is a read of a real simulation fact — an actually
  contested border between two actually-fighting sides. The fix is not to
  hide or invent that fact; it is to make the *display* of that already-
  computed fact legible (the player can learn what it means) and visually
  proportionate (not the loudest color on a political map for a state that
  is, most of the time, not occurring) — exactly the same "read, don't
  distort" requirement applied to a rarer piece of state.
- The tick rate is a presentation/pacing choice about how fast the player
  *observes* the simulation's own already-deterministic sequence of ticks —
  it is not itself simulation logic, provided the sequence of states
  produced and their content are unchanged. Slowing observation down (or
  reducing wasted redraw cost) changes nothing about what the world
  computes; it only changes how legible that computation is as it happens on
  screen, which is exactly the fidelity question the other four items ask
  too.

Nothing in this brief authorizes recomputing, reordering, or filtering any
simulation output to make it look better. Every fix here is: hold one
convention, read the real extent/adjacency/state, and render it faithfully
and legibly.

## Priority Order (what the owner looks at first)

Work and report in this order — do not skip ahead and leave an earlier item
half-measured because a later one looked more tractable:

1. Map orientation (Success Condition 1)
2. Initial framing on the playable Europe extent (Success Condition 2)
3. Zoom fluidity (Success Condition 3)
4. Border stroke finesse (Success Condition 4)
5. War-front red — legible and discreet (Success Condition 5)
6. UI decluttering (Success Condition 6)
7. Tick pacing (Success Condition 7)

## Success Conditions

Every Success Condition below that names a defect not yet independently
proven present must be investigated honestly before any fix is attempted.
**Two real outcomes are both acceptable passes: Outcome A (defect proven
present, then proven fixed, before/after pair required) or Outcome B
(defect investigated with a real, non-empty, cited sample and proven
absent — no fix needed, no pair required, no fabricated "before").**
Manufacturing a synthetic "before" defect to force a pair is itself a FAIL,
worse than an honest Outcome B report (this is the same rule
`harness/queue/briefs/004-polish-visuel/amendment-002-absent-defect-waiver.md`
had to retrofit after the fact — this brief states it from the start so no
amendment is needed). `sample_size == 0` (nothing actually run) is never a
pass under either outcome (`no_empty_sample_pass`).

1. **Map orientation — one convention, proven identical between the export
   path and every live path.** `MapSnapshotExporter.WriteMapBufferPng`
   explicitly inverts rows before writing (`py=0 (nord buffer) → bas
   Texture2D → haut du PNG vu`, see its own doc comment) to produce a
   north-up PNG. `InGameHud.PresentFrame` calls `Texture2D.SetPixels32`
   directly on the same `py=0`-north buffer with no equivalent inversion,
   while its own doc comment claims the opposite convention holds without
   it (`UI Toolkit affiche py=0 en haut de l'écran`). This is independently
   corroborated already present (owner + Évaluateur, both live-in-game).
   Fix it as **one** convention change at the live-path row-origin
   boundary (not two compensating inversions), proven identical to the
   export path on the same world window: for at least 3 named geographic
   reference checks (e.g. a country pair with a known north/south relation,
   plus at least one accented/asymmetric label checked for mirroring), the
   live capture after the fix must agree with a fresh
   `MapSnapshotExporter` export of the *same* window on every reference
   check. Do this for `PresentFrame` (CPU path). Also measure
   `PresentRenderTexture` (GPU path) the same way — its own doc comment
   claims it reuses `PresentFrame`'s orientation, so this is a real,
   separate thing to verify, not an assumption; if the GPU path cannot be
   exercised/measured in this environment, use Acceptable Waivers row 1.

2. **Initial camera framing — the playable Europe extent, derived from
   data, not a hard-coded constant.** `MapViewportSystem.EnsureWorldWindow`
   currently frames the entire world-buffer extent
   (`worldGeo.MinX/MaxX/MinY/MaxY`). Investigate first: does the initial
   camera window, at session start with no manual pan/zoom, actually
   contain every playable province (a province that is owned, populated, or
   otherwise carries loaded game state — derived from the same data the
   game itself loads, never a hard-coded bounding box)? If it does not
   (Outcome A), change the initial framing to the bounding extent of the
   playable provinces, with a stated margin, still derived from data. If it
   already does (Outcome B), document the investigation with the real
   count of playable provinces checked and their extent, and change
   nothing.

3. **Zoom fluidity — measured in milliseconds, not impressions.** Two
   already-instrumented candidate costs exist:
   `MapGeometryCache`/`MapDisplaySystem`'s `LastWindowRebuildMilliseconds`
   (`GEOMETRY_BUILDS` counter) for geometry rebuilds on window change, and
   `InGameHud`'s CPU-fallback `PresentFrame` path (~18ms measured at
   960×720 per its own doc comment) versus the GPU `PresentRenderTexture`
   path, tracked by `GpuBackgroundUsedThisFrame` /
   `LastGpuBackgroundMilliseconds`. Measure both, fresh, across a fixed
   zoom sequence of at least 5 distinct zoom-level transitions (e.g.
   world → country → province → back to world → back to country). If any
   measured transition exceeds a stated frame budget (state the budget
   used, e.g. 33ms for a 30fps floor) — Outcome A: identify which of the
   two candidate costs (or another one found during investigation) is
   responsible, using the fresh measurements as evidence, and fix that
   specific cost (e.g. ensure the GPU path is actually taken instead of the
   CPU fallback; avoid rebuilding geometry when the window has not
   meaningfully changed). Re-measure the same sequence after the fix. If no
   measured transition exceeds the stated budget — Outcome B: document the
   real measurements (all of them, not a favorable subset) and change
   nothing.

4. **Border stroke finesse — a fine, crisp line at every zoom level, not a
   texel-scaled or aliased one.** `MapGpuRenderer.CountryBorderTexels`
   (1.5) and `CellBorderTexels` (1.0) are expressed in texels of the ID
   texture, so their apparent screen width grows as the camera zooms in;
   `MapPolitical.shader`'s hard 4-neighbour test has no antialiasing,
   producing a stair-stepped edge. Measure the apparent stroke width in
   screen pixels (not texels) at 3 distinct zoom levels (min/mid/max, same
   fixed sequence as Success Condition 3) — Outcome A if the measured width
   grows unacceptably with zoom or the edge is visibly stair-stepped at any
   level (state the criterion used and apply it consistently), fixed by
   making the stroke's apparent width and edge quality consistent
   (proportionate to zoom or capped, and anti-aliased) across all 3 levels,
   re-measured after. Outcome B if measurement shows the stroke is already
   fine and consistent at all 3 levels, documented, unchanged.

5. **War-front red — legible and visually discreet, information kept.**
   `MapSnapshotExporter.FrontRimColor` (210,36,36 — a fully-saturated
   "front franc") marks contested-war-front borders; the owner does not
   know what it represents and dislikes it. This is not a present/absent
   defect — the feature is a real simulation fact and stays. Two distinct,
   separately provable requirements: (a) **legible** — a player looking at
   the map can find out what the red edging (and the contested-front
   checker pattern) means, via an in-game legend or tooltip reachable from
   the map itself, not external documentation; (b) **discreet** — the
   color/treatment is changed to read as a distinct-but-secondary map
   marking rather than the single loudest element on a political map (state
   the concrete change made — e.g. reduced saturation, a different hue
   family, reduced opacity, a thinner rim — and justify why it remains
   distinguishable from ordinary country borders). Prove both with a
   before/after pair on a capture that actually contains at least one
   front-rim pixel. If no contested front is reachable in a fresh capture
   within a bounded, stated number of simulated ticks, use Acceptable
   Waivers row 2 — do not fabricate a war.

6. **UI decluttering — a verifiable non-overlap rule, and no raw internal
   dumps in default mode.** Two proven-present, independently corroborated
   items, in scope together because both are "il y en a partout": (a) the
   `Lois` panel overlaps the `Impôt` panel closely enough to clip glyph
   ascenders (corroborated by the Évaluateur opening brief 004's own
   gallery) — fix so that no two HUD panel bounding rectangles intersect in
   any of the fixed gallery scenarios reused from brief 004 (world neutral,
   country selected, province selected, tax/law panels open), proven by
   measuring every visible panel pair's on-screen rectangle in each
   scenario, before and after; (b) the province `Investir` block
   (`DevelopmentHudSnapshot.cs` / `InGameHud.cs`) renders a raw internal
   dump in default mode — `DEV T5 P4 M3 score=4 coût T/P/M 250/200/150` —
   identically in default and debug mode (corroborated twice by the
   Évaluateur opening brief 004's captures; explicitly carried into this
   brief because brief 004 correctly declined to touch it as out of that
   brief's scope). Apply the same real-gate pattern brief 004 already
   proved for `LAWMOD`/`EFF`/`STAB`/`LEG` (Success Condition 2 there): the
   raw technical tokens/identifiers are hidden by default and replaced with
   legible French labels for what a player actually needs (cost, expected
   effect), while remaining reachable, as raw tokens, in the existing debug
   mode — a gate, not a deletion. This is a hierarchy requirement, not a
   redesign: decide, and state, which pieces of information are
   permanently visible versus available only on demand (hover/select), and
   prove the decision by capture.

7. **Tick pacing — measured, then defensibly rebalanced, with parity
   proven unaffected.** `TickControl.DefaultSecondsPerTick` (0.3s, ~3.33
   ticks/s at ×1, 1 simulated year every ~3.6s) and
   `MapDisplaySystem.RefreshIntervalTicks` (10 — the map only repaints one
   tick in ten) are the two named candidates for "ça a l'air de bugué".
   Measure, fresh: the real per-tick simulation cost in milliseconds (at
   least 20 consecutive ticks at ×1, from a fresh instrumented run) and the
   real per-frame presentation cost already covered by Success Condition 3.
   Using those real numbers, choose and state a defensible ×1 speed for a
   1400–1900-scope simulation (this is a presentation/pacing constant, not
   simulation logic — no rule about how a tick's *content* is computed
   changes). Whatever value is chosen (including "unchanged, and here is
   why the current one is already defensible" as a legitimate Outcome B),
   **prove it does not change**: (a) determinism — the harness/capture
   tooling has no `TickControl` and already advances 1 tick per update;
   this brief must not touch that path or its behaviour; (b) parity — the
   reference test suite (Success Condition 9) re-run fresh after this
   specific change, isolated from Success Conditions 1–6's changes if
   feasible, still passes 100%. A pacing value change with no parity proof
   is not acceptable under any outcome.

8. **Final visual proof gallery — the deliverable the owner will actually
   look at.** A fresh set of captures of the final state, taken from the
   same location and mechanism brief 004 established (`MapSnapshotExporter`
   EditMode path; the `v1_055` standalone-player framebuffer chain if
   buildable, Acceptable Waivers row 5 if not). Every one of Success
   Conditions 1–7's Outcome-A fixes (whichever ones turn out to be Outcome
   A) must be visible in at least one gallery image, not proven only by the
   narrower before/after pairs above. Outcome-B items are reported in the
   gallery's accompanying log, not necessarily pictured (there is nothing
   to picture when nothing changed).

9. **Reference suite stays 100% green; the 7 legacy-attributed tests from
   brief 003 remain untouched and red.** Re-run brief 003's own
   reconstructed reference suite (cited by pointer, not re-derived) fresh,
   after all of this brief's changes. 100% green, both counts > 0, from a
   fresh NUnit XML. The 7 legacy-attributed test files
   (`cluster_c_legacy_attributed_count`,
   `harness/queue/briefs/003-port-unity-game/deliverables/manifest.json`)
   are byte-identical to their brief-003 hand-off state.

10. **Artistic verdict is `A_REVOIR_HUMAINEMENT`, never self-declared
    `ADOPTÉ`.** Exactly brief 004's Success Condition 7, restated because
    the owner's rejection of that exact self-restraint requirement is why
    this brief exists: the owner decides "beau," not the Générateur, no
    matter how the fixes above measure. `generator-log.md` and
    `manifest.json` record the literal string `A_REVOIR_HUMAINEMENT` for
    this brief's own final gallery.

## Non-Goals

- Must **not** change any line of simulation logic — what a tick computes,
  what data a province/country/family/person holds, or how ownership/war/
  economy state is derived — anywhere in this brief, with exactly one
  named, isolated exception: Success Condition 7's pacing constant(s)
  (`TickControl.DefaultSecondsPerTick`, `MapDisplaySystem
  .RefreshIntervalTicks`), which are presentation/pacing settings, not
  simulation logic, and whose change must be proven to leave the reference
  suite's parity/determinism untouched (Success Condition 7, Success
  Condition 9).
- Must **not** touch the harness/capture tooling's own tick-advance
  behaviour (1 tick per update, no `TickControl`) — Success Condition 7's
  pacing change is a player-facing ×1-speed setting only.
- Must **not** redesign the dark-medieval art direction (palette, general
  layout structure) — this brief fixes fidelity/legibility defects within
  it, it does not re-art-direct it.
- Must **not** add a new screen, panel, or view beyond what Success
  Condition 5 (legend/tooltip reachable from the map) and Success Condition
  6 (visibility gating, reusing brief 004's existing debug-mode mechanism)
  explicitly require.
- Must **not** add any external asset or package (font, icon, texture) —
  `docs/ui/PLAN-DA-medievale-sombre.md`'s existing "no external asset"
  rule holds; existing resources/aplats only.
- Must **not** add any new third-party dependency.
- Must **not** weaken, delete, or skip any test to reach green.
- Must **not** touch any of the 7 legacy-attributed test files from brief
  003 — they remain red, on purpose, unmodified.
- Must **not** rebase, weaken, or cite by value any parity/determinism
  anchor — cited by NAME only (hard-won rule 12).
- Must **not** fix anything beyond Success Conditions 1–7's named list,
  however minor or tempting a newly discovered defect looks — report it as
  a finding for a future brief, exactly as brief 004's own carried-forward
  section did (e.g. `Promulguer land_tax`'s raw law-id button label, the
  `Sat 0,798` abbreviation, and the `ATK vs BUR` war row are explicitly
  **not** in scope for this brief unless they are the same overlap/raw-dump
  class of defect already named in Success Condition 6 — if in doubt,
  report rather than fix).
- Must **not** run `git commit` (or any staging/commit action) at any
  point.
- Must **not** hand-write a `.meta` file.
- Must **not** self-declare `ADOPTÉ` anywhere in `generator-log.md` or
  `manifest.json`.
- Must **not** launch Unity while a live Unity process holds the lockfile
  (see Precondition).
- Must **not** report any capture-based claim without a real,
  freshly-produced artifact on disk at this session's own timestamp —
  reused captures from brief 003/004 do not stand in for this brief's own
  evidence.
- Must **not** report a count derived from an empty, unloaded, or
  synthetic world/scenario as a real measurement (`no_empty_sample_pass`)
  — every counter below states its own sample source; if that source is
  genuinely unreachable, use the matching Acceptable Waiver, never a
  silent zero.

## Required Visual Proof Pairs (`must_differ_from`)

Declared only for Success Conditions whose Outcome A is confirmed during
the Générateur's own investigation (Outcome B rows need no pair — see
Success Conditions section). Each pair below is captured on the *same*
scenario/selection/world-window on both sides, varying only the fix under
test:

| # | Pair name | Left | Right |
|---|---|---|---|
| P1a | `v005_orientation_cpu` | live `PresentFrame` capture, before fix | live `PresentFrame` capture, after fix |
| P1b | `v005_orientation_gpu` | live `PresentRenderTexture` capture, before fix | live `PresentRenderTexture` capture, after fix (or Acceptable Waivers row 1) |
| P2 | `v005_initial_framing` | initial camera window, before fix | initial camera window, after fix |
| P4a–c | `v005_border_zoom_{min,mid,max}` | border stroke capture at that zoom level, before fix | same zoom level, after fix |
| P5 | `v005_front_rim` | a capture containing >=1 front-rim pixel, before fix | same scenario, after fix |
| P6a | `v005_panel_overlap` | a gallery scenario showing the `Lois`/`Impôt` overlap, before fix | same scenario, after fix |
| P6b | `v005_investir_dump` | province panel capture, before fix | same province/scenario, after fix |

## Required Counters

| name | sample source | denominator |
|---|---|---|
| map_orientation_reference_checks_matched_count / _total_count (CPU) | fresh live `PresentFrame` capture of a fixed world window, checked against a fresh `MapSnapshotExporter` export of the identical window, over a named set of geographic reference checks (north/south country pair ordering, label-mirroring on named accented labels) | total reference checks performed (must be > 0, at least 3); `_matched_count` must equal `_total_count` after the fix |
| map_orientation_reference_checks_matched_count / _total_count (GPU) | same method, `PresentRenderTexture` path, or Acceptable Waivers row 1 if unreachable | same as above |
| playable_provinces_outside_initial_window_before_count / after_count | fresh capture of the camera window at session start (no pan/zoom), checked against the playable-province set actually loaded from game data (owned/populated provinces) | total playable provinces in that loaded data set (must be > 0); `after_count` must equal 0 |
| zoom_transition_ms_measured (list, one value per transition) | `LastWindowRebuildMilliseconds` and `LastGpuBackgroundMilliseconds`/`GpuBackgroundUsedThisFrame`, measured fresh at each transition of a fixed >=5-step zoom sequence, before and after any fix | zoom transitions measured (must be > 0, at least 5); every after-value must not exceed the stated frame budget |
| border_stroke_width_px_measured (list, one value per zoom level) | on-screen pixel measurement of a country-border stroke segment, at 3 fixed zoom levels (min/mid/max), before and after | zoom levels measured (must be > 0, exactly 3); after-values must satisfy the stated finesse criterion at all 3 |
| front_rim_legend_reachable_flag | inspection: 1 if a legend/tooltip explaining the front-rim marking is reachable from the map itself in a fresh capture, 0 otherwise, sentinel -1 only if no front-rim pixel was reachable within the stated tick bound (Acceptable Waivers row 2) | 1 (single flag; must not be silently omitted) |
| front_rim_color_change_proof_count | before/after RGB(+opacity) values of the front-rim marking, read directly from a capture containing >=1 front-rim pixel, before and after | captures containing >=1 front-rim pixel checked (must be > 0) |
| panel_overlap_pairs_before_count / after_count | axis-aligned bounding rectangles of every visible HUD panel, measured in each of the fixed gallery scenarios reused from brief 004 (world neutral, country selected, province selected, tax/law expanded), checked pairwise for intersection | total panel pairs checked across those scenarios (must be > 0); `after_count` must equal 0 |
| investir_raw_token_default_mode_before_count / after_count | default-mode captures of the province `Investir` block, over >=2 distinct provinces/scenarios, counted for raw technical tokens (`DEV`, snake_case identifiers, `score=`, unexplained `T/P/M` labels) versus legible French equivalents | token slots checked in that block across those captures (must be > 0); `after_count` must equal 0 in default mode, reachable in debug mode |
| investir_raw_token_explicit_debug_mode_count | same token set, explicit debug-mode capture, same scenarios | same denominator; must be >= 1 (proves a gate, not a deletion) |
| ms_per_tick_measured (list, one value per tick) | fresh instrumented run, at least 20 consecutive ticks at ×1, before and (if Success Condition 7 changes anything) after | ticks measured (must be > 0, at least 20) |
| harness_tick_advance_unchanged_flag | inspection of the capture/harness tick-advance mechanism's own source/behaviour, before and after this brief's changes | 1 (must equal 1 — unchanged; a 0 is a hard FAIL of Success Condition 7 regardless of any other measurement) |
| reference_suite_total_count / reference_suite_passed_count (Success Condition 7 isolation run, if separable) | fresh NUnit XML re-run immediately after Success Condition 7's change alone, before Success Conditions 1–6's changes are combined, if the Générateur's workflow allows isolating it; if not separable, state why and rely on the combined Success Condition 9 run | both > 0; `passed_count` must equal `total_count` |
| reference_suite_total_count / reference_suite_passed_count (final, Success Condition 9) | fresh NUnit XML from re-running brief 003's own reconstructed reference suite (cited by pointer to `harness/queue/briefs/003-port-unity-game/deliverables/manifest.json`'s method), after all of this brief's changes | both > 0; `passed_count` must equal `total_count` |
| legacy_attributed_test_files_unchanged_count | SHA256 of each of the 7 files brief 003 attributed as legacy, compared against their SHA256 at brief 003's hand-off | 7 (must equal 7) |
| visual_proof_pairs_distinct_count | SHA256 of every declared `must_differ_from` pair member (only for Outcome-A rows actually confirmed) | total declared pairs actually claimed (must equal — every pair genuinely differs) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "the `PresentRenderTexture` (GPU) live path cannot be exercised/measured in this environment" | the actual invocation attempted to force/measure the GPU present path (e.g. the specific test/scene entry point and flags used), with an absolute log path | the invocation's actual failure output, pasted in full — if invoked, Success Condition 1's GPU-path proof is satisfied by the CPU (`PresentFrame`) path alone, with the GPU-path gap explicitly declared, not silently omitted |
| "no contested/war front is reachable in a fresh capture within a bounded number of simulated ticks" | the actual simulation-advance invocation attempted (state the tick bound used, e.g. N ticks) with an absolute log path showing the war/front state checked at each step | the log showing zero front-rim pixels across every checked step — if invoked, Success Condition 5 is judged on the legend/discreetness design applied to the existing exported reference captures already in the tree (the `v1_092`/`v1_093`-style images the code's own doc comments cite), with the live-reproduction gap explicitly declared, never fabricated |
| "a confirmed-open defect cannot be fixed without crossing the presentation-reads-only boundary into simulation code" | n/a — cite the specific coupling found (file/line) | the specific coupling quoted verbatim; the fix declared blocked/out-of-scope for this brief, escalated as a finding for a future brief — never crossed to "make it work" |
| "Unity is held by a live process when this Générateur is signaled to start, or becomes held mid-session" | `Test-Path unity/game_unity/Temp/UnityLockfile` combined with `Get-Process Unity -ErrorAction SilentlyContinue` | a live Unity.exe PID found holding the lockfile — wait, do not force a second instance; report back rather than substituting unrelated work silently for the gated work |
| "the `v1_055` standalone-player framebuffer capture chain cannot be built/run from this environment" | the actual standalone-player build invocation attempted (e.g. `-batchmode -quit -buildWindowsPlayer <path> -logFile <abs>`) | the build/run log's actual failure output, pasted in full — if invoked, Success Condition 8's gallery is satisfied by the `MapSnapshotExporter` EditMode path alone, with the standalone-chain gap explicitly declared |

## Session-Cost Note (informs `eval-rubric.md`)

This brief does not re-derive the reference suite's definition from
scratch (brief 003 already did that, cited by pointer). Its actual cost is
visual and measurement work: every declared before/after pair must be
opened and looked at by eye (hard-won rule 11, non-waivable — this brief's
entire subject is what a human sees on a map and an interface), and every
timing counter (`zoom_transition_ms_measured`, `ms_per_tick_measured`,
`border_stroke_width_px_measured`) must be independently re-derivable from
a cited log, not merely asserted.
