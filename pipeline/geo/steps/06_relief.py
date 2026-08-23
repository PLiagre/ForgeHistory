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
from collections import OrderedDict
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Set, Tuple

import numpy as np
from shapely.geometry import LineString, Point, mapping, shape
from shapely.ops import linemerge, unary_union

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    A12_RELIEF_MUST_BE_HIGH,
    A12_RELIEF_MUST_BE_LOW,
    A12_RELIEF_ZONES,
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
REQUIRED_TILES_PATH = ARTIFACTS / "dem_required_tiles_g6.json"

EARTH_RADIUS_M = 6_371_000.0
DATASET_CACHE_MAX = 48


class DemCounters:
    def __init__(self) -> None:
        self.points_grille = 0
        self.points_centroides = 0
        self.points_frontieres = 0
        self.echantillons_nodata_raster = 0
        self.echantillons_hors_couverture_dem = 0
        self.tuiles_sans_valeur_nodata_declaree = 0
        self.tiles_missing_nodata_checked: Set[str] = set()
        self.lectures_hors_bornes_du_fichier = 0
        self.echantillons_valeur_zero_exact = 0
        self.points_sur_ligne_degre_grille = 0
        self.points_sur_ligne_degre_centroides = 0
        self.points_sur_ligne_degre_frontieres = 0
        self.cell_zero_readings: List[dict] = []
        self.points_de_bord_multi_tuiles = 0
        self.points_de_bord_valeurs_concordantes = 0
        self.cells_with_raw_zero_sample: Set[int] = set()

    def public_ints(self) -> Dict[str, int]:
        return {
            "points_grille": self.points_grille,
            "points_centroides": self.points_centroides,
            "points_frontieres": self.points_frontieres,
            "echantillons_nodata_raster": self.echantillons_nodata_raster,
            "echantillons_hors_couverture_dem": self.echantillons_hors_couverture_dem,
            "tuiles_sans_valeur_nodata_declaree": self.tuiles_sans_valeur_nodata_declaree,
            "lectures_hors_bornes_du_fichier": self.lectures_hors_bornes_du_fichier,
            "echantillons_valeur_zero_exact": self.echantillons_valeur_zero_exact,
            "points_sur_ligne_degre_grille": self.points_sur_ligne_degre_grille,
            "points_sur_ligne_degre_centroides": self.points_sur_ligne_degre_centroides,
            "points_sur_ligne_degre_frontieres": self.points_sur_ligne_degre_frontieres,
            "points_de_bord_multi_tuiles": self.points_de_bord_multi_tuiles,
            "points_de_bord_valeurs_concordantes": self.points_de_bord_valeurs_concordantes,
        }

    def apply_int_delta(self, delta: Dict[str, int]) -> None:
        for name, value in delta.items():
            setattr(self, name, getattr(self, name) + int(value))


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
    """Retourne (lon_min, lat_min, lon_max, lat_max) pour une tuile Copernicus (D16)."""
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
        lat_min, lat_max = float(-lat_deg), float(-lat_deg + 1)
    if lon_hem == "E":
        lon_min, lon_max = float(lon_deg), float(lon_deg + 1)
    else:
        lon_min, lon_max = float(-lon_deg), float(-lon_deg + 1)
    return lon_min, lat_min, lon_max, lat_max


def _format_tile_name(lon_i: int, lat_i: int) -> str:
    if lat_i >= 0:
        lat_str = f"N{lat_i:02d}_00"
    else:
        lat_str = f"S{abs(lat_i):03d}_00"
    if lon_i >= 0:
        lon_str = f"E{lon_i:03d}_00"
    else:
        lon_str = f"W{abs(lon_i):03d}_00"
    return f"Copernicus_DSM_COG_30_{lat_str}_{lon_str}_DEM.tif"


def lonlat_to_tile_name(lon: float, lat: float, half_pixel_deg: float = 0.0) -> str:
    """Tuile canonique D19 : plancher(lon), plafond(lat) − 1.

    Avec registrement pixel_point (D22), les points dans la bande sud d'un
    demi-pixel sous chaque ligne de degré sont attribués à la tuile du sud ;
    ceux dans la bande est d'un demi-pixel avant chaque méridien sont attribués
    à la tuile de l'est — sans repli ni recherche de voisines.
    """
    lon_i = math.floor(lon)
    lat_i = math.ceil(lat) - 1
    if half_pixel_deg > 0.0:
        if lat <= lat_i + half_pixel_deg + 1e-12:
            lat_i -= 1
        lon_east_edge = lon_i + 1
        if lon >= lon_east_edge - half_pixel_deg - 1e-12 and lon < lon_east_edge:
            lon_i += 1
    return _format_tile_name(lon_i, lat_i)


def lonlat_to_tile_name_nominal(lon: float, lat: float) -> str:
    """Ancienne règle (carré nominal) — comparaison D21 uniquement."""
    lon_i = math.floor(lon)
    lat_i = math.floor(lat)
    return _format_tile_name(lon_i, lat_i)


