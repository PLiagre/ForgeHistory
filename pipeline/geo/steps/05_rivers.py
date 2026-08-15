"""G5 — fleuves navigables, arêtes enrichies, embouchures (v1_051 / v1_060).

Ce que fait ce module, en une phrase : il lit les tronçons Natural Earth dans
la fenêtre pilote, les classe en trois navigabilités dérivées de scalerank,
les rattache aux cellules qu'ils traversent, enrichit une *copie* des arêtes
terre-terre de G4 (artère / croisement / mixte), et dérive les embouchures
qui débouchent sur une zone de mer adjacente.

Entrées (lecture seule) : cells_g3.json, adjacency_g4.json, sea_zones_g4.json,
ne_10m_rivers_lake_centerlines (10m_physical.zip), terre/mer 1400 via
steps/02b_corrections_1400.py (chargé dynamiquement).

Sorties : artifacts/rivers_g5.json, adjacency_g5.json, mouths_g5.json,
stats_g5.json, MANIFEST_g5.json, registry/river_registry.json, deux captures.

Usage :
  ../../.venv/bin/python pipeline.py --source rivers
  ../../.venv/bin/python tests/run_proof_g5.py
"""

from __future__ import annotations

import argparse
import importlib.util
import shutil
import sys
import time
import zipfile
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

import geopandas as gpd
from shapely.geometry import Point, box, mapping, shape
from shapely.strtree import STRtree

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    FLOAT_DECIMALS,
    G5_INTERSECT_EPS_M,
    G5_MOUTH_SNAP_M,
    G5_NAMED_MAJOR_RIVERS,
    G5_NAV_SCALE_NAVIGABLE_MAX,
    G5_NAV_SCALE_NON_NAV_MIN,
    G5_PIPELINE_VERSION,
    G5_REGISTRY_CREATED,
    G5_RIVER_LAYER,
    G5_SEA_ONLY_FRACTION,
    PILOT_WINDOW_LONLAT,
    SOURCE_CRS,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402
from projection import (  # noqa: E402
    Projector,
    crs_declaration,
    detect_projection,
    project_shapely_ll_to_xy,
    unproject_shapely_xy_to_ll,
)

BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
SOURCES = ROOT / "sources"
LOCK_PATH = ROOT / "sources.lock"

SOURCE_ARCHIVE = "10m_physical.zip"
REGISTRY_PATH = REGISTRY / "river_registry.json"
LAYERS_BUILD = BUILD / "ne_layers_g5"

# Densification du bord de fenêtre (même rôle que G4) — pas une borne qualité.
WINDOW_EDGE_STEP_DEG = 0.02


def _load_module(name: str, relpath: str):
    path = ROOT / relpath
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _as_polygons(geom: Any) -> List[Any]:
    if geom is None or geom.is_empty:
        return []
    if geom.geom_type == "Polygon":
        return [geom]
    if geom.geom_type in ("MultiPolygon", "GeometryCollection"):
        out: List[Any] = []
        for g in geom.geoms:
            out.extend(_as_polygons(g))
        return out
    return []


def _normalize(geom: Any) -> Any:
    if geom is None or geom.is_empty:
        return geom
    if not geom.is_valid:
        geom = geom.buffer(0)
    polys = _as_polygons(geom)
    if not polys:
        return geom
    if len(polys) == 1:
        return polys[0]
    from shapely.geometry import MultiPolygon

    return MultiPolygon(
        sorted(polys, key=lambda p: (round(p.centroid.x, 3), round(p.centroid.y, 3)))
    )


def _project_geom(geom: Any, projector: Projector) -> Any:
    return project_shapely_ll_to_xy(geom, projector)


