"""
Moteur de simulation : boucle de tick.

tick(world, rng) fait avancer l'état du monde d'un pas de temps.
Chaîne causale (brief 012) :
    _apply_production  → produit de la nourriture avec variabilité rng
    _apply_consumption → consomme le stock, accumule food_deficit_kg si manque
    _apply_commerce    → transfère de la nourriture entre cellules adjacentes
    _update_hunger     → met à jour hunger_ticks selon le stock restant
    _apply_mortality   → mortalité proportionnelle à food_deficit_kg

Règle SC9 : aucun littéral numérique non nommé dans les fonctions de calcul.
Toutes les constantes paramétriques sont dans sim/constants.py.
"""

import random

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    HUNGER_DEATH_SCALE,
    MAX_DEATH_RATE_PER_TICK,
    RNG_YIELD_HIGH,
    RNG_YIELD_LOW,
    TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
)
from sim.model import Cell


def _apply_production(cell: Cell, rng: random.Random) -> None:
    """
    Maillon 1 — Production.
    Calcule la nourriture produite avec un facteur de rendement aléatoire
    tiré du rng (fluctuations climatiques/agronomiques) et l'ajoute au stock.
    Traite la sentinelle -1 comme un stock initial nul.
    """
    yield_factor = rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
    food_produced = cell.area_km2 * FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * yield_factor
    current = cell.food_stock_kg if cell.food_stock_kg >= 0 else 0.0
    cell.food_stock_kg = current + food_produced


def _apply_consumption(cell: Cell) -> None:
    """
    Maillon 2 — Consommation.
    Lit food_stock_kg, soustrait la consommation.
    Si stock ≥ consommation : ré-écrit le stock résiduel, remet food_deficit_kg à 0.
    Si stock < consommation : stock = 0, le manque est ajouté à food_deficit_kg.
    """
    tick_need = cell.population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    remaining = cell.food_stock_kg - tick_need
    if remaining >= 0.0:
        cell.food_stock_kg = remaining
        cell.food_deficit_kg = 0.0
    else:
        shortage = -remaining
        prev_deficit = cell.food_deficit_kg if cell.food_deficit_kg > 0 else 0.0
        cell.food_deficit_kg = prev_deficit + shortage
        cell.food_stock_kg = 0.0


def _apply_commerce(world, total_transported: list) -> None:
    """
    Maillon 3 — Commerce inter-cellules (brief 012, SC4).

    Pour chaque arête d'adjacence, transfère de la nourriture d'une cellule
    en surplus vers une cellule en déficit (food_deficit_kg > 0).
    La quantité transférée est bornée par :
      - TRADE_CAPACITY_KG_PER_EDGE_PER_TICK
      - le surplus réel de la source
      - le déficit réel de la destination

    Conservation stricte : la somme des stocks est inchangée.
    `total_transported` est une liste à un élément (accumulateur mutable).

    Les arêtes G3 ont les champs 'a' et 'b' (cell_id source et destination).
    """
    for edge in world.adjacency:
        a_id = edge["a"]
        b_id = edge["b"]
        if a_id not in world.cells or b_id not in world.cells:
            continue
        cell_a = world.cells[a_id]
        cell_b = world.cells[b_id]

        # Direction a→b si b est en déficit et a a du surplus
        if cell_b.food_deficit_kg > 0 and cell_a.food_stock_kg > 0:
            transfer = min(
                cell_a.food_stock_kg,
                cell_b.food_deficit_kg,
                TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
            )
            cell_a.food_stock_kg -= transfer
            cell_b.food_stock_kg += transfer
            cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)
            total_transported[0] += transfer

        # Direction b→a si a est en déficit et b a du surplus
        elif cell_a.food_deficit_kg > 0 and cell_b.food_stock_kg > 0:
            transfer = min(
                cell_b.food_stock_kg,
                cell_a.food_deficit_kg,
                TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
            )
            cell_b.food_stock_kg -= transfer
            cell_a.food_stock_kg += transfer
            cell_a.food_deficit_kg = max(0.0, cell_a.food_deficit_kg - transfer)
            total_transported[0] += transfer


def _update_hunger(cell: Cell) -> None:
    """
    Maillon 4 — Faim.
    Si food_stock_kg ≤ 0 (après le commerce), incrémente hunger_ticks.
    Sinon, remet hunger_ticks à 0 (la cellule est rassasiée).
    Traite la sentinelle -1 comme hunger_ticks = 0.
    """
    if cell.food_stock_kg <= 0.0:
        prev = cell.hunger_ticks if cell.hunger_ticks >= 0 else 0
        cell.hunger_ticks = prev + 1
    else:
        cell.hunger_ticks = 0


def _apply_mortality(cell: Cell) -> None:
    """
    Maillon 5 — Mortalité (brief 012, SC3).

    La mortalité est proportionnelle au déficit alimentaire cumulé par habitant
    (food_deficit_kg / population). Plus le déficit par tête est élevé,
    plus le taux de mortalité est élevé, plafonné à MAX_DEATH_RATE_PER_TICK.

    Formule :
        per_capita_deficit = food_deficit_kg / population
        death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
        deaths = max(1, int(population × death_rate))

    Aucun interrupteur binaire seul (SC3) : la mortalité est une fonction
    croissante et continue de l'ampleur du déficit accumulé.
    """
    if cell.food_deficit_kg > 0 and cell.population > 0:
        per_capita_deficit = cell.food_deficit_kg / cell.population
        death_rate = per_capita_deficit * HUNGER_DEATH_SCALE
        death_rate = min(death_rate, MAX_DEATH_RATE_PER_TICK)
        deaths = max(1, int(cell.population * death_rate))
        cell.population = max(0, cell.population - deaths)


def tick(world, rng: random.Random) -> float:
    """
    Avance le monde d'un pas de temps.

    rng : instance de random.Random initialisée par l'appelant —
          jamais d'aléa global non contrôlé.

    Retourne la quantité totale de nourriture transportée par le commerce
    pendant ce tick (kg).
    """
    total_transported = [0.0]

    for cell in world.cells.values():
        _apply_production(cell, rng)
        _apply_consumption(cell)

    _apply_commerce(world, total_transported)

    for cell in world.cells.values():
        _update_hunger(cell)
        _apply_mortality(cell)

    return total_transported[0]
