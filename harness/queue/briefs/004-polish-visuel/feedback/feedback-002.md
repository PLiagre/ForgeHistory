# Feedback 002 — Brief 004 (polish visuel)

**Authored**: 2026-08-01T12:06:00
**Author**: forge-evaluateur
**Consumes**: `verdict.md` (same directory, iteration 2 section), gate log
`evaluateur-gate-iter2-before-verdict.log`
**Verdict being fed back**: REJECT (1 rubric row FAIL, non-disqualifying)

---

## Read this first: you closed both blocking issues, and I mean that

Before the issue list, the calibration, because the shape of this feedback
is very different from `feedback-001.md`:

- **Issue 1 of `feedback-001.md` (the missing `A_REVOIR_HUMAINEMENT` in
  `manifest.json`) is closed.** Verified three ways, including reading every
  `ADOPTE`/`ADOPTÉ` occurrence with context. You held
  `A_REVOIR_HUMAINEMENT` even after a negative human verdict arrived — that
  is the harder half of the rule and you got it right.
- **Issue 2 (the `LAWMOD`/`EFF`/`STAB`/`LEG` leak) is closed.** I opened
  both `02_country_selected.png` files and extracted both editorial blocks
  myself. The row is gone in default, present in French in debug, never a
  raw token in either. It is a gate, not a deletion.
- **You found a second emission point yourself, by opening your own fresh
  capture mid-fix, and fixed it before hand-off.** That is the exact
  behaviour iteration 1 lacked. Keep doing this.
- **Issues 3–7 are all closed by method changes, not wording changes.**
- **You were right to leave the `Investir` block alone** — see Issue 3
  below, which is about your *reasoning*, not your decision.

One row still fails, and it is not one you were asked to work on. Details
below.

---

## Issue 1 — BLOCKING: the player banner's decimal separator is not French

**Rubric row**: Success Condition 3.
**Status**: FAIL. Found only by looking at the captures.
**Attribution**: not your fault. `feedback-001.md` Issue 8 told you
explicitly not to work Success Condition 3 this iteration, and my own
iteration-1 review transcribed the defective string verbatim without
noticing it. Recorded so it is not read as misconduct.

**What I found.** Every capture in this brief — including your freshest
iteration-2 ones — renders the top player banner with a dot decimal
separator, while the country panel directly below renders the same quantity
with a comma:

- `after2_default/04_pause_active.png`: banner `Trésor -269.8  Dette 0.0`,
  panel `Trésor 4,6`
- `after2_default/01_world_neutral.png`: banner `Trésor 110.2  Dette 0.0`
- `after2_default/05_tax_min.png`: banner `Trésor -265.9`, panel
  `Trésor -344,4`

It is also in your own evidence, in plain text, in the field two of your
counters read: `ui_003_visual-after2_default.log`,
`info='AN 1400  Trésor -10.1  Dette 0.0  …'`.

**Why this is Success Condition 3 and not a brief-005 aesthetic item.**
`brief.md` Success Condition 3 reads: "Wherever the player banner currently
renders a number in scientific notation (e.g. `2E-5`) **or a non-French
decimal separator**, reformat it to a French-locale decimal (comma
separator, no exponent)." The Required Counters table defines the counter's
own pattern the same way: "count of numeric strings matching scientific
notation … **or non-French decimal separators**". So the counter was
defined to catch this; it was simply measured over the fiscal panel, where
the defect does not live, and never over the player banner, which is the
surface the condition names. `scientific_notation_before_count = 0` is true
of its sample and false of its condition.

**Source, so you do not have to hunt for it.**
`MapDisplaySystem.FormatPanelLine` — the same function whose `HOVER`/`TICK`
tokens you gated in iteration 1 — builds the banner. Its own doc comment
claims « Bandeau joueur : date + métriques FR ». It formats through
`WorldMetrics.Fmt1`, which is `v.ToString("0.0", CultureInfo.InvariantCulture)`.

**How to fix it, specifically.**

1. Change the **call sites** in `FormatPanelLine` (the `Trésor` and `Dette`
   appends), not `Fmt1`/`Fmt0` themselves. `Fmt1` is also used to build
   diagnostic and parity log lines (`totalDebt=`, `worldArmyStr=`, …) which
   must stay `InvariantCulture` — changing it would alter log text that
   other things read, which is not what this brief authorizes.
   `HudValueFormatter` already owns the French formatting used by the panels;
   reuse it rather than adding a second convention.
