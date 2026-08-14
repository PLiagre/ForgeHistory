"""G4 — zones de mer et adjacence typée (v1_050).

Ce que fait ce module, en une phrase : il découpe l'eau de 1400 en zones de mer,
puis dit quelle terre touche quelle eau, quelle eau touche quelle eau, et quelles
terres un bras d'eau court sépare.

Entrées (lecture seule) : la terre corrigée de 1400 (steps/02b), les cellules
committées (artifacts/cells_g3.json), leur adjacence terre-terre
(artifacts/adjacency_g3.json), les déclarations historiques
(data/corrections_1400.json) et les noms de mer hérités du jeu
(legacy_game_data/sea_zones.json).

Sorties : artifacts/sea_zones_g4.json, adjacency_g4.json, topology_links_g4.json,
stats_g4.json, adjacency_divergence_g4.json (QA seulement), MANIFEST_g4.json,
registry/sea_zone_registry.json et trois captures.

Usage :
  ../../.venv/bin/python pipeline.py --source adjacency
  ../../.venv/bin/python tests/run_proof_g4.py
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

from shapely.geometry import MultiPolygon, Point, Polygon, box, mapping, shape
from shapely.ops import unary_union
from shapely.prepared import prep
from shapely.strtree import STRtree

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    FLOAT_DECIMALS,
    G3_AREA_EPS_M2,
    G4_PIPELINE_VERSION,
    G4_REGISTRY_CREATED,
    G4_SEA_AREA_CEIL_KM2,
    G4_SEA_AREA_FLOOR_KM2,
    G4_SEA_COMPACTNESS_MIN,
    G4_SEA_LLOYD_ITERATIONS,
    G4_SEA_MASTER_SEED,
    G4_SEA_R_CEIL_M,
    G4_SEA_R_FLOOR_M,
    G4_STRAIT_JUSTIFICATION,
    G4_STRAIT_MAX_WIDTH_M,
    LENGTH_EPS,
    PILOT_WINDOW_LONLAT,
    SEA_CELL_ID,
    SEA_ZONE_COUNT_MAX,
    SEA_ZONE_COUNT_MIN,
    SEA_ZONE_ID_BASE,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402
from projection import Projector, crs_declaration, detect_projection  # noqa: E402

BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
LEGACY = ROOT / "legacy_game_data"
DATA = ROOT / "data"

REGISTRY_PATH = REGISTRY / "sea_zone_registry.json"

# Pas de densification du BORD DE FENÊTRE, en degrés. Ce n'est ni un seuil
# d'acceptation ni une borne de qualité : la fenêtre pilote est un rectangle
# lon/lat, et son bord doit être échantillonné pour rester un rectangle une fois
# projeté. Aucun sommet du trait de côte n'est touché — seul le bord de la
# fenêtre, qui ne borde aucune cellule. Les bornes d'acceptation, elles, sont
# toutes lues de constants.py.
WINDOW_EDGE_STEP_DEG = 0.02

# Le champ d'espacement de la mer : plus fin près des côtes, plus large au large.
SEA_SPACING_FIELD = {
    "name": "coast_proximity_to_min_spacing",
    "formula": (
        "d(x)=distance a la terre ; r(x)=r_floor+(r_ceil-r_floor)*smoothstep(d(x)/d_ref)"
    ),
    "r_floor_m": G4_SEA_R_FLOOR_M,
    "r_ceil_m": G4_SEA_R_CEIL_M,
    "d_ref": "distance maximale a la terre mesuree sur la mer, grille au pas r_floor",
    "lloyd_iterations": G4_SEA_LLOYD_ITERATIONS,
    "master_seed": G4_SEA_MASTER_SEED,
    "justification": (
        "Meme famille que la maille terrestre : semis a distance minimale "
        "(Bridson a rayon variable), relaxation de Lloyd a iterations FIXES "
        "(determinisme avant convergence), Voronoi decoupe sur l'eau. La "
        "distance a la cote joue pour la mer le role que l'influence urbaine "
        "joue pour la terre : les eaux cotieres, ou tout se touche, sont "
        "decoupees plus fin que le large. Chaque composante d'eau recoit au "
        "moins un germe, sans quoi un bassin enferme n'aurait aucune zone."
    ),
}


# ---------------------------------------------------------------------------
# Chargement des entrées
# ---------------------------------------------------------------------------


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
    return MultiPolygon(
        sorted(polys, key=lambda p: (round(p.centroid.x, 3), round(p.centroid.y, 3)))
    )


def _project_geom(geom: Any, projector: Projector) -> Any:
    def ring(coords):
        return [projector.project_xy(lon, lat) for lon, lat in coords]

    if geom.geom_type == "Polygon":
        return Polygon(ring(geom.exterior.coords), [ring(r.coords) for r in geom.interiors])
    if geom.geom_type == "MultiPolygon":
        parts = [_project_geom(p, projector) for p in geom.geoms]
        return MultiPolygon(
            sorted(parts, key=lambda g: (round(g.centroid.x, 3), round(g.centroid.y, 3)))
        )
    raise TypeError(geom.geom_type)


def _inverse_lonlat(projector: Projector, x: float, y: float) -> Tuple[float, float]:
    lon, lat = projector.unproject_xy(x, y)
    return projector.lonlat_rounded(lon, lat)


def load_context(*, rebuild_land: bool = True) -> Dict[str, Any]:
    """Charge UNE fois la terre 1400, les cellules committées et les héritages."""
    t0 = time.perf_counter()
    projector = Projector(detect_projection())

    g2b = _load_module("corrections_g2b", "steps/02b_corrections_1400.py")
    corrections_doc = g2b.load_corrections()
    topology_corrections = g2b.topology_link_corrections(corrections_doc)

    coast_path = ARTIFACTS / "coastline_1400.json"
    if rebuild_land or not coast_path.is_file():
        run = g2b.run_corrections(apply_corrections=True, clean_build=False)
        land_ll = run["land_ll"]
        lakes_ll = run["lakes_ll"]
        open_sea_ll = run["open_sea_ll"]
        layer_paths = run["layer_paths"]
    else:  # pragma: no cover - chemin de secours, jamais employé par la preuve
        raise RuntimeError(
            "la mer se dérive de la terre corrigée produite par steps/02b "
            "(run_corrections), pas d'un artefact partiel"
        )
    land_xy = _project_geom(land_ll, projector)
    if not land_xy.is_valid:
        land_xy = land_xy.buffer(0)

    coastline_sha_live = sha256_file(coast_path)
    manifest_g3 = read_json(ARTIFACTS / "MANIFEST_g3.json")
    coastline_sha_g3 = str(manifest_g3.get("inputs", {}).get("coastline_1400") or "")

    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    cells = sorted(cells_doc["cells"], key=lambda c: int(c["cell_id"]))
    cell_geoms = [shape(c["geometry"]) for c in cells]
    stats_g3 = read_json(ARTIFACTS / "stats_g3.json")
    adjacency_g3 = read_json(ARTIFACTS / "adjacency_g3.json")["adjacency"]

    legacy_names = read_json(LEGACY / "sea_zones.json")["sea_zones"]
    legacy_coords = read_json(LEGACY / "province_coordinates.json")["coordinates"]
    legacy_graph = read_json(LEGACY / "province_adjacency.json")["adjacency"]

    unity_sea_zones = (
        ROOT.parents[1]
        / "unity"
        / "game_unity"
        / "Assets"
        / "StreamingAssets"
        / "data"
        / "sea_zones.json"
    )
    copy_matches = -1
    if unity_sea_zones.is_file():
        copy_matches = int(sha256_file(unity_sea_zones) == sha256_file(LEGACY / "sea_zones.json"))

    return {
        "projector": projector,
        "land_ll": land_ll,
        "land_xy": land_xy,
        "lakes_ll": lakes_ll,
        "open_sea_ll": open_sea_ll,
        "layer_paths": layer_paths,
        "coastline_sha_live": coastline_sha_live,
        "coastline_sha_g3": coastline_sha_g3,
        "coastline_sha_equal": int(coastline_sha_live == coastline_sha_g3),
        "cells": cells,
        "cell_geoms": cell_geoms,
        "cell_ids": [int(c["cell_id"]) for c in cells],
        "cell_count_declared": int(stats_g3["cell_count"]),
        "adjacency_g3": adjacency_g3,
        "legacy_names": legacy_names,
        "legacy_coords": legacy_coords,
        "legacy_graph": legacy_graph,
        "corrections_doc": corrections_doc,
        "topology_corrections": topology_corrections,
        "copy_sea_zones_identical": copy_matches,
        "elapsed_s": time.perf_counter() - t0,
    }


# ---------------------------------------------------------------------------
# PARTIE 1 — la mer de 1400 (D2)
# ---------------------------------------------------------------------------


def derive_sea(context: Dict[str, Any]) -> Dict[str, Any]:
    """Eau = fenêtre − terre. Mer = eau extérieure ∪ bassins reclassés. Lacs exclus."""
    t0 = time.perf_counter()
    projector = context["projector"]
    land_ll = context["land_ll"]
    lakes_ll = context["lakes_ll"]
    open_sea_ll = context["open_sea_ll"]

    west, south, east, north = PILOT_WINDOW_LONLAT
    window_ll = box(west, south, east, north).segmentize(WINDOW_EDGE_STEP_DEG)
    water_ll = window_ll.difference(land_ll)
    parts_ll = _as_polygons(water_ll)
    border_ll = window_ll.exterior

    components: List[Dict[str, Any]] = []
    excluded_lakes = 0
    excluded_slivers = 0
    enclosed_examined = 0
    excluded_undeclared: List[Dict[str, Any]] = []

    for part_ll in parts_ll:
        touches_border = part_ll.intersection(border_ll).length > 0.0
        if not touches_border:
            # Dénominateur honnête des exclusions : les eaux enclavées examinées.
            enclosed_examined += 1
        geom_xy = _normalize(_project_geom(part_ll, projector))
        area_m2 = float(geom_xy.area)
        if area_m2 < G3_AREA_EPS_M2:
            # Sous la tolérance géométrique déclarée : ce n'est pas un plan d'eau.
            excluded_slivers += 1
            continue
        remainder_lake = part_ll.difference(lakes_ll).area if not lakes_ll.is_empty else part_ll.area
        is_lake = remainder_lake <= 0.0
        reclassified = (
            (not open_sea_ll.is_empty) and part_ll.intersection(open_sea_ll).area > 0.0
        )
        if is_lake:
            excluded_lakes += 1
            continue
        if touches_border:
            kind = "outer"
        elif reclassified:
            kind = "basin"
        else:
            rp = part_ll.representative_point()
            excluded_undeclared.append(
                {
                    "area_km2": round(area_m2 / 1e6, FLOAT_DECIMALS),
                    "lon": round(float(rp.x), FLOAT_DECIMALS),
                    "lat": round(float(rp.y), FLOAT_DECIMALS),
                }
            )
            continue
        rp = part_ll.representative_point()
        components.append(
            {
                "kind": kind,
                "enclosed": kind == "basin",
                "geom_ll": part_ll,
                "geom_xy": geom_xy,
                "area_m2": area_m2,
                "lon": round(float(rp.x), FLOAT_DECIMALS),
                "lat": round(float(rp.y), FLOAT_DECIMALS),
            }
        )

    components.sort(key=lambda c: (-c["area_m2"], c["lon"], c["lat"]))
    for index, comp in enumerate(components):
        comp["component_index"] = index
    sea_geom = _normalize(unary_union([c["geom_xy"] for c in components]))
    if not sea_geom.is_valid:
        sea_geom = sea_geom.buffer(0)

    return {
        "components": components,
        "sea_geom": sea_geom,
        "window_ll": window_ll,
        "excluded_lakes": excluded_lakes,
        "excluded_slivers": excluded_slivers,
        "excluded_undeclared": excluded_undeclared,
        "enclosed_water_examined": enclosed_examined,
        "water_component_count": len(parts_ll),
        "elapsed_s": time.perf_counter() - t0,
    }


# ---------------------------------------------------------------------------
# PARTIE 2 — semis, Lloyd, Voronoï (D3)
# ---------------------------------------------------------------------------


def _smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


class SpacingField:
    """r(x) : distance minimale entre germes, dérivée de la distance à la terre."""

    def __init__(self, cell_geoms: Sequence[Any], sea_geom: Any):
        self._geoms = list(cell_geoms)
        self._tree = STRtree(self._geoms)
        self._d_ref = self._measure_reference_distance(sea_geom)

    def distance_to_land(self, x: float, y: float) -> float:
        pt = Point(x, y)
        idx = self._tree.query_nearest(pt)
        try:
            indexes = list(idx)
        except TypeError:  # pragma: no cover - shapely renvoie un scalaire
            indexes = [idx]
        if not indexes:
            return 0.0
        return min(float(self._geoms[int(i)].distance(pt)) for i in indexes)

    def _measure_reference_distance(self, sea_geom: Any) -> float:
        """d_ref = plus grande distance à la terre mesurée sur la mer (pas r_floor)."""
        minx, miny, maxx, maxy = sea_geom.bounds
        step = G4_SEA_R_FLOOR_M
        prepared = prep(sea_geom)
        best = 0.0
        x = minx + step * 0.5
        while x <= maxx:
            y = miny + step * 0.5
            while y <= maxy:
                if prepared.contains(Point(x, y)):
                    d = self.distance_to_land(x, y)
                    if d > best:
                        best = d
                y += step
            x += step
        return best

    @property
    def reference_distance_m(self) -> float:
        return self._d_ref

    def radius_at(self, x: float, y: float) -> float:
        if self._d_ref <= 0.0:
            return G4_SEA_R_CEIL_M
        t = self.distance_to_land(x, y) / self._d_ref
        return G4_SEA_R_FLOOR_M + (G4_SEA_R_CEIL_M - G4_SEA_R_FLOOR_M) * _smoothstep(t)


def build_sea_seeds(
    components: Sequence[Dict[str, Any]],
    sea_geom: Any,
    spacing: SpacingField,
) -> Dict[str, Any]:
    """Bridson à rayon variable : 1 germe obligatoire par composante, puis remplissage."""
    t0 = time.perf_counter()
    rng = random.Random(G4_SEA_MASTER_SEED)
    prepared = [prep(c["geom_xy"]) for c in components]
    seeds: List[Dict[str, Any]] = []

    def component_of(x: float, y: float) -> Optional[int]:
        pt = Point(x, y)
        for i, pc in enumerate(prepared):
            if pc.contains(pt) or components[i]["geom_xy"].covers(pt):
                return i
        return None

    def far_enough(x: float, y: float, r: float) -> bool:
        for s in seeds:
            dmin = 0.5 * (r + s["r"])
            dx = x - s["x"]
            dy = y - s["y"]
            if dx * dx + dy * dy < dmin * dmin:
                return False
        return True

    def try_add(x: float, y: float, *, forced: bool = False) -> bool:
        ci = component_of(x, y)
        if ci is None:
            return False
        r = spacing.radius_at(x, y)
        if not forced and not far_enough(x, y, r):
            return False
        seeds.append(
            {
                "x": round(float(x), FLOAT_DECIMALS),
                "y": round(float(y), FLOAT_DECIMALS),
                "r": r,
                "component_index": ci,
                "origin": "mandatory" if forced else "poisson",
            }
        )
        return True

    # 1) Un germe obligatoire par composante d'eau (D3) — sinon un bassin disparaît.
    mandatory = 0
    for comp in components:
        rp = comp["geom_xy"].representative_point()
        if try_add(rp.x, rp.y, forced=True):
            mandatory += 1

    # 2) Bridson : file active, k essais par germe actif, plafond lu de constants.
    active = list(range(len(seeds)))
    k_attempts = 30
    while active and len(seeds) < SEA_ZONE_COUNT_MAX:
        ai = rng.randrange(len(active))
        parent = seeds[active[ai]]
        placed = False
        for _ in range(k_attempts):
            angle = rng.random() * 2.0 * math.pi
            radius = parent["r"] * (1.0 + rng.random())
            cx = parent["x"] + radius * math.cos(angle)
            cy = parent["y"] + radius * math.sin(angle)
            if try_add(cx, cy):
                active.append(len(seeds) - 1)
                placed = True
                break
        if not placed:
            active.pop(ai)

    # 3) Plancher de compte : densification par grille au pas r_floor si besoin.
    if len(seeds) < SEA_ZONE_COUNT_MIN:
        minx, miny, maxx, maxy = sea_geom.bounds
        step = G4_SEA_R_FLOOR_M
        candidates: List[Tuple[float, float]] = []
        x = minx + step * 0.5
        while x <= maxx:
            y = miny + step * 0.5
            while y <= maxy:
                candidates.append((x, y))
                y += step
            x += step
        for cx, cy in sorted(candidates):
            if len(seeds) >= SEA_ZONE_COUNT_MAX:
                break
            try_add(cx, cy)

    return {
        "seeds": seeds,
        "mandatory": mandatory,
        "count": len(seeds),
        "saturated_at_ceiling": len(seeds) >= SEA_ZONE_COUNT_MAX,
        "d_ref_m": round(spacing.reference_distance_m, FLOAT_DECIMALS),
        "elapsed_s": time.perf_counter() - t0,
    }


def relax_seeds(
    components: Sequence[Dict[str, Any]],
    seeds: Sequence[Dict[str, Any]],
    voronoi_on_part,
) -> List[Dict[str, Any]]:
    """Lloyd à nombre d'itérations FIXE (déterminisme avant convergence)."""
    current = [dict(s) for s in seeds]
    for _ in range(G4_SEA_LLOYD_ITERATIONS):
        for ci, comp in enumerate(components):
            local = [i for i, s in enumerate(current) if s["component_index"] == ci]
            if not local:
                continue
            if len(local) == 1:
                rp = comp["geom_xy"].representative_point()
                current[local[0]]["x"] = round(float(rp.x), FLOAT_DECIMALS)
                current[local[0]]["y"] = round(float(rp.y), FLOAT_DECIMALS)
                continue
            pts = [(current[i]["x"], current[i]["y"]) for i in local]
            cell_map = voronoi_on_part(comp["geom_xy"], pts)
            for local_i, geom in cell_map.items():
                centroid = geom.centroid
                if not geom.covers(centroid):
                    centroid = geom.representative_point()
                gi = local[local_i]
                current[gi]["x"] = round(float(centroid.x), FLOAT_DECIMALS)
                current[gi]["y"] = round(float(centroid.y), FLOAT_DECIMALS)
    return current


