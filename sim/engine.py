"""
Moteur de simulation : boucle de tick.

tick(world, rng) fait avancer l'état du monde d'un pas de temps.
Chaîne causale (brief 013) :
    _apply_production  → produit de la nourriture avec variabilité rng
    _apply_commerce    → transfère de la nourriture entre cellules adjacentes
                         (calcul en deux passes sur snapshot — SC2 brief 013)
    _apply_consumption → consomme le stock, accumule food_deficit_kg si manque ;
                         récupération graduelle si surplus (SC4 brief 013)
    _update_hunger     → met à jour hunger_ticks selon le stock restant
    _apply_mortality   → mortalité proportionnelle à food_deficit_kg,
                         sans plancher max(1, …) — SC4 brief 013

Règle SC9 : aucun littéral numérique non nommé dans les fonctions de calcul.
Toutes les constantes paramétriques sont dans sim/constants.py.

Correction brief 013 SC1 : l'ordre production → consommation → commerce
(brief 012) est remplacé par production → commerce → consommation.
Le maillon commerce ne modifie plus food_deficit_kg (SC1) ; les transferts
sont calculés sur un snapshot immuable (SC2).
"""

import random
from collections import defaultdict

from sim.constants import (
    DEFICIT_RECOVERY_RATE_PER_TICK,
    DEFICIT_ZERO_EPSILON,
    FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK,
    FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
    HUNGER_DEATH_SCALE,
    MAX_DEATH_RATE_PER_TICK,
    RNG_YIELD_HIGH,
    RNG_YIELD_LOW,
    TRADE_CAPACITY_KG_PER_EDGE_PER_TICK,
)
from sim.model import Cell


def _apply_production(cell: Cell, rng: random.Random) -> None:
    """
    Maillon 1 — Production.
    Calcule la nourriture produite avec un facteur de rendement aléatoire
    tiré du rng (fluctuations climatiques/agronomiques) et l'ajoute au stock.
    Traite la sentinelle -1 comme un stock initial nul.
    """
    yield_factor = rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
    food_produced = cell.area_km2 * FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * yield_factor
    current = cell.food_stock_kg if cell.food_stock_kg >= 0 else 0.0
    cell.food_stock_kg = current + food_produced


def _apply_commerce(world, total_transported: list) -> None:
    """
    Maillon 2 — Commerce inter-cellules (brief 013, SC1+SC2).

    Calcul en deux passes sur snapshot pour garantir le transport atomique
    (un kg ne traverse au plus qu'une arête par tick) et l'invariance à
    l'ordre des arêtes.

    Définition du besoin et du surplus (SC2 brief 013, documenté dans SEEDING.md) :
    - besoin d'une cellule = max(0, consommation_tick - stock_snapshot)
      (manque prévisible du tick courant)
    - surplus d'une cellule = max(0, stock_snapshot - consommation_tick)
      (excédent disponible après sa propre alimentation du tick)

    Allocation déterministe (multi-demandeurs sur la même source, SC2 brief 013) :
    - Les demandes sont triées par cell_id croissant (ordre stable).
    - Si la somme des demandes dépasse le surplus de la source, chaque
      receveur reçoit une part proportionnelle à son besoin.
    - Le transfert par arête est borné par TRADE_CAPACITY_KG_PER_EDGE_PER_TICK.

    Conservation stricte : seul food_stock_kg est modifié. food_deficit_kg
    n'est jamais touché par ce maillon (SC1 brief 013).
    `total_transported` est une liste à un élément (accumulateur mutable).
    """
    # Passe 1a : snapshot immuable des stocks et populations
    snapshot_stock = {
        cid: max(0.0, cell.food_stock_kg)
        for cid, cell in world.cells.items()
    }
    snapshot_pop = {cid: cell.population for cid, cell in world.cells.items()}

    def _tick_consumption(cid: int) -> float:
        return snapshot_pop[cid] * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK

    def _surplus(cid: int) -> float:
        return max(0.0, snapshot_stock[cid] - _tick_consumption(cid))

    def _need(cid: int) -> float:
        return max(0.0, _tick_consumption(cid) - snapshot_stock[cid])

    # Passe 1b : collecter les demandes par source, triées par cell_id receveur
    # Chaque entrée : (source_id, receiver_id, demand_amount)
    by_source: dict[int, list[tuple[int, float]]] = defaultdict(list)

    for edge in world.adjacency:
        a_id = edge["a"]
        b_id = edge["b"]
        if a_id not in world.cells or b_id not in world.cells:
            continue

        surplus_a = _surplus(a_id)
        surplus_b = _surplus(b_id)
        need_a = _need(a_id)
        need_b = _need(b_id)

        # Direction a→b : a a du surplus, b a un besoin
        if surplus_a > 0 and need_b > 0:
            demand = min(need_b, TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)
            by_source[a_id].append((b_id, demand))

        # Direction b→a : b a du surplus, a a un besoin
        elif surplus_b > 0 and need_a > 0:
            demand = min(need_a, TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)
            by_source[b_id].append((a_id, demand))

    # Passe 1c : allocation proportionnelle par source (tri stable par receiver cell_id)
    transfers: list[tuple[int, int, float]] = []

    for source_id in sorted(by_source.keys()):
        requests = sorted(by_source[source_id], key=lambda r: r[0])
        avail = _surplus(source_id)
        total_req = sum(d for _, d in requests)

        for receiver_id, demand in requests:
            if total_req <= avail:
                transfer = demand
            else:
                # Allocation proportionnelle pour ne pas dépasser le surplus
                transfer = avail * (demand / total_req)
                transfer = min(transfer, demand)
            if transfer > 0:
                transfers.append((source_id, receiver_id, transfer))

    # Passe 1d : écrêtage côté receveur (N3 feedback 001, brief 013).
    # Une cellule ne peut pas recevoir plus que son besoin snapshot même si
    # plusieurs sources en surplus la visent simultanément.
    # Conservation de la masse : l'excédent reste chez la source.
    snapshot_needs = {cid: _need(cid) for cid in world.cells}
    by_receiver: dict[int, list[tuple[int, float]]] = defaultdict(list)
    for src, rcv, qty in transfers:
        by_receiver[rcv].append((src, qty))

    final_transfers: list[tuple[int, int, float]] = []
    for rcv_id in sorted(by_receiver.keys()):
        incoming = by_receiver[rcv_id]
        need_r = snapshot_needs[rcv_id]
        total_in = sum(qty for _, qty in incoming)
        if total_in > need_r and total_in > 0.0:
            scale = need_r / total_in
            for src_id, qty in incoming:
                scaled = qty * scale
                if scaled > 0.0:
                    final_transfers.append((src_id, rcv_id, scaled))
        else:
            for src_id, qty in incoming:
                final_transfers.append((src_id, rcv_id, qty))

    # Passe 2 : appliquer tous les transferts (jamais food_deficit_kg)
    for source_id, receiver_id, transfer in final_transfers:
        world.cells[source_id].food_stock_kg -= transfer
        world.cells[receiver_id].food_stock_kg += transfer
        total_transported[0] += transfer


