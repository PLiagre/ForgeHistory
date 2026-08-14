"""
SC2 brief 017 — Sensibilité : mesure et prédiction bougent dans le même sens.

Une prédiction qui ne répond pas aux paramètres qu'elle prétend modéliser
n'est pas une prédiction : c'est une constante déguisée.

Mécanisme du remplacement en mémoire (documenté dans sim/SEEDING.md, SC2
brief 017) :

- Le moteur lit `HUNGER_DEATH_SCALE` via le module (`_constantes.HUNGER_DEATH_SCALE`)
  et non par valeur importée : remplacer l'attribut du module change donc bien
  le comportement mesuré.
- La prédiction est recalculée par `compute_survie_fraction_predite_stationnaire()`,
  qui relit les globales courantes. La constante de module
  `SURVIE_FRACTION_PREDITE_STATIONNAIRE`, elle, est figée au chargement : elle
  ne serait pas mise à jour par un remplacement en mémoire, et ce test ne
  l'utilise pas.
- `importlib.reload` n'est volontairement pas utilisé : il rechargerait
  `sim.constants` sans recharger `sim.engine`, laissant moteur et prédiction
  sur deux jeux de constantes différents.

Compteurs : sensibilite_hds_05_passe, sensibilite_hds_2_passe,
sensibilite_drr_direction_passe.
"""

import random

import sim.constants as constantes
from sim.constants import (
    SURVIE_TOLERANCE_SENSIBILITE,
    compute_survie_fraction_predite_stationnaire,
)
from sim.engine import tick
from sim.world import World

N_TICKS_SENSIBILITE = 200
RNG_SEED = 42

# Régimes explorés, exprimés en multiplicateurs de la constante nominale.
FACTEUR_REGIME_BAS = 0.5
FACTEUR_REGIME_HAUT = 2.0


def _mesure_fraction_survie() -> float:
    """Monde réel G3, N_TICKS_SENSIBILITE ticks, constantes courantes."""
    world = World.from_g3(rng_seed=RNG_SEED)
    rng = random.Random(RNG_SEED)
    pop_init = sum(c.population for c in world.cells.values())
    for _ in range(N_TICKS_SENSIBILITE):
        tick(world, rng)
    pop_fin = sum(c.population for c in world.cells.values())
    return pop_fin / pop_init


def _regime_hds(monkeypatch, facteur: float) -> tuple[float, float]:
    """
    Remplace HUNGER_DEATH_SCALE en mémoire par `nominal × facteur`, puis
    retourne (fraction mesurée, fraction prédite) dans ce régime.
    """
    nominal = constantes.HUNGER_DEATH_SCALE
    monkeypatch.setattr(constantes, "HUNGER_DEATH_SCALE", nominal * facteur)
    mesure = _mesure_fraction_survie()
    predite = compute_survie_fraction_predite_stationnaire()
    monkeypatch.setattr(constantes, "HUNGER_DEATH_SCALE", nominal)
    return mesure, predite


