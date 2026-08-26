#!/usr/bin/env python3
"""Mesure rejouable des compteurs du brief 035 (saison dans le rendement)."""

from __future__ import annotations

import argparse
import ast
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "035-la-saison-joue-le-rendement"
DELIVERABLES = BRIEF / "deliverables"
BASE_REF = "9df4917b8e3a4c804c9263eac5973912a8a77092"
NOT_COMPUTED = -1
ROWS: list[tuple[str, object, str]] = []
CHAMPS_CLI_DERIVES = (
    "population_arrivee",
    "cellules_affamees",
    "kg_transportes",
    "stock_kg_arrivee",
)
NOMS_SAISON_INTERDITS = {
    "SENSIBILITE_SAISON",
    "JOUR_SOLSTICE_ETE",
    "DUREE_JOUR_EQUINOXE_H",
}


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def git(*args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr or proc.stdout or f"git {' '.join(args)}")
    return proc.stdout


def run_cli(cmd: list[str], engine_src: str | None = None) -> str:
    engine_path = REPO / "sim" / "engine.py"
    main_path = REPO / "sim" / "__main__.py"
    backup_engine = engine_path.read_text(encoding="utf-8") if engine_src is not None else None
    backup_main = main_path.read_text(encoding="utf-8") if engine_src is not None else None
    try:
        if engine_src is not None:
            engine_path.write_text(engine_src, encoding="utf-8")
            main_path.write_text(git("show", f"{BASE_REF}:sim/__main__.py"), encoding="utf-8")
        proc = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        return proc.stdout
    finally:
        if backup_engine is not None:
            engine_path.write_text(backup_engine, encoding="utf-8")
        if backup_main is not None:
            main_path.write_text(backup_main, encoding="utf-8")


def cellules_par_amplitude(carte_doc: dict) -> tuple[int, int]:
    amplitudes: list[tuple[float, int]] = []
    for raw in carte_doc["cellules"]:
        climat = raw.get("climat")
        if not isinstance(climat, dict):
            continue
        ete = climat.get("duree_jour_solstice_ete_h")
        hiver = climat.get("duree_jour_solstice_hiver_h")
        if isinstance(ete, (int, float)) and isinstance(hiver, (int, float)):
            if not isinstance(ete, bool) and not isinstance(hiver, bool):
                amplitudes.append((abs(float(ete) - float(hiver)), int(raw["cell_id"])))
    if not amplitudes:
        raise RuntimeError("échantillon vide : aucune cellule avec deux solstices")
    amplitudes.sort()
    return amplitudes[-1][1], amplitudes[0][1]


def production_ete_hiver_overlay(engine_src: str, cid: int) -> tuple[float, float]:
    engine_path = REPO / "sim" / "engine.py"
    backup = engine_path.read_text(encoding="utf-8")
    avec_saison = "jour: int | None" in engine_src
    if avec_saison:
        corps = """ete = production_du_tick_kg(cell, rendement, world.carte, jour=k.jour_solstice_ete())
hiver = production_du_tick_kg(cell, rendement, world.carte, jour=k.jour_solstice_hiver())"""
    else:
        corps = """ete = production_du_tick_kg(cell, rendement, world.carte)
hiver = production_du_tick_kg(cell, rendement, world.carte)"""
    script = REPO / ".measure035_tmp.py"
    script.write_text(
        f"""from sim import constants as k
from sim.engine import production_du_tick_kg
from sim.world import World

world = World.charger(0)
cell = world.cells[{cid}]
cell.area_km2 = 10.0
rendement = 1.0
{corps}
print(ete, hiver)
""",
        encoding="utf-8",
    )
    try:
        engine_path.write_text(engine_src, encoding="utf-8")
        proc = subprocess.run(
            [str(REPO / ".venv/bin/python"), str(script)],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr or proc.stdout)
        parts = proc.stdout.strip().split()
        return float(parts[0]), float(parts[1])
    finally:
        engine_path.write_text(backup, encoding="utf-8")
        script.unlink(missing_ok=True)


def mesurer_ecart_ete_hiver(engine_src: str, cid: int) -> float:
    ete, hiver = production_ete_hiver_overlay(engine_src, cid)
    return abs(ete - hiver)


def mesurer_carte() -> dict[str, int]:
    sys.path.insert(0, str(REPO))
    from sim.world import World

    carte = World.lire_carte()
    cellules = 0
    amplitudes: set[float] = set()
    for raw in carte["cellules"]:
        climat = raw.get("climat")
        if not isinstance(climat, dict):
            continue
        ete = climat.get("duree_jour_solstice_ete_h")
        hiver = climat.get("duree_jour_solstice_hiver_h")
        if isinstance(ete, (int, float)) and isinstance(hiver, (int, float)):
            if not isinstance(ete, bool) and not isinstance(hiver, bool):
                cellules += 1
                amplitudes.add(abs(float(ete) - float(hiver)))
    return {
        "cellules_avec_deux_solstices": cellules,
        "amplitudes_distinctes_mesurees": len(amplitudes),
    }


