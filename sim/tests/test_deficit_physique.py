"""
SC5 brief 017 — La dette alimentaire ne se rembourse qu'en kilogrammes réels.

L'ancienne formule `food_deficit_kg × (1 − DEFICIT_RECOVERY_RATE_PER_TICK)`
effaçait 10 % de la dette quel que soit le surplus : un surplus d'un
nanogramme effaçait 1 000 kg d'une dette de 10 000 kg. Des kilogrammes
disparaissaient sans contrepartie physique (principe 3 : rien ne se téléporte).

Compteurs : deficit_reduction_infinitesimal, deficit_reduction_proportionnel.
"""

from sim.constants import (
    DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG,
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
)
from sim.engine import _apply_consumption
from sim.model import Cell

DETTE_INITIALE_KG = 10_000.0
POPULATION_TEST = 10
SURPLUS_INFINITESIMAL_KG = 1e-9
SURPLUS_SUBSTANTIEL_KG = 5_000.0


def _cellule_endettee(surplus_kg: float) -> Cell:
    """Cellule avec une dette de 10 000 kg et le surplus demandé ce tick."""
    besoin = POPULATION_TEST * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    return Cell(
        cell_id=1,
        area_km2=0.0,
        population=POPULATION_TEST,
        food_stock_kg=besoin + surplus_kg,
        hunger_ticks=0,
        food_deficit_kg=DETTE_INITIALE_KG,
        mortality_remainder=0.0,
    )


def test_deficit_reduction_infinitesimal():
    """
    SC5 — Un surplus de 1e-9 kg ne peut rembourser que 1e-9 kg de dette.

    Compteur : deficit_reduction_infinitesimal.
    """
    cell = _cellule_endettee(SURPLUS_INFINITESIMAL_KG)
    _apply_consumption(cell)

    deficit_reduction_infinitesimal = DETTE_INITIALE_KG - cell.food_deficit_kg
    print(f"surplus = {SURPLUS_INFINITESIMAL_KG} kg")
    print(f"deficit_apres = {cell.food_deficit_kg}")
    print(f"deficit_reduction_infinitesimal = {deficit_reduction_infinitesimal}")
    print(f"stock_apres = {cell.food_stock_kg}")

    assert cell.food_deficit_kg > 9999.9, (
        f"Un surplus de {SURPLUS_INFINITESIMAL_KG} kg a effacé "
        f"{deficit_reduction_infinitesimal} kg de dette."
    )
    assert deficit_reduction_infinitesimal <= SURPLUS_INFINITESIMAL_KG + 1e-12


def test_deficit_reduction_proportionnel():
    """
    SC5 — Un surplus de 5 000 kg rembourse 5 000 kg de dette (ratio nominal
    1 kg de dette par kg de surplus), et ces kilogrammes quittent le stock.

    Compteur : deficit_reduction_proportionnel.
    """
    cell = _cellule_endettee(SURPLUS_SUBSTANTIEL_KG)
    _apply_consumption(cell)

    deficit_reduction_proportionnel = DETTE_INITIALE_KG - cell.food_deficit_kg
    attendu = SURPLUS_SUBSTANTIEL_KG * DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG

    print(f"surplus = {SURPLUS_SUBSTANTIEL_KG} kg")
    print(f"ratio = {DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG} kg de dette par kg de surplus")
    print(f"deficit_reduction_proportionnel = {deficit_reduction_proportionnel}")
    print(f"attendu = {attendu}")
    print(f"stock_apres = {cell.food_stock_kg}")

    assert abs(deficit_reduction_proportionnel - attendu) < 1e-9
    assert abs(cell.food_stock_kg - (SURPLUS_SUBSTANTIEL_KG - attendu)) < 1e-9, (
        "Les kilogrammes remboursés doivent quitter le stock : ils sont mangés "
        "en sus du besoin d'entretien."
    )


def test_invariant_physique_reduction_bornee_par_le_surplus():
    """
    SC5 — Invariant : pour tout surplus, la réduction de dette est bornée par
    le surplus du tick. Vérifié sur une gamme de surplus couvrant sept ordres
    de grandeur, y compris un surplus supérieur à la dette.

    Un seul cas de test ne prouverait rien d'une borne (hard-won rule 6).
    """
    surplus_testes = [
        SURPLUS_INFINITESIMAL_KG,
        1e-3,
        1.0,
        SURPLUS_SUBSTANTIEL_KG,
        DETTE_INITIALE_KG,
        DETTE_INITIALE_KG * (1 + 1),
    ]
    reductions = []

    for surplus in surplus_testes:
        cell = _cellule_endettee(surplus)
        _apply_consumption(cell)
        reduction = DETTE_INITIALE_KG - cell.food_deficit_kg
        reductions.append(reduction)
        print(f"surplus={surplus:g} → reduction={reduction:g}, "
              f"stock_apres={cell.food_stock_kg:g}, dette={cell.food_deficit_kg:g}")
        assert reduction <= surplus + 1e-9, (
            f"Réduction {reduction} > surplus {surplus} : des kilogrammes de "
            "dette disparaissent sans contrepartie physique."
        )
        assert cell.food_stock_kg >= 0.0

    print(f"cas_testes = {len(surplus_testes)}")
    assert reductions[0] < reductions[-1], (
        "La réduction doit croître avec le surplus, sinon elle n'en dépend pas."
    )
