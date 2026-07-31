# Feedback — Brief `002`, Planificateur amendment after iteration 1

**Authored**: 2026-07-29T19:30:00
**Author**: forge-planificateur
**Context**: `verdict.md` (iteration 1) and `feedback/feedback-002.md`
identified a self-contradiction inside `brief.md` that no Générateur could
have resolved, plus one unauthorized code edit made while trying anyway.
This file is a narrow amendment to `brief.md` and `eval-rubric.md` only. It
does not reopen anything the Évaluateur already confirmed correct.

## What changed in `brief.md`

**Success Condition 3** — the closing sentence "No occurrence of the
substrings `game_unity` or `StreamingAssets` may remain anywhere under
`pipeline/geo/` after this adjustment" is replaced with an explicit, narrow
exception. The new text carves out exactly three named locations —
`data/divergences_1400.json` itself, its produced copy
`artifacts/divergences_1400.json`, and any `logs/*` line quoting that file's
content verbatim — and only for occurrences that are traceable line-for-line
to `data/divergences_1400.json`'s own pre-existing content (verified against
the VictoriaProject original at
`C:\Users\liagr\VictoriaProject\sandbox\geo\data\divergences_1400.json`).
Everything else — any `.py` file, any path-resolution expression, anything
not traced to that one source file's pre-existing content — is unaffected:
still forbidden, must still be zero.

Success Condition 3 also now states explicitly, in its own text (not only in
Non-Goals), that the `FORBIDDEN_GAME_PATH_MARKERS` string-splitting edit made
in iteration 1 was never authorized, must be reverted to
`"StreamingAssets",` / `"game_unity",` exactly as in
`deliverables/pre-port/constants.py.orig`, and that its two
`# FORGEHISTORY-PATH-ADJUSTMENT` markers must be removed since they were
never marking the actual path adjustment. The Non-Goals section gained one
clause making the same point in that section's own words, for anyone reading
Non-Goals in isolation.

**Required Counters table** — the `game_unity_reference_remaining_count` row
is rewritten from a flat "grep the whole tree, must be 0" rule into a
two-step, per-hit traceability rule (grep, then classify each hit by file,
then classify by byte-for-byte match against the VictoriaProject original).
The denominator is still 0 — this is **not** a raised tolerance and there is
no hardcoded "3" or "5" anywhere in the amended text. The exclusion is
computed per-hit from the named source file's content, not assumed.

## What changed in `eval-rubric.md`

- The Success Condition 3 row for the `game_unity` check is rewritten to
  describe the same per-hit traceability procedure, so the Évaluateur has a
  mechanical, repeatable test rather than a judgment call.
- One new Non-Goal row is added: a manual, line-by-line diff check that
  `constants.py`'s only difference from its `.orig` is the authorized path
  adjustment — this exists because the two pre-existing mechanical counters
  (`path_adjustment_marker_count`, `path_adjustment_unmarked_diff_line_count`)
  cannot by themselves distinguish "marked and authorized" from "marked but
  not actually the adjustment," which is exactly how the iteration-1 edit
  passed the mechanical gate while still being a real violation. This new
  row does not change how those two counters are computed.
- A plateau note is added stating explicitly that the amended counter is a
  traceability rule, not a tolerance, so a future pass does not quietly
  harden it into a magic number.

## What did NOT change

Every other Success Condition, the twenty-file copy table, `sources.lock`
handling, the `.gitignore` condition, the determinism/QA proof requirements,
the README condition, the manifest/`must_differ_from` condition, the
evidence-on-disk condition, and every other Required Counter and Non-Goal are
untouched. The Évaluateur's iteration-1 findings on all of those stand: they
are correct and do not need to be re-demonstrated from scratch, only
re-confirmed unchanged if the next verdict wants to note it did not have to
re-verify them.

## What the next Générateur iteration must do

1. **Revert** the `FORBIDDEN_GAME_PATH_MARKERS` hunk in `constants.py` to its
   original single-literal form (see `deliverables/pre-port/constants.py.orig`).
   Remove the two now-meaningless markers on those lines. Do not touch
   anything else in that file.
2. **Do not re-run the port, re-copy any file, or touch
   `steps/02_coastline.py`** — none of that is implicated by this amendment.
3. **Re-measure `game_unity_reference_remaining_count`** under the amended
   definition and report it per-hit (file + line + traced/not-traced), not
   as a single number pulled from a whole-tree grep. Expect it to land at 0
   once the revert is applied — the two `constants.py` hits disappear
   because the path adjustment itself now resolves to `legacy_game_data/...`
   (not because the literals are re-obfuscated), and the three
   `divergences_1400.json`-derived hits are excluded by the named exception.
4. **Update `deliverables/manifest.json`** to reflect the corrected
   `path_adjustment_marker_count` (drops from 13 to 11 across both files, per
   `verdict.md`'s own arithmetic) and the corrected
   `game_unity_reference_remaining_count`, both derived from the actual
   post-revert state, not carried forward stale.
