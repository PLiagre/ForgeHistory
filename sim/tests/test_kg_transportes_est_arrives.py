"""
SC5 brief 013 — Le compteur de transport mesure des kg arrivés, pas des sauts.

test_kg_transportes_egal_deltas_positifs :
    Sur un monde à 3 cellules avec 2 arêtes actives, l'accumulateur
    total_transported retourné par tick() est égal à la somme des
    variations positives de food_stock_kg pendant l'étape commerce.
    Écart doit être ≤ 1×10⁻⁹ kg.

    Compteur : ecart_kg_transportes_vs_arrives.
"""

import random

import pytest

from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import _apply_commerce
from sim.model import Cell
from sim.world import World

TOLERANCE = 1e-9


def test_kg_transportes_egal_deltas_positifs():
    """
    SC5 — L'accumulateur total_transported de _apply_commerce est identique
    à la somme des variations positives de food_stock_kg (kg effectivement
    arrivés dans les cellules receveurs).

    Avec le transport atomique (snapshot), chaque kg traverse au plus une
    arête. Aucun double comptage n'est possible.

    Monde : 3 cellules, 2 arêtes actives.
    - Cellule 1 : source (grand surplus)
    - Cellule 2 : receveur 1
    - Cellule 3 : receveur 2
    - Arêtes : 1-2 et 1-3 (cellule 1 peut donner aux deux)

    Compteur : ecart_kg_transportes_vs_arrives.
    """
    pop = 100
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 200 kg

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=besoin * 3,  # grand surplus : peut nourrir les deux
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_2 = Cell(
        cell_id=2,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=besoin,
    )
    cell_3 = Cell(
        cell_id=3,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=besoin,
    )

    adjacency = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 1, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = World(cells={1: cell_1, 2: cell_2, 3: cell_3}, adjacency=adjacency)

    # Snapshot des stocks avant commerce
    stocks_avant = {cid: c.food_stock_kg for cid, c in world.cells.items()}

    total_transported = [0.0]
    _apply_commerce(world, total_transported)

    # Somme des variations positives (kg effectivement arrivés)
    somme_deltas_positifs = sum(
        max(0.0, world.cells[cid].food_stock_kg - stocks_avant[cid])
        for cid in world.cells
    )

    ecart_kg_transportes_vs_arrives = abs(
        total_transported[0] - somme_deltas_positifs
    )

    print(f"total_transported = {total_transported[0]}")
    print(f"somme_deltas_positifs = {somme_deltas_positifs}")
    print(f"ecart_kg_transportes_vs_arrives = {ecart_kg_transportes_vs_arrives}")

    assert total_transported[0] > 0, (
        "Aucune nourriture transportée alors que des cellules avaient besoin."
    )
    assert ecart_kg_transportes_vs_arrives <= TOLERANCE, (
        f"Écart entre total_transported ({total_transported[0]}) et "
        f"somme_deltas_positifs ({somme_deltas_positifs}) = {ecart_kg_transportes_vs_arrives} > {TOLERANCE}. "
        "Double comptage résiduel détecté."
    )
