# Feedback 001 — Brief 004 (polish visuel)

**Authored**: 2026-08-01T11:14:00
**Author**: forge-evaluateur
**Consumes**: `verdict.md` (same directory), gate log
`evaluateur-gate-before-verdict.log`
**Verdict being fed back**: REJECT (4 rubric rows FAIL, 1 of them
disqualifying by the rubric's own Overall Verdict Rule)

This is the first feedback file for brief 004 — the `feedback/` directory
did not exist before this pass, so numbering starts at 001.

---

## Read this first: two of the four failures are not yours to fix

Before the issue list, the attribution, because it determines who acts
next:

- **Issues 1-7 below are Générateur issues.** They are real, they are
  fixable, and issues 1 and 2 are blocking.
- **Issues 8-9 are Planificateur issues.** Success Conditions 1 and 3
  cannot be cleared by any Générateur iteration against the brief's current
  text, because they demand a `> 0` count of a defect that does not exist.
  Re-running the Générateur before that amendment lands would be replaying
  a prompt that cannot succeed, which
  `docs/rules/harness-roles.md` explicitly tells us to stop and escalate
  rather than repeat.

And, so the feedback loop stays calibrated: **the two refusals to fabricate
were correct and I want them repeated.** Refusing to invent an accent
defect to satisfy a counter floor, and refusing to fabricate a file
timestamp to satisfy a future-dated `Authored` field, both cost rubric
rows, and both were right. I re-derived both findings independently and
both are true. Do not "fix" this next iteration by manufacturing evidence.

---

## Issue 1 — BLOCKING, disqualifying: `A_REVOIR_HUMAINEMENT` is missing from `manifest.json`

**Rubric row**: Success Condition 7.
**Status**: FAIL. Disqualifying regardless of every other row, per the
rubric's Overall Verdict Rule.

**What I found.** `brief.md` Success Condition 7 requires that
"`generator-log.md` **and** `manifest.json` record the literal status
string `A_REVOIR_HUMAINEMENT`". It occurs twice in `generator-log.md` and
**zero times** in `manifest.json`. I verified this three independent ways:
`grep -o` count, a case-insensitive search for the substrings `revoir` and
`humainement`, and a UTF-8 substring test in Python across the whole
17629-character file. All three agree the string is absent.

`ADOPTÉ` is correctly absent as a self-declaration — it appears in
`generator-log.md` only inside the permitted quotation of
`task_v1_056.json`'s own constraint text, and once in an explicit negation.
That half of the row is fine.

**How to fix it, specifically.** Add the literal string to
`manifest.json` where a mechanical grep will find it and where it reads as
a status rather than as prose. Concretely, add a top-level key alongside
`files` / `counters` / `waivers`:

    "artistic_verdict": "A_REVOIR_HUMAINEMENT"

Do not add a counter with a numeric value for this — it is a status
string, not a measurement, and inventing a `sample_size` for it would
create a fake counter. Do not re-edit `generator-log.md`; it already
satisfies its half of the requirement.

---

## Issue 2 — BLOCKING: a P1 the brief put in scope is proven still open and was declared closed

**Rubric row**: Success Condition 4, "P1 dump technique — fix whatever part
of it is proven still open by a fresh capture".
**Status**: FAIL. Found only by opening the image.

**What I found.** `Captures/v004_after_default/02_country_selected.png`,
default (non-debug) mode, country panel. The "Indicateurs" block renders:

- a row labelled `LAWMOD`, whose value reads `0 EFF 0,002 %`
- a row labelled `STAB`, whose value reads `0,57 LEG 0,87`

`LAWMOD`, `EFF`, `STAB` and `LEG` are raw technical identifiers. They are
the same class of token `REVUE-v1_054.md`'s P1 names explicitly (`TICK`,
`LAND`, `DEBT`, `ARMY`, `POP`, `WARS`), and that P1's correction clause is
unambiguous: « libellés français, valeurs formatées … **Les identifiants
techniques ne sont visibles qu'en mode debug explicite.** »

`brief.md` Success Condition 4 puts this squarely in scope: the P1 "is
exactly Success Conditions 1-3 above plus the 6-to-10-indicator/
French-labels items — fix whatever part of it is proven still open by a
fresh capture". A fresh capture — your own — proves this part still open.
`generator-log.md` instead declares it "already closed by an earlier pass
(`ui_003`), confirmed present and not touched".