def build_zones(
    components: Sequence[Dict[str, Any]],
    seeds: Sequence[Dict[str, Any]],
    voronoi_on_part,
    projector: Projector,
) -> List[Dict[str, Any]]:
    """Voronoï découpé sur chaque composante d'eau — couverture exacte, sans trou."""
    zones: List[Dict[str, Any]] = []
    for ci, comp in enumerate(components):
        local = [s for s in seeds if s["component_index"] == ci]
        if not local:
            raise RuntimeError(f"composante d'eau {ci} sans germe")
        if len(local) == 1:
            geoms = {0: _normalize(comp["geom_xy"])}
        else:
            geoms = voronoi_on_part(comp["geom_xy"], [(s["x"], s["y"]) for s in local])
        for local_i, geom in sorted(geoms.items()):
            geom = _normalize(geom)
            if geom.is_empty or geom.area <= 0:
                continue
            perimeter = geom.length
            area = float(geom.area)
            compactness = (4.0 * math.pi * area / (perimeter * perimeter)) if perimeter > 0 else 0.0
            centroid = geom.centroid
            lon, lat = _inverse_lonlat(projector, centroid.x, centroid.y)
            zones.append(
                {
                    "component_index": ci,
                    "enclosed": bool(comp["enclosed"]),
                    "component_kind": comp["kind"],
                    "alone_in_component": len(local) == 1,
                    "area_m2": round(area, FLOAT_DECIMALS),
                    "area_km2": round(area / 1e6, FLOAT_DECIMALS),
                    "compactness_polsby_popper": round(compactness, FLOAT_DECIMALS),
                    "centroid": {
                        "x_m": round(float(centroid.x), FLOAT_DECIMALS),
                        "y_m": round(float(centroid.y), FLOAT_DECIMALS),
                        "lon": lon,
                        "lat": lat,
                    },
                    "geometry": mapping(geom),
                    "_geom": geom,
                }
            )
    # Ordre STABLE et géométrique (D4) : y puis x, arrondis à la décimale d'export.
    zones.sort(
        key=lambda z: (
            round(z["centroid"]["y_m"], FLOAT_DECIMALS),
            round(z["centroid"]["x_m"], FLOAT_DECIMALS),
        )
    )
    return zones


