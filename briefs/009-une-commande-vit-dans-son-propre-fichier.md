# Brief 009 — Une commande vit dans son propre fichier

## But

Ajouter une commande à l'atelier cesse de toucher `atelier/__main__.py`.
Deux lots qui apportent chacun leur commande ont alors des périmètres
disjoints, et peuvent s'écrire en même temps.

## Règle du monde

Le cycle automatique demande que plusieurs lots avancent de front quand
leurs périmètres sont disjoints. Le verrou de l'atelier est **par
fichier** : deux lots qui nomment le même fichier ne tournent jamais
ensemble, et c'est voulu.

`atelier/__main__.py` pèse 1 121 lignes et porte les vingt-cinq
`add_parser` du programme. Chacun des lots de cette série apporte au
moins une commande. Tant que le point d'entrée est une table centrale,
**aucun d'eux n'est parallélisable** — pas par accident, par
construction. Le goulot n'est pas la plomberie : c'est un registre que
tout le monde doit éditer.

La règle est celle que ce dépôt applique déjà aux couches et aux rôles :
**un composant se déclare chez lui**. Le centre ne tient plus de liste ;
il découvre. Une liste centrale a exactement un défaut, et il est
mécanique : elle sérialise ceux qui doivent y écrire.

Rien du comportement des commandes ne change. C'est un déménagement,
pas une réécriture : une commande qui répondait `2` répond `2`, un
message d'erreur ne se reformule pas, un `--champ` ne se renomme pas.

## Périmètre

En écriture : `atelier/__main__.py`, qui devient un répartiteur —
il construit le parseur, découvre les modules de commande, appelle
celui que l'utilisateur a nommé, et rien d'autre.
`atelier/commandes/__init__.py`, qui porte la découverte et le contrat
qu'un module de commande respecte. Puis les modules qui reçoivent
l'existant, groupés par sujet et non par ordre alphabétique :
`atelier/commandes/noyau.py` (`couches`, `doctor`, `portes`, `start`,
`status`, `fusionner`, `hop`, `fumee`, `canal`, `ranger`),
`atelier/commandes/cartes.py` (`prochain`, `deposer`, `avancer`,
`echouer`, `rappeler`, `reprendre`, `verrous`, `verrouiller`, `lever`),
`atelier/commandes/roadmap.py` (`feuille`, `piloter`),
`atelier/commandes/github.py` (`pr`, `ci`, `pr-etat`) et
`atelier/commandes/postes.py` (`poste`, `pret`, `invocation`,
`branche`). Enfin `tests/test_commandes.py` pour les contrôles de ce
lot, `tests/test_cli.py` pour y **ajouter** des cas, et
`briefs/009-une-commande-vit-dans-son-propre-fichier.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/boite.py`,
`atelier/feuille.py`, `atelier/echange.py`, `atelier/backends.py`,
`atelier/couches.py`, `atelier/projet.py`, `atelier/verrou.py`,
`atelier/worktree.py`, `atelier/cycle.py`, `atelier/porte.py`,
`atelier/reprise.py`, `crons/tour.sh`, `VISION.md`, `AGENTS.md` et les
autres briefs.

Aucun message, aucun code de sortie, aucun nom d'option ne change. Un
diff qui reformule une phrase d'erreur sort du périmètre : la phrase
déménage, elle ne se réécrit pas.

Un module de commande **n'occupe pas de couche** : il ne raisonne pas,
il appelle. C'est la surface du programme, pas un composant. Il n'entre
donc pas dans `atelier/couches.py`, et ce lot est indépendant de celui
qui déplace la déclaration des couches.

## Conditions de succès

### SC1 — le point d'entrée ne tient plus de table

`atelier/__main__.py` ne contient plus aucun `add_parser`, et ne cite
plus aucun nom de commande.

```bash
test "$(grep -c 'add_parser' atelier/__main__.py)" -eq 0
```

### SC2 — toutes les commandes citées par les documents répondent encore

Le dénominateur se dérive des documents, il ne s'écrit pas dans le
test : `tests/test_cli.py` extrait déjà les commandes citées par
`AGENTS.md` et `docs/LE-WORKFLOW.md`. Un cas ajouté exige que chacune
réponde à `--help` avec le code 0. Un échantillon vide **échoue**.

```bash
python3 -m pytest tests/test_cli.py -q
```

### SC3 — le compte des commandes se dérive, des deux côtés

Un contrôle compare le nombre de commandes que le parseur expose au
nombre de commandes que les modules de `atelier/commandes/` déclarent.
Les deux comptes sont dérivés ; aucun nombre attendu n'est écrit.

```bash
python3 -m pytest tests/test_commandes.py -q
```

### SC4 — le rouge est prouvé : un module qui ne déclare rien est refusé

Un contrôle écrit un module de commande incomplet dans un répertoire
temporaire, le donne à la découverte, et exige un refus nommé — pas un
silence, pas un `AttributeError`.

```bash
python3 -m pytest tests/test_commandes.py -q -k refus
```

### SC5 — une commande neuve ne touche pas le répartiteur

Un contrôle ajoute un module de commande jetable, vérifie qu'il est
découvert et appelable, et vérifie que `atelier/__main__.py` n'a pas
été lu pour cela : le fichier du répartiteur ne cite pas son nom.

```bash
python3 -m pytest tests/test_commandes.py -q -k decouverte
```

### SC6 — la suite existante reste verte et grossit

Aucun contrôle déjà vert n'est modifié, renommé ni relâché ; le nombre
de tests collectés augmente.

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le comportement des commandes. Les commandes que les lots suivants
apportent : ce lot déménage ce qui existe, il n'anticipe rien. Le
découpage de `crons/tour.sh`, qui est l'autre goulot de la série et qui
reste sériel — le shell se découpe moins bien que le Python, et la
série l'assume.
