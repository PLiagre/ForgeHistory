"""Contrôles C1 — déterminants physiques du climat (v1_080)."""

from __future__ import annotations

from typing import Any, Dict, List, Sequence

from constants import (
    C1_MONOTONE_DLAT_DEG,
    C1_SEA_DISTANCE_EPS_M,
    WORLD_TERMS_FORBIDDEN_KEYS,
)
from qa.checks import CheckResult, q10_determinism


def c1a_mesh_unchanged(
    base_cell_ids: Sequence[int],
    climate_cells: Sequence[dict],
) -> CheckResult:
    base = sorted(int(x) for x in base_cell_ids)
    got = sorted(int(c["cell_id"]) for c in climate_cells)
    ok = base == got
    return CheckResult(
        id="C1-A",
        name="maille inchangee",
        passed=ok,
        detail=(
            f"base={len(base)} climate={len(got)} equal={ok}"
            if ok
            else f"base={base[:6]}... climate={got[:6]}..."
        ),
    )


def c1b_insolation_latitude_monotone(
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
) -> CheckResult:
    lat_by_id = {int(c["cell_id"]): float(c["centroid"]["lat"]) for c in cells_g3}
    insol_by_id = {
        int(c["cell_id"]): float(c["insolation_annual_mj_m2"]) for c in climate_cells
    }
    ordered = sorted(lat_by_id.keys(), key=lambda cid: lat_by_id[cid])
    inversions = 0
    equal_bad = 0
    above_thresh = 0
    for i in range(1, len(ordered)):
        prev_id, cur_id = ordered[i - 1], ordered[i]
        dlat = lat_by_id[cur_id] - lat_by_id[prev_id]
        prev_i, cur_i = insol_by_id[prev_id], insol_by_id[cur_id]
        if cur_i > prev_i:
            inversions += 1
        if dlat >= C1_MONOTONE_DLAT_DEG:
            above_thresh += 1
            if cur_i >= prev_i:
                equal_bad += 1
    ok = inversions == 0 and equal_bad == 0
    return CheckResult(
        id="C1-B",
        name="insolation decroissante avec la latitude",
        passed=ok,
        detail=(
            f"inversions={inversions} equal_bad={equal_bad} "
            f"pairs_above_thresh={above_thresh}"
        ),
    )


def c1c_daylight_amplitude(
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
) -> CheckResult:
    lat_by_id = {int(c["cell_id"]): float(c["centroid"]["lat"]) for c in cells_g3}
    amp_by_id: Dict[int, float] = {}
    summer_winter_bad = 0
    for c in climate_cells:
        cid = int(c["cell_id"])
        summer = float(c["daylight_h_summer_solstice"])
        winter = float(c["daylight_h_winter_solstice"])
        if summer <= winter:
            summer_winter_bad += 1
        amp_by_id[cid] = summer - winter
    ordered = sorted(lat_by_id.keys(), key=lambda cid: lat_by_id[cid])
    inversions = 0
    equal_bad = 0
    above_thresh = 0
    for i in range(1, len(ordered)):
        prev_id, cur_id = ordered[i - 1], ordered[i]
        dlat = lat_by_id[cur_id] - lat_by_id[prev_id]
        prev_a, cur_a = amp_by_id[prev_id], amp_by_id[cur_id]
        if cur_a < prev_a:
            inversions += 1
        if dlat >= C1_MONOTONE_DLAT_DEG:
            above_thresh += 1
            if cur_a <= prev_a:
                equal_bad += 1
    ok = summer_winter_bad == 0 and inversions == 0 and equal_bad == 0
    return CheckResult(
        id="C1-C",
        name="amplitude jour solstice coherent",
        passed=ok,
        detail=(
            f"summer<=winter={summer_winter_bad} amp_inversions={inversions} "
            f"amp_equal_bad={equal_bad} pairs_above_thresh={above_thresh}"
        ),
    )