def assign_zone_ids(zones: List[Dict[str, Any]], cell_ids: Sequence[int]) -> None:
    """Ids depuis la base lue, en SAUTANT tout identifiant terrestre déjà pris (D4)."""
    taken = set(int(c) for c in cell_ids)
    next_id = SEA_ZONE_ID_BASE
    for zone in zones:
        while next_id in taken:
            next_id += 1
        zone["zone_id"] = next_id
        taken.add(next_id)
        next_id += 1


# ---------------------------------------------------------------------------
# PARTIE 3 — noms hérités (D5)
# ---------------------------------------------------------------------------


def build_name_anchors(
    legacy_names: Sequence[dict], legacy_coords: Sequence[dict], projector: Projector
) -> List[Dict[str, Any]]:
    """Ancrage d'un nom hérité = moyenne des coordonnées héritées qu'il déclare."""
    by_id = {int(c["id"]): c for c in legacy_coords}
    anchors: List[Dict[str, Any]] = []
    for entry in sorted(legacy_names, key=lambda z: int(z["id"])):
        ids = [int(i) for i in entry.get("coastal_provinces", [])]
        known = [by_id[i] for i in sorted(ids) if i in by_id]
        if not known:
            anchors.append(
                {
                    "legacy_id": int(entry["id"]),
                    "name": entry["name"],
                    "is_ocean": bool(entry.get("is_ocean")),
                    "anchor": None,
                    "anchor_source_count": 0,
                }
            )
            continue
        lon = sum(float(c["lon"]) for c in known) / len(known)
        lat = sum(float(c["lat"]) for c in known) / len(known)
        x, y = projector.project_xy(lon, lat)
        anchors.append(
            {
                "legacy_id": int(entry["id"]),
                "name": entry["name"],
                "is_ocean": bool(entry.get("is_ocean")),
                "anchor": Point(x, y),
                "anchor_lon": round(lon, FLOAT_DECIMALS),
                "anchor_lat": round(lat, FLOAT_DECIMALS),
                "anchor_source_count": len(known),
            }
        )
    return anchors


