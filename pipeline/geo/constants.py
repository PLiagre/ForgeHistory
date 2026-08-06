"""Constantes partagées du pipeline cartographique (G1 fixture + G2 littoral)."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Tuple

PIPELINE_VERSION = "1.1.0-g2-v1_046"
G2B_PIPELINE_VERSION = "1.2.0-g2b-v1_047"
G3_PIPELINE_VERSION = "1.4.0-g3-v1_049"
G4_PIPELINE_VERSION = "1.5.0-g4-v1_050"
G5_PIPELINE_VERSION = "1.6.0-g5-v1_051"
G5B_PIPELINE_VERSION = "1.6.1-g5b-v1_058"
G5C_PIPELINE_VERSION = "1.6.2-g5c-v1_067"
G6_PIPELINE_VERSION = "1.7.0-g6-v1_052"
G7_PIPELINE_VERSION = "1.8.0-g7-v1_059"
G8_PIPELINE_VERSION = "1.9.0-g8-v1_060"
G8_REGISTRY_CREATED = "2026-07-26"
G9_PIPELINE_VERSION = "1.10.0-g9-v1_061"
G9_REGISTRY_CREATED = "2026-07-26"
G10_PIPELINE_VERSION = "1.11.0-g10-v1_062"
G10_REGISTRY_CREATED = "2026-07-26"
# P1 / v1_066 — proposition de peuplement (hors import). Source = NE cultural.
P1_PIPELINE_VERSION = "1.12.0-p1-v1_066"
P1_REGISTRY_CREATED = "2026-07-26"
# P2 / v1_072 — GeoNames cities500 (CC BY 4.0). Proposition + correction coords.
P2_PIPELINE_VERSION = "1.14.0-p2-v1_072"
P2_REGISTRY_CREATED = "2026-07-26"
P2_SOURCE_ARCHIVE = "cities500.zip"
P2_GEONAMES_URL = "https://download.geonames.org/export/dump/cities500.zip"
P2_GEONAMES_README_URL = "https://download.geonames.org/export/dump/readme.txt"
# Fenêtre d'appariement pour les 123 coords du jeu (plus large que pilote)
P2_MATCH_WINDOW_LONLAT = (-12.0, 30.0, 45.0, 62.0)
P2_MATCH_WINDOW_JUSTIFICATION = (
    "Couvre l'emprise des 123 city_coordinates (lon≈-9..33, lat≈31..60) "
    "plus marge : Europe + Méditerranée + Proche-Orient de jeu."
)
# Distance max (km) entre position JEU actuelle et candidat GeoNames — bornée.
P2_MAX_MATCH_DISTANCE_KM = 250.0
P2_MAX_MATCH_DISTANCE_JUSTIFICATION = (
    "250 km : au-dessus de l'écart max mesuré vs NE (~141 km Toulouse) "
    "tout en refusant les homonymes mondiaux (Londres US = milliers de km)."
)
P2_CERTAINTY_NAME = "reconstructed_established"
P2_CERTAINTY_COORDS = "derived"
P2_PROVENANCE = (
    "connaissance historique générale, non sourcée par citation primaire"
)
P2_ATTRIBUTION_TEXT = (
    "GeoNames (www.geonames.org), licensed under Creative Commons "
    "Attribution 4.0 International (CC BY 4.0)"
)
# ISO2 européens (+ EG pour Alexandrie du jeu) — filtre homonymes GeoNames.
P2_EUROPE_ISO2 = frozenset(
    {
        "FR",
        "GB",
        "BE",
        "NL",
        "DE",
        "ES",
        "CH",
        "LU",
        "AT",
        "IT",
        "IE",
        "PT",
        "DK",
        "CZ",
        "PL",
        "SE",
        "NO",
        "TR",
        "HU",
        "RO",
        "BG",
        "GR",
        "UA",
        "HR",
        "SI",
        "SK",
        "BA",
        "RS",
        "MK",
        "AL",
        "MT",
        "CY",
        "LI",
        "AD",
        "MC",
        "SM",
        "VA",
        "IM",
        "JE",
        "GG",
        "FO",
        "IS",
        "EE",
        "LV",
        "LT",
        "BY",
        "MD",
        "XK",
        "GI",
        "EG",
    }
)
# A12 / v1_069 — apparence (ombrage DEM + biomes dérivés). Hors Unity.
A12_PIPELINE_VERSION = "1.13.0-a12-v1_069"
A12_REGISTRY_CREATED = "2026-07-26"
# Paramètres d'ombrage (réglages d'apparence — publiés, pas enterrés).
A12_HILLSHADE_AZIMUTH_DEG = 315.0  # NW — classique cartographique
A12_HILLSHADE_ALTITUDE_DEG = 45.0
A12_HILLSHADE_Z_FACTOR = 2.5  # exagération verticale
A12_HILLSHADE_METHOD = "horn"  # pente 3×3 Horn (ESRI/GDAL)
A12_HILLSHADE_ROUND = "rint_clip_uint8"  # arrondi déterministe
A12_HILLSHADE_PARAM_DOC = {
    "azimuth_deg": "Direction de la source lumineuse (0=N, 90=E). Change les ombres portées.",
    "altitude_deg": "Hauteur du soleil au-dessus de l'horizon. Plus bas = ombres plus longues.",
    "z_factor": "Exagération verticale du DEM. Plus haut = relief plus dramatique.",
    "method": "horn : dérivées dz/dx,dz/dy sur voisinage 3×3 pondéré (norme cartographique).",
    "round": "rint puis clip [0,255] uint8 — deux exécutions → mêmes octets.",
}
# Résolutions FIGÉES sur G10 (mêmes cadrage / projection / tailles).
A12_RESOLUTIONS = {
    0: (4096, 3686),
    1: (2048, 1843),
    2: (1024, 922),
}
# Biomes : max part d'un seul biome (refus carte dégénérée).
A12_BIOME_MAX_SHARE = 0.55
A12_BIOME_MAX_SHARE_DOC = (
    "Si un biome ≥ 55 % des cellules, la règle n'a rien différencié — refus."
)
# Couverture forestière moderne Europe occidentale ≈ 15 % ; cible 1400 plus haute.
A12_FOREST_MODERN_PCT_REF = 15.0
A12_BIOME_CERTAINTY = "derived"
A12_BIOME_PROVENANCE = (
    "Règle écrite sur altitude/pente/rugosité/latitude/littoralité (+ climate "
    "province en indice secondaire faible). Pas de couverture du sol moderne. "
    "Biais 1400 : Europe plus boisée qu'aujourd'hui."
)
# Témoins de vraisemblance biomes (cell_id → biomes acceptés).
A12_BIOME_WITNESSES = (
    ("hautes_alpes", 1388, ("roche", "alpage")),
    ("bretagne_oceanique", 1218, ("foret", "cultures")),
    ("landes", 1176, ("lande", "foret")),
    ("camargue", 1331, ("marais",)),
    ("bouches_escaut", 1309, ("marais",)),
    ("plaine_flamande", 1293, ("cultures", "marais")),
)
# Zones nommées pour contraste d'ombrage (lon_min, lat_min, lon_max, lat_max).
A12_RELIEF_ZONES = {
    "Alpes": (5.5, 44.0, 8.5, 47.0),
    "Pyrenees": (-2.0, 42.0, 3.0, 43.3),
    "Massif_central": (1.5, 44.5, 4.5, 46.5),
    "Ardennes": (4.5, 49.5, 7.0, 50.8),
    "Jura": (5.5, 46.2, 7.0, 47.5),
    "Bassin_parisien": (1.4, 48.1, 3.5, 49.3),
    "Plaine_flamande": (2.5, 50.6, 4.5, 51.6),
}
A12_RELIEF_MUST_BE_HIGH = ("Alpes", "Pyrenees", "Massif_central", "Ardennes", "Jura")
A12_RELIEF_MUST_BE_LOW = ("Bassin_parisien", "Plaine_flamande")
# Empreintes figées à ne pas altérer (A1-D).
A12_UNCHANGED_ARTIFACTS = (
    "artifacts/cells_g3.json",
    "artifacts/adjacency_g4.json",
    "artifacts/cells_relief_g6.json",
    "artifacts/cell_ids_lod0.png",
    "artifacts/cell_ids_lod1.png",
    "artifacts/cell_ids_lod2.png",
)
A12_COPERNICUS_ATTRIBUTION = (
    "© DLR e.V. 2010-2014 et © Airbus Defence and Space GmbH 2014-2018 fournis "
    "dans le cadre de COPERNICUS par l'Union européenne et l'ESA"
)
P1_LAYER = "ne_10m_populated_places"
P1_SOURCE_ARCHIVE = "10m_cultural.zip"
# Appariement nom : homonymes hors Europe refusés (pas de plus-proche voisin).
P1_EUROPE_ADM0_A3 = frozenset(
    {
        "FRA",
        "GBR",
        "BEL",
        "NLD",
        "DEU",
        "ESP",
        "CHE",
        "LUX",
        "AUT",
        "ITA",
        "IRL",
        "PRT",
        "DNK",
        "CZE",
        "POL",
        "SWE",
        "NOR",
    }
)
# Doublon avec cities.json : même nom OU proximité ≤ borne (mètres projetés).
P1_DUPLICATE_PROXIMITY_M = 5_000.0
P1_DUPLICATE_PROXIMITY_JUSTIFICATION = (
    "5 km : tolérance de coordonnée entre cities.json (arrondi gameplay) et "
    "Natural Earth ; au-delà, deux points du même nom ne sont plus le même site."
)
P1_CERTAINTY = "reconstructed_established"
P1_PROVENANCE = (
    "connaissance historique générale, non sourcée par citation primaire dans "
    "ce brief"
)
# G10 — textures d'identifiants (§7.3). Résolution DÉRIVÉE, jamais choisie.
# Contrainte : la plus petite cellule (≈11 km²) doit couvrir ≥ N pixels sur la
# plus courte dimension de sa bbox lon/lat — seuil de cliquabilité (souris /
# doigt sur une île). N=12 : marge au-dessus de 1 px (disparition) et de ~4
# (instabilité de bord), sans exploser le poids Unity.
G10_MIN_PIXELS_SHORTEST_BBOX = 12
G10_MAX_LOD0_WIDTH = 8192
G10_MIN_PIXELS_JUSTIFICATION = (
    "N=12 pixels sur la plus courte dimension de bbox de la plus petite cellule "
    "(cell_id 1362, ≈11 km²) : un seul pixel rend la sélection aléatoire sur le "
    "bord ; 4 restent fragiles au sous-échantillon LOD ; 12 laisse une marge "
    "de clic tout en dérivant une résolution puissance-de-2 (4096) compatible "
    "GPU. La résolution se calcule : W = next_pow2(ceil(Δlon × N / min_bbox_deg))."
)
# Échantillon de concordance texture ↔ géométrie (déterministe).
G10_CONCORDANCE_SEED = 20260726
G10_CONCORDANCE_POINT_COUNT = 50_000
# Écart « bord » si distance à la frontière < cette fraction de la taille de pixel.
G10_BORDER_PIXEL_FRACTION = 1.5
# Seuils de contrôle.
G10_CONCORDANCE_MIN_RATE = 0.995  # 99.5 %
G10_HEART_MISMATCH_MAX = 0  # zéro écart au cœur
# Masque terre/mer/lac (canal R du PNG masque).
G10_MASK_SEA = 0
G10_MASK_LAND = 1
G10_MASK_LAKE = 2
# Valeur réservée « aucune cellule » dans la texture d'identifiants (= mer vide).
G10_ID_EMPTY = 0
# Arbitrage pixels de bord : identifiant le plus petit gagne (croissants).
G10_EDGE_ARB_RULE = "lowest_id_wins"
G10_EDGE_ARB_DOC = (
    "Quand deux polygones (cellule ou zone maritime) couvrent le même pixel, "
    "l'identifiant numérique le plus petit gagne. Implémentation : rastérisation "
    "par ordre d'id décroissant (REPLACE) — le plus petit écrase en dernier. "
    "Indépendant de l'ordre d'itération des fichiers sources."
)
# LOD destinés à la sélection (tous les features ≥ 1 px exigés).
G10_SELECTION_LODS = (0, 1)
# LOD2 (monde) : affichage ; disparition éventuelle = constat publié.
G10_DISPLAY_ONLY_LODS = (2,)
# G9 — LOD topologiques (§7.2) : simplification d'ARCS PARTAGÉS (topojson),
# jamais polygone-par-polygone. Tolérances Douglas-Peucker en mètres EPSG:3035.
# LOD0 = natif ; LOD1 = pays ; LOD2 = monde (plafond propre de l'outillage).
G9_LOD_TOLERANCES_M = (0.0, 400.0, 700.0)
G9_LOD_TOLERANCE_JUSTIFICATION = (
    "LOD0=0 m : geometrie v1_049 inchangee (province). "
    "LOD1=400 m : niveau pays — crenelage NE lisse, partition planaire conservee "
    "(topojson + prevent_oversimplify). "
    "LOD2=700 m : niveau monde — maximum testé sans perte d'adjacence ni "
    "chevauchement mesurable ; au-delà (750 m+) une adjacence mer-mer s'effondre, "
    "et vers 1400–2000 m des chevauchements apparaissent malgré la topologie. "
    "400/2000 du plan restent la cible de gameplay ; 700 m est le plafond "
    "propre de l'outillage disponible (topojson DP)."
)
# Quantification des sommets pour extraction d'arcs maison (controle G9-A).
G9_COORD_QUANT_DECIMALS = 3
# Trous (interstices morphologiques) / chevauchements (sum−union), m².
G9_HOLE_EPS_M2 = 10_000.0  # 0.01 km²
G9_OVERLAP_EPS_M2 = 10_000.0
# Adjacence survivante : frontière partagée minimale (m) — hors détroits.
G9_ADJ_SURVIVE_EPS_M = 1.0
G9_AREA_RATIO_DECIMALS = 6
# Fermeture morphologique pour mesurer les interstices entre cellules (m).
G9_INTERSTICE_CLOSE_M = 2.0
# Possession : certitude ADR-003 — gameplay uniquement (source = provinces.json).
G8_OWNERSHIP_CERTAINTY = "gameplay"
G8_OWNERSHIP_PROVENANCE = (
    "Derive de provinces.json owner_tag via attribution cellule→province "
    "v1_057 (borne 180 km) et, le cas echeant, via province_id de la ville "
    "rattachee (v1_059). Choix de conception du projet — PAS une source "
    "historique primaire des frontieres de 1400."
)
# Contrôle de vraisemblance (pays attendus par la donnée de jeu / brief).
G8_PLAUSIBILITY_CITIES = (
    ("Paris", "FRA"),
    ("London", "ENG"),
    ("Bordeaux", "FRA"),
    ("Dijon", "BUR"),
    ("Gand", "BUR"),
)
# Coordonnées de secours si la ville n'est pas dans cities.json (Gand).
G8_CITY_FALLBACK_LONLAT = {
    "Gand": (3.7174, 51.0543),
}
CELL_ID_BASE = 1000
SEA_CELL_ID = 0
# G4 — zones maritimes : plage d'ids DISTINCTE des cellules terrestres (≥1164).
SEA_ZONE_ID_BASE = 5000
SEA_ZONE_COUNT_MIN = 20
SEA_ZONE_COUNT_MAX = 40
# Espacement mer : même mécanisme Poisson/Lloyd que G3, rayons plus larges.
# hex(r≈110 km) ≈ 31 500 km² → ~20–30 zones sur ~650 000 km² de mer pilote.
G4_SEA_R_FLOOR_M = 100_000.0
G4_SEA_R_CEIL_M = 180_000.0
G4_SEA_MASTER_SEED = 20260726
G4_SEA_LLOYD_ITERATIONS = 10
G4_SEA_AREA_FLOOR_KM2 = 200.0
G4_SEA_AREA_CEIL_KM2 = 80_000.0
G4_SEA_COMPACTNESS_MIN = 0.12
# Détroit : deux terres non contiguës séparées par une mer plus étroite que ce seuil.
# Pas de Calais ≈ 33 km ; seuil déclaré 45 km (marge navigation / résolution NE).
G4_STRAIT_MAX_WIDTH_M = 45_000.0
G4_STRAIT_JUSTIFICATION = (
    "Seuil 45 km : le Pas de Calais (~33 km) doit produire un détroit ; "
    "au-delà, la mer n'est plus un franchissement tactique court (Manche large)."
)
G4_REGISTRY_CREATED = "2026-07-26"

# G5 — fleuves (Natural Earth rivers_lake_centerlines).
# Navigabilité dérivée UNIQUEMENT des attributs présents (pas de débit NE).
# scalerank = rang cartographique, PAS un débit ; d'où trois classes.
G5_NAV_SCALE_NAVIGABLE_MAX = 5  # scalerank <= 5 → navigable (proxy NE)
G5_NAV_SCALE_NON_NAV_MIN = 9  # scalerank >= 9 → non navigable
G5_RIVER_LAYER = "ne_10m_rivers_lake_centerlines"
G5_REGISTRY_CREATED = "2026-07-26"
# Grands fleuves nommés de la région pilote (contrôle à l'œil).
G5_NAMED_MAJOR_RIVERS = (
    "Seine",
    "Loire",
    "Rhône",
    "Garonne",
    "Rhin",
    "Meuse",
    "Escaut",
    "Tamise",
    "Severn",
)
# Tolérance intersection tronçon / cellule (m, projetés).
G5_INTERSECT_EPS_M = 1.0
# Portion mer : au-delà, un tronçon « en pleine mer » est une erreur de découpe.
G5_SEA_ONLY_FRACTION = 0.95
# Embouchure : NE arrête souvent le fleuve à quelques dizaines de m du trait de côte.
# Seuil déclaré (résolution NE), pas une invention hydrologique.
G5_MOUTH_SNAP_M = 250.0

# G5-ter — fusion ne_10m_rivers_europe (déjà dans 10m_physical.zip).
# Dédoublonnage : nom normalisé OU (Hausdorff ≤ borne ET couverture ≥ seuil).
# Borne obligatoire — pas de plus-proche-voisin sans limite (leçon v1_057).
G5C_EUROPE_LAYER = "ne_10m_rivers_europe"
G5C_DEDUP_HAUSDORFF_M = 500.0
G5C_DEDUP_COVERAGE_MIN = 0.5
G5C_REGISTRY_CREATED = "2026-07-26"

# G6 — relief Copernicus DEM GLO-90 (tuiles COG, ~90 m natifs).
# Échantillonnage DÉCLARÉ : grille lon/lat régulière, pas = 10 × résolution native
# (0.000833333…° ≈ 90 m → pas 0.008333…° ≈ 900 m). Pente via gradient central
# sur cette grille (mètres locaux). Rugosité = écart-type population des altitudes.
G6_DEM_NATIVE_DEG = 1.0 / 1200.0  # ≈ 0.000833333°
G6_SAMPLE_STRIDE_PX = 10
G6_SAMPLE_STEP_DEG = G6_DEM_NATIVE_DEG * G6_SAMPLE_STRIDE_PX
G6_EDGE_SAMPLE_STEP_M = 500.0  # densification frontière partagée (m projetés)
G6_ELEV_DECIMALS = 2  # m
G6_SLOPE_DECIMALS = 2  # degrés
G6_ROUGH_DECIMALS = 2  # m
# Échantillons DEM hors plage = artefact / carrière moderne (ex. Hambach ~−250 m)
# — exclus AVANT les stats, déclarés. Ne pas confondre avec les polders (~−7 m).
G6_SAMPLE_VALID_MIN_M = -80.0
G6_SAMPLE_VALID_MAX_M = 4800.0
# Plage plausible des stats cellule après filtrage (contrôle G6-C).
# Brief : refus −500 / 6000 ; bornes alignées sur le filtre d'échantillons.
G6_ELEV_PLAUSIBLE_MIN_M = -80.0
G6_ELEV_PLAUSIBLE_MAX_M = 4800.0
# Correspondance cols historiques : distance max (m) pour NOMMER (sinon id neutre).
G6_KNOWN_PASS_MATCH_M = 20_000.0
G6_REGISTRY_CREATED = "2026-07-26"
# Cols historiques connus dans / près de la fenêtre (lon, lat, nom). Pas inventés.
G6_KNOWN_PASSES = (
    ("pourtalet", "Col du Pourtalet", -0.417, 42.806),
    ("somport", "Col du Somport", -0.525, 42.796),
    ("ibañeta", "Col d'Ibañeta (Roncevaux)", -1.319, 43.020),
    ("puymorens", "Col de Puymorens", 1.833, 42.558),
    ("montgenevre", "Col de Montgenèvre", 6.724, 44.931),
    ("larche", "Col de Larche", 6.904, 44.422),
    ("petit_st_bernard", "Col du Petit-Saint-Bernard", 6.873, 45.680),
    ("faucille", "Col de la Faucille", 6.016, 46.367),
    ("jougne", "Col de Jougne", 6.388, 46.763),
)

# G3 — semis à espacement variable (villes / population → distance min).
# Fourchette élargie : bornée par r(x), pas par un quota de germes.
G3_SEED_COUNT_MIN = 150
G3_SEED_COUNT_MAX = 600  # FORGEHISTORY-G3-REPAIR (re-derived, Amendment 007a-R2: 400 -> 600)
G3_MASTER_SEED = 20260726
# Noyau d'influence urbaine : R en mètres projetés (LAEA). Cauchy.
G3_DENSITY_RADIUS_M = 55_000.0
G3_BASE_DENSITY = 0.12
# Champ r(x) : plancher / plafond de distance minimale entre germes (m).
# Hex(r=18 km) ≈ 280 km² ; hex(r=95 km) ≈ 7 800 km² — borne la maille.
G3_R_FLOOR_M = 18_000.0
G3_R_CEIL_M = 95_000.0
# Relaxation de Lloyd : itérations FIXES (déterminisme > convergence).
G3_LLOYD_ITERATIONS = 30  # FORGEHISTORY-G3-REPAIR (seeding param, Amendment 007a-R2: 10 -> 30, improves G3-G compactness convergence)
# Bornes de forme (contrôles G3-E/F/G) — dérivées de r(x).
# Plancher assoupli pour îles singleton (masse entière = 1 cellule).
G3_AREA_FLOOR_KM2 = 200.0
G3_AREA_CEIL_KM2 = 40_000.0  # FORGEHISTORY-G3-REPAIR (re-derived, Amendment 007a-R3: 15000 -> 40000)
G3_AREA_MAX_MEDIAN_RATIO = 8.0
# Plancher PP : les cellules côtières (trait NE fractal) descendent naturellement ;
# 0.18 interdit les lanières/échardes (v1_048 min 0.105) sans exiger un disque.
G3_COMPACTNESS_MIN = 0.18
# Tolérances couverture cellules vs terre (m²) — géométrie NE réelle.
G3_AREA_EPS_M2 = 10_000.0  # 0.01 km²
G3_OVERLAP_EPS_M2 = 10_000.0
# Bassin parisien (Île-de-France + abords) — boîte lon/lat pour métriques.
PARIS_BASIN_LONLAT = (1.4, 48.1, 3.5, 49.3)
# Flandre (côte à l'Escaut / Bruges–Gand–Anvers) — boîte lon/lat pour couverture.
FLANDERS_LONLAT = (2.3, 50.6, 4.5, 51.6)
G7_REGISTRY_CREATED = "2026-07-26"
# Date figée du registre (déterminisme ; pas d'horloge murale).
G3_REGISTRY_CREATED = "2026-07-26"
G3_REGISTRY_RETIRED = "2026-07-26"
G3_RETIRE_REASON = (
    "v1_049 mesh rebuild: density-by-spacing (Poisson r(x)) replaces "
    "coincidence seeding that produced shard rosettes"
)
# Plage d'ids retirés de la maille v1_048 (jamais réémis).
G3_RETIRED_ID_MIN = 1000
G3_RETIRED_ID_MAX = 1163

# Précision flottante déclarée (lon/lat et métriques).
FLOAT_DECIMALS = 6

# Coordonnées de jeu : mètres LAEA arrondis à l'entier (unités de jeu).
GAME_UNIT_SCALE = 1  # 1 unité = 1 mètre projeté

# Tolérances géométriques (unités projetées, mètres).
AREA_EPS = 1.0  # m²
LENGTH_EPS = 1.0  # m
OVERLAP_EPS = 1.0  # m²

TARGET_CRS = "EPSG:3035"
SOURCE_CRS = "EPSG:4326"

# Masque actuel du jeu (lecture seule, reproduction hors Unity).
# Voir MapSnapshotExporter : x = lon*cos(47.5°), y = -lat ; CellRadius = 1.20.
#
# Ces constantes alimentent la marge de la fenêtre pilote (PARTIE 1 / v1_096) :
# - la “demi-étendue province” vaut 2,5 × CellRadius
# - une province au bord exact a besoin d'une marge ≈ demi-étendue / 2
GAME_EQUIRECT_MID_LAT = 47.5
GAME_CELL_RADIUS = 1.20
GAME_CORRIDOR_HALF_WIDTH = 0.75

# ---------------------------------------------------------------------------
# Fenêtre pilote — CHOIX DE GAMEPLAY (un seul endroit, pas de copie).
# La fenêtre se dérive de province_coordinates.json (centroïdes des provinces)
# + une marge calculée à partir du rayon de cellule.
# ---------------------------------------------------------------------------
_GEO_ROOT = Path(__file__).resolve().parent  # FORGEHISTORY-PATH-ADJUSTMENT
_PROVINCE_COORDS_JSON = (
    _GEO_ROOT  # FORGEHISTORY-PATH-ADJUSTMENT
    / "legacy_game_data"  # FORGEHISTORY-PATH-ADJUSTMENT
    / "province_coordinates.json"  # FORGEHISTORY-PATH-ADJUSTMENT
)


def _load_province_coordinates() -> list[dict]:
    if not _PROVINCE_COORDS_JSON.exists():
        raise FileNotFoundError(f"province_coordinates.json absent: {_PROVINCE_COORDS_JSON}")
    doc = json.loads(_PROVINCE_COORDS_JSON.read_text(encoding="utf-8"))
    coords = doc.get("coordinates")
    if not isinstance(coords, list):
        raise ValueError("province_coordinates.json: clé 'coordinates' absente ou non-liste.")
    return coords


def _estimate_bbox_area_km2(w: float, s: float, e: float, n: float) -> float:
    """Estimation equirectangulaire (borne max raisonnable)."""
    lon_span = float(e - w)
    lat_span = float(n - s)
    km_per_deg_lat = 111_320.0
    km_per_deg_lon = 111_320.0 * math.cos(math.radians(GAME_EQUIRECT_MID_LAT))
    if lon_span <= 0.0 or lat_span <= 0.0:
        return 0.0
    return lon_span * km_per_deg_lon * lat_span * km_per_deg_lat


_PROVINCE_COORDS = _load_province_coordinates()


def _derive_pilot_window_lonlat() -> Tuple[float, float, float, float]:
    lons = [float(c["lon"]) for c in _PROVINCE_COORDS]
    lats = [float(c["lat"]) for c in _PROVINCE_COORDS]
    min_lon = min(lons)
    max_lon = max(lons)
    min_lat = min(lats)
    max_lat = max(lats)

    # Demi-étendue province ≈ 2,5 × CellRadius ; marge = demi-étendue / 2.
    margin_x = GAME_CELL_RADIUS * 1.25  # unités x/y (x=lon*cos, y=-lat)
    margin_lat = margin_x
    margin_lon = margin_x / math.cos(math.radians(GAME_EQUIRECT_MID_LAT))

    west = round(min_lon - margin_lon, FLOAT_DECIMALS)
    east = round(max_lon + margin_lon, FLOAT_DECIMALS)
    south = round(min_lat - margin_lat, FLOAT_DECIMALS)
    north = round(max_lat + margin_lat, FLOAT_DECIMALS)
    return west, south, east, north


PILOT_WEST, PILOT_SOUTH, PILOT_EAST, PILOT_NORTH = _derive_pilot_window_lonlat()
PILOT_WINDOW_LONLAT: Tuple[float, float, float, float] = (
    PILOT_WEST,
    PILOT_SOUTH,
    PILOT_EAST,
    PILOT_NORTH,
)

_min_lon_p = min(_PROVINCE_COORDS, key=lambda c: float(c["lon"]))
_max_lon_p = max(_PROVINCE_COORDS, key=lambda c: float(c["lon"]))
_min_lat_p = min(_PROVINCE_COORDS, key=lambda c: float(c["lat"]))
_max_lat_p = max(_PROVINCE_COORDS, key=lambda c: float(c["lat"]))
_margins_x = GAME_CELL_RADIUS * 1.25
_margins_lat = _margins_x
_margins_lon = _margins_x / math.cos(math.radians(GAME_EQUIRECT_MID_LAT))
PILOT_WINDOW_JUSTIFICATION = (
    "Choix de gameplay (pas un fait geographique) : fenêtre dérivée dynamiquement "
    "des centroïdes des provinces (province_coordinates.json) : bornes = min/max lon/lat "
    "+ marge calculée à partir du rayon de cellule (MapSnapshotExporter) : "
    f"marge_x = 1,25×CellRadius({GAME_CELL_RADIUS}) = {_margins_x:.6f} en unités x/y, "
    "conversion lon via x=lon*cos(mid_latitude) : "
    f"marge_lon = marge_x/cos({GAME_EQUIRECT_MID_LAT}) = {_margins_lon:.6f}, "
    "et marge_lat = marge_x. "
    "Bordures de référence (verifiables sur province_coordinates.json) : "
    f"min_lon={_min_lon_p['name']} lon={float(_min_lon_p['lon']):.6f} ; "
    f"max_lon={_max_lon_p['name']} lon={float(_max_lon_p['lon']):.6f} ; "
    f"min_lat={_min_lat_p['name']} lat={float(_min_lat_p['lat']):.6f} ; "
    f"max_lat={_max_lat_p['name']} lat={float(_max_lat_p['lat']):.6f}."
)

# Couches Natural Earth requises pour G2 (dans 10m_physical.zip).
G2_LAYERS = (
    "ne_10m_land",
    "ne_10m_coastline",
    "ne_10m_minor_islands",
    "ne_10m_lakes",
)

# Surface de terre plausible (km²) dans la fenêtre.
# Borne max dérivée de l'aire de bounding-box (equirectangulaire) : une erreur de
# projection sortirait de cette enveloppe.
_bbox_area_km2 = _estimate_bbox_area_km2(*PILOT_WINDOW_LONLAT)
G2_LAND_AREA_KM2_MIN = 0.0
G2_LAND_AREA_KM2_MAX = float(_bbox_area_km2)

# Chemins interdits pour l'export d'une fixture (données de jeu).
FORBIDDEN_GAME_PATH_MARKERS = (
    "StreamingAssets",
    "game_unity",
    "province_adjacency",
    "provinces.json",
)

# Nombre de semis cible (inclus) — G1 / G3, pas G2.
SEED_COUNT_MIN = 10
SEED_COUNT_MAX = 20

# (GAME_* déplacées plus haut : la fenêtre pilote en dépend)
