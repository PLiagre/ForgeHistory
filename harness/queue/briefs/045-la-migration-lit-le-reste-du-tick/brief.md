# Brief 045 — La migration lit le reste du tick

**Authored**: 2026-08-29T18:26:23Z
**Author**: Codex, sur demande directe du propriétaire
**Risque**: R2 — correction d'un maillon du tick et ajout d'un cas dans
`sim/tests/**`, chemin classé R2 par `control-plane/workflow-policy.toml`.

## But unique

Corriger le choix des destinations de la migration : après que les habitants
ont mangé, toute voisine qui possède encore une quantité strictement positive
de nourriture est une destination en surplus.

Le lot 041 retranche aujourd'hui une deuxième ration à ce stock déjà consommé.
Une voisine qui a réellement un reste positif, mais inférieur à une nouvelle
ration, est donc refusée. Ce lot supprime ce double compte et rien d'autre.

## Origine du correctif

L'audit rétrospectif demandé par le propriétaire sur les lots 038 à 041 a
reproduit le défaut sur le `master` du 29 août 2026 :

- la cellule source a manqué de nourriture pendant le tick ;
- la cellule voisine a déjà consommé et conserve `1.0` kg ;
- la fraction migrante donne au moins un partant entier ;
- `_apply_migration` ne déplace pourtant personne.

La cause observée est bornée : `tick()` appelle `_apply_consumption` avant
`_apply_migration`, puis `_surplus_nourriture_tick` soustrait encore
`population × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` au stock restant.

Ce constat a également été rendu par la relecture Grok du lot 041. Les suites
vertes ne le protègent pas : leurs destinations d'épreuve possèdent plusieurs
rations, donc le second retrait reste invisible.

Les lots 038, 039 et 040 ne sont pas à modifier dans ce lot. Leurs défauts de
preuve ou de pilotage ne justifient pas de changer leur mécanique produit ici.

## Dépendance et caducité

Ce lot suppose le lot 041 fusionné et la présence de `_apply_migration`.

Au démarrage, l'exécutant dérive le SHA de base du `master` reçu par son
worktree et reproduit le rouge décrit par SC1 avant toute correction. Si une
voisine dont le stock post-consommation est strictement positif est déjà
acceptée sans seconde soustraction, le brief est caduc : arrêter le lot et le
déclarer, sans adapter son objectif.

## Règle du monde

La migration est le dernier maillon du tick. À cet instant :

1. la production et le commerce ont déjà eu lieu ;
2. `_apply_consumption` a déjà retiré la ration du tick ;
3. le remboursement éventuel de la dette a déjà consommé les kilogrammes
   correspondants ;
4. le stock alimentaire encore présent est donc le reste physique disponible
   de ce tick.

Pour choisir et pondérer les destinations, le surplus d'une cellule est alors :

```text
surplus_destination_kg = max(0, stock_alimentaire_post_consommation_kg)
```

Cette définition du surplus de destination relève du niveau de fidélité 2 :
elle est plausible et générée, jamais sourcée. Une anomalie de niveau 2 n'est
pas un défaut et n'ouvre ni correctif ni brief.

La population de la destination ne retranche aucune nouvelle ration. Cette
ration a déjà été mangée. Le moteur ne préfinance pas le tick suivant.

Une quantité strictement positive, même inférieure à une ration, rend la
voisine admissible. Un stock nul ou la sentinelle négative ne la rend pas
admissible.

S'il existe plusieurs destinations, les poids de la répartition sont leurs
stocks post-consommation strictement positifs, pris sur l'instantané antérieur
à tout mouvement. Les règles existantes de plus fort reste, de départage par
`cell_id`, d'atomicité et de conservation de la population ne changent pas.