def test_sensibilite_hds(monkeypatch):
    """
    SC2 — Trois régimes de HUNGER_DEATH_SCALE (×0.5, nominal, ×2) sur le monde
    réel, N = 200 ticks.

    (a) direction : mesure et prédiction décroissent toutes deux quand la
        mortalité par faim augmente.
    (b) tolérance : |mesurée − prédite| ≤ SURVIE_TOLERANCE_SENSIBILITE
        dans chaque régime.

    Compteurs : sensibilite_hds_05_passe, sensibilite_hds_2_passe.
    """
    nominal = constantes.HUNGER_DEATH_SCALE

    s_bas, p_bas = _regime_hds(monkeypatch, FACTEUR_REGIME_BAS)
    s_nom, p_nom = _regime_hds(monkeypatch, 1.0)
    s_haut, p_haut = _regime_hds(monkeypatch, FACTEUR_REGIME_HAUT)

    assert constantes.HUNGER_DEATH_SCALE == nominal, (
        "Le régime nominal n'a pas été restauré après le test."
    )

    print(f"HDS nominal = {nominal}")
    print(f"regime x{FACTEUR_REGIME_BAS} : mesure={s_bas:.6f} predite={p_bas:.6f}")
    print(f"regime nominal  : mesure={s_nom:.6f} predite={p_nom:.6f}")
    print(f"regime x{FACTEUR_REGIME_HAUT} : mesure={s_haut:.6f} predite={p_haut:.6f}")

    direction_mesure = s_bas > s_nom > s_haut
    direction_predite = p_bas > p_nom > p_haut
    ecart_bas = abs(s_bas - p_bas)
    ecart_nom = abs(s_nom - p_nom)
    ecart_haut = abs(s_haut - p_haut)

    sensibilite_hds_05_passe = int(
        s_bas > s_nom and p_bas > p_nom and ecart_bas <= SURVIE_TOLERANCE_SENSIBILITE
    )
    sensibilite_hds_2_passe = int(
        s_nom > s_haut and p_nom > p_haut and ecart_haut <= SURVIE_TOLERANCE_SENSIBILITE
    )
    print(f"ecarts = {ecart_bas:.6f}, {ecart_nom:.6f}, {ecart_haut:.6f} "
          f"(tolerance = {SURVIE_TOLERANCE_SENSIBILITE:.6f})")
    print(f"sensibilite_hds_05_passe = {sensibilite_hds_05_passe}")
    print(f"sensibilite_hds_2_passe = {sensibilite_hds_2_passe}")

    assert direction_predite, (
        "La prédiction ne répond pas à HUNGER_DEATH_SCALE : "
        f"{p_bas:.6f} / {p_nom:.6f} / {p_haut:.6f}. "
        "Le critère de survie est aveugle à la mortalité."
    )
    assert direction_mesure, (
        "La mesure ne décroît pas quand la mortalité par faim augmente : "
        f"{s_bas:.6f} / {s_nom:.6f} / {s_haut:.6f}."
    )
    for nom_regime, ecart in (
        (f"x{FACTEUR_REGIME_BAS}", ecart_bas),
        ("nominal", ecart_nom),
        (f"x{FACTEUR_REGIME_HAUT}", ecart_haut),
    ):
        assert ecart <= SURVIE_TOLERANCE_SENSIBILITE, (
            f"Régime {nom_regime} : |mesurée − prédite| = {ecart:.6f} > "
            f"{SURVIE_TOLERANCE_SENSIBILITE:.6f}."
        )


def test_sensibilite_drr_direction(monkeypatch):
    """
    SC2 — Le successeur nommé de DEFICIT_RECOVERY_RATE_PER_TICK
    (DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG) entre dans la prédiction avec le
    bon signe : rembourser la dette plus vite ne peut pas faire baisser la
    survie prédite.

    Compteur : sensibilite_drr_direction_passe.
    """
    nominal = constantes.DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG
    predite_nominale = compute_survie_fraction_predite_stationnaire()

    monkeypatch.setattr(
        constantes,
        "DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG",
        nominal * FACTEUR_REGIME_HAUT,
    )
    predite_doublee = compute_survie_fraction_predite_stationnaire()
    monkeypatch.setattr(
        constantes, "DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG", nominal
    )

    sensibilite_drr_direction_passe = int(predite_doublee >= predite_nominale)
    print(f"DRR nominal = {nominal}, predite = {predite_nominale:.6f}")
    print(f"DRR x{FACTEUR_REGIME_HAUT} = {nominal * FACTEUR_REGIME_HAUT}, "
          f"predite = {predite_doublee:.6f}")
    print(f"sensibilite_drr_direction_passe = {sensibilite_drr_direction_passe}")

    assert constantes.DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG == nominal
    assert predite_doublee >= predite_nominale, (
        f"Signe inversé : prédiction {predite_doublee:.6f} < {predite_nominale:.6f} "
        "alors que la dette est remboursée deux fois plus vite."
    )


def test_prediction_reagit_bien_a_la_production(monkeypatch):
    """
    SC1 — Troisième propriété de signe : doubler la production alimentaire
    augmente la survie prédite. Vérifiée sur la prédiction uniquement (le
    brief n'exige pas de mesure pour ce régime).
    """
    nominal = constantes.FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
    predite_nominale = compute_survie_fraction_predite_stationnaire()

    monkeypatch.setattr(
        constantes,
        "FOOD_PRODUCTION_KG_PER_KM2_PER_TICK",
        nominal * FACTEUR_REGIME_HAUT,
    )
    predite_doublee = compute_survie_fraction_predite_stationnaire()
    monkeypatch.setattr(
        constantes, "FOOD_PRODUCTION_KG_PER_KM2_PER_TICK", nominal
    )

    print(f"production nominale = {nominal}, predite = {predite_nominale:.6f}")
    print(f"production x{FACTEUR_REGIME_HAUT}, predite = {predite_doublee:.6f}")

    assert predite_doublee > predite_nominale, (
        f"Doubler la production ne relève pas la survie prédite : "
        f"{predite_doublee:.6f} ≤ {predite_nominale:.6f}."
    )
