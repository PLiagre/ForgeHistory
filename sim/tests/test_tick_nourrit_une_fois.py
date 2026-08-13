"""
SC1 + SC2 brief 013 — Le tick nourrit une fois ; transport atomique.

test_ecart_temoin_vs_receveuse (SC1) :
    Une cellule receveuse qui reçoit exactement son besoin via commerce termine
    avec le même food_stock_kg qu'une cellule témoin ayant eu ce stock dès le
    départ. Mesure l'écart de stock, doit être ≤ 1×10⁻⁹ kg.

test_chaine_1_2_3 (SC2) :
    Dans une chaîne 1–2–3 (arêtes 1-2 et 2-3 seulement), après un tick, la
    cellule 3 ne reçoit aucune nourriture : la nourriture de la cellule 1 ne
    peut pas traverser deux arêtes en un tick (transport atomique).

test_invariance_ordre_aretes (SC2) :
    Le monde à trois cellules simulé avec les arêtes dans l'ordre [1-2, 2-3]
    donne le même état final que simulé avec l'ordre [2-3, 1-2].
    Prouve l'invariance à la permutation des arêtes (calcul sur snapshot).
"""

import random

import pytest

from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import tick
from sim.model import Cell
from sim.world import World

TOLERANCE = 1e-9


# ---------------------------------------------------------------------------
# SC1 — test_ecart_temoin_vs_receveuse
# ---------------------------------------------------------------------------


