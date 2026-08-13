"""
SC4 — Commerce inter-cellules et conservation de masse (brief 012).

Deux tests :

1. test_deficit_accumulé_quand_manque
   Vérifie que food_deficit_kg est bien écrit (> 0) quand la consommation
   dépasse le stock disponible sur un tick complet.
   Compteur : food_deficit_kg_ecrit_quand_manque (PASS/FAIL).
   Cellule construite à la main, area_km2 ≥ 1.0 (minimum réel G3 = 1.444877 km²).

2. test_conservation_masse_transport
   Mini-monde de 2 cellules adjacentes (une en surplus, une en déficit).
   Vérifie que sum(food_stock_kg) avant étape commerce = sum(food_stock_kg) après,
   à 1×10⁻⁹ kg près.
   Compteur : conservation_masse_transport (True si conservé).

   PAIRE DE PREUVE ROUGE : ce test constitue le test de conservation
   référencé dans la paire proof_red/run_transport_red.txt (sabotage)
   et run_transport_green.txt (correct).
"""

import random

import pytest

from sim.engine import _apply_commerce, _apply_consumption, _apply_production, tick
from sim.model import Cell
from sim.world import World


def test_deficit_accumule_quand_manque():
    """
    SC3 + SC4 — food_deficit_kg est écrit (> 0) quand la consommation
    dépasse le stock initial disponible sur un tick complet.

    Cellule construite à la main, area_km2 = 1.0 km² (≥ minimum G3).
    Consommation = population × 2 kg/tick >> stock initial.
    """
    # Cellule avec grand population et petit stock : déficit garanti
    # area_km2 = 1.0 : production max ≈ 1.0 × 18 × 1.5 = 27 kg
    # population = 1000 : consommation = 1000 × 2 = 2000 kg >> stock initial = 10 kg
    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=1000,
        food_stock_kg=10.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    world = World(cells={1: cell}, adjacency=[])
    rng = random.Random(42)

    tick(world, rng)

    food_deficit_kg_ecrit_quand_manque = world.cells[1].food_deficit_kg > 0
    print(f"food_deficit_kg après tick : {world.cells[1].food_deficit_kg}")
    print(f"food_deficit_kg_ecrit_quand_manque = {food_deficit_kg_ecrit_quand_manque}")

    assert food_deficit_kg_ecrit_quand_manque, (
        f"food_deficit_kg devrait être > 0 après un tick de famine : "
        f"obtenu {world.cells[1].food_deficit_kg}"
    )


def test_conservation_masse_transport():
    """
    SC4 — Conservation stricte de la masse lors du commerce.

    Mini-monde de 2 cellules adjacentes :
    - Cellule A : surplus (food_stock_kg > 0, food_deficit_kg = 0)
    - Cellule B : déficit (food_stock_kg = 0, food_deficit_kg > 0)

    Vérifie que sum(food_stock_kg) avant = sum(food_stock_kg) après,
    à 1×10⁻⁹ kg près.

    Conservation : la masse totale ne doit pas changer.
    """
    cell_a = Cell(
        cell_id=1,
        area_km2=10.0,
        population=50,
        food_stock_kg=5000.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_b = Cell(
        cell_id=2,
        area_km2=10.0,
        population=200,
        food_stock_kg=0.0,
        hunger_ticks=5,
        food_deficit_kg=300.0,
    )

    # Arête d'adjacence entre A et B (format G3 : champs 'a' et 'b')
    adjacency = [{"a": 1, "b": 2, "kind": "land", "shared_length_m": 10000.0}]
    world = World(cells={1: cell_a, 2: cell_b}, adjacency=adjacency)

    somme_avant = sum(c.food_stock_kg for c in world.cells.values())

    total_transported = [0.0]
    _apply_commerce(world, total_transported)

    somme_apres = sum(c.food_stock_kg for c in world.cells.values())

    ecart = abs(somme_avant - somme_apres)
    tolerance = 1e-9

    conservation_masse_transport = ecart <= tolerance
    print(f"somme_avant = {somme_avant}")
    print(f"somme_apres = {somme_apres}")
    print(f"écart = {ecart}")
    print(f"conservation_masse_transport = {conservation_masse_transport}")
    print(f"kg_transportes = {total_transported[0]}")

    assert conservation_masse_transport, (
        f"La masse n'est pas conservée : avant={somme_avant}, après={somme_apres}, "
        f"écart={ecart} (tolérance={tolerance})"
    )
    assert total_transported[0] > 0, (
        "Aucun kilogramme transporté alors qu'une cellule avait un déficit et "
        "l'autre un surplus."
    )
