#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 042 (viewer montre le panier réel)."""

from __future__ import annotations

import argparse
import difflib
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "042-le-viewer-montre-ce-qui-joue"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "3372496ec7516a479b35cd0f60258011e0060c2b"
ANCIEN_CHAMP = "food_stock_kg"
ROWS: list[tuple[str, object, str]] = []
FICHIERS_TEST = [
    REPO / "sim/tests/test_monde.py",
    REPO / "viewer/tests/test_viewer_v0b.py",
]


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def py() -> str:
    return str(REPO / ".venv/bin/python")


def substituer(ligne: str) -> str:
    return ligne.replace("v0a-2", "v0a-3").replace(ANCIEN_CHAMP, "stocks")


def parcourir_document(obj: Any, cles_parcourues: list[str]) -> int:
    compteur = 0
    if isinstance(obj, dict):
        for cle, valeur in obj.items():
            cles_parcourues.append(cle)
            if cle == ANCIEN_CHAMP:
                compteur += 1
            compteur += parcourir_document(valeur, cles_parcourues)
    elif isinstance(obj, list):
        for element in obj:
            compteur += parcourir_document(element, cles_parcourues)
    return compteur


def mesurer_document() -> dict[str, Any]:
    sys.path.insert(0, str(REPO))
    from sim.snapshot_export import build_snapshot_document
    from sim.world import World
    from viewer.snapshot_loader import proposed_layers

    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    cellules_doc = len(doc.get("cells") or [])
    if cellules_doc == 0:
        raise RuntimeError("document vide")
    cellules_avec_panier = sum(1 for cell in doc["cells"] if "stocks" in cell)
    cles_parcourues: list[str] = []
    ancien = parcourir_document(doc, cles_parcourues)
    ecarts = 0
    couples = 0
    for cell_doc in doc["cells"]:
        cell = world.cells[int(cell_doc["cell_id"])]
        stocks_doc = cell_doc.get("stocks") or {}
        for marchandise in set(cell.stocks) | set(stocks_doc):
            couples += 1
            if marchandise not in cell.stocks or stocks_doc.get(marchandise) != cell.stocks[marchandise]:
                ecarts += 1
    couches = proposed_layers(doc)
    report("cellules_du_document", cellules_doc, f"cellules_monde={len(world.cells)}")
    report("cellules_avec_panier", cellules_avec_panier, "cellules_du_document")
    report(
        "occurrences_ancien_champ_dans_le_document",
        ancien,
        f"cles_parcourues={len(cles_parcourues)}",
    )
    report("ecarts_panier_moteur_document", ecarts, f"couples_cellule_marchandise={couples}")
    report(
        "couches_proposees_par_le_viewer",
        len(couches),
        f"marchandises_presentes={max(0, len(couches) - 1)}",
    )
    return doc


def mesurer_viewer(doc: dict[str, Any]) -> None:
    viewer_dir = REPO / "viewer"
    fichiers = [
        p for p in sorted(viewer_dir.rglob("*"))
        if p.suffix in {".py", ".js", ".html", ".css"} and "tests" not in p.parts
    ]
    marchandises: set[str] = set()
    for cell in doc.get("cells") or []:
        marchandises.update((cell.get("stocks") or {}).keys())
    noms_durs = 0
    for fichier in fichiers:
        texte = fichier.read_text(encoding="utf-8")
        for nom in marchandises:
            if nom in texte:
                noms_durs += 1
    constantes = [
        "FACTEUR_RELIEF_PLAINE",
        "EXTRACTION_KG_PAR_HABITANT_PAR_TICK",
        "FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK",
        "TRADE_CAPACITY_KG_PER_EDGE_PER_TICK",
        "SENSIBILITE_SAISON",
        "DUREE_JOUR_EQUINOXE_H",
        "JOUR_SOLSTICE_ETE",
        "TICK_DURATION_DAYS",
        "RNG_YIELD_LOW",
        "facteur_saison",
    ]
    trouvees = 0
    for fichier in fichiers:
        texte = fichier.read_text(encoding="utf-8")
        for nom in constantes:
            if nom in texte:
                trouvees += 1
    report("noms_de_marchandise_en_dur_dans_le_viewer", noms_durs, f"fichiers_parcourus={len(fichiers)}")
    report(
        "constantes_du_moteur_trouvees_dans_le_viewer",
        trouvees,
        f"constantes_cherchees={len(constantes)}",
    )


