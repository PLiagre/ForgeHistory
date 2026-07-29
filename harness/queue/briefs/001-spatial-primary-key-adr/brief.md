# Brief 001: ADR-0003 — the single spatial primary key (failure mode #1)

**Authored**: 2026-07-29T10:00:00
**Author**: forge-planificateur

## World-Terms Requirement

Stated causally, not as a code-quality preference:

Geometry (`cell_id`) and simulation (`ProvinceId`) each independently claim to
answer "where is this thing." When a province boundary is redrawn in the geo
pipeline, the set of cells that used to map to Province A now maps to
Province B — but every `Person`, `Family`, and `Building` record still
carries the old `ProvinceId`, because nothing forced those two answers to
stay reconciled. Population, tax, and trade logic that resolves "where" via
`ProvinceId` then disagrees with what the terrain/render pipeline shows at
that same location via `cell_id`. A family's building sits in one province
by geometry and a different one by simulation state, and every downstream
system that re-derives province membership from only one of the two IDs
(migration, army movement, trade routing) compounds the disagreement instead
of resolving it. This is failure mode #1
(`docs/rules/simulation-principles.md`, row 1 of the Seven Diagnosed Failure
Modes). ADR-0003's job is to pick the ONE identifier the whole
World -> Country -> Province -> City -> District -> Building -> Family ->
Person hierarchy resolves "where" through, so that every level of that
hierarchy — and both trees currently stubbed on this decision, `sim/` and
`pipeline/geo/` — agrees on it before either tree gains a single line of
code.

This brief is scoped to writing that decision down, and to updating the two
places in the repository that currently assert code cannot land until the
decision exists. It is NOT scoped to writing that code.

## Success Conditions

1. **ADR file exists** at `docs/adr/0003-<slug>.md` (the Générateur chooses
   `<slug>` to reflect the decision's title) with frontmatter matching
   `docs/adr/template.md`: `**Date**`, `**Status**: accepted`, `**Deciders**:
   project owner` — matching the convention already set by ADR-0001 and
   ADR-0002 (both `accepted`, same Deciders line). Status `accepted` is
   required here, not `proposed`: `sim/README.md` and `pipeline/geo/README.md`
   currently block on the ADR *existing*, and leaving it `proposed`
   indefinitely reproduces the exact stuck-gate problem this brief exists to
   close, with no mechanism ever specified for later flipping it.

2. The ADR contains, in this order, the top-level sections required by
   `docs/adr/template.md`: `## Context`, `## Decision`,
   `## Alternatives Considered`, `## Consequences` — and `## Consequences`
   contains `### Positive`, `### Negative`, `### Risks`.

3. `## Alternatives Considered` contains at least **3** `### Alternative N:
   <name>` entries: the chosen approach, plus at least 2 genuinely rejected
   ones. Each entry states Pros / Cons / Why-not (the chosen entry may use
   "Why not rejected", matching ADR-0002's house style) that are specific to
   ForgeHistory's actual data model — not generic filler like "adds
   complexity." At minimum, one rejected alternative must be **"keep both
   IDs (`ProvinceId` and `cell_id`) with a single-location, test-guarded
   translation layer"** — this is the exact fallback named in
   `simulation-principles.md`'s failure-mode-#1 countermeasure text, and it
   must be evaluated on its own merits, not dismissed by assertion.

4. The ADR explicitly names failure mode #1 — by number, and by citing the
   "sim ProvinceId vs geometry cell_id" language from
   `docs/rules/simulation-principles.md` — in `## Context` or `## Decision`,
   and explains, causally (see World-Terms Requirement above), how the
   chosen key resolves it. A justification phrased only in code-quality
   terms ("cleaner," "simpler") without the causal chain does not satisfy
   this condition.

5. If the ADR's `## Decision` differs from the on-record recommendation in
   `docs/rules/simulation-principles.md` (cell is the key, province is an
   aggregation of cells), that deviation must itself be one of the named
   `### Alternative` entries, with its own why-chosen reasoning — never a
   silent substitution. (See Non-Goals.)

6. **Unblock `sim/README.md`**: the sentence "do not add simulation code
   here before that ADR exists, to avoid re-importing VictoriaProject's
   double-primary-key defect" must be replaced with a sentence that (a)
   references `docs/adr/0003-<slug>.md` by relative path, and (b) states that
   the ADR-existence condition is now satisfied. It must NOT additionally
   claim this brief authorizes writing `sim/` code — that remains a
   separate, future brief's scope.

7. **Unblock `pipeline/geo/README.md`**: the sentence "F1 begins with an ADR
   deciding the single spatial primary key ... before any code lands here"
   must be replaced the same way: reference `docs/adr/0003-<slug>.md` by
   path, state the condition is now satisfied, do not authorize
   `pipeline/geo/` code in this brief.

8. **`docs/adr/README.md`'s index** must gain a row for ADR-0003 (path,
   title, `accepted`, date), and its trailing line "ADR-0003 (single spatial
   primary key) is reserved for F1 — not written yet." must be removed — it
   becomes false the moment the ADR exists, and a stale index is itself a
   declared-field-nobody-checks defect.

