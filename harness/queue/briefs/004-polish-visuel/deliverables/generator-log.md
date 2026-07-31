# Generator log — Brief 004: bounded visual polish (accents, debug leakage, localization)

**Author**: forge-generateur
**Session**: 2026-07-31, ~20:18–21:58 (system clock; see "Blocking finding" below)

## Blocking finding to report first — brief.md's own Authored timestamp is in the future

`brief.md`'s frontmatter reads `**Authored**: 2026-08-01T11:00:00`. `eval-rubric.md` reads
`2026-08-01T11:00:01`. The actual system clock throughout this entire session, confirmed
repeatedly via `Get-Date` and via every file's own on-disk `LastWriteTime`, stayed at
**2026-07-31, ~20:18 through ~21:58** — i.e. `brief.md`'s own file mtime is
`31/07/2026 20:18:44`, consistent with "now", but the *text* inside it names a point
roughly 13 hours in the future. `harness/verdict_audit.py`'s `check_mtime_after_brief` and
`check_rubric_predates` both parse that literal `Authored:` text field (not the file's own
mtime) and compare it against every declared deliverable's on-disk mtime
(`harness/verdict_audit.py:91-101`). No file this Générateur produces during this session —
however fresh, however real — can have an mtime later than 2026-08-01T11:00:00 without
literally waiting ~13 hours or fabricating a timestamp, and I will do neither: waiting idly
for the clock is not work, and fabricating an mtime is the kind of integrity violation the
harness exists to prevent (indistinguishable from lying about when work happened).

This is reported here, first, loudly, precisely because it is **not** a finding I can act on
by working harder or more carefully — it is a metadata inconsistency in `brief.md`/
`eval-rubric.md` themselves (their own `Authored:` field vs. their own file mtime disagree by
~15 hours), outside this Générateur's authority to edit (Single Source of Instruction: only
the Planificateur authors the brief). I expect `py harness/verdict_audit.py
harness/queue/briefs/004-polish-visuel` to report `FAIL` on `mtime_after_brief` and
`rubric_predates_deliverables` as a direct, mechanical consequence of this, regardless of how
the rest of the brief's substance was handled. I ran it anyway (last section of this log) so
the exact, real, current state is on record rather than guessed at.

## Precondition — Unity availability

Checked before every Unity invocation (Editor and standalone player) via
`Test-Path unity/game_unity/Temp/UnityLockfile` combined with
`Get-Process Unity -ErrorAction SilentlyContinue` (7 Editor invocations, each preceded by
exactly one check, all reporting the lockfile absent and no live `Unity.exe` process —
`unity_lockfile_checked_before_invocation_count=7`). The orchestrator's signal that brief
003's evaluation had released Unity was received before any of this work began; no Unity
invocation in this session predates that signal.

## What this brief actually found, in order

