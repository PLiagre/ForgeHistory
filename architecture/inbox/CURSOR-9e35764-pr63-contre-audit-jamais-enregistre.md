---
audit_id:                CURSOR-9e35764-pr63-contre-audit-jamais-enregistre
auditor:                 cursor-cloud
target_branch:           master
target_commit:           9e35764e4dc3ce0f88c20b22fa22633f85754d61
created_at:              2026-08-13T09:05:00Z
audit_type:              pr-critique
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #63 — « challenge: revue de l'audit CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois »

Rôle : `cursor-auditor` (contrat `architecture/agents/cursor-auditor.md`).
Référentiel de jugement : `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle
(`architecture/README.md`, ADR-0005/0006).

## 0. Ce qu'il faut retenir en trois phrases

Le contenu de cette PR est bon : j'ai rejoué moi-même, sans recopier ses
chiffres, les deux mesures les plus importantes du contre-audit et j'obtiens
les mêmes résultats (§ 8.1). Mais le livrable **n'a produit aucun effet** :
la revue est bien sur `master`, et le registre `architecture/audit-ledger.jsonl`
ne contient **aucune ligne** pour l'audit qu'elle contre-audite — l'événement
a été calculé puis perdu sur un conflit de rebase (P0-1). Conséquence
concrète : le défaut P0 du moteur qu'elle confirme (« la nourriture
transférée nourrit deux fois ») ne deviendra jamais un brief par la boucle
automatique, sauf intervention.

### Provenance et périmètre audité

| | |
|---|---|
| Pull request | [#63](https://github.com/PLiagre/ForgeHistory/pull/63), non-brouillon, ouverte 2026-08-13T08:34:40Z |
| Tête de branche | `25b318521d48a7d5ede6913a0f436a6fb955b2df` (branche `forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615`) |
| Fusionnée | 2026-08-13T08:35:09Z, en écrasement (*squash*) → `9e35764e4dc3ce0f88c20b22fa22633f85754d61` sur `master` |
| Diff | 1 fichier, +124 / −0 : `architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md` (création) |
| Auteur du commit | `forge-bot` (workflow `pipeline-challenge`, run [31681378615](https://github.com/PLiagre/ForgeHistory/actions/runs/31681378615)) |

`target_commit` est le commit d'écrasement sur `master`, pas la tête de
branche : la règle d'intégrité 4 de `architecture/README.md` exige un
`target_commit` présent dans l'historique de `target_branch`, et un *squash*
laisse `25b3185` hors de cet historique. Vérification :

```
$ gh api repos/PLiagre/ForgeHistory/compare/9e35764e4dc3ce0f88c20b22fa22633f85754d61...master --jq '.status'
ahead
```

(`ahead` = `master` est en avance sur ce commit, donc ce commit est bien un
ancêtre de `master`.)

## 1. Lentille 1 — Intention avant diff

L'intention est lisible et correctement bornée. Le contrat de
`architecture/README.md` (§ « Un seul rôle écrit dans chaque dossier ») dit
que `reviews/` est écrit par Claude seul et contient un verdict par point
avec preuve ; le diff fait exactement cela, et rien d'autre. La description
de PR annonce « uniquement `architecture/reviews/CLAUDE-CURSOR-a4de4bb-…md` »
et le diff le confirme (1 fichier).

Deux écarts entre ce que la PR annonce et ce que la machine a fait sont
traités plus bas : la cause annoncée de l'ouverture manuelle est démentie par
le journal (P1-2), et la promesse « après fusion, `pipeline-orchestrate`
enregistre `AUDIT_CHALLENGED` puis applique la décision automatique » n'a pas
eu lieu (P0-1).

## 2. Lentille 3 — Portes mécaniques : classification de la CI

Portes mécaniques du commit audité (tête de PR `25b3185`) :

```
$ gh api repos/PLiagre/ForgeHistory/commits/25b318521d48a7d5ede6913a0f436a6fb955b2df/check-runs \
    --jq '.check_runs[] | "\(.name)\t\(.status)\t\(.conclusion)\t\(.started_at)\t\(.completed_at)"'
