"""
Brief 018 — la Province dérivée : couverture, garde spatiale, couverture
d'écriture de la vue.

Ce fichier couvre :
- SC1 : chaque cellule chargée relève d'exactement une province ;
- SC1/D5 : une cellule sans position connue fait lever une erreur explicite,
  jamais une attribution par défaut ;
- SC2 : aucune dataclass de `sim.model` ni du module d'agrégation ne porte de
  champ dont le nom normalisé commence par `province` (découverte par
  introspection, jamais par une liste de noms écrite à la main) ;
- SC2 : la garde `_NoBadSpatialField` est exercée sur plusieurs variantes de
  nom interdit ;
- SC2 : chaque champ déclaré par le module d'agrégation a un site de
  construction ET un site de lecture dans le code de production ;
- SC4 : les égalités de distance observées sur le monde réel sont comptées.

Les compteurs sont imprimés : `-s` les montre.
"""

import ast
import dataclasses
import inspect
import json
import pathlib

import pytest

import sim.aggregation as agregation
import sim.model as modele
from sim.aggregation import (
    PositionCelluleInconnue,
    agregat_depuis_monde,
    appartenance_depuis_regroupements,
    charger_centres,
    charger_latitude_moyenne,
    charger_positions,
    facteur_de_projection,
    identifiant_de_province_de_cellule,
    nom_de_province_de_cellule,
    positions_du_monde,
    projeter,
    province_de_cellule,
    regroupements_non_vides,
)
from sim.model import _NoBadSpatialField
from sim.world import World

RNG_SEED = 42

_RACINE_DEPOT = pathlib.Path(__file__).parent.parent.parent
_CHEMIN_STATS = _RACINE_DEPOT / "pipeline" / "geo" / "artifacts" / "stats_g3.json"
_CHEMIN_CENTRES = (
    _RACINE_DEPOT / "pipeline" / "geo" / "legacy_game_data" / "province_coordinates.json"
)

_FICHIERS_PRODUCTION = [
    pathlib.Path(agregation.__file__),
    pathlib.Path(agregation.__file__).parent / "world.py",
]

# Préfixe interdit, dérivé de la garde elle-même : si la garde change de
# préfixe, ce test le suit au lieu de le contredire.
_PREFIXE_INTERDIT = _NoBadSpatialField._FORBIDDEN_PREFIX


def _dataclasses_du_module(module):
    """Découvre par introspection les dataclasses déclarées par un module."""
    trouvees = []
    for _nom, obj in inspect.getmembers(module, inspect.isclass):
        if dataclasses.is_dataclass(obj) and obj.__module__ == module.__name__:
            trouvees.append(obj)
    return trouvees


def _normaliser(nom: str) -> str:
    return nom.lower().replace("_", "")


