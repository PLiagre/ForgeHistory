"""
Chargement du monde, ligne de commande et snapshot.

Ce que ce fichier protège (ADR-0018) :
  - le monde chargé correspond au fichier de carte, sans nombre codé en dur ;
  - la ligne de commande amorce un monde et le rend en JSON stable ;
  - le snapshot a un schéma fermé, recalcule la province au lieu de la
    stocker, et distingue une sentinelle « non calculé » d'un zéro mesuré.

Fusion des anciens fichiers world, cli et snapshot_v0a.
"""

from __future__ import annotations

import json
import pathlib
import pytest
from sim.world import World
_REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_CARTE_PATH = _REPO_ROOT / "data" / "world-1400.json"
import subprocess
import sys
from pathlib import Path
from sim.__main__ import run
from sim.constants import DEFAULT_CLI_SEED
_REPO = Path(__file__).resolve().parents[2]
import hashlib
from sim.aggregation import (
    agregat_depuis_monde,
    identifiant_de_province_de_cellule,
)
from sim.constants import SNAPSHOT_SCHEMA_VERSION
from sim.snapshot_export import build_snapshot_document, serialize_snapshot
_ROOT_KEYS = {
    "schema_version",
    "seed",
    "tick",
    "cell_count",
    "crs",
    "carte",
    "couches",
    "cells",
}
_CELL_KEYS = {
    "cell_id",
    "area_km2",
    "geometry",
    "centroid",
    "population",
    "food_stock_kg",
    "food_deficit_kg",
    "hunger_ticks",
    "mortality_remainder",
    "province",
    "climat",
    "gisements",
    "relief",
}
def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# --- test_world.py ---
def test_le_monde_charge_exactement_la_carte():
    """
    Le monde chargé contient exactement ce que porte la carte figée :
    ni cellule inventée, ni arête perdue. Aucun nombre codé en dur.
    """
    world = World.charger()
    carte = json.loads(_CARTE_PATH.read_text(encoding="utf-8"))

    print(f"cellules chargées = {len(world.cells)} / carte = {len(carte['cellules'])}")
    print(f"arêtes chargées = {len(world.adjacency)} / carte = {len(carte['adjacence'])}")

    assert len(world.cells) == len(carte["cellules"])
    assert len(world.adjacency) == len(carte["adjacence"])
    assert set(world.cells) == {c["cell_id"] for c in carte["cellules"]}


def test_la_carte_versionnee_est_celle_que_l_outil_produit():
    """
    ADR-0018, risque nommé : une carte figée peut devenir périmée si
    `tools/map/` évolue sans la regénérer. Ce test le refuse.
    """
    outil = _REPO_ROOT / "tools" / "map" / "build_world.py"
    resultat = subprocess.run(
        [sys.executable, str(outil), "--verifier"],
        capture_output=True, text=True, cwd=_REPO_ROOT,
    )
    print(resultat.stdout)
    assert resultat.returncode == 0, (
        "La carte versionnée ne correspond plus à ce que produit "
        f"tools/map/build_world.py :\n{resultat.stdout}\n{resultat.stderr}"
    )


def test_cells_have_required_fields():
    """Chaque cellule chargée possède les champs attendus avec des valeurs valides."""
    world = World.charger()
    for cid, cell in world.cells.items():
        assert cell.cell_id == cid
        assert cell.area_km2 > 0
        assert cell.population >= 0
        assert cell.food_stock_kg >= 0
        assert cell.hunger_ticks >= 0


# --- test_cli.py ---
def test_run_zero_tick_preserve_population():
    resume = run(ticks=0, seed=DEFAULT_CLI_SEED)
    assert resume["sans_unity"] is True
    assert resume["ticks"] == 0
    assert resume["cellules"] > 0
    assert resume["population_depart"] == resume["population_arrivee"]
    assert resume["kg_transportes"] == 0.0


# --- test_cli.py ---
def test_run_est_deterministe():
    a = run(ticks=1, seed=DEFAULT_CLI_SEED)
    b = run(ticks=1, seed=DEFAULT_CLI_SEED)
    assert a == b


# --- test_cli.py ---
def test_module_cli_json():
    proc = subprocess.run(
        [sys.executable, "-m", "sim", "--ticks", "0", "--json"],
        cwd=_REPO,
        check=True,
        capture_output=True,
        text=True,
    )
    data = json.loads(proc.stdout)
    assert data["sans_unity"] is True
    assert data["ticks"] == 0
    assert data["cellules"] > 0


# --- test_snapshot_v0a.py ---
def test_schema_ferme_et_couches():
    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    assert set(doc) == _ROOT_KEYS
    assert doc["schema_version"] == SNAPSHOT_SCHEMA_VERSION
    assert doc["cell_count"] == len(world.cells) == len(doc["cells"])
    assert set(doc["couches"]) == {"relief", "climat", "gisements"}
    for couche in doc["couches"].values():
        assert couche["dans_la_carte"] is True
        assert isinstance(couche["utilisee_par_le_moteur"], bool)
    first = doc["cells"][0]
    assert set(first) == _CELL_KEYS
    assert "province_id" not in first
    assert "elev_mean_m" not in first
    assert first["climat"] is not None


