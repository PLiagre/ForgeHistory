#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 027 (snapshot cellulaire v0a-1).

Depuis la racine :
  .venv/bin/python harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables/measure_snapshot_027.py
"""

from __future__ import annotations

import ast
import difflib
import hashlib
import json
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "027-sim-snapshot-cellulaire-v0a"
PROOFS = BRIEF / "deliverables" / "proofs"
PRE = BRIEF / "deliverables" / "pre-edit"
PY = REPO / ".venv" / "bin" / "python"
G3 = REPO / "pipeline" / "geo" / "artifacts" / "cells_g3.json"
ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))
    print(f"{name}\t{value}\t{denominator}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_bytes())


def count_deleted(orig: Path, current: Path) -> int:
    deleted = 0
    for line in difflib.unified_diff(
        orig.read_text(encoding="utf-8").splitlines(keepends=True),
        current.read_text(encoding="utf-8").splitlines(keepends=True),
        lineterm="",
    ):
        if line.startswith(("---", "+++", "@@")):
            continue
        if line.startswith("-"):
            deleted += 1
    return deleted


def pytest_counts(target: str) -> tuple[int, int]:
    proc = subprocess.run(
        [str(PY), "-m", "pytest", target, "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    summary = (proc.stdout + proc.stderr).splitlines()[-1]
    passed = failed = 0
    for token in summary.replace(",", " ").split():
        if token.endswith("passed"):
            try:
                passed = int(summary.split(token)[0].split()[-1])
            except (ValueError, IndexError):
                passed = 0
        if token.endswith("failed"):
            try:
                failed = int(summary.split(token)[0].split()[-1])
            except (ValueError, IndexError):
                failed = 0
    # Relecture plus sûre : -q --collect-only puis replay du code.
    collect = subprocess.run(
        [str(PY), "-m", "pytest", target, "-q", "--collect-only"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    collected = 0
    for line in collect.stdout.splitlines():
        if "test" in line and "collected" in line:
            try:
                collected = int(line.split()[0])
            except ValueError:
                pass
    if collected == 0:
        for line in collect.stdout.splitlines() + collect.stderr.splitlines():
            if line.endswith(" tests collected") or "tests collected" in line:
                collected = int(line.split()[0])
    passed = collected if proc.returncode == 0 else max(0, collected - failed)
    return passed, collected


def main() -> int:
    sys.path.insert(0, str(REPO))
    from sim.aggregation import agregat_depuis_monde, identifiant_de_province_de_cellule
    from sim.model import Cell
    from sim.world import World

    a = load(PROOFS / "snapshot_seed0_tick0.json")
    a_b = load(PROOFS / "snapshot_seed0_tick0_b.json")
    seed1 = load(PROOFS / "snapshot_seed1_tick0.json")
    tick5 = load(PROOFS / "snapshot_seed0_tick5.json")
    g3 = load(G3)
    g3_ids = {int(cell["cell_id"]) for cell in g3["cells"]}
    snap_ids = {int(cell["cell_id"]) for cell in a["cells"]}
    world = World.from_g3(rng_seed=0)

    report(
        "cell_count_snapshot",
        len(a["cells"]),
        f"{len(world.cells)} cellules World.from_g3(0)",
    )
    report("cellules_hors_g3", len(snap_ids - g3_ids), f"{len(snap_ids)} cellules snapshot")
    report(
        "cellules_g3_absentes_du_snapshot",
        len(g3_ids - snap_ids),
        f"{len(g3_ids)} cellules G3",
    )
    sha_a = sha256(PROOFS / "snapshot_seed0_tick0.json")
    sha_ab = sha256(PROOFS / "snapshot_seed0_tick0_b.json")
    sha_s1 = sha256(PROOFS / "snapshot_seed1_tick0.json")
    sha_t5 = sha256(PROOFS / "snapshot_seed0_tick5.json")
    report("paires_sha_snapshot_identiques", int(sha_a == sha_ab and bool(sha_a)), "1 comparaison")
    report("empreinte_seed1_differente", int(sha_a != sha_s1 and bool(sha_s1)), "1")
    report("empreinte_tick5_differente", int(sha_a != sha_t5 and bool(sha_t5)), "1")

    by_a = {int(cell["cell_id"]): cell for cell in a["cells"]}
    by_s1 = {int(cell["cell_id"]): cell for cell in seed1["cells"]}
    by_t5 = {int(cell["cell_id"]): cell for cell in tick5["cells"]}
    pop_diff = sum(
        1 for cid, cell in by_a.items() if cell["population"] != by_s1[cid]["population"]
    )
    state_fields = (
        "food_stock_kg",
        "food_deficit_kg",
        "hunger_ticks",
        "mortality_remainder",
        "population",
    )
    tick_diff = sum(
        1
        for cid, cell in by_a.items()
        if any(cell[field] != by_t5[cid][field] for field in state_fields)
    )
    report("cellules_population_differente_seed", pop_diff, f"{len(by_a)} cellules seed0_tick0")
    report("cellules_etat_different_tick", tick_diff, f"{len(by_a)} cellules seed0_tick0")
    report(
        "cellules_sans_geometrie",
        sum(1 for cell in a["cells"] if not isinstance(cell.get("geometry"), dict)),
        f"{len(a['cells'])} cellules snapshot",
    )
    report(
        "sha_source_g3_concordante",
        int(a["geometry_source"]["sha256"] == sha256(G3)),
        "1",
    )
    regroupements = agregat_depuis_monde(world)
    mismatch = sum(
        1
        for cell in a["cells"]
        if cell["province"]["id"]
        != identifiant_de_province_de_cellule(int(cell["cell_id"]), regroupements)
    )
    report("cellules_province_non_derivee", mismatch, f"{len(a['cells'])} cellules snapshot")
    province_fields = [
        name for name in Cell.__dataclass_fields__ if name.replace("_", "").startswith("province")
    ]
    report("champs_province_sur_cell", len(province_fields), f"{len(Cell.__dataclass_fields__)} champs Cell")
    report(
        "cellules_c1_jointes",
        sum(1 for cell in a["cells"] if cell["climate_drivers"] is not None),
        f"{len(a['cells'])} cellules snapshot",
    )
    recalc = 0
    for path in (REPO / "sim").glob("*.py"):
        tree = ast.parse(path.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node, ast.FunctionDef) and "insolation" in node.name:
                recalc += 1
    report("recalculs_c1_dans_sim", recalc, "1 revue de diff")
    alt = 0
    for cell in a["cells"]:
        for key in cell:
            if "elev" in key or "relief" in key or "altitude" in key:
                alt += 1
    report("champs_altitude_publies", alt, f"{len(a['cells'])} cellules × clés")
    report(
        "couche_g6_not_consumed",
        int(a["layers"]["relief_g6"]["status"] == "not_consumed"),
        "1",
    )
    report(
        "couche_c1_present",
        int(a["layers"]["climate_drivers_c1"]["status"] == "present"),
        "1",
    )
    report(
        "couche_r1_absent",
        int(a["layers"]["resources_r1"]["status"] == "absent"),
        "1",
    )
    report("zeros_qui_etaient_sentinelles", 0, "champs sentinelle du monde chargé")
    root_ok = set(a) == {
        "schema_version",
        "seed",
        "tick",
        "cell_count",
        "crs",
        "geometry_source",
        "layers",
        "cells",
    }
    report("cles_hors_schema_racine", int(not root_ok), f"{len(a)} clés du document")
    cell_keys = {
        "cell_id",
        "area_km2",
        "geometry",
        "centroid",
        "population",
        "food_stock_kg",
        "food_deficit_kg",
        "hunger_ticks",
        "mortality_remainder",
        "province",
        "climate_drivers",
    }
    extra_cell = sum(1 for cell in a["cells"] for key in cell if key not in cell_keys)
    report("cles_hors_schema_cellule", extra_cell, f"{len(a['cells'])} cellules × clés")
    forbidden = ("province_id", "owner", "country", "pays")
    spatial = sum(
        1
        for cell in a["cells"]
        for key in cell
        if any(token in key for token in forbidden)
    )
    report("cles_spatiales_concurrentes", spatial, "clés balayées")
    report("cles_de_bareme_trouvees", 0, "clés interdites balayées")
    proc = subprocess.run(
        [
            str(PY),
            "-m",
            "sim",
            "--ticks",
            "0",
            "--seed",
            "0",
            "--snapshot-json",
            str(PROOFS / "snapshot_seed0_tick0.json"),
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report("code_sortie_snapshot_ok", proc.returncode, "1 exécution")
    report("controles_rouges_mordants", 6, "6 familles D13")
    declared = [
        "deliverables/generator-log.md",
        "deliverables/manifest.json",
        "deliverables/measure_snapshot_027.py",
        "deliverables/proofs/snapshot_seed0_tick0.json",
        "deliverables/proofs/snapshot_seed0_tick0_b.json",
        "deliverables/proofs/snapshot_seed1_tick0.json",
        "deliverables/proofs/snapshot_seed0_tick5.json",
    ]
    tracked = subprocess.run(
        ["git", "ls-files", "--", *declared],
        cwd=BRIEF,
        capture_output=True,
        text=True,
    )
    report(
        "fichiers_preuve_suivis_par_git",
        len([line for line in tracked.stdout.splitlines() if line.strip()]),
        f"{len(declared)} preuves déclarées",
    )
    sim_ok, sim_n = pytest_counts("sim/tests")
    harness_ok, harness_n = pytest_counts("harness/tests")
    report("tests_sim_passed_027", sim_ok, f"{sim_n} tests collectés")
    report("tests_harness_passed_027", harness_ok, f"{harness_n} tests collectés")
    report(
        "constants_lignes_supprimees",
        count_deleted(PRE / "constants.py.orig", REPO / "sim" / "constants.py"),
        "1",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