def test_province_couverture_totale_monde_reel():
    """
    SC1 — sur le monde réel, chaque cellule chargée relève d'exactement une
    province. Aucun nombre de cellules n'est écrit en dur : tout est dérivé
    des fichiers.

    Compteurs : cellules_chargees_g3, centroides_lus, cellules_avec_province,
    cellules_sans_province, cellules_position_absente, provinces_non_vides.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()

    cell_count_fichier = json.loads(_CHEMIN_STATS.read_text(encoding="utf-8"))["cell_count"]
    centroides_fichier = len(
        json.loads(_CHEMIN_CENTRES.read_text(encoding="utf-8"))["coordinates"]
    )

    cellules_chargees_g3 = len(monde.cells)
    centroides_lus = len(centres)
    cellules_position_absente = sum(
        1 for cell_id in monde.cells if cell_id not in positions
    )

    regroupements = agregat_depuis_monde(monde, positions=positions, centres=centres)

    # « Exactement une » : ni zéro, ni deux. On compte les occurrences de
    # chaque cellule dans l'ensemble des regroupements.
    occurrences: dict = {}
    for regroupement in regroupements:
        for cell_id in regroupement.cell_ids:
            occurrences[cell_id] = occurrences.get(cell_id, 0) + 1

    cellules_avec_province = sum(
        1 for cell_id in monde.cells if occurrences.get(cell_id, 0) == 1
    )
    cellules_sans_province = sum(
        1 for cell_id in monde.cells if occurrences.get(cell_id, 0) == 0
    )
    cellules_en_double = sum(
        1 for cell_id in monde.cells if occurrences.get(cell_id, 0) > 1
    )
    provinces_non_vides = len(regroupements_non_vides(regroupements))

    print(f"cellules_chargees_g3 = {cellules_chargees_g3} / {cell_count_fichier} (cell_count de stats_g3.json)")
    print(f"centroides_lus = {centroides_lus} / {centroides_fichier} (longueur du tableau coordinates)")
    print(f"cellules_avec_province = {cellules_avec_province} / {cellules_chargees_g3}")
    print(f"cellules_sans_province = {cellules_sans_province} / {cellules_chargees_g3} (zero = mesure reelle, sentinelle = -1)")
    print(f"cellules_position_absente = {cellules_position_absente} / {cellules_chargees_g3}")
    print(f"cellules_en_double = {cellules_en_double} / {cellules_chargees_g3}")
    print(f"provinces_non_vides = {provinces_non_vides} / {centroides_lus} (fait mesure, aucun plancher exige)")

    assert cellules_chargees_g3 == cell_count_fichier
    assert centroides_lus == centroides_fichier
    assert cellules_position_absente == 0
    assert cellules_sans_province == 0
    assert cellules_en_double == 0
    assert cellules_avec_province == cellules_chargees_g3
    # D6 : le nombre de provinces peuplées est rapporté, jamais imposé. La
    # seule borne est celle du réel : il ne peut pas y en avoir plus que de
    # centres lus, et une couverture totale en impose au moins une.
    assert 0 < provinces_non_vides <= centroides_lus


def test_province_consultation_rend_le_centre_le_plus_proche():
    """
    SC1 — la consultation de production rend bien le centre le plus proche,
    re-dérivé indépendamment pour un échantillon de cellules.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()
    latitude_moyenne = charger_latitude_moyenne()
    facteur = facteur_de_projection(latitude_moyenne)

    regroupements = agregat_depuis_monde(monde, positions=positions, centres=centres)

    verifiees = 0
    for cell_id in sorted(monde.cells):
        latitude, longitude = positions[cell_id]
        abscisse, ordonnee = projeter(latitude, longitude, facteur)

        attendu = min(
            centres,
            key=lambda centre: (
                (abscisse - projeter(centre.lat, centre.lon, facteur)[0]) ** 2
                + (ordonnee - projeter(centre.lat, centre.lon, facteur)[1]) ** 2,
                centre.id,
            ),
        )
        assert identifiant_de_province_de_cellule(cell_id, regroupements) == attendu.id
        assert nom_de_province_de_cellule(cell_id, regroupements) == attendu.name
        verifiees += 1

    print(f"cellules_reverifiees_par_recalcul_independant = {verifiees} / {len(monde.cells)}")
    assert verifiees == len(monde.cells)