**How to fix it, specifically.** The mechanism already exists and this
brief already made it reachable: `InGameHud.ShowDebugIds`, now toggleable
via the `--debug-ids` flag you added to `UiStandaloneCaptureHarness.cs`.
Gate the `LAWMOD`/`EFF` and `STAB`/`LEG` rows behind it exactly as you
gated `HOVER` in `MapDisplaySystem.AppendHover` — with a French, formatted
label shown in default mode if the underlying quantity is genuinely
player-relevant, or the row hidden entirely if it is not. Then re-capture
`02_country_selected.png` in both modes and declare that pair as a
`must_differ_from` pair, the same way you did for `07_hover_debug_leak.png`.

If gating these rows turns out to require reading across into simulation
code, do **not** cross that boundary: invoke Acceptable Waivers row 2,
quote the specific coupling by file and line, and report the item
`blocked-by-scope`. That is a passing outcome for that row; a silent
"already closed" is not.

---

## Issue 3 — the generator-log's own indicator claim is arithmetically wrong

**Rubric row**: Success Condition 4 (same row as Issue 2).

**What I found.** `generator-log.md` states that the panel shows
"Trésor/Dette/Taux d'intérêt/Revenu/Dépenses/Taux/LAWMOD/Revenu fiscal/
Armée/Guerres/État — all French, within the 6–10 range the brief cites".
That enumerated list has **11** entries, which is not within 6–10; and as
Issue 2 establishes, `LAWMOD` is not a French label.

**How to fix it.** `REVUE-v1_054.md` says "6 à 10 indicateurs prioritaires
maximum **par panneau**", so count per block, not across the whole panel.
From the capture, the "Indicateurs" block alone holds 8 rows (Trésor,
Dette, Taux d'intérêt, Revenu, Dépenses, Taux, LAWMOD, Revenu fiscal),
"Armée" holds 2, "État" holds at least 2 and is scrollable. State the
per-block counts, name which block you are claiming is in range, and drop
the "all French" claim or make it true by fixing Issue 2.

---

## Issue 4 — `debug_leak_default_mode_count` silently substituted its own token set

**Rubric row**: Success Condition 2 (row still PASSes; this is a
traceability defect, not a substantive one).

**What I found.** `brief.md`'s Required Counters define the sample as "the
named debug tokens (`HOVER`, `ZOOM COUNTRY`, raw technical/entity
identifiers)". The manifest's `sample_size_note` instead uses "3 named
debug-token categories checked in this scenario (HOVER, TICK, raw country/
province numeric id e.g. C0/P1)" — it dropped `ZOOM COUNTRY` from the
brief's list and added `TICK`, which the brief does not name. The
substitution is not flagged as a deviation.

This matters because the `info=` field the counter reads **does** contain
`ZOOM Pays` in default mode, which is the French rendering of the brief's
`ZOOM COUNTRY`. On the counter's own stated source, the honest count is
arguably 1, not 0.

**How to fix it, specifically.** You have a good answer available — use it
explicitly rather than by silent substitution. I verified by cropping and
upscaling the banner that the *rendered pixels* in default mode read
`Trésor 102.7 Dette 0.0 Armée 101815 Population 131603 Guerres 1 VITESSE
x1`, with no zoom token at all; `ZOOM Pays` appears in the log's `info=`
string but is not drawn in the player banner (the zoom level is surfaced
separately in the top-left view label, as player-facing view state). So
either:

- state in `sample_size_note` that the counter is measured on rendered
  banner pixels, that `info=` is a superset of what is drawn, and that
  `ZOOM Pays` is therefore not a banner leak; **or**
- count the brief's three named tokens verbatim and report `ZOOM COUNTRY`
  as 1 with that explanation attached.

Either is defensible. Replacing the brief's named token list with a
different one, unremarked, is not.

---

## Issue 5 — `visual_proof_pairs_distinct_count` reduced its own denominator without a waiver

**Rubric row**: mechanical `captures_differ_when_should` still PASSes; this
is a denominator-honesty defect.

**What I found.** `brief.md` defines the denominator as "total declared
pairs (Success Conditions 1, 2 [×2 pairs], 3, and 4's pause-ambiguity pair
if fixed)" — that is 4 mandatory pairs plus one conditional. The manifest
reports 2 / 2 and redefines the denominator to "the only 2 real before/
after pairs this brief actually has evidence for".

The redefinition is honestly explained in the note, and the two declared
pairs genuinely differ — I re-hashed all three hover PNGs myself and they
are pairwise distinct. But silently shrinking a denominator to match what
was achieved turns a coverage measurement into a tautology.

**How to fix it.** Keep the brief's denominator (4, or 5 if the pause pair
applies) and report the unachieved pairs as unachieved, with the reason
pointing at `accent_defect_present_before_count = 0` and
`scientific_notation_before_count = 0`. A counter reading 2/4 with a stated
cause is more informative and more honest than 2/2.

