"""
Moteur de simulation : boucle de tick.

tick(world, rng) fait avancer l'état du monde d'un pas de temps.
Chaîne causale (brief 013) :
    _apply_production  → produit de la nourriture avec variabilité rng
    _apply_commerce    → transfère de la nourriture entre cellules adjacentes
                         (calcul en deux passes sur snapshot — SC2 brief 013)
    _apply_consumption → consomme le stock, accumule food_deficit_kg si manque ;
                         rembourse la dette avec des kg réels de surplus
                         (SC5 brief 017) et retourne la pénurie du tick
    _update_hunger     → met à jour hunger_ticks selon la pénurie du tick
                         (SC4 brief 017), non selon le stock restant
    _apply_mortality   → mortalité proportionnelle à food_deficit_kg,
                         sans plancher max(1, …) — SC4 brief 013 —
                         avec report de la fraction de mort (SC3 brief 017)

Règle SC9 : aucun littéral numérique non nommé dans les fonctions de calcul.
Toutes les constantes paramétriques sont dans sim/constants.py.

Correction brief 013 SC1 : l'ordre production → consommation → commerce
(brief 012) est remplacé par production → commerce → consommation.
Le maillon commerce ne modifie plus food_deficit_kg (SC1) ; les transferts
sont calculés sur un snapshot immuable (SC2).
"""

import random
from collections import defaultdict

from sim import constants as _constantes
from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise

# Carte lue pendant un tick sur un monde chargé ; None hors tick ou sans carte.
_carte_du_tick: dict | None = None


class ReliefInvalideError(ValueError):
    """Classe de relief absente ou inconnue sur une cellule du monde chargé."""


class ClimatInvalideError(ValueError):
    """Climat absent ou durée de solstice invalide sur une cellule du monde chargé."""

# Le moteur lit TOUTES ses constantes réglables par le module `_constantes`,
# jamais par `from sim.constants import ...`. Un nom importé par valeur est
# figé au chargement : le remplacer en mémoire ne change alors rien au moteur,
# et un test de régime mesure un moteur inchangé en croyant mesurer le régime.
# La règle est uniforme et vérifiée par sim/tests/test_write_coverage.py.

def _facteur_relief_pour_cellule(cell: Cell, carte: dict) -> float:
    """Lit world.carte[cell_id]["relief"] et refuse toute classe inconnue."""
    raw = carte.get(cell.cell_id)
    if raw is None:
        raise ReliefInvalideError(
            f"cell_id={cell.cell_id} relief=absent de world.carte"
        )
    relief = raw.get("relief")
    if relief is None:
        raise ReliefInvalideError(
            f"cell_id={cell.cell_id} relief=None"
        )
    facteurs = _constantes.facteurs_production_par_relief()
    if relief not in facteurs:
        raise ReliefInvalideError(
            f"cell_id={cell.cell_id} relief={relief!r}"
        )
    return facteurs[relief]


def _lire_solstices(cell: Cell, carte: dict) -> tuple[float, float]:
    """Lit les deux durées de solstice depuis world.carte[cell_id]["climat"]."""
    raw = carte.get(cell.cell_id)
    if raw is None:
        raise ClimatInvalideError(
            f"cell_id={cell.cell_id} climat=absent de world.carte"
        )
    climat = raw.get("climat")
    if climat is None:
        raise ClimatInvalideError(
            f"cell_id={cell.cell_id} climat=None"
        )
    cle_ete = "duree_jour_solstice_ete_h"
    cle_hiver = "duree_jour_solstice_hiver_h"
    ete_h = climat.get(cle_ete)
    hiver_h = climat.get(cle_hiver)
    if not isinstance(ete_h, (int, float)) or isinstance(ete_h, bool):
        raise ClimatInvalideError(
            f"cell_id={cell.cell_id} {cle_ete}={ete_h!r}"
        )
    if not isinstance(hiver_h, (int, float)) or isinstance(hiver_h, bool):
        raise ClimatInvalideError(
            f"cell_id={cell.cell_id} {cle_hiver}={hiver_h!r}"
        )
    return float(ete_h), float(hiver_h)


def _facteur_saison_pour_cellule(cell: Cell, carte: dict, jour: int) -> float:
    """Facteur saisonnier dérivé du climat de la cellule et du jour de l'année."""
    ete_h, hiver_h = _lire_solstices(cell, carte)
    duree = _constantes.duree_jour_h(jour, ete_h, hiver_h)
    return _constantes.facteur_saison(duree)


