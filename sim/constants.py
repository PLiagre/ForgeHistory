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

# --- Seuil de survie de la population (SC5 brief 012) ---

# Fraction minimale de la population initiale devant subsister après N ticks.
# Choix : 0.70 — autorise des pertes locales réelles sans effondrement global.
# Justification dans SEEDING.md.
SEUIL_SURVIE_POPULATION_FRACTION = 0.70
