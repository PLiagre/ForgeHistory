---
audit_id:                CURSOR-70380c6-pr83-porte-desarmee-par-la-derive-de-sha
auditor:                 cursor-cloud
target_branch:           forge/014-pipeline-contre-audit-porte-e180
target_commit:           70380c6faf08d1c45fc654cca1acfbe39b5c8507
created_at:              2026-08-13T13:10:00Z
audit_type:              pr-critique
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #83 — commit de tête `70380c6`

<https://github.com/PLiagre/ForgeHistory/pull/83>

Cet audit est une **proposition**. Il n'instruit rien, n'autorise rien, ne
décide rien : la décision appartient à la boucle (`architecture/README.md`,
ADR-0005/0006). Les trois drapeaux `*_authorized` du frontmatter valent
`false`.

---

## 0. Ce qu'il faut retenir en trois phrases

1. Le défaut de sécurité signalé au commit précédent (`bd34ded`) est
   **réellement fermé** : `github.head_ref` passe désormais par un bloc
   `env:`, et `actionlint` est vert sur ce commit. La CI de `70380c6` est
   entièrement verte.
2. Mais le commit qui a fermé ce défaut a, du même geste, **désarmé la porte
   que la PR installe**. Ce n'est plus une prédiction : au commit audité,
   l'audit `CURSOR-bd34ded-pr83-…` est **présent dans l'arbre que la porte
   lit**, il n'a **aucune ligne au registre**, et la porte répond quand même
   « contrôle vert » (exit 0), confirmé par `audit-check success` en CI. La PR
   qui installe la porte échappe à sa propre porte.
3. Deux mécanismes distincts expliquent que ce vert ne puisse pas être
   détecté par le lot lui-même : la garde **échoue en vert** quand son entrée
   est absente (une inbox introuvable et une inbox lue sans correspondance
   produisent le même message et le même code de sortie), et **les quatre
   compteurs de la rubrique sont mesurés sur des inbox synthétiques** dont la
   configuration n'existe pas dans le corpus réel.

---

## Provenance et périmètre audité

| élément | valeur |
|---|---|
| PR | #83, `forge/014-pipeline-contre-audit-porte-e180` → `master` |
| SHA de tête audité | `70380c6faf08d1c45fc654cca1acfbe39b5c8507` |
| base (merge-base) | `da536505c804e3ecc937bab16e3747e09c81968f` |
| diff cumulé | 22 fichiers, +4696 / −26 |
| état à l'audit | ouverte, non brouillon, `mergeStateStatus: UNSTABLE` |
| commits | 9 |

**Audit précédent de la même PR.** `CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre`
(commit `bd34ded`) est déjà dans `architecture/inbox/`. Le présent fichier est
un **nouvel** audit d'un **nouveau** commit — l'inbox est append-only, rien
n'est modifié ni réécrit. Les constats de l'audit précédent qui persistent à
l'identique sont **reportés en une ligne chacune** (§ 6), avec la preuve
qu'ils persistent ; ils ne sont pas re-plaidés. Les § 3 à 5 ne contiennent que
des constats **neufs ou nouvellement mesurés**.

Deux commits sont arrivés pendant la rédaction de cet audit
(`bd34ded` → `150fd14` → `70380c6`). Toutes les mesures ci-dessous ont été
rejouées au SHA final. Le diff `150fd14..70380c6` ne touche qu'un fichier :

```
$ git diff --stat 150fd14..70380c6
 .../briefs/014-pipeline-contre-audit-porte/verdict.md | 14 ++++++++++++++
 1 file changed, 14 insertions(+)
```

Toutes les commandes ont été rejouées sur des arbres de travail positionnés
sur `refs/pull/83/head` et `refs/pull/83/merge`, avec `.venv/bin/python`. Les
sorties sont collées telles quelles.

---

## 1. Lentille 3 — Portes mécaniques : classification de la CI du commit audité

