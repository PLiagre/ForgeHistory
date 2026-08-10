# Full-Auto Pipeline — Activation, Roles, Emergency Disable

Brief `006-full-auto-agent-pipeline`, Lot 006b, Success Condition 20.
Normative operational doc for the loop `docs/adr/0006-full-auto-agent-pipeline.md`
authorizes as a derogation to ADR-0005's owner-in-the-loop accept/reject
step. This file documents **how to run it and how to stop it**; it does not
restate the brief's Success Conditions (`CLAUDE.md` › Single Source of
Instruction) — read `harness/queue/briefs/006-full-auto-agent-pipeline/brief.md`
for those.

## Diagram

```
merge code (Claude/bot) on master
  │
  ▼
[cursor-auditor] ──(companion: cursor-qa-scout)──▶ architecture/inbox/CURSOR-<sha>-<slug>.md
  │  .github/workflows/pipeline-audit.yml (push master)
  ▼
auto-merge (merge-bot.yml, inbox/** allowlisted) ──▶ ledger AUDIT_PROPOSED (optional)
  │
  ▼
[claude-challenger] ──▶ architecture/reviews/CLAUDE-<audit_id>.md
  │  .github/workflows/pipeline-challenge.yml (push touching inbox/*.md)
  ▼
auto-merge (merge-bot.yml, reviews/** allowlisted) ──▶ ledger AUDIT_CHALLENGED
  │
  ▼
[pipeline-orchestrator] event=review_recorded ──▶ audit_decision.decide_auto()
  │  .github/workflows/pipeline-orchestrate.yml (push touching reviews/*.md)
  │
  ├─ all REFUTED ─────────────────▶ ledger AUDIT_REJECTED → AUDIT_ARCHIVED (end cycle)
  ├─ ≥1 CONFIRMED/PARTIAL ────────▶ ledger AUDIT_APPROVED (retained points)
  └─ NEEDS_OWNER only ────────────▶ ledger AUDIT_REJECTED ("policy: no owner in full_auto")
  │
  ▼  (APPROVED only)
[pipeline-orchestrator] event=audit_approved ──▶ audit_convert.convert()
  │                                                ──▶ ledger AUDIT_CONVERTED, brief seed(s)
  ▼
[claude-planificateur] fills brief <<TODO>> (separate invocation, same pipeline)
  │
  ▼
[claude-developer] /forge-run <brief>  ──▶ deliverables + gate
  │  .github/workflows/pipeline-forge-run.yml (workflow_dispatch or `forge-run/queued` label)
  │  mandatory preflight: harness/budget.py split-check (brief 006 SC15, Lot 006c)
  ▼
gate ACCEPT ──▶ [claude-evaluator] (forge-evaluateur) ──▶ verdict.md PASS
  │
  ▼
merge (CI green; deny-listed paths NEVER auto-merged — see .github/merge-bot.yaml)
  │
  ▼
[pipeline-orchestrator] event=evaluateur_pass ──▶ ledger AUDIT_IMPLEMENTED → AUDIT_VERIFIED
  │
  ▼
audit_archive.py ──▶ ledger AUDIT_ARCHIVED
```

Off-path branches, both machine-handled, no human wait:
- 3 consecutive mechanical REJECTs on the same brief →
  `pipeline-orchestrator` event `gate_reject` with `reject_streak >= 3` →
  escalate `pipeline-stuck` (a bot-filed issue, not a blocked pipeline).
