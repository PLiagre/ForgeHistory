# Generator log — lot 036-on-nait-aussi

SHA de base : `9df4917b8e3a4c804c9263eac5973912a8a77092` (master au lancement).

## Rouges prouvés avant correction

### SC1 — cellule rassasiée immobile

Micro-monde : une cellule productive (`area_km2 = 50`, `population = 100`,
stock initial large, `food_deficit_kg = 0`). Après 5 000 ticks avec
`random.Random(42)`, la population reste à 100 sur le SHA de base (aucun
maillon de natalité).

### SC3 — stérilité par arrondi sans report

Même micro-monde avec `population = 5` : le produit `population × taux` vaut
0,001, donc `int(brut)` reste 0 à chaque tick. Sans `natalite_remainder`, la
population resterait à 5 indéfiniment ; sur le SHA de base elle reste aussi à
5 (natalité absente). La borne dérivée est
`ceil(1 / (population × taux)) = 1000` ticks rassasiés.

## Fichiers modifiés

- `sim/constants.py` — `NAISSANCES_PAR_HABITANT_PAR_TICK` et
  `naissances_par_habitant_par_tick()`
- `sim/model.py` — champ `Cell.natalite_remainder` (sentinelle -1.0)
- `sim/world.py` — amorçage 0.0 et sérialisation `World.to_dict()`
- `sim/engine.py` — maillon `_apply_natalite` après la mortalité
- `sim/tests/test_survie.py` — six cas ajoutés (SC1 à SC6)
- `harness/queue/briefs/036-on-nait-aussi/deliverables/*`

## Commandes jouées

```bash
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest sim/tests/test_survie.py sim/tests/test_write_coverage.py sim/tests/test_no_hardcoded.py -q
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python harness/queue/briefs/036-on-nait-aussi/deliverables/measure_036.py
python3 harness/verdict_audit.py harness/queue/briefs/036-on-nait-aussi
grep -n 'global \|natalit\|naissance' sim/engine.py
```

## Résultats

- 75 tests `sim/tests/` verts (69 + 6 nouveaux cas natalité).
- CLI `--ticks 365 --seed 0 --json` : deux exécutions identiques, différentes
  de l'archive `pre-edit/cli_ticks365_seed0.json`.
- `cellules_en_croissance` : 0 avant, > 0 après sur le monde réel à 365 ticks.
- Un seul site d'augmentation de `population` dans `sim/` (hors tests).
- Aucune lecture de `NAISSANCES_PAR_HABITANT_PAR_TICK` par attribut dans
  `engine.py`.

## Limites

- `sim/MODELE.md`, `snapshot_export.py` et `test_monde.py` hors périmètre :
  `natalite_remainder` n'y figure pas encore.
- Le mesureur utilise le `.venv` du dépôt parent si absent dans le worktree.

## Correctif SC2 (relecture PR 152)

Le contrôle `test_cellule_affamee_ne_gagne_pas_habitants` passait un tick
complet : la mortalité vidait la cellule avant qu'un remainder
inconditionnel n'atteigne 1. L'assertion `remainder_max == cell.natalite_remainder`
comparait le max au dernier état du même objet. Les deux gardaient le test
vert si `_apply_natalite` ignorait la faim.

Le maillon est maintenant appelé **directement** avec une pénurie dérivée
(`population × ration`), sur la borne `ceil(1 / (population × taux))` qui
suffirait à naître si la porte était ouverte. Un cas frère ferme la porte
sur une dette non nulle à pénurie nulle. Un cas `ration exacte` exige que
la porte s'ouvre sur `penurie_kg == 0` même si le stock restant est nul.

Le mesureur `naissances_en_cellule_affamee` suit le même protocole isolé :
le zéro est une mesure qui deviendrait un entier positif si la natalité
ignorait la pénurie.