def name_zones(zones: Sequence[Dict[str, Any]], anchors: Sequence[Dict[str, Any]]) -> None:
    """Chaque zone prend le nom de l'ancrage le plus proche ; égalité → plus petit id."""
    usable = [a for a in anchors if a["anchor"] is not None]
    for zone in zones:
        if not usable:
            zone["name"] = None
            zone["is_ocean"] = False
            zone["name_anchor_distance_m"] = -1.0
            zone["name_source"] = "legacy_game_data/sea_zones.json"
            continue
        best = min(
            usable,
            key=lambda a: (
                round(zone["_geom"].distance(a["anchor"]), 3),
                a["legacy_id"],
            ),
        )
        zone["name"] = best["name"]
        zone["is_ocean"] = bool(best["is_ocean"])
        zone["name_anchor_distance_m"] = round(
            float(zone["_geom"].distance(best["anchor"])), FLOAT_DECIMALS
        )
        zone["name_source"] = "legacy_game_data/sea_zones.json"


# ---------------------------------------------------------------------------
# PARTIE 4 — les quatre natures d'arête (D6, D7)
# ---------------------------------------------------------------------------


def read_land_land_edges(adjacency_g3: Sequence[dict]) -> List[dict]:
    """land-land est LU du graphe committé, jamais recalculé (D6)."""
    edges: List[dict] = []
    for edge in adjacency_g3:
        if edge.get("kind") != "land-land":
            continue
        a, b = sorted((int(edge["a"]), int(edge["b"])))
        if SEA_CELL_ID in (a, b):
            continue
        edges.append(
            {
                "a": a,
                "b": b,
                "kind": "land-land",
                "shared_length_m": round(float(edge.get("shared_length_m", 0.0)), FLOAT_DECIMALS),
            }
        )
    return edges


def derive_land_sea_edges(
    cells: Sequence[dict], cell_geoms: Sequence[Any], zones: Sequence[Dict[str, Any]]
) -> List[dict]:
    tree = STRtree(list(cell_geoms))
    edges: List[dict] = []
    for zone in zones:
        geom = zone["_geom"]
        boundary = geom.boundary
        for idx in tree.query(geom, predicate="intersects"):
            cell_geom = cell_geoms[int(idx)]
            inter = boundary.intersection(cell_geom.boundary)
            length = 0.0 if inter.is_empty else float(inter.length)
            if length < LENGTH_EPS:
                continue
            a, b = sorted((int(cells[int(idx)]["cell_id"]), int(zone["zone_id"])))
            edges.append(
                {
                    "a": a,
                    "b": b,
                    "kind": "land-sea",
                    "shared_length_m": round(length, FLOAT_DECIMALS),
                }
            )
    return edges


def derive_sea_sea_edges(zones: Sequence[Dict[str, Any]]) -> List[dict]:
    geoms = [z["_geom"] for z in zones]
    tree = STRtree(geoms)
    edges: List[dict] = []
    for i, zone in enumerate(zones):
        for idx in tree.query(zone["_geom"], predicate="intersects"):
            j = int(idx)
            if j <= i:
                continue
            inter = zone["_geom"].boundary.intersection(geoms[j].boundary)
            length = 0.0 if inter.is_empty else float(inter.length)
            if length < LENGTH_EPS:
                continue
            a, b = sorted((int(zone["zone_id"]), int(zones[j]["zone_id"])))
            edges.append(
                {
                    "a": a,
                    "b": b,
                    "kind": "sea-sea",
                    "shared_length_m": round(length, FLOAT_DECIMALS),
                    "declared_topology_link": False,
                }
            )
    return edges


def land_components(cell_ids: Sequence[int], land_land_edges: Sequence[dict]) -> Dict[int, int]:
    """Masses terrestres = composantes connexes du graphe terre-terre."""
    neighbors: Dict[int, List[int]] = {int(c): [] for c in cell_ids}
    for edge in land_land_edges:
        neighbors.setdefault(edge["a"], []).append(edge["b"])
        neighbors.setdefault(edge["b"], []).append(edge["a"])
    label: Dict[int, int] = {}
    current = 0
    for start in sorted(neighbors.keys()):
        if start in label:
            continue
        stack = [start]
        label[start] = current
        while stack:
            node = stack.pop()
            for nxt in neighbors.get(node, []):
                if nxt not in label:
                    label[nxt] = current
                    stack.append(nxt)
        current += 1
    return label


def derive_strait_edges(
    cells: Sequence[dict],
    cell_geoms: Sequence[Any],
    mass_of: Dict[int, int],
) -> List[dict]:
    """Détroit : deux terres NON contiguës séparées d'au plus le seuil lu (D7)."""
    tree = STRtree(list(cell_geoms))
    edges: List[dict] = []
    for i, cell in enumerate(cells):
        for idx in tree.query(cell_geoms[i], predicate="dwithin", distance=G4_STRAIT_MAX_WIDTH_M):
            j = int(idx)
            if j <= i:
                continue
            gap = float(cell_geoms[i].distance(cell_geoms[j]))
            if gap < LENGTH_EPS or gap > G4_STRAIT_MAX_WIDTH_M:
                continue
            id_a = int(cell["cell_id"])
            id_b = int(cells[j]["cell_id"])
            a, b = sorted((id_a, id_b))
            edges.append(
                {
                    "a": a,
                    "b": b,
                    "kind": "strait",
                    "gap_m": round(gap, FLOAT_DECIMALS),
                    "crosses_land_masses": bool(mass_of.get(id_a) != mass_of.get(id_b)),
                }
            )
    return edges


# ---------------------------------------------------------------------------
# PARTIE 5 — liens topologiques déclarés et atteignabilité (D8)
# ---------------------------------------------------------------------------


def _named_water_geometry(context: Dict[str, Any], name: str) -> Optional[Any]:
    """Géométrie lon/lat de l'eau nommée par une correction (couche source, lue)."""
    import geopandas as gpd

    layer = context["layer_paths"].get("ne_10m_lakes")
    if layer is None:
        return None
    gdf = gpd.read_file(layer)
    if "name" not in gdf.columns:
        return None
    hits = gdf[gdf["name"] == name]
    if hits.empty:
        return None
    return unary_union([g for g in hits.geometry if g is not None and not g.is_empty])


