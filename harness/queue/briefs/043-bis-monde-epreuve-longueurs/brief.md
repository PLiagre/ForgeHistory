# Brief 043-bis — Le monde d'épreuve exerce longueur et repli

**Authored**: 2026-08-30T09:01:27Z
**Author**: Codex/OpenAI, sur demande explicite du propriétaire
**Risque**: R2 — correction bornée d'un fixture de harnais, sans changement
produit ; `sim/tests/**` est néanmoins classé R2 par
`control-plane/workflow-policy.toml`.

## But unique

Réparer le monde d'épreuve de
`test_chaque_constante_du_moteur_change_le_monde` afin qu'il exerce la
capacité physique par longueur de frontière introduite par le lot 043, tout
en créant sur l'autre arête un besoin de commerce qui exerce réellement le
repli plat par arête. Le même monde d'épreuve doit ainsi rendre actives
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` et
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` au moyen de deux changements de fixture
précis : une longueur de `1000.0` mètres sur l'arête 1-2 et une population de
`1000` au lieu de `50` dans la cellule 3.

Ce micro-lot ne change aucune règle du monde. Il ne modifie aucun fichier
produit et reste strictement séparé du lot produit 044.

## Cause prouvée et base

La base produit de comparaison imposée est le `master`
`4b732778fc7970ce3e0e108369adc5ff60b5a2a5` (`4b732778`). Le HEAD réel du
worktree peut être un descendant de cette base qui ajoute uniquement le
présent brief 043-bis. Sur la base produit,
`_MondeEpreuve.adjacency` contient deux arêtes sans `shared_length_m`.

`sim/engine.py::_capacite_base_arete_kg` prend donc, pour les deux arêtes, le
repli `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. La constante
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` est bien consultée par le moteur, mais
elle ne peut pas changer ce monde d'épreuve. Le contrôle suivant échoue pour
cette raison :

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q
```

L'exécution de la première correction envisagée a apporté une seconde preuve :
ajouter seulement `shared_length_m=1000.0` à l'arête 1-2 laisse le contrôle
rouge, cette fois avec `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` inerte.
L'arête 1-3 reste bien sur le repli, mais la cellule 3 dite « en équilibre »
n'a aucun besoin de commerce ; cette arête ne transporte donc rien et la
présence du chemin de repli ne suffit pas à en prouver la fonction.

Une expérimentation de lecture seule a établi que la combinaison demandée
ci-dessous rend toutes les constantes consultées actives : l'arête 1-2 exerce
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK`, tandis que l'arête 1-3 exerce
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`.

Avant toute édition, enregistrer le SHA complet de la base produit, le HEAD
réel, les commandes de vérification suivantes et leurs sorties :

```bash
git rev-parse HEAD
git merge-base --is-ancestor 4b732778fc7970ce3e0e108369adc5ff60b5a2a5 HEAD
git diff --cached --name-only
git diff --name-only 4b732778fc7970ce3e0e108369adc5ff60b5a2a5 -- . ':(exclude)harness/queue/briefs/043-bis-monde-epreuve-longueurs/brief.md'
git status --short --untracked-files=all -- . ':(exclude)harness/queue/briefs/043-bis-monde-epreuve-longueurs/brief.md'
```

La vérification d'ascendance doit réussir. Les trois commandes d'état qui la
suivent doivent rester vides : aucune différence indexée ni aucun changement,
suivi ou non, hors du chemin du présent brief avant édition. Exécuter ensuite
la commande pytest ciblée ci-dessus et enregistrer sa sortie rouge sur le
produit ainsi confirmé identique à `4b732778`. Arrêter sans adapter le brief
si la base produit n'est pas ancêtre du HEAD, si l'index n'est pas vide, si
une différence existe hors du présent brief, ou si l'échec ne nomme pas
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` comme constante inerte. Ne pas arrêter
sur le seul fait que le HEAD réel diffère de `4b732778` en portant uniquement
le présent brief.

## Correction demandée

Modifier uniquement le fixture `_MondeEpreuve` dans
`sim/tests/test_write_coverage.py`, avec exactement ces deux changements :

- ajouter `shared_length_m=1000.0` à l'arête 1-2 seulement ; l'arête 1-3
  reste sans cette clé afin d'exercer le repli
  `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` ;
- porter la population de la cellule 3 de `50` à `1000`, sans changer ses
  autres champs, afin qu'elle ait un besoin de commerce que l'ancienne
  capacité de repli de 200 kg ne peut pas couvrir entièrement.

La longueur est une valeur en mètres, positive, finie, explicite et
déterministe. Elle représente une frontière physique du fixture ; elle n'est
ni aléatoire, ni calculée depuis la valeur d'une constante testée. La
population `1000` est un choix de fixture anti-faux-vert : elle est assez
grande pour que la capacité de repli soit effectivement contraignante. Ce
n'est ni une règle produit, ni une calibration du monde. Mettre à jour au
minimum la docstring ou le commentaire qui décrit encore la cellule 3 comme
« en équilibre » ; un court commentaire peut aussi expliquer l'unité de la
longueur si nécessaire.

Ne modifier aucune assertion, aucun facteur de mutation, aucune dérivation de
constantes, aucune fonction ni aucun champ du fixture au-delà des deux valeurs
nommées ci-dessus. Ne pas ajouter de test. Le contrôle existant doit passer
parce que son échantillon exerce désormais simultanément le chemin par
longueur et le chemin de repli, pas parce que le contrôle a été relâché ou
contourné.

