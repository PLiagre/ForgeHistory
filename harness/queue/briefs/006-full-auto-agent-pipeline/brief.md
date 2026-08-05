# Brief 006 : pipeline multi-agents full-auto (sans intervention humaine)

**Authored**: 2026-08-05T10:05:00Z
**Author**: forge-planificateur (brief demandé par le propriétaire — objectif « zero-touch »)

## Provenance

Brief **autonome** (pas issu d'un audit converti). Il remplace **explicitement**
la boucle manuelle décrite dans ADR-0005 (étapes propriétaire
`/forge-audit-accept`, `-reject`, merge review) par une **politique
automatique documentée**. ADR-0006 doit citer cette dérogation et ses garde-fous.

## World-Terms Requirement

Énoncé causal, pas comme préférence d'outillage :

Quand une session de développement s'arrête parce qu'un humain doit merger une
PR, lancer une commande, ou trancher un audit, le **débit de corrections**
tombe à zéro entre deux sessions. Les findings Cursor restent dans
`architecture/inbox/` sans devenir des briefs ; les briefs ouverts restent
sans Évaluateur ; le contexte agent regonfle à chaque reprise manuelle. Sur
le brief 003, un seul Générateur a consommé **1 015 appels outils** parce
qu'aucun superviseur n'a coupé la session — le coût token est l'intégrale sous
cette courbe. Tant que les transitions audit → challenge → brief →
`/forge-run` → merge dépendent d'un humain qui n'est pas devant l'écran,
**le pipeline annoncé n'existe pas** : il existe seulement une checklist que
personne n'exécute.

Ce brief exige une **orchestration machine** : événements GitHub + scripts
déterministes + rôles agent figés, de sorte qu'après un merge de code par
Claude, Cursor audite, Claude challenge et exécute, **sans aucune action du
propriétaire**, jusqu'à PASS gate + Évaluateur ou arrêt budget/plateau
documenté.

## Vision cible (une boucle fermée)

```
merge code (Claude/bot)
  → trigger Cursor Auditor (QA + recherche web best practices)
  → PR documentaire inbox/ (auto-merge si CI verte)
  → trigger Claude Challenger (auto /forge-audit-review)
  → PR review/ + ledger CHALLENGED (auto-merge si CI verte)
  → Orchestrateur AUTO-DECIDE (pas de humain) selon politique § Politique auto
  → auto-convert findings → brief(s) atomiques
  → auto /forge-run (Claude Générateur) sur chaque brief
  → merge si gate ACCEPT + Évaluateur PASS
  → ledger IMPLEMENTED → VERIFIED → ARCHIVED
  → si REJECT/plateau/budget : feedback fichier → relance Générateur (max N)
  → si nouvel audit nécessaire : retour Cursor Auditor
```

Le propriétaire **n'intervient jamais** dans cette boucle une fois la politique
activée (`pipeline/config.yaml`: `mode: full_auto`).

## Rôles agents (contrats obligatoires)

Le Générateur **doit** créer un fichier de rôle par agent Cursor, chemin fixe
`architecture/agents/<role-id>.md`, avec **exactement** les sections ci-dessous.
Aucun rôle ne cumule développement + jugement final.

### 1. `cursor-auditor` — Auditeur indépendant (lecture seule)

| Champ | Valeur |
|---|---|
| Entrée | SHA merge sur `master`, diff du merge, état repo |
| Sortie | `architecture/inbox/CURSOR-<sha>-<slug>.md` + PR `cursor/*` inbox-only |
| Interdit | Tout chemin hors `architecture/inbox/**` ; flags `*_authorized: true` |
| Obligation | Recherche web **≥ 3 sources** datées sur « autonomous AI dev pipeline », « agent orchestration CI », « token budget LLM agents » ; section `# Sources externes` avec URL + date consultée |
| QA | Rejouer commandes citées ; classifier CI ; lister risques P0–P3 ; proposer **≤ 3 briefs atomiques** par audit |

### 2. `cursor-qa-scout` — Veille best practices (lecture seule)

| Champ | Valeur |
|---|---|
| Entrée | Thème du cycle (ex. budget, orchestration, sécurité CI) |
| Sortie | Section append-only dans l'audit ou fichier `architecture/inbox/SOURCES-<date>.md` référencé par l'audit |
| Obligation | Comparer le repo à l'état de l'art (GitHub Actions merge queues, agentic loops, cost caps) ; **ne pas** dupliquer un finding déjà couvert par un brief ouvert |

### 3. `claude-challenger` — Contre-audit (pas de code produit)

| Champ | Valeur |
|---|---|
| Entrée | Audit `PROPOSED` dans inbox |
| Sortie | `architecture/reviews/CLAUDE-<audit_id>.md` + ledger `AUDIT_CHALLENGED` |
| Déclencheur | GitHub Action `pipeline-challenge.yml` sur merge touchant `architecture/inbox/**` |
| Obligation | Verdict CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER **par point numéroté** avec preuve reproductible |

### 4. `claude-developer` — Générateur (code)

| Champ | Valeur |
|---|---|
| Entrée | Brief sous `harness/queue/briefs/` (seed ou complet) |
| Sortie | Livrables brief + PR code |
| Déclencheur | Orchestrateur après auto-convert ou queue brief existante |
| Obligation | Respect budget (`harness/budget.py`) ; backend **claude** par défaut |

### 5. `claude-evaluator` — Évaluateur (verdict seulement)

| Champ | Valeur |
|---|---|
| Entrée | Brief avec gate ACCEPT |
| Sortie | `verdict.md` PASS ou `feedback/feedback-N.md` |
| Règle | Ne jamais être la même invocation que le Générateur du même brief |

### 6. `pipeline-orchestrator` — Machine (pas un LLM juge métier)

| Champ | Valeur |
|---|---|
| Implémentation | Script Python `harness/pipeline/orchestrator.py` + workflows GH |
| Entrée | Événements ledger + statuts CI + fichiers |
| Sortie | Appends ledger, labels PR, lance agents via API/CLI, enqueue `/forge-run` |
| Rôle | Remplace le propriétaire pour accept/reject **selon politique figée** |

## Politique auto (remplace le propriétaire)

Fichier normatif : `harness/pipeline/auto_policy.yaml` (versionné, testé).

Règles **déterministes** — un LLM ne décide pas :

| Événement | Condition | Action auto |
|---|---|---|
| Audit PR merge | CI `audit-guard` + `harness-ci` vert | Auto-merge déjà fait par bot ; ledger optionnel `AUDIT_PROPOSED` |
| Review enregistrée | Tous points REFUTED | `AUDIT_REJECTED` + `AUDIT_ARCHIVED` ; **fin cycle** |
| Review enregistrée | ≥1 CONFIRMED ou PARTIAL | `AUDIT_APPROVED` avec `retained_points` = numéros CONFIRMED ∪ PARTIAL |
| NEEDS_OWNER sans CONFIRMED/PARTIAL | — | `AUDIT_REJECTED` + log « policy: no owner in full_auto » |
| APPROVED | Toujours | `audit_convert` → **un brief par finding retenu** si split-check NEEDS_SPLIT sur estimation ; sinon un brief |
| Brief seed créé | Planificateur auto | Agent `claude-planificateur` remplit TODO **dans la même pipeline** (invocation séparée) |
| Gate ACCEPT | — | Lance Évaluateur auto |
| Évaluateur PASS | CI verte post-merge | `AUDIT_IMPLEMENTED` puis `AUDIT_VERIFIED` sur audit source ; archive |
| 3 REJECT mécaniques consécutifs | même brief | Escalade **machine** : ouvrir issue bot `pipeline-stuck` — **pas** d'attente humaine |
| BUDGET_EXHAUSTED | — | Checkpoint + enqueue brief continuation ; **pas** de retry aveugle |

**Interdit en full_auto** : merge vers `master` si un workflow requis est rouge ;
modifier `auto_policy.yaml` dans la même PR qu'un audit Cursor.

## Success Conditions

### Phase A — Gouvernance et ADR

1. **`docs/adr/0006-full-auto-agent-pipeline.md`** existe, `Status: accepted`, cite
   la dérogation à ADR-0005 (plus de `/forge-audit-accept` humain), liste les
   risques (décision automatique erronée, merge non désiré, coût runaway), et
   les mitigations (policy YAML, budget supervisor, branch bot-only).

2. **`harness/pipeline/auto_policy.yaml`** existe ; chaque règle du tableau
   § Politique auto y est représentée ; `mode: full_auto` documenté.

3. **`harness/pipeline/config.yaml`** expose : `mode`, `max_forge_run_iterations`,
   `auto_merge_audit_prs: true`, `auto_merge_review_prs: true`,
   `claude_challenge_on_inbox_merge: true`, `cursor_audit_on_master_push: true`.

### Phase B — Rôles Cursor documentés

4. Les **six** fichiers `architecture/agents/<role-id>.md` existent avec sections
   obligatoires : `# Identité`, `# Entrées`, `# Sorties`, `# Interdits`,
   `# Déclencheur`, `# Preuve de fin`, `# Budget max appels`.

5. Chaque rôle Cursor référence **une** commande slash ou script d'invocation
   (ex. Cloud Agent template, ou doc `architecture/agents/README.md`).

### Phase C — Orchestrateur et FSM ledger

6. **`harness/pipeline/orchestrator.py`** : CLI `run --event <kind>` consommant
   payloads JSON (merge SHA, audit_id, brief_dir) ; appelle les modules existants
   (`audit_review`, `audit_decision`, `audit_convert`, `audits`) **sans**
   bypass FSM — **corrige** le contournement ledger identifié dans l'audit
   post-merge `CURSOR-POSTMERGE-42cb054`.

7. **`harness/audit_ledger.py`** : FSM centralisée — append refuse transitions
   invalides (test adversarial reproduisant bypass APPROVED sans CHALLENGED).

8. **`harness/audit_decision.py`** : mode `--policy auto` lisant
   `auto_policy.yaml` ; `--reason` généré depuis template machine, pas vide.

### Phase D — GitHub Actions (déclencheurs)

9. **`.github/workflows/pipeline-audit.yml`** : sur push `master`, lance
   Cursor Cloud Agent (ou documente webhook) avec rôle `cursor-auditor` ;
   permissions minimales.

10. **`.github/workflows/pipeline-challenge.yml`** : sur merge/push touchant
    `architecture/inbox/*.md`, invoque Claude (API ou `claude`-CLI headless
    documenté) → scaffold + fill review + record CHALLENGED → PR
    `architecture/reviews/**` + ledger.

11. **`.github/workflows/pipeline-orchestrate.yml`** : sur merge review ou
    ledger file change (artifact) ou workflow_dispatch, exécute
    `orchestrator.py` : auto-approve → convert → enqueue forge-run.

12. **`.github/workflows/pipeline-forge-run.yml`** : workflow_dispatch ou label
    `forge-run/queued` sur branche bot ; exécute `/forge-run` équivalent
    headless pour **un** brief ; commente résultat + met à jour ledger.

13. **Auto-merge** : fichier `.github/merge-bot.yaml` ou doc + Action utilisant
    `gh pr merge --auto` **uniquement** pour PRs bot dont paths ⊆ allowlist
    (`architecture/inbox/**`, `architecture/reviews/**`,
    `harness/queue/briefs/**/feedback/**`, branches `cursor/*`, `forge-bot/*`).

### Phase E — Budget et tokens (full auto)

14. **`harness/bipeline/supervisor.py`** (ou extension `budget.py`) : process
    parent qui **SIGTERM** le Générateur à `HARD_STOP_CALLS` ; test d'intégration
    avec transcript fixture.

15. **`/forge-run` orchestrateur** (`.claude/commands/forge-run.md` + script) :
    appelle `split-check --estimated-calls` **obligatoire** ; exit non-zero
    bloque lancement si NEEDS_SPLIT.

16. **`harness/backends/ledger.py`** : champ optionnel `audit_id` sur entrées
    cost ledger quand brief issu de conversion ; test lien audit → brief → coût.

### Phase F — Preuve de bout en bout (fixture)

17. **`harness/pipeline/fixtures/mini_repo/`** ou test intégration avec tmp_path
    simulant : inbox audit → review → auto approve → convert → forge-run mock
    → VERIFIED → ARCHIVED **sans input humain** (subprocess chain).

18. **`harness/pipeline/demo/run_full_auto_demo.sh`** : démo reproductible <
    5 min sur Linux CI ; exit 0 ; log `deliverables/full-auto-demo.log`.

19. **`architecture/audit-ledger.jsonl`** : après démo, contient chaîne complète
    pour fixture audit_id `CURSOR-FIXTURE-full-auto-demo`.

### Phase G — Documentation opérationnelle

20. **`docs/rules/full-auto-pipeline.md`** : diagramme, rôles, comment activer
    `mode: full_auto`, comment **désactiver d'urgence** (`mode: manual`,
    kill-switch label `pipeline/pause`).

