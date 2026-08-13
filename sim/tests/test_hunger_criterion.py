"""
SC4 brief 017 — « Affamée » veut dire en manque ce tick, pas garde-manger vide.

Une cellule ravitaillée EXACTEMENT à son besoin par le commerce termine le tick
avec un stock nul et un déficit nul : elle a mangé sa ration. L'ancien critère
(le stock résiduel nul après consommation) la comptait comme affamée.

Le scénario est celui du brief 013 (témoin / source / receveuse), rejoué avec
un tick complet.

Compteur : hunger_ticks_cellule_ravitaillee.
"""

import random

from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import _apply_consumption, _update_hunger, tick
from sim.model import Cell
from sim.world import World

POPULATION_SCENARIO = 50


def _build_monde_temoin_receveuse() -> tuple[World, int, int]:
    """
    Trois cellules, production désactivée (area_km2 = 0.0) :
    - témoin (100)    : possède exactement sa ration, aucune adjacence.
    - source (101)    : population nulle, stock = une ration à donner.
    - receveuse (102) : stock nul, déficit nul, reçoit exactement sa ration.
    """
    besoin_kg = POPULATION_SCENARIO * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK

    temoin = Cell(
        cell_id=100, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=besoin_kg, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    source = Cell(
        cell_id=101, area_km2=0.0, population=0,
        food_stock_kg=besoin_kg, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    receveuse = Cell(
        cell_id=102, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    adjacency = [{"a": 101, "b": 102, "kind": "land", "shared_length_m": 5000.0}]
    world = World(cells={100: temoin, 101: source, 102: receveuse}, adjacency=adjacency)
    return world, 100, 102


def test_hunger_ticks_cellule_ravitaillee():
    """
    SC4 — Après un tick complet, ni le témoin ni la receveuse ne sont comptés
    affamés : tous deux ont mangé exactement leur ration.

    Compteur : hunger_ticks_cellule_ravitaillee.
    """
    world, id_temoin, id_receveuse = _build_monde_temoin_receveuse()
    rng = random.Random(0)  # production désactivée : le tirage n'a pas d'effet

    tick(world, rng)

    temoin = world.cells[id_temoin]
    receveuse = world.cells[id_receveuse]

    print(f"temoin    : stock={temoin.food_stock_kg}, deficit={temoin.food_deficit_kg}, "
          f"hunger_ticks={temoin.hunger_ticks}")
    print(f"receveuse : stock={receveuse.food_stock_kg}, deficit={receveuse.food_deficit_kg}, "
          f"hunger_ticks={receveuse.hunger_ticks}")

    hunger_ticks_cellule_ravitaillee = temoin.hunger_ticks + receveuse.hunger_ticks
    print(f"hunger_ticks_cellule_ravitaillee = {hunger_ticks_cellule_ravitaillee}")

    assert temoin.food_stock_kg == 0.0 and temoin.food_deficit_kg == 0.0
    assert receveuse.food_stock_kg == 0.0 and receveuse.food_deficit_kg == 0.0
    assert temoin.hunger_ticks == 0, (
        "Le témoin possédait sa ration exacte : stock nul après consommation "
        "n'est pas de la sous-alimentation."
    )
    assert receveuse.hunger_ticks == 0, (
        "La receveuse a été ravitaillée exactement à son besoin : elle n'a "
        "manqué de rien ce tick."
    )
    assert hunger_ticks_cellule_ravitaillee == 0


def test_penurie_reelle_incremente_toujours():
    """
    SC4 — Le critère reste fonctionnel dans l'autre sens : une cellule qui
    manque réellement de nourriture voit bien hunger_ticks progresser.

    Sans ce contrôle, un critère qui n'incrémente jamais passerait le test
    ci-dessus (hard-won rule 6 : un contrôle trop grossier coûte autant qu'un
    contrôle laxiste).
    """
    cell = Cell(
        cell_id=1, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    penurie_kg = _apply_consumption(cell)
    _update_hunger(cell, penurie_kg)

    print(f"penurie_kg = {penurie_kg}, hunger_ticks = {cell.hunger_ticks}")
    assert penurie_kg > 0.0
    assert cell.hunger_ticks == 1


def test_penurie_retournee_est_le_manque_exact():
    """
    SC4 — La pénurie retournée par _apply_consumption est le manque en kg du
    tick, pas un booléen déguisé : elle vaut exactement
    besoin − stock disponible.
    """
    population = POPULATION_SCENARIO
    besoin = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    stock_partiel = besoin / (1 + 1)

    cell = Cell(
        cell_id=2, area_km2=0.0, population=population,
        food_stock_kg=stock_partiel, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    penurie_kg = _apply_consumption(cell)

    print(f"besoin={besoin}, stock={stock_partiel}, penurie_kg={penurie_kg}")
    assert abs(penurie_kg - (besoin - stock_partiel)) < 1e-9
    assert abs(cell.food_deficit_kg - penurie_kg) < 1e-9
