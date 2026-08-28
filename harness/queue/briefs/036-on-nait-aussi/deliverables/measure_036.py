#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 036 (natalité).

Usage depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/036-on-nait-aussi/deliverables/measure_036.py
  .venv/bin/python .../measure_036.py --write-manifest
"""

from __future__ import annotations

import argparse
import ast
import json
import math
import os
import random
import re
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "036-on-nait-aussi"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "9df4917b8e3a4c804c9263eac5973912a8a77092"
CLI_CMD = [".venv/bin/python", "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
CHAMPS_CLI = (
    "population_arrivee",
    "cellules_affamees",
    "kg_transportes",
    "stock_kg_arrivee",
)
RNG_SEED = 42
N_TICKS_OBSERVES = 200
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
    import shutil

    venv_python = REPO / ".venv" / "bin" / "python"
    if not venv_python.is_file():
        venv_python = Path("/home/hermes/src/ForgeHistory/.venv/bin/python")
    with tempfile.TemporaryDirectory(prefix="measure036-") as td:
        overlay = Path(td)
        overlay_sim = overlay / "sim"
        shutil.copytree(
            REPO / "sim",
            overlay_sim,
            ignore=shutil.ignore_patterns("__pycache__", "*.pyc"),
        )
        if engine_src is not None:
            (overlay_sim / "engine.py").write_text(engine_src, encoding="utf-8")
        if constants_src is not None:
            (overlay_sim / "constants.py").write_text(constants_src, encoding="utf-8")
        env = os.environ.copy()
        env["PYTHONPATH"] = str(overlay) + os.pathsep + env.get("PYTHONPATH", "")
        cmd = [str(venv_python), "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
        proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, env=env)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return json.loads(proc.stdout.strip())


def mesurer_sites_augmentation_population() -> tuple[int, int]:
    """Sites dans sim/ (hors tests) où population augmente."""
    sim_dir = REPO / "sim"
    tests_dir = sim_dir / "tests"
    modules_parcourus = 0
    sites = 0
    for path in sorted(sim_dir.rglob("*.py")):
        if path.is_relative_to(tests_dir):
            continue
        modules_parcourus += 1
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.AugAssign) and isinstance(node.target, ast.Attribute):
                if node.target.attr == "population" and isinstance(node.op, ast.Add):
                    sites += 1
            if isinstance(node, ast.Assign):
                for target in node.targets:
                    if (
                        isinstance(target, ast.Attribute)
                        and target.attr == "population"
                        and isinstance(node.value, ast.BinOp)
                        and isinstance(node.value.op, ast.Add)
                    ):
                        sites += 1
    return sites, modules_parcourus


def mesurer_noms_constantes_engine() -> tuple[int, int]:
    engine_path = REPO / "sim" / "engine.py"
    tree = ast.parse(engine_path.read_text(encoding="utf-8"))
    motif = "NAISSANCES_PAR_HABITANT_PAR_TICK"
    noms_cherches = 1
    trouves = 0
    for node in ast.walk(tree):
        if isinstance(node, ast.Attribute) and node.attr == motif:
            trouves += 1
    return trouves, noms_cherches


def _cellule_rassasiee_productive(population: int, area_km2: float = 50.0):
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.model import Cell
    from sim.world import World

    besoin = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    cell = Cell(
        cell_id=1,
        area_km2=area_km2,
        population=population,
        food_stock_kg=besoin * 10,
        hunger_ticks=0,
        food_deficit_kg=0.0,
        mortality_remainder=0.0,
        natalite_remainder=0.0,
    )
    return World(cells={1: cell}, adjacency=[]), population


def mesurer_ticks_premiere_naissance() -> tuple[int, int]:
    sys.path.insert(0, str(REPO))
    from sim import constants as c
    from sim.engine import tick

    population = 100
    world, pop_init = _cellule_rassasiee_productive(population)
    rate = c.naissances_par_habitant_par_tick()
    borne = math.ceil(1.0 / (rate * population))
    rng = random.Random(42)
    ticks = 0
    for t in range(borne + 1):
        tick(world, rng)
        ticks = t + 1
        if world.cells[1].population > pop_init:
            return ticks, borne
    return NOT_COMPUTED, borne


def mesurer_naissances_cellule_affamee() -> tuple[int, int]:
    """Naissances si le maillon voit une pénurie — hors mortalité.

    Un tick complet vide la cellule avant qu'un remainder inconditionnel
    n'atteigne 1 : le zéro ne mesurerait alors rien. La borne est celle
    qui suffirait à naître si la porte était ouverte.
    """
    sys.path.insert(0, str(REPO))
    from sim import constants as c
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_natalite
    from sim.model import Cell

    population = 50
    cell = Cell(
        cell_id=1,
        area_km2=0.0,
        population=population,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
        mortality_remainder=0.0,
        natalite_remainder=0.0,
    )
    rate = c.naissances_par_habitant_par_tick()
    borne = math.ceil(1.0 / (rate * population))
    penurie = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    naissances = 0
    pop_init = population
    for _ in range(borne):
        _apply_natalite(cell, penurie)
        naissances = max(naissances, max(0, cell.population - pop_init))
    return naissances, borne


def mesurer_cellules_en_croissance(
    engine_src: str | None,
    constants_src: str | None,
    model_src: str | None = None,
    world_src: str | None = None,
) -> tuple[int, int]:
    import shutil

    venv_python = REPO / ".venv" / "bin" / "python"
    if not venv_python.is_file():
        venv_python = Path("/home/hermes/src/ForgeHistory/.venv/bin/python")
    with tempfile.TemporaryDirectory(prefix="measure036-croissance-") as td:
        overlay = Path(td)
        overlay_sim = overlay / "sim"
        shutil.copytree(
            REPO / "sim",
            overlay_sim,
            ignore=shutil.ignore_patterns("__pycache__", "*.pyc"),
        )
        if engine_src is not None:
            (overlay_sim / "engine.py").write_text(engine_src, encoding="utf-8")
        if constants_src is not None:
            (overlay_sim / "constants.py").write_text(constants_src, encoding="utf-8")
        if model_src is not None:
            (overlay_sim / "model.py").write_text(model_src, encoding="utf-8")
        if world_src is not None:
            (overlay_sim / "world.py").write_text(world_src, encoding="utf-8")
        env = os.environ.copy()
        env["PYTHONPATH"] = str(overlay) + os.pathsep + env.get("PYTHONPATH", "")
        code = """
