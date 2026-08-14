"""
Brief 018, SC3 — redessin d'un centre : l'agrégat change, les cellules ne sont
pas réécrites.

Scénario, entièrement en mémoire :
1. charger le monde réel et calculer l'appartenance A ;
2. relever l'empreinte des cellules (sérialisation canonique `World.to_dict()`
   à clés triées, et le contenu complet des attributs d'instance de chaque
   cellule) ;
3. déplacer, dans les enregistrements lus en mémoire, le centre de plus petit
   `id` sur la position exacte d'une cellule qui relève d'un autre centre ;
4. recalculer l'appartenance B ;
5. vérifier que l'agrégat a bougé et que rien d'autre n'a bougé.

Compteurs : redessin_change_agregat,
cellules_changeant_de_province_apres_redessin, redessin_cellules_intactes,
attributs_dynamiques_sur_cellules, fichier_centroides_inchange_apres_redessin.
"""

import dataclasses
import json
import pathlib

from sim.aggregation import (
    agregat_depuis_monde,
    appartenance_depuis_regroupements,
    charger_centres,
    charger_positions,
)
from sim.world import World

RNG_SEED = 42

_RACINE_DEPOT = pathlib.Path(__file__).parent.parent.parent
_CHEMIN_CENTRES = (
    _RACINE_DEPOT / "pipeline" / "geo" / "legacy_game_data" / "province_coordinates.json"
)


def _releve_attributs(monde) -> dict:
    """Contenu complet des attributs d'instance de chaque cellule."""
    return {cell_id: dict(vars(cellule)) for cell_id, cellule in monde.cells.items()}


def _champs_declares(cellule) -> set:
    return {champ.name for champ in dataclasses.fields(cellule)}


def test_redessin_change_agregat_sans_reecrire_les_cellules():
    """
    SC3 — les deux faits sont vérifiés ensemble : l'agrégat bouge ET les
    cellules ne bougent pas. Vérifier l'un sans l'autre ne prouverait rien.
    """
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()

    regroupements_a = agregat_depuis_monde(monde, positions=positions, centres=centres)
    appartenance_a = appartenance_depuis_regroupements(regroupements_a)

    serialisation_avant = json.dumps(monde.to_dict(), sort_keys=True)
    attributs_avant = _releve_attributs(monde)
    octets_avant = _CHEMIN_CENTRES.read_bytes()

    # Le centre de plus petit id est déplacé sur la position exacte d'une
    # cellule qui relève actuellement d'un autre centre : à distance nulle il
    # gagne, et une éventuelle égalité serait tranchée en sa faveur par D4.
    centre_deplace = min(centres, key=lambda centre: centre.id)
    cible = next(
        cell_id
        for cell_id in sorted(appartenance_a)
        if appartenance_a[cell_id] != centre_deplace.id
    )
    latitude_cible, longitude_cible = positions[cible]

    centres_redessines = [
        dataclasses.replace(centre, lat=latitude_cible, lon=longitude_cible)
        if centre.id == centre_deplace.id
        else centre
        for centre in centres
    ]

    regroupements_b = agregat_depuis_monde(
        monde, positions=positions, centres=centres_redessines
    )
    appartenance_b = appartenance_depuis_regroupements(regroupements_b)

    serialisation_apres = json.dumps(monde.to_dict(), sort_keys=True)
    attributs_apres = _releve_attributs(monde)
    octets_apres = _CHEMIN_CENTRES.read_bytes()

    cellules_changeant = sum(
        1 for cell_id in appartenance_a if appartenance_a[cell_id] != appartenance_b[cell_id]
    )
    redessin_change_agregat = int(cellules_changeant > 0)

    attributs_dynamiques = sum(
        1
        for cell_id, cellule in monde.cells.items()
        if set(vars(cellule)) - _champs_declares(cellule)
    )

    redessin_cellules_intactes = int(
        serialisation_avant == serialisation_apres and attributs_avant == attributs_apres
    )
    fichier_centroides_inchange = int(octets_avant == octets_apres)

    total = len(monde.cells)
    print(f"centre deplace : id={centre_deplace.id} nom={centre_deplace.name}")
    print(f"cellule cible : {cible} (relevait du centre {appartenance_a[cible]})")
    print(f"redessin_change_agregat = {redessin_change_agregat} / 1")
    print(f"cellules_changeant_de_province_apres_redessin = {cellules_changeant} / {total}")
    print(f"redessin_cellules_intactes = {redessin_cellules_intactes} / 1 ({total} cellules comparees)")
    print(f"attributs_dynamiques_sur_cellules = {attributs_dynamiques} / {total}")
    print(f"fichier_centroides_inchange_apres_redessin = {fichier_centroides_inchange} / 1")

    assert redessin_change_agregat == 1, (
        "Le redessin ne change aucune appartenance : la province n'est pas "
        "derivee, elle est figee."
    )
    assert cellules_changeant > 0
    assert appartenance_b[cible] == centre_deplace.id, (
        "A distance nulle, le centre deplace doit gagner."
    )
    assert attributs_dynamiques == 0, (
        "Au moins une cellule a acquis un attribut d'instance : "
        "l'appartenance a ete estampillee sur les cellules."
    )
    assert redessin_cellules_intactes == 1, (
        "La serialisation ou les attributs des cellules ont change pendant le "
        "redessin : la province a ete reecrite sur les habitants."
    )
    assert fichier_centroides_inchange == 1, (
        "Le fichier de centres a change sur le disque : le lot est en lecture "
        "seule sur les donnees geographiques."
    )


def test_redessin_naffecte_pas_les_enregistrements_lus():
    """
    SC3 — le redessin produit de nouveaux enregistrements de centres ; il ne
    réécrit pas ceux qui ont été lus du fichier.
    """
    centres = charger_centres()
    avant = [dataclasses.astuple(centre) for centre in centres]

    centre_deplace = min(centres, key=lambda centre: centre.id)
    redessines = [
        dataclasses.replace(centre, lat=centre.lat + 1.0, lon=centre.lon + 1.0)
        if centre.id == centre_deplace.id
        else centre
        for centre in centres
    ]

    apres = [dataclasses.astuple(centre) for centre in centres]
    modifies = sum(1 for a, b in zip(avant, apres) if a != b)
    rang = centres.index(centre_deplace)

    print(f"enregistrements_de_centres_modifies = {modifies} / {len(centres)}")
    assert modifies == 0
    assert redessines[rang] != centres[rang]
