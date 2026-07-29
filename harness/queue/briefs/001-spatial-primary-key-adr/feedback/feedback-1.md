# Feedback — Iteration 1 -> 2

**From**: orchestrator, relaying `verdict.md`'s "Feedback for Next Iteration"
(items 1-4 only; item 5 is explicitly out of scope for this brief — it's
about the mechanical gate itself, not this brief's deliverables).

## Mechanical gate state after iteration 1

`py harness/verdict_audit.py harness/queue/briefs/001-spatial-primary-key-adr`
exits 1, with exactly one FAIL:

```
[FAIL] no_bare_python_alias: bare `python` found in: ['eval-rubric.md']
```

This is a confirmed false positive (verified independently by the
Évaluateur): the only occurrence of the word is `eval-rubric.md`'s own row
*describing* the `no_bare_python_alias` check, not a real invocation. This is
a gate defect, out of scope for this brief. **Do not edit `eval-rubric.md` to
dodge it** — the rubric must be able to name what it enforces.

The other 8 checks now pass. Fix the following four real items so the
deliverables are fully honest, independent of the gate defect above:

## 1. Stale citation in the ADR's `## Decision`

`docs/adr/0003-single-spatial-primary-key.md`'s `## Decision` currently says
the chosen key "matches the on-record recommendation already written into
`pipeline/geo/README.md`" and quotes it in the present tense. That
parenthetical no longer exists in `pipeline/geo/README.md` — this same brief
deleted it as part of the required unblock edit (correctly). Fix: attribute
the recommendation to `docs/rules/simulation-principles.md`'s failure-mode-1
row as the durable source. If you keep the exact wording as a quotation, mark
it as what stood in `pipeline/geo/README.md` *before* this brief's edit, and
point at `deliverables/pre-edit/pipeline-geo-README.md.orig`. Do not restore
the parenthetical to the live README — removing it was correct.

## 2. `failure_mode_1_citation_count`'s declared command doesn't reproduce its value

The declared `Select-String` command is case-insensitive and returns one
more match than the recorded value of 22 (the `### Positive` bullet opens
with a capitalized "Failure mode #1"). Fix by making command and value
agree: either record the case-insensitive count, or add `-CaseSensitive` to
the declared command so it documents what was actually run to produce 22.

## 3. `sample_size` is a copy of `value` on every counter

For `alternatives_considered_count` this is legitimate (denominator *is* the
heading count). For `failure_mode_1_citation_count`, `readme_unblock_reference_count`,
and `adr_index_rows_count`, brief.md's "sample source" column names a real
denominator (lines scanned in the ADR; files scanned; numbered ADR files on
disk) that isn't currently being recorded — `sample_size` should reflect that
denominator, not just repeat `value`.

## 4. Undeclared artifact

`harness/queue/cost-ledger.jsonl` was created (via `ledger.py`) during
iteration 1 but is not declared in `manifest.json`'s files list. Declare it,
even though it's outside `sim/`/`pipeline/geo/` and violates no Non-Goal.

## What NOT to touch

- Do not edit `eval-rubric.md`.
- Do not edit `verdict_audit.py` or any other gate file.
- Do not restore the deleted parenthetical to `pipeline/geo/README.md`.
- Do not touch any file under `sim/` or `pipeline/geo/` other than the two
  README.md files already in scope.