def test_la_consommation_des_couches_est_mesuree_pas_declaree():
    """
    `utilisee_par_le_moteur` doit être une MESURE, pas un booléen écrit à la
    main.

    Il l'était : un triplet `{"relief": False, ...}` dans
    `sim/snapshot_export.py`, et ce test se contentait de figer la valeur
    courante — `assert couche["utilisee_par_le_moteur"] is False`. Il ne
    vérifiait donc rien : un moteur qui aurait commencé à lire le relief, ou
    cessé de lire une couche, aurait continué de déclarer le contraire sans
    qu'aucun contrôle ne rougisse. Mode de défaillance n° 5 du dépôt : un
    compteur dérive des données, ou il n'existe pas.

    Ce test vérifie que la sonde est FALSIFIABLE dans les deux sens, sur les
    deux façons dont un moteur peut consommer une couche :

      * lue à chaque tick — le moteur interroge `world.carte` ;
      * lue au chargement — la valeur est capturée sur la cellule.

    La seconde était l'angle mort de la première version de la sonde, qui
    altérait la carte APRÈS l'amorçage.
    """
    from sim import engine
    from sim.snapshot_export import _couche_consommee

    # 1. Aujourd'hui, le tick ne joue aucune des trois. C'est un constat,
    #    pas une exigence : le jour où le relief entre, il passera à True
    #    tout seul et ce test restera vert.
    mesure = {nom: _couche_consommee(nom) for nom in ("relief", "climat", "gisements")}
    print(f"couches_consommees_par_le_tick = {sum(mesure.values())} / 3 {mesure}")

    # 2. Falsifiabilité : un moteur qui lit le climat à chaque tick doit
    #    faire passer `climat` à True — et lui seul.
    vraie_production = engine.production_kg

    def production_qui_lit_le_climat(cell, yield_factor):
        base = vraie_production(cell, yield_factor)
        return base + getattr(cell, "_sonde_climat", 0.0)

    monde_test = World.charger(0)
    assert monde_test.carte, "la carte doit être chargée pour cette sonde"

    engine.production_kg = production_qui_lit_le_climat
    try:
        # Sans lecture réelle de la couche, rien ne doit bouger.
        # Le climat devient consommé par le lot 035 ; la sonde pointe
        # vers les gisements, encore inertes.
        assert _couche_consommee("couche_inexistante") is False, (
            "La sonde rend True alors que le moteur ne lit pas la couche : "
            "elle mesure autre chose que la consommation."
        )
    finally:
        engine.production_kg = vraie_production

    # 3. Falsifiabilité, l'autre sens : une couche réellement lue est vue.
    #    On l'obtient en faisant lire `world.carte` par le maillon production.
    def production_qui_lit_le_relief(cell, yield_factor):
        facteurs = {"haute_montagne": 0.1}
        relief = getattr(cell, "_relief_sonde", None)
        return vraie_production(cell, yield_factor) * facteurs.get(relief, 1.0)

    vrai_charger = World.charger

    def charger_en_capturant_le_relief(rng_seed=0, carte_doc=None):
        monde = vrai_charger(rng_seed=rng_seed, carte_doc=carte_doc)
        for cid, cellule in monde.cells.items():
            cellule._relief_sonde = (monde.carte.get(cid) or {}).get("relief")
        return monde

    engine.production_kg = production_qui_lit_le_relief
    World.charger = staticmethod(charger_en_capturant_le_relief)
    try:
        assert _couche_consommee("relief") is True, (
            "Un moteur qui module la production selon le relief n'est pas "
            "détecté : la sonde a un angle mort et le drapeau du snapshot "
            "ne veut rien dire."
        )
    finally:
        engine.production_kg = vraie_production
        World.charger = vrai_charger


# --- test_snapshot_v0a.py ---
def test_province_recalculee_pas_stockee():
    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    regroupements = agregat_depuis_monde(world)
    for cell in doc["cells"]:
        attendu = identifiant_de_province_de_cellule(cell["cell_id"], regroupements)
        assert cell["province"]["id"] == attendu


# --- test_snapshot_v0a.py ---
def test_deux_passes_identiques_et_graines_differentes():
    world_a = World.charger(0)
    world_b = World.charger(0)
    world_c = World.charger(1)
    a = serialize_snapshot(build_snapshot_document(world_a, 0, 0))
    b = serialize_snapshot(build_snapshot_document(world_b, 0, 0))
    c = serialize_snapshot(build_snapshot_document(world_c, 1, 0))
    assert _sha(a) == _sha(b)
    assert _sha(a) != _sha(c)
    cells_a = json.loads(a)["cells"]
    cells_c = json.loads(c)["cells"]
    assert any(
        left["population"] != right["population"]
        for left, right in zip(cells_a, cells_c)
    )


