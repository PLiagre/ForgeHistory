#!/usr/bin/env python
"""Preuve G2 / v1_046 : coastline ×2, contrôles verts+mordants, log + qa.json."""

from __future__ import annotations

import importlib.util
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import G2_LAND_AREA_KM2_MAX, G2_LAND_AREA_KM2_MIN, PIPELINE_VERSION
from io_util import write_json
from qa.checks import run_g2_green
from tests.test_qa_red_g2 import run_all_red_g2


def _load_coastline():
    path = ROOT / "steps" / "02_coastline.py"
    spec = importlib.util.spec_from_file_location("coastline_g2", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    coastline = _load_coastline()
    logs = ROOT / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    t_all = time.perf_counter()

    run1 = coastline.run_coastline(clean_build=True)
    shas1 = dict(run1["shas"])
    run2 = coastline.run_coastline(clean_build=True)
    shas2 = dict(run2["shas"])

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }

    land_ll = run2["land_ll"]
    land_xy = run2["land_xy"]
    lakes_ll = run2["lakes_ll"]
    built = run2["result"]
    projection = run2["projection"]
    timings = run2["timings"]
    window = coastline.pilot_window_polygon()

    green = run_g2_green(
        land_ll,
        land_xy,
        lakes_ll,
        window,
        built["land_area_km2"],
        G2_LAND_AREA_KM2_MIN,
        G2_LAND_AREA_KM2_MAX,
        sha_pairs,
    )
    reds = run_all_red_g2(land_ll, land_xy, lakes_ll, window)

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
    write_json(logs / "v1_046_qa.json", qa_report)

    elapsed_all = time.perf_counter() - t_all
    thr = built["threshold"]["threshold_km2"]
    largest_dropped = built["largest_dropped"]
    dropped_names = ", ".join(
        f"{d['name']} {d['area_km2']:.1f} km²" for d in largest_dropped[:5]
    )
    dist = built["threshold"]["distribution"]

    log_lines = [
        "PIPELINE G2 / v1_046 — littoral réel",
        f"pipeline_version: {PIPELINE_VERSION}",
        f"projection: {projection.epsg}",
        f"projection_fallback: {projection.fallback}",
        f"projection_reason: {projection.reason}",
        f"source_sha256: {run2['fingerprint']['sha256']}",
        "",
        "=== source (constat avant découpe) ===",
    ]
    for layer in run2["source_inspect"]["layers"]:
        log_lines.append(
            f"  {layer['layer']}: n={layer['feature_count']} crs={layer['crs']} "
            f"bounds={layer['bounds_lonlat']}"
        )
    log_lines += [
        "",
        "=== fenêtre pilote (choix de gameplay) ===",
        f"  lonlat: {built['window']['lonlat']}",
        f"  justification: {built['window']['justification']}",
        "",
        "=== clip ===",
        f"  {built['clip_counts']}",
        "",
        "=== seuil d'îles ===",
        f"  threshold_km2: {thr}",
        f"  gap: {built['threshold']['gap']}",
        f"  method: {built['threshold']['method']}",
        (
            f"  distribution: count={dist['count']} min={dist['min_km2']} "
            f"p50={dist['p50_km2']} p90={dist['p90_km2']} max={dist['max_km2']}"
        ),
        f"  kept: {len(built['islands_kept'])}  dropped: {len(built['islands_dropped'])}",
        f"  largest_dropped: {dropped_names}",
        "",
        "=== terre ===",
        f"  land_area_km2: {built['land_area_km2']}",
        f"  lakes_subtracted: {built['lakes_subtracted']} ({', '.join(built['lake_names'])})",
        f"  hole_count: {built['hole_count']}",
        "",
        "=== timings (s, dernière exécution) ===",
    ]
    for k, v in sorted(timings.items()):
        log_lines.append(f"  {k}: {v:.6f}")
    log_lines.append(f"  total_proof_wall: {elapsed_all:.6f}")
    log_lines.append("")
    log_lines.append("=== contrôles verts (donnée saine) ===")
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
    log_lines.append("=== déterminisme SHA256 (run1 vs run2) ===")
    for path, pair in sorted(sha_pairs.items()):
        same = pair[0] == pair[1]
        log_lines.append(f"  {path}: match={same}")
        log_lines.append(f"    run1={pair[0]}")
        log_lines.append(f"    run2={pair[1]}")
    log_lines.append("")
    log_lines.append(f"capture: {run2['capture']}")

    n_pass = sum(1 for c in checks_out if c["passed"])
    total_parts = len(built["islands_kept"]) + len(built["islands_dropped"])
    verdict = (
        f"VERDICT MESURÉ: fenêtre {built['window']['lonlat']} justifiée ; "
        f"{built['clip_counts']['land_polygons']} polygones land + "
        f"{built['clip_counts']['minor_islands']} minor_islands après découpe ; "
        f"{len(built['islands_kept'])} îles/masses gardées sur {total_parts} "
        f"(seuil {thr} km² dérivé du décrochement) ; "
        f"plus grandes écartées : {dropped_names} ; "
        f"{built['lakes_subtracted']} lacs soustraits ; "
        f"surface totale {built['land_area_km2']:.0f} km² ; "
        f"{n_pass}/{len(checks_out)} contrôles verts+mordants ; "
        f"déterminisme={'OK' if determinism_match else 'FAIL'} ; "
        f"projection={projection.epsg} ; "
        f"capture publiée"
    )
    if not (G2_LAND_AREA_KM2_MIN <= built["land_area_km2"] <= G2_LAND_AREA_KM2_MAX):
        verdict = "FAIL — " + verdict
    log_lines.append(verdict)
    log_text = "\n".join(log_lines) + "\n"
    (logs / "v1_046_coastline.log").write_bytes(log_text.encode("utf-8"))
    try:
        print(log_text)
    except UnicodeEncodeError:
        sys.stdout.buffer.write(log_text.encode("utf-8", errors="replace"))

    if not all_green_ok or not all_red_ok or not determinism_match:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
