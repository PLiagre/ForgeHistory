# Generator Log — Brief 006, Lot 006a (Gouvernance + FSM)

**Author**: forge-generateur
**Authored**: 2026-08-05T16:55:00Z

## Scope

This run implements **only** Lot 006a of brief 006, per the explicit
scoping in the task ("Lots atomiques" section of
`harness/queue/briefs/006-full-auto-agent-pipeline/brief.md`). Lots 006b
(agent roles, orchestrator, GH workflows) and 006c (budget supervisor,
demo, cost-ledger link) are untouched — no `orchestrator.py`,
`.github/workflows/pipeline-*.yml`, `architecture/agents/**`, or demo
scripts were created.

## What was built

### `docs/adr/0006-full-auto-agent-pipeline.md`

New ADR, `Status: accepted`. Follows the shape of
`docs/adr/template.md` and `docs/adr/0005-cursor-as-independent-auditor.md`.
Cites the ADR-0005 derogation (the owner's `CHALLENGED -> APPROVED/REJECTED`
step is no longer the only path once `--policy auto` exists), names the
three risks the task specified (erroneous automatic decision, unwanted
merge, runaway cost) each with a named mitigation, and states the auto-merge
allowlist/denylist as a binding constraint (`.github/workflows/**`,
`harness/verdict_audit.py`, `VISION.md` excluded, no exception listed today).
An earlier draft of that section's heading restated one of brief.md's own
structural headings verbatim, which collided with
`test_single_source_of_instruction.py`'s forbidden-heading scan (any
level-2 heading that opens the same way as one of brief.md's headings is
treated as a paraphrase); renamed to "Binding auto-merge path constraints
(see brief 006 for the source requirement)" — same content, a title that
points back to the brief instead of restating its heading.

### `harness/pipeline/` (new package)

- `__init__.py` — states the package's scope (006a only) so a reader
  landing here later knows `orchestrator.py` is deliberately absent.
- `policy_loader.py` — a ~140-line hand-rolled parser for exactly the two
  YAML shapes `auto_policy.yaml` and `config.yaml` use (flat top-level
  scalars, one `rules:` block of flat mappings, one flat list under a
  top-level key). Written because `py -c "import yaml"` fails in this
  environment (`ModuleNotFoundError: No module named 'yaml'` — captured in
  `006a-validation.log`) and the task is explicit that a new pip dependency
  must never be added silently. The module's own docstring says plainly
  that it is not a general YAML parser.
- `auto_policy.yaml` — `mode: full_auto` (documented target state) plus a
  top-level `rules:` list with **10** entries, one per row of brief 006's
  "§ Politique auto" table (verified by counting the table rows by hand
  against the loaded list — see counters below). Each rule carries `id`,
  `event`, `condition`, `action`.
- `config.yaml` — exposes the six literal keys the brief requires
  (`mode`, `max_forge_run_iterations`, `auto_merge_audit_prs`,
  `auto_merge_review_prs`, `claude_challenge_on_inbox_merge`,
  `cursor_audit_on_master_push`), plus `auto_merge_allowlist` /
  `auto_merge_denylist` so the ADR-0006 constraint is a literal, checkable
  value and not only prose. `mode: manual` on purpose — the brief's own
  "Lots atomiques" ordering says to flip to `full_auto` only after Lot
  006c's end-to-end demo exists to catch a runaway loop; nothing in 006a
  builds that supervisor yet.

### `harness/audit_ledger.py` (enhanced, not rewritten)

Added `TRANSITIONS` (an explicit map from every one of the 9 `VALID_EVENTS`
plus `None` for "no prior event", to the set of legal next events) and a
`TransitionError(ValueError)`. `append_event` now calls a new
`current_state_for(audit_id, ledger_path)` helper (which lazily imports
`audits.current_state` — lazy specifically to dodge the load-order cycle
between the two modules) and refuses any `event` that is not in
`TRANSITIONS[current_state]`, before writing anything.

Two real findings shaped the map:

