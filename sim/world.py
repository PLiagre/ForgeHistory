"""
Chargement et représentation du monde simulé.

World.charger() lit la carte figée `data/world-1400.json` — un seul
fichier, produit une fois par `tools/map/build_world.py` (ADR-0018) — et
amorce la population de chaque cellule avec un rng_seed déterministe.
"""

import json
import pathlib
import random

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    INITIAL_FOOD_RESERVE_TICKS,
    INITIAL_POPULATION_PER_KM2,
    MARCHANDISE_NOURRITURE,
    SEED_POPULATION_VARIATION_HIGH,
    SEED_POPULATION_VARIATION_LOW,
)
from sim.model import Cell, cellule_vers_dict

# Racine du dépôt : deux niveaux au-dessus du paquet sim/
_REPO_ROOT = pathlib.Path(__file__).parent.parent

# La carte figée est la seule entrée géographique du jeu (ADR-0018).
CARTE_RELATIVE = "data/world-1400.json"
CARTE_PATH = _REPO_ROOT / "data" / "world-1400.json"


def _seed_population(area_km2: float, rng: random.Random) -> int:
    """Amorçage paramétrique de la population (voir sim/MODELE.md)."""
    base = area_km2 * INITIAL_POPULATION_PER_KM2
    variation = rng.uniform(SEED_POPULATION_VARIATION_LOW, SEED_POPULATION_VARIATION_HIGH)
    return max(0, int(base * variation))


def _seed_food_stock(population: int) -> float:
    """Stock alimentaire initial : INITIAL_FOOD_RESERVE_TICKS ticks de consommation."""
    tick_need = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    return tick_need * INITIAL_FOOD_RESERVE_TICKS


class World:
    """
    Représentation complète du monde simulé à un instant donné.

    Attributs :
        cells      : dict cell_id → Cell (l'état que le moteur fait évoluer)
        adjacency  : liste des arêtes d'adjacence entre cellules
        carte      : dict cell_id → enregistrement de la carte figée
                     (géométrie, centroïde, relief, climat, gisements).
                     Donnée de terrain, en lecture seule : le moteur ne la
                     modifie jamais.
        carte_meta : l'en-tête de la carte (version, projection, versions
                     du pipeline qui l'a produite).
    """

    def __init__(self, cells: dict, adjacency: list,
                 carte: dict | None = None, carte_meta: dict | None = None):
        self.cells = cells
        self.adjacency = adjacency
        self.carte = carte or {}
        self.carte_meta = carte_meta or {}

    @classmethod
    def lire_carte(cls) -> dict:
        """La carte figée, telle qu'elle est sur le disque."""
        if not CARTE_PATH.is_file():
            raise FileNotFoundError(
                f"Carte du monde introuvable : {CARTE_PATH}. "
                "La reconstruire avec `python tools/map/build_world.py`."
            )
        return json.loads(CARTE_PATH.read_text(encoding="utf-8"))

    @classmethod
    def charger(cls, rng_seed: int = 0, carte_doc: dict | None = None) -> "World":
        """
        Amorce le monde à partir de la carte figée.

        Le nombre de cellules est dérivé du fichier — jamais codé en dur.

        `carte_doc` permet d'amorcer depuis une carte déjà en mémoire au lieu
        du disque. Sert aux sondes qui demandent « le moteur lit-il cette
        couche ? » : altérer la carte APRÈS le chargement ne prouverait rien
        d'un moteur qui la lit AU chargement. Aucun appelant du jeu ne s'en
        sert ; le comportement par défaut est inchangé.
        """
        rng = random.Random(rng_seed)

        if carte_doc is None:
            carte_doc = cls.lire_carte()

        raw_cells = carte_doc["cellules"]
        raw_adjacency = carte_doc["adjacence"]
        carte = {int(raw["cell_id"]): raw for raw in raw_cells}
        carte_meta = {cle: valeur for cle, valeur in carte_doc.items()
                      if cle not in ("cellules", "adjacence")}

        cells: dict = {}
        for raw in raw_cells:
            cid = raw["cell_id"]
            area = raw["area_km2"]
            pop = _seed_population(area, rng)
            stock = _seed_food_stock(pop)
            stocks_init = {MARCHANDISE_NOURRITURE: stock}
            cell = Cell(
                cell_id=cid,
                area_km2=area,
                population=pop,
                stocks=stocks_init,
                hunger_ticks=0,
                food_deficit_kg=0.0,
                # Monde amorcé : aucune fraction de mort en attente.
                # La sentinelle -1.0 signifie « non calculé », jamais « nul ».
                mortality_remainder=0.0,
                migration_remainder=0.0,
            )
            cells[cid] = cell

        return cls(cells=cells, adjacency=raw_adjacency,
                   carte=carte, carte_meta=carte_meta)

    def to_dict(self) -> dict:
        """
        Sérialisation canonique pour calcul d'empreinte SHA256.
        Les clés sont triées pour garantir le déterminisme.
        """
        return {
            "cells": {
                str(cid): cellule_vers_dict(c)
                for cid, c in sorted(self.cells.items())
            }
        }
