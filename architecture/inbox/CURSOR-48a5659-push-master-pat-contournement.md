---
audit_id:                CURSOR-48a5659-push-master-pat-contournement
auditor:                 cursor-cloud
target_branch:           master
target_commit:           48a56591914aadb8af4c607bbf5724c2d56e0d81
created_at:              2026-08-12T15:37:00Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---
# Audit du merge 48a5659 — le push direct sur `master` ne satisfait plus la protection de branche, il la contourne

Audit post-fusion du rôle `cursor-auditor`
(`architecture/agents/cursor-auditor.md`), avec `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`) en compagnon de session : sa veille
occupe la section « Veille externe » de ce même fichier, comme son contrat le
prévoit.

**Un audit n'instruit rien.** Ce fichier est une *entrée* pour
`claude-challenger`, puis pour le propriétaire (`architecture/README.md`,
ADR-0005 / ADR-0006). Aucun constat ci-dessous n'est un ordre, aucun brief
proposé n'est pré-autorisé, et les trois flags `*_authorized` du frontmatter
valent `false`.

## Résumé en une page

Le merge est minuscule — deux fichiers, +14/-0, deux lignes utiles — et il
répare un vrai défaut. `hermes-dashboard / regenerate` échouait à chaque tour
depuis plus d'une heure (`bad1ffb`, `2a4f808`, `01e7c24` : trois runs rouges
d'affilée), parce que son `git push origin master` était refusé par la
protection de branche. Le diff donne au `checkout` de ce workflow et de
`pipeline-orchestrate.yml` le jeton `FORGE_BOT_PAT`, et le résultat est
mesurable : la CI du SHA audité est **entièrement verte**, `regenerate`
compris, et le tableau de bord a de nouveau été poussé (`620a5ea`). C'est
exactement le point P1-b laissé ouvert par l'audit `CURSOR-cd1dcd2` : les
cinq points d'écriture de bot sont désormais câblés sur le même jeton.

Le désaccord ne porte donc pas sur l'effet, mais sur **la façon dont l'effet
est obtenu**, que le log rend littérale :

```
remote: Bypassed rule violations for refs/heads/master:
remote: - 5 of 5 required status checks are expected.
   48a5659..620a5ea  master -> master
```

Le push ne satisfait pas les cinq vérifications requises : il les
**contourne**, en empruntant le privilège d'administrateur du propriétaire.
Ce qui était une barrière devient une barrière à trous, et le trou n'est pas
réservé au tableau de bord : `pipeline-orchestrate.yml` est autorisé à pousser
par le même chemin `harness/queue/briefs/**` (l.115), c'est-à-dire un
**brief** — la source unique d'instruction du dépôt
(`CLAUDE.md` › Single Source of Instruction) — sans pull request, sans revue
humaine, et sans qu'aucune vérification requise n'ait à passer.

Deuxième écart, plus discret : le raisonnement anti-boucle écrit en commentaire
est juste, vérifié en réel pour `hermes/**` (le push `620a5ea` a bien été
classé « documentaire », aucun agent relancé), mais il ne couvre pas ce même
chemin `harness/queue/briefs/**`, qui n'est pas dans le filtre documentaire de
`pipeline-audit.yml`. Le jour où la boucle convertira un audit approuvé en
brief, son propre push relancera un agent Cursor payant.

| sévérité | nombre | objet |
|---|---|---|
| P0 | 1 | un brief peut atterrir sur `master` sans PR ni vérification, par un push qui contourne explicitement la protection de branche |
| P1 | 3 | le chemin `harness/queue/briefs/**` échappe au filtre anti-boucle ; le PAT reste disponible à toutes les étapes du job, pas seulement au push ; toujours aucune garde mécanique sur le câblage des jetons |
| P2 | 3 | le bloc `permissions:` ne borne plus l'écriture réelle ; deux écrivains directs concurrents sur `master` sans sérialisation commune ; la page de règles ne décrit plus l'usage réel du secret |
| P3 | 2 | `actions/checkout` épinglé en v4.2.2 (v6 sort le jeton du workspace) ; une App GitHub est l'acteur de contournement prévu par GitHub, pas un PAT personnel |

