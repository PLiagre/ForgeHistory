"""
Chargement et représentation du monde simulé.

World.from_g3() charge les artefacts géographiques G3 et amorce
la population de chaque cellule avec un rng_seed déterministe.
"""

import json
import pathlib
import random

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    INITIAL_FOOD_DAYS,
    INITIAL_POPULATION_PER_KM2,
    SEED_POPULATION_VARIATION_HIGH,
    SEED_POPULATION_VARIATION_LOW,
)
from sim.model import Cell

# Racine du dépôt : deux niveaux au-dessus du paquet sim/
_REPO_ROOT = pathlib.Path(__file__).parent.parent

_CELLS_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "cells_g3.json"
_ADJACENCY_PATH = _REPO_ROOT / "pipeline" / "geo" / "artifacts" / "adjacency_g3.json"


def _seed_population(area_km2: float, rng: random.Random) -> int:
    """Amorçage paramétrique de la population (voir sim/SEEDING.md)."""
    base = area_km2 * INITIAL_POPULATION_PER_KM2
    variation = rng.uniform(SEED_POPULATION_VARIATION_LOW, SEED_POPULATION_VARIATION_HIGH)
    return max(0, int(base * variation))


def _seed_food_stock(population: int) -> float:
    """Stock alimentaire initial : INITIAL_FOOD_DAYS ticks de consommation."""
    daily_need = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    return daily_need * INITIAL_FOOD_DAYS


class World:
    """
    Représentation complète du monde simulé à un instant donné.

    Attributs :
        cells      : dict cell_id → Cell
        adjacency  : liste des arêtes d'adjacence G3
    """

    def __init__(self, cells: dict, adjacency: list):
        self.cells = cells
        self.adjacency = adjacency

    @classmethod
    def from_g3(cls, rng_seed: int = 0) -> "World":
        """
        Charge les artefacts G3 et amorce le monde.

        Le nombre de cellules est dérivé du fichier — jamais codé en dur.
        """
        rng = random.Random(rng_seed)

        raw_cells_doc = json.loads(_CELLS_PATH.read_text(encoding="utf-8"))
        raw_adjacency_doc = json.loads(_ADJACENCY_PATH.read_text(encoding="utf-8"))

        raw_cells = raw_cells_doc["cells"]
        raw_adjacency = raw_adjacency_doc["adjacency"]

        cells: dict = {}
        for raw in raw_cells:
            cid = raw["cell_id"]
            area = raw["area_km2"]
            pop = _seed_population(area, rng)
            stock = _seed_food_stock(pop)
            cell = Cell(
                cell_id=cid,
                area_km2=area,
                population=pop,
                food_stock_kg=stock,
                hunger_ticks=0,
            )
            cells[cid] = cell

        return cls(cells=cells, adjacency=raw_adjacency)

    def to_dict(self) -> dict:
        """
        Sérialisation canonique pour calcul d'empreinte SHA256.
        Les clés sont triées pour garantir le déterminisme.
        """
        return {
            "cells": {
                str(cid): {
                    "cell_id": c.cell_id,
                    "area_km2": c.area_km2,
                    "population": c.population,
                    "food_stock_kg": c.food_stock_kg,
                    "hunger_ticks": c.hunger_ticks,
                }
                for cid, c in sorted(self.cells.items())
            }
        }