def apply_topology_links(
    context: Dict[str, Any],
    sea: Dict[str, Any],
    zones: Sequence[Dict[str, Any]],
    apply_links: bool,
) -> Dict[str, Any]:
    """Chaque déclaration historique devient une arête sea-sea marquée, ou rien."""
    links: List[Dict[str, Any]] = []
    edges: List[dict] = []
    if not apply_links:
        return {"links": links, "edges": edges, "target_name_found": 0}

    target_found = 0
    for correction in context["topology_corrections"]:
        from_water = correction.get("from_water") or {}
        to_water = correction.get("to_water") or {}
        basin_name = str(from_water.get("name") or "")
        target_name = str(to_water.get("name") or "")

        water_ll = _named_water_geometry(context, basin_name)
        basin_zone = None
        if water_ll is not None:
            candidates = [
                z
                for z in zones
                if sea["components"][z["component_index"]]["geom_ll"].intersects(water_ll)
                and sea["components"][z["component_index"]]["enclosed"]
            ]
            if candidates:
                basin_zone = min(
                    candidates,
                    key=lambda z: (
                        -sea["components"][z["component_index"]]["geom_ll"]
                        .intersection(water_ll)
                        .area,
                        int(z["zone_id"]),
                    ),
                )
        if basin_zone is None:
            raise RuntimeError(
                f"declaration {correction.get('id')} : bassin '{basin_name}' introuvable "
                "parmi les composantes d'eau enfermées"
            )

        outer_named = [
            z
            for z in zones
            if not z["enclosed"] and z.get("name") == target_name
        ]
        if not outer_named:
            raise RuntimeError(
                f"declaration {correction.get('id')} : aucune zone de mer exterieure "
                f"ne porte le nom atteste '{target_name}'"
            )
        target_found += 1
        target_zone = min(
            outer_named,
            key=lambda z: (
                round(z["_geom"].distance(basin_zone["_geom"]), 3),
                int(z["zone_id"]),
            ),
        )
        a, b = sorted((int(basin_zone["zone_id"]), int(target_zone["zone_id"])))
        gap = float(basin_zone["_geom"].distance(target_zone["_geom"]))
        edges.append(
            {
                "a": a,
                "b": b,
                "kind": "sea-sea",
                "gap_m": round(gap, FLOAT_DECIMALS),
                "declared_topology_link": True,
                "link_id": correction.get("id"),
                "source": correction.get("source"),
                "date": correction.get("date"),
                "certainty": correction.get("certainty"),
            }
        )
        links.append(
            {
                "id": correction.get("id"),
                "source": correction.get("source"),
                "date": correction.get("date"),
                "certainty": correction.get("certainty"),
                "historical_reason": correction.get("historical_reason"),
                "enclosed_water_name": basin_name,
                "attested_target_name": target_name,
                "zone_a": int(basin_zone["zone_id"]),
                "zone_b": int(target_zone["zone_id"]),
                "gap_m": round(gap, FLOAT_DECIMALS),
            }
        )
    links.sort(key=lambda link: str(link["id"]))
    edges.sort(key=lambda e: (e["a"], e["b"]))
    return {"links": links, "edges": edges, "target_name_found": target_found}


def compute_reachability(
    zones: Sequence[Dict[str, Any]], sea_sea_edges: Sequence[dict]
) -> Dict[str, Any]:
    """Une eau enfermée n'est atteignable que par un chemin d'eau, déclaré ou non."""
    neighbors: Dict[int, List[int]] = {int(z["zone_id"]): [] for z in zones}
    for edge in sea_sea_edges:
        neighbors.setdefault(int(edge["a"]), []).append(int(edge["b"]))
        neighbors.setdefault(int(edge["b"]), []).append(int(edge["a"]))
    sources = [int(z["zone_id"]) for z in zones if not z["enclosed"]]
    seen = set(sources)
    stack = list(sources)
    while stack:
        node = stack.pop()
        for nxt in neighbors.get(node, []):
            if nxt not in seen:
                seen.add(nxt)
                stack.append(nxt)
    enclosed = [int(z["zone_id"]) for z in zones if z["enclosed"]]
    unreachable = sorted(z for z in enclosed if z not in seen)
    by_id = {int(z["zone_id"]): z for z in zones}
    return {
        "all_enclosed_reachable": len(unreachable) == 0,
        "enclosed_zone_ids": sorted(enclosed),
        "unreachable_enclosed_zone_ids": unreachable,
        "unreachable_named": [
            {
                "zone_id": zid,
                "name": by_id[zid].get("name"),
                "area_km2": by_id[zid]["area_km2"],
                "lon": by_id[zid]["centroid"]["lon"],
                "lat": by_id[zid]["centroid"]["lat"],
            }
            for zid in unreachable
        ],
        "outer_zone_ids": sorted(sources),
    }


# ---------------------------------------------------------------------------
# PARTIE 6 — confrontation QA au graphe hérité (D10)
# ---------------------------------------------------------------------------


def build_divergence(
    context: Dict[str, Any], land_land_edges: Sequence[dict]
) -> Dict[str, Any]:
    """Comparaison UNIQUE au graphe hérité — jamais une autorité spatiale."""
    projector = context["projector"]
    cells = context["cells"]
    cell_geoms = context["cell_geoms"]
    tree = STRtree(list(cell_geoms))

    located: Dict[int, Dict[str, Any]] = {}
    unlocated: List[Dict[str, Any]] = []
    for entry in sorted(context["legacy_coords"], key=lambda c: int(c["id"])):
        lon = float(entry["lon"])
        lat = float(entry["lat"])
        x, y = projector.project_xy(lon, lat)
        pt = Point(x, y)
        hit = None
        for idx in tree.query(pt, predicate="intersects"):
            hit = int(cells[int(idx)]["cell_id"])
            break
        nearest_idx = tree.query_nearest(pt)
        try:
            nearest_list = list(nearest_idx)
        except TypeError:  # pragma: no cover
            nearest_list = [nearest_idx]
        nearest_cell = int(cells[int(nearest_list[0])]["cell_id"]) if nearest_list else None
        nearest_distance = (
            round(float(cell_geoms[int(nearest_list[0])].distance(pt)), FLOAT_DECIMALS)
            if nearest_list
            else -1.0
        )
        record = {
            "legacy_province_id": int(entry["id"]),
            "legacy_province_name": entry.get("name"),
            "cell_id": hit,
            "nearest_cell_id": nearest_cell,
            "nearest_cell_distance_m": nearest_distance,
        }
        if hit is None:
            unlocated.append(record)
        else:
            located[int(entry["id"])] = record

    derived = {(e["a"], e["b"]) for e in land_land_edges}
    legacy_pairs = set()
    for entry in context["legacy_graph"]:
        a = int(entry["id"])
        for b in entry.get("neighbors", []):
            pair = tuple(sorted((a, int(b))))
            legacy_pairs.add(pair)

    confirmed: List[dict] = []
    contradicted: List[dict] = []
    missing: List[dict] = []
    for a, b in sorted(legacy_pairs):
        ra = located.get(a)
        rb = located.get(b)
        if ra is None or rb is None:
            missing.append(
                {
                    "legacy_province_a": a,
                    "legacy_province_b": b,
                    "reason": "au moins une province heritee n'est situee dans aucune cellule",
                }
            )
            continue
        cell_a = int(ra["cell_id"])
        cell_b = int(rb["cell_id"])
        if cell_a == cell_b:
            contradicted.append(
                {
                    "legacy_province_a": a,
                    "legacy_province_b": b,
                    "cell_a": cell_a,
                    "cell_b": cell_b,
                    "reason": "les deux provinces heritees tombent dans la meme cellule",
                }
            )
            continue
        pair = tuple(sorted((cell_a, cell_b)))
        if pair in derived:
            confirmed.append(
                {
                    "legacy_province_a": a,
                    "legacy_province_b": b,
                    "cell_a": pair[0],
                    "cell_b": pair[1],
                }
            )
        else:
            contradicted.append(
                {
                    "legacy_province_a": a,
                    "legacy_province_b": b,
                    "cell_a": pair[0],
                    "cell_b": pair[1],
                    "reason": "cellules situees mais non contigues dans le graphe derive",
                }
            )

    legacy_straits = set()
    for entry in context["legacy_graph"]:
        a = int(entry["id"])
        for b in entry.get("straits", []):
            legacy_straits.add(tuple(sorted((a, int(b)))))

    return {
        "qa_only": True,
        "pipeline_version": G4_PIPELINE_VERSION,
        "comment": (
            "Comparaison QA UNIQUE entre le graphe terre-terre derive (cellules, "
            "seule cle spatiale, ADR-0003) et le graphe herite du jeu "
            "(province_adjacency.json). Ce fichier n'est lu que par la preuve QA "
            "tests/run_proof_g4.py. Il n'est jamais une autorite spatiale, il ne "
            "fonde aucune appartenance, et aucun autre artefact ne le consomme. "
            "La localisation d'un identifiant herite se fait par la cellule qui "
            "contient sa coordonnee ; la distance a la cellule la plus proche est "
            "reportee comme repere de comparaison, jamais comme appartenance."
        ),
        "legacy_source": "legacy_game_data/province_adjacency.json",
        "legacy_coordinates_source": "legacy_game_data/province_coordinates.json",
        "legacy_edge_count": len(legacy_pairs),
        "confirmed_count": len(confirmed),
        "contradicted_count": len(contradicted),
        "missing_count": len(missing),
        "located_count": len(located),
        "unlocated_count": len(unlocated),
        "legacy_strait_pair_count": len(legacy_straits),
        "confirmed": confirmed,
        "contradicted": contradicted,
        "missing": missing,
        "unlocated": unlocated,
        "localisation": [located[k] for k in sorted(located.keys())],
    }


