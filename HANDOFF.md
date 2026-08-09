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

## Status (verified 2026-08-09, end of session, live command output)

- `py -m pytest harness/tests/ -q` — **269 passed, 1 failed**. The single
  red is `test_run_unity.py::test_no_brief_prescribes_polling`, **pre-existing
  and unrelated to this session** — red on `origin/master` before any
  brief-008 work began, verified twice by the Évaluateur (once in a detached
  worktree at `origin/master`). Offender is brief 007's
  `deliverables/checkpoint-002.md:291`. Needs its own investigation (see Open
  TODOs); not introduced here. NB `origin/master` is now `32640da` (PR #11),
  not `198cfd9` as the previous checkpoint said.
- `py harness/harness_audit.py` — **23/24**. The one FAIL
  (`no_premature_stub_content`) is the audit tool being stale, not the repo
  being wrong: it still assumes `pipeline/geo/`, `unity/` are empty stubs,
  which briefs 002/003 legitimately un-stubbed through the gate. Fix the
  audit's assumption (see Open TODOs) — do not "clean" the dirs.
- `py harness/verdict_audit.py harness/queue/briefs/008-full-auto-automation-gaps`
  — **10/10, VERDICT: ACCEPT**.
- **Brief 008 — lot 008a ACCEPTED, lot 008b generated (Évaluateur not yet
  run), lot 008c unblocked but not yet specified.**
  - **008a**: `LOT_008a: ACCEPT` after one REJECT→fix→re-accept cycle. The
    iteration-1 REJECT is preserved in `verdict.md`, not sanitized away.
  - **008b**: Générateur DONE this session, **gate ACCEPT, Évaluateur NOT
    run** — that is the first thing next session.
  - **008c**: the three owner questions that blocked it are **answered**
    (see below). A fresh Planificateur pass converts them into a real lot.
- **Audit CURSOR-5633ee7-automation-completeness** — loop closed in the
  previous session: `PROPOSED → CHALLENGED (be86205) → APPROVED (f3a7056) →
  CONVERTED (c4ec462)`. Ledger: `architecture/audit-ledger.jsonl`.

## Owner product decision (2026-08-09) — lot 008c is no longer blocked

Recorded in full, with rationale, at the end of
`architecture/decisions/DECISION-CURSOR-5633ee7-automation-completeness.md`.
Do not paraphrase it into a brief — a fresh Planificateur pass reads it there
and writes the lot. Summary of what was decided:

1. **First agent to wire: `claude-challenger`** (`pipeline-challenge.yml`),
   then cursor-auditor, then forge-run last.
2. **`mode: full_auto` gets split** into `full_auto_decision_only` (audit →
   challenge → owner decides) and `full_auto` (reserved until forge-run is
   really wired), with a fail-closed migration.
3. **Recurring CI budget accepted**: 5 $/invocation for challenge,
   50 $/invocation for forge-run, 200 $/month cumulative; on breach the CI
   flips itself to `mode: manual` via the existing kill-switch.

Calibrated on measured cost, not estimates (`py harness/backends/ledger.py
tokens`): full brief-008 loop = 13.37 $; Générateur median ≈ 20–45 $; worst
observed Générateur = 119.96 $ over 982 calls (the brief-003 Unity port —
atypical in scope and hit by the since-fixed log-polling bug). The measured
cost lever is **mean context per call** (371 K on that outlier), not call
count.

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

- [ ] **Run the Évaluateur on brief 008 lot 008b** (first thing next
      session). The mechanical gate is already ACCEPT (10/10) — that is
      necessary, not sufficient; it cannot see whether the four 008b counters
      are honest. Judge SC7–SC11 and the four `pipeline_job_failed_*` /
      `pipeline_workflow_run_trigger_coverage_count` /
      `run_31085883052_style_escalation_regression_count` counters only; SC1–SC6
      are lot 008a and already accepted. Scrutinise in particular the
      `actionlint` waiver (the tool is genuinely absent — the Générateur
      applied the brief's prescribed substitute and noted, honestly, that
      PyYAML *is* importable here contrary to the waiver row's own claim).
- [ ] **Convert the owner's 008c decision into a real lot** — a fresh
      **Planificateur** pass (never a Générateur straight away). Read the
      decision at the end of
      `architecture/decisions/DECISION-CURSOR-5633ee7-automation-completeness.md`.
      Scope for the first lot: wire `claude-challenger` in
      `pipeline-challenge.yml`, split `mode: full_auto` into
      `full_auto_decision_only` + `full_auto` with a fail-closed
      `policy_loader` migration, and enforce the 5 $ / 50 $ / 200 $ caps.
      Note this lot legitimately touches `config.yaml` and ADR-0006, which
      brief 008's own non-goals forbid — so it is a **new brief**, not an
      amendment to 008.
- [ ] **008a follow-up recorded by the Évaluateur** (not blocking, it
      ACCEPTed): `test_resolve_payload_with_no_audit_id_passes_through_unguarded`
      asserts pass-through but not that *no transition is reachable* for a
      payload carrying no `audit_id`. The Évaluateur proved that property
      itself (32 adversarial dispatches, zero transitions); per hard-won rule
      9 the proof belongs in the suite, not in a verdict. Also: the counter
      script `deliverables/measure_ledger_consult_paths.py` finds branches by
      `ast.unparse(...) == "in_payload"`/`"in_audit_id"`, so a future fourth
      branch would be silently skipped rather than flagged.
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

## Last Session Summary (2026-08-09)

Closed lot 008a through a real REJECT→fix→ACCEPT cycle, generated lot 008b,
and took the owner decision that unblocks 008c. Local commits only.

1. **Gate + Évaluateur on lot 008a → REJECT, on a real defect.** The
   Générateur had scoped the counter `ledger_consult_before_transition_paths_count`
   to `resolve_push()` alone and disclosed the narrowing honestly. The
   Évaluateur refused it anyway, and was right: `resolve()`'s two
   `workflow_dispatch` branches (`--payload`, `--audit-id`) returned a
   non-empty `event=` with **no ledger read at all**. Proven live, not by
   reading code — `--audit-id CURSOR-FIXTURE-full-auto-demo` (the actual
   `AUDIT_ARCHIVED` audit from incident `31085883052`) still produced
   `event=review_recorded`. Two of three routes still handed the orchestrator
   the incident's exact mechanism. Its own detector was blind to this: it
   only recognised `ast.Constant` event values, and those branches pass an
   `ast.Name`.
2. **Iteration 2 fixed the code, not the description of the code.** All three
   `resolve()` branches now consult `audit_ledger.current_state_for` and skip
   terminal audits with a `::notice::`; the detector was rewritten as a
   committed script that treats any non-Constant value as capable. Red-first
   proof: pointed at iteration 1's committed code it prints `gated=1
   capable=3`, at iteration 2 `gated=3 capable=3`. 5 new tests, 17 total, all
   12 originals unmodified.
3. **Évaluateur re-pass → `LOT_008a: ACCEPT`**, verified independently: 32
   adversarial dispatches across 8 event kinds × 4 payload shapes (zero
   transitions), end-to-end through the real CLI subprocess against the live
   ledger, and a second red-first probe isolating the two new tests precisely
   to the fix. It accepted the remaining "no `audit_id`" exemption as a
   genuine structural one — `_require()` fails closed on absent/empty/null —
   while noting the Générateur asserted that in prose and the Évaluateur is
   the one who proved it.
4. **Lot 008b generated** (SC7–SC11): `pipeline_job_failed` rule in
   `auto_policy.yaml`, `handle_pipeline_job_failed` in `orchestrator.py`, and
   a new `pipeline-failure-escalate.yml` triggering on `workflow_run`
   `conclusion: failure` across all four `pipeline-*.yml`. 3 new tests
   including the incident-shaped SC10 regression. Gate **10/10 ACCEPT**;
   **Évaluateur not yet run**.
5. **Owner decided the three 008c product questions** — challenger first,
   `full_auto` split in two, and a 5 $ / 50 $ / 200 $ CI budget calibrated on
   the ledger's real measured costs rather than a guess.
6. **Fixed a live regression inherited from the last checkpoint**: `e3cc258`
   (the HANDOFF rewrite itself) had reintroduced a verbatim brief heading and
   turned `test_single_source_of_instruction.py` red on this branch. The
   Évaluateur's own two new files then added two more offenders. All three
   reworded; the test is green and no finding or number was removed.

Suite: **269 passed, 1 pre-existing red**. **Nothing pushed.** Commits this
session: `1beaa6d`, `c5f35ea`, `9a6ce32`, plus this checkpoint.
