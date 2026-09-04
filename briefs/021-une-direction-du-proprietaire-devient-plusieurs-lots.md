# Brief 021 — Une direction du propriétaire devient plusieurs lots

## But

Le propriétaire écrit une phrase. Plusieurs lots se retrouvent
`a-briefer` dans la feuille de route du produit, sans qu'il ait touché
au registre.

## Règle du monde

Ce qui reste au propriétaire, c'est de donner des directions. Aujourd'hui
« donner une direction » veut dire : ouvrir `ROADMAP.md`, écrire une
fiche, choisir un numéro libre, un titre, un chemin de brief, une
couche, des dépendances, poser `état : a-briefer`, faire passer la
validation, ouvrir une PR, la fusionner. Pour un lot. Le pilote ne
dépose une carte de briefer que si une fiche existe déjà — c'est le
registre qui fait autorité, et c'est bien.

Une direction est donc traduite en fiches, et quelqu'un doit le faire.
Ce n'est pas l'éclaireur : il est en lecture seule et il propose, il ne
crée rien. Ce n'est pas le briefer : il écrit **un** brief pour **un**
lot déjà inscrit.

C'est un rôle de plus, `cadrer`, et son travail est exactement un
découpage :

- `atelier direction --projet P --texte "…"` écrit la direction dans
  `.atelier/directions/`, horodatée, non consommée. C'est le seul geste
  du propriétaire. Un `--fichier` fait la même chose pour un texte plus
  long ;
- `atelier piloter` voit une direction non consommée et dépose une carte
  pour `cadrer` — une seule à la fois : découper deux directions en même
  temps produit des lots qui se recouvrent ;
- le rôle `cadrer` lit la direction, la vision du produit, son modèle et
  sa feuille de route, et ouvre **une** PR qui ajoute N fiches
  `a-briefer` au registre. Il n'écrit aucun brief, ne touche à aucune
  fiche existante, ne change aucune priorité, ne code rien ;
- cette PR passe par la relecture de brief et l'intégration mécanique,
  comme les autres. Rien n'entre dans le registre sans être relu ;
- une fois la PR intégrée, la direction est marquée consommée, avec les
  numéros qu'elle a produits. Une direction consommée ne se rejoue pas :
  sans cette marque, chaque réveil du pilote redécoupe la même phrase.

Deux gardes, parce que ce rôle écrit dans le seul document qui fasse
autorité :

- **il n'ajoute que des fiches neuves.** Une PR de cadrage qui modifie
  ou supprime une fiche existante est refusée par la validation de la
  feuille — les transitions le disent déjà, et le lot les invoque au
  lieu de les réécrire ;
- **il ne fabrique pas de brief.** Une fiche `a-briefer` dont le fichier
  de brief existe déjà fait rougir la validation. C'est la garde qui
  empêche le cadreur de déborder sur le briefer.

Combien de fiches ? Ce que la direction contient, borné par le plafond
du branchement. Une direction qui donnerait trente lots est une
direction mal posée, et le rôle le **dit** au lieu d'en fabriquer trente.

## Périmètre

En écriture : `atelier/direction.py`, le composant — écriture, lecture,
marque de consommation. `atelier/commandes/direction.py` pour la
commande. `atelier/feuille.py`, pour que le pilote voie une direction non
consommée et pour la marque après intégration. `atelier/boite.py`, pour
le rôle `cadrer` et sa boîte. `atelier/backends.py`, pour son prompt et
le champ de `[roles]` qu'il lit. `crons/tour.sh` et
`crons/profils/jour.sh`, pour son tour et son réveil.
`docs/LE-WORKFLOW.md`. `tests/test_direction.py`,
`tests/test_feuille.py` et `tests/test_boite.py` pour y **ajouter** des
cas. Enfin
`briefs/021-une-direction-du-proprietaire-devient-plusieurs-lots.md`, ce
brief.

Tout autre chemin est interdit, nommément `atelier/verdict.py`,
`atelier/integration.py`, `atelier/porte.py`, `atelier/verrou.py`,
`atelier/prise.py`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le cadreur n'écrit pas de brief et le lot ne lui en donne pas le droit :
son prompt le lui interdit, et la validation de la feuille l'attrape
s'il essaie quand même.

## Conditions de succès

### SC1 — une phrase produit plusieurs fiches `a-briefer`

Le scénario de bout en bout sur un produit jetable : une direction, une
carte de cadrage, une PR, N fiches. N est compté sur le registre après
intégration, pas écrit dans le contrôle.

```bash
python3 -m pytest tests/test_direction.py -q -k bout_en_bout
```

### SC2 — le rouge est prouvé : une fiche existante modifiée est refusée

Une PR de cadrage qui touche à une fiche déjà là fait échouer la
validation de la feuille, et le message nomme le numéro.

```bash
python3 -m pytest tests/test_direction.py -q -k fiche_existante
```

### SC3 — le cadreur ne fabrique pas de brief

Une PR de cadrage qui apporte un fichier de brief fait rougir la
validation : une fiche `a-briefer` dont le brief existe est un mensonge.

```bash
python3 -m pytest tests/test_direction.py -q -k pas_de_brief
```

### SC4 — une direction consommée ne se rejoue pas

Deux appels de pilote d'affilée : une seule carte de cadrage, et la
seconde fois le pilote ne dépose rien.

```bash
python3 -m pytest tests/test_direction.py -q -k consommee
```

### SC5 — une seule direction se découpe à la fois

Deux directions non consommées : une seule carte de cadrage.

```bash
python3 -m pytest tests/test_direction.py -q -k une_a_la_fois
```

### SC6 — le plafond arrête le découpage, et le dit

Une direction qui dépasse le plafond du branchement fait rendre une
erreur nommée, et aucune fiche n'est ajoutée. Le plafond se lit dans le
branchement.

```bash
python3 -m pytest tests/test_direction.py -q -k plafond
```

### SC7 — sans direction, rien ne change

Un produit sans direction non consommée fait un pilote identique à
celui d'avant le lot.

```bash
python3 -m pytest tests/test_feuille.py -q
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

La qualité du découpage : c'est le rôle du relecteur de brief et, en
amont, de l'éclaireur qui propose. Ce lot fournit le chemin, pas le
jugement.

L'écriture des briefs eux-mêmes, qui reste au briefer.