```
$ gh api "repos/PLiagre/ForgeHistory/commits/70380c6/check-runs" --jq '…' | sort -u
actionlint               success
audit-check              success
audit-check              skipped      (événement push — conforme au `if:`)
check-and-automerge      skipped
cursor-scope             skipped
f0-demo                  success
gitleaks                 success
invoke-cursor-auditor    success
schema                   success
sim-tests                success
tests                    success
Reconcile local Hermes state   queued
```

**La CI du commit audité est verte.** Aucun job en échec. C'est une
amélioration nette par rapport à `bd34ded`, où le workflow `security` était
rouge sur ses deux runs.

`audit-check success` est ici un signal à retenir pour le § 3 : la porte a
tourné, elle a répondu vert, et elle avait tort.

---

## 2. Ce qui est réellement fermé depuis `bd34ded` (crédit)

### Le défaut d'injection est corrigé, et corrigé de la bonne façon

```
$ git diff bd34ded..150fd14 -- .github/workflows/audit-guard.yml
+        env:
+          PR_HEAD_BRANCH: ${{ github.head_ref }}
+          PR_HEAD_COMMIT: ${{ github.event.pull_request.head.sha }}
         run: |
           set -euo pipefail
           python harness/pipeline/pr_audit_guard.py check \
-            --head-branch "${{ github.head_ref }}" \
-            --head-commit "${{ github.event.pull_request.head.sha }}"
+            --head-branch "$PR_HEAD_BRANCH" \
+            --head-commit "$PR_HEAD_COMMIT"
```

C'est exactement le motif que le job voisin `cursor-scope` utilisait déjà : la
valeur non fiable ne traverse plus le texte du script, elle n'est plus qu'une
variable shell citée. `actionlint` est vert sur les deux runs de `70380c6`. Le
constat P0-1 de l'audit précédent est **fermé** ; il n'est pas repris ci-après.

### Le verdict a été re-vérifié après le correctif de CI

Le commit `70380c6` ajoute une note datée au `verdict.md` : l'Évaluateur
constate que le correctif est limité à ce qu'il annonce, rejoue le gate et les
suites, et maintient PASS. C'est la bonne réaction à un commit de CI arrivé
après un verdict, et cela évite ici l'instance du motif « verdict périmé à la
fusion » — motif par ailleurs **déjà retenu** par
`DECISION-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion` (APPROVED, 11
points), donc non re-plaidé dans cet audit.

### Le gate mécanique est rejoué et ACCEPT au SHA de tête

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
[PASS] files_declared_exist … [PASS] verdict_is_not_self_authored: generator/evaluator
       actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur
VERDICT: ACCEPT
```

```
$ .venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py \
    harness/tests/test_vendor_refusal.py \
    harness/tests/test_pipeline_challenge_paths.py -q
34 passed in 0.13s
```

---

## 3. P0-1 — La porte est verte alors qu'un audit non adjugé de cette PR est dans l'arbre qu'elle lit

> **Report du P0-2 de `CURSOR-bd34ded`, avec un élément neuf décisif.** L'audit
> précédent démontrait le défaut dans un bac à sable, et devait argumenter que
> l'audit « n'est pas encore dans l'arbre ». Il l'est maintenant. Le défaut
> passe de *prédit* à *mesuré in situ, sur la PR elle-même*.

### Les trois faits, dans l'ordre

**(1) L'audit visant cette PR est bien dans l'arbre que la porte lit.** Le job
`audit-check` fait `actions/checkout` sur l'événement `pull_request`, donc lit
le merge-ref — qui contient `master`, où l'audit précédent a été fusionné :

```
$ git worktree add /tmp/pr83n refs/pull/83/merge
$ ls architecture/inbox/ | grep pr83
CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre.md
```

**(2) Cet audit n'est pas adjugé.** Aucune ligne au registre :

```
$ python -c "import audit_ledger; print(audit_ledger.current_state_for(
    'CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre',
    audit_ledger.LEDGER_PATH))"
etat: None
```

**(3) La porte répond vert.** Au SHA de tête réel, sur cet arbre :

```
$ .venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/014-pipeline-contre-audit-porte-e180 \
    --head-commit 70380c6faf08d1c45fc654cca1acfbe39b5c8507
