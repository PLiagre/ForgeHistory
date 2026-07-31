# ADR-0004: Bulk-port VictoriaProject's Unity game into `unity/game_unity/`, automation layer excluded

**Date**: 2026-07-31
**Status**: accepted
**Deciders**: project owner

## Context

`unity/` has held nothing but a stub since F0: no renderer exists anywhere in
this repository to turn any state — a geo-pipeline artifact, an ADR-decided
cell, a future `sim/` tick — into something a person can look at and compare
against what changed. F1's own definition of done is stated in world-terms,
not code-terms: "je change une possession dans les données et la carte le
montre, capture à l'appui" (`FORGE-HISTORY-BRIEF.md` §8). A change with no
observable consequence is not delivered — this is failure mode #3 (the
terminal variable) applied to the render client itself.

`FORGE-HISTORY-BRIEF.md` §3 recorded a default recommendation for
VictoriaProject's 60 C# simulation systems: "relire, ne pas copier" (reread,
don't copy) — treat VictoriaProject as reference material, not a body to
carry across whole. This ADR records an explicit, owner-arbitrated deviation
from that default, scoped specifically to the Unity render/game client (not
the sim systems generally). The project owner's decision, recorded on
2026-07-31 in `brief.md`'s "Owner Decision This Brief Implements" section,
reads:

> « Récupérer le code existant fonctionnel de VictoriaProject. Mais les
> harnais et contrôles sont ceux de ForgeHistory. Objectif : un jeu beau et
> fonctionnel à la fin de la session. »