def verify_source_fingerprint() -> Dict[str, Any]:
    if not LOCK_PATH.exists():
        raise FileNotFoundError(f"sources.lock absent ({LOCK_PATH})")
    lock = read_json(LOCK_PATH)
    files = lock.get("files") or {}
    if SOURCE_ARCHIVE not in files:
        raise RuntimeError(f"{SOURCE_ARCHIVE} absent de sources.lock")
    expected = files[SOURCE_ARCHIVE]["sha256"]
    archive = SOURCES / SOURCE_ARCHIVE
    if not archive.exists():
        raise FileNotFoundError(f"Archive manquante : {archive}")
    actual = sha256_file(archive)
    if actual != expected:
        raise RuntimeError(
            f"EMPREINTE DIVERGENTE pour {SOURCE_ARCHIVE}.\n"
            f"  attendu : {expected}\n  calculé : {actual}"
        )
    layers = files[SOURCE_ARCHIVE].get("layers") or []
    if G5_RIVER_LAYER not in layers:
        raise RuntimeError(
            f"{G5_RIVER_LAYER} absente de sources.lock "
            f"(layers de {SOURCE_ARCHIVE})"
        )
    return {
        "archive": SOURCE_ARCHIVE,
        "sha256": actual,
        "bytes": files[SOURCE_ARCHIVE].get("bytes"),
        "licence": lock.get("licence", {}),
    }


def extract_river_layer(archive: Path, dest: Path) -> Path:
    """Extrait uniquement la couche fleuves vers un répertoire de build."""
    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True, exist_ok=True)
    shp: Optional[Path] = None
    with zipfile.ZipFile(archive) as zf:
        for name in sorted(zf.namelist()):
            if Path(name).stem != G5_RIVER_LAYER:
                continue
            target = dest / Path(name).name
            with zf.open(name) as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)
            if target.suffix.lower() == ".shp":
                shp = target
    if shp is None:
        raise RuntimeError(f"Couche manquante dans l'archive : {G5_RIVER_LAYER}")
    return shp


def _clip_rivers(gdf: gpd.GeoDataFrame, window) -> gpd.GeoDataFrame:
    if gdf.crs is not None and str(gdf.crs).upper() not in ("EPSG:4326", SOURCE_CRS):
        gdf = gdf.to_crs(SOURCE_CRS)
    clipped = gpd.clip(gdf, window)
    if len(clipped) == 0:
        return clipped
    clipped = clipped.copy()
    import warnings

    with warnings.catch_warnings():
        warnings.simplefilter("ignore", UserWarning)
        clipped["_sx"] = clipped.geometry.centroid.x
        clipped["_sy"] = clipped.geometry.centroid.y
    clipped = clipped.sort_values(by=["_sx", "_sy", "ne_id"]).drop(
        columns=["_sx", "_sy"]
    )
    return clipped.reset_index(drop=True)


def navigability_of(scalerank: Any) -> str:
    """Trois classes D2 — bornes lues de constants.py, jamais en dur."""
    rank = int(scalerank)
    if rank <= G5_NAV_SCALE_NAVIGABLE_MAX:
        return "navigable"
    if rank >= G5_NAV_SCALE_NON_NAV_MIN:
        return "non_navigable"
    return "indeterminate"


def _line_endpoints(geom: Any) -> List[Point]:
    pts: List[Point] = []
    if geom is None or geom.is_empty:
        return pts
    if geom.geom_type == "LineString":
        coords = list(geom.coords)
        if len(coords) >= 1:
            pts.append(Point(coords[0]))
        if len(coords) >= 2:
            pts.append(Point(coords[-1]))
    elif geom.geom_type == "MultiLineString":
        for part in geom.geoms:
            pts.extend(_line_endpoints(part))
    return pts


def _name_fields(row: Any) -> List[str]:
    out: List[str] = []
    for col in ("name", "name_fr", "name_en", "name_alt", "label"):
        if col not in row.index:
            continue
        val = row[col]
        if val is None:
            continue
        text = str(val).strip()
        if text and text.lower() != "nan":
            out.append(text)
    return out


def named_major_found(segments: Sequence[dict]) -> Dict[str, Any]:
    """Parmi les 9 noms de G5_NAMED_MAJOR_RIVERS, lesquels sont présents."""
    present: Dict[str, List[str]] = {n: [] for n in G5_NAMED_MAJOR_RIVERS}
    for seg in segments:
        fields = [seg.get("name") or ""]
        for alt in seg.get("name_aliases") or []:
            fields.append(alt)
        lowered = {f.strip().lower() for f in fields if f}
        for target in G5_NAMED_MAJOR_RIVERS:
            if target.lower() in lowered:
                present[target].append(seg["segment_id"])
    found = sorted(n for n, ids in present.items() if ids)
    missing = sorted(n for n, ids in present.items() if not ids)
    return {
        "fleuves_nommes_trouves": len(found),
        "fleuves_nommes_attendus": len(G5_NAMED_MAJOR_RIVERS),
        "found_names": found,
        "missing_names": missing,
        "by_name": {n: sorted(set(ids)) for n, ids in present.items()},
    }


