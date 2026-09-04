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

1. Le propriétaire donne une direction ; elle devient des fiches dans la
   feuille de route du produit. `atelier piloter` en dérive les cartes,
   il ne les devine pas.
2. Un agent écrit le brief et ouvre sa PR.
3. Un agent qui n'a pas écrit le brief le relit et rend un verdict.
4. Un autre agent exécute le lot dans le worktree de ce lot, sur sa
   branche, et fait passer la fiche à `livre` dans sa PR.
5. Un agent qui n'a pas écrit le code relit le diff et rend un verdict.
6. La CI du dépôt produit joue les tests et valide la feuille.
7. Quand tous les contrôles requis sont verts sur la révision courante,
   l'intégration fusionne — une PR à la fois, retestée sur le dernier
   `master`.

Plusieurs lots parcourent ce cycle en même temps tant que leurs
périmètres sont disjoints. Deux lots qui se disputent un fichier ne
tournent jamais ensemble.

Deux règles de rôle, et elles ne se contournent pas :

- **celui qui a écrit le code ne dit pas s'il est recevable** ;
- **personne ne fusionne sur un avis** — ni un agent, ni le
  propriétaire. Une relecture terminée n'est pas une approbation : un
  verdict est une donnée liée à la révision relue.

Ce cycle est l'intention, écrite dans [VISION.md](VISION.md) le
4 septembre 2026. La **conduite** arrive lot par lot : la série 009-023
de [ROADMAP.md](ROADMAP.md) la porte, et chaque lot apporte sa ligne à
[docs/LE-WORKFLOW.md](docs/LE-WORKFLOW.md), qui décrit ce qui tourne
aujourd'hui. Tant qu'un lot n'est pas livré, c'est l'ancienne conduite
qui tourne, et c'est LE-WORKFLOW.md qui fait foi sur ce point.

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

1. un **invariant du cycle** (auteur ≠ relecteur, aucun agent ne
   fusionne, aucune fusion sans contrôles verts sur la révision
   courante, canal d'échange git-invisible et lisible, un fichier
   n'est pas dans deux lots actifs, une fiche incohérente échoue et
   une transition interdite est refusée)
2. une **règle visible** (sans `--run` rien n'est écrit ; un quota
   inconnu vaut `-1`)
3. le **déterminisme** de la porte mécanique (même brief, même refus)

Un échantillon vide échoue. Un contrôle nomme sa mesure, pas sa cible.
