# Brief 006 — La boucle ne s'arrête que sur une décision

## But

Le 3 septembre 2026 au soir, la boucle a tourné seule pour la première fois :
le répartiteur est installé, le profil vit dans un fichier que `hermes` écrit,
une carte revient seule d'un délai dépassé, et un worktree sale ne fait plus
échouer le lot suivant. La machine ne s'arrête plus sur une panne technique.

Elle s'arrête encore sur quelque chose qu'elle pourrait lire. Mesuré le même
soir, sur le produit :

```bash
python3 -m atelier piloter --projet /srv/ForgeHistory
```

a répondu `déposer a-coder 046-la-mer-est-un-port-commun` — alors que la PR
**206** est ouverte depuis des heures sur `agent/046-la-mer-est-un-port-commun`
et attend la fusion du propriétaire. La fiche dit `état : pret · PR : —`, aucune
carte ne porte le lot, et rien dans l'atelier ne sait que le travail existe
déjà. Au réveil suivant du `coder`, Composer aurait recodé un lot qui est en
relecture. Un quota dépensé pour refaire ce qui est fait, et une seconde PR sur
la même branche.

L'inverse coûte aussi. Quand le propriétaire **ferme** une PR sans la fusionner
— parce que le lot est à reprendre autrement — la carte reste dans `faite`, son
verrou tient ses fichiers, et aucun lot qui les touche ne peut plus avancer.
Rien ne l'en sort sauf `atelier reprendre`, tapée par une personne.

Les deux pannes sont la même : **l'atelier connaît le numéro de la PR d'un lot
et ne lui demande jamais où elle en est.**

« Tourner sans interruption » ne veut pas dire tourner sans le propriétaire. Il
fusionne, il abandonne, il réécrit un brief — c'est ce qui fait avancer la
feuille de route, et l'atelier ne fusionnera jamais. Cela veut dire : la
machine ne s'arrête jamais sur un **état** qu'elle pouvait lire, et n'attend
une personne que là où une personne décide.

État de départ, mesuré par :

```bash
grep -rn 'headRefName\|"state"' atelier/
```

Une seule ligne aujourd'hui, dans `atelier/echange.py` : la sonde `gh` existe,
elle ne demande que la branche d'une PR. Aucune ne demande son état. Si cette
commande rend une ligne portant `"state"`, ou si `atelier piloter` refuse déjà
de déposer un lot dont une PR est ouverte, le lot est caduc.

Ce lot **dépend du 005** (`briefs/005-la-carte-ne-passe-pas-sur-parole.md`) et
se déclare bloqué s'il n'est pas fusionné : les deux écrivent dans
`atelier/echange.py` et `atelier/__main__.py`, et le verrou refusera le second.
L'ordre est celui-là et pas l'inverse — une porte qui lit le verdict des tests
vaut mieux qu'une porte qui lit l'état d'une PR, et le 005 est déjà écrit.

Il ouvre une chose qu'il ne fait pas : rien ici ne **ferme** une PR ni n'en
ouvre. L'atelier lit ; le propriétaire décide.

## Règle du monde

L'atelier ne simule aucun monde : ce lot ne change aucun nombre du jeu et ne
touche à aucun fichier du produit. La règle en jeu est celle de l'atelier, et
elle est déjà écrite deux fois dans ce dépôt.

`atelier/quota.py` : « Un inconnu vaut -1, jamais 0. Un zéro peut être une
vraie mesure. » `atelier/echange.py` : « Si `gh` répond, la PR doit être sur
`branche`. Sinon on se tait. »

Ce lot l'applique à l'état d'une PR, et un seul point demande une décision :
**ce que fait l'inconnu, et il ne fait pas la même chose des deux côtés.**

Avant de **déposer**, l'inconnu retient. Déposer une carte est ce qui fait
dépenser un quota ; une sonde muette qui laisserait passer rendrait la garde
inutile exactement quand elle ne répond plus, et le lot serait recodé pour
rien. La fiche reste où elle est, le pilote le dit sur stderr, et le réveil
suivant redemandera — il y a trois pilotes par jour dans le profil `jour`. Une
carte non déposée se voit dans la feuille ; une dépense évitée ne se voit
nulle part, et c'est pourquoi c'est l'inconnu qui doit céder le passage.

