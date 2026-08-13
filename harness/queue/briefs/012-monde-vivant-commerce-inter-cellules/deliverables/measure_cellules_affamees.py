"""
Script de mesure du compteur cellules_affamees_monde_reel.
World.from_g3(rng_seed=42), N=200 ticks, rng_seed=42.
Affiche le nombre de cellules ayant eu hunger_ticks > 0 au moins une fois.
À lancer depuis la racine du dépôt.
"""
import pathlib
import sys
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent.parent.parent.parent.parent))

import random
from sim.world import World
from sim.engine import tick

N = 200
world = World.from_g3(rng_seed=42)
rng = random.Random(42)

cellules_avec_faim = set()
for _ in range(N):
    tick(world, rng)
    for cid, cell in world.cells.items():
        if cell.hunger_ticks > 0:
            cellules_avec_faim.add(cid)

print(len(cellules_avec_faim))
