# AGENTS.md — les règles de l'atelier

Le seul fichier de règles de ce dépôt. Il ne paraphrase pas
[VISION.md](VISION.md). En cas de contradiction, VISION.md fait foi
pour l'intention, ce fichier pour la conduite.

Les règles du *jeu* vivent dans le dépôt produit. Ici on ne les
recopie pas.

---

## Le projet en trois phrases

ForgeAtelier est l'infrastructure dont les agents ont besoin pour
exécuter un lot. Le produit est `atelier/` — `python3 -m atelier`.
Il se branche sur un dépôt produit via `atelier.toml`.

## Langue

Tout ce qui s'écrit ici est en **français clair** : commits, briefs,
commentaires. Phrases courtes. Un terme technique s'explique une fois.

## Le cycle

1. Le propriétaire place un lot dans la feuille de route du produit ;
   `atelier piloter` en dérive la prochaine carte, il ne la devine pas.
2. Un agent écrit le brief et ouvre sa PR ; le propriétaire fusionne.
3. Un autre agent l'exécute dans un worktree, sur une branche à lui, et
   fait passer la fiche du lot à `livre` dans sa PR.
4. Un agent qui n'a pas écrit le code relit le diff.
5. La CI du dépôt produit joue les tests et valide la feuille.
6. Le propriétaire fusionne.

Pas de fusion ici. Pas de verdict qui remplace l'œil.

**La seule règle de rôle : celui qui a écrit le code ne dit pas s'il
est recevable.**

## Le brief

Le format à cinq sections est une règle du *produit* (chez ForgeHistory :
`AGENTS.md`). L'atelier exige que le fichier existe, qu'il ait un
périmètre d'écriture, et que chaque condition de succès nomme une
commande qui peut échouer. Il n'invente pas le fond.

Six façons de rater un brief — la liste fait foi dans le dépôt
produit. L'atelier en tient quatre, mécaniquement :

1. un périmètre qui n'est pas une liste de fichiers
2. un critère sans commande observable
3. un échantillon vide accepté
4. l'auteur du code qui signe la revue

## Les couches

Un module de `atelier/` déclare une couche, une seule. Les tests
`tests/test_couches.py` le tiennent.

## Les commandes

`python3` sur Linux, `py` sur le PC Windows du propriétaire, jamais
`python` nu.

```bash
python3 -m atelier doctor --projet /chemin/du/produit
python3 -m atelier portes --brief /chemin/du/brief.md
python3 -m atelier start /chemin/du/brief.md --projet /chemin/du/produit
python3 -m atelier start /chemin/du/brief.md --projet /chemin/du/produit --run
python3 -m atelier status --projet /chemin/du/produit
python3 -m atelier feuille valider --projet /chemin/du/produit
python3 -m atelier piloter --projet /chemin/du/produit
python3 -m atelier branche --projet /chemin/du/produit --lot 047-slug
python3 -m atelier branche --projet /chemin/du/produit --lot 047-slug \
    --worktree /chemin/du/worktree-coder --run
python3 -m atelier pr --fichier /chemin/atelier-echange/pr.txt
python3 -m pytest tests/ -q
```

Sans `--run`, `start`, `piloter` et `branche` sont des aperçus.

## La feuille de route

L'état d'un lot s'écrit à un seul endroit : sa fiche dans le registre de
la feuille de route du produit (`[projet].feuille`). L'atelier la lit,
la valide, et en dérive la prochaine carte ; il ne lit jamais de prose
pour décider. Le format des fiches, les six états et les transitions
vivent avec le registre, dans le dépôt produit.

## Tests

Un test existe s'il protège l'une de ces trois choses, et seulement :

1. un **invariant du cycle** (auteur ≠ relecteur, pas de fusion,
   canal d'échange git-invisible et lisible, un fichier n'est pas
   dans deux lots actifs, une fiche incohérente échoue et une
   transition interdite est refusée)
2. une **règle visible** (sans `--run` rien n'est écrit ; un quota
   inconnu vaut `-1`)
3. le **déterminisme** de la porte mécanique (même brief, même refus)

Un échantillon vide échoue. Un contrôle nomme sa mesure, pas sa cible.
