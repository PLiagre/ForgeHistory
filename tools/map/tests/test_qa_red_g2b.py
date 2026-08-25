"""Cas cassés volontaires G2-bis — chaque contrôle G2b / repris doit pouvoir rougir."""

from __future__ import annotations

from typing import Any, Dict, Sequence, Set, Tuple

from shapely.geometry import Polygon, box

from qa.checks import (
    g2a_land_within_window,
    g2b_a_corrections_have_certainty_and_source,
    g2b_b_reversibility,
    g2b_c_no_invented_vertices,
    g2b_d_idempotence,
    g2c_area_plausible,
    q1_land_validity,
    q10_determinism,
)
from constants import G2_LAND_AREA_KM2_MAX, G2_LAND_AREA_KM2_MIN


def _bowtie() -> Polygon:
    return Polygon([(0, 0), (10, 10), (0, 10), (10, 0), (0, 0)])


def red_q1(land_xy: Any) -> Tuple[str, bool]:
    result = q1_land_validity(_bowtie())
    return "land_bowtie_self_intersection", (not result.passed)


def red_g2a(land_ll: Any, window: Any) -> Tuple[str, bool]:
    bloated = land_ll.union(box(-20, 20, -15, 25))
    result = g2a_land_within_window(bloated, window)
    return "land_extended_west_of_window", (not result.passed)


def red_g2c() -> Tuple[str, bool]:
    bogus = G2_LAND_AREA_KM2_MAX * 50.0
    result = g2c_area_plausible(bogus, G2_LAND_AREA_KM2_MIN, G2_LAND_AREA_KM2_MAX)
    return "area_km2_fifty_times_max", (not result.passed)


def red_q10() -> Tuple[str, bool]:
    sha_pairs = {"artifacts/coastline_1400.json": ["aaa", "bbb"]}
    result = q10_determinism(sha_pairs)
    return "forced_sha_mismatch_coastline_1400", (not result.passed)


def red_g2b_a() -> Tuple[str, bool]:
    broken = [
        {
            "id": "corr_broken_no_source",
            "certainty": "attested",
            "source": "",
            "operation": "reclassify",
            "target": {"layer": "ne_10m_lakes", "name": "IJsselmeer"},
        }
    ]
    result = g2b_a_corrections_have_certainty_and_source(broken)
    return "correction_missing_source", (not result.passed)


def red_g2b_b() -> Tuple[str, bool]:
    disabled = {"artifacts/coastline.json": "aaa"}
    reference = {"artifacts/coastline.json": "bbb"}
    result = g2b_b_reversibility(disabled, reference)
    return "forced_sha_mismatch_vs_g2_reference", (not result.passed)


def red_g2b_c(source_vertices: Set[Tuple[float, float]]) -> Tuple[str, bool]:
    # Polygone dont les sommets n'existent pas dans la source.
    invented = Polygon([(99.1, 99.2), (99.3, 99.2), (99.3, 99.4), (99.1, 99.4), (99.1, 99.2)])
    result = g2b_c_no_invented_vertices(invented, source_vertices)
    return "polygon_with_invented_vertices", (not result.passed)


def red_g2b_d(geom_once: Any) -> Tuple[str, bool]:
    # Mutation volontaire : union avec un carré distant.
    mutated = geom_once.union(box(-20, 20, -19, 21))
    result = g2b_d_idempotence(geom_once, mutated)
    return "second_pass_mutated_land", (not result.passed)


def run_all_red_g2b(
    land_ll: Any,
    land_xy: Any,
    window: Any,
    source_vertices: Set[Tuple[float, float]],
) -> Dict[str, Dict[str, Any]]:
    proofs = {}
    for qid, fn in [
        ("Q1", lambda: red_q1(land_xy)),
        ("G2-A", lambda: red_g2a(land_ll, window)),
        ("G2-C", lambda: red_g2c()),
        ("Q10", lambda: red_q10()),
        ("G2b-A", lambda: red_g2b_a()),
        ("G2b-B", lambda: red_g2b_b()),
        ("G2b-C", lambda: red_g2b_c(source_vertices)),
        ("G2b-D", lambda: red_g2b_d(land_ll)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
