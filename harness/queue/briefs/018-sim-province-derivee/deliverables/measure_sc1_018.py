"""
Brief 018, SC1 — couverture de l'agrégation dérivée sur le monde réel.

Invocation, depuis la racine du dépôt :

    .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc1_018.py

Chaque compteur est imprimé avec son dénominateur. Le monde mesuré est celui
que charge `World.from_g3(rng_seed=42)` — jamais un monde construit à la main,
jamais un monde à zéro cellule.

Sortie : code 0 si les conditions de SC1 tiennent, 1 sinon.
"""

import json
import pathlib
import sys

_RACINE_DEPOT = pathlib.Path(__file__).resolve().parents[5]
sys.path.insert(0, str(_RACINE_DEPOT))

from sim.aggregation import (  # noqa: E402
    PositionCelluleInconnue,
    agregat_depuis_monde,
    charger_centres,
    charger_positions,
    positions_du_monde,
    regroupements_non_vides,
)
from sim.world import World  # noqa: E402

RNG_SEED = 42

_CHEMIN_STATS = _RACINE_DEPOT / "pipeline" / "geo" / "artifacts" / "stats_g3.json"
_CHEMIN_CENTRES = (
    _RACINE_DEPOT / "pipeline" / "geo" / "legacy_game_data" / "province_coordinates.json"
)


def main() -> int:
    monde = World.from_g3(rng_seed=RNG_SEED)
    positions = charger_positions()
    centres = charger_centres()
    regroupements = agregat_depuis_monde(monde, positions=positions, centres=centres)

    cell_count_fichier = json.loads(_CHEMIN_STATS.read_text(encoding="utf-8"))["cell_count"]
    centroides_fichier = len(
        json.loads(_CHEMIN_CENTRES.read_text(encoding="utf-8"))["coordinates"]
    )

    cellules_chargees_g3 = len(monde.cells)
    centroides_lus = len(centres)

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
    cellules_position_absente = sum(
        1 for cell_id in monde.cells if cell_id not in positions
    )
    provinces_non_vides = len(regroupements_non_vides(regroupements))

    # Refus de deviner (D5) : une position retirée en mémoire doit faire lever
    # une erreur explicite nommant la cellule.
    cellule_retiree = sorted(monde.cells)[0]
    positions_amputees = dict(positions)
    del positions_amputees[cellule_retiree]
    try:
        positions_du_monde(monde, positions_amputees)
        refus_position_absente_leve = 0
        message_refus = "AUCUNE ERREUR — le code a devine"
    except PositionCelluleInconnue as exc:
        message_refus = str(exc)
        refus_position_absente_leve = int(str(cellule_retiree) in message_refus)

    print("=== Brief 018, SC1 — couverture de l'agregation derivee ===")
    print(f"monde mesure : World.from_g3(rng_seed={RNG_SEED})")
    print("")
    print(f"cellules_chargees_g3 = {cellules_chargees_g3} / {cell_count_fichier}"
          "   (denominateur : cell_count lu dans pipeline/geo/artifacts/stats_g3.json)")
    print(f"centroides_lus = {centroides_lus} / {centroides_fichier}"
          "   (denominateur : longueur du tableau coordinates, lue du fichier)")
    print(f"cellules_avec_province = {cellules_avec_province} / {cellules_chargees_g3}"
          "   (denominateur : cellules chargees ; couverture totale attendue)")
    print(f"cellules_sans_province = {cellules_sans_province} / {cellules_chargees_g3}"
          "   (denominateur : cellules chargees ; zero est une mesure reelle, la sentinelle est -1)")
    print(f"cellules_position_absente = {cellules_position_absente} / {cellules_chargees_g3}"
          "   (denominateur : cellules chargees)")
    print(f"cellules_en_double = {cellules_en_double} / {cellules_chargees_g3}"
          "   (denominateur : cellules chargees ; 'exactement une' exclut aussi le deux)")
    print(f"provinces_non_vides = {provinces_non_vides} / {centroides_lus}"
          "   (denominateur : centroides lus ; fait mesure, aucun plancher exige — D6)")
    print(f"refus_position_absente_leve = {refus_position_absente_leve} / 1"
          f"   (cas synthetique : position de la cellule {cellule_retiree} retiree en memoire)")
    print(f"  message du refus : {message_refus}")

    conditions = {
        "cellules_chargees_g3 == cell_count du fichier": cellules_chargees_g3 == cell_count_fichier,
        "centroides_lus == longueur du tableau coordinates": centroides_lus == centroides_fichier,
        "cellules_avec_province == cellules_chargees_g3": cellules_avec_province == cellules_chargees_g3,
        "cellules_sans_province == 0": cellules_sans_province == 0,
        "cellules_en_double == 0": cellules_en_double == 0,
        "cellules_position_absente == 0": cellules_position_absente == 0,
        "0 < provinces_non_vides <= centroides_lus": 0 < provinces_non_vides <= centroides_lus,
        "refus_position_absente_leve == 1": refus_position_absente_leve == 1,
    }

    print("")
    for libelle, tenue in conditions.items():
        print(f"  [{'OK' if tenue else 'ECHEC'}] {libelle}")

    return 0 if all(conditions.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