def is_degree_line_point(lon: float, lat: float, tol: float = 1e-9) -> bool:
    return abs(lon - round(lon)) <= tol or abs(lat - round(lat)) <= tol


def measure_tile_registration(path: Path) -> Tuple[str, float, float]:
    """Retourne (nom_registrement, demi_pixel_lon, demi_pixel_lat) depuis l'en-tête COG."""
    import rasterio

    with rasterio.open(path) as ds:
        res_x = abs(float(ds.transform.a))
        res_y = abs(float(ds.transform.e))
        half_px_lon = 0.5 * res_x
        half_px_lat = 0.5 * res_y
        w, h = ds.width, ds.height
        extent_lon = w * res_x
        extent_lat = h * res_y
        b = ds.bounds
        on_degrees = (
            abs(b.left - round(b.left)) < 1e-5
            and abs(b.bottom - round(b.bottom)) < 1e-5
            and abs(b.right - round(b.right)) < 1e-5
            and abs(b.top - round(b.top)) < 1e-5
        )
        if (
            on_degrees
            and abs(extent_lon - 1.0) < 1e-4
            and abs(extent_lat - 1.0) < 1e-4
        ):
            return "pixel_surface", half_px_lon, half_px_lat
        if abs(extent_lon - 1.0) < res_x and abs(extent_lat - 1.0) < res_y:
            return "pixel_point", half_px_lon, half_px_lat
        raise RuntimeError(
            f"registrement inconnu pour {path.name}: "
            f"bornes={b} extent=({extent_lon},{extent_lat}) res=({res_x},{res_y})"
        )


def bounds_tolerance_for_registration(
    reg_name: str, half_px_lon: float, half_px_lat: float
) -> Tuple[float, float]:
    if reg_name == "pixel_surface":
        return 1e-6, 1e-6
    return half_px_lon, half_px_lat


def pixel_indices_in_bounds(ds, lon: float, lat: float) -> Tuple[int, int]:
    row, col = ds.index(lon, lat)
    return int(row), int(col)


def verify_tile_domain_rule(
    tile_name: str, path: Path, half_pixel_deg: float = 0.0
) -> bool:
    """D19 : la règle coïncide avec le domaine indexable du fichier."""
    import rasterio

    lon_min, lat_min, lon_max, lat_max = _tile_bounds_from_name(tile_name)
    eps = 1e-6
    # Domaine indexable D19 : [lon_min, lon_max) x (lat_min, lat_max] (sud ouvert).
    test_points = [
        ((lon_min + lon_max) / 2, (lat_min + lat_max) / 2),
        (lon_min + 0.25, lat_min + 0.25),
        (lon_min + eps, lat_max - eps),
        (lon_min, lat_max),
        (lon_max - 0.25, lat_max - 0.25),
    ]
    tested = 0
    with rasterio.open(path) as ds:
        for lon, lat in test_points:
            if lonlat_to_tile_name(lon, lat, half_pixel_deg) != tile_name:
                continue
            tested += 1
            row, col = pixel_indices_in_bounds(ds, lon, lat)
            if not (0 <= row < ds.height and 0 <= col < ds.width):
                return False
        # Latitude entière : la tuile nord nommée ne doit pas indexer le point.
        if lat_max == int(lat_max) and lat_max > lat_min:
            lat_int = float(int(lat_max))
            lon_mid = (lon_min + lon_max) / 2
            north_name = _format_tile_name(
                int(math.floor(lon_mid)), int(math.floor(lat_int))
            )
            if north_name == tile_name:
                row_n, col_n = pixel_indices_in_bounds(ds, lon_mid, lat_int)
                if 0 <= row_n < ds.height and 0 <= col_n < ds.width:
                    return False
            south_name = lonlat_to_tile_name(lon_mid, lat_int, half_pixel_deg)
            if south_name == tile_name:
                row_s, col_s = pixel_indices_in_bounds(ds, lon_mid, lat_int)
                if row_s != 0:
                    return False
    return tested > 0


def verify_all_tiles_domain_rule(
    tile_names: Sequence[str],
    cache_dir: Path,
    tile_path_fn,
    half_pixel_deg: float = 0.0,
) -> Tuple[int, int, str, float]:
    """Retourne (conformes, total, registrement_nom, demi_pixel_lon_echantillon)."""
    reg_name: Optional[str] = None
    half_px_sample = 0.0
    conformes = 0
    for tile_name in sorted(tile_names):
        path = tile_path_fn(tile_name)
        if not path.is_file():
            raise RuntimeError(f"tuile absente du cache: {tile_name}")
        rname, hp_lon, hp_lat = measure_tile_registration(path)
        if reg_name is None:
            reg_name, half_px_sample = rname, hp_lon
        elif rname != reg_name:
            raise RuntimeError(
                f"registrement heterogene: {tile_name}={rname} attendu={reg_name}"
            )
        if verify_tile_domain_rule(tile_name, path, half_pixel_deg):
            conformes += 1
    return conformes, len(tile_names), reg_name or "inconnu", half_px_sample


