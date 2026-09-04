# Brief 013 — Les contrôles requis se déclarent et se vérifient

## But

La liste des contrôles qui gouvernent le bouton de fusion cesse d'être
un réglage qu'on se rappelle avoir posé. Elle se déclare dans le
branchement du produit, et une commande dit si l'état réel du dépôt lui
correspond.

## Règle du monde

Le 4 septembre 2026, la protection de la branche `master` de
ForgeHistory exige trois contrôles : `gitleaks`, `sim`, `viewer`. Il en
manque deux, et l'oubli n'est pas anodin.

**`feuille` n'est pas requis.** Le travail existe — la CI du produit le
joue sur chaque PR — mais il ne bloque rien. Le registre des lots est la
seule représentation qui fasse autorité sur l'état d'un lot, et une PR
qui le casse peut entrer. Le propriétaire demande que sa validation soit
un contrôle obligatoire ; c'est une ligne de configuration, et elle
manque.

**`enforce_admins` est à `false`.** Un administrateur fusionne par-dessus
un contrôle rouge. Tant que le propriétaire fusionnait à la main, c'était
sa marge de manœuvre. Maintenant que la fusion est mécanique et qu'elle
utilise un jeton d'administrateur, c'est une porte dérobée : la condition
« une PR fusionne uniquement si tous les contrôles requis sont verts »
serait tenue par la politesse de l'intégrateur, pas par GitHub. Une
garde que seul l'appelant respecte n'est pas une garde.

Le passer à `true` retire aussi au propriétaire son échappatoire. C'est
le sens de la décision : la rouvrir devient un geste délibéré et visible,
pas une habitude.

Ce lot ne fusionne rien et ne relit rien. Il **déclare** et il
**constate** :

- `atelier.toml` du produit gagne une section `[controles]` : la liste
  des contextes requis, et l'exigence sur les administrateurs ;
- `atelier controles --projet P` lit la protection de branche réelle et
  répond `PASS`, `FAIL` (en nommant ce qui manque et ce qui est en trop)
  ou `?`. Un `?` n'est pas un feu vert ;
- `atelier controles --projet P --run` pose ce qui manque. Sans `--run`,
  rien n'est écrit.

L'atelier ne devine pas la liste : un branchement sans `[controles]`
fait répondre « je ne sais pas », comme pour tout ce qui manque.

## Périmètre

En écriture : `atelier/controles.py`, le composant. `atelier/projet.py`,
pour lire la section `[controles]` du branchement — facultative, et
déclarée absente quand elle l'est. `atelier/commandes/controles.py` pour
la commande. `profiles/forgehistory.toml`, le gabarit, qui gagne la
liste attendue du produit. `tests/test_controles.py` pour ses contrôles,
et `tests/test_cli.py` pour y **ajouter** des cas. Enfin
`briefs/013-les-controles-requis-se-declarent-et-se-verifient.md`, ce
brief.

Tout autre chemin est interdit, nommément `atelier/echange.py`,
`atelier/verdict.py`, `atelier/boite.py`, `atelier/feuille.py`,
`crons/tour.sh`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le lot ne modifie aucune protection de branche en le livrant : `--run`
est une commande que quelqu'un tape, une fois, quand la série est prête.

## Conditions de succès

### SC1 — trois réponses, trois codes de sortie

`PASS` rend 0, `FAIL` rend 1 en nommant les écarts, l'inconnu rend 2.

```bash
python3 -m pytest tests/test_controles.py -q -k codes
```

### SC2 — le rouge est prouvé sur un contrôle manquant

Une protection à laquelle il manque un contexte de la liste rend 1 et
**nomme** ce contexte. Une protection qui en porte un de plus rend 1 et
le nomme aussi : la liste est la liste, pas un minimum.

```bash
python3 -m pytest tests/test_controles.py -q -k manquant
```

### SC3 — `enforce_admins` à `false` est un échec, pas un détail

Quand le branchement l'exige et que le dépôt ne le tient pas, la
commande rend 1 et dit pourquoi : sans lui, un jeton d'administrateur
fusionne par-dessus un contrôle rouge.

```bash
python3 -m pytest tests/test_controles.py -q -k admins
```

### SC4 — sans `[controles]`, l'atelier déclare qu'il ne sait pas

Un branchement muet rend 2, jamais 0. Une absence se déclare.

```bash
python3 -m pytest tests/test_controles.py -q -k absente
```

### SC5 — sans `--run`, rien n'est écrit

Le contrôle capture les appels de la commande injectée et exige qu'il
n'y ait aucun appel qui mute. Un aperçu n'est pas une dépense.

```bash
python3 -m pytest tests/test_controles.py -q -k apercu
```

### SC6 — la sonde est injectable

`ATELIER_PROTECTION_CMD` nomme la commande qui répond ; en son absence
c'est `gh`. Aucun contrôle de ce dépôt ne demande de compte GitHub.

```bash
python3 -m pytest tests/test_controles.py -q -k injectable
```

### SC7 — le gabarit du produit nomme les cinq contextes

`profiles/forgehistory.toml` déclare `gitleaks`, `sim`, `viewer`,
`feuille` et le contexte de relecture — ce dernier lu par la fonction
qui le porte, jamais recopié.

```bash
python3 -m pytest tests/test_controles.py -q -k gabarit
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Poser réellement la protection sur `master` : c'est un geste
d'exploitation, une fois, avec `--run`, quand la série est livrée.

L'intégrateur, qui lira ce que la protection dit mais ne la posera
jamais.
