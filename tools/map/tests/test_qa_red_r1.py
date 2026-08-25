"""Cas rouges R1 — un par contrôle (Q10 + R1-A..G)."""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from qa.checks import q10_determinism
from qa.checks_r1 import (
    r1a_declarations_complete,
    r1b_containment_only,
    r1c_no_silent_omission,
    r1d_reversibility,
    r1e_no_bareme_ni_quantite,
    r1f_cell_mesh_unchanged,
    r1g_richness_class_is_name,
)


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1] or "x"]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_sur_{key}", (not result.passed)


def red_r1a(
    declarations: dict,
    published: Sequence[dict],
    step_source: str,
    declared_ids: Sequence[str],
) -> Tuple[str, bool]:
    decl = copy.deepcopy(declarations)
    deps = decl.get("deposits") or []
    if not deps:
        return "aucune declaration", False
    deps[0] = dict(deps[0])
    deps[0].pop("source", None)
    result = r1a_declarations_complete(decl, published, step_source, declared_ids)
    return f"source_retiree_{deps[0].get('id')}", (not result.passed)


def red_r1b(
    published: Sequence[dict],
    cells_g3: Sequence[dict],
    containment_recheck: Sequence[tuple],
    cells_resources: Sequence[dict],
) -> Tuple[str, bool]:
    from shapely.geometry import Point, shape
    from projection import Projector, detect_projection

    if not containment_recheck:
        return "aucun rattachement", False
    dep_id, cell_id = containment_recheck[0]
    dep = next((d for d in published if d["id"] == dep_id), None)
    if dep is None:
        return "gisement_introuvable", False
    projector = Projector(detect_projection())
    x_m, y_m = projector.project_xy(float(dep["lon"]), float(dep["lat"]))
    pt = Point(x_m, y_m)
    wrong_cell = None
    for c in cells_g3:
        cid = int(c["cell_id"])
        if cid == cell_id:
            continue
        geom = shape(c["geometry"])
        if not (geom.contains(pt) or geom.covers(pt)):
            wrong_cell = cid
            break
    if wrong_cell is None:
        return "pas_de_cellule_incorrecte", False
    result = r1b_containment_only(
        published, cells_g3, [(dep_id, wrong_cell)], cells_resources
    )
    return f"contenance_forcee_{dep_id}_cell_{wrong_cell}", (not result.passed)


def red_r1c(stats: dict) -> Tuple[str, bool]:
    st = copy.deepcopy(stats)
    st["gisements_rattaches"] = int(st.get("gisements_rattaches", 0)) + 1
    result = r1c_no_silent_omission(st)
    return "somme_categories_cassee", (not result.passed)


def red_r1d(sha_on: str, sha_off: str, stats_off: dict, cells_off_count: int, cells_g3_count: int) -> Tuple[str, bool]:
    result = r1d_reversibility(sha_on, sha_on, stats_off, cells_off_count, cells_g3_count)
    return "empreinte_off_egale_on", (not result.passed)


def red_r1e(artifact_docs: Sequence[dict]) -> Tuple[str, bool]:
    docs = copy.deepcopy(list(artifact_docs))
    if not docs:
        return "aucun artefact", False
    if isinstance(docs[0], dict):
        docs[0]["tonnage"] = 100
    result = r1e_no_bareme_ni_quantite(docs)
    return "cle_tonnage_injectee", (not result.passed)


def red_r1f(
    base_ids: Sequence[int],
    cells_resources: Sequence[dict],
    artifact_docs: Sequence[dict],
) -> Tuple[str, bool]:
    docs = copy.deepcopy(list(artifact_docs))
    if not docs:
        return "aucun artefact", False
    if isinstance(docs[0], dict):
        docs[0]["province_id"] = 1
    result = r1f_cell_mesh_unchanged(base_ids, cells_resources, docs)
    return "province_id_dans_artefact", (not result.passed)


def red_r1g(
    declarations: dict,
    published: Sequence[dict],
    stats: dict,
    cells_resources: Sequence[dict],
    artifact_docs: Sequence[dict],
    step_source: str,
    checks_source: str,
) -> Tuple[str, bool]:
    pub = copy.deepcopy(list(published))
    if not pub:
        return "aucun gisement publie", False
    pub[0] = dict(pub[0])
    pub[0]["richness_class"] = 3
    result = r1g_richness_class_is_name(
        declarations,
        pub,
        stats,
        cells_resources,
        artifact_docs,
        step_source,
        checks_source,
    )
    return f"richness_numerique_{pub[0].get('id')}", (not result.passed)


def run_all_red_r1(
    *,
    declarations: dict,
    published: Sequence[dict],
    cells_g3: Sequence[dict],
    cells_resources: Sequence[dict],
    stats: dict,
    artifact_docs: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
    sha_cells_on: str,
    sha_cells_off: str,
    stats_off: dict,
    cells_off_count: int,
    step_source: str,
    checks_source: str,
    declared_ids: Sequence[str],
    containment_recheck: Sequence[tuple],
) -> Dict[str, Dict[str, Any]]:
    base_ids = [int(c["cell_id"]) for c in cells_g3]
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q10", lambda: red_q10(sha_pairs)),
        (
            "R1-A",
            lambda: red_r1a(declarations, published, step_source, declared_ids),
        ),
        (
            "R1-B",
            lambda: red_r1b(
                published, cells_g3, containment_recheck, cells_resources
            ),
        ),
        ("R1-C", lambda: red_r1c(stats)),
        (
            "R1-D",
            lambda: red_r1d(
                sha_cells_on, sha_cells_off, stats_off, cells_off_count, len(cells_g3)
            ),
        ),
        ("R1-E", lambda: red_r1e(artifact_docs)),
        (
            "R1-F",
            lambda: red_r1f(base_ids, cells_resources, artifact_docs),
        ),
        (
            "R1-G",
            lambda: red_r1g(
                declarations,
                published,
                stats,
                cells_resources,
                artifact_docs,
                step_source,
                checks_source,
            ),
        ),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
