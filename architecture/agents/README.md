# architecture/agents/ — contrats de rôle + invocation

Six rôles figés pour la boucle full-auto (brief `006`, Lot 006b, Success
Conditions 4–5). Chaque fichier `<role-id>.md` porte **exactement** les
sept sections `# Identité`, `# Entrées`, `# Sorties`, `# Interdits`,
`# Déclencheur`, `# Preuve de fin`, `# Budget max appels` — remplies depuis
le brief's "Rôles agents (contrats obligatoires)", jamais paraphrasées
ailleurs (`CLAUDE.md` › Single Source of Instruction).

Aucun rôle ne cumule développement + jugement final : `claude-developer`
produit, `claude-evaluator` juge, jamais la même invocation.

## Table d'invocation (Success Condition 5)

| Rôle | Invocation | Déclenché par |
|---|---|---|
| `cursor-auditor` | Cursor Cloud Agent, template = ce fichier de rôle (`architecture/agents/cursor-auditor.md` passé en system/instructions du Cloud Agent), API `POST https://api.cursor.com/v1/agents` ; pour une PR, la critique suit `architecture/review-guidelines.md` | `.github/workflows/pipeline-audit.yml` sur `push master` **et** sur `pull_request` non-brouillon hors `cursor/*` (ADR-0010) |
| `cursor-qa-scout` | Même Cloud Agent que `cursor-auditor` (compagnon, même session) pour un audit ; `workflow_dispatch` avec `input: theme` pour un cycle de veille autonome | `pipeline-audit.yml` (compagnon) ou déclenchement manuel documenté |
| `claude-challenger` | Slash command `/forge-audit-review <audit_id>` (`.claude/commands/forge-audit-review.md`), exécuté headless via `claude -p` en CI, ou fallback API (`ANTHROPIC_API_KEY`) si `which claude` échoue sur le runner | `.github/workflows/pipeline-challenge.yml` sur merge touchant `architecture/inbox/*.md` |
| `claude-developer` | Slash command `/forge-run <brief>` (`.claude/commands/forge-run.md`), backend `claude` par défaut | `harness/pipeline/orchestrator.py` (événement `audit_approved`/`brief_seed_created`) ou `.github/workflows/pipeline-forge-run.yml` |
| `claude-evaluator` | Sous-agent `forge-evaluateur` (`.claude/agents/forge-evaluateur.md`), lancé en interne par la Phase 1 de `/forge-run` après un gate ACCEPT — pas de slash command dédié, documenté ici pour que l'orchestrateur sache qu'il n'a rien de plus à invoquer lui-même | Fin de boucle `/forge-run`, ou événement orchestrateur `gate_accept` (journalisation seulement — voir `pipeline-orchestrator.md` § Interdits) |
| `pipeline-orchestrator` | Script CLI `py harness/pipeline/orchestrator.py run --event <kind> --payload '<json>'` | Les quatre workflows `pipeline-*.yml` (voir chacun pour son déclencheur GitHub) |

## Pourquoi un Cloud Agent *template* et pas un prompt inline

Le contenu du template Cloud Agent pour `cursor-auditor` /
`cursor-qa-scout` **est** le fichier de rôle lui-même
(`architecture/agents/cursor-auditor.md` /
`architecture/agents/cursor-qa-scout.md`) : la section `# Interdits` de
chaque fichier reproduit exactement les gardes déjà imposées
mécaniquement par `.github/workflows/audit-guard.yml`
(`cursor-scope` job — une PR `cursor/*` ne peut toucher que
`architecture/inbox/**`). Le rôle documente l'intention, le workflow
l'applique — deux couches, jamais une seule qui pourrait dériver.

## Compatibilité

Additif. Depuis ADR-0010 (2026-08-12), les workflows `pipeline-*.yml`
portent de vraies invocations d'agents, déclenchées sur `push`,
`pull_request` et `workflow_dispatch`. Tant que les identifiants ne sont
pas configurés (quota d'abonnement d'abord : `CLAUDE_CODE_OAUTH_TOKEN`,
`CODEX_AUTH_JSON`, `CURSOR_API_KEY` ; clés API en repli — voir
`docs/rules/full-auto-pipeline.md` § activation), chaque étape d'invocation
consigne une dérogation `::warning::` et ne fait rien — jamais d'échec ni
de succès silencieux. Le harness
existant (`/forge-run`, le gate, les briefs) fonctionne exactement comme
avant si cette boucle n'est jamais activée. Câblage et arrêt d'urgence :
`docs/rules/full-auto-pipeline.md`.
