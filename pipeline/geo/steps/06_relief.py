"""G6 — relief Copernicus DEM : altitude, pente, rugosité, barrières et cols (v1_052).

Lit cells_g3.json, adjacency_g5.json et le cache DEM vérifié ; produit
cells_relief_g6.json, adjacency_g6.json, passes_g6.json, stats_g6.json,
MANIFEST_g6.json, registry/relief_registry.json et deux captures.

Usage :
  ../../.venv/bin/python pipeline.py --source relief
  ../../.venv/bin/python tests/run_proof_g6.py
"""

from __future__ import annotations

import importlib.util
import math
import statistics
import sys
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

import numpy as np
from shapely.geometry import LineString, Point, mapping, shape
from shapely.ops import linemerge, unary_union

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
TOOLS = ROOT / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from constants import (  # noqa: E402
    G6_EDGE_SAMPLE_STEP_M,
    G6_ELEV_DECIMALS,
    G6_KNOWN_PASS_MATCH_M,
    G6_KNOWN_PASSES,
    G6_PIPELINE_VERSION,
    G6_REGISTRY_CREATED,
    G6_ROUGH_DECIMALS,
    G6_SAMPLE_STEP_DEG,
    G6_SAMPLE_VALID_MAX_M,
    G6_SAMPLE_VALID_MIN_M,
    G6_SLOPE_DECIMALS,
    PILOT_WINDOW_LONLAT,
)
from io_util import read_json, round_float, sha256_file, write_json  # noqa: E402
from projection import Projector, crs_declaration, detect_projection  # noqa: E402
from dem_batch import (  # noqa: E402
    MeasurementTable,
    measurement_table_key,
    read_grouped_windows,
)

ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
LOCK_PATH = ROOT / "sources.lock"

EARTH_RADIUS_M = 6_371_000.0