def mesurer_jours_evalues() -> int:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.world import World

    world = World.charger(0)
    cid = next(iter(world.cells))
    climat = world.carte[cid]["climat"]
    ete = climat["duree_jour_solstice_ete_h"]
    hiver = climat["duree_jour_solstice_hiver_h"]
    annee = k.CALENDAR_DAYS_PER_YEAR
    comptes = 0
    for jour in range(annee):
        k.facteur_saison(k.duree_jour_h(jour, ete, hiver))
        comptes += 1
    return comptes


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


def mesurer_gisements_base(engine_base: str) -> bool:
    engine_path = REPO / "sim" / "engine.py"
    backup = engine_path.read_text(encoding="utf-8")
    script = REPO / ".measure035_gisements.py"
    script.write_text(
        """from sim.snapshot_export import build_snapshot_document
from sim.world import World
doc = build_snapshot_document(World.charger(0), 0, 0)
print(doc['couches']['gisements']['utilisee_par_le_moteur'])
""",
        encoding="utf-8",
    )
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
        return proc.stdout.strip() == "True"
    finally:
        engine_path.write_text(backup, encoding="utf-8")
        script.unlink(missing_ok=True)


def mesurer_climat_refuse() -> int:
    import random

    sys.path.insert(0, str(REPO))
    from sim.engine import ClimatInvalideError, tick
    from sim.world import World

    mutations = [
        lambda entree: (entree.pop("climat", None), entree)[1],
        lambda entree: {
            **entree,
            "climat": {**entree.get("climat", {}), "duree_jour_solstice_ete_h": "invalide"},
        },
        lambda entree: {
            **entree,
            "climat": {**entree.get("climat", {}), "duree_jour_solstice_hiver_h": None},
        },
    ]
    refusees = 0
    for mut in mutations:
        world = World.charger(0)
        cid = next(iter(world.cells))
        world.carte[cid] = mut(dict(world.carte[cid]))
        try:
            tick(world, random.Random(0), numero_tick=0)
        except ClimatInvalideError as exc:
            if f"cell_id={cid}" in str(exc):
                refusees += 1
    return refusees


def mesurer_somme_annuelle() -> float:
    sys.path.insert(0, str(REPO))
    from sim import constants as k
    from sim.engine import _production_du_tick_kg_saison_moyenne, production_du_tick_kg
    from sim.world import World

    world = World.charger(0)
    cid = next(iter(world.cells))
    cell = world.cells[cid]
    cell.area_km2 = 10.0
    rendement = 1.0
    annee = k.CALENDAR_DAYS_PER_YEAR
    somme_saisonniere = sum(
        production_du_tick_kg(cell, rendement, world.carte, jour=k.jour_de_tick(t))
        for t in range(annee)
    )
    somme_moyenne = _production_du_tick_kg_saison_moyenne(cell, rendement, world.carte) * annee
    return abs(somme_saisonniere - somme_moyenne)


