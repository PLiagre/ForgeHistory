"""Contrôles R1 — gisements extractifs déclarés de 1400 (v1_081)."""

from __future__ import annotations

import unicodedata
from typing import Any, Dict, List, Sequence, Set

from shapely.geometry import Point, shape

from constants import (
    R1_COORDS_CERTAINTY,
    R1_FORBIDDEN_QUANTITY_KEYS,
    R1_PUBLISHED_DEPOSIT_FIELDS,
    R1_REQUIRED_DEPOSIT_FIELDS,
    R1_VALID_CERTAINTY,
    R1_VALID_RESOURCE_KINDS,
    R1_VALID_RICHNESS_CLASSES,
    WORLD_TERMS_FORBIDDEN_KEYS,
)
from qa.checks import CheckResult, q10_determinism


def _normalize_key(key: str) -> str:
    s = unicodedata.normalize("NFKD", str(key))
    s = "".join(c for c in s if not unicodedata.combining(c))
    return s.lower().replace("-", "_")


def _key_matches_forbidden(key: str) -> bool:
    norm = _normalize_key(key)
    if norm in WORLD_TERMS_FORBIDDEN_KEYS or norm in R1_FORBIDDEN_QUANTITY_KEYS:
        return True
    for tok in norm.split("_"):
        if tok and (
            tok in WORLD_TERMS_FORBIDDEN_KEYS or tok in R1_FORBIDDEN_QUANTITY_KEYS
        ):
            return True
    return False


def _walk_forbidden_keys(
    obj: Any,
    path: str = "",
    *,
    skip_par_classe: bool = False,
) -> List[str]:
    bad: List[str] = []
    if isinstance(obj, dict):
        for k, v in obj.items():
            key = str(k)
            here = f"{path}.{key}" if path else key
            if skip_par_classe and path.endswith("par_classe_de_richesse"):
                continue
            if _key_matches_forbidden(key):
                bad.append(here)
            bad.extend(
                _walk_forbidden_keys(
                    v,
                    here,
                    skip_par_classe=skip_par_classe,
                )
            )
    elif isinstance(obj, list):
        for i, item in enumerate(obj):
            bad.extend(
                _walk_forbidden_keys(
                    item,
                    f"{path}[{i}]",
                    skip_par_classe=skip_par_classe,
                )
            )
    return bad


def _schema_keys_exact(row: dict, required: Sequence[str]) -> bool:
    return set(row.keys()) == set(required)


def r1a_declarations_complete(
    declarations: dict,
    published: Sequence[dict],
    step_source: str,
    declared_ids: Sequence[str],
) -> CheckResult:
    problems: List[str] = []
    deposits = declarations.get("deposits") or []
    if not deposits:
        problems.append("deposits_vide")
    req = set(R1_REQUIRED_DEPOSIT_FIELDS)
    pub = set(R1_PUBLISHED_DEPOSIT_FIELDS)
    for dep in deposits:
        if not _schema_keys_exact(dep, R1_REQUIRED_DEPOSIT_FIELDS):
            extra = set(dep.keys()) - req
            missing = req - set(dep.keys())
            problems.append(f"schema_decl_{dep.get('id')}_extra={sorted(extra)}_miss={sorted(missing)}")
        for field in R1_REQUIRED_DEPOSIT_FIELDS:
            val = dep.get(field)
            if val is None or (isinstance(val, str) and not str(val).strip()):
                problems.append(f"vide_{dep.get('id')}.{field}")
        if dep.get("resource") not in R1_VALID_RESOURCE_KINDS:
            problems.append(f"resource_{dep.get('id')}={dep.get('resource')}")
        if dep.get("certainty") not in R1_VALID_CERTAINTY:
            problems.append(f"certainty_{dep.get('id')}={dep.get('certainty')}")
        if dep.get("coords_certainty") != R1_COORDS_CERTAINTY:
            problems.append(f"coords_{dep.get('id')}={dep.get('coords_certainty')}")
    for dep in published:
        if not _schema_keys_exact(dep, R1_PUBLISHED_DEPOSIT_FIELDS):
            problems.append(f"schema_pub_{dep.get('id')}")
    for dep_id in declared_ids:
        needle = f'"{dep_id}"'
        if needle in step_source:
            problems.append(f"en_dur_{dep_id}")
    ok = not problems
    return CheckResult(
        id="R1-A",
        name="declarations completes et hors code",
        passed=ok,
        detail="; ".join(problems[:12]) if problems else "ok",
    )


