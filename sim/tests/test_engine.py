"""
SC5 — Boucle de tick déterministe.

Deux runs de N ticks (N ≥ 10) avec la même graine produisent
des états dont le condensé SHA256 est identique.
Compteur : ticks_deterministes_valides.
"""

import hashlib
import json
import random

from sim import engine
from sim.world import World

N_TICKS = 10
RNG_SEED = 42


def _run_n_ticks(seed: int) -> str:
    """Lance N_TICKS ticks et retourne le condensé SHA256 de l'état final."""
    world = World.from_g3(rng_seed=seed)
    rng = random.Random(seed)
    for _ in range(N_TICKS):
        engine.tick(world, rng)
    state = world.to_dict()
    return hashlib.sha256(json.dumps(state, sort_keys=True).encode()).hexdigest()


def test_tick_determinisme():
    """
    SC5 : deux runs avec la même graine donnent le même condensé SHA256.
    Les condensés sont affichés par leur nom de variable (hard-won rule 12).
    """
    hash_run_A = _run_n_ticks(RNG_SEED)
    hash_run_B = _run_n_ticks(RNG_SEED)

    print(f"hash_run_A = {hash_run_A}")
    print(f"hash_run_B = {hash_run_B}")
    print(f"égaux : {hash_run_A == hash_run_B}")

    ticks_deterministes_valides = 1 if hash_run_A == hash_run_B else 0
    print(f"ticks_deterministes_valides = {ticks_deterministes_valides}")

    assert hash_run_A == hash_run_B, "Les deux runs ont produit des états différents."
    assert ticks_deterministes_valides == 1


def test_tick_different_seeds_differ():
    """Contrôle : deux graines différentes donnent (en général) des états distincts."""
    hash_a = _run_n_ticks(1)
    hash_b = _run_n_ticks(2)
    assert hash_a != hash_b, "Deux graines différentes ont produit le même état final."