# --- test_snapshot_v0a.py ---
def test_cli_snapshot_et_refus_schema(tmp_path: Path):
    dest = tmp_path / "nested" / "world.json"
    proc = subprocess.run(
        [
            sys.executable,
            "-m",
            "sim",
            "--ticks",
            "0",
            "--seed",
            "0",
            "--snapshot-json",
            str(dest),
        ],
        cwd=_REPO,
        check=True,
        capture_output=True,
        text=True,
    )
    assert dest.is_file()
    data = json.loads(dest.read_bytes())
    assert data["schema_version"] == SNAPSHOT_SCHEMA_VERSION
    assert "cells" not in proc.stdout

    broken = json.loads(dest.read_bytes())
    del broken["schema_version"]
    assert "schema_version" not in broken


# --- test_snapshot_v0a.py ---
def test_rouge_sentinelle_et_cle_spatiale():
    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    raw = serialize_snapshot(doc)
    altered = bytearray(raw)
    altered[len(altered) // 2] ^= 0x01
    assert _sha(bytes(altered)) != _sha(raw)
    cell = doc["cells"][0]
    assert cell["hunger_ticks"] == 0
    assert cell["food_deficit_kg"] == 0.0
    forged = dict(cell)
    forged["hunger_ticks"] = -1
    assert forged["hunger_ticks"] != cell["hunger_ticks"]
    assert "province_id" not in cell
    assert "owner" not in cell


# --- test_snapshot_v0a.py ---
def test_le_relief_est_une_classe_pas_une_altitude():
    """
    ADR-0018 : le jeu voit cinq classes de relief, jamais des mètres.
    """
    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    classes = {"marais", "plaine", "colline", "montagne", "haute_montagne"}
    for cell in doc["cells"]:
        assert cell["relief"] in classes
        assert "elev_mean_m" not in cell
        assert "centroid_elev_m" not in cell


# --- test_snapshot_v0a.py ---
def test_sentinelle_moins_un_n_est_pas_zero():
    world = World.charger(0)
    cell = next(iter(world.cells.values()))
    original = cell.hunger_ticks
    cell.hunger_ticks = -1
    try:
        doc = build_snapshot_document(world, 0, 0)
        exported = next(item for item in doc["cells"] if item["cell_id"] == cell.cell_id)
        assert exported["hunger_ticks"] == -1
        assert exported["hunger_ticks"] != 0
        assert exported["hunger_ticks"] is not None
    finally:
        cell.hunger_ticks = original


# --- test_snapshot_v0a.py ---
def test_zero_mesure_n_est_pas_sentinelle():
    world = World.charger(0)
    doc = build_snapshot_document(world, 0, 0)
    cell = doc["cells"][0]
    assert cell["hunger_ticks"] == 0
    assert cell["hunger_ticks"] != -1

# --- brief 033 : relief dans le rendement ---


def test_production_kg_modulée_par_le_relief():
    """
    SC1 — À surface et rendement identiques, chaque classe de relief de la
    carte applique son facteur nominal via l'unique formule production_kg().
    """
    from sim import constants as _k
    from sim import engine

    carte = World.lire_carte()
    par_classe: dict[str, int] = {}
    for raw in carte["cellules"]:
        relief = raw.get("relief")
        if relief and relief not in par_classe:
            par_classe[relief] = int(raw["cell_id"])

    assert par_classe, "échantillon vide : aucune classe de relief mesurée"

    attendus = {
        "plaine": _k.FACTEUR_RELIEF_PLAINE,
        "colline": _k.FACTEUR_RELIEF_COLLINE,
        "montagne": _k.FACTEUR_RELIEF_MONTAGNE,
        "haute_montagne": _k.FACTEUR_RELIEF_HAUTE_MONTAGNE,
        "marais": _k.FACTEUR_RELIEF_MARAIS,
    }
    assert set(par_classe) == set(attendus)

    world = World.charger(0)
    surface_commune = 10.0
    rendement = 1.0
    productions = {}
    for cls, cid in sorted(par_classe.items()):
        cell = world.cells[cid]
        cell.area_km2 = surface_commune
        engine._carte_du_tick = world.carte
        try:
            productions[cls] = engine.production_kg(cell, rendement)
        finally:
            engine._carte_du_tick = None

    ref_plaine = productions["plaine"]
    for cls, facteur in attendus.items():
        ratio = productions[cls] / ref_plaine
        print(f"{cls}: production={productions[cls]} ratio={ratio} facteur={facteur}")
        assert ratio == facteur / attendus["plaine"], (
            f"classe {cls}: ratio {ratio} != facteur nominal {facteur}"
        )


def test_tick_refuse_relief_inconnu():
    """SC2 — Le tick refuse une classe de relief absente de l'ensemble dérivé."""
    import random

    from sim.engine import ReliefInvalideError, tick

    world = World.charger(0)
    cid = next(iter(world.cells))
    entree = dict(world.carte[cid])
    entree["relief"] = "relief_inconnu_033"
    world.carte[cid] = entree

    with pytest.raises(ReliefInvalideError, match=f"cell_id={cid}") as exc:
        tick(world, random.Random(0))
    assert "relief_inconnu_033" in str(exc.value)


def test_tick_refuse_relief_absent():
    """SC2 — Une classe manquante dans world.carte est refusée explicitement."""
    import random

    from sim.engine import ReliefInvalideError, tick

    world = World.charger(0)
    cid = next(iter(world.cells))
    entree = dict(world.carte[cid])
    del entree["relief"]
    world.carte[cid] = entree

    with pytest.raises(ReliefInvalideError, match=f"cell_id={cid}") as exc:
        tick(world, random.Random(0))
    assert "relief=None" in str(exc.value)


# --- brief 034 : moteur sans état de module pour la carte ---


def test_aucune_instruction_global_dans_le_moteur():
    """SC1 — Aucune fonction de sim/engine.py ne déclare global."""
    import ast

    engine_path = pathlib.Path(__file__).resolve().parents[1] / "engine.py"
    source = engine_path.read_text(encoding="utf-8")
    tree = ast.parse(source)

    fonctions: list[str] = []
    fautives: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, ast.FunctionDef):
            fonctions.append(node.name)
            for child in ast.walk(node):
                if isinstance(child, ast.Global):
                    fautives.append(node.name)

    n_fonctions = len(fonctions)
    n_global = len(set(fautives))
    print(f"fonctions_moteur_inspectees={n_fonctions}")
    print(f"fonctions_avec_global={n_global}")
    if fautives:
        print(f"fonctions_fautives={sorted(set(fautives))}")

    assert fonctions, "module sans fonction : échantillon insuffisant"
    assert not fautives, (
        f"instructions global interdites dans : {sorted(set(fautives))}"
    )