**Fidélité : sans objet.** Cette longueur et cette population appartiennent à
un fixture synthétique de harnais. Elles ne décrivent ni la carte figée ni un
lieu historique.

## Périmètre d'écriture

Fichier de harnais autorisé :

- `sim/tests/test_write_coverage.py`, uniquement pour ajouter
  `shared_length_m=1000.0` à l'arête 1-2, porter la population de la cellule 3
  de `50` à `1000`, et mettre à jour au minimum la docstring ou le commentaire
  rendu faux ; l'arête 1-3 doit rester sans `shared_length_m`.

Livrables minimaux autorisés, seulement pour porter les preuves du lot :

- `harness/queue/briefs/043-bis-monde-epreuve-longueurs/deliverables/manifest.json` ;
- `harness/queue/briefs/043-bis-monde-epreuve-longueurs/deliverables/generator-log.md`.

Tout autre chemin est interdit. En particulier, ne modifier ni `sim/engine.py`,
ni `sim/constants.py`, ni un autre fichier de `sim/` ou de `sim/tests/`, ni les
briefs ou livrables 043 et 044, ni la roadmap, ni le dashboard, ni la carte, ni
le viewer, ni le harnais ou ForgePilot, ni ce brief, ni sa grille, ni un
`verdict.md`.

## Conditions de succès

### SC1 — Le rouge de base est conservé

Le journal contient le SHA complet de la base produit
`4b732778fc7970ce3e0e108369adc5ff60b5a2a5`, le HEAD réel, les vérifications
préalables d'ascendance, d'index vide et d'absence de différence hors du
présent brief, ainsi que la commande ciblée exécutée avant toute édition et sa
sortie en échec. L'échec montre que
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` ne change pas le monde d'épreuve privé
de longueurs.

### SC2 — Les deux chemins de capacité sont exercés

Après correction, l'arête 1-2 de `_MondeEpreuve.adjacency` porte exactement
`shared_length_m=1000.0` et l'arête 1-3 ne porte pas cette clé. La population
de la cellule 3 vaut exactement `1000` au lieu de `50` ; ses autres champs
restent identiques. Ces valeurs sont stables entre deux exécutions. Le même
monde d'épreuve exerce ainsi le chemin de capacité par longueur avec
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` sur 1-2 et le chemin de repli avec
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` sur 1-3. La docstring ou le commentaire
qui qualifiait la cellule 3 d'« en équilibre » est corrigé au strict minimum.

### SC3 — Le contrôle ciblé est vert sans contournement

Le contrôle ciblé est vert après les deux changements autorisés et sa sortie
montre le rapport `constantes_du_moteur_atteignables` dérivé par le contrôle
lui-même :

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q -s
```

Toutes les assertions du fichier, les facteurs `(0.1, 3.0, 1e6)`, la fonction
`_constantes_consultees_par_le_moteur` et la logique qui accumule `inertes`
restent strictement inchangés.

### SC4 — Toute la suite du moteur est verte

```bash
.venv/bin/python -m pytest sim/tests/ -q
```

La collecte ne diminue pas par rapport à la base. Aucun test n'est sauté,
marqué comme succès attendu, filtré ou supprimé.

### SC5 — Aucun produit ni lot voisin ne bouge

Le diff hors livrables ne contient, dans `sim/tests/test_write_coverage.py`,
que l'ajout de `shared_length_m=1000.0` sur l'arête 1-2, le passage de la
population de la cellule 3 de `50` à `1000`, et la mise à jour minimale de la
docstring ou du commentaire rendu faux. L'arête 1-3 reste sans
`shared_length_m`. Les assertions, les facteurs et les fonctions du fichier
restent identiques à la base.
`sim/engine.py`, `sim/constants.py` et tout le lot 044 restent identiques à la
base.

## Livrables et séparation des rôles

Le manifeste déclare les seuls fichiers écrits et les deux commandes pytest.
Il décrit exactement l'ajout de `shared_length_m=1000.0` à l'arête 1-2 et le
passage de la population de la cellule 3 de `50` à `1000`, l'arête 1-3 restant
sans la clé. Le journal, en français clair, contient la preuve rouge, le diff
borné montrant les deux changements et la correction minimale de description,
la preuve ciblée verte, dont la sortie montre le rapport dérivé par le contrôle
lui-même, et le résultat de la suite `sim/tests/` complète. Il indique que le
même monde d'épreuve exerce le chemin longueur
(`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK`) et le chemin de repli
(`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`), et que la population `1000` est une
valeur anti-faux-vert du fixture, pas une règle produit. Aucun mesureur dédié
n'est demandé : les commandes ci-dessus suffisent et évitent d'élargir le
micro-lot.

L'exécutant n'écrit pas de `verdict.md`, ne juge pas son propre travail, ne
fusionne rien et ne pousse pas directement sur `master`.

## Hors périmètre

- toute modification du moteur, des constantes ou d'une règle du monde ;
- tout changement produit prévu ou demandé par le lot 044 ;
- toute nouvelle calibration de capacité ou de débit ;
- toute modification supplémentaire du fixture au-delà des deux valeurs
  expressément autorisées et de la correction minimale de sa description ;
- toute modification d'une assertion, d'un facteur, d'une fonction ou de la
  portée du contrôle existant ;
- tout nouveau test ou fichier de mesure ;
- toute correction rétroactive des briefs, grilles ou livrables 043 et 044.