2. Decide and state whether `Population 131532` and `Armée 106380` should
   also carry a French thousands separator. `brief.md` asks only for the
   decimal separator, so leaving them as-is is a defensible, in-scope
   choice — but say which you chose and why, rather than leaving it
   ambiguous.
3. Re-measure `scientific_notation_before_count` / `_after_count` over
   **both** surfaces, with the banner fields named in `sample_size_note`
   (`Trésor`, `Dette`) alongside the two fiscal-panel fields. This is
   Outcome A, not Outcome B: the defect is real and present, so declare a
   genuine before/after `must_differ_from` pair on the same scenario.
4. Do not touch anything else in Success Condition 3. The fiscal panel is
   already correct and I re-verified it this pass.

---

## Issue 2 — the editorial probe's scope is narrower than it reads

**Rubric row**: none directly. This is why a defect survived two passes, so
it matters more than its rubric weight.

**What I found.** `editorial_forbidden=PASS tag=02_country_selected` and
`…tag=03_province_selected` both appear in your iteration-2 logs, and both
are true — but the `editorial_text_begin/end` block they cover contains only
the context panel's own text. I extracted both blocks and compared them
against what the PNGs actually draw:

- The `tag=02` block ends at `Impôt +`. It does **not** include the `Lois`
  panel, so `En vigueur : …` and the enact button are never checked.
- The `tag=03` block ends at `Impôt +` too. It does **not** include the
  `Investir` block, so `DEV T5 P4 M3  score=4  coût T/P/M 250/200/150` is
  never checked.

You correctly diagnosed one level of this yourself — that
`ForbiddenUserTokens` lacked the four tokens, so `AssertEditorial` passed
despite the leak, and you cited hard-won rule 6 for it. The same rule
applies one level up: the token list is now right, but the *collection
scope* still makes the check narrower than a reader of
`editorial_forbidden=PASS` would assume.

**How to fix it.** Either widen `CollectVisibleText` to the panels actually
drawn in the frame, or emit the scope in the log line itself (e.g.
`editorial_forbidden=PASS scope=CountryPanel`) so nobody — including me —
reads it as whole-screen coverage.

---

## Issue 3 — `Promulguer land_tax`: a raw identifier in the panel you fixed

**Rubric row**: Success Condition 4's P1. Does not change the row's verdict
this iteration.

**What I found.** In default mode, in the `Lois` panel you edited, the enact
button reads `Promulguer land_tax` — a raw snake_case law identifier, in
both default and debug mode, in every iteration-2 capture. Your log says of
the adjacent list: "I did not touch the adjacent `lawList` (raw
`LawId.ToString()` if any law were enacted) — no fresh capture in this
session proves that part open". A raw `LawId` **is** rendered in this
session's fresh captures — one line below the `lawmod=` suffix you gated,
in the same panel, in the same frame.

**How to fix it.** Not necessarily by fixing it — report it accurately. The
honest sentence is "a raw law id is rendered in the enact button; I did not
fix it because X", not "no fresh capture proves that part open". Getting the
finding's *statement* right is what lets the next brief scope it.

---

## Issue 4 — the `Investir` exclusion was right; the reasoning was not

**Rubric row**: Success Condition 4's P1. **Your decision stands. Do not go
fix it now.**

**What I found.** You excluded the province `Investir` block's
`DEV T5 P4 M3 … coût T/P/M` dump on the grounds that it "was **not** named by
`feedback-001.md`" and "was **not** named by this iteration's own task
scope". Neither of those can narrow a Success Condition — only `brief.md`
defines the work (`CLAUDE.md`, Single Source of Instruction). I checked the
real `docs/ui/REVUE-v1_054.md` on disk rather than the brief's paraphrase,
and its P1 bullet list explicitly includes « Les blocs pays/province listent
trop de lignes brutes sans sélection ni rythme » and « Le panneau inférieur
répète encore des séparateurs ASCII et des identifiants ». So the province
block **is** inside "what `REVUE-v1_054.md` actually lists", which is
Success Condition 4's own bounding criterion.

**Why your decision is still correct.** `feedback-001.md`'s carried-forward
section named that exact block and said "**Do not fix these in brief 004**".
You complied with a written instruction from me. Reversing it and then
grading you against the reversal would be exactly the anti-pattern I exist
to avoid. The block is brief-005 input.

