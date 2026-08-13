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
    Scénario 1 (chaîne) : le monde à trois cellules simulé avec les arêtes
    dans l'ordre [1-2, 2-3] donne le même état final que simulé avec [2-3, 1-2].
    Scénario 2 (source contestée, N1 feedback 001) : une source dont le surplus
    est inférieur à la somme des besoins de deux receveurs donne la même
    allocation proportionnelle quelle que soit l'ordre des arêtes.
    Ce scénario rougit dès que le mécanisme de snapshot est retiré, même en
    conservant les définitions de besoin et surplus du brief 013.

test_recepteur_pas_sur_livre (N3 feedback 001) :
    Un receveur adjacent à plusieurs sources en surplus ne reçoit pas plus
    que son besoin snapshot (écrêtage côté receveur, brief 013 itération 2).
"""

import random

import pytest

from sim.constants import FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
from sim.engine import _apply_commerce, tick
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


def _build_contested_source_world(edge_order: list[dict]) -> World:
    """
    Construit un monde à trois cellules pour le scénario « source contestée ».
    - Cellule 10 (source S) : pop=0, stock=80 → surplus=80.
    - Cellule 11 (receveur A) : pop=50, stock=40 → besoin=60 (50×2-40).
    - Cellule 12 (receveur B) : pop=50, stock=40 → besoin=60.
    Surplus source (80) < somme des besoins (120) : allocation proportionnelle
    attendue → A reçoit 40, B reçoit 40, quel que soit l'ordre des arêtes.
    Sans snapshot (traitement arête-par-arête en direct), le premier receveur
    traité reçoit 60 et le second 20 selon l'ordre → ordre-dépendant.
    Production désactivée : area_km2 = 0.0.
    """
    pop_receveur = 50
    stock_receveur = 40
    source = Cell(
        cell_id=10,
        area_km2=0.0,
        population=0,
        food_stock_kg=80.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    recv_a = Cell(
        cell_id=11,
        area_km2=0.0,
        population=pop_receveur,
        food_stock_kg=float(stock_receveur),
        hunger_ticks=1,
        food_deficit_kg=0.0,
    )
    recv_b = Cell(
        cell_id=12,
        area_km2=0.0,
        population=pop_receveur,
        food_stock_kg=float(stock_receveur),
        hunger_ticks=1,
        food_deficit_kg=0.0,
    )
    return World(cells={10: source, 11: recv_a, 12: recv_b}, adjacency=edge_order)


def test_invariance_ordre_aretes():
    """
    SC2 — L'état final est invariant à l'ordre des arêtes dans adjacency.

    Scénario 1 — chaîne (existant) :
    Deux exécutions avec [1-2, 2-3] et [2-3, 1-2] doivent produire des
    stocks finaux identiques pour les trois cellules (écart ≤ 1×10⁻⁹ kg).

    Scénario 2 — source contestée (N1 feedback 001) :
    Source S surplus=80, receveurs A et B besoin=60 chacun (total 120 > 80).
    Allocation proportionnelle attendue : A=40, B=40 dans les deux ordres.
    Ce scénario rougit dès que le snapshot est retiré (traitement arête-par-arête
    en direct), indépendamment de la définition du besoin/surplus.

    Compteur : etat_final_invariant_ordre_aretes (max écart sur tous les tests).
    """
    # --- Scénario 1 : chaîne ---
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

    ecarts_s1 = {}
    for cell_id in [1, 2, 3]:
        ecart = abs(
            world_AB.cells[cell_id].food_stock_kg
            - world_BA.cells[cell_id].food_stock_kg
        )
        ecarts_s1[cell_id] = ecart
        print(
            f"[scénario 1] cellule {cell_id} : "
            f"stock_AB={world_AB.cells[cell_id].food_stock_kg}, "
            f"stock_BA={world_BA.cells[cell_id].food_stock_kg}, écart={ecart}"
        )

    # --- Scénario 2 : source contestée (test du mécanisme commerce seul) ---
    # Appelle _apply_commerce directement pour comparer les stocks APRÈS COMMERCE
    # mais AVANT CONSOMMATION. Ainsi, une livraison inégale entre les deux ordres
    # se manifeste immédiatement dans food_stock_kg sans être masquée par la
    # consommation (qui ramènerait les deux receveurs à 0 dans les deux cas).
    edges_SA_SB = [
        {"a": 10, "b": 11, "kind": "land", "shared_length_m": 5000.0},
        {"a": 10, "b": 12, "kind": "land", "shared_length_m": 5000.0},
    ]
    edges_SB_SA = [
        {"a": 10, "b": 12, "kind": "land", "shared_length_m": 5000.0},
        {"a": 10, "b": 11, "kind": "land", "shared_length_m": 5000.0},
    ]

    world_SA_SB = _build_contested_source_world(edges_SA_SB)
    world_SB_SA = _build_contested_source_world(edges_SB_SA)

    _apply_commerce(world_SA_SB, [0.0])
    _apply_commerce(world_SB_SA, [0.0])

    ecarts_s2 = {}
    for cell_id in [10, 11, 12]:
        ecart = abs(
            world_SA_SB.cells[cell_id].food_stock_kg
            - world_SB_SA.cells[cell_id].food_stock_kg
        )
        ecarts_s2[cell_id] = ecart
        print(
            f"[scénario 2] cellule {cell_id} : "
            f"stock_SA_SB={world_SA_SB.cells[cell_id].food_stock_kg}, "
            f"stock_SB_SA={world_SB_SA.cells[cell_id].food_stock_kg}, écart={ecart}"
        )

    etat_final_invariant_ordre_aretes = max(
        max(ecarts_s1.values()), max(ecarts_s2.values())
    )
    print(
        f"etat_final_invariant_ordre_aretes (max_ecart) = {etat_final_invariant_ordre_aretes}"
    )

    for cell_id, ecart in ecarts_s1.items():
        assert ecart <= TOLERANCE, (
            f"[scénario 1] cellule {cell_id} : écart {ecart} > {TOLERANCE} entre "
            f"ordre [1-2,2-3] et [2-3,1-2]. Dépendance à l'ordre des arêtes."
        )
    for cell_id, ecart in ecarts_s2.items():
        assert ecart <= TOLERANCE, (
            f"[scénario 2] cellule {cell_id} : écart {ecart} > {TOLERANCE} entre "
            f"ordre [S→A,S→B] et [S→B,S→A]. Dépendance à l'ordre (snapshot absent ?)."
        )


# ---------------------------------------------------------------------------
# N3 — test_recepteur_pas_sur_livre
# ---------------------------------------------------------------------------


def test_recepteur_pas_sur_livre():
    """
    N3 feedback 001 — Écrêtage côté receveur : un receveur adjacent à plusieurs
    sources en surplus ne reçoit pas plus que son besoin snapshot.

    Topologie : deux sources S1 et S2 (surplus ≥ besoin_R chacune) adjacentes
    au même receveur R.  Sans écrêtage, R recevrait 2 × besoin_R et terminerait
    le tick avec un stock positif. Avec écrêtage (N3 brief 013 itération 2),
    R reçoit exactement besoin_R, consomme tout, stock final = 0.

    Sans écrêtage (comportement attendu en échec) :
        R reçoit besoin_R de S1 + besoin_R de S2 = 2 × besoin_R.
        Stock final après consommation = besoin_R > 0.
    Avec écrêtage :
        R reçoit besoin_R au total (réparti entre S1 et S2).
        Stock final = 0.
    """
    pop_r = 100
    besoin_r = pop_r * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 200.0 kg

    source1 = Cell(
        cell_id=1, area_km2=0.0, population=0,
        food_stock_kg=besoin_r * 2,  # large surplus
        hunger_ticks=0, food_deficit_kg=0.0,
    )
    source2 = Cell(
        cell_id=2, area_km2=0.0, population=0,
        food_stock_kg=besoin_r * 2,
        hunger_ticks=0, food_deficit_kg=0.0,
    )
    receveur = Cell(
        cell_id=3, area_km2=0.0, population=pop_r,
        food_stock_kg=0.0,
        hunger_ticks=1, food_deficit_kg=0.0,
    )

    adjacency = [
        {"a": 1, "b": 3, "kind": "land", "shared_length_m": 5000.0},
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = World(cells={1: source1, 2: source2, 3: receveur}, adjacency=adjacency)

    rng = random.Random(0)
    tick(world, rng)

    stock_r_final = world.cells[3].food_stock_kg
    print(f"besoin_r = {besoin_r}")
    print(f"stock_receveur_apres_tick = {stock_r_final}")

    assert stock_r_final == 0.0, (
        f"Receveur sur-livré : stock final {stock_r_final} ≠ 0. "
        "L'écrêtage côté receveur n'a pas borné le total reçu à son besoin."
    )
