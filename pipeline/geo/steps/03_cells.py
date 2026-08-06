"""G3-bis — Semis à espacement variable et cellules jouables (v1_049).

Correctif de v1_048 : la densité pilote la DISTANCE MINIMALE entre germes
(Poisson à rayon variable + Lloyd fixe), plus leur coincidence près des villes.
Branche Voronoï sur la terre corrigée G2-bis (1400). Lecture seule des villes.
Identifiants stables ≥ 1000 ; 1000–1163 retirés. Zéro écriture Unity.

Usage :
  .venv/Scripts/python.exe pipeline.py --source cells
  .venv/Scripts/python.exe tests/run_proof_g3.py
"""

from __future__ import annotations

import argparse
import importlib.util
import math
import random
import sys
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

from shapely import voronoi_polygons
from shapely.geometry import MultiPoint, MultiPolygon, Point, box, mapping, shape
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    CELL_ID_BASE,
    FLOAT_DECIMALS,
    G3_AREA_CEIL_KM2,
    G3_AREA_EPS_M2,
    G3_AREA_FLOOR_KM2,
    G3_AREA_MAX_MEDIAN_RATIO,
    G3_BASE_DENSITY,
    G3_COMPACTNESS_MIN,
    G3_DENSITY_RADIUS_M,
    G3_LLOYD_ITERATIONS,
    G3_MASTER_SEED,
    G3_OVERLAP_EPS_M2,
    G3_PIPELINE_VERSION,
    G3_R_CEIL_M,
    G3_R_FLOOR_M,
    G3_REGISTRY_CREATED,
    G3_REGISTRY_RETIRED,
    G3_RETIRE_REASON,
    G3_RETIRED_ID_MAX,
    G3_RETIRED_ID_MIN,
    G3_SEED_COUNT_MAX,
    G3_SEED_COUNT_MIN,
    PARIS_BASIN_LONLAT,
    PILOT_WINDOW_LONLAT,
    SEA_CELL_ID,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402
from projection import Projector, crs_declaration, detect_projection, land_lonlat_from_coast_doc  # noqa: E402

BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
REGISTRY_PATH = REGISTRY / "cell_registry.json"
G6_REFINEMENT_PATH = REGISTRY / "g6_density_refinement.json"

# Chemins lecture seule (villes) — hors sandbox, jamais d'écriture.
CITIES_JSON = ROOT / "legacy_game_data" / "cities.json"  # FORGEHISTORY-PATH-ADJUSTMENT
CITY_COORDS_JSON = ROOT / "legacy_game_data" / "city_coordinates.json"  # FORGEHISTORY-PATH-ADJUSTMENT

# Champ r(x) : influence urbaine → distance minimale (intention densité, en espacement).
RADIUS_FIELD = {
    "name": "urban_influence_to_min_spacing",
    "formula": (
        "rho(x)=rho0+sum_c ln(1+pop_c)/(1+(d/R)^2) ; "
        "r(x)=r_ceil-(r_ceil-r_floor)*smoothstep(norm(rho))"
    ),
    "rho0": G3_BASE_DENSITY,
    "R_m": G3_DENSITY_RADIUS_M,
    "r_floor_m": G3_R_FLOOR_M,
    "r_ceil_m": G3_R_CEIL_M,
    "r_ratio_ceil_over_floor": round(G3_R_CEIL_M / G3_R_FLOOR_M, 6),
    "lloyd_iterations": G3_LLOYD_ITERATIONS,
    "weight": "ln(1+population)",
    "justification": (
        "v1_048 empilait des germes près des villes (coincidence) → rosettes "
        "d'échardes. Ici la même influence urbaine pilote r(x) : plancher 18 km "
        "près des villes (hex≈280 km², borne la plus petite cellule continentale) ; "
        "plafond 95 km loin d'elles (hex≈7800 km², borne la plus grande). "
        "Rapport r_ceil/r_floor≈5.28 = vraie mesure de variation de densité. "
        "Poisson à rayon variable : deux germes jamais plus proches que "
        "(r(p)+r(q))/2. Lloyd à 10 itérations FIXES (déterminisme). "
        "Compacité plancher 0.18 : interdit les échardes (v1_048 min 0.105) ; "
        "les îles singleton restent exemptées (côte NE fractal). "
        "Raffinement montagne/forêt REGISTRÉ pour G6 — pas simulé."
    ),
    "area_bounds_km2": {
        "floor": G3_AREA_FLOOR_KM2,
        "ceil": G3_AREA_CEIL_KM2,
        "max_median_ratio": G3_AREA_MAX_MEDIAN_RATIO,
        "derived_from": "hex(r_floor)≈280 → floor 200 ; hex(r_ceil)≈7800 → ceil 15000",
    },
    "compactness_min": G3_COMPACTNESS_MIN,
    "sources": [
        "game_unity/Assets/StreamingAssets/data/cities.json",
        "game_unity/Assets/StreamingAssets/data/city_coordinates.json",
    ],
}
# Alias rétrocompat pour exports / logs.
DENSITY_LAW = RADIUS_FIELD


# ---------------------------------------------------------------------------
# Chargement terre corrigée + villes
# ---------------------------------------------------------------------------


