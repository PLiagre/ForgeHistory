---
audit_id:                CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons
auditor:                 cursor-cloud
target_branch:           master
target_commit:           cd1dcd210441d220168cbaacf620bf90288f3e55
created_at:              2026-08-12T13:36:54Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---
# Audit du merge cd1dcd2 — `FORGE_BOT_PAT` : les fusions de bots redéclenchent la CI

Audit post-fusion du rôle `cursor-auditor`
(`architecture/agents/cursor-auditor.md`), avec `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`) en compagnon de session : sa veille
est la section « Veille externe » ci-dessous, dans ce même fichier, comme son
contrat le prévoit.

**Un audit n'instruit rien.** Ce fichier est une *entrée* pour
`claude-challenger` puis pour le propriétaire (`architecture/README.md`,
ADR-0005 / ADR-0006). Aucun constat ci-dessous n'est une commande, aucune
formulation n'est un ordre, et les trois flags `*_authorized` du frontmatter
valent `false`.

## Résumé en une page

Le merge est petit — quatre fichiers, +37/-3 — et son intention est limpide :
remplacer le jeton intégré `GITHUB_TOKEN` par un jeton personnel du
propriétaire (`FORGE_BOT_PAT`) dans trois workflows, avec repli automatique
sur l'ancien jeton si le secret est absent. La raison invoquée est exacte et
vérifiable : GitHub refuse volontairement de déclencher un workflow à partir
d'un événement causé par `GITHUB_TOKEN` (source S1). Sans PAT, une PR ouverte
par un bot reste bloquée sur un bouton « Approve and run », et une fusion
faite par un bot ne déclenche aucun workflow `push` en aval.

Le problème n'est pas que le diff se trompe. Il est que **ce blocage était le
seul frein qui empêchait la boucle de s'auto-alimenter**, et que le diff le
retire sans rien mettre à la place. GitHub écrit lui-même, dans la page qui
justifie ce diff, la phrase qui le tempère : « pour minimiser vos coûts
GitHub Actions, assurez-vous de ne pas créer d'exécutions récursives ou
non voulues » (S1).

