"""
Moteur de simulation : boucle de tick.

tick(world, rng) fait avancer l'état du monde d'un pas de temps.
Chaque maillon de la chaîne causale est une fonction séparée et testable :
    _apply_production  → écrit food_stock_kg (production)
    _apply_consumption → lit et modifie food_stock_kg (consommation)
    _update_hunger     → lit food_stock_kg, écrit hunger_ticks (faim)
    _apply_mortality   → lit hunger_ticks, écrit population (mort)

Règle SC9 : aucun littéral numérique non nommé dans les fonctions de calcul.
Toutes les constantes paramétriques sont dans sim/constants.py.
"""

import random

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    HUNGER_DEATH_RATE_PER_TICK,
    HUNGER_DEATH_THRESHOLD,
)
from sim.model import Cell


def _apply_production(cell: Cell) -> None:
    """
    Maillon 1 — Production.
    Calcule la nourriture produite et l'écrit dans food_stock_kg.
    Traite la sentinelle -1 comme un stock initial nul.
    """
    food_produced = cell.area_km2 * FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
    current = cell.food_stock_kg if cell.food_stock_kg >= 0 else 0.0
    cell.food_stock_kg = current + food_produced


def _apply_consumption(cell: Cell) -> None:
    """
    Maillon 2 — Consommation.
    Lit food_stock_kg, soustrait la consommation, ré-écrit le champ.
    Le stock ne peut pas descendre en dessous de 0.
    """
    food_consumed = cell.population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    remaining = cell.food_stock_kg - food_consumed
    cell.food_stock_kg = remaining if remaining >= 0.0 else 0.0


def _update_hunger(cell: Cell) -> None:
    """
    Maillon 3 — Faim.
    Si food_stock_kg ≤ 0, incrémente hunger_ticks.
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
    Maillon 4 — Mortalité.
    Si hunger_ticks ≥ HUNGER_DEATH_THRESHOLD, réduit la population.
    Le minimum de décès est 1 (arrondi vers le bas sinon des populations
    entières resteraient indéfiniment sans diminuer).
    """
    if cell.hunger_ticks >= HUNGER_DEATH_THRESHOLD:
        deaths = cell.population * HUNGER_DEATH_RATE_PER_TICK
        deaths_int = max(1, int(deaths))
        cell.population = max(0, cell.population - deaths_int)


def tick(world, rng: random.Random) -> None:
    """
    Avance le monde d'un pas de temps.

    rng : instance de random.Random initialisée par l'appelant —
          jamais d'aléa global non contrôlé.
    """
    for cell in world.cells.values():
        _apply_production(cell)
        _apply_consumption(cell)
        _update_hunger(cell)
        _apply_mortality(cell)