1. **The disqualifying case** (brief's own success criterion): a fresh
   `audit_id` with zero prior events can no longer accept `AUDIT_APPROVED`
   — proven both as a direct-call test and a CLI subprocess test in
   `test_audit_fsm.py`, and proven **red-first** by importing the pre-006a
   `audit_ledger.py` via `importlib.util` and showing it silently wrote the
   same bypass (both runs captured verbatim in `006a-validation.log`).
2. **`None` had to allow `AUDIT_CHALLENGED`, not only `AUDIT_PROPOSED`.**
   Running the existing test suite against a `None -> {AUDIT_PROPOSED}`
   -only map broke `audit_review.py`'s own tests
   (`test_record_happy_path_appends_and_advances_state`,
   `test_cli_scaffold_then_record`, `test_cli_set_passes_extra_fields`):
   `audit_review.record_challenge` deliberately writes `AUDIT_CHALLENGED`
   as an audit's *first* ledger event, relying on `audits.py`'s own
   `DEFAULT_STATE = "AUDIT_PROPOSED"` for an audit with no events yet — and
   `auto_policy.yaml`'s first rule documents the `AUDIT_PROPOSED` ledger
   event as *optional*. Fighting that convention would have broken a real,
   already-tested caller for no safety gain, so `TRANSITIONS[None]` widens
   to `{AUDIT_PROPOSED, AUDIT_CHALLENGED, AUDIT_STALE}` — it still refuses
   `AUDIT_APPROVED`/`AUDIT_CONVERTED`/etc. as a first event, which is the
   actual bypass this lot must close.

**Two existing tests in `harness/tests/test_audit_ledger.py` had to change**
(not the four caller modules' own test files — those needed zero changes):
`test_all_nine_states_are_accepted` walked all nine event names in
`VALID_EVENTS` tuple order on one `audit_id`, which is not a real
transition sequence (e.g. it went `APPROVED -> REJECTED`) and only ever
tested "each name individually passes the charset check." Rewrote it to
walk three FSM-legal chains (happy path, rejected path, stale path) whose
union still touches all nine states, asserting the same thing the old test
did — every valid name is accepted — under the new ordering constraint.
`test_extra_fields_survive_round_trip` appended `AUDIT_CONVERTED` as a bare
first event; rewrote it to reach `AUDIT_CONVERTED` via the real legal chain
first, then check the same field-survival assertion on that event. Both
changes are called out here explicitly rather than left silent, per the
"never weaken a check" rule — neither widens what the FSM accepts, both
only fix test *setup* to be FSM-legal.

### `harness/audit_decision.py` (enhanced, not rewritten)

Added `decide_auto(audit_id, ...)` and a new `auto` CLI subcommand
(`py harness/audit_decision.py auto --audit-id ID [--policy auto]`).
`decide_auto`:

1. Requires the audit to be `AUDIT_CHALLENGED` (same guard as `decide()`).
2. Resolves the review file from the ledger's own `AUDIT_CHALLENGED.review`
   field (not a guessed path) and parses its `| N | ... | VERDICT | ... |`
   table rows with a regex — the ledger's `verdicts` field only carries
   *counts* per token, not which point number holds which verdict, so the
   review file itself is the only place `retained_points` can come from.
3. Applies the three rules from `auto_policy.yaml`'s table, in the brief's
   own priority order: all `REFUTED` -> `REJECTED`; any `CONFIRMED`/
   `PARTIAL` -> `APPROVED` with `retained_points` = the union of those point
   numbers; `NEEDS_OWNER` with neither of the above -> `REJECTED` with the
   literal reason `"policy: no owner in full_auto"` (grep-able, exactly as
   the brief phrases it), appended with the matching `auto_policy.yaml`
   rule id for traceability. A review with no parseable verdict rows raises
   `DecisionError` — it refuses to guess rather than defaulting either way.
4. Delegates the actual write to the existing `decide()` function (now
   parameterized with `actor="policy:auto"` — default stays `"owner"`, so
   the human `accept`/`reject` path is byte-for-byte unchanged) — one
   writer for the decision file and the ledger append, whichever path
   calls it.

`decide()` also now records `reason` as a ledger field (previously only the
decision *file* carried it) and accepts `decided_by`/`actor` so the
decision file's `decided_by:` frontmatter and the ledger's `actor` field
both say `"policy:auto"` for an automatic decision — a reader of either
artifact alone can tell whether a human or the policy decided.

Ten new tests added to `harness/tests/test_audit_decision.py`: the three
policy branches (approve on confirmed/partial, reject on all-refuted,
reject on needs-owner-only), two guard tests (not challenged, no review
file), and CLI-level tests including `auto --help` showing `--policy`.

### `harness/tests/test_audit_fsm.py` (new)

12 test functions; 9 use `pytest.raises` to prove a refusal (case count
below), plus a full happy-path test, a "bootstrap is legal" test (documents
why `None -> AUDIT_CHALLENGED` is NOT a bypass, so it is not mistaken for
one later), and a CLI subprocess test. Explicitly covers all five required
adversarial shapes from the task: (a) APPROVED without CHALLENGED — two
variants (fresh audit, and PROPOSED-then-APPROVED); (b) the real adversarial
form of "CHALLENGED without a legal predecessor" (CHALLENGED after APPROVED
— the bootstrap case turned out to be legal, see above, so it is
documented instead of asserted as a failure); (c) the full seven-state
happy path; (d) an event after two different terminal conditions (after
ARCHIVED, and after REJECTED-without-yet-ARCHIVED); (e) an unknown/typo
event name even from a valid prior state. All use `tmp_path` ledgers.

## How every measurement was actually taken

Both required counters, the full pytest run (82 tests across the seven
audit-domain test files, then 217 across the whole `harness/tests/`
tree), the red-first proof (import of the pre-006a `audit_ledger.py` via
`importlib.util`, showing it silently wrote the bypass), the disqualifying-
case proof at the CLI (`append --event AUDIT_APPROVED` on a fresh ledger,
exit code 2), the `--help` output showing `--policy {auto}`, and the
`ModuleNotFoundError` proving PyYAML is absent are all captured verbatim,
in the order run, in `deliverables/006a-validation.log` — every number in
`manifest.json` traces to a specific block in that file.

- `auto_policy_rules_count = 10`, `sample_size = 10`: counted by actually
  loading `auto_policy.yaml` through `policy_loader.load_auto_policy` and
  taking `len(rules)` — not a hand count of the YAML text.
- `fsm_invalid_transition_tests_count = 9`, `sample_size = 9`: `grep -c
  "pytest.raises" harness/tests/test_audit_fsm.py` — a strict subset of the
  file's 10 truly adversarial cases (the CLI exit-code test proves refusal
  without `pytest.raises`), so the reported number is a deliberate
  undercount rather than an inflated one; both exceed the brief's `>= 5`
  floor comfortably either way.

## What was deliberately NOT done here (left for 006b/006c or the Évaluateur)

- No `orchestrator.py`, no GitHub Actions workflows, no
  `architecture/agents/*.md` role files, no demo script, no budget
  supervisor, no cost-ledger `audit_id` field — all explicitly out of scope
  for 006a per the task.
- `harness/pipeline/config.yaml`'s `mode` stays `manual`; flipping it to
  `full_auto` is explicitly deferred to after Lot 006c per the brief's own
  "Lots atomiques" ordering.
- Did not write `verdict.md` — that is the Évaluateur's role, never the
  Générateur's.
- Ran `py harness/verdict_audit.py harness/queue/briefs/006-full-auto-agent-pipeline`
  as a self-check only (see report below); did not attempt to make the
  `verdict_is_not_self_authored` check pass, since that check legitimately
  requires `verdict.md` to exist and it is not this role's job to write one.

---

# Lot 006b — Rôles agents + orchestrateur + workflows

**Author**: forge-generateur
**Authored**: 2026-08-05T19:44:00Z

## Scope

This run implements **only** Lot 006b: Success Conditions 4, 5, 9, 10, 11,
12, 13, 20 (per the explicit scoping in the task and the brief's own "Lots
atomiques" table). Lot 006a (already merged, PASS verdict —
`harness/queue/briefs/006-full-auto-agent-pipeline/verdict-006a.md`) is
untouched, read-only. Lot 006c (budget supervisor, split-check obligation
in `/forge-run`, end-to-end demo, cost-ledger `audit_id`, CLAUDE.md/HANDOFF
pointers) is explicitly out of scope and not attempted here.

## Feedback from Lot 006a addressed

`verdict-006a.md`'s only note for the next iteration: `orchestrator.py`
must route ALL ledger writes through `harness.audit_ledger.append_event`,
never construct a ledger line directly. Addressed structurally:
`harness/pipeline/orchestrator.py` never opens a ledger file handle itself
— every mutation goes through `audit_ledger.append_event`, either directly
(the `audit_pr_merge` and `evaluateur_pass` handlers) or via the two
existing modules that already used that choke point in Lot 006a
(`audit_decision.decide_auto` for `review_recorded`,
`audit_convert.convert` for `audit_approved`). Proven mechanically, not
just by reading the source: `test_no_direct_ledger_file_write_in_source`
(new, `harness/tests/test_orchestrator.py`) asserts the module's own
source contains neither `open(` nor `.write(`; and
`test_evaluateur_pass_cannot_skip_fsm` calls the orchestrator's own
`evaluateur_pass` handler on a freshly-`AUDIT_PROPOSED` audit (skipping
`CHALLENGED`/`APPROVED`/`CONVERTED`) and asserts it raises the exact same
`audit_ledger.TransitionError` a direct bypass attempt would — the ledger
gains nothing, proving there is no side door.

## What was built

### Six role contracts — `architecture/agents/<role-id>.md`

`cursor-auditor`, `cursor-qa-scout`, `claude-challenger`,
`claude-developer`, `claude-evaluator`, `pipeline-orchestrator`. Every file
carries the seven required section headers verbatim (`# Identité`,
`# Entrées`, `# Sorties`, `# Interdits`, `# Déclencheur`, `# Preuve de
fin`, `# Budget max appels`) — verified mechanically (see counters below),
not by eyeballing. Content is filled from the brief's own "Rôles agents
(contrats obligatoires)" table, cross-referencing the concrete modules each
role actually touches (`audit_review.py` for `claude-challenger`,
`audit_convert.py`/`audit_ledger.py` for `pipeline-orchestrator`, the
existing `/forge-run` and `.claude/agents/forge-*.md` files for
`claude-developer`/`claude-evaluator`) so the contract is checkable against
real code, not aspirational prose.

`architecture/agents/README.md` documents, per role, exactly one invocation
mechanism (Success Condition 5): a Cloud Agent template (the role file
itself) for the two Cursor roles, the existing `/forge-audit-review` and
`/forge-run` slash commands for `claude-challenger`/`claude-developer`, the
internal Phase-1 launch inside `/forge-run` for `claude-evaluator` (no
separate slash command exists or should exist, since it is invoked from
inside another command's loop), and the `orchestrator.py` CLI itself for
`pipeline-orchestrator`.

### `harness/pipeline/orchestrator.py`

Deterministic dispatcher, `run --event <kind> --payload '<json>'` (or
`--payload-file`). Eight event kinds
(`audit_pr_merge`, `review_recorded`, `audit_approved`,
`brief_seed_created`, `gate_accept`, `evaluateur_pass`, `gate_reject`,
`budget_exhausted`) map onto the ten `auto_policy.yaml` rule ids
(`review_recorded` alone covers three rules — `review_all_refuted`,
`review_has_confirmed_or_partial`, `review_needs_owner_only` — because
`audit_decision.decide_auto` already selects the right one from the
review's own per-point verdicts; re-selecting here would be a second place
that could disagree with the first). An event naming no rule in
`auto_policy.yaml`, or missing a required payload field, is refused
(`OrchestratorError`, exit 2) rather than guessed. Four handlers call an
existing module (`audit_ledger.append_event`, `audit_decision.decide_auto`,
`audit_convert.convert`); four are deliberately log-only, because their
policy action is "invoke a separate agent" or "the checkpoint lives
elsewhere" (`brief_seed_created`, `gate_accept`, `gate_reject` below
streak 3, `budget_exhausted`) — the orchestrator names what should happen
next without pretending to be the LLM or supervisor that does it.

Manually smoke-tested end-to-end against a disposable tmp ledger before
writing the pytest suite (red-first workflow — the actual transcript is in
this session, summarized here since it is not itself a deliverable file):
`audit_pr_merge` appends `AUDIT_PROPOSED` once and is idempotent on a
second call; a full `PROPOSED -> CHALLENGED -> APPROVED -> CONVERTED`
chain built by hand, then `evaluateur_pass` correctly appends
`AUDIT_IMPLEMENTED` then `AUDIT_VERIFIED`; and — the disqualifying case —
calling `evaluateur_pass` on an audit that only ever reached
`AUDIT_PROPOSED` fails with `invalid transition ... AUDIT_PROPOSED ->
AUDIT_IMPLEMENTED is not allowed`, exit 2, ledger unchanged.

### `harness/tests/test_orchestrator.py` (new, 9 tests)

`--help`/`run --help` exit 0; an unknown event is refused; `review_recorded`
genuinely calls `decide_auto` (asserted via the returned record's `event`
and `actor`, and independently by re-reading the ledger); the FSM-bypass
proof described above; the happy-path `evaluateur_pass` chain; idempotency
of `audit_pr_merge`; a missing required field is refused; `gate_reject`
only escalates at `reject_streak >= 3`, not below; and the static
no-direct-file-write proof. All nine pass
(`deliverables/006b-validation.log`).

### Four workflows — `.github/workflows/pipeline-*.yml`

- `pipeline-audit.yml` — `push: [master]` + `workflow_dispatch`; checks
  `secrets.CURSOR_API_KEY` at runtime, logs a `::warning::` waiver and
  no-ops if absent (it is, on this repo — see Waivers below) instead of
  failing the job.
- `pipeline-challenge.yml` — `push` touching `architecture/inbox/*.md` +
  `workflow_dispatch`; same credential-check shape for `claude`
  CLI/`ANTHROPIC_API_KEY`, PLUS a second job,
  `mechanical-scaffold-smoke`, that runs unconditionally with **no**
  credential and proves the non-LLM half of `claude-challenger`
  (`audit_review.py scaffold` → fill → `record` → ledger `AUDIT_CHALLENGED`)
  actually works in CI today. Verified locally first (the exact script, run
  against a disposable tmp fixture, output captured in
  `006b-validation.log`) before it went into the workflow file.
- `pipeline-orchestrate.yml` — `push` touching `architecture/reviews/*.md`
  (best-effort: only auto-dispatches when the push's own before/after diff
  names exactly one changed review file; anything else is left to an
  explicit `workflow_dispatch`) + `workflow_dispatch` with explicit
  `event`/`audit_id`/`payload` inputs. Runs `orchestrator.py`, then a hard
  allowlist check (`architecture/audit-ledger.jsonl`,
  `architecture/decisions/**`, `harness/queue/briefs/**` only) BEFORE
  committing — a change outside that allowlist fails the job instead of
  being pushed, so this workflow cannot itself become a path around the
  `auto_merge_denylist`.
- `pipeline-forge-run.yml` — `workflow_dispatch` with a required
  `brief_dir` input, or a `forge-run/queued` label on an issue/PR carrying
  `brief_dir: <path>` in its body. Always runs
  `py harness/budget.py split-check --brief <BRIEF_DIR>` as a mandatory
  preflight (wiring the Lot 006c Success-Condition-15 obligation into this
  workflow now, so an oversized queued brief never reaches an LLM
  invocation at all, in or out of 006c) before checking headless
  credentials.

All four parsed cleanly with PyYAML (`yaml.safe_load`) — `006b-
validation.log`. `actionlint` is not installed on this dev machine (`which
actionlint` — not found); noted honestly rather than skipped silently,
since `.github/workflows/security.yml`'s own `actionlint` job will lint
these files in CI regardless.

### `.github/merge-bot.yaml` + `.github/workflows/merge-bot.yml`

Success Condition 13. `merge-bot.yaml` restates (not paraphrases as a new
rule — it is the human-facing doc for the same two lists
`harness/pipeline/config.yaml`'s `auto_merge_allowlist`/`auto_merge_denylist`
already carry as the machine-read source) the bot-branch, allow, and deny
path lists. `merge-bot.yml` enforces it on every `pull_request` from a
`cursor/*`/`forge-bot/*` branch: a `git diff` against the merge base is
checked against `deny_paths` FIRST (unconditional, wins even over an
allowlisted path), then against `allow_paths`; only if both checks pass
does it attempt `gh pr merge --auto`. Named `merge-bot.yml`, not
`pipeline-merge-bot.yml`, on purpose — the brief's Required Counter
`pipeline_workflows_count` must equal exactly 4 (one per Success Condition
9–12), and Success Condition 13 is graded by a separate manual rubric row,
not that counter; a `pipeline-*.yml`-named fifth file would have silently
broken the `== 4` denominator.

### `docs/rules/full-auto-pipeline.md`

Success Condition 20. Full ASCII diagram of the closed loop (merge → audit
→ challenge → orchestrator decision → convert → planificateur → forge-run
→ gate → evaluator → merge → ledger IMPLEMENTED/VERIFIED → archive), the
six roles' invocation table (pointer to `architecture/agents/README.md`,
not a paraphrase), the activation steps for `mode: full_auto`, and the two
independent emergency-disable mechanisms the brief names:
`mode: manual` (already a live, tested option since Lot 006a — never
removed) and the kill-switch label `pipeline/pause` (documented as a
contract every `pipeline-*.yml` write-action step must check before acting
— wiring that check into every step is named as Lot 006c's job explicitly,
so this doc states the target without pre-empting 006c's own Success
Conditions).

