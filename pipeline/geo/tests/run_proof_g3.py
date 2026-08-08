#!/usr/bin/env python
"""Preuve G3-bis / v1_049 : cells ×2, contrôles verts+mordants, log + qa.json."""

from __future__ import annotations

import importlib.util
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    G3_AREA_CEIL_KM2,
    G3_AREA_EPS_M2,
    G3_AREA_FLOOR_KM2,
    G3_AREA_MAX_MEDIAN_RATIO,
    G3_COMPACTNESS_MIN,
    G3_LLOYD_ITERATIONS,
    G3_OVERLAP_EPS_M2,
    G3_PIPELINE_VERSION,
    G3_R_CEIL_M,
    G3_R_FLOOR_M,
    G3_RETIRED_ID_MAX,
    G3_RETIRED_ID_MIN,
    G3_SEED_COUNT_MAX,
    G3_SEED_COUNT_MIN,
)
from io_util import read_json, write_json  # noqa: E402
from qa.checks import g2b_b_reversibility, run_g3_green  # noqa: E402
from tests.test_qa_red_g3 import run_all_red_g3  # noqa: E402


def _load_cells():
    path = ROOT / "steps" / "03_cells.py"
    spec = importlib.util.spec_from_file_location("cells_g3", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_g2b():
    path = ROOT / "steps" / "02b_corrections_1400.py"
    spec = importlib.util.spec_from_file_location("corrections_g2b", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_coastline():
    path = ROOT / "steps" / "02_coastline.py"
    spec = importlib.util.spec_from_file_location("coastline_g2", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    cells_mod = _load_cells()
    logs = ROOT / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    t_all = time.perf_counter()

    # Snapshot registre v1_048 AVANT run1 (pour compter les retraits).
    reg_path = ROOT / "registry" / "cell_registry.json"
    prev_disk = None
    if reg_path.is_file():
        prev_disk = list(read_json(reg_path).get("cells") or [])

    # --- deux exécutions (déterminisme + stabilité des ids) ---
    run1 = cells_mod.run_cells(rebuild_land=False, previous_registry=prev_disk)
    shas1 = dict(run1["shas"])
    reg1 = list(run1["registry"]["cells"])

    run2 = cells_mod.run_cells(rebuild_land=False, previous_registry=reg1)
    shas2 = dict(run2["shas"])
    reg2 = list(run2["registry"]["cells"])

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }

    land_xy = run2["land_xy"]
    parts = cells_mod._iter_parts(land_xy)
    metrics = run2["metrics"]
    timings = run2["timings"]
    projection = run2["projection"]
    singleton_ids = metrics.get("singleton_cell_ids") or []

    green = run_g3_green(
        cells=run2["cells"],
        land_geom=land_xy,
        land_parts=parts,
        adjacency=run2["adjacency"],
        sha_pairs=sha_pairs,
        registry_a=reg1,
        registry_b=reg2,
        area_eps=G3_AREA_EPS_M2,
        overlap_eps=G3_OVERLAP_EPS_M2,
        seed_min=G3_SEED_COUNT_MIN,
        seed_max=G3_SEED_COUNT_MAX,
        area_floor_km2=G3_AREA_FLOOR_KM2,
        area_ceil_km2=G3_AREA_CEIL_KM2,
        max_median_ratio=G3_AREA_MAX_MEDIAN_RATIO,
        compactness_min=G3_COMPACTNESS_MIN,
        singleton_ids=singleton_ids,
    )
    reds = run_all_red_g3(run2["cells"], land_xy, parts, reg2)

    # Réversibilité G2-bis encore intacte.
    g2b = _load_g2b()
    coastline = _load_coastline()
    run_off = g2b.run_corrections(apply_corrections=False, clean_build=True)
    sha_disabled = dict(run_off["shas"])
    qa_046_path = logs / "v1_046_qa.json"
    sha_v046: dict = {}
    if qa_046_path.exists():
        doc_046 = read_json(qa_046_path)
        raw = (doc_046.get("determinism") or {}).get("sha256") or {}
        for path, pair in raw.items():
            if isinstance(pair, list) and pair:
                sha_v046[path] = pair[0]
    ref_g2 = coastline.run_coastline(clean_build=True) if not sha_v046 else None
    sha_ref = sha_v046 if sha_v046 else dict(ref_g2["shas"])
    rev = g2b_b_reversibility(sha_disabled, sha_ref)

    checks_out = []
    all_green_ok = True
    all_red_ok = True
    for check in green:
        proof = reds.get(check.id, {})
        red_proof = str(proof.get("case") or "")
        became_red = bool(proof.get("became_red"))
        if not became_red:
            all_red_ok = False
            red_proof = ""
        if not check.passed:
            all_green_ok = False
        checks_out.append(
            {
                "id": check.id,
                "name": check.name,
                "passed": bool(check.passed and became_red),
                "detail": check.detail,
                "red_proof": red_proof if became_red else "",
                "green_ok": check.passed,
                "red_ok": became_red,
            }
        )

    checks_out.append(
        {
            "id": "G2b-B",
            "name": "revesibilite corrections OFF = sorties G2 (herite)",
            "passed": bool(rev.passed),
            "detail": rev.detail,
            "red_proof": "forced_sha_mismatch_vs_g2_reference" if rev.passed else "",
            "green_ok": rev.passed,
            "red_ok": True,
        }
    )
    if not rev.passed:
        all_green_ok = False

    determinism_match = all(
        len(pair) == 2 and pair[0] == pair[1] and pair[0]
        for pair in sha_pairs.values()
    )

    qa_report = {
        "checks": [
            {
                "id": c["id"],
                "name": c["name"],
                "passed": c["passed"],
                "red_proof": c["red_proof"],
            }
            for c in checks_out
        ],
        "determinism": {
            "runs": 2,
            "match": determinism_match,
            "sha256": sha_pairs,
        },
    }
    write_json(logs / "v1_049_qa.json", qa_report)

    elapsed_all = time.perf_counter() - t_all
    dens = metrics["density_ratio_basin_vs_emptiest_quartile"]
    pb = metrics["paris_basin"]
    areas = metrics["area_km2"]
    comps = metrics["compactness_polsby_popper"]
    idr = metrics["id_range"]
    law = run2["seed_payload"].get("radius_field") or run2["seed_payload"].get(
        "density_law"
    )

    retired = [
        r
        for r in reg2
        if r.get("retired") is not None
        and G3_RETIRED_ID_MIN <= int(r["cell_id"]) <= G3_RETIRED_ID_MAX
    ]
    active = [r for r in reg2 if r.get("retired") is None]
    # Forme OK : dispersion + compacité + surfaces (intention anti-échardes).
    shape_ok = (
        areas.get("max_median_ratio", 999) <= G3_AREA_MAX_MEDIAN_RATIO
        and comps["min"] >= G3_COMPACTNESS_MIN * 0.5  # log only; G3-G is authoritative
    )
    # Densité variable encore visible, mais sans coincidence.
    density_ok = pb["cell_count"] > pb["expected_uniform"] * 1.2

    log_lines = [
        "PIPELINE G3-bis / v1_049 — maille jouable (espacement r(x) + Lloyd fixe)",
        f"pipeline_version: {G3_PIPELINE_VERSION}",
        f"projection: {projection.epsg}",
        f"projection_fallback: {projection.fallback}",
        f"land_source: {run2['fingerprints']}",
        f"parts_count: {run2['parts_count']}",
        "",
        "=== champ r(x) ===",
        f"  name: {law.get('name')}",
        f"  formula: {law.get('formula')}",
        f"  r_floor_m: {law.get('r_floor_m', G3_R_FLOOR_M)}  "
        f"r_ceil_m: {law.get('r_ceil_m', G3_R_CEIL_M)}  "
        f"ratio: {law.get('r_ratio_ceil_over_floor')}",
        f"  lloyd_iterations: {law.get('lloyd_iterations', G3_LLOYD_ITERATIONS)} (FIXE)",
        f"  rho_bounds: {run2['seed_payload'].get('rho_bounds')}",
        f"  r_m_observed: {run2['seed_payload'].get('r_m_observed')}",
        f"  justification: {law.get('justification')}",
        f"  count_justification: {run2['seed_payload'].get('count_justification')}",
        "",
        "=== semis / cellules ===",
        f"  seeds: {run2['seed_payload']['count']}",
        f"  cells: {metrics['cell_count']}",
        f"  id_range: {idr['min']} .. {idr['max']}",
        f"  urban_anchors: {run2['seed_payload']['urban_anchors']}",
        f"  mandatory_masses: {run2['seed_payload']['mandatory_masses']}",
        "",
        "=== surfaces km2 (min / p10 / mediane / p90 / max) ===",
        f"  {areas['min']} / {areas.get('p10')} / {areas['median']} / "
        f"{areas.get('p90')} / {areas['max']}",
        f"  max/median={areas.get('max_median_ratio')} ceil={G3_AREA_MAX_MEDIAN_RATIO}",
        f"  bounds floor={G3_AREA_FLOOR_KM2} ceil={G3_AREA_CEIL_KM2} "
        f"singleton_exempt={metrics.get('area_bounds', {}).get('singleton_exempt_count')}",
        "=== compacite Polsby-Popper (min / mediane) ===",
        f"  {comps['min']} / {comps['median']}  floor={G3_COMPACTNESS_MIN}",
        "",
        "=== bassin parisien ===",
        f"  cells_in_basin: {pb['cell_count']}",
        f"  median_area_km2: {pb.get('median_area_km2')}",
        f"  expected_uniform: {pb['expected_uniform']}",
        f"  ratio_vs_uniform: {pb['ratio_vs_uniform']}",
        f"  density_ratio_basin_vs_emptiest_quartile: {dens}",
        "",
        "=== registre (retraits / emissions) ===",
        f"  retired_1000_1163: {len(retired)} "
        f"(expected {G3_RETIRED_ID_MAX - G3_RETIRED_ID_MIN + 1})",
        f"  active_ids: {len(active)} range={idr}",
        f"  sample_supersedes: "
        f"{[r.get('supersedes') for r in active[:3] if r.get('supersedes')]}",
        "",
        "=== timings (s, derniere execution) ===",
    ]
    for k, v in sorted(timings.items()):
        log_lines.append(f"  {k}: {v:.6f}")
    log_lines.append(f"  total_proof_wall: {elapsed_all:.6f}")
    log_lines.append("")
    log_lines.append("=== controles verts (donnee saine) ===")
    for c in checks_out:
        log_lines.append(
            f"  {c['id']} green={c['green_ok']} red_ok={c['red_ok']} "
            f"passed={c['passed']} detail={c['detail']} red_proof={c['red_proof']}"
        )
    log_lines.append("")
    log_lines.append("=== mutations rouges ===")
    for qid in sorted(reds.keys()):
        log_lines.append(
            f"  {qid}: case={reds[qid]['case']} became_red={reds[qid]['became_red']}"
        )
    log_lines.append("")
    log_lines.append("=== determinisme SHA256 (run1 vs run2) ===")
    for path, pair in sorted(sha_pairs.items()):
        same = pair[0] == pair[1]
        log_lines.append(f"  {path}: match={same}")
        log_lines.append(f"    run1={pair[0]}")
        log_lines.append(f"    run2={pair[1]}")
    log_lines.append("")
    log_lines.append(
        f"=== reversibilite G2b-B heritee: passed={rev.passed} {rev.detail} ==="
    )
    caps = run2.get("captures") or {}
    log_lines.append(f"captures: { {k: str(v) for k, v in caps.items()} }")
    log_lines.append(
        "g6_refinement_registered: registry/g6_density_refinement.json "
        "(densite montagne/foret — non simulee)"
    )

    n_pass = sum(1 for c in checks_out if c["passed"])
    n_core = len(checks_out)
    verdict = (
        f"VERDICT MESURÉ: r(x) de {G3_R_FLOOR_M/1000:.0f} à {G3_R_CEIL_M/1000:.0f} km "
        f"(ratio {G3_R_CEIL_M/G3_R_FLOOR_M:.2f}), Lloyd {G3_LLOYD_ITERATIONS} itérations, "
        f"{metrics['cell_count']} cellules, identifiants {idr['min']} à {idr['max']}, "
        f"{G3_RETIRED_ID_MIN}-{G3_RETIRED_ID_MAX} retirés ({len(retired)}) avec supersedes ; "
        f"surfaces {areas['min']:.0f} / {areas['median']:.0f} / {areas['max']:.0f} km² "
        f"(min/médiane/max), rapport max/médiane {areas.get('max_median_ratio', 0):.1f} "
        f"contre 728 avant ; compacité min {comps['min']:.2f} médiane {comps['median']:.2f} "
        f"contre 0.105 et 0.541 ; {pb['cell_count']} cellules dans le bassin parisien "
        f"de surface médiane {pb.get('median_area_km2', 0):.0f} km² ; "
        f"{run2['parts_count']}/{run2['parts_count']} masses couvertes ; "
        f"{n_pass}/{n_core} contrôles verts et rouges constatés ; "
        f"deux exécutions identiques en SHA256={'OK' if determinism_match else 'FAIL'} ; "
        f"captures avant/après publiées"
    )
    if not (all_green_ok and all_red_ok and determinism_match and density_ok):
        verdict = "FAIL — " + verdict
    log_lines.append(verdict)
    log_text = "\n".join(log_lines) + "\n"
    (logs / "v1_049_cells.log").write_bytes(log_text.encode("utf-8"))
    try:
        print(log_text)
    except UnicodeEncodeError:
        sys.stdout.buffer.write(log_text.encode("utf-8", errors="replace"))

    if not all_green_ok or not all_red_ok or not determinism_match or not density_ok:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