def load_context(*, rebuild_land: bool = True) -> Dict[str, Any]:
    """Charge une fois terre/mer 1400, cellules, adjacence G4, zones de mer, fleuves."""
    t0 = time.perf_counter()
    projector = Projector(detect_projection())
    fingerprint = verify_source_fingerprint()

    # Terre / mer comme G4 : via 02b, puis dérivation mer de 04 (lecture seule).
    g2b = _load_module("corrections_g2b", "steps/02b_corrections_1400.py")
    run = g2b.run_corrections(apply_corrections=True, clean_build=False)
    land_ll = run["land_ll"]
    lakes_ll = run["lakes_ll"]
    open_sea_ll = run["open_sea_ll"]
    land_xy = _normalize(_project_geom(land_ll, projector))
    if not land_xy.is_valid:
        land_xy = land_xy.buffer(0)

    adj_mod = _load_module("adjacency_g4", "steps/04_adjacency.py")
    ctx_g4 = {
        "projector": projector,
        "land_ll": land_ll,
        "land_xy": land_xy,
        "lakes_ll": lakes_ll,
        "open_sea_ll": open_sea_ll,
        "cell_geoms": [],
        "cell_ids": [],
    }
    # derive_sea n'a besoin que de land/lakes/open_sea + projector.
    sea_pack = adj_mod.derive_sea(
        {
            "projector": projector,
            "land_ll": land_ll,
            "lakes_ll": lakes_ll,
            "open_sea_ll": open_sea_ll,
        }
    )
    sea_xy = sea_pack["sea_geom"]

    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    cells = sorted(cells_doc["cells"], key=lambda c: int(c["cell_id"]))
    cell_geoms = {int(c["cell_id"]): shape(c["geometry"]) for c in cells}

    adj_doc = read_json(ARTIFACTS / "adjacency_g4.json")
    adjacency_g4 = list(adj_doc["adjacency"])

    zones_doc = read_json(ARTIFACTS / "sea_zones_g4.json")
    sea_zones = sorted(zones_doc["sea_zones"], key=lambda z: int(z["zone_id"]))
    zone_geoms = {int(z["zone_id"]): shape(z["geometry"]) for z in sea_zones}

    shp = extract_river_layer(SOURCES / SOURCE_ARCHIVE, LAYERS_BUILD)
    west, south, east, north = PILOT_WINDOW_LONLAT
    window_ll = box(west, south, east, north).segmentize(WINDOW_EDGE_STEP_DEG)
    gdf = gpd.read_file(shp)
    clipped = _clip_rivers(gdf, window_ll)

    return {
        "projector": projector,
        "fingerprint": fingerprint,
        "land_ll": land_ll,
        "land_xy": land_xy,
        "sea_xy": sea_xy,
        "sea_pack": sea_pack,
        "cells": cells,
        "cell_geoms": cell_geoms,
        "adjacency_g4": adjacency_g4,
        "adjacency_g4_meta": {
            k: adj_doc[k]
            for k in adj_doc
            if k != "adjacency"
        },
        "sea_zones": sea_zones,
        "zone_geoms": zone_geoms,
        "rivers_gdf": clipped,
        "window_ll": window_ll,
        "layer_shp": str(shp),
        "elapsed_s": time.perf_counter() - t0,
    }


