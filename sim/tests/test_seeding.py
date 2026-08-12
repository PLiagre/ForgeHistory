"""
SC4 — Amorçage déterministe.

Deux appels à World.from_g3() avec la même graine produisent
des populations initiales byte-identiques.
Compteur : amorçage_deterministe_valide.
"""

from sim.world import World


def test_seeding_determinisme():
    """
    SC4 : deux runs avec la même graine rng_seed = 42 donnent
    des populations identiques sur toutes les cellules.
    """
    w1 = World.from_g3(rng_seed=42)
    w2 = World.from_g3(rng_seed=42)

    assert set(w1.cells.keys()) == set(w2.cells.keys())

    mismatches = [
        cid
        for cid in w1.cells
        if w1.cells[cid].population != w2.cells[cid].population
        or w1.cells[cid].food_stock_kg != w2.cells[cid].food_stock_kg
    ]

    amorçage_deterministe_valide = 0 if mismatches else 1
    print(f"amorçage_deterministe_valide = {amorçage_deterministe_valide}")
    print(f"cellules divergentes = {len(mismatches)}")

    assert amorçage_deterministe_valide == 1, (
        f"Amorçage non déterministe : {len(mismatches)} cellule(s) divergentes."
    )


def test_different_seeds_give_different_populations():
    """
    Contrôle de sanité : deux graines différentes donnent (en général)
    des populations différentes — prouve que rng_seed a un effet.
    """
    w_a = World.from_g3(rng_seed=0)
    w_b = World.from_g3(rng_seed=99)
    pops_a = [c.population for c in w_a.cells.values()]
    pops_b = [c.population for c in w_b.cells.values()]
    # La probabilité que toutes les populations soient identiques avec des
    # graines différentes est astronomiquement faible.
    assert pops_a != pops_b, "Deux graines différentes ont produit des populations identiques."