def _production_base_kg(cell: Cell, yield_factor: float) -> float:
    """Noyau commun de l'unique formule de production alimentaire."""
    return (
        cell.area_km2
        * _constantes.FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
        * yield_factor
    )


def production_du_tick_kg(
    cell: Cell,
    yield_factor: float,
    carte: dict,
    jour: int | None = None,
) -> float:
    """
    Production alimentaire d'une cellule pendant un tick, avec relief et saison
    lus depuis la carte passée en argument (briefs 034 et 035).

    `jour` facultatif : sans jour, seul le relief module la production (appels
    historiques à trois arguments). Avec un jour explicite, le facteur saisonnier
    s'ajoute — premier jour de l'année via `jour_de_tick(None)`.
    """
    base = _production_base_kg(cell, yield_factor)
    relief = _facteur_relief_pour_cellule(cell, carte)
    if jour is None:
        return base * relief
    return base * relief * _facteur_saison_pour_cellule(cell, carte, jour)


def _production_du_tick_kg_saison_moyenne(
    cell: Cell, yield_factor: float, carte: dict
) -> float:
    """Même formule que le tick, avec le facteur saisonnier moyen sur l'année."""
    ete_h, hiver_h = _lire_solstices(cell, carte)
    base = _production_base_kg(cell, yield_factor)
    saison_moyenne = _constantes.facteur_saison_moyen_annuel(ete_h, hiver_h)
    return (
        base
        * _facteur_relief_pour_cellule(cell, carte)
        * saison_moyenne
    )


def production_kg(cell: Cell, yield_factor: float) -> float:
    """
    Ce qu'une cellule produit en un tick pour un rendement donné.

    UNE seule formule de production dans le moteur, deux lecteurs : le tick,
    qui lui passe un rendement tiré au sort, et `production_moyenne_kg_par_tick`,
    qui lui passe le rendement moyen. Le plafond physique de survie s'en déduit,
    donc il ne peut pas diverger de ce que le moteur produit vraiment.

    Sur un monde chargé (carte non vide), le relief de world.carte modère la
    production pendant le tick via `_carte_du_tick`. Sans carte, le chemin
    unitaire historique reste inchangé.
    """
    base = (
        cell.area_km2
        * _constantes.FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
        * yield_factor
    )
    if _carte_du_tick:
        return base * _facteur_relief_pour_cellule(cell, _carte_du_tick)
    return base


def production_moyenne_kg_par_tick(world) -> float:
    """
    Nourriture que le monde produit en un tick au rendement moyen.

    Sert de référence DÉRIVÉE au plafond physique de survie : une population
    ne peut pas manger durablement plus que ce que son monde produit
    (`sim/tests/test_survie.py`). Elle n'est jamais lue par le tick.
    """
    rendement_moyen = _constantes.rendement_moyen_courant()
    if not world.carte:
        return sum(
            production_kg(cell, rendement_moyen) for cell in world.cells.values()
        )
    return sum(
        _production_du_tick_kg_saison_moyenne(cell, rendement_moyen, world.carte)
        for cell in world.cells.values()
    )


def _apply_production(
    cell: Cell,
    rng: random.Random,
    carte: dict | None = None,
    jour: int | None = None,
) -> None:
    """
    Maillon 1 — Production.
    Calcule la nourriture produite avec un facteur de rendement aléatoire
    tiré du rng (fluctuations climatiques/agronomiques) et l'ajoute au stock.
    Traite la sentinelle -1 comme un stock initial nul.
    """
    yield_factor = rng.uniform(_constantes.RNG_YIELD_LOW, _constantes.RNG_YIELD_HIGH)
    if carte:
        food_produced = production_du_tick_kg(cell, yield_factor, carte, jour=jour)
    else:
        food_produced = production_kg(cell, yield_factor)
    current = lire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE)
    current = current if current >= 0 else 0.0
    ecrire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE, current + food_produced)


def _apply_production_saison_moyenne(
    cell: Cell, rng: random.Random, carte: dict
) -> None:
    """
    Production avec le facteur saisonnier moyen annuel.

    Chemin des appelants historiques qui n'indiquent pas de numéro de tick
    (sonde des couches, test_survie) : la moyenne annuelle vaut 1 au niveau 2
    et reproduit l'ancien régime de production.
    """
    yield_factor = rng.uniform(_constantes.RNG_YIELD_LOW, _constantes.RNG_YIELD_HIGH)
    food_produced = _production_du_tick_kg_saison_moyenne(cell, yield_factor, carte)
    current = lire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE)
    current = current if current >= 0 else 0.0
    ecrire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE, current + food_produced)