Avant de **libérer**, l'inconnu ne fait rien. Ranger la carte d'une PR fermée
est un geste de nettoyage : le faire sur une sonde muette rangerait des cartes
vivantes. Le statu quo est sûr des deux côtés — la carte attend, comme
aujourd'hui, et le propriétaire garde `atelier reprendre`.

L'état lu est celui que GitHub publie, pas celui que la feuille déclare. Une
fiche dit ce que le propriétaire a décidé ; une PR dit ce qui existe. Quand les
deux se contredisent — fiche `pret`, PR ouverte — c'est la PR qui décrit le
monde, et la feuille qui décrira l'intention une fois la fusion faite.

## Périmètre

En écriture, six fichiers et pas un de plus.

`atelier/echange.py` reçoit la sonde d'état, à côté de la sonde de branche qui
y vit déjà avec sa doctrine du silence. `atelier/feuille.py` reçoit la
décision : ne pas déposer un lot dont une PR est ouverte, et reconnaître une
PR fermée sans fusion parmi les rapprochements qu'il calcule déjà.
`atelier/__main__.py` reçoit la commande qui rend l'état d'une PR, pour qu'un
humain puisse la poser lui-même. `docs/LE-WORKFLOW.md` est créé par ce lot et
n'existe pas encore. `ROADMAP.md` reçoit la section du lot une fois livré et
perd la ligne du manque qu'il ferme, rien d'autre de ce fichier. Et
`briefs/006-la-boucle-ne-s-arrete-que-sur-une-decision.md`, ce brief.

En écriture aussi, trois fichiers de tests, où l'on **ajoute** des cas sans
toucher un contrôle déjà vert : `tests/test_echange.py`, `tests/test_feuille.py`
et `tests/test_cli.py`.

Tout autre chemin est interdit, nommément `atelier/boite.py`,
`atelier/reprise.py`, `atelier/verrou.py`, `atelier/worktree.py`,
`atelier/backends.py`, `atelier/projet.py`, `atelier/quota.py`,
`atelier/cycle.py`, `atelier/couches.py`, `crons/tour.sh`, `crons/pilote.sh`,
`crons/reveil.sh`, `crons/repartiteur.sh`, `crons/atelier-boucle`,
`crons/banc.sh`, `crons/profils/jour.sh`, `crons/profils/atelier.sh`,
`crons/crontab-repartiteur`, `profiles/forgehistory.toml`, `AGENTS.md`,
`VISION.md`, `README.md`, `docs/BOUCLES.md`, `docs/MISE-EN-PLACE.md` et les
autres briefs.

Aucun script de cron ne change. La décision du pilote est calculée en Python
par `atelier piloter` ; `crons/pilote.sh` ne fait que l'exécuter, et il n'a
rien à apprendre. Aucune boîte, aucun état de carte et aucun champ de carte
n'est créé : le rangement d'une carte libérée passe par la fonction qui range
déjà une carte en échec, avec une cause qui existe déjà dans le module de la
reprise.

## Conditions de succès

Chaque condition nomme une commande qui peut échouer, et aucune n'exige le
réseau. La lecture de l'état est injectable comme l'est déjà celle du quota :
`ATELIER_PR_CMD` nomme la commande qui rend l'état d'une PR et vaut `gh` en son
absence. Sans cette couture, aucun de ces tests ne s'écrit, et la suite
dépendrait d'un compte GitHub.

### SC1 — quatre réponses, et l'inconnu en est une

`atelier pr-etat --pr N --worktree W` imprime `ouverte`, `fusionnee`,
`fermee` ou `inconnue`, et rend 0 pour les trois premières, 2 pour la
quatrième. Trois causes distinctes donnent `inconnue` : la commande absente, la
commande muette ou en erreur, et une réponse que l'on ne sait pas lire. Aucune
ne donne `ouverte`.

```bash
python3 -m pytest tests/test_cli.py -q -k pr_etat
```

### SC2 — un lot dont la PR est ouverte n'est pas redéposé

Sur un produit d'épreuve dont une fiche est `pret` sans carte, et pour lequel
la fausse commande dit qu'une PR est ouverte sur la branche du lot,
`atelier piloter --run` ne dépose aucune carte `coder` pour ce lot, rend 0, et
nomme le lot et sa PR sur stderr.

