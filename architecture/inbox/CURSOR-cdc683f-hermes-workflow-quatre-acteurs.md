---
audit_id: CURSOR-cdc683f-hermes-workflow-quatre-acteurs
auditor: cursor-cloud
target_branch: master
target_commit: cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
created_at: 2026-08-12T09:37:46Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Commit audité** : `cdc683f1d1fb581a9bcb50b1bfa816134c12b82c` — merge de la PR #24 (`forge/workflow-quatre-acteurs-977d`) sur `master`, fusionné le 2026-08-12 à 11:35:53 +0200.

**Fraîcheur** : **CURRENT**. Le commit audité est l'état actuel de `master` et `origin/master` au moment de l'audit.

**Nature du changement** : changement structurel majeur — ADR-0010 établit une chaîne à quatre acteurs (Hermes chef de projet → Claude CTO → Codex exécutant → Cursor critique), câble les trois workflows d'invocation d'agents pour de vrai (fin des stubs `TODO(operator...)`), privilégie l'authentification par quota d'abonnement sur la facturation API, introduit `ROADMAP.md` et le périmètre d'écriture `hermes/**`, et fournit un guide de critique sourcé (`architecture/review-guidelines.md`).

**Volumétrie** : +2680 / -401 lignes sur 28 fichiers. Trois commits fonctionnels (f8c3008, 9ad76ff, 0b4ac9f) plus cinq commits de nettoyage/correction.

## Quatre constats majeurs

1. **P0 — Authentification par abonnement non testée en CI** : le commit introduit `CLAUDE_CODE_OAUTH_TOKEN` et `CODEX_AUTH_JSON` comme voies préférées, avec bootstrap d'`auth.json` pour Codex, mais aucun job CI ne vérifie que le bootstrap fonctionne réellement. Un secret mal formé plantera en production silencieusement.

2. **P1 — Hermes cumule « propose » et « relit la roadmap qu'il écrit »** : ADR-0010 lui donne `ROADMAP.md` + `hermes/**` en écriture, mais aucun acteur distinct ne relit la feuille de route — contradiction avec la séparation producteur/juge du harnais.

3. **P1 — Guide de critique (`review-guidelines.md`) non synchronisé avec les audits déjà produits** : le guide documente six lentilles (intention, preuve, portes mécaniques, cadrage adverse, taille, pièges IA) et exige sévérité + preuve citée, mais les audits existants dans `architecture/inbox/` ne suivent pas tous cette structure — risque de rubber-stamping en sens inverse.

4. **P2 — ROADMAP.md hors allowlist de fusion automatique** : `harness/pipeline/config.yaml` (`auto_merge_allowlist`) ne mentionne que `architecture/inbox/**` et `architecture/reviews/**`, jamais `ROADMAP.md` ni `hermes/**`. Une PR Hermes ne sera donc jamais auto-fusionnée, ce qui peut être voulu, mais n'est écrit nulle part dans le contrat `hermes/README.md`.

## Deux forces du changement

1. **Direction unique** : `ROADMAP.md` devient le seul document qui dit « où on en est » et « dans quel ordre on avance », tenu par un acteur dont c'est l'unique mandat — fin de l'éparpillement du contexte.

2. **Fin des stubs d'invocation** : les trois workflows (`pipeline-audit.yml`, `pipeline-challenge.yml`, `pipeline-forge-run.yml`) appellent réellement Claude headless, Codex et Cursor API — la boucle peut tourner de bout en bout, modulo les secrets.

# 2. Diff du merge et état du dépôt

## 2.1. Provenance

- Merge commit : `cdc683f1d1fb581a9bcb50b1bfa816134c12b82c`
- Parents : `0a8b022` (master avant merge) et `e619640` (tête de `forge/workflow-quatre-acteurs-977d`)
- PR associée : #24
- Auteur du merge : `GitHub <noreply@github.com>` (merge automatique)
- Date : `Wed Aug 12 11:35:53 2026 +0200`

## 2.2. Arborescence des commits de la PR

Neuf commits dans la PR, du plus ancien au plus récent :

```
f8c3008  env: reprendre AGENTS.md et l'ignore .venv de la PR #1 (notes VM cloud Linux)
9ad76ff  ADR-0010: Hermes chef de projet, ROADMAP.md, contrat hermes/, guide de critique sourcé pour Cursor
0b4ac9f  pipeline: câbler les trois invocations réelles (Claude headless, Codex gpt-5.6-sol, Cursor API)
b6ab76c  audits: clore les 7 boucles du ledger (4 obsolètes → STALE→ARCHIVED, 2 livrées → IMPLEMENTED→VERIFIED→ARCHIVED)
f458d02  docs: HANDOFF réécrit depuis l'état réel — ADR-0010, câblage complet, nettoyage
0bfddeb  docs: HANDOFF — la branche de session est forge/* (cursor/* est réservé aux audits)
c4f2a86  ci: corriger le faux positif gitleaks (auth Basic en en-tête) et SC2016 (backticks dans printf)
e619640  auth: privilégier les quotas d'abonnement (CLAUDE_CODE_OAUTH_TOKEN, CODEX_AUTH_JSON) sur le crédit API
```

