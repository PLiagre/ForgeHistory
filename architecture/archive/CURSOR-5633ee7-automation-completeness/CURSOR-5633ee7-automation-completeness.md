---
audit_id: CURSOR-5633ee7-automation-completeness
auditor: cursor-cloud
target_branch: master
target_commit: 5633ee74c10de5fa2653dff3b871d684a202ff30
created_at: 2026-08-06T11:14:21Z
audit_type: automation-completeness-gap-analysis
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

État de l'audit : **CURRENT**. `git fetch origin master` confirme que
`master`, `origin/master` et le commit cible pointent tous sur
`5633ee74c10de5fa2653dff3b871d684a202ff30` (merge de la PR #8,
`forge/cursor-audit-loop`).

Question posée : *que reste-t-il à faire pour que l'automatisation soit
réellement complète, de bout en bout ?* Ce rapport ne réaudite pas ce que
`CURSOR-6231186-execution-budgets.md` et
`CURSOR-POSTMERGE-42cb054-audit-system.md` ont déjà couvert ; il se
concentre sur ce qui a changé depuis (brief 006, lots a/b/c, activation de
`mode: full_auto`) et sur une preuve **survenue sur ce commit même**, pas
narrée.

## Cinq risques majeurs

1. **P0 — La boucle full-auto s'est réellement cassée sur ce commit, en
   CI, aujourd'hui.** Le job `pipeline-orchestrate` a échoué (exit 2) sur
   le push de fusion de la PR #8 : il a tenté de rejouer l'événement
   `review_recorded` sur l'audit `CURSOR-FIXTURE-full-auto-demo`, déjà
   `AUDIT_ARCHIVED`. Ce n'est pas une hypothèse — c'est un run GitHub
   Actions réel, `FAILURE`, observable maintenant.
2. **P1 — Les trois invocations d'agents qui font tourner la boucle
   n'existent pas.** `pipeline-audit.yml`, `pipeline-challenge.yml` et
   `pipeline-forge-run.yml` ne contiennent, à l'endroit même où l'agent
   devrait être appelé, qu'un `echo "TODO(operator, once ... is
   provisioned): ..."`. Fournir les secrets manquants aujourd'hui ne
   ferait **rien** tourner de plus : le code d'appel réel n'est pas écrit.
3. **P1 — Aucune règle de la table de décision ne couvre l'échec d'un
   workflow lui-même.** `auto_policy.yaml` sait réagir à trois REJECT
   mécaniques consécutifs (`three_consecutive_mechanical_rejects`) mais
   n'a aucune ligne pour « un job `pipeline-*.yml` a planté » — exactement
   ce qui vient d'arriver. Le système ne s'auto-signale pas ses propres
   pannes d'infrastructure.
4. **P1 — Le maillon « graine de brief → brief exploitable » est marqué
   `<<TODO>>` dans le schéma normatif lui-même.**
   `docs/rules/full-auto-pipeline.md` l'écrit noir sur blanc :
   `[claude-planificateur] fills brief <<TODO>>`. `audit_convert.convert()`
   crée une graine, pas un brief prêt pour `/forge-run`.
5. **P2 — Le budget d'exécution ne mesure toujours pas le backend
   Cursor**, exactement le même défaut que relevé par
   `CURSOR-6231186-execution-budgets.md` (FINDING-ARCH-003) et non corrigé
   depuis : `harness/budget.py` ne connaît qu'un statut générique
   `UNMEASURABLE`, jamais un refus explicite dédié au backend Cursor.

## Trois angles morts que ce rapport ne referme pas

1. Le vrai jeu (Unity) n'a aucune CI : aucune preuve neuve sur ce point
   depuis `CURSOR-6231186`, non ré-instruite ici par manque de licence/
   runner disponibles sur ce poste.
2. `gh secret list` et `gh api .../branches/master/protection` renvoient
   tous deux `403` sur ce runner — je ne peux confirmer l'absence des
   secrets que par la documentation du dépôt lui-même (manifest.json du
   brief 006), pas par une lecture directe.
3. Je n'ai pas cherché à reproduire une exécution complète de
   `/forge-run` en tant que telle (hors périmètre en lecture seule d'un
   audit).

# 2. Provenance et fraîcheur

- Branche cible : `master`. Commit cible complet :
  `5633ee74c10de5fa2653dff3b871d684a202ff30`. Commit court : `5633ee7`.
