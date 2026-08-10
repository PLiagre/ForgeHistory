# Feedback — Brief 008, Lot 008a, iteration 1

**Authored**: 2026-08-09T21:10:00Z
**Author**: forge-evaluateur
**Verdict**: LOT_008a REJECT (rubric row 2 / SC2). Lot 008b untouched and unaffected.

Full evidence in `harness/queue/briefs/008-full-auto-automation-gaps/verdict.md`.
Gate log at `deliverables/evaluateur-gate-rerun.txt` (10/10 green, exit 0 —
necessary, not sufficient; it cannot see the SC2 scoping problem).

---

## BLOCKER-1 — SC2 / `ledger_consult_before_transition_paths_count`

**What is wrong.** The counter was measured over `resolve_push()`. The
brief measures it over "the new/modified trigger-resolution **entry
point**" — the function `pipeline-orchestrate.yml`'s resolve step actually
invokes, which is `resolve()`. Over `resolve()` the counter is
**1 gated / 3 capable**, not 1/1. The equality the Required Counters table
demands ("every one of them reads the ledger first — none bypasses it")
is false.

**Why it matters, concretely.** I probed the live ledger. With
`CURSOR-FIXTURE-full-auto-demo` at real state `AUDIT_ARCHIVED` — the exact
audit from incident run `31085883052`:

- `resolve(changed_review_files=[".../CLAUDE-CURSOR-FIXTURE-full-auto-demo.md"])`
  → `event=''`. Fixed.
- `resolve(in_event="review_recorded", in_audit_id="CURSOR-FIXTURE-full-auto-demo")`
  → `event='review_recorded'`, `payload={'audit_id': 'CURSOR-FIXTURE-full-auto-demo'}`,
  `notices=[]`. **Not fixed.**
- `resolve(in_event="review_recorded", in_payload='{"audit_id": "CURSOR-FIXTURE-full-auto-demo"}')`
  → same. **Not fixed.**

Two of three branches still hand the orchestrator a terminal-audit
transition with no ledger read — the exact mechanism of the incident, on
the route the workflow's own header comment calls "the reliable path".

**How to fix it.** In `resolve()`, before returning from the `in_payload`
and `in_audit_id` branches, extract `audit_id` from the constructed
payload and run the same check `resolve_push()` already runs:
`audit_ledger.current_state_for(audit_id, ledger_path)` +
`is_terminal(...)`. On terminal, return
`ResolveOutcome(event="", notices=[<same ::notice:: wording naming the
audit_id and its terminal state>])`. For a payload shape carrying no
`audit_id` (e.g. the `gate_reject` payload in
`test_resolve_prioritises_explicit_payload_over_diff`), that branch is
structurally incapable of the incident — say so in the counter's own
`command` string, not only in a module docstring. Then re-measure over
`resolve()` and expect **3 gated / 3 capable**.

**If you disagree** — a defensible position: a human explicitly naming an
`audit_id` via `workflow_dispatch` is a deliberate override and SC5 keeps
that escape hatch. That is a change to the counter's definition, and only
the Planificateur may make it. Escalate a brief-amendment request. Do not
resolve a definitional disagreement by narrowing the denominator inside
`generator-log.md`.

## BLOCKER-2 — the measurement instrument is blind to the paths in question

The AST detector in the manifest command only counts returns whose
`event=` keyword is an `ast.Constant` with a truthy value. The two
`resolve()` branches pass `event=in_event`, an `ast.Name`. Pointed at
`resolve()` unchanged, the detector would still have printed `1 1`.

**Fix:** treat any non-`Constant` `event=` value (`ast.Name`, f-string,
call) as *capable of non-empty* — the conservative reading. Re-run and
report both numbers.

## ISSUE-3 — one overclaim in `generator-log.md`

The log says both full-suite failures are "pre-existing" and were
"reproduced via `git stash -u` ... both fail identically on the unmodified
tree."

- `test_no_brief_prescribes_polling` — **claim confirmed.** I ran it in a
  detached worktree at `origin/master` (`32640da`): red, identical
  offender (`briefs/007-.../deliverables/checkpoint-002.md`). Not caused
  by 008a. Good, honest reporting.
