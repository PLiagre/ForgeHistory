"""
Constantes nommées du moteur de simulation.

Toutes les valeurs paramétriques documentées dans sim/MODELE.md.
Aucun code de calcul du moteur ne doit contenir de littéral numérique
au-delà de 0 et 1 (valeurs structurelles) — voir brief 011, SC9.

Brief 012 : constantes temporelles alignées sur TICK_DURATION_DAYS.
Brief 017 : modèle de survie stationnaire dépendant des constantes de
mortalité, et récupération physique du déficit alimentaire.
"""

import math

# --- Base de temps unique (SC1 brief 012) ---

# Durée d'un tick en jours (proxy paramétrique, voir MODELE.md).
# Toutes les constantes temporelles ci-dessous sont dérivées de cette valeur.
TICK_DURATION_DAYS = 1

# --- Production alimentaire ---

# Rendement agricole paramétrique : kilogrammes de nourriture produits
# par km² et par tick.
# Proxy : 6 570 kg/km²/an (rendement médiéval estimé) ÷ 365 jours ×
# TICK_DURATION_DAYS (voir MODELE.md).
FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 18.0 * TICK_DURATION_DAYS

# --- Variabilité de rendement (SC2 brief 012) ---

# Le rendement de chaque cellule est multiplié par un facteur uniforme
# tiré du rng à chaque tick (fluctuations climatiques/agronomiques).
# Distribution : rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
# Documentée dans MODELE.md.
RNG_YIELD_LOW = 0.5
RNG_YIELD_HIGH = 1.5

# --- Consommation alimentaire ---

# Consommation alimentaire par personne et par tick (kg).
# Proxy : ration journalière médiévale ~2 kg × TICK_DURATION_DAYS.
FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0 * TICK_DURATION_DAYS

# --- Commerce inter-cellules (SC4 brief 012) ---

# Capacité de transport maximale par arête d'adjacence et par tick (kg).
# Proxy paramétrique : convoi à dos de mulet ≈ 200 kg/jour sur une
# liaison rurale (voir MODELE.md).
TRADE_CAPACITY_KG_PER_EDGE_PER_TICK = 200.0 * TICK_DURATION_DAYS

# --- Mortalité par famine (SC3 brief 012) ---

# Facteur de mortalité : fraction de la population mourant par tick
# par kg de déficit alimentaire cumulé par habitant.
# Mortalité = population × min(per_capita_deficit × HUNGER_DEATH_SCALE,
#                              MAX_DEATH_RATE_PER_TICK)
# Proxy paramétrique — voir MODELE.md.
HUNGER_DEATH_SCALE = 0.005

# Taux de mortalité maximal par tick (plafond) — empêche l'effondrement
# instantané même avec un déficit extrême.
MAX_DEATH_RATE_PER_TICK = 0.10

# --- Amorçage initial ---

# Densité de population initiale par km² (proxy paramétrique, voir MODELE.md).
INITIAL_POPULATION_PER_KM2 = 10.0

# Variation aléatoire autour de la densité nominale lors de l'amorçage.
SEED_POPULATION_VARIATION_LOW = 0.9
SEED_POPULATION_VARIATION_HIGH = 1.1

# Nombre de ticks de consommation couverts par le stock alimentaire initial.
# Renommé depuis INITIAL_FOOD_DAYS : l'unité est le tick, pas le jour
# calendaire (SC1 brief 012 — correction du nom trompeur constat P3-2).
INITIAL_FOOD_RESERVE_TICKS = 5

# --- Récupération physique du déficit alimentaire (SC5 brief 017) ---

# Successeur nommé de DEFICIT_RECOVERY_RATE_PER_TICK (brief 013, supprimée).
# Ancienne sémantique : fraction du déficit effacée par tick de surplus,
# indépendamment du surplus réel — un nanogramme d'excédent effaçait 10 % de
# la dette (principe 3 violé : des kilogrammes disparaissaient sans
# contrepartie physique).
# Nouvelle sémantique : kilogrammes de dette alimentaire remboursés par
# kilogramme de surplus RÉELLEMENT consommé au-delà du besoin d'entretien.
# Voie (a) du brief 017 : ratio 1:1. Les kg remboursés quittent le stock.
# Justification complète dans MODELE.md (SC5 brief 017).
DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG = 1.0