def build_segments(context: Dict[str, Any]) -> List[dict]:
    """Tronçons projetés + navigabilité D2 + métadonnées source."""
    projector = context["projector"]
    gdf = context["rivers_gdf"]
    segments: List[dict] = []
    for _, row in gdf.iterrows():
        geom_ll = row.geometry
        if geom_ll is None or geom_ll.is_empty:
            continue
        geom_xy = _project_geom(geom_ll, projector)
        if geom_xy is None or geom_xy.is_empty:
            continue
        if geom_xy.geom_type not in ("LineString", "MultiLineString"):
            # Après clip, un éclat ponctuel est possible — on l'ignore.
            continue
        ne_id = int(row["ne_id"])
        scalerank = int(row["scalerank"])
        featurecla = str(row["featurecla"])
        name_raw = row["name"]
        name = None if name_raw is None or str(name_raw).lower() == "nan" else str(name_raw)
        aliases = _name_fields(row)
        segments.append(
            {
                "segment_id": f"ne_{ne_id}",
                "ne_id": ne_id,
                "name": name,
                "name_aliases": sorted(set(aliases)),
                "featurecla": featurecla,
                "scalerank": scalerank,
                "navigability": navigability_of(scalerank),
                "geometry": mapping(geom_xy),
                "_geom": geom_xy,
            }
        )
    segments.sort(key=lambda s: (s["ne_id"], s["segment_id"]))
    return segments


def attach_to_cells(
    segments: Sequence[dict],
    cell_geoms: Dict[int, Any],
    eps: float,
) -> Dict[str, List[int]]:
    """D4 — rattachement : cellules réellement traversées (longueur ≥ eps)."""
    cell_ids = sorted(cell_geoms.keys())
    geoms = [cell_geoms[cid] for cid in cell_ids]
    tree = STRtree(geoms)
    attachments: Dict[str, List[int]] = {}
    for seg in segments:
        line = seg["_geom"]
        hits: List[int] = []
        for idx in tree.query(line):
            cid = cell_ids[int(idx)]
            cell = cell_geoms[cid]
            inter = cell.intersection(line)
            length = float(getattr(inter, "length", 0.0) or 0.0) if not inter.is_empty else 0.0
            if length >= eps:
                hits.append(cid)
            elif not inter.is_empty and cell.buffer(eps).intersection(line).length >= eps:
                hits.append(cid)
        attachments[seg["segment_id"]] = sorted(set(hits))
    return attachments


def enrich_adjacency(
    adjacency_g4: Sequence[dict],
    segments: Sequence[dict],
    cell_geoms: Dict[int, Any],
    eps: float,
) -> Tuple[List[dict], Dict[str, int]]:
    """D3 — copie enrichie : artery / crossing / both sur land-land touchées."""
    seg_by_id = {s["segment_id"]: s for s in segments}
    seg_geoms = [(s["segment_id"], s["_geom"]) for s in segments]
    tree = STRtree([g for _, g in seg_geoms])
    id_at = [sid for sid, _ in seg_geoms]

    artery = 0
    crossing = 0
    both = 0
    touched = 0
    out: List[dict] = []

    for edge in adjacency_g4:
        e = dict(edge)
        if e.get("kind") != "land-land":
            out.append(e)
            continue
        a = int(e["a"])
        b = int(e["b"])
        ga = cell_geoms.get(a)
        gb = cell_geoms.get(b)
        if ga is None or gb is None:
            out.append(e)
            continue
        shared = ga.intersection(gb)
        if shared.is_empty:
            out.append(e)
            continue
        # Tronçons à distance ≤ eps de la frontière partagée.
        candidate_idx = tree.query(shared.buffer(eps))
        touching_ids: List[str] = []
        for idx in candidate_idx:
            sid = id_at[int(idx)]
            geom = seg_by_id[sid]["_geom"]
            if geom.distance(shared) <= eps:
                touching_ids.append(sid)
        touching_ids = sorted(set(touching_ids))
        if not touching_ids:
            out.append(e)
            continue

        touched += 1
        navs = {seg_by_id[sid]["navigability"] for sid in touching_ids}
        has_nav = "navigable" in navs
        has_other = bool(navs - {"navigable"})
        river_docs = [
            {
                "segment_id": sid,
                "navigability": seg_by_id[sid]["navigability"],
                "name": seg_by_id[sid].get("name"),
            }
            for sid in touching_ids
        ]
        if has_nav and not has_other:
            e["fluvial_artery"] = True
            e["artery_rivers"] = river_docs
            e["fluvial_class"] = "artery"
            artery += 1
        elif has_nav and has_other:
            e["fluvial_artery"] = True
            e["artery_rivers"] = river_docs
            e["fluvial_class"] = "both"
            both += 1
        else:
            # crossing : aucun navigable — pas d'artère
            e["fluvial_artery"] = False
            e["fluvial_class"] = "crossing"
            crossing += 1
        out.append(e)

    out.sort(key=lambda x: (x["kind"], int(x["a"]), int(x["b"])))
    counts = {
        "artery_count": artery,
        "crossing_count": crossing,
        "both_count": both,
        "aretes_terre_terre_avec_fleuve": touched,
    }
    return out, counts


