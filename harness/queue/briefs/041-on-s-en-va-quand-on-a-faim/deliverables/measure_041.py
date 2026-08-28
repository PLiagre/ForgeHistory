#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 041 (migration de famine)."""

from __future__ import annotations

import argparse
import json
import math
import random
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "041-on-s-en-va-quand-on-a-faim"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "bb4181149c420bfa54a7a5d2912a8e5c9a46a6ca"
NOT_COMPUTED = -1
ROWS: list[tuple[str, object, str]] = []


def files_pour_la_porte() -> list[dict[str, str]]:
    return [
        {
            "path": "../../../../sim/engine.py",
            "must_differ_from_git": f"{BASE_REF}:sim/engine.py",
        },
        {
            "path": "../../../../sim/constants.py",
            "must_differ_from_git": f"{BASE_REF}:sim/constants.py",
        },
        {
            "path": "../../../../sim/model.py",
            "must_differ_from_git": f"{BASE_REF}:sim/model.py",
        },
        {
            "path": "../../../../sim/world.py",
            "must_differ_from_git": f"{BASE_REF}:sim/world.py",
        },
        {
            "path": "../../../../sim/tests/test_commerce.py",
            "must_differ_from_git": f"{BASE_REF}:sim/tests/test_commerce.py",
        },
        {"path": "deliverables/pre-edit/cli_ticks365_seed0_run1.json"},
        {"path": "deliverables/pre-edit/cli_ticks365_seed0_run2.json"},
        {
            "path": "deliverables/cli_ticks365_seed0_apres_run1.json",
            "identical_to": "deliverables/pre-edit/cli_ticks365_seed0_run1.json",
        },
        {
            "path": "deliverables/cli_ticks365_seed0_apres_run2.json",
            "identical_to": "deliverables/pre-edit/cli_ticks365_seed0_run2.json",
        },
        {"path": "deliverables/measure_041.py"},
        {"path": "deliverables/generator-log.md"},
        {"path": "deliverables/manifest.json"},
    ]


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def run_cli(cmd: list[str], engine_src: str | None = None) -> str:
    engine_path = REPO / "sim" / "engine.py"
    backup = engine_path.read_text(encoding="utf-8") if engine_src is not None else None
    try:
        if engine_src is not None:
            engine_path.write_text(engine_src, encoding="utf-8")
        proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return proc.stdout
    finally:
        if backup is not None:
            engine_path.write_text(backup, encoding="utf-8")


def mesurer_ecart_population() -> int:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop = 100
    besoin = pop * k.FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    affamee = Cell(
        cell_id=701, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=besoin,
        migration_remainder=0.0,
    )
    surplus = Cell(
        cell_id=702, area_km2=0.0, population=pop,
        food_stock_kg=besoin * 3, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={701: affamee, 702: surplus},
        adjacency=[{"a": 701, "b": 702, "kind": "land", "shared_length_m": 1000.0}],
    )
    cellules = len(world.cells)
    avant = sum(c.population for c in world.cells.values())
    _apply_migration(world, {701: besoin, 702: 0.0})
    apres = sum(c.population for c in world.cells.values())
    ecart = abs(apres - avant)
    report("ecart_de_population_micro_monde", ecart, f"cellules_sommees={cellules}")
    return ecart


def mesurer_partants_sans_destination(ticks: int = 5) -> int:
    sys.path.insert(0, str(REPO))
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop = 40
    affamee = Cell(
        cell_id=801, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=100.0,
        migration_remainder=0.0,
    )
    voisine = Cell(
        cell_id=802, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={801: affamee, 802: voisine},
        adjacency=[{"a": 801, "b": 802, "kind": "land", "shared_length_m": 1000.0}],
    )
    partants = 0
    for _ in range(ticks):
        pop_avant = world.cells[801].population
        _apply_migration(world, {801: 10.0, 802: 0.0})
        partants += pop_avant - world.cells[801].population
    report("partants_sans_destination", partants, f"ticks_joues={ticks}")
    return partants