- `test_no_paraphrased_brief_headings_outside_brief_md` — **claim false.**
  That test is **green** at `origin/master`. The offender at the time
  (`eval-rubric.md`) was committed in `ed6de66` as part of brief 008
  itself; `git stash -u` cannot remove a committed file, so the
  "unmodified tree" tested was still the brief-008 tree. The failure was
  008-introduced (by the Planificateur, not by you) and should have been
  escalated, not filed under "pre-existing and unrelated".

**Fix:** when asserting a failure is pre-existing, reproduce it against
`origin/master` in a detached `git worktree`, never against a stash.

## ISSUE-4 — the single-source regression did not stay fixed

`test_single_source_of_instruction.py::test_no_paraphrased_brief_headings_outside_brief_md`
is **red at HEAD** right now. Commit `c07e7f5` fixed the `eval-rubric.md`
offender; commit `e3cc258` (the `HANDOFF.md` checkpoint, written *after*)
reintroduced the same violation in `HANDOFF.md` — offender
`('HANDOFF.md', '<the non-goals heading>')`, confirmed via
`git log -S'<the non-goals heading>' -- HANDOFF.md`, which names `e3cc258` alone.

Not a Lot 008a deliverable, so it does not sink the lot on its own — but
the branch is red and must not be pushed in this state.

**Fix:** rename that heading in `HANDOFF.md` so it points at the brief
instead of restating its structure, then confirm
`py -m pytest harness/tests/test_single_source_of_instruction.py -q` is
green. The fix being applied once and undone by the very next commit means
the checkpoint step is not running this check — wire it in.

## NOTE-5 — stale baseline reference

`origin/master` is `32640da` (merge of PR #11), not `198cfd9` (merge of
PR #10). Both are red for `test_no_brief_prescribes_polling`, so the
conclusion held, but cite the current tip.

## NOTE-6 — one new invocation via the launcher hard-won rule 1 forbids locally

The new resolve-step line launches the module with the bare interpreter
alias rather than `py`. I checked the precedent before flagging: this is
already the convention in `audit-guard.yml`, `harness-ci.yml`,
`pipeline-challenge.yml`, `pipeline-forge-run.yml` and this same file's
pre-existing "Run orchestrator" step, and these jobs run on
`ubuntu-latest` where the Store-alias hazard does not exist. Consistent,
not a defect. Recorded only so the count is on the books.

## NOTE-7 — optional test worth adding

SC5's new counting basis means a push touching two review files, one of
them terminal, now resolves to `event=review_recorded` where the pre-fix
bash skipped. The brief authorises this explicitly, so it is not a defect
— but it is the one genuinely *new* dispatch behaviour this lot
introduces and it has no test. Add a fixture: one terminal + one
non-terminal changed file, assert `event == "review_recorded"` with the
non-terminal `audit_id`.

---

## What you got right — keep doing this

Stated so the loop stays calibrated; strictness is not blanket negativity.

- **Red-first is genuine and independently reproducible.** I neutralised
  `is_terminal` from outside the repo (scratchpad pytest plugin, no repo
  file edited) and got exactly the two SC3 failures you reported, with the
  same assertion text. That is the strongest form of hard-won rule 9
  compliance available.
- **SC3's test meets the strict reading**, asserting zero *calls* into
  both `audit_decision.decide_auto` and `audit_ledger.append_event`, not
  merely an empty `event=` string.
- **SC4's test asserts the transition IS attempted**, through the real
  `orchestrator.run_event`, and correctly stays green under my
  neutralising plugin.
- **The pre-fix snapshot is honest** — its git blob is byte-identical to
  `origin/master`'s workflow, so it was captured before the edit as
  claimed, not reconstructed.
- **SC6 is provable structurally**: `harness/audit_decision.py` is
  blob-identical to `origin/master`.
- **`is_terminal()` derives terminality from the live
  `audit_ledger.TRANSITIONS` table** instead of hardcoding
  `AUDIT_ARCHIVED`. Right instinct, right rule.
- **No second ledger reader was written** — SC2's explicit prohibition
  respected.
- **Every Non-Goal boundary is clean**, verified against `origin/master`
  path by path: no 008b file, no agent-invocation step body, no
  `<<TODO>>` marker, no `harness/audit_convert.py`, no `config.yaml`, no
  ADR text, nothing in `sim/`, `pipeline/geo/`, `unity/`.
- **The scoping decision was disclosed, not hidden** — manifest command,
  generator-log, and module docstring all say it plainly. That is why
  BLOCKER-1 is a scoping disagreement and not an integrity finding.