- Fraîcheur : **CURRENT** ; zéro commit entre la cible et `origin/master`
  au moment de la rédaction (`git fetch origin master` puis
  `git rev-parse origin/master` exécutés directement, sortie identique).
- Branche documentaire : `cursor/audit-5633ee7-automation-gaps-73c6` — la
  politique de branche du runner Cloud impose le préfixe `cursor/` et le
  suffixe `-73c6` ; elle prime sur tout autre format, comme déjà noté par
  `CURSOR-6231186-execution-budgets.md`.
- Fichiers inspectés en profondeur : `docs/adr/0006-full-auto-agent-pipeline.md`,
  `docs/rules/full-auto-pipeline.md`, `harness/pipeline/config.yaml`,
  `harness/pipeline/auto_policy.yaml`, `harness/pipeline/orchestrator.py`,
  `harness/budget.py`, `.github/workflows/pipeline-audit.yml`,
  `.github/workflows/pipeline-challenge.yml`,
  `.github/workflows/pipeline-forge-run.yml`,
  `.github/workflows/pipeline-orchestrate.yml`,
  `.github/workflows/merge-bot.yml`, `.github/merge-bot.yaml`,
  `.github/workflows/audit-guard.yml`, `harness/audit_schema.py`,
  `architecture/README.md`, `architecture/audit-ledger.jsonl`,
  `architecture/inbox/*.md`, `architecture/decisions/*.md`,
  `harness/queue/briefs/006-full-auto-agent-pipeline/{brief.md,verdict*.md}`.
- Non accessible depuis ce runner : `gh secret list` (403 « Resource not
  accessible by integration »), `gh api repos/.../branches/master/protection`
  (403, même message qu'au précédent audit — plan GitHub, pas une
  régression), licence/runner Unity.
- Limites : aucune modification de code effectuée ; aucun `/forge-run`
  lancé ; aucun agent Cursor/Claude headless réellement invoqué (pas de
  clé disponible ici non plus).

## Résultats exécutés (commandes réelles, sorties réelles)

| Commande | Résultat observé |
|---|---|
| `git fetch origin master && git rev-parse origin/master` | `5633ee74c10de5fa2653dff3b871d684a202ff30`, identique à `HEAD` |
| `python3 -m pytest harness/tests/ -q` | `235 passed, 15 skipped` |
| `python3 harness/harness_audit.py` | `SCORE: 20/24` (les 2 FAIL structurels sont déjà connus et attribués dans `HANDOFF.md`) |
| `gh run list --limit 15` | Le run `pipeline-orchestrate` sur le push de fusion de la PR #8 (`31085883052`) est `completed / failure` |
| `gh run view 31085883052 --log` | `error: audit 'CURSOR-FIXTURE-full-auto-demo' is AUDIT_ARCHIVED, not AUDIT_CHALLENGED; only a challenged audit can be decided (--policy auto included)` puis `Process completed with exit code 2` |
| `gh secret list` | `403: Resource not accessible by integration` (confirme, sans contredire, la documentation existante) |
| `gh api repos/PLiagre/ForgeHistory/branches/master/protection` | `403` — feature indisponible sur ce plan, conforme à `docs/rules/full-auto-pipeline.md` § Known gap |
| `python3 -m pytest harness/ -k orchestr -q` | `9 passed` — aucun de ces 9 tests ne rejoue le scénario « audit déjà terminal + heuristique de diff sur un squash-merge » |

# 3. Preuve en direct : le pipeline s'est cassé sur ce commit même

Ce n'est pas une déduction depuis le code : c'est un fait déjà survenu et
visible dans l'historique d'Actions du dépôt au moment de cet audit.

**Chronologie reconstituée** :

1. La PR #8 (`forge/cursor-audit-loop`) fusionne plusieurs fichiers
   `architecture/**` d'un coup, dont la fixture de démo du brief 006
   (`CURSOR-FIXTURE-full-auto-demo`, sa revue et sa décision), déjà menée
   jusqu'à `AUDIT_ARCHIVED` par la démo `run_full_auto_demo.sh`.
2. `pipeline-orchestrate.yml` se déclenche sur tout push à `master` qui
   touche `architecture/reviews/*.md`. Sa logique d'auto-déclenchement
   (`git diff --name-only "$before" "$after" -- 'architecture/reviews/*.md'`)
   ne regarde que **combien** de fichiers de revue ont changé entre les
   deux extrémités du push — pas leur état déjà connu dans le ledger.
