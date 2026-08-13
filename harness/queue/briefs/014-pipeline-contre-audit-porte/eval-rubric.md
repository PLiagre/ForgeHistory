# Eval Rubric — Brief 014 : Le contre-audit comme porte observable et le refus fournisseur comme état explicite du pipeline

**Authored**: 2026-08-13T11:11:00Z
**Author**: forge-planificateur

Ne pas modifier après avoir vu les livrables du Générateur.
Le Planificateur écrit la rubrique avant tout code ; l'Évaluateur l'applique sans la réécrire.

---

## Guide de lecture

Chaque entrée correspond à une condition de succès du `brief.md`. Pour chaque condition :
- **Vérification** : commande à rejouer ou code à lire (depuis la racine du dépôt, avec `.venv/bin/python`).
- **Reconstruction indépendante** : l'Évaluateur monte sa propre fixture sans reprendre les fichiers du Générateur.
- **Résultat attendu** : ce que le Générateur doit avoir produit.
- **Contre-preuve disqualifiante** : comportement qui invalide la condition même si les tests passent.

---

## SC1 — Module `pr_audit_guard.py` : détection des audits non adjugés ciblant une PR

**Vérification de l'existence du module :**

1. Vérifier que `harness/pipeline/pr_audit_guard.py` existe et est suivi par git :
   ```
   git ls-files harness/pipeline/pr_audit_guard.py
   ```
   Résultat attendu : le chemin est affiché (non vide).

**Vérification de l'interface CLI :**

2. Rejouer depuis la racine :
   ```
   .venv/bin/python harness/pipeline/pr_audit_guard.py check \
     --help
   ```
   Résultat attendu : les arguments `--head-branch`, `--head-commit`, `--inbox`, `--ledger` sont documentés.

**Vérification des tests :**

3. Rejouer :
   ```
   .venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py -v
   ```
   Résultat attendu : tous `PASSED`. L'Évaluateur vérifie que les huit scénarios listés dans SC1 sont couverts (aucun audit, audit PROPOSED implicite, CHALLENGED, APPROVED, ARCHIVED, mix, ciblage par commit, non-ciblage).

**Reconstruction indépendante :**
L'Évaluateur crée un répertoire temporaire avec :
- `inbox/CURSOR-test-pr.md` avec frontmatter `target_branch: ma-branche`, `target_commit: abc1234...`, `status: PROPOSED`
- Ledger JSONL vide (aucune ligne)

Appelle `pr_audit_guard.py check --head-branch ma-branche --head-commit abc1234 --inbox <tmp>/inbox --ledger <tmp>/ledger.jsonl`. Vérifie que le code de sortie est 1.

Modifie ensuite le ledger pour y ajouter une ligne `{"audit_id": "CURSOR-test-pr", "event": "AUDIT_CHALLENGED", ...}` puis `{"audit_id": "CURSOR-test-pr", "event": "AUDIT_APPROVED", ...}`. Rejoue. Vérifie que le code de sortie est 0.

**Résultat attendu :** PASS si l'outil détecte correctement les audits non adjugés et retourne le code de sortie approprié. FAIL si le code de sortie est toujours 0 (garde aveugle), ou si un audit APPROVED est signalé comme non adjugé.

---

## SC2 — Job `audit-check` dans `audit-guard.yml`

**Vérification de l'absence de nouveau fichier pipeline-* :**

1. Rejouer :
   ```
   ls .github/workflows/pipeline-*.yml
   ```
   Résultat attendu : liste identique à celle avant ce brief (5 fichiers existants : `pipeline-audit.yml`, `pipeline-challenge.yml`, `pipeline-failure-escalate.yml`, `pipeline-forge-run.yml`, `pipeline-orchestrate.yml`). Aucun nouveau fichier.

**Vérification de l'ajout du job :**

2. Rejouer :
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
   Résultat attendu : `'audit-check'` apparaît dans la liste, `count` ≥ 3.

3. Lire `.github/workflows/audit-guard.yml` : le job `audit-check` doit comporter la condition `if: github.event_name == 'pull_request'` (ou équivalent) pour éviter de bloquer les runs sur push.

4. Lire `.github/workflows/audit-guard.yml` : le job `audit-check` doit appeler `pr_audit_guard.py check` avec `github.head_ref` et `github.event.pull_request.head.sha` — pas de valeurs en dur.