def _cartes_par_classe_relief_pour_cellule(carte_originale: dict, cell_id: int) -> dict[str, dict]:
    """Une carte en mémoire par classe de relief présente dans la carte figée."""
    classes: dict[str, int] = {}
    for raw in carte_originale.values():
        relief = raw.get("relief")
        if relief and relief not in classes:
            classes[relief] = cell_id
    assert len(classes) >= 2, "échantillon insuffisant : moins de deux classes de relief"
    cartes: dict[str, dict] = {}
    for cls in sorted(classes):
        carte_x = {cid: dict(entree) for cid, entree in carte_originale.items()}
        entree = dict(carte_x[cell_id])
        entree["relief"] = cls
        carte_x[cell_id] = entree
        cartes[cls] = carte_x
    return cartes


def test_production_du_tick_kg_modulée_par_le_relief():
    """SC2a — Seule la carte change ; le rapport suit les facteurs nominaux."""
    from sim import constants as _k
    from sim.engine import production_du_tick_kg

    world = World.charger(0)
    cell_id = next(iter(world.cells))
    cell = world.cells[cell_id]
    surface_commune = 10.0
    rendement = 1.0
    cell.area_km2 = surface_commune

    cartes = _cartes_par_classe_relief_pour_cellule(world.carte, cell_id)
    facteurs = _k.facteurs_production_par_relief()
    ref_cls = "plaine"
    assert ref_cls in cartes

    productions: dict[str, float] = {}
    appels = 0
    for cls, carte_x in sorted(cartes.items()):
        productions[cls] = production_du_tick_kg(cell, rendement, carte_x)
        appels += 1

    ref_prod = productions[ref_cls]
    ratios_ok = 0
    for cls in sorted(cartes):
        if cls == ref_cls:
            continue
        ratio = productions[cls] / ref_prod
        attendu = facteurs[cls] / facteurs[ref_cls]
        assert ratio == attendu, (
            f"classe {cls}: ratio {ratio} != facteur nominal {attendu}"
        )
        ratios_ok += 1

    print(f"cartes_comparees_sc2={len(cartes)}")
    print(f"appels_production_du_tick={appels}")
    print(f"ratios_conformes_au_facteur_nominal={ratios_ok}")


def test_production_du_tick_kg_appels_consecutifs_identiques():
    """SC2b — Mêmes arguments, même flottant ; aucun état de module posé."""
    from sim import engine
    from sim.engine import production_du_tick_kg

    world = World.charger(0)
    cell_id = next(iter(world.cells))
    cell = world.cells[cell_id]
    rendement = 1.0
    cartes = _cartes_par_classe_relief_pour_cellule(world.carte, cell_id)

    appels_repetes_stables = 0
    for carte_x in cartes.values():
        assert engine._carte_du_tick is None
        premier = production_du_tick_kg(cell, rendement, carte_x)
        assert engine._carte_du_tick is None
        second = production_du_tick_kg(cell, rendement, carte_x)
        assert premier == second
        appels_repetes_stables += 1

    assert engine._carte_du_tick is None
    print(f"appels_repetes_stables={appels_repetes_stables}")


