# sim/

Moteur de simulation ForgeHistory — couche 1 « monde vivant ».

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
| `sim/SEEDING.md` | Documentation de l'amorçage paramétrique |

---

## Source des données d'entrée

Les artefacts G3 sont générés par le pipeline géographique :

- `pipeline/geo/artifacts/cells_g3.json` — cellules (cell_id, area_km2, centroid, geometry…)
- `pipeline/geo/artifacts/adjacency_g3.json` — arêtes d'adjacence
- `pipeline/geo/artifacts/stats_g3.json` — statistiques (`cell_count`, etc.)

Le nombre exact de cellules et d'arêtes est disponible dans `stats_g3.json`
(`cell_count`) et se recalcule à chaque rejeu du pipeline géographique — il
n'est pas recopié ici pour éviter la stale data.

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
  `docs/adr/0003-single-spatial-primary-key.md`.
- **Commerce inter-cellules physique** : les 1 364 arêtes d'adjacence G3
  sont lues par `_apply_commerce` à chaque tick. Transfert borné par
  `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Conservation stricte de la masse.
  (Brief 012, SC4.)
- **Population agrégée** : pas encore de familles ou de personnes individuelles.
- **stdlib uniquement** : le moteur n'a aucune dépendance tierce (pytest est
  réservé aux tests).