def derive_mouths(
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    adjacency_g4: Sequence[dict],
    sea_zones: Sequence[dict],
    zone_geoms: Dict[int, Any],
    sea_xy: Any,
    snap_m: float,
) -> List[dict]:
    """D6 — embouchures aval près de la mer, zone adjacente aux cellules du fleuve."""
    # cell_id → zones de mer adjacentes (arêtes land-sea)
    cell_to_zones: Dict[int, List[int]] = {}
    zone_ids = set(zone_geoms.keys())
    for edge in adjacency_g4:
        if edge.get("kind") != "land-sea":
            continue
        a, b = int(edge["a"]), int(edge["b"])
        if a in zone_ids and b not in zone_ids:
            cell_to_zones.setdefault(b, []).append(a)
        elif b in zone_ids and a not in zone_ids:
            cell_to_zones.setdefault(a, []).append(b)

    mouths: List[dict] = []
    for seg in segments:
        if seg.get("featurecla") == "Lake Centerline":
            continue
        line = seg["_geom"]
        endpoints = _line_endpoints(line)
        near = [p for p in endpoints if float(sea_xy.distance(p)) <= snap_m]
        if not near:
            continue
        # Point terminal aval : le plus proche de la mer parmi les candidats.
        pt = min(near, key=lambda p: float(sea_xy.distance(p)))
        cells = attachments.get(seg["segment_id"], [])
        adj_zone_ids = sorted(
            {
                zid
                for cid in cells
                for zid in cell_to_zones.get(cid, [])
            }
        )
        eligible: List[Tuple[float, int]] = []
        for zid in adj_zone_ids:
            zg = zone_geoms[zid]
            dist = float(zg.distance(pt))
            if dist <= snap_m:
                eligible.append((dist, zid))
        if not eligible:
            continue
        eligible.sort()
        zone_id = eligible[0][1]
        zone_name = next(
            (z.get("name") for z in sea_zones if int(z["zone_id"]) == zone_id),
            None,
        )
        mouths.append(
            {
                "segment_id": seg["segment_id"],
                "name": seg.get("name"),
                "sea_zone_id": zone_id,
                "sea_zone_name": zone_name,
                "sea_zone_adjacent_to_river_cells": True,
                "distance_to_sea_m": round(float(sea_xy.distance(pt)), FLOAT_DECIMALS),
                "geometry": mapping(pt),
            }
        )
    mouths.sort(key=lambda m: (m["segment_id"], m["sea_zone_id"]))
    return mouths


def compute_metrics(
    segments: Sequence[dict],
    edge_counts: Dict[str, int],
    mouths: Sequence[dict],
    named: Dict[str, Any],
) -> Dict[str, Any]:
    nav_counts = {"navigable": 0, "indeterminate": 0, "non_navigable": 0}
    for seg in segments:
        nav_counts[seg["navigability"]] = nav_counts.get(seg["navigability"], 0) + 1
    land_land_total = -1  # renseigné par l'appelant si besoin
    return {
        "pipeline_version": G5_PIPELINE_VERSION,
        "segment_count": len(segments),
        "navigability_counts": nav_counts,
        "artery_count": edge_counts["artery_count"],
        "crossing_count": edge_counts["crossing_count"],
        "both_count": edge_counts["both_count"],
        "aretes_terre_terre_avec_fleuve": edge_counts["aretes_terre_terre_avec_fleuve"],
        "mouth_count": len(mouths),
        "fleuves_nommes_trouves": named["fleuves_nommes_trouves"],
        "fleuves_nommes_attendus": named["fleuves_nommes_attendus"],
        "found_names": named["found_names"],
        "missing_names": named["missing_names"],
        "nav_scale_navigable_max": G5_NAV_SCALE_NAVIGABLE_MAX,
        "nav_scale_non_nav_min": G5_NAV_SCALE_NON_NAV_MIN,
        "intersect_eps_m": G5_INTERSECT_EPS_M,
        "sea_only_fraction": G5_SEA_ONLY_FRACTION,
        "mouth_snap_m": G5_MOUTH_SNAP_M,
        "river_layer": G5_RIVER_LAYER,
    }


