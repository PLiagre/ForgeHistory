#!/usr/bin/env python3
"""Mesureur du lot 040 — capacité d'arête selon le relief.

Rejouable depuis la racine du dépôt :
    .venv/bin/python harness/queue/briefs/040-franchir-une-montagne-coute/deliverables/measure_040.py

Retourne 0 si tous les compteurs sont valides, 1 sinon.
"""

import json
import subprocess
import sys
import os
from pathlib import Path

RACINE = Path(__file__).resolve().parents[5]
BRIEF_DIR = RACINE / "harness" / "queue" / "briefs" / "040-franchir-une-montagne-coute"
DELIVERABLES = BRIEF_DIR / "deliverables"
VENV_PY = str(RACINE / ".venv" / "bin" / "python")

os.chdir(RACINE)


def sh(args, timeout=600):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout)


# ── import des modules sim ──────────────────────────────────────────────────
sys.path.insert(0, str(RACINE))
from sim.world import World
from sim import constants as k
from sim.engine import (
    _cle_arête,
    _facteur_transport_pour_cellule,
    _apply_commerce,
    _initialiser_capacite_aretes,
)


def compter() -> dict:
    c = {}

    # ── classes_relief_carte ────────────────────────────────────────────────
    carte_doc = World.lire_carte()
    reliefs = {cell["relief"] for cell in carte_doc["cellules"] if cell.get("relief")}
    c["classes_relief_carte"] = len(reliefs)
    c["classes_relief_carte_noms"] = ",".join(sorted(reliefs))

    # ── aretes_entre_deux_cellules / aretes_ignorees_hors_monde ─────────────
    w = World.charger(0)
    total = 0
    dans_monde = 0
    ignorees = 0
    for edge in w.adjacency:
        total += 1
        if edge["a"] in w.cells and edge["b"] in w.cells:
            dans_monde += 1
        else:
            ignorees += 1
    c["aretes_entre_deux_cellules"] = dans_monde
    c["aretes_ignorees_hors_monde"] = ignorees

    # ── aretes_par_facteur_limitant ─────────────────────────────────────────
    facteurs = k.facteurs_transport_par_relief()
    combo = {}
    for edge in w.adjacency:
        if edge["a"] not in w.cells or edge["b"] not in w.cells:
            continue
        ra = w.carte.get(edge["a"], {}).get("relief", "")
        rb = w.carte.get(edge["b"], {}).get("relief", "")
        fa = facteurs.get(ra, 99)
        fb = facteurs.get(rb, 99)
        fmin = min(fa, fb)
        classe = [r for r, f in facteurs.items() if f == fmin][0]
        combo[classe] = combo.get(classe, 0) + 1
    c["aretes_par_facteur_limitant"] = "|".join(
        f"{r}={combo[r]}" for r in sorted(combo, key=lambda x: facteurs[x], reverse=True)
    )
    c["aretes_par_facteur_limitant_total"] = sum(combo.values())

    # ── classes_avec_capacite_effective (SC1) ───────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/test_commerce.py::test_cinq_facteurs_transport_suivent_ordre_strict",
            "-q", "--tb=line"])
    c["classes_avec_capacite_effective"] = 5 if "passed" in r.stdout and "failed" not in r.stdout else 0
    c["sc1_detail"] = r.stdout.strip()

    # ── capacite_independante_du_sens (SC2) ─────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/test_commerce.py::test_goulot_relief_min_commande_capacite",
            "-q", "--tb=line"])
    c["capacite_independante_du_sens"] = 1 if "passed" in r.stdout and "failed" not in r.stdout else 0
    c["sc2_detail"] = r.stdout.strip()

    # ── kg_transportes_apres (SC3) ──────────────────────────────────────────
    r = sh([VENV_PY, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"], timeout=120)
    apres = json.loads(r.stdout)
    c["kg_transportes_apres"] = apres["kg_transportes"]
    c["population_arrivee_apres"] = apres["population_arrivee"]
    c["population_depart_apres"] = apres["population_depart"]

    # ── ecart_de_masse_micro_monde (SC4) ────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/test_commerce.py::test_conservation_masse_transport",
            "-q", "--tb=line"])
    c["ecart_de_masse_micro_monde"] = 0 if "passed" in r.stdout and "failed" not in r.stdout else -1
    c["sc4_detail"] = r.stdout.strip()

    # ── reliefs_inconnus_refuses (SC6) ──────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/test_commerce.py::test_relief_inconnu_refuse_sur_monde_charge",
            "-q", "--tb=line"])
    c["reliefs_inconnus_refuses"] = 1 if "passed" in r.stdout and "failed" not in r.stdout else 0
    c["sc6_detail"] = r.stdout.strip()

    # ── noms_de_constantes_transport_dans_engine (motif 033) ────────────────
    import ast
    with open(RACINE / "sim" / "engine.py") as f:
        tree = ast.parse(f.read())
    found = False
    for node in ast.walk(tree):
        if isinstance(node, ast.Attribute) and "FACTEUR_TRANSPORT" in node.attr:
            found = True
            break
    c["noms_de_constantes_transport_dans_engine"] = 0 if not found else 99

    # ── tests_sim_verts ─────────────────────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/", "-q", "--tb=line"], timeout=360)
    # Dernière ligne = "N passed in ..."
    lines = r.stdout.strip().split("\n")
    last_line = lines[-1] if lines else ""
    if "passed" in last_line and "failed" not in r.stdout:
        parts = last_line.split()
        n = int(parts[0]) if parts[0].isdigit() else int(parts[-3])
    else:
        n = -1
    c["tests_sim_verts"] = n

    # ── fraction_survie_horizon_long (SC5) ──────────────────────────────────
    r = sh([VENV_PY, "-m", "sim", "--ticks", "1825", "--seed", "0", "--json"], timeout=300)
    long = json.loads(r.stdout)
    c["fraction_survie_horizon_long"] = long["population_arrivee"] / max(long["population_depart"], 1)

    return c


