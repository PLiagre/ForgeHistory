"""Cas cassés volontaires G3 — chaque contrôle doit pouvoir devenir ROUGE."""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from shapely.geometry import Polygon, mapping

from constants import (
    G3_AREA_CEIL_KM2,
    G3_AREA_EPS_M2,
    G3_AREA_FLOOR_KM2,
    G3_AREA_MAX_MEDIAN_RATIO,
    G3_COMPACTNESS_MIN,
    G3_OVERLAP_EPS_M2,
    G3_SEED_COUNT_MAX,
    G3_SEED_COUNT_MIN,
)
from qa.checks import (
    g3a_no_sea_in_cells,
    g3b_all_land_masses_covered,
    g3c_stable_ids,
    g3d_cell_count_in_range,
    g3e_area_within_bounds,
    g3f_area_dispersion,
    g3g_compactness_floor,
    g3h_no_retired_reissued,
    q1_polygon_validity,
    q10_determinism,
    q2_no_holes_eps,
    q3_no_overlaps_eps,
    q4_no_isolated,
)


def _bowtie() -> dict:
    return mapping(Polygon([(0, 0), (10, 10), (0, 10), (10, 0), (0, 0)]))


def red_q1(healthy_cells: List[dict], land_geom: Any) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    cells[0]["geometry"] = _bowtie()
    result = q1_polygon_validity(cells, land_geom)
    return "cells[0]_bowtie_self_intersection", (not result.passed)


def red_q2(healthy_cells: List[dict], land_geom: Any) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    if len(cells) < 2:
        return "need_at_least_2_cells", False
    cells = cells[1:]
    result = q2_no_holes_eps(cells, land_geom, G3_AREA_EPS_M2)
    return "drop_first_cell_creates_hole", (not result.passed)


