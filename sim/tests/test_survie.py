"""
Survie, faim, dette alimentaire et mortalité.

Ce que ce fichier protège (ADR-0018) :
  - invariant physique : la dette alimentaire ne se rembourse jamais plus
    vite que le surplus du tick ;
  - règle de jeu visible : une cellule qui manque de nourriture a faim,
    puis meurt ; une cellule ravitaillée n'a pas faim ;
  - direction du modèle : plus de nourriture = plus de survivants.

Fusion des anciens fichiers mortalite_accumulateur, mortalite_continue,
survie_stationnaire, survie_derivee, sensibilite_survie, hunger_criterion,
deficit_physique et causal_chain.
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
from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import _apply_consumption, _update_hunger, tick
POPULATION_SCENARIO = 50
def _build_monde_temoin_receveuse() -> tuple[World, int, int]:
    """
    Trois cellules, production désactivée (area_km2 = 0.0) :
    - témoin (100)    : possède exactement sa ration, aucune adjacence.
    - source (101)    : population nulle, stock = une ration à donner.
    - receveuse (102) : stock nul, déficit nul, reçoit exactement sa ration.
    """
    besoin_kg = POPULATION_SCENARIO * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK

    temoin = Cell(
        cell_id=100, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=besoin_kg, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    source = Cell(
        cell_id=101, area_km2=0.0, population=0,
        food_stock_kg=besoin_kg, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    receveuse = Cell(
        cell_id=102, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    adjacency = [{"a": 101, "b": 102, "kind": "land", "shared_length_m": 5000.0}]
    world = World(cells={100: temoin, 101: source, 102: receveuse}, adjacency=adjacency)
    return world, 100, 102
from sim.constants import (
    DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG,
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
)
from sim.engine import _apply_consumption
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
from sim.constants import (
    HUNGER_DEATH_SCALE,
    MAX_DEATH_RATE_PER_TICK,
    N_BOUND_MORT,
    N_STAT_SURVIE,
)
from sim.engine import _apply_mortality
POPULATION_PETITE_CELLULE = 5
POPULATIONS_MICRO_MONDE = [50, 137, 500]
DEFICIT_PAR_TETE_MICRO_MONDE_KG = 0.5
from sim.constants import (
    DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG,
    MAX_DEATH_RATE_PER_TICK,
)
from sim.engine import _apply_consumption, _apply_mortality
POPULATIONS_TEST = [1, 5, 9, 20, 100, 1000]
DEFICIT_MINUSCULE_KG = 1e-9
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
from sim.constants import (
    N_STAT_SURVIE,
    SURVIE_CONVERGENCE_DELTA,
    SURVIE_FRACTION_PREDITE_STATIONNAIRE,
    SURVIE_TOLERANCE_STATIONNAIRE,
)
from sim.engine import tick
RNG_SEED = 42
def _fraction_survie_apres(n_ticks: int) -> tuple[float, int, int, int]:
    """
    Exécute n_ticks sur le monde réel G3 et retourne
    (fraction_survie, population_initiale, population_finale, nombre_cellules).
    L'échantillon est toujours le monde chargé — jamais un monde à la main.
    """
    world = World.charger(rng_seed=RNG_SEED)
    rng = random.Random(RNG_SEED)

    pop_init = sum(c.population for c in world.cells.values())
    for _ in range(n_ticks):
        tick(world, rng)
    pop_fin = sum(c.population for c in world.cells.values())

    return pop_fin / pop_init, pop_init, pop_fin, len(world.cells)
import sim.constants as constantes
from sim.constants import (
    SURVIE_TOLERANCE_SENSIBILITE,
    compute_survie_fraction_predite_stationnaire,
)
N_TICKS_SENSIBILITE = 200
FACTEUR_REGIME_BAS = 0.5
FACTEUR_REGIME_HAUT = 2.0
def _mesure_fraction_survie() -> float:
    """Monde réel G3, N_TICKS_SENSIBILITE ticks, constantes courantes."""
    world = World.charger(rng_seed=RNG_SEED)
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


# --- test_causal_chain.py ---
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


# --- test_hunger_criterion.py ---
def test_hunger_ticks_cellule_ravitaillee():
    """
    SC4 — Après un tick complet, ni le témoin ni la receveuse ne sont comptés
    affamés : tous deux ont mangé exactement leur ration.

    Compteur : hunger_ticks_cellule_ravitaillee.
    """
    world, id_temoin, id_receveuse = _build_monde_temoin_receveuse()
    rng = random.Random(0)  # production désactivée : le tirage n'a pas d'effet

    tick(world, rng)

    temoin = world.cells[id_temoin]
    receveuse = world.cells[id_receveuse]

    print(f"temoin    : stock={temoin.food_stock_kg}, deficit={temoin.food_deficit_kg}, "
          f"hunger_ticks={temoin.hunger_ticks}")
    print(f"receveuse : stock={receveuse.food_stock_kg}, deficit={receveuse.food_deficit_kg}, "
          f"hunger_ticks={receveuse.hunger_ticks}")

    hunger_ticks_cellule_ravitaillee = temoin.hunger_ticks + receveuse.hunger_ticks
    print(f"hunger_ticks_cellule_ravitaillee = {hunger_ticks_cellule_ravitaillee}")

    assert temoin.food_stock_kg == 0.0 and temoin.food_deficit_kg == 0.0
    assert receveuse.food_stock_kg == 0.0 and receveuse.food_deficit_kg == 0.0
    assert temoin.hunger_ticks == 0, (
        "Le témoin possédait sa ration exacte : stock nul après consommation "
        "n'est pas de la sous-alimentation."
    )
    assert receveuse.hunger_ticks == 0, (
        "La receveuse a été ravitaillée exactement à son besoin : elle n'a "
        "manqué de rien ce tick."
    )
    assert hunger_ticks_cellule_ravitaillee == 0


# --- test_hunger_criterion.py ---
def test_penurie_reelle_incremente_toujours():
    """
    SC4 — Le critère reste fonctionnel dans l'autre sens : une cellule qui
    manque réellement de nourriture voit bien hunger_ticks progresser.

    Sans ce contrôle, un critère qui n'incrémente jamais passerait le test
    ci-dessus (hard-won rule 6 : un contrôle trop grossier coûte autant qu'un
    contrôle laxiste).
    """
    cell = Cell(
        cell_id=1, area_km2=0.0, population=POPULATION_SCENARIO,
        food_stock_kg=0.0, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    penurie_kg = _apply_consumption(cell)
    _update_hunger(cell, penurie_kg)

    print(f"penurie_kg = {penurie_kg}, hunger_ticks = {cell.hunger_ticks}")
    assert penurie_kg > 0.0
    assert cell.hunger_ticks == 1


# --- test_hunger_criterion.py ---
def test_penurie_retournee_est_le_manque_exact():
    """
    SC4 — La pénurie retournée par _apply_consumption est le manque en kg du
    tick, pas un booléen déguisé : elle vaut exactement
    besoin − stock disponible.
    """
    population = POPULATION_SCENARIO
    besoin = population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    stock_partiel = besoin / (1 + 1)

    cell = Cell(
        cell_id=2, area_km2=0.0, population=population,
        food_stock_kg=stock_partiel, hunger_ticks=0, food_deficit_kg=0.0,
        mortality_remainder=0.0,
    )
    penurie_kg = _apply_consumption(cell)

    print(f"besoin={besoin}, stock={stock_partiel}, penurie_kg={penurie_kg}")
    assert abs(penurie_kg - (besoin - stock_partiel)) < 1e-9
    assert abs(cell.food_deficit_kg - penurie_kg) < 1e-9


# --- test_deficit_physique.py ---
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


# --- test_deficit_physique.py ---
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


# --- test_deficit_physique.py ---
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


# --- test_mortalite_accumulateur.py ---
def test_champ_mortality_remainder_est_sentinelle():
    """
    Le champ `mortality_remainder` existe et vaut -1.0 par défaut : sentinelle
    « non calculé » (hard-won rule 8 — un zéro peut être une mesure réelle).
    """
    cell = Cell(cell_id=1, area_km2=1.0, population=10)
    print(f"mortality_remainder par défaut = {cell.mortality_remainder}")
    assert cell.mortality_remainder == -1.0


# --- test_mortalite_accumulateur.py ---
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


# --- test_mortalite_accumulateur.py ---
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


# --- test_mortalite_continue.py ---
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


# --- test_mortalite_continue.py ---
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


# --- test_survie_derivee.py ---
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


# --- test_survie_derivee.py ---
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


# --- test_survie_stationnaire.py ---
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


# --- test_sensibilite_survie.py ---
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


# --- test_sensibilite_survie.py ---
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


# --- test_sensibilite_survie.py ---
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
