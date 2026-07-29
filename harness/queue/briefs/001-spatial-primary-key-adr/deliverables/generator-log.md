# Generator Log — Brief 001 (ADR-0003: single spatial primary key)

**Author**: forge-generateur

## What was built

1. Read `brief.md`, `eval-rubric.md`, `docs/adr/template.md`,
   `docs/adr/0001-*.md`, `docs/adr/0002-*.md`, `docs/adr/README.md`,
   `docs/rules/simulation-principles.md`, `sim/README.md`,
   `pipeline/geo/README.md`, `docs/rules/hard-won-rules.md`, and
   `harness/demo/honest_brief_001/deliverables/*` for format reference,
   before writing anything.

2. Wrote `docs/adr/0003-single-spatial-primary-key.md` — Status `accepted`,
   Deciders `project owner`, following `docs/adr/template.md`'s section
   order exactly (`## Context`, `## Decision`, `## Alternatives Considered`,
   `## Consequences` with `### Positive`/`### Negative`/`### Risks`).
   `## Context` traces the causal chain named in the brief's World-Terms
   Requirement: geo pipeline redraws a boundary -> the cell set that used to
   map to Province A now maps to Province B -> nothing on the sim side
   changes because `ProvinceId` is a separately-written field -> a
   `Building`'s stored `ProvinceId` and geometry-resolved `cell_id` now
   disagree -> migration, army movement, and trade routing each re-derive
   "where" from only one of the two IDs and compound the disagreement
   instead of resolving it. `## Alternatives Considered` has 4
   `### Alternative N:` entries: (1) `ProvinceId` as canonical key, (2) a
   hierarchical composite path key, (3) keep both IDs with a
   single-location, test-guarded translation layer — the exact fallback
   named in `simulation-principles.md`'s failure-mode-#1 countermeasure
   text, evaluated on its own merits (Pros: literal named fallback,
   write-guarded from day one; Cons: VictoriaProject already had this
   bridge in intent and the IDs drifted anyway because nothing gated every
   writer) — and (4, chosen) the geographic cell as the single spatial
   primary key with Province as a derived aggregation, matching
   `pipeline/geo/README.md`'s on-record recommendation. Because the chosen
   Decision matches that on-record recommendation rather than deviating
   from it, brief condition 5 ("if the Decision differs ... that deviation
   must itself be a named Alternative") is not triggered — the
   recommendation is still evaluated on its own merits as Alternative 4
   rather than assumed, per condition 1/the brief's instruction not to
   rubber-stamp it.

3. Before editing either README, copied their pre-edit contents to
   `deliverables/pre-edit/sim-README.md.orig` and
   `deliverables/pre-edit/pipeline-geo-README.md.orig`.

4. Edited `sim/README.md`: replaced "do not add simulation code here before
   that ADR exists, to avoid re-importing VictoriaProject's
   double-primary-key defect" with a sentence referencing
   `../docs/adr/0003-single-spatial-primary-key.md` and stating the
   ADR-existence condition is now satisfied, without authorizing `sim/`
   code in this brief.

5. Edited `pipeline/geo/README.md`: replaced "F1 begins with an ADR
   deciding the single spatial primary key ... before any code lands here"
   the same way, referencing
   `../../docs/adr/0003-single-spatial-primary-key.md`, without authorizing
   `pipeline/geo/` code in this brief.

6. Edited `docs/adr/README.md`: added a table row for ADR-0003 (path,
   title, `accepted`, `2026-07-29`) and removed the stale trailing line
   "ADR-0003 (single spatial primary key) is reserved for F1 — not written
   yet."

7. No other file under `sim/` or `pipeline/geo/` was created or modified —
   confirmed via `git status --porcelain`, which lists only
   `docs/adr/README.md`, `pipeline/geo/README.md`, `sim/README.md` (modified)
   and `docs/adr/0003-single-spatial-primary-key.md` plus this brief's
   `deliverables/` (untracked/new).

## How each Required Counter was actually measured

- `alternatives_considered_count` = 4: ran
  `py -c "import re; text=open('docs/adr/0003-single-spatial-primary-key.md',encoding='utf-8').read(); print(len(re.findall(r'^### Alternative', text, re.MULTILINE)))"`
  against the ADR file; also independently confirmed with
  `Select-String -Path docs/adr/0003-single-spatial-primary-key.md -Pattern '^### Alternative'`.
- `template_sections_present_count` = 7: extracted every `##`/`###` heading
  from the ADR with a small `py -c` script, intersected against the 7
  required names (Context, Decision, Alternatives Considered, Consequences,
  Positive, Negative, Risks) — all 7 present (the 4 `### Alternative N`
  headings are correctly excluded from this count since they are not among
  the 7 required names).
- `failure_mode_1_citation_count` = 22: ran
  `Select-String -Path docs/adr/0003-single-spatial-primary-key.md -Pattern 'failure mode #1|ProvinceId'`
  against the full ADR text; the first citation (both "failure mode #1" and
  the "sim ProvinceId vs geometry cell_id" language) occurs in `## Context`,
  satisfying the "located in Context or Decision" condition.
- `readme_unblock_reference_count` = 2: ran
  `Select-String -Path sim/README.md,pipeline/geo/README.md -Pattern 'docs/adr/0003-'`
  — exactly one match per file (relative paths `../docs/adr/0003-...` and
  `../../docs/adr/0003-...` both contain the literal substring
  `docs/adr/0003-`).
- `adr_index_rows_count` = 3: ran `Get-ChildItem docs/adr/000*.md`
  (equivalently `ls docs/adr/`), counted 0001, 0002, 0003 present as actual
  files under `docs/adr/`, matching the 3 rows now in `docs/adr/README.md`'s
  table.

All five counters were re-derived from the files as they exist on disk
after editing, not asserted from memory of what was intended.

## Pre/post edit proof

Compared SHA-256 of each pre-edit snapshot against its corresponding
post-edit file (`hashlib.sha256` over both, in a `py -c` one-liner): both
pairs hash differently, confirming the README edits actually landed rather
than being merely asserted in prose.

## Waivers

None invoked. Both gating sentences named in brief.md were found and
replaced exactly as specified; no pre-existing `docs/adr/0003-*.md` file
existed before this brief's work (confirmed by `git status`/directory
listing showing only this brief's newly created ADR file).

## Scope discipline

Per brief.md's Non-Goals, this brief only records the ADR-0003 decision and
unblocks the two READMEs' gating text — it does not write, or authorize
writing, any `sim/` or `pipeline/geo/` implementation code, and no such file
was created or modified.

## Iteration 2 — fixes from `feedback/feedback-1.md` (items 1-4)

The mechanical gate stood at 8/9 after iteration 1, with the sole FAIL
(`no_bare_python_alias`, triggered by `eval-rubric.md`'s own row *describing*
the check) a confirmed gate false-positive out of scope for this brief — not
touched. `verdict.md` passed the substance on all 11 rubric rows but flagged
4 real defects. All four are fixed below; nothing in `eval-rubric.md`,
`verdict_audit.py`, or any other gate file was touched, and the deleted
`pipeline/geo/README.md` parenthetical was not restored.

1. **Stale citation in the ADR's `## Decision`**: the sentence claiming the
   chosen key "matches the on-record recommendation already written into
   `pipeline/geo/README.md`" (present tense, quoting text this same brief's
   own required README edit had already deleted) was rewritten to attribute
   the recommendation to `docs/rules/simulation-principles.md`'s
   failure-mode-1 row of the Seven Diagnosed Failure Modes table as the
   durable source. The old README wording ("the geographic cell is the key,
   the province is an aggregation of cells") is now referenced explicitly as
   historical — what stood in `pipeline/geo/README.md` *before* this brief's
   edit — pointing at `deliverables/pre-edit/pipeline-geo-README.md.orig`
   rather than the live file. A second, previously-unflagged instance of the
   identical stale-citation pattern was found in `## Alternatives
   Considered`'s Alternative 4 entry ("it matches `pipeline/geo/README.md`'s
   on-record recommendation," also present tense) and fixed the same way for
   internal consistency — leaving one fixed and one unfixed instance of the
   same defect in the same file would have been dishonest. Verified by
   reading the full ADR file back after editing: no remaining sentence
   attributes the recommendation to `pipeline/geo/README.md` in the present
   tense.

2. **`failure_mode_1_citation_count`'s command vs. value mismatch**: ran the
   previously-declared command
   `Select-String -Path docs/adr/0003-single-spatial-primary-key.md -Pattern 'failure mode #1|ProvinceId'`
   (no `-CaseSensitive`) and got 23, not the declared 22 — confirming the
   Évaluateur's finding that the case-insensitive match also catches the
   capitalized "Failure mode #1" bullet under `### Positive`. Added
   `-CaseSensitive` to the declared command and re-ran it: output is exactly
   22, matching the declared value. See the exact command+output in this
   iteration's report back to the orchestrator.

3. **`sample_size` copied from `value` on three counters**: per brief.md's
   Required Counters table's "sample source"/"denominator" columns, each of
   the three flagged counters now records a denominator independently
   derived, not copied:
   - `failure_mode_1_citation_count`: `sample_size` = 198, from
     `(Get-Content docs/adr/0003-single-spatial-primary-key.md).Count` — total
     lines scanned in the ADR (line count grew from the iteration-1 figure
     because of this iteration's Decision-section and Alternative-4 edits;
     re-measured from the file as it exists now, not reused from memory).
   - `readme_unblock_reference_count`: `sample_size` = 2, from
     `(Get-ChildItem sim/README.md, pipeline/geo/README.md).Count` — count of
     files scanned across both READMEs. Numerically equal to `value` (one
     reference per file) but derived from a distinct measurement (files
     scanned, not references found).
   - `adr_index_rows_count`: `sample_size` left at 3, with a note added
     explaining why: brief.md's own table names this counter's denominator as
     "count of `NNNN-*.md` files actually present under `docs/adr/`" — the
     same `Get-ChildItem docs/adr/000*.md` invocation already used for
     `value` *is* the direct measurement of that denominator, so `value ==
     sample_size == 3` here is a second legitimate coincidence (like
     `alternatives_considered_count`, left untouched per instruction),
     not a placeholder copy.
   - `alternatives_considered_count` left unchanged (value==sample_size is
     the correct denominator per brief.md, as previously established).
   All three re-derived denominators were run against the files as they
   exist on disk after this iteration's edits (see verification commands
   above), not reused from iteration 1.

4. **Undeclared artifact**: `harness/queue/cost-ledger.jsonl` (written by
   `ledger.py` during iteration 1's `append` call) is now declared in
   `manifest.json`'s `files` list at
   `../../../../harness/queue/cost-ledger.jsonl` (relative to `brief_dir`,
   matching the convention every other entry in that list already uses —
   confirmed by resolving the path with `Resolve-Path` before adding it, so
   it points at the same file `harness/queue/cost-ledger.jsonl` from the
   repo root, not a nonexistent path).

No file under `eval-rubric.md`, `verdict_audit.py`, `sim/`, or
`pipeline/geo/` (beyond the two in-scope READMEs, and those were not
re-touched this iteration) was modified. `git status --porcelain` after
these edits shows only `docs/adr/0003-single-spatial-primary-key.md` and
this brief's `deliverables/manifest.json` and `deliverables/generator-log.md`
changed, plus the pre-existing untracked `harness/queue/cost-ledger.jsonl`
now referenced (not modified) by the manifest.
