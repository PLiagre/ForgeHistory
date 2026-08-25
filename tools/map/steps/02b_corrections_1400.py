"""G2-bis — Corrections littoral 1400 (v1_047).

Couche de corrections DÉCLARATIVE, séparée de Natural Earth.
Opération unique : reclasser une entité nommée (lac → mer ouverte).
Zéro géométrie inventée. Réversible par drapeau.

Usage :
  .venv/Scripts/python.exe steps/02b_corrections_1400.py
  .venv/Scripts/python.exe tests/run_proof_g2b.py
"""

from __future__ import annotations

import argparse
import importlib.util
import sys
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Set, Tuple

import geopandas as gpd
from shapely.geometry import MultiPolygon, mapping
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    FLOAT_DECIMALS,
    G2B_PIPELINE_VERSION,
    G2_LAND_AREA_KM2_MAX,
    G2_LAND_AREA_KM2_MIN,
    PILOT_WINDOW_JUSTIFICATION,
    PILOT_WINDOW_LONLAT,
    PIPELINE_VERSION,
    SOURCE_CRS,
)
from io_util import read_json, write_json  # noqa: E402
from projection import Projector, crs_declaration, detect_projection  # noqa: E402

DATA = ROOT / "data"
CORRECTIONS_PATH = DATA / "corrections_1400.json"
DIVERGENCES_PATH = DATA / "divergences_1400.json"
BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"

VALID_CERTAINTY = ("attested", "reconstructed", "reconstructed_established", "gameplay")

# Emprise capture côte néerlandaise / mer du Nord (choix de visualisation).
NL_FOCUS_LONLAT: Tuple[float, float, float, float] = (3.5, 51.5, 7.5, 54.0)


