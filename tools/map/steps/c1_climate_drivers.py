"""C1 — déterminants physiques du climat : insolation astronomique et continentalité (v1_080).

Ce module lit les cellules, zones de mer et adjacence déjà committées, calcule
l'insolation extraterrestre annuelle et les durées de jour aux solstices,
mesure les distances à la mer et les sauts au littoral, puis exporte les
artefacts C1. Il ne produit ni température, ni précipitations, ni classification.

Entrées (lecture seule) : cells_g3.json, sea_zones_g4.json, adjacency_g5.json,
stats_g4.json (coastal_cell_count croisé seulement).

Sorties : cells_climate_drivers_c1.json, stats_c1.json, MANIFEST_c1.json,
climate_drivers_registry.json, journaux v1_080_*, deux captures PNG.

Usage :
  ../../.venv/bin/python pipeline.py --source climate_drivers
  ../../.venv/bin/python tests/run_proof_c1.py
"""

from __future__ import annotations

import argparse
import math
import sys
import time
from collections import defaultdict, deque
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

from shapely.geometry import Point, shape

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    C1_DAYLIGHT_DECIMALS,
    C1_DAYS_IN_YEAR,
    C1_DISTANCE_DECIMALS,
    C1_ECCENTRICITY_FACTOR,
    C1_INSOLATION_DECIMALS,
    C1_MJ_PER_J,
    C1_OBLIQUITY_DEG,
    C1_PIPELINE_VERSION,
    C1_REGISTRY_CREATED,
    C1_SEA_DISTANCE_EPS_M,
    C1_SOLAR_CONSTANT_W_M2,
    C1_SUMMER_SOLSTICE_DAY,
    C1_WINTER_SOLSTICE_DAY,
    PILOT_WINDOW_LONLAT,
    TARGET_CRS,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402

ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
BUILD = ROOT / "build"


def _median(vals: Sequence[float]) -> float:
    if not vals:
        return 0.0
    s = sorted(vals)
    n = len(s)
    mid = n // 2
    if n % 2:
        return float(s[mid])
    return float(s[mid - 1] + s[mid]) / 2.0


def _omega_s_and_clamp(phi: float, n: int) -> Tuple[float, bool]:
    """Angle horaire du coucher (rad) et indicateur d'écrêtage polaire."""
    obliquity_rad = math.radians(C1_OBLIQUITY_DEG)
    delta = obliquity_rad * math.sin(2 * math.pi * (284 + n) / C1_DAYS_IN_YEAR)
    u = -math.tan(phi) * math.tan(delta)
    if u >= 1.0:
        return 0.0, True
    if u <= -1.0:
        return math.pi, True
    return math.acos(u), False


def _daylight_hours(phi: float, n: int) -> float:
    omega_s, _ = _omega_s_and_clamp(phi, n)
    return round((2.0 / 15.0) * math.degrees(omega_s), C1_DAYLIGHT_DECIMALS)


def _annual_insolation_mj(phi: float) -> Tuple[float, int]:
    """Insolation annuelle extraterrestre (MJ/m²/an) et jours écrêtés."""
    total_j = 0.0
    clamps = 0
    obliquity_rad = math.radians(C1_OBLIQUITY_DEG)
    for n in range(1, C1_DAYS_IN_YEAR + 1):
        delta = obliquity_rad * math.sin(2 * math.pi * (284 + n) / C1_DAYS_IN_YEAR)
        e0 = 1.0 + C1_ECCENTRICITY_FACTOR * math.cos(2 * math.pi * n / C1_DAYS_IN_YEAR)
        omega_s, clamped = _omega_s_and_clamp(phi, n)
        if clamped:
            clamps += 1
        h = (
            (86400.0 / math.pi)
            * C1_SOLAR_CONSTANT_W_M2
            * e0
            * (
                math.cos(phi) * math.cos(delta) * math.sin(omega_s)
                + omega_s * math.sin(phi) * math.sin(delta)
            )
        )
        total_j += h
    mj = round(total_j / C1_MJ_PER_J, C1_INSOLATION_DECIMALS)
    return mj, clamps


def _derive_coastal_ids(adjacency: Sequence[dict]) -> set[int]:
    coastal: set[int] = set()
    for edge in adjacency:
        if edge.get("kind") != "land-sea":
            continue
        coastal.add(int(edge["a"]))
        coastal.add(int(edge["b"]))
    return coastal


