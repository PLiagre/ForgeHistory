# generator-log — brief 024 (G6 relief)

**Rôle** : Générateur (Cursor)
**Worktree** : `.forgepilot/worktrees/024-geo-relief-g6`
**Date** : 2026-08-20

## Provisionnement

- `.venv/bin/pip install -r pipeline/geo/requirements.txt` — OK (shapely, geopandas, pyproj, rasterio, matplotlib).
- `.venv/bin/python -c "import shapely, geopandas, pyproj, rasterio; print('ok')"` — OK.
- `.venv/bin/pip install pytest` — OK (outillage harnais, hors D13).

## Cache DEM (D2)

- `tools/fetch_dem_tiles.py` : 179/179 tuiles vérifiées (SHA256 individuel = `sources.lock`).
- Recette collective retenue : **`sha256_concat_sorted_name_plus_tile_sha256_hex`**
  — SHA256 de la concaténation triée `nom_tuile + sha256_tuile`.
- `collective_ok=True` avec `dem.collective_sha256` de `sources.lock`.
- Relance sur cache complet : 0 retéléchargement, code 0.

## Implémentation

- `steps/06_relief.py` — échantillonnage cellule, pente, rugosité, barrières/cols, export, captures.
- `tools/fetch_dem_tiles.py` — téléchargement idempotent S3, vérification par tuile et collective.
- `tests/run_proof_g6.py`, `tests/test_qa_red_g6.py` — preuve deux passes et cas rouges (Q10, G6-A…G6-E).

## Corrections appliquées pendant l'exécution

1. **`shared_boundary`** : `linemerge` sur une seule `LineString` levait `ValueError` — fusion conditionnelle via `_merge_lines`.
2. **`write_captures`** : cellules `MultiPolygon` — dessin via `_as_polygons` / `_geom_lonlat_rings`.
3. **`fetch_dem_tiles.compute_collective_sha256`** : recette corrigée (voir ci-dessus).

## Contrôles exécutés (2026-08-20, revalidation complète)

| commande | résultat |
|---|---|
| `.venv/bin/pip install -r pipeline/geo/requirements.txt` | OK (déjà installé) |
| import shapely/geopandas/pyproj/rasterio | OK |
| `tools/fetch_dem_tiles.py` | code 0, 179/179, collective_ok=True, 0 retéléchargement |
| `tests/run_proof_g6.py` | code 0, 6/6 contrôles verts, 6/6 preuves rouges, 8 paires SHA256 égales, 2096,7 s |
| `pipeline.py --source relief` | code 0, `barriers=14 passes=14`, `below_0_km2=4913.348`, 991,8 s |
| `git status --porcelain` (11 fichiers interdits) | vide |
| `git status --porcelain` (adjacency_g5, cells_g3) | vide |
| `git status --porcelain --ignored sources/dem_cache/` | `!!` (ignoré), `git ls-files` vide |
| `git add -f` + `git ls-files` (14 preuves) | 14/14 indexées (staging, sans commit) |
| `pytest harness/tests/ -q` | 348 passed, 16 skipped, 0 failed / 364 collectés |
| `measure_g6_024.py` | 20 compteurs imprimés, tous conformes aux SC |
| `ledger.py append --event generator-run` | ligne déjà présente (2026-08-20T20:31:34) |

## Mesures clés (depuis artefacts)

- `barrier_count` = 14, `pass_count` = 14, `passes_nommes_trouves` = 2/9
- `cellules_sans_echantillon` = 0 / 596
- `echantillons_exclus_hors_plage` = 21
- `below_0_land_km2` = 4913.348

## Non prononcé

- Recevabilité du lot : rôle Évaluateur uniquement.
- Aucun commit, push ni fusion effectué.
