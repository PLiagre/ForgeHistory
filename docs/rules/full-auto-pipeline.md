# Full-Auto Pipeline — Activation, Roles, Emergency Disable

Brief `006-full-auto-agent-pipeline`, Lot 006b, Success Condition 20.
Normative operational doc for the loop `docs/adr/0006-full-auto-agent-pipeline.md`
authorizes as a derogation to ADR-0005's owner-in-the-loop accept/reject
step. This file documents **how to run it and how to stop it**; it does not
restate the brief's Success Conditions (`CLAUDE.md` › Single Source of
Instruction) — read `harness/queue/briefs/006-full-auto-agent-pipeline/brief.md`
for those.

2026-08-12 (ADR-0010): the three agent-invocation workflows are wired for
real — no `TODO(operator...)` stub remains.

2026-08-13 (ADR-0012, owner decision): the per-PR critique and the
per-merge post-audit are **retired** — that cadence exhausted the Claude
monthly subscription cap twice in 24 h through the counter-audits it
triggered. `pipeline-audit.yml` now fires only when a **project milestone**
closes (merge of a `hermes/milestones/ETAPE-*.md` marker — stages defined
in `ROADMAP.md` § « Grandes étapes — jalons d'audit ») or on explicit
`workflow_dispatch`. Everything downstream of the audit deposit (challenge,
decision, conversion) is mechanically unchanged and simply follows the new,
rarer cadence. Per-PR there remain only the free mechanical checks
(harness-ci, security, audit-guard incl. the brief-014 `audit-check` job,
merge-bot).

## Diagram

```
milestone merged on master (hermes/milestones/ETAPE-*.md), or workflow_dispatch
  │
  ▼
[cursor-auditor] ──(companion: cursor-qa-scout)──▶ architecture/inbox/CURSOR-<sha>-<slug>.md
  │  .github/workflows/pipeline-audit.yml (ADR-0012: milestone/dispatch only)
  ▼
auto-merge (merge-bot.yml, inbox/** allowlisted) ──▶ ledger AUDIT_PROPOSED (optional)
  │
  ▼
[claude-challenger] ──▶ architecture/reviews/CLAUDE-<audit_id>.md
  │  .github/workflows/pipeline-challenge.yml (push touching inbox/*.md)
  │  (the PR carries reviews/** ONLY -- never the shared ledger, so
  │   concurrent challenge PRs cannot conflict on audit-ledger.jsonl)
  ▼
auto-merge (merge-bot.yml, reviews/** allowlisted)
  │
  ▼
[pipeline-orchestrator] event=review_recorded ──▶ ledger AUDIT_CHALLENGED (post-merge,
  │  .github/workflows/pipeline-orchestrate.yml     via audit_review.record_challenge)
  │  (push touching reviews/*.md)                ──▶ audit_decision.decide_auto()
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

## How to activate the wired loop (state as of ADR-0010)

1. The wiring exists: `pipeline-forge-run.yml` installs the Claude Code and
   Codex CLIs and runs `claude -p "/forge-run <brief> --backend codex"`
   (executor model: `CODEX_MODEL=gpt-5.6-sol`, forwarded by
   `harness/backends/run_codex_generator.sh` as `codex exec --model`);
   `pipeline-challenge.yml` runs `claude -p "/forge-audit-review <audit_id>"`;
   `pipeline-audit.yml` launches a Cursor Cloud Agent through the official
   API (`POST https://api.cursor.com/v1/agents`). Every headless Claude call
   carries `--max-budget-usd 5.00` (owner arbitration n°2, 2026-08-11), is
   preceded by `harness/pipeline/ci_budget_guard.py precheck` (monthly cap,
   lot 009b) and followed by its post-hoc `record` marking.
2. Provision the GitHub Actions repository secrets —
   **subscription-quota first** (owner decision 2026-08-12: consume the
   Pro-plan quotas, never API credit), API keys only as fallback:
   - `CLAUDE_CODE_OAUTH_TOKEN` — subscription token generated locally by
     `claude setup-token` (Pro/Max/Team plans). Fallback:
     `ANTHROPIC_API_KEY` (API billing). If both are set, the CLI prefers
     the API key — so set only the token.
   - `CODEX_AUTH_JSON` — the full contents of `~/.codex/auth.json`
     produced locally by `codex login` (ChatGPT-managed auth; official
     OpenAI "CI/CD auth" procedure, private trusted repos only). Fallback:
     `OPENAI_API_KEY` (API billing). Known limit: on ephemeral runners the
     refreshed token is not persisted back, so the seed secret goes stale
     after roughly 8 days without a run refresh — re-run `codex login`
     locally and update the secret when the workflow reports an auth
     error.
   - `CURSOR_API_KEY` — the agent-specific key from the Cursor dashboard's
     Cloud Agents settings (not a generic dashboard key). Cloud Agents
     launched through the API draw from the same Cursor plan usage as
     agents launched from the dashboard — this key is already
     subscription-based.
   - `FORGE_BOT_PAT` — a fine-grained personal access token of the owner
     (repository: this one only; permissions: Contents read/write + Pull
     requests read/write). Used by `pipeline-challenge.yml` and
     `pipeline-forge-run.yml` to push `forge-bot/*` branches and open their
     PRs, and by `merge-bot.yml` to enable auto-merge. Why it exists:
     GitHub never triggers workflows on events caused by the built-in
     `GITHUB_TOKEN` — a bot-opened PR sits with every check stuck on a
     manual "Approve and run" button, and a bot-performed merge fires no
     downstream `push` workflow, silently stopping the chain. With the PAT
     the events are owner-authored and the loop runs unattended. Absent
     secret → automatic fallback to `GITHUB_TOKEN` (the degraded,
     approve-by-hand behaviour). The kill-switch label and `mode: manual`
     cut the loop regardless of which token is in use.
   Until credentials exist, every invocation step logs a documented
   `::warning::` waiver and no-ops instead of failing — full_auto never
   silently pretends to run.
3. `harness/pipeline/config.yaml` declares `mode: full_auto` since
   2026-08-12. The value is legal because
   `harness/pipeline/full_auto_mode_guard.py` re-reads the forge-run
   workflow on every call and no stub marker remains (ADR-0007's
   fail-closed reservation was for exactly this moment; the guard's narrow
   structural heuristic still refuses a missing/truncated/re-stubbed file).
4. `pipeline-forge-run.yml` and `pipeline-challenge.yml` consult the
   `mode:` key at runtime (the load-bearing check brief 009 lot 009c
   reserved): `manual` skips the invocation with a logged waiver.

## How to emergency-disable

Both controls are wired and load-bearing:

1. **`mode: manual`** in `harness/pipeline/config.yaml` — read at runtime
   by `pipeline-forge-run.yml` and `pipeline-challenge.yml` before any
   invocation; it declares the fallback to the ADR-0005 human loop
   (`/forge-audit-accept`, `/forge-audit-reject`, manual `/forge-run`).
   `ci_budget_guard.py precheck` also rewrites the key to `manual` itself
   when the monthly cap is reached. The manual value remains available
   (brief 006 Non-Goals: "ne pas supprimer la boucle manuelle").
2. **Kill-switch label `pipeline/pause`** — apply it to any open PR or
   issue in this repo. The three invocation workflows query
   `repos/<owner>/<repo>/issues?labels=pipeline/pause&state=open` before
   invoking any agent; one labelled open item is treated exactly like a
   missing credential — a documented waiver logged, no write attempted.

Either control is a single, human-authored, normally-reviewed change (or a
single label click for the pause).

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