def _build_land_graph(
    adjacency: Sequence[dict], kinds: Sequence[str]
) -> Dict[int, set[int]]:
    graph: Dict[int, set[int]] = defaultdict(set)
    for edge in adjacency:
        if edge.get("kind") not in kinds:
            continue
        a, b = int(edge["a"]), int(edge["b"])
        graph[a].add(b)
        graph[b].add(a)
    return graph


def _hops_from_coastal(
    coastal_ids: set[int], graph: Dict[int, set[int]], all_ids: set[int]
) -> Dict[int, int]:
    hops: Dict[int, int] = {}
    q: deque[int] = deque()
    for cid in coastal_ids:
        hops[cid] = 0
        q.append(cid)
    while q:
        cur = q.popleft()
        for nb in graph.get(cur, ()):
            if nb in hops:
                continue
            hops[nb] = hops[cur] + 1
            q.append(nb)
    for cid in all_ids:
        if cid not in hops:
            hops[cid] = -1
    return hops


def load_context() -> Dict[str, Any]:
    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    sea_doc = read_json(ARTIFACTS / "sea_zones_g4.json")
    adj_doc = read_json(ARTIFACTS / "adjacency_g5.json")
    stats_g4 = read_json(ARTIFACTS / "stats_g4.json")
    cells = cells_doc["cells"]
    sea_zones = sea_doc["sea_zones"]
    adjacency = adj_doc["adjacency"]
    cell_ids = {int(c["cell_id"]) for c in cells}
    coastal_ids = _derive_coastal_ids(adjacency)
    coastal_land = {cid for cid in coastal_ids if cid in cell_ids}
    sea_geoms = {
        int(z["zone_id"]): shape(z["geometry"]) for z in sea_zones
    }
    return {
        "cells": cells,
        "sea_zones": sea_zones,
        "adjacency": adjacency,
        "stats_g4": stats_g4,
        "cell_ids": cell_ids,
        "coastal_ids": coastal_land,
        "sea_geoms": sea_geoms,
        "input_shas": {
            "cells_g3.json": sha256_file(ARTIFACTS / "cells_g3.json"),
            "sea_zones_g4.json": sha256_file(ARTIFACTS / "sea_zones_g4.json"),
            "adjacency_g5.json": sha256_file(ARTIFACTS / "adjacency_g5.json"),
        },
    }


