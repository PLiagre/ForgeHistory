"""Contrôles qualité pipeline (§8.1 du plan) — G1 cellules + G2 littoral."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Sequence, Tuple

from shapely.geometry import Point, shape
from shapely.ops import unary_union
from shapely.validation import explain_validity

from constants import AREA_EPS, LENGTH_EPS, OVERLAP_EPS, SEA_CELL_ID
from constants import G6_ELEV_PLAUSIBLE_MAX_M, G6_ELEV_PLAUSIBLE_MIN_M


@dataclass
class CheckResult:
    id: str
    name: str
    passed: bool
    detail: str = ""
    red_proof: str = ""


def _cell_geoms(cells: Sequence[dict]) -> List[Any]:
    return [shape(c["geometry"]) for c in cells]


def q1_polygon_validity(cells: Sequence[dict], land_geom: Any) -> CheckResult:
    bad: List[str] = []
    if not land_geom.is_valid:
        bad.append(f"land:{explain_validity(land_geom)}")
    for cell in cells:
        g = shape(cell["geometry"])
        if not g.is_valid:
            bad.append(f"cell_{cell['cell_id']}:{explain_validity(g)}")
    return CheckResult(
        id="Q1",
        name="validite des polygones",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else "tous valides",
    )


def q2_no_holes(cells: Sequence[dict], land_geom: Any) -> CheckResult:
    geoms = _cell_geoms(cells)
    if not geoms:
        return CheckResult("Q2", "absence de trous", False, "aucune cellule")
    union = unary_union(geoms)
    missing = land_geom.difference(union).area
    extra = union.difference(land_geom).area
    ok = missing <= AREA_EPS and extra <= AREA_EPS
    return CheckResult(
        id="Q2",
        name="absence de trous",
        passed=ok,
        detail=f"missing_m2={missing:.3f} extra_m2={extra:.3f} eps={AREA_EPS}",
    )


def q3_no_overlaps(cells: Sequence[dict]) -> CheckResult:
    geoms = _cell_geoms(cells)
    if len(geoms) < 2:
        return CheckResult("Q3", "absence de chevauchements", True, "moins de 2 cellules")
    sum_areas = sum(g.area for g in geoms)
    union_area = unary_union(geoms).area
    overlap = sum_areas - union_area
    ok = overlap <= OVERLAP_EPS
    return CheckResult(
        id="Q3",
        name="absence de chevauchements",
        passed=ok,
        detail=f"overlap_m2={overlap:.3f} eps={OVERLAP_EPS}",
    )


def q4_no_isolated(
    cells: Sequence[dict], adjacency: Sequence[dict]
) -> CheckResult:
    land_ids = {c["cell_id"] for c in cells}
    connected: set[int] = set()
    for edge in adjacency:
        a, b = int(edge["a"]), int(edge["b"])
        if a in land_ids:
            connected.add(a)
        if b in land_ids:
            connected.add(b)
    isolated = sorted(land_ids - connected)
    return CheckResult(
        id="Q4",
        name="aucune cellule isolee",
        passed=len(isolated) == 0,
        detail=f"isolated={isolated}" if isolated else "aucune isolee",
    )


def q5_cities_in_exactly_one_land_cell(
    cities: Sequence[dict], cells: Sequence[dict]
) -> CheckResult:
    problems: List[str] = []
    cell_geoms = [(c["cell_id"], shape(c["geometry"])) for c in cells]
    for city in cities:
        # Préférer les mètres projetés (géométrie des cellules) ; x/y = unités de jeu.
        x = float(city.get("x_m", city["x"]))
        y = float(city.get("y_m", city["y"]))
        pt = Point(x, y)
        hits = [cid for cid, g in cell_geoms if g.contains(pt) or g.touches(pt)]
        if len(hits) != 1:
            problems.append(f"{city['name']}:hits={hits}")
    return CheckResult(
        id="Q5",
        name="chaque ville dans exactement une cellule terrestre",
        passed=len(problems) == 0,
        detail="; ".join(problems) if problems else "ok",
    )


def q7_adjacency_contiguous(
    cells: Sequence[dict], adjacency: Sequence[dict]
) -> CheckResult:
    by_id = {c["cell_id"]: shape(c["geometry"]) for c in cells}
    bad: List[str] = []
    for edge in adjacency:
        a, b = int(edge["a"]), int(edge["b"])
        kind = edge["kind"]
        if kind == "land-sea":
            land_id = a if a != SEA_CELL_ID else b
            sea_id = b if a != SEA_CELL_ID else a
            if sea_id != SEA_CELL_ID or land_id not in by_id:
                bad.append(f"sea_edge_malformed:{a}-{b}")
                continue
            shared = float(edge.get("shared_length_m", 0.0))
            if shared < LENGTH_EPS:
                bad.append(f"land-sea_non_contiguous:{land_id}")
            continue
        if a not in by_id or b not in by_id:
            bad.append(f"unknown_cell:{a}-{b}")
            continue
        inter = by_id[a].boundary.intersection(by_id[b].boundary)
        length = inter.length if not inter.is_empty else 0.0
        if length < LENGTH_EPS:
            bad.append(f"non_contiguous:{a}-{b}:len={length:.3f}")
    return CheckResult(
        id="Q7",
        name="aucune adjacence entre cellules non contigues",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else "ok",
    )


def q10_determinism(sha_pairs: Dict[str, List[str]]) -> CheckResult:
    mismatches = [
        path for path, pair in sha_pairs.items() if len(pair) < 2 or pair[0] != pair[1]
    ]
    return CheckResult(
        id="Q10",
        name="determinisme d export",
        passed=len(mismatches) == 0,
        detail=(
            f"mismatch={mismatches}" if mismatches else f"match_on={len(sha_pairs)}_files"
        ),
    )


def run_all_green(
    cells: Sequence[dict],
    land_geom: Any,
    adjacency: Sequence[dict],
    cities: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        q1_polygon_validity(cells, land_geom),
        q2_no_holes(cells, land_geom),
        q3_no_overlaps(cells),
        q4_no_isolated(cells, adjacency),
        q5_cities_in_exactly_one_land_cell(cities, cells),
        q7_adjacency_contiguous(cells, adjacency),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G2 — littoral réel (pas de cellules)
# ---------------------------------------------------------------------------


def q1_land_validity(land_geom: Any) -> CheckResult:
    bad: List[str] = []
    if land_geom is None or land_geom.is_empty:
        bad.append("land:empty")
    elif not land_geom.is_valid:
        bad.append(f"land:{explain_validity(land_geom)}")
    return CheckResult(
        id="Q1",
        name="validite des polygones",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else "tous valides",
    )


def g2a_land_within_window(land_ll: Any, window: Any) -> CheckResult:
    if land_ll is None or land_ll.is_empty:
        return CheckResult("G2-A", "terre incluse dans la fenetre", False, "empty")
    outside = land_ll.difference(window.buffer(1e-9))
    area_out = 0.0 if outside.is_empty else outside.area
    ok = area_out <= 1e-8
    return CheckResult(
        id="G2-A",
        name="terre incluse dans la fenetre",
        passed=ok,
        detail=f"outside_deg2={area_out:.3e}",
    )


def g2b_lakes_are_holes(land_ll: Any, lakes_ll: Any) -> CheckResult:
    if lakes_ll is None or lakes_ll.is_empty:
        return CheckResult("G2-B", "lacs sont des trous", True, "no_lakes")
    leftover = land_ll.intersection(lakes_ll)
    leftover_area = 0.0 if leftover.is_empty else leftover.area
    ok = leftover_area <= 1e-10
    return CheckResult(
        id="G2-B",
        name="lacs sont des trous",
        passed=ok,
        detail=f"land_intersect_lakes_deg2={leftover_area:.3e}",
    )


def g2c_area_plausible(area_km2: float, amin: float, amax: float) -> CheckResult:
    ok = amin <= area_km2 <= amax
    return CheckResult(
        id="G2-C",
        name="surface terre ordre de grandeur plausible",
        passed=ok,
        detail=f"area_km2={area_km2:.1f} expected=[{amin:.0f},{amax:.0f}]",
    )


def run_g2_green(
    land_ll: Any,
    land_xy: Any,
    lakes_ll: Any,
    window: Any,
    area_km2: float,
    area_min: float,
    area_max: float,
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        q1_land_validity(land_xy),
        g2a_land_within_window(land_ll, window),
        g2b_lakes_are_holes(land_ll, lakes_ll),
        g2c_area_plausible(area_km2, area_min, area_max),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G2-bis — corrections 1400 (reclassement, pas de dessin)
# ---------------------------------------------------------------------------

VALID_HISTORICAL_CERTAINTY = (
    "attested",
    "reconstructed",
    "reconstructed_established",
    "gameplay",
)


def g2b_a_corrections_have_certainty_and_source(
    corrections: Sequence[dict],
    valid_levels: Sequence[str] = VALID_HISTORICAL_CERTAINTY,
) -> CheckResult:
    """G2b-A : toute correction porte certitude valide + source non vide."""
    levels = set(valid_levels)
    bad: List[str] = []
    for corr in corrections:
        cid = str(corr.get("id") or "?")
        certainty = corr.get("certainty")
        source = corr.get("source")
        if certainty not in levels:
            bad.append(f"{cid}:certainty={certainty!r}")
        if source is None or str(source).strip() == "":
            bad.append(f"{cid}:source_empty")
    return CheckResult(
        id="G2b-A",
        name="corrections : certitude valide et source non vide",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"n={len(list(corrections))}_ok",
    )


def g2b_b_reversibility(
    sha_disabled: Dict[str, str],
    sha_reference_g2: Dict[str, str],
) -> CheckResult:
    """G2b-B : corrections OFF ⇒ SHA256 identiques aux sorties v1_046 / G2."""
    keys = sorted(set(sha_disabled.keys()) & set(sha_reference_g2.keys()))
    if not keys:
        return CheckResult(
            id="G2b-B",
            name="revesibilite corrections OFF = sorties G2",
            passed=False,
            detail="aucun chemin commun a comparer",
        )
    mismatches = [
        k for k in keys if sha_disabled.get(k) != sha_reference_g2.get(k)
    ]
    return CheckResult(
        id="G2b-B",
        name="revesibilite corrections OFF = sorties G2",
        passed=len(mismatches) == 0,
        detail=(
            f"mismatch={mismatches}"
            if mismatches
            else f"match_on={len(keys)}_files"
        ),
    )


def g2b_c_no_invented_vertices(
    land_geom: Any,
    reference_vertices: set,
    *,
    tol: float = 1e-7,
) -> CheckResult:
    """G2b-C : aucun sommet inventé au-delà du littoral G2 de référence.

    Reclasser ne doit pas ajouter de sommet absent de la géométrie G2
    (mêmes polygones source, seule la classe change).
    """
    found: List[tuple] = []

    def collect(geom: Any) -> None:
        if geom is None or geom.is_empty:
            return
        if geom.geom_type == "Polygon":
            found.extend((float(x), float(y)) for x, y in geom.exterior.coords)
            for ring in geom.interiors:
                found.extend((float(x), float(y)) for x, y in ring.coords)
        elif geom.geom_type == "MultiPolygon":
            for g in geom.geoms:
                collect(g)
        elif hasattr(geom, "geoms"):
            for g in geom.geoms:
                collect(g)

    collect(land_geom)
    allowed_round = {
        (round(x, 9), round(y, 9)) for (x, y) in reference_vertices
    }
    bad = 0
    for x, y in found:
        key = (round(x, 9), round(y, 9))
        if key in allowed_round:
            continue
        hit = False
        for sx, sy in reference_vertices:
            if abs(sx - x) <= tol and abs(sy - y) <= tol:
                hit = True
                break
        if not hit:
            bad += 1
            if bad >= 8:
                break
    return CheckResult(
        id="G2b-C",
        name="aucune geometrie nouvelle (sommets sous-ensemble G2)",
        passed=bad == 0,
        detail=f"invented_vertices={bad} checked={len(found)}",
    )


def g2b_d_idempotence(geom_once: Any, geom_twice: Any, area_eps: float = 1e-10) -> CheckResult:
    """G2b-D : appliquer deux fois = une fois (aire symétrique nulle)."""
    if geom_once is None or geom_twice is None:
        return CheckResult("G2b-D", "idempotence des corrections", False, "null_geom")
    delta = geom_once.symmetric_difference(geom_twice)
    area = 0.0 if delta.is_empty else float(delta.area)
    ok = area <= area_eps
    return CheckResult(
        id="G2b-D",
        name="idempotence des corrections",
        passed=ok,
        detail=f"symmetric_diff_deg2={area:.3e}",
    )


def run_g2b_green(
    *,
    land_ll: Any,
    land_xy: Any,
    window: Any,
    area_km2: float,
    area_min: float,
    area_max: float,
    sha_pairs: Dict[str, List[str]],
    corrections: Sequence[dict],
    sha_disabled: Dict[str, str],
    sha_reference_g2: Dict[str, str],
    source_vertices: set,
    geom_once: Any,
    geom_twice: Any,
) -> List[CheckResult]:
    return [
        q1_land_validity(land_xy),
        g2a_land_within_window(land_ll, window),
        g2c_area_plausible(area_km2, area_min, area_max),
        q10_determinism(sha_pairs),
        g2b_a_corrections_have_certainty_and_source(corrections),
        g2b_b_reversibility(sha_disabled, sha_reference_g2),
        g2b_c_no_invented_vertices(land_ll, source_vertices),
        g2b_d_idempotence(geom_once, geom_twice),
    ]


# ---------------------------------------------------------------------------
# G3 — cellules territoriales contraintes au littoral
# ---------------------------------------------------------------------------


def g3a_no_sea_in_cells(
    cells: Sequence[dict], land_geom: Any, area_eps: float
) -> CheckResult:
    """G3-A : aucune cellule ne contient de mer (hors terre)."""
    bad: List[str] = []
    for cell in cells:
        g = shape(cell["geometry"])
        outside = g.difference(land_geom)
        area = 0.0 if outside.is_empty else float(outside.area)
        if area > area_eps:
            bad.append(f"cell_{cell['cell_id']}:sea_m2={area:.1f}")
            if len(bad) >= 8:
                break
    return CheckResult(
        id="G3-A",
        name="aucune cellule ne contient de mer",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"n={len(list(cells))}_ok eps={area_eps}",
    )


def g3b_all_land_masses_covered(
    cells: Sequence[dict], land_parts: Sequence[Any], area_eps: float
) -> CheckResult:
    """G3-B : chaque masse de terre a au moins une cellule qui la couvre."""
    union = unary_union([shape(c["geometry"]) for c in cells]) if cells else None
    uncovered: List[str] = []
    for i, part in enumerate(land_parts):
        if union is None:
            uncovered.append(f"part_{i}:no_cells")
            continue
        inter = part.intersection(union)
        area = 0.0 if inter.is_empty else float(inter.area)
        if area <= area_eps:
            uncovered.append(f"part_{i}:covered_m2={area:.1f}")
    return CheckResult(
        id="G3-B",
        name="toutes les masses de terre sont couvertes",
        passed=len(uncovered) == 0,
        detail=(
            "; ".join(uncovered)
            if uncovered
            else f"parts={len(list(land_parts))}_covered"
        ),
    )


def g3c_stable_ids(
    registry_a: Sequence[dict], registry_b: Sequence[dict]
) -> CheckResult:
    """G3-C : mêmes domain_key → mêmes cell_id sur deux régénérations."""
    map_a = {
        str(r["domain_key"]): int(r["cell_id"])
        for r in registry_a
        if r.get("retired") is None
    }
    map_b = {
        str(r["domain_key"]): int(r["cell_id"])
        for r in registry_b
        if r.get("retired") is None
    }
    keys = sorted(set(map_a) | set(map_b))
    bad: List[str] = []
    if set(map_a.keys()) != set(map_b.keys()):
        only_a = sorted(set(map_a) - set(map_b))
        only_b = sorted(set(map_b) - set(map_a))
        bad.append(f"key_diff only_a={only_a[:5]} only_b={only_b[:5]}")
    for k in keys:
        if k in map_a and k in map_b and map_a[k] != map_b[k]:
            bad.append(f"{k}: {map_a[k]}!={map_b[k]}")
            if len(bad) >= 8:
                break
    return CheckResult(
        id="G3-C",
        name="identifiants stables d une regeneration a l autre",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"stable_on={len(map_a)}_keys",
    )


def g3d_cell_count_in_range(
    cell_count: int, amin: int, amax: int
) -> CheckResult:
    ok = amin <= cell_count <= amax
    return CheckResult(
        id="G3-D",
        name=f"compte de cellules dans [{amin}, {amax}]",
        passed=ok,
        detail=f"count={cell_count} expected=[{amin},{amax}]",
    )


def g3e_area_within_bounds(
    cells: Sequence[dict],
    *,
    floor_km2: float,
    ceil_km2: float,
    singleton_ids: Sequence[int] | None = None,
) -> CheckResult:
    """G3-E : surface de chaque cellule dans les bornes (îles singleton exemptées du plancher)."""
    exempt = set(int(i) for i in (singleton_ids or []))
    bad: List[str] = []
    for c in cells:
        a = float(c["area_km2"])
        cid = int(c["cell_id"])
        if a > ceil_km2:
            bad.append(f"cell_{cid}:area={a:.1f}>ceil={ceil_km2}")
        elif a < floor_km2 and cid not in exempt:
            bad.append(f"cell_{cid}:area={a:.1f}<floor={floor_km2}")
        if len(bad) >= 8:
            break
    return CheckResult(
        id="G3-E",
        name="surface de chaque cellule dans les bornes declarees",
        passed=len(bad) == 0,
        detail=(
            "; ".join(bad)
            if bad
            else f"n={len(list(cells))}_ok floor={floor_km2} ceil={ceil_km2} "
            f"singleton_exempt={len(exempt)}"
        ),
    )


def g3f_area_dispersion(
    cells: Sequence[dict], *, max_median_ratio: float
) -> CheckResult:
    """G3-F : rapport surface max / médiane sous le plafond déclaré."""
    areas = sorted(float(c["area_km2"]) for c in cells)
    if not areas:
        return CheckResult("G3-F", "dispersion des surfaces", False, "aucune cellule")
    n = len(areas)
    mid = n // 2
    med = float(areas[mid]) if n % 2 else (float(areas[mid - 1]) + float(areas[mid])) / 2.0
    mx = float(areas[-1])
    ratio = (mx / med) if med > 0 else float("inf")
    ok = ratio <= max_median_ratio
    return CheckResult(
        id="G3-F",
        name="rapport surface max/mediane sous plafond",
        passed=ok,
        detail=f"max={mx:.1f} median={med:.1f} ratio={ratio:.3f} ceil={max_median_ratio}",
    )


def g3g_compactness_floor(
    cells: Sequence[dict],
    *,
    floor: float,
    singleton_ids: Sequence[int] | None = None,
) -> CheckResult:
    """G3-G : compacité Polsby-Popper minimale au-dessus du plancher (anti-lanières)."""
    exempt = set(int(i) for i in (singleton_ids or []))
    bad: List[str] = []
    comps = []
    for c in cells:
        cid = int(c["cell_id"])
        if cid in exempt:
            continue
        comp = float(c["compactness_polsby_popper"])
        comps.append(comp)
        if comp < floor:
            bad.append(f"cell_{cid}:pp={comp:.3f}<{floor}")
            if len(bad) >= 8:
                break
    cmin = min(comps) if comps else 0.0
    return CheckResult(
        id="G3-G",
        name="compacite Polsby-Popper minimale au-dessus du plancher",
        passed=len(bad) == 0,
        detail=(
            "; ".join(bad)
            if bad
            else f"min={cmin:.3f} floor={floor} checked={len(comps)}"
        ),
    )


def g3h_no_retired_reissued(registry: Sequence[dict]) -> CheckResult:
    """G3-H : aucun identifiant retiré réémis comme actif."""
    retired = {
        int(r["cell_id"]) for r in registry if r.get("retired") is not None
    }
    active = {
        int(r["cell_id"]) for r in registry if r.get("retired") is None
    }
    overlap = sorted(retired & active)
    return CheckResult(
        id="G3-H",
        name="aucun identifiant retire reemis",
        passed=len(overlap) == 0,
        detail=(
            f"overlap={overlap}"
            if overlap
            else f"retired={len(retired)} active={len(active)}"
        ),
    )


def q2_no_holes_eps(
    cells: Sequence[dict], land_geom: Any, area_eps: float
) -> CheckResult:
    """Q2 avec ε configurable (G3 géométrie NE réelle)."""
    geoms = _cell_geoms(cells)
    if not geoms:
        return CheckResult("Q2", "absence de trous", False, "aucune cellule")
    union = unary_union(geoms)
    missing = land_geom.difference(union).area
    extra = union.difference(land_geom).area
    ok = missing <= area_eps and extra <= area_eps
    return CheckResult(
        id="Q2",
        name="absence de trous",
        passed=ok,
        detail=f"missing_m2={missing:.3f} extra_m2={extra:.3f} eps={area_eps}",
    )


def q3_no_overlaps_eps(cells: Sequence[dict], overlap_eps: float) -> CheckResult:
    geoms = _cell_geoms(cells)
    if len(geoms) < 2:
        return CheckResult("Q3", "absence de chevauchements", True, "moins de 2 cellules")
    sum_areas = sum(g.area for g in geoms)
    union_area = unary_union(geoms).area
    overlap = sum_areas - union_area
    ok = overlap <= overlap_eps
    return CheckResult(
        id="Q3",
        name="absence de chevauchements",
        passed=ok,
        detail=f"overlap_m2={overlap:.3f} eps={overlap_eps}",
    )


def run_g3_green(
    *,
    cells: Sequence[dict],
    land_geom: Any,
    land_parts: Sequence[Any],
    adjacency: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
    registry_a: Sequence[dict],
    registry_b: Sequence[dict],
    area_eps: float,
    overlap_eps: float,
    seed_min: int,
    seed_max: int,
    area_floor_km2: float = 200.0,
    area_ceil_km2: float = 15_000.0,
    max_median_ratio: float = 8.0,
    compactness_min: float = 0.18,
    singleton_ids: Sequence[int] | None = None,
) -> List[CheckResult]:
    # Registry fusionné (run2) pour G3-H.
    registry_merged = list(registry_b) if registry_b else list(registry_a)
    return [
        q1_polygon_validity(cells, land_geom),
        q2_no_holes_eps(cells, land_geom, area_eps),
        q3_no_overlaps_eps(cells, overlap_eps),
        q4_no_isolated(cells, adjacency),
        q10_determinism(sha_pairs),
        g3a_no_sea_in_cells(cells, land_geom, area_eps),
        g3b_all_land_masses_covered(cells, land_parts, area_eps),
        g3c_stable_ids(registry_a, registry_b),
        g3d_cell_count_in_range(len(cells), seed_min, seed_max),
        g3e_area_within_bounds(
            cells,
            floor_km2=area_floor_km2,
            ceil_km2=area_ceil_km2,
            singleton_ids=singleton_ids,
        ),
        g3f_area_dispersion(cells, max_median_ratio=max_median_ratio),
        g3g_compactness_floor(
            cells, floor=compactness_min, singleton_ids=singleton_ids
        ),
        g3h_no_retired_reissued(registry_merged),
    ]


# ---------------------------------------------------------------------------
# G4 — adjacence typée / zones maritimes / Zuiderzee
# ---------------------------------------------------------------------------


def g4a_littorality_derived(
    coastal_ids: Sequence[int], adjacency: Sequence[dict], sea_ids: Sequence[int]
) -> CheckResult:
    """G4-A : littoralité = touche une zone maritime (dérivée, jamais saisie)."""
    sea = set(int(x) for x in sea_ids)
    from_edges = sorted(
        {
            (e["a"] if e["a"] not in sea else e["b"])
            for e in adjacency
            if e["kind"] == "land-sea"
        }
    )
    coastal = sorted(int(x) for x in coastal_ids)
    ok = coastal == from_edges
    return CheckResult(
        id="G4-A",
        name="littoralite derivee coherente avec adjacence maritime",
        passed=ok,
        detail=(
            f"coastal={len(coastal)} from_edges={len(from_edges)}"
            if ok
            else f"mismatch coastal={coastal[:5]}... edges={from_edges[:5]}..."
        ),
    )


def g4b_open_sea_reachable(
    unreachable_enclosed: Sequence[int],
) -> CheckResult:
    """G4-B : toute masse open_sea / bassin enclosed atteignable depuis la mer extérieure."""
    bad = sorted(int(x) for x in unreachable_enclosed)
    return CheckResult(
        id="G4-B",
        name="toute masse open_sea atteignable depuis la mer exterieure",
        passed=len(bad) == 0,
        detail=f"unreachable={bad}" if bad else "all_enclosed_reachable",
    )


def g4c_sea_covers_without_holes(
    sea_zones: Sequence[dict], sea_geom: Any, area_eps: float = 10_000.0
) -> CheckResult:
    """G4-C : zones maritimes couvrent la mer sans trou ni chevauchement."""
    from shapely.geometry import shape as _shape
    from shapely.ops import unary_union as _union

    geoms = [_shape(z["geometry"]) for z in sea_zones]
    if not geoms:
        return CheckResult("G4-C", "zones maritimes couvrent la mer", False, "aucune zone")
    union = _union(geoms)
    missing = sea_geom.difference(union).area
    extra = union.difference(sea_geom).area
    sum_a = sum(g.area for g in geoms)
    overlap = sum_a - union.area
    ok = missing <= area_eps and extra <= area_eps and overlap <= area_eps
    return CheckResult(
        id="G4-C",
        name="zones maritimes couvrent la mer sans trou ni chevauchement",
        passed=ok,
        detail=(
            f"missing_m2={missing:.1f} extra_m2={extra:.1f} overlap_m2={overlap:.1f}"
        ),
    )


def g4d_sea_ids_no_collision(
    land_ids: Sequence[int], sea_ids: Sequence[int], sea_id_base: int = 5000
) -> CheckResult:
    """G4-D : identifiants maritimes hors plage terrestre, base ≥ 5000."""
    land = set(int(x) for x in land_ids)
    sea = sorted(int(x) for x in sea_ids)
    collision = sorted(land & set(sea))
    below = [s for s in sea if s < sea_id_base]
    ok = len(collision) == 0 and len(below) == 0
    return CheckResult(
        id="G4-D",
        name="identifiants maritimes sans collision avec les terres",
        passed=ok,
        detail=(
            f"collision={collision} below_base={below}"
            if not ok
            else f"sea_ids={sea[0]}..{sea[-1]}" if sea else "no_sea"
        ),
    )


def q7_adjacency_contiguous_typed(
    land_cells: Sequence[dict],
    sea_zones: Sequence[dict],
    adjacency: Sequence[dict],
) -> CheckResult:
    """Q7 G4 : pas d'adjacence entre non-contigus, hors détroit et lien déclaré."""
    from shapely.geometry import shape as _shape

    by_id = {int(c["cell_id"]): _shape(c["geometry"]) for c in land_cells}
    by_id.update({int(z["zone_id"]): _shape(z["geometry"]) for z in sea_zones})
    bad: List[str] = []
    for edge in adjacency:
        a, b = int(edge["a"]), int(edge["b"])
        kind = edge["kind"]
        if edge.get("declared_topology_link"):
            continue
        if kind == "strait":
            # Détroit : terres non contigues, gap ≤ seuil.
            if a not in by_id or b not in by_id:
                bad.append(f"strait_unknown:{a}-{b}")
                continue
            gap = by_id[a].distance(by_id[b])
            if gap < LENGTH_EPS:
                bad.append(f"strait_but_contiguous:{a}-{b}")
            continue
        if a not in by_id or b not in by_id:
            bad.append(f"unknown:{a}-{b}")
            continue
        inter = by_id[a].boundary.intersection(by_id[b].boundary)
        length = 0.0 if inter.is_empty else inter.length
        if length < LENGTH_EPS:
            bad.append(f"non_contiguous:{kind}:{a}-{b}:len={length:.3f}")
    return CheckResult(
        id="Q7",
        name="aucune adjacence entre entites non contigues hors detroit declare",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else "ok",
    )


