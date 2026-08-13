---
audit_id:                CURSOR-70380c6-pr83-etat-refus-sans-lecteur
auditor:                 cursor-cloud
target_branch:           master
target_commit:           70380c6faf08d1c45fc654cca1acfbe39b5c8507
created_at:              2026-08-13T13:13:32Z
audit_type:              pr-critique
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #83 — état final (`70380c6`) : le refus fournisseur est consigné là où personne ne le lira

Audit produit par `cursor-auditor` (contrat :
`architecture/agents/cursor-auditor.md`), selon les six lentilles de
`architecture/review-guidelines.md`. Cet audit **ne décide rien** et
**n'instruit rien** : il propose. La décision reste au propriétaire et à la
boucle (`architecture/README.md`, ADR-0005 / ADR-0006). Les trois drapeaux
d'autorisation du frontmatter sont à `false`.

PR auditée : <https://github.com/PLiagre/ForgeHistory/pull/83>
« Brief 014 : le contre-audit comme porte observable, le refus fournisseur
comme état explicite avec repli (pipeline) ».

## 0. Ce qu'il faut retenir en cinq phrases

1. **Le P0 de l'audit précédent est réellement fermé** : l'injection shell
   `${{ github.head_ref }}` a été corrigée par bloc `env:` (`150fd14`),
   `actionlint` est vert, et j'ai vérifié qu'aucune interpolation ne subsiste
   dans un bloc `run:` de `audit-guard.yml`. L'Évaluateur a re-vérifié le
   correctif après son propre verdict — c'est la bonne discipline.
2. **La porte que ce lot ajoute est verte sur la PR qui la contient, alors
   qu'un audit non adjugé la cible pour de vrai.** Ce n'est plus une
   démonstration de laboratoire : le job `audit-check` de la PR #83 a imprimé
   « Aucun audit ne cible cette PR » à `13:05:45`, dix minutes après le dépôt
   de `CURSOR-bd34ded-pr83-…` qui critique un commit de cette même PR.
3. **L'« état explicite » du volet B n'a ni lecteur ni chemin vers `master`.**
   Aucun code du dépôt ne lit `vendor-refusal-state.jsonl` ; son chemin n'est
   pas dans l'allowlist du merge-bot ; et la branche dédiée qui le porte est
   poussée sans qu'aucune PR ne soit ouverte. C'est un journal de run, pas un
   état.
4. **La preuve centrale annonce sept chemins et n'en mesure que quatre** :
   j'ai rejoué la table du test, les libellés 1/2 et 4/5/6 sont des contextes
   *identiques*. « CLI absent », « transcript vide » et « transcript
   illisible » ne sont pas testés : ils sont renommés.
5. Et le simulateur qui porte cette preuve **ne sait pas ce qu'est une revue
   produite** : l'étape de publication y réussit toujours, par construction.
   Le chemin réel « l'invocation sort en 0 sans écrire de revue » finit donc
   vert, sans publication et sans escalade.

Aucun de ces constats n'est un reproche au travail de fond : la boucle à trois
rôles a réellement mordu trois fois sur ce lot, et les suites sont vertes. Le
reproche porte sur l'écart entre ce que les preuves mesurent et ce que les
commentaires du lot affirment.

## Provenance et périmètre audité

| Élément | Valeur |
|---|---|
| PR | #83, `OPEN`, non-brouillon |
| Branche de tête | `forge/014-pipeline-contre-audit-porte-e180` |
| SHA de tête audité | `70380c6faf08d1c45fc654cca1acfbe39b5c8507` |
| Base de comparaison | `da536505c804e3ecc937bab16e3747e09c81968f` (merge-base avec `master`) |
| Volume | 22 fichiers, +4710 / −26 |
| Commits | 9 (`d1ed1f6` … `70380c6`) |
| Audit précédent de cette PR | `CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre` (SHA `bd34ded`) |

