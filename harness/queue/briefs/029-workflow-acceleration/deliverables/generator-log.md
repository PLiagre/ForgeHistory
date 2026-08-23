# Journal du Générateur — Brief 029

**Author**: forge-generateur-cursor
**Date**: 2026-08-23

Rôle : Générateur (Cursor Cloud) sur `codex/workflow-acceleration`, head
de départ `acd231070a6979ba41156d16193cb6cecd0df22d`.

## Consolidation

- PR #125 importée par merge : `a9bfac7312a35a7648d9345f72be2b82aa8dbe0a`
  (`plan: prépare V0 snapshot cellulaire et visualiseur web mince`).
- PR #124 reprise fichier par fichier depuis
  `origin/agent/024-geo-relief-g6`
  (`4efd446f14e0280a798fca4e0ab05dbc20656a84`,
  `31c77cf2767db62b66e9e037e45604091cd5b6a5`).
- Conflit sémantique G6 : règles D16–D23 de #124 conservées ; lecture
  groupée, cache partagé, table de mesures et verrou de #126 greffés.
  `_tile_bounds_from_name` suit D16 (ouest/sud). Pas de clamp. nodata =
  `None`. LRU = 48. `load_dem_spec` rend quatre valeurs.

## Instruction propriétaire hors brief 029

Le brief 029 interdisait d'exécuter 027/028. L'instruction de cette
session demande la première tranche visible. Les dossiers 027/028
contiennent donc aussi leurs preuves. Ce n'est pas une interprétation
du brief 029 : c'est un élargissement propriétaire.

## Commandes rejouées

```
.venv/bin/python -m pytest pipeline/geo/tests/test_g6_acceleration.py -q
.venv/bin/python -m pytest sim/tests/test_snapshot_v0a.py viewer/tests/test_viewer_v0b.py -q
```

La preuve G6 Europe complète (`tests/run_proof_g6.py`) n'a pas été
relancée ici : le cache DEM Copernicus n'est pas provisionné sur cette
VM. Absence déclarée, pas convertie en succès.