def q4_no_isolated_entities(
    land_cells: Sequence[dict],
    sea_zones: Sequence[dict],
    adjacency: Sequence[dict],
) -> CheckResult:
    """Q4 G4 : aucune cellule ni zone isolée."""
    ids = {int(c["cell_id"]) for c in land_cells} | {
        int(z["zone_id"]) for z in sea_zones
    }
    connected: set = set()
    for e in adjacency:
        connected.add(int(e["a"]))
        connected.add(int(e["b"]))
    isolated = sorted(ids - connected)
    return CheckResult(
        id="Q4",
        name="aucune cellule ni zone isolee",
        passed=len(isolated) == 0,
        detail=f"isolated={isolated[:20]}" if isolated else "aucune isolee",
    )


def run_g4_green(
    *,
    land_cells: Sequence[dict],
    sea_zones: Sequence[dict],
    sea_geom: Any,
    adjacency: Sequence[dict],
    coastal_ids: Sequence[int],
    unreachable_enclosed: Sequence[int],
    sha_pairs: Dict[str, List[str]],
    area_eps: float = 10_000.0,
) -> List[CheckResult]:
    land_ids = [int(c["cell_id"]) for c in land_cells]
    sea_ids = [int(z["zone_id"]) for z in sea_zones]
    return [
        q1_polygon_validity(
            [{"cell_id": z["zone_id"], "geometry": z["geometry"]} for z in sea_zones],
            sea_geom,
        ),
        q4_no_isolated_entities(land_cells, sea_zones, adjacency),
        q7_adjacency_contiguous_typed(land_cells, sea_zones, adjacency),
        q10_determinism(sha_pairs),
        g4a_littorality_derived(coastal_ids, adjacency, sea_ids),
        g4b_open_sea_reachable(unreachable_enclosed),
        g4c_sea_covers_without_holes(sea_zones, sea_geom, area_eps),
        g4d_sea_ids_no_collision(land_ids, sea_ids),
    ]


