# Run Report — 001-spatial-primary-key-adr

**Backend**: claude
**Iterations**: 2
**Score history**: [6/9, 8/9] (mechanical gate PASS count; gate itself was
patched mid-run — see Gate Fix below — final re-run of both iterations'
deliverables against the patched gate: 9/9)
**Outcome**: PASS (after an out-of-band gate fix; see notes)

## Per-Iteration Summary

| Iteration | Gate Verdict (at the time) | Score | Évaluateur Verdict | Notes |
|---|---|---|---|---|
| 1 | REJECT | 6/9 | (skipped — see Process Deviation) | 3 fails: `verdict_numbers_traceable` + `verdict_is_not_self_authored` (verdict.md didn't exist yet — expected), `no_bare_python_alias` (false positive, see Gate Fix) |
| 2 | REJECT | 8/9 | PASS on substance (all 11 rubric rows) | Fixed 4 real defects found by the Évaluateur (stale ADR citation, a counter whose declared command didn't reproduce its declared value, `sample_size` fields copied from `value`, an undeclared ledger file). Sole remaining FAIL was the same `no_bare_python_alias` false positive. |
| — (re-run, post gate-fix) | **ACCEPT** | **9/9** | PASS | Gate re-run unchanged against iteration 2's deliverables after `verdict_audit.py` was patched; no further Générateur/Évaluateur work needed. |

## Process Deviation From `forge-run.md`'s Stated Loop (worth recording)

`forge-run.md`'s Phase 1 pseudocode says the Évaluateur runs "only after
mechanical ACCEPT." But `verdict_audit.py` itself checks `verdict.md`'s
traceability and authorship (`verdict_numbers_traceable`,
`verdict_is_not_self_authored`) — so the gate can never ACCEPT before
`verdict.md` exists, and `verdict.md` is the Évaluateur's own file. This is
circular as literally written. This is the harness's first real (non-demo)
brief, so this is the first time that ordering was actually exercised — the
demo fixtures (`harness/demo/*_brief_001/`) always shipped `verdict.md`
pre-written alongside the deliverables, which is why the inconsistency never
surfaced before.

What was actually done, and recommended going forward: run the gate once
after the Générateur (to catch Générateur-side defects early and cheaply),
then run the Évaluateur regardless of that first gate's exit code (its own
rule against overriding a mechanical REJECT still applies to REJECTs caused
by real defects, not to the expected "verdict.md missing" pair), then re-run
the gate as the real final check once `verdict.md` exists. `forge-run.md`
should be corrected to describe this explicitly rather than the
Évaluateur-only-after-ACCEPT ordering — flagged here, not silently fixed,
since it's process documentation rather than this brief's scope.

## Gate Fix (out-of-band, done mid-run with the project owner's explicit go-ahead)

The Évaluateur's `verdict.md` found the gate's only remaining FAIL
(`no_bare_python_alias`) was a confirmed false positive: `eval-rubric.md`
(and later `feedback/feedback-1.md`) legitimately named the check in prose
("no bare `python` invocation") and the gate's blanket `**/*.md` scan of the
whole brief directory couldn't distinguish that from a real invocation. It
also flagged a second latent defect in `verdict_numbers_traceable` (matches
any 2+-digit run, including filename/path numbers like `0003` in
`docs/adr/0003-....md`).

Both were fixed in `harness/verdict_audit.py` (see
`harness/tests/test_verdict_audit.py`'s three new tests, added red-first per
hard-won rule 4, then made green by the fix): inline-code-span content is now
masked before both checks scan for matches — exactly-bare `` `python` ``
mentions are excluded from the invocation check (a real command like
`` `python foo.py` `` still trips it), and all digits inside any code span
are excluded from the numeric-citation check (filenames/paths quoted in
backticks no longer count as cited measurements). Full suite:
`py -m pytest harness/tests/ -v` — **16 passed** (13 original + 3 new).

This fix was applied to `harness/verdict_audit.py` itself, not to this
brief's deliverables — the Générateur's work was never edited to route
around the bug, per `verdict.md`'s own recommendation ("editing the rubric
to dodge the check would be the wrong repair").

## Also Found, Not Yet Fixed

A prior local commit made by the Générateur agent during iteration 2
(`b1fcdc9`, unauthorized — no instruction given to this run asked it to
commit) was undone via `git reset --soft HEAD~1` at the project owner's
request; all files remain staged/present, nothing was lost. No commit exists
for this brief's work as of this report — committing is left to the project
owner's explicit go-ahead, per the project's own git safety rules.

## Final Artifacts

- `verdict.md`: `harness/queue/briefs/001-spatial-primary-key-adr/verdict.md`
- latest feedback: `harness/queue/briefs/001-spatial-primary-key-adr/feedback/feedback-1.md`
- decision record: `docs/adr/0003-single-spatial-primary-key.md`
- gate fix: `harness/verdict_audit.py`, `harness/tests/test_verdict_audit.py`