def red_q3(healthy_cells: List[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    if len(cells) < 2:
        return "need_at_least_2_cells", False
    cells[1]["geometry"] = copy.deepcopy(cells[0]["geometry"])
    result = q3_no_overlaps_eps(cells, G3_OVERLAP_EPS_M2)
    return "duplicate_geometry_overlap", (not result.passed)


def red_q4(healthy_cells: List[dict]) -> Tuple[str, bool]:
    result = q4_no_isolated(healthy_cells, [])
    return "empty_adjacency_all_isolated", (not result.passed)


def red_q10() -> Tuple[str, bool]:
    sha_pairs = {"artifacts/cells_g3.json": ["aaa", "bbb"]}
    result = q10_determinism(sha_pairs)
    return "forced_sha_mismatch_cells_g3", (not result.passed)


def red_g3a(healthy_cells: List[dict], land_geom: Any) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    sea_box = Polygon(
        [
            (land_geom.bounds[0] - 500_000, land_geom.bounds[1] - 500_000),
            (land_geom.bounds[0] - 400_000, land_geom.bounds[1] - 500_000),
            (land_geom.bounds[0] - 400_000, land_geom.bounds[1] - 400_000),
            (land_geom.bounds[0] - 500_000, land_geom.bounds[1] - 400_000),
            (land_geom.bounds[0] - 500_000, land_geom.bounds[1] - 500_000),
        ]
    )
    cells[0]["geometry"] = mapping(sea_box)
    result = g3a_no_sea_in_cells(cells, land_geom, G3_AREA_EPS_M2)
    return "cell_geometry_placed_in_sea", (not result.passed)


def red_g3b(healthy_cells: List[dict], land_parts: Sequence[Any]) -> Tuple[str, bool]:
    result = g3b_all_land_masses_covered([], land_parts, G3_AREA_EPS_M2)
    return "empty_cells_no_mass_covered", (not result.passed)


def red_g3c(registry: Sequence[dict]) -> Tuple[str, bool]:
    reg_a = copy.deepcopy(list(registry))
    reg_b = copy.deepcopy(list(registry))
    active = [r for r in reg_b if r.get("retired") is None]
    if not active:
        return "empty_registry", False
    active[0]["cell_id"] = int(active[0]["cell_id"]) + 9999
    result = g3c_stable_ids(reg_a, reg_b)
    return "domain_key_rebinding_different_id", (not result.passed)


def red_g3d() -> Tuple[str, bool]:
    result = g3d_cell_count_in_range(50, G3_SEED_COUNT_MIN, G3_SEED_COUNT_MAX)
    return "cell_count_fifty_below_min", (not result.passed)


def red_g3e(healthy_cells: List[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    # Forcer une cellule géante hors plafond (défaut v1_048 : 86 647 km²).
    cells[0]["area_km2"] = G3_AREA_CEIL_KM2 * 10.0
    result = g3e_area_within_bounds(
        cells,
        floor_km2=G3_AREA_FLOOR_KM2,
        ceil_km2=G3_AREA_CEIL_KM2,
        singleton_ids=[],
    )
    return "cell_area_above_ceil_like_v1_048_giant", (not result.passed)


def red_g3f(healthy_cells: List[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    if not cells:
        return "empty_cells", False
    # Dispersion type v1_048 : max/médiane ≈ 728.
    med = sorted(float(c["area_km2"]) for c in cells)[len(cells) // 2]
    cells[0]["area_km2"] = med * 728.0
    result = g3f_area_dispersion(cells, max_median_ratio=G3_AREA_MAX_MEDIAN_RATIO)
    return "max_median_ratio_728_like_v1_048", (not result.passed)


def red_g3g(healthy_cells: List[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(healthy_cells)
    # Lanière / écharde : compacité proche de zéro (v1_048 min 0.105).
    cells[0]["compactness_polsby_popper"] = 0.05
    result = g3g_compactness_floor(
        cells, floor=G3_COMPACTNESS_MIN, singleton_ids=[]
    )
    return "shard_compactness_below_floor", (not result.passed)


def red_g3h(registry: Sequence[dict]) -> Tuple[str, bool]:
    reg = copy.deepcopy(list(registry))
    retired = [r for r in reg if r.get("retired") is not None]
    if not retired:
        # Injecter un retrait fictif puis le réémettre.
        if not reg:
            return "empty_registry", False
        reg[0]["retired"] = "2026-07-26"
        reg.append(
            {
                "cell_id": int(reg[0]["cell_id"]),
                "domain_key": "fake:reissue",
                "retired": None,
                "supersedes": [],
                "created": "2026-07-26",
                "seed_lon": 0.0,
                "seed_lat": 0.0,
                "label": "reissued",
            }
        )
    else:
        rid = int(retired[0]["cell_id"])
        reg.append(
            {
                "cell_id": rid,
                "domain_key": "fake:reissue",
                "retired": None,
                "supersedes": [],
                "created": "2026-07-26",
                "seed_lon": 0.0,
                "seed_lat": 0.0,
                "label": "reissued",
            }
        )
    result = g3h_no_retired_reissued(reg)
    return "retired_id_reissued_as_active", (not result.passed)


def run_all_red_g3(
    healthy_cells: List[dict],
    land_geom: Any,
    land_parts: Sequence[Any],
    registry: Sequence[dict],
) -> Dict[str, Dict[str, Any]]:
    proofs = {}
    for qid, fn in [
        ("Q1", lambda: red_q1(healthy_cells, land_geom)),
        ("Q2", lambda: red_q2(healthy_cells, land_geom)),
        ("Q3", lambda: red_q3(healthy_cells)),
        ("Q4", lambda: red_q4(healthy_cells)),
        ("Q10", lambda: red_q10()),
        ("G3-A", lambda: red_g3a(healthy_cells, land_geom)),
        ("G3-B", lambda: red_g3b(healthy_cells, land_parts)),
        ("G3-C", lambda: red_g3c(registry)),
        ("G3-D", lambda: red_g3d()),
        ("G3-E", lambda: red_g3e(healthy_cells)),
        ("G3-F", lambda: red_g3f(healthy_cells)),
        ("G3-G", lambda: red_g3g(healthy_cells)),
        ("G3-H", lambda: red_g3h(registry)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
