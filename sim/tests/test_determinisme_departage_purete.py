"""
Brief 018, SC4 — fonction pure, déterminisme, départage stable.

Trois propriétés distinctes :
- **purete** : `derive_appartenance` ne modifie aucun objet reçu et n'écrit
  aucun fichier ;
- **determinisme** : deux appels sur les mêmes entrées, plus un troisième avec
  la liste des centres passée dans l'ordre inverse, rendent la même
  appartenance cellule par cellule ;
- **departage** : à distance exactement égale, le centre de plus petit `id`
  l'emporte, et ce dans les deux ordres de parcours possibles.

Compteurs : determinisme_agregation_deux_passes,
departage_egalite_plus_petit_id.
"""

import copy
import dataclasses
import json
import pathlib

from sim.aggregation import (
    CentreAdministratif,
    charger_centres,
    charger_latitude_moyenne,
    charger_positions,
    derive_appartenance,
    positions_du_monde,
)
from sim.world import World

RNG_SEED = 42

_RACINE_DEPOT = pathlib.Path(__file__).parent.parent.parent
_CHEMIN_CENTRES = (
    _RACINE_DEPOT / "pipeline" / "geo" / "legacy_game_data" / "province_coordinates.json"
)
_CHEMIN_CELLULES = _RACINE_DEPOT / "pipeline" / "geo" / "artifacts" / "cells_g3.json"

# Deux centres symétriques d'une cellule fabriquée : l'écart en longitude est
# strictement opposé, donc les carrés de distance sont exactement égaux.
_IDENTIFIANT_PETIT = 3
_IDENTIFIANT_GRAND = 7
_CELLULE_FABRIQUEE = 900001


def test_determinisme_agregation_deux_passes():
    """
    SC4 — trois appels, une seule appartenance : deux fois les mêmes entrées,
    puis les centres dans l'ordre inverse.

    Compteur : determinisme_agregation_deux_passes.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = positions_du_monde(monde, charger_positions())
    centres = charger_centres()
    latitude_moyenne = charger_latitude_moyenne()

    premiere = derive_appartenance(positions, centres, latitude_moyenne)
    deuxieme = derive_appartenance(positions, centres, latitude_moyenne)
    inverse = derive_appartenance(positions, list(reversed(centres)), latitude_moyenne)

    identiques = sum(
        1
        for cell_id in premiere
        if premiere[cell_id] == deuxieme[cell_id] == inverse[cell_id]
    )
    total = len(positions)
    determinisme_agregation_deux_passes = int(identiques == total and total > 0)

    print(f"cellules comparees = {total}")
    print(f"cellules identiques sur les trois appels = {identiques} / {total}")
    print(f"determinisme_agregation_deux_passes = {determinisme_agregation_deux_passes} / 1")

    assert determinisme_agregation_deux_passes == 1
    assert premiere == deuxieme == inverse


def test_departage_egalite_plus_petit_id():
    """
    SC4/D4 — deux centres exactement équidistants d'une cellule fabriquée : la
    cellule relève du plus petit `id`, dans les deux ordres de parcours.

    Un simple « premier arrivé, premier servi » passerait dans un ordre et
    échouerait dans l'autre : c'est précisément ce que ce test mesure.

    Compteur : departage_egalite_plus_petit_id.
    """
    latitude_moyenne = charger_latitude_moyenne()

    # La cellule est à l'origine du repère ; les deux centres sont à la même
    # latitude, de part et d'autre en longitude.
    positions = {_CELLULE_FABRIQUEE: (0.0, 0.0)}
    petit = CentreAdministratif(id=_IDENTIFIANT_PETIT, name="Petit", lon=1.0, lat=0.0)
    grand = CentreAdministratif(id=_IDENTIFIANT_GRAND, name="Grand", lon=-1.0, lat=0.0)

    ordres = [[petit, grand], [grand, petit]]
    gagnants = []
    for ordre in ordres:
        appartenance = derive_appartenance(positions, ordre, latitude_moyenne)
        gagnants.append(appartenance[_CELLULE_FABRIQUEE])

    departage_egalite_plus_petit_id = int(
        all(gagnant == _IDENTIFIANT_PETIT for gagnant in gagnants)
    )

    print(f"ordres essayes = {len(ordres)}")
    print(f"gagnants = {gagnants}")
    print(f"departage_egalite_plus_petit_id = {departage_egalite_plus_petit_id} / 1 "
          f"({len(ordres)} ordres x 1 cas synthetique)")

    assert departage_egalite_plus_petit_id == 1, (
        "Le departage depend de l'ordre de parcours : il est accidentel, pas stable."
    )


def test_departage_egalite_est_bien_une_egalite_exacte():
    """
    SC4/D4 — le cas synthétique est bien une égalité exacte de distance, pas
    une quasi-égalité que le hasard des flottants trancherait. Sans cela, le
    test précédent ne mesurerait pas le départage.
    """
    from sim.aggregation import facteur_de_projection, projeter

    facteur = facteur_de_projection(charger_latitude_moyenne())
    abscisse, ordonnee = projeter(0.0, 0.0, facteur)

    carres = []
    for longitude in (1.0, -1.0):
        centre_x, centre_y = projeter(0.0, longitude, facteur)
        carres.append((abscisse - centre_x) ** 2 + (ordonnee - centre_y) ** 2)

    print(f"carres de distance = {carres}")
    assert carres[0] == carres[1], "le cas synthetique n'est pas une egalite exacte"


def test_purete_agregation_ne_mute_pas_les_entrees():
    """
    SC4/D1 — `derive_appartenance` ne modifie aucun objet reçu et n'écrit
    aucun fichier. Comparaison avant / après sur des copies profondes, et
    comparaison des octets des deux fichiers lus par le module.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = positions_du_monde(monde, charger_positions())
    centres = charger_centres()
    latitude_moyenne = charger_latitude_moyenne()

    positions_temoin = copy.deepcopy(positions)
    centres_temoin = [dataclasses.astuple(centre) for centre in centres]
    serialisation_avant = json.dumps(monde.to_dict(), sort_keys=True)
    octets_centres_avant = _CHEMIN_CENTRES.read_bytes()
    octets_cellules_avant = _CHEMIN_CELLULES.read_bytes()

    derive_appartenance(positions, centres, latitude_moyenne)

    centres_apres = [dataclasses.astuple(centre) for centre in centres]
    serialisation_apres = json.dumps(monde.to_dict(), sort_keys=True)

    positions_mutees = sum(
        1 for cell_id in positions_temoin if positions[cell_id] != positions_temoin[cell_id]
    )
    centres_mutes = sum(
        1 for avant, apres in zip(centres_temoin, centres_apres) if avant != apres
    )
    fichiers_mutes = int(octets_centres_avant != _CHEMIN_CENTRES.read_bytes()) + int(
        octets_cellules_avant != _CHEMIN_CELLULES.read_bytes()
    )

    print(f"positions_mutees = {positions_mutees} / {len(positions_temoin)}")
    print(f"centres_mutes = {centres_mutes} / {len(centres_temoin)}")
    print(f"fichiers_lus_mutes = {fichiers_mutes} / 2")
    print(f"cellules_mutees_par_agregation = {int(serialisation_avant != serialisation_apres)} / 1")

    assert positions_mutees == 0
    assert centres_mutes == 0
    assert fichiers_mutes == 0
    assert len(positions) == len(positions_temoin)
    assert serialisation_avant == serialisation_apres