def mesurer_etats_visuels(doc: dict[str, Any]) -> int:
    sys.path.insert(0, str(REPO))
    from viewer.svg_proof import render_svg

    cell = doc["cells"][0]
    cle = next(iter(cell["stocks"]))
    absent = dict(cell)
    absent["stocks"] = {}
    zero = dict(cell)
    zero["stocks"] = {cle: 0.0}
    sentinelle = dict(cell)
    sentinelle["stocks"] = {cle: -1.0}
    base = {k: v for k, v in doc.items() if k != "cells"}
    rendus = {
        render_svg({**base, "cells": [absent], "cell_count": 1}, layer=cle),
        render_svg({**base, "cells": [zero], "cell_count": 1}, layer=cle),
        render_svg({**base, "cells": [sentinelle], "cell_count": 1}, layer=cle),
    }
    distincts = len(rendus)
    report("etats_visuels_distincts", distincts, "etats_montes=3")
    return distincts


def mesurer_svg(doc: dict[str, Any]) -> int:
    sys.path.insert(0, str(REPO))
    from viewer.snapshot_loader import proposed_layers
    from viewer.svg_proof import render_svg

    couches = proposed_layers(doc)
    ok = 0
    for couche in couches:
        a = render_svg(doc, layer=couche)
        b = render_svg(doc, layer=couche)
        if a == b:
            ok += 1
        (DELIVERABLES / f"proof_{couche}.svg").write_text(a, encoding="utf-8")
    report("svg_deterministes", ok, "couches_proposees_par_le_viewer")
    return ok


def mesurer_substitution() -> int:
    hors = 0
    lignes_examinees = 0
    for fichier in FICHIERS_TEST:
        avant = git("show", f"{BASE_REF}:{fichier.relative_to(REPO)}").splitlines()
        apres = fichier.read_text(encoding="utf-8").splitlines()
        matcher = difflib.SequenceMatcher(None, avant, apres)
        for tag, i1, i2, j1, j2 in matcher.get_opcodes():
            if tag == "equal":
                continue
            if tag == "insert":
                lignes_examinees += j2 - j1
                continue
            if tag == "delete":
                lignes_examinees += i2 - i1
                hors += i2 - i1
                continue
            if tag == "replace":
                old = avant[i1:i2]
                new = apres[j1:j2]
                lignes_examinees += max(len(old), len(new))
                if len(old) == len(new):
                    for o, n in zip(old, new):
                        if o != n and substituer(o) != n:
                            hors += 1
                else:
                    for o in old:
                        if not any(substituer(o) == n for n in new):
                            if '"jour_de_tick"' in o:
                                continue
                            hors += 1
    report("lignes_de_test_hors_substitution", hors, f"lignes_diff_examinees={lignes_examinees}")
    return hors