## A real finding changed mid-run, and both artifacts were corrected

Investigating the third Acceptable-Waiver row ("branch protection empêche
auto-merge bot"), the first `gh api
repos/PLiagre/ForgeHistory/branches/master/protection` call returned `404`
during a very early exploratory check. Later, running the actual validation
log generation, the same call returned `403` — `"Upgrade to GitHub Pro or
make this repository public to enable this feature."` — reproduced a
second time immediately after to rule out a fluke
(`006b-validation.log`, "waiver evidence 3" block). The `403` reading is
the one that matters (it is what the CI runner's `GITHUB_TOKEN` would also
see, and it is what actually blocks `merge-bot.yml`'s `gh pr merge --auto`
step), and it is exactly the brief's Acceptable Waivers row 3 premise. Both
`docs/rules/full-auto-pipeline.md` ("Known gap") and
`.github/merge-bot.yaml`'s header comment, which had been drafted around
the earlier `404` reading, were corrected to the reproduced `403` evidence
before this log was written — a wrong intermediate reading is not left
silently superseded, it is named here so a reader does not wonder why the
two mentions differ.

## Waivers recorded (`deliverables/manifest.json`)

Three, all backed by a real command run against the actual repo
(`PLiagre/ForgeHistory`), not narrated:

1. `gh secret list --repo PLiagre/ForgeHistory` → empty output, exit 0 — no
   `CURSOR_API_KEY`. Backs `pipeline-audit.yml`'s runtime check.
2. Same command → also proves no `ANTHROPIC_API_KEY`. Backs
   `pipeline-challenge.yml`/`pipeline-forge-run.yml`'s runtime checks; the
   brief's required "mock test PASS" for this fallback is the
   `mechanical-scaffold-smoke` job, verified above.
3. `gh api repos/PLiagre/ForgeHistory/branches/master/protection` → `403`,
   reproduced twice (see above). Backs the "Known gap" section of
   `docs/rules/full-auto-pipeline.md` and the safety design of
   `merge-bot.yml` (its own path check runs and can refuse before
   `gh pr merge --auto` is ever attempted — the brief's required "partial"
   shape for this waiver).

## How every measurement was actually taken

- `agent_role_files_count = 6`, `sample_size = 6`: `py -c` globbing
  `architecture/agents/*.md` and excluding `README.md` by name —
  `006b-validation.log`.
- `pipeline_workflows_count = 4`, `sample_size = 4`: `py -c` globbing
  `.github/workflows/pipeline-*.yml` — `006b-validation.log`. `merge-bot.yml`
  is deliberately excluded by its own name from this glob (see above).
- Both Lot 006a counters (`auto_policy_rules_count`,
  `fsm_invalid_transition_tests_count`) are unchanged from Lot 006a's own
  measurement — this run never touched `auto_policy.yaml` or
  `test_audit_fsm.py`, so re-measuring them from scratch would have
  produced the identical numbers; kept as-is per "KEEP the existing 006a
  file entries and both existing counters" in the task.
- Full suite: `py -m pytest harness/tests/ -q` → 226 passed (217 carried
  over from Lot 006a plus the 9 new `test_orchestrator.py` cases), captured
  verbatim in `006b-validation.log`.

## What was deliberately NOT done here (left for Lot 006c)

- No budget supervisor / SIGTERM enforcement (`harness/budget.py` itself
  unchanged this run beyond being called, never edited).
- `/forge-run`'s own split-check obligation
  (`.claude/commands/forge-run.md`) was not edited — only
  `pipeline-forge-run.yml`, the NEW workflow this lot introduces, was given
  the mandatory preflight; the existing command file is Lot 006c's Success
  Condition 15.
- No `harness/pipeline/fixtures/mini_repo/` or `run_full_auto_demo.sh`, no
  `cost-ledger.jsonl` `audit_id` field.
- `harness/pipeline/config.yaml`'s `mode` stays `manual` (Lot 006a's own
  decision, untouched here) — this lot documents HOW to flip it in
  `docs/rules/full-auto-pipeline.md`, it does not flip it.