# Seuil de coupure du déficit alimentaire (SC4 brief 013 — N4 feedback 001).
# Un déficit résiduel inférieur à cette valeur est ramené à zéro après récupération,
# évitant l'accumulation indéfinie de déficits infinitésimaux non physiques.
# Justification dans MODELE.md (SC4 brief 013 — N4 feedback 001).
DEFICIT_ZERO_EPSILON = 1e-6

# --- Modèle de survie stationnaire (SC1 brief 017) ---
#
# Remplace SURVIE_MARGE_DERIVEE / SEUIL_SURVIE_POPULATION_FRACTION (brief 013),
# supprimées : ces deux constantes ignoraient HUNGER_DEATH_SCALE et faisaient
# entrer la récupération du déficit avec le mauvais signe. Motivation détaillée
# dans MODELE.md (SC1 brief 017) et dans le journal du Générateur.
#
# Toutes les grandeurs ci-dessous sont calculées par des FONCTIONS qui relisent
# les globales courantes du module à chaque appel. C'est indispensable au test
# de sensibilité (SC2) : une valeur figée au chargement ne bougerait pas quand
# un test remplace HUNGER_DEATH_SCALE en mémoire.

# Facteurs structurels nommés (aucun littéral numérique n'est autorisé dans les
# corps de fonctions de sim/ — voir sim/tests/test_no_hardcoded.py).
FACTEUR_DEUX = 2.0                 # moyenne d'une loi uniforme, intégrale d'une rampe
VARIANCE_UNIFORME_DIVISEUR = 12.0  # variance d'une loi uniforme = (b - a)² / 12
TAU = math.tau                     # période d'un tour complet (2π)


def rendement_moyen_courant() -> float:
    """Rendement moyen du tirage uniforme de rendement agricole."""
    return (RNG_YIELD_LOW + RNG_YIELD_HIGH) / FACTEUR_DEUX


def rendement_ecart_type_courant() -> float:
    """Écart-type du tirage uniforme de rendement agricole."""
    etendue = RNG_YIELD_HIGH - RNG_YIELD_LOW
    return etendue / math.sqrt(VARIANCE_UNIFORME_DIVISEUR)


def cap_hab_km2_courant() -> float:
    """
    Capacité de charge : densité (hab/km²) que la production moyenne nourrit
    exactement, sans déficit ni surplus.
    """
    return (
        FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * rendement_moyen_courant()
        / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    )


def densite_stationnaire_courante() -> float:
    """
    Densité atteinte après le dépassement (« overshoot ») du transitoire.

    Dérivation (documentée dans MODELE.md, SC1 brief 017) : tant que la
    densité d dépasse la capacité de charge, le déficit D croît au rythme
    C·(d − cap) et la mortalité fait décroître d au rythme HDS·D. Ce couple
    est un oscillateur : d dépasse la capacité de charge par le bas d'autant
    qu'il la dépassait par le haut au départ. La densité au moment où le
    déficit revient à zéro vaut donc cap − (d0 − cap).
    """
    cap = cap_hab_km2_courant()
    return max(0.0, cap - (INITIAL_POPULATION_PER_KM2 - cap))


def manque_moyen_kg_par_km2(besoin_kg_par_km2: float) -> float:
    """
    Espérance du manque de production sur un tick, E[max(0, besoin − F·Y)],
    pour un rendement Y uniforme sur [RNG_YIELD_LOW, RNG_YIELD_HIGH].

    Forme fermée : nulle si le pire rendement suffit, linéaire si le meilleur
    rendement ne suffit jamais, quadratique entre les deux.
    """
    prod_min = FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * RNG_YIELD_LOW
    prod_max = FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * RNG_YIELD_HIGH
    if besoin_kg_par_km2 <= prod_min:
        return 0.0
    if besoin_kg_par_km2 >= prod_max:
        return besoin_kg_par_km2 - (
            FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * rendement_moyen_courant()
        )
    ecart = besoin_kg_par_km2 - prod_min
    etendue = RNG_YIELD_HIGH - RNG_YIELD_LOW
    return (ecart * ecart) / (
        FACTEUR_DEUX * FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * etendue
    )


def probabilite_tick_deficitaire_courante() -> float:
    """
    Probabilité qu'un tick soit déficitaire à la densité stationnaire :
    P(F·Y < C·d_stat). Seul ce côté de la distribution de rendement tue.
    """
    besoin = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK * densite_stationnaire_courante()
    seuil_rendement = besoin / FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
    etendue = RNG_YIELD_HIGH - RNG_YIELD_LOW
    return min(1.0, max(0.0, (seuil_rendement - RNG_YIELD_LOW) / etendue))


