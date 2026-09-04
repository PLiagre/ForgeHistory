# Brief 017 — Un lot actif a son worktree

> **Réécrit le 4 septembre 2026, après mesure.** La première version de
> ce brief disait que les cinq rôles qui travaillent sur un lot
> partageraient *un* worktree. La mesure l'a démentie : le briefer écrit
> sur `brief/<lot>` et le coder sur `<prefixe_branche><lot>`. Un
> répertoire ne peut pas être sur deux branches. Le détail de ce qui a
> été mesuré est dans le brief `017-bis`.

## But

Le répertoire de travail d'une **branche de lot** se dérive, se crée, se
reprend et se rend. Deux branches de lot ne se partagent plus un
répertoire.

## Règle du monde

Un worktree appartenait à un rôle : `ATELIER_WORKDIR_coder` était le
répertoire du coder, quel que soit le lot qu'il codait. Cette forme a
déjà coûté un lot — un agent rendait la main sur un répertoire sale, le
lot d'après échouait — et `atelier ranger` a fermé la panne sans changer
la forme.

Le cycle automatique la rouvre en grand : deux coders qui tournent en
même temps dans le même répertoire ne peuvent pas être sur deux branches
à la fois. Il n'y a pas de contre-mesure à ça.

**Ce qu'un worktree suit, c'est une branche, pas un lot.** Un lot en
porte deux au cours de sa vie : celle sur laquelle son brief s'écrit, et
celle sur laquelle son code s'écrit. Elles ne vivent jamais en même
temps — le brief est intégré avant que le code commence — mais elles ne
tiennent pas dans le même répertoire.

Le préfixe de la branche de brief n'existe nulle part dans le
branchement : il est écrit en dur dans le prompt du briefer,
`atelier/backends.py`. Un chemin de worktree qui en dépend ne peut pas
se dériver tant qu'il est là. Il entre dans `atelier.toml`, à côté de
`prefixe_branche`, et une absence se déclare — un branchement muet garde
le préfixe d'aujourd'hui et le **dit**.

Ce lot fournit le composant et la commande. Il ne change aucun tour,
aucun profil, aucun cron : c'est `017-bis`.

## Périmètre

En écriture : `atelier/worktree.py`, pour le chemin dérivé, la création,
la reprise et la libération. `atelier/projet.py`, pour le préfixe de la
branche de brief et les deux branches qu'un lot peut porter.
`atelier/commandes/worktree.py`, pour la commande. `tests/test_run.py`
pour y **ajouter** des cas : il tourne sur les deux systèmes. Enfin
`briefs/017-un-lot-actif-a-son-worktree.md`, ce brief.

Tout autre chemin est interdit, nommément `tests/test_branche.py` — qui
n'est pas collectable sans `fcntl`, donc invérifiable par l'auteur, et
dont les cas déménagent avec `017-bis` — ainsi que `crons/tour.sh`,
`crons/profils/jour.sh`, `crons/profils/atelier.sh`,
`atelier/commandes/postes.py`, `atelier/backends.py`, `atelier/boite.py`,
`atelier/prise.py`, `docs/LE-WORKFLOW.md`, `VISION.md`, `AGENTS.md` et
les autres briefs.

Le refus de détruire du travail non enregistré ne s'assouplit pas. Un
worktree sale se **range**, il ne se remet pas à zéro.

## Conditions de succès

### SC1 — le chemin se dérive de la branche, il ne se choisit pas

Deux branches différentes rendent deux chemins différents ; la même
branche rend deux fois le même. La racine où ils vivent se lit dans
l'environnement ; à défaut, c'est à côté du produit.

```bash
python3 -m pytest tests/test_run.py -q -k chemin_derive
```

### SC2 — un lot porte deux branches, et l'atelier les nomme

Le branchement dit le préfixe de la branche de code et celui de la
branche de brief. Un branchement qui ne nomme pas le second garde celui
d'aujourd'hui et le déclare sur `stderr` : une absence se déclare.

```bash
python3 -m pytest tests/test_run.py -q -k deux_branches
```

### SC3 — sans `--run`, rien n'est créé

```bash
python3 -m pytest tests/test_run.py -q -k apercu
```

### SC4 — un worktree existant se reprend, il ne se recrée pas

Un second appel rend le même chemin, garde le travail qui s'y trouve, et
ne rend pas d'erreur.

```bash
python3 -m pytest tests/test_run.py -q -k reprise
```

### SC5 — le rouge est prouvé : un worktree sale n'est pas effacé

Le contrôle vérifie ensuite que le fichier qui traînait est **toujours
là**.

```bash
python3 -m pytest tests/test_run.py -q -k sale
```

### SC6 — la libération rend le répertoire et la branche reste

C'est elle qui porte la PR.

```bash
python3 -m pytest tests/test_run.py -q -k libere
```

### SC7 — deux branches de lot travaillent dans deux répertoires

Un fichier écrit dans l'un n'apparaît pas dans l'autre.

```bash
python3 -m pytest tests/test_run.py -q -k deux_worktrees
```

### SC8 — la suite existante reste verte et grossit

Aucun contrôle déjà vert n'est modifié : ce lot ne touche à aucun tour.

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le tour, les profils et les crons : c'est `017-bis`, et il est en
collision avec plusieurs lots de la série.

Le moment où un worktree se libère : ce lot fournit le geste,
l'intégrateur décide du moment.
