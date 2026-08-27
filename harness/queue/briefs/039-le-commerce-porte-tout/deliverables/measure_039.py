#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 039 (commerce généralisé)."""

from __future__ import annotations

import argparse
import ast
import json
import random
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "039-le-commerce-porte-tout"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "476f78cfa266efcaa3c56b6103c0337d7785593a"
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
            "path": "../../../../sim/tests/test_commerce.py",
            "must_differ_from_git": f"{BASE_REF}:sim/tests/test_commerce.py",
        },
        {"path": "deliverables/pre-edit/cli_ticks20_seed0.json"},
        {"path": "deliverables/pre-edit/cli_ticks365_seed0.json"},
        {
            "path": "deliverables/cli_ticks20_seed0_apres.json",
            "identical_to": "deliverables/pre-edit/cli_ticks20_seed0.json",
        },
        {
            "path": "deliverables/cli_ticks365_seed0_apres.json",
            "identical_to": "deliverables/pre-edit/cli_ticks365_seed0.json",
        },
        {"path": "deliverables/measure_039.py"},
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


def compter_occurrences_nourriture_maillon(engine_src: str) -> int:
    tree = ast.parse(engine_src, filename="engine.py")
    total = 0
    for node in ast.walk(tree):
        if not isinstance(node, ast.FunctionDef) or node.name != "_apply_commerce":
            continue
        for sub in ast.walk(node):
            if isinstance(sub, ast.Constant) and sub.value == "nourriture":
                total += 1
            elif isinstance(sub, ast.Attribute) and sub.attr == "MARCHANDISE_NOURRITURE":
                total += 1
    return total


def compter_maillons_commerce() -> int:
    sim_dir = REPO / "sim"
    total = 0
    modules = 0
    for fichier in sorted(sim_dir.rglob("*.py")):
        if "tests" in fichier.parts:
            continue
        modules += 1
        tree = ast.parse(fichier.read_text(encoding="utf-8"), filename=str(fichier))
        for node in ast.walk(tree):
            if isinstance(node, ast.FunctionDef) and node.name == "_apply_commerce":
                total += 1
    report("maillons_commerce_dans_sim", total, f"modules_sim_parcourus={modules}")
    return total


def mesurer_marchandises_du_monde() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import MARCHANDISE_NOURRITURE
    from sim.engine import tick
    from sim.world import World

    world = World.charger(0)
    tick(world, random.Random(0), numero_tick=0)
    noms: set[str] = {MARCHANDISE_NOURRITURE}
    cellules = 0
    for entree in world.to_dict()["cells"].values():
        cellules += 1
        panier = entree.get("stocks") or {}
        noms.update(panier)
    report("marchandises_du_monde", len(noms), f"cellules_parcourues={cellules}")
    return len(noms)