**Contre-preuve d'un nouveau pipeline-* :**
```
git diff HEAD -- .github/workflows/pipeline-*.yml
```
Si un nouveau fichier `pipeline-*.yml` est apparu, SC2 est non satisfaite.

**Résultat attendu :** PASS si le job est présent dans `audit-guard.yml`, qu'aucun nouveau `pipeline-*.yml` n'existe, et que la condition `pull_request` est présente. FAIL si le job est absent ou si un nouveau `pipeline-*.yml` a été créé.

---

## SC3 — Module `vendor_refusal.py` : classification et consignation

**Vérification de l'existence du module et du fichier d'état :**

1. :
   ```
   git ls-files harness/pipeline/vendor_refusal.py harness/pipeline/vendor-refusal-state.jsonl
   ```
   Résultat attendu : les deux chemins sont affichés (suivis par git).

**Vérification des fonctions :**

2. Lire `harness/pipeline/vendor_refusal.py` : les trois fonctions `classify`, `log_refusal`, et `mark_fallback_actor` doivent être présentes avec les signatures décrites dans SC3.

3. Lire `harness/pipeline/vendor_refusal.py` : `classify()` doit lire le fichier ligne par ligne et chercher `"is_error": true` avec `"api_error_status": 429`. Elle ne doit pas appeler de code réseau ni de dépendance tierce.

**Vérification des tests :**

4. Rejouer :
   ```
   .venv/bin/python -m pytest harness/tests/test_vendor_refusal.py -v
   ```
   Résultat attendu : tous `PASSED`. L'Évaluateur vérifie que les six scénarios listés dans SC3 sont couverts.

**Reconstruction indépendante de la classification :**
L'Évaluateur écrit un script minimal :
```py
import tempfile, json, pathlib, sys
sys.path.insert(0, "harness/pipeline")
import vendor_refusal

# Cas 429
with tempfile.NamedTemporaryFile(suffix=".jsonl", mode="w", delete=False) as f:
    f.write(json.dumps({"is_error": True, "api_error_status": 429, "total_cost_usd": 0}) + "\n")
    p = f.name
print("429 ->", vendor_refusal.classify(p))  # attendu: vendor_refusal

# Cas succès
with tempfile.NamedTemporaryFile(suffix=".jsonl", mode="w", delete=False) as f:
    f.write(json.dumps({"result": "ok", "is_error": False}) + "\n")
    p = f.name
print("success ->", vendor_refusal.classify(p))  # attendu: success
```
Les deux lignes imprimées doivent correspondre aux valeurs attendues.

**Reconstruction indépendante de `mark_fallback_actor` :**
L'Évaluateur crée un fichier de revue fictif (texte quelconque sans frontmatter), appelle `vendor_refusal.mark_fallback_actor(path)`, et lit le résultat. La chaîne `forge-challenger-codex` doit apparaître dans les premières lignes significatives du fichier.

**Résultat attendu :** PASS si les trois fonctions existent, que la classification est correcte pour les trois cas, que `log_refusal` produit une ligne JSON valide avec les champs requis, et que `mark_fallback_actor` insère le marqueur lisible. FAIL si `classify()` retourne toujours `"other_error"` quelle que soit l'entrée (sabotage type paire B).

---

## SC4 — Mise à jour de `pipeline-challenge.yml` : repli Codex

**Vérification des étapes ajoutées :**

1. Lire `.github/workflows/pipeline-challenge.yml` : des étapes nommées après l'étape « Invoke claude-challenger headless » doivent appeler `vendor_refusal.classify()` et `vendor_refusal.log_refusal()`.

2. Lire `.github/workflows/pipeline-challenge.yml` : les gardes existants (kill-switch `pipeline/pause`, mode `manual`, `ci_budget_guard precheck`, `--max-budget-usd`) ne doivent pas avoir été modifiés — `git diff HEAD -- .github/workflows/pipeline-challenge.yml` ne doit montrer que des ajouts d'étapes après l'invocation Claude, pas de suppressions de gardes.

3. Lire `.github/workflows/pipeline-challenge.yml` : l'étape de repli Codex doit inclure les vérifications de credential (`CODEX_AUTH_JSON` ou `OPENAI_API_KEY`) avant toute invocation.

