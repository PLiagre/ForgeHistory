# Generator log — lot 043

## Rouge SC1 prouvé avant correction

Micro-monde : une source, trois receveuses identiques, arêtes aux longueurs
min / médiane / max dérivées de la carte (`297 m`, `80921 m`, `215610 m`).

Sur le SHA de base (`810a8a8`), avec capacité plate `200 kg/arête` :
transferts `[200.0, 200.0, 200.0]` — même quantité malgré des longueurs distinctes.

Après correction : `[59.4, 16184.2, 43122.0]` — rapports alignés sur les longueurs.

## Fichiers modifiés

- `sim/constants.py` : `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK`, `METRES_PAR_KM`, `metres_par_km()`.
- `sim/engine.py` : `_arete_adjacence`, `_capacite_base_arete_kg`, `LongueurFrontiereInvalideError` ; capacité dérivée × relief 040.
- `sim/tests/test_commerce.py` : 7 cas ajoutés (SC1, SC2, SC3, SC5, SC8).
- `deliverables/` : mesureur, manifeste, archive pre-edit.

## Commandes jouées

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json   # avant : kg_transportes ≈ 1.52e6
.venv/bin/python -m pytest sim/tests/test_commerce.py -q
.venv/bin/python -m pytest sim/tests/test_survie.py -q
.venv/bin/python harness/queue/briefs/043-le-convoi-a-l-echelle-de-la-cellule/deliverables/measure_043.py
```

## Résultats

- `kg_transportes` après correction : ≈ 8.18e7 (×46 vs médiane dérivée / capacité plate).
- `test_chaque_constante_du_moteur_change_le_monde` : échec attendu sur `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` seul (`_MondeEpreuve` sans `shared_length_m`).
- Conservation de masse, invariants commerce et survie : verts.

## Limites

- `test_survie.py` : ~34 min sur ce VPS (horizon 1000 ticks monde entier).
- Le mot « global » subsiste dans un commentaire docstring préexistant (`aléa global`) ; aucune instruction `global` ajoutée.
