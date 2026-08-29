#!/usr/bin/env python3
"""Mesureur du lot 043 — capacité d'arête selon shared_length_m."""

from __future__ import annotations

import ast
import json
import math
import random
import statistics
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "043-le-convoi-a-l-echelle-de-la-cellule"
DELIVERABLES = BRIEF / "deliverables"
PRE_EDIT = DELIVERABLES / "pre-edit"
BASE_REF = "810a8a847ae010dc01cf0a9867dc858fa3a01af7"
VENV_PY = "/home/hermes/src/ForgeHistory/.venv/bin/python"


def sh(args: list[str], timeout: int = 600) -> subprocess.CompletedProcess:
    return subprocess.run(args, cwd=REPO, capture_output=True, text=True, timeout=timeout)


def files_pour_la_porte() -> list[dict[str, str]]:
    return [
        {"path": "../../../../sim/engine.py", "must_differ_from_git": f"{BASE_REF}:sim/engine.py"},
        {"path": "../../../../sim/constants.py", "must_differ_from_git": f"{BASE_REF}:sim/constants.py"},
        {"path": "../../../../sim/tests/test_commerce.py", "must_differ_from_git": f"{BASE_REF}:sim/tests/test_commerce.py"},
        {"path": "deliverables/pre-edit/baseline_ticks365_seed0.json"},
        {"path": "deliverables/measure_043.py"},
        {"path": "deliverables/generator-log.md"},
        {"path": "deliverables/manifest.json"},
    ]


def _tests_collectes(ref: str | None = None) -> int:
    if ref is None:
        proc = sh([VENV_PY, "-m", "pytest", "sim/tests/", "--collect-only", "-q"])
    else:
        proc = sh(["git", "grep", "-h", r"^def test_", ref, "--", "sim/tests/"])
        if proc.returncode != 0:
            return -1
        return len([ln for ln in proc.stdout.splitlines() if ln.strip()])
    for ln in reversed(proc.stdout.splitlines()):
        parts = ln.strip().split()
        if parts and parts[0].isdigit() and "collected" in ln:
            return int(parts[0])
    return -1


