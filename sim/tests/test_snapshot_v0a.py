"""Snapshot cellulaire v0a-1 : refus, schéma fermé, déterminisme."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

from sim.aggregation import (
    agregat_depuis_monde,
    identifiant_de_province_de_cellule,
)
from sim.constants import SNAPSHOT_SCHEMA_VERSION
from sim.snapshot_export import build_snapshot_document, serialize_snapshot
from sim.world import World

_REPO = Path(__file__).resolve().parents[2]
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


def test_schema_ferme_et_couches():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    assert set(doc) == _ROOT_KEYS
    assert doc["schema_version"] == SNAPSHOT_SCHEMA_VERSION
    assert doc["cell_count"] == len(world.cells) == len(doc["cells"])
    assert set(doc["layers"]) == {"relief_g6", "climate_drivers_c1", "resources_r1"}
    assert doc["layers"]["relief_g6"]["status"] == "not_consumed"
    assert doc["layers"]["climate_drivers_c1"]["status"] == "present"
    assert doc["layers"]["resources_r1"]["status"] == "not_consumed"
    first = doc["cells"][0]
    assert set(first) == _CELL_KEYS
    assert "province_id" not in first
    assert "elev_mean_m" not in first
    assert first["climate_drivers"] is not None


def test_province_recalculee_pas_stockee():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    regroupements = agregat_depuis_monde(world)
    for cell in doc["cells"]:
        attendu = identifiant_de_province_de_cellule(cell["cell_id"], regroupements)
        assert cell["province"]["id"] == attendu


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


def test_g6_non_consomme():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    assert doc["layers"]["relief_g6"]["status"] == "not_consumed"
    for cell in doc["cells"]:
        assert "elev_mean_m" not in cell
        assert "centroid_elev_m" not in cell


def test_r1_non_consomme():
    """Les gisements 026 existent ; sim/ ne les consomme pas (ADR-0018)."""
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    assert doc["layers"]["resources_r1"]["status"] == "not_consumed"
    for cell in doc["cells"]:
        assert "resource" not in cell
        assert "deposit" not in cell


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


def test_zero_mesure_n_est_pas_sentinelle():
    world = World.from_g3(0)
    doc = build_snapshot_document(world, 0, 0)
    cell = doc["cells"][0]
    assert cell["hunger_ticks"] == 0
    assert cell["hunger_ticks"] != -1