Reconcile local Hermes state   queued      null       2026-08-13T08:35:12Z   null
cursor-scope                   completed   skipped    2026-08-13T08:34:43Z   2026-08-13T08:34:43Z
invoke-cursor-auditor          completed   success    2026-08-13T08:34:45Z   2026-08-13T08:35:00Z
gitleaks                       completed   success    2026-08-13T08:34:45Z   2026-08-13T08:34:54Z
actionlint                     completed   success    2026-08-13T08:34:45Z   2026-08-13T08:34:56Z
sim-tests                      completed   success    2026-08-13T08:34:45Z   2026-08-13T08:35:04Z
tests                          completed   success    2026-08-13T08:34:45Z   2026-08-13T08:35:06Z
f0-demo                        completed   success    2026-08-13T08:34:45Z   2026-08-13T08:34:54Z
schema                         completed   success    2026-08-13T08:34:52Z   2026-08-13T08:35:04Z
check-and-automerge            completed   success    2026-08-13T08:34:51Z   2026-08-13T08:35:03Z
Reconcile local Hermes state   completed   cancelled  2026-08-13T08:34:43Z   2026-08-13T08:35:12Z

$ gh api repos/PLiagre/ForgeHistory/commits/25b318521d48a7d5ede6913a0f436a6fb955b2df/status --jq '.state'
pending
```

Classification : **7 vertes** (`gitleaks`, `actionlint`, `sim-tests`,
`tests`, `f0-demo`, `schema`, `check-and-automerge`), **1 verte sans valeur
de preuve** (`invoke-cursor-auditor`, 15 s — voir P0-2), **1 ignorée**
(`cursor-scope`, à raison : la branche n'est pas `cursor/*`), **1 annulée** et
**1 en file d'attente** (`Reconcile local Hermes state`, runner auto-hébergé
Windows). État agrégé au moment de la fusion : `pending`, **pas vert**.

Les portes de contenu ont donc bien tourné et sont vertes : je ne dépense pas
de jugement dessus (lentille 3) et je concentre la critique sur ce que les
machines n'ont pas vu.

## 3. P0-1 — La revue livrée par cette PR n'est jamais entrée au registre : le contre-audit est perdu, et avec lui le P0 du moteur

### Le fait

Le fichier de revue est bien sur `master` :

```
$ gh api repos/PLiagre/ForgeHistory/contents/architecture/reviews --jq '.[].name' | grep a4de4bb
CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md
```

Le registre, lui, ne le connaît pas :

```
$ gh api repos/PLiagre/ForgeHistory/contents/architecture/audit-ledger.jsonl --jq '.content' | base64 -d > /tmp/ledger.jsonl
$ wc -l /tmp/ledger.jsonl
40 /tmp/ledger.jsonl
$ grep -c a4de4bb /tmp/ledger.jsonl
0
$ gh api repos/PLiagre/ForgeHistory/contents/architecture/decisions --jq '.[].name' | grep a4de4bb || echo "(aucune décision)"
(aucune décision)
```

Re-vérifié une seconde fois sur la tête de `master` la plus récente au moment
d'écrire (`b915e5e`), trente minutes après la fusion, pour écarter un simple
retard de la boucle :
`git show origin/master:architecture/audit-ledger.jsonl | grep -c a4de4bb` → `0`,
registre toujours à 40 lignes, et aucun fichier
`architecture/decisions/DECISION-CURSOR-a4de4bb-*`.

Aucun `AUDIT_CHALLENGED`, aucun `AUDIT_APPROVED`, aucune décision. L'audit
`CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois` reste donc, pour la
machine, `AUDIT_PROPOSED` : d'après `harness/audit_review.py` (lignes 154-161),
seul un audit `AUDIT_PROPOSED` peut être contre-audité, et un rejeu buterait
maintenant sur `harness/audit_review.py` lignes 117-121
(« refusing to overwrite a review in progress ») puisque le fichier de revue
existe déjà. L'audit est coincé entre deux gardes qui, chacune séparément, ont
raison.

Pour comparaison, la PR jumelle #62 (même workflow, même minute, autre audit)
a bien été enregistrée :

```
$ tail -2 /tmp/ledger.jsonl
{"timestamp": "2026-08-13T08:35:12Z", "audit_id": "CURSOR-a600532-fusion-sans-contre-audit", "event": "AUDIT_CHALLENGED", ...}
{"timestamp": "2026-08-13T08:35:12Z", "audit_id": "CURSOR-a600532-fusion-sans-contre-audit", "event": "AUDIT_APPROVED", "actor": "policy:auto", ...}
```

### La cause, mesurée

Le run `pipeline-orchestrate` déclenché par la fusion de cette PR a **échoué** :

```
$ gh run list --workflow pipeline-orchestrate.yml --limit 3
31682710982  2026-08-13T08:35:11Z  push  completed  failure  9e35764e  challenge: revue CLAUDE-CURSOR-a4de4bb-…
31682696284  2026-08-13T08:35:01Z  push  completed  success  96d15654  challenge: revue CLAUDE-CURSOR-a600532-…
31682196140  2026-08-13T08:28:19Z  push  completed  failure  16ff5ac7  Merge pull request #60 …
```

Il avait pourtant fait le travail — l'échec est au dernier centimètre, à la
poussée (run 31682710982, étape « Commit ledger/decision/brief-seed update ») :

```
[master 508ef8e] pipeline-orchestrate: review_recorded
 2 files changed, 20 insertions(+)
 create mode 100644 architecture/decisions/DECISION-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md
From https://github.com/PLiagre/ForgeHistory
   9e35764..97e8e7c  master     -> origin/master
Auto-merging architecture/audit-ledger.jsonl
CONFLICT (content): Merge conflict in architecture/audit-ledger.jsonl
error: could not apply 508ef8e... pipeline-orchestrate: review_recorded
##[error]Process completed with exit code 1.
```

Le commit `508ef8e` contenait la décision et les lignes de registre
attendues ; le `git pull --rebase origin master` a rencontré un conflit, la
marche s'est arrêtée là, et rien n'a été poussé.

### Pourquoi la prémisse du workflow est fausse

`.github/workflows/pipeline-orchestrate.yml` lignes 133-138 affirme
l'inverse de ce qui vient de se produire :

> « master peut avoir avancé pendant le run (fusion merge-bot, tableau de bord
> Hermes) : rebase avant push. Les seuls autres écrivains du ledger sont les
> runs de CE workflow (sérialisés par le groupe concurrency ci-dessus), donc
> **le rebase ne peut pas conflicter sur le ledger**. »

La sérialisation (`concurrency: pipeline-orchestrate-master`,
`cancel-in-progress: false`, lignes 46-53) sérialise les **exécutions**, pas
les **bases**. Sur un événement `push`, `actions/checkout` fixe l'arbre au SHA
poussé (`9e35764`) : le second run part donc d'une base qui **précède** la
poussée de registre du premier run, et deux ajouts en fin du même fichier
JSONL sur deux bases différentes se conflictent textuellement, quelle que
soit l'ordre d'exécution. C'est exactement le motif décrit par la littérature
sur les journaux append-only versionnés (S3, S6) : un ajout en fin de fichier
n'est pas une opération que le fusionneur de lignes de git sait réconcilier.

### Élément neuf par rapport à ce qui est déjà consigné

Ce n'est pas une redite. L'audit `CURSOR-2a4f808-decision-auto-ledger` § P3-7
avait examiné ce même commentaire et **concédé la prémisse pour le fichier
lui-même** :

> « Le commentaire justifie l'absence de conflit ainsi : "Les seuls autres
> écrivains du ledger sont les runs de CE workflow". **C'est exact pour le
> fichier ledger.** Mais le rebase et la poussée, eux, sont exposés à tous
> les autres écrivains de `master` […] »
> (`architecture/inbox/CURSOR-2a4f808-decision-auto-ledger.md` lignes 371-382)

L'élément neuf est double : (a) le conflit s'est produit **sur le fichier de
registre lui-même**, ce que le constat P3-7 excluait ; (b) il ne s'agit plus
d'un risque théorique classé P3 mais d'une **perte réelle**, datée, chiffrée
(un contre-audit complet, une décision, la conversion attendue d'un P0
moteur). La sévérité change parce que la preuve a changé.

### Portée : ce qui est perdu

Le point 1 du contre-audit perdu confirme, sonde à l'appui, que la nourriture
transférée par le commerce annule le déficit **et** reste en stock. Je l'ai
rejoué indépendamment (§ 8.1) : c'est vrai. `ROADMAP.md` publie déjà
`kg_transportes_monde_reel`, compteur affecté par ce défaut. Sans événement au
registre, la règle `review_has_confirmed_or_partial` d'`auto_policy.yaml` ne
s'exécute jamais, donc aucun brief ne sera semé — la boucle « audit → revue →
décision → brief » est ouverte sur ce cas précis, en silence.

### Visibilité de la panne

L'escalade a bien tourné (`pipeline-failure-escalate`, run 31682751266,
`success` à 08:35:44) mais elle est **journalisante par construction** :
`.github/workflows/pipeline-failure-escalate.yml` lignes 14-18 le dit
(« Log-only […] no real `gh issue create` call here either, matching this
brief's Non-Goals ») et ses permissions sont `contents: read` (ligne 37).
Vérification côté surfaces lisibles :

```
$ gh issue list --state open --limit 10
7  2026-08-06T07:04:21Z  Full-auto pipeline: set CI secrets + branch protection …
```

Aucune trace ouverte de l'incident du 13/08. C'est cohérent avec un Non-Goal
assumé — je ne le recompte donc pas comme un défaut distinct (le constat
`CURSOR-a600532` P1-2 l'a déjà posé) — mais c'est le mécanisme par lequel une
perte de donnée d'audit reste invisible autrement qu'en lisant l'onglet
Actions.

## 4. P0-2 — Fusion automatique 29 secondes après ouverture : trois des quatre preuves exigées par la décision du 2026-08-11 manquaient

### La règle écrite

`architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md`
lignes 30-42, reconduite telle quelle par ADR-0010 (ligne 62, « arbitrages
n°1 (porte conditionnelle de fusion) ») :

> « L'auto-fusion est autorisée seulement si **quatre** preuves sont réunies :
> CI verte, gate mécanique ACCEPT, verdict d'un Évaluateur dont l'acteur
> diffère du producteur, et audit Cursor déposé sur la pull request. […]
> aucune étape de cette porte ne peut être rendue facultative sans une
> nouvelle décision écrite. »

### Ce qui s'est passé

| Horodatage | Événement |
|---|---|
| 08:34:40 | PR #63 ouverte (non-brouillon) |
| 08:34:43 | `check-and-automerge` démarre |
| 08:34:45 → 08:35:00 | `invoke-cursor-auditor` : 15 s, conclusion `success` |
| 08:35:03 | `check-and-automerge` conclut `success` |
| 08:35:09 | PR fusionnée |
| 08:35:12 | `Reconcile local Hermes state` : `queued` (jamais exécuté) |

Preuve 1 (CI verte) : absente — état agrégé `pending` (§ 2).
Preuve 4 (audit Cursor déposé) : absente — **le présent fichier est le premier
audit Cursor de cette PR, et il est écrit après la fusion**. Les 15 secondes
de `invoke-cursor-auditor` ne sont pas un audit : le workflow poste une
demande d'agent et le dit lui-même en clair
(`.github/workflows/pipeline-audit.yml` ligne 196 :
« cursor-auditor launched -- its audit will arrive as a cursor/* PR touching
architecture/inbox/** only. »). Il s'est écoulé **9 secondes** entre la fin de
la dépêche et la fusion.

Ce n'est pas rattrapable par un réglage de délai : la porte compare deux
échelles de temps incompatibles. La garde du merge-bot ne vérifie d'ailleurs
que des **chemins**, jamais les quatre preuves —
`.github/workflows/merge-bot.yml` ligne 50 :

```
offending="$(printf '%s\n' "$changed" | grep -vE '^(architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/)' || true)"
```

puis ligne 71 `gh pr merge --auto --squash`, dont le commentaire (lignes 66-72)
assume que le filet est la protection de branche — indisponible sur ce plan
GitHub (`HTTP 403`, constaté le 2026-08-11 et consigné dans la décision
citée). Sans protection de branche, `--auto` fusionne dès que GitHub l'y
autorise, c'est-à-dire immédiatement.

### Élément neuf par rapport à `CURSOR-a600532`

L'audit `CURSOR-a600532-fusion-sans-contre-audit` § P0-1 a déjà posé « la
fusion s'est faite avec le maillon de contre-audit en panne » et a été retenu
par la décision automatique. Je ne réémets pas ce constat. L'élément neuf est
que la porte est ici **structurellement inapplicable, y compris tous maillons
en bonne santé** : l'auditeur est asynchrone par conception (agent Cloud →
PR ultérieure), donc la preuve n°4 ne peut jamais exister au moment où
`--auto` s'exécute. La littérature 2026 est convergente sur ce point : les
garde-fous non négociables doivent être déterministes et exécutés **au
runner**, avant la fusion, sans `continue-on-error` (S1), et la fusion sur la
branche principale est classée « approbation requise » dans les échelles
d'autonomie publiées (S1, S2).

## 5. P1-1 — Le registre aurait publié « 14 CONFIRMED, 4 REFUTED » pour une revue qui confirme 9 points et n'en réfute aucun

Mesuré avec le code du dépôt, sur le contenu exact ajouté par la PR :

```
$ .venv/bin/python -c "…parse le diff, écrit le fichier, appelle les deux parseurs…"
parse_verdicts (audit_review, tout le texte)          : {'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}
parse_point_verdicts (audit_decision, lignes de tableau) : [(1,'CONFIRMED'),(2,'CONFIRMED'),(3,'CONFIRMED'),(4,'CONFIRMED'),
                                                           (5,'CONFIRMED'),(6,'CONFIRMED'),(7,'CONFIRMED'),(8,'CONFIRMED'),
                                                           (9,'CONFIRMED'),(10,'PARTIAL')]
```

C'est le **premier** de ces deux comptages qui part au registre :
`harness/audit_review.py` ligne 174 (`verdicts = parse_verdicts(text)`) puis
ligne 203 (`verdicts=verdicts`) dans l'événement `AUDIT_CHALLENGED`. Le
document, lui, écrit noir sur blanc : « Aucun point n'est REFUTED »
(revue, § 4). Le registre aurait donc affirmé quatre réfutations là où la
revue n'en prononce aucune, et 14 confirmations pour 9 points confirmés.

Deux précisions qui font la sévérité :

1. **Le défaut est celui-là même que la revue confirme** (son point 9), déjà
   signalé par `CURSOR-779d97c-revue-verdicts-illisibles` et jamais converti
   en brief — je le vérifie :
   `grep -c 779d97c /tmp/ledger.jsonl` → 1 seule ligne, un
   `AUDIT_CHALLENGED`, ni approbation ni conversion.
2. **La revue l'amplifie en le décrivant.** La ligne de légende
   (« Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. »)
   valait +1 sur chaque compteur ; la cellule du point 9 cite en plus les
   quatre mots et deux dictionnaires de résultats, ce qui porte l'écart à
   +5 CONFIRMED / +4 REFUTED. C'est un piège typique du texte produit par un
   agent qui documente un défaut de comptage dans le fichier même qui est
   compté (lentille 6 : structure de données naïve — un comptage de mots par
   expression régulière là où un parseur de lignes de tableau existe déjà
   dans le dépôt).

Effet réel aujourd'hui : **nul**, parce que P0-1 a empêché l'enregistrement.
Deux défauts se sont masqués l'un l'autre ; c'est une raison de traiter les
deux, pas une atténuation.

## 6. P1-2 — La cause annoncée de l'ouverture manuelle de la PR est démentie par le journal du run

La description de PR affirme :

> « le workflow a poussé la branche `forge-bot/*` mais n'a pas pu ouvrir la PR
> (réglage GitHub « Allow GitHub Actions to create and approve pull requests »
> inactif) »

Le journal du run 31681378615, étape « Publish the review as a pull request »,
dit autre chose :

```
 * [new branch]  forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615 -> …
pull request create failed: GraphQL: Resource not accessible by personal access token (createPullRequest)
##[warning]gh pr create refused (repository setting or permissions) -- branch … is pushed; open the PR manually.
```

`Resource not accessible by personal access token` désigne le jeton employé —
`GH_TOKEN: ${{ secrets.FORGE_BOT_PAT || secrets.GITHUB_TOKEN }}`
(`.github/workflows/pipeline-challenge.yml` ligne 174) — pas le réglage
« Actions » du dépôt. Un PAT à portée fine sans permission
« Pull requests: write » produit exactement ce message. Deux conséquences :

- le propriétaire, en suivant la description, irait modifier un réglage qui
  n'est pas la cause, et la panne se reproduirait une troisième fois ;
- l'affirmation est **plus forte que la preuve disponible** — c'est
  précisément le défaut que la lentille 2 cherche (une cause affirmée, non
  mesurée), et il apparaît ici dans le texte d'accompagnement d'une PR dont
  tout le contenu, lui, est adossé à des sondes.

À noter dans le même mécanisme : l'étape s'appelle « Publish the review as a
pull request » et conclut `success` alors que rien n'a été publié (le `||
echo "::warning::…"` de la ligne 201 avale l'échec). Une étape qui ne peut
pas échouer n'est pas une porte (S1).

## 7. Constats P1/P2 restants

### P1-3 — Le maillon qui vérifie tourne sur un modèle jugé trop faible pour le maillon voisin, sans plancher écrit

Modèle réellement utilisé par le contre-audit, lu dans l'initialisation du
CLI (run 31681378615, étape « Invoke claude-challenger headless ») :

```
{"type":"system","subtype":"init", … "model":"claude-sonnet-5", "permissionMode":"acceptEdits", …}
```

`.github/workflows/pipeline-challenge.yml` ligne 152 appelle
`claude -p "/forge-audit-review …" --max-budget-usd 5.00` **sans aucun
`--model`** : le maillon prend le défaut du compte. Or le workflow voisin
consigne une exigence contraire du propriétaire pour la critique
(`.github/workflows/pipeline-audit.yml` lignes 105-113) :

> « le propriétaire exige au moins Opus pour la critique (2026-08-12 — le
> défaut claude-4.5-sonnet du premier tour était trop faible) »

et implémente pour cela une sélection outillée (interrogation de
`GET /v1/models`, préférence `opus`/`thinking`, lignes 128-152). Le maillon
`challenge` est celui qui transforme un audit en `CONFIRMED`/`REFUTED` et
alimente la décision automatique : même classe de risque, aucun plancher.

Je ne prétends **pas** que la sortie est fautive — je l'ai rejouée et elle
tient (§ 8.1). Le constat porte sur la gouvernance : un plancher de modèle
posé pour un rôle et absent pour le rôle qui le vérifie est une asymétrie non
décidée. La littérature 2026 sur le routage de modèles dit la même chose
autrement : on réserve le palier fort aux décisions « à coût d'erreur élevé
et exposées à l'audit », et on ne laisse pas un modèle juger seul de la
qualité d'une production par un pair sans arbitre déterministe (S4, S5).

### P2-1 — Le point 10 n'a pas pu être rejoué pour une raison outillable en une ligne

La revue justifie son seul verdict non plein ainsi : « cet environnement de
revue n'a pas d'authentification GitHub (`gh auth status` échoue, pas de
`GH_TOKEN`) ». Le journal confirme la cause et montre qu'elle est
accidentelle : l'étape qui invoque l'agent reçoit uniquement

```
env:
  CLAUDE_CODE_OAUTH_TOKEN: ***
  ANTHROPIC_API_KEY:
```

tandis que l'étape suivante, dans le **même job**, reçoit bien
`GH_TOKEN: ***`. Le jeton existe donc à trois lignes de là. Une classification
CI en direct — la seule preuve que le contre-audit n'a pas pu produire —
redeviendrait rejouable en exposant ce jeton à l'étape d'invocation.

### P2-2 — Critère de verdict incohérent entre points de preuve équivalente

Trois points reposent sur un rejeu partiel assumé, avec deux verdicts
différents pour la même forme de preuve :

- point 6 : « Je n'ai pas rejoué la mesure d'ampleur sur le monde réel […]
  faute de temps machine disponible dans cette revue » → **CONFIRMED** ;
- point 8 : « Je n'ai pas de position indépendante sur le seuil "~5 fichiers"
  cité par l'audit » → **CONFIRMED** ;
- point 10 : partie structurelle vérifiée, partie « état en direct » non
  rejouée → **PARTIAL**.

Le problème n'est pas l'honnêteté (elle est explicite à chaque fois, et c'est
à porter au crédit du document) mais le **critère** : à preuve de même forme,
verdict différent. Comme la règle automatique retient l'union
`CONFIRMED ∪ PARTIAL` (`auto_policy.yaml`, règle
`review_has_confirmed_or_partial` citée par le registre), l'effet pratique est
nul ici — d'où P2 et non P1. Il cesserait de l'être le jour où une règle
distinguerait les deux.

## 8. Ce qui tient (P3 — information, pas constat)

### 8.1 J'ai rejoué la substance : elle tient

Sonde écrite depuis la lecture de `sim/engine.py`, sans reprendre les chiffres
du document :

```
$ .venv/bin/python <sonde indépendante, cellule 100 hab. = besoin 200 kg>
après consommation      : stock=0.0 deficit=200.0
après commerce          : stock=200.0 deficit=0.0  transporté=200.0
témoin (ration payée)   : stock=0.0 deficit=0.0
ÉCART receveuse - témoin : +200.0 kg
ordre c1-c2 c2-c3    -> stocks {'c1': 800.0, 'c2': 0.0, 'c3': 200.0}
ordre c2-c3 c1-c2    -> stocks {'c1': 800.0, 'c2': 200.0, 'c3': 0.0}
```

Ce sont, aux décimales près, les chiffres des points 1 et 2 du contre-audit :
la nourriture reçue annule le déficit **et** reste en stock (double bénéfice
de 200 kg), et le résultat du transport dépend de l'ordre du fichier
d'adjacence (200 kg atteignent `c3`, non adjacente à la source, ou pas du
tout). Cause lisible dans le code : `sim/engine.py` lignes 93-95 (le transfert
crédite le stock puis décrémente le déficit) et `tick()` lignes 162-166
(consommation avant commerce). Le score du garde-fou du harnais est également
celui qu'annonce la revue :
`.venv/bin/python harness/harness_audit.py` → `SCORE: 20/24`.

### 8.2 L'indépendance du maillon est réelle cette fois

Le contre-audit précédent (`CLAUDE-CURSOR-3b47ffe`) était signé
`cursor-orchestrateur` — l'audit contre-audité en faisait son point 4. Ici, le
run a bien installé et invoqué Claude Code (étapes « Install Claude Code CLI »
puis « Invoke claude-challenger headless », toutes deux `success`), et
l'initialisation du CLI ci-dessus le confirme. Le producteur du lot moteur
(PR #60, 7 commits `Cursor Agent`) et le vérificateur sont donc, cette fois,
deux acteurs distincts. C'est un progrès à consigner.

### 8.3 Taille, format, régressions évitées

1 fichier, +124 lignes : très en-dessous du seuil de la lentille 5 (~5
fichiers / quelques centaines de lignes) — aucune recommandation de découpage.
Le tableau est lisible par la machine (10 lignes `| N | … |` retournées par
`parse_point_verdicts`), donc la régression du 2026-08-12 documentée dans
`harness/audit_review.py` lignes 180-193 (numérotation `§1`/`P1-1` qui bloquait
la boucle après fusion) ne s'est **pas** reproduite. Les quatre verdicts sont
dans le vocabulaire imposé, et aucun placeholder `<<TODO>>` ne subsiste.

### 8.4 Pistes vérifiées et écartées (pour éviter le bruit)

- **`reviewer: claude-code` en tête de revue vs `actor: "claude"` en dur au
  registre** (`harness/audit_review.py` ligne 199) : c'est exactement le
  point 3 du contre-audit lui-même, déjà consigné ailleurs. Rien de neuf ici,
  je n'en fais pas un constat.
- **Fusion attribuée au propriétaire** (`mergedBy: PLiagre`) alors que c'est
  `gh pr merge --auto` avec le PAT du propriétaire : même classe que le
  point 3 ci-dessus (acteur nominal ≠ acteur réel), déjà couvert.
- **`AGENTS.md` annonce `harness_audit.py` à 23/24** alors que la mesure donne
  20/24 : hors du diff de cette PR, mentionné comme information seulement.

## 9. Risques par sévérité

| Sévérité | Constat | Preuve principale |
|---|---|---|
| **P0** | P0-1 — le contre-audit livré n'est jamais entré au registre ; le P0 moteur qu'il confirme ne sera pas converti | 0 ligne `a4de4bb` dans le registre de `master` ; run 31682710982, `CONFLICT (content)` sur `audit-ledger.jsonl` |
| **P0** | P0-2 — auto-fusion en 29 s sans 2 des 4 preuves de la porte conditionnelle | horodatages de la PR + `--auto --squash` + `merge-bot.yml` ligne 50 ; décision du 2026-08-11 lignes 30-42 |
| **P1** | P1-1 — le registre publierait 14 CONFIRMED / 4 REFUTED pour 9 CONFIRMED / 0 REFUTED | mesure des deux parseurs ; `audit_review.py` lignes 174 et 203 |
| **P1** | P1-2 — cause annoncée de l'ouverture manuelle démentie par le journal ; étape « Publish… » verte sans avoir publié | log du run 31681378615 ; `pipeline-challenge.yml` lignes 174 et 201 |
| **P1** | P1-3 — le maillon de vérification tourne sur `claude-sonnet-5`, sans plancher, quand le maillon voisin en a un (Opus) | init CLI du run ; `pipeline-audit.yml` lignes 105-113 ; `pipeline-challenge.yml` ligne 152 |
| **P2** | P2-1 — point 10 non rejouable faute de `GH_TOKEN` dans l'étape d'invocation, alors qu'il existe dans le job | env des deux étapes du même job |
| **P2** | P2-2 — critère de verdict incohérent (rejeu partiel → CONFIRMED pour 6 et 8, PARTIAL pour 10) | revue, points 6, 8, 10 |
| **P3** | Substance rejouée et confirmée ; indépendance du maillon réelle ; format machine-lisible ; taille saine | § 8 |

## 10. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

1. **Rendre l'écriture du registre insensible aux écritures concurrentes, et
   rejouer l'événement perdu.** Périmètre : le mécanisme de poussée de
   `architecture/audit-ledger.jsonl` (fusion par union ou par
   enregistrement, réessai borné, échec bruyant si la perte subsiste) plus la
   réintroduction de l'événement `AUDIT_CHALLENGED` de
   `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois` — qui butera sur les
   deux gardes citées au § 3 et demande donc un chemin de rattrapage explicite.
   Preuve rouge attendue : un test qui reproduit deux runs partant de bases
   différentes et échoue avant le correctif.
2. **Rendre la porte conditionnelle effective avant `gh pr merge --auto`.**
   Périmètre : vérifier mécaniquement les quatre preuves de la décision du
   2026-08-11 (dont « audit Cursor déposé ») au niveau du runner, la fusion
   étant refusée tant qu'une preuve manque. Question ouverte à trancher dans
   le brief, pas ici : comment une porte synchrone attend un auditeur
   asynchrone (attente explicite, ou audit pré-fusion obligatoire sur un
   sous-ensemble de chemins).
3. **Un seul parseur de verdicts, celui des lignes de tableau, pour
   l'enregistrement comme pour la décision.** Périmètre :
   `harness/audit_review.py` (le comptage qui part au registre) et son test.
   Preuve rouge attendue : sur le fichier de revue de cette PR, l'événement
   enregistré doit valoir 9 CONFIRMED / 1 PARTIAL et non 14/4/6/4.

## 11. Points à porter au propriétaire (gouvernance, hors compétence d'un audit)

- **Plancher de modèle pour le maillon `challenge`** (P1-3) : le fait
  technique est établi ; décider s'il faut un plancher écrit, et lequel,
  relève de l'arbitrage coût/qualité du propriétaire, pas d'un audit.
- **Permission du PAT `FORGE_BOT_PAT`** (P1-2) : la correction est un réglage
  de jeton, hors du dépôt ; seul le propriétaire peut la faire, et la
  description de PR l'oriente aujourd'hui vers le mauvais réglage.
- **Runner auto-hébergé `Reconcile local Hermes state`** : en file d'attente
  indéfinie, il rendra la preuve « CI verte » du brief 2 impossible à obtenir
  tant qu'il compte comme une porte.

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | zolty.systems — *The autonomy ladder in practice: letting agents commit, then merge* (2026-07-24) — échelle d'autonomie, portes non négociables déterministes au runner, « no `allow_failure: true`, no `continue-on-error: true` » — <https://blog.zolty.systems/posts/2026-07-24-autonomy-ladder-in-practice/> | 2026-08-13 |
| S2 | Zylos Research — *Agentic CI/CD: AI-Driven Delivery Pipelines and the Rise of CA/CD* (2026-05-12) — paliers de risque (fusion sur `main` = approbation requise), journaux d'audit immuables — <https://zylos.ai/research/2026-05-12-agentic-cicd-ai-driven-delivery-pipelines> | 2026-08-13 |
| S3 | spec-kitty (Priivacy-ai) — *git merge driver for `status.events.jsonl` — union append-only event log on conflict*, issue #569 — « any human resolving this conflict by taking one side loses the other side's events permanently » — <https://github.com/Priivacy-ai/spec-kitty/issues/569> | 2026-08-13 |
| S4 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30) — paliers Opus/Sonnet/Haiku, réserve du palier fort aux décisions à coût d'erreur élevé — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-08-13 |
| S5 | DEV Community — *Cheap Model First, Strong Model on Failure: Building an Auditable Two-Tier LLM Pipeline* — « never let a model decide whether its own output (or a peer's) was good enough » — <https://dev.to/codego_3211/cheap-model-first-strong-model-on-failure-building-an-auditable-two-tier-llm-pipeline-32c> | 2026-08-13 |
| S6 | shipwright (svenroth-ai) — `.gitattributes` / `gitattributes-union.template` — `merge=union` pour journaux JSONL append-only écrits en parallèle, honoré aussi par la fusion côté serveur GitHub — <https://github.com/svenroth-ai/shipwright/blob/main/.gitattributes> | 2026-08-13 |

## Commandes rejouées (récapitulatif)

```
gh pr view 63 --repo PLiagre/ForgeHistory --json …            # métadonnées, horodatages, fichiers
gh pr diff 63 --repo PLiagre/ForgeHistory                     # 130 lignes de diff, 1 fichier
gh api …/commits/25b3185…/check-runs                          # classification CI (§ 2)
gh api …/commits/25b3185…/status --jq '.state'                # -> pending
gh api …/contents/architecture/audit-ledger.jsonl | base64 -d # registre de master, 40 lignes
grep -c a4de4bb /tmp/ledger.jsonl                             # -> 0
gh run list --workflow pipeline-orchestrate.yml --limit 3      # run 31682710982 : failure
gh run view 31682710982 --log-failed                          # CONFLICT (content) sur le registre
gh run view 31681378615 --log                                 # modèle claude-sonnet-5, PAT refusé
git merge-base --is-ancestor a4de4bb… HEAD                    # -> vrai (commit audité par la revue)
.venv/bin/python <sonde double comptage + ordre des arêtes>    # § 8.1
.venv/bin/python harness/harness_audit.py                     # SCORE: 20/24
```

Budget d'appels : 46 appels outils pour cet audit (plafond du contrat : 60).
