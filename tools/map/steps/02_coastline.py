"""G2 — Littoral réel de la fenêtre pilote (v1_046).

Branche Natural Earth 1:10m sur le tuyau existant. Ne produit PAS de cellules,
d'adjacence ni de villes : terre / îles / lacs découpés, projetés, contrôlés.

Usage (via pipeline.py) :
  .venv/Scripts/python.exe pipeline.py --source natural_earth
  .venv/Scripts/python.exe pipeline.py --source fixture   # retour arrière G1
"""

from __future__ import annotations

import math
import shutil
import time
import zipfile
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

import geopandas as gpd
from shapely.geometry import MultiPolygon, Point, Polygon, box, mapping
from shapely.ops import transform as shp_transform
from shapely.ops import unary_union
from shapely.validation import explain_validity

from constants import (
    FLOAT_DECIMALS,
    G2_LAND_AREA_KM2_MAX,
    G2_LAND_AREA_KM2_MIN,
    G2_LAYERS,
    GAME_CELL_RADIUS,
    GAME_CORRIDOR_HALF_WIDTH,
    GAME_EQUIRECT_MID_LAT,
    PILOT_WINDOW_JUSTIFICATION,
    PILOT_WINDOW_LONLAT,
    PIPELINE_VERSION,
    SOURCE_CRS,
    TARGET_CRS,
)
from io_util import read_json, sha256_file, write_json
from projection import Projector, crs_declaration, detect_projection

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "sources"
LOCK_PATH = ROOT / "sources.lock"
BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LAYERS_BUILD = BUILD / "ne_layers"

SOURCE_ARCHIVE = "10m_physical.zip"

# Gazetteer local (lon, lat, nom) — uniquement pour NOMMER les îles écartées.
# Appariement par centroïde le plus proche sous 0.25°. Pas de réseau.
_ISLAND_GAZETTEER: Tuple[Tuple[float, float, str], ...] = (
    (-2.198, 49.714, "Aurigny (Alderney)"),
    (-2.364, 49.433, "Sercq (Sark)"),
    (-2.449, 49.472, "Herm"),
    (-2.535, 49.455, "Jéthou"),
    (-4.012, 48.746, "Île de Batz"),
    (-4.860, 48.040, "Île de Sein"),
    (-5.085, 48.435, "Ouessant"),
    (-3.180, 47.340, "Belle-Île"),
    (-2.950, 47.500, "Houat"),
    (-2.870, 47.390, "Hoëdic"),
    (-1.562, 46.890, "Île d'Yeu"),
    (6.890, 53.640, "Schiermonnikoog (fragment)"),
    (8.295, 55.488, "Fanø (fragment)"),
    (8.152, 53.727, "Norderney (fragment)"),
    (8.494, 53.686, "Baltrum (fragment)"),
    (7.898, 53.789, "Langeoog (fragment)"),
    (6.472, 53.548, "Ameland (fragment)"),
    (7.406, 53.729, "Spiekeroog (fragment)"),
    (-6.297, 49.922, "îles Scilly (fragment)"),
    (-4.672, 51.177, "Caldey"),
    (-5.295, 51.735, "Ramsey"),
    (-6.016, 53.487, "Ireland's Eye"),
    (-6.162, 53.354, "Dalkey Island"),
    (-5.341, 51.866, "Skomer"),
)


def pilot_window_polygon() -> Polygon:
    w, s, e, n = PILOT_WINDOW_LONLAT
    return box(w, s, e, n)