VictoriaProject already built, measured, and repeatedly re-proved the one
reading path this repository needs and does not yet have: a political map
that resolves "where" through one named, test-guarded translation point
between two coexisting ID systems, a GPU-backed render path, and orientation
controls that derive their reference from measurement rather than two
hardcoded countries. None of that exists in ForgeHistory today, and
rewriting it from scratch — even "reading, not copying" — costs weeks
against a same-session goal ("un jeu beau et fonctionnel à la fin de la
session"), not hours.

## Decision

`C:\Users\liagr\VictoriaProject\game_unity\` is copied in bulk into
`D:\ForgeHistory\unity\game_unity\` via `robocopy`, excluding exactly eight
regenerable directories (`Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`,
`PresentationCache/`, `UserSettings/`, `.vs/`) and nothing else —
`Captures/` is retained because the ported test suite still reads from it
(`V1070PoliticalMapTests.cs`'s `V1070_Proof_CapturesAndLog`). VictoriaProject's
own automation layer (`cursor_tasks/`, `automation/run_queue.py`,
`RESULT_TEMPLATE.json`, `runtime_bridge/` locks) is explicitly **not**
ported — ForgeHistory's three-role harness (`harness/queue/briefs/`,
`harness/verdict_audit.py`) replaces it entirely. No simulation or gameplay
C# logic in the ported tree is modified as part of this decision.

## Alternatives Considered

### Alternative 1: Reread-and-rewrite (the §3 default for the 60 simulation systems)
- **Pros**: every line of the render client would be written under
  ForgeHistory's own conventions from the start (world-terms naming, F1
  ADR-0003's cell-as-primary-key target satisfied immediately instead of
  imported as debt); no risk of carrying forward code this repository never
  reviewed line-by-line.
- **Cons**: VictoriaProject's map renderer alone spans a GPU render path
  measured at 0.305 ms/frame (after a 98 ms/frame CPU path), a named
  ID-translation point closing failure mode #1's gap the day it was found,
  and orientation controls rewritten once already after a silent regression
  — reproducing that from "reading" alone, this session, risks reproducing
  the exact defects VictoriaProject already found and fixed (the v1_095b
  silent-blindness regression named in `brief.md`'s World-Terms
  Requirement), at a cost measured in weeks, not the remainder of one
  session.
- **Why not**: the owner decision explicitly names "un jeu beau et
  fonctionnel à la fin de la session" as the objective this brief is
  arbitrated against — a multi-week rewrite cannot meet a same-session
  target, and §3's own default was written before this session's explicit
  time budget was set. This is the deviation ADR-0004 exists to record: §9's
  arbitration overrides §3's default for this one component (the render
  client), not for the 60 simulation systems generally.

### Alternative 2: Bulk port, VictoriaProject's own automation layer included
- **Pros**: `cursor_tasks/`, `automation/run_queue.py`, and
  `runtime_bridge/` already encode a working task-queue and lock-file
  discipline that VictoriaProject used successfully across 95+ shipped
  briefs; porting it would be zero-cost reuse of a proven mechanism.
- **Cons**: ForgeHistory already has its own three-role harness
  (Planificateur / Générateur / Évaluateur, `verdict_audit.py`) built and
  proven in F0 specifically to fix VictoriaProject's costliest structural
  failure (a single agent both producing and pronouncing its own work
  acceptable — ADR-0001). Running two independent automation/queue layers
  side by side in one repository invites exactly the kind of "two
  independently-writable sources of truth" failure ADR-0003 was written to
  eliminate for spatial identity, just relocated to task orchestration.
- **Why not**: ForgeHistory's harness is not a gap this port needs to fill;
  it is the replacement already in place. Porting VictoriaProject's
  automation would reintroduce a second, competing control layer for no
  capability this repository is missing.

### Alternative 3: Bulk port, automation layer excluded (chosen)
- **Why not rejected**: this is the option chosen. It captures the
  already-working, already-measured render/game client whole, inside the
  session's time budget, while keeping exactly one automation/control layer
  in the repository — ForgeHistory's own three-role harness — rather than
  two competing ones. It defers the §3 "relire, ne pas copier" treatment to
  where it was always meant to apply first: the 60 simulation systems, none
  of which are touched by this brief.

## Consequences

### Positive
- `unity/` stops being an empty stub; F1's world-terms done-criterion ("je
  change une possession dans les données et la carte le montre, capture à
  l'appui") now has a renderer capable of proving it, once `sim/` and
  `pipeline/geo/` exist to feed it.
- The GPU render path, the political-map translation point, and the
  measurement-derived orientation controls are carried forward whole,
  without re-risking the specific regressions VictoriaProject already found
  and fixed while building them.
- Exactly one automation/control layer remains in this repository
  (ForgeHistory's three-role harness); VictoriaProject's queue/lock
  machinery is not duplicated.

### Negative
- This port **imports failure mode #1's debt rather than resolving it**.
  The ported ECS still carries a sim-side `ProvinceId` (values roughly
  1..~50) alongside the pilot map's own `cell_id` (values >= 1164) — two
  coexisting identifiers for "where," exactly the shape failure mode #1
  names ("Double primary key (sim `ProvinceId` vs geometry `cell_id`)" per
  `docs/rules/simulation-principles.md`). The only reason this does not
  currently desync is a single, named, test-guarded translation point,
  `PilotMapProvider.SimulationProvinceIdOfView` (introduced v1_094) — cited
  here by name only, per hard-won rule 12, not re-derived or re-stated. Its
  bridging behavior holds only as long as every reader of "where" goes
  through that one call site; this ADR does not re-verify that invariant,
  it only imports the code that currently satisfies it. ADR-0003 (the
  geographic cell as the single spatial primary key, Province as a derived
  aggregation) remains the F1 target this ported code does **not**
  implement — the ported tree still resolves "where" through the
  coexisting-ID-plus-translation-point shape ADR-0003 was written to
  eventually remove, not through cell-first aggregation. Resolving that
  coexistence is explicitly out of this brief's scope (`brief.md`
  Non-Goals): it is gated on `sim/` and `pipeline/geo/` reaching the point
  where they can actually replace the pilot map's current data source.
- VictoriaProject's automation-tooling knowledge (queue semantics, lock
  discipline, `RESULT_TEMPLATE.json` conventions) is not carried forward in
  any form — a future brief that wants any of that behavior must reimplement
  it against ForgeHistory's own harness contract, not resurrect the
  original files.

### Risks
- Bulk-porting a live working directory, rather than a specific reviewed
  commit, imports whatever state that directory happens to be in at copy
  time — not only the reviewed, tested history the World-Terms Requirement
  cites by version tag (v1_094/v1_095/v1_095b). This brief's own
  `generator-log.md` documents a real instance of this risk found during
  execution: the copied tree carried forward in-progress, uncommitted
  changes unrelated to the map-rendering path this ADR is about. Mitigated
  going forward by treating any future re-sync from VictoriaProject the same
  way this one was handled — read-only source, full compile/test proof
  re-run at the new location before trusting anything carried over, never
  assumed green from VictoriaProject's own prior measurements.
- The imported `ProvinceId`/`cell_id` coexistence (Negative, above) has no
  write-coverage guard in ForgeHistory today verifying every caller still
  routes through the single named translation point; if a future edit adds
  a second reader that bypasses it, nothing here would catch that
  regression. Mitigated by scoping any future rewrite of this path to land
  alongside ADR-0003's actual implementation, not before it.
