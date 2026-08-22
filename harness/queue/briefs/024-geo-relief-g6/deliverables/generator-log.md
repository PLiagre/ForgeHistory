---
**Author**: forge-generateur
**Reviewer**: forge-evaluateur
---

# generator-log — brief 024 (G6 relief) — exécution A2 (itération corrective)

**Rôle** : Générateur (Cursor / Composer)
**Worktree** : `.forgepilot/worktrees/024-geo-relief-g6`
**Date** : 2026-08-22

## Contexte A2 — corrections verdict GPT

- Suppression de `_neighbor_tile_names` et de toute branche `alternates` ; lecture hors
  bornes = erreur immédiate.
- Attribution D19 avec bandes demi-pixel mesurées par tuile (`_resolve_tile_name`), sans
  repli vers des voisines.
- Comparaison multi-tuiles sur les lignes de degré ; compteurs
  `points_de_bord_multi_tuiles` / `points_de_bord_valeurs_concordantes` publiés.
- Compteur littoral : les trois cellules et neuf lectures brutes à zéro sont toutes
  comptées et publiées (plus aucune exemption).
- D21 : listes `tuiles_retirees_par_la_regle_de_domaine` point par point (921 tuiles).
- Provenance DEM : `verify_tile_public`, `cache_files_hors_lock` dans `ensure_dem_cache`.
- Artefacts de disponibilité séparés (`dem_tile_availability_g6.json` /
  `dem_tile_availability_lock_g6.json`).
- Suppression de `test_ensure_dem_bounds_report.py` (hors D13).
- `measure_g6_024.py` : détection AST des fonctions de synthèse raster (plus de grep
  lexical sur `from_bounds`).

## Commandes exécutées (codes de sortie mesurés)

| commande | code | durée approx. |
|---|---|---|
| `tools/required_dem_tiles.py` | 0 | 382 s |
| `tools/fetch_dem_tiles.py --probe` | 0 | (inclus lot précédent) |
| `tools/fetch_dem_tiles.py --probe-lock` | 0 | (inclus lot précédent) |
| `tests/run_proof_g6.py` (2 passes + contrôles + cas rouges) | 0 | 3 881 s |
| `.venv/bin/python -m pytest harness/tests/ -q` | 0 | 9 s |
| `harness/verdict_audit.py harness/queue/briefs/024-geo-relief-g6` | 1 | <1 s |
| `deliverables/measure_g6_024.py` | 0 | 15 s |
| `.venv/bin/python -m sim --ticks 0 --json` | 0 | <1 s |

`verdict_audit` = 1 attendu ici : pas de `verdict.md` (rôle Évaluateur).

## Cache DEM (D19 / D20)

- Tuiles requises dérivées : **1110**.
- `tuiles_retirees_par_la_regle_de_domaine` contient
  `Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif` (921 entrées au total).
- Sondage requis : **1110/1110** (`200`), **0** absente.
- Sondage lock : **1110/1110** (`200`).
- `registrement_dem_mesure` : **pixel_point** (homogène sur 1110 tuiles).
- `demi_pixel_deg` : **0,000417** (publié dans `stats_g6.json`).
- `tuiles_regle_domaine_conforme` : **1110/1110**.
- `fichiers_du_cache_hors_lock` : 0
- fonctions_de_synthese_de_tuile : 0 (dérivation AST, dénominateur 1)

## Mesures clés (`measure_g6_024.py`, 2026-08-22)

Les 14 compteurs ci-dessous sont repris tels quels de la sortie du script (SC8) :

- `tuiles_verifiees` = 1110
- `tuiles_requises` = 1110
- `empreinte_collective_egale` = 1
- `points_lus_grille` = 11449061
- `points_sur_ligne_de_degre` = 188723
- `lectures_hors_bornes_du_fichier` = 0
- `points_de_bord_multi_tuiles` = 0
- `cellules_altitude_min_nulle` = 388
- `cellules_sans_littoral_avec_echantillon_a_zero` = 3
- `barrier_count` = 31
- `below_0_land_km2` = 4913.348
- `cas_rouges_amendement_non_vides` = 7
- `tuiles_bornes_nom_vs_raster_egales` = 1110
- `code_sortie_run_proof_g6` = 0

Autres faits mesurés (hors bloc SC8 des 14 compteurs) :

- tuiles_manquantes : 0 ; tuiles_excedentaires_restantes : 0
- points_lus_centroides : 596 ; points_lus_frontieres : 154897
- points_de_bord_valeurs_concordantes : 0
- pass_count : 31 ; passes_nommes_trouves : 2
- tests_harness_passed_024 : 348 passed, 16 skipped, 0 failed

## Escalade — cellules sans littoral avec échantillon brut à zéro (D23)

Trois cellules hors littoral portent au moins un échantillon brut `0,0 m` indexable.
Neuf lectures au total, publiées dans `stats_g6.json` sous
`cellules_sans_littoral_lectures_zero` :

| cell_id | lon | lat | tuile publique | row | col | valeur |
|---|---|---|---|---|---|---|
| 1492 | 34.816667 | 45.816667 | Copernicus_DSM_COG_30_N45_00_E034_00_DEM.tif | 220 | 980 | 0.0 |
| 1492 | 34.816667 | 45.825 | Copernicus_DSM_COG_30_N45_00_E034_00_DEM.tif | 210 | 980 | 0.0 |
| 1492 | 34.816667 | 45.833333 | Copernicus_DSM_COG_30_N45_00_E034_00_DEM.tif | 200 | 980 | 0.0 |
| 10189 | -0.991667 | 53.583333 | Copernicus_DSM_COG_30_N53_00_W001_00_DEM.tif | 500 | 7 | 0.0 |
| 10427 | 4.433333 | 51.066667 | Copernicus_DSM_COG_30_N51_00_E004_00_DEM.tif | 1120 | 347 | 0.0 |
| 10427 | 4.4 | 51.075 | Copernicus_DSM_COG_30_N51_00_E004_00_DEM.tif | 1110 | 320 | 0.0 |
| 10427 | 4.366667 | 51.083333 | Copernicus_DSM_COG_30_N51_00_E004_00_DEM.tif | 1100 | 293 | 0.0 |
| 10427 | 4.391667 | 51.216667 | Copernicus_DSM_COG_30_N51_00_E004_00_DEM.tif | 940 | 313 | 0.0 |
| 10427 | 4.4 | 51.233333 | Copernicus_DSM_COG_30_N51_00_E004_00_DEM.tif | 920 | 320 | 0.0 |

## Non prononcé

- Recevabilité du lot : rôle Évaluateur uniquement.
- Aucun commit, push ni fusion effectué.