def load_required_tile_set() -> Set[str]:
    if not REQUIRED_TILES_PATH.is_file():
        raise RuntimeError(
            f"artefact manquant {REQUIRED_TILES_PATH} — lancer required_dem_tiles.py"
        )
    doc = read_json(REQUIRED_TILES_PATH)
    return set(doc["tuiles_requises"])


def assert_dem_coverage(required: Set[str], lock_tiles: Set[str]) -> None:
    missing = sorted(required - lock_tiles)
    if missing:
        preview = ", ".join(missing[:8])
        suffix = "..." if len(missing) > 8 else ""
        raise RuntimeError(
            f"couverture DEM incomplete avant lecture: {len(missing)} tuile(s) "
            f"requise(s) absente(s) du bloc dem: {preview}{suffix}"
        )


def _ordered_points_key(points: Sequence[Tuple[float, float]]) -> str:
    import hashlib
    import json

    payload = json.dumps(
        [[round(float(lon), 12), round(float(lat), 12)] for lon, lat in points],
        ensure_ascii=True,
        separators=(",", ":"),
    )
    return hashlib.sha256(payload.encode("ascii")).hexdigest()


class DemSampler:
    """Lecteur MNT groupé par tuile, LRU 48, règles D19–D23."""

    def __init__(
        self,
        cache_dir: Path,
        lock_tiles: Optional[Set[str]] = None,
        required: Optional[Set[str]] = None,
        half_pixel_deg: float = 0.0,
        *,
        lock_path: Path = LOCK_PATH,
        measurement_table: Optional[MeasurementTable] = None,
    ) -> None:
        self.cache_dir = Path(cache_dir)
        self.lock_path = Path(lock_path)
        self.measurement_table = measurement_table
        self.half_pixel_deg = half_pixel_deg
        self.counters = DemCounters()
        self.last_batch_metrics: Dict[str, int] = {}
        self.sampling_metrics: Dict[str, int] = {}
        self.reset_sampling_metrics()
        if lock_tiles is None:
            lock = read_json(self.lock_path)
            lock_tiles = set(lock["dem"]["tiles"])
        self.lock_tiles = set(lock_tiles)
        self.required = set(required) if required is not None else set(self.lock_tiles)
        fetch = _load_fetch_dem()
        self._tile_paths: Dict[str, Tuple[float, float, float, float, Path]] = {}
        self._tile_half_px: Dict[str, Tuple[float, float]] = {}
        self._tile_registration: Dict[str, str] = {}
        for tile_name in sorted(self.lock_tiles):
            bounds = _tile_bounds_from_name(tile_name)
            path = fetch.tile_cache_path(tile_name, cache_dir=self.cache_dir)
            self._tile_paths[tile_name] = (*bounds, path)
            if path.is_file():
                reg, hp_lon, hp_lat = measure_tile_registration(path)
                self._tile_registration[tile_name] = reg
                # D22 : la bande demi-pixel ne s'applique qu'au registrement point.
                if reg == "pixel_point":
                    self._tile_half_px[tile_name] = (hp_lon, hp_lat)
        self._datasets: OrderedDict[str, Any] = OrderedDict()
        self._nodata_by_tile: Dict[str, Optional[float]] = {}

    def reset_sampling_metrics(self) -> None:
        self.last_batch_metrics = {}
        self.sampling_metrics = {
            "point_count": 0,
            "tile_count": 0,
            "raster_reads": 0,
            "pixels_loaded": 0,
            "measurement_cache_hits": 0,
        }

    def close(self) -> None:
        for ds in self._datasets.values():
            try:
                ds.close()
            except Exception:  # noqa: BLE001
                pass
        self._datasets.clear()

    def _dataset(self, tile_name: str):
        if tile_name in self._datasets:
            self._datasets.move_to_end(tile_name)
            return self._datasets[tile_name]
        if tile_name not in self._tile_paths:
            raise RuntimeError(f"tuile {tile_name} absente du bloc dem")
        path = self._tile_paths[tile_name][4]
        import rasterio

        ds = rasterio.open(path)
        if tile_name not in self._nodata_by_tile:
            nodata = ds.nodata
            if nodata is None and hasattr(ds, "nodatavals"):
                vals = [v for v in ds.nodatavals if v is not None]
                nodata = vals[0] if len(vals) == 1 else None
            self._nodata_by_tile[tile_name] = nodata
            if nodata is None and tile_name not in self.counters.tiles_missing_nodata_checked:
                self.counters.tuiles_sans_valeur_nodata_declaree += 1
                self.counters.tiles_missing_nodata_checked.add(tile_name)
        while len(self._datasets) >= DATASET_CACHE_MAX:
            _, old = self._datasets.popitem(last=False)
            old.close()
        self._datasets[tile_name] = ds
        return ds

    def _resolve_tile_name(self, lon: float, lat: float) -> str:
        """Attribution D19 + bandes demi-pixel mesurées par tuile (D22)."""
        lon_i = math.floor(lon)
        lat_i = math.ceil(lat) - 1
        south_name = _format_tile_name(lon_i, lat_i - 1)
        hp_lat_s = self._tile_half_px.get(south_name, (self.half_pixel_deg, self.half_pixel_deg))[1]
        if hp_lat_s > 0.0 and lat <= lat_i + hp_lat_s + 1e-12:
            lat_i -= 1
        lon_base = lon_i
        tile = _format_tile_name(lon_i, lat_i)
        hp_lon = self._tile_half_px.get(tile, (self.half_pixel_deg, self.half_pixel_deg))[0]
        lon_east = lon_base + 1
        if hp_lon > 0.0 and lon >= lon_east - hp_lon - 1e-12 and lon < lon_east:
            lon_i += 1
            tile = _format_tile_name(lon_i, lat_i)
        return tile

    def _tile_for(self, lon: float, lat: float) -> str:
        tile_name = self._resolve_tile_name(lon, lat)
        if tile_name not in self._tile_paths:
            raise RuntimeError(
                f"hors couverture DEM: lon={lon} lat={lat} tuile_necessaire={tile_name}"
            )
        return tile_name

    def _candidate_tiles_for_point(self, lon: float, lat: float) -> List[str]:
        """Tuiles candidates susceptibles d'indexer le point (voisinage 3×3 + tuile nominale nord)."""
        lon_i = math.floor(lon)
        lat_i = math.ceil(lat) - 1
        south_name = _format_tile_name(lon_i, lat_i - 1)
        hp_lat_s = self._tile_half_px.get(south_name, (self.half_pixel_deg, self.half_pixel_deg))[1]
        if hp_lat_s > 0.0 and lat <= lat_i + hp_lat_s + 1e-12:
            lat_i -= 1
        lon_base = lon_i
        tile = _format_tile_name(lon_i, lat_i)
        hp_lon = self._tile_half_px.get(tile, (self.half_pixel_deg, self.half_pixel_deg))[0]
        lon_east = lon_base + 1
        if hp_lon > 0.0 and lon >= lon_east - hp_lon - 1e-12 and lon < lon_east:
            lon_i += 1
        names: Set[str] = set()
        for dlon in (-1, 0, 1):
            for dlat in (-1, 0, 1):
                names.add(_format_tile_name(lon_i + dlon, lat_i + dlat))
        if is_degree_line_point(lon, lat):
            names.add(_format_tile_name(lon_i, math.floor(lat)))
        return sorted(n for n in names if n in self.lock_tiles)

    def _indexing_tiles(
        self, lon: float, lat: float
    ) -> List[Tuple[str, int, int, Optional[float]]]:
        """Tuiles du lock qui indexent réellement le point (indices dans le tableau)."""
        hits: List[Tuple[str, int, int, Optional[float]]] = []
        for tile_name in self._candidate_tiles_for_point(lon, lat):
            row, col, raw, ok = self._read_pixel(tile_name, lon, lat)
            if ok:
                hits.append((tile_name, row, col, raw))
        return hits

    def _compare_border_tiles(self, lon: float, lat: float) -> None:
        """D19 : concorde des valeurs quand plusieurs tuiles indexent le point."""
        hits = self._indexing_tiles(lon, lat)
        if len(hits) <= 1:
            return
        values = [
            round(float(v), 4)
            for _t, _r, _c, v in hits
            if v is not None
        ]
        if not values:
            return
        self.counters.points_de_bord_multi_tuiles += 1
        if len(set(values)) == 1:
            self.counters.points_de_bord_valeurs_concordantes += 1

    def _read_pixel(
        self, tile_name: str, lon: float, lat: float
    ) -> Tuple[int, int, Optional[float], bool]:
        ds = self._dataset(tile_name)
        row, col = pixel_indices_in_bounds(ds, lon, lat)
        in_bounds = 0 <= row < ds.height and 0 <= col < ds.width
        if not in_bounds:
            return row, col, None, False
        import numpy.ma as ma
        import rasterio

        window = rasterio.windows.Window(col, row, 1, 1)
        data = ds.read(1, window=window, masked=True)
        if ma.is_masked(data[0, 0]):
            return row, col, None, True
        return row, col, float(data[0, 0]), True

    def _count_family(self, lon: float, lat: float, family: str) -> None:
        if family == "grille":
            self.counters.points_grille += 1
            if is_degree_line_point(lon, lat):
                self.counters.points_sur_ligne_degre_grille += 1
        elif family == "centroide":
            self.counters.points_centroides += 1
            if is_degree_line_point(lon, lat):
                self.counters.points_sur_ligne_degre_centroides += 1
        elif family == "frontiere":
            self.counters.points_frontieres += 1
            if is_degree_line_point(lon, lat):
                self.counters.points_sur_ligne_degre_frontieres += 1

    def _record_value(
        self,
        lon: float,
        lat: float,
        tile_name: str,
        row: int,
        col: int,
        raw: Optional[float],
        cell_id: Optional[int],
    ) -> Optional[float]:
        if raw is None:
            self.counters.echantillons_nodata_raster += 1
            return None
        nodata = self._nodata_by_tile.get(tile_name)
        if nodata is not None and raw == float(nodata):
            self.counters.echantillons_nodata_raster += 1
            return None
        if raw == 0.0:
            self.counters.echantillons_valeur_zero_exact += 1
            if cell_id is not None:
                cid = int(cell_id)
                self.counters.cells_with_raw_zero_sample.add(cid)
                self.counters.cell_zero_readings.append(
                    {
                        "cell_id": cid,
                        "lon": round_float(lon, 6),
                        "lat": round_float(lat, 6),
                        "tuile": tile_name,
                        "row": row,
                        "col": col,
                        "valeur": raw,
                    }
                )
        return raw

    def _apply_cached_counters(self, extra: Dict[str, Any]) -> None:
        delta = extra.get("counters")
        if isinstance(delta, dict):
            self.counters.apply_int_delta({k: int(v) for k, v in delta.items()})
        for reading in extra.get("cell_zero_readings") or []:
            self.counters.cell_zero_readings.append(dict(reading))
            self.counters.cells_with_raw_zero_sample.add(int(reading["cell_id"]))

    def read_many(
        self,
        points: Sequence[Tuple[float, float]],
        *,
        measurement_id: Optional[str] = None,
        families: Optional[Sequence[str]] = None,
        contexts: Optional[Sequence[str]] = None,
        cell_ids: Optional[Sequence[Optional[int]]] = None,
    ) -> List[Optional[float]]:
        """Lit un lot ordonné : une fenêtre Rasterio par tuile, jamais de clamp."""
        if not points:
            self.last_batch_metrics = {
                "point_count": 0,
                "tile_count": 0,
                "raster_reads": 0,
                "pixels_loaded": 0,
            }
            return []

        fams = list(families) if families is not None else ["grille"] * len(points)
        ctxs = list(contexts) if contexts is not None else [""] * len(points)
        cids = list(cell_ids) if cell_ids is not None else [None] * len(points)
        if not (len(fams) == len(ctxs) == len(cids) == len(points)):
            raise ValueError("families/contexts/cell_ids doivent suivre les points")

        batch_id = measurement_id
        if batch_id:
            batch_id = f"{batch_id}:{_ordered_points_key(points)}"

        if self.measurement_table is not None and batch_id:
            cached = self.measurement_table.get(batch_id, len(points))
            if cached is not None:
                extra = self.measurement_table.extra(batch_id)
                if extra.get("counters") is not None:
                    self._apply_cached_counters(extra)
                else:
                    for (lon, lat), value, family in zip(points, cached, fams):
                        self._count_family(float(lon), float(lat), family)
                        if value is None:
                            self.counters.echantillons_nodata_raster += 1
                        elif value == 0.0:
                            self.counters.echantillons_valeur_zero_exact += 1
                self.last_batch_metrics = {
                    "point_count": len(points),
                    "tile_count": 0,
                    "raster_reads": 0,
                    "pixels_loaded": 0,
                }
                self.sampling_metrics["point_count"] += len(points)
                self.sampling_metrics["measurement_cache_hits"] += len(points)
                return cached

        before = self.counters.public_ints()
        zero_before = len(self.counters.cell_zero_readings)
        grouped: Dict[str, List[Tuple[int, int, int]]] = {}
        tiles_used: List[str] = []
        rows_used: List[int] = []
        cols_used: List[int] = []

        for index, ((raw_lon, raw_lat), context) in enumerate(zip(points, ctxs)):
            lon, lat = float(raw_lon), float(raw_lat)
            tile_name = self._tile_for(lon, lat)
            if tile_name not in self.lock_tiles:
                ctx = f" contexte={context}" if context else ""
                raise RuntimeError(
                    f"tuile requise absente du bloc dem: {tile_name} pour lon={lon} "
                    f"lat={lat}{ctx}"
                )
            ds = self._dataset(tile_name)
            row, col = pixel_indices_in_bounds(ds, lon, lat)
            if not (0 <= row < ds.height and 0 <= col < ds.width):
                self.counters.lectures_hors_bornes_du_fichier += 1
                raise RuntimeError(
                    f"lecture hors bornes: lon={lon} lat={lat} tuile={tile_name} "
                    f"row={row} col={col}"
                    + (f" contexte={context}" if context else "")
                )
            grouped.setdefault(tile_name, []).append((index, row, col))
            tiles_used.append(tile_name)
            rows_used.append(row)
            cols_used.append(col)
            if is_degree_line_point(lon, lat):
                self._compare_border_tiles(lon, lat)

        values, metrics = read_grouped_windows(
            grouped,
            self._dataset,
            output_size=len(points),
            masked=True,
        )
        recorded: List[Optional[float]] = []
        for (lon, lat), value, family, tile_name, row, col, cell_id in zip(
            points, values, fams, tiles_used, rows_used, cols_used, cids
        ):
            self._count_family(float(lon), float(lat), family)
            recorded.append(
                self._record_value(
                    float(lon), float(lat), tile_name, row, col, value, cell_id
                )
            )

        self.last_batch_metrics = metrics
        for key, value in metrics.items():
            self.sampling_metrics[key] += int(value)
        if self.measurement_table is not None and batch_id:
            after = self.counters.public_ints()
            delta = {k: after[k] - before[k] for k in after}
            extra = {
                "counters": delta,
                "cell_zero_readings": self.counters.cell_zero_readings[zero_before:],
            }
            self.measurement_table.put(batch_id, recorded, extra=extra)
        return recorded

    def read_elev(
        self,
        lon: float,
        lat: float,
        *,
        context: str = "",
        family: str = "grille",
        cell_id: Optional[int] = None,
    ) -> Optional[float]:
        """Lit une altitude ; None si nodata ; lève si hors couverture ou hors bornes."""
        return self.read_many(
            [(lon, lat)],
            families=[family],
            contexts=[context],
            cell_ids=[cell_id],
        )[0]


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