def c1d_coastal_distance_consistent(
    coastal_ids: set[int],
    climate_cells: Sequence[dict],
) -> CheckResult:
    bad_coastal: List[str] = []
    bad_inland: List[str] = []
    for c in climate_cells:
        cid = int(c["cell_id"])
        dist = float(c["dist_sea_edge_m"])
        coastal = cid in coastal_ids
        if coastal and dist > C1_SEA_DISTANCE_EPS_M:
            bad_coastal.append(f"{cid}:{dist}")
        if not coastal and dist <= C1_SEA_DISTANCE_EPS_M:
            bad_inland.append(f"{cid}:{dist}")
    ok = not bad_coastal and not bad_inland
    return CheckResult(
        id="C1-D",
        name="littoralite et distance bord coherentes",
        passed=ok,
        detail=(
            f"coastal_bad={bad_coastal[:8]} inland_bad={bad_inland[:8]}"
            if not ok
            else "ok"
        ),
    )


def c1e_continentality_consistent(
    climate_cells: Sequence[dict],
) -> CheckResult:
    rows = list(climate_cells)
    missing_hops = [int(c["cell_id"]) for c in rows if int(c.get("hops_to_sea", -1)) < 0]
    by_hop: Dict[int, List[float]] = {}
    violations = 0
    for c in rows:
        h = int(c["hops_to_sea"])
        if h < 0:
            continue
        by_hop.setdefault(h, []).append(float(c["dist_sea_centroid_m"]))
        if c.get("centroid_inside_cell"):
            edge = float(c["dist_sea_edge_m"])
            cent = float(c["dist_sea_centroid_m"])
            if edge > cent + C1_SEA_DISTANCE_EPS_M:
                violations += 1
    hop_keys = sorted(by_hop.keys())
    non_mono = 0
    for i in range(1, len(hop_keys)):
        prev_med = sorted(by_hop[hop_keys[i - 1]])[len(by_hop[hop_keys[i - 1]]) // 2]
        cur_med = sorted(by_hop[hop_keys[i]])[len(by_hop[hop_keys[i]]) // 2]
        if len(by_hop[hop_keys[i - 1]]) % 2 == 0:
            s = sorted(by_hop[hop_keys[i - 1]])
            prev_med = (s[len(s) // 2 - 1] + s[len(s) // 2]) / 2.0
        if len(by_hop[hop_keys[i]]) % 2 == 0:
            s = sorted(by_hop[hop_keys[i]])
            cur_med = (s[len(s) // 2 - 1] + s[len(s) // 2]) / 2.0
        if cur_med <= prev_med:
            non_mono += 1
    ok = not missing_hops and non_mono == 0 and violations == 0
    return CheckResult(
        id="C1-E",
        name="continentalite concordante",
        passed=ok,
        detail=(
            f"missing_hops={missing_hops[:8]} non_mono={non_mono} "
            f"edge_vs_centroid={violations}"
        ),
    )


def c1f_no_gameplay_keys(artifacts: Sequence[dict]) -> CheckResult:
    bad: List[str] = []

    def walk(obj: Any, path: str = "") -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                key = str(k)
                here = f"{path}.{key}" if path else key
                if key in WORLD_TERMS_FORBIDDEN_KEYS:
                    bad.append(here)
                walk(v, here)
        elif isinstance(obj, list):
            for i, item in enumerate(obj[:50]):
                walk(item, f"{path}[{i}]")

    for doc in artifacts:
        walk(doc)
    return CheckResult(
        id="C1-F",
        name="aucun bareme dans les artefacts C1",
        passed=len(bad) == 0,
        detail="; ".join(bad[:12]) if bad else "ok",
    )


def run_c1_green(
    *,
    cells_g3: Sequence[dict],
    climate_cells: Sequence[dict],
    coastal_ids: set[int],
    artifact_docs: Sequence[dict],
    sha_pairs: Dict[str, List[str]],
) -> List[CheckResult]:
    base_ids = [int(c["cell_id"]) for c in cells_g3]
    return [
        q10_determinism(sha_pairs),
        c1a_mesh_unchanged(base_ids, climate_cells),
        c1b_insolation_latitude_monotone(cells_g3, climate_cells),
        c1c_daylight_amplitude(cells_g3, climate_cells),
        c1d_coastal_distance_consistent(coastal_ids, climate_cells),
        c1e_continentality_consistent(climate_cells),
        c1f_no_gameplay_keys(artifact_docs),
    ]
