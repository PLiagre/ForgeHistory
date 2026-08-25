"""
Chargement du monde, ligne de commande et snapshot.

Ce que ce fichier protège (ADR-0018) :
  - le monde chargé correspond au fichier de carte, sans nombre codé en dur ;
  - la ligne de commande amorce un monde et le rend en JSON stable ;
  - le snapshot a un schéma fermé, recalcule la province au lieu de la
    stocker, et distingue une sentinelle « non calculé » d'un zéro mesuré.

Fusion des anciens fichiers world, cli et snapshot_v0a.
"""

from __future__ import annotations

import json
import pathlib
import pytest
from sim.world import World
_REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_STATS_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "stats_g3.json"
_ADJACENCY_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "adjacency_g3.json"
import subprocess
import sys
from pathlib import Path
from sim.__main__ import run
from sim.constants import DEFAULT_CLI_SEED
_REPO = Path(__file__).resolve().parents[2]
import hashlib
from sim.aggregation import (
    agregat_depuis_monde,
    identifiant_de_province_de_cellule,
)
from sim.constants import SNAPSHOT_SCHEMA_VERSION
from sim.snapshot_export import build_snapshot_document, serialize_snapshot
_ROOT_KEYS = {
    "schema_version",
    "seed",
    "tick",
    "cell_count",
    "crs",
    "geometry_source",
    "layers",
    "cells",
}
_CELL_KEYS = {
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
def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# --- test_world.py ---
def test_cells_count_matches_stats():
    """
    SC2 : le nombre de cellules chargées correspond à cell_count dans stats_g3.json.
    Les deux valeurs sont affichées côte à côte.
    Compteur : cells_chargees.
    """
    world = World.from_g3()

    stats = json.loads(_STATS_PATH.read_text(encoding="utf-8"))
    expected = stats["cell_count"]
    actual = len(world.cells)

    print(f"cells_chargees (chargées) = {actual}")
    print(f"cell_count (stats_g3.json) = {expected}")
    print(f"cells_chargees == cell_count : {actual == expected}")

    assert actual == expected, (
        f"Nombre de cellules chargées ({actual}) "
        f"ne correspond pas à cell_count ({expected}) dans stats_g3.json."
    )


# --- test_world.py ---
def test_adjacency_count_matches_file():
    """
    SC2 : le nombre d'arêtes chargées correspond à la longueur totale
    du tableau adjacency dans adjacency_g3.json.
    Compteur : aretes_adjacence_chargees.
    """
    world = World.from_g3()

    adj_doc = json.loads(_ADJACENCY_PATH.read_text(encoding="utf-8"))
    expected = len(adj_doc["adjacency"])
    actual = len(world.adjacency)

    print(f"aretes_adjacence_chargees (chargées) = {actual}")
    print(f"longueur adjacency_g3.json = {expected}")
    print(f"aretes_adjacence_chargees == len(adjacency) : {actual == expected}")

    assert actual == expected, (
        f"Nombre d'arêtes chargées ({actual}) "
        f"ne correspond pas à la longueur du fichier ({expected})."
    )


# --- test_world.py ---
def test_cells_have_required_fields():
    """Chaque cellule chargée possède les champs attendus avec des valeurs valides."""
    world = World.from_g3()
    for cid, cell in world.cells.items():
        assert cell.cell_id == cid
        assert cell.area_km2 > 0
        assert cell.population >= 0
        assert cell.food_stock_kg >= 0
        assert cell.hunger_ticks >= 0


# --- test_cli.py ---
def test_run_zero_tick_preserve_population():
    resume = run(ticks=0, seed=DEFAULT_CLI_SEED)
    assert resume["sans_unity"] is True
    assert resume["ticks"] == 0
    assert resume["cellules"] > 0
    assert resume["population_depart"] == resume["population_arrivee"]
    assert resume["kg_transportes"] == 0.0


# --- test_cli.py ---
def test_run_est_deterministe():
    a = run(ticks=1, seed=DEFAULT_CLI_SEED)
    b = run(ticks=1, seed=DEFAULT_CLI_SEED)
    assert a == b


# --- test_cli.py ---
def test_module_cli_json():
    proc = subprocess.run(
        [sys.executable, "-m", "sim", "--ticks", "0", "--json"],
        cwd=_REPO,
        check=True,
        capture_output=True,
        text=True,
    )
    data = json.loads(proc.stdout)
    assert data["sans_unity"] is True
    assert data["ticks"] == 0
    assert data["cellules"] > 0


# --- test_snapshot_v0a.py ---
def test_schema_ferme_et_couches():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    assert set(doc) == _ROOT_KEYS
    assert doc["schema_version"] == SNAPSHOT_SCHEMA_VERSION
    assert doc["cell_count"] == len(world.cells) == len(doc["cells"])
    assert set(doc["layers"]) == {"relief_g6", "climate_drivers_c1", "resources_r1"}
    assert doc["layers"]["relief_g6"]["status"] == "not_consumed"
    assert doc["layers"]["climate_drivers_c1"]["status"] == "present"
    assert doc["layers"]["resources_r1"]["status"] == "absent"
    first = doc["cells"][0]
    assert set(first) == _CELL_KEYS
    assert "province_id" not in first
    assert "elev_mean_m" not in first
    assert first["climate_drivers"] is not None


# --- test_snapshot_v0a.py ---
def test_province_recalculee_pas_stockee():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    regroupements = agregat_depuis_monde(world)
    for cell in doc["cells"]:
        attendu = identifiant_de_province_de_cellule(cell["cell_id"], regroupements)
        assert cell["province"]["id"] == attendu


# --- test_snapshot_v0a.py ---
def test_deux_passes_identiques_et_graines_differentes():
    world_a = World.from_g3(0)
    world_b = World.from_g3(0)
    world_c = World.from_g3(1)
    a = serialize_snapshot(build_snapshot_document(world_a, 0, 0))
    b = serialize_snapshot(build_snapshot_document(world_b, 0, 0))
    c = serialize_snapshot(build_snapshot_document(world_c, 1, 0))
    assert _sha(a) == _sha(b)
    assert _sha(a) != _sha(c)
    cells_a = json.loads(a)["cells"]
    cells_c = json.loads(c)["cells"]
    assert any(
        left["population"] != right["population"]
        for left, right in zip(cells_a, cells_c)
    )


# --- test_snapshot_v0a.py ---
def test_cli_snapshot_et_refus_schema(tmp_path: Path):
    dest = tmp_path / "nested" / "world.json"
    proc = subprocess.run(
        [
            sys.executable,
            "-m",
            "sim",
            "--ticks",
            "0",
            "--seed",
            "0",
            "--snapshot-json",
            str(dest),
        ],
        cwd=_REPO,
        check=True,
        capture_output=True,
        text=True,
    )
    assert dest.is_file()
    data = json.loads(dest.read_bytes())
    assert data["schema_version"] == SNAPSHOT_SCHEMA_VERSION
    assert "cells" not in proc.stdout

    broken = json.loads(dest.read_bytes())
    del broken["schema_version"]
    assert "schema_version" not in broken


# --- test_snapshot_v0a.py ---
def test_rouge_sentinelle_et_cle_spatiale():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    raw = serialize_snapshot(doc)
    altered = bytearray(raw)
    altered[len(altered) // 2] ^= 0x01
    assert _sha(bytes(altered)) != _sha(raw)
    cell = doc["cells"][0]
    assert cell["hunger_ticks"] == 0
    assert cell["food_deficit_kg"] == 0.0
    forged = dict(cell)
    forged["hunger_ticks"] = -1
    assert forged["hunger_ticks"] != cell["hunger_ticks"]
    assert "province_id" not in cell
    assert "owner" not in cell


# --- test_snapshot_v0a.py ---
def test_g6_non_consomme():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    assert doc["layers"]["relief_g6"]["status"] == "not_consumed"
    for cell in doc["cells"]:
        assert "elev_mean_m" not in cell
        assert "centroid_elev_m" not in cell


# --- test_snapshot_v0a.py ---
def test_sentinelle_moins_un_n_est_pas_zero():
    world = World.from_g3(0)
    cell = next(iter(world.cells.values()))
    original = cell.hunger_ticks
    cell.hunger_ticks = -1
    try:
        doc = build_snapshot_document(world, 0, 0)
        exported = next(item for item in doc["cells"] if item["cell_id"] == cell.cell_id)
        assert exported["hunger_ticks"] == -1
        assert exported["hunger_ticks"] != 0
        assert exported["hunger_ticks"] is not None
    finally:
        cell.hunger_ticks = original


# --- test_snapshot_v0a.py ---
def test_zero_mesure_n_est_pas_sentinelle():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    cell = doc["cells"][0]
    assert cell["hunger_ticks"] == 0
    assert cell["hunger_ticks"] != -1