def _apply_consumption(cell: Cell) -> None:
    """
    Maillon 3 — Consommation (brief 013, SC1+SC4).

    Lit food_stock_kg (après commerce), soustrait la consommation.

    Si stock ≥ consommation (surplus ou égalité) :
        - ré-écrit le stock résiduel
        - réduit food_deficit_kg graduellement (récupération partielle, SC4)
          cell.food_deficit_kg *= (1 - DEFICIT_RECOVERY_RATE_PER_TICK)
          Un seul tick d'excédent ne peut pas effacer un déficit accumulé.

    Si stock < consommation (manque) :
        - stock = 0, le manque est ajouté à food_deficit_kg.
    """
    tick_need = cell.population * FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    remaining = cell.food_stock_kg - tick_need
    if remaining >= 0.0:
        cell.food_stock_kg = remaining
        prev_deficit = cell.food_deficit_kg if cell.food_deficit_kg > 0 else 0.0
        new_deficit = max(0.0, prev_deficit * (1 - DEFICIT_RECOVERY_RATE_PER_TICK))
        # Coupure epsilon : un déficit résiduel infime (< DEFICIT_ZERO_EPSILON)
        # est ramené à zéro pour éviter l'accumulation de valeurs non physiques.
        if new_deficit < DEFICIT_ZERO_EPSILON:
            new_deficit = 0.0
        cell.food_deficit_kg = new_deficit
    else:
        shortage = -remaining
        prev_deficit = cell.food_deficit_kg if cell.food_deficit_kg > 0 else 0.0
        cell.food_deficit_kg = prev_deficit + shortage
        cell.food_stock_kg = 0.0


def _update_hunger(cell: Cell) -> None:
    """
    Maillon 4 — Faim.
    Si food_stock_kg ≤ 0 (après la consommation), incrémente hunger_ticks.
    Sinon, remet hunger_ticks à 0 (la cellule est rassasiée).
    Traite la sentinelle -1 comme hunger_ticks = 0.
    """
    if cell.food_stock_kg <= 0.0:
        prev = cell.hunger_ticks if cell.hunger_ticks >= 0 else 0
        cell.hunger_ticks = prev + 1
    else:
        cell.hunger_ticks = 0


def _apply_mortality(cell: Cell) -> None:
    """
    Maillon 5 — Mortalité (brief 013, SC4).

    La mortalité est proportionnelle au déficit alimentaire cumulé par habitant
    (food_deficit_kg / population). Plus le déficit par tête est élevé,
    plus le taux de mortalité est élevé, plafonné à MAX_DEATH_RATE_PER_TICK.

    Formule (sans plancher max(1, …) — SC4 brief 013) :
        per_capita_deficit = food_deficit_kg / population
        death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
        deaths = int(population × death_rate)

    Le taux est nul si le déficit est infime : aucune mort n'est garantie
    par le seul fait qu'un déficit est non nul. Le plafond MAX_DEATH_RATE_PER_TICK
    est respecté pour toute population ≥ 1.
    """
    if cell.food_deficit_kg > 0 and cell.population > 0:
        per_capita_deficit = cell.food_deficit_kg / cell.population
        death_rate = per_capita_deficit * HUNGER_DEATH_SCALE
        death_rate = min(death_rate, MAX_DEATH_RATE_PER_TICK)
        deaths = int(cell.population * death_rate)
        cell.population = max(0, cell.population - deaths)


def tick(world, rng: random.Random) -> float:
    """
    Avance le monde d'un pas de temps.

    Ordre du tick (brief 013, SC1) :
        1. Production  (_apply_production)   — pour chaque cellule
        2. Commerce    (_apply_commerce)     — sur le monde entier (snapshot)
        3. Consommation (_apply_consumption) — pour chaque cellule
        4. Faim        (_update_hunger)      — pour chaque cellule
        5. Mortalité   (_apply_mortality)    — pour chaque cellule

    rng : instance de random.Random initialisée par l'appelant —
          jamais d'aléa global non contrôlé.

    Retourne la quantité totale de nourriture transportée par le commerce
    pendant ce tick (kg).
    """
    total_transported = [0.0]

    for cell in world.cells.values():
        _apply_production(cell, rng)

    _apply_commerce(world, total_transported)

    for cell in world.cells.values():
        _apply_consumption(cell)
        _update_hunger(cell)
        _apply_mortality(cell)

    return total_transported[0]
