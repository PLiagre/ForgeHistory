#!/usr/bin/env python3
"""
Fabrique `data/world-1400.json` — la carte figée du jeu (ADR-0018).

Le jeu ne rejoue plus la chaîne géographique. Il lit un seul fichier,
versionné dans le dépôt, produit ici une fois à partir des artefacts de
`tools/map/artifacts/`. Ce script ne se lance que si on refait la carte.

Ce qu'il assemble, par cellule :

  - la géométrie et la superficie (étape G3) ;
  - l'adjacence entre cellules (étape G3) ;
  - le **relief en cinq classes** (étape G6) — plaine, colline, montagne,
    haute montagne, marais. Pas des mètres : le jeu n'a pas besoin d'une
    altitude au mètre près, il a besoin de savoir si on est en montagne ;
  - les déterminants du climat (étape C1) ;
  - les gisements connus de 1400 (étape R1).

Niveau de fidélité (ADR-0018) : le trait de côte, le relief et les
gisements nommés sont de **niveau 1** — justes dans les grandes lignes.
Tout le reste que le jeu déduira de cette carte est de **niveau 2** :
plausible, généré, jamais sourcé.

Usage :
    python tools/map/build_world.py
    python tools/map/build_world.py --verifier   # sans réécrire le fichier
"""
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
ARTEFACTS = Path(__file__).resolve().parent / "artifacts"
SORTIE = RACINE / "data" / "world-1400.json"

VERSION_CARTE = "world-1400-v1"

# --- Le relief, en cinq classes ---------------------------------------------
#
# Bornes en mètres d'altitude moyenne de la cellule. Ordres de grandeur
# plausibles, pas des seuils sourcés (ADR-0018, niveau 2) : ce qui compte
# est qu'une plaine ne soit pas classée montagne, pas la valeur exacte.

MARAIS_ALTITUDE_MAX_M = 5.0
MARAIS_PENTE_MAX_DEG = 1.0
PLAINE_ALTITUDE_MAX_M = 200.0
COLLINE_ALTITUDE_MAX_M = 600.0
MONTAGNE_ALTITUDE_MAX_M = 1200.0

# Une pente forte fait une montagne même à altitude moyenne modeste
# (une vallée encaissée n'est pas une plaine).
PENTE_MONTAGNE_MIN_DEG = 12.0

CLASSES_RELIEF = ("marais", "plaine", "colline", "montagne", "haute_montagne")


def classe_de_relief(altitude_moyenne_m: float | None, pente_moyenne_deg: float | None) -> str:
    """Range une cellule dans l'une des cinq classes de relief."""
    if altitude_moyenne_m is None:
        return "plaine"
    pente = pente_moyenne_deg if pente_moyenne_deg is not None else 0.0
    if altitude_moyenne_m < MARAIS_ALTITUDE_MAX_M and pente < MARAIS_PENTE_MAX_DEG:
        return "marais"
    if pente >= PENTE_MONTAGNE_MIN_DEG:
        return "haute_montagne" if altitude_moyenne_m >= MONTAGNE_ALTITUDE_MAX_M else "montagne"
    if altitude_moyenne_m < PLAINE_ALTITUDE_MAX_M:
        return "plaine"
    if altitude_moyenne_m < COLLINE_ALTITUDE_MAX_M:
        return "colline"
    if altitude_moyenne_m < MONTAGNE_ALTITUDE_MAX_M:
        return "montagne"
    return "haute_montagne"


def _charger(nom: str) -> dict:
    chemin = ARTEFACTS / nom
    if not chemin.is_file():
        raise SystemExit(f"Artefact manquant : {chemin}")
    return json.loads(chemin.read_text(encoding="utf-8"))