Aucun audit ne cible cette PR — contrôle vert.
exit=0
```

et la CI le confirme : `audit-check success` sur `70380c6`.

### La démonstration A/B, sur le seul paramètre du SHA

Même arbre, même audit, même registre — seul le SHA de tête change :

```
$ … check --head-branch forge/014-… --head-commit 70380c6…   # tête actuelle
Aucun audit ne cible cette PR — contrôle vert.
exit=0

$ … check --head-branch forge/014-… --head-commit bd34ded…   # SHA visé par l'audit
ERREUR : audits ciblant cette PR, non adjugés :
  CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre: PROPOSED (aucune ligne au ledger)
1 audit(s) non adjugé(s) cible(nt) cette PR — la décision doit être prise avant la fusion (contrôle rouge).
exit=1
```

La porte **savait** rougir. Elle a cessé de rougir au moment exact où la PR a
reçu le commit qui répondait à l'audit.

### Pourquoi les deux règles d'appariement ne peuvent pas tenir

`harness/pipeline/pr_audit_guard.py`, `_targets_pr` (lignes 79-87) :

```python
    target_branch = frontmatter.get("target_branch", "")
    if target_branch and target_branch == head_branch:
        return True
    target_commit = frontmatter.get("target_commit", "")
    if target_commit and head_commit and target_commit[:7] == head_commit[:7]:
        return True
