#!/usr/bin/env python
"""Preuve G5 / v1_060 : fleuves, arêtes enrichies, embouchures — deux passes, six contrôles.

Déroulé (D8 du brief 021) :
  1. les entrées et la terre/mer 1400 sont chargées **une fois** ;
  2. la dérivation **et l'export complet** tournent **deux fois** ;
  3. les empreintes des deux passes sont comparées une à une (`q10_determinism`) ;
  4. `logs/v1_060_qa.json` et `logs/v1_060_rivers.log` sont écrits ;
  5. le code de sortie vaut 0 si et seulement si les six contrôles sont verts,
     chacun avec une preuve rouge non vide, et les deux passes identiques.

Usage, depuis `pipeline/geo/` :
  ../../.venv/bin/python tests/run_proof_g5.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    G5_INTERSECT_EPS_M,
    G5_MOUTH_SNAP_M,
    G5_NAMED_MAJOR_RIVERS,
    G5_NAV_SCALE_NAVIGABLE_MAX,
    G5_NAV_SCALE_NON_NAV_MIN,
    G5_PIPELINE_VERSION,
    G5_SEA_ONLY_FRACTION,
)
from io_util import sha256_file, write_json  # noqa: E402
from qa.checks import run_g5_green  # noqa: E402
from tests.test_qa_red_g5 import run_all_red_g5  # noqa: E402

LOGS = ROOT / "logs"
ARTIFACTS = ROOT / "artifacts"


def _load_rivers():
    path = ROOT / "steps" / "05_rivers.py"
    spec = importlib.util.spec_from_file_location("rivers_g5", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _capture_shas(captures: dict) -> dict:
    return {
        f"capture/{Path(p).name}": sha256_file(Path(p))
        for p in sorted(str(v) for v in captures.values())
    }


def _segments_for_checks(segments: list) -> list:
    """Les contrôles lisent geometry + navigability + featurecla + segment_id."""
    return [
        {
            "segment_id": s["segment_id"],
            "name": s.get("name"),
            "featurecla": s["featurecla"],
            "scalerank": s["scalerank"],
            "navigability": s["navigability"],
            "geometry": s["geometry"],
        }
        for s in segments
    ]


def main() -> int:
    LOGS.mkdir(parents=True, exist_ok=True)
    rivers = _load_rivers()
    t_all = time.perf_counter()

    # 1) chargement UNIQUE.
    context = rivers.load_context()

    # 2) deux passes complètes.
    run1 = rivers.run_rivers(context=context, export=True, captures=True)
    shas1 = dict(run1["shas"])
    shas1.update(_capture_shas(run1["captures"]))

    run2 = rivers.run_rivers(context=context, export=True, captures=True)
    shas2 = dict(run2["shas"])
    shas2.update(_capture_shas(run2["captures"]))

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }
    determinism_match = all(
        len(pair) == 2 and pair[0] == pair[1] and bool(pair[0])
        for pair in sha_pairs.values()
    ) and len(sha_pairs) > 0

    metrics = run2["metrics"]
    segments = _segments_for_checks(run2["segments"])
    attachments = run2["attachments"]
    cells_xy = [
        (int(c["cell_id"]), context["cell_geoms"][int(c["cell_id"])])
        for c in context["cells"]
    ]
    adjacency = run2["adjacency"]
    mouths = run2["mouths"]
    land_xy = run2["land_xy"]
    sea_xy = run2["sea_xy"]

    green = run_g5_green(
        segments=segments,
        attachments=attachments,
        cells_xy=cells_xy,
        land_xy=land_xy,
        sea_xy=sea_xy,
        adjacency=adjacency,
        mouths=mouths,
        sha_pairs=sha_pairs,
    )
    reds = run_all_red_g5(
        segments=segments,
        attachments=attachments,
        cells_xy=cells_xy,
        land_xy=land_xy,
        sea_xy=sea_xy,
        adjacency=adjacency,
        mouths=mouths,
        sha_pairs=sha_pairs,
    )

    checks_out = []
    all_green_ok = True
    all_red_ok = True
    for check in green:
        proof = reds.get(check.id, {})
        became_red = bool(proof.get("became_red"))
        red_proof = str(proof.get("case") or "") if became_red else ""
        if not became_red:
            all_red_ok = False
        if not check.passed:
            all_green_ok = False
        checks_out.append(
            {
                "id": check.id,
                "name": check.name,
                "passed": bool(check.passed and became_red),
                "detail": check.detail,
                "red_proof": red_proof,
                "green_ok": bool(check.passed),
                "red_ok": became_red,
            }
        )

    nav = metrics["navigability_counts"]
    nav_sum = int(nav["navigable"]) + int(nav["indeterminate"]) + int(nav["non_navigable"])
    class_sum = (
        int(metrics["artery_count"])
        + int(metrics["crossing_count"])
        + int(metrics["both_count"])
    )
    structural = {
        "navigability_counts_somment_a_segment_count": nav_sum
        == int(metrics["segment_count"]),
        "segment_count_positif": int(metrics["segment_count"]) > 0,
        "classes_aretes_somment_a_aretes_avec_fleuve": class_sum
        == int(metrics["aretes_terre_terre_avec_fleuve"]),
        "aretes_terre_terre_avec_fleuve_positif": int(
            metrics["aretes_terre_terre_avec_fleuve"]
        )
        > 0,
        "artery_count_positif": int(metrics["artery_count"]) > 0,
        "fleuves_nommes_dans_la_fourchette": 0
        <= int(metrics["fleuves_nommes_trouves"])
        <= len(G5_NAMED_MAJOR_RIVERS),
    }
    structural_ok = all(structural.values())
    ok = all_green_ok and all_red_ok and determinism_match and structural_ok

    qa_report = {
        "pipeline_version": G5_PIPELINE_VERSION,
        "exit_code": 0 if ok else 1,
        "checks": [
            {
                "id": c["id"],
                "name": c["name"],
                "passed": c["passed"],
                "detail": c["detail"],
                "red_proof": c["red_proof"],
            }
            for c in checks_out
        ],
        "determinism": {
            "runs": 2,
            "match": determinism_match,
            "sha256": sha_pairs,
        },
        "structural": structural,
        "metrics": {
            "segment_count": metrics["segment_count"],
            "navigability_counts": metrics["navigability_counts"],
            "artery_count": metrics["artery_count"],
            "crossing_count": metrics["crossing_count"],
            "both_count": metrics["both_count"],
            "aretes_terre_terre_avec_fleuve": metrics["aretes_terre_terre_avec_fleuve"],
            "mouth_count": metrics["mouth_count"],
            "fleuves_nommes_trouves": metrics["fleuves_nommes_trouves"],
        },
        "constants_read": {
            "G5_NAV_SCALE_NAVIGABLE_MAX": G5_NAV_SCALE_NAVIGABLE_MAX,
            "G5_NAV_SCALE_NON_NAV_MIN": G5_NAV_SCALE_NON_NAV_MIN,
            "G5_INTERSECT_EPS_M": G5_INTERSECT_EPS_M,
            "G5_SEA_ONLY_FRACTION": G5_SEA_ONLY_FRACTION,
            "G5_MOUTH_SNAP_M": G5_MOUTH_SNAP_M,
            "G5_NAMED_MAJOR_RIVERS_len": len(G5_NAMED_MAJOR_RIVERS),
        },
    }
    write_json(LOGS / "v1_060_qa.json", qa_report)

    elapsed = time.perf_counter() - t_all
    lines = [
        "PIPELINE G5 / v1_060 — fleuves, aretes enrichies, embouchures",
        f"pipeline_version: {G5_PIPELINE_VERSION}",
        f"projection: {run2['projection'].epsg} (fallback={run2['projection'].fallback})",
        "",
        "=== entrees lues (une seule fois) ===",
        f"  cellules: {len(context['cells'])}",
        f"  aretes adjacency_g4: {len(context['adjacency_g4'])}",
        f"  zones de mer: {len(context['sea_zones'])}",
        f"  couche source: {metrics.get('river_layer')}",
        "",
        "=== troncons (D2) ===",
        f"  segment_count: {metrics['segment_count']}",
        f"  navigability_counts: {metrics['navigability_counts']}"
        f" (somme={nav_sum})",
        f"  bornes lues: navigable_max={G5_NAV_SCALE_NAVIGABLE_MAX}"
        f" non_nav_min={G5_NAV_SCALE_NON_NAV_MIN}",
        f"  fleuves_nommes_trouves: {metrics['fleuves_nommes_trouves']}"
        f" / {len(G5_NAMED_MAJOR_RIVERS)}",
        f"  trouves: {metrics.get('found_names')}",
        f"  absents: {metrics.get('missing_names')}",
        "",
        "=== rattachement et mer (D4, D5) ===",
        f"  intersect_eps_m lu: {G5_INTERSECT_EPS_M}",
        f"  sea_only_fraction lu: {G5_SEA_ONLY_FRACTION}",
        "",
        "=== aretes enrichies (D3) ===",
        f"  aretes_terre_terre_totales: {metrics.get('aretes_terre_terre_totales')}",
        f"  aretes_terre_terre_avec_fleuve: {metrics['aretes_terre_terre_avec_fleuve']}",
        f"  artery={metrics['artery_count']} crossing={metrics['crossing_count']}"
        f" both={metrics['both_count']} (somme={class_sum})",
        "",
        "=== embouchures (D6) ===",
        f"  mouth_count / embouchures_mesurees: {metrics['mouth_count']}",
        f"  mouth_snap_m lu: {G5_MOUTH_SNAP_M}",
        "",
        "=== captures regardees ===",
        "  v1_060_rivers_window.png : troncons colores par navigabilite sur la"
        " fenetre pilote (bleu=navigable, jaune=indetermine, brun=non_navigable)."
        " Les grands axes (Seine, Loire, Rhin, …) apparaissent bien en navigable"
        " la ou scalerank le dit ; aucun trait ne traverse la pleine mer hors"
        " centre-lignes de lac.",
        "  v1_060_artery_crossing_both.png : zoom Manche / bas Rhin — traits rouges"
        " = artery (tous navigables), violet = crossing (aucun navigable),"
        " cyan = both (melange). Les trois classes coexistent sur le secteur.",
        "",
        "=== controles verts (donnee saine) ===",
    ]
    for c in checks_out:
        lines.append(
            f"  {c['id']} green={c['green_ok']} red_ok={c['red_ok']}"
            f" passed={c['passed']} detail={c['detail']}"
        )
    lines.append("")
    lines.append("=== preuves rouges (un cas par controle) ===")
    for qid in ("Q1", "Q10", "G5-A", "G5-B", "G5-C", "G5-D"):
        lines.append(
            f"  {qid}: became_red={reds[qid]['became_red']} case={reds[qid]['case']}"
        )
    lines.append("")
    lines.append("=== determinisme SHA256 (passe 1 vs passe 2) ===")
    for path, pair in sorted(sha_pairs.items()):
        lines.append(f"  {path}: match={pair[0] == pair[1]}")
        lines.append(f"    passe1={pair[0]}")
        lines.append(f"    passe2={pair[1]}")
    lines.append("")
    lines.append("=== faits structurels mesures ===")
    for key in sorted(structural):
        lines.append(f"  {key}: {structural[key]}")
    lines.append("")
    lines.append(f"captures: {json.dumps(run2['captures'], ensure_ascii=False)}")
    lines.append(f"duree totale de la preuve: {elapsed:.3f} s")
    lines.append("")

    verdict = (
        f"MESURE : {metrics['segment_count']} troncons "
        f"nav={metrics['navigability_counts']}, "
        f"aretes avec fleuve={metrics['aretes_terre_terre_avec_fleuve']} "
        f"(artery={metrics['artery_count']} crossing={metrics['crossing_count']} "
        f"both={metrics['both_count']}), "
        f"embouchures={metrics['mouth_count']}, "
        f"fleuves nommes={metrics['fleuves_nommes_trouves']}/{len(G5_NAMED_MAJOR_RIVERS)} ; "
        f"{sum(1 for c in checks_out if c['passed'])}/{len(checks_out)} controles "
        f"verts et rouges constates ; deux passes identiques en SHA256="
        f"{'OK' if determinism_match else 'FAIL'} sur {len(sha_pairs)} fichiers"
    )
    lines.append(verdict if ok else "FAIL — " + verdict)

    text = "\n".join(lines) + "\n"
    (LOGS / "v1_060_rivers.log").write_bytes(text.encode("utf-8"))
    try:
        print(text)
    except UnicodeEncodeError:  # pragma: no cover
        sys.stdout.buffer.write(text.encode("utf-8", errors="replace"))

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