def construire() -> dict:
    g3 = _charger("cells_g3.json")
    adjacence = _charger("adjacency_g3.json")
    relief = _charger("cells_relief_g6.json")
    climat = _charger("cells_climate_drivers_c1.json")
    gisements = _charger("resources_1400_r1.json")

    relief_par_cellule = {c["cell_id"]: c for c in relief["cells"]}
    climat_par_cellule = {c["cell_id"]: c for c in climat["cells"]}

    gisements_par_cellule: dict[int, list[dict]] = {}
    for depot in gisements["deposits"]:
        gisements_par_cellule.setdefault(depot["cell_id"], []).append(
            {
                "id": depot["id"],
                "nom": depot["name"],
                "ressource": depot["resource"],
                "richesse": depot["richness_class"],
            }
        )

    cellules = []
    for brute in g3["cells"]:
        cid = brute["cell_id"]
        r = relief_par_cellule.get(cid, {})
        c = climat_par_cellule.get(cid, {})
        cellules.append(
            {
                "cell_id": cid,
                "area_km2": brute["area_km2"],
                "centroid": brute["centroid"],
                "geometry": brute["geometry"],
                "relief": classe_de_relief(r.get("elev_mean_m"), r.get("slope_mean_deg")),
                "climat": {
                    "littoral": c.get("coastal"),
                    "distance_mer_m": c.get("dist_sea_edge_m"),
                    "distance_mer_centroide_m": c.get("dist_sea_centroid_m"),
                    "sauts_jusqu_a_la_mer": c.get("hops_to_sea"),
                    "insolation_annuelle_mj_m2": c.get("insolation_annual_mj_m2"),
                    "duree_jour_solstice_ete_h": c.get("daylight_h_summer_solstice"),
                    "duree_jour_solstice_hiver_h": c.get("daylight_h_winter_solstice"),
                },
                "gisements": sorted(
                    gisements_par_cellule.get(cid, []), key=lambda d: d["id"]
                ),
            }
        )

    cellules.sort(key=lambda c: c["cell_id"])

    return {
        "version": VERSION_CARTE,
        "produite_le": datetime.now(timezone.utc).strftime("%Y-%m-%d"),
        "produite_par": "tools/map/build_world.py",
        "fidelite": (
            "Niveau 1 (juste dans les grandes lignes) pour le trait de côte, "
            "le relief et les gisements nommés. Tout ce que le jeu déduit "
            "d'ici est de niveau 2 : plausible, jamais sourcé. Voir ADR-0018."
        ),
        "crs": g3["crs"],
        "projection": g3["projection"],
        "versions_pipeline": {
            "g3_cellules": g3["pipeline_version"],
            "g3_adjacence": adjacence["pipeline_version"],
            "g6_relief": relief["pipeline_version"],
            "c1_climat": climat["pipeline_version"],
            "r1_gisements": gisements["pipeline_version"],
        },
        "classes_relief": list(CLASSES_RELIEF),
        "cellules": cellules,
        "adjacence": sorted(
            adjacence["adjacency"], key=lambda e: (e["a"], e["b"])
        ),
    }


def serialiser(carte: dict) -> str:
    """
    JSON stable, une cellule (et une arête) par ligne.

    Indenter tout le document donnerait 230 000 lignes ; tout compacter en
    donnerait une seule, illisible en revue. Une ligne par entrée garde un
    diff lisible quand la carte change.
    """
    listes = ("adjacence", "cellules")
    morceaux = []
    for cle in sorted(carte):
        valeur = carte[cle]
        if cle in listes:
            entrees = ",\n  ".join(
                json.dumps(item, ensure_ascii=False, sort_keys=True) for item in valeur
            )
            morceaux.append(f' {json.dumps(cle)}: [\n  {entrees}\n ]')
        else:
            morceaux.append(
                f' {json.dumps(cle)}: '
                + json.dumps(valeur, ensure_ascii=False, sort_keys=True, indent=1).replace("\n", "\n ")
            )
    return "{\n" + ",\n".join(morceaux) + "\n}\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verifier", action="store_true",
                        help="compare avec le fichier existant sans le réécrire")
    args = parser.parse_args(argv)

    carte = construire()
    texte = serialiser(carte)

    repartition: dict[str, int] = {}
    for cellule in carte["cellules"]:
        repartition[cellule["relief"]] = repartition.get(cellule["relief"], 0) + 1
    nb_gisements = sum(len(c["gisements"]) for c in carte["cellules"])

    print(f"{len(carte['cellules'])} cellules, {len(carte['adjacence'])} arêtes, "
          f"{nb_gisements} gisements")
    print("relief : " + ", ".join(f"{k}={repartition.get(k, 0)}" for k in CLASSES_RELIEF))

    if args.verifier:
        if not SORTIE.is_file():
            print(f"ABSENT : {SORTIE}", file=sys.stderr)
            return 1
        actuel = SORTIE.read_text(encoding="utf-8")
        # La date de production ne fait pas partie de la comparaison.
        neuf = json.loads(texte)
        ancien = json.loads(actuel)
        neuf.pop("produite_le", None)
        ancien.pop("produite_le", None)
        if neuf != ancien:
            print("La carte versionnée diffère de ce que produit ce script.", file=sys.stderr)
            return 1
        print("La carte versionnée est à jour.")
        return 0

    SORTIE.parent.mkdir(parents=True, exist_ok=True)
    SORTIE.write_text(texte, encoding="utf-8", newline="\n")
    print(f"OK: {SORTIE} ({SORTIE.stat().st_size // 1024} Kio)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