def verify_source_fingerprint() -> Dict[str, Any]:
    """Vérifie 10m_physical.zip contre sources.lock — refuse si divergente."""
    if not LOCK_PATH.exists():
        raise FileNotFoundError(
            f"sources.lock absent ({LOCK_PATH}). Exécuter tools/lock_sources.py."
        )
    lock = read_json(LOCK_PATH)
    files = lock.get("files") or {}
    if SOURCE_ARCHIVE not in files:
        raise RuntimeError(
            f"{SOURCE_ARCHIVE} absent de sources.lock — entrée non identifiée."
        )
    expected = files[SOURCE_ARCHIVE]["sha256"]
    archive = SOURCES / SOURCE_ARCHIVE
    if not archive.exists():
        raise FileNotFoundError(
            f"Archive manquante : {archive}. Ne pas télécharger depuis le brief — "
            "déposer le fichier figé par G0."
        )
    actual = sha256_file(archive)
    if actual != expected:
        raise RuntimeError(
            f"EMPREINTE DIVERGENTE pour {SOURCE_ARCHIVE}.\n"
            f"  attendu (sources.lock) : {expected}\n"
            f"  calculé                : {actual}\n"
            "Le pipeline refuse de tourner sur une entrée non identifiée."
        )
    return {
        "archive": SOURCE_ARCHIVE,
        "sha256": actual,
        "bytes": files[SOURCE_ARCHIVE].get("bytes"),
        "layers_in_lock": files[SOURCE_ARCHIVE].get("layers", []),
        "licence": lock.get("licence", {}),
    }


def extract_g2_layers(archive: Path, dest: Path) -> Dict[str, Path]:
    """Extrait les quatre couches G2 vers build/ (jamais une zone versionnée)."""
    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True, exist_ok=True)
    wanted_stems = set(G2_LAYERS)
    out: Dict[str, Path] = {}
    with zipfile.ZipFile(archive) as zf:
        for name in sorted(zf.namelist()):
            stem = Path(name).stem
            if stem not in wanted_stems:
                continue
            target = dest / Path(name).name
            with zf.open(name) as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)
            if target.suffix.lower() == ".shp":
                out[stem] = target
    missing = [layer for layer in G2_LAYERS if layer not in out]
    if missing:
        raise RuntimeError(f"Couches manquantes dans l'archive : {missing}")
    return {k: out[k] for k in sorted(out.keys())}


def _layer_report(gdf: gpd.GeoDataFrame, layer: str) -> Dict[str, Any]:
    bounds = [float(v) for v in gdf.total_bounds]
    crs = str(gdf.crs) if gdf.crs is not None else "None"
    return {
        "layer": layer,
        "feature_count": int(len(gdf)),
        "bounds_lonlat": bounds,
        "crs": crs,
        "columns": sorted(str(c) for c in gdf.columns if c != "geometry"),
    }


def inspect_source_layers(layer_paths: Dict[str, Path]) -> Dict[str, Any]:
    """Constate le contenu réel de chaque couche avant découpage."""
    reports = []
    for layer in sorted(layer_paths.keys()):
        gdf = gpd.read_file(layer_paths[layer])
        reports.append(_layer_report(gdf, layer))
    return {"layers": reports}


def _sort_geoms(geoms: Sequence[Any]) -> List[Any]:
    def key(g: Any) -> Tuple[float, float, float]:
        c = g.centroid
        return (round(c.x, 6), round(c.y, 6), round(g.area, 6))

    return sorted(list(geoms), key=key)


def _as_multipolygon(geom: Any) -> MultiPolygon:
    if geom is None or geom.is_empty:
        return MultiPolygon()
    if not geom.is_valid:
        geom = geom.buffer(0)
    if geom.geom_type == "Polygon":
        return MultiPolygon([geom])
    if geom.geom_type == "MultiPolygon":
        return MultiPolygon(_sort_geoms(list(geom.geoms)))
    if geom.geom_type == "GeometryCollection":
        polys: List[Polygon] = []
        for g in geom.geoms:
            if g.geom_type == "Polygon":
                polys.append(g)
            elif g.geom_type == "MultiPolygon":
                polys.extend(list(g.geoms))
        return MultiPolygon(_sort_geoms(polys))
    return MultiPolygon()


