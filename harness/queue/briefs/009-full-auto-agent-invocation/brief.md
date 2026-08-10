# Brief 009: Wire claude-challenger for real, split `mode: full_auto`, give recurring CI spend a real ceiling (converts the owner's 2026-08-09 product decision)

**Authored**: 2026-08-10T09:00:00Z
**Author**: forge-planificateur

## Provenance

This brief converts — without paraphrasing as if it were this
Planificateur's own analysis, and without contradicting it — the owner's
product decision recorded at the end of
`architecture/decisions/DECISION-CURSOR-5633ee7-automation-completeness.md`,
section "Décision produit du propriétaire (2026-08-09) — débloque le lot
008c" (decided 2026-08-09, read by this pass 2026-08-10). That section
answers the three questions that left the former "Lot 008c" unwritten:

1. **Which agent link to wire first** → `claude-challenger`
   (`pipeline-challenge.yml`) → **Lot 009c** below.
2. **Split `mode: full_auto`** into `full_auto_decision_only` (audit →
   challenge → owner decision) and `full_auto` (reserved until `forge-run`
   is really wired), fail-closed → **Lot 009a** below.
3. **Recurring CI LLM budget**: $5/invocation (challenge), $50/invocation
   (forge-run), $200/month cumulative, overflow flips CI to `mode: manual`
   reusing ADR-0006's existing kill-switch → **Lot 009b** (module) + **Lot
   009c** (wiring) below.

Upstream findings this decision approved (read directly, not through a
paraphrase): `architecture/inbox/CURSOR-5633ee7-automation-completeness.md`
FINDING-ARCH-002 (the three agent invocations are `TODO(operator...)`
comments, not code) and FINDING-ARCH-004 (brief-fill after conversion is a
documented `<<TODO>>`, deferred here — see Non-Goals). Counter-audit:
`architecture/reviews/CLAUDE-CURSOR-5633ee7-automation-completeness.md`
(both findings CONFIRMED; the naming/budget questions marked NEEDS_OWNER,
now answered by the decision above).