# ---------------------------------------------------------------------------
# G5 — fleuves
# ---------------------------------------------------------------------------


def q1_river_geometry_validity(segments: Sequence[dict]) -> CheckResult:
    """Q1 G5 : géométries fluviales valides (LineString / MultiLineString)."""
    from shapely.validation import explain_validity

    bad: List[str] = []
    for seg in segments:
        g = shape(seg["geometry"])
        if g is None or g.is_empty:
            bad.append(f"{seg.get('segment_id')}:empty")
            continue
        if not g.is_valid:
            bad.append(f"{seg.get('segment_id')}:{explain_validity(g)}")
        if g.geom_type not in ("LineString", "MultiLineString"):
            bad.append(f"{seg.get('segment_id')}:type={g.geom_type}")
    return CheckResult(
        id="Q1",
        name="validite des geometries fluviales",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok n={len(segments)}",
    )


def g5a_attachments_match_geometry(
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    cells_xy: Sequence[Tuple[int, Any]],
    eps: float = 1.0,
) -> CheckResult:
    """G5-A : tout tronçon rattaché tombe bien dans les cellules déclarées."""
    by_id = dict(cells_xy)
    bad: List[str] = []
    for seg in segments:
        sid = seg["segment_id"]
        line = shape(seg["geometry"])
        for cid in attachments.get(sid, []):
            if cid not in by_id:
                bad.append(f"{sid}:unknown_cell_{cid}")
                continue
            inter = by_id[cid].intersection(line)
            length = getattr(inter, "length", 0.0) if not inter.is_empty else 0.0
            if length < eps and not (
                not inter.is_empty
                and by_id[cid].buffer(eps).intersection(line).length >= eps
            ):
                bad.append(f"{sid}:cell_{cid}_no_intersection")
    return CheckResult(
        id="G5-A",
        name="troncons rattaches dans les cellules declarees",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else "ok",
    )


def g5b_no_river_in_open_sea(
    segments: Sequence[dict],
    land_xy: Any,
    sea_xy: Any,
    sea_only_fraction: float = 0.95,
) -> CheckResult:
    """G5-B : aucun fleuve entièrement (ou quasi) en pleine mer."""
    bad: List[str] = []
    for seg in segments:
        line = shape(seg["geometry"])
        total = float(line.length) if line.length else 0.0
        if total <= 0:
            continue
        in_sea = line.intersection(sea_xy)
        sea_len = float(in_sea.length) if not in_sea.is_empty else 0.0
        in_land = line.intersection(land_xy)
        land_len = float(in_land.length) if not in_land.is_empty else 0.0
        # Lake centerlines peuvent être dans un lac (hors land) — pas « pleine mer ».
        if seg.get("featurecla") == "Lake Centerline":
            continue
        if sea_len / total >= sea_only_fraction and land_len / total < (1.0 - sea_only_fraction):
            bad.append(
                f"{seg.get('segment_id')}:{seg.get('name')}"
                f":sea_frac={sea_len / total:.3f}"
            )
    return CheckResult(
        id="G5-B",
        name="aucun fleuve en pleine mer",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else "ok",
    )


def g5c_artery_has_navigable_river(
    adjacency: Sequence[dict],
    segments: Sequence[dict],
) -> CheckResult:
    """G5-C : toute arête voie fluviale est bien traversée par un fleuve navigable."""
    nav_ids = {
        s["segment_id"]
        for s in segments
        if s.get("navigability") == "navigable"
    }
    bad: List[str] = []
    for e in adjacency:
        if not e.get("fluvial_artery"):
            continue
        rivers = e.get("artery_rivers") or []
        if not rivers:
            bad.append(f"{e['a']}-{e['b']}:no_artery_rivers")
            continue
        if not any(r.get("segment_id") in nav_ids for r in rivers):
            bad.append(f"{e['a']}-{e['b']}:no_navigable")
        if not any(r.get("navigability") == "navigable" for r in rivers):
            bad.append(f"{e['a']}-{e['b']}:nav_flag_mismatch")
    return CheckResult(
        id="G5-C",
        name="voie fluviale implique fleuve navigable",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else "ok",
    )


def g5d_mouth_on_adjacent_sea(
    mouths: Sequence[dict],
) -> CheckResult:
    """G5-D : toute embouchure débouche sur une zone maritime adjacente aux cellules du fleuve."""
    bad: List[str] = []
    for m in mouths:
        if not m.get("sea_zone_adjacent_to_river_cells"):
            bad.append(
                f"{m.get('segment_id')}:{m.get('name')}:zone_{m.get('sea_zone_id')}"
            )
    return CheckResult(
        id="G5-D",
        name="embouchure sur zone maritime adjacente",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok n={len(mouths)}",
    )