def test_province_refus_position_absente():
    """
    SC1/D5 — une cellule chargée sans position connue fait lever une erreur
    explicite qui nomme la cellule. Pas de province par défaut, pas d'écart
    silencieux.

    Compteur : refus_position_absente_leve.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()

    cellule_retiree = sorted(monde.cells)[0]
    positions_amputees = dict(positions)
    del positions_amputees[cellule_retiree]

    with pytest.raises(PositionCelluleInconnue) as capture:
        positions_du_monde(monde, positions_amputees)

    message = str(capture.value)
    refus_position_absente_leve = int(str(cellule_retiree) in message)

    print(f"cellule retiree en memoire : {cellule_retiree}")
    print(f"message : {message}")
    print(f"refus_position_absente_leve = {refus_position_absente_leve} / 1")

    assert refus_position_absente_leve == 1

    # L'agrégat complet refuse pour la même raison : le refus n'est pas
    # cantonné à la fonction interne.
    with pytest.raises(PositionCelluleInconnue):
        agregat_depuis_monde(monde, positions=positions_amputees)


def test_province_aucun_champ_province_sur_entites():
    """
    SC2 — par introspection : aucune dataclass de `sim.model` ni du module
    d'agrégation ne déclare de champ dont le nom normalisé commence par le
    préfixe interdit.

    Le contrôle ne nomme aucune classe : `Person`, `Family` et `Building`
    n'existent pas encore, et un contrôle nommé d'après sa cible les
    laisserait passer.

    Compteurs : champs_province_sur_entites, dataclasses_inspectees.
    """
    classes = _dataclasses_du_module(modele) + _dataclasses_du_module(agregation)

    champs_inspectes = 0
    fautifs = []
    for classe in classes:
        for champ in dataclasses.fields(classe):
            champs_inspectes += 1
            if _normaliser(champ.name).startswith(_PREFIXE_INTERDIT):
                fautifs.append(f"{classe.__name__}.{champ.name}")

    dataclasses_inspectees = len(classes)
    champs_province_sur_entites = len(fautifs)

    print(f"classes inspectees : {[c.__name__ for c in classes]}")
    print(f"prefixe interdit derive de la garde : {_PREFIXE_INTERDIT!r}")
    print(f"dataclasses_inspectees = {dataclasses_inspectees} / {dataclasses_inspectees}")
    print(f"champs_province_sur_entites = {champs_province_sur_entites} / {champs_inspectes}")

    assert dataclasses_inspectees > 0, "introspection vide : rien n'a ete controle"
    assert champs_inspectes > 0, "denominateur nul : aucun champ regarde"
    assert champs_province_sur_entites == 0, f"champs interdits : {fautifs}"


def test_province_garde_prefixe_variantes_rouges():
    """
    SC2 — la garde `_NoBadSpatialField` est exercée : chaque variante de nom
    interdit lève une `TypeError` citant l'ADR-0003.

    Compteur : garde_prefixe_variantes_rouges.
    """
    variantes = ["province_id", "ProvinceId", "province", "province_code", "provinceCode"]

    levees = 0
    for variante in variantes:
        classe = dataclasses.make_dataclass(
            "Sonde",
            [("cell_id", int), (variante, str)],
            bases=(_NoBadSpatialField,),
        )
        with pytest.raises(TypeError, match="ADR-0003"):
            classe(**{"cell_id": 1, variante: "X"})
        levees += 1

    print(f"variantes essayees : {variantes}")
    print(f"garde_prefixe_variantes_rouges = {levees} / {len(variantes)}")
    assert levees == len(variantes)


def _sites_de_construction(fichiers, nom_classe: str, champs: set) -> set:
    """Champs passés en argument nommé à un constructeur de la classe."""
    trouves: set = set()
    for chemin in fichiers:
        arbre = ast.parse(chemin.read_text(encoding="utf-8"), filename=str(chemin))
        for noeud in ast.walk(arbre):
            if not isinstance(noeud, ast.Call):
                continue
            fonction = noeud.func
            appelee = (
                fonction.id
                if isinstance(fonction, ast.Name)
                else getattr(fonction, "attr", None)
            )
            if appelee != nom_classe:
                continue
            for mot_cle in noeud.keywords:
                if mot_cle.arg in champs:
                    trouves.add(mot_cle.arg)
    return trouves


def _sites_de_lecture(fichiers, champs: set) -> set:
    """Champs lus (`objet.champ` en contexte Load) dans le code de production."""
    trouves: set = set()
    for chemin in fichiers:
        arbre = ast.parse(chemin.read_text(encoding="utf-8"), filename=str(chemin))
        for noeud in ast.walk(arbre):
            if (
                isinstance(noeud, ast.Attribute)
                and noeud.attr in champs
                and not isinstance(noeud.ctx, ast.Store)
            ):
                trouves.add(noeud.attr)
    return trouves


def test_province_champs_vue_couverts():
    """
    SC2 — mode d'échec n° 2 appliqué à la vue dérivée : chaque champ déclaré
    par le module d'agrégation a au moins un site de construction ET au moins
    un site de lecture dans le code de production.

    Les types sont découverts par introspection du module, jamais listés à la
    main. Un champ sans lecteur de production doit être supprimé.

    Compteur : champs_vue_couverts.
    """
    classes = _dataclasses_du_module(agregation)
    assert classes, "aucun type declare par le module d'agregation"

    champs_declares = 0
    champs_vue_couverts = 0
    manquants = []

    for classe in classes:
        champs = {champ.name for champ in dataclasses.fields(classe)}
        constructions = _sites_de_construction(_FICHIERS_PRODUCTION, classe.__name__, champs)
        lectures = _sites_de_lecture(_FICHIERS_PRODUCTION, champs)

        for champ in sorted(champs):
            champs_declares += 1
            if champ in constructions and champ in lectures:
                champs_vue_couverts += 1
            else:
                manquants.append(
                    f"{classe.__name__}.{champ} "
                    f"(construction={champ in constructions}, lecture={champ in lectures})"
                )

        print(f"{classe.__name__} : champs={sorted(champs)} construits={sorted(constructions)} lus={sorted(lectures & champs)}")

    print(f"fichiers de production : {[p.name for p in _FICHIERS_PRODUCTION]}")
    print(f"champs_vue_couverts = {champs_vue_couverts} / {champs_declares}")

    assert champs_declares > 0
    assert champs_vue_couverts == champs_declares, f"champs non couverts : {manquants}"


def test_province_egalites_de_distance_monde_reel():
    """
    SC4 — nombre de cellules du monde réel dont la distance minimale est
    atteinte par au moins deux centres (égalité exacte en flottant). Ce
    compteur peut légitimement valoir 0 : c'est un fait mesuré, pas une
    sentinelle.

    Compteur : egalites_de_distance_monde_reel.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()
    facteur = facteur_de_projection(charger_latitude_moyenne())

    centres_projetes = [
        (centre.id, *projeter(centre.lat, centre.lon, facteur)) for centre in centres
    ]

    egalites = 0
    for cell_id in sorted(monde.cells):
        latitude, longitude = positions[cell_id]
        abscisse, ordonnee = projeter(latitude, longitude, facteur)
        carres = [
            (abscisse - cx) ** 2 + (ordonnee - cy) ** 2 for _cid, cx, cy in centres_projetes
        ]
        minimum = min(carres)
        if carres.count(minimum) > 1:
            egalites += 1

    print(f"egalites_de_distance_monde_reel = {egalites} / {len(monde.cells)} (fait mesure, peut valoir 0)")
    assert egalites >= 0


def test_province_agregation_ne_reference_aucune_cellule_modifiable():
    """
    SC4 — la vue dérivée ne transporte que des identifiants : jamais un objet
    `Cell` modifiable. Un agrégat qui référencerait des cellules rouvrirait la
    porte à leur réécriture.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    regroupements = agregat_depuis_monde(monde)

    types_transportes = set()
    for regroupement in regroupements:
        for cell_id in regroupement.cell_ids:
            types_transportes.add(type(cell_id))

    print(f"types transportes par cell_ids : {sorted(t.__name__ for t in types_transportes)}")
    assert types_transportes == {int}
    assert province_de_cellule(sorted(monde.cells)[0], regroupements) is not None

    appartenance = appartenance_depuis_regroupements(regroupements)
    assert set(appartenance) == set(monde.cells)