21. **`CLAUDE.md`** et **`HANDOFF.md`** : pointeurs vers ADR-0006 et
    `docs/rules/full-auto-pipeline.md` ; **sans** paraphraser les Success
    Conditions de ce brief.

## Non-Goals

- Ne **pas** supprimer la boucle manuelle : `mode: manual` reste disponible.
- Ne **pas** autoriser Cursor à modifier du code en full_auto (auditeur
  read-only inchangé).
- Ne **pas** fusionner automatiquement vers `master` une PR qui touche
  `.github/workflows/**`, `harness/verdict_audit.py`, ou `VISION.md` sans
  liste blanche explicite dans ADR-0006.
- Ne **pas** implémenter Unity batch en CI obligatoire (coût/flaky) — reste
  optionnel/informatif.
- Ne **not** porter la totalité du geo/sim F1 dans ce brief — seulement
  l'infrastructure pipeline ; les findings produits deviendront d'autres briefs.
- Si `split-check` estime **> 150 appels** pour une phase, le Générateur **doit**
  scinder en sous-briefs 006a/006b/… plutôt que tout livrer en une session.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| agent_role_files_count | `architecture/agents/*.md` excluding README | must equal 6 |
| pipeline_workflows_count | `.github/workflows/pipeline-*.yml` | must equal 4 |
| auto_policy_rules_count | `harness/pipeline/auto_policy.yaml` top-level `rules:` entries | must be >= 8 (one per row in § Politique auto) |
| fsm_invalid_transition_tests_count | `harness/tests/test_audit_fsm.py` or equivalent | must be >= 5 adversarial cases |
| full_auto_demo_steps_count | `deliverables/full-auto-demo.log` lines matching `STEP OK:` | must be >= 8 (one per major pipeline stage) |
| web_sources_cited_count | audit fixture or `architecture/inbox/CURSOR-FIXTURE-full-auto-demo.md` body | must be >= 3 URLs with consultation date |
| audit_to_brief_trace_count | ledger CONVERTED events in demo | must be >= 1 with non-empty `briefs[]` |
| cost_ledger_audit_link_count | `harness/queue/cost-ledger.jsonl` demo entries | must be >= 1 entry with `audit_id` field |

