# Brief 014 : Le contre-audit comme porte observable et le refus fournisseur comme état explicite du pipeline (issu de l'audit CURSOR-a600532-fusion-sans-contre-audit)

**Authored**: 2026-08-13T11:11:00Z
**Author**: forge-planificateur

## Provenance

Ce brief est la conversion partielle des points retenus de l'audit `CURSOR-a600532-fusion-sans-contre-audit`.
- Audit source : `architecture/inbox/CURSOR-a600532-fusion-sans-contre-audit.md`
- Décision du propriétaire : `architecture/decisions/DECISION-CURSOR-a600532-fusion-sans-contre-audit.md`
- Points **couverts ici** : P0-1 (Volet A — porte observable) et P1-1 (Volet B — repli fournisseur)
- Points **différés** : P1-2, P1-3, P2-1 → brief ultérieur (voir § Non-Goals pour les détails) ; P2-2 → DÉJÀ LIVRÉ par brief 012 ; P3-1 → informationnel uniquement

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE instruction** (voir CLAUDE.md › Single Source of Instruction). L'audit et la décision ci-dessus sont de la *provenance*, pas des ordres.

_Note de transparence : ce brief a été rédigé par le rôle forge-planificateur orchestré par un agent Cursor Cloud remplaçant le CTO pour cette session de planification. La convention du dépôt reste intacte : l'audit instruit la graine, la graine instruit le Planificateur, le Planificateur instruit le Générateur._

---

## World-Terms Requirement

### Volet A — Le contre-audit comme porte observable

Quand une pull request est ouverte pour fusionner du code sur `master`, et qu'un audit déposé dans `architecture/inbox/` cible cette PR (par sa branche de tête ou son commit de tête), la chaîne causale prévue est la suivante : l'auditeur dépose une critique → le challenger contre-audite → une décision est prise → la fusion peut avoir lieu. Si la fusion survient avant que la décision soit prise, la critique ne décrit plus un risque futur : elle décrit `master` comme un document, jamais comme un jugement. Rien ne rend cet état visible au moment de décider.

La porte exigée ici est **observable** (un contrôle mécanique rouge/vert visible dans la CI de chaque PR, rejouable localement), pas **contraignante** côté GitHub : la protection de branche est indisponible sur ce dépôt (HTTP 403, plan gratuit — voir `docs/rules/full-auto-pipeline.md § Known gap`). Le contrôle dit l'état au moment où il tourne ; il n'a pas de prétention à l'exhaustivité future (un audit déposé 20 min après l'ouverture de la PR n'existait pas lors du premier run du contrôle — c'est assumé, pas une lacune à corriger ici).

### Volet B — Le refus fournisseur comme état explicite

Quand le CLI Claude renvoie une erreur HTTP 429 (plafond mensuel de l'organisation Anthropic atteint), l'étape « Invoke claude-challenger headless » de `pipeline-challenge.yml` se termine avec `exit 1`. Le run suivant recommence à l'identique. Onze fois consécutivement (voir § 5.3 de l'audit source). Aucune revue n'est produite, aucun état n'est consigné, aucun repli n'est tenté. La chaîne causale est rompue sans trace.

Le dépôt possède déjà les décisions pour ne pas s'arrêter là : ADR-0008 (Codex peut tenir le rôle Évaluateur quand Claude a atteint son plafond, session distincte) et ADR-0009 (Codex comme backend Générateur officiel) établissent qu'un autre backend peut prendre le relais. Le causal change requis est : le refus fournisseur devient un état distinct (consigné, persistant, lisible depuis un clone), et le workflow tente le repli Codex au lieu d'échouer sec à l'identique.

---

## Success Conditions

### SC1 — Module `pr_audit_guard.py` : détection des audits non adjugés ciblant une PR

Un nouveau module `harness/pipeline/pr_audit_guard.py` est créé. Ce module est stdlib-only (aucune dépendance tierce) et directement testable par pytest sans contexte GitHub Actions.

**Définition d'un audit ciblant une PR** : un fichier `architecture/inbox/*.md` dont le frontmatter YAML contient un champ `target_branch` égal à la branche de tête de la PR, **ou** un champ `target_commit` dont les 7 premiers caractères correspondent aux 7 premiers caractères du SHA du commit de tête de la PR.

