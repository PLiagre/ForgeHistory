#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 045 (migration lit le reste du tick)."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "045-la-migration-lit-le-reste-du-tick"
DELIVERABLES = BRIEF / "deliverables"
ROWS: list[tuple[str, object, int, str]] = []


def report(name: str, value: object, sample_size: int, denominator: str) -> None:
    ROWS.append((name, value, sample_size, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def base_sha() -> str:
    return git("rev-parse", "HEAD").strip()


def run_in_temp_worktree(engine_src: str, script: str) -> str:
    """Exécute un script Python avec un engine.py fourni, hors de l'arbre courant."""
    import os

    with tempfile.TemporaryDirectory() as tmp:
        tmp_path = Path(tmp)
        sim_dir = tmp_path / "sim"
        sim_dir.mkdir()
        (sim_dir / "__init__.py").write_text("", encoding="utf-8")
        (sim_dir / "engine.py").write_text(engine_src, encoding="utf-8")
        for name in ("constants.py", "model.py", "world.py", "__main__.py"):
            src = REPO / "sim" / name
            if src.is_file():
                (sim_dir / name).write_text(src.read_text(encoding="utf-8"), encoding="utf-8")
        proc = subprocess.run(
            [sys.executable, "-c", script],
            cwd=tmp_path,
            capture_output=True,
            text=True,
            env={**os.environ, "PYTHONPATH": str(tmp_path)},
        )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return proc.stdout


def mesurer_rouge_sc1_base(sha: str) -> str:
    engine_base = git("show", f"{sha}:sim/engine.py")
    script = """
from sim.engine import _apply_migration, _surplus_nourriture_tick
from sim.model import Cell, lire_stock_marchandise
from sim.world import World
from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK, FRACTION_MIGRANTE_PAR_TICK, MARCHANDISE_NOURRITURE

pop_source = 100
ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
reste_dest = ration * 0.5
source = Cell(cell_id=701, area_km2=0.0, population=pop_source, food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=ration*pop_source, migration_remainder=0.0)
dest = Cell(cell_id=702, area_km2=0.0, population=50, food_stock_kg=reste_dest, hunger_ticks=0, food_deficit_kg=0.0, migration_remainder=0.0)
world = World(cells={701: source, 702: dest}, adjacency=[{"a": 701, "b": 702, "kind": "land", "shared_length_m": 1000.0}])
penurie = pop_source * ration
stock_dest = lire_stock_marchandise(dest, MARCHANDISE_NOURRITURE)
surplus_bug = _surplus_nourriture_tick(dest.population, stock_dest)
pop_src_avant = world.cells[701].population
pop_dst_avant = world.cells[702].population
_apply_migration(world, {701: penurie, 702: 0.0})
delta_src = pop_src_avant - world.cells[701].population
delta_dst = world.cells[702].population - pop_dst_avant
partants_attendus = int(pop_source * FRACTION_MIGRANTE_PAR_TICK)
print(f"ration={ration}, reste_dest={reste_dest}")
print(f"surplus_buggy={surplus_bug}")
print(f"partants_attendus={partants_attendus}")
print(f"delta_source={delta_src}, delta_dest={delta_dst}")
print(f"ROUGE={'oui' if delta_src == 0 and delta_dst == 0 else 'non'}")
"""
    return run_in_temp_worktree(engine_base, script)


def _paniers(world) -> dict[int, dict[str, float]]:
    return {cid: dict(c.stocks) for cid, c in world.cells.items()}


def mesurer_sc1() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FRACTION_MIGRANTE_PAR_TICK, FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop_source = 100
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    reste = ration * 0.5
    source = Cell(
        cell_id=701, area_km2=0.0, population=pop_source,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=pop_source * ration,
        migration_remainder=0.0,
    )
    dest = Cell(
        cell_id=702, area_km2=0.0, population=50,
        food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={701: source, 702: dest},
        adjacency=[{"a": 701, "b": 702, "kind": "land", "shared_length_m": 1000.0}],
    )
    penurie = pop_source * ration
    pop_src_avant = world.cells[701].population
    _apply_migration(world, {701: penurie, 702: 0.0})
    delta_src = pop_src_avant - world.cells[701].population
    partants_attendus = int(pop_source * FRACTION_MIGRANTE_PAR_TICK)
    report("destinations_reste_positif", 1, 1, "destinations positives essayées dans SC1")
    report(
        "habitants_deplaces_reste_positif",
        delta_src,
        partants_attendus,
        "partants entiers dérivés de la population et de FRACTION_MIGRANTE_PAR_TICK",
    )
    return delta_src


def mesurer_sc2() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop_source = 100
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    penurie = pop_source * ration
    deplaces_total = 0

    source_nul = Cell(
        cell_id=711, area_km2=0.0, population=pop_source,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=penurie,
        migration_remainder=0.0,
    )
    dest_nul = Cell(
        cell_id=712, area_km2=0.0, population=50,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world_nul = World(
        cells={711: source_nul, 712: dest_nul},
        adjacency=[{"a": 711, "b": 712, "kind": "land", "shared_length_m": 1000.0}],
    )
    pop_avant = world_nul.cells[711].population
    _apply_migration(world_nul, {711: penurie, 712: 0.0})
    deplaces_total += pop_avant - world_nul.cells[711].population

    source_sent = Cell(
        cell_id=721, area_km2=0.0, population=pop_source,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=penurie,
        migration_remainder=0.0,
    )
    dest_sent = Cell(cell_id=722, area_km2=0.0, population=50)
    world_sent = World(
        cells={721: source_sent, 722: dest_sent},
        adjacency=[{"a": 721, "b": 722, "kind": "land", "shared_length_m": 1000.0}],
    )
    pop_avant = world_sent.cells[721].population
    _apply_migration(world_sent, {721: penurie, 722: 0.0})
    deplaces_total += pop_avant - world_sent.cells[721].population

    report("destinations_stock_nul", 1, 1, "destinations nulles essayées dans SC2")
    report("destinations_sentinelle", 1, 1, "destinations négatives essayées dans SC2")
    report(
        "habitants_deplaces_stock_nul",
        deplaces_total,
        2,
        "appels de migration réellement joués (nul + sentinelle)",
    )
    return deplaces_total


def mesurer_sc3() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FRACTION_MIGRANTE_PAR_TICK, FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    pop_source = 200
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    reste = ration * 50
    source = Cell(
        cell_id=731, area_km2=0.0, population=pop_source,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=pop_source * ration,
        migration_remainder=0.0,
    )
    dest_a = Cell(
        cell_id=732, area_km2=0.0, population=10,
        food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    dest_b = Cell(
        cell_id=733, area_km2=0.0, population=200,
        food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={731: source, 732: dest_a, 733: dest_b},
        adjacency=[
            {"a": 731, "b": 732, "kind": "land", "shared_length_m": 1000.0},
            {"a": 731, "b": 733, "kind": "land", "shared_length_m": 1000.0},
        ],
    )
    pops_avant = {cid: c.population for cid, c in world.cells.items()}
    _apply_migration(world, {731: pop_source * ration, 732: 0.0, 733: 0.0})
    delta_a = world.cells[732].population - pops_avant[732]
    delta_b = world.cells[733].population - pops_avant[733]
    egalite = int(delta_a == delta_b)
    report(
        "poids_independants_population",
        egalite,
        2,
        "populations distinctes réellement essayées dans SC3",
    )
    return egalite


def mesurer_sc4() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import FRACTION_MIGRANTE_PAR_TICK, FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration, _repartir_habitants_proportionnellement
    from sim.model import Cell
    from sim.world import World

    pop_source = 400
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    reste_a = ration * 3
    reste_b = ration * 1
    partants = int(pop_source * FRACTION_MIGRANTE_PAR_TICK)
    poids = {742: reste_a, 743: reste_b}
    attendu = _repartir_habitants_proportionnellement(partants, poids)
    source = Cell(
        cell_id=741, area_km2=0.0, population=pop_source,
        food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=pop_source * ration,
        migration_remainder=0.0,
    )
    dest_a = Cell(
        cell_id=742, area_km2=0.0, population=60,
        food_stock_kg=reste_a, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    dest_b = Cell(
        cell_id=743, area_km2=0.0, population=120,
        food_stock_kg=reste_b, hunger_ticks=0, food_deficit_kg=0.0,
        migration_remainder=0.0,
    )
    world = World(
        cells={741: source, 742: dest_a, 743: dest_b},
        adjacency=[
            {"a": 741, "b": 742, "kind": "land", "shared_length_m": 1000.0},
            {"a": 741, "b": 743, "kind": "land", "shared_length_m": 1000.0},
        ],
    )
    pops_avant = {cid: c.population for cid, c in world.cells.items()}
    _apply_migration(world, {741: pop_source * ration, 742: 0.0, 743: 0.0})
    ok = int(
        world.cells[742].population - pops_avant[742] == attendu[742]
        and world.cells[743].population - pops_avant[743] == attendu[743]
    )
    report(
        "rapport_stocks_destination",
        ok,
        2,
        "destinations pondérées réellement observées dans SC4",
    )
    return ok


def mesurer_sc5() -> tuple[int, int]:
    sys.path.insert(0, str(REPO))
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    from sim.engine import _apply_migration
    from sim.model import Cell
    from sim.world import World

    mondes: list[World] = []
    ration = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK

    def _ajouter(source_id: int, dest_id: int, pop_source: int, reste: float) -> None:
        source = Cell(
            cell_id=source_id, area_km2=0.0, population=pop_source,
            food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=pop_source * ration,
            migration_remainder=0.0,
        )
        dest = Cell(
            cell_id=dest_id, area_km2=0.0, population=50,
            food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
            migration_remainder=0.0,
        )
        mondes.append(World(
            cells={source_id: source, dest_id: dest},
            adjacency=[{"a": source_id, "b": dest_id, "kind": "land", "shared_length_m": 1000.0}],
        ))

    _ajouter(701, 702, 100, ration * 0.5)
    _ajouter(711, 712, 100, 0.0)

    ecart_total = 0
    stock_changes = 0
    cellules_sommees = 0
    cellules_comparees = 0

    for world in mondes:
        penurie = {
            cid: (c.population * ration if c.food_stock_kg <= 0 and c.population > 0 else 0.0)
            for cid, c in world.cells.items()
        }
        pops_avant = {cid: c.population for cid, c in world.cells.items()}
        stocks_avant = _paniers(world)
        cellules_sommees += len(world.cells)
        _apply_migration(world, penurie)
        pops_apres = {cid: c.population for cid, c in world.cells.items()}
        stocks_apres = _paniers(world)
        ecart_total += abs(sum(pops_avant.values()) - sum(pops_apres.values()))
        for cid in world.cells:
            cellules_comparees += 1
            if stocks_avant[cid] != stocks_apres[cid]:
                stock_changes += 1

    def _jouer_ordre(adjacency: list[dict]) -> dict[int, int]:
        pop_source = 200
        reste = ration * 2
        source = Cell(
            cell_id=751, area_km2=0.0, population=pop_source,
            food_stock_kg=0.0, hunger_ticks=1, food_deficit_kg=pop_source * ration,
            migration_remainder=0.0,
        )
        dest_a = Cell(
            cell_id=752, area_km2=0.0, population=30,
            food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
            migration_remainder=0.0,
        )
        dest_b = Cell(
            cell_id=753, area_km2=0.0, population=80,
            food_stock_kg=reste, hunger_ticks=0, food_deficit_kg=0.0,
            migration_remainder=0.0,
        )
        w = World(cells={751: source, 752: dest_a, 753: dest_b}, adjacency=adjacency)
        _apply_migration(w, {751: pop_source * ration, 752: 0.0, 753: 0.0})
        return {cid: c.population for cid, c in w.cells.items()}

    edges = [
        {"a": 751, "b": 752, "kind": "land", "shared_length_m": 1000.0},
        {"a": 751, "b": 753, "kind": "land", "shared_length_m": 1000.0},
    ]
    ordre_identique = int(_jouer_ordre(edges) == _jouer_ordre(list(reversed(edges))))
    report("ordres_aretes_essayes", ordre_identique, 2, "ordres réellement exécutés")
    report("ecart_population_totale", ecart_total, cellules_sommees, "cellules réellement sommées")
    report(
        "cellules_dont_stock_change",
        stock_changes,
        cellules_comparees,
        "cellules réellement comparées",
    )
    return ecart_total, stock_changes


def mesurer_tests() -> tuple[int, int]:
    py = str(REPO / ".venv/bin/python")
    proc = subprocess.run(
        [py, "-m", "pytest", "sim/tests/", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m = re.search(r"(\d+) passed", proc.stdout + proc.stderr)
    verts = int(m.group(1)) if m and proc.returncode == 0 else 0
    collect = subprocess.run(
        [py, "-m", "pytest", "sim/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m2 = re.search(r"(\d+) test", collect.stdout + collect.stderr)
    total = int(m2.group(1)) if m2 else 0
    report("tests_sim_verts", verts, total, "tests réellement collectés")
    return verts, total


def files_pour_la_porte(sha: str) -> list[dict[str, str]]:
    return [
        {
            "path": "../../../../sim/engine.py",
            "must_differ_from_git": f"{sha}:sim/engine.py",
        },
        {
            "path": "../../../../sim/tests/test_commerce.py",
            "must_differ_from_git": f"{sha}:sim/tests/test_commerce.py",
        },
        {"path": "deliverables/cli_ticks365_seed0_run1.json"},
        {"path": "deliverables/cli_ticks365_seed0_run2.json"},
        {"path": "deliverables/measure_045.py"},
        {"path": "deliverables/generator-log.md"},
        {"path": "deliverables/manifest.json"},
    ]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    sha = base_sha()
    rouge_out = mesurer_rouge_sc1_base(sha)
    delta_sc1 = mesurer_sc1()
    deplaces_nul = mesurer_sc2()
    egalite_sc3 = mesurer_sc3()
    rapport_sc4 = mesurer_sc4()
    ecart_pop, stock_chg = mesurer_sc5()
    verts, total = mesurer_tests()

    erreurs: list[str] = []
    if delta_sc1 < 1:
        erreurs.append(f"habitants_deplaces_reste_positif={delta_sc1}")
    if deplaces_nul != 0:
        erreurs.append(f"habitants_deplaces_stock_nul={deplaces_nul}")
    if egalite_sc3 != 1:
        erreurs.append("poids_independants_population=0")
    if rapport_sc4 != 1:
        erreurs.append("rapport_stocks_destination=0")
    if ecart_pop != 0:
        erreurs.append(f"ecart_population_totale={ecart_pop}")
    if stock_chg != 0:
        erreurs.append(f"cellules_dont_stock_change={stock_chg}")
    if verts != total:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, sample_size, denom in ROWS:
        print(f"{name}={value}  (sample_size={sample_size}; {denom})")

    if args.json:
        print(json.dumps(
            {n: {"value": v, "sample_size": s, "denominator": d} for n, v, s, d in ROWS},
            indent=2,
        ))

    if args.write_manifest:
        manifest = {
            "brief": "045-la-migration-lit-le-reste-du-tick",
            "base_ref": sha,
            "commands": [
                "git rev-parse HEAD",
                ".venv/bin/python -m pytest sim/tests/test_commerce.py -q",
                ".venv/bin/python -m pytest sim/tests/ -q",
                ".venv/bin/python -m sim --ticks 365 --seed 0 --json",
                "git grep -n '^[[:space:]]*global ' sim/engine.py",
                ".venv/bin/python harness/queue/briefs/045-la-migration-lit-le-reste-du-tick/deliverables/measure_045.py",
            ],
            "files": files_pour_la_porte(sha),
            "counters": [
                {"name": n, "value": v, "sample_size": s, "denominator": d}
                for n, v, s, d in ROWS
            ],
            "rouge_sc1_base": rouge_out.strip(),
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