3. Le diff du push de fusion a produit exactement 1 fichier de revue
   changé → le workflow a construit `event=review_recorded`,
   `payload={"audit_id": "CURSOR-FIXTURE-full-auto-demo"}`, et a appelé
   `orchestrator.py run --event review_recorded --payload ...`.
4. `audit_decision.decide_auto()` a refusé de rejouer une transition sur
   un audit déjà en état terminal, et l'a fait bruyamment (exit 2) —
   c'est le comportement correct et voulu du garde-fou. Mais le
   **déclencheur en amont** n'a aucune notion de « cet audit est déjà
   clos, ne le redéclenche pas », donc l'échec remonte comme un job rouge
   dans Actions plutôt que d'être absorbé proprement.

**Pourquoi c'est plus grave qu'un incident isolé** : l'heuristique
« exactement 1 fichier de revue changé entre `before` et `after` » est
correcte pour un push contenant un seul commit de `claude-challenger`,
mais devient un piège dès qu'une fusion (squash ou merge commit) regroupe
plusieurs commits touchant `architecture/`, ce qui est **le mode normal
de fusion d'une PR de migration** comme la #8 elle-même. Le système ne
distingue pas « nouvelle revue jamais traitée » de « revue déjà
présente avant ce push, redevenue visible dans le diff par effet de
bord d'un squash ». Rien dans `auto_policy.yaml` ne nomme cet événement
(échec de dispatch du workflow lui-même) ; personne — ni humain, ni bot —
n'est notifié que `pipeline-orchestrate` est rouge sur `master`
aujourd'hui.

# 4. Carte de ce qui est réellement automatisé, vs déclaré, vs stub