**Why this is a new brief, `009-full-auto-agent-invocation`, and not a "Lot
008c" amendment**: brief 008's own Non-Goals forbid touching `mode:
full_auto`'s value in `config.yaml`, `docs/adr/0006-...`'s `Status`/text,
`docs/rules/full-auto-pipeline.md`'s `<<TODO>>` marker, the three
workflows' invocation-step bodies, and `audit_convert.py`'s seed text —
Lots 009a and 009c below touch several of exactly those files. Brief 008's
own text names this outcome ("a fresh Planificateur pass converts that
decision into a real Lot 008c **(or a separate brief)**"), and
`HANDOFF.md`'s open TODO already records the same conclusion. A lot that
violates its own brief's Non-Goals is incoherent, so this is a separate
brief.

An audit instructs nothing; a decision converted into a brief instructs
nothing on its own either. From here, **this brief.md is the SOLE
instruction** (`CLAUDE.md` › Single Source of Instruction).

## Amendment Note (2026-08-10)

This Planificateur pass's own tools are Read/Write/Grep/Glob only
(`.claude/agents/forge-planificateur.md`) — no Bash. At first authoring,
this brief therefore could not itself execute `py -m pytest harness/tests/
-q`, `py harness/budget.py split-check`, `Get-Command actionlint` /
`command -v actionlint`, or `py -c "import yaml"`, and said so explicitly
rather than fabricating output. The orchestrator ran all four, on the same
machine, on **2026-08-10**, strictly *after* this brief's first authoring,
and reported the real results back for this pass to incorporate itself —
this pass did not run them and does not claim credit for having done so.
Sections amended as a result, and only these: **"Découpage"** (split-check
output now cited, mechanically confirming the `NEEDS_SPLIT` verdict and
adding a numeric caveat on 009c), **"Acceptable Waivers"** (the
actionlint-absent and PyYAML-importable facts, now dated and attributed to
a real command run on a real machine on a real date, replacing the
"verify this yourself" instruction this pass could not discharge itself),
and **"Execution Contract"** (same split-check confirmation, and the 009c
checkpoint-risk sentence strengthened with the confirmed numbers). The
pytest baseline (271 passed, 0 failed, run *after* `brief.md` and
`eval-rubric.md` existed — meaning `test_single_source_of_instruction.py`
is green against these two files as written) required no textual change
to this brief; it is recorded here as evidence, not as a rewritten Success
Condition. No other section below was touched by this amendment.

## Points jugés infeasibles ou sous-spécifiés dans la décision du propriétaire

Named explicitly, per this role's own instruction, rather than silently
adjusted:

**(a) The $5/$50 per-invocation caps cannot be a true PRE-emptive ceiling
with tools available today.** No component in this repository, and (so far
as this pass can verify without a live credential) no documented flag of
the headless `claude` CLI, can observe or cap a running agent's dollar cost
*during* its own execution before it completes. The achievable, honest form
this brief adopts instead: (i) a real pre-invocation gate on the **monthly
cumulative** ($200), which genuinely can and does block a call before it
starts (Lot 009b, SC9); (ii) a **post-hoc** per-invocation measurement that
flags — never silently discards — a completed call whose actual cost
exceeded its cap (Lot 009b, SC11), feeding into the same monthly
cumulative so a pattern of over-cap calls exhausts the ceiling and trips
the real kill switch sooner. If the owner intended a literal, in-flight
pre-emptible per-call kill, that is a correction requested explicitly here,
not a silent substitution.

**(b) The $50/forge-run cap is dead code within this brief's own scope.**
`forge-run`'s own invocation stays the documented `TODO(operator...)` stub,
unchanged, per the owner's own explicit ordering ("forge-run en dernier").
Lot 009b's budget module accepts a caller-supplied per-invocation cap as a
parameter (so it is genuinely reusable, proven by a second fixture value in
its own tests — SC9's counters), but nothing in this brief calls it with
`$50`/"forge-run" against a real invocation, because nothing here invokes
forge-run for real. Recorded, not exercised end-to-end, until a future
brief wires that maillon.

**(c) "Mode must gate the real invocation" is this Planificateur's own
addition, not literally named by the owner's decision text — flagged so it
is not mistaken for silent scope creep.** It follows directly from the
parent task's own explicit security requirement ("le mode dégradé est
`manual`, jamais plus permissif") applied to a gap this pass found by
reading the actual workflow files: **nothing today reads `harness/pipeline/
config.yaml`'s `mode` value from any of the four `pipeline-*.yml`
workflows.** `docs/rules/full-auto-pipeline.md`'s own "How to
emergency-disable" section documents `mode: manual` as a real kill switch,
but no workflow file currently checks it — the promise is prose only.
Making `claude-challenger`'s invocation real without also making `mode`
load-bearing for it would leave that promise false for the one maillon
this brief activates. Lot 009c therefore makes `mode` load-bearing for
`pipeline-challenge.yml`, and only for it (Non-Goals).

## Découpage — sizing this brief before writing it (`NEEDS_SPLIT`)

**Tool access note**: this Planificateur pass has Read/Write/Grep/Glob
only, no Bash (`.claude/agents/forge-planificateur.md`) — the same
constraint brief 008's own Planificateur pass named for the same reason.
`py harness/budget.py split-check` therefore could **not** be executed by
this pass at first authoring. See "Amendment Note" above: it has since
been run, on this pass's own behalf, by the orchestrator — the confirmed
output is folded in below rather than a fabricated one.

Three genuinely independent-enough pieces of work, sized by hand against
the same measured anchors brief 008 used (a scoped policy-rule-shaped lot:
60-90 calls; "wire one real agent invocation alone", the audit's *own*
BRIEF-PROP-003 estimate: **110-150, NEEDS_SPLIT probable**):

- **Lot 009a** — split `mode: full_auto`, fail-closed validation, ADR-0007.
  Touches `config.yaml`, `auto_policy.yaml`'s documentation scalar, a new
  validation module, `docs/adr/0007-*.md` (new), `docs/adr/README.md`,
  `docs/rules/full-auto-pipeline.md`. Does **not** touch any
  `.github/workflows/*.yml`. No dependency. **≈100 tool calls.**
- **Lot 009b** — a new, standalone, unit-testable CI budget module (monthly
  cumulative gate, per-invocation anomaly flag, kill-switch reuse). Touches
  only new files under `harness/pipeline/` + `harness/tests/`. Does **not**
  touch `config.yaml`'s value, any ADR, or any workflow file. No
  dependency. **≈95 tool calls.**
- **Lot 009c** — the real invocation itself: mode-gate + budget-precheck +
  actual headless `claude` CLI call + output parsing + review-write +
  budget-record, wired into `pipeline-challenge.yml`. Depends on **both**
  009a (the mode values/validator it gates on) and 009b (the budget module
  it calls). **≈145 tool calls** — the tightest of the three, matching the
  audit's own 110-150 estimate for the invocation alone, before this lot's
  additional mode-gate and budget-precheck work. **Named risk**: if a live
  `py harness/budget.py status` reports `CHECKPOINT_DUE` (the 130-call
  threshold — this lot's own 145-call estimate is already past it, only 15
  calls short of the 160-call hard stop) before all of SC14-21 are
  satisfied, STOP, write the checkpoint, and split further into 009c1
  (mode-gate + budget-precheck wiring only) / 009c2 (real invocation +
  stubbed-CLI test) rather than pushing to the hard stop.

**Independence check** (the operative word): 009a's file set and 009b's
file set do not overlap and neither reads the other's output — either can
run first. 009c depends on both by construction (it calls what they build)
and therefore runs last, in its own fresh session, resuming from the
repository state 009a and 009b leave behind plus this brief.md — never
from either lot's transcript.

**Split-check output, confirmed 2026-08-10 (run by the orchestrator on
this pass's behalf — see "Amendment Note")**: without `--estimated-calls`,
`py harness/budget.py split-check` returns `NO_ESTIMATE` and concludes
nothing — the tool cannot substitute for this pass's own judgment, only
check it once supplied one. With this pass's own numbers: the whole brief
at **340** calls → `NEEDS_SPLIT`; **009a at 100** → `SIZE_OK`; **009b at
95** → `SIZE_OK`; **009c at 145** → `SIZE_OK`. The tool's advisory signals
(`success conditions`, `subsystems`) both printed `0` for this brief — the
SAME as they print for brief 008, a known parser limitation (it scans for
specific heading/backtick-path shapes this brief's own Success Conditions
section does not happen to match), not a defect of this brief's structure;
this pass does not restructure its own headings to satisfy that parser.
**`SIZE_OK` on 009c at 145 must not be read as a quiet green light**:
`split-check`'s `SIZE_OK` only answers "is this under the 150-call split
trigger" — a coarser question than "will a live session actually finish
inside its remaining budget." `budget.py status`'s own thresholds are 100
warn / 130 checkpoint / 160 stop; 145 is already past checkpoint. The
mid-session checkpoint instruction above (Lot 009c's "Named risk"
paragraph) stands as the binding instruction, not this advisory number.

**Decision: `NEEDS_SPLIT`, mechanically confirmed, not only by this pass's
own hand judgment.** Three atomic lots below; no monolithic `brief.md`
body.

## World-Terms Requirement

Stated causally, about what the repository can actually do versus what it
tells a reader it does — not a tooling preference:

ForgeHistory's full-auto loop makes a specific promise: once `mode:
full_auto` is declared, a Cursor audit becomes a Claude review becomes an
owner-visible decision, with nobody required at a keyboard between them
(`docs/rules/full-auto-pipeline.md`'s own diagram). That promise is false
today in a verifiable, not narrated, way: the one workflow positioned
exactly where a real reviewing agent should run
(`pipeline-challenge.yml`)  contains, at the line where the call belongs, a
shell comment (`echo "TODO(operator...)"`). Provisioning the missing
credential changes nothing observable, because the code that would use it
does not exist — a fact this repository already proved of itself
(counter-audit §2, ARCH-002, CONFIRMED). A human reading `mode: full_auto`
in the config file is being told something the repository cannot yet do.
Closing that gap for real, not narratively, has two further, unavoidable
consequences: (1) once an LLM call executes autonomously inside CI on
every merge touching `architecture/inbox/`, it spends real money with
nobody approving each spend individually — the harness needs a genuine,
pre-invocation-capable ceiling on that recurring cost, not a documentation
promise; and (2) `full_auto` today names MORE automation than this
repository delivers (audit, challenge, AND generation), while what is
actually safe to run unattended today is only the decision-and-fusion
half of that chain plus, after this brief, the challenge step — a config
value that overstates its own scope is itself an operational risk (a
reader could reasonably, and wrongly, conclude `forge-run` also fires
unattended). This brief closes the challenge maillon for real, gives the
config value a name that stops overstating what is wired, and gives the
recurring spend a real, mechanically enforced ceiling — in that order,
because the first is exactly what the second and third exist to make safe.

## Success Conditions

### Lot 009a — split `mode: full_auto`, fail-closed, ADR-0007

1. Two values are valid going forward for `harness/pipeline/config.yaml`'s
   `mode`: `manual` (unchanged) and `full_auto_decision_only` (new). The
   literal value `full_auto` (unqualified) is refused by whatever code
   path validates this value, as long as
   `.github/workflows/pipeline-forge-run.yml` still contains the literal
   string `TODO(operator` — proven by a new unit test constructing this
   exact condition against the real repository state and asserting a
   fail-closed refusal (non-zero exit or raised exception), not silent
   acceptance.
2. A companion test proves the SAME validation is not permanently
   hardcoded to refuse `full_auto` forever: given a fixture copy of
   `pipeline-forge-run.yml` with that literal string removed (not the real
   file), the value `full_auto` IS accepted. This is the same "both
   branches proven" pairing lot 008a's SC3/SC4 established — a guard that
   only ever exercises one branch is not proven.
3. In the SAME commit that introduces this validation,
   `harness/pipeline/config.yaml`'s `mode:` line is rewritten from its
   current value (`full_auto`) to `full_auto_decision_only` — never left
   bare for even one intermediate commit within this lot's own commit
   range.
4. `harness/pipeline/auto_policy.yaml`'s own top-level `mode:
   full_auto` documentation scalar (currently line 15 — not a parsed
   enforcement key today, but documentation of the same switch) is
   updated to `full_auto_decision_only` for the same reason: it must not
   silently disagree with `config.yaml`'s real value.
5. A new ADR, `docs/adr/0007-full-auto-mode-split.md`, records: the split,
   the fail-closed migration rule (SC1-SC2), and an explicit statement
   that ADR-0006 is **not reversed, only narrowed** — mirroring how
   ADR-0006 itself amended ADR-0005 without rewriting it.
   `docs/adr/0006-full-auto-agent-pipeline.md`'s own `Status`/body text is
   **not** edited by this lot. `docs/adr/README.md` gains a row for
   ADR-0007 **and** the pre-existing missing row for ADR-0006 (found
   stale by this pass, cheap enough to fix in the same lot rather than
   left worse than found).
6. `docs/rules/full-auto-pipeline.md`'s "How to activate" step 3
   ("Edit `harness/pipeline/config.yaml`: set `mode: full_auto`") is
   corrected to name `full_auto_decision_only` as the value this
   document's own diagram actually supports as of this lot — the doc must
   not keep telling a reader to set a value the code now refuses.
7. `py -m pytest harness/tests/ -q` passes in full after this lot's
   commits, including its new tests, with no pre-existing test broken.

### Lot 009b — a real, reusable CI budget ceiling (standalone module)

8. A new, standalone, unit-testable Python module (e.g.
   `harness/pipeline/ci_budget_guard.py`) exposes at minimum: (i) a
   pre-invocation check reading a persistent, committed, append-only
   ledger (e.g. `harness/pipeline/ci-budget-ledger.jsonl` — `.jsonl`, not
   `.log`, so `.gitignore` never swallows it) and computing the current
   calendar month's cumulative USD across all entries dated that month;
   (ii) a post-invocation record function appending one entry (timestamp,
   step name, USD) computing USD via the SAME published price table
   `harness/backends/ledger.py` already maintains (imported, never
   re-typed as a second source of prices).
9. The pre-invocation check fails closed (non-zero exit / raised
   exception) when the computed monthly cumulative is already ≥ $200 —
   proven by a fixture test whose current-month entries sum to ≥ $200
   asserting refusal, and a second fixture test summing to < $200
   asserting the check proceeds (both branches proven, per SC1/SC2's
   pairing).
10. When SC9 refuses, it also rewrites `harness/pipeline/config.yaml`'s
    `mode:` line to the literal value `manual` — proven by a test
    asserting the byte diff between the config file before and after
    touches ONLY that single line, every other line (including comments)
    byte-identical. This reuses ADR-0006's existing kill switch
    (`docs/rules/full-auto-pipeline.md` § "How to emergency-disable" #1),
    never a second, new mechanism.
11. A completed invocation whose own computed USD exceeds the
    caller-supplied per-invocation cap (e.g. $5 for the challenge step,
    $50 for a future forge-run caller — SC accepts the cap as a
    parameter, proven by a second fixture value in this test) is written
    to the ledger with an explicit anomaly marker (e.g. `over_cap: true`)
    rather than silently accepted as a normal entry — proven by a test.
    This is documented, in the module's own docstring and per "points
    jugés sous-spécifiés" (a) above, as a POST-hoc flag: work already
    produced is never discarded because of it.
12. A boundary test proves entries dated in a PRIOR calendar month do not
    count toward the current month's cumulative: a fixture ledger with
    $199 of entries dated last month and $10 dated this month must
    compute this month's cumulative as $10, not $209.
13. `harness/pipeline/ci-budget-ledger.jsonl` (or whichever path this lot
    picks) is proven not excluded by `.gitignore` — a mechanical check
    (e.g. `git check-ignore` exits 1 for that path) documented in
    `deliverables/generator-log.md` with its real output. A ledger a
    clone cannot see is not a real audit trail.

### Lot 009c — wire `claude-challenger` for real (mode-gated, budget-gated)

14. `.github/workflows/pipeline-challenge.yml`'s `invoke-claude-challenger`
    job's real-invocation step no longer contains the literal string
    `TODO(operator` anywhere in its body. It constructs and runs an
    actual headless `claude` CLI call equivalent to `/forge-audit-review
    $AUDIT_ID`, per `architecture/agents/claude-challenger.md`, inside the
    SAME credential-availability branch that already exists (that
    branch's own logic — checking `cli_available`/`api_available` — is
    unchanged by this lot).
15. Before that real invocation fires, a preceding step calls a NEW,
    unit-testable Python entry point (not inline YAML bash comparison —
    the same architecture lot 008a's SC1 established for the orchestrator)
    that reads `harness/pipeline/config.yaml`'s current `mode` and refuses
    to proceed unless it is `full_auto_decision_only` or `full_auto`
    (never `manual`). Proven by two tests, one fixture per branch: `mode:
    manual` → refused; `mode: full_auto_decision_only` → proceeds. This
    closes a gap wider than ARCH-002 named — today NOTHING in any
    `pipeline-*.yml` reads `config.yaml`'s `mode` at all, so `mode:
    manual` has never actually been a kill switch for any of these four
    workflows (see "points jugés sous-spécifiés" (c)). This lot makes it
    one, for this maillon, for the first time.
16. Before the real invocation fires, the step also calls Lot 009b's
    pre-invocation budget check (SC9), for the $5 challenge-step cap
    context. A refusal there (monthly cumulative already exhausted) skips
    the invocation with a `::warning::` naming the reason — the same
    documented-skip shape the existing credential-missing branch already
    uses — never a silent skip, never a hard job failure (an intentional,
    policy-driven skip must not accidentally earn Lot 008b's own
    `pipeline_job_failed` escalation, which exists for the machine
    breaking, not for the machine correctly refusing).
17. After a real invocation completes (success or failure), the step
    calls Lot 009b's post-invocation record (SC8/SC11) with the USD
    computed from that invocation's own transcript, using the SAME
    token-to-USD logic `harness/backends/ledger.py` already implements
    for the Générateur role (imported, not reimplemented a third time).
18. The end-to-end wiring (mode-gate → budget-precheck → real invocation
    → scaffold/fill/record → budget-record) is proven by a NEW test that
    stubs the `claude` CLI itself (a disposable fake executable on `PATH`
    for the test only, or an equivalent subprocess-call interception) so
    the real code path — constructing the command, invoking it, capturing
    its output, writing a filled review, calling `audit_review.py record`
    — is exercised without a real `ANTHROPIC_API_KEY`. The test asserts
    the ledger shows `AUDIT_CHALLENGED` for the fixture `audit_id`
    afterward — the SAME mechanical final-state check
    `mechanical-scaffold-smoke` already performs, now proven reachable
    via the real (stubbed) invocation path, not only a hand-filled mock.
19. The pre-existing `mechanical-scaffold-smoke` job is unchanged in what
    it proves — it continues to run unconditionally, no credential
    needed. This lot's SC18 test is additive, not a replacement.
20. `docs/rules/full-auto-pipeline.md`'s diagram/text describing
    `[claude-challenger]` is updated to state this maillon is wired (real
    invocation, mode-gated, budget-gated) — not a stub. The remaining
    documented stops after this lot are `cursor-auditor` and `forge-run`,
    per the owner's own stated ordering; the doc must not overclaim
    either of those two as done.
21. `py -m pytest harness/tests/ -q` passes in full after this lot's
    commits, including its new tests, with no pre-existing test broken.

## Non-Goals

- Lot 009c wires **only** `claude-challenger`/`pipeline-challenge.yml`.
  `pipeline-audit.yml`'s and `pipeline-forge-run.yml`'s own
  `TODO(operator` invocation bodies are **not** touched by this brief —
  they remain exactly as they are today, per the owner's own explicit
  ordering (challenge → cursor-auditor → forge-run, forge-run last). A
  future brief wires each in turn.
- No lot implements a real `gh issue create` call anywhere (unchanged from
  brief 008's own Non-Goal — `handle_gate_reject`/`handle_pipeline_job_failed`
  stay log-only).
- No lot edits `docs/adr/0006-full-auto-agent-pipeline.md`'s own `Status`
  or body text (SC5 — ADR-0007 narrows it by reference, never rewrites
  it).
- No lot adds PyYAML (or any new pip package) as a dependency of any
  PRODUCTION code path under `harness/pipeline/*.py` or `harness/*.py` —
  `policy_loader.py`'s own "no PyYAML dependency in this repo" stays true
  for all production code. If a PyYAML-based substitute is used at all
  for the actionlint waiver (see Acceptable Waivers), it is confined to a
  test-only or generator-log-only verification step, never imported by a
  module a workflow executes at runtime.
- No lot implements the `pipeline/pause` kill-switch label check
  `docs/rules/full-auto-pipeline.md` already documents as Lot 006c's own
  unfinished responsibility — unchanged, out of scope.
- No lot pre-emptively blocks a single in-flight invocation mid-execution
  from exceeding its own per-invocation dollar cap — see "points jugés
  sous-spécifiés" (a). Only the monthly cumulative (SC9) is a true
  pre-invocation block; per-invocation caps are post-hoc anomaly flags.
- Lot 009c does not exercise the $50/forge-run cap end-to-end — see
  "points jugés sous-spécifiés" (b). Recorded (SC11's second fixture
  value), not wired to a real call.
- Subsystem boundary: this brief stays inside `harness/pipeline/**`,
  `harness/tests/**`, `.github/workflows/pipeline-challenge.yml`,
  `docs/adr/0007-*.md` (new), `docs/adr/README.md`,
  `docs/rules/full-auto-pipeline.md`. `harness/backends/ledger.py` is
  imported, never modified. No change to `sim/`, `pipeline/geo/`,
  `unity/`, or the other three `.github/workflows/pipeline-*.yml` files.
- File-set boundaries between lots: Lot 009a must not touch
  `.github/workflows/pipeline-challenge.yml`. Lot 009b must not touch
  `harness/pipeline/config.yaml`'s value, any ADR, or any workflow file.
  Lot 009c must not re-implement mode-validation or budget-ledger logic
  already built by 009a/009b — it calls them, and must not duplicate
  their tests either.
- Lot 009c's own pre-fix snapshot of `docs/rules/full-auto-pipeline.md`
  (see `must_differ_from`) is taken at the START of the 009c session,
  reflecting whatever state Lot 009a already left (SC6's edit) — not the
  pristine pre-brief state, since 009a runs first and legitimately edits
  the same file.

## Required Counters

| name | sample source | denominator / threshold | Lot |
|---|---|---|---|
| mode_full_auto_bare_rejected_test_count | new test function(s) asserting SC1 (fixture value `full_auto`, real `pipeline-forge-run.yml` state) | must be ≥ 1 | 009a |
| mode_full_auto_accepted_when_forgerun_wired_test_count | new test function(s) asserting SC2 (fixture copy of `pipeline-forge-run.yml` with the TODO string removed) | must be ≥ 1 | 009a |
| config_mode_single_commit_transition_count | `git log -p -- harness/pipeline/config.yaml` restricted to this lot's own commit range, counting distinct values the `mode:` line takes across those commits | must equal 2 (old value once, new value once — no intermediate bare `full_auto` commit) | 009a |
| adr_0007_status_field_present | presence of a non-blank `**Status**:` line in `docs/adr/0007-full-auto-mode-split.md` | must equal 1 | 009a |
| adr_readme_rows_added_count | new rows appended to `docs/adr/README.md`'s table, counted for ADR-0006 and ADR-0007 combined | must equal 2 | 009a |
| monthly_precheck_refuses_test_count | new test(s) asserting SC9's refusal branch (fixture ledger ≥ $200 this month) | must be ≥ 1 | 009b |
| monthly_precheck_proceeds_test_count | new test(s) asserting SC9's proceed branch (fixture ledger < $200 this month) | must be ≥ 1 | 009b |
| mode_flip_byte_scoped_test_count | new test(s) asserting SC10 (byte diff touches only the `mode:` line) | must be ≥ 1 | 009b |
| over_cap_anomaly_flag_test_count | new test(s) asserting SC11 (single-invocation cost > cap → `over_cap: true`), across at least two distinct cap values ($5, $50) proving the parameter is real | must be ≥ 2 (one per cap value) | 009b |
| monthly_boundary_reset_test_count | new test(s) asserting SC12 (prior-month entries excluded from current-month cumulative) | must be ≥ 1 | 009b |
| ci_budget_ledger_not_gitignored_check_count | documented `git check-ignore` (or equivalent) run against the new ledger path, recorded in `deliverables/generator-log.md` with real output | must equal 1, exit code must show NOT ignored | 009b |
| challenge_todo_stub_remaining_count | literal count of the string `TODO(operator` anywhere in `.github/workflows/pipeline-challenge.yml`, whole file, after this lot's commits | must equal 0 | 009c |
| mode_gate_manual_blocks_test_count | new test(s) asserting SC15's `mode: manual` → refused branch | must be ≥ 1 | 009c |
| mode_gate_full_auto_decision_only_proceeds_test_count | new test(s) asserting SC15's `mode: full_auto_decision_only` → proceeds branch | must be ≥ 1 | 009c |
| stubbed_cli_end_to_end_test_count | new test(s) asserting SC18 (stubbed `claude` CLI, real code path exercised, ledger shows `AUDIT_CHALLENGED` for the fixture `audit_id` afterward) | must be ≥ 1 | 009c |
| mechanical_scaffold_smoke_unchanged_check | comparison of the `mechanical-scaffold-smoke` job's trigger condition and step list against its pre-fix snapshot (SC19) | must show zero removed/weakened steps or triggers — only additive changes elsewhere in the file | 009c |

### `must_differ_from` pairs

| artifact A (pre-fix snapshot, taken at the START of the owning lot's own session) | artifact B (post-fix) | must differ because |
|---|---|---|
| `deliverables/pre-fix/config.yaml.orig` (009a) | `harness/pipeline/config.yaml` (009a) | proves the `mode:` value was actually rewritten (SC3), not merely claimed |
| `deliverables/pre-fix/auto_policy.yaml.orig` (009a) | `harness/pipeline/auto_policy.yaml` (009a) | proves the documentation scalar was actually updated (SC4) |
| `deliverables/pre-fix/full-auto-pipeline.md.orig` (009a) | `docs/rules/full-auto-pipeline.md` (009a) | proves the "How to activate" step 3 was actually corrected (SC6) |
| `deliverables/pre-fix/pipeline-challenge.yml.orig` (009c, taken from the state 009a/009b left, i.e. the repository state at the START of the 009c session — this file is untouched by 009a/009b) | `.github/workflows/pipeline-challenge.yml` (009c) | proves the real invocation, mode-gate, and budget calls were actually added (SC14-17), not merely claimed |
| `deliverables/pre-fix/full-auto-pipeline.md.orig` (009c, taken AFTER 009a's own edit already landed — see Non-Goals timing note) | `docs/rules/full-auto-pipeline.md` (009c) | proves the `[claude-challenger]` diagram text was actually updated (SC20) |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| "`actionlint` is not installable/runnable on this runner" (Lot 009c, for the edited `pipeline-challenge.yml`) | `command -v actionlint` (or `Get-Command actionlint` on PowerShell) | **Established fact, dated and attributed**: `command -v actionlint` was run by the orchestrator on the repository's own dev machine on **2026-08-10** and returned nothing — `actionlint` is confirmed absent. This waiver is ACCEPTED on that basis; the Générateur does NOT need to re-run this exact check on the SAME machine/environment to re-establish the fact. It DOES need to re-run it if the substitute check below is executed in a DIFFERENT environment than the one just verified — most notably the GitHub Actions `ubuntu-latest` runner `pipeline-challenge.yml` itself runs on, which is not the machine this fact was established on. Installed-tool state is environment-specific, not a fact that transfers across machines; re-verification is required only when the environment changes, and this brief says why rather than blanket-requiring it everywhere. **Substitute, also an established fact, dated and attributed**: `py -c "import yaml; print(yaml.__version__)"` was run by the orchestrator on the same dev machine on the same date and returned `6.0.3` — PyYAML is confirmed importable there (this corrects brief 008's own waiver row, which claimed the opposite without an independent re-check; this pass's caution in not copying that line unverified was justified by this result). Use `py -c "import yaml, sys; yaml.safe_load(open(sys.argv[1]))"` against `.github/workflows/pipeline-challenge.yml` as the syntactic-YAML-validity substitute on whichever environment actually runs this check — proves valid YAML **syntax** only, never GitHub Actions **schema** conformity (trigger key spelling, `uses:`/`run:` mutual exclusivity, secrets-context usage). The manual read-through covering those four specific things, documented in `deliverables/generator-log.md`, is NOT lightened by this confirmation — only the "is PyYAML importable at all" question is now settled for the dev machine. Per this brief's own Non-Goals, this substitute stays confined to test/verification code, never an import inside `harness/pipeline/*.py` production code. |
| "The actual dollar cost of an in-flight invocation cannot be observed before it completes, so a true pre-emptive per-invocation cap cannot be built" (Lots 009b/009c — see "points jugés sous-spécifiés" (a)) | the Générateur must read the actual `claude` CLI's own `--help` output (or its published headless-mode documentation) FOR REAL and record it, searching specifically for any native pre-emptive cost/turn/token ceiling flag | if none is found, this waiver is accepted and SC11's post-hoc form is the required implementation, documented with the real `--help` output (or doc excerpt + URL) attached in `deliverables/generator-log.md` as evidence a pre-emptive form was sought and not found — never assumed absent without looking |

## Execution Contract

- No Unity batchmode steps in this brief — N/A (entirely `harness/pipeline/**`,
  `harness/tests/**`, one `.github/workflows/*.yml`, and `docs/**`).
- **Tool-call estimate — hand-computed by this pass; mechanically confirmed
  2026-08-10 by the orchestrator running `py harness/budget.py split-check`
  on this Planificateur's own behalf** (no Bash tool available to the
  Planificateur role itself; see "Découpage" above and the "Amendment
  Note"). Lot 009a ≈ **100** (`SIZE_OK`, confirmed), Lot 009b ≈ **95**
  (`SIZE_OK`, confirmed), Lot 009c ≈ **145** (`SIZE_OK` per the
  split-check's 150-call trigger — but 145 is already PAST `budget.py
  status`'s own checkpoint threshold of 130, only 15 calls short of its
  160-call hard stop; do not read `SIZE_OK` as a quiet green light for
  009c — see the "Named risk" paragraph under Lot 009c in "Découpage").
  The whole brief at 340 calls → `NEEDS_SPLIT`, confirming the split
  decision mechanically, not only by this pass's own hand judgment. Each
  lot's own session must STILL run the check live, as its first action —
  a confirmed number from this brief's own authoring time is not a
  substitute for that session's own real, current estimate:
  `py harness/budget.py split-check --brief harness/queue/briefs/009-full-auto-agent-invocation --estimated-calls <that lot's number>`
- Every file named in each lot's `deliverables/manifest.json` must be
  under version control. `.gitignore` currently excludes `*.log` and
  `unity/game_unity/Logs/` (not directly relevant here) — the one file
  most at risk of accidental exclusion in this brief is
  `harness/pipeline/ci-budget-ledger.jsonl` (SC13); verify it explicitly,
  do not assume `.jsonl` is safe by inspection alone.
- Lot order: 009a and 009b may run in either order relative to each other
  (no dependency). Lot 009c **must** run after both — it calls both lots'
  own deliverables and cannot be meaningfully attempted before they exist.
  Three separate `/forge-run` sessions, none resuming from a prior
  transcript; each starts from this brief.md plus the repository's
  then-current state (and, for 009c, the two prior lots' checkpoints if
  either wrote one).
- Before any lot's session ends: `py -m pytest harness/tests/ -q` must be
  run and its real, complete output (not a truncated tail) recorded in
  that lot's `deliverables/generator-log.md`.

## Lots atomiques (summary)

| id | objectif | dépendances | fichiers / sous-systèmes | critères d'acceptation | commande de validation | définition de terminé |
|---|---|---|---|---|---|---|
| 009a-mode-split-fail-closed | `mode: full_auto` becomes `full_auto_decision_only` today; a bare `full_auto` is refused fail-closed until `forge-run` is really wired; ADR-0007 records why | Aucune | `harness/pipeline/config.yaml`; `harness/pipeline/auto_policy.yaml`; new validation module under `harness/pipeline/`; `docs/adr/0007-full-auto-mode-split.md` (new); `docs/adr/README.md`; `docs/rules/full-auto-pipeline.md`; `harness/tests/` | Success Conditions 1-7; counters `mode_full_auto_bare_rejected_test_count`, `mode_full_auto_accepted_when_forgerun_wired_test_count`, `config_mode_single_commit_transition_count`, `adr_0007_status_field_present`, `adr_readme_rows_added_count` | `py -m pytest harness/tests/ -k "mode_guard or mode_split or full_auto" -q` | Gate ACCEPT 009a; `config.yaml` reads `full_auto_decision_only`; the SC1/SC2 pair both pass, proven by tests, not prose |
| 009b-ci-budget-guard | A real, reusable, standalone module enforces the $200/month cumulative pre-invocation gate, flags per-invocation over-cap anomalies post-hoc, and reuses (never duplicates) ADR-0006's kill switch | Aucune (indépendant de 009a) | new `harness/pipeline/ci_budget_guard.py` (or equivalent); new `harness/pipeline/ci-budget-ledger.jsonl`; `harness/tests/` | Success Conditions 8-13; counters `monthly_precheck_refuses_test_count`, `monthly_precheck_proceeds_test_count`, `mode_flip_byte_scoped_test_count`, `over_cap_anomaly_flag_test_count`, `monthly_boundary_reset_test_count`, `ci_budget_ledger_not_gitignored_check_count` | `py -m pytest harness/tests/ -k "ci_budget or budget_guard" -q` | Gate ACCEPT 009b; the module is import-only reusable (proven by ≥2 distinct cap-value tests), touches no ADR/workflow/config value |
| 009c-wire-claude-challenger | `pipeline-challenge.yml`'s real-invocation step actually calls an agent, gated on `mode` (never fires under `manual`) and on the monthly budget ceiling, and records real spend — the challenge maillon of ARCH-002 is closed | 009a, 009b | `.github/workflows/pipeline-challenge.yml`; a new mode-gate call-site; `docs/rules/full-auto-pipeline.md` (diagram text only); `harness/tests/` | Success Conditions 14-21; counters `challenge_todo_stub_remaining_count`, `mode_gate_manual_blocks_test_count`, `mode_gate_full_auto_decision_only_proceeds_test_count`, `stubbed_cli_end_to_end_test_count`, `mechanical_scaffold_smoke_unchanged_check` | `py -m pytest harness/tests/ -k "challenge or invoke" -q` | Gate ACCEPT 009c; `TODO(operator` count is 0 in `pipeline-challenge.yml`; the stubbed-CLI end-to-end test reaches `AUDIT_CHALLENGED` in the ledger; `mode: manual` provably blocks invocation |