def mesurer_cli_identiques() -> int:
    py = str(REPO / ".venv/bin/python")
    cmd20 = [py, "-m", "sim", "--ticks", "20", "--seed", "0", "--json"]
    cmd365 = [py, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
    base_engine = git("show", f"{BASE_REF}:sim/engine.py")

    pre20 = DELIVERABLES / "pre-edit" / "cli_ticks20_seed0.json"
    pre365 = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    if not pre20.is_file():
        pre20.parent.mkdir(parents=True, exist_ok=True)
        pre20.write_text(run_cli(cmd20, base_engine), encoding="utf-8")
    if not pre365.is_file():
        pre365.write_text(run_cli(cmd365, base_engine), encoding="utf-8")

    apres20 = json.loads(run_cli(cmd20))
    apres365 = json.loads(run_cli(cmd365))
    (DELIVERABLES / "cli_ticks20_seed0_apres.json").write_text(
        json.dumps(apres20, sort_keys=True) + "\n", encoding="utf-8"
    )
    (DELIVERABLES / "cli_ticks365_seed0_apres.json").write_text(
        json.dumps(apres365, sort_keys=True) + "\n", encoding="utf-8"
    )

    base20 = json.loads(pre20.read_text(encoding="utf-8"))
    base365 = json.loads(pre365.read_text(encoding="utf-8"))
    champs = len(base20)
    identiques = int(apres20 == base20 and apres365 == base365)
    report("champs_cli_identiques", identiques, f"champs_comparés={champs}")
    return identiques


def mesurer_mineraux_immobiles() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import MARCHANDISE_NOURRITURE, DEFAULT_CLI_TICKS
    from sim.engine import _extraction_du_tick_kg, tick
    from sim.model import lire_stock_marchandise
    from sim.world import World

    world = World.charger(0)
    ticks = DEFAULT_CLI_TICKS
    extraction_cumulee_par_cellule: dict[int, dict[str, float]] = {}
    for t in range(ticks):
        for cid, cell in world.cells.items():
            raw = world.carte.get(cid) or {}
            gisements = raw.get("gisements") or []
            if not gisements:
                continue
            extrait = _extraction_du_tick_kg(cell, world.carte)
            for ressource, kg in extrait.items():
                if cid not in extraction_cumulee_par_cellule:
                    extraction_cumulee_par_cellule[cid] = {}
                extraction_cumulee_par_cellule[cid][ressource] = (
                    extraction_cumulee_par_cellule[cid].get(ressource, 0.0) + kg
                )
        tick(world, random.Random(0), numero_tick=t)

    ecarts = 0
    cellules_mesurees = 0
    for cid, cell in world.cells.items():
        cumul = extraction_cumulee_par_cellule.get(cid, {})
        if not cumul:
            continue
        cellules_mesurees += 1
        for ressource, extrait_total in cumul.items():
            stock = lire_stock_marchandise(cell, ressource)
            if abs(stock - extrait_total) > 1e-6:
                ecarts += 1
    report(
        "kg_mineraux_ayant_change_de_cellule",
        ecarts,
        f"cellules_minières_mesurées={cellules_mesurees}",
    )
    return ecarts


def mesurer_ecart_masse() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import MARCHANDISE_ESSAI_039, MARCHANDISE_NOURRITURE
    from sim.engine import _apply_commerce
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    pop = 50
    source = Cell(cell_id=501, area_km2=0.0, population=0, food_stock_kg=200.0,
                  hunger_ticks=0, food_deficit_kg=0.0)
    ecrire_stock_marchandise(source, MARCHANDISE_ESSAI_039, 200.0)
    receveuse = Cell(cell_id=502, area_km2=0.0, population=pop, food_stock_kg=0.0,
                     hunger_ticks=0, food_deficit_kg=0.0)
    world = World(
        cells={501: source, 502: receveuse},
        adjacency=[{"a": 501, "b": 502, "kind": "land", "shared_length_m": 1000.0}],
    )
    ecarts = 0
    marchandises_jouees = 0
    for marchandise in (MARCHANDISE_NOURRITURE, MARCHANDISE_ESSAI_039):
        marchandises_jouees += 1
        somme_avant = sum(
            max(0.0, lire_stock_marchandise(c, marchandise)) for c in world.cells.values()
        )
        copie_cells = {
            cid: Cell(
                cell_id=c.cell_id,
                area_km2=c.area_km2,
                population=c.population,
                stocks=dict(c.stocks),
                hunger_ticks=c.hunger_ticks,
                food_deficit_kg=c.food_deficit_kg,
                mortality_remainder=c.mortality_remainder,
            )
            for cid, c in world.cells.items()
        }
        w = World(cells=copie_cells, adjacency=list(world.adjacency))
        _apply_commerce(w, [0.0])
        somme_apres = sum(
            max(0.0, lire_stock_marchandise(c, marchandise)) for c in w.cells.values()
        )
        if abs(somme_avant - somme_apres) > 1e-9:
            ecarts += 1
    report(
        "ecart_de_masse_par_marchandise",
        ecarts,
        f"marchandises_jouées={marchandises_jouees}",
    )
    return ecarts


def mesurer_somme_transferts_arete() -> float:
    sys.path.insert(0, str(REPO))
    from sim.constants import (
        MARCHANDISE_ESSAI_039,
        MARCHANDISE_NOURRITURE,
        TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
    )
    from sim.engine import _apply_commerce
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    pop = 100
    source = Cell(cell_id=601, area_km2=0.0, population=0, food_stock_kg=500.0,
                  hunger_ticks=0, food_deficit_kg=0.0)
    ecrire_stock_marchandise(source, MARCHANDISE_ESSAI_039, 500.0)
    receveuse = Cell(cell_id=602, area_km2=0.0, population=pop, food_stock_kg=0.0,
                     hunger_ticks=0, food_deficit_kg=0.0)
    world = World(
        cells={601: source, 602: receveuse},
        adjacency=[{"a": 601, "b": 602, "kind": "land", "shared_length_m": 1000.0}],
    )
    stock_alim_avant = 0.0
    stock_essai_avant = 0.0
    _apply_commerce(world, [0.0])
    delta_alim = max(
        0.0,
        lire_stock_marchandise(world.cells[602], MARCHANDISE_NOURRITURE) - stock_alim_avant,
    )
    delta_essai = max(
        0.0,
        lire_stock_marchandise(world.cells[602], MARCHANDISE_ESSAI_039) - stock_essai_avant,
    )
    somme = delta_alim + delta_essai
    capacite = TRADE_CAPACITY_KG_PER_EDGE_PER_TICK
    report(
        "somme_transferts_sur_arete_partagee",
        somme,
        f"capacité_arête_lue={capacite}",
    )
    return somme


def mesurer_modifications_dette() -> int:
    sys.path.insert(0, str(REPO))
    from sim.engine import _apply_commerce
    from sim.model import Cell, ecrire_stock_marchandise
    from sim.constants import MARCHANDISE_ESSAI_039
    from sim.world import World

    pop = 50
    source = Cell(cell_id=701, area_km2=0.0, population=0, food_stock_kg=200.0,
                  hunger_ticks=0, food_deficit_kg=0.0)
    ecrire_stock_marchandise(source, MARCHANDISE_ESSAI_039, 200.0)
    receveuse = Cell(cell_id=702, area_km2=0.0, population=pop, food_stock_kg=0.0,
                     hunger_ticks=0, food_deficit_kg=5.0)
    world = World(
        cells={701: source, 702: receveuse},
        adjacency=[{"a": 701, "b": 702, "kind": "land", "shared_length_m": 1000.0}],
    )
    avant = {cid: c.food_deficit_kg for cid, c in world.cells.items()}
    _apply_commerce(world, [0.0])
    modifs = sum(1 for cid, v in avant.items() if world.cells[cid].food_deficit_kg != v)
    report(
        "modifications_de_dette_par_le_commerce",
        modifs,
        "appels_maillon=1",
    )
    return modifs


def mesurer_ordres_insertion() -> int:
    sys.path.insert(0, str(REPO))
    from sim.constants import MARCHANDISE_ESSAI_039, MARCHANDISE_NOURRITURE
    from sim.engine import _apply_commerce
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    ordres = [
        [MARCHANDISE_NOURRITURE, MARCHANDISE_ESSAI_039],
        [MARCHANDISE_ESSAI_039, MARCHANDISE_NOURRITURE],
    ]
    resultats: list[dict[str, float]] = []

    for ordre in ordres:
        pop = 40
        a = Cell(cell_id=801, area_km2=0.0, population=0, food_stock_kg=0.0,
                 hunger_ticks=0, food_deficit_kg=0.0)
        b = Cell(cell_id=802, area_km2=0.0, population=pop, food_stock_kg=0.0,
                 hunger_ticks=0, food_deficit_kg=0.0)
        ecrire_stock_marchandise(a, MARCHANDISE_NOURRITURE, 300.0)
        ecrire_stock_marchandise(a, MARCHANDISE_ESSAI_039, 300.0)
        for cle in ordre:
            ecrire_stock_marchandise(b, cle, 0.0)
        w = World(
            cells={801: a, 802: b},
            adjacency=[{"a": 801, "b": 802, "kind": "land", "shared_length_m": 1000.0}],
        )
        _apply_commerce(w, [0.0])
        resultats.append({
            MARCHANDISE_NOURRITURE: lire_stock_marchandise(w.cells[802], MARCHANDISE_NOURRITURE),
            MARCHANDISE_ESSAI_039: lire_stock_marchandise(w.cells[802], MARCHANDISE_ESSAI_039),
        })

    identiques = int(resultats[0] == resultats[1])
    report(
        "ordres_d_insertion_essayes",
        len(ordres),
        f"résultats_identiques={identiques}",
    )
    return len(ordres)


def mesurer_tests() -> tuple[int, int]:
    git("stash", "push", "--", "sim/tests/")
    git("checkout", BASE_REF, "--", "sim/tests/")
    collect_base = subprocess.run(
        [str(REPO / ".venv/bin/python"), "-m", "pytest", "sim/tests/", "--collect-only", "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    m_base = re.search(r"(\d+) test", collect_base.stdout + collect_base.stderr)
    collectes_base = int(m_base.group(1)) if m_base else NOT_COMPUTED

    git("checkout", "HEAD", "--", "sim/tests/")
    git("stash", "pop")

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

    report("tests_collectes_avant", collectes_base, "fichiers_collectés_sur_SHA_base")
    report("tests_collectes_apres", total, "fichiers_collectés_après_changement")
    return verts, total


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    engine_base = git("show", f"{BASE_REF}:sim/engine.py")
    engine_apres = (REPO / "sim" / "engine.py").read_text(encoding="utf-8")

    avant = compter_occurrences_nourriture_maillon(engine_base)
    apres = compter_occurrences_nourriture_maillon(engine_apres)
    fonctions = 1
    report(
        "occurrences_nourriture_dans_le_maillon_avant",
        avant,
        f"fonctions_du_maillon={fonctions}",
    )
    report(
        "occurrences_nourriture_dans_le_maillon_apres",
        apres,
        f"fonctions_du_maillon={fonctions}",
    )

    maillons = compter_maillons_commerce()
    mesurer_marchandises_du_monde()
    mesurer_cli_identiques()
    mineraux = mesurer_mineraux_immobiles()
    ecarts = mesurer_ecart_masse()
    somme = mesurer_somme_transferts_arete()
    modifs = mesurer_modifications_dette()
    mesurer_ordres_insertion()
    verts, collectes = mesurer_tests()

    erreurs: list[str] = []
    if maillons != 1:
        erreurs.append(f"maillons_commerce_dans_sim={maillons}")
    if avant <= 0:
        erreurs.append(f"rouge SC2 non prouvé : occurrences_avant={avant}")
    if apres != 0:
        erreurs.append(f"occurrences_nourriture_dans_le_maillon_apres={apres}")
    if mineraux != 0:
        erreurs.append(f"kg_mineraux_ayant_change_de_cellule={mineraux}")
    if ecarts != 0:
        erreurs.append(f"ecart_de_masse_par_marchandise={ecarts}")
    if modifs != 0:
        erreurs.append(f"modifications_de_dette_par_le_commerce={modifs}")
    if verts != collectes:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = {
            "brief": "039-le-commerce-porte-tout",
            "base_ref": BASE_REF,
            "commands": [
                ".venv/bin/python -m sim --ticks 20 --seed 0 --json",
                ".venv/bin/python -m sim --ticks 365 --seed 0 --json",
                ".venv/bin/python -m pytest sim/tests/test_commerce.py -q",
                ".venv/bin/python -m pytest sim/tests/ -q",
                ".venv/bin/python harness/queue/briefs/039-le-commerce-porte-tout/deliverables/measure_039.py",
            ],
            "counters": [
                {"name": n, "value": v, "denominator": d}
                for n, v, d in ROWS
            ],
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
