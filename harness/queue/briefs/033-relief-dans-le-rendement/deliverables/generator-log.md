# Journal du lot 033 — relief dans le rendement

**Authored**: 2026-08-25T19:10:00Z
**Author**: Cursor

## Rouge avant correction

Mesure sur le SHA de base `448aa2a6c733331aebfb031e217a5c68f4c02c07` :

- cinq appels à `production_kg()` à surface et rendement identiques produisent
  tous `180.0` kg : le relief ne distingue pas la production ;
- `build_snapshot_document(World.charger(0), 0, 0)` déclare
  `couches.relief.utilisee_par_le_moteur is False` ;
- `.venv/bin/python -m sim --ticks 20 --seed 0 --json` donne notamment
  `cellules_affamees = 3` (archivé dans `deliverables/pre-edit/cli_ticks20_seed0.json`).

## Fichiers modifiés

- `sim/constants.py` : cinq facteurs nommés niveau 2 et table
  `facteurs_production_par_relief()` ;
- `sim/engine.py` : unique `production_kg()` lit `world.carte` via
  `_carte_du_tick` pendant le tick ; erreur explicite si classe absente ou inconnue ;
- `sim/tests/test_monde.py` : trois cas SC1/SC2 ajoutés (assertions existantes inchangées).

## Commandes jouées

```bash
.venv/bin/python -m pytest sim/tests/test_monde.py -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python harness/queue/briefs/033-relief-dans-le-rendement/deliverables/measure_033.py
```

## Résultats

- `sim/tests/` : 65 passed ;
- sortie CLI après changement : `cellules_affamees = 219` (> 3 sur la base) ;
- sonde snapshot : relief consommé, climat et gisements non consommés ;
- déterminisme : deux exécutions CLI identiques.

## Limites

- le climat et les gisements ne jouent pas dans ce lot ;
- les mondes sans carte (épreuves unitaires) conservent le chemin historique sans relief.