```bash
python3 -m pytest tests/test_feuille.py -q -k "piloter and pr_ouverte"
```

### SC3 — l'inconnu retient le dépôt

Même situation, sonde muette : aucune carte n'est déposée, le tour rend 0, et
la sortie dit que le dépôt attend faute d'avoir pu lire l'état. Un test
distinct vérifie qu'aucune carte n'apparaît dans `a-coder`.

```bash
python3 -m pytest tests/test_feuille.py -q -k "piloter and pr_inconnue"
```

### SC4 — sans PR connue, le pilote dépose comme avant

Une fiche `pret` dont aucune branche ne porte de PR — le cas ordinaire d'un
lot neuf — donne exactement la décision d'aujourd'hui. La sonde n'est
interrogée que pour un lot dont la branche existe ; un lot neuf ne coûte aucun
appel.

```bash
python3 -m pytest tests/test_feuille.py -q -k "piloter and lot_neuf"
```

### SC5 — une PR fermée sans fusion libère son lot

Une carte dans `faite` ou `a-relire`, dont la PR est fermée sans fusion, est
rangée dans `echec` avec la cause `pr` et une note qui nomme le numéro, et son
verrou est levé. Le test l'observe sur les trois : la boîte de départ vide, la
carte dans `echec`, et `atelier verrous` qui ne tient plus ses fichiers.

```bash
python3 -m pytest tests/test_feuille.py -q -k "pr_fermee"
```

### SC6 — une PR fusionnée se comporte comme avant ce lot

Le rapprochement existant — fiche `livre`, carte vers `fusionnee`, verrou levé
— continue de fonctionner sans que la sonde soit consultée. La feuille reste la
source de la fusion ; ce lot n'en fait pas une seconde.

```bash
python3 -m pytest tests/test_feuille.py -q -k "rapproch"
```

### SC7 — sans sonde du tout, rien ne change

Avec `ATELIER_PR_CMD` pointant sur une commande absente et sans `gh` dans le
PATH, la suite complète passe et `atelier piloter` rend les mêmes décisions
qu'avant ce lot pour un produit sans PR.

```bash
python3 -m pytest tests/ -q
```

### SC8 — le document existe et ne cite aucune commande fausse

`docs/LE-WORKFLOW.md` explique le workflow automatique de bout en bout : le
tour d'un rôle, les boîtes et le chemin d'une carte, les causes d'échec et
lesquelles se retentent, les deux profils et la bascule, ce qui se répare tout
seul et ce qui attend une décision du propriétaire.

Un test lit le document, en extrait chaque sous-commande citée sous la forme
`atelier <mot>` ou `python3 -m atelier <mot>`, et vérifie qu'elle figure dans
l'aide de `atelier --help`. Une commande inventée fait rougir le test ; c'est
la seule façon qu'un document ait de vieillir en se faisant remarquer.

```bash
python3 -m pytest tests/test_cli.py -q -k document
```

## Hors périmètre

**Le verdict de la CI.** C'est le lot 005, dont celui-ci dépend. Lire l'état
d'une PR n'est pas lire ses contrôles, et les deux portes ne se remplacent pas.

**Fermer, rouvrir ou fusionner une PR.** L'atelier lit. La fusion est le geste
du propriétaire, et `atelier fusionner` continuera de refuser.

**Le tableau de bord.** `~/bin/atelier-vue` n'est pas dans ce dépôt et compte
encore les lignes de `/etc/cron.d/forgeatelier` pour annoncer un horaire : il
dira donc un horaire faux depuis que le crontab n'appelle qu'un répartiteur.
C'est un lot à lui, et il commence par verser l'outil dans le dépôt.

**Le banc qui se réamorce.** Le profil `atelier` joue un cycle complet puis
sort sur `RIEN`, ses deux lots étant traités ; on le relance par
`crons/banc.sh --neuf`. Une boucle d'épreuve perpétuelle est souhaitable et
n'est pas ce lot.

**Le produit ForgeHistory.** Aucun fichier du jeu ne bouge. Ce lot vit
entièrement dans l'atelier ; ce qu'il change pour ForgeHistory, c'est que sa
boucle cesse de payer deux fois le même lot.
