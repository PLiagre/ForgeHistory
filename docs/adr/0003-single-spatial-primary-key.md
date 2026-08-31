# ADR-0003: The geographic cell as the single spatial primary key (Province as derived aggregation)

> **Statut actuel — 2026-08-30 : La décision technique sur `cell_id` reste active. Toute clause de rôle ou de procédure est obsolète.**

**Date**: 2026-07-29
**Status**: accepted
**Deciders**: project owner

## Context

This is failure mode #1 from `docs/rules/simulation-principles.md`'s Seven
Diagnosed Failure Modes table: "Double primary key (sim `ProvinceId` vs
geometry `cell_id`)". Two independent trees in this repository, `sim/` and
`pipeline/geo/`, each currently stub out an answer to "where is this thing":
the simulation side would resolve location through a `ProvinceId` stored on
`Person`/`Family`/`Building` records, while the geometry side resolves it
through `cell_id`, the geo pipeline's terrain/render unit. Nothing today
forces those two answers to stay reconciled once both trees have code in
them.

Traced causally: the geo pipeline (`pipeline/geo/`, 16 deterministic steps,
re-run and SHA256-verified on every geometry change — coastline reshaping,
DEM refinement, a `sources.lock` version bump) redraws a province boundary.
The set of cells that used to map to Province A now maps to Province B. If
`ProvinceId` is a field independently written and stored on `Person`,
`Family`, and `Building` records, none of those records changes when the
boundary redraw happens — the redraw is a geometry-side event, and nothing
in that event touches sim-side rows. The result: a `Building`'s location now
disagrees with itself. Geometry says the building's `cell_id` is in Province
B. The building's stored `ProvinceId` still says Province A. Population, tax,
and trade logic that resolves "where" by reading `ProvinceId` computes
against Province A. The render pipeline and any terrain-derived logic that
resolves "where" by reading `cell_id` computes against Province B. Both are
"correct" by their own local source, and both are now wrong relative to each
other. Every downstream world-terms system that re-derives Province
membership from only one of the two IDs compounds this instead of resolving
it: migration logic evaluates push/pull pressure using the stale
`ProvinceId`'s tax and population totals, routes families toward or away
from a Province that, by geometry, no longer contains the building they are
leaving; army movement plans supply lines and marches through the
`ProvinceId`-resolved administrative graph while the terrain it actually
marches across belongs to the geometry-resolved Province, silently crossing
what should have been a border event (garrison change, tax jurisdiction
change) without triggering it; trade routing computes tariff and distance
tables from `ProvinceId` while the physical transport network (roads,
ports, storage — see Physical Economy) sits on `cell_id`-resolved terrain,
so goods move through one administrative jurisdiction on paper and a
different one on the map. None of these systems is buggy in isolation; the
defect is that two independently-writable identifiers both claim to answer
the same question, and nothing prevents them from disagreeing the moment
either side edits without the other. ADR-0003 exists to remove the second
writable copy, not to add a rule asking both copies to stay in sync.

## Decision

