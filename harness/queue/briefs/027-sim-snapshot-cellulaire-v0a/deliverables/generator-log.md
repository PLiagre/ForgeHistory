# Journal du Générateur — Brief 027

**Author**: forge-generateur-cursor
**Date**: 2026-08-23

Rôle : Générateur (Cursor Cloud). Première tranche verticale du snapshot
cellulaire, exécutée sur `codex/workflow-acceleration` après import des
plans depuis la PR #125, sur instruction propriétaire d'étendre le lot 029.

Aucune conclusion de recevabilité ici.

## Commandes

```
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/proofs/snapshot_seed0_tick0.json
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/proofs/snapshot_seed0_tick0_b.json
.venv/bin/python -m sim --ticks 0 --seed 1 --snapshot-json harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/proofs/snapshot_seed1_tick0.json
.venv/bin/python -m sim --ticks 5 --seed 0 --snapshot-json harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/proofs/snapshot_seed0_tick5.json
.venv/bin/python -m pytest sim/tests/test_snapshot_v0a.py -q
.venv/bin/python harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/measure_snapshot_027.py
```

Les deux fichiers `seed0_tick0` ont la même empreinte. `seed1` et `tick5`
diffèrent. `cell_count` égal au nombre de cellules chargées. Couche G6
`not_consumed`. Couche C1 `present`. Couche R1 `absent`.
