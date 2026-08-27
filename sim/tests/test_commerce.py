"""
Le commerce est physique : rien ne se téléporte, rien ne nourrit deux fois.

Ce que ce fichier protège (ADR-0018) :
  - invariant physique : conservation stricte de la masse au transport ;
    les kg transportés sont exactement les kg arrivés ;
  - règle de jeu visible : un kg transféré ne nourrit qu'une fois, ne
    traverse qu'une arête par tick, et un receveur n'est jamais sur-livré ;
  - déterminisme : l'état final ne dépend pas de l'ordre des arêtes.

Fusion des anciens fichiers commerce, kg_transportes_est_arrives et
tick_nourrit_une_fois.
"""

import random
import pytest
from sim.engine import _apply_commerce, _apply_consumption, _apply_production, tick
from sim.model import Cell
from sim.world import World
from sim.constants import (
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    MARCHANDISE_NOURRITURE,
    TICK_DURATION_DAYS,
    TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
)
from sim.engine import _apply_commerce
TOLERANCE = 1e-9
def _run_commerce_ecart(world: World) -> float:
    """Lance _apply_commerce et retourne l'écart entre kg comptés et kg arrivés."""
    stocks_avant = {cid: c.food_stock_kg for cid, c in world.cells.items()}
    total_transported = [0.0]
    _apply_commerce(world, total_transported)

    somme_deltas_positifs = sum(
        max(0.0, world.cells[cid].food_stock_kg - stocks_avant[cid])
        for cid in world.cells
    )
    return total_transported[0], somme_deltas_positifs
from sim.engine import _apply_commerce, tick
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


