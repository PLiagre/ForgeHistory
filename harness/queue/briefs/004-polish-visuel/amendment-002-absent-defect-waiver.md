# Amendment 002 — waiver for a named defect proven absent (Success Conditions 1 and 3)

**Date of this amendment**: 2026-08-01
**Author**: forge-planificateur
**Responds to**: `verdict.md` (Évaluateur, REJECT), `feedback/feedback-001.md`
Issue 8 (Planificateur, blocking) and Issue 9 (Planificateur, non-blocking)

## What was wrong

`eval-rubric.md`'s rows for Success Conditions 1 (accent transliteration)
and 3 (French decimal formatting) encoded a single-outcome shape: the
Required Counter floor was `*_before_count > 0`, treated as a hard FAIL
whenever the count read 0. That shape assumes the named defect is still
present in the port. The Évaluateur's independent reconstruction
(`verdict.md`, "Independent Reconstruction of Every Counter" and rows 1/3
of "Per-Rubric-Line Verdict") confirms both named defects were already
closed upstream, before this port's HEAD, and are genuinely absent:

- 11/11 accented names fold correctly (`ILE-DE-FRANCE` renders complete);
  `accent_defect_present_before_count = 0` over a real `sample = 11`.
- The fiscal panel renders `0 %` / `0,02 %` — French comma-decimal, no
  exponent; `scientific_notation_before_count = 0` over a real sample of 2
  cited fields.

Under the old rubric text, no honest Générateur pass could ever clear
these two rows: the only way to satisfy `> 0` is to fabricate a "before"
defect that does not exist. The Générateur refused to fabricate one (the
correct behaviour, confirmed correct by the Évaluateur); the rubric, not
the work, was the defect. This mirrors a pattern the rubric already
handles correctly elsewhere — `p1_pause_ambiguity_addressed_flag` already
allows `0` to mean "inspected, confirmed already resolved" as a real
passing outcome, not a FAIL. Success Conditions 1 and 3 lacked the
equivalent second outcome.

## The rule this amendment adds

For Success Conditions 1 and 3 only: a `*_before_count == 0` result is now
an explicit PASS ("Outcome B — defect absent"), and not a hard FAIL,
**if and only if**:

1. an honest, documented investigation was actually carried out — a real
   sample (`sample_size >= N`, `N` real and stated, not fabricated or
   assumed) drawn from an actually-run, cited reproduction scenario that
   names the specific historical reproduction path invoked (e.g.
   `MapSnapshotExporter.SanitizeLabelText` over a named data set, or the
   exact fiscal-panel scenario `REVUE-v1_054.md` cites);
2. the investigation is documented in the Générateur's log honestly — what
   was run, on what data, with what result — not merely asserted; and
3. no fictitious/synthetic "before" defect was manufactured to force a
   nonzero count anywhere in the process. Manufacturing one is itself a
   FAIL of the row, and a worse one than reporting Outcome B honestly.

This does **not** weaken the existing empty-sample protection
(`no_empty_sample_pass`, restated unchanged in "Plateau / Waiver Notes"):
`sample_size == 0` — no scenario run at all — is still a hard FAIL under
both outcomes. Outcome B requires a real, non-empty sample that happens to
find zero defects; it does not permit an empty or unrun sample to pass by
omission.

## Lines changed in `eval-rubric.md`

Only the two named rows (Success Conditions 1 and 3) and the two stale
timestamp parentheticals flagged by `amendment-001-authored-correction.md`
and by feedback Issue 9 were touched. Nothing else in `eval-rubric.md` was
edited — the Plateau/Waiver Notes section, the Session-Cost Calibration,
and the Overall Verdict Rule are unchanged from their pre-amendment text.

### Success Condition 1 (row 15)

**Before:**

    | 1 | Accent transliteration proven present, then proven fixed, in a real reproducible scenario | Mechanical + Manual | `accent_defect_present_before_count` > 0 and `accent_defect_present_after_count` == 0 (Required Counters); mechanical gate: `captures_differ_when_should` on the declared before/after pair; **manual, non-waivable (hard-won rule 11): Évaluateur opens both images and reads the label itself** — confirms the letter is folded (`ILE-DE-FRANCE`), not merely absent, blank, or replaced by a box glyph |

**After:**

    | 1 | Accent transliteration correct: fixed if the defect is proven present, or proven absent by honest measurement if it is not (amended by `amendment-002-absent-defect-waiver.md`) | Mechanical + Manual | **Outcome A — defect present**: `accent_defect_present_before_count` > 0 and `accent_defect_present_after_count` == 0 (Required Counters); mechanical gate: `captures_differ_when_should` on the declared before/after pair; manual, non-waivable (hard-won rule 11): Évaluateur opens both images and reads the label itself — confirms the letter is folded (`ILE-DE-FRANCE`), not merely absent, blank, or replaced by a box glyph. **Outcome B — defect absent**: `accent_defect_present_before_count` == 0 is PASS, not FAIL, if and only if (a) `sample_size` is a real, non-empty count (`no_empty_sample_pass`) drawn from an actually-run, cited reproduction scenario naming the specific accented labels checked and the historical reproduction path invoked (e.g. `MapSnapshotExporter.SanitizeLabelText` over a named `StreamingAssets` data set), (b) the Générateur's log documents the investigation honestly — what was run, on what data, with what result — rather than asserting the absence without evidence, and (c) no fictitious/synthetic "before" defect was manufactured to force a nonzero count; manufacturing one is itself a FAIL of this row, worse than reporting Outcome B honestly. In Outcome B no `must_differ_from` pair is required for this row. Manual, non-waivable in either outcome: Évaluateur independently re-runs or re-derives the cited measurement — never trusts the Générateur's claim alone — and confirms by eye that the same named labels, opened directly, are correctly folded |

