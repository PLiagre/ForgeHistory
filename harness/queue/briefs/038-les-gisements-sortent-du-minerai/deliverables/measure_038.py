#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 038 (extraction minière)."""

from __future__ import annotations

import argparse
import ast
import copy
import json
import random
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "038-les-gisements-sortent-du-minerai"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "782ae0b87d0282e25d6ea6acd19226d41aaedb53"
NOT_COMPUTED = -1
ROWS: list[tuple[str, object, str]] = []
NOMS_EXTRACTION_INTERDITS = {
    "EXTRACTION_KG_PAR_HABITANT_PAR_TICK",
    "FACTEUR_RICHESSE_MAJEURE",
    "FACTEUR_RICHESSE_NOTABLE",
    "FACTEUR_RICHESSE_MINEURE",
}


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
            "path": "../../../../sim/tests/test_monde.py",
            "must_differ_from_git": f"{BASE_REF}:sim/tests/test_monde.py",
        },
        {"path": "deliverables/pre-edit/cellules_extractrices_base.json"},
        {"path": "deliverables/pre-edit/cli_ticks365_seed0.json"},
        {
            "path": "deliverables/cli_ticks365_seed0_apres_run1.json",
            "must_differ_from": "deliverables/pre-edit/cli_ticks365_seed0.json",
        },
        {
            "path": "deliverables/cli_ticks365_seed0_apres_run2.json",
            "must_differ_from": "deliverables/pre-edit/cli_ticks365_seed0.json",
        },
        {"path": "deliverables/measure_038.py"},
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


def agreger_gisements(carte_doc: dict) -> tuple[int, int, set[str], set[str]]:
    cellules = 0
    gisements = 0
    ressources: set[str] = set()
    classes: set[str] = set()
    for raw in carte_doc["cellules"]:
        liste = raw.get("gisements") or []
        complets = [
            g for g in liste
            if isinstance(g, dict) and g.get("ressource") is not None and g.get("richesse") is not None
        ]
        if complets:
            cellules += 1
            gisements += len(complets)
            for g in complets:
                ressources.add(g["ressource"])
                classes.add(g["richesse"])
    return cellules, gisements, ressources, classes


def mesurer_extraction_apres_tick() -> tuple[int, set[str]]:
    sys.path.insert(0, str(REPO))
    from sim.constants import MARCHANDISE_NOURRITURE
    from sim.engine import tick
    from sim.world import World

    world = World.charger(0)
    tick(world, random.Random(0), numero_tick=0)
    cellules = 0
    ressources: set[str] = set()
    for cell in world.cells.values():
        mineraux = {k for k in cell.stocks if k != MARCHANDISE_NOURRITURE}
        if mineraux:
            cellules += 1
            ressources |= mineraux
    return cellules, ressources


def mesurer_richesses_ordonnees(classes_carte: set[str]) -> int:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.engine import _extraction_du_tick_kg
    from sim.world import World

    attendues = set(k.facteurs_richesse_extraction())
    if classes_carte != attendues:
        return NOT_COMPUTED
    carte = World.lire_carte()
    population = 1000
    par_classe: dict[str, float] = {}
    for raw in carte["cellules"]:
        for g in raw.get("gisements") or []:
            if not isinstance(g, dict):
                continue
            richesse = g.get("richesse")
            if richesse in attendues and richesse not in par_classe:
                monde = World.charger(0)
                cell = monde.cells[int(raw["cell_id"])]
                cell.population = population
                par_classe[richesse] = _extraction_du_tick_kg(cell, monde.carte).get(
                    g["ressource"], 0.0
                )
    if set(par_classe) != attendues:
        return NOT_COMPUTED
    majeure = par_classe["majeure"]
    notable = par_classe["notable"]
    mineure = par_classe["mineure"]
    return int(majeure > notable > mineure)


def mesurer_extraction_population_nulle() -> float:
    sys.path.insert(0, str(REPO))
    from sim.engine import _extraction_du_tick_kg, tick
    from sim.world import World

    carte = World.lire_carte()
    cid = next(
        int(raw["cell_id"])
        for raw in carte["cellules"]
        if any(
            isinstance(g, dict) and g.get("ressource") and g.get("richesse")
            for g in (raw.get("gisements") or [])
        )
    )
    world = World.charger(0)
    cell = world.cells[cid]
    cell.population = 0
    ressource = next(
        g["ressource"]
        for g in world.carte[cid]["gisements"]
        if isinstance(g, dict) and g.get("ressource") and g.get("richesse")
    )
    tick(world, random.Random(0))
    return _extraction_du_tick_kg(cell, world.carte).get(ressource, NOT_COMPUTED)