```

**Règle « branche » : inapplicable au corpus réel.** Mesure sur l'inbox du
merge-ref (41 audits) :

```
$ grep -h "^target_branch:" architecture/inbox/*.md | sort | uniq -c | sort -rn
     30 target_branch: master
      1 target_branch: forge/hermes-decision-adr-0011-c2dd
      1 target_branch: forge/013-sim-tick-nourrit-une-fois-ddda
      … (11 audits portent une vraie branche de tête)
```

**30 audits sur 41 portent `target_branch: master`** — et l'audit de la PR #83
en fait partie. Or `github.head_ref` d'une PR ne vaut jamais `master`. Pour
ces 30 audits, la règle « branche » ne peut structurellement pas se
déclencher ; il ne reste que la règle « commit ».

**Règle « commit » : effacée par le premier commit suivant.** Elle compare le
`target_commit` figé de l'audit au SHA de tête **courant**. Le cycle normal
d'une critique est : audit déposé sur le SHA n → l'auteur corrige → SHA n+1.
Cette PR l'a fait **deux fois pendant la rédaction du présent audit**
(`bd34ded` → `150fd14` → `70380c6`). L'appariement est donc adossé à la seule
valeur qui change à chaque geste de correction.

Conséquence circulaire, et c'est le cœur du constat : **plus la boucle
fonctionne, moins la porte fonctionne.** Un audit ignoré laisse la porte
rouge ; un audit pris au sérieux la rend verte.

### Ce qui n'est pas reproché

La porte n'est pas inutile : elle mord sur un audit `post-merge` visant un SHA
figé de `master`, ou sur une PR qui ne reçoit plus de commit. Son câblage
événementiel est juste (`skipped` sur `push`, `success` sur `pull_request`).
Le brief documente honnêtement une **autre** limite (« observable, pas
contraignante » — pas de protection de branche) : celle-là dit que le rouge
n'empêche pas la fusion, pas que le rouge ne survient pas.

**Sévérité : P0** (bloque la fusion). L'affirmation centrale du volet A est
démentie sur son cas nominal, par une mesure prise sur la PR elle-même.

Preuves : `pr_audit_guard.py:79-87` ; les trois commandes ci-dessus ;
`audit-check success` sur `70380c6` ; distribution des 41 `target_branch`.

---

## 4. P1-1 (neuf) — La garde échoue en vert : une entrée illisible et une entrée sans correspondance donnent le même signal

### Le fait

`pr_audit_guard.py`, `check` (lignes 109-112) :

```python
    audit_files = sorted(inbox_path.glob("*.md")) if inbox_path.exists() else []
    if not audit_files:
        print("Aucun audit ne cible cette PR — contrôle vert.")
        return 0
```

Une inbox **absente** produit exactement le message et le code de sortie d'une
inbox **lue en entier sans correspondance**. Démonstration, avec un audit qui
*devrait* rougir (le SHA visé est celui de l'audit) :

```
$ … check --head-branch forge/014-… --head-commit bd34ded… --inbox architecture/Inbox
Aucun audit ne cible cette PR — contrôle vert.
exit=0

$ mkdir -p /tmp/vide83
$ … check --head-branch x --head-commit bd34ded… --inbox /tmp/vide83
Aucun audit ne cible cette PR — contrôle vert.
exit=0
```

Une majuscule dans le chemin suffit. Le même appel avec le vrai chemin
retourne `exit=1` (§ 3). La garde ne dispose donc pas du verdict « je n'ai pas
pu regarder » : elle ne sait dire que « rien à signaler ».

### Pourquoi c'est un défaut de fond

C'est le motif *fail-open* : le sens de défaillance d'une porte de sécurité
doit être choisi, et une porte qui ne peut pas évaluer sa règle doit bloquer,
pas passer (S1, S2). Dans le cas présent, le chemin de l'inbox est un
**défaut de code** (`DEFAULT_INBOX`, dérivé de l'emplacement du fichier
source). Il suffit qu'un déplacement de `harness/pipeline/` ou un renommage de
`architecture/inbox/` survienne pour que la porte devienne verte en
permanence — silencieusement, et sans qu'aucun test ne le voie :

```
$ grep -nE "inbox_absente|not exists|missing|inexistant" harness/tests/test_pr_audit_guard.py
(aucun résultat)
```

Le scénario 1 des tests (`test_…_inbox_vide`) crée bien un répertoire vide,
puis vérifie `exit == 0` : le test **fige le comportement fail-open comme
attendu**, au lieu de le distinguer du cas « pas d'audit ciblant ».

**Sévérité : P1** (à corriger avant fusion sauf dérogation). Le défaut ne se
manifeste pas aujourd'hui — le chemin est bon — mais il fait de la porte un
mécanisme dont la panne est indistinguable du succès, ce qui est précisément
le défaut que le volet B de ce même lot corrige pour Claude/429 (un refus
fournisseur ne doit pas ressembler à un succès).

Preuves : `pr_audit_guard.py:109-112` ; les deux exécutions ci-dessus ;
absence de test ; S1, S2.

---

## 5. P1-2 (neuf) — Les quatre compteurs de la rubrique sont mesurés sur une configuration qui n'existe pas dans le corpus réel

### Le fait

Les quatre compteurs déclarés au manifeste pour le volet A
(`audits_ciblant_pr`, `audits_non_adjuges_ciblant_pr`,
`code_sortie_guard_pr_avec_audit_non_adjuge`,
`code_sortie_guard_pr_sans_audit`) sont tous mesurés par des tests qui
construisent une inbox synthétique dans `tmp_path`. Le constructeur de
fixture, et l'appel qui s'ensuit :

```python
def _make_audit(inbox, audit_id, target_branch="", target_commit="") -> None:   # ligne 32

    _make_audit(inbox, audit_id, target_branch="target-branch")                 # ligne 233
    code = pr_audit_guard.check(head_branch="target-branch",
                                head_commit="zzzzzzz", …)                        # lignes 240-245
```

Chaque compteur est donc mesuré avec `target_branch` **égal à la branche de
tête** et un `head_commit` volontairement non appariable (`"zzzzzzz"`).
Autrement dit : les quatre chiffres du manifeste n'exercent **que la règle
« branche »** — celle qui, sur le corpus réel, ne peut pas se déclencher pour
30 audits sur 41 (§ 3).

La règle « commit », seule opérante en production, est exercée par un unique
test (scénario 7, ligne 188) — et il l'exerce avec un SHA de tête **identique**
au `target_commit` de l'audit :

```python
    _make_audit(inbox, audit_id, target_commit=full_commit)   # "abc1234feedbeef…"
    …  head_commit="abc1234feedbeef"
```

Aucun test ne couvre le cas « même PR, un commit de plus » — c'est-à-dire le
seul scénario dans lequel la porte est censée servir, et celui qui échoue en
production.

### Pourquoi c'est un défaut de fond

Un test dont le vert est atteignable sans exercer la propriété revendiquée est
pire qu'un test rouge, parce qu'il se lit comme une couverture (S3). Ici la
conséquence est mesurable : les compteurs du manifeste passent le gate
(`VERDICT: ACCEPT`, § 2) et la rubrique valide SC2, alors que la propriété
énoncée par le corps de la PR — « code de sortie non nul si un audit non
adjugé cible la PR » — est fausse sur la PR qui la livre. Le lot ne dispose
d'**aucun** chiffre mesuré sur les 41 audits réels ; le corpus qui a fait
échouer la porte n'est jamais entré dans une mesure.

C'est la racine commune du § 3 et du § 5 : la rubrique demandait de vérifier
que les **bons arguments** sont passés au script (`eval-rubric.md:93`), jamais
que la porte **devient rouge** quand un audit non adjugé existe.

**Sévérité : P1.**

Preuves : `test_pr_audit_guard.py:32`, `:188-198`, `:233-245` ;
`manifest.json` (les quatre compteurs) ; distribution des `target_branch`
du § 3 ; S3.

---

## 6. Constats reportés de `CURSOR-bd34ded`, vérifiés persistants (non re-plaidés)

Le diff `bd34ded..70380c6` ne touche que `audit-guard.yml` (bloc `env:`),
`generator-log.md` (2 lignes) et `verdict.md` (14 lignes). Tous les fichiers
concernés par les constats ci-dessous sont donc **inchangés à l'octet**. Ils
sont reportés pour que la décision porte sur l'état réel du commit à
fusionner ; aucun élément nouveau n'est ajouté, et une décision propriétaire
sur l'audit `bd34ded` les couvrirait intégralement.

| report | sévérité | vérification au SHA audité |
|---|---|---|
| Le repli Codex ne peut aboutir : le CLI n'est pas installé, `~/.codex/auth.json` n'est pas amorcé | P1 | `grep -nE "npm install" pipeline-challenge.yml` → une seule ligne, `@anthropic-ai/claude-code` uniquement ; `codex exec` appelé ligne 241 |
| L'état du refus fournisseur reste sur une branche jamais fusionnée ; le `git add` de l'étape de publication est un no-op après `git checkout -` | P1 | `git checkout -` toujours ligne 298, `git add …vendor-refusal-state.jsonl` toujours ligne 329 |
| La preuve du volet B est un simulateur d'Actions du même auteur, dont un des sept chemins (`codex_succeeds=True`) est irréalisable | P1 | `test_pipeline_challenge_paths.py` inchangé |
| 22 fichiers / +4696 lignes, deux volets sans dépendance technique dans un même lot | P2 | `git diff --stat da53650...70380c6` → `22 files changed, 4696 insertions(+), 26 deletions(-)` |
| Le lecteur de frontmatter casse sur le format documenté par `architecture/README.md` (commentaires en ligne non retirés) | P2 | `_parse_frontmatter` inchangé (`pr_audit_guard.py:59-71`) |
| `audit_ledger` importé par `sys.path.insert` au niveau module | P2 | `pr_audit_guard.py:40-41` inchangé |

---

## 7. Ce qui tient (P3 — information)

- **CI entièrement verte** sur `70380c6`, y compris `actionlint` : la
  régression de sécurité de `bd34ded` est fermée (§ 2).
- **34 tests du lot verts en 0,13 s**, sans réseau ; gate `VERDICT: ACCEPT`
  rejoué (§ 2).
- **La boucle à trois rôles a réellement mordu** : deux REJECT documentés
  avant le PASS, sur des motifs techniques justes (étapes inatteignables à
  cause du `success()` implicite ; un échec non-429 rendait le job vert sans
  revue). La paire rouge/verte est committée dans `harness/pipeline/proof_red/`.
- **L'Évaluateur a re-vérifié après un commit de CI post-verdict**, au lieu de
  laisser le PASS pendre sur un commit dépassé (§ 2).
- **Aucun fichier `pipeline-*.yml` nouveau** : la contrainte du brief est
  tenue (`pipeline_workflows_count` = 5 avant comme après).
- **`AUDIT_STALE` compté comme non adjugé** reste un choix défendable : un
  audit périmé n'est pas une décision.
- **Les incidents de processus sont déclarés, pas dissimulés** (branches
  parasites `cursor/*`). Motif déjà consigné comme Non-Goal différé
  (`CURSOR-3b47ffe`, points 1 et 7) — le répéter serait du bruit.

---

## 8. Risques par sévérité

| # | sévérité | constat | preuve |
|---|---|---|---|
| P0-1 | **P0** | La porte `audit-check` est verte alors qu'un audit non adjugé de cette PR est dans l'arbre qu'elle lit ; l'appariement est effacé par le commit qui répond à l'audit | in situ § 3 : audit présent au merge-ref, `current_state_for` → `None`, garde `exit=0`, `audit-check success` sur `70380c6` ; A/B sur le seul SHA ; 30/41 `target_branch: master` |
| P1-1 | **P1** | La garde échoue en vert sur entrée illisible : inbox absente ou mal orthographiée = même message et même code de sortie qu'une inbox lue sans correspondance ; aucun test ne couvre le cas | `pr_audit_guard.py:109-112` ; deux exécutions § 4 ; S1, S2 |
| P1-2 | **P1** | Les quatre compteurs de la rubrique n'exercent que la règle « branche », inopérante sur 30 audits réels sur 41 ; la dérive de SHA n'est couverte par aucun test | `test_pr_audit_guard.py:32`, `:188-198`, `:233-245` ; `manifest.json` ; S3 |
| — | P1 | Trois reports du volet B (repli Codex inerte, état jamais fusionné, simulateur du même auteur) | § 6, vérifiés persistants |
| — | P2 | Trois reports (taille du lot, lecteur de frontmatter, `sys.path.insert`) | § 6, vérifiés persistants |
| P3 | P3 | CI verte, 34 tests verts, gate ACCEPT, boucle réellement mordante, re-vérification du verdict | § 2, § 7 |

---

## 9. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

Ces propositions n'autorisent rien : elles n'entrent en vigueur que si le
propriétaire les convertit en briefs (`architecture/README.md`, cycle de vie).

**B-1 — Apparier l'audit et la PR sur une clé qui ne bouge pas (ferme P0-1).**
Remplacer l'appariement par SHA de tête courant par une clé stable dans le
temps : le numéro de PR, ou la branche de tête portée explicitement par le
frontmatter de l'audit ; et lire l'inbox depuis `master` plutôt que depuis le
merge-ref de la PR auditée. Preuve exigée : un test qui **rougit** si le
contrôle passe au vert après l'ajout d'un commit à une PR dont un audit non
adjugé est ouvert — c'est-à-dire la démonstration A/B du § 3 retournée en
garde. Un compteur mesuré sur le corpus réel (`architecture/inbox/`, 41
fichiers), pas sur `tmp_path`.

**B-2 — Donner à la garde un troisième verdict : « je n'ai pas pu regarder »
(ferme P1-1, et une partie de P1-2).** Distinguer trois états — audits lus et
aucun ne cible / audits lus et l'un cible sans décision / entrée illisible
— et faire échouer le troisième au lieu de le confondre avec le premier :
inbox absente, vide alors qu'elle ne devrait pas l'être, ou frontmatter que le
lecteur n'a pas su analyser. Preuve exigée : un test qui rougit si le chemin
d'inbox est faux, et un test sur le format documenté par
`architecture/README.md` (commentaires en ligne).

**B-3 — Trancher le repli fournisseur : l'exécuter ou le retirer (ferme les
reports P1 du volet B).** Report inchangé de l'audit `bd34ded` : soit installer
`@openai/codex` et amorcer `~/.codex/auth.json` dans `pipeline-challenge.yml`
comme le fait déjà `pipeline-forge-run.yml`, avec un `codex --version` de
fumée ; soit retirer l'étape de repli et assumer que le seul livrable du 429
est l'état consigné. Dans les deux cas, corriger le chemin de l'état pour
qu'il atteigne `master` (une PR, ou l'ajout au commit de revue **avant** tout
`git checkout -`).

---

## 10. Points à porter au propriétaire (gouvernance — hors compétence d'un audit)

- **Le correctif a précédé la décision.** Le point P0-1 de l'audit `bd34ded` a
  été implémenté au commit `150fd14` alors que cet audit reste `PROPOSED`, sans
  contre-audit, sans décision, sans ligne au registre (`current_state_for` →
  `None`, § 3). L'effet correcteur a eu lieu ; la traçabilité de la boucle,
  non. Et c'est exactement l'état que la porte du volet A est censée rendre
  rouge avant fusion — d'où le § 3. Question ouverte : qui inscrit la ligne au
  registre quand un audit est traité « au vol » par l'acteur audité ?
- **Cet audit portera lui aussi une cible périssable.** Son `target_commit`
  vaut `70380c6` ; le prochain commit sur la PR le rendra non appariable, comme
  il l'a fait pour `bd34ded`. Le `target_branch` de ce fichier a donc été
  volontairement fixé à la **branche de tête** de la PR, et non à `master`
  comme le fait la majorité du corpus : c'est la seule des deux règles
  d'appariement qui survive à un nouveau commit tant que B-1 n'est pas
  tranché. Le point de gouvernance est que la convention actuelle
  (`target_branch: master` pour une critique de PR) est ce qui rend la porte
  aveugle ; elle ne se règle pas dans le code seul.

---

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | DEV Community — *Every Safety Gate Has a Failure Direction* (une porte qui ne peut pas évaluer sa règle doit bloquer, non passer ; tester ensemble le crash-fail-closed et le silent-fail-open) — <https://dev.to/jeremy_longshore/every-safety-gate-has-a-failure-direction-jd8> | 2026-08-13 |
| S2 | vibeagentmaking — *Why We Hold Every Failed Verify Now: The Fail-Open Gate That Shipped a Broken Build* (« I could not look » est un troisième verdict, distinct de « pass ») — <https://vibeagentmaking.com/blog/why-we-hold-every-failed-verify/> | 2026-08-13 |
| S3 | DEV Community — *Green CI Proves Nothing: Why Your Tests Gate Zero Calls* (un test dont le vert est atteignable sans exercer la propriété doit échouer fermé ; assertion `gated_count > 0`) — <https://dev.to/jeremy_longshore/green-ci-proves-nothing-why-your-tests-gate-zero-calls-1po9> | 2026-08-13 |
| S4 | Shiplight AI — *CI/CD for Agent-Written Code* (un contrôle ne bloque que s'il est requis par la protection de branche, et doit passer **sur le dernier SHA** de la PR ; `merge_group` pour la file de fusion) — <https://www.shiplight.ai/blog/ci-cd-for-agent-written-code> | 2026-08-13 |
| S5 | Augment Code — *CI/CD for AI Agents: How to Integrate Agent Orchestration into Your Pipeline* (si un agent modifie la configuration de CI, les contrôles requis appariés par nom de job cassent silencieusement ; d'où un contrôle agrégé stable) — <https://www.augmentcode.com/guides/cicd-ai-agents-pipeline-integration> | 2026-08-13 |
| S6 | Pondero — *CI for agents: gating merges on eval scores without blocking every PR* (2026) — la rubrique qui encode la spec de la veille : « eval est périmée » plutôt que « le code est faux » — <https://pondero.ai/enterprise/guides/ci-for-agents-eval-gating-2026/> | 2026-08-13 |
| S7 | tianpan.co — *Backpressure Patterns for LLM Pipelines: Why Exponential Backoff Isn't Enough* (2026-04-15) — une chaîne de repli doit se terminer sur une ressource que l'on contrôle ; sinon la dépendance est déplacée, pas supprimée — <https://tianpan.co/blog/2026-04-15-backpressure-llm-pipelines> | 2026-08-13 |
| S8 | llmtest.io — *How to handle LLM rate limits: 4 production-tested patterns* (bascule fournisseur sur 429 soutenu ; état par fournisseur) — <https://llmtest.io/blog/llm-rate-limits-production-patterns> | 2026-08-13 |

Rattachement des sources aux constats :

- **S1, S2** fondent **P1-1** : le sens de défaillance d'une porte est un choix
  d'architecture, et « je n'ai pas pu vérifier » ne doit jamais produire le
  même signal que « vérifié, rien à signaler ».
- **S3** fonde **P1-2** : un compteur mesuré sur une configuration où la
  propriété ne peut pas échouer certifie une couverture qui n'existe pas.
- **S4** fonde **P0-1** : un contrôle doit être vert **sur le dernier SHA**
  pour valoir preuve à la fusion. Ce lot fait l'inverse — il fait dépendre le
  *contenu* de la vérification du dernier SHA, de sorte que le contrôle
  s'affaiblit à mesure que la PR avance.
- **S5** fonde la lecture du § 10 : quand un agent touche à la configuration de
  CI, les portes appariées par nom cassent en silence ; ici la porte ne casse
  pas, elle se contente de ne plus rien apparier — même classe de panne
  silencieuse.
- **S6** fonde **P1-2** et le § 10 : la rubrique a vérifié la forme des
  arguments et non l'effet de la porte, et c'est la rubrique — pas
  l'Évaluateur — qu'il faut corriger.
- **S7, S8** fondent les reports du volet B (§ 6) : une chaîne de repli dont le
  second maillon n'est pas installé n'est pas une chaîne de repli ; elle
  produit le même échec qu'avant, avec plus de code.

---

## Commandes rejouées (récapitulatif)

```bash
# état de la PR et classification de la CI du commit audité
gh pr view 83 -R PLiagre/ForgeHistory --json headRefOid,mergeStateStatus,commits
gh api "repos/PLiagre/ForgeHistory/commits/70380c6/check-runs?per_page=50" \
  --jq '.check_runs[] | "\(.name)\t\(.conclusion // .status)"' | sort -u

# arbres de travail : tête de PR et merge-ref
git fetch origin 'refs/pull/83/head:pr83b' 'refs/pull/83/merge:pr83mergeb'
git worktree add /tmp/pr83n pr83mergeb
git diff --stat 150fd14..70380c6
git diff bd34ded..150fd14 -- .github/workflows/audit-guard.yml
git diff --stat da53650...70380c6

# gate et tests au SHA de tête
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
.venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py \
  harness/tests/test_vendor_refusal.py \
  harness/tests/test_pipeline_challenge_paths.py -q          # 34 passed in 0.13s

# § 3 — la porte, in situ, sur la PR #83 elle-même
ls architecture/inbox/ | grep pr83
python -c "import audit_ledger; print(audit_ledger.current_state_for(
  'CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre', audit_ledger.LEDGER_PATH))"
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-pipeline-contre-audit-porte-e180 \
  --head-commit 70380c6faf08d1c45fc654cca1acfbe39b5c8507      # exit 0  (vert)
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-pipeline-contre-audit-porte-e180 \
  --head-commit bd34dedbb713863d7f9bfa8f9341975aa01291d6      # exit 1  (rouge)
grep -h "^target_branch:" architecture/inbox/*.md | sort | uniq -c | sort -rn

# § 4 — fail-open sur entrée illisible
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-… --head-commit bd34ded… --inbox architecture/Inbox   # exit 0
mkdir -p /tmp/vide83 && .venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch x --head-commit bd34ded… --inbox /tmp/vide83                    # exit 0

# § 6 — reports vérifiés persistants
grep -nE "npm install|codex|CODEX" .github/workflows/pipeline-challenge.yml
grep -nE "checkout -|git add|git push|git commit" .github/workflows/pipeline-challenge.yml
```