def iter_frontier_lonlat_points(
    adjacency_g5: Sequence[dict],
    cell_geoms: Dict[int, Any],
    projector: Projector,
) -> List[Tuple[float, float, str]]:
    """Points lon/lat le long des frontières land-land (D6.2)."""
    out: List[Tuple[float, float, str]] = []
    for edge in adjacency_g5:
        if edge.get("kind") != "land-land":
            continue
        a, b = int(edge["a"]), int(edge["b"])
        ga, gb = cell_geoms.get(a), cell_geoms.get(b)
        if ga is None or gb is None:
            continue
        boundary = shared_boundary(ga, gb)
        if boundary.is_empty:
            continue
        lines = _as_lines(boundary) or (
            [boundary] if boundary.geom_type == "LineString" else []
        )
        ctx = f"arete_{a}-{b}"
        for line in lines:
            if not isinstance(line, LineString):
                continue
            for x, y in densify_line_xy(line, G6_EDGE_SAMPLE_STEP_M):
                lon, lat = projector.unproject_xy(x, y)
                out.append((lon, lat, ctx))
    return out


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
    if hasattr(dem, "read_many"):
        measured = dem.read_many(
            requests,
            measurement_id=f"cell:{cid}:grid_and_centroid",
            families=["grille"] * len(grid) + ["centroide"],
            contexts=[f"cell_id={cid}"] * len(grid) + [f"cell_id={cid}_centroide"],
            cell_ids=[cid] * len(grid) + [cid],
        )
    else:
        measured = [
            dem.read_elev(lon, lat, context=f"cell_id={cid}", family="grille", cell_id=cid)
            for lon, lat in requests[:-1]
        ]
        measured.append(
            dem.read_elev(
                clon, clat, context=f"cell_id={cid}_centroide", family="centroide"
            )
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

    c_elev_raw = measured[-1]
    if c_elev_raw is None:
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
    centroid_elev = round_float(c_elev_raw, G6_ELEV_DECIMALS)

    mean_e = statistics.mean(valid_elevs)
    pop_std = statistics.pstdev(valid_elevs) if len(valid_elevs) > 1 else 0.0
    slope_mean: Optional[float] = (
        round_float(statistics.mean(slopes), G6_SLOPE_DECIMALS) if slopes else None
    )

    return {
        "cell_id": cid,
        "sample_count": len(valid_elevs),
        "elev_mean_m": round_float(mean_e, G6_ELEV_DECIMALS),
        "elev_min_m": round_float(min(valid_elevs), G6_ELEV_DECIMALS),
        "elev_max_m": round_float(max(valid_elevs), G6_ELEV_DECIMALS),
        "centroid_elev_m": centroid_elev,
        "slope_mean_deg": slope_mean,
        "roughness_m": round_float(pop_std, G6_ROUGH_DECIMALS),
    }, excluded


def match_known_pass(lon: float, lat: float) -> Tuple[Optional[str], Optional[str]]:
    match_id: Optional[str] = None
    match_name: Optional[str] = None
    closest_m = float("inf")
    for pid, pname, plon, plat in G6_KNOWN_PASSES:
        d = haversine_m(lon, lat, plon, plat)
        if d < closest_m or (d == closest_m and (match_id is None or pid < match_id)):
            closest_m = d
            match_id = pid
            match_name = pname
    if closest_m <= G6_KNOWN_PASS_MATCH_M:
        return match_id, match_name
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
        ctx = f"arete_{a}-{b}"
        for line in lines:
            if not isinstance(line, LineString):
                continue
            for x, y in densify_line_xy(line, G6_EDGE_SAMPLE_STEP_M):
                lon, lat = projector.unproject_xy(x, y)
                sample_points.append((lon, lat))
        if hasattr(dem, "read_many"):
            elevations = dem.read_many(
                sample_points,
                measurement_id=f"edge:{a}:{b}:frontier",
                families=["frontiere"] * len(sample_points),
                contexts=[ctx] * len(sample_points),
            )
        else:
            elevations = [
                dem.read_elev(lon, lat, context=ctx, family="frontiere")
                for lon, lat in sample_points
            ]
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


def _zone_max_elev(
    zone_name: str,
    cells: Sequence[dict],
    cell_geoms: Dict[int, Any],
    projector: Projector,
    relief_by_id: Dict[int, dict],
) -> Optional[float]:
    lon_min, lat_min, lon_max, lat_max = A12_RELIEF_ZONES[zone_name]
    from shapely.geometry import box

    zone_poly = box(lon_min, lat_min, lon_max, lat_max)
    best: Optional[float] = None
    for cell in cells:
        cid = int(cell["cell_id"])
        rec = relief_by_id.get(cid)
        if rec is None or rec.get("elev_max_m") is None:
            continue
        geom = cell_geoms.get(cid)
        if geom is None:
            continue
        minx, miny, maxx, maxy = geom.bounds
        corners = [
            projector.unproject_xy(minx, miny),
            projector.unproject_xy(maxx, miny),
            projector.unproject_xy(maxx, maxy),
            projector.unproject_xy(minx, maxy),
        ]
        lons = [c[0] for c in corners]
        lats = [c[1] for c in corners]
        cell_box = box(min(lons), min(lats), max(lons), max(lats))
        if not zone_poly.intersects(cell_box):
            continue
        val = float(rec["elev_max_m"])
        best = val if best is None else max(best, val)
    return best


def compute_barrieres_par_zone(
    adjacency_g6: Sequence[dict],
) -> Dict[str, int]:
    counts = {name: 0 for name in A12_RELIEF_ZONES}
    for edge in adjacency_g6:
        if not edge.get("relief_barrier"):
            continue
        plon = float(edge["crossing_lon"])
        plat = float(edge["crossing_lat"])
        for name, (lon_min, lat_min, lon_max, lat_max) in A12_RELIEF_ZONES.items():
            if lon_min <= plon <= lon_max and lat_min <= plat <= lat_max:
                counts[name] += 1
    return counts


def compute_zones_hautes_sous_basse(
    cells: Sequence[dict],
    cell_geoms: Dict[int, Any],
    projector: Projector,
    relief_by_id: Dict[int, dict],
) -> int:
    low_maxes = [
        _zone_max_elev(name, cells, cell_geoms, projector, relief_by_id)
        for name in A12_RELIEF_MUST_BE_LOW
    ]
    low_ceiling = max(v for v in low_maxes if v is not None) if any(
        v is not None for v in low_maxes
    ) else None
    if low_ceiling is None:
        return 0
    bad = 0
    for name in A12_RELIEF_MUST_BE_HIGH:
        high = _zone_max_elev(name, cells, cell_geoms, projector, relief_by_id)
        if high is None:
            bad += 1
        elif high <= low_ceiling:
            bad += 1
    return bad


def compute_stats(
    cells_g3: Sequence[dict],
    cell_relief: Sequence[dict],
    barrier_count: int,
    passes: Sequence[dict],
    excluded_total: int,
    dem: DemSampler,
    adjacency_g6: Sequence[dict],
    cell_geoms: Dict[int, Any],
    projector: Projector,
    relief_by_id: Dict[int, dict],
    adjacency_g5: Sequence[dict],
    domain_meta: Optional[dict] = None,
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
    ctr = dem.counters
    cellules_non_mesurees = sum(
        1 for c in cell_relief if int(c.get("sample_count") or 0) <= 0
    )
    cellules_altitude_min_nulle = sum(
        1
        for c in cell_relief
        if c.get("elev_min_m") is not None and float(c["elev_min_m"]) <= 0.0
    )
    land_sea_cells: Set[int] = set()
    for edge in adjacency_g5:
        if edge.get("kind") == "land-sea":
            land_sea_cells.add(int(edge["a"]))
            land_sea_cells.add(int(edge["b"]))
    cellules_sans_littoral_avec_echantillon_a_zero = sum(
        1
        for cid in ctr.cells_with_raw_zero_sample
        if cid not in land_sea_cells
    )
    total_reads = ctr.points_grille + ctr.points_centroides + ctr.points_frontieres
    points_sur_ligne = (
        ctr.points_sur_ligne_degre_grille
        + ctr.points_sur_ligne_degre_centroides
        + ctr.points_sur_ligne_degre_frontieres
    )
    out = {
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
        "echantillons_hors_couverture_dem": ctr.echantillons_hors_couverture_dem,
        "echantillons_nodata_raster": ctr.echantillons_nodata_raster,
        "points_lus_grille": ctr.points_grille,
        "points_lus_centroides": ctr.points_centroides,
        "points_lus_frontieres": ctr.points_frontieres,
        "cellules_non_mesurees": cellules_non_mesurees,
        "couverture_grille": ctr.points_grille,
        "couverture_centroides": ctr.points_centroides,
        "couverture_frontieres": ctr.points_frontieres,
        "tuiles_sans_valeur_nodata_declaree": ctr.tuiles_sans_valeur_nodata_declaree,
        "barrieres_par_zone_nommee": compute_barrieres_par_zone(adjacency_g6),
        "zones_hautes_sous_une_zone_basse": compute_zones_hautes_sous_basse(
            cells_g3, cell_geoms, projector, relief_by_id
        ),
        "total_lectures_altitude": total_reads,
        "lectures_hors_bornes_du_fichier": ctr.lectures_hors_bornes_du_fichier,
        "echantillons_valeur_zero_exact": ctr.echantillons_valeur_zero_exact,
        "cellules_altitude_min_nulle": cellules_altitude_min_nulle,
        "cellules_sans_littoral_avec_echantillon_a_zero": cellules_sans_littoral_avec_echantillon_a_zero,
        "points_sur_ligne_de_degre": points_sur_ligne,
        "points_sur_ligne_de_degre_par_famille": {
            "grille": ctr.points_sur_ligne_degre_grille,
            "centroides": ctr.points_sur_ligne_degre_centroides,
            "frontieres": ctr.points_sur_ligne_degre_frontieres,
        },
        "points_de_bord_multi_tuiles": ctr.points_de_bord_multi_tuiles,
        "points_de_bord_valeurs_concordantes": ctr.points_de_bord_valeurs_concordantes,
        "cellules_sans_littoral_lectures_zero": [
            reading
            for reading in ctr.cell_zero_readings
            if int(reading["cell_id"]) not in land_sea_cells
        ],
    }
    if domain_meta:
        out.update(domain_meta)
    return out


def load_context(*, verify_dem: bool = True, download_dem: bool = False) -> dict:
    dem_report: dict = {}
    if verify_dem:
        ok, detail, dem_report = verify_dem_fingerprint(download=download_dem)
        if not ok:
            raise RuntimeError(f"DEM non verifie avant lecture: {detail}")

    required = load_required_tile_set()
    lock = read_json(LOCK_PATH)
    lock_tiles = set(lock["dem"]["tiles"])
    assert_dem_coverage(required, lock_tiles)

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
    sample_tile = sorted(lock_tiles)[0]
    sample_path = fetch.tile_cache_path(sample_tile, cache_dir=effective_cache)
    _, half_px_lon, _ = measure_tile_registration(sample_path)

    def _tile_path(name: str, cache_dir: Path = effective_cache) -> Path:
        return fetch.tile_cache_path(name, cache_dir=cache_dir)

    conformes, total_tiles, reg_name, half_px = verify_all_tiles_domain_rule(
        sorted(lock_tiles), effective_cache, _tile_path, half_px_lon
    )
    domain_meta = {
        "tuiles_regle_domaine_conforme": conformes,
        "tuiles_registrement_homogene": total_tiles,
        "registrement_dem_mesure": reg_name,
        "demi_pixel_deg": half_px,
    }
    table_key, table_inputs = measurement_table_key(
        sources_lock=LOCK_PATH,
        cells=ARTIFACTS / "cells_g3.json",
        adjacency=ARTIFACTS / "adjacency_g5.json",
        sampling_code=Path(__file__),
        sample_step=G6_SAMPLE_STEP_DEG,
        extra_files={
            "dem_batch.py": ROOT / "tools" / "dem_batch.py",
            "constants.py": ROOT / "constants.py",
            "projection.py": ROOT / "projection.py",
        },
        domain_rule="D19-v1",
    )
    measurement_table = MeasurementTable(effective_cache, table_key, table_inputs)
    dem = DemSampler(
        effective_cache,
        lock_tiles,
        required,
        half_pixel_deg=half_px_lon,
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
        "crs": crs_declaration(has_geometry_lonlat=False),
        "required_tiles": required,
        "domain_meta": domain_meta,
        "measurement_table_key": table_key,
        "measurement_table_inputs": table_inputs,
    }


def derive_relief(context: dict) -> dict:
    projector: Projector = context["projector"]
    dem: DemSampler = context["dem"]
    dem.counters = DemCounters()
    if dem._nodata_by_tile:
        dem.counters.tuiles_sans_valeur_nodata_declaree = sum(
            1 for v in dem._nodata_by_tile.values() if v is None
        )
        dem.counters.tiles_missing_nodata_checked = set(dem._nodata_by_tile.keys())
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
        context["cells"],
        cell_relief,
        barrier_count,
        passes,
        excluded_total,
        dem,
        adjacency_g6,
        context["cell_geoms"],
        projector,
        relief_by_id,
        context["adjacency_g5"],
        domain_meta=context.get("domain_meta"),
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
        float_decimals=6,
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
        em = relief_by_id[cid].get("elev_mean_m")
        if em is None:
            continue
        for lonlat in _geom_lonlat_rings(geom, projector):
            patches.append(MplPolygon(lonlat, closed=True))
            values.append(float(em))
    coll = PatchCollection(patches, cmap="terrain", edgecolor="#555555", linewidth=0.15)
    coll.set_array(np.array(values))
    ax.add_collection(coll)
    fig.colorbar(coll, ax=ax, shrink=0.7, label="elev_mean_m")
    ax.grid(True, alpha=0.2)
    path = CAPTURE / "v1_052_elevation_window.png"
    fig.savefig(path, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["elevation_window"] = path

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
        "elapsed_s": time.perf_counter() - t_all,
        "sampling": ctx["dem"].sampling_metrics,
        "measurement_table": {
            "key": ctx.get("measurement_table_key"),
            "inputs": ctx.get("measurement_table_inputs"),
        },
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