def export_g5(
    *,
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    adjacency: Sequence[dict],
    adjacency_meta: Dict[str, Any],
    mouths: Sequence[dict],
    metrics: Dict[str, Any],
    context: Dict[str, Any],
) -> Dict[str, str]:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    REGISTRY.mkdir(parents=True, exist_ok=True)
    projector = context["projector"]
    shas: Dict[str, str] = {}

    rivers_out = {
        "pipeline_version": G5_PIPELINE_VERSION,
        "data_class": "natural_earth_g5_rivers",
        "comment": (
            "Tronçons G5 — ne_10m_rivers_lake_centerlines dans la fenêtre pilote. "
            "navigability dérivée de scalerank (proxy cartographique, pas un débit)."
        ),
        "projection": projector.info.epsg,
        "crs": crs_declaration(geometry_crs=projector.info.epsg, has_geometry_lonlat=False),
        "segments": [
            {
                "segment_id": s["segment_id"],
                "ne_id": s["ne_id"],
                "name": s.get("name"),
                "name_aliases": s.get("name_aliases") or [],
                "featurecla": s["featurecla"],
                "scalerank": s["scalerank"],
                "navigability": s["navigability"],
                "attachments": attachments.get(s["segment_id"], []),
                "geometry": s["geometry"],
            }
            for s in segments
        ],
    }
    shas["artifacts/rivers_g5.json"] = write_json(ARTIFACTS / "rivers_g5.json", rivers_out)

    adj_out = {
        **adjacency_meta,
        "pipeline_version": G5_PIPELINE_VERSION,
        "data_class": "natural_earth_g5_adjacency",
        "comment": (
            "Copie enrichie de adjacency_g4.json — arêtes land-land touchées par "
            "un fleuve portent fluvial_artery / artery_rivers / fluvial_class "
            "(artery|crossing|both). adjacency_g4.json n'est pas modifié."
        ),
        "source_adjacency": "artifacts/adjacency_g4.json",
        "adjacency": list(adjacency),
    }
    shas["artifacts/adjacency_g5.json"] = write_json(
        ARTIFACTS / "adjacency_g5.json", adj_out
    )

    mouths_out = {
        "pipeline_version": G5_PIPELINE_VERSION,
        "data_class": "natural_earth_g5_mouths",
        "comment": (
            "Embouchures G5 — point terminal aval d'un tronçon non-lac à moins de "
            "G5_MOUTH_SNAP_M de la mer, sur une zone maritime adjacente aux cellules."
        ),
        "projection": projector.info.epsg,
        "crs": crs_declaration(geometry_crs=projector.info.epsg, has_geometry_lonlat=False),
        "mouths": list(mouths),
    }
    shas["artifacts/mouths_g5.json"] = write_json(ARTIFACTS / "mouths_g5.json", mouths_out)

    shas["artifacts/stats_g5.json"] = write_json(ARTIFACTS / "stats_g5.json", metrics)

    registry = {
        "pipeline_version": G5_PIPELINE_VERSION,
        "created": G5_REGISTRY_CREATED,
        "data_class": "natural_earth_g5_river_registry",
        "segment_count": len(segments),
        "segments": [
            {
                "segment_id": s["segment_id"],
                "ne_id": s["ne_id"],
                "name": s.get("name"),
                "navigability": s["navigability"],
                "scalerank": s["scalerank"],
                "featurecla": s["featurecla"],
            }
            for s in segments
        ],
    }
    shas["registry/river_registry.json"] = write_json(REGISTRY_PATH, registry)

    manifest = {
        "pipeline_version": G5_PIPELINE_VERSION,
        "projection": projector.info.epsg,
        "projection_fallback": projector.info.fallback,
        "inputs": {
            "10m_physical.zip": context["fingerprint"]["sha256"],
            "adjacency_g4.json": sha256_file(ARTIFACTS / "adjacency_g4.json"),
            "sea_zones_g4.json": sha256_file(ARTIFACTS / "sea_zones_g4.json"),
            "cells_g3.json": sha256_file(ARTIFACTS / "cells_g3.json"),
            "river_layer": G5_RIVER_LAYER,
        },
        "outputs": {
            "rivers_g5.json": shas["artifacts/rivers_g5.json"],
            "adjacency_g5.json": shas["artifacts/adjacency_g5.json"],
            "mouths_g5.json": shas["artifacts/mouths_g5.json"],
            "stats_g5.json": shas["artifacts/stats_g5.json"],
            "river_registry.json": shas["registry/river_registry.json"],
        },
    }
    shas["artifacts/MANIFEST_g5.json"] = write_json(
        ARTIFACTS / "MANIFEST_g5.json", manifest
    )
    return shas