Before touching any source, I read the brief, the rubric, `docs/rules/hard-won-rules.md`,
`docs/ui/REVUE-v1_054.md`, `docs/ui/PLAN-DA-medievale-sombre.md`,
`C:\Users\liagr\VictoriaProject\cursor_tasks\hold\task_v1_056.json` (read-only), and brief
003's own `deliverables/generator-log.md` + `manifest.json` for the reference-suite pointer
and the 7 legacy-attributed test filenames. Then I read every code path the brief names —
`MapSnapshotExporter.cs`'s bitmap glyph renderer, `SanitizeLabelText`/`FoldDiacriticsToAscii`,
`HudValueFormatter.cs`, `MapDisplaySystem.cs`'s `AppendHover`/`FormatPanelLine`,
`InGameHud.ShowDebugIds`, `HudDetailPresenter.cs` — **before** running anything, per hard-won
rule 4 (prove, don't assume) applied in the direction of not assuming a defect either.

That reading turned up a load-bearing fact the brief's own text does not mention: this port's
HEAD state (v1_095b) is *later* than the point in VictoriaProject's own history
(`v1_055`/held `v1_056`, 2026-07-26) that `REVUE-v1_054.md` and `task_v1_056.json` describe.
Between that point and this port's HEAD, VictoriaProject shipped `v1_073`
(`MapSnapshotExporter.FoldDiacriticsToAscii`, still present, with its own doc comment: "Accents
repliés vers ASCII (NFD + cas spéciaux) AVANT le filtre — jamais élargis") and evidently closed
the decimal-formatting item too (`HudValueFormatter.FormatNumber`/`FormatTaxPercent` already
avoid scientific notation). I did not take this as settled from reading alone — I built and
ran the actual game to check with my own eyes, per hard-won rule 11, and to find whatever the
reading missed. What follows is what running it actually showed.

## Success Condition 1 — accent transliteration

**Measured state: no reproducible defect found. `accent_defect_present_before_count = 0`
(sample_size 11), not the `> 0` the brief's Required Counter expects.**

I did not stop at source reading. `V004AccentCaptureRunner.cs` (new file,
`Assets/Scripts/Presentation/Editor/`, `-executeMethod
VictoriaGame.Presentation.Editor.V004AccentCaptureRunner.Run`) calls
`MapSnapshotExporter.SanitizeLabelText` — the exact function every province/country/city
label-drawing call site in `MapSnapshotExporter.cs`/`MapLabelLayout.cs`/`CityPresentation.cs`
already runs through before `DrawBitmapText` — against every distinct accented name that
actually exists in this game's own `StreamingAssets/data/` (11 names: `Île-de-France`
(`ownership_1400.json`), `Châlons`, `Kutná Hora`, `Königsberg`, `Lübeck`, `Târgoviște`,
`Besançon`, `Liège`, `Nimègue`, `Nîmes`, `Orléans` (`cities.json`)). Result
(`unity/game_unity/Logs/v004_accent_capture.log`, copied to
`deliverables/evidence/v004_accent_capture.log`): **0 of 11 produced any unmapped/dropped
character** — `Île-de-France` → `ILE-DE-FRANCE`, `Târgoviște` → `TARGOVISTE`, etc., every
letter folded, none blanked.

I then reused brief 003's own proven mechanism (`MapSnapshotExporter` EditMode political-map
path, fresh run, `WriteMapBufferPng`) to render this at 960×720 with real province-name
labels — `Captures/v004_accent/02_world_province_labels.png` — and opened it myself.
**`ILE-DE-FRANCE` is legible, complete, right-side-up, no dropped letter, no blank, no box
glyph.** This image is this brief's own gallery item for Success Condition 5's accent
requirement.

