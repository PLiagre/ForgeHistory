# Journal du lot 037 — Le stock devient un panier

## Rouge prouvé (SC1)

Avant édition, le parcours AST de `sim/` hors tests comptait **16** références
à l'attribut `food_stock_kg` (notamment dans `engine.py`, `world.py`,
`__main__.py`, `snapshot_export.py`). Après migration : **0**.

## Fichiers modifiés

- `sim/model.py` — champ `stocks`, accès nommés `lire_stock_marchandise` /
  `ecrire_stock_marchandise`, sérialisation `cellule_vers_dict`, compatibilité
  `food_stock_kg=` via `__init__` et propriété.
- `sim/constants.py` — `MARCHANDISE_NOURRITURE`, `MARCHANDISE_SONDE_037`.
- `sim/engine.py` — moteur via accès nommés uniquement.
- `sim/world.py` — amorçage `Cell(stocks=…)`, `to_dict()` via `cellule_vers_dict`.
- `sim/__main__.py`, `sim/snapshot_export.py` — lecture via accès nommés.
- `sim/tests/test_monde.py` — ajouts SC3, SC4, SC5 en fin de fichier.
- `deliverables/` — mesureur, archives SC2, manifeste.

## Commandes jouées

```bash
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json …
.venv/bin/python -m pytest sim/tests/ viewer/tests/ -q
.venv/bin/python harness/queue/briefs/037-le-stock-devient-un-panier/deliverables/measure_037.py
```

## Résultats

Les trois sorties SC2 sont byte-identiques aux archives prises avant édition.
`World.to_dict()` expose désormais le panier complet ; ce n'est pas SC2.
`tests_collectes_apres` (79) > `tests_collectes_avant` (76).

## Limites

- Aucune marchandise réelle nouvelle simulée ; seule la forme du stockage change.
- Le snapshot conserve la clé `food_stock_kg` ; le viewer n'a pas été touché.
- `verdict.md` non écrit par l'exécutant (porte mécanique séparée).
