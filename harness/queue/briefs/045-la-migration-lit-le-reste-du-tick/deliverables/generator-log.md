# Journal générateur — Brief 045

**Author**: forge-generateur-cursor
**Authored**: 2026-08-29T21:30:00Z

## SHA de base

```
git rev-parse HEAD
0f87165739ccae2e744760feba8259a5ba01c01f
```

## Rouge SC1 avant correction

Commande (répertoire temporaire avec `engine.py` lu via `git show`, sans remplacer l'arbre courant) :

```
.venv/bin/python harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/measure_045.py
```

Sortie du micro-monde SC1 sur le SHA de base :

```
ration=2.0, reste_dest=1.0
surplus_buggy=0.0
partants_attendus=1
delta_source=0, delta_dest=0
ROUGE=oui
```

La destination possède 1,0 kg post-consommation (strictement positif, inférieur à la ration de 2,0 kg). Le surplus calculé par l'ancienne formule vaut 0,0 : personne ne part.

## Correction produit

Dans `sim/engine.py`, `_surplus_nourriture_tick` lit désormais `max(0, stock_effectif)` sans retrancher une seconde ration. Le surplus du commerce (`_surplus` local, lignes 415-416) n'a pas été touché.

## Diff des chemins autorisés

```
sim/engine.py
sim/tests/test_commerce.py
harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/measure_045.py
harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/manifest.json
harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/generator-log.md
harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/cli_ticks365_seed0_run1.json
harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/cli_ticks365_seed0_run2.json
```

## Compteurs

| compteur | valeur | sample_size |
|---|---|---|
| destinations_reste_positif | 1 | 1 |
| habitants_deplaces_reste_positif | 1 | 1 |
| destinations_stock_nul | 1 | 1 |
| destinations_sentinelle | 1 | 1 |
| habitants_deplaces_stock_nul | 0 | 2 |
| poids_independants_population | 1 | 2 |
| rapport_stocks_destination | 1 | 2 |
| ordres_aretes_essayes | 1 | 2 |
| ecart_population_totale | 0 | 4 |
| cellules_dont_stock_change | 0 | 4 |
| tests_sim_verts | 118 | 118 |

## Suites exigées

```
.venv/bin/python -m pytest sim/tests/test_commerce.py -q
31 passed in 0.19s

.venv/bin/python -m pytest sim/tests/ -q
118 passed in 532.62s

git grep -n '^[[:space:]]*global ' sim/engine.py
(zéro occurrence, exit 1)
```

## CLI déterministe

Deux exécutions `.venv/bin/python -m sim --ticks 365 --seed 0 --json` produisent des sorties byte-identiques, archivées dans `cli_ticks365_seed0_run1.json` et `cli_ticks365_seed0_run2.json`.
