# unity/

The render client. Zero simulation/business logic — rendering, input,
animation, and UI only (see `VISION.md`: "Unity n'est jamais responsable de
la logique métier... Le backend fonctionne sans Unity"). It must only ever
READ simulation state exposed by `sim/`, never re-derive or re-filter it
(see `docs/rules/simulation-principles.md` failure mode #4 — presentation
re-implementing the simulation was one of VictoriaProject's costliest
defects).

## `game_unity/`

As of brief `003-port-unity-game`, `game_unity/` holds VictoriaProject's
existing Unity map/game client, bulk-ported from
`C:\Users\liagr\VictoriaProject\game_unity\` (see
`docs/adr/0004-bulk-port-victoriaproject-unity-game.md` for the decision and
the failure-mode-#1 debt this port carries in, not resolves). It still
carries the sim-side `ProvinceId` / geometry-side `cell_id` coexistence
named in that ADR — `sim/` and `pipeline/geo/` are not yet the data source
it reads from.

**The one supported way to open it**: launch the Unity 6000.0.43f1 editor
with

```
-projectPath unity/game_unity -openfile unity/game_unity/Assets/Scenes/Main.unity
```

`unity/open-game.ps1` wraps this exact invocation. There is no other
supported entry point: VictoriaProject's own `automation/demo.py`,
`automation/run_queue.py`, and its `cursor_tasks/`/`runtime_bridge/`
queue-and-lock machinery were deliberately **not** ported (see ADR-0004) —
ForgeHistory's three-role harness (`harness/queue/briefs/`,
`harness/verdict_audit.py`) is the only automation layer in this
repository. This README does not assert a single "it's green" headline for
the ported project — see brief `003-port-unity-game`'s
`deliverables/generator-log.md` for the actual, freshly-measured state: the
compile and capture proofs are green; the EditMode suite VictoriaProject
itself actually maintained as its acceptance bar (reconstructed by proof,
not a fixed filter file) is 100% green from this location; the raw,
unfiltered `-testPlatform EditMode` run has 8 individually-attributed
failures — 7 frozen, legacy fixtures predating a deliberate,
VictoriaProject-documented parity-breaking change (`v1_090`), left red on
purpose rather than weakened or rebased, and 1 (`V1095GpuMapTests`) whose
failure is a pre-existing, VictoriaProject-documented invocation
requirement (`-nographics` disables the GPU path it asserts on), not a port
defect. VictoriaProject's own unfiltered run of the same suite exists and
was found (`C:\Users\liagr\VictoriaProject\game_unity\Logs\testresults_full.xml`,
2026-07-28 18:17:32Z): **same total, 274; same 7 failing `fullname`s,
byte-for-byte; same single skipped case, `V1015CollapseDiagnostic`.** This
port reproduces VictoriaProject's own unfiltered result case for case — the
strongest evidence available that the port introduced zero regressions and
that the 7 legacy reds were already red upstream, not a claim resting on
date correlation alone.

## `game_unity/Logs/` — two bridged historical artifacts, same discipline as the sandbox bridge below

`unity/game_unity/Logs/v1_041_tests.xml` and `v1_077_large.xml` are bridged,
read-only, from VictoriaProject's own local disk (`brief 003-port-unity-game`,
amendment-003, corrected Cluster B) — these two files are what
`V1042SuiteBudgetTests`'s 5 methods actually read, and they were never part
of any VictoriaProject commit (`game_unity/Logs/` is wholesale gitignored
there; amendment-002's original assumption that they were committed proof
archives was checked and found wrong, corrected in amendment-003). Like the
`sandbox/geo/artifacts/` bridge below, this is additive to `Logs/`'s
regenerable-cache status (Success Condition 1's exclusion of `Logs/` is
unaffected) and is not a durable ForgeHistory data source — it exists only
so the already-ported, unmodified test methods can read what they already
expect.

## `sandbox/geo/artifacts/` — a temporary, read-only test bridge, not a second geo-pipeline

`unity/sandbox/geo/artifacts/coordinate_correction_proposal_v1_072.json`
exists **solely** so that `V1037CityPlacementTests`/`V1080CoordinatesTests`
(already-ported, unmodified) can read the file they already expect at
`Path.Combine(GameUnityRoot, "..", "sandbox", "geo", "artifacts", ...)` —
byte-identical to VictoriaProject's own `sandbox/geo/artifacts/` copy
(`brief 003-port-unity-game`, amendment-002, Cluster A). It is **not** a
second, competing geo-artifact source and is **not** the same tree as
`pipeline/geo/` (brief 002 ported only G2/coastline there; VictoriaProject's
`sandbox/geo/artifacts/` holds many more artifacts from later pipeline
steps that have no `pipeline/geo/` equivalent yet). This bridge is temporary
by design: `harness/queue/geo-pipeline-port-plan.md`'s later brief slots
(G3 cells onward) are what will eventually regenerate this artifact's
equivalent inside `pipeline/geo/` on ForgeHistory's own terms; a
non-divergence test between the two trees is future work for that time, not
created by this brief.