import json, random
from sim.world import World
from sim.engine import tick
w = World.charger(0)
pops = {cid: c.population for cid, c in w.cells.items()}
rng = random.Random(0)
for _ in range(365):
    tick(w, rng)
croissance = sum(1 for cid, c in w.cells.items() if c.population > pops[cid])
print(json.dumps({"croissance": croissance, "cellules": len(w.cells)}))
"""
        proc = subprocess.run(
            [str(venv_python), "-c", code],
            cwd=REPO,
            capture_output=True,
            text=True,
            env=env,
        )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        data = json.loads(proc.stdout.strip())
        return data["croissance"], data["cellules"]


def mesurer_fractions_survie() -> tuple[float, float, float, int]:
    sys.path.insert(0, str(REPO))
    from sim import constants as c
    from sim.engine import production_moyenne_kg_par_tick, tick
    from sim.world import World

    def _fraction(facteur: float) -> float:
        nominal = c.NAISSANCES_PAR_HABITANT_PAR_TICK
        c.NAISSANCES_PAR_HABITANT_PAR_TICK = nominal * facteur
        try:
            world = World.charger(rng_seed=RNG_SEED)
            rng = random.Random(RNG_SEED)
            pop_init = sum(cell.population for cell in world.cells.values())
            for _ in range(N_TICKS_OBSERVES):
                tick(world, rng)
            pop_fin = sum(cell.population for cell in world.cells.values())
            return pop_fin / pop_init
        finally:
            c.NAISSANCES_PAR_HABITANT_PAR_TICK = nominal

    s_nul = _fraction(0.0)
    s_nom = _fraction(1.0)
    s_double = _fraction(2.0)
    world = World.charger(rng_seed=RNG_SEED)
    pop_init = sum(cell.population for cell in world.cells.values())
    return s_nul, s_nom, s_double, pop_init


def mesurer_plafond_derive() -> tuple[float, float, float]:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import production_moyenne_kg_par_tick
    from sim.world import World

    world = World.charger(rng_seed=RNG_SEED)
    pop_init = sum(cell.population for cell in world.cells.values())
    production = production_moyenne_kg_par_tick(world)
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK * pop_init
    return production / ration, production, ration


def mesurer_cli() -> tuple[int, int, int, dict, dict]:
    apres_a = run_cli_with_modules(None, None)
    apres_b = run_cli_with_modules(None, None)
    pre_edit = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    if pre_edit.is_file():
        base = json.loads(pre_edit.read_text(encoding="utf-8"))
    else:
        base_engine = git("show", f"{BASE_REF}:sim/engine.py")
        base_constants = git("show", f"{BASE_REF}:sim/constants.py")
        base = run_cli_with_modules(base_engine, base_constants)
        pre_edit.parent.mkdir(parents=True, exist_ok=True)
        pre_edit.write_text(json.dumps(base, ensure_ascii=False) + "\n", encoding="utf-8")
    identiques = 1 if apres_a == apres_b else 0
    modifiees = sum(1 for k in CHAMPS_CLI if apres_a.get(k) != base.get(k))
    return identiques, 2, modifiees, apres_a, base


def mesurer_tests() -> tuple[int, int]:
    venv_python = REPO / ".venv" / "bin" / "python"
    if not venv_python.is_file():
        venv_python = Path("/home/hermes/src/ForgeHistory/.venv/bin/python")
    proc = subprocess.run(
        [str(venv_python), "-m", "pytest", "sim/tests/", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m = re.search(r"(\d+) passed", proc.stdout + proc.stderr)
    verts = int(m.group(1)) if m else NOT_COMPUTED
    collect = subprocess.run(
        [str(venv_python), "-m", "pytest", "sim/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m2 = re.search(r"(\d+) test", collect.stdout + collect.stderr)
    total = int(m2.group(1)) if m2 else NOT_COMPUTED
    ok = 1 if proc.returncode == 0 else 0
    return ok * verts if ok else 0, total


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    sites, modules = mesurer_sites_augmentation_population()
    report("sites_d_augmentation_de_population", sites, f"modules parcourus = {modules}")

    ticks, borne_ticks = mesurer_ticks_premiere_naissance()
    report(
        "ticks_jusqu_a_la_premiere_naissance",
        ticks,
        f"borne dérivée = {borne_ticks}",
    )

    naissances_affamee, horizon_affamee = mesurer_naissances_cellule_affamee()
    report(
        "naissances_en_cellule_affamee",
        naissances_affamee,
        f"ticks joués = {horizon_affamee}",
    )

    base_engine = git("show", f"{BASE_REF}:sim/engine.py")
    base_constants = git("show", f"{BASE_REF}:sim/constants.py")
    base_model = git("show", f"{BASE_REF}:sim/model.py")
    base_world = git("show", f"{BASE_REF}:sim/world.py")
    pre_croissance = DELIVERABLES / "pre-edit" / "cellules_en_croissance.json"
    if pre_croissance.is_file():
        archived = json.loads(pre_croissance.read_text(encoding="utf-8"))
        avant = archived["cellules_en_croissance"]
        cellules = archived["cellules"]
    else:
        avant, cellules = mesurer_cellules_en_croissance(
            base_engine, base_constants, base_model, base_world
        )
        pre_croissance.parent.mkdir(parents=True, exist_ok=True)
        pre_croissance.write_text(
            json.dumps({"cellules_en_croissance": avant, "cellules": cellules}) + chr(10),
            encoding="utf-8",
        )
    report(
        "cellules_en_croissance_avant",
        avant,
        f"cellules chargées = {cellules}",
    )
    apres, cellules2 = mesurer_cellules_en_croissance(None, None)
    report(
        "cellules_en_croissance_apres",
        apres,
        f"cellules chargées = {cellules2}",
    )

    s_nul, s_nom, s_double, pop_init = mesurer_fractions_survie()
    report(
        "fraction_survie_taux_nul",
        round(s_nul, 6),
        f"population de départ = {pop_init}",
    )
    report(
        "fraction_survie_taux_nominal",
        round(s_nom, 6),
        f"population de départ = {pop_init}",
    )
    report(
        "fraction_survie_taux_double",
        round(s_double, 6),
        f"population de départ = {pop_init}",
    )

    plafond, production, ration = mesurer_plafond_derive()
    report(
        "plafond_derive",
        round(plafond, 6),
        f"production_moyenne={production:.0f} / ration={ration:.0f}",
    )

    identiques, runs, modifiees, apres_cli, base_cli = mesurer_cli()
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

    noms_engine, noms_cherches = mesurer_noms_constantes_engine()
    report(
        "noms_de_constantes_natalite_dans_engine",
        noms_engine,
        f"noms cherchés = {noms_cherches}",
    )

    verts, collectes = mesurer_tests()
    report("tests_sim_verts", verts, f"tests collectés = {collectes}")

    pre_edit = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    pre_edit.parent.mkdir(parents=True, exist_ok=True)
    if not pre_edit.is_file():
        pre_edit.write_text(json.dumps(base_cli, ensure_ascii=False) + "\n", encoding="utf-8")
    (DELIVERABLES / "cli_ticks365_seed0_apres.json").write_text(
        json.dumps(apres_cli, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    errors = []
    if sites != 1:
        errors.append(f"sites_d_augmentation_de_population={sites} (attendu 1)")
    if naissances_affamee != 0:
        errors.append(f"naissances_en_cellule_affamee={naissances_affamee} (attendu 0)")
    if noms_engine != 0:
        errors.append(f"noms_de_constantes_natalite_dans_engine={noms_engine} (attendu 0)")
    if apres <= avant:
        errors.append(
            f"cellules_en_croissance_apres={apres} <= avant={avant}"
        )
    if not (s_nul < s_nom < s_double):
        errors.append(
            f"fractions survie non ordonnées : {s_nul} / {s_nom} / {s_double}"
        )
    for err in errors:
        print(f"ERREUR : {err}", file=sys.stderr)

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        counters = []
        sample_map = {
            "sites_d_augmentation_de_population": modules,
            "ticks_jusqu_a_la_premiere_naissance": borne_ticks,
            "naissances_en_cellule_affamee": horizon_affamee,
            "cellules_en_croissance_avant": cellules,
            "cellules_en_croissance_apres": cellules2,
            "fraction_survie_taux_nul": pop_init,
            "fraction_survie_taux_nominal": pop_init,
            "fraction_survie_taux_double": pop_init,
            "plafond_derive": 1,
            "sorties_cli_deterministes": runs,
            "champs_cli_modifies": len(CHAMPS_CLI),
            "noms_de_constantes_natalite_dans_engine": noms_cherches,
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

    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
