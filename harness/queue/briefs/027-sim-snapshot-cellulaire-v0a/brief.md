# Brief 027 : snapshot cellulaire déterministe (V0-A) — l'état du monde, cellule par cellule

**Authored**: 2026-08-22T12:30:00Z
**Author**: forge-planificateur
**Statut**: PRÊT — exécutable en l'état, aucun arbitrage préalable requis
**Classement de risque**: R1 — produit borné

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Cursor
> Cloud (Grok 4.6 XHigh), invoqué en lecture et planification seulement, à
> partir de la décision propriétaire
> `hermes/requests/DEMANDE-20260821-visualiseur-web-v0.md` et du jalon
> transversal V0 de `ROADMAP.md`. Cette session n'exécute pas le lot, ne
> rédige aucun verdict, ne modifie aucun code produit, et ne fusionne rien.

> **Pourquoi R1, et pas R2.** Ce lot ajoute un export déterministe dans
> `sim/`. Il applique des invariants déjà tranchés : `cell_id` seule clé
> spatiale (ADR-0003), Province dérivée (brief 018), sentinelle `-1` pour
> « non calculé », déterminisme par empreinte. Il ne crée pas d'invariant
> nouveau, ne touche ni à la sécurité, ni à la gouvernance, ni à une masse
> DEM, ni à un lot déjà faux-vert. Le schéma JSON est un contrat public
> nouveau, mais c'est une photographie du monde déjà simulé, pas une seconde
> source de vérité. R2 n'est donc pas imposé.

À partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md`
› Single Source of Instruction).

---

## Provenance

Le jalon V0 — Monde visible — est un jalon transversal, pas une couche de
simulation. La demande acceptée
`hermes/requests/DEMANDE-20260821-visualiseur-web-v0.md` le coupe en deux
lots bornés : d'abord l'export (ce lot), ensuite le regard (brief 028).
`.venv/bin/python -m sim --json` n'écrit aujourd'hui qu'un résumé global.
Unity est en veille. Sans export par cellule, rien n'est visible honnêtement.

Ce lot **ne dépend d'aucune fusion restante**. Le brief 025 (déterminants
climatiques C1) est fusionné. Le brief 026 (gisements) n'est pas exécuté et
n'est pas lu. Le brief 024 a publié `cells_relief_g6.json`, mais cet
artefact n'est **pas consommable** (D6) : ce lot ne le corrige pas.

Le brief 028 n'existe que pour lire ce que ce lot aura publié. **Ce lot
n'instruit aucun rendu.**

---

## World-Terms Requirement

**Chaîne causale.**

Un lieu, à un instant, contient des habitants, de la nourriture, de la faim,
et parfois un reste de mort qui n'a pas encore été appliqué. Ces faits sont
déjà produits par le moteur. Ils ne se voient pas encore : le résumé global
les noie, et aucun regard n'a le droit de les recalculer.

Ce lot **photographie** le monde déjà simulé, cellule par cellule, pour une
graine et un nombre de ticks donnés. La photographie n'ajoute rien : elle
nomme ce qui est, elle déclare ce qui manque, elle refuse de deviner ce qui
n'est pas encore calculé.

La terre a une forme (la géométrie G3) et un identifiant (`cell_id`). Elle
relève aujourd'hui d'un centre administratif : cette appartenance se
recalcule au moment de l'export, elle n'est jamais estampillée sur la
cellule. Deux déterminants physiques déjà mesurés — l'énergie reçue du
Soleil et la distance à la mer — peuvent accompagner la photographie s'ils
sont vraiment là. Un relief dont les zéros ne se distinguent pas d'une
mesure n'accompagne pas la photographie.

**Interdit** : aucun barème, aucun bonus, aucune règle du type « si faim
alors couleur X ». Ce lot n'écrit aucune conséquence. Il écrit l'état.

---

## Vocabulaire (expliqué une fois)

- **snapshot** : le fichier JSON produit par `sim/` pour une graine et un
  nombre de ticks. C'est une photographie, pas une seconde simulation.
- **schéma** : la forme versionnée de ce fichier. La version de ce lot est
  `v0a-1`.
- **couche** : une famille de grandeurs rattachées aux cellules (population,
  nourriture, déterminants climatiques…). Une couche est `present`,
  `absent` ou `not_consumed`.
- **zéro mesuré** : la grandeur a été calculée et vaut `0`.
- **absent / `null`** : la grandeur n'existe pas dans cette photographie
  (couche absente, champ non publié).
- **non calculé** : la sentinelle du projet, `-1` — le moteur n'a pas encore
  produit la grandeur. Un zéro n'est jamais cette sentinelle.
- **référence géométrique honnête** : la géométrie publiée est copiée des
  cellules G3 au moment de l'export, avec le chemin et l'empreinte de la
  source. Ce n'est pas une géométrie inventée, ni un second cadastre.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur `origin/master` à `13cfe214a2959769bd3739f586f301236f061f20`.
Les nombres sont des **constats de contexte**, pas des seuils (règle n° 2).

- `sim/__main__.py` : `.venv/bin/python -m sim --ticks N --seed S --json`
  avance le monde et imprime un résumé global. Aucun export par cellule.
- `sim/model.py` : `Cell` porte `cell_id`, `area_km2`, `population`,
  `food_stock_kg`, `hunger_ticks`, `food_deficit_kg`,
  `mortality_remainder`. Aucun champ de province.
- `sim/world.py` : `World.from_g3(rng_seed=…)` amorce depuis
  `cells_g3.json` ; `to_dict()` sérialise déjà les cellules par
  `cell_id` trié, sans géométrie.
- `sim/aggregation.py` : `agregat_depuis_monde` recalcule l'appartenance.
  `province_de_cellule` / `identifiant_de_province_de_cellule` /
  `nom_de_province_de_cellule` consultent la vue dérivée. Rien n'est écrit
  sur `Cell`.
- `pipeline/geo/artifacts/cells_g3.json` : `596` cellules, `cell_id`,
  `geometry` (EPSG:3035), `centroid` (`lat`, `lon`, `x_m`, `y_m`),
  `area_km2`.
- `pipeline/geo/artifacts/cells_climate_drivers_c1.json` : `596` cellules,
  mêmes `cell_id`, toutes avec une insolation. **Consommable.**
- `pipeline/geo/artifacts/cells_relief_g6.json` : `596` cellules, toutes
  avec `sample_count > 0`. **`473` ont toutes leurs altitudes à `0.0`.**
  Un zéro d'altitude n'est donc pas distinguable d'une mesure manquante.
  `ROADMAP.md` signale encore la correction de couverture DEM. **Non
  consommable.**
- `pipeline/geo/artifacts/cells_resources_r1.json` : **absent** (lot 026
  non exécuté).
- `sim/` reste en bibliothèque standard uniquement.

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit les noms de
fonctions internes dans le périmètre autorisé.

### D1 — Commande exacte

Depuis la racine, Linux :

```
.venv/bin/python -m sim --ticks <N> --seed <S> --snapshot-json <CHEMIN>
```

Windows (règle n° 1) :

```
py -m sim --ticks <N> --seed <S> --snapshot-json <CHEMIN>
```

- `<N>` est un entier `≥ 0` (le refus existant `EXIT_REFUS = 2` pour
  `--ticks < 0` reste inchangé).
- `<S>` est un entier, défaut `DEFAULT_CLI_SEED` lu de `sim/constants.py`.
- `<CHEMIN>` est un fichier, pas un répertoire. Les parents absents sont
  créés. L'écriture se fait en **octets** (`Path.write_bytes`), jamais via
  un mode texte qui convertirait les fins de ligne.
- La commande avance le monde de `<N>` ticks avec la graine `<S>`, **puis**
  écrit le snapshot. Le résumé humain existant (et `--json` s'il est aussi
  demandé) continue d'imprimer le résumé **global**. Le snapshot n'est
  **jamais** déversé sur la sortie standard : trop gros, et ce n'est pas
  le contrat de `--json`.
- Code de sortie `0` si le fichier est écrit. Impossible d'écrire :
  message sur la sortie d'erreur, code `2`.
- Sans `--snapshot-json`, le comportement actuel ne change pas.
- Aucun serveur, aucun démon, aucune base de données.

### D2 — Version de schéma

Constante ajoutée en fin de `sim/constants.py`, avec une phrase de
justification :

| nom | valeur | justification à écrire |
|---|---|---|
| `SNAPSHOT_SCHEMA_VERSION` | `"v0a-1"` | première photographie cellulaire du jalon V0-A ; le suffixe `-1` permet une révision du contrat sans réutiliser le même nom |
| `SNAPSHOT_FLOAT_DECIMALS` | `6` | même pas que `pipeline/geo/io_util.py` ; plus fin serait du bruit, plus gros écraserait des centroïdes voisins |

Le document racine porte `schema_version` égal à
`SNAPSHOT_SCHEMA_VERSION`, **lu**, jamais recopié en littéral dans le
module d'export.

### D3 — Document racine, schéma fermé

Le document JSON a **exactement** ces clés, ni plus ni moins :

| clé | contenu |
|---|---|
| `schema_version` | `"v0a-1"` |
| `seed` | la graine utilisée |
| `tick` | le nombre de ticks réellement avancés (`N`) |
| `cell_count` | nombre de cellules **dérivé** du tableau `cells`, jamais un littéral |
| `crs` | `"EPSG:3035"` |
| `geometry_source` | objet fermé : `path` (chaîne POSIX relative à la racine, `"pipeline/geo/artifacts/cells_g3.json"`) et `sha256` (empreinte **calculée à l'export**, jamais recopiée) |
| `layers` | objet des couches (D6, D7, D8) |
| `cells` | tableau des cellules, ordre D5 |

Aucune horloge murale, aucun horodatage, aucune empreinte du snapshot
lui-même à l'intérieur du document (l'empreinte se calcule **sur** le
fichier écrit).

### D4 — Objet cellule, schéma fermé

Chaque entrée de `cells` porte **exactement** ces clés :

| clé | origine | sentinelle |
|---|---|---|
| `cell_id` | `Cell.cell_id` | jamais absente |
| `area_km2` | `Cell.area_km2` | jamais absente |
| `geometry` | copie de `cells_g3.json` pour ce `cell_id` | export refusé si absente (D9) |
| `centroid` | objet fermé `{lat, lon, x_m, y_m}` copié de G3 | export refusé si absent |
| `population` | `Cell.population` | entier `≥ 0` mesuré |
| `food_stock_kg` | `Cell.food_stock_kg` | `-1` = non calculé |
| `food_deficit_kg` | `Cell.food_deficit_kg` | `-1` = non calculé |
| `hunger_ticks` | `Cell.hunger_ticks` | `-1` = non calculé |
| `mortality_remainder` | `Cell.mortality_remainder` | `-1` = non calculé |
| `province` | vue dérivée (D10) | objet fermé `{id, name}` ; jamais un champ stocké |
| `climate_drivers` | jointure C1 (D7) | objet fermé si la couche est `present` **et** la cellule y figure ; `null` sinon |

`centroid` a **exactement** les quatre clés `lat`, `lon`, `x_m`, `y_m`.
`province` a **exactement** `id` (entier du centre) et `name` (nom lu).
`climate_drivers`, quand ce n'est pas `null`, a **exactement** :

- `insolation_annual_mj_m2`
- `daylight_h_summer_solstice`
- `daylight_h_winter_solstice`
- `dist_sea_centroid_m`
- `hops_to_sea`
- `coastal`

Aucune autre clé. En particulier : pas de `province_id`, pas de `owner`,
pas de `country`, pas d'altitude, pas de quantité de ressource, pas de
barème.

### D5 — Sérialisation canonique et ordre

1. Les cellules sont émises par `cell_id` **entier croissant** — pas un
   tri de chaînes (`"100" < "20"` est interdit).
2. Chaque objet a ses clés triées (`sort_keys=True`).
3. Les flottants sont arrondis à `SNAPSHOT_FLOAT_DECIMALS` **lu**. Les
   entiers restent des entiers JSON.
4. `json.dumps(..., ensure_ascii=False, separators=(",", ":"), sort_keys=True)`
   puis un unique `\n` final. Pas de BOM. Pas de `\r\n`, y compris sous
   Windows : on écrit des octets.
5. Interdit : `NaN`, `Infinity`, `-Infinity`.
6. L'empreinte SHA256 est celle des **octets écrits**. Deux exécutions
   identiques (`même N`, `même S`, mêmes artefacts d'entrée) produisent
   le même fichier et la même empreinte.
7. `sim/` n'importe pas `pipeline/geo/io_util.py` : ce module n'est pas
   une dépendance du moteur, et le moteur reste en bibliothèque
   standard. L'export vit dans un module neuf sous `sim/`.

### D6 — Relief G6 : présent sur disque, non consommé

`cells_relief_g6.json` est suivi par git. Le Planificateur a mesuré
`473` cellules dont `elev_mean_m`, `elev_min_m`, `elev_max_m` et
`centroid_elev_m` valent toutes `0.0`, alors que `sample_count > 0`
partout. Publier ces zéros comme des altitudes ferait d'un défaut de
couverture DEM un niveau de la mer mesuré — exactement la donnée inventée
en silence que la règle n° 10 interdit.

`layers.relief_g6` vaut **exactement** :

```
{"status":"not_consumed","path":"pipeline/geo/artifacts/cells_relief_g6.json"}
```

si le fichier existe ; sinon `{"status":"absent"}`. Aucun champ d'altitude
n'entre dans une cellule. Ce lot ne corrige pas G6.

Le statut `not_consumed` est distinct de `absent` : le fichier est là, on
refuse de le croire. Un futur lot de correction G6 pourra passer la couche
à `present` ; ce n'est pas ce lot.

### D7 — Déterminants climatiques C1 : consommés s'ils sont complets

Si `pipeline/geo/artifacts/cells_climate_drivers_c1.json` existe **et**
que l'ensemble de ses `cell_id` égale exactement l'ensemble G3 :

- `layers.climate_drivers_c1` vaut
  `{"status":"present","path":"pipeline/geo/artifacts/cells_climate_drivers_c1.json","sha256":"<calculé>"}` ;
- chaque cellule porte l'objet `climate_drivers` de D4, recopié des
  champs C1, jamais recalculé.

Si le fichier est absent : `{"status":"absent"}` et
`climate_drivers: null` sur **toutes** les cellules.

Si le fichier existe mais que les `cell_id` ne recouvrent pas G3 : la
couche passe à `not_consumed` (même forme que D6, avec le `path`), et
toutes les cellules ont `climate_drivers: null`. On ne complète pas par
invention. Le compte des cellules C1 manquantes est un **fait mesuré**,
pas un seuil.

`sim/` ne recalcule aucune insolation, aucune distance à la mer, aucun
saut. Il joint par `cell_id`.

### D8 — Autres couches

| couche | constat à l'écriture | publication |
|---|---|---|
| `resources_r1` | fichier `cells_resources_r1.json` absent | `{"status":"absent"}` |
| `rivers_g5` | `rivers_g5.json` existe, mais ce sont des lignes, pas des grandeurs de cellule | **non déclarée** dans `layers` — ce n'est pas une couche cellulaire |
| température, précipitations, saisons | jamais livrées | non déclarées |

`layers` a **exactement** trois clés, toujours présentes, dans cet
ensemble : `relief_g6`, `climate_drivers_c1`, `resources_r1`. Une couche
future s'ajoute par un lot futur, pas ici.

### D9 — Géométrie : copie sourcée, pas un second cadastre

Pour chaque `cell_id` du monde chargé, l'export copie `geometry` et
`centroid` depuis `cells_g3.json`. Si une cellule du monde n'a pas de
géométrie dans G3, **l'export refuse** (code `2`, cellule nommée). On ne
fabrique pas un polygone.

`geometry_source.sha256` est l'empreinte du fichier G3 lue à l'export.
C'est une provenance, pas une géométrie parallèle : le viewer du brief
028 lira **ce** snapshot, pas `cells_g3.json`.

### D10 — Province dérivée, jamais stockée

À l'export seulement, appeler les fonctions **déjà livrées** par
`sim/aggregation.py` (`agregat_depuis_monde` puis consultation par
cellule). Ne pas les recopier. Ne pas ajouter de champ sur `Cell`.
`sim/model.py` reste sans préfixe `province`.

Si `PositionCelluleInconnue` est levée, ou si une cellule chargée n'a pas
de regroupement : **l'export refuse** (code `2`, cellule nommée). Le brief
018 a déjà exigé la couverture totale ; inventer une province ici
rouvrirait l'ADR-0003.

`province.id` est l'identifiant du centre. `province.name` est le nom lu.
Ce couple est une **vue**, recalculée à chaque export.

### D11 — Distinguer zéro, `null` et `-1`

| situation | JSON |
|---|---|
| grandeur calculée, valeur nulle | `0` ou `0.0` |
| couche ou champ non publié | `null` (seulement `climate_drivers` dans D4) |
| grandeur du moteur non encore calculée | `-1` ou `-1.0` |

Un test rouge doit montrer qu'une sentinelle `-1` n'est pas réécrite en
`0` ni en `null`, et qu'un `0` mesuré n'est pas réécrit en `-1`.

Après un monde amorcé (`ticks ≥ 0`), les champs de nourriture, faim et
reste de mortalité du moteur sont aujourd'hui initialisés à des zéros
**mesurés** (voir `World.from_g3`). Ce lot ne change pas cette
initialisation. Il la photographie.

### D12 — Preuves déterministes, et preuves que ça change

Trois exécutions, mêmes artefacts d'entrée, chemins de sortie distincts
sous `deliverables/proofs/` (fichiers suivis par git, pas sous un
répertoire gitignoré) :

| fichier | commande |
|---|---|
| `deliverables/proofs/snapshot_seed0_tick0.json` | `--ticks 0 --seed 0 --snapshot-json …` |
| `deliverables/proofs/snapshot_seed0_tick0_b.json` | **la même**, second passage |
| `deliverables/proofs/snapshot_seed1_tick0.json` | `--ticks 0 --seed 1` |
| `deliverables/proofs/snapshot_seed0_tick5.json` | `--ticks 5 --seed 0` |

- Les deux fichiers `seed0_tick0` ont la **même** empreinte SHA256, non
  vide.
- `seed1_tick0` a une empreinte **différente**, et le nombre de cellules
  dont `population` diffère de `seed0_tick0` est **strictement positif**.
- `tick5` a une empreinte **différente**, et le nombre de cellules dont
  au moins un des champs `food_stock_kg`, `food_deficit_kg`,
  `hunger_ticks`, `mortality_remainder`, `population` diffère est
  **strictement positif**.

Les empreintes se comparent à l'exécution. Aucune valeur hexadécimale
n'est recopiée dans un test, un commentaire ou un document (règle n° 12).
Les quatre chemins sont des couples `must_differ_from` dans
`deliverables/manifest.json` : `seed0_tick0` ↔ `seed1_tick0` ;
`seed0_tick0` ↔ `tick5`. Le couple `seed0_tick0` ↔ `seed0_tick0_b` n'est
**pas** un `must_differ_from` : il doit être identique.

### D13 — Tests rouges puis verts

Nouveau module `sim/tests/test_snapshot_v0a.py` (nom libre dans
`sim/tests/`, préfixe `test_snapshot`). Bibliothèque standard + pytest
déjà utilisé par `sim/tests/`.

**Rouges** (un par famille, mutation en mémoire ou fichier temporaire hors
dépôt — jamais en modifiant le contrôle pour le faire passer) :

1. deux passes identiques dont on altère un octet du second fichier → le
   test d'égalité d'empreinte **rougit** ;
2. on force toutes les `population` de `seed1` à égaler `seed0` → le test
   « la graine change la population » **rougit** ;
3. on remplace un `-1` par `0` dans une copie → le test des sentinelles
   **rougit** ;
4. on retire `schema_version` → le test de schéma fermé **rougit** ;
5. on ajoute `province_id` sur une cellule du JSON → le test d'absence de
   clé spatiale concurrente **rougit** ;
6. on copie une altitude G6 dans une cellule → le test « G6 non consommé »
   **rougit**.

**Verts** : schéma fermé, ordre des `cell_id`, déterminisme deux passes,
graine et ticks qui changent les champs attendus, C1 joint sans
recalcul, G6 absent des cellules, Province dérivée égale à
`identifiant_de_province_de_cellule` recalculé par l'Évaluateur,
`--snapshot-json` omis laisse le CLI inchangé, `sim/tests/` reste verte,
`harness/tests/` reste verte (SKIP Linux/Unity acceptés).

### D14 — Périmètre de fichiers

**Autorisé (création) :**

- un module neuf sous `sim/` pour l'export (nom libre, paquet `sim`) ;
- `sim/tests/test_snapshot_v0a.py` (ou équivalent `test_snapshot*.py`) ;
- `harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/**`.

**Autorisé (modification bornée) :**

- `sim/__main__.py` — ajouter `--snapshot-json` et l'appel d'export ; le
  résumé existant et `--json` restent ; `EXIT_REFUS` inchangé ;
- `sim/constants.py` — **ajout en fin de fichier uniquement**, zéro
  constante préexistante changée ;
- `sim/README.md` — documenter `--snapshot-json` et ce que le snapshot
  n'est pas (pas un rendu, pas une seconde simulation) ;
- `harness/queue/cost-ledger.jsonl` — une seule ligne ajoutée.

**Interdit :** `sim/model.py` (sauf si une garde existante doit rester
intacte — aucun champ nouveau) ; `sim/engine.py` ; `sim/aggregation.py`
(appelé, pas modifié) ; tout fichier sous `pipeline/` ; tout fichier sous
un futur `viewer/` ; `unity/` ; `control-plane/` ; `.github/` ;
`docs/adr/**` ; `ROADMAP.md` ; `HANDOFF.md` ; `hermes/**` ; `VISION.md` ;
`harness/*.py` ; les archives des briefs 001 à 026.

### D15 — Aucune instruction de rendu

Ce brief ne choisit aucune couleur, aucune palette, aucun zoom, aucun
serveur HTTP, aucun fichier HTML. Le brief 028 lira le fichier. S'il
manque un champ ici, c'est ici qu'il se corrige, pas dans le viewer.

---

## Success Conditions

### SC1 — La commande écrit un snapshot `v0a-1` pour une graine et des ticks

Depuis la racine :

```
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/proofs/snapshot_seed0_tick0.json
```

- Code de sortie `0`.
- Le fichier existe, suivi par git.
- `schema_version` lu de `SNAPSHOT_SCHEMA_VERSION`.
- `seed == 0`, `tick == 0`.
- `cell_count` égale le nombre d'entrées de `cells`, et égale le nombre
  de cellules chargées par `World.from_g3` (dérivé, jamais `596` écrit en
  dur dans un test).
- Chaque `cell_id` du snapshot appartient à G3 ; l'ensemble est exactement
  celui de G3.

### SC2 — Déterminisme : deux passes, même empreinte

Les deux fichiers `snapshot_seed0_tick0.json` et
`snapshot_seed0_tick0_b.json` ont la même empreinte SHA256, non vide.
`paires_sha_snapshot_identiques` vaut `1` sur `1`.

### SC3 — Une autre graine, d'autres ticks, changent les champs attendus

- `cellules_population_differente_seed` est **strictement positif**
  (dénominateur : cellules du snapshot `seed0_tick0`).
- `cellules_etat_different_tick` est **strictement positif**
  (dénominateur : mêmes cellules ; un champ parmi stock, déficit, faim,
  reste de mortalité, population).
- Les empreintes `seed1_tick0` et `tick5` diffèrent chacune de
  `seed0_tick0`.

### SC4 — Géométrie sourcée, Province dérivée, C1 joint, G6 non consommé

- `geometry_source.sha256` égale l'empreinte recalculée de
  `cells_g3.json` à la vérification.
- `cellules_sans_geometrie` vaut `0`.
- `cellules_province_non_derivee` vaut `0` ; chaque `province.id` égale
  `identifiant_de_province_de_cellule` recalculé par l'Évaluateur via
  `sim/aggregation.py`, **sans lire le code d'export**.
- `champs_province_sur_cell` vaut `0` dans `sim/model.py`.
- `layers.climate_drivers_c1.status` vaut `present` sur ce dépôt (C1 est
  fusionné et complet) ; `cellules_c1_jointes` égale `cell_count` ;
  `recalculs_c1_dans_sim` vaut `0` (aucune formule d'insolation dans
  `sim/` hors commentaires).
- `layers.relief_g6.status` vaut `not_consumed` ; `champs_altitude_publies`
  vaut `0`.
- `layers.resources_r1.status` vaut `absent`.

### SC5 — Sentinelles honnêtes, schéma fermé, pas de clé spatiale concurrente

- `zeros_qui_etaient_sentinelles` vaut `0` : aucun `-1` du moteur n'est
  devenu `0` ou `null` dans le snapshot.
- `cles_hors_schema_racine` vaut `0` et `cles_hors_schema_cellule` vaut `0`.
- `cles_spatiales_concurrentes` vaut `0` (sous-chaînes `province_id`,
  `owner`, `country`, `pays` dans une **clé** du snapshot — `province`
  comme objet dérivé de D4 est la seule exception nommée, et ses clés
  internes sont `id` et `name`).
- `cles_de_bareme_trouvees` vaut `0` sur le snapshot (mêmes mots que
  `WORLD_TERMS_FORBIDDEN_KEYS` **si** la constante existe déjà dans
  `pipeline/geo/constants.py` ; sinon la liste minimale du brief 025 est
  lue de ce fichier, pas recopiée dans `sim/`).

### SC6 — Preuves committées, README sans sur-revendication, suites vertes

- `fichiers_preuve_suivis_par_git` égale le nombre de preuves déclarées
  (les quatre JSON de D12, le script de mesure, le manifeste, le journal).
- `sim/README.md` décrit `--snapshot-json` et dit que le snapshot n'est
  ni un rendu ni une simulation parallèle.
- Suites :

```
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_sim_passed_027` et `tests_harness_passed_027` portent le nombre
  de tests collectés pour dénominateur. SKIP Linux/Unity acceptés et
  déclarés. Sentinelle `-1` si pytest est ininstallable (Waivers), jamais
  `0`.

- Les tests rouges de D13 existent et **rougissent** sous sabotage
  (`controles_rouges_mordants` égale le nombre de familles de D13).

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Rendre, colorier, zoomer, servir une page, ou choisir une palette.
2. Recalculer une insolation, une distance à la mer, une altitude, une
   mortalité, une faim ou un stock — le moteur et C1 l'ont déjà fait.
3. Consommer `cells_relief_g6.json` comme des altitudes vraies.
4. Exécuter, amender ou juger le brief 026, ni inventer des gisements.
5. Ajouter un champ `province_*` sur `Cell` ou dans `sim/model.py`.
6. Ouvrir un serveur, un démon, une base de données, un compte ou un secret.
7. Importer `pipeline/geo/` dans `sim/` (hors lecture JSON d'artefacts
   committés, bibliothèque standard).
8. Modifier `sim/engine.py` ou la logique de `tick`.
9. Recopier une empreinte hexadécimale (règle n° 12).
10. Reprendre `596`, `473` ou tout autre constat de contexte comme seuil.
11. Employer l'alias nu de l'interpréteur (règle n° 1) : Linux
    `.venv/bin/python`, Windows `py`.
12. Committer, pousser, créer une branche ou fusionner (ADR-0014).
13. Rédiger `verdict.md` ou modifier ce brief.
14. Convertir `null` en `0`, `-1` en `0`, ou `0` en `-1`.
15. Écrire le snapshot sur la sortie standard.

---

## Required Counters

| nom | source | dénominateur |
|---|---|---|
| `cell_count_snapshot` | longueur de `cells` dans le snapshot `seed0_tick0` | cellules chargées par `World.from_g3(rng_seed=0)` ; les deux doivent être égaux |
| `cellules_hors_g3` | `cell_id` du snapshot absents de G3 | cellules du snapshot ; doit valoir `0` |
| `cellules_g3_absentes_du_snapshot` | `cell_id` G3 absents du snapshot | cellules G3 ; doit valoir `0` |
| `paires_sha_snapshot_identiques` | égalité des empreintes `seed0_tick0` et `seed0_tick0_b` | `1` comparaison ; doit valoir `1` |
| `empreinte_seed1_differente` | empreintes `seed0_tick0` vs `seed1_tick0` différentes et non vides | `1` ; doit valoir `1` |
| `empreinte_tick5_differente` | empreintes `seed0_tick0` vs `tick5` différentes et non vides | `1` ; doit valoir `1` |
| `cellules_population_differente_seed` | cellules dont `population` diffère entre `seed0_tick0` et `seed1_tick0` | cellules de `seed0_tick0` ; **strictement positif** |
| `cellules_etat_different_tick` | cellules dont au moins un champ d'état (stock, déficit, faim, reste de mortalité, population) diffère entre `seed0_tick0` et `tick5` | cellules de `seed0_tick0` ; **strictement positif** |
| `cellules_sans_geometrie` | cellules sans `geometry` objet | cellules du snapshot ; doit valoir `0` |
| `sha_source_g3_concordante` | égalité de `geometry_source.sha256` avec l'empreinte recalculée de `cells_g3.json` | `1` ; doit valoir `1` |
| `cellules_province_non_derivee` | cellules dont `province.id` ≠ identifiant recalculé par `aggregation.py` | cellules du snapshot ; doit valoir `0` |
| `champs_province_sur_cell` | champs dont le nom normalisé commence par `province` dans `Cell` | champs de `Cell` ; doit valoir `0` |
| `cellules_c1_jointes` | cellules dont `climate_drivers` n'est pas `null` | cellules du snapshot ; doit égaler `cell_count` sur ce dépôt |
| `recalculs_c1_dans_sim` | formules d'insolation ou de distance à la mer ajoutées sous `sim/` | `1` revue de diff ; doit valoir `0` |
| `champs_altitude_publies` | clés d'altitude / `elev_` / `relief` dans une cellule du snapshot | cellules × clés ; doit valoir `0` |
| `couche_g6_not_consumed` | `layers.relief_g6.status == "not_consumed"` | `1` ; doit valoir `1` sur ce dépôt |
| `couche_c1_present` | `layers.climate_drivers_c1.status == "present"` | `1` ; doit valoir `1` sur ce dépôt |
| `couche_r1_absent` | `layers.resources_r1.status == "absent"` | `1` ; doit valoir `1` sur ce dépôt |
| `zeros_qui_etaient_sentinelles` | champs moteur à `-1` devenus `0` ou `null` dans le snapshot | champs sentinelle du monde chargé ; doit valoir `0` |
| `cles_hors_schema_racine` | clés du document hors D3 | clés du document ; doit valoir `0` |
| `cles_hors_schema_cellule` | clés d'une cellule hors D4 | cellules × clés ; doit valoir `0` |
| `cles_spatiales_concurrentes` | clés interdites (D4 / SC5) | clés balayées ; doit valoir `0` |
| `cles_de_bareme_trouvees` | clés de barème dans le snapshot | clés interdites balayées ; doit valoir `0` |
| `code_sortie_snapshot_ok` | code de la commande SC1 | `1` exécution ; doit valoir `0` |
| `controles_rouges_mordants` | familles de D13 dont le sabotage rougit | `6` ; doit valoir `6` |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec D12 + deliverables | preuves déclarées |
| `tests_sim_passed_027` | tests réussis de `sim/tests/` | tests collectés ; `-1` si waiver |
| `tests_harness_passed_027` | tests réussis de `harness/tests/` | tests collectés ; `-1` si waiver |
| `constants_lignes_supprimees` | lignes supprimées au diff de `sim/constants.py` contre l'instantané pré-édition | `1` ; doit valoir `0` |

Un script committé sous
`harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/measure_snapshot_027.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des fichiers — jamais une valeur recopiée à la main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « pytest n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest`. Outillage de test : le Générateur peut l'installer dans `.venv`. Si l'installation échoue, `tests_*_passed_027` valent `-1` |
| « `cells_g3.json` n'est pas lisible » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/cells_g3.json', encoding='utf-8'))"` | `FileNotFoundError` ou `JSONDecodeError` nommant le fichier. **Blocage**, pas excuse : sans G3 il n'y a pas de monde |
| « C1 est incomplet, je ne peux pas joindre » | un décompte, produit par commande, des `cell_id` G3 absents de C1 | la liste réelle. Alors D7 impose `not_consumed` + `null`, **pas** un inventaire inventé. SC4 `couche_c1_present` n'est pas excusée sur ce dépôt où C1 est complet ; si le fichier a disparu depuis l'écriture, c'est une escalade |
| « deux passes ne peuvent pas être identiques sous Windows » | les deux empreintes et `repr` des fins de ligne des deux fichiers | la sortie réelle. Ce n'est **pas** un waiver : D5 exige `write_bytes` et `\n`. Un échec est un défaut d'export |

---

## Execution Contract

### Interpréteur

Linux : `.venv/bin/python` depuis la racine. Windows : `py`. Jamais
l'alias nu. Aucun worker Unity. Aucune dépendance nouvelle : stdlib +
pytest déjà utilisé par `sim/tests/`.

### Estimation d'appels d'outils

**Estimation du Planificateur : `95` appels.** Sous le seuil de `150`.
Ancres : le brief 018 (agrégation dans `sim/`, ~même largeur) ; ce lot
ajoute un drapeau CLI, un schéma fermé et quatre preuves d'empreinte, sans
nouvelle physique. À vérifier avant génération :

```
.venv/bin/python harness/budget.py split-check --brief harness/queue/briefs/027-sim-snapshot-cellulaire-v0a --estimated-calls 95
```

Un seul sous-système modifié (`sim/`). Lecture seule des artefacts geo.
Pas un objectif global.

### Deliverables obligatoires

Sous `harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/` :

- `manifest.json` — `files[]` (avec les deux couples `must_differ_from` de
  D12), `counters[]` (valeur, `sample_size`, commande), `waivers[]` ;
- `generator-log.md` — français clair : ordre des actes, ce qui a résisté,
  tout écart avec les constats de contexte (`473` zéros G6, C1 complet) ;
- `measure_snapshot_027.py` ;
- `pre-edit/sim-README.md.orig`, `pre-edit/constants.py.orig`,
  `pre-edit/__main__.py.orig` ;
- `proofs/snapshot_seed0_tick0.json`, `proofs/snapshot_seed0_tick0_b.json`,
  `proofs/snapshot_seed1_tick0.json`, `proofs/snapshot_seed0_tick5.json`.

Les instantanés pré-édition sont les couples `must_differ_from` du README
et de `constants.py` / `__main__.py` publiés.

### Interdictions pour le Générateur

Il ne prononce pas la recevabilité, ne rédige aucun `verdict.md`, ne
modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas, ne pousse pas,
ne crée ni ne change de branche, et ne fusionne rien. Il n'exécute pas le
brief 028.

### Fin de lot

Le lot est terminé quand la commande de SC1 sort en `0`, que les six
conditions SC1–SC6 sont couvertes par des compteurs reconstruits, que les
six familles rouges de D13 mordent, et que les deliverables sont prêts
pour l'orchestrateur.

---

## Registre de coût

```
.venv/bin/python harness/backends/ledger.py append --backend cursor --brief harness/queue/briefs/027-sim-snapshot-cellulaire-v0a --event generator-run
```
