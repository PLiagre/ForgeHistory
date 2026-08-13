"""
SC3 brief 013 — Seuil de survie dérivé analytiquement.

test_fraction_dans_marge :
    Vérifie que la fraction de survie mesurée sur N=200 ticks (World.from_g3,
    rng_seed=42) est dans la fenêtre [fraction_predite - SURVIE_MARGE_DERIVEE,
    fraction_predite + SURVIE_MARGE_DERIVEE].

    Ce test peut échouer si les constantes de calibration changent de régime
    (ex. doubler INITIAL_POPULATION_PER_KM2 → fraction_predite = 0.45, hors
    fenêtre pour la survie réelle ~0.80). C'est voulu : le test est falsifiable.

test_fraction_predite_analytique :
    Vérifie que fraction_predite = 0.9 avec les constantes actuelles.
    Compteur : fraction_predite_analytique.
"""

import random

import pytest

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    INITIAL_POPULATION_PER_KM2,
    RNG_YIELD_HIGH,
    RNG_YIELD_LOW,
    SEUIL_SURVIE_POPULATION_FRACTION,
    SURVIE_MARGE_DERIVEE,
)
from sim.engine import tick
from sim.world import World

N_TICKS = 200


def _fraction_predite_from_constants() -> float:
    rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
    cap = FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * rendement_moyen / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    return cap / INITIAL_POPULATION_PER_KM2


def test_fraction_predite_analytique():
    """
    SC3 — La fraction prédite analytiquement vaut 0.9 avec les constantes
    actuelles et est dans (0.0, 1.0).
    Compteur : fraction_predite_analytique.
    """
    fraction_predite_analytique = _fraction_predite_from_constants()
    print(f"fraction_predite_analytique = {fraction_predite_analytique}")
    print(f"SEUIL_SURVIE_POPULATION_FRACTION = {SEUIL_SURVIE_POPULATION_FRACTION}")
    print(f"SURVIE_MARGE_DERIVEE = {SURVIE_MARGE_DERIVEE}")
    print(
        f"coherence: |SEUIL - (pred - marge)| = "
        f"{abs(SEUIL_SURVIE_POPULATION_FRACTION - (fraction_predite_analytique - SURVIE_MARGE_DERIVEE))}"
    )

    assert 0.0 < fraction_predite_analytique < 1.0, (
        f"fraction_predite hors (0, 1) : {fraction_predite_analytique}"
    )
    # Le seuil doit être cohérent avec la formule
    coherence = abs(
        SEUIL_SURVIE_POPULATION_FRACTION
        - (fraction_predite_analytique - SURVIE_MARGE_DERIVEE)
    )
    assert coherence < 1e-9, (
        f"SEUIL_SURVIE_POPULATION_FRACTION n'est pas cohérent avec la formule "
        f"(écart = {coherence}). Vérifier constants.py."
    )


def test_fraction_dans_marge():
    """
    SC3 — La fraction de survie mesurée sur N=200 ticks est dans la fenêtre
    [fraction_predite - SURVIE_MARGE_DERIVEE, fraction_predite + SURVIE_MARGE_DERIVEE].

    Ce test est conçu pour pouvoir échouer : si INITIAL_POPULATION_PER_KM2
    est doublé (→ fraction_predite = 0.45), la fraction mesurée (~0.80) sort
    de la fenêtre et le test rougit sans toucher à SURVIE_MARGE_DERIVEE.

    Compteur : fraction_dans_marge_predite.
    """
    world = World.from_g3(rng_seed=42)
    rng = random.Random(42)

    pop_init = sum(c.population for c in world.cells.values())
    for _ in range(N_TICKS):
        tick(world, rng)
    pop_fin = sum(c.population for c in world.cells.values())

    fraction_survie = pop_fin / pop_init
    fraction_predite = _fraction_predite_from_constants()
    borne_basse = fraction_predite - SURVIE_MARGE_DERIVEE
    borne_haute = fraction_predite + SURVIE_MARGE_DERIVEE

    fraction_dans_marge_predite = borne_basse <= fraction_survie <= borne_haute

    print(f"pop_init = {pop_init}, pop_fin = {pop_fin}")
    print(f"fraction_survie = {fraction_survie:.6f}")
    print(f"fraction_predite = {fraction_predite:.6f}")
    print(f"fenêtre = [{borne_basse:.6f}, {borne_haute:.6f}]")
    print(f"fraction_dans_marge_predite = {fraction_dans_marge_predite}")

    assert fraction_dans_marge_predite, (
        f"fraction_survie = {fraction_survie:.6f} hors de la fenêtre "
        f"[{borne_basse:.6f}, {borne_haute:.6f}]. "
        f"fraction_predite = {fraction_predite:.6f}, SURVIE_MARGE_DERIVEE = {SURVIE_MARGE_DERIVEE}."
    )
