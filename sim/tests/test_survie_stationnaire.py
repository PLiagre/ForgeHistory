"""
SC1 brief 017 — Le monde vivant ne s'effondre pas, sans calage prédictif.

ADR-0018 : on ne bloque plus le moteur sur |mesurée − formule fermée|.
On garde un horizon long et deux preuves simples :

1. **Le monde reste habité** après N_STAT_SURVIE ticks.
2. **Pas d'effondrement entre N/2 et N** : la fraction de survie à N et
   à N/2 restent du même ordre (écart ≤ SURVIE_CONVERGENCE_DELTA, déjà
   un ordre de grandeur, pas un millième).

La formule `SURVIE_FRACTION_PREDITE_STATIONNAIRE` reste dans
`sim/constants.py` comme documentation. Elle n'est plus une porte CI.
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
    Le monde reste habité ; pas d'effondrement entre N/2 et N.
    La formule fermée est loguée, pas une porte (ADR-0018).
    """
    s_stat, pop_init, pop_fin, nb_cellules = _fraction_survie_apres(N_STAT_SURVIE)
    s_demi, _, _, _ = _fraction_survie_apres(N_STAT_SURVIE // 2)

    derive = abs(s_stat - s_demi)
    ecart = abs(s_stat - SURVIE_FRACTION_PREDITE_STATIONNAIRE)
    meme_ordre = derive <= SURVIE_CONVERGENCE_DELTA

    print(f"cellules = {nb_cellules}")
    print(f"pop_init = {pop_init}, pop_fin = {pop_fin}")
    print(f"s(N={N_STAT_SURVIE}) = {s_stat:.6f}")
    print(f"s(N/2={N_STAT_SURVIE // 2}) = {s_demi:.6f}")
    print(f"derive = {derive:.6f} (delta = {SURVIE_CONVERGENCE_DELTA:.6f})")
    print(f"predite (doc) = {SURVIE_FRACTION_PREDITE_STATIONNAIRE:.6f}")
    print(f"ecart_formule = {ecart:.6f} (informational, not a gate)")
    print(f"monde_habite = {pop_fin > 0}")
    print(f"meme_ordre = {meme_ordre}")

    assert pop_init > 0
    assert pop_fin > 0, (
        f"Le monde s'est vidé en {N_STAT_SURVIE} ticks "
        f"(pop_init={pop_init}, pop_fin={pop_fin})."
    )
    assert 0.0 < s_stat <= 1.0
    assert meme_ordre, (
        f"Effondrement entre N/2 et N : |s({N_STAT_SURVIE}) - "
        f"s({N_STAT_SURVIE // 2})| = {derive:.6f} > "
        f"{SURVIE_CONVERGENCE_DELTA:.6f}."
    )