### Success Condition 3 (row 18)

**Before:**

    | 3 | Player-banner decimals in French format, no scientific notation, proven before/after | Mechanical + Manual | `scientific_notation_before_count` > 0 and `_after_count` == 0; declared pair passes `captures_differ_when_should`; manual: Évaluateur reads the after-capture's fiscal panel directly and confirms a French comma-decimal, no exponent |

**After:**

    | 3 | Player-banner decimals in French format, no scientific notation: fixed if the defect is proven present, or proven absent by honest measurement if it is not (amended by `amendment-002-absent-defect-waiver.md`) | Mechanical + Manual | **Outcome A — defect present**: `scientific_notation_before_count` > 0 and `_after_count` == 0; declared pair passes `captures_differ_when_should`; manual: Évaluateur reads the after-capture's fiscal panel directly and confirms a French comma-decimal, no exponent. **Outcome B — defect absent**: `scientific_notation_before_count` == 0 is PASS, not FAIL, if and only if (a) `sample_size` is a real, non-empty count (`no_empty_sample_pass`) drawn from the exact reproducible scenario the brief cites (`REVUE-v1_054.md`'s fiscal-panel case), with the specific fields/values checked named, (b) the Générateur's log documents the investigation honestly — what was run, on what data, with what result — rather than asserting the absence without evidence, and (c) no fictitious/synthetic "before" defect was manufactured to force a nonzero count; manufacturing one is itself a FAIL of this row, worse than reporting Outcome B honestly. In Outcome B no `must_differ_from` pair is required for this row. Manual, non-waivable in either outcome: Évaluateur independently re-derives the cited measurement and confirms by eye that the fiscal panel, opened directly, reads a French comma-decimal with no exponent |

### Stale timestamp cleanup (feedback Issue 9)

`amendment-001-authored-correction.md` deliberately scoped itself to the
two frontmatter `Authored:` fields only and flagged, without fixing, two
residual prose parentheticals still citing the old future-dated values.
Both gates read the frontmatter programmatically, so these were cosmetic,
not live defects — but they are cleaned up here so the next reader is not
misled into re-diagnosing an already-resolved problem.

**`mtime_after_brief` row (was row 34):**

Before: `...postdates `brief.md`'s Authored timestamp (2026-08-01T11:00:00)`
After: `...postdates `brief.md`'s Authored timestamp (2026-07-31T20:18:44)`

(value taken from `amendment-001-authored-correction.md`'s own correction
table — `brief.md`'s real, corrected `Authored:` value.)

**`rubric_predates_deliverables` row (was row 39):**

Before: `This rubric's Authored timestamp (2026-08-01T11:00:01) predates every deliverable's mtime`
After: `This rubric's Authored timestamp (2026-07-31T20:18:45) predates every deliverable's mtime`

(value taken from `amendment-001-authored-correction.md`'s own correction
table — `eval-rubric.md`'s real, corrected `Authored:` value.)

## What this amendment deliberately did not touch

- `brief.md` is not modified by this amendment. Its Required Counters
  table (rows for `accent_defect_present_before_count` and
  `scientific_notation_before_count`) still literally reads "must be > 0
  (proves the defect is real)". `eval-rubric.md` is the document the
  Évaluateur applies (its own header states this: "The Évaluateur applies
  it independently"), and the rule above governs the check as of this
  amendment. This is a known, deliberately bounded residual — not hidden,
  named here exactly as `amendment-001` named its own residual — left for
  a future amendment or the next full brief revision to reconcile the
  Required Counters table's prose to match. It does not create a live gate
  conflict: no mechanical check in `harness/verdict_audit.py` reads
  `brief.md`'s Required Counters prose directly; the counters it inspects
  are `manifest.json`'s numeric fields and `sample_size`, both governed by
  the rule this amendment states.
- No other Success Condition row, no Non-Goal row, no mechanical-gate row
  beyond the two timestamp parentheticals, the Session-Cost Calibration,
  the Overall Verdict Rule, and the Plateau/Waiver Notes section (all
  reproduced unchanged) were touched.
- Feedback Issues 1-7 (Générateur-side) and the harness's other findings
  (P1 "dump technique" still open, Success Condition 7's missing
  `manifest.json` string) are unchanged by this amendment and remain the
  Générateur's to fix on the next iteration.
- No code, no `deliverables/`, no `manifest.json`, no `verdict.md`, no
  commit. This amendment touches only `eval-rubric.md` and this file.

## Attribution

This is the Planificateur closing the gap the Évaluateur identified and
attributed to the brief, not to the work (`verdict.md`: "Closing it is the
Planificateur's job, not the Générateur's"). It does not retroactively
requalify `verdict.md`'s REJECT — that verdict stands as written against
the rubric text that existed when it was rendered. The next Générateur
iteration is evaluated against this amended `eval-rubric.md`.