# ---------------------------------------------------------------------------
# PARTIE 7 — statistiques, export, captures
# ---------------------------------------------------------------------------


def _distribution(values: Sequence[float]) -> Dict[str, float]:
    vals = sorted(float(v) for v in values)
    if not vals:
        return {"count": 0, "min": -1.0, "median": -1.0, "max": -1.0}
    mid = len(vals) // 2
    median = vals[mid] if len(vals) % 2 else (vals[mid - 1] + vals[mid]) / 2.0
    return {
        "count": len(vals),
        "min": round(vals[0], FLOAT_DECIMALS),
        "median": round(median, FLOAT_DECIMALS),
        "max": round(vals[-1], FLOAT_DECIMALS),
    }


def compute_metrics(
    zones: Sequence[Dict[str, Any]],
    edges: Sequence[dict],
    sea: Dict[str, Any],
    context: Dict[str, Any],
    seeding: Dict[str, Any],
    reachability: Dict[str, Any],
    links: Sequence[dict],
) -> Dict[str, Any]:
    by_kind = {
        "land-land": 0,
        "land-sea": 0,
        "sea-sea": 0,
        "strait": 0,
    }
    for edge in edges:
        by_kind[edge["kind"]] = by_kind.get(edge["kind"], 0) + 1
    zone_ids = {int(z["zone_id"]) for z in zones}
    coastal = sorted(
        {
            (edge["a"] if edge["a"] not in zone_ids else edge["b"])
            for edge in edges
            if edge["kind"] == "land-sea"
        }
    )
    strait_gaps = [float(e["gap_m"]) for e in edges if e["kind"] == "strait"]
    cross_mass = sum(
        1 for e in edges if e["kind"] == "strait" and e.get("crosses_land_masses")
    )
    exempt_ids = [
        int(z["zone_id"])
        for z in zones
        if z["alone_in_component"] and z["enclosed"]
    ]
    out_of_intent = [
        int(z["zone_id"])
        for z in zones
        if int(z["zone_id"]) not in exempt_ids
        and (
            float(z["area_km2"]) < G4_SEA_AREA_FLOOR_KM2
            or float(z["area_km2"]) > G4_SEA_AREA_CEIL_KM2
            or float(z["compactness_polsby_popper"]) < G4_SEA_COMPACTNESS_MIN
        )
    ]
    names_used = sorted({z["name"] for z in zones if z.get("name")})
    attested = sorted({str(z["name"]) for z in context["legacy_names"]})
    placeholder_edges = sum(
        1 for e in edges if SEA_CELL_ID in (int(e["a"]), int(e["b"]))
    )
    return {
        "pipeline_version": G4_PIPELINE_VERSION,
        "sea_zone_count": len(zones),
        "adjacency_count": len(edges),
        "by_kind": by_kind,
        "coastal_cell_count": len(coastal),
        "coastal_cell_ids": coastal,
        "cell_count_read": len(context["cells"]),
        "cell_count_declared_g3": context["cell_count_declared"],
        "sea_component_count": len(sea["components"]),
        "sea_component_covered_count": len({int(z["component_index"]) for z in zones}),
        "enclosed_component_count": sum(1 for c in sea["components"] if c["enclosed"]),
        "excluded_lake_count": sea["excluded_lakes"],
        "excluded_sliver_count": sea["excluded_slivers"],
        "excluded_undeclared_count": len(sea["excluded_undeclared"]),
        "enclosed_water_examined_count": sea["enclosed_water_examined"],
        # Dénominateur exact des exclusions : les composantes d'eau au-dessus de
        # la tolérance géométrique, donc lacs exclus + composantes retenues.
        "water_bodies_examined_count": sea["water_component_count"]
        - sea["excluded_slivers"],
        "water_component_count": sea["water_component_count"],
        "sea_area_km2": round(float(sum(c["area_m2"] for c in sea["components"])) / 1e6, 3),
        "area_km2": _distribution([z["area_km2"] for z in zones]),
        "compactness_polsby_popper": _distribution(
            [z["compactness_polsby_popper"] for z in zones]
        ),
        "seed_count": seeding["count"],
        "seed_mandatory_count": seeding["mandatory"],
        "seed_saturated_at_ceiling": seeding["saturated_at_ceiling"],
        "spacing_reference_distance_m": seeding["d_ref_m"],
        "strait_threshold_m": G4_STRAIT_MAX_WIDTH_M,
        "strait_threshold_justification": G4_STRAIT_JUSTIFICATION,
        "strait_gap_min_m": round(min(strait_gaps), FLOAT_DECIMALS) if strait_gaps else -1.0,
        "strait_gap_max_m": round(max(strait_gaps), FLOAT_DECIMALS) if strait_gaps else -1.0,
        "strait_between_distinct_masses": cross_mass,
        "declared_topology_link_count": len(links),
        "declared_topology_link_available": len(context["topology_corrections"]),
        "enclosed_unreachable_count": len(reachability["unreachable_enclosed_zone_ids"]),
        "zones_named": sum(1 for z in zones if z.get("name")),
        "names_attested_read": len(attested),
        "names_used_distinct": len([n for n in names_used if n in attested]),
        "names_attested_unused": len([n for n in attested if n not in names_used]),
        "names_outside_attested_list": [n for n in names_used if n not in attested],
        "zones_out_of_intent_bounds": len(out_of_intent),
        "zones_out_of_intent_bound_ids": out_of_intent,
        "zones_exempt_whole_enclosed_basin": len(exempt_ids),
        "intent_bounds": {
            "area_floor_km2": G4_SEA_AREA_FLOOR_KM2,
            "area_ceil_km2": G4_SEA_AREA_CEIL_KM2,
            "compactness_min": G4_SEA_COMPACTNESS_MIN,
            "blocking": False,
        },
        "id_range": {
            "min": min(int(z["zone_id"]) for z in zones) if zones else -1,
            "max": max(int(z["zone_id"]) for z in zones) if zones else -1,
            "base_read": SEA_ZONE_ID_BASE,
        },
        "count_bounds_read": {
            "min": SEA_ZONE_COUNT_MIN,
            "max": SEA_ZONE_COUNT_MAX,
        },
        "edges_with_g3_sea_placeholder": placeholder_edges,
        "coastline_1400_sha_equals_g3_input": context["coastline_sha_equal"],
    }