def _load_corrections_module():
    path = ROOT / "steps" / "02b_corrections_1400.py"
    spec = importlib.util.spec_from_file_location("corrections_g2b", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_pipeline_module():
    path = ROOT / "pipeline.py"
    spec = importlib.util.spec_from_file_location("geo_pipeline", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    # Évite de ré-exécuter main ; charge le module pour réutiliser Voronoï / derive.
    sys.modules["geo_pipeline"] = mod
    spec.loader.exec_module(mod)
    return mod


def load_corrected_land(
    *, rebuild: bool = False, projector: Optional[Projector] = None
) -> Dict[str, Any]:
    """Charge la terre G2-bis. rebuild=True relance 02b (lent) ; sinon artefact."""
    projector = projector or Projector(detect_projection())
    art = ARTIFACTS / "coastline_1400.json"
    if rebuild or not art.exists():
        g2b = _load_corrections_module()
        run = g2b.run_corrections(apply_corrections=True, clean_build=True)
        return {
            "land_ll": run["land_ll"],
            "land_xy": run["land_xy"],
            "land_area_km2": run["result"]["land_area_km2"],
            "islands_kept": run["result"]["islands_kept"],
            "fingerprint": run["fingerprint"],
            "projection": run["projection"],
            "source": "rebuilt_g2b",
        }
    doc = read_json(art)
    land_ll = land_lonlat_from_coast_doc(doc)
    land_xy = _project_geom(land_ll, projector)
    if not land_xy.is_valid:
        land_xy = land_xy.buffer(0)
    return {
        "land_ll": land_ll,
        "land_xy": land_xy,
        "land_area_km2": float(doc["land_area_km2"]),
        "islands_kept_count": int(doc.get("islands_kept_count") or 0),
        "fingerprint": {"sha256": sha256_file(art)},
        "projection": projector.info,
        "source": "artifacts/coastline_1400.json",
        "coastline_doc": doc,
    }


def load_cities_readonly(land_ll: Any, projector: Projector) -> List[dict]:
    """Lit villes + population (StreamingAssets) — filtre fenêtre ∩ terre."""
    if not CITIES_JSON.is_file() or not CITY_COORDS_JSON.is_file():
        raise FileNotFoundError(
            f"villes introuvables (lecture seule) : {CITIES_JSON} / {CITY_COORDS_JSON}"
        )
    cities_doc = read_json(CITIES_JSON)
    coords_doc = read_json(CITY_COORDS_JSON)
    by_name = {c["name"]: c for c in cities_doc["cities"]}
    w, s, e, n = PILOT_WINDOW_LONLAT
    window = box(w, s, e, n)
    out: List[dict] = []
    for feat in sorted(coords_doc["coordinates"], key=lambda c: c["name"]):
        name = feat["name"]
        base = by_name.get(name)
        if base is None:
            continue
        lon = float(feat["lon"])
        lat = float(feat["lat"])
        pt = Point(lon, lat)
        if not window.contains(pt):
            continue
        if not (land_ll.contains(pt) or land_ll.touches(pt)):
            # Snapping léger : representative sur terre la plus proche si < 15 km ~.
            continue
        x_m, y_m = projector.project_xy(lon, lat)
        gx, gy = projector.to_game(x_m, y_m)
        lon_r, lat_r = projector.lonlat_rounded(lon, lat)
        out.append(
            {
                "id": int(base["id"]),
                "name": name,
                "population": int(base["population"]),
                "lon": lon_r,
                "lat": lat_r,
                "x_m": round(x_m, FLOAT_DECIMALS),
                "y_m": round(y_m, FLOAT_DECIMALS),
                "x": gx,
                "y": gy,
            }
        )
    return sorted(out, key=lambda c: c["name"])


def _project_geom(geom: Any, projector: Projector) -> Any:
    def _coords(coords):
        return [projector.project_xy(lon, lat) for lon, lat in coords]

    if geom.geom_type == "Polygon":
        return type(geom)(
            _coords(list(geom.exterior.coords)),
            [_coords(list(r.coords)) for r in geom.interiors],
        )
    if geom.geom_type == "MultiPolygon":
        parts = [_project_geom(p, projector) for p in geom.geoms]
        parts = sorted(
            parts, key=lambda g: (round(g.centroid.x, 3), round(g.centroid.y, 3))
        )
        return MultiPolygon(parts)
    raise TypeError(geom.geom_type)


def _inverse_lonlat(projector: Projector, x: float, y: float) -> Tuple[float, float]:
    if projector._transformer is not None:
        from pyproj import Transformer

        inv = Transformer.from_crs("EPSG:3035", "EPSG:4326", always_xy=True)
        lon, lat = inv.transform(x, y)
        return projector.lonlat_rounded(lon, lat)
    cos_lat = math.cos(math.radians(47.5))
    lon = x / (cos_lat * 111_320.0)
    lat = y / 111_320.0
    return projector.lonlat_rounded(lon, lat)


def _iter_parts(land_xy: Any) -> List[Any]:
    if land_xy.geom_type == "Polygon":
        return [land_xy]
    if land_xy.geom_type == "MultiPolygon":
        return sorted(
            list(land_xy.geoms),
            key=lambda g: (round(g.centroid.x, 3), round(g.centroid.y, 3), round(g.area, 1)),
        )
    raise TypeError(land_xy.geom_type)


def _as_polygons(geom: Any) -> List[Any]:
    if geom is None or geom.is_empty:
        return []
    if geom.geom_type == "Polygon":
        return [geom]
    if geom.geom_type == "MultiPolygon":
        return list(geom.geoms)
    if geom.geom_type == "GeometryCollection":
        out: List[Any] = []
        for g in geom.geoms:
            out.extend(_as_polygons(g))
        return out
    return []


def _normalize_cell_geom(geom: Any) -> Any:
    if geom is None or geom.is_empty:
        return geom
    if not geom.is_valid:
        geom = geom.buffer(0)
    polys = _as_polygons(geom)
    if not polys:
        return geom
    if len(polys) == 1:
        return polys[0]
    return MultiPolygon(
        sorted(polys, key=lambda p: (round(p.centroid.x, 3), round(p.centroid.y, 3)))
    )


# ---------------------------------------------------------------------------
# PARTIE 1 — Champ r(x) + Poisson rayon variable + Lloyd fixe
# ---------------------------------------------------------------------------


def city_weight(population: int) -> float:
    return math.log(1.0 + max(0, int(population)))


def density_at(
    x: float, y: float, cities_xy: Sequence[dict], radius_m: float, rho0: float
) -> float:
    total = rho0
    r2 = radius_m * radius_m
    for c in cities_xy:
        dx = x - c["x_m"]
        dy = y - c["y_m"]
        d2 = dx * dx + dy * dy
        total += city_weight(c["population"]) / (1.0 + d2 / r2)
    return total


def _smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def radius_at(
    x: float,
    y: float,
    cities_xy: Sequence[dict],
    *,
    rho_lo: float,
    rho_hi: float,
    r_floor: float = G3_R_FLOOR_M,
    r_ceil: float = G3_R_CEIL_M,
    radius_m: float = G3_DENSITY_RADIUS_M,
    rho0: float = G3_BASE_DENSITY,
) -> float:
    """r(x) petit près des villes, grand loin — plancher / plafond déclarés."""
    rho = density_at(x, y, cities_xy, radius_m, rho0)
    if rho_hi <= rho_lo:
        return r_ceil
    norm = (rho - rho_lo) / (rho_hi - rho_lo)
    # Haute densité → r bas.
    return r_ceil - (r_ceil - r_floor) * _smoothstep(norm)


def _estimate_rho_bounds(
    land_xy: Any, cities: Sequence[dict]
) -> Tuple[float, float]:
    """Échantillonne rho sur une grille déterministe pour normaliser r(x)."""
    minx, miny, maxx, maxy = land_xy.bounds
    step = 40_000.0
    samples: List[float] = []
    x = minx + step * 0.5
    while x <= maxx + 1e-6:
        y = miny + step * 0.5
        while y <= maxy + 1e-6:
            if land_xy.contains(Point(x, y)):
                samples.append(
                    density_at(x, y, cities, G3_DENSITY_RADIUS_M, G3_BASE_DENSITY)
                )
            y += step
        x += step
    for c in cities:
        samples.append(
            density_at(
                c["x_m"], c["y_m"], cities, G3_DENSITY_RADIUS_M, G3_BASE_DENSITY
            )
        )
    if not samples:
        return G3_BASE_DENSITY, G3_BASE_DENSITY + 1.0
    samples.sort()
    # Percentiles stables (évite outliers d'une seule ville).
    lo = samples[max(0, int(0.05 * (len(samples) - 1)))]
    hi = samples[min(len(samples) - 1, int(0.95 * (len(samples) - 1)))]
    if hi <= lo:
        hi = lo + 1e-6
    return float(lo), float(hi)


def _snap_to_land(pt: Point, land_xy: Any, parts: Sequence[Any]) -> Optional[Point]:
    if land_xy.contains(pt) or land_xy.covers(pt):
        return pt
    best_d = None
    best_pt = None
    for part in parts:
        cand = part.exterior.interpolate(part.exterior.project(pt))
        d = pt.distance(cand)
        if best_d is None or d < best_d:
            best_d = d
            best_pt = cand
    if best_pt is None:
        return None
    if land_xy.covers(best_pt) or land_xy.contains(best_pt):
        return best_pt
    return None


def _poisson_variable_radius(
    land_xy: Any,
    cities: Sequence[dict],
    parts: Sequence[Any],
    *,
    rho_lo: float,
    rho_hi: float,
    master_seed: int,
    r_floor: float = G3_R_FLOOR_M,
    r_ceil: float = G3_R_CEIL_M,
    seed_count_min: int = G3_SEED_COUNT_MIN,
    seed_count_max: int = G3_SEED_COUNT_MAX,
    place_urban_anchors: bool = True,
) -> List[Tuple[float, float]]:
    """Semis Poisson à rayon variable — Bridson adapté, déterministe.

    Contrainte : dist(p,q) >= (r(p)+r(q))/2. Grille d'indexation sur r_floor.
    Paramètres optionnels : réemploi G4 (mer) sans second chemin algorithmique.
    """
    rng = random.Random(master_seed)
    cell_size = r_floor / math.sqrt(2.0)
    minx, miny, maxx, maxy = land_xy.bounds
    # Marge pour indices de grille.
    nx = max(1, int(math.ceil((maxx - minx) / cell_size)) + 1)
    ny = max(1, int(math.ceil((maxy - miny) / cell_size)) + 1)

    # seeds: list of (x, y, r)
    seeds: List[Tuple[float, float, float]] = []
    grid: Dict[Tuple[int, int], int] = {}

    def grid_idx(x: float, y: float) -> Tuple[int, int]:
        return (int((x - minx) / cell_size), int((y - miny) / cell_size))

    def far_enough(x: float, y: float, r: float) -> bool:
        gi, gj = grid_idx(x, y)
        # Rayon de recherche en cellules : r_ceil / cell_size.
        span = int(math.ceil(r_ceil / cell_size)) + 1
        for di in range(-span, span + 1):
            for dj in range(-span, span + 1):
                ii, jj = gi + di, gj + dj
                if ii < 0 or jj < 0 or ii >= nx or jj >= ny:
                    continue
                idx = grid.get((ii, jj))
                if idx is None:
                    continue
                sx, sy, sr = seeds[idx]
                dmin = 0.5 * (r + sr)
                dx = x - sx
                dy = y - sy
                if dx * dx + dy * dy < dmin * dmin:
                    return False
        return True

    def try_add(x: float, y: float, label_force: bool = False) -> bool:
        pt = Point(x, y)
        if not (land_xy.contains(pt) or land_xy.covers(pt)):
            return False
        r = radius_at(
            x, y, cities, rho_lo=rho_lo, rho_hi=rho_hi, r_floor=r_floor, r_ceil=r_ceil
        )
        if not far_enough(x, y, r):
            if not label_force:
                return False
            # Force (masse obligatoire) : refuser seulement si quasi-coïncident (<0.5*r_floor).
            for sx, sy, _sr in seeds:
                dx = x - sx
                dy = y - sy
                if dx * dx + dy * dy < (0.5 * r_floor) ** 2:
                    return False
        gi = grid_idx(x, y)
        grid[gi] = len(seeds)
        seeds.append(
            (round(x, FLOAT_DECIMALS), round(y, FLOAT_DECIMALS), r)
        )
        return True

    # 1) Germes obligatoires : 1 par masse (couverture îles / bassins).
    for i, part in enumerate(parts):
        rp = part.representative_point()
        try_add(rp.x, rp.y, label_force=True)

    # 2) Ancres urbaines (1 par ville) — espacées, pas d'anneaux empilés.
    if place_urban_anchors:
        for c in sorted(cities, key=lambda z: z["name"]):
            pt = Point(c["x_m"], c["y_m"])
            snapped = _snap_to_land(pt, land_xy, parts)
            if snapped is None:
                continue
            try_add(snapped.x, snapped.y)

    # 3) Bridson : file active + k essais par point actif.
    # Ordre initial stable : germes déjà placés, triés.
    active: List[int] = list(range(len(seeds)))
    k_attempts = 30
    # Limite de sécurité : ne pas dépasser le plafond déclaré.
    while active and len(seeds) < seed_count_max:
        # Tirage déterministe dans la file active.
        ai = rng.randrange(len(active))
        si = active[ai]
        sx, sy, sr = seeds[si]
        placed = False
        for _ in range(k_attempts):
            ang = rng.random() * 2.0 * math.pi
            # Candidat entre sr et 2*sr du parent (Bridson), borné par r local.
            rad = sr * (1.0 + rng.random())
            cx = sx + rad * math.cos(ang)
            cy = sy + rad * math.sin(ang)
            if cx < minx or cx > maxx or cy < miny or cy > maxy:
                continue
            if try_add(cx, cy):
                active.append(len(seeds) - 1)
                placed = True
                break
        if not placed:
            active.pop(ai)

    # 4) Si sous le plancher de compte : densifier via grille à pas r_floor
    #    (toujours respectant far_enough — pas de coincidence).
    if len(seeds) < seed_count_min:
        step = r_floor * 0.85
        extras: List[Tuple[float, float]] = []
        x = minx + step * 0.5
        while x <= maxx + 1e-6:
            y = miny + step * 0.5
            while y <= maxy + 1e-6:
                if land_xy.contains(Point(x, y)):
                    extras.append((x, y))
                y += step
            x += step
        for x, y in sorted(extras, key=lambda t: (t[0], t[1])):
            if len(seeds) >= seed_count_max:
                break
            try_add(x, y)

    return [(x, y) for x, y, _r in seeds]


def _lloyd_relax(
    land_xy: Any,
    seeds: Sequence[Tuple[float, float]],
    parts: Sequence[Any],
    *,
    iterations: int = G3_LLOYD_ITERATIONS,
) -> List[Tuple[float, float]]:
    """Relaxation de Lloyd à nombre d'itérations FIXE (jamais convergence)."""
    current = [
        (round(x, FLOAT_DECIMALS), round(y, FLOAT_DECIMALS)) for x, y in seeds
    ]
    for _it in range(iterations):
        # Associer seeds → masses.
        seed_part: List[int] = []
        for x, y in current:
            pt = Point(x, y)
            idx = None
            for i, part in enumerate(parts):
                if part.covers(pt) or part.contains(pt):
                    idx = i
                    break
            if idx is None:
                idx = min(range(len(parts)), key=lambda i: parts[i].distance(pt))
            seed_part.append(idx)

        new_seeds: List[Optional[Tuple[float, float]]] = [None] * len(current)
        for pi, part in enumerate(parts):
            local_idxs = [i for i, p in enumerate(seed_part) if p == pi]
            if not local_idxs:
                continue
            local = [current[i] for i in local_idxs]
            if len(local) == 1:
                # Île / singleton : ancrer au centroïde de la masse (stable).
                c = part.centroid
                snapped = _snap_to_land(c, land_xy, [part]) or part.representative_point()
                new_seeds[local_idxs[0]] = (
                    round(snapped.x, FLOAT_DECIMALS),
                    round(snapped.y, FLOAT_DECIMALS),
                )
                continue
            cell_map = _voronoi_on_part(part, local)
            for li, geom in cell_map.items():
                gi = local_idxs[li]
                cen = geom.centroid
                snapped = _snap_to_land(cen, land_xy, [part])
                if snapped is None:
                    snapped = geom.representative_point()
                new_seeds[gi] = (
                    round(snapped.x, FLOAT_DECIMALS),
                    round(snapped.y, FLOAT_DECIMALS),
                )
            # Seeds locaux sans cellule Voronoï : conserver.
            for li, gi in enumerate(local_idxs):
                if new_seeds[gi] is None:
                    new_seeds[gi] = current[gi]

        current = [
            new_seeds[i] if new_seeds[i] is not None else current[i]
            for i in range(len(current))
        ]
        # Tri stable après chaque itération (ordre d'index conservé — pas de
        # resort ici : les indices restent alignés sur les germes).
    return current


def build_seeds(
    land_xy: Any,
    cities: Sequence[dict],
    projector: Projector,
    *,
    master_seed: int = G3_MASTER_SEED,
) -> Dict[str, Any]:
    """Semis déterministe : r(x) → Poisson variable → Lloyd fixe."""
    t0 = time.perf_counter()
    parts = _iter_parts(land_xy)
    rho_lo, rho_hi = _estimate_rho_bounds(land_xy, cities)

    raw = _poisson_variable_radius(
        land_xy,
        cities,
        parts,
        rho_lo=rho_lo,
        rho_hi=rho_hi,
        master_seed=master_seed,
    )
    # Dédup spatiale stricte (1 m) puis tri stable avant Lloyd.
    uniq: Dict[Tuple[int, int], Tuple[float, float]] = {}
    for x, y in raw:
        key = (int(round(x)), int(round(y)))
        if key not in uniq:
            uniq[key] = (x, y)
    seeds_sorted = sorted(uniq.values(), key=lambda p: (p[0], p[1]))

    relaxed = _lloyd_relax(land_xy, seeds_sorted, parts, iterations=G3_LLOYD_ITERATIONS)
    # Re-dédup après Lloyd + tri final (domain_key stable).
    uniq2: Dict[Tuple[int, int], Tuple[float, float]] = {}
    for x, y in relaxed:
        key = (int(round(x)), int(round(y)))
        if key not in uniq2:
            uniq2[key] = (x, y)
    seeds_xy = sorted(uniq2.values(), key=lambda p: (p[0], p[1]))

    # Réinjecter masses manquantes (Lloyd / dédup ont pu les perdre).
    for i, part in enumerate(parts):
        hit = any(
            part.covers(Point(x, y)) or part.contains(Point(x, y)) for x, y in seeds_xy
        )
        if not hit:
            rp = part.representative_point()
            seeds_xy.append(
                (round(rp.x, FLOAT_DECIMALS), round(rp.y, FLOAT_DECIMALS))
            )
    seeds_xy = sorted(set(seeds_xy), key=lambda p: (p[0], p[1]))

    seed_records = []
    for i, (x, y) in enumerate(seeds_xy):
        lon, lat = _inverse_lonlat(projector, x, y)
        r = radius_at(x, y, cities, rho_lo=rho_lo, rho_hi=rho_hi)
        seed_records.append(
            {
                "order": i,
                "x_m": x,
                "y_m": y,
                "lon": lon,
                "lat": lat,
                "r_m": round(r, FLOAT_DECIMALS),
                "label": f"poisson:{lon:.6f}:{lat:.6f}",
                "domain_key": f"{lon:.6f}:{lat:.6f}",
            }
        )

    n = len(seeds_xy)
    # v1_096 : le nombre de semis varie avec l'élargissement de la fenêtre
    # pilote ; on ne le calibre plus sur des bornes étroites.

    uncovered = []
    for i, part in enumerate(parts):
        hit = any(
            part.covers(Point(x, y)) or part.contains(Point(x, y)) for x, y in seeds_xy
        )
        if not hit:
            uncovered.append(i)
    if uncovered:
        raise RuntimeError(f"masses sans germe : {uncovered}")

    rs = [s["r_m"] for s in seed_records]
    payload = {
        "count": len(seed_records),
        "master_seed": master_seed,
        "radius_field": RADIUS_FIELD,
        "density_law": RADIUS_FIELD,  # alias
        "rho_bounds": {"lo": rho_lo, "hi": rho_hi},
        "r_m_observed": {
            "min": min(rs) if rs else 0.0,
            "median": sorted(rs)[len(rs) // 2] if rs else 0.0,
            "max": max(rs) if rs else 0.0,
        },
        "lloyd_iterations": G3_LLOYD_ITERATIONS,
        "seeds": seed_records,
        "mandatory_masses": len(parts),
        "urban_anchors": len(cities),
        "count_justification": (
            f"Nombre = résultat du Poisson r∈[{G3_R_FLOOR_M},{G3_R_CEIL_M}] m "
            f"sur la terre, pas un quota. Bornes surface "
            f"[{G3_AREA_FLOOR_KM2},{G3_AREA_CEIL_KM2}] km² dérivées de hex(r)."
        ),
    }
    write_json(BUILD / "03_g3_seeds.json", payload)
    return {
        "seeds": seeds_xy,
        "seed_records": seed_records,
        "elapsed_s": time.perf_counter() - t0,
        "payload": payload,
        "parts": parts,
    }


# ---------------------------------------------------------------------------
# PARTIE 2 — Cellules Voronoï contraintes (par masse de terre)
# ---------------------------------------------------------------------------


def _voronoi_on_part(
    part: Any, seeds: Sequence[Tuple[float, float]]
) -> Dict[int, Any]:
    """Voronoï local à une masse — une cellule = un seul tenant sur cette masse."""
    if not seeds:
        return {}
    if len(seeds) == 1:
        return {0: _normalize_cell_geom(part)}

    seed_pts = [Point(x, y) for x, y in seeds]
    mp = MultiPoint(seed_pts)
    pad = max(part.bounds[2] - part.bounds[0], part.bounds[3] - part.bounds[1], 1.0)
    envelope = part.envelope.buffer(pad)
    raw = voronoi_polygons(mp, extend_to=envelope)
    voronoi_list = list(raw.geoms) if hasattr(raw, "geoms") else [raw]

    cell_map: Dict[int, Any] = {}
    for i, seed_pt in enumerate(seed_pts):
        matches = [g for g in voronoi_list if g.covers(seed_pt)]
        if not matches:
            matches = sorted(voronoi_list, key=lambda g: g.distance(seed_pt))
        clipped = matches[0].intersection(part)
        polys = _as_polygons(clipped)
        if not polys:
            continue
        containing = [p for p in polys if p.covers(seed_pt) or p.contains(seed_pt)]
        geom = max(containing or polys, key=lambda p: p.area)
        cell_map[i] = _normalize_cell_geom(geom)

    for i, seed_pt in enumerate(seed_pts):
        if i in cell_map:
            continue
        guess = seed_pt.buffer(pad * 0.05).intersection(part)
        polys = _as_polygons(guess)
        if polys:
            cell_map[i] = _normalize_cell_geom(max(polys, key=lambda p: p.area))

    # Assignation du reste UNIQUEMENT dans cette masse.
    for _ in range(10):
        if not cell_map:
            break
        covered = unary_union(list(cell_map.values()))
        remainder = part.difference(covered.buffer(0))
        if remainder.is_empty or remainder.area <= 1.0:
            break
        frags = _as_polygons(remainder)
        if not frags:
            remainder = remainder.buffer(0.5).intersection(part)
            frags = _as_polygons(remainder)
        if not frags:
            break
        for frag in sorted(frags, key=lambda g: (-g.area, round(g.centroid.x, 3), round(g.centroid.y, 3))):
            if frag.area <= 0:
                continue
            c = frag.representative_point()
            nearest_i = min(cell_map.keys(), key=lambda i: seed_pts[i].distance(c))
            merged = unary_union([cell_map[nearest_i], frag])
            cell_map[nearest_i] = _normalize_cell_geom(merged.intersection(part))

    for i in list(cell_map.keys()):
        cell_map[i] = _normalize_cell_geom(cell_map[i].intersection(part))
        if cell_map[i].is_empty or cell_map[i].area <= 0:
            del cell_map[i]
    return cell_map


def build_cells(
    land_xy: Any,
    seeds: Sequence[Tuple[float, float]],
    seed_records: Sequence[dict],
    projector: Projector,
) -> Dict[str, Any]:
    t0 = time.perf_counter()
    parts = _iter_parts(land_xy)
    typical_area = land_xy.area / max(len(seeds), 1)

    # Associer chaque seed à une masse (indice part).
    seed_part: List[int] = []
    for x, y in seeds:
        pt = Point(x, y)
        idx = None
        for i, part in enumerate(parts):
            if part.covers(pt) or part.contains(pt):
                idx = i
                break
        if idx is None:
            # Seed sur bord : masse la plus proche.
            idx = min(range(len(parts)), key=lambda i: parts[i].distance(pt))
        seed_part.append(idx)

    raw_cells: List[dict] = []
    for pi, part in enumerate(parts):
        local_idxs = [i for i, p in enumerate(seed_part) if p == pi]
        local_seeds = [seeds[i] for i in local_idxs]
        # Petite masse ou un seul germe → cellule entière (île non découpée).
        if len(local_seeds) <= 1 or part.area < typical_area * 0.55:
            geom = _normalize_cell_geom(part)
            si = local_idxs[0] if local_idxs else None
            if si is None:
                raise RuntimeError(f"masse {pi} sans seed")
            raw_cells.append(
                {
                    "seed_index": si,
                    "geometry": geom,
                    "part_index": pi,
                }
            )
            continue
        cell_map = _voronoi_on_part(part, local_seeds)
        for local_i, geom in sorted(cell_map.items()):
            raw_cells.append(
                {
                    "seed_index": local_idxs[local_i],
                    "geometry": geom,
                    "part_index": pi,
                }
            )

    # Identifiants STABLES : tri sur domain_key du germe (lon/lat), jamais ordre d'itération.
    enriched = []
    for rc in raw_cells:
        si = rc["seed_index"]
        rec = seed_records[si]
        geom = rc["geometry"]
        cx, cy = geom.centroid.x, geom.centroid.y
        gx, gy = projector.to_game(cx, cy)
        lon, lat = _inverse_lonlat(projector, cx, cy)
        perim = geom.length
        area = geom.area
        compactness = (4.0 * math.pi * area / (perim * perim)) if perim > 0 else 0.0
        enriched.append(
            {
                "domain_key": rec["domain_key"],
                "seed_index": si,
                "seed_lon": rec["lon"],
                "seed_lat": rec["lat"],
                "seed_label": rec["label"],
                "part_index": rc["part_index"],
                "area_m2": round(area, FLOAT_DECIMALS),
                "area_km2": round(area / 1_000_000.0, FLOAT_DECIMALS),
                "compactness_polsby_popper": round(compactness, FLOAT_DECIMALS),
                "centroid": {
                    "x": gx,
                    "y": gy,
                    "x_m": round(cx, FLOAT_DECIMALS),
                    "y_m": round(cy, FLOAT_DECIMALS),
                    "lon": lon,
                    "lat": lat,
                },
                "geometry": mapping(geom),
                "_geom": geom,
            }
        )

    enriched.sort(key=lambda c: c["domain_key"])
    cells_out: List[dict] = []
    for order, c in enumerate(enriched):
        # cell_id provisoire — build_registry attribue les ids stables (≥ suite).
        entry = {k: v for k, v in c.items() if k != "_geom"}
        entry["cell_id"] = CELL_ID_BASE + order  # sera réécrit par le registre
        entry["arc_ring_placeholder"] = True
        entry["_geom"] = c["_geom"]
        cells_out.append(entry)

    cells_out = sorted(cells_out, key=lambda c: c["domain_key"])
    write_json(
        BUILD / "04_g3_cells.json",
        {
            "count": len(cells_out),
            "cells": [
                {k: v for k, v in c.items() if k not in ("geometry", "_geom")}
                | {"geometry": c["geometry"]}
                for c in cells_out
            ],
        },
    )
    return {
        "cells": cells_out,
        "cells_xy": [(c["cell_id"], c["_geom"]) for c in cells_out],
        "elapsed_s": time.perf_counter() - t0,
    }


# ---------------------------------------------------------------------------
# PARTIE 3 — Registre + adjacence QA + export
# ---------------------------------------------------------------------------


def build_registry(
    cells: Sequence[dict], previous: Optional[List[dict]] = None
) -> Tuple[dict, List[dict]]:
    """Registre §5.1 — retire 1000–1163, émet à la suite, jamais de réattribution.

    Retourne (doc_registre, cells_avec_ids_stables).
    """
    prev_rows: List[dict] = list(previous) if previous else []
    if not prev_rows and REGISTRY_PATH.is_file():
        try:
            prev_rows = list(read_json(REGISTRY_PATH).get("cells") or [])
        except Exception:
            prev_rows = []

    prev_by_key: Dict[str, dict] = {}
    all_known_ids: set = set()
    retired_rows: List[dict] = []
    already_retired_ids: set = set()

    for row in prev_rows:
        cid = int(row["cell_id"])
        all_known_ids.add(cid)
        if row.get("retired") is not None:
            retired_rows.append(dict(row))
            already_retired_ids.add(cid)
            continue
        prev_by_key[str(row["domain_key"])] = dict(row)

    new_keys = {str(c["domain_key"]) for c in cells}

    def _retire(row: dict) -> None:
        cid = int(row["cell_id"])
        if cid in already_retired_ids:
            return
        retired = dict(row)
        retired["retired"] = G3_REGISTRY_RETIRED
        retired["retire_reason"] = G3_RETIRE_REASON
        retired_rows.append(retired)
        already_retired_ids.add(cid)

    # 1) Retirer TOUTE la plage v1_048 (1000–1163), même si domain_key coïncide.
    #    La maille change : aucune réattribution, jamais.
    for key, row in sorted(prev_by_key.items(), key=lambda t: t[0]):
        cid = int(row["cell_id"])
        if G3_RETIRED_ID_MIN <= cid <= G3_RETIRED_ID_MAX:
            _retire(row)
            continue
        if key not in new_keys:
            _retire(row)

    # Clés encore « réutilisables » : hors plage retirée + toujours actives.
    reusable: Dict[str, dict] = {}
    for key, row in prev_by_key.items():
        cid = int(row["cell_id"])
        if cid in already_retired_ids:
            continue
        if key in new_keys:
            reusable[key] = row

    # Mapping spatial pour supersedes : vieux germes de la plage retirée → nouvelle cellule.
    old_for_map = [
        r
        for r in prev_by_key.values()
        if G3_RETIRED_ID_MIN <= int(r["cell_id"]) <= G3_RETIRED_ID_MAX
        or str(r["domain_key"]) not in new_keys
    ]
    new_cells_sorted = sorted(cells, key=lambda c: c["domain_key"])
    supersedes_bags: Dict[str, List[int]] = {
        c["domain_key"]: [] for c in new_cells_sorted
    }

    def _key_lonlat(domain_key: str) -> Tuple[float, float]:
        parts = domain_key.split(":")
        return float(parts[0]), float(parts[1])

    for old in sorted(old_for_map, key=lambda r: int(r["cell_id"])):
        olon = float(old.get("seed_lon", _key_lonlat(str(old["domain_key"]))[0]))
        olat = float(old.get("seed_lat", _key_lonlat(str(old["domain_key"]))[1]))
        best_key = None
        best_d = None
        for c in new_cells_sorted:
            nlon, nlat = _key_lonlat(c["domain_key"])
            d = (nlon - olon) ** 2 + (nlat - olat) ** 2
            if best_d is None or d < best_d:
                best_d = d
                best_key = c["domain_key"]
        if best_key is not None:
            supersedes_bags[best_key].append(int(old["cell_id"]))

    next_id = max(all_known_ids | already_retired_ids | {G3_RETIRED_ID_MAX}) + 1
    if next_id < G3_RETIRED_ID_MAX + 1:
        next_id = G3_RETIRED_ID_MAX + 1

    active_rows: List[dict] = []
    remapped: List[dict] = []
    used_ids: set = set(already_retired_ids)

    for c in new_cells_sorted:
        key = str(c["domain_key"])
        prev = reusable.get(key)
        if prev is not None:
            cid = int(prev["cell_id"])
            if cid in already_retired_ids or (
                G3_RETIRED_ID_MIN <= cid <= G3_RETIRED_ID_MAX
            ):
                raise RuntimeError(f"réattribution d'id retiré interdite: {cid}")
            supersedes = list(prev.get("supersedes") or [])
        else:
            while (
                next_id in used_ids
                or next_id in already_retired_ids
                or (G3_RETIRED_ID_MIN <= next_id <= G3_RETIRED_ID_MAX)
            ):
                next_id += 1
            cid = next_id
            next_id += 1
            supersedes = sorted(set(supersedes_bags.get(key) or []))

        if cid < CELL_ID_BASE or cid < 1000:
            raise RuntimeError("empiètement plage 1–999 interdit")
        if cid in already_retired_ids:
            raise RuntimeError(f"id retiré réémis: {cid}")
        if G3_RETIRED_ID_MIN <= cid <= G3_RETIRED_ID_MAX:
            raise RuntimeError(f"id plage v1_048 réémis: {cid}")

        used_ids.add(cid)
        active_rows.append(
            {
                "cell_id": cid,
                "created": G3_REGISTRY_CREATED,
                "seed_lon": c["seed_lon"],
                "seed_lat": c["seed_lat"],
                "label": c.get("seed_label") or f"cell_{cid}",
                "domain_key": c["domain_key"],
                "retired": None,
                "supersedes": supersedes,
            }
        )
        entry = {k: v for k, v in c.items() if k != "_geom"}
        entry["cell_id"] = cid
        if "_geom" in c:
            entry["_geom"] = c["_geom"]
        remapped.append(entry)

    # Contrôle : anciens actifs hors nouvelle maille doivent être retirés.
    vanished_check = []
    for row in prev_rows:
        if row.get("retired") is not None:
            continue
        cid = int(row["cell_id"])
        key = str(row["domain_key"])
        if key not in new_keys or (G3_RETIRED_ID_MIN <= cid <= G3_RETIRED_ID_MAX):
            if cid not in already_retired_ids:
                vanished_check.append(cid)
    if vanished_check:
        raise RuntimeError(
            f"identifiants actifs disparus sans retired : {vanished_check}"
        )

    active_now = {r["cell_id"] for r in active_rows}
    overlap = active_now & already_retired_ids
    if overlap:
        raise RuntimeError(f"ids retirés encore actifs : {sorted(overlap)}")

    all_rows = sorted(retired_rows + active_rows, key=lambda r: int(r["cell_id"]))
    doc = {
        "version": 1,
        "pipeline_version": G3_PIPELINE_VERSION,
        "comment": (
            "Registre d'identifiants de cellules — autorité §5.1. "
            "Un cell_id n'est jamais réattribué. Plage ≥ 1000 (provinces 1–999). "
            "v1_049 : 1000–1163 retirés (coincidence seeding), suite ≥ 1164."
        ),
        "created": G3_REGISTRY_CREATED,
        "cells": all_rows,
    }
    remapped = sorted(remapped, key=lambda c: c["cell_id"])
    return doc, remapped


def write_g6_refinement_registry() -> dict:
    """Raffinement densité montagne/forêt — non dérivable avant G6."""
    doc = {
        "version": 1,
        "comment": (
            "Raffinements de densité ATTENDUS à G6 — registrés, pas simulés. "
            "Même discipline que v1_047 pour les polders."
        ),
        "refinements": [
            {
                "id": "g6_relief_forest_density",
                "title": "Densité lâche en montagne et forêt",
                "what_we_know": (
                    "Le plan G3 vise aussi des cellules plus grandes en montagne "
                    "et en forêt. Aucune couche relief/forêt n'existe encore."
                ),
                "certainty": "attested_gap",
                "why_not_now": (
                    "Dériver cette densité sans MNT / couverture forestière serait "
                    "inventer de la donnée. Interdit par le brief v1_048."
                ),
                "what_would_be_needed": "Étape G6 (relief) + couche forêt sourcée.",
                "status": "expected_refinement_g6",
            }
        ],
    }
    write_json(G6_REFINEMENT_PATH, doc)
    return doc


def derive_adjacency(cells_xy: Sequence[Tuple[int, Any]], land_xy: Any) -> List[dict]:
    """Adjacence terre-terre / terre-mer pour Q4 — pas de typage G4 (détroit…)."""
    pipe = _load_pipeline_module()
    derived = pipe.stage_derive(cells_xy, land_xy)
    return derived["adjacency"]


def compute_metrics(
    cells: Sequence[dict],
    land_xy: Any,
    projector: Projector,
    parts: Optional[Sequence[Any]] = None,
) -> dict:
    areas = sorted(float(c["area_km2"]) for c in cells)
    comps = sorted(float(c["compactness_polsby_popper"]) for c in cells)

    def percentile(vals: List[float], p: float) -> float:
        if not vals:
            return 0.0
        if len(vals) == 1:
            return float(vals[0])
        idx = p * (len(vals) - 1)
        lo = int(math.floor(idx))
        hi = int(math.ceil(idx))
        if lo == hi:
            return float(vals[lo])
        w = idx - lo
        return float(vals[lo]) * (1.0 - w) + float(vals[hi]) * w

    def median(vals: List[float]) -> float:
        return percentile(vals, 0.5)

    pw, ps, pe, pn = PARIS_BASIN_LONLAT
    basin = box(pw, ps, pe, pn)
    in_basin = [
        c
        for c in cells
        if basin.contains(Point(c["centroid"]["lon"], c["centroid"]["lat"]))
    ]
    corners = [
        projector.project_xy(pw, ps),
        projector.project_xy(pw, pn),
        projector.project_xy(pe, ps),
        projector.project_xy(pe, pn),
    ]
    bx0, bx1 = min(c[0] for c in corners), max(c[0] for c in corners)
    by0, by1 = min(c[1] for c in corners), max(c[1] for c in corners)
    basin_xy = box(bx0, by0, bx1, by1).intersection(land_xy)
    basin_area = float(basin_xy.area) if not basin_xy.is_empty else 0.0
    total_area = float(land_xy.area)
    n = len(cells)
    expected_uniform = n * (basin_area / total_area) if total_area > 0 else 0.0
    actual_basin = len(in_basin)
    basin_areas = sorted(float(c["area_km2"]) for c in in_basin)

    dens_basin = (actual_basin / (basin_area / 1e6)) if basin_area > 0 else 0.0
    basin_ids = {c["cell_id"] for c in in_basin}
    outside = [c for c in cells if c["cell_id"] not in basin_ids]
    outside_sorted = sorted(outside, key=lambda c: -c["area_km2"])
    empty_slice = (
        outside_sorted[: max(1, len(outside_sorted) // 4)] if outside_sorted else []
    )
    empty_area = sum(c["area_km2"] for c in empty_slice)
    dens_empty = (len(empty_slice) / empty_area) if empty_area > 0 else 0.0
    density_ratio = (dens_basin / dens_empty) if dens_empty > 0 else 0.0

    med = median(areas)
    mx = areas[-1] if areas else 0.0
    max_med_ratio = (mx / med) if med > 0 else 0.0

    singleton_ids: set = set()
    if parts is not None:
        for part in parts:
            covering = []
            for c in cells:
                g = c.get("_geom") or shape(c["geometry"])
                inter = part.intersection(g)
                if not inter.is_empty and inter.area > G3_AREA_EPS_M2:
                    covering.append(c["cell_id"])
            if len(covering) == 1:
                singleton_ids.add(covering[0])

    return {
        "cell_count": n,
        "area_km2": {
            "min": areas[0] if areas else 0.0,
            "p10": percentile(areas, 0.10),
            "median": med,
            "p90": percentile(areas, 0.90),
            "max": mx,
            "max_median_ratio": round(max_med_ratio, 6),
        },
        "area_bounds": {
            "floor_km2": G3_AREA_FLOOR_KM2,
            "ceil_km2": G3_AREA_CEIL_KM2,
            "max_median_ratio_ceil": G3_AREA_MAX_MEDIAN_RATIO,
            "singleton_exempt_count": len(singleton_ids),
        },
        "compactness_polsby_popper": {
            "min": comps[0] if comps else 0.0,
            "median": median(comps),
            "floor": G3_COMPACTNESS_MIN,
        },
        "paris_basin": {
            "cell_count": actual_basin,
            "expected_uniform": round(expected_uniform, 3),
            "ratio_vs_uniform": round(
                actual_basin / expected_uniform if expected_uniform > 0 else 0.0, 3
            ),
            "median_area_km2": median(basin_areas) if basin_areas else 0.0,
            "bbox_lonlat": list(PARIS_BASIN_LONLAT),
        },
        "density_ratio_basin_vs_emptiest_quartile": round(density_ratio, 3),
        "id_range": {
            "min": cells[0]["cell_id"] if cells else None,
            "max": cells[-1]["cell_id"] if cells else None,
        },
        "singleton_cell_ids": sorted(singleton_ids),
    }


def export_g3(
    *,
    cells: Sequence[dict],
    adjacency: Sequence[dict],
    metrics: dict,
    registry: dict,
    land_meta: dict,
    projection,
    fingerprints: Dict[str, str],
    density_law: dict,
) -> Dict[str, str]:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    REGISTRY.mkdir(parents=True, exist_ok=True)
    shas: Dict[str, str] = {}

    cells_out = {
        "pipeline_version": G3_PIPELINE_VERSION,
        "data_class": "natural_earth_g3_cells",
        "comment": (
            "Cellules territoriales G3 — Voronoï contraint au littoral 1400. "
            "Identifiants ≥ 1000. Densité = villes/population uniquement."
        ),
        "projection": projection.epsg if hasattr(projection, "epsg") else projection,
        "crs": crs_declaration(
            geometry_crs=(
                projection.epsg if hasattr(projection, "epsg") else str(projection)
            ),
            has_geometry_lonlat=False,
        ),
        "density_law": density_law,
        "metrics": metrics,
        "cells": [
            {
                "cell_id": c["cell_id"],
                "domain_key": c["domain_key"],
                "seed_lon": c["seed_lon"],
                "seed_lat": c["seed_lat"],
                "area_m2": c["area_m2"],
                "area_km2": c["area_km2"],
                "compactness_polsby_popper": c["compactness_polsby_popper"],
                "centroid": c["centroid"],
                "geometry": c["geometry"],
                "arc_ring_placeholder": True,
            }
            for c in sorted(cells, key=lambda x: x["cell_id"])
        ],
    }
    shas["artifacts/cells_g3.json"] = write_json(ARTIFACTS / "cells_g3.json", cells_out)

    adj_out = {
        "pipeline_version": G3_PIPELINE_VERSION,
        "data_class": "natural_earth_g3_cells",
        "comment": "Adjacence dérivée pour QA G3 (pas de typage détroit — G4).",
        "adjacency": sorted(adjacency, key=lambda e: (e["a"], e["b"], e["kind"])),
    }
    shas["artifacts/adjacency_g3.json"] = write_json(
        ARTIFACTS / "adjacency_g3.json", adj_out
    )

    shas["artifacts/stats_g3.json"] = write_json(ARTIFACTS / "stats_g3.json", metrics)
    shas["registry/cell_registry.json"] = write_json(REGISTRY_PATH, registry)

    g6 = write_g6_refinement_registry()
    shas["registry/g6_density_refinement.json"] = sha256_file(G6_REFINEMENT_PATH)

    manifest = {
        "pipeline_version": G3_PIPELINE_VERSION,
        "data_class": "natural_earth_g3_cells",
        "comment": "MANIFEST G3 — fixed_timestamp figé ; timings exclus.",
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "projection": {
            "epsg": projection.epsg,
            "fallback": projection.fallback,
            "reason": projection.reason,
        },
        "inputs": {k: fingerprints[k] for k in sorted(fingerprints.keys())},
        "outputs": {k: shas[k] for k in sorted(shas.keys())},
        "land_source": land_meta.get("source"),
        "g6_refinements_registered": [r["id"] for r in g6["refinements"]],
    }
    shas["artifacts/MANIFEST_g3.json"] = write_json(
        ARTIFACTS / "MANIFEST_g3.json", manifest
    )
    return shas


# ---------------------------------------------------------------------------
# Captures
# ---------------------------------------------------------------------------


def write_captures(
    land_ll: Any,
    cells: Sequence[dict],
    cities: Sequence[dict],
) -> Dict[str, Path]:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.patches import Polygon as MplPolygon
    from matplotlib.colors import hsv_to_rgb

    CAPTURE.mkdir(parents=True, exist_ok=True)
    paths: Dict[str, Path] = {}

    def _polys(geom: Any) -> List[Any]:
        return _as_polygons(geom)

    def _cell_patches(cell_list: Sequence[dict]):
        patches = []
        colors = []
        for c in sorted(cell_list, key=lambda x: x["cell_id"]):
            g = shape(c["geometry"])
            # Couleur déterministe depuis cell_id.
            hue = ((c["cell_id"] * 47) % 360) / 360.0
            rgb = hsv_to_rgb((hue, 0.45, 0.92))
            for poly in _polys(g):
                # Géométrie projetée → on affiche en lon/lat via centroïde mapping?
                # Les cellules sont en EPSG:3035. Pour la capture on reprojecte approx
                # via seed_lon/lat n'affiche pas le trait de côte. On utilise
                # inverse via pyproj si dispo.
                pass
        return patches, colors

    # Reproject cells to lon/lat for display.
    from pyproj import Transformer

    inv = Transformer.from_crs("EPSG:3035", "EPSG:4326", always_xy=True)

    def to_ll_poly(geom_xy: Any) -> List[Any]:
        out = []
        for poly in _polys(geom_xy):
            coords = [inv.transform(x, y) for x, y in poly.exterior.coords]
            out.append(coords)
        return out

    def draw_cells(ax, cell_list, xlim=None, ylim=None, title=""):
        ax.set_aspect("equal")
        ax.set_facecolor("#bbdefb")
        ax.set_title(title, fontsize=10)
        ax.grid(True, alpha=0.25)
        if xlim:
            ax.set_xlim(*xlim)
        if ylim:
            ax.set_ylim(*ylim)
        # Terre fond
        for poly in _polys(land_ll):
            ax.add_patch(
                MplPolygon(
                    list(zip(*poly.exterior.xy)),
                    closed=True,
                    facecolor="#c8e6c9",
                    edgecolor="#1b5e20",
                    linewidth=0.4,
                    alpha=0.9,
                )
            )
        exteriors = []
        facecolors = []
        for c in sorted(cell_list, key=lambda x: x["cell_id"]):
            hue = ((int(c["cell_id"]) * 47) % 360) / 360.0
            rgb = tuple(hsv_to_rgb((hue, 0.55, 0.88)))
            for ring in to_ll_poly(shape(c["geometry"])):
                exteriors.append(MplPolygon(ring, closed=True))
                facecolors.append(rgb)
        if exteriors:
            ax.add_collection(
                PatchCollection(
                    exteriors,
                    facecolors=facecolors,
                    edgecolors="#333333",
                    linewidths=0.25,
                    alpha=0.75,
                )
            )
        for city in cities:
            ax.plot(city["lon"], city["lat"], "o", color="#b71c1c", markersize=3, zorder=5)
        ax.set_xlabel("longitude")
        ax.set_ylabel("latitude")

    w, s, e, n = PILOT_WINDOW_LONLAT
    fig, ax = plt.subplots(figsize=(14, 12), dpi=120)
    draw_cells(
        ax,
        cells,
        xlim=(w, e),
        ylim=(s, n),
        title="G3-bis v1_049 — fenêtre pilote (espacement r(x))",
    )
    fig.tight_layout()
    p_full = CAPTURE / "v1_049_cells_window.png"
    fig.savefig(p_full, format="png")
    plt.close(fig)
    paths["window"] = p_full

    # Zoom Bretagne / Cotentin
    fig, ax = plt.subplots(figsize=(10, 9), dpi=120)
    draw_cells(
        ax,
        cells,
        xlim=(-5.2, -0.8),
        ylim=(46.8, 49.8),
        title="G3-bis zoom côte — Bretagne / Cotentin",
    )
    fig.tight_layout()
    p_coast = CAPTURE / "v1_049_cells_coast_zoom.png"
    fig.savefig(p_coast, format="png")
    plt.close(fig)
    paths["coast_zoom"] = p_coast

    # Zoom bassin parisien
    fig, ax = plt.subplots(figsize=(9, 8), dpi=120)
    pw, ps, pe, pn = PARIS_BASIN_LONLAT
    draw_cells(
        ax,
        cells,
        xlim=(pw - 0.3, pe + 0.3),
        ylim=(ps - 0.2, pn + 0.2),
        title="G3-bis zoom — bassin parisien (maille graduée)",
    )
    fig.tight_layout()
    p_paris = CAPTURE / "v1_049_cells_paris_basin.png"
    fig.savefig(p_paris, format="png")
    plt.close(fig)
    paths["paris_basin"] = p_paris

    # Comparaison avant/après bassin parisien (v1_048 à gauche si dispo).
    before = CAPTURE / "v1_048_cells_paris_basin.png"
    fig, axes = plt.subplots(1, 2, figsize=(14, 6), dpi=120)
    if before.is_file():
        import matplotlib.image as mpimg

        img = mpimg.imread(before)
        axes[0].imshow(img)
        axes[0].set_title("AVANT v1_048 — rosette d'échardes")
        axes[0].axis("off")
    else:
        axes[0].text(0.5, 0.5, "v1_048 capture absente", ha="center", va="center")
        axes[0].axis("off")
    draw_cells(
        axes[1],
        cells,
        xlim=(pw - 0.3, pe + 0.3),
        ylim=(ps - 0.2, pn + 0.2),
        title="APRÈS v1_049 — maille graduée",
    )
    fig.tight_layout()
    p_cmp = CAPTURE / "v1_049_paris_basin_before_after.png"
    fig.savefig(p_cmp, format="png")
    plt.close(fig)
    paths["paris_before_after"] = p_cmp
    return paths


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------


def run_cells(
    *,
    rebuild_land: bool = False,
    previous_registry: Optional[List[dict]] = None,
) -> Dict[str, Any]:
    t_all = time.perf_counter()
    timings: Dict[str, float] = {}
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    REGISTRY.mkdir(parents=True, exist_ok=True)

    projector = Projector(detect_projection())

    t = time.perf_counter()
    land_pack = load_corrected_land(rebuild=rebuild_land, projector=projector)
    timings["load_land"] = time.perf_counter() - t
    land_ll = land_pack["land_ll"]
    land_xy = land_pack["land_xy"]
    parts = _iter_parts(land_xy)

    t = time.perf_counter()
    cities = load_cities_readonly(land_ll, projector)
    timings["load_cities"] = time.perf_counter() - t

    t = time.perf_counter()
    seeded = build_seeds(land_xy, cities, projector)
    timings["seed"] = seeded["elapsed_s"]

    t = time.perf_counter()
    celled = build_cells(
        land_xy, seeded["seeds"], seeded["seed_records"], projector
    )
    timings["cells"] = celled["elapsed_s"]

    # Registre : retire 1000–1163, attribue ids stables, remappe les cellules.
    registry, cells = build_registry(celled["cells"], previous=previous_registry)
    cells_xy = [
        (c["cell_id"], c.get("_geom") or shape(c["geometry"])) for c in cells
    ]

    t = time.perf_counter()
    adjacency = derive_adjacency(cells_xy, land_xy)
    timings["adjacency"] = time.perf_counter() - t
    write_json(
        BUILD / "05_g3_adjacency.json",
        {"adjacency": adjacency, "count": len(adjacency)},
    )

    metrics = compute_metrics(cells, land_xy, projector, parts=parts)

    # Nettoyer _geom avant export.
    cells_export = []
    for c in cells:
        entry = {k: v for k, v in c.items() if k != "_geom"}
        cells_export.append(entry)

    fingerprints = {
        "coastline_1400": land_pack["fingerprint"]["sha256"],
        "cities": sha256_file(CITIES_JSON),
        "city_coordinates": sha256_file(CITY_COORDS_JSON),
    }

    t = time.perf_counter()
    shas = export_g3(
        cells=cells_export,
        adjacency=adjacency,
        metrics=metrics,
        registry=registry,
        land_meta=land_pack,
        projection=projector.info,
        fingerprints=fingerprints,
        density_law=RADIUS_FIELD,
    )
    timings["export"] = time.perf_counter() - t

    t = time.perf_counter()
    captures = write_captures(land_ll, cells_export, cities)
    timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t_all
    write_json(
        BUILD / "99_timings_g3.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )

    return {
        "cells": cells_export,
        "cells_xy": cells_xy,
        "adjacency": adjacency,
        "land_ll": land_ll,
        "land_xy": land_xy,
        "cities": cities,
        "metrics": metrics,
        "registry": registry,
        "shas": shas,
        "timings": timings,
        "captures": captures,
        "projection": projector.info,
        "seed_payload": seeded["payload"],
        "fingerprints": fingerprints,
        "parts_count": len(parts),
    }


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="G3 cellules territoriales")
    parser.add_argument(
        "--rebuild-land",
        action="store_true",
        help="Relancer G2-bis au lieu de lire artifacts/coastline_1400.json",
    )
    args = parser.parse_args(list(argv) if argv is not None else None)
    result = run_cells(rebuild_land=args.rebuild_land)
    m = result["metrics"]
    print(
        f"pipeline {G3_PIPELINE_VERSION} | cells={m['cell_count']} | "
        f"ids={m['id_range']} | "
        f"paris_basin={m['paris_basin']['cell_count']} "
        f"(uniform≈{m['paris_basin']['expected_uniform']}) | "
        f"density_ratio={m['density_ratio_basin_vs_emptiest_quartile']}"
    )
    for path, digest in sorted(result["shas"].items()):
        print(f"  {path}  {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
