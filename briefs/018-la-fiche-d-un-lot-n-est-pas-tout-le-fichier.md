# Brief 018 — La fiche d'un lot n'est pas tout le fichier

## But

Deux lots peuvent tenir deux fiches du même registre. Le verrou cesse de
confondre « la fiche du lot 046 » avec « la feuille de route ».

## Règle du monde

La fiche d'un lot fait partie du périmètre implicite de sa PR : c'est
par elle que le registre passe à `livre` au moment exact de
l'intégration. Tout brief la nomme donc, et tout brief nomme le même
fichier — `ROADMAP.md` chez ForgeHistory.

Le verrou de l'atelier tient des **fichiers**. Conséquence mécanique :
tant que la fiche est « le fichier de la feuille », **aucun lot n'est
jamais disjoint d'aucun autre**. Le parallélisme est impossible, non pas
par prudence mais par une confusion de granularité. C'est le défaut le
plus cher de la série, et le moins visible : rien ne le signale, la file
avance simplement une carte à la fois pour une raison que personne n'a
décidée.

Le format du registre a déjà tout prévu. Une fiche tient sur deux
lignes, séparée de la suivante par une ligne vide, « et c'est cette
ligne vide qui permet à deux PR de lots voisins de fusionner sans
conflit ». Git sait déjà fusionner deux fiches voisines. C'est le verrou
qui ne sait pas les distinguer.

Après ce lot, une **ressource** tenue par un lot est soit un fichier,
soit une fiche :

```
sim/aggregation.py          un fichier
ROADMAP.md#047              la fiche du lot 047
```

- deux lots qui tiennent deux fiches différentes du même registre ne
  sont pas en collision ;
- deux lots qui tiendraient la même fiche le sont — mais ce cas est
  déjà impossible : une fiche appartient à son lot ;
- un lot qui nomme le **fichier entier** de la feuille dans son
  périmètre écrit est en collision avec tous les autres, et l'atelier le
  **dit** au lieu de le subir. Un lot d'exploitation qui réorganise la
  feuille a le droit d'exister ; il a juste le droit d'être seul.

La fiche du lot n'a plus à être nommée dans le périmètre écrit d'un
brief : elle s'ajoute toute seule, dérivée du numéro du lot. C'est ce
qui la rend impossible à oublier, et impossible à élargir.

## Périmètre

En écriture : `atelier/verrou.py`, pour la ressource — fichier ou fiche
— et la collision qui les distingue. `atelier/cycle.py`, pour que le
lecteur de périmètre ajoute la fiche du lot et refuse le fichier entier
de la feuille. `atelier/projet.py`, pour dire au lecteur quel fichier
est la feuille. `tests/test_verrou.py` et `tests/test_cycle.py` pour y
**ajouter** des cas. Enfin
`briefs/018-la-fiche-d-un-lot-n-est-pas-tout-le-fichier.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/feuille.py`,
`atelier/boite.py`, `atelier/prise.py`, `atelier/worktree.py`,
`crons/tour.sh`, `VISION.md`, `AGENTS.md` et les autres briefs.

La forme du registre ne change pas : ni les repères, ni les fiches, ni
les champs, ni la ligne vide. Ce lot apprend au verrou à lire ce qui
existe déjà.

## Conditions de succès

### SC1 — deux fiches du même registre ne sont pas en collision

Deux lots, deux fiches, un même fichier de feuille : les deux verrous se
posent.

```bash
python3 -m pytest tests/test_verrou.py -q -k deux_fiches
```

### SC2 — le rouge est prouvé : le fichier entier reste en collision

Un lot qui nomme le fichier de la feuille dans son périmètre entre en
collision avec un lot qui tient une fiche de ce fichier, et la collision
**nomme la feuille**. Sans ce cas, le lot aurait simplement désarmé le
verrou sur la feuille.

```bash
python3 -m pytest tests/test_verrou.py -q -k feuille_entiere
```

### SC3 — la fiche s'ajoute au périmètre, elle ne se demande pas

Le périmètre lu d'un brief qui ne cite pas la feuille contient quand
même la fiche de son lot, dérivée de son numéro.

```bash
python3 -m pytest tests/test_cycle.py -q -k fiche_implicite
```

### SC4 — un périmètre qui élargit la fiche est refusé

Un brief dont le périmètre nomme la fiche d'un **autre** lot est
refusé, et le refus nomme les deux numéros.

```bash
python3 -m pytest tests/test_cycle.py -q -k fiche_etrangere
```

### SC5 — sans feuille déclarée, rien ne change

Un branchement qui ne nomme pas `[projet].feuille` continue de tenir des
fichiers, et aucune fiche n'apparaît. L'atelier ne cherche pas un
registre au hasard.

```bash
python3 -m pytest tests/test_cycle.py -q -k sans_feuille
```

### SC6 — le compte des ressources tenues se dérive

`atelier verrous` affiche fichiers et fiches, et le total qu'il annonce
est compté sur ce qu'il affiche. Un échantillon vide **échoue**.

```bash
python3 -m pytest tests/test_verrou.py -q -k compte
```

### SC7 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le dépôt de plusieurs cartes, qui suit dans son propre lot : ce lot rend
le parallélisme **possible**, il ne le déclenche pas.

Le format du registre, qui vit dans le dépôt produit.