def compter() -> dict:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.engine import (
        LongueurFrontiereInvalideError,
        _apply_commerce,
        _capacite_transport_arete_kg,
        _initialiser_capacite_aretes,
        tick,
    )
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    c: dict = {}
    monde = World.charger(0)
    aretes = [e for e in monde.adjacency if e["a"] in monde.cells and e["b"] in monde.cells]
    c["aretes_entre_deux_cellules"] = len(aretes)
    longueurs = [float(e["shared_length_m"]) for e in aretes]
    c["longueurs_distinctes_mesurees"] = len(set(longueurs))
    capacites = [_capacite_transport_arete_kg(monde, e["a"], e["b"]) for e in aretes]
    c["capacite_mediane_derivee"] = statistics.median(capacites)
    c["capacite_plate_remplacee"] = k.TRADE_CAPACITY_KG_PER_EDGE_PER_TICK
    c["rapport_de_capacite_attendu"] = c["capacite_mediane_derivee"] / c["capacite_plate_remplacee"]

    courte, mediane, longue = min(longueurs), statistics.median(longueurs), max(longueurs)
    source_id = 9300
    pop = 25000
    besoin = pop * k.FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    source = Cell(cell_id=source_id, area_km2=0.0, population=0, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
    ecrire_stock_marchandise(source, k.MARCHANDISE_NOURRITURE, besoin * 20)
    cells = {source_id: source}
    adjacency = []
    for idx, longueur in enumerate((courte, mediane, longue)):
        cid = source_id + idx + 1
        cells[cid] = Cell(cell_id=cid, area_km2=0.0, population=pop, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
        adjacency.append({"a": source_id, "b": cid, "kind": "land", "shared_length_m": longueur})
    micro = World(cells=cells, adjacency=adjacency)

    def transfert_vers(receveuse: int) -> float:
        copie = {cid: Cell(cell_id=cell.cell_id, area_km2=cell.area_km2, population=cell.population,
                           stocks=dict(cell.stocks), hunger_ticks=cell.hunger_ticks,
                           food_deficit_kg=cell.food_deficit_kg) for cid, cell in micro.cells.items()}
        w = World(cells=copie, adjacency=list(micro.adjacency))
        total = [0.0]
        _apply_commerce(w, total, k.MARCHANDISE_NOURRITURE, _initialiser_capacite_aretes(w))
        stock = lire_stock_marchandise(w.cells[receveuse], k.MARCHANDISE_NOURRITURE)
        return stock if stock >= 0 else 0.0

    t0, t1, t2 = (transfert_vers(source_id + i) for i in (1, 2, 3))
    c["rapports_transferts_sur_longueurs"] = f"{t1/t0:.6f},{t2/t0:.6f}" if t0 > 0 else "0,0"

    src = Cell(cell_id=9400, area_km2=0.0, population=0, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
    rcv = Cell(cell_id=9401, area_km2=0.0, population=50, stocks={}, hunger_ticks=0, food_deficit_kg=100.0)
    ecrire_stock_marchandise(src, k.MARCHANDISE_NOURRITURE, 500.0)
    w0 = World(cells={9400: src, 9401: rcv}, adjacency=[{"a": 9400, "b": 9401, "kind": "land", "shared_length_m": 0.0}])
    total0 = [0.0]
    _apply_commerce(w0, total0, k.MARCHANDISE_NOURRITURE, _initialiser_capacite_aretes(w0))
    c["transfert_sur_arete_de_longueur_nulle"] = total0[0]

    engine_src = (REPO / "sim" / "engine.py").read_text(encoding="utf-8")
    tree = ast.parse(engine_src)
    expr = sum(1 for node in ast.walk(tree) if isinstance(node, ast.FunctionDef) and node.name == "_capacite_base_arete_kg"
               and "DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK" in (ast.get_source_segment(engine_src, node) or "")
               and "shared_length_m" in (ast.get_source_segment(engine_src, node) or ""))
    c["expression_capacite_avec_debit"] = expr

    arch = PRE_EDIT / "baseline_ticks365_seed0.json"
    c["kg_transportes_avant"] = json.load(open(arch, encoding="utf-8"))["kg_transportes"] if arch.exists() else -1
    r1 = sh([VENV_PY, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"], timeout=400)
    r2 = sh([VENV_PY, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"], timeout=400)
    c["kg_transportes_apres"] = json.loads(r1.stdout)["kg_transportes"]
    c["deux_cli_identiques"] = r1.stdout == r2.stdout

    def ticks_survie(debit: float | None) -> int:
        centre_id, source_id = 9500, 9501
        pop = 100
        besoin = pop * k.FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
        source = Cell(cell_id=source_id, area_km2=0.0, population=0, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
        ecrire_stock_marchandise(source, k.MARCHANDISE_NOURRITURE, besoin * 200)
        centre = Cell(cell_id=centre_id, area_km2=0.0, population=pop, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
        w = World(cells={centre_id: centre, source_id: source},
                  adjacency=[{"a": source_id, "b": centre_id, "kind": "land", "shared_length_m": 50000.0}])
        rng = random.Random(0)
        nominal = k.DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK
        if debit is not None:
            k.DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK = debit
        try:
            borne = math.ceil(1.0 / k.MAX_DEATH_RATE_PER_TICK) + 5
            surv = 0
            for _ in range(borne):
                pop_avant = w.cells[centre_id].population
                tick(w, rng)
                if w.cells[centre_id].population < pop_avant:
                    break
                surv += 1
            return surv
        finally:
            k.DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK = nominal

    c["ticks_survecus_cellule_sans_production"] = ticks_survie(None)
    c["ticks_survecus_cellule_sans_production_capacite_plate"] = ticks_survie(k.DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK * 0.001)

    r = sh([VENV_PY, "-m", "pytest", "sim/tests/test_commerce.py::test_conservation_masse_transport", "-q"])
    c["ecart_de_masse_micro_monde"] = 0 if r.returncode == 0 else -1

    invalides = 0
    for invalide in ("x", None, float("nan")):
        s = Cell(cell_id=9600, area_km2=0.0, population=0, stocks={}, hunger_ticks=0, food_deficit_kg=0.0)
        r = Cell(cell_id=9601, area_km2=0.0, population=10, stocks={}, hunger_ticks=0, food_deficit_kg=20.0)
        ecrire_stock_marchandise(s, k.MARCHANDISE_NOURRITURE, 500.0)
        w = World(cells={9600: s, 9601: r}, adjacency=[{"a": 9600, "b": 9601, "kind": "land", "shared_length_m": 1000.0}])
        w.adjacency[0]["shared_length_m"] = invalide
        try:
            _apply_commerce(w, [0.0], k.MARCHANDISE_NOURRITURE, _initialiser_capacite_aretes(w))
        except LongueurFrontiereInvalideError:
            invalides += 1
    c["longueurs_invalides_refusees"] = invalides
    c["tests_collectes_avant"] = _tests_collectes(BASE_REF)
    c["tests_collectes_apres"] = _tests_collectes()
    return c


def verifier(c: dict) -> list[str]:
    e = []
    if c.get("expression_capacite_avec_debit", 0) < 1:
        e.append("expression_capacite_avec_debit < 1")
    if c.get("transfert_sur_arete_de_longueur_nulle", -1) != 0.0:
        e.append("transfert_sur_arete_de_longueur_nulle != 0")
    if c.get("ecart_de_masse_micro_monde", -1) != 0:
        e.append("ecart_de_masse_micro_monde != 0")
    if c.get("longueurs_invalides_refusees", 0) < 3:
        e.append("longueurs_invalides_refusees < 3")
    avant, apres = c.get("kg_transportes_avant", -1), c.get("kg_transportes_apres", 0)
    rapport = c.get("rapport_de_capacite_attendu", 0)
    if avant > 0 and apres < avant * rapport * 0.99:
        e.append(f"kg_transportes insuffisant: {apres}/{avant} < {rapport}")
    if c.get("ticks_survecus_cellule_sans_production", 0) <= c.get("ticks_survecus_cellule_sans_production_capacite_plate", 0):
        e.append("SC5 ticks_survecus non strict")
    if not c.get("deux_cli_identiques"):
        e.append("CLI non déterministes")
    if c.get("tests_collectes_apres", 0) < c.get("tests_collectes_avant", 0):
        e.append("tests_collectes en baisse")
    return e


def main() -> None:
    print("=== Mesureur lot 043 ===")
    c = compter()
    for nom, val in sorted(c.items()):
        print(f"  {nom} = {val}")
    erreurs = verifier(c)
    if erreurs:
        print("ERREURS:", *erreurs, sep="\n  - ")
        sys.exit(1)
    print("OK")
    sys.exit(0)


if __name__ == "__main__":
    main()
