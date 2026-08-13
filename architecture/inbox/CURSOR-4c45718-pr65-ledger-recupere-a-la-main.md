---
audit_id:                CURSOR-4c45718-pr65-ledger-recupere-a-la-main
auditor:                 cursor-cloud
target_branch:           forge/boucle-audits-post-pr60-ddda
target_commit:           4c4571892476603e41740f3d3ef52ca527ba5358
created_at:              2026-08-13T08:52:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #65 — tenue de registre de la boucle d'audit

Audit de la PR [#65](https://github.com/PLiagre/ForgeHistory/pull/65)
(11 fichiers, +843 / −0, base `master` = `97e8e7c`, tête `4c45718`).
Méthode : `architecture/review-guidelines.md` — six lentilles, sévérités
P0–P3, une preuve citée par constat. Rôle : auditeur en **lecture seule**.
Cet audit **n'instruit rien** et ne vaut pas décision
(`architecture/README.md`).

Toutes les mesures ci-dessous ont été rejouées par l'auditeur dans un arbre
de travail séparé (`git worktree` + `git apply` du diff de la PR), jamais
dans la branche auditée. Les sorties sont collées telles quelles au § 7.

## 0. Synthèse

| # | Sévérité | Constat en une phrase |
|---|---|---|
| 1 | **P1** | La cause de l'incident annoncée par la PR est **fausse** : les deux orchestrations n'ont pas été concurrentes (jobs 08:35:03–08:35:18 et 08:35:27–08:35:42, aucun recouvrement) ; le groupe `concurrency` a bien sérialisé. La cause réelle est que `actions/checkout` fixe l'arbre au SHA poussé, pas à la tête de `master`. |
| 2 | **P1** | La PR réécrit à la main une ligne de ledger dont elle sait les comptages **faux** (`REFUTED: 4` alors qu'aucun point de la revue n'est réfuté), dans un journal **append-only** : après fusion, la ligne ne pourra plus être retirée, seulement contredite. La « Réserve connue » de la PR ne la mentionne pas. |
| 3 | **P1** | `AUDIT_VERIFIED` est écrit **sans jamais consulter la CI**, alors que `architecture/README.md` le définit comme « Mergé, CI verte sur le SHA final » — et sur ce SHA final (`16ff5ac`) il y a un run rouge. Affirmation, pas mesure. |
| 4 | **P1** | Les deux gardes mécaniques de portée (`cursor-scope`, `check-and-automerge`) sont conditionnées au **préfixe de branche**, pas aux chemins : toutes deux sont `skipping` sur cette PR, qui touche pourtant `architecture/decisions/**` (« Propriétaire seul ») et le ledger. Renommer une branche désarme la garde. |
| 5 | **P2** | La section « Validation » de la PR affiche un état (`a4de4bb → AUDIT_APPROVED`, `a600532 → AUDIT_APPROVED`) que le contenu final de la PR **contredit** : les deux audits y sont `AUDIT_CONVERTED`. Preuve d'exécution périmée. |
| 6 | **P2** | Les lignes de ledger récupérées portent l'heure du rejeu (`08:40:11Z`) et **aucun pointeur** vers le run d'origine `31682710982` (08:35:36Z). La seule trace de la récupération vit dans la description de PR, qui n'est pas dans le dépôt. |
| 7 | **P2** | La PR ouvre en citant « une PR par objet » (constat 8 de `a4de4bb`, CONFIRMED et retenu) puis regroupe **trois objets** (récupération, clôture, deux conversions). Contradiction avec son propre argument d'ouverture. |
| 8 | **P3** | Les acteurs du ledger restent codés en dur (`"actor": "owner"` sur deux lignes émises par une machine). Déjà signalé (point 3 de `a4de4bb`), retenu, différé — rappelé seulement parce que les lignes de cette PR l'exhibent. |
| 9 | **P3** | Le ledger déclare 8 audits `AUDIT_ARCHIVED` alors que `architecture/archive/` ne contient que 3 paquets après cette PR. Divergence antérieure, ni causée ni aggravée ici. |
| 10 | **P3** | `harness/pipeline/ci-budget-ledger.jsonl` est **vide** : la boucle qui referme ici trois cycles n'a jamais enregistré un seul coût. |

**Aucun P0.** Rien dans ce diff ne casse un comportement mesuré du dépôt :
la suite de tests est verte, le schéma des audits est valide, les copies
d'archive sont identiques au bit près, et les deux affirmations
quantitatives centrales de la PR (« mêmes points retenus que le log CI »,
« aucun fichier hors `architecture/` et `harness/queue/briefs/` ») sont
**vraies** — vérifiées au § 4. Fabriquer un P0 ici serait du bruit.

## 1. Intention avant diff (lentille 1)

L'intention est lisible et honnête, ce qui est rare : la PR annonce
qu'elle est « de tenue de registre, volontairement séparée du prochain lot »
et cite le constat qui l'y oblige. Elle nomme sa réserve non corrigée au
lieu de la masquer. C'est le bon réflexe.

Le problème n'est pas l'intention mais **le récit de la cause**. Une PR de
récupération d'incident a deux livrables : l'état rétabli *et* le diagnostic
qui empêchera la récidive. Le premier est correct ; le second est faux
(constat 1) — et c'est ce diagnostic faux qui est légué à un brief futur.

## 2. Portes mécaniques d'abord (lentille 3)

Classification de la CI sur `4c45718` (`gh pr checks 65`, rejoué § 7) :
**15 vertes, 2 ignorées, 1 en attente**, aucune rouge.

| état | jobs |
|---|---|
| vertes | `tests` ×2, `sim-tests` ×2, `f0-demo` ×2, `schema` ×2, `actionlint` ×2, `gitleaks` ×2, `invoke-cursor-auditor` |
| ignorées | `cursor-scope` ×2, `check-and-automerge` |
| en attente | `Reconcile local Hermes state` (`hermes-observer`) — runner auto-hébergé, file indéfinie |

Le point à retenir n'est pas la couleur mais **quelles portes ne se sont pas
exécutées du tout** : les deux seules qui contrôlent la *portée* des chemins
sont ignorées (constat 4). Une PR peut donc écrire dans `decisions/` et dans
le ledger sans qu'aucune machine ne regarde. Le `mergeStateStatus` est
`UNSTABLE` uniquement à cause du job en attente.

## 3. Constats

### Constat 1 — la cause annoncée de l'incident est réfutée par les horaires (P1)

La PR écrit : « deux orchestrations concurrentes à 12 s d'écart […] la
sérialisation des orchestrations concurrentes reste incomplète (le
`concurrency group` n'a pas suffi ici) ».

Mesure (API GitHub, § 7) :

```
run 31682696284  orchestrate  started=2026-08-13T08:35:03Z  completed=2026-08-13T08:35:18Z  success
run 31682710982  orchestrate  started=2026-08-13T08:35:27Z  completed=2026-08-13T08:35:42Z  failure
```

Les deux jobs **ne se recouvrent pas** : le second démarre 9 secondes après
la fin du premier. Le groupe `concurrency: pipeline-orchestrate-master`
(`.github/workflows/pipeline-orchestrate.yml:51-53`) a fait exactement ce
qu'on lui demande. L'écart de 12 s cité par la PR est celui des dates de
*création* des runs (08:35:01 et 08:35:11), pas de leur exécution : le
second a attendu en file.

La cause réelle est ailleurs. `actions/checkout`
(`pipeline-orchestrate.yml:64-67`) est appelé **sans `ref:`** : sur un
évènement `push`, il place l'arbre sur `github.sha`, c'est-à-dire le commit
poussé (`9e35764`) — pas sur la tête courante de `master`. Or la tête
contenait déjà `6f1ebc6` (« pipeline-orchestrate: review_recorded »), la
ligne de ledger écrite par le run précédent. Le second run a donc ajouté sa
ligne **à la même position de fin de fichier**, sur une base périmée, et le
`git pull --rebase origin master` de la ligne 139 a produit :

```
CONFLICT (content): Merge conflict in architecture/audit-ledger.jsonl
error: could not apply 508ef8e... pipeline-orchestrate: review_recorded
```

Conséquence : **sérialiser les runs ne sert à rien** tant que chaque run
part d'un SHA figé par l'évènement. Le commentaire du workflow
(`pipeline-orchestrate.yml:135-138`) affirme précisément le contraire — « les
seuls autres écrivains du ledger sont les runs de CE workflow (sérialisés
par le groupe concurrency ci-dessus), donc le rebase ne peut pas conflicter
sur le ledger » — et cet incident le réfute. Deuxième réfutation du même
commentaire : **cette PR elle-même écrit le ledger depuis l'extérieur du
workflow**, ce qui ajoute une classe d'écrivain que l'invariant ignore.

Pourquoi P1 et pas P3 : la PR lègue ce diagnostic à un brief futur (« À
noter pour un brief futur »). Un brief écrit contre une cause fausse
renforcera la sérialisation — déjà correcte — et laissera la vraie cause en
place. Le défaut se reproduira à la prochaine double fusion.

### Constat 2 — une ligne connue fausse écrite dans un journal irréversible (P1)

La PR ajoute (`architecture/audit-ledger.jsonl`, ligne 41) :

```json
{"timestamp": "2026-08-13T08:40:11Z", "audit_id": "CURSOR-a4de4bb-...",
 "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "...",
 "verdicts": {"CONFIRMED": 14, "REFUTED": 4, "PARTIAL": 6, "NEEDS_OWNER": 4}}
```

Le document de revue référencé contient **10 points numérotés**, dont les
verdicts réels sont `9 CONFIRMED` et `1 PARTIAL` — **zéro REFUTED**, **zéro
NEEDS_OWNER** (mesure § 7). L'écart vient de
`harness/audit_review.py:127-134`, qui compte les occurrences des quatre
mots sur **tout le texte**, y compris la ligne de légende 11 (« Un verdict
par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER ») et la prose de la
ligne 9 du tableau.

Ce défaut n'est pas nouveau : c'est **exactement le point 9 de la revue que
cette ligne transcrit**, verdict `CONFIRMED`, retenu dans
`retained_points: [1..10]` et donc converti dans la graine du brief 013.

L'élément neuf, et la raison du P1 :

1. le ledger est **append-only** par contrat (`architecture/README.md`,
   règle d'intégrité 3). Après fusion, cette ligne ne pourra plus être
   retirée — seulement contredite par une autre ligne. C'est le seul artefact
   du dépôt où une erreur connue devient définitive ;
2. la PR a une section « Réserve connue, non corrigée ici » qui ne cite que
   le point 3 (acteurs codés en dur). Le comptage faux n'y figure pas, alors
   qu'il est visible dans la ligne même que la PR recopie.

Je note la tension honnêtement : corriger le comptage aurait cassé la
propriété que la PR revendique — « identiques au log CI » — laquelle est
vraie et vérifiée (§ 4). Il existait une troisième voie (consigner la ligne
*et* la signaler), qui n'a pas été prise. Je ne monte pas à P0 : la revue
enregistrée classe le même défaut en P3 et le propriétaire l'a déjà retenu
pour conversion ; monter de P3 à bloquant sans élément nouveau autre que
l'irréversibilité serait disproportionné.

### Constat 3 — `AUDIT_VERIFIED` affirme une CI verte que personne n'a regardée (P1)

`architecture/README.md` définit : « `AUDIT_VERIFIED` — Mergé, CI verte sur
le SHA final ». La PR ajoute cette transition pour `3b47ffe`
(`audit-ledger.jsonl`, lignes 38-39) :

```json
{"timestamp": "...08:40:26Z", "audit_id": "CURSOR-3b47ffe-...", "event": "AUDIT_IMPLEMENTED", "actor": "policy:auto"}
{"timestamp": "...08:40:26Z", "audit_id": "CURSOR-3b47ffe-...", "event": "AUDIT_VERIFIED",    "actor": "policy:auto"}
```

Deux choses à voir.

**a) Le code n'interroge jamais la CI.**
`harness/pipeline/orchestrator.py:224-229` :

```python
def handle_evaluateur_pass(payload: dict, *, ledger_path: Path, **_kw) -> dict:
    _require(payload, "audit_id")
    audit_id = payload["audit_id"]
    implemented = audit_ledger.append_event(audit_id, "AUDIT_IMPLEMENTED", ...)
    verified    = audit_ledger.append_event(audit_id, "AUDIT_VERIFIED", ...)
```

`AUDIT_VERIFIED` est émis **inconditionnellement, dans le même appel** que
`AUDIT_IMPLEMENTED`. Aucun SHA, aucun run, aucune vérification.

**b) Sur le SHA final, il y a un run rouge.** Le SHA final de `3b47ffe` est
`16ff5ac` (fusion de la PR #60). Le run `31682196140` y a échoué :

```
error: audit 'CURSOR-3b47ffe-pr57-monde-sans-faim' is AUDIT_CONVERTED, not AUDIT_CHALLENGED
##[error]Process completed with exit code 2
```

Je qualifie honnêtement cet échec : c'est un **refus fail-closed**
inoffensif d'un déclencheur mal résolu, pas une régression produit. Il ne
prouve pas que le travail est mauvais. Il prouve que la phrase « CI verte
sur le SHA final » n'a pas été évaluée — sinon elle aurait rendu faux.

**c) Les lignes sans preuve sont exactement celles qui affirment le succès.**
Toutes les autres transitions portent un pointeur : `AUDIT_CHALLENGED` →
`review` + `verdicts` ; `AUDIT_APPROVED` → `decision` + `retained_points` ;
`AUDIT_CONVERTED` → `briefs` ; `AUDIT_ARCHIVED` → `archive` + `bundled`.
Seules `AUDIT_IMPLEMENTED` et `AUDIT_VERIFIED` sont nues (6 occurrences dans
tout le ledger, § 7). C'est le motif « correction hallucinée » de la
lentille 6 : le succès affirmé est le seul non mesuré.

À décharge, et je l'ai vérifié : la preuve **existe**, elle n'est simplement
pas référencée. Le brief 012 a bien un `verdict.md` signé `forge-evaluateur`
et le gate rejoué rend `VERDICT: ACCEPT` (§ 7). `AUDIT_IMPLEMENTED` est donc
matériellement fondé ; c'est la traçabilité qui manque, pas le travail.

### Constat 4 — les gardes de portée dépendent du nom de branche, pas des chemins (P1)

Cette PR écrit dans `architecture/decisions/` — dossier dont
`architecture/README.md` dit « écrit par **Propriétaire seul** » — dans
`architecture/audit-ledger.jsonl` et dans `architecture/archive/`.

Les deux gardes qui existent pour ça ne se sont pas exécutées :

- `.github/workflows/audit-guard.yml:30` :
  `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')`
- `.github/workflows/merge-bot.yml:27` :
  `if: startsWith(github.head_ref, 'cursor/') || startsWith(github.head_ref, 'forge-bot/')`

La branche est `forge/boucle-audits-post-pr60-ddda` : aucun des deux
préfixes. Résultat observé dans `gh pr checks 65` : `cursor-scope` →
`skipping`, `check-and-automerge` → `skipping`.

Conséquence : la liste blanche de chemins de `merge-bot.yml:50`
(`architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/`)
n'est **jamais** confrontée à ce diff, qui la violerait entièrement. Une
garde qu'on désarme en renommant sa branche protège la convention de
nommage, pas le dépôt. Le contrat que *je* dois respecter
(`cursor-auditor.md` § Interdits : « tout chemin en dehors de
`architecture/inbox/**` ») est vérifié mécaniquement pour moi et pas pour
les autres producteurs — l'asymétrie est ce qui rend ce constat P1.

### Constat 5 — la « Validation » affichée est contredite par la PR elle-même (P2)

La PR affirme : « `audit_schema.py` et `audits.py list` cohérents après
opérations (3b47ffe → `AUDIT_ARCHIVED`, a4de4bb → `AUDIT_APPROVED`,
a600532 → `AUDIT_APPROVED`) ».

Rejeu sur la tête `4c45718` (§ 7) :

```
[AUDIT_CONVERTED]  (2)
  CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois  |  a4de4bb  |  ...
  CURSOR-a600532-fusion-sans-contre-audit           |  a600532  |  ...
[AUDIT_ARCHIVED]  (8)
  CURSOR-3b47ffe-pr57-monde-sans-faim               |  3b47ffe  |  ...
```

Les deux audits sont `AUDIT_CONVERTED`, pas `AUDIT_APPROVED` — la PR ajoute
elle-même les deux lignes `AUDIT_CONVERTED` (ledger, lignes 42-43). La
sortie citée est celle d'un état intermédiaire, recopiée sans être rejouée
en fin de travail. Le fond est sain (la cohérence est réelle) ; c'est la
*preuve* qui est périmée, et la lentille 2 porte précisément sur la preuve.

### Constat 6 — la récupération n'est pas traçable depuis le dépôt (P2)

Les lignes rétablies portent `"timestamp": "2026-08-13T08:40:11Z"`, l'heure
du rejeu. La transition réelle a été calculée à `08:35:36Z` par le run
`31682710982`. Aucune ligne ne porte de champ pointant vers ce run.

Le ledger est censé être la mémoire de la boucle. Ici, la chronologie qu'il
raconte est décalée de cinq minutes et l'existence même d'un incident n'y
figure pas : elle ne vit que dans la description de la PR, hors du dépôt.
Un lecteur futur du seul ledger ne peut ni voir l'incident, ni distinguer
une ligne écrite par la machine d'une ligne retapée à la main.

### Constat 7 — trois objets dans une PR qui plaide pour un objet par PR (P2)

Première ligne de la PR : « PR de tenue de registre, volontairement séparée
du prochain lot (réponse au constat 8 de l'audit `CURSOR-a4de4bb` : une PR
par objet) ». Puis trois objets numérotés : récupération d'une décision
perdue, clôture d'un cycle, conversion de deux audits.

Chiffres réels : 11 fichiers, +843. Mais j'ai vérifié que **704 de ces 843
lignes sont des copies au bit près** de fichiers déjà présents
(`diff -q` : les trois fichiers archivés sont identiques à leurs sources).
Le contenu réellement neuf est de ~139 lignes, dont ~96 de gabarits
`<<TODO>>`. La mesure honnête est donc : **7 lignes de ledger + 18 lignes de
décision**, tout le reste étant mécanique.

Le volume n'est donc pas le problème (lentille 5 : « ce qu'une relecture
honnête peut connecter à l'intention »). Le problème est le couplage : la
récupération d'un incident et deux conversions de routine partagent un
verdict de fusion unique. Si un seul des trois est refusé, les trois
attendent. P2, pas plus.

### Constat 8 — acteurs de ledger codés en dur, exhibés par cette PR (P3)

Les lignes 40 et 42-43 ajoutées portent `"actor": "owner"` pour
`AUDIT_ARCHIVED` et `AUDIT_CONVERTED`, alors qu'aucun humain n'a agi — la
PR dit elle-même que l'orchestrateur a été rejoué. Le
`README` promet pourtant qu'« on peut prouver mécaniquement **qui** a écrit
**quoi** ».

C'est le point 3 de `a4de4bb`, CONFIRMED, retenu, différé au brief 014, et
la PR le consigne explicitement. Je ne le re-plaide pas — le signaler à
nouveau au-delà de P3 serait le « rubber-stamping inverse » que les
guidelines interdisent. Élément neuf minimal : les lignes de cette PR en
sont trois nouvelles instances.

### Constat 9 — 8 audits `ARCHIVED` au ledger, 3 paquets sur le disque (P3)

`audits.py list` classe 8 audits en `AUDIT_ARCHIVED` (dont `198cfd9`,
`6231186`, `bbe6da5`, `POSTMERGE-42cb054`) alors que
`architecture/archive/` n'en contient que 3 après cette PR. Divergence
**antérieure** à ce diff, ni causée ni aggravée ici ; notée parce qu'elle
affaiblit la même propriété que le constat 3 (l'état déclaré n'est pas
l'état constatable).

### Constat 10 — la boucle ne mesure pas son propre coût (P3)

`harness/pipeline/ci-budget-ledger.jsonl` est **vide** (1 ligne blanche).
Cette PR referme trois cycles, chacun ayant consommé au moins une session
d'agent (auditeur, contre-auditeur, orchestrateur, plus le rejeu manuel).
Aucun coût n'est enregistré.

Un audit antérieur, `CURSOR-6231186-execution-budgets`, est archivé sur un
sujet voisin ; je ne le rejoue pas. Élément neuf : depuis que la boucle
tourne sans humain (ADR-0006), le nombre d'invocations par fusion a
augmenté, et la seule ligne de mesure prévue pour ça n'a jamais été
alimentée. La littérature 2026 est constante sur ce point : un plafond de
jetons doit être **imposé avant l'appel**, pas constaté sur la facture
[S3, S6].

## 4. Ce qui tient (cadrage adverse, résultat négatif)

J'ai cherché où les affirmations de la PR sont fausses. Ces quatre-là sont
**vraies** et je les consigne comme telles :

1. **« Points retenus identiques au log CI » : vrai.** Le log du run
   `31682710982` (ligne 181) contient exactement
   `"verdicts": {"CONFIRMED": 14, "REFUTED": 4, "PARTIAL": 6, "NEEDS_OWNER": 4}`
   et `"retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]` — identiques au
   bit près aux lignes ajoutées. Le rejeu manuel est fidèle.
2. **« Aucun fichier hors `architecture/` et `harness/queue/briefs/` » :
   vrai.** Les 11 chemins du diff sont dans ces deux arbres.
3. **Les copies d'archive sont exactes.** `diff -q` entre `inbox/`,
   `reviews/`, `decisions/` et le paquet d'archive : identiques. Le
   « copier, jamais déplacer » est documenté et assumé
   (`harness/audit_archive.py:9-13`) ; ce n'est pas une duplication
   accidentelle et je ne la compte pas comme défaut.
4. **Les graines de brief `<<TODO>>` ne sont pas un danger.** J'ai cherché
   si un brief vide pouvait être consommé comme une instruction : non.
   `orchestrator.py:200-211` refuse de lancer `/forge-run` et met le
   Planificateur en file (« needs claude-planificateur to fill its
   `<<TODO>>` markers before /forge-run can start »), et
   `harness/tests/test_audit_convert.py:94` verrouille le fait que la spec
   n'est pas fabriquée. Le gate rejoué sur le brief 013 rend `REJECT` pour
   la bonne raison (`verdict.md missing`). Conception correcte.

Enfin, les portes mécaniques du dépôt sont vertes sur la tête auditée :
`314 passed, 16 skipped` et `All 28 audit(s) valid` (§ 7).

## 5. Limite de cet audit (à lire avant de s'en servir)

- Je n'ai pas pu cloner la branche auditée (aucun remote configuré dans
  l'environnement) : le diff a été obtenu par `gh pr diff 65` puis appliqué
  sur `master` local (`72a69e7`) dans un worktree séparé. `master` a avancé
  d'un commit (`72a69e7`, PR #64) depuis la base de la PR (`97e8e7c`) ; ce
  commit ne touche que `architecture/inbox/`, donc aucune de mes mesures
  n'en dépend. Si la PR est rebasée, tout ce qui précède est à revérifier.
- Le job `Reconcile local Hermes state` était encore en attente au moment
  de l'audit : sa couleur finale n'est pas connue de moi.
- Le constat 1 s'appuie sur les horaires renvoyés par l'API GitHub
  (`started_at` / `completed_at` du job). Je n'ai pas d'accès à
  l'ordonnanceur ; une autre explication du non-recouvrement reste
  concevable, mais elle devrait aussi expliquer le conflit sur un fichier
  qu'un seul writer touchait à la fois.
- Je n'ai pas rejoué le raisonnement du contre-audit lui-même : je vérifie
  que le ledger décrit fidèlement le document, pas que le document a raison.

## 6. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

Ce sont des **propositions**. Aucune n'est autorisée, aucune n'instruit quoi
que ce soit ; seuls le contre-audit puis la décision sont compétents.

1. **Écrire le ledger sur la tête de `master`, pas sur le SHA de
   l'évènement.** Couvre le constat 1. Le cœur du problème est
   `actions/checkout` sans `ref:` combiné à un `.jsonl` en append, que git
   traite comme un conflit de contenu là où l'union est la seule résolution
   sensée. Une preuve rejouable existerait : un test qui rejoue deux
   appends sur bases divergentes et échoue avant, passe après.
2. **Aucune transition d'état sans son pointeur de preuve.** Couvre les
   constats 3, 6 et 9. `AUDIT_IMPLEMENTED` et `AUDIT_VERIFIED` sont les
   deux seuls évènements nus du ledger, et ce sont ceux qui affirment le
   succès ; la récupération manuelle du constat 6 pose le même besoin
   (d'où vient cette ligne ?).
3. **Indexer les gardes de portée sur les chemins, pas sur le préfixe de
   branche.** Couvre le constat 4. Les listes blanches existent déjà ; ce
   qui manque est qu'elles s'exécutent pour toute PR.

Le comptage faux du constat 2 est **déjà** dans le périmètre de provenance
du brief 013 (point 9 de `a4de4bb`, retenu) : je ne propose pas de quatrième
brief pour lui.

## 7. Commandes rejouées (sorties collées)

Environnement : Linux, `.venv/bin/python` (voir `AGENTS.md`). Worktree
séparé `/tmp/pr65wt`, diff appliqué par `git apply --index /tmp/pr65.diff`.

```console
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31682710982/jobs -q '.jobs[]|"\(.name) started=\(.started_at) completed=\(.completed_at)"'
orchestrate started=2026-08-13T08:35:27Z completed=2026-08-13T08:35:42Z
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31682696284/jobs -q '.jobs[]|"\(.name) started=\(.started_at) completed=\(.completed_at)"'
orchestrate started=2026-08-13T08:35:03Z completed=2026-08-13T08:35:18Z
```

```console
$ gh run view 31682710982 --repo PLiagre/ForgeHistory --log        # extraits
...Run orchestrator...  {"action": "decide_auto", "challenge_record": {..., "verdicts":
  {"CONFIRMED": 14, "REFUTED": 4, "PARTIAL": 6, "NEEDS_OWNER": 4}}, "record": {...,
  "retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]}, ...}
...Commit ledger...  CONFLICT (content): Merge conflict in architecture/audit-ledger.jsonl
...Commit ledger...  error: could not apply 508ef8e... pipeline-orchestrate: review_recorded
```

```console
$ gh run view 31682196140 --repo PLiagre/ForgeHistory --log-failed  # SHA 16ff5ac
error: audit 'CURSOR-3b47ffe-pr57-monde-sans-faim' is AUDIT_CONVERTED, not AUDIT_CHALLENGED;
only a challenged audit can be decided (--policy auto included)
##[error]Process completed with exit code 2
```

Comptage réel des verdicts du document de revue, avec le code du dépôt :

```console
$ .venv/bin/python -c "
import sys,re; sys.path.insert(0,'harness'); import audit_review
from collections import Counter
t=open('architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md',encoding='utf-8').read()
print('parse_verdicts (ce qui part au ledger) :', audit_review.parse_verdicts(t))
rows=[l for l in t.splitlines() if re.match(r'^\|\s*\d+\s*\|',l)]
c=Counter(l.split('|')[3].strip().replace('*','') for l in rows)
print('verdicts reels, colonne 3 du tableau  :', dict(c), '/ points:', len(rows))"
parse_verdicts (ce qui part au ledger) : {'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}
verdicts reels, colonne 3 du tableau  : {'CONFIRMED': 9, 'PARTIAL': 1} / points: 10
```

Portes du dépôt sur la tête auditée :

```console
$ .venv/bin/python harness/audit_schema.py        # dans /tmp/pr65wt
All 28 audit(s) valid.
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 17.03s
$ .venv/bin/python harness/audits.py list         # extrait
[AUDIT_CONVERTED]  (2)
  CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois  |  a4de4bb  |  2026-08-13T08:10:02Z  |  cursor-cloud
  CURSOR-a600532-fusion-sans-contre-audit           |  a600532  |  2026-08-13T06:30:00Z  |  cursor-cloud
[AUDIT_ARCHIVED]  (8)
  ... CURSOR-3b47ffe-pr57-monde-sans-faim  |  3b47ffe  |  2026-08-12T17:15:00Z  |  cursor-cloud
```

```console
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
[FAIL] verdict_numbers_traceable: verdict.md missing
[FAIL] verdict_is_not_self_authored: Author frontmatter missing on generator-log.md or verdict.md
VERDICT: REJECT
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules
VERDICT: ACCEPT
```

Fidélité des copies d'archive et volume réel du diff :

```console
$ diff -q architecture/inbox/CURSOR-3b47ffe-....md    architecture/archive/CURSOR-3b47ffe-.../CURSOR-3b47ffe-....md   && echo IDENTIQUE
IDENTIQUE
$ diff -q architecture/reviews/CLAUDE-CURSOR-3b47ffe-....md   .../CLAUDE-CURSOR-3b47ffe-....md   && echo IDENTIQUE
IDENTIQUE
$ diff -q architecture/decisions/DECISION-CURSOR-3b47ffe-....md .../DECISION-CURSOR-3b47ffe-....md && echo IDENTIQUE
IDENTIQUE
$ git diff --cached --stat | tail -1
 11 files changed, 843 insertions(+)
```

```console
$ grep -cE "AUDIT_IMPLEMENTED|AUDIT_VERIFIED" architecture/audit-ledger.jsonl
6      # 3 paires, aucune ne porte de SHA, de run CI ni de chemin de verdict
$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl      # une ligne blanche : aucun coût enregistré
```

```console
$ gh pr checks 65 --repo PLiagre/ForgeHistory      # 15 pass, 2 skipping, 1 pending
cursor-scope          skipping   0
check-and-automerge   skipping   0
Reconcile local Hermes state   pending  0
tests / sim-tests / f0-demo / schema / actionlint / gitleaks / invoke-cursor-auditor   pass
```

## 8. Risques par sévérité

| Sévérité | Constats | Risque si rien n'est fait |
|---|---|---|
| **P0** | — | aucun constat bloquant. |
| **P1** | 1, 2, 3, 4 | le même incident de ledger se reproduira à la prochaine double fusion, corrigé par un brief visant la mauvaise cause ; une ligne fausse devient permanente dans un journal irréversible ; « vérifié » continue de vouloir dire « personne n'a regardé » ; les gardes de portée restent contournables par un nom de branche. |
| **P2** | 5, 6, 7 | les preuves d'exécution des PR peuvent être périmées sans que rien ne le signale ; les incidents ne laissent pas de trace dans le dépôt ; les objets restent couplés dans un verdict de fusion unique. |
| **P3** | 8, 9, 10 | l'attribution des actes au ledger reste fausse ; l'état déclaré diverge de l'état constatable ; le coût de la boucle autonome reste inconnu. |

## 9. Sources externes

| # | source | date de la source | consulté le |
|---|---|---|---|
| S1 | Prefactor — *Audit Trails in CI/CD: Best Practices for AI Agents* (identifiants stables `agent_id` / `pipeline_run_id` injectés dans **chaque** enregistrement ; séquence d'évènements sans trou) — <https://prefactor.tech/blog/audit-trails-in-ci-cd-best-practices-for-ai-agents> | 2026 | 2026-08-13 |
| S2 | bitbox.cloud — *Autonomous CI/CD: When AI Runs Your Pipelines — Risks and Safeguards* (journaux append-only + attestations signées ; « auditability gaps » et « silent pipeline drift » listés comme risques propres aux pipelines autonomes) — <https://bitbox.cloud/autonomous-ci-cd-when-ai-runs-your-pipelines-risks-and-safeg> | 2026 | 2026-08-13 |
| S3 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (plafonds de jetons imposés à la couche d'orchestration ; sans quoi les boucles d'agents drainent le budget « undetected ») — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-06-30 | 2026-08-13 |
| S4 | Lyu et al. — *CoAgent: Concurrency Control for Multi-Agent Systems*, arXiv:2606.15376 (sérialiser les acteurs ne suffit pas : sans **pré-ordre** fixant une vue stable comme prémisse, les écritures concurrentes restent non sérialisables) — <https://arxiv.org/html/2606.15376> | 2026-06 | 2026-08-13 |
| S5 | Augment Code — *How to Run a Multi-Agent Coding Workspace (2026)* (fusion **séquentielle** avec rebase de chaque branche sur le `main` le plus récent après chaque intégration — exactement la discipline que `actions/checkout` sur `github.sha` casse) — <https://www.augmentcode.com/guides/how-to-run-a-multi-agent-coding-workspace> | 2026 | 2026-08-13 |
| S6 | tianpan.co — *Token Budget as Architecture Constraint* (plafonds par trace, détection d'anomalie de dépense : « a 2-sigma deviation should trigger investigation, not silent continuation ») — <https://tianpan.co/blog/2026-04-13-token-budget-as-architecture-constraint> | 2026-04-13 | 2026-08-13 |

Les trois thèmes de veille exigés par le contrat
(`architecture/agents/cursor-auditor.md` § Preuve de fin) sont couverts :
pipeline de développement autonome (S1, S2), orchestration d'agents en CI
(S4, S5), budget de jetons des agents (S3, S6). S4 et S5 fondent le
constat 1 ; S1 et S2 fondent les constats 3 et 6 ; S3 et S6 fondent le
constat 10.

---

Fin de l'audit. Statut `PROPOSED` : aucun point ci-dessus n'est une
instruction, aucun n'autorise une implémentation, aucun n'est
pré-approuvé. Le contre-audit (`architecture/reviews/`), puis la décision
(`architecture/decisions/` ou la politique automatique d'ADR-0006),
restent seuls compétents.