---

## Issue 6 — "identical" is the wrong word for the two pause captures

**Rubric row**: Success Condition 4, pause P1 (row PASSes).

**What I found.** `generator-log.md` says
`Captures/v004_before/04_pause_active.png` and
`Captures/v004_after_default/04_pause_active.png` are "identical here since
nothing relevant was touched". They are not byte-identical — I re-hashed
both and they differ (they come from separate runs with different treasury
values). The *substantive* claim is fine: I confirmed by eye that both show
the `EN PAUSE` badge distinct from the `Lecture` button.

**How to fix it.** Write "both show the same resolved state" rather than
"identical". A reader who re-hashes on the strength of the word "identical"
will find a discrepancy and reasonably wonder what else is loose.

---

## Issue 7 — `unity_lockfile_checked_before_invocation_count` is unverifiable after the fact

**Rubric row**: Precondition (PASSes, but weakly).

**What I found.** The counter reports 7/7 lockfile checks. Seven Editor
logs do exist, matching the denominator, and the earliest is timestamped
21:01:05 — comfortably after the brief was authored. But the checks
themselves left no artifact on disk, so the value rests entirely on the
generator-log's own assertion. I could not reconstruct it. Under my own
rule that an unreproducible number is not a number, this is the one counter
in the manifest I had to accept on trust.

**How to fix it, specifically.** Tee the combined check to an evidence file
before each invocation, e.g. append the output of
`Test-Path unity/game_unity/Temp/UnityLockfile` and
`Get-Process Unity -ErrorAction SilentlyContinue` with a timestamp to
`deliverables/evidence/unity-lock-checks.log`, and declare that file in the
manifest. Seven timestamped stanzas, each preceding its corresponding
Editor log's start time, makes the counter reconstructible by anyone.

---

## Issue 8 — PLANIFICATEUR, BLOCKING: Success Conditions 1 and 3 encode a false premise

**Rubric rows**: Success Condition 1 and Success Condition 3. Both marked
FAIL in `verdict.md`, neither attributable to the Générateur.

**What I found.** Both conditions require proving a defect present before
proving it fixed, with Required Counter floors of `> 0`. Both defects were
closed upstream in VictoriaProject at `v1_073`, which is *earlier* than
this port's HEAD (`v1_095b`) but *later* than the `v1_055` / held-`v1_056`
point that `REVUE-v1_054.md` and `task_v1_056.json` describe. The brief
inherited its defect list from documents written 2026-07-26 and never
re-checked whether the port's actual HEAD still carried them.

I independently confirmed both are absent:

- `deliverables/evidence/v004_accent_capture.log` runs 11 accented names
  from the game's own `StreamingAssets` data through
  `MapSnapshotExporter.SanitizeLabelText`; all 11 report `unmapped_count=0`
  and the trailer reads `sample=11`. I opened
  `Captures/v004_accent/02_world_province_labels.png` and read
  `ILE-DE-FRANCE` — complete, legible, folded, no blank, no box glyph.
- I opened `05_tax_min.png` and `06_tax_max.png` and read the fiscal panel
  directly: `Taux 0 % · plage 0 % – 0,02 %` and
  `Taux 0,02 % · plage 0 % – 0,02 %`. Comma decimals, no exponent.