| Maillon de la chaîne (voir `docs/rules/full-auto-pipeline.md` § Diagram) | État réel constaté |
|---|---|
| Audit Cursor après merge sur master | **Stub.** `pipeline-audit.yml` vérifie `CURSOR_API_KEY`, puis n'exécute qu'un `echo "TODO(operator...)"` — même avec la clé, rien n'est appelé. |
| Contre-audit Claude après un nouvel audit | **Stub pour la partie LLM**, réel pour la mécanique : `mechanical-scaffold-smoke` prouve que `scaffold → record → ledger` fonctionne sans LLM ; l'invocation Claude elle-même est un `echo "TODO(operator...)"`. |
| Fusion automatique des PR de bot (`inbox/`, `reviews/`, `feedback/`) | **Réel et testé.** `merge-bot.yml` vérifie liste blanche/noire avant tout `gh pr merge --auto` ; échec de fusion traité en soft-failure (PR reste ouverte). |
| Décision automatique (accepter/rejeter/convertir) | **Réel et testé.** `audit_decision.py`, 9 tests dédiés à `orchestrator.py`, chemin d'écriture unique vers le ledger prouvé mécaniquement (`test_no_direct_ledger_file_write_in_source`). C'est la partie la plus solide de toute la chaîne. |
| Détection de l'événement à traiter (auto-dispatch sur push) | **Fragile, prouvé cassé** (section 3) — heuristique de diff non robuste aux fusions multi-fichiers. |
| Remplissage du brief après conversion (Planificateur) | **Non automatisé, marqué `<<TODO>>`** dans le schéma normatif lui-même. Une « graine » de brief existe ; le contenu réel (Success Conditions, portée, budget estimé) reste une étape humaine ou une invocation Claude séparée non câblée. |
| Lancement du Générateur (`/forge-run`) | **Stub pour l'invocation**, réel pour le garde-fou : `pipeline-forge-run.yml` exécute réellement `budget.py split-check` avant tout lancement (aucun brief surdimensionné ne peut être lancé, même en théorie) ; l'appel `claude-developer` lui-même est un `echo "TODO(operator...)"`. |
| Gate mécanique après génération | **Réel, mature, testé** (`verdict_audit.py`, 9 contrôles, démos fake/honest). Hors périmètre de ce rapport, déjà audité ailleurs. |
| Lancement de l'Évaluateur après ACCEPT | **Non implémenté.** `orchestrator.py` le documente lui-même : `gate_accept -> (log only -- launching claude-evaluator is an external agent invocation, not this module's job)`. |
| Escalade sur 3 REJECT mécaniques consécutifs | **Réel** (règle nommée dans `auto_policy.yaml`, testée). |
| Escalade sur panne d'un workflow `pipeline-*` lui-même | **Absente.** Aucune ligne de `auto_policy.yaml` ne nomme cet événement (section 3). |
| Budget d'exécution — backend Claude | **Réel**, seuils 100/130/160 câblés et testés (`harness/tests/test_budget.py`). |
| Budget d'exécution — backend Cursor | **Non mesurable**, statut générique `UNMEASURABLE` seulement — pas de refus dédié, gap déjà connu et non corrigé depuis `CURSOR-6231186`. |
| Protection de la branche `master` contre une PR bot malveillante/buguée | **Un seul rempart**, pas deux : la vérification `deny_paths` interne à `merge-bot.yml`, faute de fonctionnalité de protection de branche GitHub disponible sur ce plan (403, documenté, pas caché). |

# 5. Constats d'architecture

## FINDING-ARCH-001 — L'auto-déclenchement de l'orchestrateur ne connaît pas l'état du ledger

- Priorité : **P0**
- Confiance : HIGH
- Source : PERSONAL_INFERENCE, corroborée par un run GitHub Actions réel
- Fichiers concernés : `.github/workflows/pipeline-orchestrate.yml`,
  `harness/pipeline/orchestrator.py`, `harness/audit_decision.py`
- Risque : un job rouge non traité à chaque fusion qui regroupe plusieurs
  fichiers `architecture/reviews/*.md`, dont les fusions de migration
  elles-mêmes
- Complexité : faible
- Rollback : revenir à `workflow_dispatch` uniquement (perdre le
  déclenchement automatique, garder la fiabilité)

### Observation

Le déclencheur choisit l'événement à envoyer à l'orchestrateur en
comptant les fichiers `architecture/reviews/*.md` changés entre les deux
bornes du push, sans jamais consulter `architecture/audit-ledger.jsonl`
pour savoir si l'audit visé est déjà dans un état terminal.

### Preuve

`gh run view 31085883052 --log` (voir section 3) : exit 2, message exact
`audit 'CURSOR-FIXTURE-full-auto-demo' is AUDIT_ARCHIVED, not
AUDIT_CHALLENGED`. Run public :
https://github.com/PLiagre/ForgeHistory/actions/runs/31085883052.

### Conséquence

Toute PR qui fusionne plusieurs fichiers `architecture/` en une fois (un
mode de fusion parfaitement normal, y compris pour les propres PR de
migration du système) peut rejouer un événement sur un audit déjà clos et
faire échouer le job, sans qu'aucune règle de la table de politique ne
sache réagir à ce cas.

### Recommandation minimale

Avant de construire le payload, lire le ledger et exclure tout
`audit_id` dont le dernier état est déjà terminal
(`AUDIT_ARCHIVED`/`AUDIT_VERIFIED`/`AUDIT_REJECTED` sans suite) de la
liste des candidats à un nouveau `review_recorded`.

### Alternatives

Remplacer l'heuristique de comptage par une lecture explicite du
frontmatter du fichier de revue lui-même (`status: PROPOSED` seulement
sur les revues neuves) plutôt qu'un comptage de diff ; ou n'auto-déclencher
que sur un push d'exactement un commit (refuser silencieusement les
squash multi-fichiers, en documentant que ceux-là exigent
`workflow_dispatch`).

### Critères d'acceptation

Un test rejoue exactement le scénario du run `31085883052` (fixture déjà
`AUDIT_ARCHIVED` réapparaissant dans un diff multi-fichiers) et prouve que
le workflow ne tente plus la transition — soit il l'ignore proprement
avec un `::notice::`, soit il échoue mais avec une action de nettoyage
documentée, jamais un job rouge muet.

## FINDING-ARCH-002 — Les trois invocations d'agents sont des commentaires, pas du code

- Priorité : P1
- Confiance : HIGH
- Source : OFFICIAL (lecture directe des trois fichiers de workflow)
- Fichiers concernés : `.github/workflows/pipeline-audit.yml`,
  `.github/workflows/pipeline-challenge.yml`,
  `.github/workflows/pipeline-forge-run.yml`
- Risque : croire que provisionner `CURSOR_API_KEY`/`ANTHROPIC_API_KEY`
  suffit à activer la boucle réelle
- Complexité : moyenne à élevée (dépend de l'API Cursor Cloud Agent et du
  mode headless Claude retenus)
- Rollback : garder les invocations manuelles documentées
  (`docs/rules/full-auto-pipeline.md` § How to activate) tant que le
  code réel n'est pas livré

### Observation

Les trois étapes qui devraient réellement appeler un agent contiennent,
mot pour mot, un bloc `echo "TODO(operator, once ... is provisioned):
..."` à la place d'un appel HTTP ou CLI réel.

### Preuve

Lignes 56-62 de `pipeline-audit.yml`, lignes 63-72 de
`pipeline-challenge.yml`, lignes 90-100 de `pipeline-forge-run.yml` —
citées telles quelles dans ce rapport, section 2.

### Conséquence

`mode: full_auto` est activé dans `harness/pipeline/config.yaml`, mais la
chaîne ne peut aujourd'hui produire aucun audit Cursor réel, aucune revue
Claude réelle, ni aucun brief réellement généré sans intervention
manuelle — quel que soit l'état des secrets. C'est l'écart le plus
important entre ce que le nom « full-auto » suggère et ce que le dépôt
peut exécuter seul.

### Recommandation minimale

Écrire l'appel réel pour au moins un des trois maillons (le plus mesurable
étant `pipeline-challenge.yml`, qui a déjà un test mécanique
« scaffold-smoke » à côté duquel comparer un vrai résultat), avec un test
qui échoue tant que le corps de l'étape reste une chaîne `TODO`.

### Alternatives

Documenter explicitement que `mode: full_auto` ne couvre aujourd'hui que
la **décision et la fusion**, pas la **génération de contenu par agent**,
en renommant le mode ou en ajoutant un sous-statut
(`mode: full_auto_decision_only`) plutôt que de laisser un seul mot
couvrir deux niveaux d'automatisation très différents.

### Critères d'acceptation

Un test CI échoue si un des trois fichiers contient encore la chaîne
littérale `TODO(operator` après qu'un secret requis est configuré ; sinon
même provisionner les clés ne change jamais le comportement observable.

## FINDING-ARCH-003 — Aucune règle de politique ne couvre l'échec d'un workflow `pipeline-*` lui-même

- Priorité : P1
- Confiance : HIGH
- Source : OFFICIAL (lecture de `auto_policy.yaml`) + démonstration en
  direct (section 3)
- Fichiers concernés : `harness/pipeline/auto_policy.yaml`,
  `harness/pipeline/orchestrator.py`
- Risque : une panne d'infrastructure silencieuse, jamais élevée au rang
  d'incident
- Complexité : faible à moyenne
- Rollback : garder l'escalade actuelle (3 REJECT mécaniques) inchangée

### Observation

`auto_policy.yaml` ne définit que dix règles ; une seule concerne une
escalade (`three_consecutive_mechanical_rejects`, sur l'événement
`gate_reject`). Aucune règle ne nomme l'événement « un job
`pipeline-*.yml` a terminé en échec », que ce soit pour une erreur de
script, un timeout, ou — comme démontré — une transition refusée par le
garde-fou lui-même.

### Preuve

Lecture exhaustive de `auto_policy.yaml` (14 lignes de `rules:`, 10
entrées) : zéro occurrence de mots-clés comme `workflow_failure`,
`infra`, ou `job_failed`. Corroboré par le run `31085883052`, resté rouge
sans qu'aucun ticket ni notification ne soit créé.

### Conséquence

Un système qui se veut « zéro intervention humaine » doit survivre à ses
propres pannes d'infrastructure au moins aussi bien qu'à un mauvais
travail d'agent. Aujourd'hui, un audit mal formé peut ouvrir
`pipeline-stuck` ; un pipeline cassé ne le peut pas — il attend qu'un
humain regarde l'onglet Actions par hasard.

### Recommandation minimale

Ajouter une règle `pipeline_job_failed` (déclenchée par un
`workflow_run` sur `conclusion: failure` des quatre workflows
`pipeline-*.yml`) qui ouvre le même type d'issue bot `pipeline-stuck`
que la règle existante, avec le nom du job et le lien du run en corps.

### Alternatives

Ajouter un cron quotidien (`nightly-resilience`-like) qui relit les runs
`pipeline-*` des dernières 24h via `gh run list --status failure` et
ouvre une issue groupée plutôt qu'une par échec.

### Critères d'acceptation

Un run de test simulant l'échec du run `31085883052` (rejoué en fixture)
produit une issue `pipeline-stuck` dans les mêmes conditions qu'un
troisième REJECT mécanique consécutif.

## FINDING-ARCH-004 — Le remplissage du brief après conversion reste un `<<TODO>>` documenté comme tel

- Priorité : P2
- Confiance : HIGH
- Source : OFFICIAL (citation directe du document normatif)
- Fichiers concernés : `docs/rules/full-auto-pipeline.md`,
  `harness/audit_convert.py`, `harness/pipeline/auto_policy.yaml`
- Risque : une conversion d'audit accepté qui s'arrête à une coquille
  vide, sans qu'aucun mécanisme ne relance la suite
- Complexité : élevée (dépend du même problème que FINDING-ARCH-002 —
  invocation réelle d'un agent)
- Rollback : garder l'étape humaine (le propriétaire remplit le brief
  lui-même après conversion)

### Observation

Le diagramme normatif du pipeline écrit lui-même :
`[claude-planificateur] fills brief <<TODO>>`, et la règle de politique
`brief_seed_created` a pour action
`claude_planificateur_fills_todo_same_pipeline_separate_invocation` — un
nom de règle qui documente honnêtement l'absence de câblage plutôt que de
le cacher.

### Preuve

`docs/rules/full-auto-pipeline.md`, section Diagram, ligne 40 ; `auto_policy.yaml`
lignes 45-48.

### Conséquence

Le maillon `AUDIT_CONVERTED → brief rempli → /forge-run` n'est pas fermé
en full-auto : une graine de brief existe, mais rien n'automatise
aujourd'hui son passage à un brief exploitable par `pipeline-forge-run.yml`.

### Recommandation minimale

Documenter explicitement, dans `docs/rules/full-auto-pipeline.md`, que
cette étape reste un point d'arrêt humain **assumé**, jusqu'à ce qu'un
brief dédié la ferme — plutôt que de laisser un lecteur pressé croire que
`<<TODO>>` est un texte de gabarit oublié.

### Alternatives

Aucune ; fermer réellement ce maillon dépend de la même décision produit
que FINDING-ARCH-002 (quel agent, quelle API, quel budget).

### Critères d'acceptation

Un test de documentation (`test_single_source_of_instruction.py`-like)
vérifie que toute occurrence de `<<TODO>>` dans `docs/rules/` est
référencée par au moins un brief ouvert dans `harness/queue/briefs/`.

## FINDING-ARCH-005 — Le budget d'exécution reste aveugle au backend Cursor (régression non corrigée)

- Priorité : P2
- Confiance : HIGH
- Source : OFFICIAL, déjà documentée par `CURSOR-6231186-execution-budgets.md`
  (FINDING-ARCH-003), toujours vraie sur ce commit
- Fichiers concernés : `harness/budget.py`, `harness/backends/ledger.py`,
  `harness/backends/run_cursor_generator.sh`
- Risque : un Générateur Cursor sans plafond d'appels réellement imposé
- Complexité : moyenne
- Rollback : bloquer explicitement le backend Cursor pour tout brief
  soumis au plafond, plutôt que de le laisser `UNMEASURABLE`

### Observation

`harness/budget.py` classe toujours toute source non reconnue en
`UNMEASURABLE` générique (ligne 269, ligne 417) ; aucun statut dédié
`UNSUPPORTED_BUDGET_SOURCE` pour le backend Cursor n'a été ajouté depuis
le brief 006, alors que ce même brief a livré le superviseur de budget
(Lot 006c) pour le backend Claude.

### Preuve

`grep -n "Cursor|UNMEASURABLE|UNSUPPORTED" harness/budget.py` ne retourne
aucune ligne spécifique au backend Cursor ; `harness/backends/ledger.py`
documente lui-même que les tokens Cursor ne sont pas observables (déjà
cité par l'audit précédent, non modifié depuis).

### Conséquence

Le superviseur de budget livré en Lot 006c protège le backend Claude
contre un dépassement de 160 appels ; un Générateur lancé via le backend
Cursor (ADR-0002) n'a, à ce jour, toujours aucune garantie équivalente.

### Recommandation minimale

Distinguer `UNMEASURABLE` (source absente/illisible) de
`UNSUPPORTED_BUDGET_SOURCE` (backend connu mais non instrumenté), et
refuser explicitement de lancer un brief à fort risque de dépassement sur
le backend Cursor tant que ce dernier statut est actif.

### Alternatives

Utiliser le flux `stream-json` documenté par Cursor CLI (déjà identifié
comme piste par `CURSOR-6231186-execution-budgets.md`) pour instrumenter
réellement ce backend plutôt que de le bloquer.

### Critères d'acceptation

Un test prouve qu'un transcript Cursor connu (fixture anonymisée) produit
un statut distinct de `UNMEASURABLE`, et qu'un brief marqué à fort risque
refuse de démarrer sur ce backend tant que ce statut n'est pas résolu.

# 6. Sources externes consultées

Consultées le 2026-08-06. Une date d'activité est une observation
GitHub/fournisseur, pas une garantie de maintenance future.

| Source | Classe | Activité/licence | Pratique pertinente | Limite et applicabilité |
|---|---|---|---|---|
| [GitHub Actions — `workflow_run` trigger](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow-run) | OFFICIAL | Documentation consultée 2026-08-06 | Déclencher un workflow sur la conclusion d'un autre (`conclusion: failure`) | Base directe pour FINDING-ARCH-003 ; ne remplace pas une règle de politique testée localement. |
| [GitHub Actions — réutiliser les runs et leurs logs](https://docs.github.com/en/actions/how-tos/monitor-workflows/use-workflow-run-logs) | OFFICIAL | Documentation consultée 2026-08-06 | `gh run view --log` pour une preuve reproductible | Utilisé directement dans ce rapport pour citer le run `31085883052`. |
| [Cursor — Headless CLI](https://cursor.com/docs/cli/headless) | OFFICIAL | Documentation consultée 2026-08-06 | `stream-json` expose les événements d'appels d'outils, utile à la fois pour l'invocation réelle (FINDING-ARCH-002) et pour le budget (FINDING-ARCH-005) | Déjà cité par l'audit précédent ; toujours non exploité dans le code au moment de cet audit. |
| [Anthropic — Claude Code headless / SDK](https://docs.anthropic.com/en/docs/claude-code/headless) | OFFICIAL | Documentation consultée 2026-08-06 | Mode d'invocation non interactif utilisable dans `pipeline-challenge.yml`/`pipeline-forge-run.yml` | Candidat direct pour remplacer les blocs `TODO(operator...)` ; nécessite `ANTHROPIC_API_KEY`, absent aujourd'hui. |
| [GitHub — limites du plan gratuit sur la protection de branche](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) | OFFICIAL | Documentation consultée 2026-08-06 | Confirme que la protection de branche avancée est un avantage payant/dépôt public | Corrobore le `403` déjà documenté ; n'offre pas de contournement gratuit propre. |

# 7. Briefs proposés à Claude

Ces briefs sont **proposés, non autorisés**. Aucun n'engage
d'implémentation ; seule une conversion explicite par le propriétaire
(ou, en `mode: full_auto`, par la table de décision automatique elle-même
si ce rapport est confirmé et retenu) peut les transformer en travail
réel.

## BRIEF-PROP-001 — Fiabiliser le déclencheur de l'orchestrateur (corrige un incident réel)

- Finding source : ARCH-001.
- Objectif : qu'un push fusionnant plusieurs fichiers `architecture/reviews/*.md`
  ne puisse plus rejouer une transition sur un audit déjà terminal.
- Contexte vérifié : run `31085883052`, `FAILURE`, exit 2, message cité
  section 3.
- Périmètre : lecture du ledger avant construction du payload dans
  `pipeline-orchestrate.yml` ; un test qui rejoue exactement ce scénario.
- Hors périmètre : réécriture de la politique de décision elle-même,
  nouveaux rôles, Unity.
- Fichiers probablement concernés : `.github/workflows/pipeline-orchestrate.yml`,
  `harness/audit_ledger.py` (lecture), nouveau test sous `harness/tests/`.
- Tests à ajouter : rejouer le diff exact de la PR #8 sur une fixture
  isolée ; vérifier qu'aucune transition n'est tentée sur un `audit_id`
  déjà terminal.
- Critères d'acceptation : le scénario reproduit dans ce rapport ne
  produit plus de job rouge muet — soit un skip documenté, soit une
  escalade explicite (voir BRIEF-PROP-002).
- Budget estimé : 60 à 90 appels outils (correction ciblée, un seul
  fichier de logique + tests).
- Risques : faux négatif si le ledger est en retard sur le push en cours.
- Rollback : revenir à `workflow_dispatch` uniquement le temps du
  correctif.
- Dépendances : aucune.

## BRIEF-PROP-002 — Escalade automatique sur panne d'un workflow `pipeline-*`

- Finding source : ARCH-003.
- Objectif : qu'une panne d'infrastructure (pas seulement un REJECT
  mécanique répété) ouvre la même escalade `pipeline-stuck`.
- Contexte vérifié : `auto_policy.yaml` n'a aucune règle pour cet
  événement ; le run `31085883052` est resté rouge sans signal.
- Périmètre : nouvelle règle de politique + déclencheur `workflow_run`
  sur les quatre `pipeline-*.yml` + test.
- Hors périmètre : les invocations d'agents elles-mêmes (BRIEF-PROP-003).
- Fichiers probablement concernés : `harness/pipeline/auto_policy.yaml`,
  nouveau workflow ou job additionnel, `harness/pipeline/orchestrator.py`.
- Tests à ajouter : simuler un `workflow_run` en échec, vérifier
  l'ouverture d'une issue `pipeline-stuck` avec le lien du run.
- Critères d'acceptation : une panne simulée produit une escalade
  identique en forme à celle des 3 REJECT consécutifs.
- Budget estimé : 70 à 100 appels outils.
- Risques : bruit si les échecs transitoires (timeout réseau GitHub) ne
  sont pas distingués des échecs de logique.
- Rollback : garder l'observation manuelle des runs en attendant.
- Dépendances : aucune ; peut être fait indépendamment de BRIEF-PROP-001.

## BRIEF-PROP-003 — Écrire l'invocation réelle d'au moins un des trois agents

- Finding source : ARCH-002, ARCH-004.
- Objectif : remplacer un des trois blocs `TODO(operator...)` par un
  appel réel, en commençant par le maillon le plus mesurable
  (`pipeline-challenge.yml`, qui a déjà `mechanical-scaffold-smoke` comme
  point de comparaison).
- Contexte vérifié : aucun des trois maillons n'appelle réellement un
  agent aujourd'hui, secrets ou non.
- Périmètre : un maillon complet (invocation + parsing de sortie +
  écriture du fichier attendu), pas les trois — `NEEDS_SPLIT` probable
  sinon.
- Hors périmètre : les deux autres maillons ; le remplissage automatique
  du brief (dépend d'une décision produit séparée, ARCH-004).
- Fichiers probablement concernés : `.github/workflows/pipeline-challenge.yml`,
  éventuellement un script `harness/backends/` dédié, tests avec fixture
  de réponse LLM enregistrée.
- Tests à ajouter : un test qui échoue tant que le corps de l'étape
  contient la chaîne littérale `TODO(operator`.
- Critères d'acceptation : avec `ANTHROPIC_API_KEY` fourni (en secret de
  test, jamais commité), une revue réelle est produite et passe
  `audit_schema.py`.
- Budget estimé : **NEEDS_SPLIT** probable — 110 à 150 appels rien que
  pour un maillon avec tests et fixtures.
- Risques : dépendance à une clé API réelle pour la validation finale ;
  coût récurrent à chaque déclenchement une fois activé.
- Rollback : garder le stub documenté si le coût récurrent est jugé trop
  élevé pour la fréquence des audits.
- Dépendances : décision du propriétaire sur le budget récurrent accepté
  pour des appels LLM automatiques en CI (question non tranchée par ce
  rapport).

# 8. Décisions humaines requises

1. Autoriser ou refuser chacun des trois briefs proposés ; ce rapport
   n'autorise aucune implémentation.
2. Décider si `mode: full_auto` doit être renommé ou scindé
   (FINDING-ARCH-002) pour ne plus laisser croire que la génération de
   contenu par agent est automatisée alors que seule la décision/fusion
   l'est.
3. Décider si l'escalade sur panne d'infrastructure (BRIEF-PROP-002) doit
   notifier un humain (issue GitHub) ou seulement un canal de log — ce
   rapport ne tranche pas le mode de notification, seulement l'absence
   actuelle de tout signal.
4. Choisir le budget récurrent accepté pour des appels LLM automatiques
   en CI avant d'autoriser BRIEF-PROP-003 — un maillon réellement câblé a
   un coût par déclenchement que ce rapport ne peut pas chiffrer sans
   accès aux tarifs contractuels du propriétaire.
5. Décider si le job `pipeline-orchestrate` actuellement rouge sur
   `master` (run `31085883052`) doit être traité comme un incident à
   corriger en urgence (BRIEF-PROP-001) avant tout autre brief
   d'automatisation, ou accepté comme un risque connu le temps qu'un
   brief le referme.