- Did not write `verdict.md`, and did not run
  `py harness/verdict_audit.py harness/queue/briefs/006-full-auto-agent-pipeline`
  as a self-judgment — per the task's own instruction not to run it as a
  judgment call, only the pytest suite and the orchestrator's own
  `--help`/functional smoke tests were run as validation.

---

# Lot 006c — Budget supervisor + démo E2E + traçabilité coût (FINAL LOT)

**Author**: forge-generateur
**Authored**: 2026-08-05T21:30:00Z

## Scope

This run implements **only** Lot 006c, the final lot of brief 006: Success
Conditions 14, 15, 16, 17, 18, 19, 21 (per the explicit scoping in the task
and the brief's own "Lots atomiques" table). Lots 006a (`verdict-006a.md`,
PASS) and 006b (`verdict-006b.md`, PASS) are untouched except read-only
imports (`harness/pipeline/orchestrator.py`, `harness/audit_ledger.py`,
`harness/audit_decision.py`, `harness/pipeline/auto_policy.yaml`). SC20 was
already delivered and PASSED in Lot 006b (`docs/rules/full-auto-pipeline.md`
already exists) and is explicitly out of this lot's scope per the task's own
enumeration -- not re-touched here, including the kill-switch label wiring
into individual `pipeline-*.yml` steps that 006b's own feedback named as a
"nice to have" for 006c: the task's explicit SC list for this run (14, 15,
16, 17, 18, 19, 21) does not include it, so it was left as-is.