## Ce que le merge change

`git diff --stat 01e7c24..48a5659`, rejoué (sortie en fin de document) :

```
 .github/workflows/hermes-dashboard.yml     | 7 +++++++
 .github/workflows/pipeline-orchestrate.yml | 7 +++++++
 2 files changed, 14 insertions(+)
```

Une seule ligne fonctionnelle par fichier, identique :
`token: ${{ secrets.FORGE_BOT_PAT || github.token }}` sur
`actions/checkout`. Les douze autres lignes sont deux commentaires qui
expliquent pourquoi.

Lentille 1 du guide de critique (l'intention avant le diff) : l'intention est
écrite, exacte et vérifiable — le run `31609187517` cité dans le commentaire
existe bien, et l'échec `GH006` qu'il décrit est réel. Le diff fait ce qu'il
annonce, sans effet de bord caché. Trois choses méritent d'être portées à son
crédit avant les constats :

1. **Il ferme un rouge persistant**, pas un rouge théorique : trois runs
   consécutifs échouaient avant lui.
2. **Le repli est honnête.** Le commentaire annonce « repli : `GITHUB_TOKEN`
   (échouera au push si `master` est protégé — visible, jamais silencieux) »,
   et c'est exact pour ces deux workflows : sans PAT, le job devient rouge, il
   ne dégrade pas en silence. C'est une amélioration nette sur le repli muet
   relevé en P2 dans `CURSOR-cd1dcd2`.
3. **Le raisonnement anti-boucle a été fait et il est vrai** pour le périmètre
   qu'il énonce — la preuve empirique est en fin de document.

## CI du commit audité — **verte**

`gh api .../commits/48a5659.../check-runs`, rejoué (sortie complète en fin de
document) :

| job | conclusion |
|---|---|
| `tests`, `f0-demo` (harness-ci) | success |
| `schema` (audit-guard) | success |
| `cursor-scope` (audit-guard) | skipped (normal : pas une PR) |
| `invoke-cursor-auditor` (pipeline-audit) | success |
| `actionlint`, `gitleaks` (security) | success |
| `Reconcile local Hermes state` ×2 (hermes-observer) | success |
| **`regenerate` (hermes-dashboard)** | **success** — rouge sur les trois SHA précédents |

En local au SHA audité : `314 passed, 16 skipped` sur `harness/tests/`, et
`harness/audit_schema.py` valide les 23 audits présents.

## Constats

### P0 — un brief peut atteindre `master` sans pull request, sans revue et sans vérification, par un contournement explicite de la protection

**Preuve, en trois pièces.**

*Pièce 1 — le push contourne, il ne satisfait pas.* Log du run
`31612718942` (`hermes-dashboard / regenerate`, déclenché par le merge
audité) :

```
remote: Bypassed rule violations for refs/heads/master:
remote: - 5 of 5 required status checks are expected.
To https://github.com/PLiagre/ForgeHistory
   48a5659..620a5ea  master -> master
```

GitHub nomme lui-même l'opération : *bypassed*. Les cinq vérifications
requises n'ont pas été satisfaites, elles ont été enjambées, parce que le PAT
porte l'identité d'un administrateur et que « Do not allow bypassing » est
décoché — le commentaire du diff le dit d'ailleurs explicitement. Le même
push, une heure plus tôt et avec le jeton interne, produisait
`GH006 ... (protected branch hook declined)`.

*Pièce 2 — le même chemin peut porter un brief.* `pipeline-orchestrate.yml`
n'est pas limité au tableau de bord. Sa garde de portée (l.115) autorise
trois familles de chemins au commit puis au push :

```
grep -vE '^(architecture/audit-ledger\.jsonl|architecture/decisions/|harness/queue/briefs/)'
```

et `harness/pipeline/orchestrator.py:190` (`handle_audit_approved`) écrit
précisément sous `briefs_dir`, c'est-à-dire `harness/queue/briefs/`, lors de
la conversion d'un audit approuvé. Le workflow ajoute d'ailleurs ce répertoire
au commit sans condition (`git add ... harness/queue/briefs`, l.131).

*Pièce 3 — la décision qui déclenche cette conversion est, elle aussi, une
machine.* La chaîne `decision auto` est en place (commits `8d0f2d9`,
`f43cadd` et suivants) et `harness/pipeline/config.yaml` déclare
`mode: full_auto`. Il n'existe donc, sur ce chemin, aucune étape où un humain
doit approuver quoi que ce soit entre « un audit est proposé » et « un brief
est sur `master` ».

**Pourquoi c'est un P0 et pas un P1.** Le dépôt entier repose sur la règle
« exactement un document dit à un agent ce qu'il doit faire : le brief »
(`CLAUDE.md` › Single Source of Instruction), et sur la séparation
« trois rôles, jamais un seul agent ». Un chemin d'écriture qui dépose un
brief sur `master` sans PR ni vérification retire au propriétaire le seul
point où il voyait passer le contenu instructif — et il le fait par un
mécanisme dont le nom technique, dans le log, est « contournement de règle ».
Ce n'est pas une régression fonctionnelle : c'est l'affaiblissement de la
garde qui protège tout le reste. Il faut noter que ce chemin **n'a pas encore
été emprunté** : `git log -- harness/queue/briefs/` ne montre aucun commit de
`forge-bot`. Le constat porte sur une porte ouverte, pas sur un dégât
constaté.

Élément de contexte utile à l'arbitrage : le brief `010` (l.212) écrivait
encore, le 2026-08-11, « la protection de branche est indisponible sur ce plan
GitHub (`HTTP 403`, vérifié le 2026-08-11) : cette denylist [du merge-bot] est
la seule barrière réelle ». Une protection existe depuis — le `GH006` du
2026-08-12 le prouve. Le dépôt a donc gagné une barrière, et ce merge la rend
franchissable pour les jobs porteurs du PAT, sans que la denylist du merge-bot
(qui ne s'applique qu'aux **pull requests**) ne prenne le relais sur ce
chemin-là.

### P1-a — le raisonnement anti-boucle est exact pour `hermes/**`, et muet sur le chemin le plus large

Le commentaire de `hermes-dashboard.yml` affirme : « le filtre "push
documentaire" de `pipeline-audit.yml` couvre `hermes/**`, donc un push porté
par le PAT ne déclenche pas d'audit ». C'est vrai, et c'est prouvé — voir la
preuve empirique en fin de document, deux pushes classés documentaires,
aucun agent relancé.

Mais `pipeline-orchestrate.yml`, dont le commentaire ne dit rien du
réamorçage, pousse jusqu'à trois familles de chemins, et le filtre n'en couvre
que deux :

```
grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)'
```

| chemin poussé par `pipeline-orchestrate` | couvert par le filtre documentaire ? |
|---|---|
| `architecture/audit-ledger.jsonl` | oui |
| `architecture/decisions/**` | oui |
| **`harness/queue/briefs/**`** | **non** → `skip=false` → un agent Cursor est invoqué |

Conséquence : le jour où la boucle convertit un audit approuvé en brief — le
cas nominal du cycle `AUDIT_APPROVED → AUDIT_CONVERTED` décrit dans
`architecture/README.md` — son propre push déclenche un audit Cursor sur sa
propre production. Avant ce merge, ce push n'existait pas (il était refusé) ;
le diff le rend possible sans étendre le filtre. C'est le même angle mort que
celui corrigé le 2026-08-12 pour `architecture/inbox/`, déplacé d'un cran.

Le coût n'est pas nul : `pipeline-audit.yml` reste le seul des trois workflows
d'invocation sans `ci_budget_guard precheck` ni `--max-budget-usd`
(constat P0 de `CURSOR-cd1dcd2`, toujours ouvert au SHA audité), et
`harness/pipeline/ci-budget-ledger.jsonl` fait toujours **1 octet**.

### P1-b — le PAT est disponible à toutes les étapes du job, alors que seule la dernière en a besoin

`actions/checkout` est appelé sans `persist-credentials`, dont la valeur par
défaut est `true` : le jeton est écrit dans la configuration git du workspace
sous forme d'en-tête `http.https://github.com/.extraheader`. Ce n'est pas une
déduction : le nettoyage post-job de n'importe quel run du dépôt l'affiche
(sortie en fin de document, run `31612860576`).

Ce que cela change concrètement dans les deux workflows touchés :

| étape | a besoin du PAT ? | l'a quand même |
|---|---|---|
| `setup-python` | non | oui |
| `Collect live GitHub data` (`gh run list`, `gh pr list`) | non — utilise `GITHUB_TOKEN` | oui |
| `Collect recent Cursor Cloud agents` (`curl` vers `api.cursor.com`) | non | oui |
| `python hermes/dashboard.py` / `python harness/pipeline/orchestrator.py` | non | oui |
| `git push origin master` | **oui** | oui |

Le PAT est un identifiant durable, personnel, avec `Contents` et
`Pull requests` en lecture/écriture sur le dépôt, et — depuis ce merge — un
privilège de contournement de la protection de `master`. Il est désormais posé
sur le disque du runner avant l'exécution de code Python versionné du dépôt et
avant un appel réseau sortant. La littérature d'outillage sécurité traite ce
motif comme un défaut par défaut (S3, S4) : « toute étape ultérieure ou action
tierce peut lire le fichier d'identifiants et l'utiliser pour pousser des
commits » (S4). Le remède documenté est de ne pas persister le jeton et de ne
le fournir qu'à l'étape qui pousse (S3, S5).

Nuance honnête, pour ne pas gonfler le constat : ces deux workflows-ci ne se
déclenchent **pas** sur `pull_request` (`push` sur `master`, `schedule`,
`workflow_dispatch` uniquement). Ce merge n'aggrave donc pas le constat P1-a
de `CURSOR-cd1dcd2` (PAT lisible depuis une branche de bot) ; il élargit
seulement la fenêtre d'exposition à l'intérieur de jobs déjà déclenchés par
le dépôt lui-même.

### P1-c — toujours aucune garde mécanique sur le câblage des jetons

Lentille 2 du guide (preuve d'exécution, pas d'affirmation). Ce merge existe
parce qu'un audit humainement rédigé a compté « 3 sur 5 » ; la correction a été
faite à la main, et rien dans le dépôt ne saurait le recompter :

```
$ rg -ln "FORGE_BOT_PAT|persist-credentials" harness/tests/
(aucun résultat)
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped
```

314 tests, et aucun ne dit quoi que ce soit sur les points d'écriture des
workflows. Le dépôt sait pourtant tester la forme d'un workflow (les tests du
lot 006b le font). Ce qui reste indétecté aujourd'hui : un sixième point
d'écriture ajouté sans PAT, un `persist-credentials` manquant, ou un chemin
poussé qui sort du filtre documentaire (P1-a). Le brief B3 proposé par
`CURSOR-cd1dcd2` visait déjà ce vide ; il n'a pas encore été converti, et ce
merge en est la deuxième illustration.