def export_g4(
    *,
    zones: Sequence[Dict[str, Any]],
    edges: Sequence[dict],
    metrics: Dict[str, Any],
    links: Sequence[dict],
    reachability: Dict[str, Any],
    divergence: Dict[str, Any],
    context: Dict[str, Any],
) -> Dict[str, str]:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    REGISTRY.mkdir(parents=True, exist_ok=True)
    projector = context["projector"]
    shas: Dict[str, str] = {}

    zones_out = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "data_class": "natural_earth_g4_sea_zones",
        "comment": (
            "Zones de mer G4 — Voronoi decoupe sur l'eau de 1400. Identifiants "
            "hors de la plage des cellules. Les noms sont un proxy herite du jeu "
            "(voir README), jamais une cle spatiale."
        ),
        "projection": projector.info.epsg,
        "crs": crs_declaration(geometry_crs=projector.info.epsg, has_geometry_lonlat=False),
        "spacing_field": SEA_SPACING_FIELD,
        "sea_zones": [
            {
                "zone_id": int(z["zone_id"]),
                "name": z.get("name"),
                "is_ocean": bool(z.get("is_ocean")),
                "enclosed": bool(z["enclosed"]),
                "component_index": int(z["component_index"]),
                "area_m2": z["area_m2"],
                "area_km2": z["area_km2"],
                "compactness_polsby_popper": z["compactness_polsby_popper"],
                "centroid": z["centroid"],
                "name_source": z["name_source"],
                "name_anchor_distance_m": z["name_anchor_distance_m"],
                "geometry": z["geometry"],
            }
            for z in sorted(zones, key=lambda z: int(z["zone_id"]))
        ],
    }
    shas["artifacts/sea_zones_g4.json"] = write_json(ARTIFACTS / "sea_zones_g4.json", zones_out)

    adjacency_out = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "data_class": "natural_earth_g4_adjacency",
        "comment": (
            "Adjacence typee G4 : land-land (lu de adjacency_g3.json), land-sea, "
            "sea-sea (geometrie + liens historiques declares), strait (seuil lu). "
            "Aucune cle spatiale autre que la cellule et la zone de mer."
        ),
        "kinds": ["land-land", "land-sea", "sea-sea", "strait"],
        "adjacency": sorted(edges, key=lambda e: (e["kind"], e["a"], e["b"])),
    }
    shas["artifacts/adjacency_g4.json"] = write_json(
        ARTIFACTS / "adjacency_g4.json", adjacency_out
    )

    links_out = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "data_class": "natural_earth_g4_topology_links",
        "comment": (
            "Liens topologiques declares : une source historique atteste une "
            "communication que la geometrie moderne ne montre plus. La geometrie "
            "n'est pas retouchee ; la continuite est declaree."
        ),
        "links": list(links),
        "reachability": reachability,
    }
    shas["artifacts/topology_links_g4.json"] = write_json(
        ARTIFACTS / "topology_links_g4.json", links_out
    )

    shas["artifacts/stats_g4.json"] = write_json(ARTIFACTS / "stats_g4.json", metrics)
    shas["artifacts/adjacency_divergence_g4.json"] = write_json(
        ARTIFACTS / "adjacency_divergence_g4.json", divergence
    )

    registry = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "created": G4_REGISTRY_CREATED,
        "comment": "Registre des zones de mer emises par G4 (identifiants stables).",
        "zones": [
            {
                "zone_id": int(z["zone_id"]),
                "domain_key": (
                    f"sea:{z['centroid']['lon']:.6f},{z['centroid']['lat']:.6f}"
                ),
                "created": G4_REGISTRY_CREATED,
                "enclosed": bool(z["enclosed"]),
                "name": z.get("name"),
            }
            for z in sorted(zones, key=lambda z: int(z["zone_id"]))
        ],
    }
    shas["registry/sea_zone_registry.json"] = write_json(REGISTRY_PATH, registry)

    manifest = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "data_class": "natural_earth_g4_adjacency",
        "comment": (
            "MANIFEST G4 — fixed_timestamp fige ; timings exclus. Les entrees "
            "heritees sont designees par leur role (frontiere ADR-0003) ; les "
            "chemins correspondants sont listes dans README.md."
        ),
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
        },
        "inputs": {
            "coastline_1400": context["coastline_sha_live"],
            "cells_g3": sha256_file(ARTIFACTS / "cells_g3.json"),
            "adjacency_g3": sha256_file(ARTIFACTS / "adjacency_g3.json"),
            "stats_g3": sha256_file(ARTIFACTS / "stats_g3.json"),
            "corrections_1400": sha256_file(DATA / "corrections_1400.json"),
            "legacy_sea_zone_names": sha256_file(LEGACY / "sea_zones.json"),
            "legacy_name_anchors": sha256_file(LEGACY / "province_coordinates.json"),
            "legacy_reference_graph": sha256_file(LEGACY / "province_adjacency.json"),
        },
        "outputs": {k: shas[k] for k in sorted(shas.keys())},
        "coastline_1400_sha_declared_by_g3": context["coastline_sha_g3"],
        "coastline_1400_sha_equal": context["coastline_sha_equal"],
    }
    shas["artifacts/MANIFEST_g4.json"] = write_json(ARTIFACTS / "MANIFEST_g4.json", manifest)
    return shas


