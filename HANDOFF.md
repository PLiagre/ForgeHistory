# HANDOFF.md

State-of-play. Rewritten at the end of every session (via `/forge-checkpoint`)
— not a changelog, but "what a new agent needs to pick up exactly where the
last session left off."

## Current Milestone

F0 done (see git history for its proof trail). **F1 in progress, and the
repository is no longer stubs**: the owner arbitrated the `FORGE-HISTORY-BRIEF.md`
§9 question on 2026-07-31 — "récupérer le code existant fonctionnel de
VictoriaProject ; les harnais et contrôles sont ceux de ForgeHistory" — and
VictoriaProject's working Unity game now lives, compiles, tests and captures
from `unity/game_unity/`. Recorded in
[ADR-0004](docs/adr/0004-bulk-port-victoriaproject-unity-game.md) (which also
names the imported double-primary-key debt; ADR-0003 remains the target).

## Status (verified 2026-07-31, end of session, live command output)

- `py -m pytest harness/tests/ -q` — **16 passed**.
- `py harness/harness_audit.py` — **23/24**. The one FAIL
  (`no_premature_stub_content`) is the audit tool being stale, not the repo
  being wrong: it still assumes `pipeline/geo/`, `unity/` are empty stubs,
  which briefs 002/003 legitimately un-stubbed through the gate. Fix the
  audit's assumption next session (see Open TODOs) — do not "clean" the dirs.
- `py harness/verdict_audit.py harness/queue/briefs/003-port-unity-game` —
  **ACCEPT 9/9**, Évaluateur verdict **PASS**
  (`harness/queue/briefs/003-port-unity-game/verdict.md`), its feedback-001
  closed by Générateur iteration 5.