### P2 — le bloc `permissions:` ne borne plus l'écriture réelle

Les deux workflows conservent `permissions: contents: write`. Ce bloc borne
les droits du `GITHUB_TOKEN` — il n'a aucun effet sur le PAT, dont les droits
sont fixés côté compte du propriétaire. Un lecteur du fichier croit y lire le
plafond de ce que le job peut écrire ; depuis ce merge, ce plafond est ailleurs
et n'est pas dans le dépôt. Ce n'est pas un défaut de fonctionnement, c'est une
perte de lisibilité sur exactement la question que le bloc était censé rendre
lisible.

### P2 — deux écrivains directs concurrents sur `master`, sérialisés séparément

`hermes-dashboard.yml` (groupe `hermes-dashboard`) et
`pipeline-orchestrate.yml` (groupe `pipeline-orchestrate-master`) poussent
tous deux directement sur `master`, chacun sérialisé **par rapport à lui-même
seulement**. Les deux font `git pull --rebase origin master` puis `git push`,
sans nouvelle tentative : entre le rebase et le push subsiste une fenêtre où
l'autre workflow peut avancer `master`, et le perdant devient rouge.

Le commentaire de `pipeline-orchestrate.yml:46-50` documente exactement cet
incident (« la deuxième a perdu son push … et sa ligne de ledger avec ») et y
répond par un groupe de concurrence **intra-workflow**. Ce merge augmente la
fréquence de la fenêtre, puisque les pushes qui échouaient réussissent
maintenant : le 2026-08-12, `620a5ea` à 15:31:15 et `4cace88` à 15:32:28, soit
73 secondes d'écart entre deux écrivains différents.

