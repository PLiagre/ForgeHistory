# sim/

Moteur de simulation ForgeHistory — **le produit vivant** (ADR-0016).
Il tourne **sans Unity** :

```
.venv/bin/python -m sim
.venv/bin/python -m sim --ticks 0 --json
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
```

`--snapshot-json` écrit une photographie cellulaire déterministe (schéma
`SNAPSHOT_SCHEMA_VERSION`) : géométrie, état simulé, province dérivée,
climat. Ce n'est pas une seconde simulation. Le snapshot déclare lui-même,
couche par couche, ce que le moteur consomme et ce qu'il ne consomme pas.

Le nom du schéma n'est pas recopié ici : il est dans `sim/constants.py`, et
un document qui porte une version morte piège le brief suivant (règle 12).

La clé spatiale unique qui fonde tout ceci est décidée par
`docs/adr/0003-single-spatial-primary-key.md`. Les briefs qui ont autorisé
l'écriture de ce code sont archivés au commit `da1596d` (voir AGENTS.md,
§ « Les archives »).

La vision complète du moteur est dans [`VISION.md`](../VISION.md). Les
principes de simulation (sept modes d'échec diagnostiqués) sont dans
[`AGENTS.md`](../AGENTS.md).

---

## Modules

| Fichier | Rôle |
|---|---|
| `sim/__init__.py` | Paquet Python, expose `__version__` |
| `sim/constants.py` | Constantes paramétriques nommées (voir `sim/MODELE.md`) |
| `sim/model.py` | Dataclass `Cell` — entité géographique de base |
| `sim/world.py` | `World` — chargement depuis les artefacts G3, sérialisation |
| `sim/engine.py` | `tick(world, rng)` — avance le monde d'un pas de temps (production + consommation + commerce + faim + mortalité) |
| `sim/aggregation.py` | Agrégation dérivée : regroupe les cellules par centre administratif le plus proche. Ne modifie rien, n'écrit rien |
| `sim/__main__.py` | `python -m sim` — lance le monde, sans Unity |
| `sim/snapshot_export.py` | Photographie cellulaire déterministe (`--snapshot-json`) |
| `sim/MODELE.md` | Comment le monde fonctionne — tenu par Claude (ADR-0018) |

---

## Source des données d'entrée

Les artefacts G3 sont générés par le pipeline géographique :

- `data/world-1400.json` — la carte figée : cellules (cell_id, area_km2, centroid,
  geometry, relief, climat, gisements) et adjacence, dans un seul fichier

`sim/aggregation.py` lit deux sources supplémentaires, toujours en lecture
seule :

- `data/world-1400.json` — la position géographique de chaque
  cellule (`centroid.lat`, `centroid.lon`, repère WGS84) ;
- `data/province-centres-1400.json` — les centres
  administratifs hérités du jeu (tableau `coordinates` : `id`, `name`, `lon`,
  `lat`) et le paramètre de projection `projection.mid_latitude`.

Ces centres sont un proxy hérité, pas des frontières historiques : leur
provenance et les limites de ce qu'ils prouvent sont décrites dans
`sim/MODELE.md`, section « La province dérivée et ses centres ».

Le nombre exact de cellules et d'arêtes n'est recopié nulle part : il se lit
dans `data/world-1400.json` et se dérive du chargement. La commande qui le
donne est `python -m sim --ticks 0 --json`.

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
  (nombre lu dans `data/world-1400.json` / le fichier
  d'adjacence, jamais recopié ici) sont lues par `_apply_commerce` à
  chaque tick. Transfert borné par
  `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Conservation stricte de la masse.
  (Brief 012, SC4.)
- **Population agrégée** : pas encore de familles ou de personnes individuelles.
- **stdlib uniquement** : le moteur n'a aucune dépendance tierce (pytest est
  réservé aux tests).
