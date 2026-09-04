# Brief 017 — Un lot actif a son worktree

## But

Un lot qui avance possède son répertoire de travail, sur sa branche.
Deux lots ne se partagent plus un répertoire, et l'un ne salit plus
celui de l'autre.

## Règle du monde

Aujourd'hui, un worktree appartient à un **rôle** :
`ATELIER_WORKDIR_coder` est le répertoire du coder, quel que soit le lot
qu'il code. `crons/tour.sh` y extrait la branche du lot, y invoque
l'agent, et le lot suivant réutilise le même répertoire.

Cette forme a déjà coûté : un agent qui rend la main sur un répertoire
sale faisait échouer le lot d'après. La contre-mesure — `atelier ranger`,
qui enregistre ce qui traîne sur la branche du lot — a fermé la panne
sans changer la forme. Le cycle automatique la rouvre en grand : deux
coders qui tournent en même temps dans le même répertoire ne peuvent pas
être sur deux branches à la fois. Il n'y a pas de contre-mesure à ça.

Un worktree par **lot** :

- `atelier worktree --projet P --lot L` rend le chemin du worktree du
  lot, dérivé du nom du produit et du slug — jamais choisi par
  l'appelant, jamais recopié dans un script ;
- avec `--run`, il le crée s'il manque, sur la branche du lot dérivée de
  `prefixe_branche`, depuis la base du produit ; il le reprend s'il
  existe. Il ne fait jamais de `reset --hard` ni de `checkout -f` —
  `atelier/worktree.py` porte déjà ce refus, et il ne s'assouplit pas ;
- `atelier worktree --projet P --lot L --liberer --run` le retire, une
  fois le lot intégré. Un worktree par lot qui ne se rend jamais finit
  par remplir le disque : la libération fait partie du composant, pas
  d'un nettoyage qu'on se rappellera de faire.

Les rôles qui ne travaillent pas sur un lot gardent leur répertoire : le
pilote lit la feuille de route du produit, l'éclaireur lit le dépôt.
Ceux qui travaillent sur un lot — briefer, relire-brief, planifier,
coder, relire — travaillent dans le worktree de ce lot.

## Périmètre

En écriture : `atelier/worktree.py`, qui gagne le chemin dérivé, la
reprise et la libération. `atelier/commandes/worktree.py` pour la
commande. `crons/tour.sh`, qui demande le worktree du lot au lieu de
lire `ATELIER_WORKDIR_<role>` pour les rôles qui travaillent sur un lot.
`crons/profils/jour.sh` et `crons/profils/atelier.sh`, dont les
variables par rôle disparaissent au profit d'une racine de worktrees.
`docs/LE-WORKFLOW.md`, pour le tour d'un rôle. `tests/test_branche.py`
et `tests/test_run.py` pour y **ajouter** des cas. Enfin
`briefs/017-un-lot-actif-a-son-worktree.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/prise.py`,
`atelier/boite.py`, `atelier/verrou.py`, `atelier/feuille.py`,
`crons/banc.sh`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le refus de détruire du travail non enregistré ne s'assouplit pas. Un
worktree de lot sale se **range**, il ne se remet pas à zéro.

## Conditions de succès

### SC1 — le chemin se dérive, il ne se choisit pas

Le chemin vient du nom du produit et du slug du lot. Deux lots
différents rendent deux chemins différents ; le même lot rend deux fois
le même. Aucun script ne le compose.

```bash
python3 -m pytest tests/test_branche.py -q -k chemin_derive
```

### SC2 — deux lots disjoints travaillent dans deux worktrees

Deux lots pris en même temps sont sur deux branches, dans deux
répertoires, et un fichier écrit dans l'un n'apparaît pas dans l'autre.

```bash
python3 -m pytest tests/test_run.py -q -k deux_worktrees
```

### SC3 — sans `--run`, rien n'est créé

La commande imprime le chemin ; le répertoire n'existe pas après.

```bash
python3 -m pytest tests/test_branche.py -q -k apercu
```

### SC4 — un worktree existant se reprend, il ne se recrée pas

Un second appel sur le même lot rend le même chemin, garde le travail
qui s'y trouve, et ne rend pas d'erreur.

```bash
python3 -m pytest tests/test_branche.py -q -k reprise
```

### SC5 — le rouge est prouvé : un worktree sale n'est pas effacé

Un worktree du lot qui porte des modifications non enregistrées fait
rendre une erreur nommée. Le contrôle vérifie ensuite que le fichier
qui traînait est **toujours là**.

```bash
python3 -m pytest tests/test_branche.py -q -k sale
```

### SC6 — la libération rend le répertoire et la branche reste

Après `--liberer --run`, le répertoire n'existe plus et la branche du
lot existe encore : c'est elle qui porte la PR.

```bash
python3 -m pytest tests/test_branche.py -q -k libere
```

### SC7 — plus aucune variable de worktree par rôle

```bash
! grep -rn 'ATELIER_WORKDIR_' crons/ atelier/ docs/
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le banc et ses worktrees d'épreuve, qui suivent dans leur propre lot.

Quand libérer : ce lot fournit le geste, l'intégrateur décide du moment.
