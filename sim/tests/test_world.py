"""
SC2 — Chargement du monde depuis les artefacts G3.

Vérifie que World.from_g3() charge le bon nombre de cellules (dérivé
du fichier — jamais codé en dur) et les arêtes d'adjacence.
"""

import json
import pathlib

import pytest

from sim.world import World

_REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_STATS_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "stats_g3.json"
_ADJACENCY_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "adjacency_g3.json"


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


def test_cells_have_required_fields():
    """Chaque cellule chargée possède les champs attendus avec des valeurs valides."""
    world = World.from_g3()
    for cid, cell in world.cells.items():
        assert cell.cell_id == cid
        assert cell.area_km2 > 0
        assert cell.population >= 0
        assert cell.food_stock_kg >= 0
        assert cell.hunger_ticks >= 0