def compute_survie_fraction_predite_stationnaire() -> float:
    """
    Fraction de population survivante prédite à l'état stationnaire.

    Deux termes, tous deux dérivés des constantes (jamais calibrés sur une
    mesure — voir MODELE.md SC1 brief 017, rédigé avant toute mesure) :

    1. Dépassement déterministe : d_stat / d0, où d_stat est la densité
       atteinte quand le déficit revient à zéro (densite_stationnaire_courante).

    2. Érosion stochastique : à la densité stationnaire, un tick de mauvais
       rendement crée un déficit moyen `manque_moyen_kg_par_km2`. Ce déficit
       tue une fraction min(HDS × déficit_par_tête, MAX_DEATH_RATE_PER_TICK)
       de la population, pendant le temps qu'il met à être remboursé
       (1 / DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG ticks au ratio nominal), et
       ce sur l'échelle de temps du tampon alimentaire
       (INITIAL_FOOD_RESERVE_TICKS ticks, seule échelle de stockage nommée
       du modèle).

    Dépendances explicites exigées par le brief : HUNGER_DEATH_SCALE,
    MAX_DEATH_RATE_PER_TICK, DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK, FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    INITIAL_POPULATION_PER_KM2, RNG_YIELD_LOW, RNG_YIELD_HIGH.
    """
    if INITIAL_POPULATION_PER_KM2 <= 0:
        return 0.0

    densite_stationnaire = densite_stationnaire_courante()
    fraction_depassement = min(1.0, densite_stationnaire / INITIAL_POPULATION_PER_KM2)

    if densite_stationnaire <= 0:
        return 0.0

    besoin = FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK * densite_stationnaire
    deficit_par_tete = manque_moyen_kg_par_km2(besoin) / densite_stationnaire
    taux_mortalite = min(
        HUNGER_DEATH_SCALE * deficit_par_tete, MAX_DEATH_RATE_PER_TICK
    )

    if DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG > 0:
        ticks_de_remboursement = 1.0 / DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG
    else:
        ticks_de_remboursement = INITIAL_FOOD_RESERVE_TICKS

    erosion = min(
        1.0,
        taux_mortalite * ticks_de_remboursement * INITIAL_FOOD_RESERVE_TICKS,
    )
    return max(0.0, min(1.0, fraction_depassement * (1.0 - erosion)))


def compute_survie_tolerance_stationnaire() -> float:
    """
    Tolérance sur |mesuré − prédit| à l'horizon stationnaire.

    Dérivation : le modèle remplace le tirage de rendement par sa moyenne.
    L'erreur au premier ordre est la dispersion relative du rendement
    (écart-type / moyenne), convertie en habitants via le rapport
    capacité de charge / densité initiale, et restreinte au seul côté de la
    distribution qui tue (probabilité qu'un tick soit déficitaire).
    """
    moyenne = rendement_moyen_courant()
    if moyenne <= 0 or INITIAL_POPULATION_PER_KM2 <= 0:
        return 0.0
    dispersion_relative = rendement_ecart_type_courant() / moyenne
    conversion_habitants = cap_hab_km2_courant() / INITIAL_POPULATION_PER_KM2
    return (
        dispersion_relative
        * conversion_habitants
        * probabilite_tick_deficitaire_courante()
    )


def compute_survie_convergence_delta() -> float:
    """
    Tolérance sur |s(N_STAT_SURVIE) − s(N_STAT_SURVIE ÷ 2)|.

    Dérivation : à l'état stationnaire, la seule dérive restante vient des
    fluctuations de rendement. Une fluctuation d'un écart-type expose une
    fraction `dispersion_relative` de la population, et une population exposée
    ne peut perdre plus que MAX_DEATH_RATE_PER_TICK par tick.
    """
    moyenne = rendement_moyen_courant()
    if moyenne <= 0:
        return 0.0
    dispersion_relative = rendement_ecart_type_courant() / moyenne
    return dispersion_relative * MAX_DEATH_RATE_PER_TICK