### P2 — la page de règles ne décrit plus l'usage réel du secret

`docs/rules/full-auto-pipeline.md:120-128` décrit `FORGE_BOT_PAT` comme
« utilisé par `pipeline-challenge.yml` et `pipeline-forge-run.yml` … et par
`merge-bot.yml` ». Ils sont cinq depuis ce merge, et surtout la page ne dit
nulle part que le secret sert désormais à **contourner la protection de
branche de `master`** — c'est-à-dire sa propriété la plus lourde de
conséquences. Le diff ne touche pas cette page. Un lecteur de la règle ne peut
donc pas savoir ce que le secret permet réellement.

### P3 — `actions/checkout` est épinglé en v4.2.2

Le dépôt épingle proprement par SHA (bonne pratique respectée partout). En
v4, les identifiants persistés vont dans `.git/config`, à l'intérieur du
workspace ; la v6 les déplace sous `$RUNNER_TEMP` (S3), ce qui réduit la
surface décrite en P1-b même quand la persistance reste nécessaire pour
pousser. Comparaison versée au dossier, pas plus.

### P3 — l'acteur de contournement prévu par GitHub est une App, pas un PAT personnel

Détaillé dans la veille externe ci-dessous. La comparaison App / PAT avait déjà
été versée par `CURSOR-cd1dcd2` (source S10) ; l'angle **nouveau**, apporté par
ce merge, est celui de la liste de contournement de la protection de branche.