9. `deliverables/manifest.json` declares pre-edit snapshots of both edited
   READMEs (e.g. `deliverables/pre-edit/sim-README.md.orig`,
   `deliverables/pre-edit/pipeline-geo-README.md.orig`) with
   `must_differ_from` pointing at the post-edit files — proving the edits
   actually happened rather than being merely asserted in prose. **This is
   the `must_differ_from` pair this brief requires**; there are no other
   before/after artifacts in scope.

10. No file under `sim/` or `pipeline/geo/` other than the two named
    `README.md` files is created or modified.

## Non-Goals

- Must NOT write or modify any `sim/` or `pipeline/geo/` file other than
  `sim/README.md` and `pipeline/geo/README.md`.
- Must NOT silently reinterpret or weaken failure mode #1's countermeasure —
  any deviation from `simulation-principles.md`'s on-record recommendation
  must appear as an explicit, evaluated `### Alternative`, never a quiet
  substitution.
- Must NOT justify the decision in gameplay-terms or hardcoded-rule language
  ("if double-key then bugs") — the causal chain from World-Terms
  Requirement must appear IN the ADR itself, not only in this brief.
- Must NOT have the README unblock sentences claim this brief authorizes
  starting F1 simulation or geo-pipeline code — only that the specific
  ADR-existence condition those READMEs name is now met.
- Must NOT leave `docs/adr/README.md`'s index stale or false once ADR-0003
  exists.
- Must NOT report structural completeness (e.g. "all sections present") from
  memory of what was intended to be written — claims must correspond to what
  is actually on disk after the edit (presence is not function).

## Required Counters

| name | sample source | denominator |
|---|---|---|
| alternatives_considered_count | `docs/adr/0003-<slug>.md`, `### Alternative` headings found under its `## Alternatives Considered` section | count of all `### Alternative` headings in that section (must be >= 3) |
| template_sections_present_count | `docs/adr/0003-<slug>.md`'s `##`/`###` headings, compared against `docs/adr/template.md` | 7 required headings: Context, Decision, Alternatives Considered, Consequences, Positive, Negative, Risks (must equal 7) |
| failure_mode_1_citation_count | full text of `docs/adr/0003-<slug>.md` | count of explicit references naming "failure mode #1", or naming `ProvinceId` and `cell_id` together (must be >= 1, located in Context or Decision) |
| readme_unblock_reference_count | `sim/README.md` + `pipeline/geo/README.md` combined | count of references to `docs/adr/0003-<slug>.md` across both files (must equal 2, one per file) |
| adr_index_rows_count | `docs/adr/README.md`'s ADR table rows | count of `NNNN-*.md` files actually present under `docs/adr/` (must equal 3: 0001, 0002, 0003) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "the gating sentence in `sim/README.md` or `pipeline/geo/README.md` cannot be located to edit" | `Get-Content sim/README.md` (or the `pipeline/geo/README.md` equivalent) | command output does not contain the gating sentence at all — proving it is already absent/changed, not merely asserted |
| "`docs/adr/` already contains a file numbered 0003 not authored by this brief" | `Get-ChildItem docs/adr/0003-*` | command lists a pre-existing `0003-*.md` file whose content predates this brief's `Authored` timestamp (2026-07-29T10:00:00) |
