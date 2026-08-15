"""Cas cassés volontaires G5 — chaque contrôle doit pouvoir devenir ROUGE.

Un contrôle qui ne peut pas rougir ne prouve rien. Six mutations locales sur
des copies en mémoire ; aucune ne modifie `qa/checks.py`.

Ce module expose `run_all_red_g5(...)`, importé par `tests/run_proof_g5.py`.
Il n'est pas collecté par pytest (comme ses homologues G2/G3/G4).
"""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from shapely.geometry import LineString, mapping

from qa.checks import (
    g5a_attachments_match_geometry,
    g5b_no_river_in_open_sea,
    g5c_artery_has_navigable_river,
    g5d_mouth_on_adjacent_sea,
    q1_river_geometry_validity,
    q10_determinism,
)


def red_q1(segments: Sequence[dict]) -> Tuple[str, bool]:
    segs = copy.deepcopy(list(segments))
    if not segs:
        return "aucun troncon", False
    # Géométrie invalide : LineString dégénérée à un seul point répété + NaN.
    segs[0]["geometry"] = mapping(LineString([(0.0, 0.0), (float("nan"), 1.0)]))
    result = q1_river_geometry_validity(segs)
    return f"troncon_{segs[0]['segment_id']}_coordonnee_nan", (not result.passed)


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1]]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_forcees_sur_{key}", (not result.passed)


def red_g5a(
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    cells_xy: Sequence[Tuple[int, Any]],
) -> Tuple[str, bool]:
    segs = list(segments)
    if not segs:
        return "aucun troncon", False
    # Choisir un tronçon rattaché et retirer une cellule qu'il traverse vraiment
    # ne rougit pas G5-A (G5-A ne vérifie que le sens déclaré → géométrie).
    # À l'inverse : déclarer une cellule que le tronçon ne traverse pas.
    att = {k: list(v) for k, v in attachments.items()}
    victim = next((s for s in segs if att.get(s["segment_id"])), segs[0])
    sid = victim["segment_id"]
    known = {cid for cid, _ in cells_xy}
    declared = set(att.get(sid, []))
    intruder = next((cid for cid, _ in cells_xy if cid not in declared), None)
    if intruder is None:
        # Forcer un cell_id inconnu.
        intruder = (max(known) + 1) if known else 999999
    att[sid] = sorted(set(att.get(sid, [])) | {intruder})
    result = g5a_attachments_match_geometry(segs, att, cells_xy)
    return f"rattachement_force_cellule_{intruder}_sur_{sid}", (not result.passed)


def red_g5b(
    segments: Sequence[dict],
    land_xy: Any,
    sea_xy: Any,
) -> Tuple[str, bool]:
    segs = copy.deepcopy(list(segments))
    # Placer un faux tronçon entièrement en mer (hors Lake Centerline).
    if sea_xy is None or sea_xy.is_empty:
        return "mer vide", False
    # Un segment court au centroïde de la mer.
    c = sea_xy.representative_point()
    x, y = float(c.x), float(c.y)
    fake = {
        "segment_id": "fake_open_sea",
        "name": "FAKE_OPEN_SEA",
        "featurecla": "River",
        "navigability": "non_navigable",
        "geometry": mapping(LineString([(x, y), (x + 50.0, y + 50.0)])),
    }
    segs.append(fake)
    result = g5b_no_river_in_open_sea(segs, land_xy, sea_xy)
    return "troncon_entierement_en_pleine_mer_injecte", (not result.passed)


def red_g5c(
    adjacency: Sequence[dict],
    segments: Sequence[dict],
) -> Tuple[str, bool]:
    edges = copy.deepcopy(list(adjacency))
    # Arête fluvial_artery=true sans artery_rivers navigable.
    target = next((e for e in edges if e.get("kind") == "land-land"), None)
    if target is None:
        return "aucune arete land-land", False
    target["fluvial_artery"] = True
    target["artery_rivers"] = []
    result = g5c_artery_has_navigable_river(edges, segments)
    return (
        f"arete_{target['a']}_{target['b']}_fluvial_artery_sans_artery_rivers",
        (not result.passed),
    )


def red_g5d(mouths: Sequence[dict]) -> Tuple[str, bool]:
    ms = copy.deepcopy(list(mouths))
    if not ms:
        # Ensemble vide : forcer une embouchure invalide.
        ms = [
            {
                "segment_id": "fake_mouth",
                "name": "FAKE",
                "sea_zone_id": -1,
                "sea_zone_adjacent_to_river_cells": False,
            }
        ]
        case = "embouchure_inventee_flag_adjacent_false"
    else:
        ms[0]["sea_zone_adjacent_to_river_cells"] = False
        case = f"embouchure_{ms[0]['segment_id']}_flag_adjacent_force_false"
    result = g5d_mouth_on_adjacent_sea(ms)
    return case, (not result.passed)


def run_all_red_g5(
    *,
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    cells_xy: Sequence[Tuple[int, Any]],
    land_xy: Any,
    sea_xy: Any,
    adjacency: Sequence[dict],
    mouths: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> Dict[str, Dict[str, Any]]:
    """Un cas rouge par contrôle des six assemblés par `run_g5_green`."""
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q1", lambda: red_q1(segments)),
        ("Q10", lambda: red_q10(sha_pairs)),
        ("G5-A", lambda: red_g5a(segments, attachments, cells_xy)),
        ("G5-B", lambda: red_g5b(segments, land_xy, sea_xy)),
        ("G5-C", lambda: red_g5c(adjacency, segments)),
        ("G5-D", lambda: red_g5d(mouths)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