def _build_sc1_world(besoin_kg: float) -> tuple[World, int, int]:
    """
    Construit le micro-monde SC1 avec trois cellules :
    - Cellule 100 (témoin)  : food_stock_kg = besoin_kg, pas d'adjacence.
    - Cellule 101 (source)  : population = 0, food_stock_kg = besoin_kg.
    - Cellule 102 (receveuse): food_stock_kg = 0, food_deficit_kg = besoin_kg.
    Arête : 101 → 102 uniquement.
    Production désactivée : area_km2 = 0.0 sur toutes les cellules.
    Capacité de transport ≥ besoin_kg assurée par le paramètre (200 kg/arête).
    """
    # Nombre de personnes pour que besoin_kg = pop × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    pop_receveuse = 50  # besoin_kg = 50 × 2 = 100 kg

    temoin = Cell(
        cell_id=100,
        area_km2=0.0,
        population=pop_receveuse,
        food_stock_kg=besoin_kg,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    source = Cell(
        cell_id=101,
        area_km2=0.0,
        population=0,          # pas de consommation propre → surplus = stock
        food_stock_kg=besoin_kg,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    receveuse = Cell(
        cell_id=102,
        area_km2=0.0,
        population=pop_receveuse,
        food_stock_kg=0.0,
        hunger_ticks=0,
        food_deficit_kg=besoin_kg,
    )

    adjacency = [{"a": 101, "b": 102, "kind": "land", "shared_length_m": 5000.0}]
    world = World(
        cells={100: temoin, 101: source, 102: receveuse},
        adjacency=adjacency,
    )
    return world, 100, 102


def test_ecart_temoin_vs_receveuse():
    """
    SC1 — Le commerce avant consommation garantit qu'un kg transféré ne nourrit
    qu'une fois. L'écart de stock entre la cellule témoin (stock initial = besoin)
    et la cellule receveuse (stock = 0, reçoit besoin via commerce) doit être
    ≤ 1×10⁻⁹ kg après un tick complet (production désactivée).

    Compteur : ecart_stock_temoin_vs_receveuse.

    Note de conception : la cellule receveuse démarre avec food_deficit_kg =
    besoin_kg (déficit accumulé du tick précédent). Après le tick, son
    food_stock_kg final est le même que celui de la cellule témoin (tous deux
    ont consommé exactement leur besoin). Le food_deficit_kg peut différer
    (récupération graduelle sur le déficit accumulé de la receveuse — SC4),
    ce qui est cohérent avec la physique : seul le stock alimentaire du tick
    courant est équivalent, pas l'historique de famine.
    """
    besoin_kg = 50 * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 100.0 kg

    world, id_temoin, id_receveuse = _build_sc1_world(besoin_kg)
    rng = random.Random(0)  # graine fixe, production désactivée (area_km2=0)

    tick(world, rng)

    stock_temoin = world.cells[id_temoin].food_stock_kg
    stock_receveuse = world.cells[id_receveuse].food_stock_kg
    ecart_stock_temoin_vs_receveuse = abs(stock_temoin - stock_receveuse)

    print(f"stock_temoin    = {stock_temoin}")
    print(f"stock_receveuse = {stock_receveuse}")
    print(f"ecart_stock_temoin_vs_receveuse = {ecart_stock_temoin_vs_receveuse}")
    print(f"deficit_temoin    = {world.cells[id_temoin].food_deficit_kg}")
    print(f"deficit_receveuse = {world.cells[id_receveuse].food_deficit_kg}")

    assert ecart_stock_temoin_vs_receveuse <= TOLERANCE, (
        f"Écart de stock trop grand : {ecart_stock_temoin_vs_receveuse} > {TOLERANCE}. "
        f"La nourriture a peut-être été comptée deux fois."
    )


# ---------------------------------------------------------------------------
# SC2 — test_chaine_1_2_3
# ---------------------------------------------------------------------------


def _build_chain_world(edge_order: list[dict]) -> World:
    """
    Construit un monde à trois cellules en chaîne.
    - Cellule 1 : source (pop=0, large surplus).
    - Cellule 2 : intermédiaire (pop=100, stock=0, déficit=200).
    - Cellule 3 : terminus (pop=20, stock=0, déficit=200).
      La petite population de la cellule 3 (besoin=40 kg) garantit que si elle
      reçoit 200 kg via multi-saut (ancien comportement), son stock APRÈS
      consommation reste positif (160 kg), rendant l'invariance détectable.
    Production désactivée : area_km2 = 0.0.
    """
    pop_2 = 100
    pop_3 = 20
    besoin_2 = pop_2 * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK   # 200 kg
    deficit_3 = 200.0  # déficit accumulé > besoin courant (40 kg)

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=besoin_2 * 3,  # 600 kg : suffisant pour nourrir cellule 2
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_2 = Cell(
        cell_id=2,
        area_km2=0.0,
        population=pop_2,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=besoin_2,
    )
    cell_3 = Cell(
        cell_id=3,
        area_km2=0.0,
        population=pop_3,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=deficit_3,
    )
    return World(cells={1: cell_1, 2: cell_2, 3: cell_3}, adjacency=edge_order)


def test_chaine_1_2_3():
    """
    SC2 — Transport atomique : dans une chaîne 1–2–3, la cellule 3 (non
    adjacente à 1) ne reçoit aucune nourriture après 1 tick.
    La nourriture de la cellule 1 ne peut franchir qu'une arête par tick.

    Compteur : cellule_3_stock_apres_1_tick_chaine_1_2_3 = 0.0
    """
    edges = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = _build_chain_world(edges)
    rng = random.Random(0)

    tick(world, rng)

    cellule_3_stock_apres_1_tick_chaine_1_2_3 = world.cells[3].food_stock_kg
    print(
        f"cellule_3_stock_apres_1_tick_chaine_1_2_3 = "
        f"{cellule_3_stock_apres_1_tick_chaine_1_2_3}"
    )

    assert cellule_3_stock_apres_1_tick_chaine_1_2_3 == 0.0, (
        f"La cellule 3 a reçu de la nourriture en un tick via deux arêtes : "
        f"stock = {cellule_3_stock_apres_1_tick_chaine_1_2_3}. "
        "Le transport n'est pas atomique."
    )


# ---------------------------------------------------------------------------
# SC2 — test_invariance_ordre_aretes
# ---------------------------------------------------------------------------


def test_invariance_ordre_aretes():
    """
    SC2 — L'état final est invariant à l'ordre des arêtes dans adjacency.
    Deux exécutions avec [1-2, 2-3] et [2-3, 1-2] doivent produire des
    stocks finaux identiques pour les trois cellules (écart ≤ 1×10⁻⁹ kg).

    Compteur : etat_final_invariant_ordre_aretes (max écart sur 3 cellules).
    """
    edges_AB = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    edges_BA = [
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
    ]

    world_AB = _build_chain_world(edges_AB)
    world_BA = _build_chain_world(edges_BA)

    rng_AB = random.Random(0)
    rng_BA = random.Random(0)

    tick(world_AB, rng_AB)
    tick(world_BA, rng_BA)

    ecarts = {}
    for cell_id in [1, 2, 3]:
        ecart = abs(
            world_AB.cells[cell_id].food_stock_kg
            - world_BA.cells[cell_id].food_stock_kg
        )
        ecarts[cell_id] = ecart
        print(
            f"cellule {cell_id} : stock_AB={world_AB.cells[cell_id].food_stock_kg}, "
            f"stock_BA={world_BA.cells[cell_id].food_stock_kg}, écart={ecart}"
        )

    etat_final_invariant_ordre_aretes = max(ecarts.values())
    print(f"etat_final_invariant_ordre_aretes (max_ecart) = {etat_final_invariant_ordre_aretes}")

    for cell_id, ecart in ecarts.items():
        assert ecart <= TOLERANCE, (
            f"Cellule {cell_id} : écart de stock {ecart} > {TOLERANCE} entre "
            f"ordre [1-2,2-3] et [2-3,1-2]. L'état final dépend de l'ordre des arêtes."
        )
