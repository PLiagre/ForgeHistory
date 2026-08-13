"""
SC2 — Variabilité rng et déterminisme (brief 012).

Trois tests distincts :

1. test_rng_etat_change_apres_tick
   L'état interne du rng est différent après 10 ticks sur
   World.from_g3(rng_seed=42) — prouve que tick() consomme le rng.
   Compteur : rng_etat_change_apres_tick.

2. test_ticks_deterministes_meme_graine
   Deux runs de N=200 ticks avec world_seed=42 et rng_seed=42 produisent
   des condensés SHA256 égaux.
   Les condensés sont cités par nom de variable (hard-won rule 12).
   Compteur : ticks_deterministes_meme_graine.

3. test_ticks_differents_graines_rng_differentes
   Deux runs de N=200 ticks avec world_seed=42 mais rng_seed=42 et 999
   produisent des condensés différents — l'écart vient du chemin du tick.
   Compteur : ticks_differents_graines_rng_differentes.
"""

import hashlib
import json
import random

from sim import engine
from sim.world import World

N_TICKS_DETERMINISME = 200


def _run_n_ticks_digest(world_seed: int, rng_seed: int) -> str:
    """Lance N_TICKS_DETERMINISME ticks et retourne le condensé SHA256 de l'état final."""
    world = World.from_g3(rng_seed=world_seed)
    rng = random.Random(rng_seed)
    for _ in range(N_TICKS_DETERMINISME):
        engine.tick(world, rng)
    state = world.to_dict()
    return hashlib.sha256(json.dumps(state, sort_keys=True).encode()).hexdigest()


def test_rng_etat_change_apres_tick():
    """
    SC2.1 — Le rng est consommé à chaque tick.
    rng.getstate() avant ≠ rng.getstate() après 10 ticks.
    Compteur : rng_etat_change_apres_tick (True si état différent).
    """
    world = World.from_g3(rng_seed=42)
    rng = random.Random(42)

    etat_avant = rng.getstate()

    for _ in range(10):
        engine.tick(world, rng)

    etat_apres = rng.getstate()

    rng_etat_change_apres_tick = etat_avant != etat_apres
    print(f"etat_avant == etat_apres : {etat_avant == etat_apres}")
    print(f"rng_etat_change_apres_tick = {rng_etat_change_apres_tick}")

    assert rng_etat_change_apres_tick, (
        "Le rng n'a pas été consommé : son état est identique avant et après 10 ticks."
    )


def test_ticks_deterministes_meme_graine():
    """
    SC2.2 — Déterminisme à graine fixe.
    Deux runs de 200 ticks, world_seed=42 et rng_seed=42, donnent
    le même condensé SHA256. Condensés cités par nom (hard-won rule 12).
    Compteur : ticks_deterministes_meme_graine (True si condensés égaux).
    """
    hash_run_A = _run_n_ticks_digest(world_seed=42, rng_seed=42)
    hash_run_B = _run_n_ticks_digest(world_seed=42, rng_seed=42)

    print(f"hash_run_A = {hash_run_A}")
    print(f"hash_run_B = {hash_run_B}")
    print(f"égaux : {hash_run_A == hash_run_B}")

    ticks_deterministes_meme_graine = hash_run_A == hash_run_B
    print(f"ticks_deterministes_meme_graine = {ticks_deterministes_meme_graine}")

    assert ticks_deterministes_meme_graine, (
        "Les deux runs avec la même graine ont produit des condensés différents."
    )


def test_ticks_differents_graines_rng_differentes():
    """
    SC2.3 — Sensibilité à la graine rng.
    Deux runs de 200 ticks, world_seed=42, mais rng_seed=42 vs rng_seed=999 :
    les condensés doivent être différents (l'écart vient du tick, pas de
    l'amorçage seul).
    Compteur : ticks_differents_graines_rng_differentes (True si condensés différents).
    """
    hash_graine_42 = _run_n_ticks_digest(world_seed=42, rng_seed=42)
    hash_graine_999 = _run_n_ticks_digest(world_seed=42, rng_seed=999)

    print(f"hash_graine_42  = {hash_graine_42}")
    print(f"hash_graine_999 = {hash_graine_999}")
    print(f"différents : {hash_graine_42 != hash_graine_999}")

    ticks_differents_graines_rng_differentes = hash_graine_42 != hash_graine_999
    print(
        f"ticks_differents_graines_rng_differentes = "
        f"{ticks_differents_graines_rng_differentes}"
    )

    assert ticks_differents_graines_rng_differentes, (
        "Les deux runs avec des graines rng différentes ont produit le même condensé. "
        "Le rng n'influence pas le chemin du tick."
    )