Or la chaîne est bouclée par construction : une PR d'audit fusionnée pousse un
fichier `architecture/inbox/*.md`, ce qui déclenche `pipeline-challenge.yml`,
qui ouvre une PR `forge-bot/*` **non brouillon**, que `pipeline-audit.yml`
critique (il n'exclut que les brouillons et les branches `cursor/*`), ce qui
produit un nouvel audit dans `inbox/`, que le merge-bot fusionne, ce qui
repousse un `inbox/*.md`… Chaque tour dépense un agent Cursor Cloud **et** un
appel Claude. Et `pipeline-audit.yml` est précisément le seul des trois
workflows d'invocation qui n'a **ni** contrôle `mode: manual`, **ni**
`ci_budget_guard precheck`, **ni** plafond `--max-budget-usd`.

Par ailleurs, la CI du SHA audité est **rouge** : `hermes-dashboard /
regenerate` échoue. Le diff a converti trois des cinq endroits où un bot
écrit dans le dépôt ; les deux restants poussent encore directement sur un
`master` protégé, et l'un des deux échoue déjà.

Enfin, le mécanisme n'a jamais été exercé : la PR #45 qui l'introduit a été
**fusionnée à la main par le propriétaire** (`autoMergeRequest: null`), donc
ni l'auto-fusion par le PAT, ni le déclenchement en aval qu'elle promet
n'ont été observés une seule fois.

| sévérité | nombre | objet |
|---|---|---|
| P0 | 1 | la boucle audit ↔ contre-audit devient auto-alimentée, et le seul maillon sans plafond de dépense est celui qui lance Cursor |
| P1 | 3 | PAT du propriétaire lisible par un job déclenché sur une branche de bot ; CI rouge (`hermes-dashboard`) et conversion des jetons faite à 3/5 ; mécanisme jamais exercé, aucun test |
| P2 | 3 | repli de jeton silencieux ; expiration du PAT non tracée ; la doc décrit une couverture qu'elle n'a pas |
| P3 | 2 | file de fusion (merge queue) vs `--auto` au volume actuel ; App GitHub plutôt que PAT personnel |

## Ce que le merge change

`git diff --stat 3807764..cd1dcd2` (rejoué, sortie en fin de document) :

```
 .github/workflows/merge-bot.yml          | 10 +++++++++-
 .github/workflows/pipeline-challenge.yml | 10 +++++++++-
 .github/workflows/pipeline-forge-run.yml |  7 ++++++-
 docs/rules/full-auto-pipeline.md         | 13 +++++++++++++
 4 files changed, 37 insertions(+), 3 deletions(-)
```

1. **`merge-bot.yml:68`** — `GH_TOKEN: ${{ secrets.FORGE_BOT_PAT ||
   secrets.GITHUB_TOKEN }}` sur l'étape `gh pr merge --auto`.
2. **`pipeline-challenge.yml:52` et `:174`** — le PAT sert au `checkout` (donc
   au `git push` de la branche) et au `gh pr create`.
3. **`pipeline-forge-run.yml:58` et `:222`** — même chose.
4. **`docs/rules/full-auto-pipeline.md`** — treize lignes qui déclarent le
   secret, ses permissions (Contents et Pull requests en lecture/écriture,
   dépôt unique) et la raison de son existence.

Lentille 1 du guide de critique (intention avant diff) : l'intention est
écrite, exacte, et le diff y correspond exactement. Le désaccord porte
uniquement sur ce que l'intention entraîne et que le diff ne traite pas.

## CI du commit audité — **rouge**

`gh api .../commits/cd1dcd2.../check-runs`, rejoué (sortie complète en fin de
document) :

| job | conclusion |
|---|---|
| `tests` (harness-ci) | success |
| `f0-demo` (harness-ci) | success |
| `schema` (audit-guard) | success |
| `cursor-scope` (audit-guard) | skipped (normal : pas une PR) |
| `invoke-cursor-auditor` (pipeline-audit) | success |
| `actionlint`, `gitleaks` (security) | success |
| `escalate-on-failure` | skipped |
| `Reconcile local Hermes state` ×5 (hermes-observer) | success |
| **`regenerate` (hermes-dashboard)** | **failure** |

Le dépôt lui-même est sain : `309 passed, 16 skipped` sur
`harness/tests/`, et `harness/audit_schema.py` valide les 14 audits présents.
La rougeur vient d'un seul job, traité en P1-b.

## Constats

### P0 — la boucle s'auto-alimente désormais, et l'appel le plus cher est le seul sans plafond

**Preuve, en trois pièces.**

*Pièce 1 — la boucle est fermée par construction.* Aucune de ces quatre
affirmations n'a d'exception dans le dépôt au SHA audité :

- `pipeline-challenge.yml:22-26` se déclenche sur `push` vers `master` filtré
  sur `paths: architecture/inbox/*.md`.
- `pipeline-challenge.yml:189` ouvre sa PR de revue sans `--draft`, sur une
  branche `forge-bot/review-…`.
- `pipeline-audit.yml:43` n'écarte que les brouillons et les branches
  `cursor/*` : `if: github.event_name != 'pull_request' ||
  (github.event.pull_request.draft == false &&
  !startsWith(github.event.pull_request.head.ref, 'cursor/'))`. Une PR
  `forge-bot/*` est donc critiquée.
- Le livrable de cette critique est un audit dans `architecture/inbox/`, que
  `merge-bot.yml:50` autorise à l'auto-fusion.

D'où le cycle : audit fusionné → `push` sur `inbox/*.md` → contre-audit Claude
→ PR `forge-bot/*` → critique Cursor → nouvel audit → fusion → `push` sur
`inbox/*.md` → … Le garde anti-boucle de `pipeline-audit.yml:55-74` ne coupe
pas ce cycle : il ne regarde que les `push`, et le rebouclage passe par
l'événement `pull_request`.

*Pièce 2 — c'est ce merge qui referme le cycle.* Avant lui, une fusion faite
par `GITHUB_TOKEN` ne produisait aucun événement `push` (S1, documentation
GitHub : « lorsqu'une exécution de workflow pousse du code avec le
`GITHUB_TOKEN` du dépôt, aucun nouveau workflow ne s'exécute »). La chaîne
calait donc à chaque tour, et c'est le propriétaire qui la relançait en
fusionnant à la main. Le but explicite du diff, écrit dans
`merge-bot.yml:62-64`, est de supprimer exactement ce comportement.

*Pièce 3 — la cadence mesurée, avec le frein humain encore en place.*
`gh pr list --state all --limit 25` : 24 pull requests entre 08:01 et 13:26 le
2026-08-12, soit environ une toutes les 13 minutes, et 6 restent ouvertes
(#31, #40, #41, #42, #43, #44). Cette cadence a été atteinte *alors que*
chaque fusion demandait un clic. Le diff retire le clic.

**Ce qui aggrave le constat** : les trois freins déclarés
(`docs/rules/full-auto-pipeline.md`) ne sont pas répartis également.
`rg -n "ci_budget_guard|max-budget-usd|mode.*manual"` sur
`.github/workflows/` ne renvoie **aucune** ligne pour `pipeline-audit.yml` :

| workflow | label `pipeline/pause` | `mode: manual` runtime | `ci_budget_guard precheck` | `--max-budget-usd` |
|---|---|---|---|---|
| `pipeline-forge-run.yml` | oui (l.94) | oui (l.105-119) | oui (l.126) | oui (l.197) |
| `pipeline-challenge.yml` | oui (l.60) | oui (l.71-85) | oui (l.115) | oui (l.155) |
| **`pipeline-audit.yml`** | oui (l.76-89) | **non** | **non** | **non** |

`harness/pipeline/config.yaml` déclare `mode: full_auto` — la posture est
active, pas théorique. Et `harness/pipeline/ci-budget-ledger.jsonl` fait
**1 octet** : aucune dépense n'y a jamais été enregistrée, alors que la
journée compte une dizaine d'invocations. Le plafond mensuel ne mesure donc
rien aujourd'hui, y compris pour les deux workflows qui l'appellent.

Le seul frein qui reste réellement en travers du chemin est le label
`pipeline/pause`, c'est-à-dire une action humaine — dans un dispositif dont
le nom est *full-auto*.

**Pourquoi P0 et non P1.** Le guide définit P0 comme « bloque la fusion » ;
le commit étant déjà fusionné, la lecture équivalente est : ceci demande une
réponse **avant que la boucle ne tourne sans surveillance**. Le mode de
défaillance n'est pas une régression fonctionnelle mais une dépense non
bornée sur trois abonnements (Cursor, Claude, Codex), et le dépôt n'a
aujourd'hui aucun compteur capable de la constater après coup.

### P1-a — le PAT du propriétaire est lisible par un job déclenché depuis une branche de bot

`merge-bot.yml:16-18` se déclenche sur `pull_request`. La documentation
GitHub (S2) est explicite sur ce point : « l'événement `pull_request` exécute
le fichier de workflow **à partir du commit de fusion de la pull request** ».
Pour une PR issue d'un fork, les secrets sont retirés — mais pour une PR
issue d'une branche **du dépôt lui-même**, ils sont fournis. Le changelog
GitHub de juin 2026 (S3) le redit en une phrase : « les pull requests du même
dépôt ne sont pas concernées » par le durcissement récent.

Conséquence : une branche `cursor/*` ou `forge-bot/*` qui modifie
`merge-bot.yml` fait tourner **sa propre version** du workflow, avec
`secrets.FORGE_BOT_PAT` dans l'environnement. La liste de refus de
`merge-bot.yml:43` protège contre l'auto-**fusion** d'un changement de
workflow ; elle ne protège pas contre son **exécution**, qui a lieu avant.
La note de recherche CSA sur la campagne prt-scan (S4) décrit précisément
cette étape d'injection de workflow comme le maillon qui transforme un jeton
volé en exfiltration de tous les secrets.

Ce qui rend le constat concret ici plutôt que théorique : les acteurs qui
disposent du droit de pousser une branche `cursor/*` ou `forge-bot/*` sont
des agents (Cursor Cloud, Claude headless, Codex), et ces agents lisent du
texte non maîtrisé — corps de PR, résultats de recherche web, contenu de
fichiers audités. Le dépôt fait donc reposer un identifiant durable du
propriétaire sur la fiabilité d'agents exposés à l'injection de consigne.

**Écart créé par ce diff, précisément.** Avant : `GITHUB_TOKEN` éphémère,
`permissions: contents: read, pull-requests: write`, périmé à la fin du run.
Après : PAT du propriétaire, `Contents` et `Pull requests` en
lecture/écriture, longue durée de vie, réutilisable ailleurs.

### P1-b — CI rouge : deux des cinq points d'écriture n'ont pas été convertis, et l'un échoue déjà

`gh run view 31601868896 --log-failed` sur le SHA audité :

```
remote: error: GH006: Protected branch update failed for refs/heads/master.
remote: - 5 of 5 required status checks are expected.
 ! [remote rejected] master -> master (protected branch hook declined)
error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
```

`hermes-dashboard.yml:103` pousse directement sur `master`, avec le jeton par
défaut, et la protection de branche le refuse. `hermes/DASHBOARD.md` est, par
ADR-0011, la console du propriétaire : elle cesse silencieusement d'être à
jour, au moment précis où le volume d'événements à suivre augmente.

`rg -n "git push|gh pr create|gh pr merge" .github/workflows/*.yml` donne
cinq points d'écriture ; le diff en a converti trois :

| point d'écriture | jeton après le merge |
|---|---|
| `merge-bot.yml:71` (`gh pr merge --auto`) | `FORGE_BOT_PAT` ✔ |
| `pipeline-challenge.yml:188-189` | `FORGE_BOT_PAT` ✔ |
| `pipeline-forge-run.yml:237-238` | `FORGE_BOT_PAT` ✔ |
| **`hermes-dashboard.yml:103`** (`git push origin master`) | `GITHUB_TOKEN` ✘ — **rouge** |
| **`pipeline-orchestrate.yml:117`** (`git push`) | `GITHUB_TOKEN` ✘ — même schéma, pas encore observé |

Le second cas n'a pas encore échoué dans les exécutions consultées, mais il
pousse sur la même branche protégée, par le même chemin, avec le même jeton :
il y a tout lieu de penser qu'il échouera de la même façon. Ce n'est pas une
critique du choix de jeton en soi — un PAT ne suffirait d'ailleurs pas
forcément à contourner « 5 of 5 required status checks » — mais du fait que
la question « comment un bot écrit-il sur un `master` protégé » a été tranchée
pour trois workflows et laissée ouverte pour deux, dont un déjà rouge.

### P1-c — le mécanisme n'a jamais été exercé, et rien ne le vérifie

Lentille 2 du guide (preuve d'exécution, pas d'affirmation). Le diff affirme
deux comportements. Aucun des deux n'est démontré :

```
$ gh pr view 45 --json autoMergeRequest,mergedBy,headRefName
{"autoMergeRequest": null,
 "mergedBy": {"login": "PLiagre", "name": "Pierre-Edouard Liagre"},
 "headRefName": "cursor/forge-bot-pat-39f4"}
```

`autoMergeRequest: null` : l'auto-fusion n'a pas été activée sur la PR qui
introduit l'auto-fusion. Elle a été fusionnée à la main. La PR qui déclare
« la boucle tourne sans surveillance » est donc la preuve que, ce jour-là,
elle ne tournait pas sans surveillance.

Et aucune porte mécanique ne parle de ce changement :

```
$ rg -ln "FORGE_BOT_PAT" --glob '!.github/**' .
./docs/rules/full-auto-pipeline.md
```

`FORGE_BOT_PAT` n'apparaît que dans les workflows et dans une page de règles.
Aucun test sous `harness/tests/` (309 tests) n'affirme quoi que ce soit sur le
câblage des jetons — ni « le PAT n'est pas exposé à un job déclenché par
`pull_request` », ni « tout point d'écriture de bot utilise le même jeton ».
C'est cohérent avec le fait que le diff soit passé de 3/5 sans que rien ne le
signale (P1-b) : il n'existait aucun compteur pour le voir.

### P2 — le repli de jeton est muet

`secrets.FORGE_BOT_PAT || secrets.GITHUB_TOKEN` fonctionne, mais rien ne
journalise lequel des deux a servi. Si le PAT est révoqué, expiré, ou mal
nommé, le dispositif retombe exactement dans l'état d'avant — PR en attente
d'un « Approve and run », fusion sans déclenchement aval — et le symptôme est
une absence : des workflows qui ne démarrent pas. C'est le mode de panne le
plus coûteux à diagnostiquer.

Cela détonne avec la discipline établie du dépôt : chaque identifiant absent
produit une dérogation explicite en `::warning::`
(`pipeline-audit.yml:102`, `pipeline-forge-run.yml:156`,
`pipeline-challenge.yml:134`). Le nouveau secret est le seul à se replier en
silence.

### P2 — l'expiration du PAT n'est consignée nulle part

`docs/rules/full-auto-pipeline.md` décrit les permissions du PAT et son
périmètre, mais ni sa date d'expiration, ni qui le renouvelle, ni comment on
s'aperçoit qu'il est mort. Un PAT fine-grained GitHub a une durée de vie
bornée. Combiné au repli muet ci-dessus, l'expiration produit une panne
différée, silencieuse, et sans propriétaire désigné.

### P2 — la documentation décrit une couverture qu'elle n'a pas

Les lignes ajoutées disent que le PAT est utilisé « par
`pipeline-challenge.yml` et `pipeline-forge-run.yml` […] et par
`merge-bot.yml` ». C'est littéralement exact, mais la phrase qui suit —
« avec le PAT les événements sont portés par le propriétaire et la boucle
tourne sans surveillance » — ne l'est pas : `hermes-dashboard.yml` et
`pipeline-orchestrate.yml` restent en dehors (P1-b). Un lecteur de cette page
en conclura que le sujet est clos.

### P3 — au volume actuel, l'état de l'art est la file de fusion, pas `--auto`

Détaillé dans la veille externe ci-dessous.

### P3 — une App GitHub est la réponse documentée à ce besoin précis

Détaillé dans la veille externe ci-dessous.

## Veille externe (`cursor-qa-scout`)

Section produite par le rôle compagnon `cursor-qa-scout`, dans le fichier de
l'audit en cours comme son contrat le prévoit. Elle **compare**, elle
n'ordonne rien.

**Aucun doublon avec un brief ouvert.** Briefs vérifiés un par un :
`001` (clé spatiale), `002` et `007` (pipeline géo), `003`, `004`, `005`
(Unity), `006` (pipeline full-auto — pose les workflows, ne traite pas les
jetons), `008-contexte-opus5`, `008-full-auto-automation-gaps`,
`009-full-auto-agent-invocation`, `010-repartition-roles-full-auto`.
`rg -n "FORGE_BOT_PAT"` ne renvoie aucun brief. Les briefs `008` et `009`
mentionnent bien `pipeline-audit.yml`, mais pour d'autres objets : `008` pour
le périmètre de fichiers autorisé, `009` (l.376) pour la vérification
d'identifiants. Le plafond de dépense de `009` a été livré sous forme de
`ci_budget_guard` **câblé dans `pipeline-forge-run` et `pipeline-challenge`
seulement** — le bras Cursor de la boucle n'entrait pas dans son périmètre,
ce qui est exactement le vide constaté en P0.

**Axe 1 — files de fusion (merge queues).** La documentation GitHub (S5)
positionne la file de fusion pour « une branche avec un nombre relativement
élevé de pull requests fusionnées chaque jour » ; une grille de maturité
indépendante (S6) va plus loin : « les politiques d'auto-fusion ne
fonctionnent en sécurité que dans le contexte d'une file de fusion ; sans
file, l'auto-fusion à haut volume produit le problème de course à l'échelle ».
Le dépôt est à environ une PR toutes les 13 minutes, produites par trois
agents concurrents, avec `gh pr merge --auto`. Point directement actionnable
pour la décision : si le propriétaire active un jour « Require merge queue »,
**aucun** des workflows du dépôt ne porte `merge_group` dans son `on:` — les
vérifications requises ne seraient jamais rapportées et la file resterait
bloquée indéfiniment (S5, S7, S6 le signalent tous trois comme *le* piège
classique).

**Axe 2 — boucles agentiques.** La littérature 2026 sur les garde-fous
d'agents (S8, S9) converge sur trois contrôles : plafond de dépense
**avant** l'appel, détecteur de boucle (même invite répétée, profondeur de
récursion), et budget hiérarchique par cycle / par tâche / par jour. La
formule qui résume l'écart avec le dépôt : « vérifier la dépense après coup,
c'est du reporting, pas de la mise en application » (S8) — or
`ci-budget-ledger.jsonl` fait 1 octet, donc même le reporting est vide. Le
dépôt possède la bonne forme (un `precheck` avant l'appel, un
`--max-budget-usd` pendant) mais ne l'applique qu'à deux des trois bras, et
n'a aucun détecteur de boucle, alors que la topologie audit ↔ contre-audit en
est un cas d'école.

**Axe 3 — jetons et privilèges.** La documentation GitHub (S1) et deux
analyses indépendantes (S10, S7) recommandent, pour chaîner des workflows,
un **jeton d'installation d'App GitHub** plutôt qu'un PAT personnel : durée
de vie d'environ une heure, réémis à chaque exécution, non rattaché à une
personne, révocable sans toucher au compte du propriétaire. S10 le formule
ainsi : « pour la production, utiliser une App GitHub est l'approche
recommandée, pour la sécurité comme pour l'auditabilité ». Le dépôt a choisi
le PAT, qui est le chemin le plus court et fonctionne — la comparaison est
versée au dossier, la décision reste au propriétaire.

## Briefs proposés (3 au plus — 3 proposés)

Propositions, pas instructions. Seul le propriétaire peut les convertir en
briefs (`architecture/README.md`, `CLAUDE.md` › Single Source of Instruction).

**B1 — donner au bras Cursor les freins qu'ont les deux autres, et fermer le
cycle audit ↔ contre-audit.** Couvre le P0. Deux volets indissociables : d'une
part aligner `pipeline-audit.yml` sur ses deux jumeaux (lecture runtime de
`mode:`, `ci_budget_guard precheck`, enregistrement de la dépense Cursor dans
le même registre) ; d'autre part rendre le cycle terminant — par exemple en
excluant les branches `forge-bot/review-*` de la critique, ou par un compteur
de profondeur de chaîne. Preuve attendue : un test rouge-puis-vert qui, à
partir d'une topologie d'événements simulée, montre la chaîne infinie avant
et bornée après.

**B2 — sortir le jeton durable des jobs déclenchés par `pull_request`, et le
rendre observable.** Couvre P1-a, les deux P2 sur le repli muet et
l'expiration, et verse au dossier la comparaison App GitHub / PAT (P3). Le
cœur du sujet n'est pas le choix du jeton mais le fait qu'un identifiant
durable soit présent dans un job dont le fichier de workflow provient de la
branche auditée.

**B3 — traiter les deux points d'écriture restants et remettre la CI au
vert.** Couvre P1-b (dont la rougeur de `hermes-dashboard / regenerate`) et
P1-c. Inclurait la garde mécanique qui manque : un test qui énumère les
points d'écriture de bot dans `.github/workflows/**` et échoue si l'un
d'eux diverge de la convention retenue — c'est l'absence de ce compteur qui
a laissé passer 3/5 sans signal.

## Commandes rejouées

```
$ git show --stat --format='%H%n%an%n%ad%n%s%n%P' cd1dcd210441d220168cbaacf620bf90288f3e55
cd1dcd210441d220168cbaacf620bf90288f3e55
Pierre-Edouard Liagre
Wed Aug 12 15:31:39 2026 +0200
Merge pull request #45 from PLiagre/cursor/forge-bot-pat-39f4
3807764933c0a7521ae03a4038dd4f197186fffa 82691a6d02178f2cca8cb99282c9f1fb9f1e755d

 .github/workflows/merge-bot.yml          | 10 +++++++++-
 .github/workflows/pipeline-challenge.yml | 10 +++++++++-
 .github/workflows/pipeline-forge-run.yml |  7 ++++++-
 docs/rules/full-auto-pipeline.md         | 13 +++++++++++++
 4 files changed, 37 insertions(+), 3 deletions(-)
```

```
$ gh api "repos/{owner}/{repo}/commits/cd1dcd21.../check-runs" \
    --jq '.check_runs[] | "\(.name)\t\(.status)\t\(.conclusion)"'
Reconcile local Hermes state	completed	success
Reconcile local Hermes state	completed	success
Reconcile local Hermes state	completed	success
escalate-on-failure	completed	skipped
Reconcile local Hermes state	completed	success
Reconcile local Hermes state	completed	success
cursor-scope	completed	skipped
tests	completed	success
f0-demo	completed	success
regenerate	completed	failure
schema	completed	success
invoke-cursor-auditor	completed	success
actionlint	completed	success
gitleaks	completed	success
```

```
$ gh run view 31601868896 --log-failed   (hermes-dashboard / regenerate, SHA cd1dcd2)
[master d68c9be] hermes: tableau de bord régénéré
 1 file changed, 19 insertions(+), 16 deletions(-)
Current branch master is up to date.
remote: error: GH006: Protected branch update failed for refs/heads/master.
remote: - 5 of 5 required status checks are expected.
 ! [remote rejected] master -> master (protected branch hook declined)
error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
##[error]Process completed with exit code 1.
```

```
$ gh pr view 45 --json number,author,mergedBy,autoMergeRequest,headRefName,state
{"author":{"login":"PLiagre"},"autoMergeRequest":null,
 "headRefName":"cursor/forge-bot-pat-39f4","mergedAt":"2026-08-12T13:31:39Z",
 "mergedBy":{"login":"PLiagre","name":"Pierre-Edouard Liagre"},
 "number":45,"state":"MERGED",
 "title":"pipeline : les PR et fusions de bots utilisent FORGE_BOT_PAT (repli GITHUB_TOKEN)"}
```

```
$ rg -n "git push|gh pr create|gh pr merge" .github/workflows/*.yml
.github/workflows/pipeline-challenge.yml:188:          git push -u origin "$branch"
.github/workflows/pipeline-challenge.yml:189:          gh pr create \
.github/workflows/hermes-dashboard.yml:103:          git push origin master
.github/workflows/pipeline-orchestrate.yml:117:            git push
.github/workflows/pipeline-forge-run.yml:237:          git push -u origin "$branch"
.github/workflows/pipeline-forge-run.yml:238:          gh pr create \
.github/workflows/merge-bot.yml:66:      - name: gh pr merge --auto
.github/workflows/merge-bot.yml:71:          gh pr merge --auto --squash ...
```

```
$ rg -n "ci_budget_guard|max-budget-usd" .github/workflows/*.yml | cut -d: -f1 | sort -u
.github/workflows/pipeline-challenge.yml
.github/workflows/pipeline-forge-run.yml
        (pipeline-audit.yml : absent)

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl

$ rg -n "^mode:" harness/pipeline/config.yaml
mode: full_auto
```

```
$ rg -ln "FORGE_BOT_PAT" --glob '!.github/**' .
./docs/rules/full-auto-pipeline.md
```

```
$ gh pr list --state all --limit 25 --jq '.[]|"\(.number)\t\(.state)\t\(.createdAt)\t\(.headRefName)"'
45	MERGED	2026-08-12T13:26:08Z	cursor/forge-bot-pat-39f4
44	OPEN	2026-08-12T13:03:14Z	forge-bot/review-CURSOR-bb8fe11-...
43	OPEN	2026-08-12T13:00:30Z	forge-bot/review-CURSOR-0269d8e-...
42	OPEN	2026-08-12T12:59:55Z	forge-bot/review-CURSOR-e849633-...
41	OPEN	2026-08-12T12:49:08Z	cursor/audit-de-la-pr-36-3dd2
40	OPEN	2026-08-12T12:46:31Z	cursor/cursor-audit-pr-35-88f0
39	MERGED	2026-08-12T12:40:01Z	cursor/audit-pull-request-34-713b
...
22	MERGED	2026-08-12T08:01:14Z	forge/stabilisation-2026-08-12
(24 PR entre 08:01 et 13:26 ; 6 ouvertes)
```

```
$ python3 harness/audit_schema.py
... All 14 audit(s) valid.

$ .venv/bin/python -m pytest harness/tests/ -q
309 passed, 16 skipped in 16.75s
```

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | GitHub Docs — *Trigger a workflow* (« events triggered by the `GITHUB_TOKEN` will not create a new workflow run » ; « ensure that you don't create recursive or unintended workflow runs ») — <https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow> | 2026-08-12 |
| S2 | GitHub Docs — *Securely using `pull_request_target`* (« the `pull_request` event […] runs the workflow file from the merge commit of the pull request ») — <https://docs.github.com/en/actions/reference/security/securely-using-pull_request_target> | 2026-08-12 |
| S3 | GitHub Changelog (2026-06-18) — *Safer `pull_request_target` defaults for GitHub Actions checkout* (« Same-repository pull requests aren't affected ») — <https://github.blog/changelog/2026-06-18-safer-pull_request_target-defaults-for-github-actions-checkout/> | 2026-08-12 |
| S4 | Cloud Security Alliance Labs (2026) — *prt-scan: GitHub Actions Supply Chain Campaign* (étape d'injection de workflow à partir d'un jeton en écriture) — <https://labs.cloudsecurityalliance.org/research/csa-research-note-github-actions-prt-scan-supply-chain-2026/> | 2026-08-12 |
| S5 | GitHub Docs — *Managing a merge queue* / *About protected branches* (file de fusion pour les branches à fort volume ; `merge_group` obligatoire dans `on:`) — <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue> | 2026-08-12 |
| S6 | VirtusLab — *Basic merge queues*, Visdom Maturity Matrix (« auto-merge policies only work safely in the context of a merge queue ») — <https://visdom-maturity-matrix.virtuslab.com/guides/delivery/basic-merge-queues> | 2026-08-12 |
| S7 | Mergify — *How to Enable GitHub Merge Queue with GitHub Actions* (le piège du `merge_group` manquant) — <https://mergify.com/blog/enable-github-merge-queue-actions-setup> | 2026-08-12 |
| S8 | Nexgismo — *AI Agent Budget Guards: How to Stop Runaway API Costs in 2026* (plafond avant appel, disjoncteur de boucle, « checking spend after the fact is reporting, not enforcement ») — <https://www.nexgismo.com/blog/ai-agent-budget-guards-stop-runaway-api-costs> | 2026-08-12 |
| S9 | Muhammad Amal — *Guardrails and Cost Controls for Agentic DevOps in Production* (« an autonomous agent without guardrails is an unbounded liability » ; le budget doit être du code que l'agent ne peut pas contourner) — <https://muhammadamal.my.id/blog/agentic-devops-guardrails-cost-controls/> | 2026-08-12 |
| S10 | DeKu — *Why GitHub Actions Workflows Don't Re-trigger — GITHUB_TOKEN, PAT, and GitHub Apps* (jeton d'App ~1 h réémis par run, recommandé face au PAT personnel) — <https://deku.posstree.com/en/github_actions/github-actions-workflow-retrigger/> | 2026-08-12 |

## Budget de l'audit

29 appels d'outils sur les 60 autorisés par le contrat
(`architecture/agents/cursor-auditor.md` › Budget max appels), veille
`cursor-qa-scout` incluse.