def derive_climate_drivers(context: Dict[str, Any]) -> Dict[str, Any]:
    cells = context["cells"]
    sea_geoms = context["sea_geoms"]
    adjacency = context["adjacency"]
    coastal_ids = context["coastal_ids"]
    cell_ids = context["cell_ids"]

    graph_ll = _build_land_graph(adjacency, ("land-land",))
    graph_full = _build_land_graph(adjacency, ("land-land", "strait"))
    hops_map = _hops_from_coastal(coastal_ids, graph_full, cell_ids)
    reachable_ll = _hops_from_coastal(coastal_ids, graph_ll, cell_ids)
    strait_only = sum(
        1 for cid in cell_ids if reachable_ll.get(cid, -1) < 0 and hops_map.get(cid, -1) >= 0
    )

    zone_items = sorted(sea_geoms.items(), key=lambda x: x[0])
    outputs: List[dict] = []
    ecretages_total = 0
    centroid_outside = 0
    contact_point = 0

    for cell in sorted(cells, key=lambda c: int(c["cell_id"])):
        cid = int(cell["cell_id"])
        lat = float(cell["centroid"]["lat"])
        phi = math.radians(lat)
        insol, clamps = _annual_insolation_mj(phi)
        ecretages_total += clamps
        summer_h = _daylight_hours(phi, C1_SUMMER_SOLSTICE_DAY)
        winter_h = _daylight_hours(phi, C1_WINTER_SOLSTICE_DAY)

        cell_geom = shape(cell["geometry"])
        cx = float(cell["centroid"]["x_m"])
        cy = float(cell["centroid"]["y_m"])
        centroid_pt = Point(cx, cy)
        inside = cell_geom.contains(centroid_pt) or cell_geom.covers(centroid_pt)
        if not inside:
            centroid_outside += 1

        best_edge = float("inf")
        best_cent = float("inf")
        best_zone = -1
        for zone_id, sea_geom in zone_items:
            d_edge = cell_geom.distance(sea_geom)
            d_cent = centroid_pt.distance(sea_geom)
            if d_cent < best_cent or (
                abs(d_cent - best_cent) <= 1e-9 and zone_id < best_zone
            ):
                best_cent = d_cent
                best_zone = zone_id
            if d_edge < best_edge:
                best_edge = d_edge

        coastal = cid in coastal_ids
        if not coastal and best_edge <= C1_SEA_DISTANCE_EPS_M:
            contact_point += 1

        outputs.append(
            {
                "cell_id": cid,
                "insolation_annual_mj_m2": insol,
                "daylight_h_summer_solstice": summer_h,
                "daylight_h_winter_solstice": winter_h,
                "polar_clamp_days": clamps,
                "dist_sea_edge_m": round(best_edge, C1_DISTANCE_DECIMALS),
                "dist_sea_centroid_m": round(best_cent, C1_DISTANCE_DECIMALS),
                "nearest_sea_zone_id": best_zone,
                "hops_to_sea": int(hops_map.get(cid, -1)),
                "coastal": coastal,
                "centroid_inside_cell": inside,
            }
        )

    by_hop: Dict[int, List[float]] = defaultdict(list)
    for row in outputs:
        h = int(row["hops_to_sea"])
        if h >= 0:
            by_hop[h].append(float(row["dist_sea_centroid_m"]))

    hop_keys = sorted(by_hop.keys())
    mediane_par_saut = [round(_median(by_hop[h]), C1_DISTANCE_DECIMALS) for h in hop_keys]
    cellules_par_saut = {str(h): len(by_hop[h]) for h in hop_keys}

    insols = [float(r["insolation_annual_mj_m2"]) for r in outputs]
    amps = [
        float(r["daylight_h_summer_solstice"]) - float(r["daylight_h_winter_solstice"])
        for r in outputs
    ]
    dists = [float(r["dist_sea_centroid_m"]) for r in outputs]

    metrics = {
        "pipeline_version": C1_PIPELINE_VERSION,
        "cell_count": len(outputs),
        "insolation_mj_m2": {
            "min": min(insols),
            "median": round(_median(insols), C1_INSOLATION_DECIMALS),
            "max": max(insols),
        },
        "daylight_amplitude_h": {
            "min": round(min(amps), C1_DAYLIGHT_DECIMALS),
            "median": round(_median(amps), C1_DAYLIGHT_DECIMALS),
            "max": round(max(amps), C1_DAYLIGHT_DECIMALS),
        },
        "ecretages_polaires_total": ecretages_total,
        "coastal_cell_count_derive": len(coastal_ids),
        "coastal_cell_count_g4": int(context["stats_g4"].get("coastal_cell_count", -1)),
        "dist_sea_centroid_m": {
            "min": round(min(dists), C1_DISTANCE_DECIMALS),
            "median": round(_median(dists), C1_DISTANCE_DECIMALS),
            "max": round(max(dists), C1_DISTANCE_DECIMALS),
        },
        "mediane_dist_sea_centroid_par_saut": mediane_par_saut,
        "cellules_par_saut": cellules_par_saut,
        "cellules_atteintes_par_strait_seulement": strait_only,
        "cellules_centroide_hors_polygone": centroid_outside,
        "contact_ponctuel_sans_arete_land_sea": contact_point,
    }
    return {
        "cells_out": outputs,
        "metrics": metrics,
        "coastal_ids": coastal_ids,
    }


def export_c1(
    context: Dict[str, Any],
    derived: Dict[str, Any],
) -> Dict[str, str]:
    shas: Dict[str, str] = {}
    cells_out = derived["cells_out"]
    metrics = derived["metrics"]

    cells_doc = {
        "pipeline_version": C1_PIPELINE_VERSION,
        "crs": TARGET_CRS,
        "cells": cells_out,
    }
    shas["artifacts/cells_climate_drivers_c1.json"] = write_json(
        ARTIFACTS / "cells_climate_drivers_c1.json", cells_doc
    )
    shas["artifacts/stats_c1.json"] = write_json(ARTIFACTS / "stats_c1.json", metrics)

    registry_doc = {
        "created": C1_REGISTRY_CREATED,
        "data_class": "climate_drivers_c1_registry",
        "pipeline_version": C1_PIPELINE_VERSION,
        "cell_count": len(cells_out),
        "cells": [{"cell_id": int(c["cell_id"])} for c in cells_out],
    }
    shas["registry/climate_drivers_registry.json"] = write_json(
        REGISTRY / "climate_drivers_registry.json", registry_doc
    )

    manifest = {
        "pipeline_version": C1_PIPELINE_VERSION,
        "crs": TARGET_CRS,
        "inputs": context["input_shas"],
        "outputs": {
            "cells_climate_drivers_c1.json": shas["artifacts/cells_climate_drivers_c1.json"],
            "stats_c1.json": shas["artifacts/stats_c1.json"],
            "climate_drivers_registry.json": shas["registry/climate_drivers_registry.json"],
        },
    }
    shas["artifacts/MANIFEST_c1.json"] = write_json(
        ARTIFACTS / "MANIFEST_c1.json", manifest
    )
    return shas


