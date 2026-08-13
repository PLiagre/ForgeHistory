"""
Constantes nommées du moteur de simulation.

Toutes les valeurs paramétriques documentées dans sim/SEEDING.md.
Aucun code de calcul du moteur ne doit contenir de littéral numérique
au-delà de 0 et 1 (valeurs structurelles) — voir brief 011, SC9.

Brief 012 : constantes temporelles alignées sur TICK_DURATION_DAYS.
"""

# --- Base de temps unique (SC1 brief 012) ---

# Durée d'un tick en jours (proxy paramétrique, voir SEEDING.md).
# Toutes les constantes temporelles ci-dessous sont dérivées de cette valeur.
TICK_DURATION_DAYS = 1

# --- Production alimentaire ---

# Rendement agricole paramétrique : kilogrammes de nourriture produits
# par km² et par tick.
# Proxy : 6 570 kg/km²/an (rendement médiéval estimé) ÷ 365 jours ×
# TICK_DURATION_DAYS (voir SEEDING.md).
FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 18.0 * TICK_DURATION_DAYS

# --- Variabilité de rendement (SC2 brief 012) ---

# Le rendement de chaque cellule est multiplié par un facteur uniforme
# tiré du rng à chaque tick (fluctuations climatiques/agronomiques).
# Distribution : rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
# Documentée dans SEEDING.md.
RNG_YIELD_LOW = 0.5
RNG_YIELD_HIGH = 1.5

# --- Consommation alimentaire ---

# Consommation alimentaire par personne et par tick (kg).
# Proxy : ration journalière médiévale ~2 kg × TICK_DURATION_DAYS.
FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0 * TICK_DURATION_DAYS

# --- Commerce inter-cellules (SC4 brief 012) ---

# Capacité de transport maximale par arête d'adjacence et par tick (kg).
# Proxy paramétrique : convoi à dos de mulet ≈ 200 kg/jour sur une
# liaison rurale (voir SEEDING.md).
TRADE_CAPACITY_KG_PER_EDGE_PER_TICK = 200.0 * TICK_DURATION_DAYS

# --- Mortalité par famine (SC3 brief 012) ---

# Facteur de mortalité : fraction de la population mourant par tick
# par kg de déficit alimentaire cumulé par habitant.
# Mortalité = population × min(per_capita_deficit × HUNGER_DEATH_SCALE,
#                              MAX_DEATH_RATE_PER_TICK)
# Proxy paramétrique — voir SEEDING.md.
HUNGER_DEATH_SCALE = 0.005

# Taux de mortalité maximal par tick (plafond) — empêche l'effondrement
# instantané même avec un déficit extrême.
MAX_DEATH_RATE_PER_TICK = 0.10

# --- Amorçage initial ---

# Densité de population initiale par km² (proxy paramétrique, voir SEEDING.md).
INITIAL_POPULATION_PER_KM2 = 10.0

# Variation aléatoire autour de la densité nominale lors de l'amorçage.
SEED_POPULATION_VARIATION_LOW = 0.9
SEED_POPULATION_VARIATION_HIGH = 1.1

# Nombre de ticks de consommation couverts par le stock alimentaire initial.
# Renommé depuis INITIAL_FOOD_DAYS : l'unité est le tick, pas le jour
# calendaire (SC1 brief 012 — correction du nom trompeur constat P3-2).
INITIAL_FOOD_RESERVE_TICKS = 5

# --- Vitesse de récupération du déficit alimentaire (SC4 brief 013) ---

# Fraction du déficit accumulé effacée par un tick de surplus.
# Justification dans SEEDING.md (SC4 brief 013).
# 10 % par tick de surplus = demi-vie du déficit ≈ 7 ticks
# (≈ une semaine de surplus efface la moitié d'un déficit accumulé sur la même durée).
# Valeur dérivée de la physique médiévale, sans observation de la mesure.
DEFICIT_RECOVERY_RATE_PER_TICK = 0.10

# Seuil de coupure du déficit alimentaire (SC4 brief 013 — N4 feedback 001).
# Un déficit résiduel inférieur à cette valeur est ramené à zéro après récupération,
# évitant l'accumulation indéfinie de déficits infinitésimaux non physiques.
# Justification dans SEEDING.md (SC4 brief 013 — N4 feedback 001).
DEFICIT_ZERO_EPSILON = 1e-6

# --- Seuil de survie de la population (SC5 brief 012 → SC3 brief 013 dérivé) ---

# Fraction prédite analytiquement (capacité de charge malthusienne) :
# fraction_predite = (FOOD_PRODUCTION × rendement_moyen)
#                    / (FOOD_CONSUMPTION × INITIAL_POPULATION_PER_KM2)
# rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2 = 1.0
# → fraction_predite = (18.0 × 1.0) / (2.0 × 10.0) = 0.9
# Formule complète documentée dans SEEDING.md (SC3 brief 013).
_rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
_fraction_predite = (
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * _rendement_moyen
) / (FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK * INITIAL_POPULATION_PER_KM2)

# Marge entre la fraction prédite et le seuil bas (SC3 brief 013 — itération 2).
# EXPRESSION calculée depuis les constantes du modèle ; formula et justification
# dans SEEDING.md (SC3 brief 013 — N4 feedback 001, itération 2).
#
# Deux effets quantifiés sans observation :
#   (1) Dépassement initial de la capacité de charge :
#       dépassement = (d0 - cap) / d0 = (10 - 9) / 10 = 0.10
#       Fraction de la population initiale au-dessus de la cap. de charge ;
#       cette fraction mourra pendant la fenêtre de 200 ticks.
#       Exprimé en points de fraction_predite : dépassement × fraction_predite
#   (2) Pression stochastique des ticks déficitaires :
#       p_déficit = P(yield < C/P) = (C/P - RNG_LOW) / (RNG_HIGH - RNG_LOW)
#                = (1.111 - 0.5) / 1.0 = 0.611
#       La probabilité de déficit multipliée par le taux de récupération
#       donne la pression nette du déficit stochastique sur la mortalité.
#       Terme : p_déficit × DEFICIT_RECOVERY_RATE_PER_TICK
#
# SURVIE_MARGE_DERIVEE = _depassement_initial × _fraction_predite
#                        + _p_tick_deficitaire × DEFICIT_RECOVERY_RATE_PER_TICK
# Avec les constantes actuelles : 0.1×0.9 + (11/18)×0.1 = 0.09 + 0.0611 ≈ 0.1511
_ratio_conso_prod = (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK * INITIAL_POPULATION_PER_KM2
) / FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
_p_tick_deficitaire = min(1.0, max(0.0, (
    _ratio_conso_prod - RNG_YIELD_LOW
) / (RNG_YIELD_HIGH - RNG_YIELD_LOW)))
_cap_hab_km2 = (
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * _rendement_moyen
) / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
_depassement_initial = max(0.0, (
    INITIAL_POPULATION_PER_KM2 - _cap_hab_km2
) / INITIAL_POPULATION_PER_KM2)

SURVIE_MARGE_DERIVEE = (
    _depassement_initial * _fraction_predite
    + _p_tick_deficitaire * DEFICIT_RECOVERY_RATE_PER_TICK
)

# Seuil dérivé : fraction_predite - SURVIE_MARGE_DERIVEE
# Remplace le littéral 0.70 du brief 012 (SC3 brief 013).
SEUIL_SURVIE_POPULATION_FRACTION = _fraction_predite - SURVIE_MARGE_DERIVEE