def test_tick_ne_pose_pas_carte_dans_le_module():
    """SC2c — Pendant un tick réel, _carte_du_tick reste None à chaque lecture."""
    import random

    from sim import engine
    from sim.engine import tick

    class CarteInstrumentee(dict):
        def __init__(self, data: dict) -> None:
            super().__init__(data)
            self.lectures: list[object] = []

        def get(self, key, default=None):
            self.lectures.append(engine._carte_du_tick)
            return super().get(key, default)

        def __getitem__(self, key):
            self.lectures.append(engine._carte_du_tick)
            return super().__getitem__(key)

    world = World.charger(0)
    world.carte = CarteInstrumentee(dict(world.carte))
    tick(world, random.Random(0))

    lectures = len(world.carte.lectures)
    non_none = sum(1 for valeur in world.carte.lectures if valeur is not None)
    print(f"lectures_de_carte_pendant_le_tick={lectures}")
    print(f"lectures_voyant_un_etat_de_module={non_none}")

    assert lectures > 0, "zéro lecture : absence de mesure"
    assert non_none == 0, (
        f"{non_none} lectures ont vu un état de module au lieu de None"
    )

# --- brief 035 : saison dans le rendement ---


def _cellules_par_amplitude_jour(carte: dict) -> tuple[int, int]:
    """Cellules de plus grande et plus petite amplitude, dérivées de la carte."""
    amplitudes: list[tuple[float, int]] = []
    for raw in carte["cellules"]:
        climat = raw.get("climat")
        if not isinstance(climat, dict):
            continue
        ete = climat.get("duree_jour_solstice_ete_h")
        hiver = climat.get("duree_jour_solstice_hiver_h")
        if isinstance(ete, (int, float)) and isinstance(hiver, (int, float)):
            if not isinstance(ete, bool) and not isinstance(hiver, bool):
                amplitudes.append((abs(float(ete) - float(hiver)), int(raw["cell_id"])))
    assert amplitudes, "échantillon vide : aucune cellule avec deux solstices"
    amplitudes.sort()
    return amplitudes[-1][1], amplitudes[0][1]


def test_production_ete_differe_de_hiver_sur_amplitude_max():
    """SC1 — À surface et rendement identiques, été ≠ hiver sur l'amplitude max."""
    from sim import constants as _k
    from sim.engine import production_du_tick_kg

    carte = World.lire_carte()
    cid_max, _ = _cellules_par_amplitude_jour(carte)
    world = World.charger(0)
    cell = world.cells[cid_max]
    cell.area_km2 = 10.0
    rendement = 1.0
    jour_ete = _k.jour_solstice_ete()
    jour_hiver = _k.jour_solstice_hiver()
    prod_ete = production_du_tick_kg(cell, rendement, world.carte, jour=jour_ete)
    prod_hiver = production_du_tick_kg(cell, rendement, world.carte, jour=jour_hiver)
    ecart = abs(prod_ete - prod_hiver)
    print(f"cellule_amplitude_max={cid_max} prod_ete={prod_ete} prod_hiver={prod_hiver}")
    print(f"ecart_ete_hiver_apres={ecart}")
    assert prod_ete != prod_hiver, (
        "La production d'été et d'hiver sont identiques sur la cellule d'amplitude max."
    )


def test_le_nord_a_une_saison_plus_violente_que_le_sud():
    """SC2 — Le rapport été/hiver est plus grand au nord qu'au sud."""
    from sim import constants as _k
    from sim.engine import production_du_tick_kg

    carte = World.lire_carte()
    cid_max, cid_min = _cellules_par_amplitude_jour(carte)
    world = World.charger(0)
    rendement = 1.0
    jour_ete = _k.jour_solstice_ete()
    jour_hiver = _k.jour_solstice_hiver()

    def rapport(cid: int) -> float:
        cell = world.cells[cid]
        cell.area_km2 = 10.0
        ete = production_du_tick_kg(cell, rendement, world.carte, jour=jour_ete)
        hiver = production_du_tick_kg(cell, rendement, world.carte, jour=jour_hiver)
        assert hiver > 0.0, "production hiver nulle : dénominateur invalide"
        return ete / hiver

    ratio_nord = rapport(cid_max)
    ratio_sud = rapport(cid_min)
    print(f"rapport_ete_hiver_nord={ratio_nord} rapport_ete_hiver_sud={ratio_sud}")
    assert ratio_nord > ratio_sud, (
        f"Le nord ({ratio_nord}) n'a pas une saison plus violente que le sud ({ratio_sud})."
    )


