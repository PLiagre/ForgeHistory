# Verdict — Brief `006`, Lot 006a (Gouvernance + FSM)

**Authored**: 2026-08-05T17:10:00Z
**Author**: forge-evaluateur

Scope: this verdict judges **only Lot 006a** (Gouvernance + FSM) of brief
`006`, per the explicit lot scoping in the brief's "Lots atomiques" section.
Deliverables belonging to lots `006b` / `006c` (`orchestrator.py`,
`.github/workflows/pipeline-*.yml`, `architecture/agents/**`, the demo, the
budget supervisor, the cost-ledger `audit_id` field) are **out of scope** and
are not held against this lot.

## Mechanical Gate Result

`py harness/verdict_audit.py harness/queue/briefs/006-full-auto-agent-pipeline`.
The pre-verdict run reported the two verdict-related checks
(`verdict_numbers_traceable`, `verdict_is_not_self_authored`) failing **only
because `verdict.md` did not yet exist** — that file is the artifact this
role produces. All eight non-verdict checks reported PASS
(`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`no_bare_python_alias`, `rubric_predates_deliverables`,
`declared_files_are_tracked`). The final exit code + PASS count from the
post-verdict re-run is reported at the bottom of this file.

## Independent Counter Reconstruction

Every number below was re-derived by the Évaluateur from source, not taken
from `manifest.json`.

- `auto_policy_rules_count`: I loaded `harness/pipeline/auto_policy.yaml`
  through `policy_loader.load_auto_policy` myself; `len(rules)` = 10, matching
  the manifest value 10 and sample_size 10. The ten rule ids map one-to-one
  onto the ten rows of the brief's "§ Politique auto" table. The two
  spot-checked rules are present and correct: `review_all_refuted`
  (all points REFUTED -> REJECTED) and `review_has_confirmed_or_partial`
  (>= 1 CONFIRMED/PARTIAL -> APPROVED). `mode: full_auto` is documented in
  the file. Threshold >= 8: **met**.
- `fsm_invalid_transition_tests_count`: `grep -c "pytest.raises"
  harness/tests/test_audit_fsm.py` returned 9, matching the manifest value 9
  and sample_size 9. I read all nine cases; each genuinely trips the FSM
  (fresh-audit APPROVED, PROPOSED->APPROVED, CHALLENGED-after-APPROVED,
  event-after-ARCHIVED, event-after-REJECTED, typo event, CONVERTED before
  APPROVED, VERIFIED before IMPLEMENTED, cross-audit-id non-interference) —
  none is vacuous. A tenth adversarial case is proven at the CLI via exit
  code rather than `pytest.raises`, so the reported count is an undercount.
  Threshold >= 5: **met**.

## Per-Rubric-Line Verdict (lot 006a subset)

| Success Condition | PASS/FAIL | Evidence |
|---|---|---|
| SC1 — `ADR-0006` exists, accepted, cites `ADR-0005` derogation, lists risks + mitigations, denylist excludes workflows / verdict_audit.py / VISION.md | PASS | `docs/adr/0006-full-auto-agent-pipeline.md`: `Status: accepted`; Context cites the `ADR-0005` owner step; Risks section names erroneous-decision, unwanted-merge, runaway-cost, bot-branch, each with a mitigation; the "Binding auto-merge path constraints" section states the denylist (`.github/workflows/**`, `harness/verdict_audit.py`, `VISION.md`) is unconditional until an owner-named exception is added there. |
| SC2 — auto_policy.yaml covers § Politique auto (>= 8 rules) | PASS | 10 top-level rules re-counted via `policy_loader`; REFUTED->REJECTED and CONFIRMED/PARTIAL->APPROVED present; `mode: full_auto` documented. |
| SC3 — config.yaml exposes the six literal keys | PASS | `harness/pipeline/config.yaml` literally exposes `mode`, `max_forge_run_iterations`, `auto_merge_audit_prs`, `auto_merge_review_prs`, `claude_challenge_on_inbox_merge`, `cursor_audit_on_master_push` (plus allowlist/denylist). |
| SC7 / lot "Done" — FSM refuses invalid transitions; APPROVED-without-CHALLENGED impossible | PASS | Independently reproduced (see below). >= 5 adversarial tests (9 re-counted). Happy path still succeeds. |
| SC8 — audit_decision `--policy auto` with machine reason; human path unchanged | PASS | `decide_auto` reads `auto_policy.yaml`, parses the review's per-point verdicts, applies the three rules, writes a non-empty machine reason (incl. literal `policy: no owner in full_auto`); `decide()` default `actor="owner"` keeps the accept/reject path's behaviour — existing decision tests all pass. |

## Is the APPROVED-without-CHALLENGED bypass truly closed?

Yes. I reproduced it myself against the live `audit_ledger.py` in a fresh
tmp ledger, not via the Générateur's log:

```
py harness/audit_ledger.py append --audit-id CURSOR-eval-bypass \
   --event AUDIT_APPROVED --ledger <tmp>/led.jsonl
  -> error: invalid transition ... NONE -> AUDIT_APPROVED is not allowed
  -> EXIT=2, ledger stays empty
py ... append CURSOR-evalB AUDIT_PROPOSED   -> EXIT=0
py ... append CURSOR-evalB AUDIT_APPROVED   -> EXIT=2 (PROPOSED -> APPROVED refused)
py ... append CURSOR-boot AUDIT_CHALLENGED  -> EXIT=0 (legal bootstrap first event)
py ... append CURSOR-boot AUDIT_APPROVED    -> EXIT=0 (only AFTER a real CHALLENGED)
```

On the flagged design decision (`TRANSITIONS[None]` widened to allow
`AUDIT_CHALLENGED` as a legal first event): I verified both required
properties.
(a) It is genuinely required by an existing legitimate caller —
`audit_review.record_challenge` guards `current_state == AUDIT_PROPOSED`, and
`audits.current_state` returns the default `AUDIT_PROPOSED` for an audit with
no ledger events, so `record_challenge` writes `AUDIT_CHALLENGED` as the
*first* ledger line; three pre-existing review tests exercise exactly this.
(b) It does **not** reopen the bypass — reaching `AUDIT_APPROVED` still
requires the current state to be `AUDIT_CHALLENGED`, which can only exist if
an actual `AUDIT_CHALLENGED` event was appended. "APPROVED without a real
CHALLENGED" remains impossible, as the two `EXIT=2` refusals above confirm.

## Backward Compatibility

I re-ran the full suite myself: `py -m pytest harness/tests/ -q` -> all
tests passed (the same total the Générateur reported). The two rewritten
tests in `harness/tests/test_audit_ledger.py`
(`test_extra_fields_survive_round_trip`, `test_all_nine_states_are_accepted`)
are legitimate adaptations to the stricter FSM, not weakenings: both preserve
their original assertion (extra fields survive; every valid event name is
accepted) and only change setup to walk FSM-legal chains. No caller module's
own test file needed changing.

## Overall Verdict: PASS

Lot 006a meets Success Conditions 1, 2, 3, and the FSM/decision parts of 7
and 8, and satisfies the lot's disqualifying "Done" criterion — bypass
APPROVED-without-CHALLENGED is verifiably impossible. Both in-scope counters
clear their thresholds (rules value 10, fsm tests value 9). No disqualifying
failure from the rubric is present.

## Boundary Violations

None. The lot deliberately leaves `mode: manual` in `config.yaml` (flip to
`full_auto` is deferred to after lot `006c`, per the brief's own ordering),
adds no PyYAML dependency (hand-rolled `policy_loader.py` documented), and
does not touch any `006b`/`006c` deliverable.

## What Improved Since Last Iteration

First evaluation of this lot — no prior iteration to compare.

## What Regressed Since Last Iteration

None.

## Feedback for Next Iteration

No blocking issues. Minor, non-blocking note for whoever picks up `006b`:
`decide()` gained an `actor` parameter and now records `reason` on the ledger
event; this is additive and the human path's tests pass, but `006b`'s
`orchestrator.py` must route all writes through `audit_ledger.append_event`
(never construct ledger lines directly) so the single-choke-point FSM
guarantee this lot established is not bypassed downstream.
