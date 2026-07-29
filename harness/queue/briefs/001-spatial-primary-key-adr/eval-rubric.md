# Eval Rubric — Brief 001 (ADR-0003: single spatial primary key)

**Authored**: 2026-07-29T10:00:01

This rubric is written before any Générateur work exists for this brief. The
Évaluateur applies it independently; it is not to be revised after seeing the
deliverables.

| # | Success Condition (from brief.md) | Checked by |
|---|---|---|
| 1 | `docs/adr/0003-<slug>.md` exists, frontmatter has `**Date**`, `**Status**: accepted`, `**Deciders**: project owner` | manual: open the file; confirm all three frontmatter fields literally present and Status is exactly `accepted` (not `proposed`) |
| 2 | Top-level sections present in order — Context, Decision, Alternatives Considered, Consequences — with Consequences containing Positive/Negative/Risks subsections | manual: `Select-String -Pattern "^#"` (or `grep -n "^#"`) on the ADR file; diff the heading list against `docs/adr/template.md`'s heading list; order and names must match |
| 3 | >= 3 `### Alternative N:` entries under Alternatives Considered, each with real Pros/Cons/Why-not specific to this codebase | mechanical count: `Select-String -Pattern "^### Alternative"` count >= 3, feeds `alternatives_considered_count` (gate: `no_empty_sample_pass` requires a real, nonzero sample_size on this counter in manifest.json); content specificity (not filler) is a manual judgment call — reject any alternative whose Cons/Why-not could be pasted onto an unrelated decision unchanged |
| 4 | One rejected alternative is specifically "keep both IDs with a single-location, test-guarded translation layer" | manual: read the alternative list; this exact fallback (or an unmistakable paraphrase of it) must appear as a named, evaluated alternative, not be absent |
| 5 | Failure mode #1 named explicitly (by number, and by the ProvinceId/cell_id language) in Context or Decision, with a causal (not code-quality-only) explanation of how the decision resolves it | manual: `Select-String -Pattern "failure mode #1|ProvinceId"` on the ADR; then read the surrounding paragraph — reject if the explanation never traces a causal chain (hungry->seek->steal style: X desyncs -> Y disagrees -> Z observable wrong-world effect) |
| 6 | Any deviation from `simulation-principles.md`'s on-record recommendation (cell = key) appears as its own named `### Alternative`, never a silent substitution | manual: compare the ADR's `## Decision` against the on-record recommendation; if they differ, confirm the recommendation itself is listed and argued against as an alternative — a decision that quietly diverges without doing so FAILS this condition regardless of how well-argued the rest of the ADR is |
| 7 | `sim/README.md` no longer contains "do not add simulation code here before that ADR exists, to avoid re-importing VictoriaProject's double-primary-key defect"; replacement references `docs/adr/0003-<slug>.md` by path; does not claim this brief authorizes writing `sim/` code | mechanical grep: old sentence absent (`Select-String` returns no match); new sentence contains the literal string `docs/adr/0003-` — feeds `readme_unblock_reference_count` |
| 8 | `pipeline/geo/README.md` no longer contains "F1 begins with an ADR deciding the single spatial primary key ... before any code lands here"; replacement references `docs/adr/0003-<slug>.md` by path; does not authorize `pipeline/geo/` code | mechanical grep, same pattern as row 7, other file |
| 9 | `docs/adr/README.md` gains a table row for ADR-0003 (path/title/`accepted`/date); the line "ADR-0003 (single spatial primary key) is reserved for F1 — not written yet." is removed | mechanical grep: old sentence absent; new table row present and references the same `docs/adr/0003-<slug>.md` filename used elsewhere — feeds `adr_index_rows_count` (must equal 3) |
| 10 | `deliverables/manifest.json` declares pre-edit README snapshots with `must_differ_from` pointing at the post-edit files | mechanical gate: `files_declared_exist` + `captures_differ_when_should` (fails if snapshot and edited file are byte-identical, i.e. no real edit occurred) |
| 11 | No file under `sim/` or `pipeline/geo/` other than the two named READMEs is created/modified | manual: read `deliverables/manifest.json`'s `files` list; any path under `sim/` or `pipeline/geo/` that is not exactly `sim/README.md` or `pipeline/geo/README.md` is an automatic FAIL of this row |
| — | All counters in `manifest.json` carry a real, nonzero `sample_size` | mechanical gate: `no_empty_sample_pass` |
| — | Every declared deliverable postdates `brief.md`'s Authored timestamp (2026-07-29T10:00:00) | mechanical gate: `mtime_after_brief` |
| — | Any waiver claim carries the exact command + error required by brief.md's Acceptable Waivers table | mechanical gate: `waivers_have_command_and_error` |
| — | No bare `python` invocation anywhere in deliverables/logs | mechanical gate: `no_bare_python_alias` |
| — | Any number cited in `verdict.md` traces back to a value/sample_size actually present in `manifest.json` | mechanical gate: `verdict_numbers_traceable` |
| — | `verdict.md`'s Author differs from `generator-log.md`'s Author | mechanical gate: `verdict_is_not_self_authored` |
| — | This rubric's Authored timestamp (2026-07-29T10:00:01) predates every deliverable's mtime | mechanical gate: `rubric_predates_deliverables` |

## Overall Verdict Rule

ACCEPT only if every numbered row (1-11) passes its check AND every
mechanical-gate row passes `py harness/verdict_audit.py
harness/queue/briefs/001-spatial-primary-key-adr` with exit code 0. A single
FAIL on rows 5 or 6 (silent reinterpretation of failure mode #1, or a
non-causal justification) is disqualifying regardless of how complete the
document otherwise looks — completeness is not the same as correctness of
the decision this brief exists to force.
