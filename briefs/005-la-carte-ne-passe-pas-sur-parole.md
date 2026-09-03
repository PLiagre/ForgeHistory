# Brief 005 — Une carte ne passe pas sur parole

## But

Le 3 septembre 2026, le premier tour complet de l'atelier a livré le lot 046
de ForgeHistory : PR ouverte, fiche marquée `livre`, carte avancée jusqu'au
relecteur, qui l'a relue et rangée dans `faite`. L'agent avait écrit dans son
compte rendu : « 164 passent, 3 échecs identiques à `master` (préexistants) ».

La CI a dit l'inverse : `sim` rouge, `3 failed, 149 passed`, et les quatre
dernières exécutions sur `master` étaient vertes. Ce n'étaient pas des échecs
préexistants, c'étaient des régressions.

Rien dans l'atelier n'a démenti l'agent, parce que rien ne l'a mesuré. Entre
« l'agent s'est arrêté » et la boîte `a-relire`, `crons/tour.sh` ne demande que
deux choses : un code de sortie nul, et un entier positif dans
`atelier-echange/pr.txt`. Le relecteur a donc été payé pour relire du code
cassé — et il l'a dit lui-même, faute de pouvoir exécuter quoi que ce soit :
« à confirmer par pytest ».

Ce lot pose la porte manquante. Avant d'invoquer le relecteur, le tour de
`relire` lit le verdict des contrôles obligatoires de la PR que porte la carte.

État de départ, mesuré par :

```bash
grep -rn "pr checks" atelier/ crons/
```

Aucune ligne aujourd'hui : l'atelier ne lit jamais un verdict de CI. Si cette
commande rend quelque chose, le lot est caduc.

Ce lot ne dépend d'aucun autre. Il n'ouvre rien qu'un lot aval devrait
attendre, sauf une chose qu'il ne fait pas : transmettre le verdict au prompt
du relecteur, pour qu'il relise en sachant ce qui casse. Ce sera un autre lot,
et il supposera cette porte posée.

## Règle du monde

L'atelier ne simule aucun monde : ce lot ne change aucun nombre du jeu et ne
touche à aucun dépôt produit. La règle en jeu est celle de l'atelier, et elle
est déjà écrite deux fois dans ce dépôt.

`atelier/quota.py` : « Un inconnu vaut -1, jamais 0. Un zéro peut être une
vraie mesure. » `atelier/echange.py` : « Si `gh` répond, la PR doit être sur
`branche`. Sinon on se tait. »

Ce lot applique la même règle à un troisième objet — le verdict des tests. Un
seul point demande une décision, et c'est la conséquence de l'inconnu.

Un désaccord de branche non mesuré est un détail : on se tait, on passe. Un
verdict de tests non mesuré est **toute la porte** : si l'inconnu passait, la
porte s'ouvrirait à chaque hoquet de `gh`, et on aurait construit une garde qui
cède exactement quand elle ne répond plus. Ici l'inconnu **attend**. La carte
ne bouge pas, le relecteur n'est pas invoqué, le tour sort 0, et le prochain
réveil de `relire` redemandera — il y en a quatre par jour dans le crontab
livré. Une carte qui attend se voit dans `a-relire` ; une porte qui s'ouvre
toute seule ne se voit nulle part.

Le verdict lu est celui des contrôles **obligatoires** de la branche de base,
pas de tous les contrôles. C'est la même liste qui gouverne le bouton de
fusion : la porte de l'atelier et la porte de GitHub disent alors la même
chose, et un contrôle tiers instable ne bloque pas un lot que GitHub aurait
accepté.

## Périmètre

En écriture : `atelier/echange.py`, où la sonde `gh` vit déjà avec sa doctrine
du silence et où elle apprendra à rendre un verdict. `atelier/__main__.py`,
pour la commande `atelier ci`. `crons/tour.sh`, pour la porte elle-même, au
début du tour de `relire`. `tests/test_echange.py`, `tests/test_cli.py` et
`tests/test_run.py`, pour y **ajouter** des cas — aucun contrôle déjà vert
n'est modifié. `ROADMAP.md`, pour la section du lot une fois livré et pour la
ligne du manque qu'il ferme, rien d'autre de ce fichier. Et
`briefs/005-la-carte-ne-passe-pas-sur-parole.md`, ce brief.

Tout autre chemin est interdit, nommément : `atelier/boite.py`,
`atelier/feuille.py`, `atelier/backends.py`, `atelier/verrou.py`,
`atelier/projet.py`, `atelier/quota.py`, `atelier/worktree.py`,
`atelier/cycle.py`, `crons/pilote.sh`, `crons/reveil.sh`, `crons/veille.sh`,
`crons/crontab`, `crons/installer-profils.sh`, `profiles/forgehistory.toml`,
`AGENTS.md`, `VISION.md`, `README.md`, `pyproject.toml`, et les autres briefs.

Le rangement d'une carte refusée passe par la commande qui existe déjà,
`atelier echouer` : aucune boîte, aucun état et aucun champ de carte n'est
créé, et le module des boîtes n'a pas à changer.

