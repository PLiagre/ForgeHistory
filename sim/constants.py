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

# --- Marchandises (brief 037) ---

# Première marchandise du panier ; seule entrée réellement simulée pour l'instant.
MARCHANDISE_NOURRITURE = "nourriture"

# Clé-sonde SC5 : prouve qu'une deuxième entrée peut coexister dans le panier.
MARCHANDISE_SONDE_037 = "__sonde_panier_037__"

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


# --- Saison dans le rendement (brief 035, fidélité niveau 2) ---

# Durée d'un jour d'équinoxe — niveau 1 (douze heures partout).
DUREE_JOUR_EQUINOXE_H = 12.0

# Sensibilité du rendement à l'écart de durée du jour par rapport à l'équinoxe ;
# ordre de grandeur plausible niveau 2, jamais sourcé.
SENSIBILITE_SAISON = 0.5

# Rang du solstice d'été dans l'année calendaire ; niveau 2.
JOUR_SOLSTICE_ETE = 172


def jour_de_tick(numero_tick: int | None) -> int:
    """
    Jour de l'année pour un numéro de tick ; absence = premier jour.

    Relit TICK_DURATION_DAYS et CALENDAR_DAYS_PER_YEAR à chaque appel.
    """
    if numero_tick is None:
        return 0
    return (numero_tick * TICK_DURATION_DAYS) % CALENDAR_DAYS_PER_YEAR


def jour_solstice_ete() -> int:
    """Rang du solstice d'été ; relu à chaque appel."""
    return JOUR_SOLSTICE_ETE


def jour_solstice_hiver() -> int:
    """Rang du solstice d'hiver, dérivé de la base de temps."""
    return (JOUR_SOLSTICE_ETE + CALENDAR_DAYS_PER_YEAR // FACTEUR_DEUX) % CALENDAR_DAYS_PER_YEAR


def duree_jour_h(jour: int, ete_h: float, hiver_h: float) -> float:
    """
    Durée du jour à une date, oscillant entre les deux solstices de la cellule.

    Relit JOUR_SOLSTICE_ETE et CALENDAR_DAYS_PER_YEAR à chaque appel.
    """
    moyenne_h = (ete_h + hiver_h) / FACTEUR_DEUX
    amplitude_h = (ete_h - hiver_h) / FACTEUR_DEUX
    annee = CALENDAR_DAYS_PER_YEAR
    solstice = JOUR_SOLSTICE_ETE
    return moyenne_h + amplitude_h * math.cos(math.tau * (jour - solstice) / annee)


def facteur_saison(duree_jour_h_val: float) -> float:
    """
    Modulation du rendement selon la durée du jour, par rapport à l'équinoxe.

    Relit SENSIBILITE_SAISON et DUREE_JOUR_EQUINOXE_H à chaque appel.
    Le plancher à zéro est un invariant physique : pas de production négative.
    """
    equinoxe = DUREE_JOUR_EQUINOXE_H
    sensibilite = SENSIBILITE_SAISON
    ecart = (duree_jour_h_val - equinoxe) / equinoxe
    plancher = 0.0
    return max(plancher, 1.0 + sensibilite * ecart)


def facteur_saison_moyen_annuel(ete_h: float, hiver_h: float) -> float:
    """
    Moyenne du facteur saisonnier sur une année calendaire complète.

    Somme jour par jour, divisée par le nombre de jours dérivé des constantes
    de temps — pas la valeur 1 supposée.
    """
    annee = CALENDAR_DAYS_PER_YEAR
    total = 0.0
    jour = 0
    while jour < annee:
        duree = duree_jour_h(jour, ete_h, hiver_h)
        total += facteur_saison(duree)
        jour += 1
    return total / annee


# --- Extraction minière (brief 038, fidélité niveau 2) ---

# Kilogrammes extraits par habitant et par tick sur un gisement notable ;
# ordre de grandeur plausible niveau 2, jamais sourcé.
EXTRACTION_KG_PAR_HABITANT_PAR_TICK = 0.02

# Facteurs de débit par classe de richesse du gisement ; niveau 2.
FACTEUR_RICHESSE_MAJEURE = 2.0
FACTEUR_RICHESSE_NOTABLE = 1.0
FACTEUR_RICHESSE_MINEURE = 0.4


def extraction_kg_par_habitant_par_tick() -> float:
    """Débit unitaire par habitant ; relu à chaque appel."""
    return EXTRACTION_KG_PAR_HABITANT_PAR_TICK


def facteurs_richesse_extraction() -> dict[str, float]:
    """
    Table des facteurs de débit par classe de richesse d'un gisement.

    Relue les constantes nommées à chaque appel : un test de régime qui
    remplace une constante en mémoire doit changer le moteur.
    """
    return {
        "majeure": FACTEUR_RICHESSE_MAJEURE,
        "notable": FACTEUR_RICHESSE_NOTABLE,
        "mineure": FACTEUR_RICHESSE_MINEURE,
    }


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


# --- Relief dans le transport (brief 040, fidélité niveau 2) ---

# Facteurs de capacité de transport par classe de relief : ordres de grandeur
# plausibles niveau 2, jamais sourcés historiquement — échelle distincte de
# la production (un marais se traverse mal et produit mal, sans coïncidence
# garantie entre les deux tables).
FACTEUR_TRANSPORT_PLAINE = 1.00
FACTEUR_TRANSPORT_COLLINE = 0.70
FACTEUR_TRANSPORT_MARAIS = 0.40
FACTEUR_TRANSPORT_MONTAGNE = 0.30
FACTEUR_TRANSPORT_HAUTE_MONTAGNE = 0.10


def facteurs_transport_par_relief() -> dict[str, float]:
    """
    Table des facteurs de capacité de transport par classe de relief.

    Relue les constantes nommées à chaque appel : un test de régime qui
    remplace une constante en mémoire doit changer le moteur.
    """
    return {
        "plaine": FACTEUR_TRANSPORT_PLAINE,
        "colline": FACTEUR_TRANSPORT_COLLINE,
        "marais": FACTEUR_TRANSPORT_MARAIS,
        "montagne": FACTEUR_TRANSPORT_MONTAGNE,
        "haute_montagne": FACTEUR_TRANSPORT_HAUTE_MONTAGNE,
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


def consommation_kg_par_habitant_par_tick(marchandise: str) -> float:
    """
    Kilogrammes consommés par habitant et par tick pour une marchandise.

    Seul lieu du moteur qui distingue une marchandise d'une autre pour la
    consommation (brief 039). Relit les constantes nommées à chaque appel.
    """
    if marchandise == MARCHANDISE_NOURRITURE:
        return FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    return 0.0

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

# --- Natalité (brief 036, fidélité niveau 2) ---

# Naissances par habitant et par tick, sur les seuls ticks où la cellule
# a mangé sa ration entière sans dette alimentaire — ordre de grandeur
# plausible niveau 2, jamais sourcé.
NAISSANCES_PAR_HABITANT_PAR_TICK = 0.0002


def naissances_par_habitant_par_tick() -> float:
    """Relit NAISSANCES_PAR_HABITANT_PAR_TICK à chaque appel (motif 033)."""
    return NAISSANCES_PAR_HABITANT_PAR_TICK

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

# --- Migration de famine (brief 041) ---

# Part de la population d'une cellule affamée qui émigre en un tick.
# Niveau 2 : ordre de grandeur plausible, jamais sourcé (voir MODELE.md).
FRACTION_MIGRANTE_PAR_TICK = 0.01

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