def r1b_containment_only(
    published: Sequence[dict],
    cells: Sequence[dict],
    containment_recheck: Sequence[tuple],
    cells_resources: Sequence[dict],
) -> CheckResult:
    from projection import Projector, detect_projection

    cell_geoms = [(int(c["cell_id"]), shape(c["geometry"])) for c in cells]
    bad: List[str] = []
    projector = Projector(detect_projection())
    for dep_id, cell_id in containment_recheck:
        dep = next((d for d in published if d["id"] == dep_id), None)
        if dep is None:
            bad.append(f"missing_{dep_id}")
            continue
        lon = float(dep["lon"])
        lat = float(dep["lat"])
        x_m, y_m = projector.project_xy(lon, lat)
        pt = Point(x_m, y_m)
        geom_match = [g for i, g in cell_geoms if i == cell_id]
        if not geom_match:
            bad.append(f"cellule_inconnue_{cell_id}")
            continue
        geom = geom_match[0]
        if not (geom.contains(pt) or geom.covers(pt)):
            bad.append(f"non_contenu_{dep_id}_cell_{cell_id}")
    dep_cells: Dict[str, List[int]] = {}
    for row in cells_resources:
        for dep_id in row.get("resources") or []:
            dep_cells.setdefault(str(dep_id), []).append(int(row["cell_id"]))
    dup = [k for k, v in dep_cells.items() if len(v) > 1]
    if dup:
        bad.append(f"deux_cellules_{dup[:4]}")
    for dep in published:
        if dep.get("attachment") == "contained" and dep.get("cell_id") is None:
            bad.append(f"sans_cellule_{dep.get('id')}")
    ok = not bad
    return CheckResult(
        id="R1-B",
        name="contenance seule",
        passed=ok,
        detail="; ".join(bad[:12]) if bad else "ok",
    )


def r1c_no_silent_omission(stats: dict) -> CheckResult:
    declared = int(stats.get("gisements_declares", -1))
    attached = int(stats.get("gisements_rattaches", -1))
    outside_w = stats.get("gisements_hors_fenetre")
    outside_l = stats.get("gisements_hors_terre")
    if not isinstance(outside_w, list) or not isinstance(outside_l, list):
        return CheckResult(
            id="R1-C",
            name="aucune omission silencieuse",
            passed=False,
            detail="listes_hors_manquantes",
        )
    total = attached + len(outside_w) + len(outside_l)
    ok = total == declared
    return CheckResult(
        id="R1-C",
        name="aucune omission silencieuse",
        passed=ok,
        detail=f"declares={declared} somme={total} hors_f={len(outside_w)} hors_t={len(outside_l)}",
    )


def r1d_reversibility(
    sha_on: str,
    sha_off: str,
    stats_off: dict,
    cells_off_count: int,
    cells_g3_count: int,
) -> CheckResult:
    problems: List[str] = []
    if sha_on == sha_off:
        problems.append("empreintes_egales")
    if int(stats_off.get("gisements_rattaches", -1)) != 0:
        problems.append(f"rattaches_off={stats_off.get('gisements_rattaches')}")
    if int(stats_off.get("cellules_dotees", -1)) != 0:
        problems.append(f"dotees_off={stats_off.get('cellules_dotees')}")
    if cells_off_count != cells_g3_count:
        problems.append(f"cellules_off={cells_off_count}!={cells_g3_count}")
    ok = not problems and bool(sha_on) and bool(sha_off)
    return CheckResult(
        id="R1-D",
        name="reversibilite declarations",
        passed=ok,
        detail="; ".join(problems) if problems else "ok",
    )


def r1e_no_bareme_ni_quantite(artifact_docs: Sequence[dict]) -> CheckResult:
    bad: List[str] = []
    for doc in artifact_docs:
        bad.extend(_walk_forbidden_keys(doc))
    return CheckResult(
        id="R1-E",
        name="ni bareme ni quantite",
        passed=len(bad) == 0,
        detail="; ".join(bad[:12]) if bad else "ok",
    )


