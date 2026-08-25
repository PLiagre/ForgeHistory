"""Cas cassés volontaires G4 — chaque contrôle doit pouvoir devenir ROUGE.

Un contrôle qui ne peut pas rougir ne prouve rien. Sept cas sont des mutations
locales sur des copies en mémoire ; le huitième, `G4-B`, est le cas **naturel** :
on coupe la déclaration historique et on constate que les bassins enfermés
redeviennent injoignables. Aucun cas ne modifie `qa/checks.py`.

Ce module expose `run_all_red_g4(...)`, importé par `tests/run_proof_g4.py`.
Il n'est pas collecté par pytest (comme ses homologues G2/G3).
"""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from shapely.geometry import Polygon, mapping

from qa.checks import (
    g4a_littorality_derived,
    g4b_open_sea_reachable,
    g4c_sea_covers_without_holes,
    g4d_sea_ids_no_collision,
    q1_polygon_validity,
    q4_no_isolated_entities,
    q7_adjacency_contiguous_typed,
    q10_determinism,
)


def _bowtie() -> dict:
    """Polygone auto-sécant : le défaut de validité le plus simple à constater."""
    return mapping(Polygon([(0, 0), (10, 10), (0, 10), (10, 0), (0, 0)]))


def _as_check_cells(sea_zones: Sequence[dict]) -> List[dict]:
    """`q1_polygon_validity` parle en cellules ; une zone s'y présente pareil."""
    return [
        {"cell_id": int(z["zone_id"]), "geometry": z["geometry"]} for z in sea_zones
    ]


def red_q1(sea_zones: Sequence[dict], sea_geom: Any) -> Tuple[str, bool]:
    zones = copy.deepcopy(list(sea_zones))
    if not zones:
        return "aucune zone de mer", False
    zones[0]["geometry"] = _bowtie()
    result = q1_polygon_validity(_as_check_cells(zones), sea_geom)
    return "zone_de_mer_0_polygone_auto_secant", (not result.passed)


def red_q4(land_cells: Sequence[dict], sea_zones: Sequence[dict]) -> Tuple[str, bool]:
    result = q4_no_isolated_entities(land_cells, sea_zones, [])
    return "graphe_vide_toutes_entites_isolees", (not result.passed)


def red_q7(
    land_cells: Sequence[dict],
    sea_zones: Sequence[dict],
    adjacency: Sequence[dict],
) -> Tuple[str, bool]:
    """Un détroit entre deux terres qui se touchent n'est pas un détroit."""
    edges = copy.deepcopy(list(adjacency))
    contiguous = next((e for e in edges if e["kind"] == "land-land"), None)
    if contiguous is None:
        return "aucune arete terre-terre a requalifier", False
    contiguous["kind"] = "strait"
    contiguous["gap_m"] = 0.0
    result = q7_adjacency_contiguous_typed(land_cells, sea_zones, edges)
    return (
        f"arete_terre_terre_{contiguous['a']}_{contiguous['b']}_requalifiee_en_detroit",
        (not result.passed),
    )


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    """Un tri instable à l'export ferait exactement cela : deux passes divergentes."""
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1]]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_forcees_sur_{key}", (not result.passed)


def red_g4a(
    coastal_ids: Sequence[int],
    adjacency: Sequence[dict],
    sea_ids: Sequence[int],
) -> Tuple[str, bool]:
    """Littoralité saisie à la main sur une cellule sans arête terre-mer."""
    coastal = sorted(int(x) for x in coastal_ids)
    if not coastal:
        return "aucune cellule littorale", False
    intruder = max(coastal) + 1
    while intruder in set(coastal):
        intruder += 1
    result = g4a_littorality_derived(coastal + [intruder], adjacency, sea_ids)
    return f"littoralite_saisie_sur_cellule_{intruder}_sans_arete_terre_mer", (
        not result.passed
    )


def red_g4b(unreachable_when_links_cut: Sequence[dict]) -> Tuple[str, bool]:
    """Cas NATUREL : la déclaration historique coupée, le monde se referme."""
    ids = [int(b["zone_id"]) for b in unreachable_when_links_cut]
    noms = ", ".join(
        f"{b.get('historical_name') or b.get('name')} (zone {b['zone_id']})"
        for b in unreachable_when_links_cut
    )
    result = g4b_open_sea_reachable(ids)
    case = (
        "cas_naturel_liens_topologiques_declares_coupes — "
        f"bassins enfermes injoignables : {noms}"
    )
    return case, (not result.passed)


def red_g4c(sea_zones: Sequence[dict], sea_geom: Any, area_eps: float) -> Tuple[str, bool]:
    """Retirer la zone d'un bassin enfermé ouvre un trou de couverture."""
    zones = copy.deepcopy(list(sea_zones))
    if len(zones) < 2:
        return "moins de deux zones", False
    enclosed = [z for z in zones if z.get("enclosed")]
    victim = enclosed[0] if enclosed else zones[0]
    kept = [z for z in zones if int(z["zone_id"]) != int(victim["zone_id"])]
    result = g4c_sea_covers_without_holes(kept, sea_geom, area_eps)
    return f"zone_{victim['zone_id']}_retiree_trou_de_couverture", (not result.passed)


def red_g4d(
    land_ids: Sequence[int], sea_ids: Sequence[int], sea_id_base: int
) -> Tuple[str, bool]:
    """Un `zone_id` ramené sur un `cell_id` existant : deux entités, une identité."""
    land = sorted(int(x) for x in land_ids)
    sea = sorted(int(x) for x in sea_ids)
    if not land or not sea:
        return "ensembles vides", False
    collided = [land[0]] + sea[1:]
    result = g4d_sea_ids_no_collision(land, collided, sea_id_base)
    return f"zone_forcee_sur_identifiant_terrestre_{land[0]}", (not result.passed)


def run_all_red_g4(
    *,
    land_cells: Sequence[dict],
    sea_zones: Sequence[dict],
    sea_geom: Any,
    adjacency: Sequence[dict],
    coastal_ids: Sequence[int],
    sha_pairs: Dict[str, List[str]],
    unreachable_when_links_cut: Sequence[dict],
    sea_id_base: int,
    area_eps: float,
) -> Dict[str, Dict[str, Any]]:
    """Un cas rouge par contrôle des huit assemblés par `run_g4_green`."""
    land_ids = [int(c["cell_id"]) for c in land_cells]
    sea_ids = [int(z["zone_id"]) for z in sea_zones]
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q1", lambda: red_q1(sea_zones, sea_geom)),
        ("Q4", lambda: red_q4(land_cells, sea_zones)),
        ("Q7", lambda: red_q7(land_cells, sea_zones, adjacency)),
        ("Q10", lambda: red_q10(sha_pairs)),
        ("G4-A", lambda: red_g4a(coastal_ids, adjacency, sea_ids)),
        ("G4-B", lambda: red_g4b(unreachable_when_links_cut)),
        ("G4-C", lambda: red_g4c(sea_zones, sea_geom, area_eps)),
        ("G4-D", lambda: red_g4d(land_ids, sea_ids, sea_id_base)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
