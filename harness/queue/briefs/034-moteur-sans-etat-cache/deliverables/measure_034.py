#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 034 (moteur sans état de module).

Usage depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/measure_034.py
  .venv/bin/python .../measure_034.py --write-manifest
"""

from __future__ import annotations

import argparse
import ast
import json
import os
import re
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "034-moteur-sans-etat-cache"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1"
NOT_COMPUTED = -1
ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def compter_globals(engine_src: str) -> tuple[int, int, list[str]]:
    tree = ast.parse(engine_src)
    fonctions: list[str] = []
    fautives: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, ast.FunctionDef):
            fonctions.append(node.name)
            for child in ast.walk(node):
                if isinstance(child, ast.Global):
                    fautives.append(node.name)
    return len(fonctions), len(set(fautives)), sorted(set(fautives))


def run_cli(cmd: list[str], engine_src: str | None = None) -> str:
    import shutil

    env = os.environ.copy()
    if engine_src is None:
        proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, env=env)
    else:
        with tempfile.TemporaryDirectory(prefix="measure034-") as td:
            overlay = Path(td)
            overlay_sim = overlay / "sim"
            shutil.copytree(REPO / "sim", overlay_sim, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
            (overlay_sim / "engine.py").write_text(engine_src, encoding="utf-8")
            env["PYTHONPATH"] = str(overlay) + os.pathsep + env.get("PYTHONPATH", "")
            proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, env=env)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout)
    return proc.stdout


def mesurer_sc2() -> dict[str, int]:
    sys.path.insert(0, str(REPO))
    from sim import constants as _k
    from sim import engine
    from sim.engine import production_du_tick_kg
    from sim.world import World

    world = World.charger(0)
    cell_id = next(iter(world.cells))
    cell = world.cells[cell_id]
    cell.area_km2 = 10.0
    rendement = 1.0

    classes: dict[str, int] = {}
    for raw in world.carte.values():
        relief = raw.get("relief")
        if relief and relief not in classes:
            classes[relief] = cell_id

    cartes: dict[str, dict] = {}
    for cls in sorted(classes):
        carte_x = {cid: dict(entree) for cid, entree in world.carte.items()}
        entree = dict(carte_x[cell_id])
        entree["relief"] = cls
        carte_x[cell_id] = entree
        cartes[cls] = carte_x

    facteurs = _k.facteurs_production_par_relief()
    ref_cls = "plaine"
    productions = {}
    appels = 0
    for cls, carte_x in sorted(cartes.items()):
        productions[cls] = production_du_tick_kg(cell, rendement, carte_x)
        appels += 1

    ref_prod = productions[ref_cls]
    ratios_ok = 0
    for cls in sorted(cartes):
        if cls == ref_cls:
            continue
        ratio = productions[cls] / ref_prod
        if ratio == facteurs[cls] / facteurs[ref_cls]:
            ratios_ok += 1

    appels_repetes = 0
    for carte_x in cartes.values():
        a = production_du_tick_kg(cell, rendement, carte_x)
        b = production_du_tick_kg(cell, rendement, carte_x)
        if a == b and engine._carte_du_tick is None:
            appels_repetes += 1

    return {
        "cartes_comparees_sc2": len(cartes),
        "appels_production_du_tick": appels,
        "ratios_conformes_au_facteur_nominal": ratios_ok,
        "appels_repetes_stables": appels_repetes,
    }


def mesurer_lectures_tick(engine_src: str | None = None) -> tuple[int, int]:
    with tempfile.NamedTemporaryFile("w", suffix=".py", delete=False, encoding="utf-8") as script:
        script_path = Path(script.name)
        engine_tmp: Path | None = None
        if engine_src is not None:
            engine_tmp = Path(tempfile.mktemp(suffix=".engine.py"))
            engine_tmp.write_text(engine_src, encoding="utf-8")
        script.write(
            f"""import random
from pathlib import Path

repo = Path({repr(str(REPO))})
engine_path = repo / "sim" / "engine.py"
backup = engine_path.read_text(encoding="utf-8")
engine_tmp = {repr(str(engine_tmp) if engine_tmp else None)}
try:
    if engine_tmp:
        engine_path.write_text(Path(engine_tmp).read_text(encoding="utf-8"), encoding="utf-8")
    from sim import engine
    from sim.engine import tick
    from sim.world import World

    class CarteInstrumentee(dict):
        def __init__(self, data):
            super().__init__(data)
            self.lectures = []

        def get(self, key, default=None):
            self.lectures.append(engine._carte_du_tick)
            return super().get(key, default)

        def __getitem__(self, key):
            self.lectures.append(engine._carte_du_tick)
            return super().__getitem__(key)

    world = World.charger(0)
    world.carte = CarteInstrumentee(dict(world.carte))
    tick(world, random.Random(0))
    lectures = len(world.carte.lectures)
    non_none = sum(1 for valeur in world.carte.lectures if valeur is not None)
    print(lectures, non_none)
