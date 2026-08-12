"""
SC7 — Chaîne causale testée maillon par maillon et de bout en bout.

SC7a : production insuffisante → food_stock_kg baisse
SC7b : food_stock_kg ≤ 0 → hunger_ticks progresse
SC7c : hunger_ticks ≥ seuil → population diminue
SC7d : intégration bout en bout — cellule à rendement nul, population diminue

Compteur : maillons_chaine_causale_testes_unitairement (SC7a + SC7b + SC7c = 3).
"""

import random

import pytest

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    HUNGER_DEATH_THRESHOLD,
)
from sim.engine import (
    _apply_consumption,
    _apply_mortality,
    _apply_production,
    _update_hunger,
    tick,
)
from sim.model import Cell
from sim.world import World


# ---------------------------------------------------------------------------
# SC7a — Maillon 1 : production < consommation → stock baisse
# ---------------------------------------------------------------------------

def test_sc7a_stock_decreases_when_production_lt_consumption():
    """
    SC7a : une cellule avec area_km2 très faible (production quasi-nulle)
    et une grande population voit son stock baisser après production+consommation.
    État initial construit à la main. Un seul maillon testé (production + consommation).
    """
    # Production = 0.001 * 50 = 0.05 kg/tick (très faible)
    # Consommation = 100 * 2.0 = 200 kg/tick
    cell = Cell(
        cell_id=1,
        area_km2=0.001,
        population=100,
        food_stock_kg=1000.0,
        hunger_ticks=0,
    )
    stock_before = cell.food_stock_kg

    _apply_production(cell)
    _apply_consumption(cell)

    stock_after = cell.food_stock_kg
    print(f"stock_before = {stock_before}, stock_after = {stock_after}")

    assert stock_after < stock_before, (
        f"Le stock aurait dû baisser : avant={stock_before}, après={stock_after}"
    )


# ---------------------------------------------------------------------------
# SC7b — Maillon 3 : food_stock_kg ≤ 0 → hunger_ticks progresse
# ---------------------------------------------------------------------------

def test_sc7b_hunger_ticks_increments_when_stock_empty():
    """
    SC7b : une cellule avec stock = 0 voit hunger_ticks augmenter d'au moins 1.
    État initial construit à la main. Un seul maillon testé (_update_hunger).
    """
    cell = Cell(
        cell_id=2,
        area_km2=0.0,
        population=10,
        food_stock_kg=0.0,
        hunger_ticks=0,
    )
    hunger_before = cell.hunger_ticks

    _update_hunger(cell)

    hunger_after = cell.hunger_ticks
    print(f"hunger_before = {hunger_before}, hunger_after = {hunger_after}")

    assert hunger_after >= hunger_before + 1, (
        f"hunger_ticks aurait dû progresser : avant={hunger_before}, après={hunger_after}"
    )


# ---------------------------------------------------------------------------
# SC7c — Maillon 4 : hunger_ticks ≥ seuil → population diminue
# ---------------------------------------------------------------------------

def test_sc7c_population_decreases_when_hunger_above_threshold():
    """
    SC7c : une cellule avec hunger_ticks = HUNGER_DEATH_THRESHOLD voit
    sa population diminuer après un appel à _apply_mortality.
    Le seuil est lu depuis HUNGER_DEATH_THRESHOLD (jamais codé en dur).
    État initial construit à la main. Un seul maillon testé (_apply_mortality).
    """
    cell = Cell(
        cell_id=3,
        area_km2=0.0,
        population=100,
        food_stock_kg=0.0,
        hunger_ticks=HUNGER_DEATH_THRESHOLD,
    )
    population_before = cell.population

    _apply_mortality(cell)

    population_after = cell.population
    print(
        f"population_before = {population_before}, population_after = {population_after}, "
        f"HUNGER_DEATH_THRESHOLD = {HUNGER_DEATH_THRESHOLD}"
    )

    assert population_after < population_before, (
        f"La population aurait dû diminuer : avant={population_before}, après={population_after}"
    )


# ---------------------------------------------------------------------------
# SC7d — Intégration bout en bout
# ---------------------------------------------------------------------------

def test_sc7d_zero_yield_leads_to_population_decline():
    """
    SC7d : une cellule avec rendement = 0 (area_km2 = 0) et population
    initiale > 0 finit par avoir une population strictement inférieure
    après suffisamment de ticks.

    Le résultat émerge des états intermédiaires (production → stock →
    faim → mortalité) via tick() complet — pas d'appel direct à la règle
    de mort.
    """
    # Construire un monde minimal avec une seule cellule
    cell = Cell(
        cell_id=10,
        area_km2=0.0,   # rendement = 0 → pas de production alimentaire
        population=200,
        food_stock_kg=0.0,
        hunger_ticks=0,
    )
    world = World(cells={10: cell}, adjacency=[])
    rng = random.Random(0)

    population_initiale = cell.population

    # N ticks doit suffire pour dépasser HUNGER_DEATH_THRESHOLD et voir des morts
    # Avec threshold=3 et rate=5%, après ~5 ticks la population diminue.
    N = 20
    for _ in range(N):
        tick(world, rng)

    population_finale = world.cells[10].population
    print(f"population_initiale = {population_initiale}")
    print(f"population_finale = {population_finale}")
    print(f"test_integration_bout_en_bout_resultat = {'PASS' if population_finale < population_initiale else 'FAIL'}")

    assert population_finale < population_initiale, (
        f"La population n'a pas diminué après {N} ticks sans nourriture : "
        f"initiale={population_initiale}, finale={population_finale}"
    )
