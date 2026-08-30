# Journal du lot 042

## Rouge prouvé (SC1)

Sur le SHA de base `3372496ec7516a479b35cd0f60258011e0060c2b`, `sim/snapshot_export.py`
exportait `food_stock_kg` via `lire_stock_marchandise` et aucune cellule ne portait de
clé `stocks`. Le mesureur enregistre `rouge_sc1_base_avait_panier=0`.

## Fichiers modifiés

- `sim/constants.py` — `SNAPSHOT_SCHEMA_VERSION` incrémenté (convention v0a-*).
- `sim/snapshot_export.py` — panier `stocks` tel que la cellule le porte ; `jour_de_tick` au niveau document.
- `sim/tests/test_monde.py` — substitution schéma/chemin ; cas panier et jour.
- `viewer/snapshot_loader.py` — refus nommant la version attendue ; `proposed_layers`.
- `viewer/svg_proof.py` — lecture des couches depuis `stocks`.
- `viewer/static/app.js` — couches dérivées du document ; affichage du jour ou « absent ».
- `viewer/static/index.html` — emplacement pour le jour de l'année.
- `viewer/tests/test_viewer_v0b.py` — cas couches, trois états visuels, schéma inconnu.

## Commandes jouées

```bash
.venv/bin/python -m pytest sim/tests/ viewer/tests/ -q
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python harness/queue/briefs/042-le-viewer-montre-ce-qui-joue/deliverables/measure_042.py --write-manifest
```

## Résultats

- 130 tests collectés (125 sur le SHA de base), 130 passés.
- Sortie CLI `--ticks 365 --seed 0 --json` byte-identique au SHA de base.
- Mesureur : 596 cellules avec panier, 0 écart moteur/document, 2 couches proposées
  (population + nourriture), 3 états visuels distincts, 2 SVG déterministes.
- `lignes_de_test_hors_substitution=0`.

## Limites

- Les branches `insolation` / `dist_sea` de `cell_value` restent pour le test climat
  existant ; elles ne figurent plus dans les couches proposées.
- Le jour photographié est `jour_de_tick(tick)` du moteur, pas une date inventée.