- `py harness/verdict_audit.py harness/queue/briefs/004-polish-visuel` —
  **REJECT**, for exactly two reasons, both understood and neither a
  work defect: (1) `verdict.md` doesn't exist — the brief 004 Évaluateur
  pass has NOT run yet (deliberately deferred at the owner's stop request);
  (2) the Planificateur future-dated `brief.md`/`eval-rubric.md`
  (`Authored: 2026-08-01T11:00:00` while the session clock was 2026-07-31),
  so `mtime_after_brief`/`rubric_predates_deliverables` fail against every
  deliverable. The Générateur refused to fabricate timestamps to route
  around this — correct behavior. Fix path is in Open TODOs.

## What Exists (delta this session — 2026-07-31)

Four commits: `fce1d82` (brief 002 baseline), `8fb4bef` (brief 003 port),
`0f05936` (brief 003 iteration 5), plus this session's closing commit.

- **`unity/game_unity/`** — the full VictoriaProject game at its HEAD
  (v1_095b): 6 player verbs, GPU map, medieval-dark UI (v1_053–055),
  855 files + 77 dirty-working-tree files restored to HEAD + 9
  PresentationCache tracked files (4 unit sprites) restored after Évaluateur
  feedback. Compile green from this location; conquest capture pair
  regenerated fresh here. Whole tree verified blob-by-blob against
  VictoriaProject HEAD: 0 missing. VictoriaProject itself proven untouched
  (sentinel hashes, 3 checkpoints). Launcher: `unity/open-game.ps1`,
  Unity 6000.0.43f1.
- **Test truth for the ported game** (full detail:
  `harness/queue/briefs/003-port-unity-game/deliverables/generator-log.md`):
  reference suite green; full EditMode suite 274 cases with **7 legacy reds,
  individually attributed** (hardcoded anchors predating the v1_090 rebase,
  files untouched since 2026-07-23, same 7 red in VictoriaProject's own
  `testresults_full.xml`) — left as-is, never weakened. `V1095GpuMapTests`
  needs an invocation **without `-nographics`** (proven: 99.6% CPU/GPU
  agreement with graphics, blind without).
- **Two read-only bridges** from VictoriaProject local disk, SHA256-declared:
  `unity/sandbox/geo/artifacts/coordinate_correction_proposal_v1_072.json`
  and 2 XML baselines in `unity/game_unity/Logs/` — temporary until
  `pipeline/geo/` catches up (geo port plan briefs 003–005).
- **Brief 004 (polish visuel) — Générateur DONE, Évaluateur NOT run.**
  Real fix delivered: debug-token leak (`HOVER Île-de-France` in the normal
  banner) now hidden by default, reachable via `--debug-ids`, proof pairs
  SHA256-distinct. Accents and French decimals investigated honestly:
  already fixed upstream (v1_073 `FoldDiacriticsToAscii`), sample_size=11,
  no fake fix applied. Reference suite still green after changes. Artistic
  verdict recorded as `A_REVOIR_HUMAINEMENT` — the owner judges "beau".
  Galleries: `unity/game_unity/Captures/v004_*/`.
- **Brief 003 ran with 3 brief amendments + 1 feedback cycle** — including
  amendment-003 explicitly correcting amendment-002's wrong diagnosis. The
  drift found on the way: VictoriaProject's working tree was dirty
  (uncommitted, non-compiling v1_096 work) while its HANDOFF claimed clean.

## Open TODOs

- [ ] **Cursor audit loop (ADR-0005) — review & merge PR #4**: branch
      `forge/cursor-audit-loop` carries the multi-agent audit loop under
      `architecture/` (steps 1–11: skeleton, ledger, the seven
      `/forge-audit-*` commands, and three CI workflows). Local commits only
      until reviewed; CI is green on the PR. Contract in
      `architecture/README.md`.
- [ ] **Run the brief 004 Évaluateur pass** (first thing next session):
      before it, the Planificateur must correct its own future-dated
      `Authored:` fields in `004-polish-visuel/{brief.md,eval-rubric.md}`
      to the true authoring time (real file mtimes were 2026-07-31 ~20:3x —
      cite evidence, don't invent), via an explicit amendment note, then
      gate + Évaluateur normally.
- [ ] **Owner judges the visual polish**: look at
      `unity/game_unity/Captures/v004_after_default/` and pronounce on
      `A_REVOIR_HUMAINEMENT` (the harness never self-adopts "beau").
- [ ] **Fix `harness_audit.py`'s stale stub assumption** (23/24) — it must
      learn that `pipeline/geo/` and `unity/` are legitimately populated.
- [ ] **Map label upside-down orientation bug** — found by the brief 004
      Générateur, deliberately left untouched as out-of-scope; documented in
      `004-polish-visuel/deliverables/generator-log.md`. Needs its own brief.
- [ ] Geo pipeline port continues: briefs 003–005 of
      `harness/queue/geo-pipeline-port-plan.md` (G3 cells onward); the two
      read-only bridges above get retired when it lands.
- [ ] `forge-run.md` phase-order doc mismatch (carried from previous
      session, still true).
- [ ] VISION.md dead internal links (carried, still true; ADR needed).
- [ ] No human-facing README.md (carried, still true).
- [ ] Tier-3 adversarial gate not built (carried, intentional).

## Known Risks

- Never fabricate VictoriaProject content beyond what was actually read.
  VictoriaProject is read-only for this repo — proven untouched this
  session; keep it that way.
- **VictoriaProject's own HANDOFF lies about its tree state** (says clean,
  tree is dirty with non-compiling v1_096 work). Any future re-sync must
  target HEAD, never the working tree.
- The 7 attributed legacy reds must stay red-and-attributed until someone
  deliberately retires or rebases them with the three-part proof
  (understood / documented / proven-deliberate). Never "fix" them casually.
- Générateur subagents must never git commit (rule held this session; one
  Générateur even correctly reported the orchestrator's own mid-brief
  commit rather than hiding it).
- Sonnet subagents running long Unity batchmode jobs tend to end their turn
  "to wait" (had to be re-prompted twice on 2026-07-31). The old fix —
  telling them to re-check the `-logFile` every 30-60 s — cured the symptom
  and created the bill: each re-check is a separate API request carrying the
  agent's whole accumulated context, and one measured Générateur spent 586
  tool calls on `wc -l` of a single log file. **Use `unity/run-unity.ps1`
  instead**: it waits inside one PowerShell process and returns exactly once
  (see `unity/README.md`). Short jobs go in the foreground with
  `-TimeoutSec`; long ones (a first `Library/` rebuild) go through the Bash
  tool's `run_in_background`, which re-invokes the agent on exit — a
  notification, not a poll. Never re-read a Unity log across tool calls.
- `pytest` installed via `--user`, not vendored (carried).

## Last Session Summary (2026-07-31)

The owner relaunched the project with one instruction: recover
VictoriaProject's working code under ForgeHistory's harness, aiming for a
beautiful, functional game by session end, autonomously. Delivered through
the full three-role harness (2 Planificateur briefs, 3 amendments, 6
Générateur iterations across 2 briefs, 1 Évaluateur pass):

1. Committed the pending verified baseline (briefs 001+002).
2. **Brief 003 — the port — gate ACCEPT 9/9, Évaluateur PASS**: the whole
   game now lives in `unity/game_unity/`, compiling, testing green on its
   reference suite, and regenerating real conquest captures from
   ForgeHistory. En route: caught VictoriaProject's dirty-tree lie,
   restored to HEAD, attributed every one of 8 red tests instead of
   hand-waving them (7 legacy anchors, 1 `-nographics` environment proof).
3. **Brief 004 — polish — Générateur complete, Évaluateur pending**: debug
   leak fixed with before/after proof; accent/decimal "defects" proven
   already-fixed rather than fake-fixed; artistic verdict left to the human.
4. Closed the session at the owner's request: single-source-of-instruction
   test violation (generator logs restating brief headings) fixed
   mechanically and transparently, 16/16 green, this file rewritten from
   live output, everything committed locally. **Nothing pushed.**