def _apply_commerce(world, total_transported: list) -> None:
    """
    Maillon 2 — Commerce inter-cellules (brief 013, SC1+SC2).

    Calcul en deux passes sur snapshot pour garantir le transport atomique
    (un kg ne traverse au plus qu'une arête par tick) et l'invariance à
    l'ordre des arêtes.

    Définition du besoin et du surplus (SC2 brief 013, documenté dans MODELE.md) :
    - besoin d'une cellule = max(0, consommation_tick - stock_snapshot)
      (manque prévisible du tick courant)
    - surplus d'une cellule = max(0, stock_snapshot - consommation_tick)
      (excédent disponible après sa propre alimentation du tick)

    Allocation déterministe (multi-demandeurs sur la même source, SC2 brief 013) :
    - Les demandes sont triées par cell_id croissant (ordre stable).
    - Si la somme des demandes dépasse le surplus de la source, chaque
      receveur reçoit une part proportionnelle à son besoin.
    - Le transfert par arête est borné par TRADE_CAPACITY_KG_PER_EDGE_PER_TICK.

    Conservation stricte : seul le stock de nourriture est modifié. food_deficit_kg
    n'est jamais touché par ce maillon (SC1 brief 013).
    `total_transported` est une liste à un élément (accumulateur mutable).
    """
    # Passe 1a : snapshot immuable des stocks et populations
    snapshot_stock = {
        cid: max(0.0, lire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE))
        for cid, cell in world.cells.items()
    }
    snapshot_pop = {cid: cell.population for cid, cell in world.cells.items()}

    def _tick_consumption(cid: int) -> float:
        return snapshot_pop[cid] * _constantes.FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK

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
            demand = min(need_b, _constantes.TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)
            by_source[a_id].append((b_id, demand))

        # Direction b→a : b a du surplus, a a un besoin
        elif surplus_b > 0 and need_a > 0:
            demand = min(need_a, _constantes.TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)
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
        source_cell = world.cells[source_id]
        receiver_cell = world.cells[receiver_id]
        source_stock = lire_stock_marchandise(source_cell, _constantes.MARCHANDISE_NOURRITURE)
        source_eff = source_stock if source_stock >= 0 else 0.0
        ecrire_stock_marchandise(
            source_cell, _constantes.MARCHANDISE_NOURRITURE, source_eff - transfer
        )
        receiver_stock = lire_stock_marchandise(receiver_cell, _constantes.MARCHANDISE_NOURRITURE)
        receiver_eff = receiver_stock if receiver_stock >= 0 else 0.0
        ecrire_stock_marchandise(
            receiver_cell, _constantes.MARCHANDISE_NOURRITURE, receiver_eff + transfer
        )
        total_transported[0] += transfer


def _apply_consumption(cell: Cell) -> float:
    """
    Maillon 3 — Consommation (brief 013 SC1 ; brief 017 SC4+SC5).

    Lit le stock de nourriture (après commerce), soustrait la consommation, et retourne
    la pénurie du tick en kg (0.0 s'il n'y a pas eu de manque). Cette valeur
    de retour est le critère causal de la faim : c'est elle, et non un stock
    vide, qui dit qu'une cellule a MANQUÉ de nourriture ce tick (SC4).

    Si stock ≥ consommation (surplus ou égalité) — SC5 brief 017 :
        - le surplus disponible est `remaining`
        - la dette alimentaire est remboursée par des kilogrammes RÉELS :
          remboursement = min(dette, remaining × ratio) où le ratio nommé est
          DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG (1 kg de dette par kg de surplus)
        - ces kilogrammes quittent le stock (ils sont mangés en sus du besoin
          d'entretien) : rien ne se téléporte (principe 3)
        - un surplus d'un nanogramme ne peut donc effacer qu'un nanogramme de
          dette, jamais 10 % d'une dette de 10 000 kg
        - aucun seuil de coupure : la soustraction atteint 0.0 exactement
          quand le surplus couvre, et tout résidu est une dette réelle

    Si stock < consommation (manque) :
        - stock = 0, le manque est ajouté à food_deficit_kg, et la pénurie
          du tick est retournée.
    """
    tick_need = cell.population * _constantes.FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
    stock = lire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE)
    stock_eff = stock if stock >= 0 else 0.0
    remaining = stock_eff - tick_need
    prev_deficit = cell.food_deficit_kg if cell.food_deficit_kg > 0 else 0.0

    if remaining >= 0.0:
        # Le ratio est borné à 1 kg de dette par kg de surplus : la réduction
        # du déficit ne peut jamais dépasser le surplus physique du tick,
        # quelle que soit la valeur donnée à la constante.
        ratio = min(1.0, _constantes.DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG)
        remboursement = min(prev_deficit, max(0.0, remaining) * ratio)
        # Le remboursement est SOUSTRAIT : quand le surplus couvre la dette,
        # `min` rend la dette elle-même et la soustraction donne exactement
        # 0.0. Tout résidu est donc une dette réelle, jamais un artefact de
        # calcul flottant — et l'effacer serait faire disparaître des
        # kilogrammes sans contrepartie (principe 3).
        cell.food_deficit_kg = prev_deficit - remboursement
        ecrire_stock_marchandise(
            cell, _constantes.MARCHANDISE_NOURRITURE, remaining - remboursement
        )
        return 0.0

    shortage = -remaining
    cell.food_deficit_kg = prev_deficit + shortage
    ecrire_stock_marchandise(cell, _constantes.MARCHANDISE_NOURRITURE, 0.0)
    return shortage