4. Lire `.github/workflows/pipeline-challenge.yml` : en cas d'échec du repli Codex ou d'absence d'identifiant, l'étape doit émettre un `::warning::` (pas `::error::`) mais se terminer avec `exit 1` (pas de succès simulé). La présence de `exit 0` sans revue produite est un échec disqualifiant.

5. Lire `.github/workflows/pipeline-challenge.yml` : `vendor-refusal-state.jsonl` doit être inclus dans le `git add` de l'étape de publication de la revue, de sorte que le fichier d'état soit commis avec la branche de revue.

**Vérification du marqueur d'acteur Codex :**

6. Rejouer le test unitaire SC3 sur `mark_fallback_actor()` : le fichier de revue modifié contient `forge-challenger-codex`. (Le test pytest SC3 suffit comme vérification mécanique ; une revue Codex réelle en CI dépend des identifiants.)

**Contre-preuve du succès simulé :**
Dans une copie hors dépôt, modifier le workflow pour retirer l'`exit 1` du cas « repli échoué » et garder `exit 0`. Rejouer une invocation qui simule un transcript 429 sans identifiant Codex. Si le job se termine avec code 0 sans revue produite, SC4 est non satisfaite.

**Résultat attendu :** PASS si les étapes de détection, de consignation et de repli sont présentes, que les gardes ne sont pas modifiés, et que l'absence de repli aboutit à un exit 1 documenté. FAIL si un succès est simulé, si les gardes existants ont été retirés, ou si `vendor-refusal-state.jsonl` n'est pas inclus dans le commit de revue.

---

## SC5 — Preuves rouges : deux paires

**Paire A — garde toujours verte :**

1. Lire `harness/pipeline/proof_red/run_guard_pr_red.txt` : doit contenir au moins une ligne `FAILED`.
2. Lire `harness/pipeline/proof_red/run_guard_pr_green.txt` : doit contenir seulement des `PASSED` (aucun `FAILED`).
3. Vérifier que les deux fichiers existent et sont suivis par git :
   ```
   git ls-files harness/pipeline/proof_red/run_guard_pr_red.txt \
                harness/pipeline/proof_red/run_guard_pr_green.txt
   ```

**Contre-preuve indépendante de la paire A :**
L'Évaluateur crée un répertoire temporaire, copie `harness/pipeline/pr_audit_guard.py`, sabote la fonction pour retourner toujours `0`, monte la fixture du test `test_exits_1_when_audit_challenged` et rejoue le test. Sa sortie doit contenir `FAILED`.

**Paire B — classification toujours other_error :**

4. Lire `harness/pipeline/proof_red/run_vendor_classif_red.txt` : doit contenir au moins une ligne `FAILED`.
5. Lire `harness/pipeline/proof_red/run_vendor_classif_green.txt` : doit contenir seulement des `PASSED`.
6. Vérifier que les deux fichiers sont suivis par git :
   ```
   git ls-files harness/pipeline/proof_red/run_vendor_classif_red.txt \
                harness/pipeline/proof_red/run_vendor_classif_green.txt
   ```

**Vérification de la différence (gate `captures_differ_when_should`) :**
Le gate mécanique `verdict_audit.py` vérifie que les paires `must_differ_from` sont effectivement différentes. Si le gate retourne `ACCEPT`, ce critère est satisfait.

**Résultat attendu :** PASS si les quatre fichiers existent, sont suivis par git, si red contient `FAILED` et green contient `PASSED`, et si les paires diffèrent. FAIL si un fichier est absent, ou si red et green sont identiques.

---

## SC6 — Suite complète verte

**Vérification :**

1. Rejouer :
   ```
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   Résultat attendu : aucun `FAILED`. Les `SKIP` (tests Unity sur Linux) sont acceptés.

2. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/ -v
   ```
   Résultat attendu : aucun `FAILED`.

3. Vérifier que les deux sorties sont recopiées dans `deliverables/generator-log.md` (pas seulement déposées dans un fichier annexe) :
   ```
   grep -c "passed" harness/queue/briefs/014-pipeline-contre-audit-porte/deliverables/generator-log.md
   ```
   Résultat attendu : ≥ 2 occurrences de `passed` dans le journal (une par suite).