Les migrants ne déplacent toujours aucun kilogramme. La correction lit le
stock ; elle ne le modifie pas.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py`, uniquement pour supprimer le second retrait de ration dans
  la dérivation des surplus utilisés par la migration ;
- `sim/tests/test_commerce.py`, uniquement pour ajouter les cas de régression
  de ce brief. Les assertions déjà présentes restent inchangées.

Livrables autorisés :

- `harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/manifest.json` ;
- `harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/generator-log.md` ;
- `harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/measure_045.py` ;
- les sorties déterministes produites par ce mesureur dans le même dossier.

Tout autre chemin est interdit. En particulier, ne modifier ni les briefs ou
livrables 038 à 044, ni `sim/constants.py`, `sim/model.py`, `sim/world.py`,
`sim/tests/test_survie.py`, `sim/tests/test_write_coverage.py`, la carte, le
viewer, le harness, ForgePilot, les règles du dépôt ou un `verdict.md`.

## Conditions de succès

### SC1 — Un reste positif est une destination

Sur un micro-monde à deux cellules, après consommation simulée :

- la source a une pénurie strictement positive et assez d'habitants pour
  produire au moins un partant entier ;
- la destination a un stock strictement positif mais inférieur à sa ration
  d'un tick ;
- les deux cellules sont voisines.

Après `_apply_migration`, la source perd au moins un habitant et la destination
en gagne exactement autant.

Le rouge est prouvé avant la correction sur le SHA de base : le même contrôle
ne déplace personne parce que la ration est soustraite une seconde fois.

### SC2 — Le stock nul ne suffit pas

Le même micro-monde, avec un stock post-consommation exactement nul sur la
destination, ne déplace personne. Le zéro est une mesure : le maillon est
appelé et son résultat est observé.

Une sentinelle de stock négative ne devient pas un surplus.

### SC3 — La population ne réserve pas une seconde ration

Deux destinations ayant le même stock post-consommation strictement positif,
mais des populations différentes, reçoivent le même poids de destination.
Leur ration du tick a déjà été retirée ; leur population ne doit donc pas
changer le surplus lu par la migration.

Le contrôle dérive les deux populations et le stock depuis son échantillon. Il
n'appelle pas une copie de la formule corrigée pour calculer son résultat
attendu.

### SC4 — La pondération lit le stock réel

Avec deux destinations de stocks post-consommation positifs et différents, la
répartition des partants suit le rapport de ces stocks, avec la règle existante
du plus fort reste et le départage par `cell_id`.

Le nombre de partants est choisi de façon à rendre la pondération observable.
Les valeurs attendues sont dérivées avant l'appel à `_apply_migration`.

### SC5 — Rien d'autre ne bouge

Sur chaque micro-monde du lot :

- la somme des populations est strictement identique avant et après ;
- la somme et le détail cellule par cellule des stocks de toutes les
  marchandises sont strictement identiques avant et après ;
- une cellule qui reçoit ne renvoie personne le même tick ;
- inverser l'ordre des arêtes donne le même état.

### SC6 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ -q` est vert ;
- tous les tests de migration déjà présents dans `sim/tests/test_commerce.py`
  restent verts sans modification ;
- les trois propriétés de `sim/tests/test_survie.py` restent vertes sans
  modification de ce fichier ;
- les contrôles de `sim/tests/test_write_coverage.py` restent verts sans
  modification de ce fichier ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json`
  sont strictement identiques entre elles ;
- aucune instruction `global` n'apparaît dans `sim/engine.py`.

## Compteurs exigés

`deliverables/measure_045.py` reconstruit les compteurs. Il ne réécrit jamais
un fichier produit pour mesurer un autre SHA : les références de base sont
lues par Git ou exécutées dans un répertoire temporaire séparé.

| compteur | source | dénominateur dérivé |
|---|---|---|
| `destinations_reste_positif` | micro-monde SC1 | destinations positives réellement essayées |
| `habitants_deplaces_reste_positif` | différence des populations SC1 | partants entiers dérivés de la population et de la constante |
| `destinations_stock_nul` | micro-monde SC2 | destinations nulles réellement essayées |
| `habitants_deplaces_stock_nul` | différence des populations SC2 | appels de migration réellement joués |
| `destinations_sentinelle` | micro-monde SC2 avec stock négatif | destinations négatives réellement essayées |
| `poids_independants_population` | deux destinations SC3 | populations distinctes réellement essayées |
| `rapport_stocks_destination` | stocks post-consommation SC4 | destinations pondérées réellement observées |
| `ecart_population_totale` | sommes avant/après de tous les micro-mondes | cellules réellement sommées |
| `cellules_dont_stock_change` | comparaison des paniers avant/après | cellules réellement comparées |
| `ordres_aretes_essayes` | même monde avec deux ordres | ordres réellement exécutés |
| `tests_sim_verts` | collecte pytest | tests réellement collectés |

`habitants_deplaces_stock_nul`, `ecart_population_totale` et
`cellules_dont_stock_change` doivent valoir `0`. Ces zéros sont des mesures,
jamais des sentinelles. Les échantillons vides échouent.

## Livrables et preuve

Le manifeste déclare chaque fichier produit et les commandes exactes. Le
journal, en français clair, contient :

- le SHA de base dérivé au démarrage ;
- la commande et la sortie du rouge SC1 avant correction ;
- le diff des seuls chemins autorisés ;
- les compteurs et leurs tailles d'échantillon ;
- le résultat des suites exigées ;
- deux sorties CLI déterministes après correction.

La preuve de base ne s'obtient jamais en remplaçant temporairement
`sim/engine.py` dans le dépôt courant. Utiliser Git dans un répertoire
temporaire séparé ou une commande qui lit directement l'objet Git.

Le compte-rendu final et le verdict sont produits dans une invocation neuve,
indépendante de celle qui a écrit le code. L'exécutant ne prononce pas la
recevabilité de son travail.

## Hors périmètre

- réécrire ou compléter rétroactivement les preuves des lots 038 à 041 ;
- modifier la règle de survie ou recalibrer un test de régime ;
- changer la fraction migrante, la mortalité, la natalité ou la consommation ;
- faire porter de la nourriture ou une autre marchandise aux migrants ;
- modifier l'accumulation de `migration_remainder` lorsqu'aucune destination
  n'existe ;
- corriger les rôles Hermes, les compétences, les sorties JSON ou les reprises
  ForgePilot ; ces sujets forment un lot structurel séparé ;
- modifier la carte, le viewer, le schéma du snapshot ou sa version.

## Interdictions pour l'exécutant

L'exécutant ne modifie ni ce brief ni sa grille, n'écrit pas de `verdict.md`,
ne juge pas son propre travail, ne pousse pas sur `master` et ne fusionne rien.