def mesurer_cli() -> tuple[int, int, int]:
    cmd = [".venv/bin/python", "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]
    pre = DELIVERABLES / "pre-edit" / "cli_ticks365_seed0.json"
    if not pre.is_file():
        pre.parent.mkdir(parents=True, exist_ok=True)
        base_engine = git("show", f"{BASE_REF}:sim/engine.py")
        pre.write_text(run_cli(cmd, base_engine), encoding="utf-8")

    base = json.loads(pre.read_text(encoding="utf-8"))
    runs = [json.loads(run_cli(cmd)) for _ in range(2)]
    (DELIVERABLES / "cli_ticks365_seed0_apres_run1.json").write_text(
        json.dumps(runs[0], ensure_ascii=False) + "\n", encoding="utf-8"
    )
    (DELIVERABLES / "cli_ticks365_seed0_apres_run2.json").write_text(
        json.dumps(runs[1], ensure_ascii=False) + "\n", encoding="utf-8"
    )
    identiques = int(runs[0] == runs[1])
    modifies = sum(1 for champ in CHAMPS_CLI_DERIVES if base.get(champ) != runs[0].get(champ))
    return identiques, modifies, len(CHAMPS_CLI_DERIVES)


def mesurer_noms_saison_engine() -> int:
    tree = ast.parse((REPO / "sim" / "engine.py").read_text(encoding="utf-8"))
    attrs = {
        node.attr
        for node in ast.walk(tree)
        if isinstance(node, ast.Attribute) and not isinstance(node.ctx, ast.Store)
    }
    return len(attrs & NOMS_SAISON_INTERDITS)


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
    carte_doc = json.loads((REPO / "data" / "world-1400.json").read_text(encoding="utf-8"))
    cid_max, _ = cellules_par_amplitude(carte_doc)

    carte_stats = mesurer_carte()
    report(
        "cellules_avec_deux_solstices",
        carte_stats["cellules_avec_deux_solstices"],
        f"cellules mesurées dans la carte figée",
    )
    report(
        "amplitudes_distinctes_mesurees",
        carte_stats["amplitudes_distinctes_mesurees"],
        f"cellules_avec_deux_solstices = {carte_stats['cellules_avec_deux_solstices']}",
    )

    jours = mesurer_jours_evalues()
    sys.path.insert(0, str(REPO))
    from sim import constants as k

    report(
        "jours_de_l_annee_evalues",
        jours,
        f"nombre de jours dérivé des constantes = {k.CALENDAR_DAYS_PER_YEAR}",
    )

    ecart_avant = mesurer_ecart_ete_hiver(engine_base, cid_max)
    ecart_apres = mesurer_ecart_ete_hiver(engine_actuel, cid_max)
    report("ecart_ete_hiver_avant", ecart_avant, "couples été/hiver comparés = 1")
    report("ecart_ete_hiver_apres", ecart_apres, "couples été/hiver comparés = 1")

    consommees, n_couches, relief, climat, gisements = mesurer_couches()
    pre_gisements_path = DELIVERABLES / "pre-edit" / "gisements_utilisee.json"
    if not pre_gisements_path.is_file():
        pre_gisements_path.parent.mkdir(parents=True, exist_ok=True)
        gisements_base = mesurer_gisements_base(engine_base)
        pre_gisements_path.write_text(json.dumps(gisements_base) + "\n", encoding="utf-8")
    gisements_base = json.loads(pre_gisements_path.read_text(encoding="utf-8"))
    report(
        "couches_consommees_par_tick",
        consommees,
        f"couches déclarées dans le snapshot = {n_couches}",
    )

    refusees = mesurer_climat_refuse()
    report("cellules_climat_incomplet_refusees", refusees, "mutations exécutées = 3")

    ecart_somme = mesurer_somme_annuelle()
    report("ecart_relatif_somme_annuelle", ecart_somme, "cellules sommées = 1")

    cli_identiques, champs_modifies, n_champs = mesurer_cli()
    report("sorties_cli_deterministes", cli_identiques, "exécutions lancées = 2")
    report("champs_cli_modifies", champs_modifies, f"champs dérivés comparés = {n_champs}")

    noms_saison = mesurer_noms_saison_engine()
    report(
        "noms_de_constantes_saison_dans_engine",
        noms_saison,
        f"noms du motif 033 cherchés = {len(NOMS_SAISON_INTERDITS)}",
    )

    verts, collectes = mesurer_tests()
    report("tests_sim_verts", verts, f"tests collectés = {collectes}")

    erreurs: list[str] = []
    if carte_stats["cellules_avec_deux_solstices"] == 0:
        erreurs.append("cellules_avec_deux_solstices=0")
    if ecart_avant != 0:
        erreurs.append(f"ecart_ete_hiver_avant={ecart_avant} (rouge non prouvé)")
    if ecart_apres == 0:
        erreurs.append(f"ecart_ete_hiver_apres={ecart_apres}")
    if not relief:
        erreurs.append("couche relief non consommée")
    if not climat:
        erreurs.append("couche climat non consommée")
    if gisements != gisements_base:
        erreurs.append("gisements a changé par rapport au SHA de base")
    if refusees != 3:
        erreurs.append(f"cellules_climat_incomplet_refusees={refusees}")
    if cli_identiques != 1:
        erreurs.append("sorties CLI non déterministes")
    if champs_modifies == 0:
        erreurs.append("aucun champ CLI modifié par rapport au SHA de base")
    if noms_saison != 0:
        erreurs.append(f"noms_de_constantes_saison_dans_engine={noms_saison}")
    if verts != collectes:
        erreurs.append("suite sim/tests non entièrement verte")

    for name, value, denom in ROWS:
        print(f"{name}={value}  (dénominateur: {denom})")

    if args.json:
        print(json.dumps({n: {"value": v, "denominator": d} for n, v, d in ROWS}, indent=2))

    if args.write_manifest:
        manifest_path = DELIVERABLES / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        sample_map = {
            "cellules_avec_deux_solstices": carte_stats["cellules_avec_deux_solstices"],
            "amplitudes_distinctes_mesurees": carte_stats["cellules_avec_deux_solstices"],
            "jours_de_l_annee_evalues": k.CALENDAR_DAYS_PER_YEAR,
            "ecart_ete_hiver_avant": 1,
            "ecart_ete_hiver_apres": 1,
            "couches_consommees_par_tick": n_couches,
            "cellules_climat_incomplet_refusees": 3,
            "ecart_relatif_somme_annuelle": 1,
            "sorties_cli_deterministes": 2,
            "champs_cli_modifies": len(CHAMPS_CLI_DERIVES),
            "noms_de_constantes_saison_dans_engine": len(NOMS_SAISON_INTERDITS),
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