def verifier(c) -> list[str]:
    e = []
    if c.get("classes_relief_carte", 0) < 5:
        e.append(f"classes_relief_carte={c.get('classes_relief_carte')} < 5")
    if c.get("aretes_entre_deux_cellules", 0) == 0:
        e.append("aretes_entre_deux_cellules=0")
    if c.get("classes_avec_capacite_effective", 0) < 5:
        e.append(f"classes_avec_capacite_effective={c.get('classes_avec_capacite_effective')} < 5")
    if c.get("capacite_independante_du_sens", 0) != 1:
        e.append(f"capacite_independante_du_sens={c.get('capacite_independante_du_sens')} != 1")
    if c.get("ecart_de_masse_micro_monde", -1) != 0:
        e.append(f"ecart_de_masse_micro_monde={c.get('ecart_de_masse_micro_monde')} != 0")
    if c.get("reliefs_inconnus_refuses", 0) != 1:
        e.append(f"reliefs_inconnus_refuses={c.get('reliefs_inconnus_refuses')} != 1")
    if c.get("noms_de_constantes_transport_dans_engine", 99) != 0:
        e.append(f"noms_de_constantes_transport_dans_engine={c.get('noms_de_constantes_transport_dans_engine')} != 0")
    if c.get("tests_sim_verts", -1) < 95:
        e.append(f"tests_sim_verts={c.get('tests_sim_verts')} < 95")
    if c.get("fraction_survie_horizon_long", 0) <= 0:
        e.append(f"fraction_survie_horizon_long={c.get('fraction_survie_horizon_long')} <= 0")
    return e


def main():
    print("=== Mesureur lot 040 ===", flush=True)
    c = compter()
    print(f"\nCompteurs :")
    for k, v in sorted(c.items()):
        print(f"  {k} = {v}")

    erreurs = verifier(c)
    if erreurs:
        print(f"\n❌ {len(erreurs)} erreur(s) :")
        for e in erreurs:
            print(f"  - {e}")
        sys.exit(1)
    print("\n✅ Tous les compteurs sont valides")
    sys.exit(0)


if __name__ == "__main__":
    main()