## Acceptable Waivers

| claim | required command | required error |
|---|---|---|
| « Cursor Cloud Agent API indisponible en CI » | `curl -sfI https://api.cursor.com` or documented env check | connection refused or 401 with log proving API not configured — alors workflow utilise `workflow_dispatch` + doc activation manuelle **temporaire**, et demo passe en `--offline-fixture` |
| « Claude headless non installable sur runner GH » | `which claude` on ubuntu-latest | exit non-zero — autoriser fallback Anthropic API **si** secret `ANTHROPIC_API_KEY` present et test mock PASS |
| « Branch protection empêche auto-merge bot » | `gh api repos/{owner}/{repo}/branches/master/protection` | 403 or rules blocking — waiver **partiel** : pipeline s'arrête avant auto-merge avec ledger `AUDIT_IMPLEMENTED` only ; documenté dans ADR-0006 Risks |

## Budget d'exécution

- **Estimation** : 400–600 appels outils si tenté monolithiquement.
- **Obligation Planificateur/Générateur** : exécuter
  `py harness/budget.py split-check --brief harness/queue/briefs/006-full-auto-agent-pipeline --estimated-calls 500`
  en **première** action ; le résultat observé est **NEEDS_SPLIT** — **ne pas**
  implémenter ce brief monolithiquement. Utiliser les lots § Lots atomiques.

