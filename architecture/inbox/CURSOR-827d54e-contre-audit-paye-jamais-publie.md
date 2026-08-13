---
audit_id:                CURSOR-827d54e-contre-audit-paye-jamais-publie
auditor:                 cursor-cloud
target_branch:           master
target_commit:           827d54ec2b0ee3b49d1b1a1992d64137759f32a6
created_at:              2026-08-13T11:05:00Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit post-fusion du commit `827d54e` (PR #65)

Audit du commit de fusion `827d54ec2b0ee3b49d1b1a1992d64137759f32a6`
(« Merge pull request #65 »), fusionné sur `master` le 2026-08-13 à
10:47:51 UTC par `PLiagre`. Parents : `e034f07` (tête de `master`) et
`4c45718` (tête de la branche `forge/boucle-audits-post-pr60-ddda`).
Diff apporté par la fusion : **11 fichiers, +843, −0**.

Méthode : `architecture/review-guidelines.md` (six lentilles, sévérités
P0–P3, une preuve citée par constat). Rôle : auditeur en **lecture seule**.
Cet audit **n'instruit rien**, n'autorise rien et ne vaut pas décision
(`architecture/README.md`) : seuls le contre-audit puis la décision sont
compétents.

Traçabilité du déclencheur : le run `pipeline-audit`
[31692686803](https://github.com/PLiagre/ForgeHistory/actions/runs/31692686803)
a lancé l'agent `bc-e43ea71a-9cb4-42a8-a590-1d15cb044efe` à 10:48:12 UTC ;
c'est l'agent qui écrit ces lignes. Toutes les mesures ont été rejouées
dans l'arbre de travail positionné sur `827d54e`, **sans aucune écriture
hors de ce fichier**. Les sorties sont collées au § 6.

## Ce que cet audit ne refait pas

Cette PR avait déjà été critiquée **avant** fusion par l'audit
`CURSOR-4c45718-pr65-ledger-recupere-a-la-main` (10 constats : 4 P1, 3 P2,
3 P3). Je ne re-plaide aucun de ces dix constats — les répéter sans élément
neuf serait le « rubber-stamping inverse » que le guide interdit. Je porte
uniquement sur :

1. ce que **la fusion elle-même** a produit ;
2. ce que l'audit pré-fusion **ne pouvait pas savoir** : que le contre-audit
   de ses propres constats existait déjà, qu'il a coûté 2,43 $, et qu'il
   n'a jamais atteint `master` ;
3. l'état mesurable du dépôt **au commit fusionné**.

## 0. Synthèse

| # | Sévérité | Constat en une phrase |
|---|---|---|
| 1 | **P0** | Le contre-audit de l'audit de cette PR **existe**, il a été écrit à 09:03:43 UTC et facturé **2,4342555 $**. `gh pr create` l'a refusé une seconde plus tard (`Resource not accessible by personal access token (createPullRequest)`), l'étape a dégradé en `::warning::` vert, et la fusion a eu lieu **1 h 44 plus tard** sans lui. Sept branches `forge-bot/review-*` sont poussées ; le dépôt n'a **aucune** PR ouverte. |
| 2 | **P1** | La fusion n'a retouché **aucun** des 11 fichiers de la PR : les 4 constats P1 pré-fusion entrent tels quels dans le tronc, et les deux plus lourds y deviennent irréversibles — la ligne de registre au comptage faux (14/4/6/4 pour une revue qui porte 9 `CONFIRMED` et 1 `PARTIAL` sur 10 points) et les 8 événements `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` sans le moindre pointeur de preuve. |
| 3 | **P1** | Les deux briefs produits par la conversion (013, 014) sont des **graines vides** : 6 marqueurs `<<TODO (planificateur)>>` chacun, rubrique idem, gate `REJECT`. Les 27 points retenus par la décision automatique n'instruisent donc rien, et aucun registre ni workflow ne mesure le temps passé dans cet état. |
| 4 | **P2** | La dépense est mesurée à l'exécution puis **jetée** : `ci_budget_guard record` écrit dans le workspace du runner, l'étape de publication ne commite que `architecture/reviews`. Au tronc, `harness/pipeline/ci-budget-ledger.jsonl` fait **1 octet** alors que les transcripts de la seule journée portent **7,2771804 $**. |
| 5 | **P2** | Arriéré de la boucle au commit fusionné : **15 audits sur 31 n'ont aucune ligne** au registre (dont les 4 du jour), 15 sont `AUDIT_PROPOSED`, 13 revues existent pour 31 audits. Déjà signalé (P2-1 de `a600532`, retenu, converti en brief 014) : je ne le re-plaide pas, je mesure sa **croissance** (12/25 → 15/31). |
| 6 | **P3** | 54 runs `hermes-observer` sur les 100 derniers sont **en file d'attente**, le plus ancien depuis 08:56:46 UTC — soit près de deux heures avant la fusion. Le runner auto-hébergé n'en a drainé aucun et rien ne signale la file. |
| 7 | **P3** | `harness/harness_audit.py` rend **20/24** sur un clone propre (2 FAIL), alors qu'`AGENTS.md:50` annonce 23/24 avec un seul FAIL connu : le fichier de preuve `run_demo.log` est git-ignoré (`.gitignore:7 *.log`) et n'a jamais été commité, donc le point de 3 ne peut passer que sur une machine où quelqu'un a déjà lancé la démo. |

**Un P0.** Il ne porte pas sur le contenu fusionné — celui-ci est mécaniquement
propre (§ 4) — mais sur le fait que la chaîne à quatre acteurs (ADR-0010) a
produit et payé sa relecture, puis l'a perdue, et que la fusion s'est faite
sans elle. C'est la **quatrième** occurrence en une journée, et la première
où la relecture perdue concernait la PR fusionnée elle-même.

## 1. Le fait central, en cinq horodatages

| Heure (UTC) | Événement | Preuve |
|---|---|---|
| 08:52:00 | L'audit pré-fusion de la PR #65 est daté : 4 constats P1. | `created_at` de `architecture/inbox/CURSOR-4c45718-...md` |
| 08:56:18 | Cet audit arrive sur `master` (commit `fe8443c`, PR #68) ; `pipeline-challenge` démarre. | run [31684301091](https://github.com/PLiagre/ForgeHistory/actions/runs/31684301091) |
| **09:03:43** | **Le contre-audit est écrit** : 58 tours, 7 constats `CONFIRMED`, 3 `PARTIAL`, 1 `NEEDS_OWNER`. Coût facturé : **2,4342555 $**. | `total_cost_usd` du transcript, § 6.1 |
| 09:03:46 | `gh pr create` est refusé (`Resource not accessible by personal access token`). L'étape émet un `::warning::` et le job **réussit**. | log du run, § 6.1 |
| **10:47:51** | La PR #65 est fusionnée. Le contre-audit dort toujours sur `forge-bot/review-CURSOR-4c45718-...-31684301091`. | `gh pr view 65`, `git ls-remote`, § 6.2 |

Entre l'écriture de la relecture et la fusion : **1 h 44**. Rien, dans le
dépôt ni dans l'interface GitHub, ne montrait qu'une relecture complète
existait.

## 2. Portes mécaniques d'abord (lentille 3)

Classification de la CI du commit audité (§ 6.3) : **5 workflows `push`
terminés, tous verts**, 1 en file.

| état | workflows |
|---|---|
| verts | `harness-ci`, `security`, `audit-guard`, `hermes-dashboard`, `pipeline-audit` |
| en file | `hermes-observer` (runner auto-hébergé, constat 6) |
| non déclenché | `pipeline-orchestrate` — **correctement** : son filtre ne vise que `architecture/reviews/*.md`, or la fusion n'apporte qu'une **copie d'archive** de la revue (§ 4) |

Aucun run rouge, contrairement à la fusion précédente (`16ff5ac`) où
`pipeline-orchestrate` sortait en erreur. La couleur n'est pas le sujet :
le sujet est que **la seule porte capable de refuser cette fusion — la
présence d'un contre-audit enregistré — n'existe pas**, et que la porte qui
aurait pu la signaler (l'étape de publication) est écrite pour ne jamais
échouer (constat 1).

## 3. Constats

### Constat 1 — la relecture a été produite, payée, puis perdue ; la fusion s'est faite sans elle (P0)

Le run `pipeline-challenge` [31684301091](https://github.com/PLiagre/ForgeHistory/actions/runs/31684301091)
a fait exactement ce qu'on lui demande : il a résolu l'`audit_id`, vérifié
le budget, invoqué `claude-challenger` headless, obtenu une revue complète
et poussé la branche. Puis :

```
09:03:46  pull request create failed: GraphQL: Resource not accessible by
          personal access token (createPullRequest)
09:03:46  ##[warning]gh pr create refused (repository setting or permissions)
          -- branch forge-bot/review-CURSOR-4c45718-...-31684301091 is pushed;
          open the PR manually.
```

Trois faits, chacun mesuré :

**a) L'échec est un échec de droits, pas un réglage de dépôt.** Le message
`Resource not accessible by personal access token (createPullRequest)` nomme
le jeton : le PAT utilisé (`FORGE_BOT_PAT`) peut pousser une branche mais
n'a pas le droit « Pull requests : write ». Le message du workflow
(`.github/workflows/pipeline-challenge.yml:201`) parle vaguement de
« repository setting or permissions » ; l'API, elle, est précise. Les
quatre runs du jour échouent sur la **même** chaîne (§ 6.1).

**b) L'étape est écrite pour ne jamais échouer.** Le `|| echo "::warning::…"`
de la ligne 201 transforme un échec dur en avertissement, et le job se
termine `success` — les 12 étapes de `invoke-claude-challenger` sont
`success` (§ 6.1). C'est l'anti-motif documenté en 2026 sur les étapes
critiques : la conclusion enregistrée devient `success` et une protection de
branche voit du vert alors que le maillon n'a rien livré [S4, S5].

**c) Le résultat est stable et chiffrable.** Sept branches
`forge-bot/review-*` sont poussées sur le dépôt ; `gh pr list --state open`
rend **zéro** PR ouverte (§ 6.2). Quatre de ces branches ont été créées
aujourd'hui (`16ff5ac`, `4c45718`, `9e35764`, `ab0e7f0`) et aucune des
quatre revues n'est sur `master` : le dossier `architecture/reviews/`
contient 13 fichiers pour 31 audits.

**Pourquoi P0, et ce que je ne réclame pas.** Le même mécanisme est déjà
décrit comme P0 par l'audit `CURSOR-16ff5ac-contre-audit-perdu-a-la-publication`
— lequel est lui-même resté `AUDIT_PROPOSED`, sans aucune ligne de registre
(§ 6.4). Je ne rejoue pas son analyse. Les éléments neufs qui justifient de
le reposer au même niveau sont au nombre de trois : la relecture perdue
concernait cette fois **la PR fusionnée elle-même** ; la cause exacte est
désormais identifiée (**portée du PAT**, pas un réglage de dépôt) ; et la
perte est maintenant **chiffrée en argent** — 7,2771804 $ de relectures
produites aujourd'hui, dont aucune n'est arrivée dans le tronc (constat 4).
L'état de l'art 2026 place précisément la porte à cet endroit : une porte de
fusion déterministe qui vérifie que la relecture existe avant d'autoriser
l'intégration, jamais une étape « au mieux » [S1, S2, S3].

### Constat 2 — les quatre P1 pré-fusion entrent tels quels, et deux d'entre eux deviennent irréversibles (P1)

`git diff 4c45718..827d54e` ne nomme que **5 fichiers, tous venant du côté
`master`** (4 audits + `hermes/DASHBOARD.md`) : aucun des 11 fichiers de la
PR n'a été retouché entre l'audit et la fusion (§ 6.5). L'audit pré-fusion
n'a donc modifié aucune ligne du diff qu'il critiquait.

Deux de ses constats changent de nature en franchissant `master`, parce que
le registre est **append-only** par contrat (`architecture/README.md`, règle
d'intégrité 3) :

**a) Le comptage faux est maintenant définitif.** Rejeu avec le code du
dépôt sur le fichier fusionné (§ 6.6) :

```
parse_verdicts (ce qui est figé dans le registre) : {'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}
verdicts réels (colonne 3 du tableau)             : {'CONFIRMED': 9, 'PARTIAL': 1} | points: 10
```

La ligne du registre annonce 28 verdicts pour une revue qui en porte 10, et
4 `REFUTED` là où aucun point n'est réfuté. Elle ne pourra plus être
retirée, seulement contredite par une autre ligne.

**b) « Vérifié » veut toujours dire « personne n'a regardé ».** Mesure sur
les 47 lignes du registre fusionné (§ 6.6) : **8 événements**
`AUDIT_IMPLEMENTED` / `AUDIT_VERIFIED` ne portent **aucun** champ de preuve
— ni SHA, ni run CI, ni chemin de verdict — alors que toutes les autres
transitions en portent un (`review`, `decision`, `briefs`, `archive`). Je
note au passage que l'audit pré-fusion annonçait 6 et que le contre-audit
non publié corrigeait à 8 : ma mesure indépendante donne **8**, ce qui
confirme la correction du contre-audit — correction qui, faute de
publication, n'existe nulle part dans le tronc.

Le véhicule de correction prévu pour ce défaut est le brief 013 (point 9 de
`a4de4bb`, retenu). Ce véhicule est vide : constat 3.

### Constat 3 — la conversion a produit deux briefs qui n'instruisent rien (P1)

La fusion apporte `harness/queue/briefs/013-…` et `014-…`. Mesure (§ 6.7) :

```
013/brief.md        : 6 marqueurs <<TODO (planificateur)>>
014/brief.md        : 6 marqueurs <<TODO (planificateur)>>
013/eval-rubric.md  : 1        014/eval-rubric.md : 1
verdict_audit.py harness/queue/briefs/014-…  ->  VERDICT: REJECT
```

Aucun de ces deux fichiers ne contient de condition de succès, de compteur
ni de non-objectif. Or ce sont eux qui portent, par la décision automatique,
**27 points retenus** (10 pour `a4de4bb`, 17 pour `a600532`) — dont le
comptage faux du constat 2 et la porte de contre-audit du constat 1.

La conception est saine et je l'ai vérifiée : `orchestrator.py` refuse de
lancer `/forge-run` sur une graine et met le Planificateur en file. Le
problème n'est pas qu'un brief vide soit exécuté par erreur, c'est
**l'inverse** : rien ne mesure combien de temps un brief converti reste une
graine. Le registre n'a aucun événement entre `AUDIT_CONVERTED` et
`AUDIT_IMPLEMENTED`, et aucun tableau de bord ne compte les `<<TODO>>` en
tête de file. Une décision « APPROVED, 27 points retenus » se traduit donc,
côté dépôt, par zéro instruction — et l'écart n'est visible nulle part.

### Constat 4 — la boucle mesure sa dépense, puis la jette (P2)

`harness/pipeline/ci-budget-ledger.jsonl` fait **1 octet** au commit
fusionné (une ligne vide). Ce n'est pas faute d'avoir mesuré : l'étape 11 du
run de contre-audit (`Post-hoc budget marking`) s'est exécutée avec succès,
et les transcripts portent le coût exact de chaque invocation (§ 6.1) :

| run | audit relu | coût facturé |
|---|---|---|
| 31683198126 | `CURSOR-16ff5ac-…` | 2,6352315 $ |
| 31683996328 | `CURSOR-9e35764-…` | 2,2076934 $ |
| 31684301091 | `CURSOR-4c45718-…` | 2,4342555 $ |
| 31684016021 | `CURSOR-ab0e7f0-…` | non extrait (limite § 5) |
| **total mesuré** | | **7,2771804 $** |

Le mécanisme de la perte est localisable à la ligne près :
`ci_budget_guard record` écrit le fichier dans le workspace du runner, et
l'étape de publication ne commite que `architecture/reviews`
(`pipeline-challenge.yml:194` : `git add architecture/reviews`). La ligne de
coût meurt avec le runner.

Élément neuf par rapport au P3 « le registre de coût est vide » de l'audit
pré-fusion : la **cause** est identifiée, et le montant perdu est désormais
chiffré — 7,28 $ pour une journée dont **aucune** relecture n'a atteint le
tronc. L'état de l'art décrit exactement l'inverse : un registre append-only
tenu par l'orchestrateur, une ligne par session close, avec le coût total et
l'issue [S6, S7, S8]. Le dépôt fait déjà bien le **plafond avant l'appel**
(`--max-budget-usd 5.00`, ligne 155), ce que ces mêmes sources réclament ;
c'est l'enregistrement d'après qui manque.

### Constat 5 — l'arriéré de la boucle grandit, et reste invisible au moment de décider (P2)

Mesure au commit fusionné (§ 6.4) :

```
audits en inbox: 31 | avec >=1 ligne de registre: 16 | SANS aucune ligne: 15
[AUDIT_PROPOSED] (15)  [AUDIT_CHALLENGED] (3)  [AUDIT_APPROVED] (3)
[AUDIT_CONVERTED] (2)  [AUDIT_ARCHIVED] (8)
```

Ce constat est **déjà** dans le périmètre de provenance du brief 014
(P2-1 de `a600532` : « 12 audits sur 25 n'ont aucune ligne, et l'événement
`AUDIT_PROPOSED` n'apparaît jamais »), retenu par la décision. Je ne le
re-plaide pas ; je consigne seulement sa **croissance** entre les deux
mesures : 12/25 le 2026-08-13 au matin, **15/31** après cette fusion. Les
quatre audits déposés aujourd'hui sont tous dans les 15. L'arriéré
s'aggrave plus vite que la boucle ne le résorbe, et le brief censé le traiter
est une graine vide (constat 3).

### Constat 6 — 54 runs `hermes-observer` en file depuis deux heures (P3)

Sur les 100 derniers runs du dépôt, **54 sont en état `queued`, tous
`hermes-observer`**, le plus ancien créé à 08:56:46 UTC (§ 6.3). Aucun n'a
démarré : le workflow vise un runner auto-hébergé qui n'est pas là.

Ce n'est pas un défaut de cette fusion — elle en ajoute simplement un de
plus. Je le classe P3 (information) parce qu'aucune décision de fusion n'en
dépend, mais il touche la même propriété que les constats 1 et 4 : une file
qui ne se vide pas, personne pour le dire. À 54 runs, la file consomme aussi
le quota de concurrence du dépôt.

### Constat 7 — le score d'auto-audit annoncé n'est pas reproductible depuis le tronc (P3)

`AGENTS.md:50` annonce : « `harness_audit.py` currently scores 23/24: the
single FAIL (`no_premature_stub_content`) is a known stale assumption ».
Rejeu sur un clone propre au commit audité (§ 6.8) :

```
SCORE: 20/24
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: unexpected files in stub dirs: [...]
```

La cause est vérifiable : `harness/demo/fake_brief_001/run_demo.log` est
git-ignoré (`.gitignore:7` = `*.log`) et n'a **jamais** été commité
(`git log -- …/run_demo.log` est vide). Le point de 3 ne peut donc passer que
sur une machine où quelqu'un a lancé la démo auparavant — jamais sur un clone
neuf, ni en CI. Le chiffre de référence du dépôt décrit un état de poste de
travail, pas un état du tronc. La preuve F0 elle-même n'est pas en cause :
le job `f0-demo` de `harness-ci.yml` la rejoue et il est vert.

## 4. Ce qui tient (cadrage adverse, résultats négatifs)

J'ai cherché où la fusion casse quelque chose. Ces cinq points sont **bons**
et je les consigne comme tels :

1. **La fusion est propre.** Elle n'introduit rien qui ne vienne de ses deux
   parents : `git diff 4c45718..827d54e` ne nomme que les 5 fichiers venus
   de `master`, et `git diff e034f07..827d54e` exactement les 11 fichiers de
   la PR (§ 6.5). Aucun conflit résolu à la main, aucun ajout opportuniste.
2. **Les portes mécaniques sont vertes.** `314 passed, 16 skipped` pour le
   harnais, `25 passed` pour `sim/`, `All 31 audit(s) valid` pour le schéma
   des audits (§ 6.8).
3. **Un constat antérieur est levé.** Le P2-2 de `a600532` (« les 20 tests du
   moteur `sim/` ne tournent dans aucun job de CI ») ne tient plus : le job
   `sim-tests` existe (`harness-ci.yml:38`) et il est vert sur ce commit.
   C'est la boucle qui fonctionne, et il faut le dire.
4. **Le gate refuse bien les graines.** `verdict_audit.py` sur le brief 014
   rend `REJECT` — un brief vide ne peut pas être présenté comme livré
   (§ 6.7). Le défaut du constat 3 est un défaut de visibilité, pas de
   sûreté.
5. **`pipeline-orchestrate` a eu raison de ne pas se déclencher.** Son filtre
   ne vise que `architecture/reviews/*.md` ; la fusion n'apporte qu'une copie
   d'archive de la revue de `3b47ffe`. Ne pas rejouer une transition déjà
   enregistrée est ici le comportement correct — c'est précisément le rejeu
   qui avait mis la CI au rouge à la fusion précédente.

## 5. Limites de cet audit (à lire avant de s'en servir)

- Je n'ai pas extrait le coût du run `31684016021` (contre-audit de
  `ab0e7f0`) : son transcript ne rendait pas le champ `total_cost_usd` à la
  recherche employée. Le total de 7,2771804 $ est donc un **minorant** de la
  dépense du jour.
- Je n'ai pas ouvert le contenu des sept branches `forge-bot/review-*` : je
  constate qu'elles existent et qu'aucune PR ne leur correspond, pas que
  chacune contient une revue complète. Pour `4c45718`, le résumé du
  transcript CI atteste du contenu (§ 6.1).
- Le constat 1 impute l'échec à la portée du PAT sur la foi du message de
  l'API GitHub. Je n'ai pas accès aux réglages du jeton pour le vérifier
  directement ; une restriction équivalente au niveau du dépôt produirait le
  même message.
- `master` a avancé depuis le commit audité (`cccf458` au moment de l'audit,
  régénération du tableau de bord Hermes). Mes mesures portent sur
  `827d54e`.
- Je ne juge pas le raisonnement du contre-audit non publié : je constate
  qu'il existe et qu'il n'est pas arrivé.

## 6. Commandes rejouées (sorties collées)

Environnement : Linux, `.venv/bin/python` (voir `AGENTS.md`), arbre
positionné sur `827d54e`, `git status --porcelain` vide avant et après
mesures.

### 6.1 Le contre-audit produit, son coût, son échec de publication

```console
$ gh run view 31684301091 --repo PLiagre/ForgeHistory --log | grep -oE '"total_cost_usd":[0-9.]+'
"total_cost_usd":2.4342554999999995

$ gh run view 31684301091 --repo PLiagre/ForgeHistory --log | grep -o 'pull request create failed: [^"]*'
pull request create failed: GraphQL: Resource not accessible by personal access token (createPullRequest)

$ gh run view 31684301091 --repo PLiagre/ForgeHistory --json jobs   # extrait
JOB invoke-claude-challenger -> success
   10 Invoke claude-challenger headless (/forge-audit-review)        success
   11 Post-hoc budget marking (lot 009b, arbitrage n°2)              success
   12 Publish the review as a pull request                           success

$ for r in 31683198126 31683996328; do gh run view $r --log | grep -oE '"total_cost_usd":[0-9.]+'; done
"total_cost_usd":2.6352314999999993
"total_cost_usd":2.2076934
```

Résumé du contre-audit, extrait du transcript du run (09:03:43Z) :

```
"7 constats CONFIRMED sans réserve (2, 4, 5, 6, 8, 9, 10) — j'ai reproduit
 chaque mesure localement […] 2 constats PARTIAL […] Constat 3 : le compte
 « 6 occurrences nues dans le ledger » devrait être 8."
```

### 6.2 Sept branches de revue, zéro PR ouverte

```console
$ gh pr list --repo PLiagre/ForgeHistory --state open --limit 20
(aucune sortie : zéro PR ouverte)

$ git ls-remote origin 'refs/heads/forge-bot/review-*'
forge-bot/review-CURSOR-16ff5ac-contre-audit-perdu-a-la-publication-31683198126
forge-bot/review-CURSOR-4c45718-pr65-ledger-recupere-a-la-main-31684301091
forge-bot/review-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur-31594124761
forge-bot/review-CURSOR-73022bd-hermes-dashboard-modele-auditeur-31593583378
forge-bot/review-CURSOR-779d97c-revue-verdicts-illisibles-31596321701
forge-bot/review-CURSOR-9e35764-pr63-contre-audit-jamais-enregistre-31683996328
forge-bot/review-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion-31684016021

$ gh pr view 65 --json mergedAt,mergedBy,changedFiles,additions
{"additions":843,"changedFiles":11,"mergedAt":"2026-08-13T10:47:51Z",
 "mergedBy":{"login":"PLiagre"}}
```

### 6.3 CI du commit audité et file d'attente

```console
$ gh run list --commit 827d54ec2b0ee3b49d1b1a1992d64137759f32a6
31692702747 | hermes-observer   | workflow_run | queued    |         | 10:48:07Z
31692686820 | audit-guard       | push         | completed | success | 10:47:54Z
31692686782 | security          | push         | completed | success | 10:47:54Z
31692686803 | pipeline-audit    | push         | completed | success | 10:47:54Z
31692686790 | hermes-dashboard  | push         | completed | success | 10:47:54Z
31692686756 | harness-ci        | push         | completed | success | 10:47:54Z

$ gh run list --limit 100   # agrégation des runs non terminés
queued     hermes-observer   54
plus ancien queued: 2026-08-13T08:56:46Z
```

### 6.4 Arriéré du registre

```console
$ .venv/bin/python - <<'PY'   # comparaison inbox <-> audit-ledger.jsonl
audits en inbox: 31 | avec >=1 ligne de registre: 16 | SANS aucune ligne: 15
  - CURSOR-16ff5ac-contre-audit-perdu-a-la-publication
  - CURSOR-4c45718-pr65-ledger-recupere-a-la-main
  - CURSOR-9e35764-pr63-contre-audit-jamais-enregistre
  - CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion
  (… 11 autres)
PY

$ .venv/bin/python harness/audits.py list   # extrait
[AUDIT_PROPOSED] (15)  [AUDIT_CHALLENGED] (3)  [AUDIT_APPROVED] (3)
[AUDIT_CONVERTED] (2)  [AUDIT_ARCHIVED] (8)

$ ls architecture/reviews/*.md | wc -l
13
```

### 6.5 Le diff de fusion

```console
$ git diff --stat e034f07..827d54ec | tail -1
 11 files changed, 843 insertions(+)

$ git diff --stat 4c45718..827d54ec       # ce que master apporte, rien d'autre
 architecture/inbox/CURSOR-16ff5ac-…md   | 544 +++
 architecture/inbox/CURSOR-4c45718-…md   | 538 +++
 architecture/inbox/CURSOR-9e35764-…md   | 598 +++
 architecture/inbox/CURSOR-ab0e7f0-…md   | 576 +++
 hermes/DASHBOARD.md                     |  39 +-
 5 files changed, 2278 insertions(+), 17 deletions(-)
```

### 6.6 Registre fusionné : comptage faux et événements nus

```console
$ .venv/bin/python -c "… audit_review.parse_verdicts(…) …"
parse_verdicts (ce qui est figé dans le registre) : {'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}
verdicts réels (colonne 3 du tableau)             : {'CONFIRMED': 9, 'PARTIAL': 1} | points: 10

$ .venv/bin/python - <<'PY'   # champs de preuve des transitions de succès
  AUDIT_IMPLEMENTED CURSOR-FIXTURE-full-auto-demo            | champs de preuve: AUCUN
  AUDIT_VERIFIED    CURSOR-FIXTURE-full-auto-demo            | champs de preuve: AUCUN
  AUDIT_IMPLEMENTED CURSOR-5633ee7-automation-completeness   | champs de preuve: AUCUN
  AUDIT_VERIFIED    CURSOR-5633ee7-automation-completeness   | champs de preuve: AUCUN
  AUDIT_IMPLEMENTED CURSOR-e9a6f4c-codex-passation-full-auto | champs de preuve: AUCUN
  AUDIT_VERIFIED    CURSOR-e9a6f4c-codex-passation-full-auto | champs de preuve: AUCUN
  AUDIT_IMPLEMENTED CURSOR-3b47ffe-pr57-monde-sans-faim      | champs de preuve: AUCUN
  AUDIT_VERIFIED    CURSOR-3b47ffe-pr57-monde-sans-faim      | champs de preuve: AUCUN
total lignes=47 ; IMPLEMENTED/VERIFIED sans aucun pointeur de preuve = 8
PY
```

### 6.7 Les deux graines de briefs

```console
$ grep -c "TODO (planificateur)" harness/queue/briefs/01{3,4}-*/brief.md
harness/queue/briefs/013-sim-tick-nourrit-une-fois/brief.md:6
harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md:6

$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
VERDICT: REJECT

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
```

### 6.8 Portes du dépôt au commit audité

```console
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 17.66s
$ .venv/bin/python -m pytest sim/tests/ -q
25 passed in 0.92s
$ .venv/bin/python harness/audit_schema.py
All 31 audit(s) valid.
$ .venv/bin/python harness/harness_audit.py | tail -1
SCORE: 20/24
$ git check-ignore -v harness/demo/fake_brief_001/run_demo.log
.gitignore:7:*.log	harness/demo/fake_brief_001/run_demo.log
```

## 7. Risques par sévérité

| Sévérité | Constats | Risque si rien n'est fait |
|---|---|---|
| **P0** | 1 | La chaîne à quatre acteurs continue de produire et de payer des relectures que personne ne lit : chaque fusion se fait sur la seule foi de l'audit, la contradiction prévue par ADR-0010 n'existe pas en pratique, et l'échec reste vert. |
| **P1** | 2, 3 | Le tronc conserve définitivement une ligne de registre fausse et huit transitions de succès sans preuve ; les 27 points retenus restent dans deux fichiers qui n'instruisent rien, sans que personne mesure depuis quand. |
| **P2** | 4, 5 | Le coût réel de la boucle autonome reste inconnu du dépôt alors qu'il est mesuré à chaque run ; l'arriéré d'audits non traités croît (15/31) et n'est visible d'aucune surface au moment de décider d'une fusion. |
| **P3** | 6, 7 | Une file de 54 runs ne se vide pas sans que rien ne l'annonce ; le score d'auto-audit annoncé par la documentation ne peut pas être reproduit depuis un clone propre. |

## 8. Briefs atomiques proposés (3 au maximum — propositions, pas instructions)

Aucune de ces propositions n'est autorisée et aucune n'instruit quoi que ce
soit. Seuls le contre-audit (`architecture/reviews/`) puis la décision
(`architecture/decisions/` ou la politique d'ADR-0006) sont compétents.

1. **Publier la relecture, ou échouer bruyamment.** Couvre le constat 1.
   L'étape qui ouvre la PR de contre-audit doit faire échouer son job quand
   `gh pr create` est refusé, et le jeton employé doit porter le droit
   d'ouvrir une PR. Preuve rejouable possible : un test qui simule le refus
   `createPullRequest` et vérifie que le job sort non nul (rouge avant,
   toujours rouge après le refus, vert seulement quand la PR existe).
   *Chevauchement assumé* : le brief 014 porte déjà la porte de fusion côté
   « pas de fusion sans contre-audit enregistré ». Ce que je propose ici est
   la cause en amont (la relecture n'arrive jamais). Fondre les deux dans le
   014 plutôt qu'ouvrir un brief de plus est un arbitrage recevable — il
   revient à la décision, pas à moi.
2. **Une dépense d'agent qui n'atterrit pas dans le tronc doit être un
   échec.** Couvre le constat 4. La ligne de coût produite par
   `ci_budget_guard record` doit être commitée avec l'artefact qu'elle
   finance ; si le run a dépensé et n'a rien enregistré, il échoue. Preuve
   rejouable : un run de contre-audit fait passer
   `ci-budget-ledger.jsonl` de 1 octet à une ligne portant le run, l'audit et
   le montant.
3. **Rendre visible une conversion qui n'instruit rien.** Couvre le
   constat 3. Un brief resté à l'état de graine `<<TODO>>` après un
   `AUDIT_CONVERTED` doit être comptable et daté (registre ou tableau de
   bord), pour qu'« APPROVED, 27 points retenus » ne puisse pas signifier
   zéro instruction en silence. Preuve rejouable : une commande qui liste les
   briefs convertis encore non planifiés, avec leur âge.

Je **ne propose pas** de brief pour le constat 2 (déjà dans la provenance du
brief 013, point 9 de `a4de4bb`, retenu) ni pour le constat 5 (déjà dans la
provenance du brief 014, P2-1 de `a600532`, retenu). Les constats 6 et 7 sont
des informations, pas des demandes de lot.

## 9. Veille externe (`cursor-qa-scout`, compagnon de cet audit)

Section produite par le rôle compagnon `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`), append-only dans l'audit en
cours. Elle **compare** l'état du dépôt à l'état de l'art ; elle n'instruit
rien.

**Déclaration de non-doublon.** Les briefs ouverts au commit audité ont été
vérifiés un par un (`verdict.md` absent = ouvert) : `008-contexte-opus5-right-sizing`,
`013-sim-tick-nourrit-une-fois`, `014-pipeline-contre-audit-porte`. Aucune
des trois propositions du § 8 ne duplique leur objet : 008 porte sur le
dimensionnement du contexte, 013 sur l'ordre du tick de nourriture (points
retenus de `a4de4bb`), 014 sur la porte de contre-audit à la fusion (points
retenus de `a600532`). Le chevauchement partiel entre la proposition 1 et le
brief 014 est signalé explicitement au § 8 plutôt que masqué.

**Axe 1 — portes de fusion pour travail produit par agents.** L'état de
l'art 2026 converge vers une **porte déterministe au moment de la PR**,
indexée sur le contenu et la politique, avec un verdict lisible par machine
(`mergeable | human_review_required | blocked`) que l'agent comme l'humain
doivent respecter [S1, S2]. Le dépôt possède les briques (`audit-guard`,
`merge-bot` et leur liste blanche de chemins) mais les conditionne au
**préfixe de branche** — constat 4 de l'audit pré-fusion, confirmé par le
contre-audit non publié — et il n'a aucune porte qui exige l'existence d'une
relecture enregistrée. Résultat mesuré ici : une fusion à 10:47:51 avec zéro
relecture GitHub.

**Axe 2 — traçabilité de bout en bout.** Les cadres de gouvernance 2026
demandent une chaîne complète « action de l'agent → changement fusionné »,
exportable et immuable, et notent que l'opacité empêche d'attribuer la
responsabilité une fois les actions autonomes à l'échelle (avis Five Eyes du
2026-05-01) [S3]. Le dépôt a la bonne intuition — `audit-ledger.jsonl` est
append-only — mais 15 de ses 31 audits n'y ont aucune ligne et ses 8
transitions de succès n'y portent aucun pointeur (constats 2 et 5).

**Axe 3 — plafonds de jetons et registre de dépense.** L'état de l'art
sépare deux gestes : le **plafond avant l'appel** et le **registre après**,
une ligne par session close portant coût total et issue [S6, S7, S8]. Le
dépôt fait déjà le premier (`--max-budget-usd 5.00` sur chaque invocation, et
un `precheck` mensuel) — c'est à porter à son crédit. Il ne fait pas le
second : le fichier prévu pour ça fait 1 octet pendant que 7,28 $ de
relectures s'évaporent avec les runners (constat 4).

**Axe 4 — l'échec silencieux en CI.** La littérature 2026 est explicite : sur
une étape qui garde une porte, absorber l'échec (par `continue-on-error` ou
par un `|| echo` en bash) fait enregistrer `success` et laisse une protection
de branche voir du vert ; la règle est de laisser échouer, ou de relire
explicitement le résultat de l'étape en aval [S4, S5]. Le dépôt applique
l'anti-motif à l'endroit le plus coûteux : l'étape qui publie le contre-audit
(constat 1).

