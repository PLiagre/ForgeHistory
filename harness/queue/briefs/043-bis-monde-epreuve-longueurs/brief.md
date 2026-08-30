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
en conservant sur l'autre arête le repli plat par arête. Le même monde
d'épreuve doit ainsi rendre actives `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` et
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`.

Ce micro-lot ne change aucune règle du monde. Il ne modifie aucun fichier
produit et reste strictement séparé du lot produit 044.

## Cause prouvée et base

La base imposée est le `master`
`4b732778fc7970ce3e0e108369adc5ff60b5a2a5` (`4b732778`). Sur cette base,
`_MondeEpreuve.adjacency` contient deux arêtes sans `shared_length_m`.

`sim/engine.py::_capacite_base_arete_kg` prend donc, pour les deux arêtes, le
repli `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. La constante
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` est bien consultée par le moteur, mais
elle ne peut pas changer ce monde d'épreuve. Le contrôle suivant échoue pour
cette raison :

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q
```

Avant toute édition, enregistrer le SHA exact, cette commande et sa sortie
rouge. Si le SHA de départ n'est pas celui imposé, ou si l'échec ne nomme pas
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` comme constante inerte, arrêter sans
adapter le brief.

## Correction demandée

Modifier uniquement la donnée `adjacency` de `_MondeEpreuve` dans
`sim/tests/test_write_coverage.py` : ajouter `shared_length_m` à une seule des
deux arêtes. L'autre reste sans cette clé afin d'exercer le repli
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`.

La longueur est une valeur en mètres, positive, finie, explicite et
déterministe. Elle représente une frontière physique du fixture ; elle n'est
ni aléatoire, ni calculée depuis la valeur d'une constante testée. Un court
commentaire peut expliquer son unité si nécessaire.

Ne modifier aucune assertion, aucun facteur de mutation, aucune dérivation de
constantes, aucune cellule et aucune autre donnée du monde d'épreuve. Ne pas
ajouter de test. Le contrôle existant doit passer parce que son échantillon
exerce désormais simultanément le chemin par longueur et le chemin de repli,
pas parce que le contrôle a été relâché ou contourné.

**Fidélité : sans objet.** Cette longueur appartient à un fixture synthétique
de harnais. Elle ne décrit ni la carte figée ni un lieu historique.

## Périmètre d'écriture

Fichier de harnais autorisé :

- `sim/tests/test_write_coverage.py`, uniquement pour ajouter une clé
  `shared_length_m` à une seule arête et, si utile, son commentaire d'unité ;
  l'autre arête doit rester sans cette clé.

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

Le journal contient le SHA complet imposé, la commande ciblée exécutée avant
toute édition et sa sortie en échec. L'échec montre que
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` ne change pas le monde d'épreuve privé
de longueurs.

### SC2 — Les deux chemins de capacité sont exercés

Après correction, une seule des deux entrées de `_MondeEpreuve.adjacency`
porte un `shared_length_m` numérique, positif et fini ; l'autre ne porte pas
cette clé. La valeur est stable entre deux exécutions. Le même monde d'épreuve
exerce ainsi le chemin de capacité par longueur avec
`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` et le chemin de repli avec
`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Aucun autre élément du fixture n'a
changé.

### SC3 — Le contrôle ciblé devient vert sans contournement

La même commande que pour SC1 est verte après la seule correction autorisée :

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q
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

Le diff hors livrables ne contient que l'ajout d'une longueur sur une seule
arête, et éventuellement son commentaire, dans
`sim/tests/test_write_coverage.py`. L'autre arête reste sans
`shared_length_m`.
`sim/engine.py`, `sim/constants.py` et tout le lot 044 restent identiques à la
base.

## Livrables et séparation des rôles

Le manifeste déclare les seuls fichiers écrits et les deux commandes pytest.
Il décrit la modification comme l'ajout de `shared_length_m` à une seule arête,
l'autre restant sans la clé. Le journal, en français clair, contient la preuve
rouge, le diff borné montrant cette répartition, le vert ciblé et le résultat
de la suite complète. Il indique que le même monde d'épreuve exerce le chemin
longueur (`DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK`) et le chemin de repli
(`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`). Aucun mesureur dédié n'est demandé :
les commandes ci-dessus suffisent et évitent d'élargir le micro-lot.

L'exécutant n'écrit pas de `verdict.md`, ne juge pas son propre travail, ne
fusionne rien et ne pousse pas directement sur `master`.

## Hors périmètre

- toute modification du moteur, des constantes ou d'une règle du monde ;
- tout changement produit prévu ou demandé par le lot 044 ;
- toute nouvelle calibration de capacité ou de débit ;
- toute modification d'une assertion ou de la portée du contrôle existant ;
- tout nouveau test ou fichier de mesure ;
- toute correction rétroactive des briefs, grilles ou livrables 043 et 044.