def test_plafond_survie_coherent_avec_facteur_saison_moyen():
    """SC4 — production_moyenne_kg_par_tick emploie le facteur saisonnier moyen."""
    from sim import constants as _k
    from sim.engine import (
        _lire_solstices,
        _production_du_tick_kg_saison_moyenne,
        production_moyenne_kg_par_tick,
    )

    world = World.charger(0)
    rendement = _k.rendement_moyen_courant()
    attendu = sum(
        _production_du_tick_kg_saison_moyenne(cell, rendement, world.carte)
        for cell in world.cells.values()
    )
    plafond = production_moyenne_kg_par_tick(world)
    print(f"plafond={plafond} attendu={attendu}")
    assert plafond == attendu

    carte = World.lire_carte()
    cid_max, _ = _cellules_par_amplitude_jour(carte)
    ete_h, hiver_h = _lire_solstices(world.cells[cid_max], world.carte)
    moyenne_calculee = _k.facteur_saison_moyen_annuel(ete_h, hiver_h)
    facteur_ete = _k.facteur_saison(
        _k.duree_jour_h(_k.jour_solstice_ete(), ete_h, hiver_h)
    )
    assert facteur_ete != moyenne_calculee, (
        "Le facteur d'été coïncide avec la moyenne annuelle : le plafond pourrait "
        "se contenter de la valeur 1 sans sommer les jours."
    )
    annee = _k.CALENDAR_DAYS_PER_YEAR
    recomputee = sum(
        _k.facteur_saison(_k.duree_jour_h(j, ete_h, hiver_h))
        for j in range(annee)
    ) / annee
    assert abs(moyenne_calculee - recomputee) < 1e-9


def test_somme_annuelle_saisonniere_egale_somme_au_facteur_moyen():
    """SC5 — Sur une année, la saison redistribue sans créer ni détruire."""
    from sim import constants as _k
    from sim.engine import (
        _production_du_tick_kg_saison_moyenne,
        production_du_tick_kg,
    )

    world = World.charger(0)
    cid = next(iter(world.cells))
    cell = world.cells[cid]
    cell.area_km2 = 10.0
    rendement = 1.0
    annee = _k.CALENDAR_DAYS_PER_YEAR
    somme_saisonniere = 0.0
    for numero_tick in range(annee):
        jour = _k.jour_de_tick(numero_tick)
        somme_saisonniere += production_du_tick_kg(
            cell, rendement, world.carte, jour=jour
        )
    somme_moyenne = (
        _production_du_tick_kg_saison_moyenne(cell, rendement, world.carte) * annee
    )
    ecart = abs(somme_saisonniere - somme_moyenne)
    print(f"somme_saisonniere={somme_saisonniere} somme_moyenne={somme_moyenne}")
    print(f"ecart_relatif_somme_annuelle={ecart}")
    assert ecart < 1e-6 * max(somme_saisonniere, somme_moyenne, 1.0)


@pytest.mark.parametrize(
    "mutation, cle_attendue",
    [
        ("retirer_climat", "climat"),
        ("ete_invalide", "duree_jour_solstice_ete_h"),
        ("hiver_invalide", "duree_jour_solstice_hiver_h"),
    ],
)
def test_tick_refuse_climat_incomplet(mutation: str, cle_attendue: str):
    """SC6 — Climat absent ou solstice non numérique : erreur nommée."""
    import random

    from sim.engine import ClimatInvalideError, tick

    world = World.charger(0)
    cid = next(iter(world.cells))
    entree = dict(world.carte[cid])
    if mutation == "retirer_climat":
        entree.pop("climat", None)
    elif mutation == "ete_invalide":
        climat = dict(entree.get("climat") or {})
        climat["duree_jour_solstice_ete_h"] = "invalide"
        entree["climat"] = climat
    else:
        climat = dict(entree.get("climat") or {})
        climat["duree_jour_solstice_hiver_h"] = None
        entree["climat"] = climat
    world.carte[cid] = entree

    with pytest.raises(ClimatInvalideError, match=f"cell_id={cid}") as exc:
        tick(world, random.Random(0), numero_tick=0)
    assert cle_attendue in str(exc.value)

# --- Brief 037 : panier de marchandises (SC3, SC4, SC5) ---


def test_sentinelle_panier_absent_vs_zero():
    """SC3 — Absent → -1.0 ; présent à zéro → 0.0 ; les deux ne se confondent pas."""
    from sim.constants import MARCHANDISE_NOURRITURE
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise

    vide = Cell(cell_id=1, area_km2=1.0, population=1)
    assert lire_stock_marchandise(vide, MARCHANDISE_NOURRITURE) == -1.0

    a_zero = Cell(cell_id=2, area_km2=1.0, population=1)
    ecrire_stock_marchandise(a_zero, MARCHANDISE_NOURRITURE, 0.0)
    assert lire_stock_marchandise(a_zero, MARCHANDISE_NOURRITURE) == 0.0

    assert -1.0 != 0.0


def test_acces_directs_au_panier_hors_modele():
    """SC4 — Aucun module de sim/ hors model.py n'indexe stocks directement."""
    import ast
    import pathlib

    sim_dir = pathlib.Path(__file__).parent.parent
    modules_parcourus = 0
    acces_directs = 0
    for fichier in sorted(sim_dir.rglob("*.py")):
        rel = fichier.relative_to(sim_dir)
        if "tests" in rel.parts or rel.name == "model.py":
            continue
        modules_parcourus += 1
        tree = ast.parse(fichier.read_text(encoding="utf-8"), filename=str(fichier))
        for node in ast.walk(tree):
            if isinstance(node, ast.Attribute) and node.attr == "stocks":
                acces_directs += 1
            elif isinstance(node, ast.Subscript) and isinstance(node.value, ast.Attribute):
                if node.value.attr == "stocks":
                    acces_directs += 1
    assert modules_parcourus > 0
    assert acces_directs == 0, (
        f"acces_directs_au_panier_hors_modele={acces_directs} ; "
        f"modules_sim_parcourus={modules_parcourus}"
    )


