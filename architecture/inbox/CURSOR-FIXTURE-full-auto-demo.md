---
audit_id: CURSOR-FIXTURE-full-auto-demo
auditor: cursor-cloud
target_branch: master
target_commit: 000000000000000000000000000000000000000f
created_at: 2026-08-05T23:00:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# Audit fixture -- CURSOR-FIXTURE-full-auto-demo

Fixture d'audit **synthétique**, écrite pour Lot 006c du brief
`006-full-auto-agent-pipeline` (Success Conditions 18-19). Elle fournit un
`audit_id` stable et rejouable pour `harness/pipeline/demo/run_full_auto_demo.sh`,
et une section `# Sources externes` datée pour le compteur
`web_sources_cited_count`. Ce n'est **pas** un audit Cursor réel de ce
dépôt -- `architecture/agents/cursor-auditor.md` documente le format et les
obligations d'un vrai audit ; cette fixture ne prétend inspecter aucun
commit réel et ne doit jamais être traitée comme un finding à part entière.

## Point retenu (verdict CONFIRMED attendu au challenge)

1. Le pipeline full-auto (ADR-0006) manquait d'une démonstration
   mécanique, rejouable de bout en bout, prouvant que la chaîne
   audit -> challenge -> décision auto -> conversion -> forge-run ->
   IMPLEMENTED -> VERIFIED -> ARCHIVED se ferme sans intervention du
   propriétaire. Ce fixture existe pour fournir cette preuve, sans toucher
   au code du dépôt.

# Sources externes

Recherche web ≥ 3 sources datées, comme l'exige
`architecture/agents/cursor-auditor.md` ("autonomous AI dev pipeline",
"agent orchestration CI", "token budget LLM agents") :

1. "Building effective agents" -- Anthropic engineering,
   https://www.anthropic.com/engineering/building-effective-agents -- consulté le 2026-08-05.
2. "Automating your workflow with GitHub Actions" -- GitHub Docs,
   https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/triggering-a-workflow -- consulté le 2026-08-05.
3. "Manage costs for Claude" -- Anthropic pricing/cost guidance,
   https://docs.anthropic.com/en/docs/build-with-claude/pricing -- consulté le 2026-08-05.
