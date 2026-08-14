"""
SC4 brief 013 — Mortalité continue et plafonnée ; déficit à mémoire graduelle.

test_plafond_toute_population :
    Pour chaque population dans [1, 5, 9, 20, 100, 1000] et un déficit
    minuscule (1e-9 kg), le taux effectif deaths/population est ≤ MAX_DEATH_RATE_PER_TICK.
    Compteur : max_taux_mortalite_effectif_pop_1.

test_deficit_non_efface_en_1_tick :
    Une cellule avec food_deficit_kg = 10 000 kg et un tick de surplus INFÉRIEUR
    à sa dette conserve un déficit résiduel > 0.
    Compteur : deficit_non_efface_en_1_tick.

ADAPTATION brief 017 (SC5) — motivation :
    La version brief 013 de ce test encodait la formule
    `D × (1 - DEFICIT_RECOVERY_RATE_PER_TICK)`, c'est-à-dire une récupération
    indépendante du surplus réel. Le brief 017 supprime cette formule : la
    dette ne se rembourse qu'avec des kilogrammes effectivement consommés en
    sus du besoin d'entretien. Le test conserve sa PROPRIÉTÉ (un tick de
    surplus n'efface pas une dette accumulée) mais l'adosse désormais à la
    physique : le surplus du tick est plus petit que la dette, donc la dette
    survit. La valeur attendue devient `D - surplus × ratio` au lieu de
    `D × (1 - r)`. Le test reste falsifiable : si la réduction redevenait
    indépendante du surplus, la valeur exacte ne correspondrait plus.
"""

from sim.constants import (
    DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG,
    MAX_DEATH_RATE_PER_TICK,
)
from sim.engine import _apply_consumption, _apply_mortality
from sim.model import Cell

POPULATIONS_TEST = [1, 5, 9, 20, 100, 1000]
DEFICIT_MINUSCULE_KG = 1e-9


def test_plafond_toute_population():
    """
    SC4 — Pour toute population dans POPULATIONS_TEST avec un déficit minuscule,
    le taux effectif de mortalité est ≤ MAX_DEATH_RATE_PER_TICK.

    Sans le plancher max(1, …), deaths = int(pop × rate) = 0 pour un déficit
    de 1e-9 kg quelle que soit la population : le taux effectif = 0.0 ≤ 0.10. ✓

    Avant la correction (avec max(1, …)) : pour pop=1, deaths=1, taux=1.0 > 0.10. ✗

    Compteur : max_taux_mortalite_effectif_pop_1.
    """
    max_taux_observe = -1.0  # sentinel (hard-won rule 8)

    for pop in POPULATIONS_TEST:
        cell = Cell(
            cell_id=pop,
            area_km2=1.0,
            population=pop,
            food_stock_kg=0.0,
            hunger_ticks=0,
            food_deficit_kg=DEFICIT_MINUSCULE_KG,
        )
        pop_avant = cell.population
        _apply_mortality(cell)
        deaths = pop_avant - cell.population

        taux_effectif = deaths / pop_avant
        print(
            f"pop={pop_avant}: deaths={deaths}, "
            f"taux_effectif={taux_effectif:.6g} (max={MAX_DEATH_RATE_PER_TICK})"
        )

        if taux_effectif > max_taux_observe:
            max_taux_observe = taux_effectif

        assert taux_effectif <= MAX_DEATH_RATE_PER_TICK, (
            f"Plafond dépassé pour pop={pop_avant} : taux={taux_effectif} > {MAX_DEATH_RATE_PER_TICK}. "
            "Le plancher max(1, …) réintroduit-il le dépassement ?"
        )

    max_taux_mortalite_effectif_pop_1 = max_taux_observe
    print(f"max_taux_mortalite_effectif_pop_1 = {max_taux_mortalite_effectif_pop_1}")


def test_deficit_non_efface_en_1_tick():
    """
    SC4 brief 013, adapté SC5 brief 017 — Un tick de surplus plus petit que la
    dette ne peut pas effacer cette dette.

    Construit une cellule avec food_deficit_kg = 10 000 kg et un stock qui
    couvre sa consommation du tick plus un surplus de 200 kg (2 × la
    consommation du tick, très inférieur à la dette). Après _apply_consumption,
    le déficit résiduel doit être > 0 et valoir exactement
    D - surplus × DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG.

    Compteur : deficit_non_efface_en_1_tick.
    """
    D = 10_000.0  # déficit accumulé initial (kg)
    pop = 100
    # Stock = 2 × consommation du tick → surplus = 1 × consommation = 200 kg
    from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    besoin_tick = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    stock_suffisant = besoin_tick * 2
    surplus = stock_suffisant - besoin_tick

    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=pop,
        food_stock_kg=stock_suffisant,
        hunger_ticks=0,
        food_deficit_kg=D,
        mortality_remainder=0.0,
    )

    _apply_consumption(cell)

    deficit_residuel = cell.food_deficit_kg
    deficit_attendu = D - surplus * DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG
    deficit_non_efface_en_1_tick = deficit_residuel > 0

    print(f"D_initial = {D}, surplus_du_tick = {surplus}")
    print(f"deficit_non_efface_en_1_tick = {deficit_residuel}")
    print(f"deficit_attendu = {deficit_attendu}")
    print(
        "DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG = "
        f"{DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG}"
    )

    assert deficit_non_efface_en_1_tick, (
        f"Le déficit a été entièrement effacé en un seul tick de surplus : "
        f"D={D}, surplus={surplus}, résiduel={deficit_residuel}."
    )

    # Vérifier la valeur exacte (formule déterministe, bornée par le surplus)
    assert abs(deficit_residuel - deficit_attendu) < 1e-9, (
        f"Valeur de récupération incorrecte : attendu={deficit_attendu}, "
        f"obtenu={deficit_residuel}."
    )