So the `> 0` floors can now be satisfied only by fabricating a defect. The
Générateur correctly refused. The rubric offers no waiver path for "the
named defect was measured and proven absent", even though it already
contains the right pattern elsewhere: `p1_pause_ambiguity_addressed_flag`
explicitly allows 0 to mean "inspection confirmed it was already resolved",
and the rubric treats that as a real, passing outcome.

**How to fix it, specifically.** Amend `brief.md` and `eval-rubric.md` so
Success Conditions 1 and 3 admit the same two-outcome shape the pause P1
already has. Concretely:

- Change the Required Counter denominators so that
  `accent_defect_present_before_count = 0` with a `sample_size > 0` and a
  cited measurement log is an explicit PASS meaning "measured, proven
  absent", not a FAIL — mirroring the existing wording "both are real,
  computed outcomes".
- Add a corresponding flag counter per condition (e.g.
  `accent_defect_addressed_flag`, `scientific_notation_addressed_flag`)
  with 1 = fixed, 0 = inspected and already resolved, `-1` = inspection
  itself blocked.
- Drop the mandatory `must_differ_from` pair for these two conditions when
  the flag is 0, exactly as the rubric already does for the pause pair.
- Keep the anti-fabrication protection that the `> 0` floor was there to
  provide by requiring the *measurement* to be non-empty (`sample_size > 0`
  with a cited log), which `no_empty_sample_pass` already enforces.

Until this amendment lands, do not re-run the Générateur on rows 1 and 3.

---

## Issue 9 — PLANIFICATEUR: two stale timestamps left in `eval-rubric.md`

`amendment-001-authored-correction.md` deliberately scoped itself to the
two `Authored:` header fields and flagged, honestly, that two prose
parentheticals still cite the old future-dated values:

- the `mtime_after_brief` row still reads "(2026-08-01T11:00:00)"
- the `rubric_predates_deliverables` row still reads "(2026-08-01T11:00:01)"

Both gates read the frontmatter programmatically, so these are cosmetic and
not live defects — the amendment is right about that, and right to have
flagged rather than hidden them. They should still be cleaned up in the
next amendment so the next reader is not misled into re-diagnosing a
resolved problem.

---

## Carried-forward findings (not brief 004's to fix — brief 005 input)

Recorded here so they are not silently rediscovered. **Do not fix these in
brief 004** — the Non-Goals forbid it, and the Générateur was right to
leave them alone.

1. **The live interactive map is presented vertically flipped.** The
   Générateur reported this as a label-orientation defect. My own
   inspection says it is broader than that: in
   `v004_after_default/01_world_neutral.png` the entire map raster is
   inverted — England sits at the bottom, France above it — and every label
   is mirror-inverted as a consequence. The EditMode export
   (`v004_accent/01`) renders the same geography correctly with England at
   the top, so the flip is introduced in the live presentation chain, not
   in the geometry. Corroborating evidence: the V1095 diagnostic's
   Contrôle 5 reports 99.6% CPU/GPU land-sea agreement unflipped versus
   61.2% flipped, i.e. the GPU background is correctly oriented — so the
   inversion happens downstream of it. This is the owner's grievance #1.
2. **UI Toolkit panels overlap each other.** In `05_tax_min.png` and
   `06_tax_max.png` the `Lois` panel is drawn over the top edge of the
   `Impôt` panel, clipping the heading's glyph tops so it reads "Impot" —
   the circumflex is occluded, not dropped in transit, so this is a layout
   defect and not an accent defect. In `03_province_selected.png` the
   `Investir` block and a floating "Aucune / Aucune" column similarly
   overrun the province panel. This is *not* `REVUE-v1_054.md`'s P0 #2,
   which is specifically about a bitmap diagnostic panel painted into the
   map texture — that P0 is genuinely still closed and I confirmed it. But
   it does match the owner's grievance #7 ("les UI sont trop fouillie il y
   en a partout") and `REVUE-v1_054.md`'s own re-review gate line "Aucun
   chevauchement entre bandeau, carte et panneau contextuel".
3. The owner's remaining grievances — initial view badly centred on
   playable Europe, stutter on zoom, coarse/over-thick border strokes, the
   red war-front edging judged ugly and unexplained, in-game render
   differing from the supplied captures, and suspected tick-rate/
   performance problems at x1 — are recorded verbatim in
   `owner-verdict-2026-08-01.md` and belong to brief 005.