## Conditions de succès

Chaque condition nomme une commande qui peut échouer. Aucune n'exige de
réseau : la lecture est injectable, comme l'est déjà celle du quota
(`ATELIER_QUOTA_CMD`). `ATELIER_CI_CMD` nomme la commande qui rend le verdict
et vaut `gh` en son absence ; sans cette couture, aucun de ces tests ne
s'écrit.

### SC1 — trois verdicts, trois codes de sortie

`atelier ci --projet P --pr N` rend 0 quand tous les contrôles obligatoires
sont au vert, 1 quand au moins un est rouge, 2 quand le verdict n'est pas
lisible. Les trois cas sont exercés par une fausse commande que le test pose
lui-même, jamais par le réseau.

```bash
python3 -m pytest tests/test_cli.py -q -k ci
```

### SC2 — rouge nomme les fautifs

La sortie du cas rouge imprime le nom de chaque contrôle en échec, un par
ligne. Un compte sans nom ne passe pas : c'est le nom qui dit au propriétaire
quoi regarder.

```bash
python3 -m pytest tests/test_echange.py -q -k "ci and rouge"
```

### SC3 — l'inconnu n'est ni vert ni rouge

Trois causes distinctes rendent 2 et le mot « inconnu » : la commande absente,
la commande muette ou en erreur, et des contrôles encore en cours. Aucune ne
rend 0.

```bash
python3 -m pytest tests/test_echange.py -q -k "ci and inconnu"
```

### SC4 — le verdict est celui qui gouverne la fusion

L'appel construit demande les contrôles **obligatoires**, pas tous. Le test
l'observe sur les arguments reçus par la fausse commande.

```bash
python3 -m pytest tests/test_echange.py -q -k "ci and obligatoires"
```

### SC5 — rouge : la carte ne va pas au relecteur

Un tour `relire` sous `ATELIER_INVOQUER=1`, avec un verdict rouge, range la
carte dans `echec/` avec les contrôles fautifs dans sa `note`, ne lance
**aucun** binaire d'agent, et rend un code non nul.

```bash
python3 -m pytest tests/test_run.py -q -k "relire and rouge"
```

### SC6 — inconnu : la carte ne bouge pas

Même tour, verdict illisible : la carte est encore dans `a-relire` à la fin,
aucun agent n'a été lancé, le tour rend 0, et la sortie dit pourquoi elle
attend.

```bash
python3 -m pytest tests/test_run.py -q -k "relire and inconnu"
```

### SC7 — vert ne change rien

Avec un verdict vert, le tour de `relire` se comporte exactement comme avant ce
lot : même invocation construite, carte avancée vers `faite`.

```bash
python3 -m pytest tests/test_run.py tests/test_invocation.py -q -k relire
```

### SC8 — une carte sans PR ne se bloque pas

Une carte dont le champ `pr` est vide traverse la porte comme aujourd'hui : la
porte se tait, la relecture a lieu. Le lot ne transforme pas une absence de
coordonnée en refus.

```bash
python3 -m pytest tests/test_run.py -q -k "relire and sans_pr"
```

### SC9 — la lecture se fait par une fonction, pas par un nom recopié

Le script n'appelle pas `gh` lui-même : il appelle la commande de l'atelier,
qui seule sait comment un verdict se lit. Les deux comptes se vérifient.

```bash
grep -c "pr checks" crons/tour.sh
grep -c "atelier ci" crons/tour.sh
```

Le premier rend 0, le second au moins 1.

### SC10 — la suite existante reste verte, et le compte de tests augmente

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Ce lot **ne lance pas les tests** du produit sur le VPS. `[projet].tests` est
déclaré dans `atelier.toml`, validé comme obligatoire, et exécuté nulle part :
c'est un vrai manque, mais c'en est un autre. Refaire sur la machine un calcul
que la CI fait déjà ajouterait sept minutes à chaque tour de coder, et la CI
reste le juge que GitHub écoute.

Il **ne transmet pas** le verdict au prompt du relecteur. Un relecteur qui
saurait quels tests cassent relirait mieux ; c'est le lot d'après.

Il **ne touche pas** au tour du coder. La carte du coder est avancée sur la
foi du numéro de PR, et ce lot ne change pas cette règle : au moment où le
coder finit, la CI n'a pas encore rendu son verdict.

Il **n'exige pas** `gh`. Sans binaire, sans authentification ou sans réseau, le
verdict est inconnu et la carte attend. Aucun cron ne devient dépendant d'une
authentification.

Il **ne crée** ni boîte, ni état, ni champ de carte. Il **ne fusionne** rien,
ne relance aucun lot, ne reprend aucune carte de `echec/` : le propriétaire
décide, avec `atelier reprendre`.

Il **ne compte pas** les tentatives et n'abandonne pas au bout de N inconnus.
Une carte qui attend indéfiniment est visible dans `a-relire` ; un compteur
serait un état de plus à tenir, pour un cas qui se voit.