def _load_coastline():
    path = ROOT / "steps" / "02_coastline.py"
    spec = importlib.util.spec_from_file_location("coastline_g2", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def load_corrections(path: Path = CORRECTIONS_PATH) -> Dict[str, Any]:
    doc = read_json(path)
    if "corrections" not in doc:
        raise ValueError(f"{path} : clé 'corrections' manquante")
    return doc


def load_divergences(path: Path = DIVERGENCES_PATH) -> Dict[str, Any]:
    doc = read_json(path)
    if "divergences" not in doc:
        raise ValueError(f"{path} : clé 'divergences' manquante")
    return doc


def validate_corrections_doc(doc: Dict[str, Any]) -> List[str]:
    """Retourne la liste des erreurs (vide = OK). Contrôle G2b-A.

    Opérations acceptées : reclassify (G2-bis), declare_topology_link (G4),
    declare_navigability (G5-bis).
    """
    errors: List[str] = []
    levels = set(doc.get("valid_certainty_levels") or VALID_CERTAINTY)
    valid_ops = set(
        doc.get("valid_operations")
        or ("reclassify", "declare_topology_link", "declare_navigability")
    )
    for corr in doc.get("corrections") or []:
        cid = corr.get("id") or "?"
        certainty = corr.get("certainty")
        source = corr.get("source")
        provenance = corr.get("provenance")
        op = corr.get("operation")
        if certainty not in levels:
            errors.append(f"{cid}:certainty_invalid={certainty!r}")
        # provenance (G5-bis) ou source (G2/G4) — au moins un non vide.
        prov_ok = (
            (source is not None and str(source).strip() != "")
            or (provenance is not None and str(provenance).strip() != "")
        )
        if not prov_ok:
            errors.append(f"{cid}:source_empty")
        if not corr.get("date"):
            errors.append(f"{cid}:date_missing")
        if op not in valid_ops:
            errors.append(f"{cid}:operation_invalid={op!r}")
            continue
        if op == "reclassify":
            target = corr.get("target") or {}
            if not target.get("layer") or not target.get("name"):
                errors.append(f"{cid}:target_incomplete")
        elif op == "declare_topology_link":
            fw = corr.get("from_water") or {}
            tw = corr.get("to_water") or {}
            if not fw.get("name"):
                errors.append(f"{cid}:from_water_name_missing")
            if not tw.get("name"):
                errors.append(f"{cid}:to_water_name_missing")
            if not corr.get("link_kind"):
                errors.append(f"{cid}:link_kind_missing")
        elif op == "declare_navigability":
            if not corr.get("river_name"):
                errors.append(f"{cid}:river_name_missing")
            if not corr.get("imposed_navigability"):
                errors.append(f"{cid}:imposed_navigability_missing")
            if provenance is None or str(provenance).strip() == "":
                errors.append(f"{cid}:provenance_empty")
    return errors


def topology_link_corrections(doc: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Liens topologiques déclarés (G4) — triés, déterministes."""
    items = [
        c
        for c in (doc.get("corrections") or [])
        if c.get("operation") == "declare_topology_link"
    ]
    return sorted(items, key=lambda c: str(c.get("id") or ""))


def navigability_corrections(doc: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Surcharges de navigabilité déclarées (G5-bis) — triées, déterministes."""
    items = [
        c
        for c in (doc.get("corrections") or [])
        if c.get("operation") == "declare_navigability"
    ]
    return sorted(items, key=lambda c: str(c.get("id") or ""))


def reclassify_targets(doc: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Liste ordonnée (tri stable) des corrections de reclassement lac→mer."""
    items = [
        c
        for c in (doc.get("corrections") or [])
        if c.get("operation") == "reclassify" and c.get("to_class") == "open_sea"
    ]
    return sorted(items, key=lambda c: str(c.get("id") or ""))


def _collect_coords(geom: Any, out: Set[Tuple[float, float]]) -> None:
    if geom is None or geom.is_empty:
        return
    if geom.geom_type == "Polygon":
        for x, y in geom.exterior.coords:
            out.add((float(x), float(y)))
        for ring in geom.interiors:
            for x, y in ring.coords:
                out.add((float(x), float(y)))
    elif geom.geom_type == "MultiPolygon":
        for g in geom.geoms:
            _collect_coords(g, out)
    elif hasattr(geom, "geoms"):
        for g in geom.geoms:
            _collect_coords(g, out)


def source_vertex_set(layer_paths: Dict[str, Path], window) -> Set[Tuple[float, float]]:
    """Sommets des couches source découpées à la fenêtre (référence G2b-C)."""
    coast = _load_coastline()
    verts: Set[Tuple[float, float]] = set()
    for layer in ("ne_10m_land", "ne_10m_minor_islands", "ne_10m_lakes"):
        gdf = coast._clip_gdf(gpd.read_file(layer_paths[layer]), window)
        for geom in gdf.geometry:
            _collect_coords(geom, verts)
    return verts


def geometry_vertices_subset(
    geom: Any, allowed: Set[Tuple[float, float]], tol: float = 1e-9
) -> Tuple[bool, int]:
    """True si tous les sommets de geom appartiennent à allowed (tolérance)."""
    found: Set[Tuple[float, float]] = set()
    _collect_coords(geom, found)
    # Index grossier pour tolérance.
    ok = 0
    bad = 0
    for x, y in found:
        if (x, y) in allowed:
            ok += 1
            continue
        # Tolérance : chercher voisin (rare ; G2 difference peut snaper).
        hit = False
        for ax, ay in allowed:
            if abs(ax - x) <= tol and abs(ay - y) <= tol:
                hit = True
                break
        if hit:
            ok += 1
        else:
            bad += 1
            if bad > 5:
                break
    return bad == 0, bad


def build_land_with_reclass(
    coastline_mod: Any,
    layer_paths: Dict[str, Path],
    projector: Projector,
    corrections_doc: Dict[str, Any],
    *,
    apply_corrections: bool,
) -> Dict[str, Any]:
    """Construit la terre en réutilisant G2, avec reclassement optionnel.

    Reclasser lac→open_sea : l'entité quitte la classe lacustre et reste de la
    MER (toujours soustraite de la terre). Aucun sommet inventé — mêmes polygones
    source, seule la classe change. Les lacs restants restent des trous lacustres.
    """
    targets = reclassify_targets(corrections_doc) if apply_corrections else []
    reclass_names = sorted({str(c["target"]["name"]) for c in targets})

    window = coastline_mod.pilot_window_polygon()
    land = coastline_mod._clip_gdf(gpd.read_file(layer_paths["ne_10m_land"]), window)
    islands = coastline_mod._clip_gdf(
        gpd.read_file(layer_paths["ne_10m_minor_islands"]), window
    )
    lakes = coastline_mod._clip_gdf(gpd.read_file(layer_paths["ne_10m_lakes"]), window)
    coastline = coastline_mod._clip_gdf(
        gpd.read_file(layer_paths["ne_10m_coastline"]), window
    )

    # Séparer lacs restants / mer ouverte (reclassés).
    if apply_corrections and reclass_names and "name" in lakes.columns:
        mask = lakes["name"].isin(reclass_names)
        open_sea_gdf = lakes[mask].copy()
        lakes_remain = lakes[~mask].copy()
    else:
        open_sea_gdf = lakes.iloc[0:0].copy()
        lakes_remain = lakes

    land_geoms = coastline_mod._sort_geoms(
        [g for g in land.geometry if g is not None and not g.is_empty]
    )
    island_geoms = coastline_mod._sort_geoms(
        [g for g in islands.geometry if g is not None and not g.is_empty]
    )
    lake_geoms = coastline_mod._sort_geoms(
        [g for g in lakes_remain.geometry if g is not None and not g.is_empty]
    )
    open_sea_geoms = coastline_mod._sort_geoms(
        [g for g in open_sea_gdf.geometry if g is not None and not g.is_empty]
    )

    raw_union = coastline_mod._as_multipolygon(unary_union(land_geoms + island_geoms))
    parts_ll = list(raw_union.geoms) if raw_union.geom_type == "MultiPolygon" else [raw_union]
    parts_ll = coastline_mod._sort_geoms(parts_ll)

    part_records: List[Dict[str, Any]] = []
    for part in parts_ll:
        part_xy = coastline_mod._project_geom_ll_to_xy(part, projector)
        area_km2 = coastline_mod._area_km2(part_xy)
        lon, lat = float(part.centroid.x), float(part.centroid.y)
        part_records.append(
            {
                "area_km2": round(area_km2, 6),
                "centroid_lon": round(lon, FLOAT_DECIMALS),
                "centroid_lat": round(lat, FLOAT_DECIMALS),
                "geom_ll": part,
                "name": coastline_mod._name_island(lon, lat, area_km2),
            }
        )
    part_records = sorted(
        part_records,
        key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"]),
    )

    threshold_info = coastline_mod.derive_island_threshold_km2(
        [r["area_km2"] for r in part_records]
    )
    threshold = float(threshold_info["threshold_km2"])
    kept = [r for r in part_records if r["area_km2"] >= threshold]
    dropped = [r for r in part_records if r["area_km2"] < threshold]
    kept = sorted(kept, key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"]))
    dropped = sorted(
        dropped, key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"])
    )

    kept_union = (
        coastline_mod._as_multipolygon(unary_union([r["geom_ll"] for r in kept]))
        if kept
        else MultiPolygon()
    )

    # Eau soustraite : lacs restants + mer ouverte reclassée (mêmes polygones source).
    water_geoms = lake_geoms + open_sea_geoms
    water_union = unary_union(water_geoms) if water_geoms else MultiPolygon()
    if not water_union.is_empty and not water_union.is_valid:
        water_union = water_union.buffer(0)

    lakes_union = unary_union(lake_geoms) if lake_geoms else MultiPolygon()
    if not lakes_union.is_empty and not lakes_union.is_valid:
        lakes_union = lakes_union.buffer(0)

    open_sea_union = unary_union(open_sea_geoms) if open_sea_geoms else MultiPolygon()
    if not open_sea_union.is_empty and not open_sea_union.is_valid:
        open_sea_union = open_sea_union.buffer(0)

    land_minus = (
        kept_union.difference(water_union) if not water_union.is_empty else kept_union
    )
    land_ll = coastline_mod._as_multipolygon(land_minus)
    land_xy = coastline_mod._project_geom_ll_to_xy(land_ll, projector)
    if not land_xy.is_valid:
        land_xy = coastline_mod._as_multipolygon(land_xy.buffer(0))

    lake_names: List[str] = []
    if "name" in lakes_remain.columns:
        for nm in lakes_remain["name"].tolist():
            if nm is not None and str(nm) != "nan":
                lake_names.append(str(nm))
    lake_names = sorted(set(lake_names))

    open_sea_names: List[str] = []
    if apply_corrections and "name" in open_sea_gdf.columns:
        for nm in open_sea_gdf["name"].tolist():
            if nm is not None and str(nm) != "nan":
                open_sea_names.append(str(nm))
    open_sea_names = sorted(set(open_sea_names))

    hole_count = sum(
        len(p.interiors)
        for p in (list(land_ll.geoms) if land_ll.geom_type == "MultiPolygon" else [land_ll])
    )

    applied = []
    for corr in targets:
        name = str(corr["target"]["name"])
        applied.append(
            {
                "id": corr["id"],
                "name": name,
                "operation": corr["operation"],
                "from_class": corr.get("from_class"),
                "to_class": corr.get("to_class"),
                "certainty": corr.get("certainty"),
                "source": corr.get("source"),
                "date": corr.get("date"),
                "applied": name in open_sea_names,
            }
        )
    applied = sorted(applied, key=lambda a: a["id"])

    return {
        "land_ll": land_ll,
        "land_xy": land_xy,
        "lakes_ll": (
            coastline_mod._as_multipolygon(lakes_union)
            if not lakes_union.is_empty
            else MultiPolygon()
        ),
        "open_sea_ll": (
            coastline_mod._as_multipolygon(open_sea_union)
            if not open_sea_union.is_empty
            else MultiPolygon()
        ),
        "coastline_count": int(len(coastline)),
        "clip_counts": {
            "land_polygons": int(len(land)),
            "minor_islands": int(len(islands)),
            "lakes": int(len(lakes_remain)),
            "open_sea_reclassified": int(len(open_sea_gdf)),
            "coastline_segments": int(len(coastline)),
        },
        "threshold": threshold_info,
        "islands_kept": [
            {
                "name": r["name"],
                "area_km2": r["area_km2"],
                "centroid_lon": r["centroid_lon"],
                "centroid_lat": r["centroid_lat"],
            }
            for r in kept
        ],
        "islands_dropped": [
            {
                "name": r["name"],
                "area_km2": r["area_km2"],
                "centroid_lon": r["centroid_lon"],
                "centroid_lat": r["centroid_lat"],
            }
            for r in dropped
        ],
        "largest_dropped": [
            {
                "name": r["name"],
                "area_km2": r["area_km2"],
                "centroid_lon": r["centroid_lon"],
                "centroid_lat": r["centroid_lat"],
            }
            for r in dropped[:8]
        ],
        "lake_names": lake_names,
        "open_sea_names": open_sea_names,
        "lakes_subtracted": int(len(lakes_remain)),
        "open_sea_subtracted": int(len(open_sea_gdf)),
        "hole_count": hole_count,
        "land_area_km2": round(coastline_mod._area_km2(land_xy), 3),
        "land_area_m2": round(coastline_mod._area_m2(land_xy), FLOAT_DECIMALS),
        "window": {
            "lonlat": list(PILOT_WINDOW_LONLAT),
            "justification": PILOT_WINDOW_JUSTIFICATION,
            "gameplay_choice": True,
        },
        "corrections_applied": applied,
        "corrections_enabled": apply_corrections,
        "reclass_names": reclass_names,
    }


def write_g2b_outputs(
    result: Dict[str, Any],
    projector: Projector,
    fingerprint: Dict[str, Any],
    source_inspect: Dict[str, Any],
    corrections_doc: Dict[str, Any],
    divergences_doc: Dict[str, Any],
    area_before_km2: float,
) -> Dict[str, str]:
    """Écrit build/ + artifacts/ pour G2-bis. Retourne {relpath: sha256}."""
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    land_ll = result["land_ll"]
    land_xy = result["land_xy"]
    delta = round(result["land_area_km2"] - area_before_km2, 3)

    meta = {
        "pipeline_version": G2B_PIPELINE_VERSION,
        "g2_pipeline_version": PIPELINE_VERSION,
        "data_class": "natural_earth_g2b_1400",
        "comment": "Littoral G2-bis 1400 — reclassements, PAS de cellules (G3).",
        "source": fingerprint,
        "source_inspect": source_inspect,
        "window": result["window"],
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
            "source_crs": SOURCE_CRS,
            "target_crs": "EPSG:3035",
        },
        "clip_counts": result["clip_counts"],
        "threshold": {
            "threshold_km2": result["threshold"]["threshold_km2"],
            "gap": result["threshold"]["gap"],
            "method": result["threshold"]["method"],
        },
        "islands_kept_count": len(result["islands_kept"]),
        "islands_dropped_count": len(result["islands_dropped"]),
        "largest_dropped": result["largest_dropped"],
        "lakes_subtracted": result["lakes_subtracted"],
        "lake_names": result["lake_names"],
        "open_sea_subtracted": result["open_sea_subtracted"],
        "open_sea_names": result["open_sea_names"],
        "corrections_enabled": result["corrections_enabled"],
        "corrections_applied": result["corrections_applied"],
        "hole_count": result["hole_count"],
        "land_area_km2": result["land_area_km2"],
        "land_area_km2_before_corrections": area_before_km2,
        "land_area_delta_km2": delta,
        "land_area_m2": result["land_area_m2"],
        "geom_type": land_ll.geom_type,
        "part_count": (
            len(land_ll.geoms)
            if land_ll.geom_type == "MultiPolygon"
            else (0 if land_ll.is_empty else 1)
        ),
    }

    shas: Dict[str, str] = {}
    shas["build/02b_corrections_meta.json"] = write_json(
        BUILD / "02b_corrections_meta.json", meta
    )
    shas["build/02b_corrections_1400.json"] = write_json(
        BUILD / "02b_corrections_1400.json",
        {
            "enabled": result["corrections_enabled"],
            "corrections": corrections_doc.get("corrections"),
            "applied": result["corrections_applied"],
        },
    )
    shas["build/02b_divergences_1400.json"] = write_json(
        BUILD / "02b_divergences_1400.json", divergences_doc
    )

    land_geojson = {
        "type": "FeatureCollection",
        "data_class": "natural_earth_g2b_1400",
        "crs": {"type": "name", "properties": {"name": SOURCE_CRS}},
        "features": [
            {
                "type": "Feature",
                "properties": {
                    "role": "land_corrected_1400",
                    "area_km2": result["land_area_km2"],
                    "corrections_enabled": result["corrections_enabled"],
                },
                "geometry": mapping(land_ll),
            }
        ],
    }
    shas["build/02b_land.geojson"] = write_json(BUILD / "02b_land.geojson", land_geojson)

    land_xy_geojson = {
        "type": "FeatureCollection",
        "data_class": "natural_earth_g2b_1400",
        "crs": {"type": "name", "properties": {"name": projector.info.epsg}},
        "features": [
            {
                "type": "Feature",
                "properties": {
                    "role": "land_projected_1400",
                    "area_m2": result["land_area_m2"],
                },
                "geometry": mapping(land_xy),
            }
        ],
    }
    shas["build/02b_land_projected.geojson"] = write_json(
        BUILD / "02b_land_projected.geojson", land_xy_geojson
    )

    # Artefact principal (chemin stable pour déterminisme / réversibilité).
    artifact = {
        "pipeline_version": G2B_PIPELINE_VERSION,
        "g2_pipeline_version": PIPELINE_VERSION,
        "data_class": "natural_earth_g2b_1400",
        "comment": "G2-bis littoral 1400 — aucune cellule.",
        "projection": projector.info.epsg,
        "crs": crs_declaration(geometry_crs=projector.info.epsg, has_geometry_lonlat=True),
        "window": result["window"],
        "land_area_km2": result["land_area_km2"],
        "land_area_km2_before_corrections": area_before_km2,
        "land_area_delta_km2": delta,
        "corrections_enabled": result["corrections_enabled"],
        "corrections_applied": result["corrections_applied"],
        "open_sea_names": result["open_sea_names"],
        "lake_names": result["lake_names"],
        "islands_kept_count": len(result["islands_kept"]),
        "islands_dropped_count": len(result["islands_dropped"]),
        "largest_dropped": result["largest_dropped"],
        "lakes_subtracted": result["lakes_subtracted"],
        "threshold_km2": result["threshold"]["threshold_km2"],
        # Usage = projeté ; lon/lat conservées (plan §3.2 / v1_064).
        "geometry": mapping(land_xy),
        "geometry_lonlat": mapping(land_ll),
    }
    shas["artifacts/coastline_1400.json"] = write_json(
        ARTIFACTS / "coastline_1400.json", artifact
    )
    shas["artifacts/divergences_1400.json"] = write_json(
        ARTIFACTS / "divergences_1400.json", divergences_doc
    )

    # Quand corrections OFF : aussi republier artifacts/coastline.json = G2 bit-stable.
    if not result["corrections_enabled"]:
        g2_artifact = {
            "pipeline_version": PIPELINE_VERSION,
            "data_class": "natural_earth_g2",
            "comment": "G2 littoral réel — aucune cellule.",
            "projection": projector.info.epsg,
            "crs": crs_declaration(geometry_crs=projector.info.epsg, has_geometry_lonlat=True),
            "window": result["window"],
            "land_area_km2": result["land_area_km2"],
            "islands_kept_count": len(result["islands_kept"]),
            "islands_dropped_count": len(result["islands_dropped"]),
            "largest_dropped": result["largest_dropped"],
            "lakes_subtracted": result["lakes_subtracted"] + result["open_sea_subtracted"],
            "threshold_km2": result["threshold"]["threshold_km2"],
            "geometry": mapping(land_xy),
            "geometry_lonlat": mapping(land_ll),
        }
        # lake_names must include reclass names when off (all lakes).
        # Rebuild via G2 path for exact match — handled in run_corrections.

    manifest = {
        "pipeline_version": G2B_PIPELINE_VERSION,
        "g2_pipeline_version": PIPELINE_VERSION,
        "data_class": "natural_earth_g2b_1400",
        "comment": "MANIFEST G2-bis — fixed_timestamp figé ; timings exclus.",
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "corrections_enabled": result["corrections_enabled"],
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
        },
        "inputs": {
            "10m_physical.zip": fingerprint["sha256"],
            "corrections_1400.json": __import__("io_util", fromlist=["sha256_file"]).sha256_file(
                CORRECTIONS_PATH
            ),
        },
        "outputs": {
            k: shas[k] for k in sorted(shas.keys()) if k.startswith("artifacts/")
        },
        "build_outputs": {
            k: shas[k] for k in sorted(shas.keys()) if k.startswith("build/")
        },
    }
    shas["artifacts/MANIFEST_g2b.json"] = write_json(
        ARTIFACTS / "MANIFEST_g2b.json", manifest
    )
    return shas


def write_before_after_captures(
    land_before: Any,
    land_after: Any,
    open_sea_ll: Any,
    lakes_after: Any,
) -> Dict[str, Path]:
    """PNG avant/après : focus NL + vue fenêtre entière."""
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.patches import Polygon as MplPolygon

    from constants import PILOT_WINDOW_LONLAT

    CAPTURE.mkdir(parents=True, exist_ok=True)
    paths: Dict[str, Path] = {}

    def _polys(geom: Any) -> List[Any]:
        if geom is None or geom.is_empty:
            return []
        if geom.geom_type == "Polygon":
            return [geom]
        if geom.geom_type == "MultiPolygon":
            return list(geom.geoms)
        out: List[Any] = []
        for g in getattr(geom, "geoms", []):
            out.extend(_polys(g))
        return out

    def _draw(ax, geom, facecolor, edgecolor, alpha=0.75):
        exteriors = []
        holes = []
        for poly in _polys(geom):
            exteriors.append(MplPolygon(list(zip(*poly.exterior.xy)), closed=True))
            for ring in poly.interiors:
                holes.append(MplPolygon(list(zip(*ring.xy)), closed=True))
        if exteriors:
            ax.add_collection(
                PatchCollection(
                    exteriors,
                    facecolor=facecolor,
                    edgecolor=edgecolor,
                    alpha=alpha,
                    linewidths=0.35,
                )
            )
        if holes:
            ax.add_collection(
                PatchCollection(
                    holes,
                    facecolor="#bbdefb",
                    edgecolor="#1565c0",
                    alpha=1.0,
                    linewidths=0.3,
                )
            )

    def _panel(ax, title, land_geom, open_sea=None, lakes=None, xlim=None, ylim=None):
        ax.set_aspect("equal")
        ax.set_title(title, fontsize=10)
        ax.set_xlabel("longitude")
        ax.set_ylabel("latitude")
        ax.grid(True, alpha=0.25)
        ax.set_facecolor("#e3f2fd")
        if xlim:
            ax.set_xlim(*xlim)
        if ylim:
            ax.set_ylim(*ylim)
        _draw(ax, land_geom, "#2e7d32", "#1b5e20")
        if lakes is not None and not lakes.is_empty:
            _draw(ax, lakes, "#1565c0", "#0d47a1", alpha=0.55)
        if open_sea is not None and not open_sea.is_empty:
            # Mer ouverte : même bleu océan que le fond, bordure plus marquée.
            _draw(ax, open_sea, "#0277bd", "#01579b", alpha=0.9)

    # Focus NL
    fw, fs, fe, fn = NL_FOCUS_LONLAT
    fig, axes = plt.subplots(1, 2, figsize=(12, 7), dpi=120)
    _panel(
        axes[0],
        "Avant (G2 moderne) — lacs = trous",
        land_before,
        lakes=None,
        xlim=(fw, fe),
        ylim=(fs, fn),
    )
    _panel(
        axes[1],
        "Après (G2-bis 1400) — Zuiderzee/Lauwerszee = mer ouverte",
        land_after,
        open_sea=open_sea_ll,
        lakes=lakes_after,
        xlim=(fw, fe),
        ylim=(fs, fn),
    )
    fig.suptitle(
        "v1_047 G2-bis — côte néerlandaise / mer du Nord (avant → après)",
        fontsize=12,
    )
    fig.tight_layout()
    p_nl = CAPTURE / "v1_047_nl_before_after.png"
    fig.savefig(p_nl, format="png")
    plt.close(fig)
    paths["nl_focus"] = p_nl

    # Fenêtre entière
    w, s, e, n = PILOT_WINDOW_LONLAT
    fig, axes = plt.subplots(1, 2, figsize=(14, 8), dpi=120)
    _panel(
        axes[0],
        "Avant — fenêtre pilote",
        land_before,
        xlim=(w, e),
        ylim=(s, n),
    )
    _panel(
        axes[1],
        "Après — fenêtre pilote (contrôle : reste inchangé hors NL)",
        land_after,
        open_sea=open_sea_ll,
        lakes=lakes_after,
        xlim=(w, e),
        ylim=(s, n),
    )
    fig.suptitle("v1_047 G2-bis — fenêtre pilote complète", fontsize=12)
    fig.tight_layout()
    p_full = CAPTURE / "v1_047_window_before_after.png"
    fig.savefig(p_full, format="png")
    plt.close(fig)
    paths["window"] = p_full
    return paths


def run_corrections(
    *,
    apply_corrections: bool = True,
    clean_build: bool = True,
) -> Dict[str, Any]:
    """Exécute G2 puis G2-bis. apply_corrections=False ⇒ sorties G2 (réversibilité)."""
    coastline = _load_coastline()
    t0 = time.perf_counter()
    timings: Dict[str, float] = {}

    if clean_build and BUILD.exists():
        import shutil

        shutil.rmtree(BUILD)
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    corrections_doc = load_corrections()
    divergences_doc = load_divergences()

    t = time.perf_counter()
    fingerprint = coastline.verify_source_fingerprint()
    timings["verify_lock"] = time.perf_counter() - t

    t = time.perf_counter()
    layer_paths = coastline.extract_g2_layers(
        coastline.SOURCES / coastline.SOURCE_ARCHIVE, coastline.LAYERS_BUILD
    )
    timings["extract"] = time.perf_counter() - t

    t = time.perf_counter()
    source_inspect = coastline.inspect_source_layers(layer_paths)
    write_json(BUILD / "02_source_inspect.json", source_inspect)
    timings["inspect"] = time.perf_counter() - t

    projector = Projector(detect_projection())

    # Baseline G2 (avant) — toujours calculée pour l'écart de surface et captures.
    t = time.perf_counter()
    before = coastline.build_land_geometry(layer_paths, projector)
    timings["build_land_g2"] = time.perf_counter() - t
    area_before = float(before["land_area_km2"])

    t = time.perf_counter()
    if apply_corrections:
        after = build_land_with_reclass(
            coastline, layer_paths, projector, corrections_doc, apply_corrections=True
        )
    else:
        # Réversibilité : déléguer strictement à G2 (mêmes sorties SHA256).
        g2_shas = coastline.write_coastline_outputs(
            before, projector, fingerprint, source_inspect
        )
        after = {
            **before,
            "open_sea_ll": MultiPolygon(),
            "open_sea_names": [],
            "open_sea_subtracted": 0,
            "corrections_applied": [],
            "corrections_enabled": False,
            "reclass_names": [],
            "clip_counts": {
                **before["clip_counts"],
                "open_sea_reclassified": 0,
            },
        }
        timings["build_land_g2b"] = time.perf_counter() - t
        timings["export"] = 0.0
        capture_g2 = coastline.write_comparison_capture(before["land_ll"])
        timings["capture"] = 0.0
        timings["total"] = time.perf_counter() - t0
        # Publier aussi les artefacts 02b (meta + registre) pour la trace.
        write_json(
            BUILD / "02b_corrections_meta.json",
            {
                "corrections_enabled": False,
                "land_area_km2": after["land_area_km2"],
                "land_area_km2_before_corrections": area_before,
                "land_area_delta_km2": 0.0,
                "note": "corrections désactivées — sorties G2 (v1_046) bit pour bit",
            },
        )
        write_json(BUILD / "02b_divergences_1400.json", divergences_doc)
        write_json(
            BUILD / "99_timings_g2b.json",
            {k: round(v, 6) for k, v in sorted(timings.items())},
        )
        return {
            "projection": projector.info,
            "land_ll": after["land_ll"],
            "land_xy": after["land_xy"],
            "lakes_ll": after["lakes_ll"],
            "open_sea_ll": after["open_sea_ll"],
            "land_before_ll": before["land_ll"],
            "result": after,
            "before": before,
            "timings": timings,
            "shas": g2_shas,
            "fingerprint": fingerprint,
            "source_inspect": source_inspect,
            "capture": capture_g2,
            "captures": {},
            "corrections_doc": corrections_doc,
            "divergences_doc": divergences_doc,
            "area_before_km2": area_before,
            "apply_corrections": False,
            "layer_paths": layer_paths,
        }

    timings["build_land_g2b"] = time.perf_counter() - t

    t = time.perf_counter()
    shas = write_g2b_outputs(
        after,
        projector,
        fingerprint,
        source_inspect,
        corrections_doc,
        divergences_doc,
        area_before,
    )
    timings["export"] = time.perf_counter() - t

    t = time.perf_counter()
    captures = write_before_after_captures(
        before["land_ll"],
        after["land_ll"],
        after["open_sea_ll"],
        after["lakes_ll"],
    )
    timings["capture"] = time.perf_counter() - t
    timings["total"] = time.perf_counter() - t0
    write_json(
        BUILD / "99_timings_g2b.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )

    return {
        "projection": projector.info,
        "land_ll": after["land_ll"],
        "land_xy": after["land_xy"],
        "lakes_ll": after["lakes_ll"],
        "open_sea_ll": after["open_sea_ll"],
        "land_before_ll": before["land_ll"],
        "result": after,
        "before": before,
        "timings": timings,
        "shas": shas,
        "fingerprint": fingerprint,
        "source_inspect": source_inspect,
        "captures": captures,
        "corrections_doc": corrections_doc,
        "divergences_doc": divergences_doc,
        "area_before_km2": area_before,
        "apply_corrections": True,
        "layer_paths": layer_paths,
    }


def apply_corrections_once(built: Dict[str, Any]) -> Any:
    """Idempotence : réappliquer le reclassement sur un résultat déjà corrigé."""
    # Le résultat corrigé a déjà open_sea séparé ; re-soustraire open_sea ∪ lakes
    # doit produire la même géométrie.
    land = built["land_ll"]
    lakes = built["lakes_ll"]
    open_sea = built["open_sea_ll"]
    water = []
    if lakes is not None and not lakes.is_empty:
        water.append(lakes)
    if open_sea is not None and not open_sea.is_empty:
        water.append(open_sea)
    if not water:
        return land
    # Land is already without water ; subtracting again is idempotent.
    again = land.difference(unary_union(water))
    coastline = _load_coastline()
    return coastline._as_multipolygon(again)


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="G2-bis corrections littoral 1400")
    parser.add_argument(
        "--no-corrections",
        action="store_true",
        help="Désactive les corrections (réversibilité → sorties v1_046)",
    )
    args = parser.parse_args(list(argv) if argv is not None else None)
    result = run_corrections(apply_corrections=not args.no_corrections)
    built = result["result"]
    print(
        f"pipeline {G2B_PIPELINE_VERSION} | g2bis | corrections={not args.no_corrections} | "
        f"land_km2={built['land_area_km2']:.3f} "
        f"(before={result['area_before_km2']:.3f}, "
        f"delta={built['land_area_km2'] - result['area_before_km2']:.3f}) | "
        f"open_sea={built.get('open_sea_names', [])}"
    )
    for path, digest in sorted(result["shas"].items()):
        print(f"  {path}  {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