def run_g5_green(
    *,
    segments: Sequence[dict],
    attachments: Dict[str, List[int]],
    cells_xy: Sequence[Tuple[int, Any]],
    land_xy: Any,
    sea_xy: Any,
    adjacency: Sequence[dict],
    mouths: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        q1_river_geometry_validity(segments),
        q10_determinism(sha_pairs),
        g5a_attachments_match_geometry(segments, attachments, cells_xy),
        g5b_no_river_in_open_sea(segments, land_xy, sea_xy),
        g5c_artery_has_navigable_river(adjacency, segments),
        g5d_mouth_on_adjacent_sea(mouths),
    ]


# ---------------------------------------------------------------------------
# G5-bis — surcharges de navigabilité déclaratives (v1_058)
# ---------------------------------------------------------------------------

G5B_VALID_CERTAINTY = (
    "attested",
    "reconstructed",
    "reconstructed_established",
    "gameplay",
)

# Champs qui encoderait une limite amont — INTERDITS dans les artefacts G5b.
G5B_FORBIDDEN_UPSTREAM_KEYS = frozenset(
    {
        "navigable_upstream_km",
        "navigation_reach",
        "navigation_reach_km",
        "upstream_limit",
        "upstream_limit_km",
        "navigable_to_km",
        "reach_km",
        "navigation_range",
        "portee_navigation",
        "limite_amont",
        "limite_amont_km",
        "navigable_extent_km",
    }
)


def g5b_a_overrides_have_certainty_and_provenance(
    overrides: Sequence[dict],
    valid_levels: Sequence[str] = G5B_VALID_CERTAINTY,
) -> CheckResult:
    """G5b-A : toute surcharge porte certitude valide + provenance non vide."""
    levels = set(valid_levels)
    bad: List[str] = []
    for corr in overrides:
        cid = str(corr.get("id") or corr.get("river_name") or "?")
        certainty = corr.get("certainty")
        provenance = corr.get("provenance")
        if provenance is None or str(provenance).strip() == "":
            provenance = corr.get("source")
        if certainty not in levels:
            bad.append(f"{cid}:certainty={certainty!r}")
        if provenance is None or str(provenance).strip() == "":
            bad.append(f"{cid}:provenance_empty")
    return CheckResult(
        id="G5b-A",
        name="surcharges : certitude valide et provenance non vide",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"n={len(list(overrides))}_ok",
    )


def g5b_b_reversibility(
    sha_disabled: Dict[str, str],
    sha_reference_v051: Dict[str, str],
) -> CheckResult:
    """G5b-B : surcharge OFF ⇒ SHA256 identiques aux sorties v1_051 / G5."""
    keys = sorted(set(sha_disabled.keys()) & set(sha_reference_v051.keys()))
    if not keys:
        return CheckResult(
            id="G5b-B",
            name="revesibilite surcharge OFF = sorties v1_051",
            passed=False,
            detail="aucun chemin commun a comparer",
        )
    mismatches = [
        k for k in keys if sha_disabled.get(k) != sha_reference_v051.get(k)
    ]
    return CheckResult(
        id="G5b-B",
        name="revesibilite surcharge OFF = sorties v1_051",
        passed=len(mismatches) == 0,
        detail=(
            f"mismatch={mismatches}"
            if mismatches
            else f"match_on={len(keys)}_files"
        ),
    )


def g5b_c_no_override_for_absent_river(
    overrides: Sequence[dict],
    river_labels_in_window: Sequence[str],
    *,
    applied_absent: Sequence[str] | None = None,
) -> CheckResult:
    """G5b-C : aucune surcharge ne s'applique à un fleuve absent de la fenêtre.

    `applied_absent` : fleuves auxquels une surcharge a été APPLIQUÉE alors
    qu'ils sont absents (doit rester vide). Les corrections déclarées pour un
    fleuve absent doivent être signalées (reported) sans application silencieuse.
    """
    present = {str(x) for x in river_labels_in_window}
    declared_absent = sorted(
        {
            str(c.get("river_name") or "")
            for c in overrides
            if c.get("river_name") and str(c.get("river_name")) not in present
        }
    )
    wrongly_applied = list(applied_absent or [])
    # Vert si aucune application sur absent. Déclarer un absent sans l'appliquer = OK
    # (on le dit) ; l'appliquer en silence = ROUGE.
    ok = len(wrongly_applied) == 0
    return CheckResult(
        id="G5b-C",
        name="aucune surcharge appliquee a un fleuve absent",
        passed=ok,
        detail=(
            f"applied_absent={wrongly_applied}; declared_absent={declared_absent}"
        ),
    )


def g5b_d_no_upstream_limit_encoded(artifacts: Sequence[dict]) -> CheckResult:
    """G5b-D : aucune donnée de portée / limite amont dans les artefacts."""
    bad: List[str] = []

    def walk(obj: Any, path: str = "") -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                key = str(k)
                here = f"{path}.{key}" if path else key
                if key in G5B_FORBIDDEN_UPSTREAM_KEYS:
                    bad.append(here)
                walk(v, here)
        elif isinstance(obj, list):
            for i, item in enumerate(obj[:50]):
                walk(item, f"{path}[{i}]")

    for doc in artifacts:
        walk(doc)
    return CheckResult(
        id="G5b-D",
        name="limite amont non encodee dans les artefacts",
        passed=len(bad) == 0,
        detail="; ".join(bad[:12]) if bad else "ok_no_upstream_fields",
    )