Nouveauté depuis l'audit précédent : deux commits, `150fd14` (correctif
`actionlint`) et `70380c6` (note de re-vérification de l'Évaluateur). Cet
audit est un **fichier neuf** ; `architecture/inbox/` est append-only et
l'audit précédent n'est ni modifié ni supprimé.

Méthode : lecture seule sur un arbre de travail détaché au SHA audité
(`git worktree add /tmp/pr83 70380c6f`), commandes rejouées avec
`/workspace/.venv/bin/python`, et lecture des journaux de CI réels via
`gh run view --log`.

## 1. Lentille 1 — l'intention est lisible, et le diff y répond à moitié

Le brief `harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md` dit
deux choses, chacune issue de l'audit `CURSOR-a600532-fusion-sans-contre-audit`
(points P0-1 et P1-1) :

- **Volet A** : rendre le contre-audit *observable* comme une porte de PR — on
  doit pouvoir voir qu'une PR est fusionnée alors qu'un audit la ciblant n'a
  pas été adjugé.
- **Volet B** : faire du refus fournisseur (HTTP 429) un **état explicite**
  plutôt qu'un job rouge muet, avec un repli.

La description de PR est honnête sur une limite (« la porte est observable,
pas contraignante — protection de branche indisponible sur ce plan ») et sur
les incidents de processus (branches parasites). C'est appréciable et rare.

Mais l'intention n'est pas « écrire un script qui sort en 1 » : c'est
« qu'on voie ». Sur les deux volets, ce qui est livré s'arrête juste avant
l'effet visible :

- Volet A : le script sort bien en 1 quand on lui donne le bon SHA, mais la CI
  ne lui donne jamais ce SHA (§ 3).
- Volet B : l'état est bien écrit, mais dans un fichier que rien ne lit et qui
  ne peut pas atteindre `master` (§ 4).

C'est le motif que S1 nomme *false green* : le mécanisme existe, la case est
verte, et l'observation que le brief demandait n'a pas lieu.

## 2. Lentille 3 — portes mécaniques : la CI du commit audité est verte

Classification de la CI au SHA `70380c6faf08d1c45fc654cca1acfbe39b5c8507`
(sortie de `gh pr checks 83` et `gh run list --commit`) :

| Workflow | Job | Conclusion |
|---|---|---|
| `security` | `actionlint` | **pass** (10 s / 12 s, deux runs `push` + `pull_request`) |
| `security` | `gitleaks` | pass |
| `audit-guard` | `schema` | pass |
| `audit-guard` | `audit-check` | **pass** — c'est le constat P0-1 ci-dessous |
| `audit-guard` | `cursor-scope` | skipping (branche non-`cursor/*`, attendu) |
| `harness-ci` | `tests` | pass (23 s / 25 s) |
| `harness-ci` | `sim-tests` | pass |
| `harness-ci` | `f0-demo` | pass |
| `pipeline-audit` | `invoke-cursor-auditor` | pass |
| `merge-bot` | `check-and-automerge` | skipping (branche `forge/*`, hors `bot_branches`) |
| `hermes-observer` | `Reconcile local Hermes state` | pending au moment de l'audit |

**Aucun job rouge.** Le `actionlint` rouge relevé par l'audit précédent (run
[31701797271](https://github.com/PLiagre/ForgeHistory/actions/runs/31701797271))
est éteint : runs
[31703192082](https://github.com/PLiagre/ForgeHistory/actions/runs/31703192082)
et
[31703196709](https://github.com/PLiagre/ForgeHistory/actions/runs/31703196709)
verts.

Portes rejouées localement au SHA audité (sorties collées en § 10) :

- `harness/tests/` → **348 passed, 16 skipped** (les skips sont Unity/Linux,
  attendus).
- `sim/tests/` → **35 passed**.
- `harness/verdict_audit.py …/014-…` → **VERDICT: ACCEPT**, dix contrôles au
  vert.

Le jugement qui suit ne porte donc pas sur ce que les machines couvrent déjà :
il porte sur ce que ces vertes ne disent pas.

## 3. P0-1 — la porte `audit-check` est verte sur la PR #83 alors qu'un audit non adjugé la cible réellement

*Ce point confirme le P0-2 de `CURSOR-bd34ded`. Je ne le réémets pas par
répétition : l'audit précédent le démontrait par un A/B construit ; ici, le
cas s'est produit **en production**, sur cette PR, et c'est un élément
nouveau.*

### La mesure

Le job `audit-check` du run
[31703196778](https://github.com/PLiagre/ForgeHistory/actions/runs/31703196778/job/94457131459),
sur le commit de tête `70380c6` :

```
PR_HEAD_BRANCH: forge/014-pipeline-contre-audit-porte-e180
PR_HEAD_COMMIT: 70380c6faf08d1c45fc654cca1acfbe39b5c8507
Aucun audit ne cible cette PR — contrôle vert.
```

Or, à cet instant, `master` contient
`architecture/inbox/CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre.md`,
dont le frontmatter dit :

```
target_commit:           bd34dedbb713863d7f9bfa8f9341975aa01291d6
status:                  PROPOSED
```

`bd34ded` est le **7ᵉ commit de cette PR** (`git log --oneline
da53650..70380c6` le liste), et l'audit n'a **aucune ligne au ledger** — il
n'est donc pas adjugé. La porte censée dire « une décision est due avant la
fusion » dit exactement le contraire.

### La cause, en une ligne de code

`harness/pipeline/pr_audit_guard.py`, lignes 79-87 :

```python
def _targets_pr(frontmatter: dict, head_branch: str, head_commit: str) -> bool:
    target_branch = frontmatter.get("target_branch", "")
    if target_branch and target_branch == head_branch:
        return True
    target_commit = frontmatter.get("target_commit", "")
    if target_commit and head_commit and target_commit[:7] == head_commit[:7]:
        return True
    return False
```

L'appariement se fait sur **le seul SHA de tête**. Un audit de PR est par
construction écrit sur un commit qui n'est plus la tête au moment où il
arrive sur `master` : le pipeline ré-audite à chaque poussée, et l'audit
voyage par une PR `cursor/*` qui met quelques minutes à fusionner. La fenêtre
où la porte peut mordre est donc l'intervalle entre deux poussées — ici, elle
a duré moins de dix minutes et s'est refermée avant même que l'audit ne soit
lisible par la CI.

### A/B rejoué localement, sur les données réelles de production

Le script n'existe que dans l'arbre de la PR ; les deux commandes tournent donc
depuis un arbre de travail détaché au SHA audité (`/tmp/pr83`) et lisent
l'inbox + le ledger d'un second arbre détaché sur `master` (`/tmp/master`, au
commit `1601290`). Deux arbres figés : l'A/B reste reproductible à
l'identique.

```
$ cd /tmp/pr83   # arbre au SHA 70380c6
$ /workspace/.venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/014-pipeline-contre-audit-porte-e180 \
    --head-commit 70380c6faf08d1c45fc654cca1acfbe39b5c8507 \
    --inbox /tmp/master/architecture/inbox \
    --ledger /tmp/master/architecture/audit-ledger.jsonl
Aucun audit ne cible cette PR — contrôle vert.
exit=0

$ /workspace/.venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/014-pipeline-contre-audit-porte-e180 \
    --head-commit bd34ded \
    --inbox /tmp/master/architecture/inbox \
    --ledger /tmp/master/architecture/audit-ledger.jsonl
ERREUR : audits ciblant cette PR, non adjugés :
  CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre: PROPOSED (aucune ligne au ledger)
1 audit(s) non adjugé(s) cible(nt) cette PR — la décision doit être prise avant la fusion (contrôle rouge).
exit=1
```

### La démonstration s'est refermée sur moi pendant la rédaction

En écrivant cet audit, je l'ai posé dans `architecture/inbox/` avec
`target_commit: 70380c6…`. J'ai alors rejoué la première commande contre mon
inbox de travail : elle **rougit**, en nommant cet audit-ci.

C'est la mécanique du défaut, vue de l'autre côté. La porte devient juste au
moment exact où l'audit atteint l'arbre lu par la CI — c'est-à-dire *après* la
fusion de la PR d'audit, alors que la PR #83, elle, aura très probablement
reçu une nouvelle poussée entre-temps (elle en a reçu deux en dix minutes
aujourd'hui). La porte n'est donc pas « parfois fausse » : elle est en retard
d'un cycle, structurellement, et le retard est précisément la durée pendant
laquelle la décision serait utile.

Même arbre, même inbox, même ledger : seul le SHA change. Le mécanisme
fonctionne ; ce qu'on lui demande de comparer est faux.

### Ce qui n'est pas reproché

Le lot annonce lui-même que la porte est « observable, pas contraignante ».
Le reproche ne porte pas sur l'absence de blocage — il porte sur le fait que
l'*observation* elle-même est fausse : un lecteur qui regarde la case verte
en conclut « aucun audit en attente », ce qui est l'inverse de la réalité.
Une porte qui se trompe est pire qu'une porte absente, parce qu'elle produit
une affirmation.

**Sévérité : P0** (bloque la fusion), reconduite de l'audit précédent avec
une preuve de production.

## 4. P1-1 — l'« état explicite » du refus fournisseur n'a ni lecteur ni chemin vers `master`

Le brief demande de transformer le 429 en **état**. Un état se distingue d'un
journal par une propriété simple : quelqu'un le lit. J'ai cherché ce
quelqu'un.

### Aucun lecteur

```
$ rg -n --hidden "vendor-refusal-state|vendor_refusal" \
    --glob '!harness/queue/briefs/**' --glob '!architecture/inbox/**' \
    --glob '!harness/pipeline/vendor_refusal.py' \
    --glob '!harness/tests/test_vendor_refusal.py' \
    --glob '!harness/pipeline/proof_red/**' --glob '!.git/**' .
```

(`--hidden` est nécessaire : sans lui, `rg` saute `.github/` et on manquerait
le workflow.)

Résultat : 21 occurrences dans `.github/workflows/pipeline-challenge.yml`, qui
sont **toutes des écritures ou des conditions** — `classify`, `log_refusal`,
`mark_fallback_actor`, `mark_fallback_attempted`, `git add`, `git diff
--quiet`, et des tests de la sortie `classification`. Aucune n'ouvre le
fichier pour en tirer une conclusion. Hors de là, il ne reste que des chaînes
de caractères dans `test_pipeline_challenge_paths.py`.

En particulier `harness/pipeline/orchestrator.py`,
`.github/workflows/pipeline-failure-escalate.yml`, `hermes/dashboard.py` et
`harness/pipeline/ci_budget_guard.py` **ne le lisent jamais**. Aucun test
n'assure qu'un lecteur existe.

### Aucun chemin vers `master`

Deux obstacles indépendants, tous deux mécaniques :

1. **Le chemin n'est pas dans l'allowlist du merge-bot.**
   `.github/merge-bot.yaml` :

   ```yaml
   allow_paths:
     - "architecture/inbox/**"
     - "architecture/reviews/**"
     - "harness/queue/briefs/**/feedback/**"
   ```

   `harness/pipeline/vendor-refusal-state.jsonl` n'y est pas. Toute PR de bot
   qui le contiendrait serait refusée par `merge-bot.yml` (ligne 50-55 :
   « PR touches path(s) outside … allow_paths — refusing auto-merge »).

2. **Aucune PR n'est ouverte pour la branche d'état.**
   `pipeline-challenge.yml`, étape « Commit état du refus fournisseur »,
   lignes 290-298 : la branche `forge-bot/vendor-refusal-<audit>-<run>` est
   créée, commitée, poussée — puis `git checkout -`. Il n'y a **pas** de
   `gh pr create`, contrairement à l'étape de publication de revue (ligne
   332). La branche existe et personne ne la regarde.

À quoi s'ajoute le défaut déjà relevé par l'audit précédent (P1-2) : après
`git checkout -`, l'arbre de travail revient à son état d'origine, si bien que
le `git add harness/pipeline/vendor-refusal-state.jsonl` de l'étape de
publication (ligne 329) n'a plus rien à ajouter. Les deux voies possibles vers
`master` sont donc fermées, chacune pour une raison différente.

### Pourquoi c'est un défaut de fond

C'est exactement ce que S6 formule pour les budgets de jetons : *« un compteur
local n'est pas une limite »* — un état qui vit dans le processus (ici : dans
un run, sur une branche orpheline) n'est pas un état du système. Et S5 rappelle
que l'intérêt d'observer les 429 est de pouvoir **agir** dessus (taux de refus,
état du disjoncteur, bascule de fournisseur) ; sans lecteur, l'écriture ne
produit aucune décision.

Concrètement : après onze refus 429 mesurés le jour même, rien dans le dépôt
ne permet de répondre à « combien d'audits attendent un contre-audit à cause
du plafond fournisseur ? ». C'est la question que le brief voulait rendre
répondable.

**Sévérité : P1.**

## 5. P1-2 — la preuve « sept chemins » n'en mesure que quatre

`harness/tests/test_pipeline_challenge_paths.py` est présenté comme la preuve
mécanique du volet B (B4) : sept chemins du job, conclusion vérifiée pour
chacun. J'ai rejoué la table.

```
$ .venv/bin/python -  # charge SEVEN_PATHS et déduplique les contextes
libellés : 7
contextes distincts : 4
   {"check_available": true, "classification": "vendor_refusal", "codex_succeeds": false, "invoke_outcome": "failure"}
      -> ['1:429 sans identifiant Codex', '2:429, identifiants présents, CLI absent']
   {"check_available": true, "classification": "vendor_refusal", "codex_succeeds": true,  "invoke_outcome": "failure"}
      -> ['3:429, Codex réussit']
   {"check_available": true, "classification": "other_error",    "codex_succeeds": false, "invoke_outcome": "failure"}
      -> ['4:erreur statut 500', '5:CLI qui plante, transcript vide', '6:transcript illisible, revue produite']
   {"check_available": true, "classification": "success",        "codex_succeeds": false, "invoke_outcome": "success"}
      -> ['7:succès normal']
```

Les chemins 1 et 2 sont le **même** dictionnaire d'entrée. Les chemins 4, 5 et
6 aussi. Le test exécute donc quatre situations et en compte sept.

Ce n'est pas une chicane de comptage : les trois libellés fusionnés décrivent
des causes que le lecteur croit couvertes et qui ne le sont pas.

- « CLI absent » (chemin 2) devrait distinguer *identifiants présents mais
  binaire `codex` introuvable* de *identifiants absents* (chemin 1) — or
  l'étape de repli sort en 1 par deux branches différentes du script (lignes
  230-234 pour les identifiants, ligne 264-267 pour l'échec de la commande).
  Le test ne distingue pas laquelle a été prise.
- « transcript vide » et « transcript illisible » (chemins 5 et 6) ne sont pas
  des entrées du simulateur : ils sont écrasés en `classification:
  other_error`. Le libellé 6 dit même « revue produite », alors que la notion
  de revue produite n'existe pas dans le simulateur (§ 6).

S1 appelle *false green* une suite verte qui ne garde plus le comportement
qu'elle prétend garder ; S2 recense la même famille sous « assertions
affaiblies » et « mocks circulaires » — détectables déterministement, sans
LLM. Ici, personne n'a triché : on a simplement compté des étiquettes au lieu
de compter des états.

**Sévérité : P1** — la preuve reste utile (elle rougit bien si l'étape B3 est
retirée, la paire rouge/verte est committée), mais sa portée annoncée est plus
de deux fois supérieure à sa portée réelle.

## 6. P1-3 — le simulateur ne sait pas ce qu'est une revue produite, et un chemin réel finit vert sans revue

`pipeline-challenge.yml`, commentaire de l'étape de publication, lignes
300-305 :

> « Aucun chemin ne peut finir vert avec une revue produite non publiée. »

Le test censé étayer cette phrase modélise l'étape ainsi
(`test_pipeline_challenge_paths.py`, `_step_outcome`) :

```python
    if "publish" in name or "pull request" in name:
        return "success"  # exit 0 même sans revue (warning interne)
```

L'étape de publication **réussit toujours** dans le simulateur, et il n'existe
aucune variable de contexte « une revue a été écrite ». Le simulateur ne peut
donc ni confirmer ni infirmer l'affirmation : il ne mesure pas la grandeur
dont elle parle. Il ne modélise pas davantage l'échec de `git push` ni celui
de `gh pr create`.

Et l'affirmation, lue littéralement, laisse passer le cas le plus probable en
pratique. Dans le workflow :

- étape « Publish », lignes 320-323 : si `git status --porcelain --
  architecture/reviews` est vide → `::warning::` puis **`exit 0`** ;
- étape B3, ligne 346 : `if: steps.invoke.outcome == 'failure' && …` — elle ne
  s'exécute **que** si l'invocation a échoué.

Donc : **invocation qui sort en 0 sans écrire de fichier de revue → job vert,
aucune revue, aucune PR, aucune escalade.** `pipeline-failure-escalate.yml`
ne se déclenche que sur `conclusion == 'failure'` (ligne 42) : le silence est
complet.

Ce cas n'est pas théorique dans ce dépôt : la branche de repli a dû élargir
son motif de recherche de fichier (« Motif élargi pour couvrir les noms
produits par Codex », ligne 250-252), preuve que le nom du fichier de revue
n'est pas garanti ; et l'inbox contient déjà
`CURSOR-827d54e-contre-audit-paye-jamais-publie.md`, c'est-à-dire un incident
enregistré de cette famille exacte.

S3 le dit d'une phrase : *« tout simuler ne prouve rien »* — la parade
recommandée est de rejouer des traces réelles enregistrées plutôt que de
réimplémenter le moteur. Ce lot a les traces réelles sous la main (les onze
runs 429 du jour) et ne les rejoue pas.

**Sévérité : P1.**

## 7. Constats P2

### P2-1 — `classify()` ignore le signal qui arrive en premier dans le vrai transcript

J'ai extrait les lignes réelles du run
[31694643198](https://github.com/PLiagre/ForgeHistory/actions/runs/31694643198)
(le cas 429 que ce lot vise). Le flux contient **deux** lignes utiles, dans cet
ordre :

```jsonc
// 1re ligne : événement assistant
{"type":"assistant","message":{…"content":[{"type":"text","text":"You've hit your org's monthly spend limit …"}]},
 "error":"rate_limit","is_api_error_message":true,"request_id":"req_011CdzejDGZop22HsGwb6Nqk"}
// 2e ligne : événement result
{"is_error":true,…,"terminal_reason":"api_error","subtype":"success","api_error_status":429,
 "result":"You've hit your org's monthly spend limit …","type":"result"}
```

Bonne nouvelle, à dire clairement : sur ce transcript réel, `classify()`
retourne bien `vendor_refusal` — la seconde ligne porte `is_error: true` et
`api_error_status: 429`. Le volet B fonctionne sur le cas mesuré.

La fragilité est ailleurs : `vendor_refusal.classify()` (lignes 60-67) ne
regarde **que** les champs de haut niveau `is_error` / `api_error_status`, donc
uniquement l'événement `result` final. Les marqueurs `"error":"rate_limit"` et
`"is_api_error_message":true`, présents dès la première ligne, sont ignorés.
Si le CLI meurt avant d'émettre l'événement `result` (délai du runner,
annulation, `tee` interrompu, OOM), le transcript contient la preuve du refus
fournisseur et la classification retourne `other_error` : pas de consignation,
pas de repli, et l'étape B3 rend le job rouge sans état. Le signal est dans les
données, disponible, et volontairement non utilisé.

Les fixtures de `test_vendor_refusal.py` sont toutes **mono-ligne** et
synthétiques (lignes 39-100) : aucune ne reproduit la forme réelle à deux
lignes. **Sévérité : P2.**

### P2-2 — l'écriture de l'état n'est pas concurrente-sûre, et chaque run crée sa propre divergence

`log_refusal` ajoute en fin de fichier ; `mark_fallback_attempted` (lignes
104-133) fait une **lecture-modification-réécriture complète** du fichier. Deux
runs `pipeline-challenge` simultanés (deux audits poussés coup sur coup, ce qui
arrive : le pipeline déclenche sur chaque `architecture/inbox/*.md`) perdent une
ligne. À cela s'ajoute une branche par run
(`forge-bot/vendor-refusal-<audit>-<run>`), chacune portant sa propre version
mono-ligne du même fichier : si ces branches devenaient un jour fusionnables,
elles entreraient en conflit systématique sur la dernière ligne.

S6 traite précisément ce cas pour les budgets : l'état partagé doit vivre dans
un magasin partagé, pas dans une copie par processus. **Sévérité : P2.**

### P2-3 — le contre-poids du `continue-on-error` est écrit par le même acteur que le `continue-on-error`

Le correctif B1 de l'itération 2 consiste à poser `continue-on-error: true`
sur l'étape d'invocation (ligne 156) pour que les étapes suivantes soient
atteignables, puis à rétablir la rougeur du job par une étape terminale (B3,
ligne 338-349).

Deux remarques, sans procès d'intention :

1. `continue-on-error: true` sur une étape est précisément le motif que les
   détecteurs déterministes de « tests joués » classent en *CI weakening* —
   « CI workflow disarmed so failures stop blocking » (S2, table des règles).
   Le motif n'est pas interdit ; il exige un contre-poids **vérifié par un
   autre acteur que celui qui l'a posé**, ce qui est la règle du harnais
   elle-même.
2. Ce contre-poids existe et sa chaîne d'escalade tient : j'ai vérifié que
   `pipeline-failure-escalate.yml` surveille bien `pipeline-challenge` par
   `workflow_run` (lignes 27-32) et ne se déclenche que sur
   `conclusion == 'failure'` (ligne 42). Mais la seule preuve que B3 rougit
   au bon moment est le simulateur du § 6, écrit par le Générateur, dont
   l'espace d'entrée est celui du § 5.

**Sévérité : P2** — le motif est acceptable, la preuve qui l'accompagne est
plus faible que ce qu'elle annonce.

### P2-4 — taille du lot (constat reconduit, sans élément nouveau)

22 fichiers, +4710 / −26, deux volets sans dépendance technique l'un envers
l'autre. La lentille 5 fixe le seuil de relecture honnête à ~5 fichiers ou
quelques centaines de lignes ; on est au-delà d'un ordre de grandeur. Point
déjà porté par `CURSOR-bd34ded` (P2-1) ; je le mentionne pour mémoire, sans le
re-développer.

## 8. Constats P3 (information)

- **`VENDOR_REFUSAL_STATE` est une constante morte.**
  `vendor_refusal.py:30` calcule le chemin canonique du fichier d'état ;
  `rg -n "VENDOR_REFUSAL_STATE"` ne trouve **que** cette ligne. Le workflow
  écrit le chemin en dur (`"harness/pipeline/vendor-refusal-state.jsonl"`,
  lignes 212, 259). Deux sources pour un même chemin, dont une inutilisée.
- **`log_refusal` inscrit `api_error_status: 429` en constante** (ligne 96)
  plutôt que de relire la valeur du transcript. C'est conforme au brief
  (`brief.md:125`), donc ce n'est pas un écart du Générateur ; c'est une limite
  de conception à connaître : le fichier d'état enregistre une constante, pas
  une mesure. Un futur code fournisseur (529, quota) serait consigné « 429 ».
- **Le gate ne vérifie le suivi git que de 2 des 17 fichiers déclarés.**
  Sortie de `verdict_audit.py` : « all 2 in-brief declared files are tracked;
  15 declared outside the brief dir, not checked ». Limite du gate, pas du
  lot — mais elle réduit la portée du `ACCEPT` sur un lot dont l'essentiel du
  code vit hors du dossier du brief.
- **Ce qui tient, et qui mérite d'être dit** : le correctif `150fd14` est
  exactement ce qu'il annonce (j'ai vérifié qu'il ne reste aucune
  interpolation `${{ }}` dans un bloc `run:` de `audit-guard.yml` : les trois
  occurrences sont dans des blocs `env:`) ; l'Évaluateur a re-vérifié après
  son propre verdict au lieu de le laisser périmer (`70380c6`) ; la boucle a
  produit deux REJECT motivés avant l'ACCEPT ; les paires rouge/verte sont
  committées sous `proof_red/` ; le câblage événementiel de l'escalade est
  juste ; et les 34 tests du lot passent, dans une suite globale de 348 +
  35 verte.

## 9. Risques par sévérité

| # | Sév. | Risque | Preuve |
|---|---|---|---|
| P0-1 | **P0** | La porte `audit-check` affirme « aucun audit ne cible cette PR » alors qu'un audit `PROPOSED` cible un commit de la PR ; l'appariement ne regarde que le SHA de tête | job [94457131459](https://github.com/PLiagre/ForgeHistory/actions/runs/31703196778/job/94457131459) ; `pr_audit_guard.py:79-87` ; A/B rejoué § 3 |
| P1-1 | **P1** | L'état du refus fournisseur n'a aucun lecteur, son chemin est hors allowlist du merge-bot, et sa branche est poussée sans PR | `rg` § 4 ; `.github/merge-bot.yaml` allow_paths ; `pipeline-challenge.yml:290-298` |
| P1-2 | **P1** | « Sept chemins » = 4 contextes distincts ; « CLI absent », « transcript vide », « transcript illisible » sont des libellés, pas des cas | mesure rejouée § 5 sur `SEVEN_PATHS` |
| P1-3 | **P1** | Le simulateur ne modélise pas « revue produite » (publish toujours vert) ; une invocation à 0 sans revue finit verte, sans publication ni escalade | `_step_outcome` (publish → success) ; `pipeline-challenge.yml:320-323` et `:346` ; `pipeline-failure-escalate.yml:42` |
| P2-1 | P2 | `classify()` ignore `error: rate_limit` / `is_api_error_message` et dépend de la seule ligne `result` finale ; fixtures toutes mono-ligne | transcript réel du run [31694643198](https://github.com/PLiagre/ForgeHistory/actions/runs/31694643198) ; `vendor_refusal.py:60-67` |
| P2-2 | P2 | Écriture d'état non concurrente-sûre (réécriture complète) + une branche divergente par run | `vendor_refusal.py:104-133` ; `pipeline-challenge.yml:290` |
| P2-3 | P2 | `continue-on-error: true` (motif « CI weakening ») dont l'unique preuve de contre-poids est le simulateur du même acteur | `pipeline-challenge.yml:156` ; S2 ; § 5 |
| P2-4 | P2 | 22 fichiers / +4710 lignes, deux volets indépendants (reconduit, sans élément nouveau) | diffstat § provenance |
| P3 | P3 | Constante morte, `api_error_status` en dur, gate qui ne vérifie le suivi que de 2/17 fichiers déclarés ; et la liste de ce qui tient | § 8 |

## 10. Commandes rejouées (récapitulatif)

```bash
# état de la PR et de la CI au SHA audité
gh pr view 83 --repo PLiagre/ForgeHistory --json headRefOid,changedFiles,additions,deletions
#   -> 70380c6faf08d1c45fc654cca1acfbe39b5c8507 ; 22 fichiers ; +4710 / -26
gh pr checks 83 --repo PLiagre/ForgeHistory
#   -> actionlint pass, audit-check pass, tests pass, sim-tests pass, gitleaks pass, schema pass…
gh run view 31703196778 --repo PLiagre/ForgeHistory --log --job 94457131459
#   -> "Aucun audit ne cible cette PR — contrôle vert."

# arbre de travail en lecture seule au SHA audité
git worktree add /tmp/pr83 70380c6faf08d1c45fc654cca1acfbe39b5c8507
git merge-base origin/master 70380c6f   # -> da536505c804e3ecc937bab16e3747e09c81968f

# portes mécaniques
.venv/bin/python -m pytest harness/tests/ -q   # -> 348 passed, 16 skipped
.venv/bin/python -m pytest sim/tests/ -q       # -> 35 passed
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
#   -> VERDICT: ACCEPT (10 contrôles PASS)

# P0-1 : la porte, sur les données réelles de production
# (script depuis /tmp/pr83 = arbre au SHA audité ; inbox+ledger depuis
#  /tmp/master = arbre détaché sur master au commit 1601290)
git worktree add /tmp/master origin/master
/workspace/.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-pipeline-contre-audit-porte-e180 \
  --head-commit 70380c6faf08d1c45fc654cca1acfbe39b5c8507 \
  --inbox /tmp/master/architecture/inbox \
  --ledger /tmp/master/architecture/audit-ledger.jsonl   # -> exit 0 (vert)
/workspace/.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-pipeline-contre-audit-porte-e180 \
  --head-commit bd34ded \
  --inbox /tmp/master/architecture/inbox \
  --ledger /tmp/master/architecture/audit-ledger.jsonl   # -> exit 1 (rouge)
# et : l'inbox de l'arbre de la PR ne contient pas l'audit de master
ls /tmp/pr83/architecture/inbox/ | grep -c CURSOR-bd34ded   # -> 0

# P1-1 : chercher un lecteur de l'état (--hidden, sinon .github/ est sauté)
rg -n --hidden "vendor-refusal-state|vendor_refusal" \
  --glob '!harness/queue/briefs/**' --glob '!architecture/inbox/**' \
  --glob '!harness/pipeline/vendor_refusal.py' \
  --glob '!harness/tests/test_vendor_refusal.py' \
  --glob '!harness/pipeline/proof_red/**' --glob '!.git/**' .
#   -> 21 occurrences, toutes dans le workflow qui écrit ; aucun lecteur

# P1-2 : déduplication des sept chemins
#   -> libellés : 7 ; contextes distincts : 4

# P2-1 : forme réelle du transcript 429
gh run view 31694643198 --repo PLiagre/ForgeHistory --log | grep "api_error_status"
#   -> ligne assistant ("error":"rate_limit") PUIS ligne result ("api_error_status":429)

# vérification du correctif du P0 précédent
rg -n '\$\{\{' .github/workflows/audit-guard.yml
#   -> 3 occurrences, toutes dans des blocs env: (aucune dans un run:)
```

## 11. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

Ces trois propositions ne sont **pas** des instructions et ne préautorisent
rien : seul un brief sous `harness/queue/briefs/` instruit
(`CLAUDE.md` › Single Source of Instruction), et seul le propriétaire convertit
un audit en brief.

1. **Faire mordre la porte sur la bonne fenêtre.** Apparier un audit à une PR
   par **l'ensemble des commits de la PR** (et non le seul SHA de tête), et
   traiter les audits encore portés par une PR `cursor/*` ouverte comme
   ciblants. Preuve attendue : un test rouge-puis-vert qui reproduit le cas
   réel du § 3 (audit sur `bd34ded`, tête `70380c6`) et exige `exit 1`.
2. **Donner un lecteur à l'état du refus fournisseur.** Un chemin d'arrivée sur
   `master` (allowlist ou job autorisé), un consommateur réel (escalade,
   `hermes/DASHBOARD.md`, ou une commande `status`), et un test qui lit l'état
   au lieu de vérifier seulement qu'il a été écrit. Compteur naturel : nombre
   d'audits en attente pour cause de refus fournisseur, lisible en une
   commande.
3. **Rendre honnête la preuve des chemins de workflow.** Imposer la
   déduplication des contextes de la table (un libellé = un état d'entrée
   distinct), ajouter la variable « revue produite », et exiger qu'un chemin
   « invocation à 0 sans revue » soit **rouge**. Si possible, rejouer un
   transcript réel enregistré plutôt que de re-simuler le moteur GitHub
   Actions (S3).

## 12. Points à porter au propriétaire (gouvernance — hors compétence d'un audit)

- La porte `audit-check` est « observable, pas contraignante » faute de
  protection de branche sur le plan courant. Tant que ce point n'est pas
  tranché, une porte fausse (§ 3) est indistinguable d'une porte vraie pour
  qui lit la case verte.
- Les sources externes convergent sur un point qui dépasse ce lot :
  la configuration de pipeline est la catégorie de changement que S4 classe
  « Critical — Human-only », et ce lot modifie deux workflows. Le denylist du
  merge-bot le reflète déjà (`.github/workflows/**` jamais auto-fusionné) ;
  reste la question du reviewer humain effectif.

## Sources externes

Recherche web du 2026-08-13 sur les trois thèmes imposés par le contrat
(« autonomous AI dev pipeline », « agent orchestration CI », « token budget
LLM agents »).

| # | Source | Publication | Consulté le | Ce qu'elle étaye |
|---|---|---|---|---|
| S1 | DEV Community — *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green* — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> | 2026 | 2026-08-13 | « A green suite that no longer guards the bug is a false green » ; exiger un pack de preuves (commandes, résultats, périmètre non couvert) — § 1, § 5 |
| S2 | Veredicto (JoniMartin27) — *The CI check that catches when an AI agent games your tests* — <https://github.com/JoniMartin27/veredicto> | 2026 | 2026-08-13 | Table de règles déterministes : `ci-weakening` (`continue-on-error: true`), `weakened-assertions`, `circular-mocks` — § 5, § 7 P2-3 |
| S3 | agentverify (simukappu) — *pytest plugin for deterministic testing of AI agent actions* — <https://github.com/simukappu/agentverify> | 2026 | 2026-08-13 | « Mocking everything proves nothing » ; enregistrer puis rejouer des traces réelles plutôt que réimplémenter le moteur — § 6 |
| S4 | buildmvpfast — *AI Agents in CI/CD: Productivity, Risk & Governance (2026)* — <https://www.buildmvpfast.com/blog/ai-agents-ci-cd-pipeline-devops-automation-2026> | 2026 | 2026-08-13 | Tableau d'autonomie par type de changement : « Pipeline config — Critical — Human-only » — § 12 |
| S5 | TrueFoundry — *Rate Limiting AI Agents: Preventing LLM API Exhaustion with a 3-Layer Gateway* — <https://www.truefoundry.com/blog/rate-limiting-ai-agents-preventing-llm-api-exhaustion> | 2026 | 2026-08-13 | Chaîne de repli déclarative et observabilité du taux de 429 / de l'état du disjoncteur comme condition pour agir — § 4 |
| S6 | Learn Cloud Native — *Agentgateway rate limiting for agents* — <https://learncloudnative.com/blog/2026-07-16-agentgateway-rate-limiting> | 2026-07-16 | 2026-08-13 | « A local limit isn't a limit on the agent » : un état par copie de processus n'est pas un état du système ; le budget doit être partagé — § 4, § 7 P2-2 |

## Budget de cet audit

≤ 60 appels d'outils (contrat `cursor-auditor` › Budget max appels) : environ
35 appels, un seul commit audité, aucune passe scindée.