## Lots atomiques (ordre d'exécution — chaque lot = un `/forge-run` séparé)

### Lot 006a — Gouvernance + FSM (≤120 appels estimés)

| Champ | Valeur |
|---|---|
| Objectif | ADR-0006, `auto_policy.yaml`, `config.yaml`, FSM ledger, tests adversariaux |
| Dépendances | Aucune |
| Fichiers | `docs/adr/0006-*.md`, `harness/pipeline/auto_policy.yaml`, `harness/pipeline/config.yaml`, `harness/audit_ledger.py`, `harness/audit_decision.py`, `harness/tests/test_audit_fsm.py` |
| Critères | Success Conditions 1–3, 6–8 ; counters `auto_policy_rules_count`, `fsm_invalid_transition_tests_count` |
| Validation | `py -m pytest harness/tests/test_audit*.py -q` |
| Done | Gate ACCEPT 006a ; bypass APPROVED sans CHALLENGED **impossible** |

### Lot 006b — Rôles agents + orchestrateur + workflows (≤140 appels)

| Champ | Valeur |
|---|---|
| Objectif | Six rôles Cursor, `orchestrator.py`, 4 workflows pipeline, auto-merge bot |
| Dépendances | 006a mergé (`auto_policy.yaml` existe) |
| Fichiers | `architecture/agents/**`, `harness/pipeline/orchestrator.py`, `.github/workflows/pipeline-*.yml`, `docs/rules/full-auto-pipeline.md` |
| Critères | Success Conditions 4–5, 9–13, 20 |
| Validation | `./actionlint` ; `py harness/pipeline/orchestrator.py --help` |
| Done | Gate ACCEPT 006b ; déclencheurs GH présents (même si secrets API manquants → waiver documenté) |

### Lot 006c — Budget supervisor + démo E2E + traçabilité coût (≤130 appels)

| Champ | Valeur |
|---|---|
| Objectif | Supervisor budget, split-check forge-run obligatoire, demo full-auto, lien cost ledger |
| Dépendances | 006a + 006b |
| Fichiers | `harness/budget.py` ou `supervisor.py`, `harness/backends/ledger.py`, `harness/pipeline/demo/**`, tests e2e, `.claude/commands/forge-run.md` |
| Critères | Success Conditions 14–19, 21 ; counters demo + `audit_id` cost |
| Validation | `bash harness/pipeline/demo/run_full_auto_demo.sh` exit 0 |
| Done | Gate ACCEPT 006c ; chaîne ledger fixture complète ; **pipeline full_auto activable** via `config.yaml` |

**Après 006c** : activer `mode: full_auto` et laisser la boucle tourner sans intervention humaine.
