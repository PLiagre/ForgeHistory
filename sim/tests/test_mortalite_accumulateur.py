"""
SC3 brief 017 — Accumulateur de mortalité fractionnaire.

`int(population × death_rate)` arrondit à zéro dès que
`population × death_rate < 1`. Une cellule de 5 habitants en famine totale
produit 0.5 mort par tick : jetée à la virgule, tick après tick, elle rendait
les petites cellules immortelles. Le champ `Cell.mortality_remainder` reporte
la fraction non appliquée au tick suivant.

Compteurs : famine_tue_cellule_5hab, mortalite_precision_n_ticks.
"""

from sim.constants import (
    HUNGER_DEATH_SCALE,
    MAX_DEATH_RATE_PER_TICK,
    N_BOUND_MORT,
    N_STAT_SURVIE,
)
from sim.engine import _apply_mortality
from sim.model import Cell

# Cellule minuscule : c'est la taille pour laquelle la troncature était fatale
# (au sens inverse : elle rendait la cellule immortelle).
POPULATION_PETITE_CELLULE = 5

# Micro-monde de précision : trois cellules, populations distinctes ≥ 50.
POPULATIONS_MICRO_MONDE = [50, 137, 500]

# Déficit par tête du micro-monde (kg) : assez petit pour que la mortalité par
# tick reste très en dessous d'un habitant sur la plus petite cellule — c'est
# exactement le régime où la troncature jetait tout.
DEFICIT_PAR_TETE_MICRO_MONDE_KG = 0.5


def test_champ_mortality_remainder_est_sentinelle():
    """
    Le champ `mortality_remainder` existe et vaut -1.0 par défaut : sentinelle
    « non calculé » (hard-won rule 8 — un zéro peut être une mesure réelle).
    """
    cell = Cell(cell_id=1, area_km2=1.0, population=10)
    print(f"mortality_remainder par défaut = {cell.mortality_remainder}")
    assert cell.mortality_remainder == -1.0


def test_famine_tue_en_borne_de_ticks():
    """
    SC3 — Une cellule de 5 habitants en famine totale perd au moins un
    habitant en au plus N_BOUND_MORT ticks.

    Borne analytique (dérivée, documentée dans sim/SEEDING.md SC3 brief 017) :
    au plafond de mortalité, `raw` augmente de
    `population × MAX_DEATH_RATE_PER_TICK` par tick, soit une mort entière en
    au plus `ceil(1 / MAX_DEATH_RATE_PER_TICK)` ticks quelle que soit la
    taille de la cellule. Avec les constantes actuelles : N_BOUND_MORT = 10.

    Le déficit est pris à `population / HUNGER_DEATH_SCALE` kg, ce qui garantit
    d'atteindre le plafond : per_capita × HDS = 1/HDS × HDS = 1 ≥
    MAX_DEATH_RATE_PER_TICK.

    Compteur : famine_tue_cellule_5hab.
    """
    deficit_kg = POPULATION_PETITE_CELLULE / HUNGER_DEATH_SCALE
    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=POPULATION_PETITE_CELLULE,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=deficit_kg,
        mortality_remainder=0.0,
    )

    tick_du_premier_mort = -1  # sentinelle : aucune mort observée
    for numero_tick in range(1, N_BOUND_MORT + 1):
        population_avant = cell.population
        _apply_mortality(cell)
        if cell.population < population_avant and tick_du_premier_mort < 0:
            tick_du_premier_mort = numero_tick
            break

    morts = POPULATION_PETITE_CELLULE - cell.population
    famine_tue_cellule_5hab = morts
    print(f"N_BOUND_MORT = {N_BOUND_MORT} (= ceil(1 / {MAX_DEATH_RATE_PER_TICK}))")
    print(f"deficit_kg = {deficit_kg}")
    print(f"tick_du_premier_mort = {tick_du_premier_mort}")
    print(f"famine_tue_cellule_5hab = {famine_tue_cellule_5hab}")

    assert famine_tue_cellule_5hab >= 1, (
        f"Cellule de {POPULATION_PETITE_CELLULE} habitants en famine totale "
        f"toujours intacte après {N_BOUND_MORT} ticks : la fraction de mort "
        "est encore jetée par int()."
    )
    assert 0 < tick_du_premier_mort <= N_BOUND_MORT


def test_precision_mortalite_sur_n_ticks():
    """
    SC3 — Sur N_STAT_SURVIE ticks d'un micro-monde déterministe à trois
    cellules (populations ≥ 50, déficit par tête constant non nul), l'écart
    entre les morts réellement appliqués et la somme exacte
    `population × death_rate` accumulée tick par tick est ≤ 1 par cellule.

    L'écart maximal possible est la fraction en attente (< 1) : c'est
    précisément ce que le report garantit.

    Compteur : mortalite_precision_n_ticks.
    """
    ecarts = []

    for population_initiale in POPULATIONS_MICRO_MONDE:
        deficit_kg = population_initiale * DEFICIT_PAR_TETE_MICRO_MONDE_KG
        cell = Cell(
            cell_id=population_initiale,
            area_km2=1.0,
            population=population_initiale,
            food_stock_kg=0.0,
            hunger_ticks=0,
            food_deficit_kg=deficit_kg,
            mortality_remainder=0.0,
        )

        somme_exacte = 0.0
        morts_appliques = 0

        for _ in range(N_STAT_SURVIE):
            if cell.population <= 0:
                break
            # Taux du tick, recalculé comme le fait le moteur.
            per_capita = cell.food_deficit_kg / cell.population
            taux = min(per_capita * HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
            somme_exacte += cell.population * taux

            population_avant = cell.population
            _apply_mortality(cell)
            morts_appliques += population_avant - cell.population
            # Déficit maintenu constant : le maillon consommation n'est pas
            # joué ici, seul l'accumulateur de mortalité est sous test.
            cell.food_deficit_kg = deficit_kg

        ecart = abs(morts_appliques - somme_exacte)
        ecarts.append(ecart)
        print(
            f"pop_initiale={population_initiale}: morts_appliques={morts_appliques}, "
            f"somme_exacte={somme_exacte:.6f}, ecart={ecart:.6f}, "
            f"remainder_final={cell.mortality_remainder:.6f}"
        )

    mortalite_precision_n_ticks = max(ecarts)
    print(f"cellules = {len(POPULATIONS_MICRO_MONDE)}, ticks = {N_STAT_SURVIE}")
    print(f"mortalite_precision_n_ticks = {mortalite_precision_n_ticks:.6f}")

    assert mortalite_precision_n_ticks <= 1.0, (
        f"Écart maximal {mortalite_precision_n_ticks:.6f} > 1 mort : des morts "
        "fractionnaires sont encore perdues."
    )
