# sim/

Moteur de simulation ForgeHistory — **le produit vivant** (ADR-0016).
Il tourne **sans Unity** :

```
.venv/bin/python -m sim
.venv/bin/python -m sim --ticks 0 --json
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
```

`--snapshot-json` écrit une photographie cellulaire déterministe (schéma
`v0a-1`) : géométrie G3, état simulé, province dérivée, déterminants C1
déjà joints. Ce n'est pas une seconde simulation. ADR-0019 : le snapshot
ne porte plus G6 ni R1. `pipeline/geo/` est une archive, pas le produit.

L'amorçage est un **proxy d'ordre de grandeur** (`sim/SEEDING.md`).
Le moteur n'exige pas des données historiques absolument valides ni une
prédiction fermée. On garde l'économie physique, le déterminisme et
`cell_id` unique.

Ce répertoire était un stub vide jusqu'au brief 011
(`harness/queue/briefs/011-sim-monde-vivant-amorcage/brief.md`), qui constitue
la première autorisation d'écrire du code de simulation. Le brief 012
(`harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/brief.md`)
a ajouté la base de temps unique, le déficit alimentaire persisté et le
commerce inter-cellules physique. Cette autorisation était conditionnée à
l'ADR sur la clé spatiale — voir
`docs/adr/0003-single-spatial-primary-key.md`.

La vision complète du moteur est dans [`VISION.md`](../VISION.md). Les
principes de simulation (sept modes d'échec diagnostiqués) sont dans
[`docs/rules/simulation-principles.md`](../docs/rules/simulation-principles.md).

---

## Modules

| Fichier | Rôle |
|---|---|
| `sim/__init__.py` | Paquet Python, expose `__version__` |
| `sim/constants.py` | Constantes paramétriques nommées (voir `sim/SEEDING.md`) |
| `sim/model.py` | Dataclass `Cell` — entité géographique de base |
| `sim/world.py` | `World` — chargement depuis les artefacts G3, sérialisation |
| `sim/engine.py` | `tick(world, rng)` — avance le monde d'un pas de temps (production + consommation + commerce + faim + mortalité) |
| `sim/aggregation.py` | Agrégation dérivée : regroupe les cellules par centre administratif le plus proche. Ne modifie rien, n'écrit rien |
| `sim/__main__.py` | `python -m sim` — lance le monde, sans Unity |
| `sim/snapshot_export.py` | Photographie cellulaire déterministe (`--snapshot-json`) |
| `sim/SEEDING.md` | Documentation de l'amorçage paramétrique |

---

## Source des données d'entrée

Les artefacts G3 sont générés par le pipeline géographique :

- `pipeline/geo/artifacts/cells_g3.json` — cellules (cell_id, area_km2, centroid, geometry…)
- `pipeline/geo/artifacts/adjacency_g3.json` — arêtes d'adjacence
- `pipeline/geo/artifacts/stats_g3.json` — statistiques (`cell_count`, etc.)

`sim/aggregation.py` lit deux sources supplémentaires, toujours en lecture
seule :

- `pipeline/geo/artifacts/cells_g3.json` — la position géographique de chaque
  cellule (`centroid.lat`, `centroid.lon`, repère WGS84) ;
- `pipeline/geo/legacy_game_data/province_coordinates.json` — les centres
  administratifs hérités du jeu (tableau `coordinates` : `id`, `name`, `lon`,
  `lat`) et le paramètre de projection `projection.mid_latitude`.

Ces centres sont un proxy hérité, pas des frontières historiques : leur
provenance et les limites de ce qu'ils prouvent sont décrites dans
`sim/SEEDING.md`, section « brief 018 ».

Le nombre exact de cellules et d'arêtes est dans `stats_g3.json`
(`cell_count`). `pipeline/geo/` est une archive (ADR-0019) : on ne
rejoue pas G6 ni le pipeline comme travail courant. Le chiffre n'est
pas recopié ici.

Ces fichiers sont suivis par git et lisibles depuis un clone frais.

---

## Lancer les tests

```
.venv/bin/python -m pytest sim/tests/ -v
```

Depuis la racine du dépôt. La suite attend tous les tests PASSED,
exit code 0. Les fichiers `proof_red/*.txt` ne font pas partie de la
suite de tests (artefacts de preuve, non collectés par pytest).

---

## Règles architecturales importantes

- **ADR-0003** : `cell_id` est la seule clé spatiale. `Province` est une
  agrégation dérivée — jamais un champ stocké. Voir
  `docs/adr/0003-single-spatial-primary-key.md`. `sim/aggregation.py` met
  cette règle en œuvre : la vue dérivée `Regroupement` y est déclarée, hors de
  `sim.model`, et le déplacement d'un centre administratif recalcule
  l'appartenance sans réécrire aucune cellule.
- **Commerce inter-cellules physique** : les arêtes d'adjacence G3
  (nombre lu dans `pipeline/geo/artifacts/stats_g3.json` / le fichier
  d'adjacence, jamais recopié ici) sont lues par `_apply_commerce` à
  chaque tick. Transfert borné par
  `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Conservation stricte de la masse.
  (Brief 012, SC4.)
- **Population agrégée** : pas encore de familles ou de personnes individuelles.
- **stdlib uniquement** : le moteur n'a aucune dépendance tierce (pytest est
  réservé aux tests).
