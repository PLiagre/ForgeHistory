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

## Status (verified 2026-08-08, end of session, live command output)

- `py -m pytest harness/tests/ -q` — **261 passed, 1 failed**. The single
  red is `test_run_unity.py::test_no_brief_prescribes_polling`, **pre-existing
  and unrelated to this session** — it was already red on `origin/master`
  (`198cfd9`) before any brief-008 work began (verified at session start).
  Needs its own investigation (see Open TODOs); not introduced here.
- `py harness/harness_audit.py` — **23/24**. The one FAIL
  (`no_premature_stub_content`) is the audit tool being stale, not the repo
  being wrong: it still assumes `pipeline/geo/`, `unity/` are empty stubs,
  which briefs 002/003 legitimately un-stubbed through the gate. Fix the
  audit's assumption (see Open TODOs) — do not "clean" the dirs.
- **Audit CURSOR-5633ee7-automation-completeness — full loop closed this
  session**: `PROPOSED → CHALLENGED (be86205) → APPROVED (f3a7056) →
  CONVERTED (c4ec462)`. Ledger: `architecture/audit-ledger.jsonl`. Review:
  `architecture/reviews/CLAUDE-CURSOR-5633ee7-automation-completeness.md`
  (5/5 CONFIRMED; ARCH-005 not retained, redundant with CURSOR-6231186).
- **Brief 008 (`harness/queue/briefs/008-full-auto-automation-gaps`) —
  NEEDS_SPLIT, lot 008a Générateur DONE, gate + Évaluateur NOT run**
  (deferred at owner's stop request). Lot 008a self-check
  `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`
  = **7/10** at Générateur handoff; the 3 open rows are expected
  (`verdict.md` is the Évaluateur's file, and `declared_files_are_tracked`
  wanted a commit — now committed in `6292e16`). Re-run the gate next session.

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

- [ ] **Run gate + Évaluateur on brief 008 lot 008a** (first thing next
      session): `py harness/verdict_audit.py
      harness/queue/briefs/008-full-auto-automation-gaps` then a fresh
      Évaluateur pass. Lot 008a fixes the real P0 incident (orchestrator
      trigger re-acting on a terminal audit — run `31085883052`); the fix is
      `harness/pipeline/trigger_resolve.py` (reads the ledger, excludes
      terminal audits) + `pipeline-orchestrate.yml` + 12 tests in
      `harness/tests/test_trigger_resolve.py`.
- [ ] **Generate brief 008 lot 008b** (ARCH-003, independent of 008a): a
      `pipeline_job_failed` policy rule in `auto_policy.yaml` + orchestrator
      handler + a `workflow_run` trigger over the four `pipeline-*.yml`.
      Fully specified in `008-full-auto-automation-gaps/brief.md` (SC7–SC11).
      `/forge-run` it as its own fresh session.
- [ ] **Brief 008 lot 008c is BLOCKED on an owner decision** (ARCH-002 +
      ARCH-004): which agent/API to wire first, whether `mode: full_auto`
      should be renamed/split (`full_auto_decision_only`), and what recurring
      per-invocation LLM budget in CI is acceptable. Not specified until the
      owner decides — a fresh Planificateur pass follows that decision. The
      owner-decision list is in
      `architecture/decisions/DECISION-CURSOR-5633ee7-automation-completeness.md`
      and the audit's §8.
- [ ] **ARCH-005 (budget blind to Cursor backend)** was NOT retained for
      brief 008 — it is redundant with `CURSOR-6231186` FINDING-ARCH-003,
      already an open item. Track it there, not as a new brief.
- [ ] **Full-auto agent pipeline (brief 006)**: merged to `master` (PRs #6/#8/#10).
      See `docs/adr/0006-full-auto-agent-pipeline.md` and
      `docs/rules/full-auto-pipeline.md` for the derogation, the roles, and
      how to activate/emergency-disable `mode: full_auto`. NB brief 008a/008b
      harden exactly this machinery.
- [ ] **Investigate pre-existing red `test_no_brief_prescribes_polling`**
      (`harness/tests/test_run_unity.py`) — red on `master` before this
      session; a brief's text apparently prescribes log-polling. Find which
      brief and correct it (or the test) with evidence.
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

## Last Session Summary (2026-08-08)

A full turn of the Cursor audit loop, then implementation of its highest-
priority finding — all through the three-role harness, local commits only.

1. **Synced the branch**: `forge/cursor-audit-loop` was far behind
   `origin/master` (`198cfd9`, PRs #9/#10 merged since); fast-forwarded so
   the new audit's inbox file was present locally.
2. **Reviewed audit `CURSOR-5633ee7-automation-completeness`** (Cursor's
   "what's left for the full-auto loop to be truly complete" report).
   Challenge = **5/5 CONFIRMED, 0 refuted**. The headline P0 was verified
   *live*, not from code: `gh run view 31085883052` is a real `FAILURE`
   (exit 2) — `pipeline-orchestrate` replayed a transition on an already
   `AUDIT_ARCHIVED` audit. Flagged ARCH-005 as double-counting a prior
   audit; noted the ledger's naive verdict-tally (counts keyword mentions
   anywhere — real verdicts were 5/0/0, ledger says 7/2/2/3).
3. **Owner APPROVED, retaining ARCH-001/002/003/004** (dropped 005), and
   **converted** to brief seed `008-full-auto-automation-gaps`.
4. **Planificateur filled brief 008 → NEEDS_SPLIT**: lot 008a (ARCH-001,
   ready), lot 008b (ARCH-003, ready), lot 008c (ARCH-002+004, deliberately
   left unspecified — blocked on an owner product decision).
5. **Générateur built lot 008a** (96 tool calls, under budget): new
   `harness/pipeline/trigger_resolve.py` consults the existing ledger reader
   and excludes terminal audits before building any payload; the ~27 lines
   of inline decision-bash in `pipeline-orchestrate.yml` replaced by a call
   to it; 12 new tests including the exact incident-shape regression (SC3
   zero-transition) and a non-blanket-skip guard (SC4). `audit_decision.py`'s
   FSM guard left untouched (SC6). Gate self-check 7/10 at handoff (3 rows
   are the Évaluateur's / needed the commit).
6. **Fixed a real regression before closing**: the Planificateur's
   `eval-rubric.md` and the Générateur's `generator-log.md` had restated
   brief.md's `## Non-Goals` heading, breaking
   `test_single_source_of_instruction.py` (green at convert time, red after).
   Corrected both; suite back to 261 passed / 1 pre-existing red.

Gate + Évaluateur on 008a, and generation of 008b, were **deferred at the
owner's stop request**. This file rewritten from live output. **Nothing
pushed.** Commits this session: `be86205`, `f3a7056`, `c4ec462`, `ed6de66`,
`6292e16`, `c07e7f5`.