**Définition de l'adjudication** : un audit est *adjugé* si son état courant dans `architecture/audit-ledger.jsonl` (lu via `audit_ledger.current_state_for`) est l'un de : `AUDIT_APPROVED`, `AUDIT_REJECTED`, `AUDIT_CONVERTED`, `AUDIT_IMPLEMENTED`, `AUDIT_VERIFIED`, `AUDIT_ARCHIVED`. Un audit est *non adjugé* si son état est `None` (aucune ligne au ledger — PROPOSED implicite), `AUDIT_PROPOSED`, `AUDIT_CHALLENGED`, ou `AUDIT_STALE`. Ce choix est conservateur : un audit STALE décrit une critique jamais décidée, et le contrôle doit le signaler plutôt que le silencer.

**Interface CLI** :
```
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch <branche> \
  --head-commit <sha> \
  [--inbox architecture/inbox] \
  [--ledger architecture/audit-ledger.jsonl]
```

Comportement attendu :
- Si aucun audit dans l'inbox ne cible cette PR : sortie 0, message « Aucun audit ne cible cette PR — contrôle vert. »
- Si tous les audits ciblants sont adjugés : sortie 0, message listant les audits et leur état.
- Si au moins un audit ciblant est non adjugé : sortie **non nulle (1)**, message explicite listant les audits concernés et leur état actuel.

Le module ne modifie aucun fichier ; il est en lecture seule.

**Tests pytest** (compteurs `code_sortie_guard_pr_avec_audit_non_adjuge` et `code_sortie_guard_pr_sans_audit`) :

```
.venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py -v
```

Résultats attendus : tous PASSED. Les tests utilisent des fixtures inline (inbox et ledger synthétiques dans des répertoires temporaires — pas de dépendance au dépôt réel) et couvrent au minimum :
1. Aucun audit dans l'inbox → sortie 0
2. Un audit cible la PR, état PROPOSED implicite (aucune ligne au ledger) → sortie 1
3. Un audit cible la PR, état CHALLENGED → sortie 1
4. Un audit cible la PR, état APPROVED → sortie 0
5. Un audit cible la PR, état ARCHIVED → sortie 0
6. Deux audits ciblent la PR, l'un CHALLENGED, l'autre APPROVED → sortie 1 (le premier suffit)
7. Un audit cible par `target_commit` (7 premiers caractères du SHA) → détection correcte
8. Un audit ne cible pas la PR (branch et commit différents) → sortie 0

---

### SC2 — Job `audit-check` dans `audit-guard.yml` : porte visible dans la CI des PRs

Un nouveau job `audit-check` est ajouté au fichier `.github/workflows/audit-guard.yml` **existant**. Il est déclenché uniquement sur l'événement `pull_request` (la condition `if: github.event_name == 'pull_request'` est nécessaire pour ne pas bloquer les runs sur push).

Le job appelle `pr_audit_guard.py check` avec :
- `--head-branch ${{ github.head_ref }}`
- `--head-commit ${{ github.event.pull_request.head.sha }}`

