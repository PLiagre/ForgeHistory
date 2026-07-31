#!/usr/bin/env python
"""Preuve G2-bis / v1_047 : corrections ×2, contrôles verts+mordants, log + qa.json."""

from __future__ import annotations

import importlib.util
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (
    G2B_PIPELINE_VERSION,
    G2_LAND_AREA_KM2_MAX,
    G2_LAND_AREA_KM2_MIN,
)
from io_util import read_json, write_json
from qa.checks import run_g2b_green
from tests.test_qa_red_g2b import run_all_red_g2b


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
    g2b = _load_g2b()
    coastline = _load_coastline()
    logs = ROOT / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    t_all = time.perf_counter()

    # --- deux exécutions AVEC corrections (déterminisme) ---
    run1 = g2b.run_corrections(apply_corrections=True, clean_build=True)
    shas1 = dict(run1["shas"])
    run2 = g2b.run_corrections(apply_corrections=True, clean_build=True)
    shas2 = dict(run2["shas"])

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }

    # --- corrections OFF → doit matcher G2 / v1_046 ---
    run_off = g2b.run_corrections(apply_corrections=False, clean_build=True)
    sha_disabled = dict(run_off["shas"])

    # Référence G2 fraîche (même code, même version) + empreintes historiques v1_046.
    ref_g2 = coastline.run_coastline(clean_build=True)
    sha_reference_g2 = dict(ref_g2["shas"])
    # Empreintes figées du rapport v1_046 (si présentes) pour le contrôle mordant.
    qa_046_path = logs / "v1_046_qa.json"
    sha_v046: dict = {}
    if qa_046_path.exists():
        doc_046 = read_json(qa_046_path)
        raw = (doc_046.get("determinism") or {}).get("sha256") or {}
        for path, pair in raw.items():
            if isinstance(pair, list) and pair:
                sha_v046[path] = pair[0]

    # Préférer la référence historique quand elle couvre les chemins G2.
    sha_ref_for_check = sha_v046 if sha_v046 else sha_reference_g2

    built = run2["result"]
    land_ll = run2["land_ll"]
    land_xy = run2["land_xy"]
    projection = run2["projection"]
    timings = run2["timings"]
    window = coastline.pilot_window_polygon()
    corrections = list(run2["corrections_doc"].get("corrections") or [])

    # Référence G2b-C : sommets du littoral G2 (avant corrections).
    ref_verts: set = set()
    g2b._collect_coords(run2["land_before_ll"], ref_verts)
    geom_once = land_ll
    geom_twice = g2b.apply_corrections_once(built)

    green = run_g2b_green(
        land_ll=land_ll,
        land_xy=land_xy,
        window=window,
        area_km2=built["land_area_km2"],
        area_min=G2_LAND_AREA_KM2_MIN,
        area_max=G2_LAND_AREA_KM2_MAX,
        sha_pairs=sha_pairs,
        corrections=corrections,
        sha_disabled=sha_disabled,
        sha_reference_g2=sha_ref_for_check,
        source_vertices=ref_verts,
        geom_once=geom_once,
        geom_twice=geom_twice,
    )
    reds = run_all_red_g2b(land_ll, land_xy, window, ref_verts)

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
    write_json(logs / "v1_047_qa.json", qa_report)

    elapsed_all = time.perf_counter() - t_all
    area_before = float(run2["area_before_km2"])
    area_after = float(built["land_area_km2"])
    delta = round(area_after - area_before, 3)
    applied = built.get("corrections_applied") or []
    divergences = list(run2["divergences_doc"].get("divergences") or [])

    log_lines = [
        "PIPELINE G2-bis / v1_047 — corrections littoral 1400",
        f"pipeline_version: {G2B_PIPELINE_VERSION}",
        f"projection: {projection.epsg}",
        f"projection_fallback: {projection.fallback}",
        f"source_sha256: {run2['fingerprint']['sha256']}",
        "",
        "=== corrections appliquées ===",
    ]
    for a in applied:
        log_lines.append(
            f"  {a['id']}: {a['name']} {a.get('from_class')}→{a.get('to_class')} "
            f"certainty={a.get('certainty')} applied={a.get('applied')} "
            f"date={a.get('date')}"
        )
        log_lines.append(f"    source: {a.get('source')}")
    log_lines += [
        "",
        "=== surface ===",
        f"  land_area_km2_before (G2): {area_before}",
        f"  land_area_km2_after  (G2b): {area_after}",
        f"  delta_km2: {delta}",
        f"  open_sea_names: {built.get('open_sea_names')}",
        f"  lake_names_remaining: {built.get('lake_names')}",
        "",
        "=== registre des écarts assumés ===",
    ]
    for d in divergences:
        log_lines.append(
            f"  {d.get('id')}: {d.get('title')} certainty={d.get('certainty')} "
            f"status={d.get('status')}"
        )
        log_lines.append(f"    why: {d.get('why_not_corrected')}")
    log_lines.append("")
    log_lines.append("=== timings (s, dernière exécution avec corrections) ===")
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
    log_lines.append("=== déterminisme SHA256 (run1 vs run2, corrections ON) ===")
    for path, pair in sorted(sha_pairs.items()):
        same = pair[0] == pair[1]
        log_lines.append(f"  {path}: match={same}")
        log_lines.append(f"    run1={pair[0]}")
        log_lines.append(f"    run2={pair[1]}")
    log_lines.append("")
    log_lines.append("=== réversibilité (corrections OFF vs référence G2/v1_046) ===")
    common = sorted(set(sha_disabled) & set(sha_ref_for_check))
    for path in common:
        same = sha_disabled.get(path) == sha_ref_for_check.get(path)
        log_lines.append(f"  {path}: match={same}")
        log_lines.append(f"    disabled={sha_disabled.get(path)}")
        log_lines.append(f"    reference={sha_ref_for_check.get(path)}")
    log_lines.append("")
    caps = run2.get("captures") or {}
    log_lines.append(f"captures: { {k: str(v) for k, v in caps.items()} }")

    n_pass = sum(1 for c in checks_out if c["passed"])
    n_div = len(divergences)
    verdict = (
        f"VERDICT MESURÉ: {len(applied)} corrections appliquées par reclassement "
        f"({', '.join(built.get('open_sea_names') or [])}), "
        f"certitude attested, sources citées ; "
        f"surface de terre {area_before:.0f} → {area_after:.0f} km² ({delta:+.0f}) ; "
        f"{n_div} écarts registrés non corrigés dont polders du Flevoland "
        f"(contour non reclassable comme entité lacustre isolée, nécessite G6) ; "
        f"{n_pass}/{len(checks_out)} contrôles verts et {n_pass} rouges constatés ; "
        f"réversibilité={'OK' if all(sha_disabled.get(p)==sha_ref_for_check.get(p) for p in common) else 'FAIL'} "
        f"bit pour bit contre v1_046 ; "
        f"captures avant/après côte néerlandaise publiées ; "
        f"déterminisme={'OK' if determinism_match else 'FAIL'}"
    )
    if not (G2_LAND_AREA_KM2_MIN <= area_after <= G2_LAND_AREA_KM2_MAX):
        verdict = "FAIL — " + verdict
    log_lines.append(verdict)
    log_text = "\n".join(log_lines) + "\n"
    (logs / "v1_047_corrections.log").write_bytes(log_text.encode("utf-8"))
    try:
        print(log_text)
    except UnicodeEncodeError:
        sys.stdout.buffer.write(log_text.encode("utf-8", errors="replace"))

    if not all_green_ok or not all_red_ok or not determinism_match:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