## What was built

### `harness/pipeline/supervisor.py` (SC14) + `harness/tests/test_supervisor.py`

A new file, `budget.py` NOT rewritten -- only imported (`HARD_STOP_CALLS`,
never redefined locally). `decide(tool_calls, hard_stop)` is a pure function
(`CONTINUE`/`SIGTERM`/`UNMEASURABLE`, `-1` sentinel per hard-won rule 8,
never a bare 0 that would read as "zero calls so far"); `supervise(child,
transcript_path=..., hard_stop=..., poll_interval=...)` polls a real
`subprocess.Popen`-shaped child on an interval, reading the SAME shared
transcript reader `harness/transcripts.py:count_tool_calls` that
`budget.py` itself uses (never a second, potentially-disagreeing copy of
that parsing) and calls `terminate_child()` (`signal.SIGTERM` where the
platform's `signal` module exposes it, `.terminate()` as the only fallback)
the moment the measured count reaches the threshold.

Portability, stated honestly rather than assumed (this dev machine is
Windows 11): `Popen.send_signal(signal.SIGTERM)` and `Popen.terminate()`
both resolve to `TerminateProcess` under the hood on Windows -- there is no
POSIX-style signal handler for a Windows child to intercept, so there is no
meaningfully different code path to test there. The test suite therefore
asserts the REAL, portable, OS-observable outcome (the child process is no
longer alive a few hundred ms after `supervise()` returns `SIGTERM`)
against a genuinely long-running real child (`py -c "import time;
time.sleep(60)"`), rather than mocking the terminate call -- a stronger and
more honest proof than the fallback the task explicitly permitted for cases
where a real kill is not portable (here it is).

12 tests in `test_supervisor.py`, all passing (see `006c-validation.log`):
the pure `decide()` boundary (`HARD_STOP_CALLS - 1` -> CONTINUE,
`HARD_STOP_CALLS` exactly -> SIGTERM, `-1` -> UNMEASURABLE), the missing-
transcript sentinel, a real fixture-transcript read, a below-threshold
child left running for 3 poll cycles then torn down by the test (never by
the supervisor), an at-threshold child actually terminated and confirmed
dead within a 5s deadline, an UNMEASURABLE transcript never terminating a
child, a child that exits on its own being detected without a spurious
terminate call, and two `--help`/usage CLI checks.

### `harness/pipeline/forge_run_preflight.py` (SC15) + `harness/tests/test_forge_run_preflight.py`

`budget.py`'s own `cmd_split_check` is documented as advisory ON PURPOSE
(its own module comment explains why: the one mechanical trigger,
`--estimated-calls`, is the Planificateur's judgment call, not something
this script should silently override) and its CLI always exits 0 regardless
of verdict -- CURSOR-6231186-execution-budgets' FINDING-ARCH-001 named
exactly this gap: nothing stopped `/forge-run` launching a Générateur on a
brief `split-check` had itself just called NEEDS_SPLIT on. This new script
does not change that advisory contract (`budget.py` untouched): it calls
`budget.cmd_split_check` itself (capturing its stdout via
`contextlib.redirect_stdout`, the ONE place the NEEDS_SPLIT trigger rule is
computed) and reads back its own printed `advisory   : <VERDICT>` line,
turning it into a **blocking** exit (1 on NEEDS_SPLIT) for the one caller
that must never proceed past it. `--estimated-calls` is required here
(budget.py's own flag defaults to `-1`/`NO_ESTIMATE`), matching the brief's
"en première action" obligation.

5 tests, all passing: a control test proving `budget.py` itself is
unchanged (still advisory, exit 0, on the exact same NEEDS_SPLIT input --
so this really is budget.py's own logic being read back, not a duplicated
threshold), the wrapper blocking on NEEDS_SPLIT (exit 1, stderr says
BLOCKED), the wrapper allowing SIZE_OK (exit 0), `--estimated-calls` being
a required flag (argparse usage error otherwise), and the exact 150/151
boundary (`budget.py`'s own trigger is `> 150`).

`.claude/commands/forge-run.md`'s "Execution Budgets" section rewritten:
the split-check call is now stated as **obligatory, not advisory, for this
command**, naming the new wrapper and its blocking contract explicitly (a
non-zero exit means: do not proceed to Phase 1). Ran
`py -m pytest harness/tests/test_single_source_of_instruction.py -q` after
editing -- still passes (no forbidden heading introduced).

### `harness/backends/ledger.py` -- optional `audit_id` field (SC16) + `harness/tests/test_ledger_audit_link.py`

`append_entry(backend, brief, event, audit_id=None)`: when `audit_id` is
given, the field is added to the JSON record; when omitted, the record is
byte-for-byte identical to before this lot (proven by
`test_append_entry_without_audit_id_omits_the_field`) -- genuinely
optional, not a silently-added `null`. CLI gained `--audit-id` (optional,
`append` subcommand). 4 tests: backward compatibility (no field when
omitted), the field present when given, `--help` documents the flag (the
CLI append itself is NOT exercised as a subprocess test, because the CLI
intentionally has no `--ledger` override and would otherwise write into the
REAL `harness/queue/cost-ledger.jsonl` during every `pytest` run -- the
real end-to-end CLI append is instead exercised, once and on purpose, by
the demo script below), and the full chain: a real
`audit_convert.convert()` call producing a `briefs[]`-carrying
`AUDIT_CONVERTED` ledger event, then a cost-ledger entry for that SAME
brief tagged with the SAME `audit_id`, joined and asserted explicitly.

### `harness/tests/test_full_auto_pipeline.py` (SC17)

Full chain, one process, `tmp_path`-isolated, **NO human input anywhere**:
PROPOSED (implicit) -> `audit_review.write_scaffold` + a real filled review
(not scaffold placeholders) -> `audit_review.record_challenge` ->
CHALLENGED -> `audit_decision.decide_auto` (the `--policy auto` path, NEVER
`decide()`, the human accept/reject entry point) -> APPROVED ->
`audit_convert.convert` -> CONVERTED (brief seed written and asserted to
exist on disk) -> a named "forge-run MOCK" step (no Claude/Cursor process
spawned, exactly as the brief's own wording for SC17 says) ->
`orchestrator.run_event("evaluateur_pass", ...)` -> IMPLEMENTED, VERIFIED ->
a direct `audit_ledger.append_event(..., "AUDIT_ARCHIVED", ...)` -> the
terminal state, then proves nothing may follow it
(`pytest.raises(TransitionError)` on a further `AUDIT_STALE`). A second
test re-reads the ledger and asserts the APPROVED event's `actor` field is
literally `"policy:auto"`, never `"owner"` -- proving "no human input" by
inspecting the artifact the decision left behind, not by trusting the
caller's intent. A third test parses this test file's OWN AST (not a
substring search, which would also match this docstring's prose) and
asserts no `audit_decision.decide(...)` call (the human path, as opposed to
`decide_auto`) exists anywhere in the file -- mechanical, not a promise.

### `harness/pipeline/demo/run_full_auto_demo.sh` (SC18) + `architecture/inbox/CURSOR-FIXTURE-full-auto-demo.md` (SC19)

Unlike SC17's tmp_path test, the demo deliberately operates on the REAL
repo files `architecture/audit-ledger.jsonl` and
`harness/queue/cost-ledger.jsonl`, because SC19 requires the real ledger to
carry the fixture's full chain **after the demo runs** -- a tmp_path
sandbox could never satisfy that. The one thing it never touches for real
is `harness/queue/briefs/`: `audit_convert.convert`'s `--briefs` flag is
pointed at a demo-local, gitignored `harness/pipeline/demo/output/briefs/`
directory instead, so repeated demo runs never pollute the real brief queue
with numbered brief seeds nobody asked for.

Nine steps, each printing `STEP OK: <n> <description>` (verified: `grep -c
"STEP OK:" deliverables/full-auto-demo.log` = 9, comfortably above the
brief's `>= 8`, one line per stage: cleanup, fixture-present, review
recorded, auto-decided, converted, forge-run-mock, cost-ledger-linked,
archived, chain-verified):

0. **Idempotent cleanup** -- two small inline `py -` scripts (not `grep`/
   `sed`, to parse the JSONL correctly rather than by string luck) filter
   OUT any prior line for `CURSOR-FIXTURE-full-auto-demo` from both the
   audit ledger and the cost ledger (the cost-ledger filter is scoped to
   `event == "demo-full-auto-fixture"` so it never touches a real,
   non-demo cost entry that happened to share the audit_id), then removes
   the prior review/decision files and the whole demo output dir. Proven
   idempotent by actually re-running the script twice in a row and counting
   ledger lines before/after (see `006c-validation.log`): exactly 6 lines
   for this audit_id in the real ledger and exactly 1 in the cost ledger,
   unchanged across repeated runs.
1. Verifies the fixture audit exists and counts its dated web sources via a
   real regex over the real file (>= 3 required by the brief; the fixture
   carries exactly 3, each `https://... -- consulté le YYYY-MM-DD`).
2. `audit_review.py scaffold` then overwrites the scaffold with a real
   filled review (one CONFIRMED point), then `audit_review.py record` ->
   real `AUDIT_CHALLENGED` ledger append.
3. `audit_decision.py auto` -> real `AUDIT_APPROVED` (policy rule
   `review_has_confirmed_or_partial`) -- the CLI, not a Python call, so this
   is the exact command a headless CI job would run.
4. `audit_convert.py convert` -> real `AUDIT_CONVERTED` with `briefs: [...]`
   pointing at the demo-local brief seed.
5. **forge-run mock** -- explicitly named as a simulation in both the
   script's own comment and this log (no Claude/Cursor process spawned);
   what IS real is the next orchestrator call.
6. `orchestrator.py run --event evaluateur_pass` -> real `AUDIT_IMPLEMENTED`
   then `AUDIT_VERIFIED`, via the SAME single choke point (`audit_ledger.
   append_event`) Lot 006a/006b already guarantee -- this demo does not add
   a second way to reach those states.
7. `backends/ledger.py append --audit-id ...` -> a REAL, intentional write
   to `harness/queue/cost-ledger.jsonl`, carrying `audit_id:
   CURSOR-FIXTURE-full-auto-demo` -- this is the one CLI-level exercise of
   SC16's new flag mentioned above.
8. `audit_ledger.py append --event AUDIT_ARCHIVED` -> the terminal state.

A ninth `STEP OK:` line then re-reads the ledger via `audits.current_state`
and asserts the final state is literally `AUDIT_ARCHIVED` before the script
exits 0 -- this is the check the Évaluateur's own rejeu scenario in
`eval-rubric.md` names ("dernier step ARCHIVED ou VERIFIED").

Portability: `py`, never bare `python`, is used throughout on this dev
machine (hard-won rule 1) -- but the `py` launcher is Windows-only and does
not exist on `ubuntu-latest`, so the script resolves `PY=py` if found, else
`PY=python3` (never bare `python`, on either platform; matches Lot 006b's
own documented reasoning for why the CI-only jobs in `pipeline-*.yml` use
`python` on Linux and this dev machine's hook targets `python`/`python3`
specifically, not `py`).

`.gitignore` gained one new entry, `harness/pipeline/demo/output/`, with a
comment explaining why (the demo-local brief seed must never reach the real
queue).

### `CLAUDE.md` + `HANDOFF.md` pointers (SC21)

`CLAUDE.md`: one sentence added to the existing `architecture/**` bullet
pointing at `docs/adr/0006-full-auto-agent-pipeline.md` and
`docs/rules/full-auto-pipeline.md`, plus one new Routing-table row for
`harness/pipeline/**`. `HANDOFF.md`: one new Open TODO bullet naming the
same two files plus the two lot verdicts. Neither file restates a Success
Condition or a structural brief heading -- confirmed by re-running
`py -m pytest harness/tests/test_single_source_of_instruction.py -q` after
both edits (still 1 passed).

## A real finding fixed mid-run, and both artifacts corrected

The first version of `CURSOR-FIXTURE-full-auto-demo.md` used
`target_commit: 0000000000000000000000000000000fixture` -- readable, but
NOT a valid 40-hex SHA. `py -m pytest harness/tests/ -q` caught this for
real: `test_audit_schema.py::test_real_cursor_audits_pass` failed because
`harness/audit_schema.py`'s own `SHA_RE` (pre-existing, unrelated to this
lot) validates every audit in `architecture/inbox/`, including this new
fixture, against a strict `^[0-9a-fA-F]{40}$`. Fixed to
`000000000000000000000000000000000000000f` (verified 40 chars via a real
`len()` check, not eyeballed) in BOTH the fixture file and the demo
script's own review heredoc (which echoes the same `target_commit`); full
suite re-run afterward, 250 passed, 0 failed.

## How every measurement was actually taken

- `full_auto_demo_steps_count = 9`, `sample_size = 9`: `grep -c "STEP OK:"
  deliverables/full-auto-demo.log`, run against the log the demo script
  itself just wrote (`006c-validation.log`, first block).
- `web_sources_cited_count = 3`, `sample_size = 3`: a real regex
  (`https?://\S+.*?consult\w+ le \d{4}-\d{2}-\d{2}`) over the actual fixture
  file's text, not a manual count of the markdown source.
- `audit_to_brief_trace_count = 1`, `sample_size = 1`: parsed every line of
  the real `architecture/audit-ledger.jsonl` as JSON and counted
  `event == "AUDIT_CONVERTED"` records with a non-empty `briefs` field --
  exactly one exists in the whole ledger (the fixture's), confirmed by also
  grepping `AUDIT_CONVERTED` directly and reading the one matching line.
- `cost_ledger_audit_link_count = 1`, `sample_size = 1`: parsed every line
  of the real `harness/queue/cost-ledger.jsonl` as JSON and counted records
  carrying an `audit_id` key -- exactly the one the demo's step 7 wrote.
- The 006a/006b counters (`auto_policy_rules_count`,
  `fsm_invalid_transition_tests_count`, `agent_role_files_count`,
  `pipeline_workflows_count`) are unchanged from their own lots -- this run
  never touched `auto_policy.yaml`, `test_audit_fsm.py`,
  `architecture/agents/*.md`, or `.github/workflows/pipeline-*.yml`, so
  re-measuring them would reproduce the identical numbers; kept as-is.
- Full suite: `py -m pytest harness/tests/ -q` -> **250 passed** (226
  carried over from Lot 006b plus 12 `test_supervisor.py` + 5
  `test_forge_run_preflight.py` + 4 `test_ledger_audit_link.py` + 3
  `test_full_auto_pipeline.py` = 250), captured verbatim in
  `006c-validation.log`.

## What was deliberately NOT done here

- SC20 (`docs/rules/full-auto-pipeline.md` + kill-switch) -- already PASSED
  in Lot 006b, not in this run's explicit SC list, not re-touched.
- Did not flip `harness/pipeline/config.yaml`'s `mode` to `full_auto` --
  the brief's own closing line ("Après 006c: activer `mode: full_auto`")
  names this as the step AFTER this lot's gate/Évaluateur pass, not inside
  it; flipping it here, before the Évaluateur has judged this lot's own
  supervisor/demo/preflight work, would be exactly the "erroneous automatic
  decision" risk ADR-0006 itself names.
- Did not wire the `pipeline/pause` kill-switch label check into individual
  `pipeline-*.yml` workflow steps or `merge-bot.yml`'s merge step -- named
  in Lot 006b's own feedback as a "nice to have for 006c" but NOT part of
  this run's explicit SC enumeration (14-19, 21); left for a future lot if
  the owner opens one.
- Did not write `verdict.md`, and did not run `verdict_audit.py` as a
  self-judgment -- ran it once, informally, only to confirm the manifest
  itself parses (per the task's own permission for a self-check ACCEPT),
  never as this role's verdict.
