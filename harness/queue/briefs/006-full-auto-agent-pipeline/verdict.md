# Verdict — Brief `006`, Lot `006c` (Budget supervisor + démo E2E + traçabilité coût — FINAL LOT)

**Authored**: 2026-08-05T23:45:00Z
**Author**: forge-evaluateur

Scope: this verdict judges **only Lot `006c`**, the final lot of brief `006`,
per the explicit lot scoping in the brief's "Lots atomiques" section and the
task given to this role. In-scope Success Conditions: **`SC14`, `SC15`, `SC16`,
`SC17`, `SC18`, `SC19`, `SC21`**, plus the two brief-closing "Overall Verdict
Rule" disqualifiers that only become real once the pipeline is complete.

Lots `006a` (`SC1`–`SC3`, `SC7`–`SC8`) and `006b` (`SC4`–`SC6`, `SC9`–`SC13`,
`SC20`) already PASSED in `verdict-006a.md` / `verdict-006b.md` and are **not
re-litigated**.

## Mechanical Gate Result

`py harness/verdict_audit.py harness/queue/briefs/006-full-auto-agent-pipeline`
— re-run by this role: **exit `0`, `VERDICT: ACCEPT`**, all `10` checks PASS
(`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`verdict_numbers_traceable`, `no_bare_python_alias`,
`verdict_is_not_self_authored`, `rubric_predates_deliverables`,
`declared_files_are_tracked`). The tool's own stdout is the log of record (not
re-typed here per hard-won rule `12`). A mechanical ACCEPT is necessary but not
sufficient — every counter below was independently reconstructed from source.

## Independent Counter Reconstruction

Every number re-derived from source by this role, not taken from
`manifest.json`.

- **`full_auto_demo_steps_count`** (manifest value 9, sample_size 9): I actually
  ran `bash harness/pipeline/demo/run_full_auto_demo.sh` myself. **Exit `0`**;
  `grep -c "STEP OK:" deliverables/full-auto-demo.log` = 9, which clears the
  brief's `>= 8` floor. Last step is `STEP OK: 8 ... final state AUDIT_ARCHIVED`
  — matching the rubric's rejeu-scenario expectation ("dernier step ARCHIVED ou
  VERIFIED"). **Idempotency reproduced**: I ran the demo a SECOND time — again
  exit `0`, again 9 `STEP OK:` lines; the fixture's line count in
  `architecture/audit-ledger.jsonl` stayed at exactly `6` and the cost-ledger's
  `audit_id` entry count stayed at exactly `1` across both runs. No
  duplication, no corruption, no FSM trip on the second run. Threshold `>= 8`:
  **met**.
- **`web_sources_cited_count`** (manifest value 3, sample_size 3): I counted the
  dated URLs in `architecture/inbox/CURSOR-FIXTURE-full-auto-demo.md` myself
  via the manifest's own regex — exactly `3`, each carrying a `consulté le
  YYYY-MM-DD` date (anthropic building-effective-agents, github actions
  triggering-a-workflow, anthropic pricing; all consulted `2026-08-05`).
  Threshold `>= 3`: **met**.
- **`audit_to_brief_trace_count`** (manifest value 1, sample_size 1): I parsed
  every line of `architecture/audit-ledger.jsonl` as JSON myself and counted
  `AUDIT_CONVERTED` records with a non-empty `briefs[]` — exactly `1`, for
  `CURSOR-FIXTURE-full-auto-demo`, whose `briefs[]` names the demo-local brief
  seed under `harness/pipeline/demo/output/briefs/` (never the real queue).
  Threshold `>= 1` with non-empty `briefs[]`: **met**.
- **`cost_ledger_audit_link_count`** (manifest value 1, sample_size 1): I
  grepped/parsed `harness/queue/cost-ledger.jsonl` myself for entries carrying
  an `audit_id` field — exactly `1`, `audit_id=CURSOR-FIXTURE-full-auto-demo`,
  `event=demo-full-auto-fixture`, pointing at the same demo-local brief. Threshold
  `>= 1`: **met**.

## Per-Rubric-Line Verdict (Lot `006c` subset)

| Success Condition | PASS/FAIL | Evidence |
|---|---|---|
| `SC14` — budget supervisor SIGTERMs the Générateur at `HARD_STOP_CALLS`; integration test | PASS | `harness/pipeline/supervisor.py` imports `budget.HARD_STOP_CALLS` (single source, never redefined) and `transcripts.count_tool_calls` (single reader). I ran `py -m pytest harness/tests/test_supervisor.py -q` → `12` passed. `test_supervise_sigterms_child_at_hard_stop` launches a REAL 60s-sleep child, feeds a fixture transcript at exactly `HARD_STOP_CALLS`, and asserts the child is no longer alive after `supervise()` returns `SIGTERM` — not a mock. `test_supervise_leaves_child_running_below_threshold` proves it does NOT fire at `HARD_STOP_CALLS - 1`. `budget.py` is byte-for-byte untouched (`git diff HEAD -- harness/budget.py` empty; not in `git status`) — no destructive rewrite. |
| `SC15` — `/forge-run` split-check obligatoire; non-zero exit blocks on NEEDS_SPLIT | PASS | `harness/pipeline/forge_run_preflight.py` calls `budget.cmd_split_check` (the ONE place the trigger is computed) and reads back its `advisory` line. I ran it myself against this brief with `--estimated-calls 500` → **exit `1`** (BLOCKED, NEEDS_SPLIT); with `--estimated-calls 30` → exit `0` (SIZE_OK); omitting `--estimated-calls` → exit `2` (required flag). `py -m pytest harness/tests/test_forge_run_preflight.py -q` → `5` passed, incl. a control test proving `budget.py` itself is still advisory/exit-0. `.claude/commands/forge-run.md` states the check "obligatory, not advisory, for this command" and names the wrapper's blocking contract; it does NOT paraphrase Success Conditions (single-source test passes, see `SC21`). |
| `SC16` — cost-ledger optional `audit_id` field; audit→brief→cost link test; backward-compatible | PASS | `harness/backends/ledger.py::append_entry` adds `audit_id` to the JSON record only when given (`if audit_id:`), never a bare `null`. I ran `py -m pytest harness/tests/test_ledger_audit_link.py -q` → `4` passed, incl. `test_append_entry_without_audit_id_omits_the_field` (no key when omitted — byte-identical to pre-lot) and the full `audit_convert → AUDIT_CONVERTED.briefs[] → cost entry` join. Old entries without the field still load (`load_entries` / `report` use `.get`). Backward compatibility confirmed (see section below). |
| `SC17` — integration test full chain sans humain (subprocess/tmp_path) | PASS | `py -m pytest harness/tests/test_full_auto_pipeline.py -q` → `3` passed. `test_full_chain_no_human_input` runs the real modules (`audit_review`, `audit_decision.decide_auto`, `audit_convert`, `orchestrator`, `audit_ledger`) against a `tmp_path` ledger, reaching `CHALLENGED → APPROVED → CONVERTED → IMPLEMENTED → VERIFIED → ARCHIVED`, then proves nothing follows `ARCHIVED` (`pytest.raises(TransitionError)`). It uses `decide_auto` (`actor="policy:auto"`), NEVER the human `decide()`. `test_no_human_decision_call_anywhere_in_this_file` parses the test file's own AST and asserts zero `audit_decision.decide(` calls — mechanical, not a promise. |
| `SC18` — demo script exit 0 on CI, `>= 8` `STEP OK:` lines | PASS | Reconstructed above: I ran the script, exit `0`, `9` `STEP OK:` lines, idempotent on a second run. Portable (`PY=py` else `python3`, never bare `python`). |
| `SC19` — demo ledger chain complete for `CURSOR-FIXTURE-full-auto-demo` | PASS | I read `architecture/audit-ledger.jsonl` myself: the fixture's events are `AUDIT_CHALLENGED → AUDIT_APPROVED → AUDIT_CONVERTED → AUDIT_IMPLEMENTED → AUDIT_VERIFIED → AUDIT_ARCHIVED`. The chain ends `IMPLEMENTED → VERIFIED → ARCHIVED` exactly as required. (`AUDIT_PROPOSED` is legitimately optional per the `006a` FSM `TRANSITIONS[None]`.) |
| `SC21` — `CLAUDE.md` + `HANDOFF.md` pointers only, no SC paraphrase | PASS | I grepped both files myself: `CLAUDE.md` points to the full-auto ADR and `docs/rules/full-auto-pipeline.md` (lines `51`–`52`) and adds a `harness/pipeline/**` routing row (line `105`). `HANDOFF.md` adds a TODO bullet pointing at the same two files (lines `84`–`85`). Neither paraphrases a Success Condition: `py -m pytest harness/tests/test_single_source_of_instruction.py -q` → `1` passed. |

## Brief-closing disqualifier checks (reproduced myself)

**Disqualifier: "Human step still required for accept/reject in `mode:
full_auto` without documented waiver."** — NOT triggered. The full-chain path
reaches `VERIFIED`/`ARCHIVED` with NO `audit_decision.decide()` human call:
- In the demo, step 3 runs `py harness/audit_decision.py auto` (the `--policy
  auto` CLI) → `AUDIT_APPROVED` with `actor=policy:auto`; the log line itself
  reads "no owner call".
- In `SC17`, the chain uses `decide_auto` only, and the AST test proves no
  `decide(` call exists anywhere in the test file.
- I re-verified the recorded artifact: the fixture's `AUDIT_APPROVED` ledger
  event was written by the policy path, and `SC17`'s second test asserts
  `actor == "policy:auto"` and `!= "owner"`. No human hand is required between
  audit and archive.

**Disqualifier: "FSM bypass not reopened by the demo/supervisor path."** — NOT
triggered. The demo routes every ledger write through the `006a`/`006b` single
choke point: steps 2/3/4/7 use the `audit_review`/`audit_decision`/
`audit_convert`/`audit_ledger` CLIs (all funnel through
`audit_ledger.append_event`), and step 5 uses `orchestrator.py run --event
evaluateur_pass` (also `append_event`). `supervisor.py` touches no ledger at
all. I re-ran the live adversarial check against a fresh tmp ledger:
`py harness/audit_ledger.py append --event AUDIT_APPROVED` on a brand-new
`audit_id` → `error: invalid transition ... NONE -> AUDIT_APPROVED is not
allowed`, **exit `2`, ledger stays empty**. APPROVED-without-CHALLENGED remains
impossible. The full suite (`250` passed) includes `test_audit_fsm.py`.

## Backward Compatibility

I re-ran the FULL suite myself: `py -m pytest harness/tests/ -q` → **`250`
passed**, matching the Générateur's reported total (`226` from `006b` plus `12`
supervisor + `5` preflight + `4` ledger-link + `3` full-auto-pipeline). No
pre-existing test weakened. `budget.py` is untouched (no destructive rewrite of
the split-check logic — the wrapper reads its output rather than duplicating the
trigger). The cost-ledger `audit_id` field is additive: entries without it are
byte-identical to before and still load. `config.yaml`'s `mode` is still
`manual` — nothing in this lot flips the switch.

## Overall Verdict: PASS

Lot `006c` meets Success Conditions `SC14`, `SC15`, `SC16`, `SC17`, `SC18`,
`SC19`, `SC21`. All four in-scope counters clear their thresholds
(reconstructed independently: demo steps `9 >= 8`; web sources `3 >= 3`;
audit→brief trace `1`; cost-ledger link `1`). Neither brief-closing
disqualifier is present: the full-auto path reaches ARCHIVED with no human
decision call, and the FSM bypass is not reopened. The mechanical gate exits
`0`.

## Boundary Violations

None. The lot correctly leaves `config.yaml`'s `mode: manual` (the brief's own
"Après `006c`" ordering flips to `full_auto` only after this lot's gate/verdict
pass — flipping it here would be exactly the "erroneous automatic decision"
risk the full-auto ADR names). It does not rewrite `budget.py`, does not touch
the real `harness/queue/briefs/` queue (demo brief seed goes to a gitignored
demo-local dir), and does not re-touch `SC20` (delivered/PASSED in `006b`). The
demo's real writes to `architecture/audit-ledger.jsonl` and
`harness/queue/cost-ledger.jsonl` are idempotent and scoped to the fixture
`audit_id`.

## What Improved Since Last Iteration

First evaluation of Lot `006c` — no prior iteration of this lot. All five
carry-forward items named in `verdict-006b.md`'s "Feedback for Lot `006c`" are
delivered: `SC14` supervisor with a real-child SIGTERM test; `SC15` split-check
obligation wired into `.claude/commands/forge-run.md` itself (not just the
workflow); `SC16` optional `audit_id` with an audit→brief→cost link test;
`SC17`/`SC18`/`SC19` the end-to-end test, idempotent demo, and complete fixture
ledger chain.

## What Regressed Since Last Iteration

None.

## Feedback for Next Iteration

No blocking issues. Non-blocking, out-of-scope note (NOT held against this lot,
named only so the owner sees it): the `pipeline/pause` kill-switch label check
is documented in `docs/rules/full-auto-pipeline.md` as a contract every
`pipeline-*.yml` write step "must" honor, but the per-step wiring into the
workflow files and `merge-bot.yml`'s merge step was explicitly outside this
lot's SC enumeration and is not yet mechanically enforced. If the owner wants
the kill-switch to be more than documentation, that wiring is a candidate for a
future brief.

## Brief-closing note: is brief `006` complete and `mode: full_auto` activable?

Yes. With `006a` (PASS), `006b` (PASS), and `006c` (PASS) all cleared, every
Success Condition `SC1`–`SC21` of brief `006` is now met and independently
verified. The full-auto loop is proven end-to-end with no human input — both by
the `tmp_path` `SC17` integration test and by the reproducible, idempotent demo
that drives the REAL ledgers from inbox audit to `AUDIT_ARCHIVED` via
`--policy auto`. The FSM single-choke-point guarantee holds, the auto-merge
denylist unconditionally excludes `.github/workflows/**`,
`harness/verdict_audit.py`, and `VISION.md`, and the budget supervisor provides
the external SIGTERM enforcement that brief `003`'s `1015`-call runaway proved
was missing. `config.yaml` correctly still reads `mode: manual`; flipping it to
`mode: full_auto` (the brief's own closing step "Après `006c`") is now a safe,
supported activation — the whole brief `006` is complete.