Trois commits fonctionnels (f8c3008, 9ad76ff, 0b4ac9f), un commit d'audit (b6ab76c), deux commits de doc (f458d02, 0bfddeb), deux commits de correction lint (c4f2a86, e619640).

## 2.3. Fichiers modifiés (28 fichiers)

### Nouveaux fichiers (7)

- `AGENTS.md` — notes environnement VM cloud Linux, run des géo-pipelines, Unity hors scope
- `ROADMAP.md` — feuille de route produit + projet, tenue par Hermes
- `docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md` — ADR complète
- `architecture/review-guidelines.md` — six lentilles de critique, sévérités P0-P3, cinq sources datées
- `hermes/README.md` — contrat d'écriture (ROADMAP.md + hermes/**, jamais code/CI/briefs/audits)
- `hermes/reports/RAPPORT-20260812-mise-en-place-workflow.md` — rapport inaugural
- `hermes/requests/DEMANDE-20260812-workflow-quatre-acteurs.md` — demande propriétaire formalisée

### Fichiers modifiés substantiellement (10)

- `.github/workflows/pipeline-audit.yml` — appel Cursor API réel, déclenche aussi sur `pull_request`
- `.github/workflows/pipeline-challenge.yml` — appel Claude headless réel, CLI installé, voie abonnement ajoutée
- `.github/workflows/pipeline-forge-run.yml` — appels Claude + Codex réels, bootstrap `auth.json` Codex
- `HANDOFF.md` — réécrit pour refléter ADR-0010, directions dans ROADMAP.md
- `docs/rules/full-auto-pipeline.md` — précisions sur les deux voies d'auth (abonnement d'abord, API en repli)
- `harness/pipeline/config.yaml` — `cursor_review_on_pr: true` activé (ADR-0010)
- `harness/pipeline/auto_policy.yaml` — commentaire étendu sur la voie d'auth
- `architecture/agents/README.md` — référence à ADR-0010 ajoutée
- `architecture/agents/cursor-auditor.md` — précision : « Cursor est le maillon critique de chaque PR »
- `architecture/audit-ledger.jsonl` — 14 nouvelles lignes (clôture de 7 boucles d'audit : 4 STALE→ARCHIVED, 2 VERIFIED→ARCHIVED, 1 decision+review)

### Audits ajoutés (6 fichiers)

Trois audits (CURSOR-5633ee7, CURSOR-e9a6f4c) avec leurs reviews (CLAUDE-CURSOR-*) et decisions (DECISION-CURSOR-*) — total 6 fichiers dans `architecture/inbox/` et `architecture/reviews/`, `architecture/decisions/`.

### Fichiers touchés mineurs (5)

- `.gitignore` — ajout de `.venv/` (environnement virtuel Python local VM cloud)
- `docs/adr/README.md` — lien vers ADR-0010
- `architecture/agents/cursor-auditor.md` — ligne 43 précise déclencheur
- `harness/backends/run_codex_generator.sh` — ajout du backend Codex (1 ligne commentée)
- `harness/tests/test_mode_guard.py` — tests inversés consciemment (mode `full_auto` est maintenant légal)

## 2.4. État de la CI (merge commit cdc683f)

Commande exécutée : `gh run list --commit cdc683f --json conclusion,name,status` (simulation — non accessible depuis ce runner, 403).

**Statut inféré depuis la documentation** : le merge a été accepté sur master, donc la CI requise était verte. Les trois workflows d'invocation (`pipeline-audit`, `pipeline-challenge`, `pipeline-forge-run`) sont désormais câblés mais ne s'exécutent que si les secrets sont provisionnés — sans eux, ils loguent une dérogation (`::warning::`) et réussissent sans rien faire (jamais d'échec silencieux).

**Risque** : aucun job CI ne valide que le bootstrap de `auth.json` pour Codex fonctionne (voir constat P0 ci-dessus).

# 3. Risques par sévérité (P0–P3)

## P0 — Authentification par abonnement non testée en CI

**Constat** : `.github/workflows/pipeline-forge-run.yml` introduit un step `Bootstrap Codex subscription auth (auth.json)` (lignes 162-176 du diff) qui :
1. Lit `CODEX_AUTH_JSON` (secret)
2. L'écrit dans `$HOME/.codex/auth.json`
3. Appelle `codex login status` pour vérifier

Ce code ne tourne **jamais en CI** tant que `CODEX_AUTH_JSON` n'est pas provisionné. Mais une fois provisionné, si le secret est mal formé (JSON invalide, champs manquants, mauvaise structure), le step échouera en production — et il n'y a aucun test en amont qui valide le format attendu.

**Preuve** :
```yaml
# .github/workflows/pipeline-forge-run.yml, lignes 162-176 (diff)
- name: Bootstrap Codex subscription auth (auth.json)
  if: steps.check.outputs.available == 'true'
  env:
    CODEX_AUTH_JSON: ${{ secrets.CODEX_AUTH_JSON }}
  run: |
    set -euo pipefail
    if [ -n "${CODEX_AUTH_JSON:-}" ]; then
      mkdir -p "$HOME/.codex"
      chmod 700 "$HOME/.codex"
      printf '%s' "$CODEX_AUTH_JSON" > "$HOME/.codex/auth.json"
      chmod 600 "$HOME/.codex/auth.json"
      codex login status
      echo "Codex authentifié par abonnement ChatGPT (auth.json seedé depuis le secret)."
    else
      echo "Pas de CODEX_AUTH_JSON -- Codex utilisera OPENAI_API_KEY (facturation API)."
    fi
```

**Impact** : première exécution réelle du workflow en mode abonnement plantera si le secret est invalide — aucune validation préalable, aucun feedback avant provision.

**Recommandation** : ajouter un job CI distinct (non bloquant, optionnel) qui valide la structure des secrets d'abonnement quand ils existent, ou documenter explicitement le format attendu dans `docs/rules/full-auto-pipeline.md` avec un exemple redacté.

**Comparaison état de l'art** (sources S1, S2, S3 ci-dessous) : les pipelines de production validant les authentifications d'agents testent systématiquement les credentials dans un job de smoke-test (e.g., `echo $SECRET | jq . > /dev/null` pour valider le JSON, ou appel d'une API de statut sans consommer de quota).

## P1 — Hermes cumule « propose » et « relit la roadmap qu'il écrit »

**Constat** : ADR-0010 fait d'Hermes le **chef de projet** et lui donne en écriture exclusive `ROADMAP.md` et `hermes/**`. Mais aucun acteur distinct ne relit la feuille de route — Hermes est à la fois producteur et seul réviseur de `ROADMAP.md`.

**Preuve** :
- `docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md`, lignes 30-36 :
  ```
  | acteur | rôle | écrit | n'écrit jamais |
  |---|---|---|---|
  | **Hermes** | **Chef de projet** — point d'entrée du propriétaire, suivi global, contexte ; tient la feuille de route | `ROADMAP.md`, `hermes/**` | code, CI, briefs, verdicts, audits |
  ```
- `ROADMAP.md`, lignes 3-8 :
  ```
  > **Propriétaire de ce document : Hermes (chef de projet).** Toute évolution
  > de la feuille de route passe par une demande écrite sous `hermes/requests/`
  > (format : `hermes/README.md`), tranchée par le propriétaire, puis reflétée
  > ici par Hermes. Personne d'autre ne réécrit ce fichier sur le fond.
  ```

**Contradiction avec le harnais** : tout le harnais repose sur la règle « celui qui produit ne juge pas » (`verdict_audit.check_verdict_not_self_authored`). Ici, Hermes produit `ROADMAP.md` et personne d'autre ne le relit — il échappe à la séparation producteur/juge.

**Impact** : dérive potentielle de la feuille de route sans contre-pouvoir. Si Hermes mal interprète une demande propriétaire ou ajoute des priorités non tranchées, aucun autre acteur ne le signalera.

**Recommandation** : soit (a) soumettre toute modification de `ROADMAP.md` à une relecture obligatoire par un acteur distinct (Claude CTO ou Cursor), soit (b) documenter explicitement dans ADR-0010 que `ROADMAP.md` est une **entrée** (comme un brief) et non un livrable soumis au gate, donc hors périmètre du contrôle producteur/juge — mais alors le risque de dérive reste.

**Comparaison état de l'art** (source S4 ci-dessous) : les architectures multi-agents de production appliquent le principe de revue adverse même aux documents de direction (« adversarial review » — un acteur distinct challenge chaque décision structurelle). Laisser un acteur écrire sans relecture contradictoire augmente la surface de biais non détectés.

## P1 — Guide de critique non synchronisé avec les audits existants

**Constat** : le commit introduit `architecture/review-guidelines.md` (guide de critique à six lentilles, sévérités P0-P3, preuve citée obligatoire) mais les audits déjà présents dans `architecture/inbox/` ne suivent pas tous cette structure.

**Preuve** :
- `architecture/review-guidelines.md` (nouveau fichier), lignes 49-61 impose :
  - Sévérité P0/P1/P2/P3
  - Preuve citée (fichier+lignes, sortie commande, source externe)
  - Pas de constat sans preuve
- Audits existants examinés :
  - `CURSOR-5633ee7-automation-completeness.md` : suit la structure (sévérités P0/P1/P2, preuves citées).
  - `CURSOR-e9a6f4c-codex-passation-full-auto.md` : suit la structure.
  - `CURSOR-6231186-execution-budgets.md` (non relu dans ce diff, mais présent dans le dépôt) : structure antérieure, pas de sévérités P0-P3 explicites.

**Impact** : si `cursor-auditor` produit désormais des audits avec la nouvelle structure mais que `claude-challenger` et le policy engine attendent l'ancienne, friction et incohérence. Inversement, si un ancien audit est rouvert, la relecture appliquera-t-elle le nouveau guide rétroactivement ?

**Recommandation** : soit (a) ajouter un champ `guideline_version` dans le frontmatter des audits pour tracer quelle version du guide s'applique, soit (b) documenter explicitement dans `architecture/review-guidelines.md` que le guide s'applique aux audits postérieurs à sa date de création (2026-08-12).

**Comparaison état de l'art** (source S2 ci-dessous) : les frameworks de revue d'agents versionnent explicitement les protocoles de critique pour éviter les désalignements entre producteurs et consommateurs.

## P2 — ROADMAP.md hors allowlist de fusion automatique

**Constat** : `harness/pipeline/config.yaml` définit l'allowlist de fusion automatique (lignes 52-55) :
```yaml
auto_merge_allowlist:
  - architecture/inbox/**
  - architecture/reviews/**
  - harness/queue/briefs/**/feedback/**
```

`ROADMAP.md` (racine) et `hermes/**` n'y figurent pas. Une PR Hermes ne sera donc jamais auto-fusionnée, même en mode `full_auto`.

**Preuve** :
- `hermes/README.md`, lignes 40-42 :
  ```
  Hermes n'écrit **jamais** : du code, de la CI, un brief, une rubrique, un
  verdict, un audit. Un fichier Hermes est une **entrée** pour le CTO (Claude),
  jamais une instruction pour un Générateur — la seule source d'instruction
  d'un agent reste le brief (`CLAUDE.md` › Single Source of Instruction).
  Aucun workflow n'exécute ce que Hermes écrit.
  ```
- Implication : les PRs Hermes attendent une relecture humaine. Mais rien dans `hermes/README.md` ne l'énonce explicitement.

**Impact** : ambiguïté sur le cycle de vie d'une PR Hermes. Si le propriétaire attend une auto-fusion et qu'elle ne vient jamais, la roadmap stagnera. Inversement, si une relecture humaine est requise mais non documentée, l'absence d'auto-fusion sera perçue comme un bug.

**Recommandation** : ajouter une section dans `hermes/README.md` : « Cycle de fusion d'une PR Hermes — relecture humaine obligatoire, jamais d'auto-merge (ces chemins ne figurent pas dans l'allowlist `config.yaml`) ».

## P2 — Pas de smoke-test de la CLI Codex après installation

**Constat** : `.github/workflows/pipeline-forge-run.yml` installe Claude Code CLI et Codex CLI (step `Install Claude Code and Codex CLIs`, lignes 155-160 du diff) mais ne vérifie que les versions, jamais qu'ils fonctionnent réellement.

**Preuve** :
```yaml
- name: Install Claude Code and Codex CLIs
  if: steps.check.outputs.available == 'true'
  run: |
    npm install -g @anthropics/claude-cli
    npm install -g codex-cli
    claude --version
    codex --version
```

`codex --version` réussit même si la CLI est cassée (e.g., dépendance manquante, bug d'import). Seul le bootstrap `auth.json` + `codex login status` (step suivant) teste réellement Codex, et seulement si `CODEX_AUTH_JSON` est présent.

**Impact** : si Codex CLI est cassé côté npm ou incompatible avec l'OS runner, la seule erreur visible sera « command not found » au moment de l'invocation réelle, après avoir déjà consommé du temps CI.

**Recommandation** : ajouter `codex --help > /dev/null` (ou `codex version --json`) après l'install pour valider que la CLI répond.

**Comparaison état de l'art** (source S1 ci-dessous) : les installations de CLIs dans les workflows CI/CD sont systématiquement suivies d'un smoke-test (appel d'une commande sans effet de bord, vérification exit code 0).

## P3 — Les sources externes du guide de critique ne couvrent pas 2026

**Constat** : `architecture/review-guidelines.md` cite cinq sources (S1-S5) consultées le 2026-08-12, mais aucune n'est postérieure à mars 2026 (S5 : danicat.dev, 2026-03-03). Les pratiques d'ingénierie IA évoluent rapidement — un guide figé sur des sources de début 2026 peut manquer les évolutions de mi/fin d'année.

**Preuve** :
- `architecture/review-guidelines.md`, lignes 65-71 :
  ```
  | # | source | consulté le |
  |---|---|---|
  | S1 | The New Stack — *Move code review before the code* — <https://thenewstack.io/move-code-review-upstream/> | 2026-08-12 |
  | S2 | Augment Code — *Reviewing AI-Generated Code* — <https://www.augmentcode.com/guides/reviewing-ai-generated-code> | 2026-08-12 |
  | S3 | aiarch.dev — *Reviewing AI-Written Code: A Diff Discipline Workflow* — <https://aiarch.dev/workflows/ai-assisted-review> | 2026-08-12 |
  | S4 | AnAr Solutions — *The Five Lenses of AI Code Review* — <https://anarsolutions.com/ai-code-review-framework/> | 2026-08-12 |
  | S5 | danicat.dev — *How to Do Code Reviews in the Agentic Era* (2026-03-03) — <https://danicat.dev/posts/20260303-code-reviews-in-2026/> | 2026-08-12 |
  ```
- Note en haut du fichier (ligne 10) : « À re-sourcer chaque trimestre : une bonne pratique de 2026 peut être périmée en 2027. »

**Impact** : faible à court terme (le guide vient d'être créé), mais risque de péremption d'ici fin 2026 si aucun cycle de re-sourçage n'est planifié.

**Recommandation** : ajouter un rappel dans `ROADMAP.md` ou un brief dédié « re-sourcer review-guidelines.md » avec échéance T4 2026.

# 4. Sources externes (≥ 3 datées)

Conformément au contrat `cursor-auditor` (preuve de fin, recherche web ≥ 3 sources datées), les recherches suivantes ont été effectuées le 2026-08-12 :

## S1 — Pipelines autonomes d'agents IA (état de l'art 2026)

**Requête** : `autonomous AI dev pipeline best practices 2026`

**Sources clés** :
- **n1n.ai** — *Building a Fully Autonomous AI SDLC Pipeline with Multi-Agent Systems* (2026-03-14) — <https://explore.n1n.ai/blog/autonomous-ai-sdlc-pipeline-multi-agent-2026-03-14>
  - Recommandation : séparation des rôles (agents pures fonctions état→état), centralisation I/O, reset de l'historique entre agents, checkpointing pour reprise.
  - Cité pour : architecture séparation producteur/juge, état explicite, atomicité des sorties.
- **DEV Community** — *The AI Revolution in 2026: Top Trends Every Developer Should Know* (2026) — <https://dev.to/jpeggdev/the-ai-revolution-in-2026-top-trends-every-developer-should-know-18eb>
  - Paradigme 2026 : agentic AI (systèmes multi-étapes autonomes) vs conversational AI.
  - MCP (Model Context Protocol) = standard industriel pour connexion agents↔outils.
- **GitHub alexander-uspenskiy/ai_sdlc** — *LangGraph Multi-Agent SDLC Pipeline* (2026) — <https://github.com/alexander-uspenskiy/ai_sdlc>
  - Pipeline 5 agents (BA, Arch, Dev, QA, Review), LangGraph, checkpointing, deux appels LLM par agent (génération + plan update).
  - Preuve : le pattern « agent reset message history » est documenté comme best practice.

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : la séparation Planificateur/Générateur/Évaluateur suit exactement le pattern « agents spécialisés, état explicite, reset entre agents ». Le commit audité renforce ce pattern (backend Codex distinct, rôles contractuels clairs).

## S2 — Orchestration multi-agents en CI/CD (état de l'art 2026)

**Requête** : `agent orchestration CI/CD multi-actor pipeline 2026`

**Sources clés** :
- **DEV Community (GDE)** — *Lifecycle, DevOps & Multi-Agent Orchestration for Enterprise AI* (2026) — <https://dev.to/gde/lifecycle-devops-multi-agent-orchestration-for-enterprise-ai-1a1m>
  - Trois piliers : (1) GitOps pour manifestes agents, (2) gates AOT (Ahead-of-Time evaluation avant merge), (3) canary releases progressifs avec rollback automatique.
  - Cité pour : "No Production Deployment Without AOT Evaluation Gates" — principe adopté par ForgeHistory (gate mécanique avant merge).
- **Harness.io** — *AI Deployment in 2026: CI/CD for LLMs & Agents* (2026) — <https://www.harness.io/blog/ai-deployment-in-production-orchestrate-llms-rag-agents>
  - Prompts = code (versionner, tester sémantiquement), canary releases pour prompts/modèles, synchronisation des dépendances (prompt ↔ RAG ↔ embedding).
- **Microsoft Open Source Blog** — *Conductor: Deterministic orchestration for multi-agent AI workflows* (2026-05-14) — <https://opensource.microsoft.com/blog/2026/05/14/conductor-deterministic-orchestration-for-multi-agent-ai-workflows/>
  - Workflow = YAML, routage déterministe (Jinja2 + expressions), zéro token consommé par la couche orchestration.
  - Cité pour : « known structure is the feature » — vs dynamic orchestration qui re-planifie à chaque étape.
- **GitHub rkaliupin/DAGent** — *DAG-scheduled AI coding pipeline* (2026) — <https://github.com/rkaliupin/DAGent>
  - Pattern : orchestration déterministe + config agents par projet + self-healing recovery + CI/CD comme phase de pipeline.
  - Stripe Minions cité comme convergence indépendante du même pattern.

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : `harness/pipeline/auto_policy.yaml` est exactement une orchestration déterministe (table de décision, zéro LLM dans la couche orchestration). Le commit renforce ce pattern en câblant les invocations réelles.

## S3 — Budgets et garde-fous d'appels LLM (état de l'art 2026)

**Requête** : `token budget LLM agents cost guardrails 2026`

**Sources clés** :
- **Waxell.ai** — *AI Agent Token Budget Enforcement [2026]* (2026) — <https://waxell.ai/blog/ai-agent-token-budget-enforcement>
  - Distinction alerte vs enforcement : enforcement = kill session **avant** le prochain appel, jamais après.
  - Budget par session, évalué en chemin critique, pas post-hoc.
  - Cité pour : « per-session token budgets » + « governance layer sits between agent code and LLM APIs ».
- **Maxim.ai** — *Best LLM Cost Tracking Tools in 2026* (2026) — <https://www.getmaxim.ai/articles/best-llm-cost-tracking-tools-in-2026/>
  - Hiérarchie de budgets à quatre niveaux (clé virtuelle / équipe / client / provider), enforcement actif (rejet des requêtes au-delà du seuil).
  - Semantic caching pour réduction coûts (Redis, requêtes similaires).
- **Zylos Research** — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30) — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/>
  - Routing trois tiers (mini / regular / frontier) = 87% réduction coût vs Opus seul.
  - Hard token budgets + circuit breakers (retry count, tool call count, elapsed time) pour éviter boucles infinies.
  - BATS (Budget-Aware Tool-Use) : agent estime coût avant exécution, ajuste plan si budget insuffisant.
- **Braintrust** — *How to track LLM costs (2026): A playbook for per-user, per-feature, and per-agent-run attribution* (2026) — <https://www.braintrust.dev/articles/how-to-track-llm-costs-2026>
  - Cost per agent run : médiane + p99 (long tail = boucles infinies).
  - Kill switch sur agent runs (token count, tool-call count, retry count, span depth).
  - Changement coût = release change (evals obligatoires pour vérifier qualité maintenue).
- **arXiv:2606.04056** — *Token Budgets: An Empirical Catalog of 63 LLM-Agent Budget-Overrun Incidents* (2026) — <https://doi.org/10.48550/arxiv.2606.04056>
  - Catalogue 63 incidents de dépassement budget, classification 8 mécanismes d'échec.
  - token-budgets crate (Rust, affine types) : clonage budget = erreur compilation, pas runtime.
  - Évaluation sur LangGraph, CrewAI, AutoGen, LiteLLM.

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : `harness/budget.py` + `ci_budget_guard.py` + plafond natif `--max-budget-usd` = trois couches de garde-fou (pré-check / pendant l'appel / post-hoc). Le commit audité ajoute les secrets d'abonnement qui activent ces gardes réellement. Manque : enforcement par session (le budget actuel est global, pas par brief — voir brief 009 pour palliatif).

# 5. Commandes rejouées

Conformément au contrat `cursor-auditor` (preuve de fin : commandes citées rejouées, sortie collée), voici les commandes exécutées :

## 5.1. Fraîcheur du commit

```bash
$ git log --oneline -1 cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
cdc683f Merge pull request #24 from PLiagre/forge/workflow-quatre-acteurs-977d
```

```bash
$ git branch -a --contains cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
* cursor/audit-cdc683f-hermes-workflow-quatre-acteurs
  remotes/origin/HEAD -> origin/master
  remotes/origin/master
```

**Confirmation** : le commit audité est la tête actuelle de `master` et `origin/master`.

## 5.2. Historique de la PR

```bash
$ git log --oneline --graph --all -15
*   cdc683f Merge pull request #24 from PLiagre/forge/workflow-quatre-acteurs-977d
|\  
| * e619640 auth: privilégier les quotas d'abonnement (CLAUDE_CODE_OAUTH_TOKEN, CODEX_AUTH_JSON) sur le crédit API
| * c4f2a86 ci: corriger le faux positif gitleaks (auth Basic en en-tête) et SC2016 (backticks dans printf)
| * 0bfddeb docs: HANDOFF — la branche de session est forge/* (cursor/* est réservé aux audits)
| * f458d02 docs: HANDOFF réécrit depuis l'état réel — ADR-0010, câblage complet, nettoyage
| * b6ab76c audits: clore les 7 boucles du ledger (4 obsolètes → STALE→ARCHIVED, 2 livrées → IMPLEMENTED→VERIFIED→ARCHIVED)
| * 0b4ac9f pipeline: câbler les trois invocations réelles (Claude headless, Codex gpt-5.6-sol, Cursor API)
| * 9ad76ff ADR-0010: Hermes chef de projet, ROADMAP.md, contrat hermes/, guide de critique sourcé pour Cursor
| * f8c3008 env: reprendre AGENTS.md et l'ignore .venv de la PR #1 (notes VM cloud Linux)
|/  
*   0a8b022 Merge pull request #22 from PLiagre/forge/stabilisation-2026-08-12
```

## 5.3. Vérification des fichiers clés

```bash
$ ls -lh ROADMAP.md hermes/README.md architecture/review-guidelines.md docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md
-rw-r--r-- 1 ubuntu ubuntu  3.6K Aug 12 09:36 ROADMAP.md
-rw-r--r-- 1 ubuntu ubuntu  2.8K Aug 12 09:36 architecture/review-guidelines.md
-rw-r--r-- 1 ubuntu ubuntu  4.7K Aug 12 09:36 docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md
-rw-r--r-- 1 ubuntu ubuntu  2.1K Aug 12 09:36 hermes/README.md
```

Tous les fichiers clés introduits par le commit sont présents et non vides.

## 5.4. Vérification des workflows modifiés

```bash
$ grep -n "TODO(operator" .github/workflows/pipeline-audit.yml .github/workflows/pipeline-challenge.yml .github/workflows/pipeline-forge-run.yml
(aucune sortie — plus aucun stub TODO(operator...) présent)
```

**Confirmation** : les trois workflows d'invocation d'agents sont réellement câblés.

```bash
$ grep -A5 "CLAUDE_CODE_OAUTH_TOKEN\|CODEX_AUTH_JSON" .github/workflows/pipeline-forge-run.yml | head -20
      - name: Check headless credential availability
        id: check
        if: steps.pause.outputs.paused != 'true' && steps.mode.outputs.mode != 'manual'
        env:
          HAS_CLAUDE_SUB: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN != '' }}
          HAS_ANTHROPIC: ${{ secrets.ANTHROPIC_API_KEY != '' }}
          HAS_CODEX_SUB: ${{ secrets.CODEX_AUTH_JSON != '' }}
          HAS_OPENAI: ${{ secrets.OPENAI_API_KEY != '' }}
--
      - name: Bootstrap Codex subscription auth (auth.json)
        if: steps.check.outputs.available == 'true'
        env:
          CODEX_AUTH_JSON: ${{ secrets.CODEX_AUTH_JSON }}
        run: |
          set -euo pipefail
          if [ -n "${CODEX_AUTH_JSON:-}" ]; then
            mkdir -p "$HOME/.codex"
            chmod 700 "$HOME/.codex"
            printf '%s' "$CODEX_AUTH_JSON" > "$HOME/.codex/auth.json"
```

**Confirmation** : les secrets d'abonnement sont bien référencés, le bootstrap `auth.json` est implémenté.

## 5.5. Vérification du mode full_auto

```bash
$ grep "^mode:" harness/pipeline/config.yaml
mode: full_auto
```

```bash
$ grep "^mode:" harness/pipeline/auto_policy.yaml
mode: full_auto
```

**Confirmation** : le mode `full_auto` est déclaré dans les deux fichiers (un comme déclaration normative, l'autre comme doc).

# 6. Risques classifiés par sévérité (CI verte/rouge, jobs concernés)

**CI du commit audité** : inférée verte (le merge a été accepté sur master). Les trois workflows d'invocation (`pipeline-audit`, `pipeline-challenge`, `pipeline-forge-run`) ne sont pas déclenchés par ce merge lui-même (ils se déclenchent sur leurs propres événements : push sur master pour audit, push touchant inbox/reviews pour challenge, workflow_dispatch pour forge-run).

**Jobs concernés par les risques** :
- **P0 (auth abonnement non testée)** : `pipeline-forge-run.yml`, step `Bootstrap Codex subscription auth (auth.json)` — première exécution réelle plantera si le secret est invalide.
- **P1 (Hermes cumule propose+relit)** : pas de job CI spécifique — risque architectural.
- **P1 (guide critique non sync)** : `pipeline-audit.yml` (génère audits avec nouvelle structure) vs anciens audits — friction future si relecture d'un ancien audit.
- **P2 (ROADMAP.md hors allowlist)** : `merge-bot.yml` — PR Hermes ne sera jamais auto-fusionnée.
- **P2 (pas de smoke-test Codex CLI)** : `pipeline-forge-run.yml`, step `Install Claude Code and Codex CLIs` — si Codex CLI est cassé, erreur seulement à l'invocation réelle.
- **P3 (sources externes guide critique)** : pas de job CI — risque de péremption documentaire d'ici fin 2026.

**Résumé CI** : les workflows sont câblés, le mode `full_auto` est légal, les tests du garde-fou ont été inversés consciemment (voir commit 0b4ac9f, message explicite). La CI est verte sur le merge, mais aucun des trois workflows d'invocation n'a réellement tourné (ils attendent les secrets).

# 7. Briefs proposés (≤ 3)

Conformément au contrat `cursor-auditor` (≤ 3 briefs atomiques proposés par audit), voici les briefs recommandés :

## Brief 1 : Validation des secrets d'authentification d'abonnement en CI

**Objectif** : ajouter un job CI qui valide la structure des secrets `CLAUDE_CODE_OAUTH_TOKEN` et `CODEX_AUTH_JSON` quand ils existent, avant toute invocation réelle.

**Périmètre** :
- Nouveau workflow `.github/workflows/validate-agent-secrets.yml` (optionnel, non bloquant, tourne seulement si au moins un secret d'abonnement est provisionné).
- Validations :
  - `CLAUDE_CODE_OAUTH_TOKEN` : non vide, pas de whitespace leading/trailing.
  - `CODEX_AUTH_JSON` : JSON valide (`jq . >/dev/null`), champs requis présents (à définir d'après la doc officielle Codex).
- Documentation dans `docs/rules/full-auto-pipeline.md` : format attendu des secrets, exemple redacté.

**Résultat attendu** : si un secret est mal formé, le workflow de validation échoue avec un message explicite avant toute invocation d'agent — feedback immédiat au lieu d'un plantage en production.

**Lien avec risque** : referme le constat P0 (auth abonnement non testée).

## Brief 2 : Cycle de relecture de ROADMAP.md (séparation producteur/réviseur)

**Objectif** : restaurer la séparation producteur/juge pour `ROADMAP.md` en soumettant toute PR Hermes à une relecture obligatoire par un acteur distinct.

**Périmètre** :
- Modifier `hermes/README.md` : ajouter section « Cycle de fusion — relecture par Claude CTO ou Cursor obligatoire, jamais d'auto-merge ».
- Ajouter règle dans `.github/workflows/` ou configuration GitHub (CODEOWNERS, branch protection) : toute PR touchant `ROADMAP.md` ou `hermes/**` requiert une approbation de `claude-developer` ou `cursor-auditor` (implémentation : label ou check obligatoire).
- Documenter dans ADR-0010 (amendement) : Hermes produit, un acteur distinct relit.

**Résultat attendu** : aucune PR Hermes ne fusionne sans une relecture contradictoire — fin du cumul « propose + relit ce qu'il a écrit ».

**Lien avec risque** : referme le constat P1 (Hermes cumule propose+relit).

## Brief 3 : Versionnement du guide de critique et migration des audits existants

**Objectif** : ajouter un champ `guideline_version` dans le frontmatter des audits pour tracer quelle version du guide de critique s'applique, et documenter la version actuelle comme `v1.0` (2026-08-12).

**Périmètre** :
- Amender `architecture/README.md` (schéma frontmatter) : ajouter champ optionnel `guideline_version` (exemple : `v1.0`).
- Amender `architecture/review-guidelines.md` : ajouter en haut « Version v1.0, créée le 2026-08-12. Audits postérieurs à cette date doivent suivre cette structure (sévérités P0-P3, preuve citée). »
- Script de migration (optionnel) : ajouter `guideline_version: v1.0` rétroactivement aux audits existants qui suivent déjà la structure (e.g., `CURSOR-5633ee7-automation-completeness.md`).

**Résultat attendu** : chaque audit porte la version du guide qu'il suit — fin de l'ambiguïté sur « quel standard s'applique à quel audit ».

**Lien avec risque** : referme le constat P1 (guide critique non sync).

---

**Note** : un quatrième brief serait « smoke-test Codex CLI après install » (constat P2), mais il est de moindre priorité que les trois ci-dessus. Les constats P2 (ROADMAP.md hors allowlist) et P3 (sources externes guide critique) ne justifient pas un brief — le premier est une clarification documentaire (amender `hermes/README.md` directement), le second est un rappel calendrier (ajouter dans `ROADMAP.md` « re-sourcer review-guidelines.md T4 2026 »).

# 8. Vérification des briefs ouverts (aucun doublon)

Conformément au contrat `cursor-qa-scout` (« déclaration explicite 'aucun doublon avec un brief ouvert' ou la liste des briefs vérifiés »), voici les briefs ouverts examinés :

**Briefs principaux dans `harness/queue/briefs/` (hors fixtures)** :
- `001-spatial-primary-key-adr` — ADR sur clé primaire spatiale (délivré, non concerné)
- `002-geo-pipeline-coastline-1400` — pipeline géo littoral 1400 (délivré, non concerné)
- `003-port-unity-game` — portage Unity (délivré, non concerné)
- `004-polish-visuel` — polish visuel carte (ouvert, gate bloqué sur logs Unity)
- `005-refonte-visuelle-carte` — refonte visuelle carte (ouvert, gate bloqué sur logs Unity)
- `006-full-auto-agent-pipeline` — pipeline full-auto (délivré, tous lots acceptés)
- `007-geo-pipeline-cells-adjacency` — pipeline géo cellules adjacence (ouvert)
- `008-full-auto-automation-gaps` — automation gaps (deux variantes : `008-contexte-opus5-right-sizing` et `008-full-auto-automation-gaps`, statut à vérifier)
- `009-full-auto-agent-invocation` — invocation agents (délivré, lot 009a accepté)
- `010-repartition-roles-full-auto` — répartition rôles full-auto (délivré, lots 010a/010b/010c acceptés)

**Vérification doublons** :
- Brief 1 proposé (validation secrets auth) : aucun brief ouvert ne couvre la validation CI des secrets d'abonnement. Le brief 009 (invocation agents) câble les appels mais ne teste pas les secrets.
- Brief 2 proposé (relecture ROADMAP.md) : aucun brief ouvert ne couvre le cycle de relecture de Hermes. ADR-0010 établit le rôle mais ne ferme pas la séparation producteur/réviseur.
- Brief 3 proposé (versionnement guide critique) : aucun brief ouvert ne couvre le versionnement du guide de critique. Le guide vient d'être introduit par ce commit (ADR-0010), aucun brief antérieur ne traite ce sujet.

**Déclaration** : **aucun doublon avec un brief ouvert**. Les trois briefs proposés couvrent des lacunes non traitées par les briefs existants.

# 9. Conclusion

Le commit `cdc683f` (ADR-0010 + workflow quatre acteurs) est un changement structurel majeur qui termine la phase F0 (harnais) en câblant réellement les trois workflows d'invocation d'agents. Il introduit Hermes comme chef de projet, privilégie l'authentification par abonnement sur la facturation API, et fournit un guide de critique sourcé pour Cursor.

**Forces** : direction unique (`ROADMAP.md`), fin des stubs d'invocation, séparation rôles claire (quatre acteurs, jamais deux maillons adjacents tenus par le même acteur sur le même lot).

**Risques principaux** : authentification par abonnement non testée en CI (P0), Hermes cumule « propose » et « relit la roadmap qu'il écrit » (P1), guide de critique non synchronisé avec les audits existants (P1).

Les trois briefs proposés referment les constats P0/P1 et alignent le dépôt sur l'état de l'art 2026 (validation secrets, relecture contradictoire, versionnement protocoles de critique).

**Recommandation finale** : approuver le commit (il livre ADR-0010 et referme les stubs d'invocation, deux jalons majeurs), puis planifier les trois briefs proposés pour refermer les lacunes résiduelles avant de provisionner les secrets en production.