- `BUDGET_EXHAUSTED` on a Générateur run → `harness/budget.py checkpoint`
  writes the handoff; the continuation brief is enqueued, never a blind
  retry (Lot 006c's supervisor owns the SIGTERM enforcement).

## Roles

Six fixed contracts under `architecture/agents/<role-id>.md` (invocation
table in `architecture/agents/README.md`): `cursor-auditor`,
`cursor-qa-scout`, `claude-challenger`, `claude-developer`,
`claude-evaluator`, `pipeline-orchestrator`. No role cumulates
"builds code" and "renders final judgement" — the same separation
`docs/rules/harness-roles.md` already enforces for the ordinary (non-audit)
harness loop.

## How to activate `mode: full_auto_decision_only`

1. Confirm Lot 006a + 006b + 006c are all merged (the FSM ledger, the
   policy table, the six role contracts, the four `pipeline-*.yml`
   workflows, the budget supervisor, and the end-to-end demo — see the
   brief's "Lots atomiques").
2. Provision the credentials the waivers in
   `harness/queue/briefs/006-full-auto-agent-pipeline/deliverables/manifest.json`
   name as missing today (`CURSOR_API_KEY` for Cloud Agent invocation,
   `ANTHROPIC_API_KEY` for headless Claude) as GitHub Actions repository
   secrets. Until they exist, every workflow that would call an external
   agent logs a documented waiver and no-ops instead of failing — full_auto
   cannot silently pretend to run without them.
3. Edit `harness/pipeline/config.yaml`: set `mode: full_auto_decision_only`
   (brief 009 / ADR-0007 narrowed the single `full_auto` value to this
   name). As of brief 009 Lot 009a, this activates the audit ->
   owner-decision half of the diagram above unattended; the challenge link
   (`claude-challenger`, `pipeline-challenge.yml`) is wired by Lot 009c,
   not yet as of this lot -- until 009c lands, `pipeline-challenge.yml`'s
   own invocation step is still the documented `TODO(operator...)` stub.
   The unqualified `full_auto` value is reserved and refused fail-closed by
   `harness/pipeline/full_auto_mode_guard.py` until `forge-run`'s own
   invocation is wired for real.
4. Commit and merge that single-line change (this file IS allowed to be
   part of a normal, human-reviewed PR — flipping the switch is not itself
   a bot action).
5. From the next `push` to `master`, the audit -> owner-decision half of
   the diagram above runs unattended; the `[claude-challenger]` step in
   between stays a documented stub until Lot 009c lands (see step 3).

## How to emergency-disable

Two independent switches, either one alone stops the loop from taking a
NEW automatic action (neither retroactively undoes anything already
merged):

1. **`mode: manual`** in `harness/pipeline/config.yaml` — falls back to the
   ADR-0005 human loop (`/forge-audit-accept`, `/forge-audit-reject`,
   manual `/forge-run`). This is the same `mode:` key the activation step
   above sets; it never had that key removed as an option (brief 006
   Non-Goals: "ne pas supprimer la boucle manuelle").
2. **Kill-switch label `pipeline/pause`** — apply it to any open PR or
   issue in this repo. Every `pipeline-*.yml` workflow's automatic-action
   steps (the Cursor/Claude invocation, the orchestrator's ledger writes,
   `merge-bot.yml`'s `gh pr merge --auto`) must be preceded by a check for
   this label on the PR/issue in question before taking any write action;
   a labelled item is treated exactly like a missing credential — a
   documented waiver logged, no write attempted. (Wiring this check into
   every workflow step is Lot 006c's responsibility per the brief's own
   Lot ordering; this doc names the contract so 006c has a fixed target,
   not a paraphrase of Lot 006c's own Success Conditions.)

Either switch is a single, human-authored, normally-reviewed change — not
itself a full_auto action, so it cannot be blocked by the loop it disables.

## Known gap (real, not narrated)

`gh api repos/{owner}/{repo}/branches/master/protection` on this repo
returns `403` — `"Upgrade to GitHub Pro or make this repository public to
enable this feature."` (reproduced twice, real `gh api` calls; see
`harness/queue/briefs/006-full-auto-agent-pipeline/deliverables/generator-log.md`
and the waiver recorded in `deliverables/manifest.json`). This is exactly
the brief's Acceptable Waivers row 3 ("branch protection empêche
auto-merge bot" → `403 or rules blocking`) — on this repo's current plan,
the branch-protection **feature itself** is unavailable, not merely
unconfigured, so it can enforce **none** of the `deny_paths` boundary in
`.github/merge-bot.yaml`. Per the brief's own text for that waiver, this is
a **partial** waiver: the pipeline must stop before the actual auto-merge
rather than rely on GitHub-side protection, which
`.github/workflows/merge-bot.yml` already does two ways — its own
`git diff` path check runs and refuses *before* the `gh pr merge --auto`
step, and that step itself treats a refusal (including one caused by the
auto-merge feature being unavailable on this plan) as a soft failure that
leaves the PR open rather than erroring the job. `merge-bot.yml`'s own
path check is therefore the *only* thing standing between a bot PR and a
merge touching `.github/workflows/**`, `harness/verdict_audit.py`, or
`VISION.md` until this repo is made public or upgraded to a plan with
branch protection — recorded here as a risk, not silently worked around.