def write_captures(
    context: Dict[str, Any],
    segments: Sequence[dict],
    adjacency: Sequence[dict],
) -> Dict[str, Path]:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Polygon as MplPolygon

    CAPTURE.mkdir(parents=True, exist_ok=True)
    projector = context["projector"]
    land_ll = context["land_ll"]
    paths: Dict[str, Path] = {}

    nav_color = {
        "navigable": "#1565c0",
        "indeterminate": "#f9a825",
        "non_navigable": "#6d4c41",
    }
    class_color = {
        "artery": "#c62828",
        "crossing": "#6a1b9a",
        "both": "#00838f",
    }

    def draw_land(ax):
        for poly in _as_polygons(land_ll):
            ax.add_patch(
                MplPolygon(
                    list(zip(*poly.exterior.xy)),
                    closed=True,
                    facecolor="#dcedc8",
                    edgecolor="#33691e",
                    linewidth=0.25,
                )
            )

    def line_ll(geom_xy):
        return unproject_shapely_xy_to_ll(geom_xy, projector)

    # --- fenêtre pilote : tronçons classés ---
    fig, ax = plt.subplots(figsize=(12, 10), dpi=120)
    ax.set_aspect("equal")
    ax.set_facecolor("#e3f2fd")
    ax.set_title("G5 — fleuves (navigable / indéterminé / non navigable)")
    w, s, e, n = PILOT_WINDOW_LONLAT
    ax.set_xlim(w, e)
    ax.set_ylim(s, n)
    draw_land(ax)
    for seg in segments:
        gll = line_ll(seg["_geom"])
        color = nav_color[seg["navigability"]]
        lw = 1.4 if seg["navigability"] == "navigable" else 0.7
        if gll.geom_type == "LineString":
            xs, ys = gll.xy
            ax.plot(xs, ys, color=color, linewidth=lw, solid_capstyle="round")
        elif gll.geom_type == "MultiLineString":
            for part in gll.geoms:
                xs, ys = part.xy
                ax.plot(xs, ys, color=color, linewidth=lw, solid_capstyle="round")
    ax.grid(True, alpha=0.2)
    path = CAPTURE / "v1_060_rivers_window.png"
    fig.savefig(path, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["rivers_window"] = path

    # --- zoom artère / croisement / mixte (bassin parisien / Manche) ---
    fig, ax = plt.subplots(figsize=(10, 8), dpi=120)
    ax.set_aspect("equal")
    ax.set_facecolor("#e3f2fd")
    ax.set_title("G5 — arêtes enrichies (artery / crossing / both)")
    # Secteur fixe et déterministe : nord de la France / bas Rhin.
    ax.set_xlim(-2.0, 8.0)
    ax.set_ylim(48.0, 53.5)
    draw_land(ax)
    cell_geoms = context["cell_geoms"]
    for edge in adjacency:
        cls = edge.get("fluvial_class")
        if not cls:
            continue
        a, b = int(edge["a"]), int(edge["b"])
        ga, gb = cell_geoms.get(a), cell_geoms.get(b)
        if ga is None or gb is None:
            continue
        ca = ga.centroid
        cb = gb.centroid
        lon1, lat1 = projector.unproject_xy(ca.x, ca.y)
        lon2, lat2 = projector.unproject_xy(cb.x, cb.y)
        ax.plot(
            [lon1, lon2],
            [lat1, lat2],
            color=class_color[cls],
            linewidth=2.0 if cls == "artery" else 1.2,
            alpha=0.9,
        )
    for seg in segments:
        if seg["navigability"] != "navigable":
            continue
        gll = line_ll(seg["_geom"])
        if gll.geom_type == "LineString":
            xs, ys = gll.xy
            ax.plot(xs, ys, color="#1565c0", linewidth=0.8, alpha=0.7)
        elif gll.geom_type == "MultiLineString":
            for part in gll.geoms:
                xs, ys = part.xy
                ax.plot(xs, ys, color="#1565c0", linewidth=0.8, alpha=0.7)
    ax.grid(True, alpha=0.2)
    path = CAPTURE / "v1_060_artery_crossing_both.png"
    fig.savefig(path, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["artery_crossing_both"] = path
    return paths


def run_rivers(
    *,
    context: Optional[Dict[str, Any]] = None,
    export: bool = True,
    captures: bool = True,
) -> Dict[str, Any]:
    """Dérive fleuves + enrichissement ; exporte les artefacts G5. Sans argument requis."""
    t_all = time.perf_counter()
    timings: Dict[str, float] = {}
    ctx = context or load_context()

    t = time.perf_counter()
    segments = build_segments(ctx)
    timings["segments"] = time.perf_counter() - t

    t = time.perf_counter()
    attachments = attach_to_cells(
        segments, ctx["cell_geoms"], G5_INTERSECT_EPS_M
    )
    for seg in segments:
        seg["attachments"] = attachments.get(seg["segment_id"], [])
    timings["attachments"] = time.perf_counter() - t

    t = time.perf_counter()
    adjacency, edge_counts = enrich_adjacency(
        ctx["adjacency_g4"], segments, ctx["cell_geoms"], G5_INTERSECT_EPS_M
    )
    timings["enrich"] = time.perf_counter() - t

    t = time.perf_counter()
    mouths = derive_mouths(
        segments,
        attachments,
        ctx["adjacency_g4"],
        ctx["sea_zones"],
        ctx["zone_geoms"],
        ctx["sea_xy"],
        G5_MOUTH_SNAP_M,
    )
    timings["mouths"] = time.perf_counter() - t

    named = named_major_found(segments)
    metrics = compute_metrics(segments, edge_counts, mouths, named)
    land_land_total = sum(1 for e in ctx["adjacency_g4"] if e.get("kind") == "land-land")
    metrics["aretes_terre_terre_totales"] = land_land_total

    shas: Dict[str, str] = {}
    if export:
        t = time.perf_counter()
        shas = export_g5(
            segments=segments,
            attachments=attachments,
            adjacency=adjacency,
            adjacency_meta=ctx["adjacency_g4_meta"],
            mouths=mouths,
            metrics=metrics,
            context=ctx,
        )
        timings["export"] = time.perf_counter() - t

    capture_paths: Dict[str, Path] = {}
    if captures:
        t = time.perf_counter()
        capture_paths = write_captures(ctx, segments, adjacency)
        timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t_all
    BUILD.mkdir(parents=True, exist_ok=True)
    write_json(
        BUILD / "99_timings_g5.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )

    return {
        "context": ctx,
        "segments": segments,
        "attachments": attachments,
        "adjacency": adjacency,
        "mouths": mouths,
        "metrics": metrics,
        "named": named,
        "land_xy": ctx["land_xy"],
        "sea_xy": ctx["sea_xy"],
        "projection": ctx["projector"].info,
        "captures": {k: str(v) for k, v in capture_paths.items()},
        "shas": shas,
        "timings": timings,
    }


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="G5 fleuves")
    parser.parse_args(list(argv) if argv is not None else None)
    result = run_rivers()
    m = result["metrics"]
    print(
        f"pipeline g5 | projection={result['projection'].epsg} | "
        f"segments={m['segment_count']} nav={m['navigability_counts']} | "
        f"artery={m['artery_count']} crossing={m['crossing_count']} "
        f"both={m['both_count']} | mouths={m['mouth_count']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