def r1f_cell_mesh_unchanged(
    base_cell_ids: Sequence[int],
    resource_cells: Sequence[dict],
    artifact_docs: Sequence[dict],
) -> CheckResult:
    base = sorted(int(x) for x in base_cell_ids)
    got = sorted(int(c["cell_id"]) for c in resource_cells)
    spatial_bad: List[str] = []

    def walk(obj: Any, path: str = "") -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                key = str(k).lower()
                here = f"{path}.{k}" if path else str(k)
                if any(tok in key for tok in ("province", "owner", "country", "pays")):
                    spatial_bad.append(here)
                walk(v, here)
        elif isinstance(obj, list):
            for i, item in enumerate(obj):
                walk(item, f"{path}[{i}]")

    for doc in artifact_docs:
        walk(doc)
    ok = base == got and not spatial_bad
    return CheckResult(
        id="R1-F",
        name="maille cellule inchangee sans cle concurrente",
        passed=ok,
        detail=(
            f"base={len(base)} got={len(got)} spatial={spatial_bad[:8]}"
            if not ok
            else "ok"
        ),
    )


def _richness_literals_in_source(source: str, vocabulary: Sequence[str]) -> List[str]:
    found: List[str] = []
    for val in vocabulary:
        if f'"{val}"' in source:
            found.append(val)
    return found


def r1g_richness_class_is_name(
    declarations: dict,
    published: Sequence[dict],
    stats: dict,
    cells_resources: Sequence[dict],
    artifact_docs: Sequence[dict],
    step_source: str,
    checks_source: str,
) -> CheckResult:
    problems: List[str] = []
    vocab = set(R1_VALID_RICHNESS_CLASSES)

    def check_richness(dep: dict, label: str) -> None:
        rc = dep.get("richness_class")
        if not isinstance(rc, str) or not rc.strip() or rc not in vocab:
            problems.append(f"vocab_{label}_{dep.get('id')}={rc!r}")

    for dep in declarations.get("deposits") or []:
        check_richness(dep, "decl")
    for dep in published:
        check_richness(dep, "pub")

    for val in _richness_literals_in_source(step_source, R1_VALID_RICHNESS_CLASSES):
        problems.append(f"literal_step_{val}")
    for val in _richness_literals_in_source(checks_source, R1_VALID_RICHNESS_CLASSES):
        problems.append(f"literal_checks_{val}")

    def walk_numeric_class_keys(obj: Any, path: str = "") -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                here = f"{path}.{k}" if path else str(k)
                if str(k) in vocab and isinstance(v, (int, float)):
                    if "par_classe_de_richesse" not in path:
                        problems.append(f"num_key_{here}={v}")
                if k == "par_classe_de_richesse" and isinstance(v, dict):
                    for ck, cv in v.items():
                        if str(ck) in vocab and not isinstance(cv, int):
                            problems.append(f"par_classe_non_int_{ck}={cv!r}")
                walk_numeric_class_keys(v, here)
        elif isinstance(obj, list):
            for i, item in enumerate(obj):
                walk_numeric_class_keys(item, f"{path}[{i}]")

    for doc in artifact_docs:
        walk_numeric_class_keys(doc)

    par = stats.get("par_classe_de_richesse") or {}
    if set(par.keys()) != vocab:
        problems.append(f"par_classe_cles={sorted(par.keys())}")
    declared = int(stats.get("gisements_declares", -1))
    if sum(int(par.get(k, 0)) for k in vocab) != declared:
        problems.append(f"somme_par_classe!={declared}")

    def walk_class_in_cells(obj: Any) -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                if str(k) in vocab or (isinstance(v, str) and v in vocab):
                    problems.append(f"classe_dans_cellules_{k}={v}")
                walk_class_in_cells(v)
        elif isinstance(obj, list):
            for item in obj:
                walk_class_in_cells(item)

    walk_class_in_cells({"cells": cells_resources})

    ok = not problems
    return CheckResult(
        id="R1-G",
        name="classe de richesse est un nom",
        passed=ok,
        detail="; ".join(problems[:12]) if problems else "ok",
    )


def run_r1_green(
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
) -> List[CheckResult]:
    return [
        q10_determinism(sha_pairs),
        r1a_declarations_complete(
            declarations, published, step_source, declared_ids
        ),
        r1b_containment_only(
            published, cells_g3, containment_recheck, cells_resources
        ),
        r1c_no_silent_omission(stats),
        r1d_reversibility(
            sha_cells_on,
            sha_cells_off,
            stats_off,
            cells_off_count,
            len(cells_g3),
        ),
        r1e_no_bareme_ni_quantite(artifact_docs),
        r1f_cell_mesh_unchanged(
            [int(c["cell_id"]) for c in cells_g3],
            cells_resources,
            artifact_docs,
        ),
        r1g_richness_class_is_name(
            declarations,
            published,
            stats,
            cells_resources,
            artifact_docs,
            step_source,
            checks_source,
        ),
    ]
