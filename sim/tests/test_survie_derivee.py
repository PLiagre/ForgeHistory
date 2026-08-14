"""
SC3 brief 013 — Seuil de survie dérivé analytiquement (ADAPTÉ par le brief 017).

ADAPTATION brief 017 — motivation (reprise dans le journal du Générateur) :

`test_fraction_dans_marge` mesurait la fraction de survie à N = 200 ticks et la
comparait à la fenêtre `[fraction_predite − SURVIE_MARGE_DERIVEE,
fraction_predite + SURVIE_MARGE_DERIVEE]`. Deux défauts, établis par les audits
sources du brief 017 :

1. `SURVIE_MARGE_DERIVEE` ne dépendait ni de `HUNGER_DEATH_SCALE` ni de
   `MAX_DEATH_RATE_PER_TICK` : la garde certifiait la survie sans regarder ce
   qui tue. Une famine deux fois plus mortelle passait le même contrôle.
2. La fenêtre était verte à N = 200 et rouge à N ≥ 1600 sans aucune régression
   du moteur : le critère dépendait de l'horizon de test.

Les deux constantes `SURVIE_MARGE_DERIVEE` et `SEUIL_SURVIE_POPULATION_FRACTION`
sont supprimées de `sim/constants.py`. Ce fichier n'est donc PAS supprimé mais
recentré sur ce qu'il prouve encore honnêtement : la capacité de charge
analytique et la densité stationnaire qui en découle. La conformité de la
couche F2 est désormais portée par
`sim/tests/test_survie_stationnaire.py` (horizon N_STAT_SURVIE, convergence +
tolérance) et `sim/tests/test_sensibilite_survie.py` (signes).

Compteur conservé : fraction_predite_analytique.
"""

from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    INITIAL_POPULATION_PER_KM2,
    RNG_YIELD_HIGH,
    RNG_YIELD_LOW,
    SURVIE_FRACTION_PREDITE_STATIONNAIRE,
    cap_hab_km2_courant,
    densite_stationnaire_courante,
)


def _fraction_predite_from_constants() -> float:
    """Capacité de charge malthusienne rapportée à la densité initiale."""
    rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
    cap = (
        FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
        * rendement_moyen
        / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    )
    return cap / INITIAL_POPULATION_PER_KM2


def test_fraction_predite_analytique():
    """
    SC3 brief 013 — La fraction prédite par la seule capacité de charge est
    dans (0, 1), et `cap_hab_km2_courant()` la reproduit exactement.

    Compteur : fraction_predite_analytique.
    """
    fraction_predite_analytique = _fraction_predite_from_constants()
    cap_module = cap_hab_km2_courant() / INITIAL_POPULATION_PER_KM2

    print(f"fraction_predite_analytique = {fraction_predite_analytique}")
    print(f"cap_hab_km2_courant / d0    = {cap_module}")

    assert 0.0 < fraction_predite_analytique < 1.0
    assert abs(fraction_predite_analytique - cap_module) < 1e-12, (
        "La capacité de charge du module ne reproduit plus la formule "
        "analytique du brief 013."
    )


def test_stationnaire_est_sous_la_capacite_de_charge():
    """
    Brief 017 — La densité stationnaire est strictement inférieure à la
    capacité de charge : la dette accumulée pendant la descente continue de
    tuer après le passage sous `cap`. C'est ce dépassement par le bas que
    l'ancienne fenêtre du brief 013 ne modélisait pas.

    Ce test rougit si la densité stationnaire redevenait la simple capacité de
    charge (retour au modèle aveugle au transitoire).
    """
    cap = cap_hab_km2_courant()
    stationnaire = densite_stationnaire_courante()

    print(f"cap_hab_km2 = {cap}")
    print(f"densite_stationnaire = {stationnaire}")
    print(f"SURVIE_FRACTION_PREDITE_STATIONNAIRE = {SURVIE_FRACTION_PREDITE_STATIONNAIRE}")

    assert INITIAL_POPULATION_PER_KM2 > cap, (
        "Le monde démarre désormais sous la capacité de charge : le modèle de "
        "dépassement du brief 017 doit être revu."
    )
    assert stationnaire < cap
    assert SURVIE_FRACTION_PREDITE_STATIONNAIRE < _fraction_predite_from_constants(), (
        "La prédiction stationnaire doit être plus basse que la simple "
        "capacité de charge : sinon le dépassement et l'érosion ont disparu."
    )