## Veille externe (`cursor-qa-scout`)

Section produite par le rôle compagnon `cursor-qa-scout`, dans le fichier de
l'audit en cours comme son contrat le prévoit. Elle **compare**, elle
n'ordonne rien.

**Aucun doublon avec un brief ouvert.** Briefs vérifiés un par un :
`001`, `002`, `003`, `004`, `005`, `006`, `007`, `008-contexte-opus5`,
`008-full-auto-automation-gaps`, `009-full-auto-agent-invocation`,
`010-repartition-roles-full-auto`. `rg -n "FORGE_BOT_PAT"` ne renvoie **aucun**
brief (seulement `docs/rules/full-auto-pipeline.md` et trois audits de
`inbox/`). Le seul brief qui parle de protection de branche est `010` (l.212),
et il dit l'inverse de l'état actuel : « la protection de branche est
indisponible sur ce plan GitHub (`HTTP 403`, vérifié le 2026-08-11) » — son
périmètre est la denylist d'auto-fusion des **pull requests**, pas les pushes
directs, et son verdict est ACCEPT (lot clos).

**Axe 1 — écrire sur une branche protégée depuis la CI (état de l'art).** Le
motif rencontré ici est un classique documenté : un bot qui pousse sur la
branche par défaut se casse dès qu'on active les vérifications requises, parce
que « les vérifications requises et la PR obligatoire bloquent le push direct
sur la référence, pas seulement la fusion des PR » (S1). Trois options
circulent, et les sources les classent dans le même ordre : App GitHub
inscrite sur la liste de contournement (recommandée), clé de déploiement, PAT
personnel (déconseillé). Le motif exact du refus du PAT est celui qui décrit le
mieux la situation du dépôt : « un PAT d'administrateur dans la liste de
contournement exempte **toutes** les actions d'administrateur, y compris vos
propres pushes interactifs — c'est un privilège attaché à un rôle, pas à une
automatisation, et il est durable » (S1). S2 ajoute l'argument de continuité :
un PAT est « lié à un humain — il casse quand cette personne part ou fait
tourner son jeton ». Le dépôt a choisi le chemin le plus court, qui fonctionne
et qui est prouvé ; la comparaison est versée, la décision reste au
propriétaire.

Deux détails d'implémentation, dans les mêmes sources, sont directement
applicables au constat P1-a de cet audit : (a) `github-actions[bot]` ne peut
**pas** figurer sur une liste de contournement, par conception — donc « rendre
le jeton interne suffisant » n'est pas une option disponible (S1, S2) ; (b)
« une vérification qui ne tourne jamais sur `push` ne rapporte jamais rien sur
le push direct du bot, et le bloque donc en permanence » (S1) — c'est
exactement le message `5 of 5 required status checks are expected` observé
ici, et cela explique pourquoi la seule issue trouvée a été le contournement.

**Axe 2 — jetons persistés dans le workspace.** Les outils d'audit de
workflows traitent aujourd'hui `persist-credentials` non renseigné comme un
défaut à signaler (S4 le classe en règle dédiée, avec correction
automatique). La formulation la plus utile pour le dossier : « `persist-credentials`
vaut `true` par défaut, il faut donc l'écrire explicitement à `false` — ne pas
compter sur l'omission » (S4), et « toute étape ultérieure ou action tierce peut
lire le fichier d'identifiants » (S4). La v6 de `actions/checkout` déplace le
stockage hors du workspace (S3), ce qui atténue le cas « archivage du
workspace » mais pas le cas « une étape ultérieure lit le jeton ».

**Axe 3 — plafonds et détecteurs de boucle pour agents.** La littérature 2026
converge sur une architecture en couches : budget de tours, budget de
jetons/coût vérifié **avant** l'appel, détecteur de boucle sur la vélocité, et
un plafond de dépense au niveau de la clé — « la couche qui vous sauve, parce
qu'elle tient quand le code est faux » (S6). S5 ajoute le chiffre qui rend le
sujet concret pour un dépôt comme celui-ci : une exécution en boucle de type
ReAct consomme en O(n²), et « un agent de revue de PR qui coûte 0,04 $ en local
peut atteindre 0,40 $ dès qu'il boucle ». L'écart avec le dépôt est le même
qu'au tour précédent, et il n'a pas bougé au SHA audité : la bonne forme existe
(`ci_budget_guard precheck` + `--max-budget-usd`) mais seulement sur deux des
trois bras, `pipeline-audit.yml` — celui qui lance Cursor, donc celui que le
constat P1-a peut réamorcer — n'en a aucun, et le registre de dépense fait
toujours 1 octet.

## Briefs proposés (3 au plus — 3 proposés)

Propositions, pas instructions. Seul le propriétaire peut les convertir en
briefs (`architecture/README.md`, `CLAUDE.md` › Single Source of Instruction).

**B1 — décider par quel chemin un bot écrit sur `master`, et empêcher qu'un
brief l'emprunte sans revue.** Couvre le P0. Le cœur n'est pas le choix du
jeton mais la question laissée implicite : quels contenus ont le droit
d'atteindre `master` sans passer par une pull request ? Un artefact
documentaire (tableau de bord, ligne de ledger) et un brief — qui instruit un
agent — ne demandent visiblement pas la même réponse. Preuve attendue : une
garde qui échoue si un push direct de bot porte un chemin instructif, et une
trace lisible (dans le log ou le ledger) chaque fois qu'une règle de protection
est contournée.

**B2 — étendre le filtre anti-boucle aux chemins que la boucle sait maintenant
pousser, et donner au bras Cursor le plafond qu'ont les deux autres.** Couvre
le P1-a, et reprend le P0 encore ouvert de `CURSOR-cd1dcd2` sous l'angle
nouveau que ce merge crée. Preuve attendue : un test qui, à partir de la liste
des chemins autorisés au push par `pipeline-orchestrate.yml`, échoue si l'un
d'eux n'est pas classé par le filtre documentaire de `pipeline-audit.yml` —
c'est-à-dire une garde qui lie les deux fichiers au lieu de les laisser dériver
séparément.

**B3 — réduire la fenêtre pendant laquelle le PAT est disponible, et rendre le
câblage des jetons mécaniquement vérifiable.** Couvre P1-b, P1-c et les trois
P2. Un seul brief parce que les trois se démontrent avec la même garde : un
test qui énumère les points d'écriture de `.github/workflows/**` et vérifie,
pour chacun, le jeton employé, la persistance des identifiants et la
concordance avec `docs/rules/full-auto-pipeline.md`. C'est l'absence de ce
compteur qui a laissé passer 3/5 au tour précédent, et qui laisse aujourd'hui
la page de règles décrire un secret qui a changé de nature.

## Commandes rejouées

```
$ git show --stat --format='%H%n%an%n%ad%n%s%n%P' 48a56591914aadb8af4c607bbf5724c2d56e0d81
48a56591914aadb8af4c607bbf5724c2d56e0d81
Pierre-Edouard Liagre
Wed Aug 12 17:30:55 2026 +0200
Merge pull request #55 from PLiagre/cursor/push-master-pat-39f4
01e7c24e6fba4991bac79d2c2da89c06cbe71304 f43cadd08532de4b73c6246633c597df446a2246

 .github/workflows/hermes-dashboard.yml     | 7 +++++++
 .github/workflows/pipeline-orchestrate.yml | 7 +++++++
 2 files changed, 14 insertions(+)
```

```
$ gh api "repos/PLiagre/ForgeHistory/commits/48a5659.../check-runs" \
    --jq '.check_runs[] | "\(.name)\t\(.status)\t\(.conclusion)"' | sort | uniq -c
      1 actionlint	completed	success
      1 cursor-scope	completed	skipped
      1 f0-demo	completed	success
      1 gitleaks	completed	success
      1 invoke-cursor-auditor	completed	success
      2 Reconcile local Hermes state	completed	success
      1 regenerate	completed	success
      1 schema	completed	success
      1 tests	completed	success
```

```
$ gh run list --workflow hermes-dashboard.yml --limit 5 \
    --json databaseId,headSha,conclusion,createdAt
31612860572	4cace88	success	2026-08-12T15:32:31Z
31612718942	48a5659	success	2026-08-12T15:30:58Z     <- le merge audité
31609830989	01e7c24	failure	2026-08-12T14:59:26Z
31608759556	2a4f808	failure	2026-08-12T14:47:47Z
31604845295	bad1ffb	failure	2026-08-12T14:05:01Z
```

Le push effectué par ce run — la pièce centrale du P0 :

```
$ gh run view 31612718942 --log   (hermes-dashboard / regenerate, SHA 48a5659)
[master 620a5ea] hermes: tableau de bord régénéré
remote: Bypassed rule violations for refs/heads/master:
remote: - 5 of 5 required status checks are expected.
To https://github.com/PLiagre/ForgeHistory
   48a5659..620a5ea  master -> master
```

Preuve empirique du raisonnement anti-boucle (il tient pour les chemins qu'il
couvre) :

```
$ gh run view 31612750199 --log   (pipeline-audit, push 620a5ea = hermes/DASHBOARD.md)
##[notice]push documentaire (artefacts de la boucle uniquement) -- pas de nouvel audit lancé.

$ gh run view 31612860576 --log   (pipeline-audit, push 4cace88 = ledger + decisions)
##[notice]push documentaire (artefacts de la boucle uniquement) -- pas de nouvel audit lancé.
```

Preuve de la persistance des identifiants (P1-b), extraite du nettoyage
post-job du même run :

```
$ gh run view 31612860576 --log | rg extraheader
Post Run actions/checkout@11bd719   [command]/usr/bin/git config --local --name-only --get-regexp http\.https\:\/\/github\.com\/\.extraheader
Post Run actions/checkout@11bd719   http.https://github.com/.extraheader
Post Run actions/checkout@11bd719   [command]/usr/bin/git config --local --unset-all http.https://github.com/.extraheader
```

Les deux commits poussés sur `master` par des bots depuis le merge audité :

```
$ git log --oneline 48a5659..origin/master
4cace88 pipeline-orchestrate: review_recorded      (auteur: forge-bot, 15:32:28)
620a5ea hermes: tableau de bord régénéré           (auteur: hermes,    15:31:15)
```

Périmètre réellement poussable par `pipeline-orchestrate.yml`, et sa
couverture par le filtre anti-boucle :

```
$ rg -n "grep -vE" .github/workflows/pipeline-orchestrate.yml
115: offending="$(printf '%s\n' "$changed" | grep -vE '^(architecture/audit-ledger\.jsonl|architecture/decisions/|harness/queue/briefs/)' || true)"

$ rg -n "hors_boucle=" .github/workflows/pipeline-audit.yml
69: hors_boucle="$(... grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)' || true)"

$ rg -n "briefs_dir" harness/pipeline/orchestrator.py | head -3
190:def handle_audit_approved(payload: dict, *, ledger_path: Path, inbox: Path | None, briefs_dir: Path, **_kw) -> dict:
304:    briefs_dir = briefs_dir or (REPO_ROOT / "harness" / "queue" / "briefs")
```

Gardes mécaniques locales au SHA audité :

```
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 16.96s

$ python3 harness/audit_schema.py
All 23 audit(s) valid.

$ rg -ln "FORGE_BOT_PAT|persist-credentials" harness/tests/
(aucun résultat)

$ rg -n "persist-credentials" .github/workflows/
(aucun résultat -- la valeur par défaut `true` s'applique partout)

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl

$ rg -n "^mode:" harness/pipeline/config.yaml
33:mode: full_auto
```

```
$ rg -ln "FORGE_BOT_PAT" --glob '!.github/**' .
./docs/rules/full-auto-pipeline.md
./architecture/inbox/CURSOR-e2896e7-pr44-challenge-bb8fe11.md
./architecture/inbox/CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons.md
./architecture/inbox/CURSOR-7e5244b-ledger-post-fusion-poussee-master.md
   (aucun brief sous harness/queue/briefs/ -- voir la déclaration de non-doublon)
```

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | agent-almanac — *harden-github-repo-security* (« required checks and required PR block direct pushes to the ref, not just PR merges » ; « the default `GITHUB_TOKEN` / `github-actions[bot]` cannot be a bypass actor » ; « PAT bypass is an anti-pattern … exempts ALL admin actions, role-wide, not automation-scoped ») — <https://github.com/pjt222/agent-almanac/blob/main/skills/harden-github-repo-security/SKILL.md> | 2026-08-12 |
| S2 | Netcentric/fe-build, issue #146 — *Release workflow fails on every push to main (protected branch blocks semantic-release)* (comparatif PAT / App : « tied to one human — breaks when they leave or rotate the token » ; App retenue comme « GitHub's own recommendation for CI/CD pushing to protected branches ») — <https://github.com/Netcentric/fe-build/issues/146> | 2026-08-12 |
| S3 | `actions/checkout` — README et notes de version (« the auth token is persisted in the local git config … set `persist-credentials: false` to opt-out » ; v6 : credentials sous `$RUNNER_TEMP` au lieu de `.git/config`) — <https://github.com/actions/checkout> | 2026-08-12 |
| S4 | actsense — *unsafe checkout* (« subsequent steps can access and potentially misuse these credentials » ; « `persist-credentials` defaults to `true`, so it must be explicitly set to `false` ») — <https://actsense.dev/vulnerabilities/unsafe_checkout/> | 2026-08-12 |
| S5 | DZone — *Token Attribution Framework for Agentic AI in CI/CD* (coût en O(n²) d'une boucle ReAct ; « a PR review agent … which costs $0.04 in local development can rack up $0.40+ once it gets stuck in its looping process » ; garde de boucle sur la vélocité de jetons) — <https://dzone.com/articles/agentic-ai-token-attribution-ci-cd> | 2026-08-12 |
| S6 | Multigrid — *Agent Cost Control: Capping Spend Per Task* (plafond au niveau de la clé : « this is the layer that saves you, because it holds when the code is wrong » ; un budget dans la boucle est « necessary, not sufficient ») — <https://multigrid.ai/learn/agent-cost-control> | 2026-08-12 |
| S7 | aiarch.dev — *The Bounded Agentic Loop: How to Stop an Agent From Running Away* (budgets de tours / de coût, interrupteur hors bande : « budgets stop the loop at the next guard check; the kill switch stops it now ») — <https://aiarch.dev/patterns/bounded-agentic-loop> | 2026-08-12 |

## Budget de l'audit

38 appels d'outils sur les 60 autorisés par le contrat
(`architecture/agents/cursor-auditor.md` › Budget max appels), veille
`cursor-qa-scout` et dépôt de la pull request compris.