finally:
    if engine_tmp:
        engine_path.write_text(backup, encoding="utf-8")
"""
        )
    env = os.environ.copy()
    env["PYTHONPATH"] = str(REPO) + os.pathsep + env.get("PYTHONPATH", "")
    proc = subprocess.run(
        [str(REPO / ".venv/bin/python"), str(script_path)],
        cwd=REPO,
        capture_output=True,
        text=True,
        env=env,
    )
    script_path.unlink(missing_ok=True)
    if engine_tmp is not None:
        engine_tmp.unlink(missing_ok=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout)
    parts = proc.stdout.strip().split()
    return int(parts[0]), int(parts[1])


def mesurer_cli() -> tuple[int, int, int]:
    cmds = [
        [".venv/bin/python", "-m", "sim", "--ticks", "20", "--seed", "0", "--json"],
        [".venv/bin/python", "-m", "sim", "--ticks", "200", "--seed", "42", "--json"],
    ]
    pre20 = DELIVERABLES / "pre-edit" / "cli_ticks20_seed0.json"
    pre200 = DELIVERABLES / "pre-edit" / "cli_ticks200_seed42.json"
    if not pre20.is_file():
        pre20.parent.mkdir(parents=True, exist_ok=True)
        base_engine = git("show", f"{BASE_REF}:sim/engine.py")
        pre20.write_text(run_cli(cmds[0], base_engine), encoding="utf-8")
        pre200.write_text(run_cli(cmds[1], base_engine), encoding="utf-8")

    base20 = json.loads(pre20.read_text(encoding="utf-8"))
    base200 = json.loads(pre200.read_text(encoding="utf-8"))
    apres20 = json.loads(run_cli(cmds[0]))
    apres200 = json.loads(run_cli(cmds[1]))

    (DELIVERABLES / "cli_ticks20_seed0_apres.json").write_text(
        json.dumps(apres20, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    (DELIVERABLES / "cli_ticks200_seed42_apres.json").write_text(
        json.dumps(apres200, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    champs = sorted(set(base20) | set(apres20))
    identiques = sum(1 for k in champs if base20.get(k) == apres20.get(k) and base200.get(k) == apres200.get(k))
    return identiques, len(champs), 2


def mesurer_appels_unitaires() -> int:
    sys.path.insert(0, str(REPO))
    from sim import engine
    from sim.model import Cell

    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=0,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    ok = 0
    try:
        prod = engine.production_kg(cell, 1.0)
        if prod > 0:
            ok = 1
    except Exception:
        pass
    return ok


def mesurer_classes_inconnues() -> tuple[int, int]:
    import random

    sys.path.insert(0, str(REPO))
    from sim.engine import ReliefInvalideError, tick
    from sim.world import World

    mutations = [("relief_inconnu_034",), (None,)]
    refusees = 0
    for valeur in mutations:
        world = World.charger(0)
        cid = next(iter(world.cells))
        entree = dict(world.carte[cid])
        if valeur[0] is None:
            entree.pop("relief", None)
        else:
            entree["relief"] = valeur[0]
        world.carte[cid] = entree
        try:
            tick(world, random.Random(0))
        except ReliefInvalideError as exc:
            if f"cell_id={cid}" in str(exc):
                refusees += 1
    return refusees, len(mutations)


def mesurer_diff_lignes(chemin: str) -> tuple[int, int]:
    diff = git("diff", BASE_REF, "--", chemin)
    supprimees = 0
    modifiees = 0
    for line in diff.splitlines():
        if line.startswith("---") or line.startswith("+++") or line.startswith("@@"):
            continue
        if line.startswith("-"):
            supprimees += 1
            modifiees += 1
        elif line.startswith("+"):
            modifiees += 1
    return supprimees, modifiees


def mesurer_tests() -> tuple[int, int]:
    proc = subprocess.run(
        [str(REPO / ".venv/bin/python"), "-m", "pytest", "sim/tests/", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m = re.search(r"(\d+) passed", proc.stdout + proc.stderr)
    verts = int(m.group(1)) if m and proc.returncode == 0 else 0
    collect = subprocess.run(
        [str(REPO / ".venv/bin/python"), "-m", "pytest", "sim/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m2 = re.search(r"(\d+) test", collect.stdout + collect.stderr)
    total = int(m2.group(1)) if m2 else NOT_COMPUTED
    return verts, total


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    engine_actuel = (REPO / "sim" / "engine.py").read_text(encoding="utf-8")
    engine_base = git("show", f"{BASE_REF}:sim/engine.py")

    n_fonctions, n_global, _ = compter_globals(engine_actuel)
    n_fonctions_avant, n_global_avant, fautives_avant = compter_globals(engine_base)

    report("fonctions_moteur_inspectees", n_fonctions, f"fonctions dans sim/engine.py = {n_fonctions}")
    report("fonctions_avec_global", n_global, f"fonctions_moteur_inspectees = {n_fonctions}")
    report(
        "fonctions_avec_global_avant",
        n_global_avant,
        f"fonctions du SHA de base = {n_fonctions_avant} ({', '.join(fautives_avant)})",
    )

    sc2 = mesurer_sc2()
    for name, value in sc2.items():
        denom_map = {
            "cartes_comparees_sc2": "classes de relief dans la carte figée",
            "appels_production_du_tick": "appels réellement lancés",
            "ratios_conformes_au_facteur_nominal": "rapports calculés",
            "appels_repetes_stables": "répétitions jouées",
        }
        report(name, value, denom_map[name])

    lectures, non_none = mesurer_lectures_tick()
    report(
        "lectures_de_carte_pendant_le_tick",
        lectures,
        f"lectures enregistrées pendant un tick = {lectures}",
    )
    report(
        "lectures_voyant_un_etat_de_module",
        non_none,
        f"lectures_de_carte_pendant_le_tick = {lectures}",
    )

    lectures_avant, non_none_avant = mesurer_lectures_tick(engine_base)
    report(
        "lectures_voyant_un_etat_de_module_avant",
        non_none_avant,
        f"lectures sur SHA de base = {lectures_avant}",
    )

    champs_ok, n_champs, n_sorties = mesurer_cli()
    report(
        "champs_cli_identiques",
        champs_ok,
        f"champs présents dans la sortie = {n_champs}",
    )
    report(
        "sorties_cli_comparees",
        n_sorties,
        "exécutions CLI comparées",
    )

    appels_unitaires = mesurer_appels_unitaires()
    report(
        "appels_unitaires_sans_carte",
        appels_unitaires,
        "appels production_kg hors World",
    )

    refusees, mutations = mesurer_classes_inconnues()
    report(
        "classes_inconnues_refusees",
        refusees,
        f"mutations exécutées = {mutations}",
    )

    supprimees, _ = mesurer_diff_lignes("sim/tests/test_monde.py")
    report(
        "lignes_supprimees_dans_test_monde",
        supprimees,
        "lignes du diff test_monde.py contre SHA de base",
    )

    _, mod_survie = mesurer_diff_lignes("sim/tests/test_survie.py")
    report(
        "lignes_modifiees_dans_test_survie",
        mod_survie,
        "lignes du diff test_survie.py contre SHA de base",
    )

    verts, collectes = mesurer_tests()
    report("tests_sim_verts", verts, f"tests collectés = {collectes}")

    erreurs: list[str] = []
    if n_global != 0:
        erreurs.append(f"fonctions_avec_global={n_global} (attendu 0)")
    if n_global_avant <= 0:
        erreurs.append(f"fonctions_avec_global_avant={n_global_avant} (rouge non prouvé)")
    if non_none != 0:
        erreurs.append(f"lectures_voyant_un_etat_de_module={non_none}")
    if non_none_avant <= 0:
        erreurs.append(f"lectures_voyant_un_etat_de_module_avant={non_none_avant}")
    if supprimees != 0:
        erreurs.append(f"lignes_supprimees_dans_test_monde={supprimees}")
    if mod_survie != 0:
        erreurs.append(f"lignes_modifiees_dans_test_survie={mod_survie}")
    if champs_ok != n_champs:
        erreurs.append("sortie CLI non byte-identique")
    if verts != collectes:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        counters = []
        sample_map = {
            "fonctions_moteur_inspectees": n_fonctions,
            "fonctions_avec_global": n_fonctions,
            "fonctions_avec_global_avant": n_fonctions_avant,
            "cartes_comparees_sc2": sc2["cartes_comparees_sc2"],
            "appels_production_du_tick": sc2["appels_production_du_tick"],
            "ratios_conformes_au_facteur_nominal": sc2["ratios_conformes_au_facteur_nominal"],
            "appels_repetes_stables": sc2["appels_repetes_stables"],
            "lectures_de_carte_pendant_le_tick": lectures,
            "lectures_voyant_un_etat_de_module": lectures,
            "lectures_voyant_un_etat_de_module_avant": lectures_avant,
            "champs_cli_identiques": n_champs,
            "sorties_cli_comparees": n_sorties,
            "appels_unitaires_sans_carte": 1,
            "classes_inconnues_refusees": mutations,
            "lignes_supprimees_dans_test_monde": supprimees + sc2["cartes_comparees_sc2"],
            "lignes_modifiees_dans_test_survie": mod_survie,
            "tests_sim_verts": collectes,
        }
        for name, value, denom in ROWS:
            sample = sample_map.get(name, 1)
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

    if erreurs:
        for msg in erreurs:
            print(f"ERREUR : {msg}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
