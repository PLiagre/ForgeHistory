#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 033 (relief dans le rendement).

Usage depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/033-relief-dans-le-rendement/deliverables/measure_033.py
  .venv/bin/python .../measure_033.py --write-manifest
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "033-relief-dans-le-rendement"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "448aa2a6c733331aebfb031e217a5c68f4c02c07"
CLI_CMD = [".venv/bin/python", "-m", "sim", "--ticks", "20", "--seed", "0", "--json"]
CHAMPS_CLI = (
    "population_arrivee",
    "cellules_affamees",
    "kg_transportes",
    "stock_kg_arrivee",
)
NOT_COMPUTED = -1
ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def run_cli_with_modules(engine_src: str | None, constants_src: str | None) -> dict:
    """Joue la CLI en surchargeant engine.py et constants.py si fournis."""
    import shutil

    with tempfile.TemporaryDirectory(prefix="measure033-") as td:
        overlay = Path(td)
        overlay_sim = overlay / "sim"
        shutil.copytree(REPO / "sim", overlay_sim, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        if engine_src is not None:
            (overlay_sim / "engine.py").write_text(engine_src, encoding="utf-8")
        if constants_src is not None:
            (overlay_sim / "constants.py").write_text(constants_src, encoding="utf-8")
        env = os.environ.copy()
        env["PYTHONPATH"] = str(overlay) + os.pathsep + env.get("PYTHONPATH", "")
        proc = subprocess.run(CLI_CMD, cwd=REPO, capture_output=True, text=True, env=env)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return json.loads(proc.stdout.strip())


def mesurer_classes_carte() -> tuple[dict[str, int], int, int]:
    sys.path.insert(0, str(REPO))
    from sim.world import World

    carte = World.lire_carte()
    compteur = Counter()
    for raw in carte["cellules"]:
        relief = raw.get("relief")
        if relief:
            compteur[relief] += 1
    classes = dict(compteur)
    return classes, len(classes), sum(compteur.values())


def mesurer_facteurs_effectifs(classes: dict[str, int]) -> int:
    sys.path.insert(0, str(REPO))
    from sim import constants as _k
    from sim import engine
    from sim.world import World

    world = World.charger(0)
    surface = 10.0
    rendement = 1.0
    attendus = _k.facteurs_production_par_relief()
    plaine_cid = next(
        int(raw["cell_id"])
        for raw in World.lire_carte()["cellules"]
        if raw.get("relief") == "plaine"
    )
    plaine_cell = world.cells[plaine_cid]
    plaine_cell.area_km2 = surface
    engine._carte_du_tick = world.carte
    try:
        ref_plaine = engine.production_kg(plaine_cell, rendement)
    finally:
        engine._carte_du_tick = None
    effectifs = 1
    for cls in sorted(classes):
        if cls == "plaine":
            continue
        cid = next(
            int(raw["cell_id"])
            for raw in World.lire_carte()["cellules"]
            if raw.get("relief") == cls
        )
        cell = world.cells[cid]
        cell.area_km2 = surface
        engine._carte_du_tick = world.carte
        try:
            prod = engine.production_kg(cell, rendement)
        finally:
            engine._carte_du_tick = None
        ratio = prod / ref_plaine
        if ratio == attendus[cls] / attendus["plaine"]:
            effectifs += 1
    return effectifs


def mesurer_classes_inconnues_refusees() -> tuple[int, int]:
    import random

    sys.path.insert(0, str(REPO))
    from sim.engine import ReliefInvalideError, tick
    from sim.world import World

    mutations = [
        ("relief_inconnu_033", "relief_inconnu_033"),
        (None, "relief=None"),
    ]
    refusees = 0
    for valeur, _ in mutations:
        world = World.charger(0)
        cid = next(iter(world.cells))
        entree = dict(world.carte[cid])
        if valeur is None:
            entree.pop("relief", None)
        else:
            entree["relief"] = valeur
        world.carte[cid] = entree
        try:
            tick(world, random.Random(0))
        except ReliefInvalideError as exc:
            if f"cell_id={cid}" in str(exc):
                refusees += 1
    return refusees, len(mutations)


def mesurer_couches() -> tuple[int, int]:
    sys.path.insert(0, str(REPO))
    from sim.snapshot_export import build_snapshot_document
    from sim.world import World

    doc = build_snapshot_document(World.charger(0), 0, 0)
    couches = doc["couches"]
    consommees = sum(1 for c in couches.values() if c["utilisee_par_le_moteur"])
    return consommees, len(couches)


def mesurer_cli() -> tuple[int, int, int, dict, dict]:
    apres_a = run_cli_with_modules(None, None)
    apres_b = run_cli_with_modules(None, None)
    pre_edit = DELIVERABLES / "pre-edit" / "cli_ticks20_seed0.json"
    if pre_edit.is_file():
        base = json.loads(pre_edit.read_text(encoding="utf-8"))
    else:
        base_engine = git("show", f"{BASE_REF}:sim/engine.py")
        base_constants = git("show", f"{BASE_REF}:sim/constants.py")
        base = run_cli_with_modules(base_engine, base_constants)
        pre_edit.parent.mkdir(parents=True, exist_ok=True)
        pre_edit.write_text(json.dumps(base, ensure_ascii=False) + chr(10), encoding="utf-8")
    identiques = 1 if apres_a == apres_b else 0
    modifiees = sum(1 for k in CHAMPS_CLI if apres_a.get(k) != base.get(k))
    return identiques, 2, modifiees, apres_a, base


def mesurer_tests() -> tuple[int, int]:
    proc = subprocess.run(
        [str(REPO / ".venv/bin/python"), "-m", "pytest", "sim/tests/", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    match = None
    import re

    m = re.search(r"(\d+) passed", proc.stdout + proc.stderr)
    verts = int(m.group(1)) if m else NOT_COMPUTED
    collect = subprocess.run(
        [str(REPO / ".venv/bin/python"), "-m", "pytest", "sim/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m2 = re.search(r"(\d+) test", collect.stdout + collect.stderr)
    total = int(m2.group(1)) if m2 else NOT_COMPUTED
    ok = 1 if proc.returncode == 0 else 0
    return ok * verts if ok else 0, total


def mesurer_rouge_facteurs(classes: dict[str, int]) -> int:
    import shutil

    base_engine = git("show", f"{BASE_REF}:sim/engine.py")
    base_constants = git("show", f"{BASE_REF}:sim/constants.py")
    with tempfile.TemporaryDirectory(prefix="measure033-red-") as td:
        overlay = Path(td)
        overlay_sim = overlay / "sim"
        shutil.copytree(REPO / "sim", overlay_sim, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        (overlay_sim / "engine.py").write_text(base_engine, encoding="utf-8")
        (overlay_sim / "constants.py").write_text(base_constants, encoding="utf-8")
        env = os.environ.copy()
        env["PYTHONPATH"] = str(overlay) + os.pathsep + env.get("PYTHONPATH", "")
        code = """
import json
from sim.model import Cell
from sim import engine
carte = __import__('sim.world', fromlist=['World']).World.lire_carte()
classes = {}
for raw in carte['cellules']:
    r = raw.get('relief')
    if r and r not in classes:
        classes[r] = int(raw['cell_id'])
prods = []
for cls, cid in sorted(classes.items()):
    cell = Cell(cell_id=cid, area_km2=10.0, population=0, food_stock_kg=0,
                hunger_ticks=0, food_deficit_kg=0, mortality_remainder=0)
    prods.append(engine.production_kg(cell, 1.0))
print(json.dumps(len(set(prods)) == 1))
"""
        proc = subprocess.run([str(REPO / ".venv" / "bin" / "python"), "-c", code], cwd=REPO, capture_output=True, text=True, env=env)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return 1 if proc.stdout.strip().lower() == "true" else 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    classes, n_classes, n_cellules = mesurer_classes_carte()
    report("classes_relief_carte", n_classes, f"classes distinctes mesurées = {n_classes}")
    report("cellules_par_classe", n_cellules, f"cellules mesurées = {n_cellules}")

    effectifs = mesurer_facteurs_effectifs(classes)
    report(
        "classes_avec_facteur_effectif",
        effectifs,
        f"classes_relief_carte = {n_classes}",
    )

    refusees, mutations = mesurer_classes_inconnues_refusees()
    report(
        "classes_inconnues_refusees",
        refusees,
        f"mutations exécutées = {mutations}",
    )

    consommees, nb_couches = mesurer_couches()
    report(
        "couches_consommees_par_tick",
        consommees,
        f"couches déclarées dans le snapshot = {nb_couches}",
    )

    identiques, runs, modifiees, apres, base = mesurer_cli()
    report(
        "sorties_cli_deterministes",
        identiques,
        f"exécutions lancées = {runs}",
    )
    report(
        "champs_cli_modifies",
        modifiees,
        f"champs comparés = {len(CHAMPS_CLI)}",
    )

    verts, collectes = mesurer_tests()
    report("tests_sim_verts", verts, f"tests collectés = {collectes}")

    rouge = mesurer_rouge_facteurs(classes)
    report(
        "production_identique_avant_correction",
        rouge,
        "1=cinq appels indistinguables sur moteur de base / 0=distinction déjà visible",
    )

    pre_edit = DELIVERABLES / "pre-edit" / "cli_ticks20_seed0.json"
    pre_edit.parent.mkdir(parents=True, exist_ok=True)
    pre_edit.write_text(json.dumps(base, ensure_ascii=False) + "\n", encoding="utf-8")
    (DELIVERABLES / "cli_ticks20_seed0_apres.json").write_text(
        json.dumps(apres, ensure_ascii=False) + "\n", encoding="utf-8",
    )

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if apres.get("cellules_affamees", 0) <= base.get("cellules_affamees", 0):
        print("ERREUR : cellules_affamees après <= base", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        counters = []
        for name, value, denom in ROWS:
            sample_map = {
                "classes_relief_carte": n_classes,
                "cellules_par_classe": n_cellules,
                "classes_avec_facteur_effectif": n_classes,
                "classes_inconnues_refusees": mutations,
                "couches_consommees_par_tick": nb_couches,
                "sorties_cli_deterministes": runs,
                "champs_cli_modifies": len(CHAMPS_CLI),
                "tests_sim_verts": collectes,
                "production_identique_avant_correction": 1,
            }
            sample = sample_map.get(name, 1)
            if sample == 0:
                sample = NOT_COMPUTED
            counters.append(
                {
                    "name": name,
                    "value": value,
                    "sample_size": sample,
                    "denominator": denom,
                }
            )
        manifest["counters"] = counters
        manifest_path.write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
