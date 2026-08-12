"""
Constantes nommées du moteur de simulation.

Toutes les valeurs paramétriques documentées dans sim/SEEDING.md.
Aucun code de calcul du moteur ne doit contenir de littéral numérique
au-delà de 0 et 1 (valeurs structurelles) — voir brief 011, SC9.
"""

# --- Production alimentaire ---

# Rendement agricole paramétrique : kilogrammes de nourriture produits
# par km² et par tick (proxy annuel divisé en ticks, voir SEEDING.md).
FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 50.0

# --- Consommation alimentaire ---

# Consommation alimentaire par personne et par tick (kg).
FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0

# --- Faim et mortalité ---

# Nombre de ticks consécutifs de faim nécessaires avant que la mortalité
# augmente (lu par test_causal_chain.py — jamais codé en dur dans les tests).
HUNGER_DEATH_THRESHOLD = 3

# Fraction de la population qui meurt par tick quand la faim persiste
# au-delà du seuil (HUNGER_DEATH_THRESHOLD).
HUNGER_DEATH_RATE_PER_TICK = 0.05

# --- Amorçage initial ---

# Densité de population initiale par km² (proxy paramétrique, voir SEEDING.md).
INITIAL_POPULATION_PER_KM2 = 10.0

# Variation aléatoire autour de la densité nominale lors de l'amorçage.
SEED_POPULATION_VARIATION_LOW = 0.9
SEED_POPULATION_VARIATION_HIGH = 1.1

# Nombre de ticks de consommation couverts par le stock alimentaire initial.
INITIAL_FOOD_DAYS = 30