def _load_fetch_dem():
    path = ROOT / "tools" / "fetch_dem_tiles.py"
    spec = importlib.util.spec_from_file_location("fetch_dem_tiles", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def verify_dem_fingerprint(*, download: bool = False) -> Tuple[bool, str, dict]:
    fetch = _load_fetch_dem()
    report = fetch.ensure_dem_cache(download=download)
    detail = (
        f"verified={report['verified']}/{report['tile_count']} "
        f"collective={report['collective_ok']} "
        f"recipe={report['collective_recipe']}"
    )
    return bool(report["ok"]), detail, report


def haversine_m(lon1: float, lat1: float, lon2: float, lat2: float) -> float:
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    dphi = math.radians(lat2 - lat1)
    dlambda = math.radians(lon2 - lon1)
    a = (
        math.sin(dphi / 2) ** 2
        + math.cos(phi1) * math.cos(phi2) * math.sin(dlambda / 2) ** 2
    )
    return 2 * EARTH_RADIUS_M * math.asin(min(1.0, math.sqrt(a)))


def _tile_bounds_from_name(tile_name: str) -> Tuple[float, float, float, float]:
    """Retourne (lon_min, lat_min, lon_max, lat_max) pour une tuile Copernicus."""
    stem = Path(tile_name).stem
    token = stem.replace("Copernicus_DSM_COG_30_", "")
    lat_hem = token[0]
    rest = token[1:]
    lat_deg = int(rest.split("_")[0])
    lon_token = rest.split("_", 2)[2]
    lon_hem = lon_token[0]
    lon_deg = int(lon_token[1:].split("_")[0])
    if lat_hem == "N":
        lat_min, lat_max = float(lat_deg), float(lat_deg + 1)
    else:
        lat_min, lat_max = float(-(lat_deg + 1)), float(-lat_deg)
    if lon_hem == "E":
        lon_min, lon_max = float(lon_deg), float(lon_deg + 1)
    else:
        lon_min, lon_max = float(-(lon_deg + 1)), float(-lon_deg)
    return lon_min, lat_min, lon_max, lat_max


class DemSampler:
    """Lecteur MNT groupé par tuile, avec table de mesures réutilisable."""

    def __init__(
        self,
        cache_dir: Path,
        *,
        lock_path: Path = LOCK_PATH,
        measurement_table: Optional[MeasurementTable] = None,
    ) -> None:
        self.cache_dir = Path(cache_dir)
        self.lock_path = Path(lock_path)
        self.measurement_table = measurement_table
        self._datasets: Dict[str, Any] = {}
        self.last_batch_metrics: Dict[str, int] = {}
        self.sampling_metrics: Dict[str, int] = {}
        self.reset_sampling_metrics()
        fetch = _load_fetch_dem()
        self._tile_paths: List[Tuple[float, float, float, float, Path]] = []
        self._tiles_by_degree: Dict[
            Tuple[int, int], Tuple[float, float, float, float, Path]
        ] = {}
        lock = read_json(self.lock_path)
        lon_mins: List[float] = []
        lon_maxs: List[float] = []
        lat_mins: List[float] = []
        lat_maxs: List[float] = []
        for tile_name in sorted(lock["dem"]["tiles"]):
            bounds = _tile_bounds_from_name(tile_name)
            lon_mins.append(bounds[0])
            lat_mins.append(bounds[1])
            lon_maxs.append(bounds[2])
            lat_maxs.append(bounds[3])
            record = (
                *bounds,
                fetch.tile_cache_path(tile_name, cache_dir=self.cache_dir),
            )
            self._tile_paths.append(record)
            self._tiles_by_degree[(math.floor(bounds[0]), math.floor(bounds[1]))] = record
        # Union des tuiles déclarées : hors de cette emprise, on borde sur le MNT
        # disponible (pas de tuile inventée — lecture au bord le plus proche).
        eps = 1e-9
        self.dem_lon_min = min(lon_mins)
        self.dem_lat_min = min(lat_mins)
        self.dem_lon_max = max(lon_maxs) - eps
        self.dem_lat_max = max(lat_maxs) - eps

    def close(self) -> None:
        for ds in self._datasets.values():
            try:
                ds.close()
            except Exception:  # noqa: BLE001
                pass
        self._datasets.clear()

    def reset_sampling_metrics(self) -> None:
        self.last_batch_metrics = {}
        self.sampling_metrics = {
            "point_count": 0,
            "tile_count": 0,
            "raster_reads": 0,
            "pixels_loaded": 0,
            "measurement_cache_hits": 0,
        }

    def clamp_lonlat(self, lon: float, lat: float) -> Tuple[float, float]:
        return (
            min(max(lon, self.dem_lon_min), self.dem_lon_max),
            min(max(lat, self.dem_lat_min), self.dem_lat_max),
        )

    def _path_for(self, lon: float, lat: float) -> Tuple[Path, float, float]:
        direct = self._tiles_by_degree.get((math.floor(lon), math.floor(lat)))
        candidates = [direct] if direct is not None else self._tile_paths
        for lon_min, lat_min, lon_max, lat_max, path in candidates:
            if lon_min <= lon < lon_max and lat_min <= lat < lat_max:
                return path, lon, lat
        best_path: Optional[Path] = None
        best_dist = float("inf")
        best_bounds: Tuple[float, float, float, float] = (0.0, 0.0, 0.0, 0.0)
        for lon_min, lat_min, lon_max, lat_max, path in self._tile_paths:
            clon = (lon_min + lon_max) / 2.0
            clat = (lat_min + lat_max) / 2.0
            dist = haversine_m(lon, lat, clon, clat)
            if dist < best_dist:
                best_dist = dist
                best_path = path
                best_bounds = (lon_min, lat_min, lon_max, lat_max)
        if best_path is None:
            raise RuntimeError(f"aucune tuile DEM pour lon={lon} lat={lat}")
        lon_min, lat_min, lon_max, lat_max = best_bounds
        eps = 1e-9
        lon = min(max(lon, lon_min), lon_max - eps)
        lat = min(max(lat, lat_min), lat_max - eps)
        return best_path, lon, lat

    def _dataset(self, path: Path):
        key = str(path)
        if key not in self._datasets:
            import rasterio

            self._datasets[key] = rasterio.open(path)
        return self._datasets[key]

    def read_many(
        self,
        points: Sequence[Tuple[float, float]],
        *,
        measurement_id: Optional[str] = None,
    ) -> List[Optional[float]]:
        """Lit un lot ordonné avec au plus une fenêtre Rasterio par tuile."""
        if not points:
            self.last_batch_metrics = {
                "point_count": 0,
                "tile_count": 0,
                "raster_reads": 0,
                "pixels_loaded": 0,
            }
            return []

        if self.measurement_table is not None and measurement_id:
            cached = self.measurement_table.get(measurement_id, len(points))
            if cached is not None:
                self.last_batch_metrics = {
                    "point_count": len(points),
                    "tile_count": 0,
                    "raster_reads": 0,
                    "pixels_loaded": 0,
                }
                self.sampling_metrics["point_count"] += len(points)
                self.sampling_metrics["measurement_cache_hits"] += len(points)
                return cached

        locations: Dict[str, List[Tuple[int, float, float]]] = {}
        paths: Dict[str, Path] = {}
        for index, (raw_lon, raw_lat) in enumerate(points):
            lon, lat = self.clamp_lonlat(float(raw_lon), float(raw_lat))
            path, lon, lat = self._path_for(lon, lat)
            key = str(path)
            paths[key] = path
            locations.setdefault(key, []).append((index, lon, lat))

        grouped: Dict[str, List[Tuple[int, int, int]]] = {}
        from rasterio.transform import rowcol

        for key, tile_points in locations.items():
            dataset = self._dataset(paths[key])
            rows, cols = rowcol(
                dataset.transform,
                [point[1] for point in tile_points],
                [point[2] for point in tile_points],
            )
            grouped[key] = [
                (point[0], int(row), int(col))
                for point, row, col in zip(tile_points, rows, cols)
            ]

        values, metrics = read_grouped_windows(
            grouped,
            lambda key: self._dataset(paths[str(key)]),
            output_size=len(points),
            masked=False,
        )
        self.last_batch_metrics = metrics
        for key, value in metrics.items():
            self.sampling_metrics[key] += int(value)
        if self.measurement_table is not None and measurement_id:
            self.measurement_table.put(measurement_id, values)
        return values

    def read_elev(self, lon: float, lat: float) -> float:
        value = self.read_many([(lon, lat)])[0]
        if value is None:
            raise RuntimeError(f"lecture DEM nodata pour {lon},{lat}")
        return value


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


def _geom_lonlat_rings(geom_xy: Any, projector: Projector) -> List[List[Tuple[float, float]]]:
    """Anneaux extérieurs lon/lat pour Polygon ou MultiPolygon."""
    rings: List[List[Tuple[float, float]]] = []
    for poly in _as_polygons(geom_xy):
        rings.append([projector.unproject_xy(x, y) for x, y in zip(*poly.exterior.coords.xy)])
    return rings

def _as_lines(geom: Any) -> List[LineString]:
    if geom is None or geom.is_empty:
        return []
    if geom.geom_type == "LineString":
        return [geom]
    if geom.geom_type == "MultiLineString":
        return list(geom.geoms)
    if geom.geom_type == "GeometryCollection":
        out: List[LineString] = []
        for g in geom.geoms:
            out.extend(_as_lines(g))
        return out
    return []


def _merge_lines(geom: Any) -> Any:
    """Fusionne des tronçons ; linemerge échoue sur une seule LineString."""
    if geom is None or geom.is_empty:
        return geom
    lines = _as_lines(geom)
    if not lines:
        return geom
    union = unary_union(lines)
    if union.geom_type == "LineString":
        return union
    try:
        return linemerge(union)
    except ValueError:
        return union


def shared_boundary(geom_a: Any, geom_b: Any) -> Any:
    inter = geom_a.boundary.intersection(geom_b.boundary)
    if inter.is_empty:
        inter = geom_a.intersection(geom_b)
    if inter.is_empty:
        return inter
    return _merge_lines(inter)


def densify_line_xy(line: LineString, step_m: float) -> List[Tuple[float, float]]:
    if line.is_empty or line.length <= 0:
        return []
    n = max(2, int(math.ceil(line.length / step_m)) + 1)
    return [
        (line.interpolate(i * line.length / (n - 1)).x, line.interpolate(i * line.length / (n - 1)).y)
        for i in range(n)
    ]


def grid_points_in_polygon(geom_xy: Any, projector: Projector) -> List[Tuple[float, float, int, int]]:
    """Points lon/lat de la grille régulière strictement dans le polygone projeté."""
    minx, miny, maxx, maxy = geom_xy.bounds
    corners = [
        projector.unproject_xy(minx, miny),
        projector.unproject_xy(maxx, miny),
        projector.unproject_xy(maxx, maxy),
        projector.unproject_xy(minx, maxy),
    ]
    lons = [c[0] for c in corners]
    lats = [c[1] for c in corners]
    lon0 = math.floor(min(lons) / G6_SAMPLE_STEP_DEG) * G6_SAMPLE_STEP_DEG
    lat0 = math.floor(min(lats) / G6_SAMPLE_STEP_DEG) * G6_SAMPLE_STEP_DEG
    lon1 = math.ceil(max(lons) / G6_SAMPLE_STEP_DEG) * G6_SAMPLE_STEP_DEG
    lat1 = math.ceil(max(lats) / G6_SAMPLE_STEP_DEG) * G6_SAMPLE_STEP_DEG

    points: List[Tuple[float, float, int, int]] = []
    j = 0
    lat = lat0
    while lat <= lat1 + 1e-12:
        i = 0
        lon = lon0
        while lon <= lon1 + 1e-12:
            x, y = projector.project_xy(lon, lat)
            if geom_xy.contains(Point(x, y)):
                points.append((lon, lat, i, j))
            i += 1
            lon = round(lon0 + i * G6_SAMPLE_STEP_DEG, 12)
        j += 1
        lat = round(lat0 + j * G6_SAMPLE_STEP_DEG, 12)
    return points


def local_m_per_deg(lat: float) -> Tuple[float, float]:
    cos_lat = max(1e-6, math.cos(math.radians(lat)))
    mx = 111_320.0 * cos_lat
    my = 111_320.0
    return mx, my


def compute_cell_relief(
    cell: dict,
    geom_xy: Any,
    projector: Projector,
    dem: DemSampler,
) -> Tuple[dict, int]:
    cid = int(cell["cell_id"])
    grid = grid_points_in_polygon(geom_xy, projector)
    elev_grid: Dict[Tuple[int, int], float] = {}
    valid_elevs: List[float] = []
    excluded = 0

    centroid = cell["centroid"]
    clon = float(centroid["lon"])
    clat = float(centroid["lat"])
    requests = [(lon, lat) for lon, lat, _i, _j in grid]
    requests.append((clon, clat))
    measured = dem.read_many(
        requests, measurement_id=f"cell:{cid}:grid_and_centroid"
    )

    for (lon, lat, i, j), elev in zip(grid, measured[:-1]):
        if elev is None:
            continue
        if elev < G6_SAMPLE_VALID_MIN_M or elev > G6_SAMPLE_VALID_MAX_M:
            excluded += 1
            continue
        elev_grid[(i, j)] = elev
        valid_elevs.append(elev)

    if not valid_elevs:
        return {
            "cell_id": cid,
            "sample_count": -1,
            "elev_mean_m": None,
            "elev_min_m": None,
            "elev_max_m": None,
            "centroid_elev_m": None,
            "slope_mean_deg": None,
            "roughness_m": None,
        }, excluded

    slopes: List[float] = []
    for lon, lat, i, j in grid:
        if (i, j) not in elev_grid:
            continue
        z = elev_grid[(i, j)]
        zxp = elev_grid.get((i + 1, j))
        zxm = elev_grid.get((i - 1, j))
        zyp = elev_grid.get((i, j + 1))
        zym = elev_grid.get((i, j - 1))
        if zxp is None or zxm is None or zyp is None or zym is None:
            continue
        mx, my = local_m_per_deg(lat)
        dzdx = (zxp - zxm) / (2 * G6_SAMPLE_STEP_DEG * mx)
        dzdy = (zyp - zym) / (2 * G6_SAMPLE_STEP_DEG * my)
        slope = math.degrees(math.atan(math.sqrt(dzdx * dzdx + dzdy * dzdy)))
        slopes.append(slope)

    centroid_raw = measured[-1]
    if centroid_raw is None:
        return {
            "cell_id": cid,
            "sample_count": -1,
            "elev_mean_m": None,
            "elev_min_m": None,
            "elev_max_m": None,
            "centroid_elev_m": None,
            "slope_mean_deg": None,
            "roughness_m": None,
        }, excluded
    centroid_elev = round_float(centroid_raw, G6_ELEV_DECIMALS)

    mean_e = statistics.mean(valid_elevs)
    pop_std = statistics.pstdev(valid_elevs) if len(valid_elevs) > 1 else 0.0
    slope_mean = statistics.mean(slopes) if slopes else 0.0

    return {
        "cell_id": cid,
        "sample_count": len(valid_elevs),
        "elev_mean_m": round_float(mean_e, G6_ELEV_DECIMALS),
        "elev_min_m": round_float(min(valid_elevs), G6_ELEV_DECIMALS),
        "elev_max_m": round_float(max(valid_elevs), G6_ELEV_DECIMALS),
        "centroid_elev_m": centroid_elev,
        "slope_mean_deg": round_float(slope_mean, G6_SLOPE_DECIMALS),
        "roughness_m": round_float(pop_std, G6_ROUGH_DECIMALS),
    }, excluded


def match_known_pass(lon: float, lat: float) -> Tuple[Optional[str], Optional[str]]:
    best_id: Optional[str] = None
    best_name: Optional[str] = None
    best_dist = float("inf")
    for pid, pname, plon, plat in G6_KNOWN_PASSES:
        d = haversine_m(lon, lat, plon, plat)
        if d < best_dist or (d == best_dist and (best_id is None or pid < best_id)):
            best_dist = d
            best_id = pid
            best_name = pname
    if best_dist <= G6_KNOWN_PASS_MATCH_M:
        return best_id, best_name
    return None, None


def derive_barriers_and_passes(
    adjacency_g5: Sequence[dict],
    cell_geoms: Dict[int, Any],
    cell_relief: Dict[int, dict],
    projector: Projector,
    dem: DemSampler,
) -> Tuple[List[dict], List[dict], int]:
    enriched: List[dict] = []
    passes: List[dict] = []
    barrier_count = 0

    for edge in adjacency_g5:
        out = dict(edge)
        if edge.get("kind") != "land-land":
            enriched.append(out)
            continue

        a, b = int(edge["a"]), int(edge["b"])
        ga, gb = cell_geoms.get(a), cell_geoms.get(b)
        if ga is None or gb is None:
            enriched.append(out)
            continue

        boundary = shared_boundary(ga, gb)
        if boundary.is_empty:
            raise RuntimeError(f"frontiere vide pour arete {a}-{b} malgre adjacence")

        lines = _as_lines(boundary) or ([boundary] if boundary.geom_type == "LineString" else [])
        sample_points: List[Tuple[float, float]] = []
        for line in lines:
            if not isinstance(line, LineString):
                continue
            for x, y in densify_line_xy(line, G6_EDGE_SAMPLE_STEP_M):
                lon, lat = projector.unproject_xy(x, y)
                sample_points.append((lon, lat))

        elevations = dem.read_many(
            sample_points, measurement_id=f"edge:{a}:{b}:frontier"
        )
        samples_lonlat = [
            (lon, lat, elev)
            for (lon, lat), elev in zip(sample_points, elevations)
            if elev is not None
        ]

        if not samples_lonlat:
            enriched.append(out)
            continue

        crossing_lon, crossing_lat, crossing_elev = min(samples_lonlat, key=lambda t: t[2])
        crossing_elev = round_float(crossing_elev, G6_ELEV_DECIMALS)
        ca = float(cell_relief[a]["centroid_elev_m"])
        cb = float(cell_relief[b]["centroid_elev_m"])
        is_barrier = crossing_elev > ca and crossing_elev > cb

        if is_barrier:
            barrier_count += 1
            pid, pname = match_known_pass(crossing_lon, crossing_lat)
            if pid is None:
                lo, hi = sorted((a, b))
                pid = f"g6_derived_{lo}_{hi}"
                pname = None
            out["relief_barrier"] = True
            out["crossing_elev_m"] = crossing_elev
            out["crossing_lon"] = round_float(crossing_lon, 6)
            out["crossing_lat"] = round_float(crossing_lat, 6)
            out["pass_id"] = pid
            passes.append(
                {
                    "pass_id": pid,
                    "nom": pname,
                    "edge_a": a,
                    "edge_b": b,
                    "lon": round_float(crossing_lon, 6),
                    "lat": round_float(crossing_lat, 6),
                    "elev_m": crossing_elev,
                }
            )

        enriched.append(out)

    passes.sort(key=lambda p: (p["pass_id"], p["edge_a"], p["edge_b"]))
    return enriched, passes, barrier_count


def compute_stats(
    cells_g3: Sequence[dict],
    cell_relief: Sequence[dict],
    barrier_count: int,
    passes: Sequence[dict],
    excluded_total: int,
) -> dict:
    by_id = {int(c["cell_id"]): c for c in cells_g3}
    means = [float(c["elev_mean_m"]) for c in cell_relief if c.get("elev_mean_m") is not None]
    below_0 = sum(
        by_id[int(c["cell_id"])]["area_km2"]
        for c in cell_relief
        if c.get("elev_mean_m") is not None and float(c["elev_mean_m"]) < 0
    )
    named_ids = {p["pass_id"] for p in passes if p.get("nom")}
    known_ids = {pid for pid, _, _, _ in G6_KNOWN_PASSES}
    return {
        "cell_count": len(cell_relief),
        "elev_distribution": {
            "min": round_float(min(means), G6_ELEV_DECIMALS) if means else None,
            "max": round_float(max(means), G6_ELEV_DECIMALS) if means else None,
            "mean": round_float(statistics.mean(means), G6_ELEV_DECIMALS) if means else None,
            "median": round_float(statistics.median(means), G6_ELEV_DECIMALS) if means else None,
        },
        "barrier_count": barrier_count,
        "pass_count": len(passes),
        "passes_nommes_trouves": len(named_ids & known_ids),
        "below_0_land_km2": round_float(below_0, 3),
        "echantillons_exclus_hors_plage": excluded_total,
    }


def load_context(*, verify_dem: bool = True, download_dem: bool = False) -> dict:
    dem_report: dict = {}
    if verify_dem:
        ok, detail, dem_report = verify_dem_fingerprint(download=download_dem)
        if not ok:
            raise RuntimeError(f"DEM non verifie avant lecture: {detail}")

    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    adj_doc = read_json(ARTIFACTS / "adjacency_g5.json")
    cells = cells_doc["cells"]
    adjacency_g5 = adj_doc["adjacency"]

    projector = Projector(detect_projection())
    cell_geoms: Dict[int, Any] = {}
    for cell in cells:
        geom = shape(cell["geometry"])
        if not geom.is_valid:
            geom = geom.buffer(0)
        cell_geoms[int(cell["cell_id"])] = geom

    fetch = _load_fetch_dem()
    effective_cache = Path(dem_report.get("cache_dir", fetch.CACHE_DIR))
    table_key, table_inputs = measurement_table_key(
        sources_lock=LOCK_PATH,
        cells=ARTIFACTS / "cells_g3.json",
        adjacency=ARTIFACTS / "adjacency_g5.json",
        sampling_code=Path(__file__),
        sample_step=G6_SAMPLE_STEP_DEG,
    )
    measurement_table = MeasurementTable(effective_cache, table_key, table_inputs)
    dem = DemSampler(
        effective_cache,
        lock_path=LOCK_PATH,
        measurement_table=measurement_table,
    )

    return {
        "cells": cells,
        "adjacency_g5": adjacency_g5,
        "cell_geoms": cell_geoms,
        "projector": projector,
        "dem": dem,
        "dem_report": dem_report,
        "measurement_table_key": table_key,
        "measurement_table_inputs": table_inputs,
        "crs": crs_declaration(has_geometry_lonlat=False),
    }


def derive_relief(context: dict) -> dict:
    projector: Projector = context["projector"]
    dem: DemSampler = context["dem"]
    dem.reset_sampling_metrics()
    excluded_total = 0
    cell_relief: List[dict] = []

    for cell in sorted(context["cells"], key=lambda c: int(c["cell_id"])):
        cid = int(cell["cell_id"])
        rec, excl = compute_cell_relief(cell, context["cell_geoms"][cid], projector, dem)
        excluded_total += excl
        cell_relief.append(rec)

    relief_by_id = {int(c["cell_id"]): c for c in cell_relief}
    adjacency_g6, passes, barrier_count = derive_barriers_and_passes(
        context["adjacency_g5"],
        context["cell_geoms"],
        relief_by_id,
        projector,
        dem,
    )
    metrics = compute_stats(
        context["cells"], cell_relief, barrier_count, passes, excluded_total
    )
    if dem.measurement_table is not None:
        dem.measurement_table.save()

    return {
        "cell_relief": cell_relief,
        "adjacency_g6": adjacency_g6,
        "passes": passes,
        "metrics": metrics,
        "relief_by_id": relief_by_id,
    }


def export_g6(derived: dict, context: dict) -> Dict[str, str]:
    shas: Dict[str, str] = {}
    shas["artifacts/cells_relief_g6.json"] = write_json(
        ARTIFACTS / "cells_relief_g6.json",
        {
            "cells": derived["cell_relief"],
            "pipeline_version": G6_PIPELINE_VERSION,
            "projection": context["projector"].info.epsg,
            "crs": context["crs"],
        },
        float_decimals=G6_ELEV_DECIMALS,
    )
    shas["artifacts/adjacency_g6.json"] = write_json(
        ARTIFACTS / "adjacency_g6.json",
        {
            "adjacency": derived["adjacency_g6"],
            "pipeline_version": G6_PIPELINE_VERSION,
            "projection": context["projector"].info.epsg,
        },
    )
    shas["artifacts/passes_g6.json"] = write_json(
        ARTIFACTS / "passes_g6.json",
        {
            "passes": derived["passes"],
            "pipeline_version": G6_PIPELINE_VERSION,
        },
    )
    shas["artifacts/stats_g6.json"] = write_json(
        ARTIFACTS / "stats_g6.json",
        {
            **derived["metrics"],
            "pipeline_version": G6_PIPELINE_VERSION,
        },
        float_decimals=G6_ELEV_DECIMALS,
    )

    lock = read_json(LOCK_PATH)
    fetch = _load_fetch_dem()
    manifest = {
        "pipeline_version": G6_PIPELINE_VERSION,
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "projection": {
            "epsg": context["projector"].info.epsg,
            "fallback": context["projector"].info.fallback,
            "reason": context["projector"].info.reason,
        },
        "inputs": {
            "cells_g3.json": sha256_file(ARTIFACTS / "cells_g3.json"),
            "adjacency_g5.json": sha256_file(ARTIFACTS / "adjacency_g5.json"),
            "sources.lock": sha256_file(LOCK_PATH),
            "dem_collective": lock["dem"]["collective_sha256"],
        },
        "outputs": {
            "cells_relief_g6.json": shas["artifacts/cells_relief_g6.json"],
            "adjacency_g6.json": shas["artifacts/adjacency_g6.json"],
            "passes_g6.json": shas["artifacts/passes_g6.json"],
            "stats_g6.json": shas["artifacts/stats_g6.json"],
        },
    }
    shas["artifacts/MANIFEST_g6.json"] = write_json(
        ARTIFACTS / "MANIFEST_g6.json", manifest
    )

    registry = {
        "created": G6_REGISTRY_CREATED,
        "pipeline_version": G6_PIPELINE_VERSION,
        "cell_count": len(derived["cell_relief"]),
        "cells": [
            {
                "cell_id": int(c["cell_id"]),
                "elev_mean_m": c.get("elev_mean_m"),
                "sample_count": c.get("sample_count"),
            }
            for c in derived["cell_relief"]
        ],
    }
    shas["registry/relief_registry.json"] = write_json(
        REGISTRY / "relief_registry.json", registry, float_decimals=G6_ELEV_DECIMALS
    )
    return shas


def write_captures(derived: dict, context: dict) -> Dict[str, Path]:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.patches import Polygon as MplPolygon

    CAPTURE.mkdir(parents=True, exist_ok=True)
    projector: Projector = context["projector"]
    relief_by_id = derived["relief_by_id"]
    paths: Dict[str, Path] = {}

    w, s, e, n = PILOT_WINDOW_LONLAT

    # --- fenêtre pilote : altitude moyenne par cellule ---
    fig, ax = plt.subplots(figsize=(12, 10), dpi=120)
    ax.set_aspect("equal")
    ax.set_facecolor("#e3f2fd")
    ax.set_title("G6 — altitude moyenne par cellule (m)")
    ax.set_xlim(w, e)
    ax.set_ylim(s, n)
    patches = []
    values = []
    for cell in context["cells"]:
        cid = int(cell["cell_id"])
        geom = context["cell_geoms"][cid]
        for lonlat in _geom_lonlat_rings(geom, projector):
            patches.append(MplPolygon(lonlat, closed=True))
            values.append(float(relief_by_id[cid].get("elev_mean_m") or 0))
    coll = PatchCollection(patches, cmap="terrain", edgecolor="#555555", linewidth=0.15)
    coll.set_array(np.array(values))
    ax.add_collection(coll)
    fig.colorbar(coll, ax=ax, shrink=0.7, label="elev_mean_m")
    ax.grid(True, alpha=0.2)
    path = CAPTURE / "v1_052_elevation_window.png"
    fig.savefig(path, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["elevation_window"] = path

    # --- zoom Pyrénées / Alpes : barrières et cols ---
    fig, ax = plt.subplots(figsize=(10, 8), dpi=120)
    ax.set_aspect("equal")
    ax.set_facecolor("#f5f5f5")
    ax.set_title("G6 — barrières (rouge) et cols (nommés=etoile, derives=croix)")
    ax.set_xlim(-2.0, 8.5)
    ax.set_ylim(42.0, 47.5)
    for cell in context["cells"]:
        cid = int(cell["cell_id"])
        c = cell["centroid"]
        lon, lat = float(c["lon"]), float(c["lat"])
        if not (-2.0 <= lon <= 8.5 and 42.0 <= lat <= 47.5):
            continue
        geom = context["cell_geoms"][cid]
        for lonlat in _geom_lonlat_rings(geom, projector):
            ax.add_patch(
                MplPolygon(
                    lonlat,
                    closed=True,
                    facecolor="#eceff1",
                    edgecolor="#90a4ae",
                    linewidth=0.2,
                )
            )
    cells_by_id = {int(c["cell_id"]): c for c in context["cells"]}
    for edge in derived["adjacency_g6"]:
        if not edge.get("relief_barrier"):
            continue
        a, b = int(edge["a"]), int(edge["b"])
        ca = cells_by_id[a]
        cb = cells_by_id[b]
        lon1, lat1 = float(ca["centroid"]["lon"]), float(ca["centroid"]["lat"])
        lon2, lat2 = float(cb["centroid"]["lon"]), float(cb["centroid"]["lat"])
        ax.plot([lon1, lon2], [lat1, lat2], color="#c62828", linewidth=1.5, alpha=0.85)
        plon = float(edge["crossing_lon"])
        plat = float(edge["crossing_lat"])
        if edge.get("pass_id", "").startswith("g6_derived_"):
            ax.plot(plon, plat, marker="x", color="#1565c0", markersize=7)
        else:
            ax.plot(plon, plat, marker="*", color="#2e7d32", markersize=10)
    ax.grid(True, alpha=0.2)
    path2 = CAPTURE / "v1_052_barriers_passes.png"
    fig.savefig(path2, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["barriers_passes"] = path2
    return paths


def run_relief(
    *,
    context: Optional[dict] = None,
    export: bool = True,
    captures: bool = True,
    verify_dem: bool = True,
    download_dem: bool = False,
) -> Dict[str, Any]:
    """Dérive relief + export ; sans argument requis (contrat pipeline.py)."""
    t_all = time.perf_counter()
    ctx = context or load_context(verify_dem=verify_dem, download_dem=download_dem)
    derived = derive_relief(ctx)

    shas: Dict[str, str] = {}
    if export:
        shas = export_g6(derived, ctx)

    capture_paths: Dict[str, Path] = {}
    if captures:
        capture_paths = write_captures(derived, ctx)

    ctx["dem"].close()

    return {
        "context": ctx,
        "cell_relief": derived["cell_relief"],
        "adjacency": derived["adjacency_g6"],
        "passes": derived["passes"],
        "metrics": derived["metrics"],
        "projection": ctx["projector"].info,
        "captures": {k: str(v) for k, v in capture_paths.items()},
        "shas": shas,
        "dem_ok": bool(ctx.get("dem_report", {}).get("ok", True)),
        "dem_detail": (
            f"verified={ctx.get('dem_report', {}).get('verified')}/"
            f"{ctx.get('dem_report', {}).get('tile_count')}"
            if ctx.get("dem_report")
            else "ok"
        ),
        "sampling": dict(ctx["dem"].sampling_metrics),
        "measurement_table": (
            {
                "key": ctx.get("measurement_table_key"),
                "path": str(ctx["dem"].measurement_table.data_path),
                "cache_hits": ctx["dem"].measurement_table.cache_hits,
                "cache_misses": ctx["dem"].measurement_table.cache_misses,
                "writes": ctx["dem"].measurement_table.writes,
            }
            if ctx["dem"].measurement_table is not None
            else None
        ),
        "elapsed_s": time.perf_counter() - t_all,
    }


def main() -> int:
    result = run_relief(download_dem=False)
    m = result["metrics"]
    print(
        f"pipeline g6 | projection={result['projection'].epsg} | "
        f"cells={m['cell_count']} | elev_med={m['elev_distribution']['median']} | "
        f"barriers={m['barrier_count']} passes={m['pass_count']} | "
        f"below_0_km2={m['below_0_land_km2']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
