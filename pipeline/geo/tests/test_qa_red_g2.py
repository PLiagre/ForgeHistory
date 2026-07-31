"""Cas cassés volontaires G2 — prouvent que chaque contrôle peut devenir ROUGE."""

from __future__ import annotations

from typing import Any, Dict, Tuple

from shapely.geometry import Polygon, box

from constants import G2_LAND_AREA_KM2_MAX, G2_LAND_AREA_KM2_MIN
from qa.checks import (
    g2a_land_within_window,
    g2b_lakes_are_holes,
    g2c_area_plausible,
    q1_land_validity,
    q10_determinism,
)


def _bowtie() -> Polygon:
    return Polygon([(0, 0), (10, 10), (0, 10), (10, 0), (0, 0)])


def red_q1(land_xy: Any) -> Tuple[str, bool]:
    result = q1_land_validity(_bowtie())
    return "land_bowtie_self_intersection", (not result.passed)


def red_g2a(land_ll: Any, window: Any) -> Tuple[str, bool]:
    # Débordement volontaire hors fenêtre.
    bloated = land_ll.union(box(-20, 20, -15, 25))
    result = g2a_land_within_window(bloated, window)
    return "land_extended_west_of_window", (not result.passed)


def red_g2b(land_ll: Any, lakes_ll: Any) -> Tuple[str, bool]:
    # Remplir les trous : solid sans soustraction de lacs.
    if land_ll.geom_type == "Polygon":
        solid = Polygon(land_ll.exterior)
    else:
        from shapely.ops import unary_union

        solid = unary_union([Polygon(p.exterior) for p in land_ll.geoms])
    # Si pas de lacs, fabriquer un faux lac qui intersecte la terre.
    if lakes_ll is None or lakes_ll.is_empty:
        c = solid.representative_point()
        fake_lake = c.buffer(0.05)
        result = g2b_lakes_are_holes(solid, fake_lake)
        return "solid_land_intersects_fake_lake", (not result.passed)
    result = g2b_lakes_are_holes(solid, lakes_ll)
    return "solid_land_without_lake_holes", (not result.passed)


def red_g2c() -> Tuple[str, bool]:
    # Surface absurde (facteur 10 hors plage).
    bogus = G2_LAND_AREA_KM2_MAX * 50.0
    result = g2c_area_plausible(bogus, G2_LAND_AREA_KM2_MIN, G2_LAND_AREA_KM2_MAX)
    return "area_km2_fifty_times_max", (not result.passed)


def red_q10() -> Tuple[str, bool]:
    sha_pairs = {"artifacts/coastline.json": ["aaa", "bbb"]}
    result = q10_determinism(sha_pairs)
    return "forced_sha_mismatch_coastline_json", (not result.passed)


def run_all_red_g2(
    land_ll: Any,
    land_xy: Any,
    lakes_ll: Any,
    window: Any,
) -> Dict[str, Dict[str, Any]]:
    proofs = {}
    for qid, fn in [
        ("Q1", lambda: red_q1(land_xy)),
        ("G2-A", lambda: red_g2a(land_ll, window)),
        ("G2-B", lambda: red_g2b(land_ll, lakes_ll)),
        ("G2-C", lambda: red_g2c()),
        ("Q10", lambda: red_q10()),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
