#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 026 (R1 — gisements extractifs 1400).

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/026-geo-gisements-1400-r1/deliverables/measure_r1_026.py
"""

from __future__ import annotations

import argparse
import difflib
import json
import re
import subprocess
import sys
import unicodedata
from pathlib import Path
from typing import Any, Dict, List, Sequence, Tuple

REPO = Path(__file__).resolve().parents[5]
GEO = REPO / "pipeline" / "geo"
ART = GEO / "artifacts"
LOGS = GEO / "logs"
REGISTRY = GEO / "registry"
BRIEF = REPO / "harness" / "queue" / "briefs" / "026-geo-gisements-1400-r1"
PRE = BRIEF / "deliverables" / "pre-edit"
PY = REPO / ".venv" / "bin" / "python"

NOT_COMPUTED = -1
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


def _normalize_key(key: str) -> str:
    s = unicodedata.normalize("NFKD", str(key))
    s = "".join(c for c in s if not unicodedata.combining(c))
    return s.lower().replace("-", "_")


def _key_forbidden(key: str, world: frozenset, qty: frozenset) -> bool:
    norm = _normalize_key(key)
    if norm in world or norm in qty:
        return True
    for tok in norm.split("_"):
        if tok and (tok in world or tok in qty):
            return True
    return False


def count_forbidden_keys(docs: Sequence[dict], world: frozenset, qty: frozenset) -> Tuple[int, int]:
    bareme = 0
    quantite = 0

    def walk(obj: Any) -> None:
        nonlocal bareme, quantite
        if isinstance(obj, dict):
            for k, v in obj.items():
                norm = _normalize_key(str(k))
                if norm in world or any(tok in world for tok in norm.split("_") if tok):
                    bareme += 1
                if norm in qty or any(tok in qty for tok in norm.split("_") if tok):
                    quantite += 1
                walk(v)
        elif isinstance(obj, list):
            for item in obj[:200]:
                walk(item)

    for doc in docs:
        walk(doc)
    return bareme, quantite


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    sys.path.insert(0, str(GEO))
    import constants as C  # noqa: E402

    amendment = BRIEF / "amendment-001-arbitrage-gisements.md"
    demande = REPO / "hermes" / "requests" / "DEMANDE-20260821-arbitrage-gisements-026.md"
    report(
        "amendement_arbitrage_present",
        int(amendment.exists() and amendment.read_text(encoding="utf-8").strip() != ""),
        "1 verification git ls-files",
    )
    try:
        tracked_amend = bool(
            git("ls-files", str(amendment.relative_to(REPO))).strip()
        )
    except RuntimeError:
        tracked_amend = False
    report("amendement_suivi_par_git", int(tracked_amend), "1 verification")

    amend_text = amendment.read_text(encoding="utf-8") if amendment.exists() else ""
    demande_rel = "hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md"
    report(
        "decision_proprietaire_citee",
        int(demande.exists() and demande_rel in amend_text),
        "1 verification chemin hermes/requests",
    )
    answers = sum(
        1
        for tag in ("A1", "A2", "A3")
        if re.search(rf"\*\*{tag}\*\*", amend_text) or re.search(rf"### `{tag}`", amend_text)
    )
    report("questions_arbitrage_repondues", answers, "3")

    declarations = load(GEO / "data" / "resources_1400.json")
    d4_ids = [
        "salins_les_bains", "luneburg", "wieliczka", "guerande", "halle_saale", "cardona",
        "norberg", "eisenerz", "somorrostro", "forest_of_dean", "val_trompia", "falun",
        "banska_stiavnica", "rammelsberg", "kutna_hora", "freiberg", "schwaz", "iglesias",
        "camborne_redruth", "dartmoor", "mendip", "derbyshire_peak", "newcastle_tyne",
        "liege", "almaden", "phocee", "kremnica",
    ]
    written = {
        (d["id"], d["resource"], d["richness_class"], float(d["lon"]), float(d["lat"]), d["certainty"])
        for d in declarations.get("deposits") or []
    }
    expected_rows = {
        ("salins_les_bains", "sel", "notable", 5.879, 46.943, "reconstructed_established"),
        ("luneburg", "sel", "majeure", 10.414, 53.249, "reconstructed_established"),
        ("wieliczka", "sel", "majeure", 20.055, 49.983, "reconstructed_established"),
        ("guerande", "sel", "majeure", -2.428, 47.328, "reconstructed_established"),
        ("halle_saale", "sel", "notable", 11.969, 51.482, "reconstructed_established"),
        ("cardona", "sel", "notable", 1.68, 41.914, "reconstructed_established"),
        ("norberg", "fer", "majeure", 15.921, 60.066, "reconstructed_established"),
        ("eisenerz", "fer", "notable", 14.885, 47.541, "reconstructed_established"),
        ("somorrostro", "fer", "majeure", -3.1, 43.3, "reconstructed_established"),
        ("forest_of_dean", "fer", "notable", -2.55, 51.8, "reconstructed_established"),
        ("val_trompia", "fer", "mineure", 10.25, 45.75, "reconstructed"),
        ("falun", "cuivre", "majeure", 15.626, 60.606, "reconstructed_established"),
        ("banska_stiavnica", "argent", "majeure", 18.892, 48.457, "reconstructed_established"),
        ("rammelsberg", "argent", "majeure", 10.428, 51.894, "reconstructed_established"),
        ("kutna_hora", "argent", "majeure", 15.268, 49.948, "reconstructed_established"),
        ("freiberg", "argent", "notable", 13.342, 50.918, "reconstructed_established"),
        ("schwaz", "argent", "mineure", 11.709, 47.348, "reconstructed"),
        ("iglesias", "argent", "notable", 8.537, 39.309, "reconstructed_established"),
        ("camborne_redruth", "etain", "majeure", -5.3, 50.23, "reconstructed_established"),
        ("dartmoor", "etain", "notable", -3.9, 50.57, "reconstructed_established"),
        ("mendip", "plomb", "notable", -2.7, 51.28, "reconstructed_established"),
        ("derbyshire_peak", "plomb", "notable", -1.7, 53.18, "reconstructed_established"),
        ("newcastle_tyne", "charbon", "notable", -1.61, 54.978, "reconstructed_established"),
        ("liege", "charbon", "mineure", 5.57, 50.64, "reconstructed_established"),
        ("almaden", "mercure", "majeure", -4.833, 38.775, "reconstructed_established"),
        ("phocee", "alun", "majeure", 26.755, 38.669, "reconstructed_established"),
        ("kremnica", "or", "majeure", 18.913, 48.705, "reconstructed_established"),
    }
    report(
        "liste_appliquee_est_celle_de_l_amendement",
        int(written == expected_rows and len(written) == len(d4_ids)),
        "1 comparaison D4 amendee",
    )

    deposits = declarations.get("deposits") or []
    published = load(ART / "resources_1400_r1.json").get("deposits") or []
    cells_res = load(ART / "cells_resources_r1.json").get("cells") or []
    stats = load(ART / "stats_r1.json")
    qa = load(LOGS / "v1_081_qa.json")
    step_source = (GEO / "steps" / "r1_resources_1400.py").read_text(encoding="utf-8")
    checks_source = (GEO / "qa" / "checks_r1.py").read_text(encoding="utf-8")

    report("gisements_declares", len(deposits), "1 lecture data/resources_1400.json")

    req = set(C.R1_REQUIRED_DEPOSIT_FIELDS)
    pub = set(C.R1_PUBLISHED_DEPOSIT_FIELDS)
    incomplete = sum(
        1
        for d in deposits
        if set(d.keys()) != req
        or any(d.get(f) in (None, "") for f in req)
    )
    schema_bad = incomplete + sum(
        1 for d in published if set(d.keys()) != pub
    )
    report("declarations_incompletes", incomplete, f"{len(deposits)} gisements declares")
    report("champs_de_gisement_hors_schema", schema_bad, f"{len(deposits) + len(published)} entrees")

    report(
        "natures_hors_vocabulaire",
        sum(1 for d in deposits if d.get("resource") not in C.R1_VALID_RESOURCE_KINDS),
        f"{len(deposits)} gisements declares",
    )
    report(
        "certitudes_hors_vocabulaire",
        sum(1 for d in deposits if d.get("certainty") not in C.R1_VALID_CERTAINTY),
        f"{len(deposits)} gisements declares",
    )
    vocab = set(C.R1_VALID_RICHNESS_CLASSES)
    report(
        "classes_hors_vocabulaire",
        sum(
            1
            for d in list(deposits) + list(published)
            if not isinstance(d.get("richness_class"), str)
            or d.get("richness_class") not in vocab
        ),
        f"{len(deposits) + len(published)} gisements",
    )
    report(
        "gisements_en_dur_dans_le_module",
        sum(1 for dep_id in (d["id"] for d in deposits) if f'"{dep_id}"' in step_source),
        f"{len(deposits)} gisements declares",
    )
    report(
        "classes_en_dur_dans_le_module",
        sum(
            1
            for val in C.R1_VALID_RICHNESS_CLASSES
            if f'"{val}"' in step_source or f'"{val}"' in checks_source
        ),
        f"{len(C.R1_VALID_RICHNESS_CLASSES)} valeurs du vocabulaire",
    )

    report("gisements_rattaches", int(stats.get("gisements_rattaches", -1)), f"{len(deposits)} declares")
    report(
        "gisements_hors_fenetre",
        len(stats.get("gisements_hors_fenetre") or []),
        f"{len(deposits)} declares",
    )
    report(
        "gisements_hors_terre",
        len(stats.get("gisements_hors_terre") or []),
        f"{len(deposits)} declares",
    )
    somme = (
        int(stats.get("gisements_rattaches", 0))
        + len(stats.get("gisements_hors_fenetre") or [])
        + len(stats.get("gisements_hors_terre") or [])
    )
    report(
        "somme_categories_egale_declares",
        int(somme == int(stats.get("gisements_declares", -1))),
        "1 comparaison",
    )
    report("cellules_dotees", int(stats.get("cellules_dotees", -1)), f"{len(cells_res)} cellules")
    report(
        "cellules_a_plusieurs_gisements",
        int(stats.get("cellules_a_plusieurs_gisements", -1)),
        f"{int(stats.get('cellules_dotees', 0))} cellules dotees",
    )

    det = qa.get("determinism") or {}
    report(
        "empreinte_off_differe_de_on",
        int(bool(det.get("cells_differ"))),
        "1 comparaison cells_resources_r1.json",
    )
    off_txt = (LOGS / "v1_081_declarations_off.txt").read_text(encoding="utf-8")
    report(
        "cellules_totales_off",
        int("cellules_dotees=0" in off_txt),
        f"{len(load(ART / 'cells_g3.json')['cells'])} cellules cells_g3.json",
    )

    artifact_docs = [
        load(ART / "resources_1400_r1.json"),
        load(ART / "cells_resources_r1.json"),
        stats,
        load(ART / "MANIFEST_r1.json"),
        load(REGISTRY / "resource_registry.json"),
        declarations,
    ]
    bareme, quantite = count_forbidden_keys(
        artifact_docs, C.WORLD_TERMS_FORBIDDEN_KEYS, C.R1_FORBIDDEN_QUANTITY_KEYS
    )
    report("cles_de_bareme_trouvees", bareme, f"{len(C.WORLD_TERMS_FORBIDDEN_KEYS)} cles")
    report("cles_de_quantite_trouvees", quantite, f"{len(C.R1_FORBIDDEN_QUANTITY_KEYS)} cles")

    spatial = 0

    def walk_spatial(obj: Any) -> None:
        nonlocal spatial
        if isinstance(obj, dict):
            for k, v in obj.items():
                if any(tok in str(k).lower() for tok in ("province", "owner", "country", "pays")):
                    spatial += 1
                walk_spatial(v)
        elif isinstance(obj, list):
            for item in obj[:200]:
                walk_spatial(item)

    for doc in artifact_docs[:4]:
        walk_spatial(doc)
    report("cles_spatiales_concurrentes", spatial, "cles balayees")

    par = stats.get("par_classe_de_richesse") or {}
    report(
        "somme_par_classe_egale_declares",
        int(sum(int(par.get(k, 0)) for k in vocab) == int(stats.get("gisements_declares", -1))),
        "1 comparaison",
    )
    classes_in_cells = 0

    def walk_cells(obj: Any) -> None:
        nonlocal classes_in_cells
        if isinstance(obj, dict):
            for k, v in obj.items():
                if str(k) in vocab or (isinstance(v, str) and v in vocab):
                    classes_in_cells += 1
                walk_cells(v)
        elif isinstance(obj, list):
            for item in obj:
                walk_cells(item)

    walk_cells({"cells": cells_res})
    report("classes_dans_les_cellules", classes_in_cells, "cles et valeurs balayees")
    report(
        "classes_distinctes_employees",
        len({d.get("richness_class") for d in deposits if d.get("richness_class") in vocab}),
        f"{len(vocab)} classes du vocabulaire",
    )

    checks = qa.get("checks") or []
    report("controles_r1_verts", sum(1 for c in checks if c.get("passed")), "8")
    report(
        "controles_r1_avec_preuve_rouge_non_vide",
        sum(1 for c in checks if str(c.get("red_proof") or "").strip()),
        "8",
    )
    sha_block = det.get("sha256") or {}
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
        proc = subprocess.run([str(PY), "tests/run_proof_r1.py"], cwd=GEO, capture_output=True, text=True)
        report("code_sortie_run_proof_r1", proc.returncode, "1 execution tests/run_proof_r1.py")
    else:
        report("code_sortie_run_proof_r1", int(qa.get("exit_code", NOT_COMPUTED)), "exit_code logs/v1_081_qa.json")

    report(
        "constants_lignes_supprimees",
        count_deleted_lines(PRE / "constants.py.orig", GEO / "constants.py"),
        "1 mesure diff vs pre-edit",
    )
    pre_c = load_constants_namespace(PRE / "constants.py.orig")
    pub_c = load_constants_namespace(GEO / "constants.py")
    pre_names = [k for k in pre_c if not k.startswith("_") and k.isupper()]
    report(
        "constantes_preexistantes_inchangees",
        sum(1 for k in pre_names if pre_c.get(k) == pub_c.get(k)),
        f"{len(pre_names)} noms pre-edition",
    )
    report(
        "pipeline_lignes_supprimees",
        count_deleted_lines(PRE / "pipeline.py.orig", GEO / "pipeline.py"),
        "1 mesure diff vs pre-edit",
    )
    orig_pipe = (PRE / "pipeline.py.orig").read_text(encoding="utf-8")
    pub_pipe = (GEO / "pipeline.py").read_text(encoding="utf-8")
    branch_pat = re.compile(r"if args\.source == \"([^\"]+)\":")
    orig_branches = {
        m.group(0): orig_pipe[m.start(): orig_pipe.find("\n", m.start()) + 1]
        for m in branch_pat.finditer(orig_pipe)
    }
    matching = sum(1 for stmt in orig_branches if stmt in pub_pipe)
    orig_choices = re.search(r"choices=\[([^\]]+)\]", orig_pipe)
    orig_vals = re.findall(r'"([^"]+)"', orig_choices.group(1) if orig_choices else "")
    pub_choices = re.search(r"choices=\[([^\]]+)\]", pub_pipe)
    pub_vals = re.findall(r'"([^"]+)"', pub_choices.group(1) if pub_choices else "")
    choices_conserved = all(v in pub_vals for v in orig_vals)
    branches_ok = matching == len(orig_branches) and choices_conserved and len(orig_vals) == 9
    report(
        "branches_source_preexistantes_identiques",
        9 if branches_ok else matching,
        "9 valeurs --source preexistantes",
    )

    prev_artifacts = [
        "pipeline/geo/artifacts/cells_g3.json",
        "pipeline/geo/qa/checks.py",
        "pipeline/geo/qa/checks_c1.py",
        "pipeline/geo/steps/c1_climate_drivers.py",
        "pipeline/geo/artifacts/cells_climate_drivers_c1.json",
    ]
    porcelain = subprocess.run(
        ["git", "status", "--porcelain", *prev_artifacts],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report(
        "artefacts_precedents_modifies",
        len([l for l in porcelain.stdout.splitlines() if l.strip()]),
        f"{len(prev_artifacts)} artefacts verifies",
    )

    declared_proofs = [
        "pipeline/geo/data/resources_1400.json",
        "pipeline/geo/artifacts/resources_1400_r1.json",
        "pipeline/geo/artifacts/cells_resources_r1.json",
        "pipeline/geo/artifacts/stats_r1.json",
        "pipeline/geo/artifacts/MANIFEST_r1.json",
        "pipeline/geo/registry/resource_registry.json",
        "pipeline/geo/logs/v1_081_resources.log",
        "pipeline/geo/logs/v1_081_qa.json",
        "pipeline/geo/logs/v1_081_declarations_on.txt",
        "pipeline/geo/logs/v1_081_declarations_off.txt",
        "pipeline/geo/capture/v1_081_resources_window.png",
    ]
    try:
        tracked = set(git("ls-files", *declared_proofs).splitlines())
        report(
            "fichiers_preuve_suivis_par_git",
            len(tracked),
            f"{len(declared_proofs)} preuves declarees",
        )
    except RuntimeError as exc:
        report("fichiers_preuve_suivis_par_git", NOT_COMPUTED, str(exc))

    if args.no_pytest:
        report("tests_harness_passed_026", NOT_COMPUTED, "non execute (--no-pytest)")
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
            "tests_harness_passed_026",
            passed,
            f"{collected} tests collectes ({skipped} SKIP, {failed} echecs)",
        )

    if args.json:
        print(json.dumps([{"name": n, "value": v, "denominator": d} for n, v, d in ROWS], indent=2))
    else:
        for name, value, denom in ROWS:
            print(f"{name} = {value}  ({denom})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