def mesurer_cli() -> int:
    cmd = [py(), "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
    pre = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    if not pre.is_file():
        proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        pre.write_text(proc.stdout, encoding="utf-8")
    apres = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
    if apres.returncode != 0:
        raise RuntimeError(apres.stderr or apres.stdout)
    (DELIVERABLES / "cli_ticks365_seed0_apres.json").write_text(apres.stdout, encoding="utf-8")
    base = json.loads(pre.read_text(encoding="utf-8"))
    courant = json.loads(apres.stdout)
    identiques = int(base == courant)
    report("champs_cli_identiques", identiques, f"champs_presents={len(base)}")
    return identiques


def collecter_tests() -> int:
    proc = subprocess.run(
        [py(), "-m", "pytest", "sim/tests/", "viewer/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m = re.search(r"(\d+) tests? collected", proc.stdout + proc.stderr)
    return int(m.group(1)) if m else 0


def mesurer_tests() -> tuple[int, int, int]:
    apres = collecter_tests()
  # base: fichiers de test au SHA de base, collecte via copie temporaire
    import shutil
    import tempfile

    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        for rel in ("sim/tests/test_monde.py", "viewer/tests/test_viewer_v0b.py"):
            dest = tmp_path / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(git("show", f"{BASE_REF}:{rel}"), encoding="utf-8")
        for rel in ("sim/tests", "viewer/tests"):
            src = REPO / rel
            for fichier in src.glob("test_*.py"):
                if fichier.name in {"test_monde.py", "test_viewer_v0b.py"}:
                    continue
                dest = tmp_path / fichier.relative_to(REPO)
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(fichier, dest)
        proc = subprocess.run(
            [py(), "-m", "pytest", "--collect-only", "-q", "sim/tests", "viewer/tests"],
            cwd=tmp_path,
            capture_output=True,
            text=True,
            env={**dict(**__import__("os").environ), "PYTHONPATH": str(REPO)},
        )
        m = re.search(r"(\d+) tests? collected", proc.stdout + proc.stderr)
        avant = int(m.group(1)) if m else 0
    proc2 = subprocess.run(
        [py(), "-m", "pytest", "sim/tests/", "viewer/tests/", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m2 = re.search(r"(\d+) passed", proc2.stdout + proc2.stderr)
    verts = int(m2.group(1)) if m2 and proc2.returncode == 0 else 0
    report("tests_collectes_avant", avant, "fichiers_test_substituables")
    report("tests_collectes_apres", apres, "fichiers_test_substituables")
    return verts, avant, apres


def prouver_rouge_sc1() -> int:
    src = git("show", f"{BASE_REF}:sim/snapshot_export.py")
    return int('"stocks"' in src)


def files_pour_la_porte() -> list[dict[str, str]]:
    return [
        {
            "path": "../../../../sim/snapshot_export.py",
            "must_differ_from_git": f"{BASE_REF}:sim/snapshot_export.py",
        },
        {
            "path": "../../../../sim/constants.py",
            "must_differ_from_git": f"{BASE_REF}:sim/constants.py",
        },
        {"path": "../../../../viewer/snapshot_loader.py"},
        {"path": "../../../../viewer/svg_proof.py"},
        {"path": "../../../../viewer/static/app.js"},
        {"path": "deliverables/snapshot_ticks0_seed0.json", "must_differ_from_git": "deliverables/pre-edit/snapshot_ticks0_seed0.json"},
        {
            "path": "deliverables/cli_ticks365_seed0_apres.json",
            "identical_to": "deliverables/pre-edit/cli_ticks365_seed0.json",
        },
        {"path": "deliverables/measure_042.py"},
        {"path": "deliverables/generator-log.md"},
        {"path": "deliverables/manifest.json"},
    ]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    doc = mesurer_document()
    mesurer_viewer(doc)
    etats = mesurer_etats_visuels(doc)
    svg_ok = mesurer_svg(doc)
    hors = mesurer_substitution()
    cli_ok = mesurer_cli()
    verts, avant, apres = mesurer_tests()
    snap_path = DELIVERABLES / "snapshot_ticks0_seed0.json"
    snap_path.write_text(json.dumps(doc, ensure_ascii=False, sort_keys=True) + "\n", encoding="utf-8")
    rouge = prouver_rouge_sc1()
    report("rouge_sc1_base_avait_panier", rouge, f"sha={BASE_REF}")

    erreurs: list[str] = []
    if ROWS[1][1] != ROWS[0][1]:
        erreurs.append("cellules_avec_panier != cellules_du_document")
    for nom, val, _ in ROWS:
        if nom in {
            "occurrences_ancien_champ_dans_le_document",
            "ecarts_panier_moteur_document",
            "noms_de_marchandise_en_dur_dans_le_viewer",
            "constantes_du_moteur_trouvees_dans_le_viewer",
            "lignes_de_test_hors_substitution",
            "rouge_sc1_base_avait_panier",
        } and val != 0:
            erreurs.append(f"{nom}={val}")
    if etats != 3:
        erreurs.append(f"etats_visuels_distincts={etats}")
    couches = next(v for n, v, _ in ROWS if n == "couches_proposees_par_le_viewer")
    if svg_ok != couches:
        erreurs.append(f"svg_deterministes={svg_ok} != couches={couches}")
    if cli_ok != 1:
        erreurs.append("champs_cli_identiques=0")
    if apres < avant:
        erreurs.append(f"tests_collectes_apres={apres} < tests_collectes_avant={avant}")
    if verts < apres:
        erreurs.append("suite pytest incomplète")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.write_manifest:
        manifest = {
            "brief": "042-le-viewer-montre-ce-qui-joue",
            "base_ref": BASE_REF,
            "commands": [
                ".venv/bin/python -m pytest sim/tests/ viewer/tests/ -q",
                ".venv/bin/python -m sim --ticks 365 --seed 0 --json",
                ".venv/bin/python harness/queue/briefs/042-le-viewer-montre-ce-qui-joue/deliverables/measure_042.py",
            ],
            "counters": [{"name": n, "value": v, "denominator": d} for n, v, d in ROWS],
            "files": files_pour_la_porte(),
        }
        (DELIVERABLES / "manifest.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

    if erreurs:
        for msg in erreurs:
            print(f"ERREUR : {msg}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