def _update_hunger(cell: Cell, penurie_kg: float) -> None:
    """
    Maillon 4 — Faim (brief 017, SC4).

    `penurie_kg` est le manque du tick retourné par _apply_consumption.
    hunger_ticks progresse si et seulement si la cellule a manqué de
    nourriture CE tick.

    Une cellule ravitaillée exactement à son besoin par le commerce termine le
    tick avec un stock nul et un déficit nul : elle a mangé sa ration, elle
    n'est pas affamée. L'ancien critère testait le stock résiduel après
    consommation, ce qui confondait le garde-manger vide et la
    sous-alimentation (voir sim/MODELE.md, SC4 brief 017).
    Traite la sentinelle -1 comme hunger_ticks = 0.
    """
    if penurie_kg > 0.0:
        prev = cell.hunger_ticks if cell.hunger_ticks >= 0 else 0
        cell.hunger_ticks = prev + 1
    else:
        cell.hunger_ticks = 0


def _apply_mortality(cell: Cell) -> None:
    """
    Maillon 5 — Mortalité (brief 013 SC4 ; brief 017 SC3).

    La mortalité est proportionnelle au déficit alimentaire cumulé par habitant
    (food_deficit_kg / population), plafonnée à MAX_DEATH_RATE_PER_TICK.

    Formule (sans plancher max(1, …) — SC4 brief 013 ; avec report de la
    fraction — SC3 brief 017) :
        per_capita_deficit = food_deficit_kg / population
        death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
        raw    = population × death_rate + mortality_remainder
        deaths = int(raw)
        mortality_remainder = raw - deaths

    Le report de la fraction supprime l'immortalité par arrondi : une cellule
    de 5 habitants en famine totale produit 0.5 mort par tick, que `int()`
    jetait entièrement à chaque tick. La fraction non appliquée est désormais
    conservée et finit par tuer.

    Le taux reste nul si le déficit est infime : aucune mort n'est garantie
    par le seul fait qu'un déficit est non nul.
    """
    remainder = cell.mortality_remainder if cell.mortality_remainder >= 0.0 else 0.0

    if cell.food_deficit_kg > 0 and cell.population > 0:
        per_capita_deficit = cell.food_deficit_kg / cell.population
        death_rate = per_capita_deficit * _constantes.HUNGER_DEATH_SCALE
        death_rate = min(death_rate, _constantes.MAX_DEATH_RATE_PER_TICK)
        raw = cell.population * death_rate + remainder
        deaths = int(raw)
        cell.mortality_remainder = raw - deaths
        cell.population = max(0, cell.population - deaths)
    else:
        # Aucun décès ce tick : la fraction en attente est conservée telle
        # quelle (et la sentinelle -1 devient une mesure réelle : 0.0).
        cell.mortality_remainder = remainder


def tick(world, rng: random.Random, numero_tick: int | None = None) -> float:
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
    carte = world.carte if getattr(world, "carte", None) else None
    if carte is None:
        for cell in world.cells.values():
            _apply_production(cell, rng, carte)
    elif numero_tick is None:
        for cell in world.cells.values():
            _apply_production_saison_moyenne(cell, rng, carte)
    else:
        jour = _constantes.jour_de_tick(numero_tick)
        for cell in world.cells.values():
            _apply_production(cell, rng, carte, jour=jour)

    _apply_commerce(world, total_transported)

    for cell in world.cells.values():
        penurie_kg = _apply_consumption(cell)
        _update_hunger(cell, penurie_kg)
        _apply_mortality(cell)

    return total_transported[0]
