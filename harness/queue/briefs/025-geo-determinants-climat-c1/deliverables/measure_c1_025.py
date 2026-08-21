#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 025 (C1 — déterminants climat).

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/measure_c1_025.py
"""

from __future__ import annotations

import argparse
import difflib
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, List, Sequence, Tuple

REPO = Path(__file__).resolve().parents[5]
GEO = REPO / "pipeline" / "geo"
ART = GEO / "artifacts"
LOGS = GEO / "logs"
CAPTURE = GEO / "capture"
REGISTRY = GEO / "registry"
BRIEF = REPO / "harness" / "queue" / "briefs" / "025-geo-determinants-climat-c1"
PRE = BRIEF / "deliverables" / "pre-edit"
PY = REPO / ".venv" / "bin" / "python"

NOT_COMPUTED = -1
BASE_REF = "origin/master"
ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} a échoué (code {proc.returncode}): "
            f"{(proc.stderr or proc.stdout or '').strip()}"
        )
    return proc.stdout


def load_constants_namespace(path: Path) -> Dict[str, Any]:
    """Charge constants en forçant __file__ sur pipeline/geo/constants.py."""
    fake = GEO / "constants.py"
    ns: Dict[str, Any] = {"__name__": "constants_snapshot", "__file__": str(fake)}
    sys.path.insert(0, str(GEO))
    try:
        exec(compile(path.read_text(encoding="utf-8"), str(fake), "exec"), ns)
    finally:
        if str(GEO) in sys.path:
            sys.path.remove(str(GEO))
    return ns


def count_deleted_lines(orig: Path, current: Path) -> int:
    orig_lines = orig.read_text(encoding="utf-8").splitlines(keepends=True)
    cur_lines = current.read_text(encoding="utf-8").splitlines(keepends=True)
    deleted = 0
    for line in difflib.unified_diff(orig_lines, cur_lines, lineterm=""):
        if line.startswith("---") or line.startswith("+++") or line.startswith("@@"):
            continue
        if line.startswith("-"):
            deleted += 1
    return deleted


def consecutive_pairs_by_lat(
    cells_g3: Sequence[dict],
    climate: Sequence[dict],
    monotone_dlat: float,
) -> Tuple[int, int, int, int]:
    lat_by = {int(c["cell_id"]): float(c["centroid"]["lat"]) for c in cells_g3}
    insol_by = {int(c["cell_id"]): float(c["insolation_annual_mj_m2"]) for c in climate}
    ordered = sorted(lat_by.keys(), key=lambda cid: lat_by[cid])

    inversions = 0
    equal_bad = 0
    above = 0
    total_pairs = max(0, len(ordered) - 1)
    for i in range(1, len(ordered)):
        prev_id, cur_id = ordered[i - 1], ordered[i]
        dlat = lat_by[cur_id] - lat_by[prev_id]
        prev_i, cur_i = insol_by[prev_id], insol_by[cur_id]
        if cur_i > prev_i:
            inversions += 1
        if dlat >= monotone_dlat:
            above += 1
            if cur_i >= prev_i:
                equal_bad += 1
    return inversions, equal_bad, above, total_pairs


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    sys.path.insert(0, str(GEO))
    import constants as C  # noqa: E402

    cells_g3 = load(ART / "cells_g3.json")["cells"]
    climate_doc = load(ART / "cells_climate_drivers_c1.json")
    climate = climate_doc["cells"]
    stats = load(ART / "stats_c1.json")
    qa = load(LOGS / "v1_080_qa.json")
    stats_g4 = load(ART / "stats_g4.json")

    cell_count_g3 = len(cells_g3)
    report(
        "cellules_avec_insolation",
        sum(1 for c in climate if c.get("insolation_annual_mj_m2") is not None),
        f"{cell_count_g3} cellules lues de cells_g3.json",
    )

    inv, eq_bad, above, total_pairs = consecutive_pairs_by_lat(
        cells_g3, climate, C.C1_MONOTONE_DLAT_DEG
    )
    report("inversions_insolation_latitude", inv, f"{total_pairs} paires consecutives")
    report("egalites_insolation_hors_tolerance", eq_bad, f"{above} paires au-dessus du seuil")
    report(
        "paires_consecutives_au_dessus_du_seuil",
        above,
        f"{total_pairs} paires consecutives totales",
    )

    summer_bad = sum(
        1
        for c in climate
        if float(c["daylight_h_summer_solstice"]) <= float(c["daylight_h_winter_solstice"])
    )
    report("cellules_jour_ete_non_superieur_hiver", summer_bad, f"{len(climate)} cellules")

    amp_inv = 0
    lat_by = {int(c["cell_id"]): float(c["centroid"]["lat"]) for c in cells_g3}
    amp_by = {
        int(c["cell_id"]): float(c["daylight_h_summer_solstice"])
        - float(c["daylight_h_winter_solstice"])
        for c in climate
    }
    ordered = sorted(lat_by.keys(), key=lambda cid: lat_by[cid])
    for i in range(1, len(ordered)):
        if amp_by[ordered[i]] < amp_by[ordered[i - 1]]:
            amp_inv += 1
    report(
        "inversions_amplitude_jour_latitude",
        amp_inv,
        f"{max(0, len(ordered) - 1)} paires consecutives",
    )

    report(
        "ecretages_polaires_total",
        int(stats["ecretages_polaires_total"]),
        f"{len(climate)} x {C.C1_DAYS_IN_YEAR} jours-cellule evalues",
    )
    report(
        "coastal_cell_count_derive",
        int(stats["coastal_cell_count_derive"]),
        f"{len(climate)} cellules totales",
    )
    report(
        "ecart_littoralite_c1_vs_g4",
        abs(int(stats["coastal_cell_count_derive"]) - int(stats_g4["coastal_cell_count"])),
        "1 comparaison avec stats_g4.json",
    )

    coastal_ids = {int(c["cell_id"]) for c in climate if c.get("coastal")}
    coastal_bad = sum(
        1
        for c in climate
        if int(c["cell_id"]) in coastal_ids
        and float(c["dist_sea_edge_m"]) > C.C1_SEA_DISTANCE_EPS_M
    )
    report(
        "cellules_littorales_hors_epsilon",
        coastal_bad,
        f"{len(coastal_ids)} cellules littorales derivees",
    )
    report(
        "contact_ponctuel_sans_arete_land_sea",
        int(stats["contact_ponctuel_sans_arete_land_sea"]),
        f"{len(climate) - len(coastal_ids)} cellules non littorales",
    )

    sea_ids = {int(z["zone_id"]) for z in load(ART / "sea_zones_g4.json")["sea_zones"]}
    unknown = sum(1 for c in climate if int(c["nearest_sea_zone_id"]) not in sea_ids)
    report("zones_de_mer_inconnues", unknown, f"{len(climate)} cellules totales")

    sans_hops = sum(1 for c in climate if int(c.get("hops_to_sea", -1)) < 0)
    report("cellules_sans_hops", sans_hops, f"{len(climate)} cellules totales")
    report(
        "cellules_atteintes_par_strait_seulement",
        int(stats["cellules_atteintes_par_strait_seulement"]),
        f"{len(climate)} cellules totales",
    )

    by_hop: Dict[int, List[float]] = {}
    for c in climate:
        h = int(c["hops_to_sea"])
        if h >= 0:
            by_hop.setdefault(h, []).append(float(c["dist_sea_centroid_m"]))
    hop_keys = sorted(by_hop.keys())
    non_mono = 0
    for i in range(1, len(hop_keys)):
        prev = sorted(by_hop[hop_keys[i - 1]])
        cur = sorted(by_hop[hop_keys[i]])
        pm = prev[len(prev) // 2]
        cm = cur[len(cur) // 2]
        if cm <= pm:
            non_mono += 1
    report(
        "classes_de_sauts_non_monotones",
        non_mono,
        f"{max(0, len(hop_keys) - 1)} classes non vides moins une",
    )
    report(
        "cellules_centroide_hors_polygone",
        int(stats["cellules_centroide_hors_polygone"]),
        f"{len(climate)} cellules totales",
    )
    violations = sum(
        1
        for c in climate
        if c.get("centroid_inside_cell")
        and float(c["dist_sea_edge_m"]) > float(c["dist_sea_centroid_m"]) + C.C1_SEA_DISTANCE_EPS_M
    )
    inside_count = sum(1 for c in climate if c.get("centroid_inside_cell"))
    report(
        "violations_bord_vs_centroide",
        violations,
        f"{inside_count} cellules a centroide interieur",
    )

    artifact_docs = [
        climate_doc,
        stats,
        load(ART / "MANIFEST_c1.json"),
        load(REGISTRY / "climate_drivers_registry.json"),
    ]
    bad_keys: List[str] = []

    def walk(obj: Any, path: str = "") -> None:
        if isinstance(obj, dict):
            for k, v in obj.items():
                key = str(k)
                here = f"{path}.{key}" if path else key
                if key in C.WORLD_TERMS_FORBIDDEN_KEYS:
                    bad_keys.append(here)
                walk(v, here)
        elif isinstance(obj, list):
            for i, item in enumerate(obj[:50]):
                walk(item, f"{path}[{i}]")

    for doc in artifact_docs:
        walk(doc)
    report(
        "cles_de_bareme_trouvees",
        len(bad_keys),
        f"{len(C.WORLD_TERMS_FORBIDDEN_KEYS)} cles du frozenset",
    )

    checks = qa.get("checks") or []
    verts = sum(1 for c in checks if c.get("passed"))
    reds = sum(1 for c in checks if str(c.get("red_proof") or "").strip())
    report("controles_c1_verts", verts, "7")
    report("controles_c1_avec_preuve_rouge_non_vide", reds, "7")

    sha_block = (qa.get("determinism") or {}).get("sha256") or {}
    equal = sum(
        1
        for pair in sha_block.values()
        if isinstance(pair, list) and len(pair) == 2 and pair[0] and pair[0] == pair[1]
    )
    report(
        "paires_sha_determinisme_egales",
        equal,
        f"{len(sha_block)} paires du bloc determinism.sha256",
    )

    if args.rerun_proof:
        proc = subprocess.run(
            [str(PY), "tests/run_proof_c1.py"],
            cwd=GEO,
            capture_output=True,
            text=True,
        )
        report("code_sortie_run_proof_c1", proc.returncode, "1 execution de tests/run_proof_c1.py")
    else:
        report(
            "code_sortie_run_proof_c1",
            int(qa.get("exit_code", NOT_COMPUTED)),
            "exit_code dans logs/v1_080_qa.json",
        )

    const_del = count_deleted_lines(PRE / "constants.py.orig", GEO / "constants.py")
    report("constants_lignes_supprimees", const_del, "1 mesure diff vs pre-edit")

    pre_c = load_constants_namespace(PRE / "constants.py.orig")
    pub_c = load_constants_namespace(GEO / "constants.py")
    pre_names = [k for k in pre_c.keys() if not k.startswith("_") and k.isupper()]
    unchanged = sum(1 for k in pre_names if pre_c.get(k) == pub_c.get(k))
    report(
        "constantes_preexistantes_inchangees",
        unchanged,
        f"{len(pre_names)} noms de premier niveau dans l instantane pre-edition",
    )

    pipe_del = count_deleted_lines(PRE / "pipeline.py.orig", GEO / "pipeline.py")
    report("pipeline_lignes_supprimees", pipe_del, "1 mesure diff vs pre-edit")

    orig_pipe = (PRE / "pipeline.py.orig").read_text(encoding="utf-8")
    pub_pipe = (GEO / "pipeline.py").read_text(encoding="utf-8")
    branch_pat = re.compile(r"if args\.source == \"([^\"]+)\":")
    orig_branches = {
        m.group(0): orig_pipe[m.start():orig_pipe.find("\n", m.start()) + 1]
        for m in branch_pat.finditer(orig_pipe)
    }
    matching = sum(1 for stmt in orig_branches if stmt in pub_pipe)
    report(
        "branches_source_preexistantes_identiques",
        matching,
        f"{len(orig_branches)} branches if args.source du pre-edit (ecart brief 8 vs 7+repli documente)",
    )

    orig_choices = re.search(
        r"choices=\[([^\]]+)\]", (PRE / "pipeline.py.orig").read_text(encoding="utf-8")
    )
    orig_vals = re.findall(r'"([^"]+)"', orig_choices.group(1) if orig_choices else "")
    pub_choices = re.search(r"choices=\[([^\]]+)\]", pub_pipe)
    pub_vals = re.findall(r'"([^"]+)"', pub_choices.group(1) if pub_choices else "")
    conserved = sum(1 for v in orig_vals if v in pub_vals)
    report(
        "valeurs_source_preexistantes_conservees",
        conserved,
        f"{len(orig_vals)} valeurs choices d origine (+ climate_drivers ajoutee)",
    )

    climate_as_source = len(re.findall(r'args\.source == "climate"', pub_pipe))
    report(
        "source_climate_non_employee",
        int(climate_as_source == 0),
        "1 si absent comme valeur de --source",
    )

    prev_artifacts = [
        "pipeline/geo/artifacts/cells_g3.json",
        "pipeline/geo/artifacts/adjacency_g4.json",
        "pipeline/geo/artifacts/adjacency_g5.json",
        "pipeline/geo/artifacts/sea_zones_g4.json",
        "pipeline/geo/artifacts/stats_g3.json",
        "pipeline/geo/artifacts/stats_g4.json",
        "pipeline/geo/artifacts/stats_g5.json",
        "pipeline/geo/artifacts/rivers_g5.json",
        "pipeline/geo/artifacts/mouths_g5.json",
        "pipeline/geo/artifacts/adjacency_g3.json",
        "pipeline/geo/artifacts/topology_links_g4.json",
        "pipeline/geo/artifacts/adjacency_divergence_g4.json",
        "pipeline/geo/artifacts/MANIFEST_g3.json",
        "pipeline/geo/artifacts/MANIFEST_g4.json",
        "pipeline/geo/artifacts/MANIFEST_g5.json",
    ]
    porcelain = subprocess.run(
        ["git", "status", "--porcelain", *prev_artifacts],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    modified = [l for l in porcelain.stdout.splitlines() if l.strip()]
    report("artefacts_precedents_modifies", len(modified), "15 artefacts G3/G4/G5")

    declared_proofs = [
        "pipeline/geo/steps/c1_climate_drivers.py",
        "pipeline/geo/qa/checks_c1.py",
        "pipeline/geo/tests/run_proof_c1.py",
        "pipeline/geo/tests/test_qa_red_c1.py",
        "pipeline/geo/artifacts/cells_climate_drivers_c1.json",
        "pipeline/geo/artifacts/stats_c1.json",
        "pipeline/geo/artifacts/MANIFEST_c1.json",
        "pipeline/geo/registry/climate_drivers_registry.json",
        "pipeline/geo/logs/v1_080_qa.json",
        "pipeline/geo/logs/v1_080_climate_drivers.log",
        "pipeline/geo/capture/v1_080_insolation_window.png",
        "pipeline/geo/capture/v1_080_continentality_window.png",
    ]
    try:
        tracked = set(git("ls-files", *declared_proofs).splitlines())
        report(
            "fichiers_preuve_suivis_par_git",
            len(tracked),
            f"{len(declared_proofs)} preuves declarees (git ls-files)",
        )
    except RuntimeError as exc:
        report("fichiers_preuve_suivis_par_git", NOT_COMPUTED, str(exc))

    if args.no_pytest:
        report("tests_harness_passed_025", NOT_COMPUTED, "non execute (--no-pytest)")
    else:
        proc = subprocess.run(
            [str(PY), "-m", "pytest", "harness/tests/", "-q"],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        tail = proc.stdout.strip().splitlines()[-1] if proc.stdout.strip() else ""
        passed = int((re.search(r"(\d+) passed", tail) or [0, 0])[1]) if "passed" in tail else 0
        skipped = int((re.search(r"(\d+) skipped", tail) or [0, 0])[1]) if "skipped" in tail else 0
        failed = int((re.search(r"(\d+) failed", tail) or [0, 0])[1]) if "failed" in tail else 0
        collected = passed + skipped + failed
        report(
            "tests_harness_passed_025",
            passed,
            f"{collected} tests collectes harness/tests/ ({skipped} SKIP, {failed} echecs)",
        )

    if args.json:
        print(json.dumps([{"name": n, "value": v, "denominator": d} for n, v, d in ROWS], indent=2))
    else:
        for name, value, denom in ROWS:
            print(f"{name} = {value}  ({denom})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
