"""
SC5 brief 013 — Le compteur de transport mesure des kg arrivés, pas des sauts.

test_kg_transportes_egal_deltas_positifs (N2 feedback 001) :
    Topologie chaîne (cellule 1 → cellule 2 → cellule 3).
    Seule topologie où un kg pourrait franchir deux arêtes dans un tick (multi-saut).
    Avec l'ancien maillon du lot 012 : cellule 2 reçoit de cellule 1 via arête 1-2,
    puis redistribue vers cellule 3 via arête 2-3 dans le même tick →
    total_transported compte les deux arêtes, sur-comptant la réalité.
    Avec le maillon brief 013 (snapshot) : la cellule 2 n'a pas de surplus au
    snapshot → ne peut pas redistribuer → total_transported = kg reçus par les
    cellules, écart nul.

    Compteur : ecart_kg_transportes_vs_arrives.

test_kg_transportes_etoile (cas supplémentaire) :
    Topologie étoile : source unique, deux receveurs. Vérifie le cas de base
    (pas de double arête).
"""

import random

import pytest

from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import _apply_commerce
from sim.model import Cell
from sim.world import World

TOLERANCE = 1e-9


def _run_commerce_ecart(world: World) -> float:
    """Lance _apply_commerce et retourne l'écart entre kg comptés et kg arrivés."""
    stocks_avant = {cid: c.food_stock_kg for cid, c in world.cells.items()}
    total_transported = [0.0]
    _apply_commerce(world, total_transported)

    somme_deltas_positifs = sum(
        max(0.0, world.cells[cid].food_stock_kg - stocks_avant[cid])
        for cid in world.cells
    )
    return total_transported[0], somme_deltas_positifs


def test_kg_transportes_egal_deltas_positifs():
    """
    SC5 / N2 — Topologie chaîne : seul le receveur direct (cellule 2) reçoit
    de la nourriture. La cellule 3 (non adjacente à la source) ne reçoit rien
    via snapshot. total_transported = kg reçus par cellule 2, écart nul.

    Sans le snapshot (ancien comportement 012) : cellule 2 recevrait 200 kg
    (CAPACITY) et en redistribuerait 100 kg à cellule 3 dans le même tick ;
    total_transported compterait 300 kg alors que seuls 200 kg ont quitté une
    source — sur-comptage de 100 kg. Ce test rougirait.

    Monde : cellule 1 (source, pop=0, stock=500), cellule 2 (pop=50, stock=0,
    besoin_snapshot=100), cellule 3 (pop=50, stock=0, besoin_snapshot=100).
    Arêtes : 1-2 et 2-3.

    Compteur : ecart_kg_transportes_vs_arrives.
    """
    pop = 50
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 100 kg

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=500.0,  # grande réserve
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_2 = Cell(
        cell_id=2,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=500.0,  # grand déficit accumulé
    )
    cell_3 = Cell(
        cell_id=3,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=500.0,
    )

    adjacency = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = World(cells={1: cell_1, 2: cell_2, 3: cell_3}, adjacency=adjacency)

    transported, arrived = _run_commerce_ecart(world)
    ecart_kg_transportes_vs_arrives = abs(transported - arrived)

    print(f"total_transported = {transported}")
    print(f"somme_deltas_positifs (kg arrivés) = {arrived}")
    print(f"ecart_kg_transportes_vs_arrives = {ecart_kg_transportes_vs_arrives}")

    assert transported > 0, (
        "Aucune nourriture transportée alors que cellule 2 avait besoin."
    )
    assert ecart_kg_transportes_vs_arrives <= TOLERANCE, (
        f"Écart entre total_transported ({transported}) et "
        f"kg arrivés ({arrived}) = {ecart_kg_transportes_vs_arrives} > {TOLERANCE}. "
        "Double comptage possible (transport non atomique)."
    )


def test_kg_transportes_etoile():
    """
    SC5 (cas supplémentaire) — Topologie étoile : une source, deux receveurs.
    Aucun kg ne peut traverser deux arêtes dans cette topologie.
    Le test vérifie que total_transported = kg reçus par les deux receveurs.
    """
    pop = 100
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 200 kg

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=besoin * 3,  # peut nourrir les deux
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

    transported, arrived = _run_commerce_ecart(world)
    ecart = abs(transported - arrived)

    print(f"[étoile] total_transported = {transported}")
    print(f"[étoile] somme_deltas_positifs = {arrived}")
    print(f"[étoile] ecart = {ecart}")

    assert transported > 0
    assert ecart <= TOLERANCE