def mesurer_nourriture_changee() -> int:
    script = REPO / ".measure038_nourriture.py"
    script.write_text(
        """import copy, random, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[0]))
from sim.constants import MARCHANDISE_NOURRITURE
from sim.engine import tick
from sim.world import World

carte_avec = World.lire_carte()
carte_sans = copy.deepcopy(carte_avec)
for raw in carte_sans['cellules']:
    raw['gisements'] = []
seed = 0
avec = World.charger(seed, carte_doc=copy.deepcopy(carte_avec))
sans = World.charger(seed, carte_doc=carte_sans)
tick(avec, random.Random(seed), numero_tick=0)
tick(sans, random.Random(seed), numero_tick=0)
changes = sum(
    1 for cid in avec.cells
    if avec.cells[cid].stocks.get(MARCHANDISE_NOURRITURE, -1.0)
    != sans.cells[cid].stocks.get(MARCHANDISE_NOURRITURE, -1.0)
)
print(changes, len(avec.cells))
""",
        encoding="utf-8",
    )
    try:
        proc = subprocess.run(
            [str(REPO / ".venv/bin/python"), str(script)],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        parts = proc.stdout.strip().split()
        return int(parts[0])
    finally:
        script.unlink(missing_ok=True)


def mesurer_cellules_extractrices_base(engine_base: str) -> int:
    script = REPO / ".measure038_sc1_base.py"
    script.write_text(
        """import random, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[0]))
from sim.constants import MARCHANDISE_NOURRITURE
from sim.engine import tick
from sim.world import World

w = World.charger(0)
tick(w, random.Random(0), 0)
print(sum(1 for c in w.cells.values() if any(k != MARCHANDISE_NOURRITURE for k in c.stocks)))
""",
        encoding="utf-8",
    )
    engine_path = REPO / "sim" / "engine.py"
    backup = engine_path.read_text(encoding="utf-8")
    try:
        engine_path.write_text(engine_base, encoding="utf-8")
        proc = subprocess.run(
            [str(REPO / ".venv/bin/python"), str(script)],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return int(proc.stdout.strip())
    finally:
        engine_path.write_text(backup, encoding="utf-8")
        script.unlink(missing_ok=True)


def mesurer_refus_et_acceptations() -> tuple[int, int, int]:
    sys.path.insert(0, str(REPO))
    from sim.engine import RichesseGisementInvalideError, tick
    from sim.world import World

    refusees = 0
    world = World.charger(0)
    cid = next(
        int(raw["cell_id"])
        for raw in world.carte.values()
        if isinstance(raw, dict) and raw.get("gisements")
    )
    entree = dict(world.carte[cid])
    gisements = [dict(g) for g in entree["gisements"]]
    gisements[0]["richesse"] = "inconnue"
    entree["gisements"] = gisements
    monde_refus = World.charger(0)
    monde_refus.carte[cid] = entree
    try:
        tick(monde_refus, random.Random(0), numero_tick=0)
    except RichesseGisementInvalideError:
        refusees = 1

    incomplets = 0
    carte = World.lire_carte()
    cid2 = next(
        int(raw["cell_id"])
        for raw in carte["cellules"]
        if len([
            g for g in (raw.get("gisements") or [])
            if isinstance(g, dict) and g.get("ressource") and g.get("richesse")
        ]) >= 2
    )
    entree2 = dict(World.charger(0).carte[cid2])
    gisements2 = [dict(g) for g in entree2["gisements"]]
    complet = next(g for g in gisements2 if g.get("ressource") and g.get("richesse"))
    ressource_ok = complet["ressource"]
    incomplet = dict(complet)
    incomplet.pop("ressource", None)
    entree2["gisements"] = [incomplet, complet]
    monde_inc = World.charger(0)
    monde_inc.carte[cid2] = entree2
    tick(monde_inc, random.Random(0), numero_tick=0)
    if ressource_ok in monde_inc.cells[cid2].stocks:
        incomplets = 1

    acceptees = 0
    monde_res = World.charger(0)
    cid3 = next(iter(monde_res.cells))
    entree3 = dict(monde_res.carte[cid3])
    entree3["gisements"] = [{
        "id": "sonde-ressource",
        "ressource": "mythrite",
        "richesse": "notable",
    }]
    monde_res.carte[cid3] = entree3
    monde_res.cells[cid3].population = 100
    tick(monde_res, random.Random(0), numero_tick=0)
    if "mythrite" in monde_res.cells[cid3].stocks:
        acceptees = 1

    return refusees, incomplets, acceptees


def mesurer_couches() -> tuple[int, int, bool, bool, bool]:
    sys.path.insert(0, str(REPO))
    from sim.snapshot_export import build_snapshot_document
    from sim.world import World

    doc = build_snapshot_document(World.charger(0), 0, 0)
    couches = doc["couches"]
    relief = bool(couches["relief"]["utilisee_par_le_moteur"])
    climat = bool(couches["climat"]["utilisee_par_le_moteur"])
    gisements = bool(couches["gisements"]["utilisee_par_le_moteur"])
    return int(relief) + int(climat) + int(gisements), len(couches), relief, climat, gisements


def mesurer_noms_extraction_engine() -> int:
    tree = ast.parse((REPO / "sim" / "engine.py").read_text(encoding="utf-8"))
    attrs = {
        node.attr
        for node in ast.walk(tree)
        if isinstance(node, ast.Attribute) and not isinstance(node.ctx, ast.Store)
    }
    return len(attrs & NOMS_EXTRACTION_INTERDITS)


def mesurer_cli() -> int:
    cmd = [str(REPO / ".venv/bin/python"), "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
    pre = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    if not pre.is_file():
        pre.parent.mkdir(parents=True, exist_ok=True)
        base_engine = git("show", f"{BASE_REF}:sim/engine.py")
        pre.write_text(run_cli(cmd, base_engine), encoding="utf-8")
    runs = [json.loads(run_cli(cmd)) for _ in range(2)]
    (DELIVERABLES / "cli_ticks365_seed0_apres_run1.json").write_text(
        json.dumps(runs[0], ensure_ascii=False) + "\n", encoding="utf-8"
    )
    (DELIVERABLES / "cli_ticks365_seed0_apres_run2.json").write_text(
        json.dumps(runs[1], ensure_ascii=False) + "\n", encoding="utf-8"
    )
    return int(runs[0] == runs[1])


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

    engine_base = git("show", f"{BASE_REF}:sim/engine.py")
    carte_doc = json.loads((REPO / "data" / "world-1400.json").read_text(encoding="utf-8"))
    cellules_carte, gisements_declares, ressources_carte, classes_carte = agreger_gisements(carte_doc)
    n_cellules_total = len(carte_doc["cellules"])

    report("cellules_avec_gisement_carte", cellules_carte, f"nombre total de cellules réellement mesurées = {n_cellules_total}")
    report("gisements_declares", gisements_declares, f"cellules_avec_gisement_carte = {cellules_carte}")
    report("ressources_distinctes_carte", len(ressources_carte), f"gisements_declares = {gisements_declares}")
    report("classes_de_richesse_carte", len(classes_carte), f"gisements_declares = {gisements_declares}")

    extractrices, ressources_extraites = mesurer_extraction_apres_tick()
    report("cellules_extractrices_apres_un_tick", extractrices, f"cellules_avec_gisement_carte = {cellules_carte}")
    report("ressources_distinctes_extraites", len(ressources_extraites), f"ressources_distinctes_carte = {len(ressources_carte)}")

    ordonnees = mesurer_richesses_ordonnees(classes_carte)
    report("richesses_ordonnees", ordonnees, f"classes_de_richesse_carte = {len(classes_carte)}")

    extraction_nulle = mesurer_extraction_population_nulle()
    report("extraction_population_nulle", extraction_nulle, "nombre de ticks réellement joués = 1")

    nourriture_changee = mesurer_nourriture_changee()
    report("cellules_dont_la_nourriture_a_change", nourriture_changee, f"nombre de cellules réellement chargées = {n_cellules_total}")

    refusees, incomplets, acceptees = mesurer_refus_et_acceptations()
    report("richesses_inconnues_refusees", refusees, "nombre de mutations réellement exécutées = 1")
    report("gisements_incomplets_ignores", incomplets, "nombre de mutations réellement exécutées = 1")
    report("ressources_inconnues_acceptees", acceptees, "nombre de mutations réellement exécutées = 1")

    noms_engine = mesurer_noms_extraction_engine()
    report(
        "noms_de_constantes_extraction_dans_engine",
        noms_engine,
        f"nombre de noms du motif 033 réellement cherchés = {len(NOMS_EXTRACTION_INTERDITS)}",
    )

    consommees, n_couches, relief, climat, gisements = mesurer_couches()
    report("couches_consommees_par_tick", consommees, f"nombre de couches déclarées dans le snapshot = {n_couches}")

    cli_identiques = mesurer_cli()
    report("sorties_cli_deterministes", cli_identiques, "exécutions lancées = 2")

    verts, collectes = mesurer_tests()
    report("tests_sim_verts", verts, f"nombre de tests collectés = {collectes}")

    pre_path = DELIVERABLES / "pre-edit" / "cellules_extractrices_base.json"
    if not pre_path.is_file():
        pre_path.parent.mkdir(parents=True, exist_ok=True)
        base_count = mesurer_cellules_extractrices_base(engine_base)
        pre_path.write_text(json.dumps(base_count) + "\n", encoding="utf-8")
    cellules_base = json.loads(pre_path.read_text(encoding="utf-8"))

    erreurs: list[str] = []
    if cellules_carte == 0:
        erreurs.append("cellules_avec_gisement_carte=0")
    if cellules_base != 0:
        erreurs.append(f"rouge SC1 non prouvé : cellules_extractrices_base={cellules_base}")
    if extractrices != cellules_carte:
        erreurs.append("cellules_extractrices_apres_un_tick != cellules_avec_gisement_carte")
    if ressources_extraites != ressources_carte:
        erreurs.append("ressources_distinctes_extraites != ressources_distinctes_carte")
    if ordonnees != 1:
        erreurs.append(f"richesses_ordonnees={ordonnees}")
    if extraction_nulle != 0.0:
        erreurs.append(f"extraction_population_nulle={extraction_nulle}")
    if nourriture_changee != 0:
        erreurs.append(f"cellules_dont_la_nourriture_a_change={nourriture_changee}")
    if refusees != 1:
        erreurs.append(f"richesses_inconnues_refusees={refusees}")
    if incomplets != 1:
        erreurs.append(f"gisements_incomplets_ignores={incomplets}")
    if acceptees != 1:
        erreurs.append(f"ressources_inconnues_acceptees={acceptees}")
    if noms_engine != 0:
        erreurs.append(f"noms_de_constantes_extraction_dans_engine={noms_engine}")
    if not gisements:
        erreurs.append("couche gisements non consommée")
    if not relief:
        erreurs.append("couche relief non consommée")
    if not climat:
        erreurs.append("couche climat non consommée")
    if cli_identiques != 1:
        erreurs.append("sorties CLI non déterministes")
    if verts != collectes:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.is_file() else {
            "brief": "038-les-gisements-sortent-du-minerai",
            "base_ref": BASE_REF,
            "commands": [
                ".venv/bin/python -m pytest sim/tests/ -q",
                ".venv/bin/python -m sim --ticks 365 --seed 0 --json",
                ".venv/bin/python harness/queue/briefs/038-les-gisements-sortent-du-minerai/deliverables/measure_038.py",
            ],
        }
        sample_map = {
            "cellules_avec_gisement_carte": n_cellules_total,
            "gisements_declares": cellules_carte,
            "ressources_distinctes_carte": gisements_declares,
            "classes_de_richesse_carte": gisements_declares,
            "cellules_extractrices_apres_un_tick": cellules_carte,
            "ressources_distinctes_extraites": len(ressources_carte),
            "richesses_ordonnees": len(classes_carte),
            "extraction_population_nulle": 1,
            "cellules_dont_la_nourriture_a_change": n_cellules_total,
            "richesses_inconnues_refusees": 1,
            "gisements_incomplets_ignores": 1,
            "ressources_inconnues_acceptees": 1,
            "noms_de_constantes_extraction_dans_engine": len(NOMS_EXTRACTION_INTERDITS),
            "couches_consommees_par_tick": n_couches,
            "sorties_cli_deterministes": 2,
            "tests_sim_verts": collectes,
        }
        manifest["counters"] = [
            {
                "name": name,
                "value": value,
                "sample_size": sample_map.get(name, 1),
                "denominator": denom,
            }
            for name, value, denom in ROWS
        ]
        manifest["files"] = files_pour_la_porte()
        manifest["base_ref"] = BASE_REF
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
