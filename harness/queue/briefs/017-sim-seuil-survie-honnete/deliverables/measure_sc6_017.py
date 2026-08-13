"""
measure_sc6_017.py — Re-mesure des compteurs du monde réel après les
corrections du brief 017 (accumulateur de mortalité, critère de faim,
récupération physique du déficit).

Les corrections SC3, SC4 et SC5 changent légitimement les valeurs mesurées au
brief 013. Les archives des briefs 012 et 013 restent intactes : ce script
produit de NOUVEAUX compteurs, il ne réécrit rien.

Échantillon : World.from_g3(rng_seed=42), random.Random(42),
N = N_STAT_SURVIE ticks (horizon dérivé, voir sim/SEEDING.md SC1 brief 017).
Le monde est celui chargé par G3 — jamais un monde construit à la main.

`cellules_affamees_monde_reel_017` applique la définition SC4 du brief 017 :
une cellule est comptée affamée si elle a MANQUÉ de nourriture à au moins un
tick (hunger_ticks > 0 après ce tick), et non si son garde-manger est vide
après avoir mangé sa ration.

Usage (depuis la racine du dépôt) :
    .venv/bin/python harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/measure_sc6_017.py
"""

import pathlib
import random
import sys

# Ajouter la racine au PYTHONPATH si nécessaire
_root = pathlib.Path(__file__).resolve().parents[5]
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))

from sim.constants import (  # noqa: E402
    N_STAT_SURVIE,
    SURVIE_FRACTION_PREDITE_STATIONNAIRE,
    SURVIE_TOLERANCE_STATIONNAIRE,
)
from sim.engine import tick  # noqa: E402
from sim.world import World  # noqa: E402

RNG_SEED = 42
WORLD_SEED = 42


def main():
    world = World.from_g3(rng_seed=WORLD_SEED)
    rng = random.Random(RNG_SEED)

    nb_cellules = len(world.cells)
    nb_aretes = len(world.adjacency)
    pop_initiale = sum(c.population for c in world.cells.values())

    total_transported_all = 0.0
    cellules_affamees_ids = set()

    for _ in range(N_STAT_SURVIE):
        total_transported_all += tick(world, rng)
        for cid, cell in world.cells.items():
            # hunger_ticks n'est incrémenté que si la cellule a eu une pénurie
            # réelle à ce tick (SC4 brief 017).
            if cell.hunger_ticks > 0:
                cellules_affamees_ids.add(cid)

    pop_finale = sum(c.population for c in world.cells.values())

    cellules_affamees_monde_reel_017 = len(cellules_affamees_ids)
    morts_cumules_monde_reel_017 = pop_initiale - pop_finale
    kg_transportes_monde_reel_017 = round(total_transported_all)
    fraction_survie_monde_reel_017 = pop_finale / pop_initiale

    print(f"horizon N_STAT_SURVIE = {N_STAT_SURVIE} ticks")
    print(f"cellules chargées par G3 = {nb_cellules}")
    print(f"arêtes d'adjacence       = {nb_aretes}")
    print(f"pop_initiale = {pop_initiale}")
    print(f"pop_finale   = {pop_finale}")
    print()
    print(f"cellules_affamees_monde_reel_017 = {cellules_affamees_monde_reel_017}")
    print(f"  (dénominateur : {nb_cellules} cellules chargées ; condition : > 0)")
    print()
    print(f"morts_cumules_monde_reel_017 = {morts_cumules_monde_reel_017}")
    print(f"  (dénominateur : {pop_initiale} habitants initiaux ; condition : > 0)")
    print()
    print(f"kg_transportes_monde_reel_017 = {kg_transportes_monde_reel_017}")
    print(
        f"  (dénominateur : {nb_aretes} arêtes × {N_STAT_SURVIE} ticks "
        f"= {nb_aretes * N_STAT_SURVIE} occasions de transport ; condition : > 0)"
    )
    print()
    print(
        "fraction_survie_monde_reel_017 = "
        f"{round(fraction_survie_monde_reel_017, 6)}"
    )
    print(
        "  (fait observé, sans borne imposée par le brief ; "
        f"prédiction stationnaire = {round(SURVIE_FRACTION_PREDITE_STATIONNAIRE, 6)}, "
        f"tolérance = {round(SURVIE_TOLERANCE_STATIONNAIRE, 6)}, "
        f"écart = {round(abs(fraction_survie_monde_reel_017 - SURVIE_FRACTION_PREDITE_STATIONNAIRE), 6)})"
    )
    print()

    conditions = {
        "cellules_affamees_monde_reel_017": cellules_affamees_monde_reel_017 > 0,
        "morts_cumules_monde_reel_017": morts_cumules_monde_reel_017 > 0,
        "kg_transportes_monde_reel_017": kg_transportes_monde_reel_017 > 0,
    }
    echecs = [nom for nom, ok in conditions.items() if not ok]
    for nom in echecs:
        print(f"ERREUR : {nom} n'est pas > 0")

    if echecs:
        sys.exit(1)
    print("TOUTES LES CONDITIONS SC6 SONT SATISFAITES.")


if __name__ == "__main__":
    main()