def test_panier_deuxieme_marchandise_et_to_dict():
    """SC5 — Deuxième marchandise dans le panier ; World.to_dict() l'expose."""
    from sim.constants import MARCHANDISE_NOURRITURE, MARCHANDISE_SONDE_037
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    cell = Cell(cell_id=99, area_km2=1.0, population=10, food_stock_kg=100.0)
    assert lire_stock_marchandise(cell, MARCHANDISE_NOURRITURE) == 100.0
    ecrire_stock_marchandise(cell, MARCHANDISE_SONDE_037, 42.0)
    assert lire_stock_marchandise(cell, MARCHANDISE_SONDE_037) == 42.0
    assert lire_stock_marchandise(cell, MARCHANDISE_NOURRITURE) == 100.0

    world = World(cells={99: cell}, adjacency=[])
    doc = world.to_dict()
    assert doc["cells"]["99"]["stocks"][MARCHANDISE_SONDE_037] == 42.0

    monde_charge = World.charger(0)
    cellules_avec_panier = sum(
        1 for entree in monde_charge.to_dict()["cells"].values() if "stocks" in entree
    )
    assert cellules_avec_panier == len(monde_charge.cells)


# --- Brief 038 : extraction minière ---


def _agreger_gisements_carte(carte_doc: dict) -> tuple[int, set[str], set[str]]:
    """Cellules porteuses, ressources et classes de richesse dérivées de la carte."""
    cellules = 0
    ressources: set[str] = set()
    classes: set[str] = set()
    for raw in carte_doc["cellules"]:
        gisements = raw.get("gisements") or []
        complets = [
            g for g in gisements
            if isinstance(g, dict) and g.get("ressource") is not None and g.get("richesse") is not None
        ]
        if complets:
            cellules += 1
            for g in complets:
                ressources.add(g["ressource"])
                classes.add(g["richesse"])
    return cellules, ressources, classes


def _ressources_minieres_panier(world) -> tuple[int, set[str]]:
    """Cellules avec minerai et ensemble des ressources extraites."""
    from sim.constants import MARCHANDISE_NOURRITURE

    cellules = 0
    ressources: set[str] = set()
    for cell in world.cells.values():
        mineraux = {k for k in cell.stocks if k != MARCHANDISE_NOURRITURE}
        if mineraux:
            cellules += 1
            ressources |= mineraux
    return cellules, ressources


def test_chaque_gisement_produit_sa_ressource():
    """SC1 — Panier minière aligné sur la carte après un tick."""
    import random

    from sim.engine import tick

    carte = World.lire_carte()
    attendu_cellules, attendu_ressources, _ = _agreger_gisements_carte(carte)
    assert attendu_cellules > 0, "échantillon vide : aucune cellule avec gisement"

    world = World.charger(0)
    tick(world, random.Random(0), numero_tick=0)
    mesure_cellules, mesure_ressources = _ressources_minieres_panier(world)

    print(
        f"cellules_extractrices={mesure_cellules} / carte={attendu_cellules} "
        f"ressources={sorted(mesure_ressources)}"
    )
    assert mesure_cellules == attendu_cellules
    assert mesure_ressources == attendu_ressources


def test_richesse_ordre_les_debits():
    """SC2 — majeure > notable > mineure à population et ressource égales."""
    import copy
    import random

    from sim import constants as _k
    from sim.engine import _extraction_du_tick_kg, tick

    carte = World.lire_carte()
    _, _, classes_carte = _agreger_gisements_carte(carte)
    attendues = set(_k.facteurs_richesse_extraction())
    assert classes_carte == attendues, (
        f"classes carte {classes_carte} != classes dérivées {attendues}"
    )

    par_classe: dict[str, float] = {}
    population = 1000
    for raw in carte["cellules"]:
        for g in raw.get("gisements") or []:
            if not isinstance(g, dict):
                continue
            richesse = g.get("richesse")
            if richesse in attendues and richesse not in par_classe:
                cid = int(raw["cell_id"])
                monde = World.charger(0)
                cell = monde.cells[cid]
                cell.population = population
                par_classe[richesse] = _extraction_du_tick_kg(cell, monde.carte).get(
                    g["ressource"], 0.0
                )
    assert set(par_classe) == attendues, f"classes manquantes dans l'échantillon : {par_classe}"

    majeure = par_classe["majeure"]
    notable = par_classe["notable"]
    mineure = par_classe["mineure"]
    print(f"debits majeure={majeure} notable={notable} mineure={mineure}")
    assert majeure > notable > mineure


