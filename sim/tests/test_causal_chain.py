"""
SC7 — Chaîne causale testée maillon par maillon et de bout en bout.

SC7a : production insuffisante → food_stock_kg baisse
SC7b : food_stock_kg ≤ 0 → hunger_ticks progresse
SC7c : food_deficit_kg > 0 → population diminue (brief 012 : mortalité
       proportionnelle au déficit, pas interrupteur binaire seul)
SC7d : intégration bout en bout — cellule à rendement nul (area_km2 = 0),
       population diminue.
       NOTE (SC5 brief 012) : area_km2 = 0.0 est un cas structurellement
       inatteignable dans les données G3 (minimum réel : 1.444877 km²).
       Ce test est conservé uniquement pour tester la limite de la fonction ;
       il ne peut pas servir de preuve pour un compteur SC5.

Compteur : maillons_chaine_causale_testes_unitairement (SC7a + SC7b + SC7c = 3).
"""

import random

import pytest

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
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
    SC7a : une cellule avec production insuffisante voit son stock baisser
    après production+consommation.
    État initial construit à la main. Un seul maillon testé (production + consommation).
    area_km2 = 1.0 (≥ minimum G3 = 1.444877 km²), conforme au plancher SC5 brief 012.
    Production max = 1.0 × 18 × 1.5 = 27 kg << consommation = 5000 × 2 = 10 000 kg.
    """
    # Production max = 1.0 × 18 × 1.5 = 27 kg/tick (très faible)
    # Consommation = 5000 × 2.0 = 10 000 kg/tick
    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=5000,
        food_stock_kg=1000.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    stock_before = cell.food_stock_kg

    rng = random.Random(42)
    _apply_production(cell, rng)
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
    area_km2 = 1.0 (≥ minimum G3 = 1.444877 km²), conforme au plancher SC5 brief 012.
    La superficie n'est pas lue par _update_hunger, mais le test respecte le plancher.
    """
    cell = Cell(
        cell_id=2,
        area_km2=1.0,
        population=10,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    hunger_before = cell.hunger_ticks

    _update_hunger(cell)

    hunger_after = cell.hunger_ticks
    print(f"hunger_before = {hunger_before}, hunger_after = {hunger_after}")

    assert hunger_after >= hunger_before + 1, (
        f"hunger_ticks aurait dû progresser : avant={hunger_before}, après={hunger_after}"
    )


# ---------------------------------------------------------------------------
# SC7c — Maillon 5 : food_deficit_kg > 0 → population diminue
# (brief 012 : mortalité proportionnelle au déficit accumulé, pas interrupteur
# binaire seul — SC3)
# ---------------------------------------------------------------------------

def test_sc7c_population_decreases_when_deficit_positive():
    """
    SC7c (brief 012) : une cellule avec food_deficit_kg > 0 voit
    sa population diminuer après un appel à _apply_mortality.
    La mortalité est proportionnelle à food_deficit_kg / population
    (fonction croissante continue du déficit — SC3 brief 012).
    État initial construit à la main.
    """
    # food_deficit_kg = 500 kg, population = 100, area_km2 = 1.0
    # per_capita_deficit = 5 kg → death_rate = 5 × 0.005 = 0.025 (2.5%)
    # deaths = max(1, int(100 × 0.025)) = max(1, 2) = 2
    cell = Cell(
        cell_id=3,
        area_km2=1.0,
        population=100,
        food_stock_kg=0.0,
        hunger_ticks=5,
        food_deficit_kg=500.0,
    )
    population_before = cell.population

    _apply_mortality(cell)

    population_after = cell.population
    print(
        f"population_before = {population_before}, population_after = {population_after}, "
        f"food_deficit_kg = {cell.food_deficit_kg}"
    )

    assert population_after < population_before, (
        f"La population aurait dû diminuer : avant={population_before}, après={population_after}"
    )


# ---------------------------------------------------------------------------
# SC7d — Intégration bout en bout
# NOTE : area_km2 = 0.0 est un cas hors données G3 (min réel = 1.444877 km²),
# conservé uniquement pour tester la limite de la fonction (SC5 brief 012).
# ---------------------------------------------------------------------------

def test_sc7d_zero_yield_leads_to_population_decline():
    """
    SC7d : une cellule avec rendement = 0 (area_km2 = 0) et population
    initiale > 0 finit par avoir une population strictement inférieure
    après suffisamment de ticks.

    CAS HORS DONNÉES G3 : area_km2 = 0.0 n'existe pas dans les données réelles
    (minimum G3 = 1.444877 km²). Ce test est conservé uniquement pour tester
    la limite de la fonction — pas utilisable pour les compteurs SC5.

    Le résultat émerge des états intermédiaires (production → stock →
    déficit → mortalité) via tick() complet — pas d'appel direct à la règle
    de mort.
    """
    # Construire un monde minimal avec une seule cellule
    cell = Cell(
        cell_id=10,
        area_km2=0.0,     # rendement = 0 → pas de production alimentaire
        population=200,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    world = World(cells={10: cell}, adjacency=[])
    rng = random.Random(0)

    population_initiale = cell.population

    # N ticks suffisent pour voir des morts : dès le tick 1, food_deficit = 400 kg
    # per_capita = 2 kg → death_rate = 0.01 → 2 morts/tick
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