def _clip_gdf(gdf: gpd.GeoDataFrame, window: Polygon) -> gpd.GeoDataFrame:
    """Découpe à la fenêtre : intersection, pas rejet ni conservation entière."""
    if gdf.crs is not None and str(gdf.crs).upper() not in ("EPSG:4326", SOURCE_CRS):
        gdf = gdf.to_crs(SOURCE_CRS)
    clipped = gpd.clip(gdf, window)
    if len(clipped) == 0:
        return clipped
    clipped = clipped.copy()
    # Centroïdes lon/lat uniquement pour un tri stable (pas pour des distances).
    import warnings

    with warnings.catch_warnings():
        warnings.simplefilter("ignore", UserWarning)
        clipped["_sx"] = clipped.geometry.centroid.x
        clipped["_sy"] = clipped.geometry.centroid.y
    clipped = clipped.sort_values(by=["_sx", "_sy"]).drop(columns=["_sx", "_sy"])
    return clipped.reset_index(drop=True)


def _project_geom_ll_to_xy(geom: Any, projector: Projector) -> Any:
    def _coords(coords):
        return [projector.project_xy(lon, lat) for lon, lat in coords]

    if geom.is_empty:
        return geom
    if geom.geom_type == "Polygon":
        exterior = _coords(list(geom.exterior.coords))
        holes = [_coords(list(r.coords)) for r in geom.interiors]
        return Polygon(exterior, holes)
    if geom.geom_type == "MultiPolygon":
        parts = [_project_geom_ll_to_xy(p, projector) for p in _sort_geoms(list(geom.geoms))]
        return MultiPolygon(parts)
    raise TypeError(geom.geom_type)


def _area_m2(geom_xy: Any) -> float:
    return float(geom_xy.area)


def _area_km2(geom_xy: Any) -> float:
    return _area_m2(geom_xy) / 1_000_000.0


def _name_island(lon: float, lat: float, area_km2: float) -> str:
    best = None
    best_d = 1e9
    for glon, glat, name in _ISLAND_GAZETTEER:
        d = math.hypot(lon - glon, lat - glat)
        if d < best_d:
            best_d = d
            best = name
    if best is not None and best_d <= 0.25:
        return best
    return f"île_anonyme@{lon:.3f},{lat:.3f}({area_km2:.2f}km²)"