Si le script retourne un code de sortie non nul, le job échoue (rouge visible dans l'interface des vérifications de la PR). Si le script retourne 0, le job passe (vert).

**INTERDIT** : créer un nouveau fichier `.github/workflows/pipeline-*.yml`. Le job est ajouté à `audit-guard.yml` uniquement.

Compteur `pipeline_workflows_count` : le nombre de fichiers `pipeline-*.yml` dans `.github/workflows/` doit être identique avant et après (aucun nouveau fichier de ce motif).

Compteur `audit_guard_job_count` : le nombre de jobs dans `audit-guard.yml` après modification doit être ≥ 3 (schema + cursor-scope + audit-check).

```
.venv/bin/python -c "
import pathlib, re
txt = pathlib.Path('.github/workflows/audit-guard.yml').read_text()
in_jobs = False; jobs = []
for line in txt.splitlines():
    if line.rstrip() == 'jobs:':
        in_jobs = True; continue
    if in_jobs and re.match(r'^  [a-zA-Z0-9_-]+:$', line):
        jobs.append(line.strip().rstrip(':'))
    elif in_jobs and line and not line.startswith(' '):
        in_jobs = False
print('jobs:', sorted(jobs)); print('count:', len(jobs))
"
```

---

### SC3 — Module `vendor_refusal.py` : classification du flux Claude et consignation persistante

Un nouveau module `harness/pipeline/vendor_refusal.py` est créé. Stdlib-only, testable par pytest.

**Fonction `classify(transcript_path: Path | str) -> str`** : lit un fichier JSONL stream-json produit par le CLI Claude (`--output-format stream-json`). Retourne :
- `"vendor_refusal"` si au moins une ligne contient `"is_error": true` **et** `"api_error_status": 429` (le cas mesuré onze fois dans l'audit source)
- `"success"` si le flux contient une ligne avec `"result"` et `"is_error": false` (ou `"is_error"` absent), sans aucune ligne d'erreur fatale
- `"other_error"` si le flux contient `"is_error": true` mais `"api_error_status"` n'est pas 429

Si le fichier est vide ou inexistant, `classify` retourne `"other_error"` (pas de succès simulé).

**Fonction `log_refusal(audit_id: str, transcript_path: Path | str, state_path: Path | str) -> None`** : ajoute une ligne JSON à `state_path` (fichier JSONL) avec les champs : `timestamp` (ISO 8601 UTC), `audit_id`, `error_type` (`"vendor_refusal"`), `api_error_status` (429), `fallback_attempted` (initialement `false`, mis à jour par la fonction `mark_fallback_attempted` si appelée par la suite).

**Fonction `mark_fallback_actor(review_path: Path | str, actor: str = "forge-challenger-codex") -> None`** : insère au début du corps du fichier de revue (après le frontmatter YAML) un encart clairement identifié :

```
> **Acteur réel** : `forge-challenger-codex` (repli fournisseur — Claude a retourné HTTP 429).
> Ce contre-audit a été produit par le CLI Codex en remplacement du CLI Claude dont
> le plafond mensuel de l'organisation était atteint (ADR-0008, ADR-0009).
```

**Fichier d'état persistant** : `harness/pipeline/vendor-refusal-state.jsonl`. Ce fichier est :
- Suivi par git (non exclu par `.gitignore`) — la preuve d'un refus doit être consultable depuis un clone, pas seulement dans le log du run.
- Créé vide (0 octets ou première ligne vide) par le Générateur comme fichier initial committé.
- Appendé lors de chaque détection de refus fournisseur par la CI.

**Tests pytest** (compteurs `classification_transcript_429`, `classification_transcript_succes`, `classification_transcript_autre`, `lignes_etat_refus_apres_log`) :

```
.venv/bin/python -m pytest harness/tests/test_vendor_refusal.py -v
```

Résultats attendus : tous PASSED. Les fixtures sont des fichiers JSONL synthétiques créés dans des répertoires temporaires. Tests minimaux couverts :
1. Transcript 429 (`is_error=true`, `api_error_status=429`) → `"vendor_refusal"`
2. Transcript succès (`result=...`, pas d'erreur) → `"success"`
3. Transcript autre erreur (`is_error=true`, `api_error_status=500`) → `"other_error"`
4. Transcript vide → `"other_error"`
5. `log_refusal()` sur fixture → fichier d'état contient ≥ 1 ligne, champs requis présents
6. `mark_fallback_actor()` sur un fichier de revue fixture → le marqueur `forge-challenger-codex` est présent dans le corps du fichier

---

### SC4 — Mise à jour de `pipeline-challenge.yml` : repli Codex en cas de refus fournisseur

Le fichier `.github/workflows/pipeline-challenge.yml` est modifié pour intégrer la gestion du refus fournisseur. Les gardes existants (kill-switch, mode `manual`, `ci_budget_guard`, plafond par appel) **ne sont pas modifiés** — seules des étapes sont ajoutées **après** l'étape d'invocation Claude existante.

**Étapes ajoutées après « Invoke claude-challenger headless »** :

1. **Classify vendor refusal** : appelle `vendor_refusal.classify()` sur le transcript JSONL produit par l'étape précédente. Si la classification est `"vendor_refusal"` :
   a. Appelle `vendor_refusal.log_refusal()` → ajoute une ligne à `harness/pipeline/vendor-refusal-state.jsonl`.
   b. Vérifie la disponibilité du CLI Codex et des identifiants (`CODEX_AUTH_JSON` ou `OPENAI_API_KEY`).
   c. Si disponibles : tente `codex exec` avec la même commande (`/forge-audit-review <AUDIT_ID>`) sous les mêmes gardes (mode `manual` coupe, label `pipeline/pause` coupe, `ci_budget_guard precheck`, plafond par appel). Le job Codex ne requiert pas `--max-budget-usd` (interface CLI différente) mais la durée de l'appel est bornée par `ci_budget_guard`.
   d. Si Codex produit une revue dans `architecture/reviews/` : appelle `vendor_refusal.mark_fallback_actor()` pour insérer le marqueur `forge-challenger-codex` dans le fichier de revue.
   e. Inclut `harness/pipeline/vendor-refusal-state.jsonl` dans le commit de la branche de revue (avec `git add`).
   f. Si Codex échoue ou si aucun identifiant Codex n'est disponible : `::warning::Repli Codex échoué (ou absent) pour AUDIT_ID <audit_id> — contre-audit non produit. État du refus consigné dans vendor-refusal-state.jsonl.` — puis exit 1 (pas de succès simulé, pas de silence).

2. Si la classification est `"other_error"` ou si le transcript est absent : comportement inchangé (`exit 1` existant, le job échoue).

**Compteur `repli_codex_marque_acteur_reel`** : le test unitaire SC3 prouve mécaniquement que `mark_fallback_actor()` insère le marqueur correct dans un fichier de revue fixture. La vérification en CI (revue Codex réelle) dépend de la disponibilité des identifiants Codex en secret du dépôt — c'est une dérogation acceptable si les identifiants sont absents (voir § Acceptable Waivers).

---

### SC5 — Preuves rouges : deux paires obligatoires

Chaque paire est produite depuis une copie sabotée hors du dépôt (répertoire temporaire sans lien git). Les sorties sont committées sous `harness/pipeline/proof_red/` en `.txt` (jamais `.log`).

**Paire A — sabotage « garde toujours verte » (Volet A) :**
- Sabotage : dans la copie hors dépôt, modifier `pr_audit_guard.py` pour que la fonction retourne toujours 0 (ignorer l'état d'adjudication).
- Test affecté : `test_pr_audit_guard.py::test_exits_1_when_audit_challenged`.
- `harness/pipeline/proof_red/run_guard_pr_red.txt` : sortie pytest avec le sabotage → doit contenir au moins un `FAILED`.
- `harness/pipeline/proof_red/run_guard_pr_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

**Paire B — sabotage « classification toujours other_error » (Volet B) :**
- Sabotage : dans la copie hors dépôt, modifier `vendor_refusal.classify()` pour retourner toujours `"other_error"`.
- Test affecté : `test_vendor_refusal.py::test_classify_429_returns_vendor_refusal`.
- `harness/pipeline/proof_red/run_vendor_classif_red.txt` : sortie pytest avec le sabotage → doit contenir au moins un `FAILED`.
- `harness/pipeline/proof_red/run_vendor_classif_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

Forme `must_differ_from` dans `deliverables/manifest.json` — **par fichier**, forme lue par `harness/verdict_audit.py` :

```json
{
  "path": "../../../../harness/pipeline/proof_red/run_guard_pr_green.txt",
  "must_differ_from": "../../../../harness/pipeline/proof_red/run_guard_pr_red.txt"
}
```

(idem pour la paire B). Les quatre fichiers de preuve sont committés avant l'écriture du journal.

---

### SC6 — Suite complète verte

```
.venv/bin/python -m pytest harness/tests/ -q
.venv/bin/python -m pytest sim/tests/ -v
```

Les deux commandes s'achèvent sans `FAILED`. Les `SKIP` attendus sur Linux (tests Unity, `test_run_unity.py`) restent acceptés.

---

### SC7 — Registre de coût

Une ligne est ajoutée en fin de `harness/queue/cost-ledger.jsonl` via :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/014-pipeline-contre-audit-porte \
  --event generator-run \
  --audit-id CURSOR-a600532-fusion-sans-contre-audit
```

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Traiter P1-2** (escalade log-only invisible dans la vue du propriétaire) ni **P2-1** (enregistrement `AUDIT_PROPOSED` à l'entrée, santé de la boucle dans `hermes/DASHBOARD.md`) : proposition n°2 de l'audit source — brief ultérieur à créer.

2. **Traiter P1-3** (constats pré-fusion entrés sans arbitrage) : question de gouvernance, pas d'objet de code pour ce brief.

3. **Traiter les acteurs réels au ledger** : `audit_review`/`audit_convert`/`audit_archive` écrivent `claude`/`owner` en dur, et le comptage des verdicts porte sur tout le texte plutôt que sur les seules lignes de tableau (comparer `audit_review.py` à `audit_decision.parse_point_verdicts`) — brief ultérieur.

4. **Traiter la sérialisation des orchestrations concurrentes** (cause réelle mesurée par l'audit `CURSOR-4c45718` point 1 : `actions/checkout` fixe l'arbre au SHA poussé, pas à la tête de master) — brief ultérieur.

5. **Traiter les points 1 et 7 différés de `CURSOR-3b47ffe`** (traçage mécanique de l'acteur des trois rôles ; gate sur les fichiers déclarés hors dossier de brief) — brief ultérieur.

6. **Redemander `sim/tests/` en CI** : DÉJÀ LIVRÉ par le brief 012 (job `sim-tests` dans `harness-ci.yml`) — le constater, ne rien redemander.

7. **Rendre la porte contraignante côté GitHub** (protection de branche, statut « vérification requise ») : le plan gratuit retourne HTTP 403 sur l'API de protection de branche — voir `docs/rules/full-auto-pipeline.md § Known gap`. La porte est observable, pas bloquante.

8. **Modifier** : `harness/verdict_audit.py`, `sim/`, `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, `hermes/`, les archives des briefs 001–013.

9. **Rapporter un compteur depuis un inbox vide** : tout compteur SC1 mesurant le nombre d'audits ciblants doit être calculé sur des fixtures non vides (au moins un fichier .md dans l'inbox synthétique).

10. **Créer un nouveau fichier `.github/workflows/pipeline-*.yml`** : précédent du compteur `pipeline_workflows_count` du brief 006, contrat explicite de ce brief.

---

## Required Counters

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `audits_ciblant_pr` | `architecture/inbox/*.md` dont le frontmatter `target_branch` ou `target_commit` (7 premiers chars) correspond à la fixture de PR dans le test | tous les fichiers .md de l'inbox synthétique du test ; doit être ≥ 1 dans les tests positifs |
| `audits_non_adjuges_ciblant_pr` | sous-ensemble des audits ciblants dont l'état dans le ledger synthétique est None, PROPOSED, CHALLENGED ou STALE | `audits_ciblant_pr` ; doit être ≥ 1 dans le test `test_exits_1_when_audit_challenged` |
| `code_sortie_guard_pr_avec_audit_non_adjuge` | code de sortie de `pr_audit_guard.py check` sur fixture avec ≥ 1 audit CHALLENGED ciblant la PR | 1 invocation ; doit être = 1 |
| `code_sortie_guard_pr_sans_audit` | code de sortie de `pr_audit_guard.py check` sur fixture avec inbox vide ou aucun audit ciblant | 1 invocation ; doit être = 0 |
| `pipeline_workflows_count` | `ls .github/workflows/pipeline-*.yml` avant et après modification | nombre de fichiers `pipeline-*.yml` ; doit être identique (aucun nouveau fichier) |
| `audit_guard_job_count` | clés sous `jobs:` dans `.github/workflows/audit-guard.yml` après modification | doit être ≥ 3 (schema + cursor-scope + audit-check) |
| `classification_transcript_429` | fixture JSONL avec `"is_error": true, "api_error_status": 429` passée à `vendor_refusal.classify()` | 1 classification ; doit être = `"vendor_refusal"` |
| `classification_transcript_succes` | fixture JSONL avec `"result": "...", "is_error": false` passée à `vendor_refusal.classify()` | 1 classification ; doit être = `"success"` |
| `classification_transcript_autre` | fixture JSONL avec `"is_error": true, "api_error_status": 500` passée à `vendor_refusal.classify()` | 1 classification ; doit être = `"other_error"` |
| `lignes_etat_refus_apres_log` | fichier `vendor-refusal-state.jsonl` synthétique après un appel `vendor_refusal.log_refusal()` | 1 fichier ; nombre de lignes JSON valides ≥ 1 |
| `repli_codex_marque_acteur_reel` | fichier de revue fixture après `vendor_refusal.mark_fallback_actor()` | 1 fichier ; contient la chaîne `forge-challenger-codex` |
| `ci_pr_guard_collectes_014` | `.venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py --collect-only -q` depuis la racine | nombre de tests collectés ; **> 0** |
| `ci_vendor_refusal_collectes_014` | `.venv/bin/python -m pytest harness/tests/test_vendor_refusal.py --collect-only -q` depuis la racine | nombre de tests collectés ; **> 0** |

---

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/014-pipeline-contre-audit-porte` | la sortie contient la chaîne `UNMEASURABLE` |
| « `audit_ledger` n'est pas importable depuis `harness/pipeline/` » | `.venv/bin/python -c "import sys; sys.path.insert(0, 'harness'); import audit_ledger"` depuis la racine | le message d'erreur Python exact (ImportError avec le nom du module manquant) |
| « `codex exec` n'est pas disponible sur ce runner » | `codex --version 2>&1 \|\| echo "exit:$?"` depuis la racine | la sortie contient `exit:127` ou `command not found` |
| « la protection de branche GitHub est indisponible sur ce dépôt » | `gh api repos/PLiagre/ForgeHistory/branches/master/protection` | la réponse contient `403` ou `Resource not accessible by integration` |
| « les identifiants Codex sont absents en CI » | inspection du secret `CODEX_AUTH_JSON` et `OPENAI_API_KEY` dans le job | `::warning::Repli Codex indisponible — identifiants absents` (message exact dans le log du job) |

Aucune autre dérogation n'est recevable. En particulier :
- « Il est impossible de tester `pr_audit_guard.py` sans une vraie inbox/ledger » **n'est pas une dérogation** : les tests utilisent des fixtures synthétiques dans des répertoires temporaires, conformément au motif de `harness/pipeline/trigger_resolve.py`.
- « La classification 429 est non testable sans un vrai transcript Claude » **n'est pas une dérogation** : les fixtures JSONL sont construites inline dans les tests (les lignes de l'audit source § 5.3 servent de modèle exact).
- « La modification de `pipeline-challenge.yml` est trop invasive » **n'est pas une dérogation** : les gardes existants ne sont pas modifiés, seules des étapes sont ajoutées après l'invocation Claude.

---

## Execution Contract

### Périmètre autorisé

Ce brief couvre exclusivement :
- `harness/pipeline/pr_audit_guard.py` (nouveau module)
- `harness/pipeline/vendor_refusal.py` (nouveau module)
- `harness/pipeline/vendor-refusal-state.jsonl` (nouveau fichier d'état, committé vide)
- `harness/pipeline/proof_red/` (preuves rouges nouvelles)
- `harness/tests/test_pr_audit_guard.py` (nouveau test)
- `harness/tests/test_vendor_refusal.py` (nouveau test)
- `.github/workflows/audit-guard.yml` (ajout d'un job uniquement)
- `.github/workflows/pipeline-challenge.yml` (ajout d'étapes après l'invocation Claude)
- `harness/queue/briefs/014-pipeline-contre-audit-porte/` (livrables du présent lot)
- `harness/queue/cost-ledger.jsonl` (ajout d'une seule ligne en fin de fichier, SC7)

Fichiers interdits : tout fichier sous `harness/verdict_audit.py`, `harness/pipeline/config.yaml`, `harness/pipeline/ci_budget_guard.py`, `harness/pipeline/trigger_resolve.py`, `harness/pipeline/orchestrator.py`, `architecture/`, `sim/`, `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, `hermes/`, `.github/workflows/pipeline-*.yml` (existants ou nouveaux), et tout fichier sous `harness/queue/briefs/001-*/` à `harness/queue/briefs/013-*/`.

Tout fichier déclaré dans `deliverables/manifest.json` doit être suivi par git. `.gitignore` exclut `*.log` et `unity/game_unity/Logs/` — les preuves sont committées en `.txt`, jamais en `.log`.

### Estimation d'appels d'outils

**Estimation : 130 appels.** Ancres : brief 012 (~120 appels, sous-système `sim/` de zéro) ; brief 013 (estimé 125, corrections sur sous-système existant). Ce brief touche `harness/pipeline/` avec deux nouveaux modules stdlib de taille modeste (~100-150 lignes chacun), deux fichiers de test (~100 lignes chacun), deux modifications chirurgicales de workflows (ajout de job + ajout d'étapes), quatre fichiers de preuve rouge, un manifest, un journal. Pas de sous-système entièrement nouveau. Plafond dur : 160 appels ; checkpoint obligatoire à 130.

Commande de vérification pré-génération (à exécuter avant tout travail de fond) :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/014-pipeline-contre-audit-porte \
  --estimated-calls 130
```

Le Générateur déclare dans son journal, avant de commencer le travail de fond, soit la valeur mesurée du budget, soit la dérogation `UNMEASURABLE` (avec la sortie de `harness/budget.py status` à l'appui).

### Preuve rouge d'abord (hard-won rule 4) — deux paires obligatoires

Chaque paire est produite depuis une copie sabotée hors du dépôt (répertoire temporaire sans lien git, sans toucher les fichiers du dépôt). Les sorties sont committées sous `harness/pipeline/proof_red/` en `.txt`.

**Paire A — sabotage « garde toujours verte » :**
- Sabotage : dans la copie hors dépôt, faire retourner `0` à la fonction principale de `pr_audit_guard.py` sans consulter le ledger.
- Test affecté : `test_pr_audit_guard.py::test_exits_1_when_audit_challenged`.
- `harness/pipeline/proof_red/run_guard_pr_red.txt` : sortie pytest avec sabotage → doit contenir au moins un `FAILED`.
- `harness/pipeline/proof_red/run_guard_pr_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

**Paire B — sabotage « classification toujours other_error » :**
- Sabotage : dans la copie hors dépôt, faire retourner `"other_error"` à `vendor_refusal.classify()` quelle que soit l'entrée.
- Test affecté : `test_vendor_refusal.py::test_classify_429_returns_vendor_refusal`.
- `harness/pipeline/proof_red/run_vendor_classif_red.txt` : sortie pytest avec sabotage → doit contenir au moins un `FAILED`.
- `harness/pipeline/proof_red/run_vendor_classif_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

Forme `must_differ_from` dans `deliverables/manifest.json` — par fichier :

```json
{
  "path": "../../../../harness/pipeline/proof_red/run_guard_pr_green.txt",
  "must_differ_from": "../../../../harness/pipeline/proof_red/run_guard_pr_red.txt"
}
```

(idem pour la paire B). Les quatre fichiers de preuve sont committés avant l'écriture du journal.

### Interdictions pour le Générateur

- **Ne pas committer, ne pas pousser, ne pas créer de branche.** Cette interdiction est répétée en toutes lettres parce qu'elle a été violée deux fois par des Générateurs précédents sur ce dépôt (briefs 009 et 010) : un commit ou un push par le Générateur pollue l'historique git et invalide les garanties du harnais.
- Ne pas modifier `brief.md`, `eval-rubric.md` ni `verdict.md`.
- **Jamais `python` nu** — toujours `.venv/bin/python`.
- Ne pas recopier de valeur hexadécimale de condensé SHA256 (hard-won rule 12).
- Ne pas créer de fichier `.github/workflows/pipeline-*.yml` (existants ou nouveaux).
- Ne pas modifier les gardes existants de `pipeline-challenge.yml` (kill-switch, mode, `ci_budget_guard`, plafond `--max-budget-usd`) — seulement ajouter des étapes après l'invocation Claude.
- Ne pas simuler un succès Codex quand Codex échoue ou est absent — la dérogation est un `::warning::` explicite suivi d'un exit 1, jamais un exit 0 sans revue produite.

### Fin de lot

Le gate mécanique doit répondre `ACCEPT` :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
```

La suite complète doit être verte :

```
.venv/bin/python -m pytest harness/tests/ -q
.venv/bin/python -m pytest sim/tests/ -v
```

Les deux sorties réelles sont recopiées dans le journal — pas seulement déposées dans un fichier annexe.

**Celui qui produit ne prononce pas la recevabilité.**

---

_Amendement — 2026-08-13T11:22:00Z : (1) commande du compteur `audit_guard_job_count` en SC2 remplacée par un one-liner stdlib-only (suppression de `import yaml`, non disponible dans le venv du dépôt). (2) `${{ github.sha }}` corrigé en `${{ github.event.pull_request.head.sha }}` en SC2 — sur un événement `pull_request`, `github.sha` est le SHA du commit de fusion simulé, pas le commit de tête de la PR. Aucun changement de fond._