def write_captures(
    context: Dict[str, Any],
    sea: Dict[str, Any],
    zones: Sequence[Dict[str, Any]],
    edges: Sequence[dict],
    *,
    apply_links: bool,
) -> Dict[str, Path]:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.colors import hsv_to_rgb
    from matplotlib.patches import Polygon as MplPolygon

    CAPTURE.mkdir(parents=True, exist_ok=True)
    paths: Dict[str, Path] = {}
    land_ll = context["land_ll"]
    zone_ll = {
        int(z["zone_id"]): _as_polygons(sea["components"][z["component_index"]]["geom_ll"])
        for z in zones
    }
    projector = context["projector"]

    def zone_rings(zone: Dict[str, Any]) -> List[List[Tuple[float, float]]]:
        rings = []
        for poly in _as_polygons(zone["_geom"]):
            rings.append([projector.unproject_xy(x, y) for x, y in poly.exterior.coords])
        return rings

    def draw(ax, xlim, ylim, title, *, label_zones: bool):
        ax.set_aspect("equal")
        ax.set_facecolor("#e3f2fd")
        ax.set_title(title, fontsize=10)
        ax.set_xlim(*xlim)
        ax.set_ylim(*ylim)
        ax.grid(True, alpha=0.2)
        for poly in _as_polygons(land_ll):
            ax.add_patch(
                MplPolygon(
                    list(zip(*poly.exterior.xy)),
                    closed=True,
                    facecolor="#dcedc8",
                    edgecolor="#33691e",
                    linewidth=0.3,
                )
            )
        for zone in sorted(zones, key=lambda z: int(z["zone_id"])):
            hue = ((int(zone["zone_id"]) * 61) % 360) / 360.0
            rgb = tuple(hsv_to_rgb((hue, 0.55, 0.95)))
            for ring in zone_rings(zone):
                ax.add_patch(
                    MplPolygon(
                        ring,
                        closed=True,
                        facecolor=rgb,
                        edgecolor="#0d47a1",
                        linewidth=0.4,
                        alpha=0.55,
                    )
                )
        by_id = {int(z["zone_id"]): z for z in zones}
        for edge in edges:
            if edge["kind"] != "sea-sea":
                continue
            za = by_id.get(int(edge["a"]))
            zb = by_id.get(int(edge["b"]))
            if za is None or zb is None:
                continue
            declared = bool(edge.get("declared_topology_link"))
            ax.plot(
                [za["centroid"]["lon"], zb["centroid"]["lon"]],
                [za["centroid"]["lat"], zb["centroid"]["lat"]],
                color="#b71c1c" if declared else "#1565c0",
                linewidth=1.8 if declared else 0.7,
                linestyle="-" if declared else "--",
                alpha=0.95 if declared else 0.6,
            )
        if label_zones:
            for zone in sorted(zones, key=lambda z: int(z["zone_id"])):
                lon = zone["centroid"]["lon"]
                lat = zone["centroid"]["lat"]
                # Une etiquette dont le centre sort du cadre serait dessinee hors
                # des axes : constate a l'oeil sur la premiere capture (regle 11).
                if not (xlim[0] <= lon <= xlim[1] and ylim[0] <= lat <= ylim[1]):
                    continue
                ax.text(
                    zone["centroid"]["lon"],
                    zone["centroid"]["lat"],
                    f"{zone['zone_id']}\n{zone.get('name') or ''}",
                    fontsize=5,
                    ha="center",
                    va="center",
                    color="#0d47a1",
                )

    west, south, east, north = PILOT_WINDOW_LONLAT
    if apply_links:
        fig, ax = plt.subplots(figsize=(11, 9), dpi=130)
        draw(
            ax,
            (west, east),
            (south, north),
            "G4 v1_050 — zones de mer 1400 et aretes sea-sea (rouge = lien declare)",
            label_zones=True,
        )
        path = CAPTURE / "v1_050_sea_zones_window.png"
        fig.tight_layout()
        fig.savefig(path, format="png", metadata={"Software": None})
        plt.close(fig)
        paths["window"] = path

    fig, ax = plt.subplots(figsize=(8, 7), dpi=130)
    draw(
        ax,
        (3.6, 7.6),
        (51.6, 54.2),
        (
            "Zuiderzee / Lauwerszee — liens declares "
            + ("ACTIFS" if apply_links else "COUPES")
        ),
        label_zones=True,
    )
    suffix = "on" if apply_links else "off"
    path = CAPTURE / f"v1_050_zuiderzee_links_{suffix}.png"
    fig.tight_layout()
    fig.savefig(path, format="png", metadata={"Software": None})
    plt.close(fig)
    paths[f"zuiderzee_links_{suffix}"] = path
    return paths


# ---------------------------------------------------------------------------
# Point d'entrée
# ---------------------------------------------------------------------------


def run_adjacency(
    apply_topology_links_flag: bool = True,
    *,
    context: Optional[Dict[str, Any]] = None,
    export: bool = True,
    captures: bool = True,
) -> Dict[str, Any]:
    """Dérive les zones de mer et l'adjacence typée ; exporte les artefacts G4."""
    t_all = time.perf_counter()
    timings: Dict[str, float] = {}
    ctx = context or load_context()

    cells_mod = _load_module("cells_g3", "steps/03_cells.py")

    t = time.perf_counter()
    sea = derive_sea(ctx)
    timings["sea"] = time.perf_counter() - t

    t = time.perf_counter()
    spacing = SpacingField(ctx["cell_geoms"], sea["sea_geom"])
    seeding = build_sea_seeds(sea["components"], sea["sea_geom"], spacing)
    seeds = relax_seeds(sea["components"], seeding["seeds"], cells_mod._voronoi_on_part)
    timings["seed"] = time.perf_counter() - t

    t = time.perf_counter()
    zones = build_zones(sea["components"], seeds, cells_mod._voronoi_on_part, ctx["projector"])
    assign_zone_ids(zones, ctx["cell_ids"])
    anchors = build_name_anchors(ctx["legacy_names"], ctx["legacy_coords"], ctx["projector"])
    name_zones(zones, anchors)
    timings["zones"] = time.perf_counter() - t

    t = time.perf_counter()
    land_land = read_land_land_edges(ctx["adjacency_g3"])
    mass_of = land_components(ctx["cell_ids"], land_land)
    land_sea = derive_land_sea_edges(ctx["cells"], ctx["cell_geoms"], zones)
    sea_sea = derive_sea_sea_edges(zones)
    straits = derive_strait_edges(ctx["cells"], ctx["cell_geoms"], mass_of)
    timings["edges"] = time.perf_counter() - t

    applied = apply_topology_links(ctx, sea, zones, apply_topology_links_flag)
    sea_sea_all = sea_sea + applied["edges"]
    reachability = compute_reachability(zones, sea_sea_all)

    edges = sorted(
        land_land + land_sea + sea_sea_all + straits,
        key=lambda e: (e["kind"], e["a"], e["b"]),
    )

    divergence = build_divergence(ctx, land_land)
    metrics = compute_metrics(
        zones, edges, sea, ctx, seeding, reachability, applied["links"]
    )
    metrics["target_attested_name_found"] = applied["target_name_found"]
    metrics["apply_topology_links"] = bool(apply_topology_links_flag)

    shas: Dict[str, str] = {}
    if export:
        t = time.perf_counter()
        shas = export_g4(
            zones=zones,
            edges=edges,
            metrics=metrics,
            links=applied["links"],
            reachability=reachability,
            divergence=divergence,
            context=ctx,
        )
        timings["export"] = time.perf_counter() - t

    capture_paths: Dict[str, Path] = {}
    if captures:
        t = time.perf_counter()
        capture_paths = write_captures(
            ctx, sea, zones, edges, apply_links=apply_topology_links_flag
        )
        timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t_all
    BUILD.mkdir(parents=True, exist_ok=True)
    write_json(BUILD / "99_timings_g4.json", {k: round(v, 6) for k, v in sorted(timings.items())})

    return {
        "context": ctx,
        "sea": sea,
        "zones": zones,
        "adjacency": edges,
        "land_land": land_land,
        "sea_sea": sea_sea_all,
        "straits": straits,
        "metrics": metrics,
        "links": applied["links"],
        "divergence": divergence,
        "reachability": reachability,
        "projection": ctx["projector"].info,
        "captures": {k: str(v) for k, v in capture_paths.items()},
        "shas": shas,
        "seeding": seeding,
        "timings": timings,
        "apply_topology_links": bool(apply_topology_links_flag),
    }


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="G4 — zones de mer et adjacence typée")
    parser.add_argument("--no-links", action="store_true", help="coupe les liens déclarés")
    args = parser.parse_args(list(argv) if argv is not None else None)
    result = run_adjacency(apply_topology_links_flag=not args.no_links)
    m = result["metrics"]
    print(
        f"g4 | zones={m['sea_zone_count']} | edges={m['adjacency_count']} {m['by_kind']} "
        f"| coastal={m['coastal_cell_count']} "
        f"| reachable={result['reachability']['all_enclosed_reachable']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