**Vérification que les archives des briefs 001–013 sont intactes :**
```
git diff HEAD -- harness/queue/briefs/001-*/
git diff HEAD -- harness/queue/briefs/013-*/
```
Résultats attendus : aucun diff.

**Résultat attendu :** PASS si les deux suites sont vertes ET si les archives antérieures sont intactes.

---

## SC7 — Registre de coût

**Vérification :**

1. Rejouer :
   ```
   .venv/bin/python harness/backends/ledger.py report
   ```
   Le brief `014-pipeline-contre-audit-porte` doit apparaître avec au moins `cursor=1`.

2. Lire la dernière ligne ajoutée à `harness/queue/cost-ledger.jsonl` : `"event": "generator-run"` (tiret, pas tiret bas), `"brief"` contient `014`, `"audit_id"` est `CURSOR-a600532-fusion-sans-contre-audit`.

**Résultat attendu :** PASS si la ligne est présente avec les bons champs.

---

## Preuves rouges — récapitulatif des vérifications

| paire | fichier red | fichier green | test sabotage |
|---|---|---|---|
| A (garde) | `run_guard_pr_red.txt` contient `FAILED` | `run_guard_pr_green.txt` contient `PASSED` | `pr_audit_guard.py` retourne toujours 0 → test `test_exits_1_when_audit_challenged` FAILED |
| B (classification) | `run_vendor_classif_red.txt` contient `FAILED` | `run_vendor_classif_green.txt` contient `PASSED` | `classify()` retourne toujours `"other_error"` → test `test_classify_429_returns_vendor_refusal` FAILED |

---

## Gate mécanique

La commande suivante doit retourner `VERDICT: ACCEPT` avec tous les contrôles au vert avant que l'Évaluateur rédige son verdict de fond :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
```

**Avertissement de lecture :** le gate juge la *forme* du lot, pas sa substance. Un lot peut obtenir `VERDICT: ACCEPT` du gate tout en recevant `FAIL` de l'Évaluateur de fond.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| `pr_audit_guard.py check` retourne 0 quand un audit CHALLENGED cible la PR | SC1 non satisfaite — le constat P0-1 se reproduit |
| `pr_audit_guard.py check` retourne 1 quand aucun audit ne cible la PR | SC1 non satisfaite — faux positif qui bloquera toutes les PRs ordinaires |
| Création d'un fichier `.github/workflows/pipeline-*.yml` | SC2 non satisfaite — contrat explicite du brief et du brief 006 |
| Job `audit-check` absent de `audit-guard.yml` | SC2 non satisfaite |
| `vendor_refusal.classify()` retourne toujours `"other_error"` quelle que soit l'entrée | SC3 non satisfaite — le cas 429 passe sans détection |
| `harness/pipeline/vendor-refusal-state.jsonl` absent du suivi git | SC3 non satisfaite — état lisible seulement dans le log du run, pas depuis un clone |
| Exit 0 du workflow quand le repli Codex échoue ou est absent, sans revue produite | SC4 non satisfaite — succès simulé (jamais acceptable) |
| Modification ou suppression des gardes existants de `pipeline-challenge.yml` (kill-switch, mode, `ci_budget_guard`, plafond) | SC4 non satisfaite — sécurité du pipeline dégradée |
| Compteur SC1 mesuré sur une inbox vide ou un ledger vide | Mode d'échec n°6 — échantillon vide passe en silence |
| Condensé SHA256 recopié en valeur hexadécimale dans un test ou un document | Hard-won rule 12 |
| Modification de `brief.md`, `eval-rubric.md` ou `verdict.md` par le Générateur | Principe 7 — « celui qui produit ne prononce pas la recevabilité » |
| Commit, push ou création de branche par le Générateur | Interdiction explicite — violée deux fois sur ce dépôt (briefs 009, 010) |

---

_Amendement — 2026-08-13T11:22:00Z : (1) commande du compteur `audit_guard_job_count` en SC2 remplacée par un one-liner stdlib-only (suppression de `import yaml`, non disponible dans le venv du dépôt). (2) « `github.head_ref` et `github.sha` » corrigé en « `github.head_ref` et `github.event.pull_request.head.sha` » en SC2 point 4 — `github.sha` est le SHA du commit de fusion simulé sur un événement `pull_request`, pas le commit de tête. Aucun changement de fond._