## 10. Sources externes

| # | source | date de la source | consulté le |
|---|---|---|---|
| S1 | *MergeGate — engine-agnostic gate CLI for AI-assisted development* (porte déterministe devant les agents de code : enregistrer, verrouiller, relier l'état branche/PR/fusion au travail réel) — <https://github.com/ShunsukeHayashi/mergegate/blob/main/README.md> | 2026 | 2026-08-13 |
| S2 | *Agents Shipgate — the deterministic merge gate for AI-generated agent capability changes* (verdict de fusion lisible par machine : `mergeable / human_review_required / insufficient_evidence / blocked`) — <https://github.com/mseep-ai/agents-shipgate> | 2026 | 2026-08-13 |
| S3 | CodeRabbit — *AI agent governance: a framework for engineering leaders* (« la gouvernance doit vivre dans la PR, la CI et la protection de branche » ; journaux immuables reliant agent, relecteur et date ; avis Five Eyes du 2026-05-01 sur le risque de responsabilité) — <https://www.coderabbit.ai/guides/ai-agent-governance> | 2026 | 2026-08-13 |
| S4 | Latchkey Learn — *GitHub Actions `continue-on-error` silently masks a failing step* (« ne jamais mettre cela sur une étape qui garde une porte ; si l'échec est toléré, relire `outcome` en aval ») — <https://latchkey.dev/learn/github-actions/gha-continue-on-error-masks-failure> | 2026 | 2026-08-13 |
| S5 | actsense — *continue-on-error on a critical job* (auditeur de sécurité GitHub Actions : « la protection de branche voit un check vert alors que l'étape a échoué ») — <https://actsense.dev/vulnerabilities/continue_on_error_critical_job/> | mis à jour le 2026-04-24 | 2026-08-13 |
| S6 | DZone — *Token attribution framework for agentic AI in CI/CD* (attribution par run/étape, plafonds à plusieurs niveaux, `cost.outcome` conservé même quand le run échoue) — <https://dzone.com/articles/agentic-ai-token-attribution-ci-cd> | 2026 | 2026-08-13 |
| S7 | Solana Garden — *LLM agent cost attribution and token accounting explained* (« un run ledger est un enregistrement append-only tenu par l'orchestrateur, pas par le modèle » ; plafond par run 0,50–8 $) — <https://solana.garden/guides/llm-agent-cost-attribution-token-accounting-explained/> | 2026 | 2026-08-13 |
| S8 | dev.to — *40 cents a day, three weeks of corrupted writes, zero alerts fired* (plafond **avant** l'invocation ; à la clôture d'une session, écrire une ligne : coût total, profondeur, nombre d'agents — « un plafond rangé dans un fichier que personne ne lit, c'est du théâtre de réconciliation ») — <https://dev.to/nathanielc85523/40-cents-a-day-three-weeks-of-corrupted-writes-zero-alerts-fired-54i0> | 2026 | 2026-08-13 |

Les trois thèmes exigés par le contrat (`architecture/agents/cursor-auditor.md`
§ Preuve de fin) sont couverts : pipeline de développement autonome (S1, S2,
S3), orchestration d'agents en CI (S4, S5), budget de jetons des agents
(S6, S7, S8). S1–S3 fondent le constat 1 ; S4–S5 fondent le point (b) du
constat 1 ; S6–S8 fondent le constat 4.

---

Fin de l'audit. Statut `PROPOSED` : aucun point ci-dessus n'est une
instruction, aucun n'autorise une implémentation, aucun n'est pré-approuvé.
Le contre-audit, puis la décision, restent seuls compétents.