def run_g5b_green(
    *,
    overrides: Sequence[dict],
    river_labels_in_window: Sequence[str],
    applied_absent: Sequence[str],
    artifacts: Sequence[dict],
    sha_disabled: Dict[str, str],
    sha_reference_v051: Dict[str, str],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        g5b_a_overrides_have_certainty_and_provenance(overrides),
        g5b_b_reversibility(sha_disabled, sha_reference_v051),
        g5b_c_no_override_for_absent_river(
            overrides, river_labels_in_window, applied_absent=applied_absent
        ),
        g5b_d_no_upstream_limit_encoded(artifacts),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G5-ter — fusion Europe (v1_067) — ids G5c-A..D (≠ G5-C artère)
# ---------------------------------------------------------------------------


def g5cter_a_no_duplicate_named_across_layers(
    segments: Sequence[dict],
    adjacency: Sequence[dict],
) -> CheckResult:
    """G5c-A : aucun fleuve nommé présent dans deux couches ; pas d'artère doublée."""
    import unicodedata

    def _norm(label: str) -> str:
        s = unicodedata.normalize("NFKD", str(label or ""))
        s = "".join(c for c in s if not unicodedata.combining(c))
        return " ".join(s.lower().split())

    by_key: Dict[str, set] = {}
    for s in segments:
        if s.get("featurecla") not in ("River", "Canal"):
            continue
        key = _norm(s.get("major_label") or s.get("name") or "")
        if not key:
            continue
        by_key.setdefault(key, set()).add(str(s.get("source_layer") or "?"))
    dup_layers = sorted(k for k, layers in by_key.items() if len(layers) > 1)

    artery_bad: List[str] = []
    for e in adjacency:
        rivers = e.get("artery_rivers") or []
        # Doublon pathologique : même nom + deux source_layer via segment_id préfixe.
        by_name: Dict[str, set] = {}
        for r in rivers:
            n = _norm(r.get("name") or "")
            if not n:
                continue
            sid = str(r.get("segment_id") or "")
            layer = "europe" if sid.startswith("riv_eu_") else "world"
            by_name.setdefault(n, set()).add(layer)
        for n, layers in by_name.items():
            if len(layers) > 1:
                artery_bad.append(f"{e.get('a')}-{e.get('b')}:{n}")

    ok = len(dup_layers) == 0 and len(artery_bad) == 0
    return CheckResult(
        id="G5c-A",
        name="aucun fleuve nomme en double dans le reseau fusionne",
        passed=ok,
        detail=(
            f"dup_layers={dup_layers}; artery_cross_layer={artery_bad}"
            if not ok
            else f"ok named_keys={len(by_key)}"
        ),
    )


def g5cter_b_overrides_cover_all_named_segments(
    segments: Sequence[dict],
    overrides: Sequence[dict],
) -> CheckResult:
    """G5c-B : chaque surcharge s'applique à TOUS les tronçons du fleuve nommé."""
    by_label: Dict[str, List[dict]] = {}
    for s in segments:
        lab = s.get("major_label")
        if lab:
            by_label.setdefault(str(lab), []).append(s)

    bad: List[str] = []
    for corr in overrides:
        name = str(corr.get("river_name") or "")
        imposed = str(corr.get("imposed_navigability") or "")
        segs = by_label.get(name, [])
        if not segs:
            continue
        missing = [
            s["segment_id"]
            for s in segs
            if s.get("navigability") != imposed
        ]
        if missing:
            bad.append(f"{name}:uncovered={missing}")
    return CheckResult(
        id="G5c-B",
        name="surcharges v1_058 appliquees a tous les troncons du fleuve",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"ok overrides={len(list(overrides))}",
    )


def g5cter_c_immutable_artifacts(
    sha_before: Dict[str, str],
    sha_after: Dict[str, str],
) -> CheckResult:
    """G5c-C : maille, relief, villes, possession inchangés (empreintes)."""
    keys = sorted(set(sha_before) & set(sha_after))
    required = (
        "artifacts/cells_g3.json",
        "artifacts/cells_relief_g6.json",
        "artifacts/cities_g7.json",
        "artifacts/ownership_1400.json",
    )
    missing_req = [k for k in required if k not in keys]
    mismatches = [k for k in keys if sha_before.get(k) != sha_after.get(k)]
    ok = len(missing_req) == 0 and len(mismatches) == 0
    return CheckResult(
        id="G5c-C",
        name="maille relief villes possession inchanges (empreinte)",
        passed=ok,
        detail=(
            f"missing={missing_req}; mismatch={mismatches}"
            if not ok
            else f"match_on={len(keys)}_files"
        ),
    )


def g5cter_d_reversibility_to_g5b(
    sha_disabled: Dict[str, str],
    sha_reference_g5b: Dict[str, str],
) -> CheckResult:
    """G5c-D : drapeau Europe OFF ⇒ sorties v1_058 bit pour bit."""
    keys = sorted(set(sha_disabled.keys()) & set(sha_reference_g5b.keys()))
    # Cœur fluvial G5b (pas captures matplotlib ni divergences éventuellement enrichies).
    core = [
        k
        for k in keys
        if k.startswith("artifacts/")
        and "g5b" in k
        and not k.startswith("capture/")
    ]
    if not core:
        core = keys
    mismatches = [
        k for k in core if sha_disabled.get(k) != sha_reference_g5b.get(k)
    ]
    return CheckResult(
        id="G5c-D",
        name="revesibilite europe OFF = sorties v1_058",
        passed=len(mismatches) == 0 and len(core) > 0,
        detail=(
            f"mismatch={mismatches}"
            if mismatches
            else f"match_on={len(core)}_files"
        ),
    )


def run_g5cter_green(
    *,
    segments: Sequence[dict],
    adjacency: Sequence[dict],
    overrides: Sequence[dict],
    sha_immutable_before: Dict[str, str],
    sha_immutable_after: Dict[str, str],
    sha_disabled: Dict[str, str],
    sha_reference_g5b: Dict[str, str],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        g5cter_a_no_duplicate_named_across_layers(segments, adjacency),
        g5cter_b_overrides_cover_all_named_segments(segments, overrides),
        g5cter_c_immutable_artifacts(sha_immutable_before, sha_immutable_after),
        g5cter_d_reversibility_to_g5b(sha_disabled, sha_reference_g5b),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G6 — relief
# ---------------------------------------------------------------------------


def g6a_dem_fingerprint_verified(dem_ok: bool, detail: str = "") -> CheckResult:
    """G6-A : empreinte collective DEM vérifiée avant lecture."""
    return CheckResult(
        id="G6-A",
        name="empreinte collective DEM verifiee avant lecture",
        passed=bool(dem_ok),
        detail=detail or ("ok" if dem_ok else "fingerprint_mismatch"),
    )


def g6b_all_cells_sampled(cell_relief: Sequence[dict]) -> CheckResult:
    """G6-B : chaque cellule terrestre a une altitude échantillonnée."""
    bad: List[str] = []
    for c in cell_relief:
        n = int(c.get("sample_count") or 0)
        if n <= 0:
            bad.append(f"{c.get('cell_id')}:sample_count=0")
        if c.get("elev_mean_m") is None:
            bad.append(f"{c.get('cell_id')}:elev_mean_missing")
    return CheckResult(
        id="G6-B",
        name="chaque cellule terrestre a une altitude echantillonnee",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok n={len(cell_relief)}",
    )


def g6c_elevations_plausible(
    cell_relief: Sequence[dict],
    elev_min: float = G6_ELEV_PLAUSIBLE_MIN_M,
    elev_max: float = G6_ELEV_PLAUSIBLE_MAX_M,
) -> CheckResult:
    """G6-C : altitudes dans une plage plausible pour la fenêtre."""
    bad: List[str] = []
    for c in cell_relief:
        for key in ("elev_mean_m", "elev_min_m", "elev_max_m"):
            v = c.get(key)
            if v is None:
                bad.append(f"{c.get('cell_id')}:{key}=None")
                continue
            if float(v) < elev_min or float(v) > elev_max:
                bad.append(f"{c.get('cell_id')}:{key}={v}")
    return CheckResult(
        id="G6-C",
        name="altitudes dans une plage plausible pour la fenetre",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok range=[{elev_min},{elev_max}]",
    )


def g6d_barrier_above_both_cells(adjacency: Sequence[dict], cell_relief: Sequence[dict]) -> CheckResult:
    """G6-D : toute arête barrière a un franchissement > centroïde des deux cellules."""
    by_id = {int(c["cell_id"]): c for c in cell_relief}
    bad: List[str] = []
    for e in adjacency:
        if not e.get("relief_barrier"):
            continue
        a, b = int(e["a"]), int(e["b"])
        crossing = e.get("crossing_elev_m")
        if crossing is None:
            bad.append(f"{a}-{b}:no_crossing")
            continue
        if a not in by_id or b not in by_id:
            bad.append(f"{a}-{b}:unknown_cell")
            continue
        # Centroïde (PLAN) ; repli sur elev_mean si absent.
        ca = by_id[a].get("centroid_elev_m", by_id[a]["elev_mean_m"])
        cb = by_id[b].get("centroid_elev_m", by_id[b]["elev_mean_m"])
        if not (float(crossing) > float(ca) and float(crossing) > float(cb)):
            bad.append(f"{a}-{b}:crossing={crossing} centroids={ca}/{cb}")
    return CheckResult(
        id="G6-D",
        name="barriere implique franchissement au-dessus des deux cellules",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else "ok",
    )


def g6e_mesh_unchanged(
    base_cell_ids: Sequence[int], relief_cell_ids: Sequence[int],
) -> CheckResult:
    """G6-E : maille inchangée — mêmes cellules / identifiants que G3."""
    base = sorted(int(x) for x in base_cell_ids)
    relief = sorted(int(x) for x in relief_cell_ids)
    expected_count = len(base)
    ok = base == relief and expected_count > 0
    return CheckResult(
        id="G6-E",
        name=f"maille inchangee {expected_count} cellules memes identifiants",
        passed=ok,
        detail=(
            f"base={len(base)} relief={len(relief)} "
            f"equal={base == relief} expected={expected_count}"
        ),
    )


def run_g6_green(
    *,
    dem_ok: bool,
    dem_detail: str,
    cell_relief: Sequence[dict],
    adjacency: Sequence[dict],
    base_cell_ids: Sequence[int],
    sha_pairs: Dict[str, List[str]],
    elev_min: float = G6_ELEV_PLAUSIBLE_MIN_M,
    elev_max: float = G6_ELEV_PLAUSIBLE_MAX_M,
) -> List[CheckResult]:
    relief_ids = [int(c["cell_id"]) for c in cell_relief]
    return [
        q10_determinism(sha_pairs),
        g6a_dem_fingerprint_verified(dem_ok, dem_detail),
        g6b_all_cells_sampled(cell_relief),
        g6c_elevations_plausible(cell_relief, elev_min, elev_max),
        g6d_barrier_above_both_cells(adjacency, cell_relief),
        g6e_mesh_unchanged(base_cell_ids, relief_ids),
    ]


# ---------------------------------------------------------------------------
# G7 — comparaisons legacy bornées (v1_057)
# ---------------------------------------------------------------------------


def g7a_no_out_of_window_in_compare(
    retained_ids: Sequence[int],
    compared_province_ids: Sequence[int],
) -> CheckResult:
    """G7-A : aucune province hors fenêtre n'entre dans une comparaison."""
    retained = {int(x) for x in retained_ids}
    bad = sorted({int(x) for x in compared_province_ids} - retained)
    return CheckResult(
        id="G7-A",
        name="aucune province hors fenetre n entre dans une comparaison",
        passed=len(bad) == 0,
        detail=("ok" if not bad else f"out_of_window_in_compare={bad}"),
    )


def g7b_unassigned_counted_never_forced(
    unassigned_ids: Sequence[int],
    cell_to_province: Dict[int, int],
    published_unassigned_count: int,
) -> CheckResult:
    """G7-B : toute cellule non attribuée est comptée et publiée, jamais forcée."""
    unassigned = {int(x) for x in unassigned_ids}
    forced = sorted(cid for cid in unassigned if cid in cell_to_province)
    count_ok = int(published_unassigned_count) == len(unassigned)
    ok = count_ok and len(forced) == 0
    detail_parts = []
    if not count_ok:
        detail_parts.append(
            f"count_mismatch published={published_unassigned_count} "
            f"list={len(unassigned)}"
        )
    if forced:
        detail_parts.append(f"forced_attach={forced[:8]}")
    return CheckResult(
        id="G7-B",
        name="cellule non attribuee comptee et jamais rattachee d office",
        passed=ok,
        detail="; ".join(detail_parts) if detail_parts else f"ok n={len(unassigned)}",
    )


def g7c_distance_bound_effective(
    assignments: Sequence[dict],
    bound_m: float,
) -> CheckResult:
    """G7-C : la borne de distance est effective — au-delà, refus."""
    bad: List[str] = []
    for row in assignments:
        d = float(row.get("distance_m", -1.0))
        if d > bound_m:
            bad.append(
                f"cell_{row.get('cell_id')}:d={d:.1f}>bound={bound_m:.1f}"
            )
    return CheckResult(
        id="G7-C",
        name="borne de distance effective — appariement au-dela refuse",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok bound_m={bound_m}",
    )


def run_g7_green(
    *,
    retained_ids: Sequence[int],
    compared_province_ids: Sequence[int],
    unassigned_ids: Sequence[int],
    cell_to_province: Dict[int, int],
    published_unassigned_count: int,
    assigned_rows: Sequence[dict],
    bound_m: float,
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        q10_determinism(sha_pairs),
        g7a_no_out_of_window_in_compare(retained_ids, compared_province_ids),
        g7b_unassigned_counted_never_forced(
            unassigned_ids, cell_to_province, published_unassigned_count
        ),
        g7c_distance_bound_effective(assigned_rows, bound_m),
    ]


# ---------------------------------------------------------------------------
# G7 — villes historiques par contenance (v1_059)
# (IDs Q5 / G7-A / G7-B / G7-C / Q10 — sémantique villes, distincte de
#  compare_legacy v1_057 ci-dessus ; preuves séparées.)
# ---------------------------------------------------------------------------


def g7cities_q5_attached_in_exactly_one_land_cell(
    attached: Sequence[dict], cells: Sequence[dict]
) -> CheckResult:
    """Q5 : chaque ville rattachée est dans exactement une cellule terrestre."""
    return q5_cities_in_exactly_one_land_cell(attached, cells)


def g7cities_a_containment_only(
    attached: Sequence[dict],
    defects: Sequence[dict],
    cells: Sequence[dict],
    attachment_method: str,
) -> CheckResult:
    """G7-A : aucune ville n'est rattachée par proximité, seulement par contenance."""
    problems: List[str] = []
    if attachment_method != "containment":
        problems.append(f"method={attachment_method}")
    cell_geoms = {int(c["cell_id"]): shape(c["geometry"]) for c in cells}
    for city in attached:
        cid = city.get("cell_id")
        if cid is None:
            problems.append(f"{city.get('name')}:attached_without_cell")
            continue
        g = cell_geoms.get(int(cid))
        if g is None:
            problems.append(f"{city.get('name')}:unknown_cell_{cid}")
            continue
        x = float(city.get("x_m", city.get("x", 0)))
        y = float(city.get("y_m", city.get("y", 0)))
        pt = Point(x, y)
        if not (g.contains(pt) or g.touches(pt)):
            problems.append(f"{city.get('name')}:proximity_attach_cell_{cid}")
        method = city.get("attachment_method")
        if method not in (None, "containment"):
            problems.append(f"{city.get('name')}:method={method}")
    for city in defects:
        if city.get("cell_id") is not None:
            problems.append(
                f"{city.get('name')}:defect_forced_attach_cell_{city.get('cell_id')}"
            )
    return CheckResult(
        id="G7-A",
        name="aucune ville rattachee par proximite — contenance seule",
        passed=len(problems) == 0,
        detail="; ".join(problems[:12]) if problems else "ok containment",
    )


def g7cities_b_three_categories_sum(
    counts: Dict[str, int],
    attached: Sequence[dict],
    outside: Sequence[dict],
    defects: Sequence[dict],
) -> CheckResult:
    """G7-B : trois catégories disjointes, somme = total."""
    problems: List[str] = []
    a, o, d = len(attached), len(outside), len(defects)
    total = int(counts.get("total", -1))
    if int(counts.get("attached", -1)) != a:
        problems.append(f"attached_count {counts.get('attached')}!={a}")
    if int(counts.get("outside_pilot_window", -1)) != o:
        problems.append(f"outside_count {counts.get('outside_pilot_window')}!={o}")
    if int(counts.get("defect", -1)) != d:
        problems.append(f"defect_count {counts.get('defect')}!={d}")
    if a + o + d != total:
        problems.append(f"sum {a}+{o}+{d}!={total}")
    names_a = {c["name"] for c in attached}
    names_o = {c["name"] for c in outside}
    names_d = {c["name"] for c in defects}
    overlap = (names_a & names_o) | (names_a & names_d) | (names_o & names_d)
    if overlap:
        problems.append(f"overlap={sorted(overlap)[:8]}")
    return CheckResult(
        id="G7-B",
        name="trois categories separees — somme egale total villes",
        passed=len(problems) == 0,
        detail=(
            "; ".join(problems)
            if problems
            else f"ok attached={a} outside={o} defect={d} total={total}"
        ),
    )


def g7cities_c_defect_coords_unchanged(
    defects: Sequence[dict],
    source_coords: Dict[str, Dict[str, float]],
) -> CheckResult:
    """G7-C : aucune ville en défaut n'a été déplacée — coords = source."""
    problems: List[str] = []
    for city in defects:
        name = city["name"]
        src = source_coords.get(name)
        if src is None:
            problems.append(f"{name}:missing_source")
            continue
        lon = float(city.get("lon_source", city.get("lon")))
        lat = float(city.get("lat_source", city.get("lat")))
        if abs(lon - float(src["lon"])) > 1e-9 or abs(lat - float(src["lat"])) > 1e-9:
            problems.append(
                f"{name}:moved src=({src['lon']},{src['lat']}) "
                f"pub=({lon},{lat})"
            )
        if city.get("cell_id") is not None:
            problems.append(f"{name}:relocated_to_cell_{city.get('cell_id')}")
    return CheckResult(
        id="G7-C",
        name="ville en defaut non deplacee — coords identiques a la source",
        passed=len(problems) == 0,
        detail="; ".join(problems[:12]) if problems else f"ok n_defect={len(defects)}",
    )


def run_g7_cities_green(
    *,
    attached: Sequence[dict],
    outside: Sequence[dict],
    defects: Sequence[dict],
    cells: Sequence[dict],
    counts: Dict[str, int],
    attachment_method: str,
    source_coords: Dict[str, Dict[str, float]],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        g7cities_q5_attached_in_exactly_one_land_cell(attached, cells),
        g7cities_a_containment_only(
            attached, defects, cells, attachment_method
        ),
        g7cities_b_three_categories_sum(counts, attached, outside, defects),
        g7cities_c_defect_coords_unchanged(defects, source_coords),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G8 — possession dérivée (v1_060)
# ---------------------------------------------------------------------------

G8_VALID_OWNERSHIP_CERTAINTY = ("gameplay",)
G8_FORBIDDEN_OWNERSHIP_CERTAINTY = ("attested", "reconstructed")


def g8a_owned_have_certainty_and_provenance(
    ownership_rows: Sequence[dict],
) -> CheckResult:
    """G8-A : chaque cellule possédée porte certitude valide + provenance non vide."""
    bad: List[str] = []
    for row in ownership_rows:
        if row.get("owner_tag") is None:
            continue
        cid = row.get("cell_id")
        certainty = row.get("certainty")
        provenance = str(row.get("provenance") or "").strip()
        if certainty not in G8_VALID_OWNERSHIP_CERTAINTY:
            bad.append(f"cell_{cid}:certainty={certainty!r}")
        if not provenance:
            bad.append(f"cell_{cid}:empty_provenance")
    return CheckResult(
        id="G8-A",
        name="cellule possedee : certitude valide et provenance non vide",
        passed=len(bad) == 0,
        detail="; ".join(bad[:12]) if bad else "ok",
    )


def g8b_unowned_have_no_default_owner(
    ownership_rows: Sequence[dict],
    unassigned_ids: Sequence[int],
) -> CheckResult:
    """G8-B : aucune cellule non attribuée n'a reçu de propriétaire par défaut.

    La voie ville (city_only) est une voie indépendante légitime ; le défaut
    (NN / voisin / tag inventé) est interdit.
    """
    by_id = {int(r["cell_id"]): r for r in ownership_rows}
    forced: List[int] = []
    for cid in unassigned_ids:
        row = by_id.get(int(cid))
        if row is None:
            continue
        tag = row.get("owner_tag")
        path = row.get("path")
        if tag is not None and path != "city_only":
            forced.append(int(cid))
        if row.get("owner_via_province") is not None:
            # Incohérent : non attribuée v1_057 mais voie province renseignée.
            forced.append(int(cid))
    forced = sorted(set(forced))
    return CheckResult(
        id="G8-B",
        name="cellule non attribuee sans proprietaire par defaut",
        passed=len(forced) == 0,
        detail=("ok" if not forced else f"forced_default_owner={forced[:12]}"),
    )


def g8c_borders_recomputable(
    ownership_rows: Sequence[dict],
    adjacency: Sequence[dict],
    published_pairs: Sequence[dict],
) -> CheckResult:
    """G8-C : frontières = contour recalculable, pas une donnée d'entrée."""
    owner_of = {int(r["cell_id"]): r.get("owner_tag") for r in ownership_rows}
    recomputed: Dict[Tuple[str, str], float] = {}
    for edge in adjacency:
        if edge.get("kind") != "land-land":
            continue
        a, b = int(edge["a"]), int(edge["b"])
        oa, ob = owner_of.get(a), owner_of.get(b)
        if oa is None or ob is None or oa == ob:
            continue
        pair = tuple(sorted((str(oa), str(ob))))
        recomputed[pair] = recomputed.get(pair, 0.0) + float(edge["shared_length_m"])

    published = {
        (str(p["tag_a"]), str(p["tag_b"])): float(p["length_m"])
        for p in published_pairs
    }
    # Normaliser ordre des tags.
    published_norm = {
        tuple(sorted(k)): v for k, v in published.items()
    }
    bad: List[str] = []
    all_keys = sorted(set(recomputed) | set(published_norm))
    for key in all_keys:
        a = recomputed.get(key, 0.0)
        b = published_norm.get(key, 0.0)
        if abs(a - b) > LENGTH_EPS:
            bad.append(f"{key}:recomputed={a:.3f} published={b:.3f}")
    return CheckResult(
        id="G8-C",
        name="frontieres = contour recalculable non stocke en entree",
        passed=len(bad) == 0,
        detail="; ".join(bad[:8]) if bad else f"ok pairs={len(published_norm)}",
    )


def g8d_no_attested_or_reconstructed_ownership(
    ownership_rows: Sequence[dict],
) -> CheckResult:
    """G8-D : aucune certitude attested ni reconstructed pour la possession."""
    bad: List[str] = []
    for row in ownership_rows:
        if row.get("owner_tag") is None:
            continue
        certainty = row.get("certainty")
        if certainty in G8_FORBIDDEN_OWNERSHIP_CERTAINTY:
            bad.append(f"cell_{row.get('cell_id')}:certainty={certainty}")
        if certainty != "gameplay":
            bad.append(f"cell_{row.get('cell_id')}:not_gameplay={certainty!r}")
    return CheckResult(
        id="G8-D",
        name="aucune certitude attested/reconstructed pour la possession",
        passed=len(bad) == 0,
        detail="; ".join(bad[:12]) if bad else "ok all gameplay",
    )


def run_g8_green(
    *,
    ownership_rows: Sequence[dict],
    unassigned_ids: Sequence[int],
    adjacency: Sequence[dict],
    published_border_pairs: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        g8a_owned_have_certainty_and_provenance(ownership_rows),
        g8b_unowned_have_no_default_owner(ownership_rows, unassigned_ids),
        g8c_borders_recomputable(
            ownership_rows, adjacency, published_border_pairs
        ),
        g8d_no_attested_or_reconstructed_ownership(ownership_rows),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G9 — LOD topologiques (arcs partagés)
# ---------------------------------------------------------------------------


def g9a_arc_reconstitution_identical(reconstitution: Dict[str, Any]) -> CheckResult:
    ok = bool(reconstitution.get("ok"))
    return CheckResult(
        id="G9-A",
        name="recomposition arcs non simplifies = maille native",
        passed=ok,
        detail=(
            f"checked={reconstitution.get('checked')} "
            f"mismatches={reconstitution.get('mismatch_count')} "
            f"eps_m2={reconstitution.get('eps_m2')}"
        ),
    )


def g9b_no_holes_any_lod(levels: Sequence[dict], hole_eps_m2: float) -> CheckResult:
    bad = []
    for lvl in levels:
        holes = float(lvl.get("holes_m2") or 0.0)
        if holes > hole_eps_m2:
            bad.append(f"lod{lvl.get('lod')}:holes_m2={holes:.3f}")
    return CheckResult(
        id="G9-B",
        name="aucun trou entre cellules a aucun niveau",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"holes<=eps ({hole_eps_m2}) tous LOD",
    )


def g9c_no_overlaps_any_lod(
    levels: Sequence[dict], overlap_eps_m2: float
) -> CheckResult:
    bad = []
    for lvl in levels:
        ov = float(lvl.get("overlaps_m2") or 0.0)
        if ov > overlap_eps_m2:
            bad.append(f"lod{lvl.get('lod')}:overlaps_m2={ov:.3f}")
    return CheckResult(
        id="G9-C",
        name="aucun chevauchement a aucun niveau",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else f"overlaps<=eps ({overlap_eps_m2}) tous LOD",
    )


def g9d_adjacencies_survive_all_lods(levels: Sequence[dict]) -> CheckResult:
    bad = []
    for lvl in levels:
        lost = int(lvl.get("adjacency_lost") or 0)
        total = int(lvl.get("adjacency_total") or 0)
        survived = int(lvl.get("adjacency_survived") or 0)
        if lost > 0 or (total > 0 and survived != total):
            bad.append(
                f"lod{lvl.get('lod')}:survived={survived}/{total} lost={lost}"
            )
    return CheckResult(
        id="G9-D",
        name="toute adjacence v1_050 survit a chaque niveau",
        passed=len(bad) == 0,
        detail="; ".join(bad) if bad else "toutes adjacences survivantes",
    )


def g9e_native_bit_identical(levels: Sequence[dict], native_fp: str) -> CheckResult:
    lod0 = next((lvl for lvl in levels if int(lvl.get("lod", -1)) == 0), None)
    if lod0 is None:
        return CheckResult(
            id="G9-E",
            name="niveau natif bit-a-bit = v1_049",
            passed=False,
            detail="LOD0 absent",
        )
    fp = str(lod0.get("geometry_fingerprint") or "")
    flag = bool(lod0.get("is_native_bit_identical"))
    ok = flag and fp == native_fp and fp != ""
    return CheckResult(
        id="G9-E",
        name="niveau natif bit-a-bit = v1_049",
        passed=ok,
        detail=f"lod0_fp={fp[:16]}… native_fp={native_fp[:16]}… flag={flag}",
    )


def run_g9_green(
    *,
    reconstitution: Dict[str, Any],
    levels: Sequence[dict],
    native_fp: str,
    hole_eps_m2: float,
    overlap_eps_m2: float,
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        g9a_arc_reconstitution_identical(reconstitution),
        g9b_no_holes_any_lod(levels, hole_eps_m2),
        g9c_no_overlaps_any_lod(levels, overlap_eps_m2),
        g9d_adjacencies_survive_all_lods(levels),
        g9e_native_bit_identical(levels, native_fp),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# G10 — textures d'identifiants (v1_062)
# ---------------------------------------------------------------------------


def g10a_all_entities_present_selection_lods(
    presence: Dict[int, Dict[str, Any]],
    selection_lods: Sequence[int],
) -> CheckResult:
    """G10-A : chaque cellule et zone maritime ≥ 1 px aux LOD de sélection."""
    missing: List[str] = []
    for lod in selection_lods:
        p = presence.get(int(lod)) or {}
        for cid in p.get("missing_land") or []:
            missing.append(f"lod{lod}:land:{cid}")
        for zid in p.get("missing_sea") or []:
            missing.append(f"lod{lod}:sea:{zid}")
    return CheckResult(
        id="G10-A",
        name="chaque cellule et zone maritime occupe >=1 px aux LOD selection",
        passed=len(missing) == 0,
        detail=("missing=" + ",".join(missing)) if missing else "all_present",
    )


def g10b_concordance_texture_geometry(
    concordance: Dict[int, Dict[str, Any]],
    min_rate: float,
    heart_max: int,
    selection_lods: Sequence[int],
) -> CheckResult:
    """G10-B : concordance ≥ seuil ; écarts de cœur à zéro."""
    fails: List[str] = []
    for lod in selection_lods:
        c = concordance.get(int(lod)) or {}
        rate = float(c.get("match_rate") or 0.0)
        heart = int(c.get("heart_mismatches") or 0)
        if rate < min_rate:
            fails.append(f"lod{lod}:rate={rate}<{min_rate}")
        if heart > heart_max:
            fails.append(f"lod{lod}:heart={heart}>{heart_max}")
    return CheckResult(
        id="G10-B",
        name="concordance texture/geometrie au-dessus du seuil, coeur a zero",
        passed=len(fails) == 0,
        detail="; ".join(fails) if fails else "ok",
    )


def g10c_no_id_collision_with_empty(
    encoding: Dict[str, Any],
) -> CheckResult:
    """G10-C : aucun identifiant réel ne collisionne avec mer/vide (=0)."""
    collisions = list(encoding.get("collisions_with_empty") or [])
    empty = encoding.get("empty_reserved")
    return CheckResult(
        id="G10-C",
        name="aucun identifiant reel ne collisionne avec la valeur reservee mer",
        passed=len(collisions) == 0 and empty == 0,
        detail=(
            f"collisions={collisions} empty={empty}"
            if collisions or empty != 0
            else "no_collision empty=0"
        ),
    )


def g10d_edge_arbitration_deterministic(
    sha_pairs: Dict[str, List[str]],
    png_keys: Sequence[str],
) -> CheckResult:
    """G10-D : règle d'arbitrage déterministe — mêmes PNG ×2."""
    relevant = {
        k: v
        for k, v in sha_pairs.items()
        if any(k.endswith(pk) or pk in k for pk in png_keys)
    }
    if not relevant:
        # Fallback : toutes les clés cell_ids / mask.
        relevant = {
            k: v
            for k, v in sha_pairs.items()
            if "cell_ids_lod" in k or "mask_land_sea_lake" in k
        }
    bad = [
        path
        for path, pair in relevant.items()
        if len(pair) < 2 or pair[0] != pair[1] or not pair[0]
    ]
    return CheckResult(
        id="G10-D",
        name="arbitrage de bords deterministe (PNG identiques x2)",
        passed=len(bad) == 0 and len(relevant) > 0,
        detail=(
            f"mismatch={bad}" if bad else f"match_on={len(relevant)}_png"
        ),
    )


def run_g10_green(
    *,
    presence: Dict[int, Dict[str, Any]],
    concordance: Dict[int, Dict[str, Any]],
    encoding: Dict[str, Any],
    sha_pairs: Dict[str, List[str]],
    selection_lods: Sequence[int],
    min_rate: float,
    heart_max: int,
) -> List[CheckResult]:
    png_keys = [
        "cell_ids_lod0.png",
        "cell_ids_lod1.png",
        "cell_ids_lod2.png",
        "mask_land_sea_lake_lod0.png",
        "mask_land_sea_lake_lod1.png",
        "mask_land_sea_lake_lod2.png",
    ]
    return [
        g10a_all_entities_present_selection_lods(presence, selection_lods),
        g10b_concordance_texture_geometry(
            concordance, min_rate, heart_max, selection_lods
        ),
        g10c_no_id_collision_with_empty(encoding),
        g10d_edge_arbitration_deterministic(sha_pairs, png_keys),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# P1 — proposition de peuplement (v1_066)
# ---------------------------------------------------------------------------


def p1a_no_population_from_source(
    proposed: Sequence[dict],
    *,
    population_fields_imported: int,
    population_leak: Sequence[str],
) -> CheckResult:
    """P1-A : aucune ville proposée ne porte de population issue de la source."""
    bad = []
    for row in proposed:
        for key, val in row.items():
            kl = str(key).lower()
            if "pop" in kl and val is not None:
                bad.append(f"{row.get('requested') or row.get('name')}.{key}={val}")
    bad.extend(str(x) for x in population_leak)
    passed = population_fields_imported == 0 and len(bad) == 0
    return CheckResult(
        id="P1-A",
        name="aucune population issue de la source moderne",
        passed=passed,
        detail=(
            f"imported={population_fields_imported} leaks={bad[:8]}"
            if not passed
            else "0 population importee"
        ),
    )


def p1b_no_silent_omission(
    *,
    cto_list: Sequence[str],
    matched: Sequence[dict],
    not_found: Sequence[dict],
    outside: Sequence[dict],
    unattached: Sequence[dict],
    counts: Dict[str, int],
) -> CheckResult:
    """P1-B : toute ville CTO non appariée est nommée ; hors fenêtre ≠ non retrouvée."""
    named_nf = {r["requested"] for r in not_found}
    named_out = {r["requested"] for r in outside}
    named_ok = {r["requested"] for r in matched}
    named_un = {r["requested"] for r in unattached}
    all_named = named_nf | named_out | named_ok | named_un
    missing_silent = [n for n in cto_list if n not in all_named]
    sum_ok = (
        int(counts.get("matched_in_window", -1))
        + int(counts.get("not_found", -1))
        + int(counts.get("outside_pilot_window", -1))
        == int(counts.get("requested_total", -2))
    )
    # Catégories disjointes (hors unattached ⊆ matched_in_window).
    overlap_nf_out = named_nf & named_out
    overlap_ok_nf = (named_ok | named_un) & named_nf
    passed = (
        len(missing_silent) == 0
        and sum_ok
        and len(overlap_nf_out) == 0
        and len(overlap_ok_nf) == 0
        and len(named_nf) == int(counts.get("not_found", -1))
        and len(named_out) == int(counts.get("outside_pilot_window", -1))
    )
    return CheckResult(
        id="P1-B",
        name="aucune omission silencieuse — non retrouvees et hors fenetre separees",
        passed=passed,
        detail=(
            f"silent={missing_silent[:8]} sum_ok={sum_ok} overlap={sorted(overlap_nf_out)}"
            if not passed
            else f"named_nf={len(named_nf)} named_out={len(named_out)}"
        ),
    )


def p1c_containment_only(
    attached: Sequence[dict],
    unattached: Sequence[dict],
    cells: Sequence[dict],
    attachment_method: str,
) -> CheckResult:
    """P1-C : rattachement par contenance seule — jamais proximité non bornée."""
    if attachment_method != "containment":
        return CheckResult(
            id="P1-C",
            name="rattachement par contenance seule",
            passed=False,
            detail=f"method={attachment_method}",
        )
    # Aucun unattached ne doit porter un cell_id forcé.
    forced = [u for u in unattached if u.get("cell_id") is not None]
    cell_geoms = [(int(c["cell_id"]), shape(c["geometry"])) for c in cells]
    outside_cell = []
    for city in attached:
        pt = Point(float(city["x_m"]), float(city["y_m"]))
        cid = int(city["cell_id"])
        hits = [
            i
            for i, g in cell_geoms
            if (g.contains(pt) or g.touches(pt)) and i == cid
        ]
        if not hits:
            outside_cell.append(city.get("requested") or city.get("name"))
    passed = len(forced) == 0 and len(outside_cell) == 0
    return CheckResult(
        id="P1-C",
        name="rattachement par contenance seule — pas de proximite non bornee",
        passed=passed,
        detail=(
            f"forced={forced[:5]} outside_cell={outside_cell[:5]}"
            if not passed
            else f"attached={len(attached)} unattached_named={len(unattached)}"
        ),
    )


def p1d_duplicates_signaled(
    duplicates: Sequence[dict],
    matched: Sequence[dict],
    existing_names_norm: Sequence[str],
) -> CheckResult:
    """P1-D : tout doublon avec cities.json est signalé."""
    import re
    import unicodedata

    def _norm(value: Any) -> str:
        s = str(value) if value is not None else ""
        if s.lower() in ("nan", "none", ""):
            return ""
        s = unicodedata.normalize("NFKD", s)
        s = "".join(ch for ch in s if not unicodedata.combining(ch))
        s = s.lower().replace("'", "").replace("’", "")
        return re.sub(r"[^a-z0-9]+", " ", s).strip()

    signaled = {_norm(d["proposed"]) for d in duplicates}
    existing = set(existing_names_norm)
    silent = []
    for m in matched:
        n = _norm(m.get("requested") or m.get("name"))
        src = _norm(m.get("name_source"))
        if n in existing or src in existing:
            hit = any(_norm(d["proposed"]) == n for d in duplicates)
            if not hit and n not in signaled and src not in signaled:
                silent.append(m.get("requested") or m.get("name"))
    passed = len(silent) == 0
    return CheckResult(
        id="P1-D",
        name="aucun doublon cities.json non signale",
        passed=passed,
        detail=(
            f"silent_duplicates={silent[:8]}"
            if not passed
            else f"signaled={len(duplicates)}"
        ),
    )


def run_p1_settlements_green(
    *,
    proposed: Sequence[dict],
    matched: Sequence[dict],
    not_found: Sequence[dict],
    outside: Sequence[dict],
    unattached: Sequence[dict],
    duplicates: Sequence[dict],
    cells: Sequence[dict],
    counts: Dict[str, int],
    cto_list: Sequence[str],
    attachment_method: str,
    population_fields_imported: int,
    population_leak: Sequence[str],
    existing_names_norm: Sequence[str],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        p1a_no_population_from_source(
            proposed,
            population_fields_imported=population_fields_imported,
            population_leak=population_leak,
        ),
        p1b_no_silent_omission(
            cto_list=cto_list,
            matched=matched,
            not_found=not_found,
            outside=outside,
            unattached=unattached,
            counts=counts,
        ),
        p1c_containment_only(matched, unattached, cells, attachment_method),
        p1d_duplicates_signaled(duplicates, matched, existing_names_norm),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# P2 — GeoNames settlements (v1_072)
# ---------------------------------------------------------------------------


def p2a_no_population_from_geonames(
    proposed: Sequence[dict],
    corrections: Sequence[dict],
    *,
    population_fields_imported: int,
    population_leak: Sequence[str],
) -> CheckResult:
    """P2-A : aucune population / densité / rang GeoNames dans une sortie."""
    bad = []
    for row in list(proposed) + list(corrections):
        for key, val in row.items():
            kl = str(key).lower()
            if any(tok in kl for tok in ("pop", "density", "rank")) and val is not None:
                if key == "population" and val is None:
                    continue
                bad.append(f"{row.get('requested') or row.get('name')}.{key}={val}")
    bad.extend(str(x) for x in population_leak)
    passed = population_fields_imported == 0 and len(bad) == 0
    return CheckResult(
        id="P2-A",
        name="aucune population densite rang GeoNames dans les sorties",
        passed=passed,
        detail=(
            f"imported={population_fields_imported} leaks={bad[:8]}"
            if not passed
            else "0 population/rang GeoNames"
        ),
    )


def p2b_matching_bounded(
    corrections_matched: Sequence[dict],
    corrections_not_found: Sequence[dict],
    proposal_matched: Sequence[dict],
    proposal_outside: Sequence[dict],
    *,
    max_distance_km: float,
    match_window_lonlat: Sequence[float],
    pilot_window_lonlat: Sequence[float],
) -> CheckResult:
    """P2-B : tout appariement borné (fenêtre + distance) ; hors borne nommé."""
    w, s, e, n = [float(x) for x in match_window_lonlat]
    pw, ps, pe, pn = [float(x) for x in pilot_window_lonlat]
    bad = []
    for row in corrections_matched:
        dist = row.get("distance_km")
        if dist is None or float(dist) > float(max_distance_km):
            bad.append(f"corr_dist:{row.get('name')}={dist}")
        if row.get("match_window_lonlat") is None and row.get("max_distance_km") is None:
            # fenêtre doit être déclarée sur la ligne ou globalement
            pass
        lon = row.get("lon_proposed")
        lat = row.get("lat_proposed")
        if lon is None or lat is None:
            bad.append(f"corr_missing_coords:{row.get('name')}")
        elif not (w <= float(lon) <= e and s <= float(lat) <= n):
            bad.append(f"corr_outside_window:{row.get('name')}")
        declared = row.get("max_distance_km")
        if declared is not None and float(declared) > float(max_distance_km) + 1e-9:
            bad.append(f"corr_unbounded:{row.get('name')}")
    for row in proposal_matched:
        lon, lat = row.get("lon"), row.get("lat")
        if lon is None or lat is None:
            bad.append(f"prop_missing:{row.get('requested')}")
        elif not (pw <= float(lon) <= pe and ps <= float(lat) <= pn):
            bad.append(f"prop_outside_as_matched:{row.get('requested')}")
    # Hors fenêtre / not_found doivent être nommés (listes non silencieuses)
    named_ok = all(r.get("name") for r in corrections_not_found) and all(
        r.get("requested") for r in proposal_outside
    )
    passed = len(bad) == 0 and named_ok
    return CheckResult(
        id="P2-B",
        name="appariement borne fenetre+distance — hors borne nomme",
        passed=passed,
        detail=(
            f"bad={bad[:8]} named_ok={named_ok}"
            if not passed
            else f"max_km={max_distance_km} matched_corr={len(corrections_matched)}"
        ),
    )


def p2c_no_silent_omission_123_and_105(
    *,
    game_city_names: Sequence[str],
    cto_list: Sequence[str],
    corrections_matched: Sequence[dict],
    corrections_not_found: Sequence[dict],
    proposal_matched: Sequence[dict],
    proposal_not_found: Sequence[dict],
    proposal_outside: Sequence[dict],
    proposal_unattached: Sequence[dict],
    corr_counts: Dict[str, int],
    prop_counts: Dict[str, int],
) -> CheckResult:
    """P2-C : aucune des 123 ni des 105 omise en silence."""
    corr_named = {r["name"] for r in corrections_matched} | {
        r["name"] for r in corrections_not_found
    }
    silent_123 = [n for n in game_city_names if n not in corr_named]
    sum_123 = (
        int(corr_counts.get("matched", -1)) + int(corr_counts.get("not_found", -1))
        == int(corr_counts.get("examined", -2))
        == 123
    )

    prop_named = (
        {r["requested"] for r in proposal_matched}
        | {r["requested"] for r in proposal_not_found}
        | {r["requested"] for r in proposal_outside}
        | {r["requested"] for r in proposal_unattached}
    )
    silent_105 = [n for n in cto_list if n not in prop_named]
    sum_105 = (
        int(prop_counts.get("matched_in_window", -1))
        + int(prop_counts.get("not_found", -1))
        + int(prop_counts.get("outside_pilot_window", -1))
        == int(prop_counts.get("requested_total", -2))
        == len(cto_list)
    )
    passed = (
        len(silent_123) == 0
        and len(silent_105) == 0
        and sum_123
        and sum_105
        and len(game_city_names) == 123
        and len(cto_list) == 105
    )
    return CheckResult(
        id="P2-C",
        name="aucune omission silencieuse des 123 et des 105",
        passed=passed,
        detail=(
            f"silent_123={silent_123[:5]} silent_105={silent_105[:5]} "
            f"sum_123={sum_123} sum_105={sum_105}"
            if not passed
            else "123+105 toutes nommees"
        ),
    )


def p2d_licence_locked(lock_doc: Dict[str, Any]) -> CheckResult:
    """P2-D : sources.lock porte geonames_cities500 avec attribution."""
    entry = lock_doc.get("geonames_cities500") or {}
    lic = entry.get("licence") or {}
    ok = (
        bool(entry.get("sha256"))
        and bool(entry.get("file"))
        and lic.get("attribution_required") is True
        and bool(lic.get("attribution_text"))
        and "CC BY 4.0" in str(lic.get("licence") or "")
    )
    return CheckResult(
        id="P2-D",
        name="licence GeoNames verrouillee avec attribution",
        passed=bool(ok),
        detail=(
            f"entry_keys={sorted(entry.keys())} lic={sorted(lic.keys())}"
            if not ok
            else f"sha256={str(entry.get('sha256'))[:16]}…"
        ),
    )


def p2e_containment_only(
    attached: Sequence[dict],
    unattached: Sequence[dict],
    cells: Sequence[dict],
    attachment_method: str,
) -> CheckResult:
    """P2-E : rattachement par contenance seule."""
    if attachment_method != "containment":
        return CheckResult(
            id="P2-E",
            name="rattachement par contenance seule (P2)",
            passed=False,
            detail=f"method={attachment_method}",
        )
    forced = [u for u in unattached if u.get("cell_id") is not None]
    cell_geoms = [(int(c["cell_id"]), shape(c["geometry"])) for c in cells]
    outside_cell = []
    for city in attached:
        pt = Point(float(city["x_m"]), float(city["y_m"]))
        cid = int(city["cell_id"])
        hits = [
            i
            for i, g in cell_geoms
            if (g.contains(pt) or g.touches(pt)) and i == cid
        ]
        if not hits:
            outside_cell.append(city.get("requested") or city.get("name"))
    passed = len(forced) == 0 and len(outside_cell) == 0
    return CheckResult(
        id="P2-E",
        name="rattachement par contenance seule (P2)",
        passed=passed,
        detail=(
            f"forced={forced[:5]} outside_cell={outside_cell[:5]}"
            if not passed
            else f"attached={len(attached)} unattached_named={len(unattached)}"
        ),
    )


def run_p2_geonames_green(
    *,
    proposed: Sequence[dict],
    corrections_matched: Sequence[dict],
    corrections_not_found: Sequence[dict],
    proposal_matched: Sequence[dict],
    proposal_not_found: Sequence[dict],
    proposal_outside: Sequence[dict],
    proposal_unattached: Sequence[dict],
    cells: Sequence[dict],
    game_city_names: Sequence[str],
    cto_list: Sequence[str],
    corr_counts: Dict[str, int],
    prop_counts: Dict[str, int],
    attachment_method: str,
    population_fields_imported: int,
    population_leak: Sequence[str],
    lock_doc: Dict[str, Any],
    max_distance_km: float,
    match_window_lonlat: Sequence[float],
    pilot_window_lonlat: Sequence[float],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        p2a_no_population_from_geonames(
            proposed,
            list(corrections_matched) + list(corrections_not_found),
            population_fields_imported=population_fields_imported,
            population_leak=population_leak,
        ),
        p2b_matching_bounded(
            corrections_matched,
            corrections_not_found,
            proposal_matched,
            proposal_outside,
            max_distance_km=max_distance_km,
            match_window_lonlat=match_window_lonlat,
            pilot_window_lonlat=pilot_window_lonlat,
        ),
        p2c_no_silent_omission_123_and_105(
            game_city_names=game_city_names,
            cto_list=cto_list,
            corrections_matched=corrections_matched,
            corrections_not_found=corrections_not_found,
            proposal_matched=proposal_matched,
            proposal_not_found=proposal_not_found,
            proposal_outside=proposal_outside,
            proposal_unattached=proposal_unattached,
            corr_counts=corr_counts,
            prop_counts=prop_counts,
        ),
        p2d_licence_locked(lock_doc),
        p2e_containment_only(
            proposal_matched, proposal_unattached, cells, attachment_method
        ),
        q10_determinism(sha_pairs),
    ]


# ---------------------------------------------------------------------------
# A12 — apparence ombrage + biomes (v1_069)
# ---------------------------------------------------------------------------


def a1a_hillshade_aligned_with_id_textures(
    *,
    alignment_ok: bool,
    framing: Dict[str, Any],
    detail: Sequence[str],
) -> CheckResult:
    """A1-A : ombrage et textures d'identifiants partagent cadrage / projection / résolutions."""
    res = framing.get("resolutions") or {}
    expected = {"0": [4096, 3686], "1": [2048, 1843], "2": [1024, 922]}
    res_ok = all(res.get(k) == expected[k] for k in expected)
    passed = bool(alignment_ok) and res_ok
    return CheckResult(
        id="A1-A",
        name="ombrage aligne cadrage projection resolution des textures ids",
        passed=passed,
        detail=(
            f"align={alignment_ok} res_ok={res_ok} detail={list(detail)[:4]}"
            if not passed
            else f"resolutions={res}"
        ),
    )


def a1b_every_cell_has_biome(
    *,
    all_have_biome: bool,
    n_cells: int,
    missing: Sequence[int],
) -> CheckResult:
    """A1-B : aucune cellule sans biome attribué."""
    passed = bool(all_have_biome) and n_cells > 0 and len(missing) == 0
    return CheckResult(
        id="A1-B",
        name="aucune cellule sans biome attribue",
        passed=passed,
        detail=(
            f"n={n_cells} missing={list(missing)[:8]}"
            if not passed
            else f"n={n_cells}"
        ),
    )


def a1c_biome_distribution_not_degenerate(
    *,
    max_share: float,
    threshold: float,
    distribution: Dict[str, int],
) -> CheckResult:
    """A1-C : distribution biomes non dégénérée (seuil déclaré)."""
    passed = max_share < threshold and len(distribution) >= 4
    return CheckResult(
        id="A1-C",
        name="distribution biomes non degenerée",
        passed=passed,
        detail=(
            f"max_share={max_share:.4f} threshold={threshold} dist={distribution}"
        ),
    )


def a1d_geometry_ids_relief_unchanged(
    *,
    unchanged_ok: bool,
    fp_before: Dict[str, str],
    fp_after: Dict[str, str],
) -> CheckResult:
    """A1-D : géométrie / identifiants / relief G3–G6–G10 inchangés (empreinte)."""
    mismatches = [
        k for k in sorted(set(fp_before) | set(fp_after))
        if fp_before.get(k) != fp_after.get(k)
    ]
    passed = bool(unchanged_ok) and len(mismatches) == 0
    return CheckResult(
        id="A1-D",
        name="geometrie identifiants relief inchanges (empreinte)",
        passed=passed,
        detail=(
            f"mismatches={mismatches}"
            if not passed
            else f"fingerprints_ok={len(fp_after)}"
        ),
    )


def run_a12_green(
    *,
    alignment_ok: bool,
    framing: Dict[str, Any],
    alignment_detail: Sequence[str],
    all_have_biome: bool,
    n_cells: int,
    missing_biome: Sequence[int],
    max_share: float,
    threshold: float,
    distribution: Dict[str, int],
    unchanged_ok: bool,
    fp_before: Dict[str, str],
    fp_after: Dict[str, str],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    return [
        a1a_hillshade_aligned_with_id_textures(
            alignment_ok=alignment_ok,
            framing=framing,
            detail=alignment_detail,
        ),
        a1b_every_cell_has_biome(
            all_have_biome=all_have_biome,
            n_cells=n_cells,
            missing=missing_biome,
        ),
        a1c_biome_distribution_not_degenerate(
            max_share=max_share,
            threshold=threshold,
            distribution=distribution,
        ),
        a1d_geometry_ids_relief_unchanged(
            unchanged_ok=unchanged_ok,
            fp_before=fp_before,
            fp_after=fp_after,
        ),
        q10_determinism(sha_pairs),
    ]