def compute_survie_tolerance_sensibilite() -> float:
    """
    Tolérance sur |mesuré − prédit| dans les régimes de sensibilité (SC2),
    mesurés à N = 200 ticks, c'est-à-dire avant amortissement complet du
    transitoire dans les régimes à faible HUNGER_DEATH_SCALE.

    Dérivation : tolérance stationnaire + amplitude du dépassement initial
    (fraction de la population initiale au-dessus de la capacité de charge),
    qui est encore partiellement en cours de résorption à cet horizon.
    """
    if INITIAL_POPULATION_PER_KM2 <= 0:
        return compute_survie_tolerance_stationnaire()
    depassement_initial = max(
        0.0,
        (INITIAL_POPULATION_PER_KM2 - cap_hab_km2_courant())
        / INITIAL_POPULATION_PER_KM2,
    )
    return compute_survie_tolerance_stationnaire() + depassement_initial


def compute_periode_oscillation_transitoire() -> float:
    """
    Période de l'oscillation déficit ↔ population pendant le transitoire.

    Le couple (déficit D, écart de densité x = d − cap) obéit à
    D' = C·x et x' = −HDS·D : un oscillateur de pulsation √(HDS·C).
    La durée du transitoire est de l'ordre d'une demi-période.
    """
    pulsation_carree = HUNGER_DEATH_SCALE * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    if pulsation_carree <= 0:
        return 0.0
    return TAU / math.sqrt(pulsation_carree)


def compute_n_stat_survie() -> int:
    """
    Horizon de convergence (ticks), justifié AVANT toute mesure.

    Le transitoire dure au plus une période d'oscillation
    (compute_periode_oscillation_transitoire ≈ 63 ticks avec les constantes
    actuelles). L'horizon couvre cette période répétée 1/MAX_DEATH_RATE_PER_TICK
    fois — l'échelle de temps au bout de laquelle toute mortalité résiduelle
    au taux plafond est épuisée. Un plancher de N_STAT_SURVIE_PLANCHER ticks
    est imposé par le brief 017.
    """
    if MAX_DEATH_RATE_PER_TICK <= 0:
        return N_STAT_SURVIE_PLANCHER
    horizon_derive = math.ceil(
        compute_periode_oscillation_transitoire() / MAX_DEATH_RATE_PER_TICK
    )
    return max(N_STAT_SURVIE_PLANCHER, horizon_derive)


# Plancher d'horizon imposé par le brief 017 (SC1) : jamais moins de 1000 ticks.
N_STAT_SURVIE_PLANCHER = 1000

N_STAT_SURVIE = compute_n_stat_survie()
SURVIE_FRACTION_PREDITE_STATIONNAIRE = compute_survie_fraction_predite_stationnaire()
SURVIE_TOLERANCE_STATIONNAIRE = compute_survie_tolerance_stationnaire()
SURVIE_CONVERGENCE_DELTA = compute_survie_convergence_delta()
SURVIE_TOLERANCE_SENSIBILITE = compute_survie_tolerance_sensibilite()

# --- Borne de ticks pour qu'une fraction de mort devienne une mort entière ---
# N_BOUND_MORT = ceil(1 / MAX_DEATH_RATE_PER_TICK) : au plafond de mortalité,
# une cellule accumule au moins MAX_DEATH_RATE_PER_TICK mort par habitant et
# par tick ; le report de la fraction (mortality_remainder) garantit qu'une
# mort entière est appliquée en au plus ce nombre de ticks (SC3 brief 017).
N_BOUND_MORT = math.ceil(1.0 / MAX_DEATH_RATE_PER_TICK)

# --- Entrée en ligne de commande (python -m sim), ADR-0016 ---
# Un an calendaire de ticks, dérivé de la base de temps unique : jamais un
# second littéral de durée. TICK_DURATION_DAYS vaut 1 aujourd'hui.
CALENDAR_DAYS_PER_YEAR = 365
DEFAULT_CLI_TICKS = CALENDAR_DAYS_PER_YEAR * TICK_DURATION_DAYS
DEFAULT_CLI_SEED = 0

# --- Snapshot cellulaire V0-A (brief 027) ---
# Première photographie cellulaire du jalon V0-A ; le suffixe -1 permet une
# révision du contrat sans réutiliser le même nom.
SNAPSHOT_SCHEMA_VERSION = "v0a-2"
# Même pas que tools/map/io_util.py : plus fin serait du bruit, plus gros
# écraserait des centroïdes voisins.
SNAPSHOT_FLOAT_DECIMALS = 6