def write_captures(
    context: Dict[str, Any],
    derived: Dict[str, Any],
) -> Dict[str, Path]:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.patches import Polygon as MplPolygon
    from matplotlib.colors import Normalize
    from pyproj import Transformer

    CAPTURE.mkdir(parents=True, exist_ok=True)
    inv = Transformer.from_crs(TARGET_CRS, "EPSG:4326", always_xy=True)
    cells = context["cells"]
    by_id = {int(c["cell_id"]): c for c in cells}
    outputs = derived["cells_out"]
    w, s, e, n = PILOT_WINDOW_LONLAT

    def to_ll_ring(geom_xy: Any) -> List[Tuple[float, float]]:
        g = shape(geom_xy)
        if g.geom_type == "Polygon":
            poly = g
        else:
            poly = g.geoms[0]
        return [inv.transform(x, y) for x, y in poly.exterior.coords]

    def draw_field(ax, field: str, title: str, cmap: str) -> None:
        ax.set_aspect("equal")
        ax.set_facecolor("#e3f2fd")
        ax.set_title(title, fontsize=10)
        ax.set_xlim(w, e)
        ax.set_ylim(s, n)
        ax.grid(True, alpha=0.2)
        vals = [float(r[field]) for r in outputs]
        norm = Normalize(vmin=min(vals), vmax=max(vals))
        cmap_obj = plt.get_cmap(cmap)
        patches = []
        colors = []
        for row in sorted(outputs, key=lambda r: int(r["cell_id"])):
            cell = by_id[int(row["cell_id"])]
            ring = to_ll_ring(cell["geometry"])
            patches.append(MplPolygon(ring, closed=True))
            colors.append(cmap_obj(norm(float(row[field]))))
        if patches:
            ax.add_collection(
                PatchCollection(patches, facecolor=colors, edgecolor="#333", linewidth=0.15)
            )

    paths: Dict[str, Path] = {}
    fig, ax = plt.subplots(figsize=(12, 10), dpi=120)
    draw_field(ax, "insolation_annual_mj_m2", "C1 — insolation extraterrestre annuelle (MJ/m²/an)", "viridis")
    p1 = CAPTURE / "v1_080_insolation_window.png"
    fig.savefig(p1, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["insolation"] = p1

    fig, ax = plt.subplots(figsize=(12, 10), dpi=120)
    draw_field(
        ax,
        "dist_sea_centroid_m",
        "C1 — distance centroïde → mer (m)",
        "YlOrRd",
    )
    p2 = CAPTURE / "v1_080_continentality_window.png"
    fig.savefig(p2, format="png", metadata={"Software": None})
    plt.close(fig)
    paths["continentality"] = p2
    return paths


def run_climate_drivers(
    *,
    context: Optional[Dict[str, Any]] = None,
    export: bool = True,
    captures: bool = True,
) -> Dict[str, Any]:
    """Dérive insolation et continentalité ; exporte les artefacts C1."""
    t_all = time.perf_counter()
    timings: Dict[str, float] = {}
    ctx = context or load_context()

    t = time.perf_counter()
    derived = derive_climate_drivers(ctx)
    timings["derive"] = time.perf_counter() - t

    shas: Dict[str, str] = {}
    if export:
        t = time.perf_counter()
        shas = export_c1(ctx, derived)
        timings["export"] = time.perf_counter() - t

    capture_paths: Dict[str, Path] = {}
    if captures:
        t = time.perf_counter()
        capture_paths = write_captures(ctx, derived)
        timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t_all
    BUILD.mkdir(parents=True, exist_ok=True)
    write_json(BUILD / "99_timings_c1.json", {k: round(v, 6) for k, v in sorted(timings.items())})

    return {
        "context": ctx,
        "derived": derived,
        "metrics": derived["metrics"],
        "cells_out": derived["cells_out"],
        "shas": shas,
        "captures": capture_paths,
        "projection": {"epsg": TARGET_CRS},
        "timings": timings,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="C1 — déterminants physiques du climat")
    parser.parse_args()
    result = run_climate_drivers()
    m = result["metrics"]
    print(
        f"C1 | cells={m['cell_count']} | "
        f"insol_med={m['insolation_mj_m2']['median']} | "
        f"dist_sea_med={m['dist_sea_centroid_m']['median']} | "
        f"ecretages={m['ecretages_polaires_total']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