# --- test_commerce.py ---
def test_deficit_accumule_quand_manque():
    """
    SC3 + SC4 — food_deficit_kg est écrit (> 0) quand la consommation
    dépasse le stock initial disponible sur un tick complet.

    Cellule construite à la main, area_km2 = 1.0 km² (≥ minimum G3).
    Consommation = population × 2 kg/tick >> stock initial.
    """
    # Cellule avec grand population et petit stock : déficit garanti
    # area_km2 = 1.0 : production max ≈ 1.0 × 18 × 1.5 = 27 kg
    # population = 1000 : consommation = 1000 × 2 = 2000 kg >> stock initial = 10 kg
    cell = Cell(
        cell_id=1,
        area_km2=1.0,
        population=1000,
        food_stock_kg=10.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    world = World(cells={1: cell}, adjacency=[])
    rng = random.Random(42)

    tick(world, rng)

    food_deficit_kg_ecrit_quand_manque = world.cells[1].food_deficit_kg > 0
    print(f"food_deficit_kg après tick : {world.cells[1].food_deficit_kg}")
    print(f"food_deficit_kg_ecrit_quand_manque = {food_deficit_kg_ecrit_quand_manque}")

    assert food_deficit_kg_ecrit_quand_manque, (
        f"food_deficit_kg devrait être > 0 après un tick de famine : "
        f"obtenu {world.cells[1].food_deficit_kg}"
    )


# --- test_commerce.py ---
def test_conservation_masse_transport():
    """
    SC4 — Conservation stricte de la masse lors du commerce.

    Mini-monde de 2 cellules adjacentes :
    - Cellule A : surplus (food_stock_kg > 0, food_deficit_kg = 0)
    - Cellule B : déficit (food_stock_kg = 0, food_deficit_kg > 0)

    Vérifie que sum(food_stock_kg) avant = sum(food_stock_kg) après,
    à 1×10⁻⁹ kg près.

    Conservation : la masse totale ne doit pas changer.
    """
    cell_a = Cell(
        cell_id=1,
        area_km2=10.0,
        population=50,
        food_stock_kg=5000.0,
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_b = Cell(
        cell_id=2,
        area_km2=10.0,
        population=200,
        food_stock_kg=0.0,
        hunger_ticks=5,
        food_deficit_kg=300.0,
    )

    # Arête d'adjacence entre A et B (format G3 : champs 'a' et 'b')
    adjacency = [{"a": 1, "b": 2, "kind": "land", "shared_length_m": 10000.0}]
    world = World(cells={1: cell_a, 2: cell_b}, adjacency=adjacency)

    somme_avant = sum(c.food_stock_kg for c in world.cells.values())

    total_transported = [0.0]
    _apply_commerce(world, total_transported)

    somme_apres = sum(c.food_stock_kg for c in world.cells.values())

    ecart = abs(somme_avant - somme_apres)
    tolerance = 1e-9

    conservation_masse_transport = ecart <= tolerance
    print(f"somme_avant = {somme_avant}")
    print(f"somme_apres = {somme_apres}")
    print(f"écart = {ecart}")
    print(f"conservation_masse_transport = {conservation_masse_transport}")
    print(f"kg_transportes = {total_transported[0]}")

    assert conservation_masse_transport, (
        f"La masse n'est pas conservée : avant={somme_avant}, après={somme_apres}, "
        f"écart={ecart} (tolérance={tolerance})"
    )
    assert total_transported[0] > 0, (
        "Aucun kilogramme transporté alors qu'une cellule avait un déficit et "
        "l'autre un surplus."
    )


# --- test_kg_transportes_est_arrives.py ---
def test_kg_transportes_egal_deltas_positifs():
    """
    SC5 / N2 — Topologie chaîne : seul le receveur direct (cellule 2) reçoit
    de la nourriture. La cellule 3 (non adjacente à la source) ne reçoit rien
    via snapshot. total_transported = kg reçus par cellule 2, écart nul.

    Sans le snapshot (ancien comportement 012) : cellule 2 recevrait 200 kg
    (CAPACITY) et en redistribuerait 100 kg à cellule 3 dans le même tick ;
    total_transported compterait 300 kg alors que seuls 200 kg ont quitté une
    source — sur-comptage de 100 kg. Ce test rougirait.

    Monde : cellule 1 (source, pop=0, stock=500), cellule 2 (pop=50, stock=0,
    besoin_snapshot=100), cellule 3 (pop=50, stock=0, besoin_snapshot=100).
    Arêtes : 1-2 et 2-3.

    Compteur : ecart_kg_transportes_vs_arrives.
    """
    pop = 50
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 100 kg

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=500.0,  # grande réserve
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_2 = Cell(
        cell_id=2,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=500.0,  # grand déficit accumulé
    )
    cell_3 = Cell(
        cell_id=3,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=500.0,
    )

    adjacency = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 2, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = World(cells={1: cell_1, 2: cell_2, 3: cell_3}, adjacency=adjacency)

    transported, arrived = _run_commerce_ecart(world)
    ecart_kg_transportes_vs_arrives = abs(transported - arrived)

    print(f"total_transported = {transported}")
    print(f"somme_deltas_positifs (kg arrivés) = {arrived}")
    print(f"ecart_kg_transportes_vs_arrives = {ecart_kg_transportes_vs_arrives}")

    assert transported > 0, (
        "Aucune nourriture transportée alors que cellule 2 avait besoin."
    )
    assert ecart_kg_transportes_vs_arrives <= TOLERANCE, (
        f"Écart entre total_transported ({transported}) et "
        f"kg arrivés ({arrived}) = {ecart_kg_transportes_vs_arrives} > {TOLERANCE}. "
        "Double comptage possible (transport non atomique)."
    )


# --- test_kg_transportes_est_arrives.py ---
def test_kg_transportes_etoile():
    """
    SC5 (cas supplémentaire) — Topologie étoile : une source, deux receveurs.
    Aucun kg ne peut traverser deux arêtes dans cette topologie.
    Le test vérifie que total_transported = kg reçus par les deux receveurs.
    """
    pop = 100
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK  # 200 kg

    cell_1 = Cell(
        cell_id=1,
        area_km2=0.0,
        population=0,
        food_stock_kg=besoin * 3,  # peut nourrir les deux
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    cell_2 = Cell(
        cell_id=2,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=besoin,
    )
    cell_3 = Cell(
        cell_id=3,
        area_km2=0.0,
        population=pop,
        food_stock_kg=0.0,
        hunger_ticks=1,
        food_deficit_kg=besoin,
    )

    adjacency = [
        {"a": 1, "b": 2, "kind": "land", "shared_length_m": 5000.0},
        {"a": 1, "b": 3, "kind": "land", "shared_length_m": 5000.0},
    ]
    world = World(cells={1: cell_1, 2: cell_2, 3: cell_3}, adjacency=adjacency)

    transported, arrived = _run_commerce_ecart(world)
    ecart = abs(transported - arrived)

    print(f"[étoile] total_transported = {transported}")
    print(f"[étoile] somme_deltas_positifs = {arrived}")
    print(f"[étoile] ecart = {ecart}")

    assert transported > 0
    assert ecart <= TOLERANCE


# --- test_tick_nourrit_une_fois.py ---
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


# --- test_tick_nourrit_une_fois.py ---
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


# --- test_tick_nourrit_une_fois.py ---
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

# --- Brief 039 : commerce généralisé ---

# Nom local au test : pas de constante moteur (brief 039, risque test_aucune_constante_terminale).
_MARCHANDISE_ESSAI_SC3 = "__essai_commerce_039__"
_CONSOMMATION_ESSAI_SC3 = 1.0 * TICK_DURATION_DAYS


def _patch_consommation_essai(monkeypatch):
    """Monkeypatch du seul accès nommé pour la marchandise d'essai SC3."""
    from sim import constants as k

    original = k.consommation_kg_par_habitant_par_tick

    def _patched(marchandise: str) -> float:
        if marchandise == _MARCHANDISE_ESSAI_SC3:
            return _CONSOMMATION_ESSAI_SC3
        return original(marchandise)

    monkeypatch.setattr(k, "consommation_kg_par_habitant_par_tick", _patched)


def test_maillon_commerce_sans_nom_nourriture():
    """SC2 — L'AST du maillon commerce ne nomme plus la marchandise alimentaire."""
    import ast
    import pathlib

    engine_file = pathlib.Path(__file__).parent.parent / "engine.py"
    tree = ast.parse(engine_file.read_text(encoding="utf-8"), filename=str(engine_file))
    fonctions = {
        node.name
        for node in tree.body
        if isinstance(node, ast.FunctionDef) and node.name.startswith("_apply_commerce")
    }
    assert fonctions, "maillon commerce introuvable"

    occurrences = 0
    for node in ast.walk(tree):
        if not isinstance(node, ast.FunctionDef) or node.name != "_apply_commerce":
            continue
        for sub in ast.walk(node):
            if isinstance(sub, ast.Constant) and sub.value == "nourriture":
                occurrences += 1
            elif isinstance(sub, ast.Attribute) and sub.attr == "MARCHANDISE_NOURRITURE":
                occurrences += 1
    assert occurrences == 0, (
        f"occurrences_nourriture_dans_le_maillon={occurrences} ; "
        f"fonctions_du_maillon={len(fonctions)}"
    )


def _build_sc3_micro_monde() -> tuple[World, int, int]:
    """Deux cellules adjacentes : source et receveuse pour deux marchandises."""
    from sim.model import ecrire_stock_marchandise

    pop = 50
    source = Cell(
        cell_id=201,
        area_km2=0.0,
        population=0,
        stocks={MARCHANDISE_NOURRITURE: 200.0, _MARCHANDISE_ESSAI_SC3: 200.0},
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    receveuse = Cell(
        cell_id=202,
        area_km2=0.0,
        population=pop,
        stocks={},
        hunger_ticks=0,
        food_deficit_kg=0.0,
    )
    adjacency = [{"a": 201, "b": 202, "kind": "land", "shared_length_m": 1000.0}]
    return World(cells={201: source, 202: receveuse}, adjacency=adjacency), 201, 202


def test_marchandise_essai_circule_sans_ligne_supplementaire(monkeypatch):
    """SC3 — Une marchandise d'essai consommée circule via l'accès nommé seul."""
    from sim.model import lire_stock_marchandise

    _patch_consommation_essai(monkeypatch)
    world, _src, rcv = _build_sc3_micro_monde()
    stock_avant = lire_stock_marchandise(world.cells[rcv], _MARCHANDISE_ESSAI_SC3)
    assert stock_avant == -1.0

    _apply_commerce(world, [0.0])

    stock_apres = lire_stock_marchandise(world.cells[rcv], _MARCHANDISE_ESSAI_SC3)
    assert stock_apres > 0.0, (
        "La marchandise d'essai n'a pas circulé sans ligne ajoutée au maillon."
    )


def test_capacite_arete_partagee_entre_marchandises(monkeypatch):
    """SC3 — Sur une arête saturée, la somme des transferts égale la capacité lue."""
    from sim.model import ecrire_stock_marchandise, lire_stock_marchandise

    _patch_consommation_essai(monkeypatch)
    pop = 100
    besoin_alim = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    besoin_essai = pop * _CONSOMMATION_ESSAI_SC3

    source = Cell(
        cell_id=301, area_km2=0.0, population=0,
        stocks={MARCHANDISE_NOURRITURE: 500.0, _MARCHANDISE_ESSAI_SC3: 500.0},
        hunger_ticks=0, food_deficit_kg=0.0,
    )
    receveuse = Cell(
        cell_id=302, area_km2=0.0, population=pop,
        stocks={},
        hunger_ticks=0, food_deficit_kg=0.0,
    )
    adjacency = [{"a": 301, "b": 302, "kind": "land", "shared_length_m": 1000.0}]
    world = World(cells={301: source, 302: receveuse}, adjacency=adjacency)

    _apply_commerce(world, [0.0])

    stock_alim_avant = 0.0
    stock_essai_avant = 0.0
    delta_alim = max(
        0.0,
        lire_stock_marchandise(world.cells[302], MARCHANDISE_NOURRITURE) - stock_alim_avant,
    )
    delta_essai = max(
        0.0,
        lire_stock_marchandise(world.cells[302], _MARCHANDISE_ESSAI_SC3) - stock_essai_avant,
    )
    somme_transferts = delta_alim + delta_essai
    capacite = TRADE_CAPACITY_KG_PER_EDGE_PER_TICK

    # La généralisation est réelle : sans elle, sur le moteur de base, la
    # marchandise d'essai ne circule pas (delta_essai == 0) et ce contrôle
    # échoue. C'est le rouge prouvé avant correction (SC3).
    assert delta_essai > 0, (
        f"marchandise d'essai non transportée : delta_essai={delta_essai} ; "
        f"la généralisation du commerce est absente"
    )
    assert somme_transferts <= capacite + TOLERANCE, (
        f"somme_transferts_sur_arete_partagee={somme_transferts} > capacite={capacite}"
    )
    assert abs(somme_transferts - capacite) <= TOLERANCE, (
        f"somme_transferts={somme_transferts} != capacite={capacite} ; "
        f"besoins additionnés={besoin_alim + besoin_essai}"
    )


def test_conservation_masse_par_marchandise(monkeypatch):
    """SC5 — Le commerce déplace sans créer ni détruire, par marchandise."""
    from sim.model import lire_stock_marchandise

    _patch_consommation_essai(monkeypatch)
    world, _, _ = _build_sc3_micro_monde()
    for marchandise in (MARCHANDISE_NOURRITURE, _MARCHANDISE_ESSAI_SC3):
        somme_avant = sum(
            max(0.0, lire_stock_marchandise(c, marchandise))
            for c in world.cells.values()
        )
        copie = World(
            cells={cid: Cell(
                cell_id=c.cell_id,
                area_km2=c.area_km2,
                population=c.population,
                stocks=dict(c.stocks),
                hunger_ticks=c.hunger_ticks,
                food_deficit_kg=c.food_deficit_kg,
                mortality_remainder=c.mortality_remainder,
            ) for cid, c in world.cells.items()},
            adjacency=list(world.adjacency),
        )
        _apply_commerce(copie, [0.0])
        somme_apres = sum(
            max(0.0, lire_stock_marchandise(c, marchandise))
            for c in copie.cells.values()
        )
        # SC5 : identité au bit près, sans tolérance — le commerce déplace,
        # il ne crée ni ne détruit. Aucun seuil n'absorbe un écart.
        assert somme_apres == somme_avant, (
            f"ecart_de_masse pour {marchandise!r} : "
            f"avant={somme_avant!r} après={somme_apres!r}"
        )


def test_commerce_ne_modifie_pas_food_deficit(monkeypatch):
    """SC6 — Le maillon commerce ne touche jamais food_deficit_kg."""
    _patch_consommation_essai(monkeypatch)
    world, _, _ = _build_sc3_micro_monde()
    deficits_avant = {cid: c.food_deficit_kg for cid, c in world.cells.items()}
    _apply_commerce(world, [0.0])
    for cid, avant in deficits_avant.items():
        assert world.cells[cid].food_deficit_kg == avant, (
            f"modification_de_dette cellule {cid}"
        )


def test_ordre_insertion_paniers_invariant(monkeypatch):
    """SC7 — Deux ordres d'insertion des paniers donnent le même résultat."""
    from sim.model import ecrire_stock_marchandise, lire_stock_marchandise

    _patch_consommation_essai(monkeypatch)

    def _monde(ordre: list[str]) -> World:
        pop = 40
        a = Cell(
            cell_id=401, area_km2=0.0, population=0,
            stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
        )
        b = Cell(
            cell_id=402, area_km2=0.0, population=pop,
            stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
        )
        for cle in ordre:
            ecrire_stock_marchandise(a, cle, 300.0)
            ecrire_stock_marchandise(b, cle, 0.0)
        adjacency = [{"a": 401, "b": 402, "kind": "land", "shared_length_m": 1000.0}]
        return World(cells={401: a, 402: b}, adjacency=adjacency)

    w1 = _monde([MARCHANDISE_NOURRITURE, _MARCHANDISE_ESSAI_SC3])
    w2 = _monde([_MARCHANDISE_ESSAI_SC3, MARCHANDISE_NOURRITURE])
    _apply_commerce(w1, [0.0])
    _apply_commerce(w2, [0.0])

    for marchandise in (MARCHANDISE_NOURRITURE, _MARCHANDISE_ESSAI_SC3):
        s1 = lire_stock_marchandise(w1.cells[402], marchandise)
        s2 = lire_stock_marchandise(w2.cells[402], marchandise)
        assert abs(s1 - s2) <= TOLERANCE, (
            f"ordre d'insertion change le résultat pour {marchandise!r}: {s1} vs {s2}"
        )

# --- Brief 040 : capacité d'arête selon le relief ---

def _classes_relief_depuis_carte() -> list[str]:
    """Les cinq classes de relief dérivées de World.lire_carte()."""
    from sim import constants as k

    carte_doc = World.lire_carte()
    reliefs = {
        cell.get("relief")
        for cell in carte_doc.get("cellules", [])
        if cell.get("relief") is not None
    }
    attendues = set(k.facteurs_transport_par_relief())
    manquantes = attendues - reliefs
    assert not manquantes, f"classes de relief absentes de la carte : {sorted(manquantes)}"
    return sorted(attendues, key=lambda r: k.facteurs_transport_par_relief()[r], reverse=True)


def _build_micro_monde_relief_transport() -> tuple[World, int, dict[str, int]]:
    """
    Micro-monde SC1 : une source en plaine, une receveuse par classe de relief.
    Retourne le monde, l'id source et le mapping relief → cell_id receveuse.
    """
    from sim.model import ecrire_stock_marchandise

    classes = _classes_relief_depuis_carte()
    source_id = 9000
    pop = 100
    besoin = pop * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    stock_source = besoin * len(classes) * 10

    carte: dict[int, dict] = {
        source_id: {"cell_id": source_id, "relief": "plaine"},
    }
    cells = {
        source_id: Cell(
            cell_id=source_id,
            area_km2=0.0,
            population=0,
            stocks={},
            hunger_ticks=0,
            food_deficit_kg=0.0,
        ),
    }
    ecrire_stock_marchandise(cells[source_id], MARCHANDISE_NOURRITURE, stock_source)

    receveuses: dict[str, int] = {}
    adjacency: list[dict] = []
    for idx, relief in enumerate(classes):
        cid = source_id + idx + 1
        receveuses[relief] = cid
        carte[cid] = {"cell_id": cid, "relief": relief}
        cells[cid] = Cell(
            cell_id=cid,
            area_km2=0.0,
            population=pop,
            stocks={},
            hunger_ticks=0,
            food_deficit_kg=0.0,
        )
        adjacency.append(
            {"a": source_id, "b": cid, "kind": "land", "shared_length_m": 1000.0}
        )

    world = World(cells=cells, adjacency=adjacency, carte=carte)
    return world, source_id, receveuses


def _transfert_vers(world: World, receveuse_id: int) -> float:
    from sim.model import lire_stock_marchandise

    stock_avant = lire_stock_marchandise(world.cells[receveuse_id], MARCHANDISE_NOURRITURE)
    stock_avant = stock_avant if stock_avant >= 0 else 0.0
    copie_cells = {
        cid: Cell(
            cell_id=c.cell_id,
            area_km2=c.area_km2,
            population=c.population,
            stocks=dict(c.stocks),
            hunger_ticks=c.hunger_ticks,
            food_deficit_kg=c.food_deficit_kg,
            mortality_remainder=c.mortality_remainder,
        )
        for cid, c in world.cells.items()
    }
    w = World(cells=copie_cells, adjacency=list(world.adjacency), carte=dict(world.carte))
    from sim.engine import _initialiser_capacite_aretes
    cap = _initialiser_capacite_aretes(w)
    _apply_commerce(w, [0.0], MARCHANDISE_NOURRITURE, cap)
    stock_apres = lire_stock_marchandise(w.cells[receveuse_id], MARCHANDISE_NOURRITURE)
    stock_apres = stock_apres if stock_apres >= 0 else 0.0
    return stock_apres - stock_avant


def test_cinq_facteurs_transport_suivent_ordre_strict():
    """SC1 — Cinq transferts distincts, ordre strict des facteurs de transport."""
    from sim import constants as k

    world, _source_id, receveuses = _build_micro_monde_relief_transport()
    facteurs = k.facteurs_transport_par_relief()
    classes = _classes_relief_depuis_carte()
    transferts = [_transfert_vers(world, receveuses[r]) for r in classes]

    for i in range(len(transferts) - 1):
        assert transferts[i] > transferts[i + 1] + TOLERANCE, (
            f"ordre des transferts violé entre {classes[i]} ({transferts[i]}) "
            f"et {classes[i + 1]} ({transferts[i + 1]}) ; facteurs={facteurs}"
        )

    distincts = len({round(t, 6) for t in transferts})
    assert distincts == len(classes), (
        f"transferts non distincts : {dict(zip(classes, transferts))}"
    )


def _build_monde_arete_relief(relief_a: str, relief_b: str) -> World:
    from sim.model import ecrire_stock_marchandise

    a_id, b_id = 9100, 9101
    pop = 100
    carte = {
        a_id: {"cell_id": a_id, "relief": relief_a},
        b_id: {"cell_id": b_id, "relief": relief_b},
    }
    source = Cell(
        cell_id=a_id, area_km2=0.0, population=0,
        stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
    )
    receveuse = Cell(
        cell_id=b_id, area_km2=0.0, population=pop,
        stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
    )
    ecrire_stock_marchandise(source, MARCHANDISE_NOURRITURE, 10000.0)
    adjacency = [{"a": a_id, "b": b_id, "kind": "land", "shared_length_m": 1000.0}]
    return World(cells={a_id: source, b_id: receveuse}, adjacency=adjacency, carte=carte)


def test_goulot_relief_min_commande_capacite():
    """SC2 — Le bout le plus difficile commande ; sens de lecture invariant."""
    from sim import constants as k

    base = TRADE_CAPACITY_KG_PER_EDGE_PER_TICK
    facteurs = k.facteurs_transport_par_relief()

    t_plaine_plaine = _transfert_vers(_build_monde_arete_relief("plaine", "plaine"), 9101)
    t_plaine_hm = _transfert_vers(_build_monde_arete_relief("plaine", "haute_montagne"), 9101)
    t_hm_hm = _transfert_vers(_build_monde_arete_relief("haute_montagne", "haute_montagne"), 9101)
    t_hm_plaine = _transfert_vers(_build_monde_arete_relief("haute_montagne", "plaine"), 9101)

    cap_plaine_hm = base * min(facteurs["plaine"], facteurs["haute_montagne"])
    cap_hm_hm = base * facteurs["haute_montagne"]
    cap_plaine_plaine = base * facteurs["plaine"]

    assert abs(t_plaine_hm - t_hm_hm) <= TOLERANCE, (
        f"plaine–haute_montagne ({t_plaine_hm}) != haute_montagne–haute_montagne ({t_hm_hm})"
    )
    assert t_plaine_hm < t_plaine_plaine - TOLERANCE, (
        f"plaine–haute_montagne ({t_plaine_hm}) n'est pas < plaine–plaine ({t_plaine_plaine})"
    )
    assert abs(t_plaine_hm - t_hm_plaine) <= TOLERANCE, (
        f"sens inversé : {t_plaine_hm} vs {t_hm_plaine}"
    )
    besoin = 100 * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    assert abs(t_plaine_hm - min(besoin, cap_plaine_hm)) <= TOLERANCE
    assert abs(t_hm_hm - min(besoin, cap_hm_hm)) <= TOLERANCE
    assert abs(t_plaine_plaine - min(besoin, cap_plaine_plaine)) <= TOLERANCE


def test_relief_inconnu_refuse_sur_monde_charge():
    """SC6 — Relief inconnu sur monde chargé : erreur explicite avec cell_id et valeur."""
    from sim.engine import ReliefInvalideError

    world = _build_monde_arete_relief("plaine", "plaine")
    a_id, b_id = 9100, 9101
    world.carte[b_id]["relief"] = "relief_inexistant_040"

    with pytest.raises(ReliefInvalideError, match=str(b_id)):
        _apply_commerce(world, [0.0])
    with pytest.raises(ReliefInvalideError, match="relief_inexistant_040"):
        _apply_commerce(world, [0.0])


def test_sans_carte_capacite_transport_inchangee():
    """SC6 — Sans carte, tick et commerce gardent la capacité de base."""
    from sim.model import ecrire_stock_marchandise, lire_stock_marchandise

    pop = 50
    source = Cell(
        cell_id=9200, area_km2=0.0, population=0,
        stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
    )
    receveuse = Cell(
        cell_id=9201, area_km2=0.0, population=pop,
        stocks={}, hunger_ticks=0, food_deficit_kg=0.0,
    )
    ecrire_stock_marchandise(source, MARCHANDISE_NOURRITURE, 500.0)
    world = World(
        cells={9200: source, 9201: receveuse},
        adjacency=[{"a": 9200, "b": 9201, "kind": "land", "shared_length_m": 1000.0}],
    )

    tick(world, random.Random(0))
    _apply_commerce(world, [0.0])
    stock = lire_stock_marchandise(world.cells[9201], MARCHANDISE_NOURRITURE)
    assert stock > 0.0
    assert stock <= TRADE_CAPACITY_KG_PER_EDGE_PER_TICK + TOLERANCE

