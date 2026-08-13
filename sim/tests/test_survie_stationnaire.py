"""
SC1 brief 017 — Le modèle de survie prédit l'état stationnaire.

Remplace `test_survie_derivee.py::test_fraction_dans_marge` comme test de
conformité de la couche F2 (motivation dans le journal du Générateur).

Deux conditions, jamais une seule :

1. **Convergence** : la fraction de survie mesurée à N_STAT_SURVIE ticks et
   celle mesurée à N_STAT_SURVIE ÷ 2 ticks diffèrent d'au plus
   SURVIE_CONVERGENCE_DELTA. Sans cette condition, un critère vert à N = 200
   et rouge à N = 1600 passerait inaperçu (c'est exactement le défaut relevé
   par l'audit du brief 013).
2. **Conformité** : |mesurée − SURVIE_FRACTION_PREDITE_STATIONNAIRE| ≤
   SURVIE_TOLERANCE_STATIONNAIRE, où la prédiction dépend explicitement des
   constantes de mortalité (voir sim/SEEDING.md, SC1 brief 017).

Compteur : fraction_survie_dans_tolerance_stationnaire.
"""

import random

from sim.constants import (
    N_STAT_SURVIE,
    SURVIE_CONVERGENCE_DELTA,
    SURVIE_FRACTION_PREDITE_STATIONNAIRE,
    SURVIE_TOLERANCE_STATIONNAIRE,
)
from sim.engine import tick
from sim.world import World

RNG_SEED = 42


def _fraction_survie_apres(n_ticks: int) -> tuple[float, int, int, int]:
    """
    Exécute n_ticks sur le monde réel G3 et retourne
    (fraction_survie, population_initiale, population_finale, nombre_cellules).
    L'échantillon est toujours le monde chargé — jamais un monde à la main.
    """
    world = World.from_g3(rng_seed=RNG_SEED)
    rng = random.Random(RNG_SEED)

    pop_init = sum(c.population for c in world.cells.values())
    for _ in range(n_ticks):
        tick(world, rng)
    pop_fin = sum(c.population for c in world.cells.values())

    return pop_fin / pop_init, pop_init, pop_fin, len(world.cells)


def test_horizon_est_au_dela_du_transitoire():
    """
    L'horizon N_STAT_SURVIE est un entier ≥ 1000 ticks, dérivé (voir
    sim/SEEDING.md SC1 brief 017 : période d'oscillation du transitoire
    divisée par MAX_DEATH_RATE_PER_TICK, plancher 1000).
    """
    print(f"N_STAT_SURVIE = {N_STAT_SURVIE}")
    assert isinstance(N_STAT_SURVIE, int), "N_STAT_SURVIE doit être un entier"
    assert N_STAT_SURVIE >= 1000, (
        f"Horizon trop court : {N_STAT_SURVIE} < 1000 ticks (brief 017 SC1)"
    )


def test_bornes_des_constantes_du_modele():
    """
    Les constantes du modèle sont dans les intervalles exigés par le brief.
    Ce test rougit si une future modification des constantes de production ou
    de mortalité sort le modèle de son domaine de validité.
    """
    print(
        f"SURVIE_FRACTION_PREDITE_STATIONNAIRE = "
        f"{SURVIE_FRACTION_PREDITE_STATIONNAIRE}"
    )
    print(f"SURVIE_TOLERANCE_STATIONNAIRE = {SURVIE_TOLERANCE_STATIONNAIRE}")
    print(f"SURVIE_CONVERGENCE_DELTA = {SURVIE_CONVERGENCE_DELTA}")

    assert 0.0 < SURVIE_FRACTION_PREDITE_STATIONNAIRE < 1.0
    assert 0.0 < SURVIE_TOLERANCE_STATIONNAIRE < 0.5
    assert 0.0 < SURVIE_CONVERGENCE_DELTA < 0.1


def test_fraction_survie_dans_tolerance_stationnaire():
    """
    SC1 — Convergence puis conformité au modèle prédit.
    Compteur : fraction_survie_dans_tolerance_stationnaire.
    """
    s_stat, pop_init, pop_fin, nb_cellules = _fraction_survie_apres(N_STAT_SURVIE)
    s_demi, _, _, _ = _fraction_survie_apres(N_STAT_SURVIE // 2)

    derive = abs(s_stat - s_demi)
    ecart = abs(s_stat - SURVIE_FRACTION_PREDITE_STATIONNAIRE)
    converge = derive <= SURVIE_CONVERGENCE_DELTA
    dans_tolerance = ecart <= SURVIE_TOLERANCE_STATIONNAIRE

    print(f"cellules = {nb_cellules}")
    print(f"pop_init = {pop_init}, pop_fin = {pop_fin}")
    print(f"s(N={N_STAT_SURVIE}) = {s_stat:.6f}")
    print(f"s(N/2={N_STAT_SURVIE // 2}) = {s_demi:.6f}")
    print(f"derive = {derive:.6f} (delta = {SURVIE_CONVERGENCE_DELTA:.6f})")
    print(f"predite = {SURVIE_FRACTION_PREDITE_STATIONNAIRE:.6f}")
    print(f"ecart = {ecart:.6f} (tolerance = {SURVIE_TOLERANCE_STATIONNAIRE:.6f})")
    print(f"converge = {converge}")
    print(f"dans_tolerance = {dans_tolerance}")
    print(
        "fraction_survie_dans_tolerance_stationnaire = "
        f"{1 if (converge and dans_tolerance) else 0}"
    )

    assert converge, (
        f"Pas de convergence : |s({N_STAT_SURVIE}) - s({N_STAT_SURVIE // 2})| = "
        f"{derive:.6f} > {SURVIE_CONVERGENCE_DELTA:.6f}. "
        "La fraction de survie dépend encore de l'horizon de test."
    )
    assert dans_tolerance, (
        f"Écart au modèle : |{s_stat:.6f} - "
        f"{SURVIE_FRACTION_PREDITE_STATIONNAIRE:.6f}| = {ecart:.6f} > "
        f"{SURVIE_TOLERANCE_STATIONNAIRE:.6f}."
    )