The geographic cell (`cell_id`, the geo pipeline's spatial unit) is the
single spatial primary key for the entire World -> Country -> Province ->
City -> District -> Building -> Family -> Person hierarchy. Province (and
every coarser level up through Country) is always a **derived aggregation**
of which cells currently compose it — never an independently-writable field
stored on `Person`, `Family`, or `Building`. When the geo pipeline redraws a
boundary, the aggregation is recomputed from the new cell set; there is no
second, separately-writable "where" field anywhere in the hierarchy left to
go stale, so the disagreement traced in Context has no code path left to
produce it. This matches the on-record recommendation whose durable source
is `docs/rules/simulation-principles.md`'s failure-mode-1 row of the Seven
Diagnosed Failure Modes table ("ONE spatial primary key decided before any
code (F1 ADR)"). Before this brief's required unblock edit,
`pipeline/geo/README.md` stated the same recommendation in more concrete
words — "the geographic cell is the key, the province is an aggregation of
cells" — a sentence this brief's own edit correctly removed from the live
README once the ADR-existence condition it gated on was satisfied; that
historical wording is preserved verbatim at
`deliverables/pre-edit/pipeline-geo-README.md.orig`, not in the current
`pipeline/geo/README.md`. That recommendation is evaluated on its own
merits below, against three genuinely rejected alternatives, rather than
adopted by assertion.

## Alternatives Considered

### Alternative 1: `ProvinceId` as the canonical spatial primary key, `cell_id` derived/cached from it
- **Pros**: all existing sim-side world-terms logic (migration, tax, army
  movement, trade routing) is already written in terms of Province
  membership; zero migration cost for any sim code that assumes
  `ProvinceId` is authoritative; smallest short-term change for `sim/`.
- **Cons**: the geo pipeline's whole reason for existing is to *redraw*
  boundaries (16-step deterministic pipeline, re-run and SHA256-verified on
  every source update) — that is the frequent operation, not the rare one.
  If `ProvinceId` is canonical, every geometry regeneration must reverse an
  administrative boundary that may have physically moved back into a set of
  cells, deriving sim state from render output — the same direction of
  dependency failure mode #1 already describes, just with the stale side
  relabeled from `ProvinceId` to `cell_id`.
- **Why not**: it optimizes for the sim engineer's convenience today at the
  cost of making the geo pipeline's own core, expected, recurring operation
  (redrawing) the exact moment identity breaks. This is not a hypothetical
  edge case; it is failure mode #1 restated with the labels swapped.

### Alternative 2: Hierarchical composite path key (e.g. `WorldId/CountryId/ProvinceId/CityId/DistrictId/BuildingId`) as the canonical location identity for every leaf record
- **Pros**: mirrors the World -> Country -> Province -> City -> District ->
  Building -> Family -> Person hierarchy directly; no separate "which
  Province" concept is ever needed since a Person's path already names it.
- **Cons**: a path key embeds the current administrative tree directly into
  every leaf record's identity string. Redrawing a province boundary — the
  exact operation this ADR exists to make safe — now requires rewriting the
  embedded path segment on every `Person`, `Family`, and `Building` record
  that changes Province, a strictly larger blast radius (every leaf, every
  level) than either single-ID design, turning a boundary edit into a mass
  identity rewrite instead of a bounded reconciliation at one aggregation
  step.
- **Why not**: fails on the same axis as failure mode #1 but at a larger
  scale — instead of two IDs disagreeing, a boundary change either forces a
  cascading identity rewrite through the whole hierarchy, or the embedded
  path segment goes stale exactly the way `ProvinceId` does today.

### Alternative 3: Keep both IDs (`ProvinceId` and `cell_id`) with a single-location, test-guarded translation layer
- **Pros**: this is the literal fallback named in
  `simulation-principles.md`'s failure-mode-#1 countermeasure text ("any
  coexisting ID systems get a same-day, single-location, test-guarded
  translation"); requires no rewrite of either stub tree's assumptions;
  write-guarded and red-tested from day one, it could in principle catch
  drift the day it is introduced rather than relying on convention alone.
- **Cons**: it does not remove the two independently-updatable sources of
  truth that caused the failure mode — it inserts a bridge between them.
  For the bridge to hold, literally every writer of Province-membership
  state, in both `sim/` and `pipeline/geo/`, forever, must call through the
  one translation location; F0 has no mechanism yet that gates every
  Province-writing call site against this (the write-coverage check for
  failure mode #2 does not exist for a field that, under this alternative,
  would still exist to be written). VictoriaProject's own defect existed
  with a translation layer in *intent*, not enforced in practice, and the
  two IDs drifted anyway.
- **Why not**: it treats drift as a bug to reconcile after the fact instead
  of removing the second writable copy that made drift possible in the
  first place. It would be the right choice only if a large body of
  instrumentation already had a hard, unmovable dependency on `ProvinceId`
  as a stored field — it does not; both `sim/` and `pipeline/geo/` are
  still empty stubs, so there is no legacy cost to removing the second copy
  now instead of bridging it forever.

### Alternative 4: The geographic cell is the single spatial primary key; Province is a derived aggregation of cells (chosen)
- **Why not rejected**: this is the option chosen, and it matches the
  on-record recommendation whose durable source is
  `docs/rules/simulation-principles.md`'s failure-mode-1 row (the same
  recommendation `pipeline/geo/README.md` stated in more concrete words
  before this brief's required unblock edit removed that wording — see
  `deliverables/pre-edit/pipeline-geo-README.md.orig` for the historical
  text). It directly severs
  the causal chain described in Context: because Province membership is
  *computed* from the current cell set rather than stored as an
  independently-writable field, a boundary redraw cannot leave a stale
  `ProvinceId` anywhere for migration, army movement, or trade routing to
  disagree about — there is no second writable "where" answer left to go
  stale. It concentrates reconciliation cost at the one place (the geo
  pipeline's cell-to-Province aggregation step) where boundary changes
  already originate, instead of chasing every writer across the whole `sim/`
  tree (Alternative 1), every leaf record's identity (Alternative 2), or an
  unenforced convention bridging two copies that VictoriaProject already
  proved does not hold under real use (Alternative 3).

## Consequences

### Positive
- Failure mode #1 becomes structurally impossible for future code to
  reintroduce, not merely policed by review: there is no second writable
  "where" field anywhere in the World -> ... -> Person hierarchy for a
  writer to disagree with `cell_id` about.
- Migration, army movement, and trade routing all resolve "where" through
  one read (current cell membership), so the three-way disagreement traced
  in Context (administrative jurisdiction vs. terrain vs. physical
  transport network) has no code path left to produce it.
- The geo pipeline's existing 16-step, SHA256-verified re-run process
  becomes the single point where "where" changes; `sim/` never needs its
  own province-reconciliation logic, because it never stores a second copy
  to reconcile.

### Negative
- Any sim-side logic that wants a cheap "is this Person in Province X"
  check must derive it from cell aggregation rather than reading a stored
  `ProvinceId` field directly — one hop more expensive than
  VictoriaProject's design, by design.
- Administrative-domain concepts that are naturally per-Province rather
  than per-cell (province names, tax codes, garrison assignments) need a
  cell-to-current-Province lookup layer to exist before any migration, tax,
  or army logic can be written meaningfully — this is now a required F1
  component that did not need to exist under Alternative 1.

### Risks
- If the cell-to-Province aggregation step itself has a defect or races
  during a boundary redraw, every downstream system reads from it
  simultaneously, so an aggregation bug propagates instantly and uniformly
  instead of staying isolated to one stale record. Mitigated by the geo
  pipeline's already-planned SHA256 two-pass QA, plus a write-coverage test
  (failure mode #2's countermeasure) scoped specifically to the aggregation
  step once F1 implements it.
- Recomputing Province aggregation naively on every geometry change could be
  expensive at full-world scale (thousands of cells). Mitigated by scoping
  recomputation to only the cells whose boundary actually changed rather
  than a full-world recompute — an F1 implementation detail, not decided by
  this ADR.
