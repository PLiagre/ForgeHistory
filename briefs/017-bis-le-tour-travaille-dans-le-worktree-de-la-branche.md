# Brief 017-bis — Le tour travaille dans le worktree de sa branche

## But

`crons/tour.sh` cesse de lire un répertoire par rôle. Chaque tour
travaille dans le worktree de la branche sur laquelle il écrit, et rien
ne nomme plus `ATELIER_WORKDIR_` pour un rôle qui travaille sur un lot.

## Règle du monde

Le lot 017 fournit le composant : le chemin d'un worktree se dérive de
sa branche, se crée, se reprend et se rend. Il ne change aucun tour, et
c'est délibéré — le brancher est un lot à lui, parce qu'il traverse tout
ce qui suppose *un répertoire par rôle*.

Ce que la mesure a montré le 4 septembre 2026, en essayant de faire les
deux ensemble :

1. **Le briefer n'est pas sur la branche du lot.** Il écrit son brief sur
   `brief/<lot>`, le coder son code sur `<prefixe_branche><lot>`. Un
   worktree « du lot » les mettrait dans le même répertoire, sur deux
   branches, ce qui n'existe pas. Les tours du briefer sortaient tous en
   « branche de base introuvable ».
2. **Le relecteur n'écrit pas.** Il lit une PR sur GitHub. Lui exiger un
   worktree sur une branche ajoute une dépendance à git là où il n'y en
   avait pas, et fait échouer un tour qui réussissait.
3. **Trois fichiers de contrôle encodent l'ancien design** :
   `tests/test_branche.py` vérifie que le tour du coder laisse le
   worktree *du rôle* sur la branche du lot ; `tests/test_profils.py`
   lit `$ATELIER_WORKDIR_coder` dans le profil du banc ;
   `tests/test_invocation.py` monte des produits qui ne sont pas des
   dépôts git. Ces cas ne se **calibrent** pas — ils portent une règle
   que ce lot remplace, et ils déménagent avec elle.
4. **Quatre endroits portent de la prose morte** une fois la bascule
   faite : `crons/installer-profils.sh` génère un répertoire par rôle,
   `crons/crontab` est l'ancien crontab, `atelier/commandes/postes.py`
   porte une vérification devenue vide et un conseil devenu périmé,
   `docs/MISE-EN-PLACE.md` deux paragraphes.

Qui travaille où, après ce lot :

| rôle | branche | répertoire |
|---|---|---|
| `briefer` | `brief/<lot>` | le worktree de cette branche |
| `planifier`, `coder` | `<prefixe_branche><lot>` | le worktree de cette branche |
| `relire` | aucune | le clone du produit — il lit, il n'écrit pas |
| `pilote` | aucune | son répertoire, `ATELIER_WORKDIR_pilote` |

`ATELIER_WORKDIR_pilote` **reste** : le pilote lit la feuille de route du
produit, il n'a pas de branche. Un contrôle qui interdirait toute
occurrence de `ATELIER_WORKDIR_` l'interdirait à tort.

## Périmètre

En écriture : `crons/tour.sh`, pour le répertoire de travail et la
branche déjà extraite. `crons/profils/jour.sh` et
`crons/profils/atelier.sh`, dont les variables par rôle laissent la place
à une racine de worktrees. `crons/installer-profils.sh` et
`crons/crontab`, qui cessent d'en générer — ce dernier n'a pas
d'extension, donc le lecteur de périmètre ne sait pas le tenir : ce lot
ne peut de toute façon pas tourner avec un autre. `atelier/commandes/postes.py`,
pour la vérification de `pret` et le message de `branche --run`.
`docs/LE-WORKFLOW.md` et `docs/MISE-EN-PLACE.md`. Enfin
`tests/test_branche.py`, `tests/test_profils.py`, `tests/test_roles.py`
et `tests/test_invocation.py`, dont les cas qui portent l'ancienne règle
déménagent — aucun n'est relâché, et le compte de contrôles ne baisse
pas. Et `briefs/017-bis-le-tour-travaille-dans-le-worktree-de-la-branche.md`,
ce brief.

Tout autre chemin est interdit, nommément `atelier/worktree.py`,
`atelier/projet.py`, `atelier/prise.py`, `atelier/boite.py`,
`atelier/backends.py`, `VISION.md`, `AGENTS.md` et les autres briefs.

Ce lot **n'ajoute aucune capacité** : le composant existe déjà. Un diff
qui touche `atelier/worktree.py` se trompe de lot.

## Conditions de succès

### SC1 — un tour de coder travaille dans le worktree de la branche du lot

Le worktree du rôle n'existe plus ; celui de la branche porte le travail.

```bash
python3 -m pytest tests/test_branche.py -q
```

### SC2 — un tour de briefer travaille sur sa propre branche

Il ne demande pas la branche du lot, et son tour ne sort pas en
« branche de base introuvable ».

```bash
python3 -m pytest tests/test_feuille.py -q -k briefer
```

### SC3 — un tour de relecteur n'exige aucun worktree de branche

Il tourne sur un produit qui n'est pas un dépôt git, comme avant.

```bash
python3 -m pytest tests/test_roles.py -q
```

### SC4 — deux lots avancent dans deux répertoires

Deux tours de coder d'affilée, deux lots, deux répertoires, deux
branches, les deux avancés.

```bash
python3 -m pytest tests/test_branche.py -q -k deux_lots
```

### SC5 — plus aucun répertoire par rôle, sauf le pilote

Le contrôle dérive la liste des rôles de `atelier/boite.py` et exige
qu'aucun ne soit nommé ; `pilote` n'en fait pas partie, et son cas est
vérifié séparément.

```bash
python3 -m pytest tests/test_profils.py -q -k workdir
```

### SC6 — le rouge est prouvé : un worktree de branche indisponible fait échouer le tour

La carte tombe dans `echec` avec la cause `worktree`, et aucun agent
n'est lancé.

```bash
python3 -m pytest tests/test_branche.py -q -k worktree_indisponible
```

### SC7 — aucun contrôle n'est relâché

Les cas qui déménagent gardent leur affirmation ; le nombre de tests
collectés ne baisse pas.

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le composant lui-même, livré par le lot 017.

Le relâchement du `flock` par rôle : deux tours d'un même rôle dans deux
worktrees deviennent possibles ici, mais c'est le pilote qui déposera
plusieurs cartes, et c'est un autre lot.
