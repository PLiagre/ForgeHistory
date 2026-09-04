# Brief 019 — Le pilote dépose autant de lots que de périmètres disjoints

## But

`atelier piloter` cesse de déposer au plus une carte par rôle. Il dépose
tous les lots admissibles dont les périmètres sont disjoints, borné par
un plafond que le branchement déclare.

## Règle du monde

`feuille.decider` porte deux drapeaux, `briefer_pris` et `coder_pris` :
la première fiche admissible prend la place, les suivantes sont sautées
sans être examinées. C'était juste tant qu'un rôle ne pouvait tenir
qu'un lot à la fois.

Les trois lots précédents ont retiré cette contrainte : la prise est
atomique, chaque lot a son worktree, et deux fiches du même registre ne
sont plus en collision. Il reste à déposer plus d'une carte.

Ce que le pilote calcule après ce lot, dans l'ordre de la feuille :

1. les lots déjà en carte sont sautés — c'est déjà le cas ;
2. un lot dont une PR est ouverte sur sa branche est sauté, et devant
   l'inconnu il retient — c'est déjà le cas, et ça ne s'assouplit pas ;
3. un lot dont une dépendance n'est pas livrée est retenu, et il **dit**
   laquelle ;
4. un lot dont une ressource est tenue par un autre lot — un fichier ou
   une fiche — est retenu, et il dit par qui. Le lot retenu n'est pas
   perdu : le réveil suivant le reprendra quand la ressource sera rendue ;
5. **les verrous des cartes déjà décidées comptent dans le calcul.** Un
   pilote qui déciderait de deux lots en collision dans le même tour
   ferait échouer le second à la prise. Le tableau des verrous ne les
   porte pas encore : c'est au calcul de les tenir ;
6. le plafond `[cycle].parallele` du branchement arrête le compte. Une
   absence se déclare : un branchement muet vaut **un**, l'ancien
   comportement, et le pilote le dit.

Le plafond n'est pas une prudence décorative. Chaque carte déposée
finira par faire dépenser un quota ; déposer douze lots un matin où
trois abonnements sont disponibles fabrique neuf échecs. Le plafond est
le seul endroit où le propriétaire règle le débit, et il le règle par
une ligne de branchement, sans toucher au code.

## Périmètre

En écriture : `atelier/feuille.py`, pour le calcul de la décision.
`atelier/projet.py`, pour le plafond de parallélisme du branchement et
son absence déclarée. `profiles/forgehistory.toml`, le gabarit.
`tests/test_feuille.py` pour y **ajouter** des cas. Et
`briefs/019-le-pilote-depose-autant-de-lots-que-de-perimetres-disjoints.md`,
ce brief.

Tout autre chemin est interdit, nommément `atelier/verrou.py`,
`atelier/prise.py`, `atelier/boite.py`, `atelier/cycle.py`,
`crons/tour.sh`, `crons/pilote.sh`, `VISION.md`, `AGENTS.md` et les
autres briefs.

Aucune sonde ne change : `pr_ouverte` et `etat_pr` gardent leur
doctrine, et l'inconnu retient toujours. Un lot qui aurait besoin de
l'assouplir pour montrer du parallélisme se trompe de lot.

## Conditions de succès

### SC1 — deux lots disjoints sont décidés dans le même tour

Deux fiches `pret`, deux périmètres disjoints, plafond à deux : deux
décisions de rôle `coder`.

```bash
python3 -m pytest tests/test_feuille.py -q -k deux_disjoints
```

### SC2 — le rouge est prouvé : deux lots en collision ne le sont jamais

Deux fiches `pret` qui partagent un fichier : une seule décision, et le
motif du retenu **nomme** le fichier et le lot qui le tient. Le contrôle
échoue si les deux sont décidés.

```bash
python3 -m pytest tests/test_feuille.py -q -k collision_dans_le_tour
```

### SC3 — les décisions du tour comptent entre elles

Trois fiches : A et B disjointes, C en collision avec B. Le calcul rend
A et B, jamais C, alors qu'aucun verrou n'est encore posé sur le disque.

```bash
python3 -m pytest tests/test_feuille.py -q -k decisions_du_tour
```

### SC4 — le plafond arrête le compte

Cinq lots disjoints, plafond à trois : trois décisions. Le nombre attendu
se lit dans le branchement du produit jetable, il n'est pas écrit dans le
contrôle.

```bash
python3 -m pytest tests/test_feuille.py -q -k plafond
```

### SC5 — un branchement muet vaut un, et le dit

Sans `[cycle].parallele`, une seule carte par rôle, et une phrase sur
`stderr`. L'atelier ne devine pas un débit.

```bash
python3 -m pytest tests/test_feuille.py -q -k plafond_absent
```

### SC6 — ce qui est retenu se dit

Chaque lot non déposé produit une ligne sur `stderr` qui nomme sa cause :
dépendance, ressource tenue, PR ouverte, PR inconnue. Le compte des
lignes s'accorde au compte des lots retenus, dérivé des deux côtés.

```bash
python3 -m pytest tests/test_feuille.py -q -k retenues
```

### SC7 — sans `--run`, rien n'est déposé

```bash
python3 -m pytest tests/test_feuille.py -q -k apercu
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

L'invocation des agents : le pilote dépose des cartes, il n'appelle
personne.

Le nombre de réveils par jour, qui vit dans les profils.
