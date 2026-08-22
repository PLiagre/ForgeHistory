#!/usr/bin/env python
"""Dérive la liste des tuiles Copernicus DEM requises par G6 (D15, D19).

Importe les fonctions de génération de points de ``steps/06_relief.py`` ;
n'ouvre aucun raster.

Usage, depuis ``pipeline/geo/`` :
  ../../.venv/bin/python tools/required_dem_tiles.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path
from typing import Dict, List, Set, Tuple

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from io_util import read_json, write_json  # noqa: E402

ARTIFACTS = ROOT / "artifacts"
LOCK_PATH = ROOT / "sources.lock"
PRE_EDIT_LOCK = (
    ROOT.parent.parent
    / "harness/queue/briefs/024-geo-relief-g6/deliverables/pre-edit/pipeline-geo-sources.lock.orig"
)
OUTPUT = ARTIFACTS / "dem_required_tiles_g6.json"


def _load_relief():
    path = ROOT / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    relief = _load_relief()
    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    adj_doc = read_json(ARTIFACTS / "adjacency_g5.json")
    cells = cells_doc["cells"]
    adjacency_g5 = adj_doc["adjacency"]

    from projection import Projector, detect_projection  # noqa: E402
    from shapely.geometry import shape  # noqa: E402

    projector = Projector(detect_projection())
    cell_geoms = {}
    for cell in cells:
        geom = shape(cell["geometry"])
        if not geom.is_valid:
            geom = geom.buffer(0)
        cell_geoms[int(cell["cell_id"])] = geom

    lock = read_json(LOCK_PATH)
    lock_tiles = set(lock["dem"]["tiles"])
    fetch_path = ROOT / "tools" / "fetch_dem_tiles.py"
    import importlib.util

    fspec = importlib.util.spec_from_file_location("fetch_req", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)
    sample_tile = sorted(lock_tiles)[0]
    _, half_px_lon, _ = relief.measure_tile_registration(
        fetch.tile_cache_path(sample_tile)
    )
    tile_resolver = relief.DemSampler(fetch.CACHE_DIR, lock_tiles, set())

    grid_points = 0
    centroid_points = 0
    frontier_points = 0
    points_sur_ligne_grille = 0
    points_sur_ligne_centroides = 0
    points_sur_ligne_frontieres = 0
    lon_min = lon_max = lat_min = lat_max = None
    required: Set[str] = set()
    required_nominal: Set[str] = set()
    retired_by_rule: Set[str] = set()
    added_by_rule: Set[str] = set()

    def note_point(lon: float, lat: float, family: str) -> None:
        nonlocal lon_min, lon_max, lat_min, lat_max
        nonlocal points_sur_ligne_grille, points_sur_ligne_centroides
        nonlocal points_sur_ligne_frontieres
        canonical = tile_resolver._resolve_tile_name(lon, lat)
        nominal = relief.lonlat_to_tile_name_nominal(lon, lat)
        required.add(canonical)
        required_nominal.add(nominal)
        if relief.is_degree_line_point(lon, lat) and canonical != nominal:
            retired_by_rule.add(nominal)
            added_by_rule.add(canonical)
        if relief.is_degree_line_point(lon, lat):
            if family == "grille":
                points_sur_ligne_grille += 1
            elif family == "centroide":
                points_sur_ligne_centroides += 1
            else:
                points_sur_ligne_frontieres += 1
        if lon_min is None:
            lon_min = lon_max = lon
            lat_min = lat_max = lat
        else:
            lon_min = min(lon_min, lon)
            lon_max = max(lon_max, lon)
            lat_min = min(lat_min, lat)
            lat_max = max(lat_max, lat)

    for cell in cells:
        cid = int(cell["cell_id"])
        geom = cell_geoms[cid]
        for lon, lat, _i, _j in relief.grid_points_in_polygon(geom, projector):
            grid_points += 1
            note_point(lon, lat, "grille")
        c = cell["centroid"]
        centroid_points += 1
        note_point(float(c["lon"]), float(c["lat"]), "centroide")

    for lon, lat, _ctx in relief.iter_frontier_lonlat_points(
        adjacency_g5, cell_geoms, projector
    ):
        frontier_points += 1
        note_point(lon, lat, "frontiere")

    present = required & lock_tiles
    missing = sorted(required - lock_tiles)
    excess = sorted(lock_tiles - required)

    pre_edit_tiles: Set[str] = set()
    if PRE_EDIT_LOCK.is_file():
        pre_edit_tiles = set(read_json(PRE_EDIT_LOCK)["dem"]["tiles"])
    tuiles_ajoutees = sorted(required - pre_edit_tiles) if pre_edit_tiles else []
    tuiles_excedentaires_retirees = (
        sorted(pre_edit_tiles - set(lock["dem"]["tiles"])) if pre_edit_tiles else []
    )
    tuiles_retirees_par_la_regle_de_domaine = sorted(retired_by_rule)
    tuiles_ajoutees_par_la_regle_de_domaine = sorted(added_by_rule)

    doc = {
        "tuiles_requises": sorted(required),
        "comptes": {
            "tuiles_requises": len(required),
            "tuiles_presentes_dans_lock": len(present),
            "tuiles_manquantes": len(missing),
            "tuiles_excedentaires": len(excess),
        },
        "tuiles_manquantes": missing,
        "tuiles_excedentaires": excess,
        "tuiles_retirees_par_la_regle_de_domaine": tuiles_retirees_par_la_regle_de_domaine,
        "tuiles_ajoutees_par_la_regle_de_domaine": tuiles_ajoutees_par_la_regle_de_domaine,
        "tuiles_ajoutees": tuiles_ajoutees,
        "tuiles_excedentaires_retirees": tuiles_excedentaires_retirees,
        "points": {
            "grille": grid_points,
            "centroides": centroid_points,
            "frontieres": frontier_points,
            "total_lectures_altitude": grid_points + centroid_points + frontier_points,
            "sur_ligne_de_degre": {
                "grille": points_sur_ligne_grille,
                "centroides": points_sur_ligne_centroides,
                "frontieres": points_sur_ligne_frontieres,
                "total": (
                    points_sur_ligne_grille
                    + points_sur_ligne_centroides
                    + points_sur_ligne_frontieres
                ),
            },
        },
        "emprise": {
            "lon_min": lon_min,
            "lon_max": lon_max,
            "lat_min": lat_min,
            "lat_max": lat_max,
        },
    }
    write_json(OUTPUT, doc)

    c = doc["comptes"]
    p = doc["points"]
    print(
        f"required_dem_tiles: requises={c['tuiles_requises']} "
        f"presentes={c['tuiles_presentes_dans_lock']} "
        f"manquantes={c['tuiles_manquantes']} "
        f"excedentaires={c['tuiles_excedentaires']}"
    )
    print(
        f"points: grille={p['grille']} centroides={p['centroides']} "
        f"frontieres={p['frontieres']} total={p['total_lectures_altitude']}"
    )
    print(
        f"points_sur_ligne_de_degre: total={p['sur_ligne_de_degre']['total']} "
        f"retirees_regle={len(tuiles_retirees_par_la_regle_de_domaine)} "
        f"ajoutees_regle={len(tuiles_ajoutees_par_la_regle_de_domaine)}"
    )
    print(f"emprise: lon=[{lon_min}, {lon_max}] lat=[{lat_min}, {lat_max}]")
    print(f"artefact: {OUTPUT}")
    tile_resolver.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
