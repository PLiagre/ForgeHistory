"""Cas cassés volontaires G6 — chaque contrôle doit pouvoir devenir ROUGE.

Un contrôle qui ne peut pas rougir ne prouve rien. Six mutations locales sur
des copies en mémoire ; aucune ne modifie `qa/checks.py`.

Ce module expose `run_all_red_g6(...)`, importé par `tests/run_proof_g6.py`.
Il n'est pas collecté par pytest (comme ses homologues G2/G3/G4/G5).
"""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from qa.checks import (
    g6a_dem_fingerprint_verified,
    g6b_all_cells_sampled,
    g6c_elevations_plausible,
    g6d_barrier_above_both_cells,
    g6e_mesh_unchanged,
    q10_determinism,
)


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1]]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_forcees_sur_{key}", (not result.passed)


def red_g6a() -> Tuple[str, bool]:
    result = g6a_dem_fingerprint_verified(False, "empreinte_collective_falsifiee")
    return "dem_ok=false avant lecture", (not result.passed)


def red_g6b(cell_relief: Sequence[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(list(cell_relief))
    if not cells:
        return "aucune cellule", False
    cells[0]["sample_count"] = 0
    cells[0]["elev_mean_m"] = None
    result = g6b_all_cells_sampled(cells)
    return f"cellule_{cells[0]['cell_id']}_sample_count_zero", (not result.passed)


def red_g6c(cell_relief: Sequence[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(list(cell_relief))
    if not cells:
        return "aucune cellule", False
    cells[0]["elev_mean_m"] = 9999.0
    result = g6c_elevations_plausible(cells)
    return f"cellule_{cells[0]['cell_id']}_altitude_hors_plage", (not result.passed)


def red_g6d(
    adjacency: Sequence[dict], cell_relief: Sequence[dict]
) -> Tuple[str, bool]:
    edges = copy.deepcopy(list(adjacency))
    cells = copy.deepcopy(list(cell_relief))
    target = next((e for e in edges if e.get("relief_barrier")), None)
    if target is None:
        target = next((e for e in edges if e.get("kind") == "land-land"), None)
    if target is None or not cells:
        return "aucune arete barriere", False
    a, b = int(target["a"]), int(target["b"])
    by_id = {int(c["cell_id"]): c for c in cells}
    ca = float(by_id[a].get("centroid_elev_m") or by_id[a]["elev_mean_m"])
    target["relief_barrier"] = True
    target["crossing_elev_m"] = ca - 100.0
    result = g6d_barrier_above_both_cells(edges, cells)
    return f"arete_{a}_{b}_crossing_sous_centroide", (not result.passed)


def red_g6e(
    base_cell_ids: Sequence[int], cell_relief: Sequence[dict]
) -> Tuple[str, bool]:
    relief_ids = [int(c["cell_id"]) for c in cell_relief]
    if len(relief_ids) < 2:
        return "maille trop petite", False
    trimmed = relief_ids[:-1]
    result = g6e_mesh_unchanged(base_cell_ids, trimmed)
    return f"cell_id_{relief_ids[-1]}_retire", (not result.passed)


def run_all_red_g6(
    *,
    cell_relief: Sequence[dict],
    adjacency: Sequence[dict],
    base_cell_ids: Sequence[int],
    sha_pairs: Dict[str, List[str]],
) -> Dict[str, Dict[str, Any]]:
    """Un cas rouge par contrôle des six assemblés par `run_g6_green`."""
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q10", lambda: red_q10(sha_pairs)),
        ("G6-A", lambda: red_g6a()),
        ("G6-B", lambda: red_g6b(cell_relief)),
        ("G6-C", lambda: red_g6c(cell_relief)),
        ("G6-D", lambda: red_g6d(adjacency, cell_relief)),
        ("G6-E", lambda: red_g6e(base_cell_ids, cell_relief)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
