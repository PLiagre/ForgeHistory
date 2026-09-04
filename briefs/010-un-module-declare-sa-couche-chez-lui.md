# Brief 010 — Un module déclare sa couche chez lui

## But

`atelier/couches.py` cesse de tenir la liste des modules. Chaque module
déclare la couche qu'il occupe, chez lui, et la liste se dérive.

## Règle du monde

Même défaut que le point d'entrée, autre fichier. La table `MODULES`
de `atelier/couches.py` nomme les treize modules de l'atelier. Tout lot
qui ajoute un module doit y écrire une ligne — donc tenir le fichier,
donc bloquer les autres. Six des lots de cette série ajoutent un
module.

VISION.md dit : « Chaque composant de ce dépôt **déclare** une couche.
Un composant qui en occupe deux est un défaut. » Il dit *déclare*, pas
*est inscrit ailleurs*. La table centrale est une seconde source :
elle peut dire d'un module ce que le module ne dit pas de lui-même, et
personne ne s'en apercevrait — un module supprimé y reste, un module
neuf s'y oublie.

Après ce lot, `couches.MODULES` existe toujours et rend la même chose,
mais **dérivée** : elle importe le paquet `atelier`, lit l'attribut
`COUCHE` de chaque module public, et refuse celui qui n'en a pas. Les
contrôles de `tests/test_couches.py` qui passent aujourd'hui passent
sans être touchés — c'est la preuve que la dérivation est fidèle.

## Périmètre

En écriture : `atelier/couches.py`, dont la table devient une fonction
de découverte. Et l'attribut `COUCHE` posé en tête de chacun des
modules qui y sont aujourd'hui nommés : `atelier/backends.py`,
`atelier/skills_index.py`, `atelier/memoire.py`, `atelier/worktree.py`,
`atelier/cycle.py`, `atelier/etat.py`, `atelier/feuille.py`,
`atelier/verrou.py`, `atelier/quota.py`, `atelier/echange.py`,
`atelier/boite.py`, `atelier/reprise.py` et `atelier/porte.py`. Enfin
`tests/test_couches.py` pour y **ajouter** des cas, et
`briefs/010-un-module-declare-sa-couche-chez-lui.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/__main__.py`,
`atelier/projet.py`, `crons/tour.sh`, `VISION.md`, `AGENTS.md` et les
autres briefs.

Dans les treize modules, le lot n'écrit **que** la ligne de
déclaration et son commentaire. Un diff qui touche à autre chose dans
l'un d'eux sort du périmètre : ce lot est un déplacement de
déclaration, pas une retouche.

## Conditions de succès

### SC1 — la table centrale a disparu du fichier des couches

`atelier/couches.py` ne cite plus aucun nom de module.

```bash
test "$(grep -c 'atelier\.' atelier/couches.py)" -eq 0
```

### SC2 — les contrôles existants passent sans être relâchés

Les contrôles de `tests/test_couches.py` qui passent aujourd'hui
passent encore, sous les mêmes noms : le lot **ajoute** ses cas, il
n'en modifie, n'en renomme et n'en relâche aucun. Le nombre de tests
collectés dans ce fichier augmente.

```bash
python3 -m pytest tests/test_couches.py -q --collect-only
python3 -m pytest tests/test_couches.py -q
```

### SC3 — le compte se dérive des deux côtés

Un contrôle compare le nombre de modules découverts au nombre de
fichiers `.py` publics de `atelier/`, hors `__main__` et `__init__`.
Les deux comptes sont dérivés du disque ; aucun nombre attendu n'est
écrit. Un échantillon vide **échoue**.

```bash
python3 -m pytest tests/test_couches.py -q -k derive
```

### SC4 — le rouge est prouvé : un module sans couche fait rougir

Un contrôle écrit un module sans `COUCHE` dans un paquet temporaire,
lance la découverte dessus, et exige un refus qui **nomme le module**.
Sans ce cas, rien ne prouve que l'oubli se voit.

```bash
python3 -m pytest tests/test_couches.py -q -k sans_couche
```

### SC5 — une couche déclarée que personne n'occupe reste rouge

Le contrôle qui exige que toutes les couches soient occupées continue
de rougir si l'énumération gagne une valeur sans module. C'est ce
rouge que `VISION.md` invoque pour la huitième couche.

```bash
python3 -m pytest tests/test_couches.py -q -k occupees
```

### SC6 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
python3 -m atelier couches
```

## Hors périmètre

Les modules de commande : ils sont la surface du programme, pas des
composants, et la découverte ne descend pas dans le paquet. C'est ce qui
rend ce lot indépendant de celui qui découpe le point d'entrée.

L'ajout de la couche `intégration` : elle est ouverte par `VISION.md`
et occupée par le lot de l'intégrateur, pas ici. Une couche déclarée
sans module fait rougir, et c'est exactement l'effet voulu.

Le découpage du point d'entrée, qui est un lot à lui.