**How to fix the reasoning, for next time.** When declaring something out of
scope, cite `brief.md` and the source document the brief bounds itself to —
here, REVUE's own bullet list — and, if it genuinely is in scope but you are
not fixing it, invoke Acceptable Waivers row 2 (`blocked-by-scope`, coupling
quoted) or say plainly "in scope, deliberately deferred on the Évaluateur's
written instruction, see feedback-NNN". A justification that rests on a
feedback file or a task-scope message will not survive the next reader.

---

## Issue 5 — `unity_lockfile_checked_before_invocation_count` blends evidenced and asserted

**Rubric row**: Precondition (PASSes, improving).

**What I found.** The new `deliverables/evidence/unity-lock-checks.log` is a
real improvement and I could actually use it: its `v004b_tests` stanza
precedes the test XML's own `start-time` by seconds, which is a genuine,
reconstructible correspondence — exactly what `feedback-001.md` Issue 7
asked for. But the counter's *value* sums this iteration's evidenced checks
with the previous iteration's unevidenced ones. Read alone, the value looks
fully evidenced. Its note discloses the split honestly; the number does not.

**How to fix it.** Either scope the counter to this iteration's own
invocations, or split it into two counters (evidenced / asserted). A number
whose reconstructibility varies across its own sample should not be
presented as one number.

---

## Issue 6 — toggle pairs should hold everything but the toggle constant

**Rubric row**: Success Condition 2 / Success Condition 4's P1 pair (both
PASS; this is a methodology note).

**What I found.** The default and debug `02_country_selected.png` captures
select **different countries** (`Bourgogne` and `Champagne`), and so do the
before/after pair. The pair hashes therefore differ partly for a reason that
has nothing to do with the gate. The substantive proof survived — the
`LAWMOD`/`EFF` row's presence does not depend on which controlled country is
selected, and I confirmed that in the editorial text of both — but a hash
difference alone proves nothing here, and a reader who trusted only the
`captures_differ_when_should` result would be trusting a coincidence.

Separately: `after_default/02` ↔ `after2_default/02` is declared twice, once
in each direction, which inflates the gate's declared-pair list without
adding a pair.

**How to fix it.** Pin the selected entity in the capture scenario so the
only variable between the two frames is `--debug-ids`. Declare each pair
once.

---

## Issue 7 — PLANIFICATEUR: the counter samples a different surface than its condition

**Rubric row**: Success Condition 3.

`brief.md`'s Required Counters row for `scientific_notation_before_count`
defines the pattern as "scientific notation … or non-French decimal
separators" but defines the sample source as "the known fiscal-panel
reproduction scenario". The Success Condition itself is about the player
banner. A Générateur following the counter row literally will measure the
fiscal panel, find `0`, and never look at the banner — which is exactly what
happened, twice, without anyone lying.

`amendment-002-absent-defect-waiver.md` inherited the same narrowing into
the amended rubric row 18's Outcome B condition (a), which anchors the
sample to "`REVUE-v1_054.md`'s fiscal-panel case".

**How to fix it.** Amend the counter's sample source to name the surface the
Success Condition names — the player banner — with the fiscal panel as one
cited case within it, not as the whole sample. This is the same class of
defect `amendment-002` already fixed once: a counter whose shape cannot
express the condition's real outcome.

---

## Carried-forward findings (brief 005 input — do NOT fix in brief 004)

Unchanged from `feedback-001.md`, plus this pass's additions:

1. The live interactive map is still presented vertically flipped, labels
   mirror-inverted, in every iteration-2 capture. Owner grievance #1.
2. UI Toolkit panels still overlap: the `Lois` panel clips the `Impôt`
   heading's circumflex; the province panel's `Investir` block and the
   floating `Aucune / Aucune` column still overrun. Owner grievance #7.
3. **New this pass**: the `Investir` block's raw `DEV T5 P4 M3  score=4
   coût T/P/M 250/200/150` dump (`DevelopmentHudSnapshot.cs`,
   `InGameHud.cs`), the `Promulguer land_tax` button label, the `Sat 0,798`
   abbreviation in the bottom panel, and an `ATK vs BUR CB  Conquest SCR=0.0`
   war row seen in `after2_debug/06_tax_max.png` whose default-mode
   behaviour no capture in this brief settles (the war state differed
   between the two runs, so it is genuinely unknown, not assumed absent).
4. The owner's remaining grievances are recorded verbatim in
   `owner-verdict-2026-08-01.md` and belong to brief 005.
