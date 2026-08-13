"""
measure_sc6_013.py — Re-mesure des compteurs monde réel après corrections brief 013.

SC6 brief 013 : corrections SC1/SC2 changent les valeurs des compteurs 012.
Ce script produit les nouvelles valeurs sur World.from_g3(rng_seed=42),
random.Random(42), N=200 ticks.

Usage (depuis la racine du dépôt) :
    .venv/bin/python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
"""

import random
import sys
import pathlib

# Ajouter la racine au PYTHONPATH si nécessaire
_root = pathlib.Path(__file__).resolve().parents[5]
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))

from sim.constants import SEUIL_SURVIE_POPULATION_FRACTION
from sim.engine import tick
from sim.world import World

N_TICKS = 200
RNG_SEED = 42
WORLD_SEED = 42


def main():
    world = World.from_g3(rng_seed=WORLD_SEED)
    rng = random.Random(RNG_SEED)

    # Population initiale
    pop_initiale = sum(c.population for c in world.cells.values())

    # Simuler N ticks, accumuler total_transported et suivre hunger_ticks
    total_transported_all = 0.0
    cellules_affamees_ids = set()

    for _ in range(N_TICKS):
        transported = tick(world, rng)
        total_transported_all += transported
        for cid, cell in world.cells.items():
            if cell.hunger_ticks > 0:
                cellules_affamees_ids.add(cid)

    # Compteurs finaux
    pop_finale = sum(c.population for c in world.cells.values())

    cellules_affamees_monde_reel_re = len(cellules_affamees_ids)
    morts_cumules_monde_reel_re = pop_initiale - pop_finale
    kg_transportes_monde_reel_re = round(total_transported_all)
    fraction_survie_monde_reel_re = pop_finale / pop_initiale

    print(f"pop_initiale = {pop_initiale}")
    print(f"pop_finale   = {pop_finale}")
    print()
    print(f"cellules_affamees_monde_reel_re = {cellules_affamees_monde_reel_re}")
    print(f"  (sur {len(world.cells)} cellules chargées, condition : > 0)")
    print()
    print(f"morts_cumules_monde_reel_re = {morts_cumules_monde_reel_re}")
    print(f"  (condition : > 0)")
    print()
    print(f"kg_transportes_monde_reel_re = {kg_transportes_monde_reel_re}")
    print(f"  (condition : > 0)")
    print()
    print(f"fraction_survie_monde_reel_re = {round(fraction_survie_monde_reel_re, 6)}")
    print(f"  SEUIL_SURVIE_POPULATION_FRACTION = {SEUIL_SURVIE_POPULATION_FRACTION}")
    print(f"  condition : > {SEUIL_SURVIE_POPULATION_FRACTION}")
    print(f"  satisfaite : {fraction_survie_monde_reel_re > SEUIL_SURVIE_POPULATION_FRACTION}")
    print()

    # Vérifications
    ok = True
    if not (cellules_affamees_monde_reel_re > 0):
        print("ERREUR : cellules_affamees_monde_reel_re n'est pas > 0")
        ok = False
    if not (morts_cumules_monde_reel_re > 0):
        print("ERREUR : morts_cumules_monde_reel_re n'est pas > 0")
        ok = False
    if not (kg_transportes_monde_reel_re > 0):
        print("ERREUR : kg_transportes_monde_reel_re n'est pas > 0")
        ok = False
    if not (fraction_survie_monde_reel_re > SEUIL_SURVIE_POPULATION_FRACTION):
        print("ERREUR : fraction_survie_monde_reel_re n'est pas > SEUIL")
        ok = False

    if ok:
        print("TOUTES LES CONDITIONS SC6 SONT SATISFAITES.")
    else:
        sys.exit(1)


if __name__ == "__main__":
    main()
