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


# --- Relief dans le rendement (brief 033, fidélité niveau 2) ---

# Facteurs de production par classe de relief : ordres de grandeur plausibles
# niveau 2, jamais sourcés historiquement.
FACTEUR_RELIEF_PLAINE = 1.0
FACTEUR_RELIEF_COLLINE = 0.80
FACTEUR_RELIEF_MONTAGNE = 0.45
FACTEUR_RELIEF_HAUTE_MONTAGNE = 0.15
FACTEUR_RELIEF_MARAIS = 0.50


def facteurs_production_par_relief() -> dict[str, float]:
    """
    Table des facteurs de production par classe de relief.

    Relue les constantes nommées à chaque appel : un test de régime qui
    remplace une constante en mémoire doit changer le moteur.
    """
    return {
        "plaine": FACTEUR_RELIEF_PLAINE,
        "colline": FACTEUR_RELIEF_COLLINE,
        "montagne": FACTEUR_RELIEF_MONTAGNE,
        "haute_montagne": FACTEUR_RELIEF_HAUTE_MONTAGNE,
        "marais": FACTEUR_RELIEF_MARAIS,
    }

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

# --- Rendement moyen (seule grandeur dérivée que le moteur consulte) ---

# Le corps d'une fonction de sim/ ne peut pas contenir de littéral numérique
# autre que 0, 1 et -1 (sim/tests/test_no_hardcoded.py) : le diviseur d'une
# moyenne de deux bornes se nomme.
FACTEUR_DEUX = 2.0


def rendement_moyen_courant() -> float:
    """
    Rendement moyen du tirage uniforme de rendement agricole.

    Relit les globales du module à chaque appel : remplacer une borne de
    rendement en mémoire doit changer la moyenne, comme cela change le moteur.
    """
    return (RNG_YIELD_LOW + RNG_YIELD_HIGH) / FACTEUR_DEUX


# --- Ce qui a été retiré ici, et pourquoi ---
#
# Un modèle analytique de survie occupait 262 des 358 lignes de ce fichier :
# capacité de charge, densité stationnaire d'un oscillateur déficit/population,
# espérance du manque de production, probabilité de tick déficitaire, fraction
# de survie prédite, trois tolérances dérivées et un horizon de 1 000 ticks.
# Il prédisait la valeur ABSOLUE de la fraction de survivants, et deux tests
# comparaient la mesure à cette prédiction.
#
# Il est remplacé par trois propriétés mesurées sur le moteur lui-même
# (`sim/tests/test_survie.py`) : le monde ne meurt pas et ne nourrit pas plus
# de monde qu'il ne produit ; la survie répond aux constantes de mortalité ;
# la survie répond à la nourriture.
#
# La raison n'est pas le poids, c'est que la dérivation suppose UNE capacité
# de charge globale, `cap = F x rendement_moyen / C`. Dès que la production
# varie d'une cellule à l'autre — ce que fait le prochain pas du modèle, le
# relief — cette grandeur n'existe plus. Mesuré : avec le relief qui joue, la
# survie tombe à 0.447 contre une prédiction de 0.797 +/- 0.101, soit 3,5 fois
# la tolérance. Le test devient rouge sans qu'aucun défaut n'existe, et la
# seule issue commode est d'élargir la tolérance après avoir vu la mesure —
# exactement la calibration après mesure que ce fichier interdisait.
#
# La garde payée par un vrai défaut est conservée : le critère de survie ne
# doit pas être aveugle aux constantes qui gouvernent la mort (c'est ce que le
# brief 017 reprochait à celui du brief 013). Elle est désormais tenue par la
# DIRECTION de la réponse, mesurée sur le moteur, qui survit à tout changement
# du modèle de production.

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