def _percentile(values: Sequence[float], p: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    if len(ordered) == 1:
        return round(ordered[0], 6)
    k = (len(ordered) - 1) * (p / 100.0)
    f = math.floor(k)
    c = math.ceil(k)
    if f == c:
        return round(ordered[int(k)], 6)
    return round(ordered[f] * (c - k) + ordered[c] * (k - f), 6)


def derive_island_threshold_km2(part_areas_km2: Sequence[float]) -> Dict[str, Any]:
    """Dérive le seuil depuis le décrochement log dans la bande des îles jouables.

    Les masses ≥ 1000 km² sont toujours gardées. On cherche le plus grand écart
    log10 parmi les aires consécutives dont le bord bas est dans [2, 40] km²
    (bande où un seuil de gameplay a du sens). Repli : médiane des petites.
    """
    always_keep = 1000.0
    small = sorted(a for a in part_areas_km2 if a < always_keep)
    if len(small) < 2:
        threshold = 4.0
        gap: Dict[str, float] = {"lower_km2": 0.0, "upper_km2": threshold, "log_gap": 0.0}
    else:
        best_i = -1
        best_gap = -1.0
        for i in range(len(small) - 1):
            # Bande jouable : ignorer le bruit des îlots < 2 km² (décrochements
            # parasites) et les grands blocs > 40 km² (Jersey, Wight…).
            if small[i] < 2.0 or small[i] > 40.0:
                continue
            lg = math.log10(small[i + 1]) - math.log10(small[i])
            if lg > best_gap:
                best_gap = lg
                best_i = i
        if best_i < 0:
            threshold = float(small[len(small) // 2])
            gap = {"lower_km2": threshold, "upper_km2": threshold, "log_gap": 0.0}
        else:
            lower = small[best_i]
            upper = small[best_i + 1]
            threshold = float(round(math.sqrt(lower * upper), 3))
            gap = {
                "lower_km2": round(lower, 6),
                "upper_km2": round(upper, 6),
                "log_gap": round(best_gap, 6),
            }

    distribution = {
        "count": len(part_areas_km2),
        "min_km2": round(min(part_areas_km2), 6) if part_areas_km2 else 0.0,
        "max_km2": round(max(part_areas_km2), 6) if part_areas_km2 else 0.0,
        "p10_km2": _percentile(part_areas_km2, 10),
        "p25_km2": _percentile(part_areas_km2, 25),
        "p50_km2": _percentile(part_areas_km2, 50),
        "p75_km2": _percentile(part_areas_km2, 75),
        "p90_km2": _percentile(part_areas_km2, 90),
        "areas_km2_sorted": [round(a, 6) for a in sorted(part_areas_km2)],
    }
    return {
        "threshold_km2": threshold,
        "gap": gap,
        "always_keep_above_km2": always_keep,
        "method": (
            "plus grand écart log10 entre aires consécutives dont le bord bas "
            "est dans [2, 40] km² (masses ≥ 1000 km² toujours gardées) ; "
            "seuil = moyenne géométrique des deux bords du décrochement"
        ),
        "distribution": distribution,
    }


def build_land_geometry(
    layer_paths: Dict[str, Path],
    projector: Projector,
) -> Dict[str, Any]:
    """Fusionne land+îles, soustrait lacs, filtre îles, projette."""
    window = pilot_window_polygon()
    land = _clip_gdf(gpd.read_file(layer_paths["ne_10m_land"]), window)
    islands = _clip_gdf(gpd.read_file(layer_paths["ne_10m_minor_islands"]), window)
    lakes = _clip_gdf(gpd.read_file(layer_paths["ne_10m_lakes"]), window)
    coastline = _clip_gdf(gpd.read_file(layer_paths["ne_10m_coastline"]), window)

    land_geoms = _sort_geoms([g for g in land.geometry if g is not None and not g.is_empty])
    island_geoms = _sort_geoms(
        [g for g in islands.geometry if g is not None and not g.is_empty]
    )
    lake_geoms = _sort_geoms([g for g in lakes.geometry if g is not None and not g.is_empty])

    raw_union = _as_multipolygon(unary_union(land_geoms + island_geoms))
    parts_ll = list(raw_union.geoms) if raw_union.geom_type == "MultiPolygon" else [raw_union]
    parts_ll = _sort_geoms(parts_ll)

    part_records: List[Dict[str, Any]] = []
    for part in parts_ll:
        part_xy = _project_geom_ll_to_xy(part, projector)
        area_km2 = _area_km2(part_xy)
        lon, lat = float(part.centroid.x), float(part.centroid.y)
        part_records.append(
            {
                "area_km2": round(area_km2, 6),
                "centroid_lon": round(lon, FLOAT_DECIMALS),
                "centroid_lat": round(lat, FLOAT_DECIMALS),
                "geom_ll": part,
                "name": _name_island(lon, lat, area_km2),
            }
        )
    part_records = sorted(
        part_records,
        key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"]),
    )

    threshold_info = derive_island_threshold_km2([r["area_km2"] for r in part_records])
    threshold = float(threshold_info["threshold_km2"])

    kept = [r for r in part_records if r["area_km2"] >= threshold]
    dropped = [r for r in part_records if r["area_km2"] < threshold]
    kept = sorted(kept, key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"]))
    dropped = sorted(
        dropped, key=lambda r: (-r["area_km2"], r["centroid_lon"], r["centroid_lat"])
    )

    kept_union = (
        _as_multipolygon(unary_union([r["geom_ll"] for r in kept]))
        if kept
        else MultiPolygon()
    )

    lakes_union = unary_union(lake_geoms) if lake_geoms else MultiPolygon()
    if not lakes_union.is_empty and not lakes_union.is_valid:
        lakes_union = lakes_union.buffer(0)

    land_minus_lakes = (
        kept_union.difference(lakes_union) if not lakes_union.is_empty else kept_union
    )
    land_ll = _as_multipolygon(land_minus_lakes)
    land_xy = _project_geom_ll_to_xy(land_ll, projector)
    if not land_xy.is_valid:
        land_xy = _as_multipolygon(land_xy.buffer(0))

    lake_names: List[str] = []
    if "name" in lakes.columns:
        for nm in lakes["name"].tolist():
            if nm is not None and str(nm) != "nan":
                lake_names.append(str(nm))
    lake_names = sorted(set(lake_names))

    total_km2 = round(_area_km2(land_xy), 3)
    hole_count = sum(
        len(p.interiors)
        for p in (list(land_ll.geoms) if land_ll.geom_type == "MultiPolygon" else [land_ll])
    )

    return {
        "land_ll": land_ll,
        "land_xy": land_xy,
        "lakes_ll": (
            _as_multipolygon(lakes_union) if not lakes_union.is_empty else MultiPolygon()
        ),
        "coastline_count": int(len(coastline)),
        "clip_counts": {
            "land_polygons": int(len(land)),
            "minor_islands": int(len(islands)),
            "lakes": int(len(lakes)),
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
        "lakes_subtracted": int(len(lakes)),
        "hole_count": hole_count,
        "land_area_km2": total_km2,
        "land_area_m2": round(_area_m2(land_xy), FLOAT_DECIMALS),
        "window": {
            "lonlat": list(PILOT_WINDOW_LONLAT),
            "justification": PILOT_WINDOW_JUSTIFICATION,
            "gameplay_choice": True,
        },
    }


def write_coastline_outputs(
    result: Dict[str, Any],
    projector: Projector,
    fingerprint: Dict[str, Any],
    source_inspect: Dict[str, Any],
) -> Dict[str, str]:
    """Écrit build/ + artifacts/ déterministes. Retourne {relpath: sha256}."""
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    land_ll = result["land_ll"]
    land_xy = result["land_xy"]

    meta = {
        "pipeline_version": PIPELINE_VERSION,
        "data_class": "natural_earth_g2",
        "comment": "Littoral réel G2 — PAS de cellules (G3).",
        "source": fingerprint,
        "source_inspect": source_inspect,
        "window": result["window"],
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
            "source_crs": SOURCE_CRS,
            "target_crs": TARGET_CRS,
        },
        "clip_counts": result["clip_counts"],
        "threshold": {
            "threshold_km2": result["threshold"]["threshold_km2"],
            "gap": result["threshold"]["gap"],
            "method": result["threshold"]["method"],
            "distribution_summary": {
                k: result["threshold"]["distribution"][k]
                for k in (
                    "count",
                    "min_km2",
                    "max_km2",
                    "p10_km2",
                    "p25_km2",
                    "p50_km2",
                    "p75_km2",
                    "p90_km2",
                )
            },
        },
        "islands_kept_count": len(result["islands_kept"]),
        "islands_dropped_count": len(result["islands_dropped"]),
        "largest_dropped": result["largest_dropped"],
        "lakes_subtracted": result["lakes_subtracted"],
        "lake_names": result["lake_names"],
        "hole_count": result["hole_count"],
        "land_area_km2": result["land_area_km2"],
        "land_area_m2": result["land_area_m2"],
        "geom_type": land_ll.geom_type,
        "part_count": (
            len(land_ll.geoms)
            if land_ll.geom_type == "MultiPolygon"
            else (0 if land_ll.is_empty else 1)
        ),
    }
    dist_full = {
        "areas_km2_sorted": result["threshold"]["distribution"]["areas_km2_sorted"],
        "islands_kept": result["islands_kept"],
        "islands_dropped": result["islands_dropped"],
    }

    shas: Dict[str, str] = {}
    shas["build/02_coastline_meta.json"] = write_json(BUILD / "02_coastline_meta.json", meta)
    shas["build/02_island_distribution.json"] = write_json(
        BUILD / "02_island_distribution.json", dist_full
    )

    land_geojson = {
        "type": "FeatureCollection",
        "data_class": "natural_earth_g2",
        "crs": {"type": "name", "properties": {"name": SOURCE_CRS}},
        "features": [
            {
                "type": "Feature",
                "properties": {
                    "role": "land_with_lake_holes",
                    "area_km2": result["land_area_km2"],
                },
                "geometry": mapping(land_ll),
            }
        ],
    }
    shas["build/02_land.geojson"] = write_json(BUILD / "02_land.geojson", land_geojson)

    land_xy_geojson = {
        "type": "FeatureCollection",
        "data_class": "natural_earth_g2",
        "crs": {"type": "name", "properties": {"name": projector.info.epsg}},
        "features": [
            {
                "type": "Feature",
                "properties": {
                    "role": "land_projected",
                    "area_m2": result["land_area_m2"],
                },
                "geometry": mapping(land_xy),
            }
        ],
    }
    shas["build/02_land_projected.geojson"] = write_json(
        BUILD / "02_land_projected.geojson", land_xy_geojson
    )

    gpkg_path = BUILD / "coastline.gpkg"
    if gpkg_path.exists():
        gpkg_path.unlink()
    gdf = gpd.GeoDataFrame(
        {
            "role": ["land_with_lake_holes"],
            "area_km2": [result["land_area_km2"]],
        },
        geometry=[land_ll],
        crs=SOURCE_CRS,
    )
    gdf.to_file(gpkg_path, layer="land", driver="GPKG")
    # Le binaire GPKG n'est pas bit-stable (métadonnées SQLite). La preuve de
    # déterminisme porte sur le GeoJSON canonique équivalent (02_land.geojson),
    # déjà hashé ci-dessus. On consigne seulement le chemin.
    shas["build/02_coastline_gpkg_note.json"] = write_json(
        BUILD / "02_coastline_gpkg_note.json",
        {
            "path": "build/coastline.gpkg",
            "deterministic_proxy": "build/02_land.geojson",
            "note": (
                "coastline.gpkg est produit pour QGIS ; son SHA256 binaire n'est "
                "pas garanti. Le contenu géométrique est celui de 02_land.geojson."
            ),
            "land_geojson_sha256": shas["build/02_land.geojson"],
        },
    )

    artifact = {
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
        "lakes_subtracted": result["lakes_subtracted"],
        "threshold_km2": result["threshold"]["threshold_km2"],
        # Usage = projeté ; lon/lat conservées (plan §3.2 / v1_064).
        "geometry": mapping(land_xy),
        "geometry_lonlat": mapping(land_ll),
    }
    shas["artifacts/coastline.json"] = write_json(ARTIFACTS / "coastline.json", artifact)

    manifest = {
        "pipeline_version": PIPELINE_VERSION,
        "data_class": "natural_earth_g2",
        "comment": "MANIFEST G2 — fixed_timestamp figé ; timings exclus.",
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
        },
        "inputs": {"10m_physical.zip": fingerprint["sha256"]},
        "outputs": {k: shas[k] for k in sorted(shas.keys()) if k.startswith("artifacts/")},
        "build_outputs": {k: shas[k] for k in sorted(shas.keys()) if k.startswith("build/")},
    }
    shas["artifacts/MANIFEST_g2.json"] = write_json(
        ARTIFACTS / "MANIFEST_g2.json", manifest
    )
    return shas


def build_current_game_landmask() -> Any:
    """Reproduit hors Unity un masque proche de BuildLandMask / LegacyDisks.

    Lecture seule de province_coordinates.json + province_adjacency.json.
    """
    # ROOT = pipeline/geo → read-only fixtures under legacy_game_data/  # FORGEHISTORY-PATH-ADJUSTMENT
    coords_path = ROOT.parents[1] / "data" / "province-centres-1400.json"
    adj_path = (
        ROOT  # FORGEHISTORY-PATH-ADJUSTMENT
        / "legacy_game_data"  # FORGEHISTORY-PATH-ADJUSTMENT
        / "province_adjacency.json"  # FORGEHISTORY-PATH-ADJUSTMENT
    )
    coords_doc = read_json(coords_path)
    adj_doc = read_json(adj_path)

    cos_mid = math.cos(math.radians(GAME_EQUIRECT_MID_LAT))

    def to_xy(lon: float, lat: float) -> Tuple[float, float]:
        return (lon * cos_mid, -lat)

    points: Dict[int, Tuple[float, float]] = {}
    for row in coords_doc["coordinates"]:
        pid = int(row["id"])
        points[pid] = to_xy(float(row["lon"]), float(row["lat"]))

    disks = []
    for pid in sorted(points.keys()):
        x, y = points[pid]
        disks.append(Point(x, y).buffer(GAME_CELL_RADIUS, resolution=16))

    neighbors: Dict[int, List[int]] = {}
    for entry in adj_doc["adjacency"]:
        pid = int(entry["id"])
        neighbors[pid] = sorted(int(n) for n in (entry.get("neighbors") or []))

    capsules = []
    seen = set()
    for a in sorted(neighbors.keys()):
        if a not in points:
            continue
        for b in neighbors[a]:
            if b not in points:
                continue
            key = (min(a, b), max(a, b))
            if key in seen:
                continue
            seen.add(key)
            ax, ay = points[a]
            bx, by = points[b]
            capsules.append(
                Point(ax, ay)
                .buffer(GAME_CORRIDOR_HALF_WIDTH, resolution=8)
                .union(Point(bx, by).buffer(GAME_CORRIDOR_HALF_WIDTH, resolution=8))
                .convex_hull
            )

    tris = []
    ids = sorted(neighbors.keys())
    id_set = set(ids)
    for a in ids:
        na = set(neighbors.get(a, [])) & id_set
        for b in sorted(na):
            if b <= a:
                continue
            nb = set(neighbors.get(b, [])) & id_set
            for c in sorted(na & nb):
                if c <= b:
                    continue
                if a not in points or b not in points or c not in points:
                    continue
                tris.append(Polygon([points[a], points[b], points[c], points[a]]))

    union = unary_union(disks + capsules + tris)
    if not union.is_valid:
        union = union.buffer(0)
    return union


def write_comparison_capture(land_ll: Any) -> Path:
    """PNG côte à côte : masque jeu actuel vs littoral G2 (même emprise lon/lat)."""
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.patches import Polygon as MplPolygon

    CAPTURE.mkdir(parents=True, exist_ok=True)
    w, s, e, n = PILOT_WINDOW_LONLAT

    game_mask = build_current_game_landmask()
    cos_mid = math.cos(math.radians(GAME_EQUIRECT_MID_LAT))

    def game_xy_to_lonlat(geom: Any) -> Any:
        def _f(x, y, z=None):
            return (x / cos_mid, -y)

        return shp_transform(_f, geom)

    game_ll = game_xy_to_lonlat(game_mask)
    window = pilot_window_polygon()
    game_clip = game_ll.intersection(window)

    fig, axes = plt.subplots(1, 2, figsize=(14, 8), dpi=120)
    titles = (
        "Masque actuel (enveloppe 50 points)",
        "Littoral réel G2 (Natural Earth)",
    )
    geoms = (game_clip, land_ll)
    colors = ("#c62828", "#2e7d32")

    for ax, title, geom, color in zip(axes, titles, geoms, colors):
        ax.set_xlim(w, e)
        ax.set_ylim(s, n)
        ax.set_aspect("equal")
        ax.set_title(title, fontsize=11)
        ax.set_xlabel("longitude")
        ax.set_ylabel("latitude")
        ax.grid(True, alpha=0.25)
        ax.set_facecolor("#e3f2fd")

        polys: List[Any] = []
        if geom is None or geom.is_empty:
            polys = []
        elif geom.geom_type == "Polygon":
            polys = [geom]
        elif geom.geom_type == "MultiPolygon":
            polys = list(geom.geoms)
        else:
            for g in getattr(geom, "geoms", []):
                if g.geom_type == "Polygon":
                    polys.append(g)
                elif g.geom_type == "MultiPolygon":
                    polys.extend(list(g.geoms))

        exteriors = []
        holes = []
        for poly in polys:
            exteriors.append(MplPolygon(list(zip(*poly.exterior.xy)), closed=True))
            for ring in poly.interiors:
                holes.append(MplPolygon(list(zip(*ring.xy)), closed=True))
        if exteriors:
            ax.add_collection(
                PatchCollection(
                    exteriors,
                    facecolor=color,
                    edgecolor="#1b5e20",
                    alpha=0.75,
                    linewidths=0.4,
                )
            )
        if holes:
            ax.add_collection(
                PatchCollection(
                    holes,
                    facecolor="#e3f2fd",
                    edgecolor="#1565c0",
                    alpha=1.0,
                    linewidths=0.3,
                )
            )

    fig.suptitle(
        "v1_046 G2 — comparaison masque Voronoï actuel vs littoral réel",
        fontsize=12,
    )
    fig.tight_layout()
    out = CAPTURE / "v1_046_coastline_compare.png"
    fig.savefig(out, format="png")
    plt.close(fig)
    return out


def run_coastline(*, clean_build: bool = True) -> Dict[str, Any]:
    """Exécute G2 de bout en bout. Ne touche pas aux étapes cellules/adjacence."""
    t0 = time.perf_counter()
    timings: Dict[str, float] = {}

    if clean_build and BUILD.exists():
        shutil.rmtree(BUILD)
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    t = time.perf_counter()
    fingerprint = verify_source_fingerprint()
    timings["verify_lock"] = time.perf_counter() - t

    t = time.perf_counter()
    layer_paths = extract_g2_layers(SOURCES / SOURCE_ARCHIVE, LAYERS_BUILD)
    timings["extract"] = time.perf_counter() - t

    t = time.perf_counter()
    source_inspect = inspect_source_layers(layer_paths)
    write_json(BUILD / "02_source_inspect.json", source_inspect)
    timings["inspect"] = time.perf_counter() - t

    projector = Projector(detect_projection())

    t = time.perf_counter()
    built = build_land_geometry(layer_paths, projector)
    timings["build_land"] = time.perf_counter() - t

    t = time.perf_counter()
    shas = write_coastline_outputs(built, projector, fingerprint, source_inspect)
    timings["export"] = time.perf_counter() - t

    t = time.perf_counter()
    capture = write_comparison_capture(built["land_ll"])
    timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t0
    write_json(
        BUILD / "99_timings_g2.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )

    return {
        "projection": projector.info,
        "land_ll": built["land_ll"],
        "land_xy": built["land_xy"],
        "lakes_ll": built["lakes_ll"],
        "result": built,
        "timings": timings,
        "shas": shas,
        "fingerprint": fingerprint,
        "source_inspect": source_inspect,
        "capture": capture,
    }


def g2_q1_validity(land_geom: Any) -> Tuple[bool, str]:
    if land_geom is None or land_geom.is_empty:
        return False, "land_empty"
    if not land_geom.is_valid:
        return False, explain_validity(land_geom)
    return True, "ok"


def g2a_within_window(land_ll: Any, window: Optional[Polygon] = None) -> Tuple[bool, str]:
    window = window or pilot_window_polygon()
    if land_ll.is_empty:
        return False, "empty"
    outside = land_ll.difference(window.buffer(1e-9))
    area_out = 0.0 if outside.is_empty else outside.area
    ok = area_out <= 1e-8
    return ok, f"outside_deg2={area_out:.3e}"


def g2b_lakes_are_holes(land_ll: Any, lakes_ll: Any) -> Tuple[bool, str]:
    """Aucun lac ne doit intersecter la terre finale (les lacs sont des trous)."""
    if lakes_ll is None or lakes_ll.is_empty:
        return True, "no_lakes"
    leftover = land_ll.intersection(lakes_ll)
    leftover_area = 0.0 if leftover.is_empty else leftover.area
    ok = leftover_area <= 1e-10
    return ok, f"land_intersect_lakes_deg2={leftover_area:.3e}"


def g2c_area_plausible(area_km2: float) -> Tuple[bool, str]:
    ok = G2_LAND_AREA_KM2_MIN <= area_km2 <= G2_LAND_AREA_KM2_MAX
    return ok, (
        f"area_km2={area_km2:.1f} "
        f"expected=[{G2_LAND_AREA_KM2_MIN:.0f},{G2_LAND_AREA_KM2_MAX:.0f}]"
    )
