# Eval Rubric — Brief 006 (pipeline multi-agents full-auto)

**Authored**: 2026-08-05T10:05:01Z

Rubric écrite **avant** tout travail du Générateur. L'Évaluateur l'applique
sans la réviser après avoir vu les livrables.

| # | Success Condition (from brief.md) | Checked by |
|---|---|---|
| 1 | ADR-0006 existe, accepted, cite dérogation ADR-0005 + risques/mitigations | manual: open `docs/adr/0006-full-auto-agent-pipeline.md`; grep `ADR-0005`, `full_auto`, `Risks` |
| 2 | `auto_policy.yaml` couvre les règles du brief § Politique auto (≥8 rules) | mechanical: `auto_policy_rules_count` ; manual spot-check REFUTED→REJECTED |
| 3 | `config.yaml` expose mode, flags auto_merge, triggers | manual: keys present literally |
| 4 | Six fichiers `architecture/agents/*.md` avec sections obligatoires | mechanical: `agent_role_files_count` == 6 ; manual: each has `# Identité` … `# Budget max appels` |
| 5 | Rôles Cursor référencent invocation documentée | manual: `architecture/agents/README.md` or per-file `# Déclencheur` |
| 6 | `orchestrator.py` CLI `run --event` ; pas de bypass FSM | manual + test: import orchestrator ; subprocess help |
| 7 | FSM ledger refuse transitions invalides | mechanical: `fsm_invalid_transition_tests_count` >= 5 ; pytest PASS |
| 8 | `audit_decision --policy auto` | manual: `--help` shows policy ; test auto-approve fixture |
| 9 | Workflow `pipeline-audit.yml` | mechanical: file exists ; actionlint PASS in CI |
| 10 | Workflow `pipeline-challenge.yml` | idem |
| 11 | Workflow `pipeline-orchestrate.yml` | idem |
| 12 | Workflow `pipeline-forge-run.yml` | idem |
| 13 | Auto-merge allowlist documentée et restreinte | manual: read merge-bot doc ; grep deny paths `.github/workflows` |
| 14 | Budget supervisor SIGTERM at HARD_STOP | mechanical: integration test PASS |
| 15 | forge-run split-check obligatoire | manual: read `.claude/commands/forge-run.md` ; test orchestrator preflight |
| 16 | cost ledger `audit_id` link | mechanical: `cost_ledger_audit_link_count` >= 1 |
| 17 | Integration test full chain sans humain | pytest PASS on `test_full_auto_pipeline*` |
| 18 | Demo script exit 0 on CI | mechanical: `full_auto_demo_steps_count` >= 8 from log |
| 19 | Demo ledger chain complete | manual: read fixture ledger IMPLEMENTED→VERIFIED→ARCHIVED |
| 20 | `docs/rules/full-auto-pipeline.md` + kill-switch | manual: `mode: manual`, `pipeline/pause` documented |
| 21 | CLAUDE.md + HANDOFF pointers only | mechanical: `test_single_source_of_instruction` PASS ; manual: no Success Conditions paraphrase |

## Mechanical gate rows (all briefs)

| — | All counters in manifest.json have nonzero sample_size | gate: `no_empty_sample_pass` |
| — | Deliverables mtimes after brief Authored 2026-08-05T10:05:00Z | gate: `mtime_after_brief` |
| — | Waivers have command + error | gate: `waivers_have_command_and_error` |
| — | No bare `python` in deliverables | gate: `no_bare_python_alias` |
| — | verdict numbers traceable | gate: `verdict_numbers_traceable` |
| — | verdict Author ≠ generator Author | gate: `verdict_is_not_self_authored` |
| — | rubric predates deliverables | gate: `rubric_predates_deliverables` |

## Overall Verdict Rule

**PASS** only if rows 1–21 pass (manual + mechanical) **and**
`py harness/verdict_audit.py harness/queue/briefs/006-full-auto-agent-pipeline`
exits 0.

**Disqualifying failures** (any one = FAIL regardless of completeness):

- Human step still required for accept/reject in `mode: full_auto` without
  documented waiver.
- Cursor role can write outside `architecture/inbox/**` in full_auto config.
- Auto-merge allowed for PRs touching `harness/verdict_audit.py` or
  `.github/workflows/pipeline-*.yml` without owner-listed exception in ADR-0006.
- FSM bypass still reproducible after this brief (append APPROVED without
  CHALLENGED succeeds).

## Évaluateur — scénario de rejeu minimal

Rejouer la démo :

```bash
bash harness/pipeline/demo/run_full_auto_demo.sh
```

Attendu : log contient ≥8 lignes `STEP OK:` ; exit 0 ; dernier step
`ARCHIVED` ou `VERIFIED`.