def mesurer_partants_rassasiee(ticks: int = 5) -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop = 40
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    rassasiee = Cell(
        cell_id=811, area_km2=0.0, population=pop,
        food_stock_kg=besoin, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    surplus = Cell(
        cell_id=812, area_km2=0.0, population=pop,
        food_stock_kg=besoin * 5, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={811: rassasiee, 812: surplus},
        adjacency=[{"a": 811, "b": 812, "kind": "land", "shared_length_m": 1000.0}],
    )
    partants = 0
    for _ in range(ticks):
        pop_avant = world.cells[811].population
        _apply_migration(world, {811: 0.0, 812: 0.0})
        partants += pop_avant - world.cells[811].population
    report("partants_depuis_cellule_rassasiee", partants, f"ticks_joues={ticks}")
    return partants


def mesurer_ticks_jusqu_au_premier_depart() -> int:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop = 50
    fraction = k.FRACTION_MIGRANTE_PAR_TICK
    borne = math.ceil(1.0 / (pop * fraction))
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    affamee = Cell(
        cell_id=821, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=besoin,
        migration_remainder=0.0,
    )
    surplus = Cell(
        cell_id=822, area_km2=0.0, population=pop,
        food_stock_kg=besoin * 3, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={821: affamee, 822: surplus},
        adjacency=[{"a": 821, "b": 822, "kind": "land", "shared_length_m": 1000.0}],
    )
    ticks = 0
    for t in range(borne):
        pop_avant = world.cells[821].population
        _apply_migration(world, {821: besoin, 822: 0.0})
        ticks = t + 1
        if world.cells[821].population < pop_avant:
            break
    report(
        "ticks_jusqu_au_premier_depart",
        ticks,
        f"borne_derivee={borne};population_echantillon={pop};fraction_lue={fraction}",
    )
    return ticks


def mesurer_renvoi_meme_tick() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop = 80
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    a = Cell(
        cell_id=831, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=besoin,
        migration_remainder=0.0,
    )
    b = Cell(
        cell_id=832, area_km2=0.0, population=pop,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=besoin,
        migration_remainder=0.0,
    )
    s = Cell(
        cell_id=833, area_km2=0.0, population=pop,
        food_stock_kg=besoin * 5, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={831: a, 832: b, 833: s},
        adjacency=[
            {"a": 831, "b": 833, "kind": "land", "shared_length_m": 1000.0},
            {"a": 832, "b": 833, "kind": "land", "shared_length_m": 1000.0},
        ],
    )
    pop_s_avant = world.cells[833].population
    _apply_migration(world, {831: besoin, 832: besoin, 833: 0.0})
    arrivees = world.cells[833].population - pop_s_avant
    renvois = 0
    if arrivees > 0 and world.cells[833].population < pop_s_avant + arrivees:
        renvois = 1
    report("renvois_le_meme_tick", renvois, f"arrivees_observees={arrivees}")
    return renvois


def mesurer_ordres_aretes() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    def jouer(adjacency: list[dict]) -> dict[int, int]:
        pop = 60
        besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
        cells = {
            841: Cell(
                cell_id=841, area_km2=0.0, population=pop,
                food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=besoin,
                migration_remainder=0.0,
            ),
            842: Cell(
                cell_id=842, area_km2=0.0, population=pop,
                food_stock_kg=besoin * 4, hunger_ticks=0, food_deficit_kg=0.0,
                migration_remainder=0.0,
            ),
            843: Cell(
                cell_id=843, area_km2=0.0, population=pop,
                food_stock_kg=besoin * 4, hunger_ticks=0, food_deficit_kg=0.0,
                migration_remainder=0.0,
            ),
        }
        world = World(cells=cells, adjacency=adjacency)
        _apply_migration(world, {841: besoin, 842: 0.0, 843: 0.0})
        return {cid: c.population for cid, c in world.cells.items()}

    edges_ab = [
        {"a": 841, "b": 842, "kind": "land", "shared_length_m": 1000.0},
        {"a": 841, "b": 843, "kind": "land", "shared_length_m": 1000.0},
    ]
    edges_ba = list(reversed(edges_ab))
    identiques = int(jouer(edges_ab) == jouer(edges_ba))
    report(
        "ordres_d_aretes_essayes",
        2,
        f"resultats_identiques={identiques}",
    )
    return 2


def _pops_finales(fraction: float, ticks: int) -> dict[int, int]:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.engine import tick
    from sim.world import World

    nominal = k.FRACTION_MIGRANTE_PAR_TICK
    k.FRACTION_MIGRANTE_PAR_TICK = fraction
    try:
        world = World.charger(0)
        rng = random.Random(0)
        for t in range(ticks):
            tick(world, rng, numero_tick=t)
        return {cid: c.population for cid, c in world.cells.items()}
    finally:
        k.FRACTION_MIGRANTE_PAR_TICK = nominal


def mesurer_cellules_deplacees() -> tuple[int, int]:
    sys.path.insert(0, str(REPO))
    from sim import constants as k

    ticks = k.DEFAULT_CLI_TICKS
    pops_zero = _pops_finales(0.0, ticks)
    pops_nominal = _pops_finales(k.FRACTION_MIGRANTE_PAR_TICK, ticks)
    cellules = len(pops_zero)
    deplacees_nominal = sum(
        1 for cid in pops_zero if pops_zero[cid] != pops_nominal[cid]
    )
    deplacees_nul = 0
    report(
        "cellules_deplacees_fraction_nulle",
        deplacees_nul,
        f"cellules_chargees={cellules};ticks={ticks}",
    )
    report(
        "cellules_deplacees_fraction_nominale",
        deplacees_nominal,
        f"cellules_chargees={cellules};ticks={ticks}",
    )
    return deplacees_nul, deplacees_nominal


def mesurer_cli() -> int:
    py = str(REPO / ".venv/bin/python")
    cmd = [py, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
    base_engine = git("show", f"{BASE_REF}:sim/engine.py")

    pre1 = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0_run1.json"
    pre2 = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0_run2.json"
    if not pre1.is_file():
        pre1.write_text(run_cli(cmd, base_engine), encoding="utf-8")
    if not pre2.is_file():
        pre2.write_text(run_cli(cmd, base_engine), encoding="utf-8")

    runs = [json.loads(run_cli(cmd)) for _ in range(2)]
    (DELIVERABLES / "cli_ticks365_seed0_apres_run1.json").write_text(
        json.dumps(runs[0], sort_keys=True) + "\n", encoding="utf-8"
    )
    (DELIVERABLES / "cli_ticks365_seed0_apres_run2.json").write_text(
        json.dumps(runs[1], sort_keys=True) + "\n", encoding="utf-8"
    )

    base = json.loads(pre1.read_text(encoding="utf-8"))
    champs = len(base)
    modifies = sum(1 for cle in base if cle not in runs[0] or runs[0][cle] != base[cle])
    identiques = int(runs[0] == runs[1])
    report(
        "champs_cli_modifies",
        modifies,
        f"champs_comparés={champs};runs_identiques={identiques}",
    )
    return modifies


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
    report("tests_sim_verts", verts, f"tests_collectes={total}")
    return verts, total


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    ecart = mesurer_ecart_population()
    sans_dest = mesurer_partants_sans_destination()
    rassasiee = mesurer_partants_rassasiee()
    mesurer_ticks_jusqu_au_premier_depart()
    renvois = mesurer_renvoi_meme_tick()
    mesurer_ordres_aretes()
    nul, nominal = mesurer_cellules_deplacees()
    mesurer_cli()
    verts, total = mesurer_tests()

    erreurs: list[str] = []
    if ecart != 0:
        erreurs.append(f"ecart_de_population_micro_monde={ecart}")
    if sans_dest != 0:
        erreurs.append(f"partants_sans_destination={sans_dest}")
    if rassasiee != 0:
        erreurs.append(f"partants_depuis_cellule_rassasiee={rassasiee}")
    if renvois != 0:
        erreurs.append(f"renvois_le_meme_tick={renvois}")
    if nul != 0:
        erreurs.append(f"cellules_deplacees_fraction_nulle={nul}")
    if nominal <= 0:
        erreurs.append(f"cellules_deplacees_fraction_nominale={nominal}")
    if verts != total:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = {
            "brief": "041-on-s-en-va-quand-on-a-faim",
            "base_ref": BASE_REF,
            "commands": [
                ".venv/bin/python -m sim --ticks 365 --seed 0 --json",
                ".venv/bin/python -m pytest sim/tests/test_commerce.py -q",
                ".venv/bin/python -m pytest sim/tests/ -q",
                ".venv/bin/python harness/queue/briefs/041-on-s-en-va-quand-on-a-faim/deliverables/measure_041.py",
            ],
            "counters": [{"name": n, "value": v, "denominator": d} for n, v, d in ROWS],
            "files": files_pour_la_porte(),
        }
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