Separately — and this matters for scope — the *live interactive* standalone-player captures
(`Captures/v004_before|v004_after_default|v004_after_debug/*.png`) show the map's own bitmap
labels rendered upside-down/garbled (e.g. `BOURGOGNE`, `LANGUEDOC`, `ENGLAND` — none of which
carry a diacritic — are equally illegible). This confirms the garbling is a text-*orientation*
defect specific to the live interactive rendering path (`InGameHud.PresentFrame`/
`PresentRenderTexture` via `Texture2D`), not an accent-fold defect (it affects
diacritic-free names identically), and it is present in **every** capture regardless of this
brief's fix (before, after-default, after-debug all show it identically) — i.e. unrelated to
anything I changed. It is a real, visually obvious defect but it is not named in this brief's
four Success Conditions or in `REVUE-v1_054.md`'s two P0s/two P1s. Per Non-Goals ("no defect
fixed outside the four named Success Conditions, however minor or tempting"), **I did not
touch it.** Flagging it here as a finding for a future brief: the interactive map's own
label-drawing path (GPU `PresentRenderTexture` and/or CPU `PresentFrame` →
`Texture2D.SetPixels32`) diverges from the documented single-inversion convention
(`MapSnapshotExporter.WriteMapBufferPng`'s own doc comment: "= ce que le joueur voit via UI
Toolkit"), producing upside-down text on the live map that the EditMode PNG-export path does
not reproduce.

**No source change was made for Success Condition 1** — there was nothing proven broken to
fix, and per this brief's own Non-Goals I do not touch what a fresh capture doesn't prove
open. I did not fabricate a "before" defect image to force the Required Counter's `>0` floor;
that would have been the exact violation hard-won rule 4 exists to prevent.

## Success Condition 2 — debug leakage (HOVER token)

**Measured state: real, reproducible defect found and fixed. Both required counter floors
met with real evidence.**

Reading found the genuine gap the brief describes, just not literally spelled `ZOOM COUNTRY
C0` (that exact French-less string does not exist anywhere in this port's source — the
current code already says `ZOOM Pays` and already gates the raw `C0`/`P1` ids behind
`InGameHud.ShowDebugIds`, both closed by an earlier, unrelated pass). What is **not** gated:
`MapDisplaySystem.AppendHover` unconditionally appended `"  HOVER " + hover` to the
player-facing metrics/info-bar line whenever the mouse hovered a province — redundant with
the already-clean, already-gate-free `InGameHud.HoverLabel` widget (a small floating tag that
already shows just the name, no "HOVER" prefix). This is exactly the class of diagnostic
leakage Success Condition 2 targets.

**Proof, before any fix** (original/unmodified `MapDisplaySystem.cs`, restored via
`git show HEAD:... > ...` for a clean baseline build, plus a new hover-simulation capture
step added to `UiStandaloneCaptureHarness.cs` — `MapViewport.SetHover(provinceId, name)` +
`MapDisplaySystem.RequestRefresh()`, a real, reproducible, fixed scenario, not a fabricated
string): standalone player built (`Ui002BuildPlayer.BuildFromCommandLine`,
`unity/game_unity/Logs/v004_build_before.log`/`v004_build_before2.log`, 0 `error CS`), run
with `--ui-capture-dir`. `Captures/v004_before/07_hover_debug_leak.png`'s top banner reads,
literally, **"...Armée 97750  Population 131603  Guerres 1  HOVER Île-de-France  VITESSE
x1"** — the raw `HOVER` token, visible in the default player banner, opened and read by eye.
`debug_leak_default_mode_count` measured on this unfixed build: not applicable (this counter
is defined for the *fixed* build's default mode — see below); the "before" capture's own
purpose is only to prove the defect was real, which it does.

**Fix**: `MapDisplaySystem.AppendHover` now returns the metrics line unchanged unless
`InGameHud.ShowDebugIds` is true (`MapDisplaySystem.cs`, ~9 lines changed, all inside the one
method). `UiStandaloneCaptureHarness.cs` gained a real, reachable toggle for
`ShowDebugIds` — a `--debug-ids` command-line flag (`HasFlag` helper, `ArgDebugIds` constant)
— replacing the two previously-hardcoded `InGameHud.ShowDebugIds = false;` lines. This is a
gate, not a deletion: the same static field `InGameHud.ShowDebugIds` already used for
`C#`/`P#`/`TICK` gating now also gates `HOVER`, and is reachable both ways from the command
line.

**Proof after the fix, default mode** (rebuilt, `unity/game_unity/Logs/v004_build_after.log`,
0 `error CS`; run with `--ui-capture-dir`, no `--debug-ids`):
`Captures/v004_after_default/07_hover_debug_leak.png`'s banner reads **"...Armée 101815
Population 131603  Guerres 1  VITESSE x1"** — no `HOVER`, no `TICK`, no raw `C0`. Opened and
read by eye. `debug_leak_default_mode_count = 0` (sample_size 3: HOVER, TICK, raw id, all
checked, none present).

**Proof after the fix, explicit debug mode** (same exe, re-run with `--debug-ids`):
`Captures/v004_after_debug/07_hover_debug_leak.png`'s banner reads **"...TICK 5  Trésor
209.1 ... ZOOM Pays C0  HOVER Île-de-France  VITESSE x1"** — all three reappear together.
Opened and read by eye. `debug_leak_explicit_debug_mode_count = 3` (>= 1 required; the gate
demonstrably toggles both ways, not a one-way removal).

Both declared `must_differ_from` pairs verified by SHA256
(`deliverables/evidence/gallery-sha256.txt`):
`v004_before/07_hover_debug_leak.png` (`329df25b...27728`) ≠
`v004_after_default/07_hover_debug_leak.png` (`78fe78eb...4f19`) ≠
`v004_after_debug/07_hover_debug_leak.png` (`b3d6b30c...b3c18`).

## Success Condition 3 — French decimals, no scientific notation

**Measured state: no reproducible defect found. `scientific_notation_before_count = 0`
(sample_size 2), not the `> 0` the brief's Required Counter expects.**

`TaxPolicy.DefaultProductionTaxRate = 0.00002f` — this is literally `2E-5`, the exact number
`REVUE-v1_054.md` cites ("Les valeurs fiscales apparaissent en notation scientifique
(2E-5)"). `05_tax_min.png` captures the tax panel at exactly this value
(`TaxPolicyLimits.MinProductionTaxRate = 0f` used as the floor, `DefaultProductionTaxRate`
as the live starting rate before any player action);
`06_tax_max.png` captures it at `MaxProductionTaxRate = DefaultProductionTaxRate * 10f =
0.0002f`. Both, in every capture set (before, after-default — `HudValueFormatter.cs` was
never touched by this brief, so "before"/"after" are identical here by construction), render
as `"Taux 0 % · plage 0 % – 0,02 %"` and `"Taux 0,02 % · plage 0 % – 0,02 %"` — comma
decimal, no exponent, opened and read by eye on both PNGs and the `editorial_probe` text
blocks. `HudValueFormatter.FormatNumber`'s `LooksScientific` check + comma substitution
already covers this path.

**No source change was made for Success Condition 3**, for the same reason as Success
Condition 1: nothing proven broken in a fresh capture from the exact historically-cited
reproduction scenario.

## Success Condition 4 — REVUE-v1_054.md P0s/P1s

**P0 #1 (synthetic bitmap capture instead of real UI Toolkit framebuffer) — confirmed still
closed.** Every capture this brief produced logs `source=standalone framebuffer` and
`composer=NONE` (`deliverables/evidence/ui_003_visual-after_default.log` etc.) — the real
player framebuffer, not `UiDaCaptureComposer`'s bitmap composite.

**P0 #2 (overlapping bitmap/UI-Toolkit panels) — confirmed still closed.** Opened
`01_world_neutral.png`, `02_country_selected.png`, `03_province_selected.png` by eye:
exactly one UI Toolkit panel at a time (`CountryPanel` XOR `ProvincePanel`, both hidden at
world level), no duplicate bitmap diagnostic panel anywhere in the map texture.
`p0_regression_check_count = 2` (both confirmed, neither silently regressed, neither
re-fixed — no source change was needed).

**P1 "dump technique" (= this brief's Success Conditions 1-3 + 6-to-10-indicator/French
labels)** — Success Conditions 1-3 are addressed above (2 already-closed, 1 genuinely closed
by this brief). The indicator-count/French-label item: `02_country_selected.png`'s
"Indicateurs" block shows Trésor/Dette/Taux d'intérêt/Revenu/Dépenses/Taux/LAWMOD/Revenu
fiscal/Armée/Guerres/État — all French, within the 6–10 range the brief cites — already
closed by an earlier pass (`ui_003`), confirmed present and not touched.

**P1 pause ambiguity — inspected first, confirmed already resolved.**
`p1_pause_ambiguity_addressed_flag = 0`. `Captures/v004_before/04_pause_active.png` and
`Captures/v004_after_default/04_pause_active.png` (identical here since nothing relevant was
touched) both show a distinct red **"EN PAUSE"** badge (`InGameHud.PaceStatusBadge`) —
different position, different color, different text from the action button, which reads
**"Lecture"** (`InGameHud.PauseButton`). The two notions `REVUE-v1_054.md` asked to be
separated already are. Opened and read by eye on both PNGs. No fix made; no before/after pair
declared for this item (nothing to pair — it was never broken in this port).

**No defect outside Success Conditions 1-4's named list was fixed.** `git status --porcelain
-- unity/` shows exactly: 2 modified source files (`MapDisplaySystem.cs`,
`UiStandaloneCaptureHarness.cs`, both scoped to this brief's fixes), 1 new Editor-only
tooling file + its Unity-generated `.meta`, regenerated `Captures/`/`Logs/` artifacts. The
map-label-orientation defect found during this work (see Success Condition 1) was
deliberately **not** touched, exactly per this rule.

## Success Condition 5 — final visual gallery

- `MapSnapshotExporter` EditMode political-map path: `Captures/v004_accent/
  01_world_country_labels.png` + `02_world_province_labels.png`, fresh run this session
  (`V004AccentCaptureRunner.Run`), reusing brief 003's proven mechanism
  (`BuildMapGeometry`/`RenderPoliticalPixels`/`WriteMapBufferPng`), not recycled captures.
- `v1_055` standalone-player framebuffer chain: **built and run successfully** —
  `Ui002BuildPlayer.BuildFromCommandLine` (same mechanism `ui_002`/`ui_003` established),
  `Captures/v004_after_default/{01_world_neutral,02_country_selected,03_province_selected,
  04_pause_active,05_tax_min,06_tax_max,07_hover_debug_leak}.png`. Acceptable Waivers row 1
  does not apply — the chain was not infeasible, it worked.
- All three of this brief's target fixes are visible in the gallery: the accent fold in
  `02_world_province_labels.png` (`ILE-DE-FRANCE`); the hidden HOVER token in
  `07_hover_debug_leak.png` (default mode); the French comma-decimal tax rate in
  `05_tax_min.png`/`06_tax_max.png`.
- Every gallery file's mtime postdates `brief.md`'s file-on-disk mtime
  (2026-07-31 20:18:44) — but **not** `brief.md`'s own internal `Authored:` text
  (2026-08-01T11:00:00), per the blocking finding at the top of this log.

## Success Condition 6 — reference suite + legacy files

Fresh full `-runTests -nographics` EditMode run after this brief's own changes
(`unity/game_unity/Logs/v004_tests.log`/`v004_test-results.xml`, started 19:22:19Z, ended
19:47:31Z): **`total=274 passed=265 failed=8 skipped=1`** — byte-for-byte identical to brief
003's own hand-off run. The 8 failing `fullname`s
(`deliverables/evidence/failed-test-cases.txt`) are byte-for-byte identical to brief 003's own
attributed set: `V1008MeasurementTests`, `V1014MeasurementTests`, `V1MapSnapshotTests`,
`V1bMapMaskTests`, `V1cMapReadableTests`, `V1dChronicleTests`, `V1eMapLayerTests` (7 legacy,
untouched) + `V1095GpuMapTests` (1, invocation-mismatch). Re-ran the V1095 no-nographics
diagnostic (`V1095BatchRunner.Run`, no `-nographics`, same invocation brief 003 used):
all 6 verdicts VERT, 99.6% CPU/GPU terre/mer agreement — same figure brief 003 reported.
`reference_suite_total_count = 266` (274 − 7 legacy − 1 skipped),
`reference_suite_passed_count = 266` (265 + V1095), cited by pointer to brief 003's own
reconstructed definition (`unity/README.md`), not re-derived.

`legacy_attributed_test_files_unchanged_count = 7`: `git status --porcelain` scoped to each
of the 7 files returns empty (never opened for editing this brief); fresh SHA256 recorded
(`deliverables/evidence/legacy-attributed-sha256-after.txt`).

## Success Condition 7 — artistic verdict

The literal status string for this brief's final gallery is: **A_REVOIR_HUMAINEMENT**.
This Générateur does not, and did not anywhere in this log or `manifest.json`, declare
`ADOPTÉ` as its own verdict on the work's aesthetic quality — that decision belongs to the
project owner, mirroring `task_v1_056.json`'s own constraint
("OBLIGATOIRE — verdict artistique final A_REVOIR_HUMAINEMENT, sans auto-ADOPTÉ"), quoted
here only as a citation of that file's own text, not as this brief's own claim of acceptance.

## Perimetre exclu par brief.md — auto-controle

- No new screen/panel/view: confirmed — the hover-capture step is an *additional capture
  step* in an existing test/diagnostic harness (`UiStandaloneCaptureHarness.cs`), not a new
  gameplay screen; no new UXML, no new `Resources/UI/` asset.
- No external asset/package added: confirmed, `git status --porcelain` shows no new asset
  files under `Resources/`.
- No test weakened/deleted/skipped: confirmed — zero files under `Assets/Tests/` were opened
  for editing (`git status --porcelain -- unity/game_unity/Assets/Tests/` is empty).
- Zero simulation-logic lines changed: confirmed — both edited files are under
  `Assets/Scripts/Presentation/`; `git diff --stat` shows 2 files, 66 insertions / 3
  deletions, both entirely inside `MapDisplaySystem.AppendHover` and
  `UiStandaloneCaptureHarness`'s capture-sequence/arg-parsing code.
- No parity/determinism anchor rebased or cited by value: confirmed — no hex-literal pattern
  appears in this log or `manifest.json`; the V1095 99.6%/61.2% figures are cited as
  percentages already published in brief 003's own manifest and VictoriaProject's own
  HANDOFF.md, not a hex fingerprint.
- No `.meta` hand-written: `V004AccentCaptureRunner.cs.meta` was Unity-generated on import
  (standard `fileFormatVersion: 2` + `guid:` content, same pattern as every other `.meta` in
  the project) — I never wrote it directly.
- No `git commit`: confirmed, zero git-write invocations this session
  (`generator_git_commits_count` counter documents the literal query and its 3 pre-existing,
  owner-authored hits, none mine).

## Self-check

```
py harness/verdict_audit.py harness/queue/briefs/004-polish-visuel
```

Run at hand-off; result recorded verbatim below (not narrated) —
see the tool output accompanying this session's final response. Expected, per the blocking
finding above: `FAIL` on `mtime_after_brief` and `rubric_predates_deliverables` (brief.md's
own `Authored:` text is ~13h in the future of the actual system clock this entire session ran
under); all other checks expected `PASS` given the evidence above.
