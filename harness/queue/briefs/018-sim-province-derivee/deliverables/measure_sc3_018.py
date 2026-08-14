"""
Brief 018, SC3 — redessin d'un centre : l'agrégat change, les cellules ne sont
pas réécrites.

Invocation, depuis la racine du dépôt :

    .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc3_018.py

Le scénario est monté entièrement en mémoire, sur le monde réel chargé par
`World.from_g3(rng_seed=42)`. Aucune écriture de fichier. Chaque compteur est
imprimé avec son dénominateur.

Sortie : code 0 si les conditions de SC3 tiennent, 1 sinon.
"""

import dataclasses
import json
import pathlib
import sys

_RACINE_DEPOT = pathlib.Path(__file__).resolve().parents[5]
sys.path.insert(0, str(_RACINE_DEPOT))

from sim.aggregation import (  # noqa: E402
    agregat_depuis_monde,
    appartenance_depuis_regroupements,
    charger_centres,
    charger_positions,
)
from sim.world import World  # noqa: E402

RNG_SEED = 42

_CHEMIN_CENTRES = (
    _RACINE_DEPOT / "pipeline" / "geo" / "legacy_game_data" / "province_coordinates.json"
)


def _releve_attributs(monde) -> dict:
    return {cell_id: dict(vars(cellule)) for cell_id, cellule in monde.cells.items()}


def main() -> int:
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()

    regroupements_a = agregat_depuis_monde(monde, positions=positions, centres=centres)
    appartenance_a = appartenance_depuis_regroupements(regroupements_a)

    serialisation_avant = json.dumps(monde.to_dict(), sort_keys=True)
    attributs_avant = _releve_attributs(monde)
    octets_avant = _CHEMIN_CENTRES.read_bytes()

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

    total = len(monde.cells)
    cellules_changeant = sum(
        1 for cell_id in appartenance_a if appartenance_a[cell_id] != appartenance_b[cell_id]
    )
    redessin_change_agregat = int(cellules_changeant > 0)
    attributs_dynamiques = sum(
        1
        for cellule in monde.cells.values()
        if set(vars(cellule)) - {champ.name for champ in dataclasses.fields(cellule)}
    )
    redessin_cellules_intactes = int(
        serialisation_avant == serialisation_apres and attributs_avant == attributs_apres
    )
    fichier_centroides_inchange = int(octets_avant == octets_apres)

    print("=== Brief 018, SC3 — redessin d'un centre administratif ===")
    print(f"monde mesure : World.from_g3(rng_seed={RNG_SEED})")
    print(f"centre deplace : id={centre_deplace.id} nom={centre_deplace.name}")
    print(f"deplace sur la position exacte de la cellule {cible}, "
          f"qui relevait du centre {appartenance_a[cible]}")
    print("")
    print(f"redessin_change_agregat = {redessin_change_agregat} / 1"
          "   (denominateur : 1 scenario de redessin)")
    print(f"cellules_changeant_de_province_apres_redessin = {cellules_changeant} / {total}"
          "   (denominateur : cellules chargees ; fait mesure, strictement positif attendu)")
    print(f"redessin_cellules_intactes = {redessin_cellules_intactes} / 1"
          f"   (denominateur : 1 comparaison portant sur {total} cellules serialisees)")
    print(f"attributs_dynamiques_sur_cellules = {attributs_dynamiques} / {total}"
          "   (denominateur : cellules chargees ; attributs d'instance compares aux champs declares)")
    print(f"fichier_centroides_inchange_apres_redessin = {fichier_centroides_inchange} / 1"
          "   (denominateur : 1 comparaison des octets du fichier de centres, avant / apres)")

    conditions = {
        "redessin_change_agregat == 1": redessin_change_agregat == 1,
        "cellules_changeant_de_province_apres_redessin > 0": cellules_changeant > 0,
        "la cellule cible releve desormais du centre deplace":
            appartenance_b[cible] == centre_deplace.id,
        "redessin_cellules_intactes == 1": redessin_cellules_intactes == 1,
        "attributs_dynamiques_sur_cellules == 0": attributs_dynamiques == 0,
        "fichier_centroides_inchange_apres_redessin == 1": fichier_centroides_inchange == 1,
    }

    print("")
    for libelle, tenue in conditions.items():
        print(f"  [{'OK' if tenue else 'ECHEC'}] {libelle}")

    return 0 if all(conditions.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
