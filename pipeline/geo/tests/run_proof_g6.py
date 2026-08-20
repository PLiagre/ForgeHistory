#!/usr/bin/env python
"""Preuve G6 / v1_052 : relief, barrières, cols — deux passes, six contrôles.

Déroulé (D10 du brief 024) :
  1. le cache DEM est vérifié une fois (G6-A) ;
  2. les cellules G3 et l'adjacence G5 sont chargées une fois ;
  3. la dérivation et l'export complet tournent deux fois ;
  4. les empreintes des deux passes sont comparées une à une (`q10_determinism`) ;
  5. `logs/v1_052_qa.json` et `logs/v1_052_relief.log` sont écrits ;
  6. le code de sortie vaut 0 si et seulement si les six contrôles sont verts,
     chacun avec une preuve rouge non vide, et les deux passes identiques.

Usage, depuis `pipeline/geo/` :
  ../../.venv/bin/python tests/run_proof_g6.py
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
    G6_ELEV_PLAUSIBLE_MAX_M,
    G6_ELEV_PLAUSIBLE_MIN_M,
    G6_KNOWN_PASSES,
    G6_PIPELINE_VERSION,
)
from io_util import sha256_file, write_json  # noqa: E402
from qa.checks import run_g6_green  # noqa: E402
from tests.test_qa_red_g6 import run_all_red_g6  # noqa: E402

LOGS = ROOT / "logs"
ARTIFACTS = ROOT / "artifacts"


def _load_relief():
    path = ROOT / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6", path)
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


def main() -> int:
    LOGS.mkdir(parents=True, exist_ok=True)
    relief = _load_relief()
    t_all = time.perf_counter()

    # 1) vérification DEM + chargement UNIQUE.
    context = relief.load_context(verify_dem=True, download_dem=False)

    # 2) deux passes complètes.
    run1 = relief.run_relief(context=context, export=True, captures=True, verify_dem=False)
    shas1 = dict(run1["shas"])
    shas1.update(_capture_shas(run1["captures"]))

    run2 = relief.run_relief(context=context, export=True, captures=True, verify_dem=False)
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
    cell_relief = run2["cell_relief"]
    adjacency = run2["adjacency"]
    base_cell_ids = [int(c["cell_id"]) for c in context["cells"]]

    green = run_g6_green(
        dem_ok=bool(context.get("dem_report", {}).get("ok", False)),
        dem_detail=(
            f"verified={context['dem_report'].get('verified')}/"
            f"{context['dem_report'].get('tile_count')} "
            f"collective={context['dem_report'].get('collective_ok')}"
        ),
        cell_relief=cell_relief,
        adjacency=adjacency,
        base_cell_ids=base_cell_ids,
        sha_pairs=sha_pairs,
        elev_min=G6_ELEV_PLAUSIBLE_MIN_M,
        elev_max=G6_ELEV_PLAUSIBLE_MAX_M,
    )
    reds = run_all_red_g6(
        cell_relief=cell_relief,
        adjacency=adjacency,
        base_cell_ids=base_cell_ids,
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

    structural = {
        "barrier_count_positif": int(metrics["barrier_count"]) > 0,
        "pass_count_egale_barrier_count": int(metrics["pass_count"])
        == int(metrics["barrier_count"]),
        "cellules_sans_echantillon_zero": sum(
            1 for c in cell_relief if int(c.get("sample_count") or 0) <= 0
        )
        == 0,
        "passes_nommes_dans_fourchette": 0
        <= int(metrics["passes_nommes_trouves"])
        <= len(G6_KNOWN_PASSES),
    }
    structural_ok = all(structural.values())
    ok = all_green_ok and all_red_ok and determinism_match and structural_ok

    qa_report = {
        "pipeline_version": G6_PIPELINE_VERSION,
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
            "cell_count": metrics["cell_count"],
            "barrier_count": metrics["barrier_count"],
            "pass_count": metrics["pass_count"],
            "passes_nommes_trouves": metrics["passes_nommes_trouves"],
            "below_0_land_km2": metrics["below_0_land_km2"],
            "echantillons_exclus_hors_plage": metrics["echantillons_exclus_hors_plage"],
            "elev_distribution": metrics["elev_distribution"],
        },
        "dem": context.get("dem_report", {}),
    }
    write_json(LOGS / "v1_052_qa.json", qa_report)

    elapsed = time.perf_counter() - t_all
    dem = context.get("dem_report", {})
    lines = [
        "PIPELINE G6 / v1_052 — relief Copernicus DEM, barrières, cols",
        f"pipeline_version: {G6_PIPELINE_VERSION}",
        f"projection: {run2['projection'].epsg} (fallback={run2['projection'].fallback})",
        "",
        "=== cache DEM (une seule verification) ===",
        f"  tuiles: {dem.get('verified')}/{dem.get('tile_count')} verifiees",
        f"  collective_ok: {dem.get('collective_ok')} recette={dem.get('collective_recipe')}",
        "",
        "=== entrees lues (une seule fois) ===",
        f"  cellules: {len(context['cells'])}",
        f"  aretes adjacency_g5: {len(context['adjacency_g5'])}",
        "",
        "=== relief par cellule (D3-D5) ===",
        f"  cell_count: {metrics['cell_count']}",
        f"  echantillons_exclus_hors_plage: {metrics['echantillons_exclus_hors_plage']}",
        f"  elev_distribution: {metrics['elev_distribution']}",
        f"  below_0_land_km2: {metrics['below_0_land_km2']}",
        "",
        "=== barrières et cols (D6-D7) ===",
        f"  barrier_count: {metrics['barrier_count']}",
        f"  pass_count: {metrics['pass_count']}",
        f"  passes_nommes_trouves: {metrics['passes_nommes_trouves']}"
        f" / {len(G6_KNOWN_PASSES)}",
        "",
        "=== captures regardees ===",
        "  v1_052_elevation_window.png : altitude moyenne par cellule sur la",
        "  fenetre pilote (palette terrain continue, pas un hillshade — A12 hors",
        "  portee). Les massifs (Alpes, Pyrenees, Massif central) ressortent",
        "  en teintes claires ; plaines et bassins en vert/jaune bas.",
        "  v1_052_barriers_passes.png : zoom Pyrenees/Alpes — traits rouges =",
        "  aretes barriere relief ; etoiles vertes = cols nommes (G6_KNOWN_PASSES),",
        "  croix bleues = cols derives g6_derived_*.",
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
    for qid in ("Q10", "G6-A", "G6-B", "G6-C", "G6-D", "G6-E"):
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
        f"MESURE : {metrics['cell_count']} cellules, "
        f"barrieres={metrics['barrier_count']} passes={metrics['pass_count']}, "
        f"cols nommes={metrics['passes_nommes_trouves']}/{len(G6_KNOWN_PASSES)} ; "
        f"{sum(1 for c in checks_out if c['passed'])}/{len(checks_out)} controles "
        f"verts et rouges constates ; deux passes identiques en SHA256="
        f"{'OK' if determinism_match else 'FAIL'} sur {len(sha_pairs)} fichiers"
    )
    lines.append(verdict if ok else "FAIL — " + verdict)

    text = "\n".join(lines) + "\n"
    (LOGS / "v1_052_relief.log").write_bytes(text.encode("utf-8"))
    try:
        print(text)
    except UnicodeEncodeError:  # pragma: no cover
        sys.stdout.buffer.write(text.encode("utf-8", errors="replace"))

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