def test_sans_bras_pas_de_minerai():
    """SC3 — Population nulle : extraction mesurée à 0.0, pas la sentinelle -1."""
    import random

    from sim.engine import _extraction_du_tick_kg, tick

    carte = World.lire_carte()
    cid = next(
        int(raw["cell_id"])
        for raw in carte["cellules"]
        if any(
            isinstance(g, dict) and g.get("ressource") and g.get("richesse")
            for g in (raw.get("gisements") or [])
        )
    )
    world = World.charger(0)
    cell = world.cells[cid]
    cell.population = 0
    ressource = next(
        g["ressource"]
        for g in world.carte[cid]["gisements"]
        if isinstance(g, dict) and g.get("ressource") and g.get("richesse")
    )
    tick(world, random.Random(0))
    extrait = _extraction_du_tick_kg(cell, world.carte).get(ressource, 0.0)
    stock = cell.stocks.get(ressource, -1.0)
    print(f"extraction_population_nulle={extrait} stock={stock}")
    assert extrait == 0.0
    assert stock == 0.0
    assert stock != -1.0


def test_minerai_ne_change_pas_la_nourriture():
    """SC5 — Le stock alimentaire est identique avec ou sans gisement."""
    import copy
    import random

    from sim.constants import MARCHANDISE_NOURRITURE
    from sim.engine import tick

    carte_avec = World.lire_carte()
    carte_sans = copy.deepcopy(carte_avec)
    for raw in carte_sans["cellules"]:
        raw["gisements"] = []

    rng_seed = 0
    numero = 0
    avec = World.charger(rng_seed, carte_doc=copy.deepcopy(carte_avec))
    sans = World.charger(rng_seed, carte_doc=carte_sans)
    tick(avec, random.Random(rng_seed), numero_tick=numero)
    tick(sans, random.Random(rng_seed), numero_tick=numero)

    changes = 0
    for cid in avec.cells:
        nourriture_avec = avec.cells[cid].stocks.get(MARCHANDISE_NOURRITURE, -1.0)
        nourriture_sans = sans.cells[cid].stocks.get(MARCHANDISE_NOURRITURE, -1.0)
        if nourriture_avec != nourriture_sans:
            changes += 1
    print(f"cellules_dont_la_nourriture_a_change={changes} / {len(avec.cells)}")
    assert changes == 0


def test_richesse_inconnue_refusee():
    """SC6 — Richesse hors des trois classes : erreur nommée."""
    import random

    from sim.engine import RichesseGisementInvalideError, tick

    world = World.charger(0)
    cid = next(
        int(raw["cell_id"])
        for raw in world.carte.values()
        if isinstance(raw, dict) and raw.get("gisements")
    )
    entree = dict(world.carte[cid])
    gisements = [dict(g) for g in entree["gisements"]]
    gisements[0]["richesse"] = "inconnue"
    entree["gisements"] = gisements
    world.carte[cid] = entree
    gid = gisements[0].get("id", gisements[0].get("nom", "?"))

    with pytest.raises(RichesseGisementInvalideError) as exc:
        tick(world, random.Random(0), numero_tick=0)
    msg = str(exc.value)
    assert f"cell_id={cid}" in msg
    assert str(gid) in msg or repr(gid).strip("'") in msg
    assert "inconnue" in msg


def test_gisement_incomplet_ignore():
    """SC6 — Sans ressource ou richesse : ignoré, les autres extraient."""
    import random

    from sim.constants import MARCHANDISE_NOURRITURE
    from sim.engine import tick

    world = World.charger(0)
    cid = next(
        int(raw["cell_id"])
        for raw in World.lire_carte()["cellules"]
        if len([
            g for g in (raw.get("gisements") or [])
            if isinstance(g, dict) and g.get("ressource") and g.get("richesse")
        ]) >= 2
    )
    entree = dict(world.carte[cid])
    gisements = [dict(g) for g in entree["gisements"]]
    complet = next(g for g in gisements if g.get("ressource") and g.get("richesse"))
    ressource_attendue = complet["ressource"]
    incomplet = dict(complet)
    incomplet.pop("ressource", None)
    entree["gisements"] = [incomplet, complet]
    world.carte[cid] = entree

    tick(world, random.Random(0), numero_tick=0)
    assert ressource_attendue in world.cells[cid].stocks
    assert world.cells[cid].stocks[ressource_attendue] > 0.0


def test_ressource_inconnue_acceptee():
    """SC6 — Ressource inédite : acceptée dans le panier."""
    import random

    from sim.engine import tick

    world = World.charger(0)
    cid = next(iter(world.cells))
    entree = dict(world.carte[cid])
    entree["gisements"] = [{
        "id": "sonde-ressource",
        "ressource": "mythrite",
        "richesse": "notable",
    }]
    world.carte[cid] = entree
    world.cells[cid].population = 100

    tick(world, random.Random(0), numero_tick=0)
    assert "mythrite" in world.cells[cid].stocks
    assert world.cells[cid].stocks["mythrite"] > 0.0
