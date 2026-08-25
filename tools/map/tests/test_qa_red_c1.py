"""Cas rouges C1 — un par contrôle (Q10 + C1-A..F)."""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from constants import C1_SEA_DISTANCE_EPS_M
from qa.checks import q10_determinism
from qa.checks_c1 import (
    c1a_mesh_unchanged,
    c1b_insolation_latitude_monotone,
    c1c_daylight_amplitude,
    c1d_coastal_distance_consistent,
    c1e_continentality_consistent,
    c1f_no_gameplay_keys,
)


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1] or "x"]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_sur_{key}", (not result.passed)


def red_c1a(base_ids: Sequence[int], climate_cells: Sequence[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(list(climate_cells))
    if not cells:
        return "aucune cellule", False
    cells.pop()
    result = c1a_mesh_unchanged(base_ids, cells)
    return "cell_id_retire", (not result.passed)


def red_c1b(
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
) -> Tuple[str, bool]:
    g3 = list(cells_g3)
    climate = copy.deepcopy(list(climate_cells))
    if len(g3) < 2:
        return "moins de 2 cellules", False
    by_lat = sorted(g3, key=lambda c: float(c["centroid"]["lat"]))
    south_id = int(by_lat[0]["cell_id"])
    north_id = int(by_lat[-1]["cell_id"])
    south_row = next(c for c in climate if int(c["cell_id"]) == south_id)
    north_row = next(c for c in climate if int(c["cell_id"]) == north_id)
    south_row["insolation_annual_mj_m2"], north_row["insolation_annual_mj_m2"] = (
        north_row["insolation_annual_mj_m2"],
        south_row["insolation_annual_mj_m2"],
    )
    result = c1b_insolation_latitude_monotone(g3, climate)
    return f"insolation_echangee_{south_id}_{north_id}", (not result.passed)


def red_c1c(
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
) -> Tuple[str, bool]:
    climate = copy.deepcopy(list(climate_cells))
    if not climate:
        return "aucune cellule", False
    row = climate[0]
    row["daylight_h_winter_solstice"] = float(row["daylight_h_summer_solstice"]) + 1.0
    result = c1c_daylight_amplitude(cells_g3, climate)
    return f"winter_au_dessus_summer_cell_{row['cell_id']}", (not result.passed)


def red_c1d(
    coastal_ids: set[int],
    climate_cells: Sequence[dict],
) -> Tuple[str, bool]:
    climate = copy.deepcopy(list(climate_cells))
    coastal_list = [c for c in climate if int(c["cell_id"]) in coastal_ids]
    if not coastal_list:
        return "aucune cellule littorale", False
    row = coastal_list[0]
    row["dist_sea_edge_m"] = float(C1_SEA_DISTANCE_EPS_M) + 100.0
    result = c1d_coastal_distance_consistent(coastal_ids, climate)
    return f"dist_edge_forcee_cell_{row['cell_id']}", (not result.passed)


def red_c1e(climate_cells: Sequence[dict]) -> Tuple[str, bool]:
    climate = copy.deepcopy(list(climate_cells))
    by_hop: Dict[int, List[dict]] = {}
    for c in climate:
        h = int(c["hops_to_sea"])
        if h >= 0:
            by_hop.setdefault(h, []).append(c)
    if len(by_hop) < 2:
        return "moins de 2 classes de sauts", False
    hops = sorted(by_hop.keys())
    inner = max(by_hop[hops[1]], key=lambda r: float(r["dist_sea_centroid_m"]))
    inner["dist_sea_centroid_m"] = 0.0
    result = c1e_continentality_consistent(climate)
    return f"mediane_saut_{hops[1]}_ecrasee", (not result.passed)


def red_c1f(artifact_docs: Sequence[dict]) -> Tuple[str, bool]:
    docs = copy.deepcopy(list(artifact_docs))
    if not docs:
        return "aucun artefact", False
    if isinstance(docs[0], dict):
        docs[0]["multiplier"] = 2.0
    result = c1f_no_gameplay_keys(docs)
    return "cle_multiplier_injectee", (not result.passed)


def run_all_red_c1(
    *,
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
    coastal_ids: set[int],
    artifact_docs: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> Dict[str, Dict[str, Any]]:
    base_ids = [int(c["cell_id"]) for c in cells_g3]
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q10", lambda: red_q10(sha_pairs)),
        ("C1-A", lambda: red_c1a(base_ids, climate_cells)),
        ("C1-B", lambda: red_c1b(cells_g3, climate_cells)),
        ("C1-C", lambda: red_c1c(cells_g3, climate_cells)),
        ("C1-D", lambda: red_c1d(coastal_ids, climate_cells)),
        ("C1-E", lambda: red_c1e(climate_cells)),
        ("C1-F", lambda: red_c1f(artifact_docs)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
