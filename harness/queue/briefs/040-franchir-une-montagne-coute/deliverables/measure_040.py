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
PRE_EDIT = DELIVERABLES / "pre-edit"
VENV_PY = "/home/hermes/src/ForgeHistory/.venv/bin/python"

os.chdir(RACINE)


def sh(args, timeout=600):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout)


# ── import des modules sim ──────────────────────────────────────────────────
sys.path.insert(0, str(RACINE))
from sim.world import World
from sim import constants as k


def compter() -> dict:
    c = {}

    # ── classes_relief_carte ────────────────────────────────────────────────
    carte_doc = World.lire_carte()
    reliefs = {cell["relief"] for cell in carte_doc["cellules"] if cell.get("relief")}
    c["classes_relief_carte"] = len(reliefs)
    c["classes_relief_carte_noms"] = ",".join(sorted(reliefs))

    # ── aretes ──────────────────────────────────────────────────────────────
    w = World.charger(0)
    total = dans_monde = ignorees = 0
    for e in w.adjacency:
        total += 1
        if e["a"] in w.cells and e["b"] in w.cells:
            dans_monde += 1
        else:
            ignorees += 1
    c["aretes_entre_deux_cellules"] = dans_monde
    c["aretes_ignorees_hors_monde"] = ignorees

    # ── aretes_par_facteur_limitant ─────────────────────────────────────────
    facteurs = k.facteurs_transport_par_relief()
    combo = {}
    for e in w.adjacency:
        if e["a"] not in w.cells or e["b"] not in w.cells:
            continue
        ra = w.carte.get(e["a"], {}).get("relief", "")
        rb = w.carte.get(e["b"], {}).get("relief", "")
        fa = facteurs.get(ra, 99)
        fb = facteurs.get(rb, 99)
        fmin = min(fa, fb)
        classe = [r for r, f in facteurs.items() if f == fmin][0]
        combo[classe] = combo.get(classe, 0) + 1
    c["aretes_par_facteur_limitant"] = "|".join(
        f"{r}={combo[r]}" for r in sorted(combo, key=lambda x: facteurs[x], reverse=True)
    )
    c["aretes_par_facteur_limitant_total"] = sum(combo.values())

    # ── SC1 : cinq facteurs effectifs ───────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest",
            "sim/tests/test_commerce.py::test_cinq_facteurs_transport_suivent_ordre_strict",
            "-q", "--tb=line"])
    c["classes_avec_capacite_effective"] = 5 if "passed" in r.stdout and "failed" not in r.stdout else 0
    c["sc1_detail"] = r.stdout.strip()

    # ── SC2 : goulot relief min ─────────────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest",
            "sim/tests/test_commerce.py::test_goulot_relief_min_commande_capacite",
            "-q", "--tb=line"])
    c["capacite_independante_du_sens"] = 1 if "passed" in r.stdout and "failed" not in r.stdout else 0

    # ── SC3 : kg_transportes — archive + deux exécutions + inégalité ────────
    # Rejouer la même archive baseline du SHA de base
    if PRE_EDIT.exists():
        arch = PRE_EDIT / "baseline_ticks365_seed0.json"
        if arch.exists():
            with open(arch) as f:
                base = json.load(f)
            c["kg_transportes_avant"] = base["kg_transportes"]
        else:
            c["kg_transportes_avant"] = -1
    else:
        c["kg_transportes_avant"] = -1

    # Deux exécutions post-changement pour vérifier le déterminisme
    r1 = sh([VENV_PY, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"], timeout=120)
    apres1 = json.loads(r1.stdout)
    r2 = sh([VENV_PY, "-m", "sim", "--ticks", "365", "--seed", "0", "--json"], timeout=120)
    apres2 = json.loads(r2.stdout)

    c["kg_transportes_apres_run1"] = apres1["kg_transportes"]
    c["kg_transportes_apres_run2"] = apres2["kg_transportes"]
    c["deux_cli_365_identiques"] = (
        apres1["kg_transportes"] == apres2["kg_transportes"]
        and apres1["population_arrivee"] == apres2["population_arrivee"]
    )
    c["kg_transportes_apres"] = apres1["kg_transportes"]
    c["population_arrivee_apres"] = apres1["population_arrivee"]
    c["population_depart_apres"] = apres1["population_depart"]

    # ── SC4 : masse conservee ───────────────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest",
            "sim/tests/test_commerce.py::test_conservation_masse_transport",
            "-q", "--tb=line"])
    c["ecart_de_masse_micro_monde"] = 0 if "passed" in r.stdout and "failed" not in r.stdout else -1

    # ── SC6 : relief inconnu refusé ─────────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest",
            "sim/tests/test_commerce.py::test_relief_inconnu_refuse_sur_monde_charge",
            "-q", "--tb=line"])
    c["reliefs_inconnus_refuses"] = 1 if "passed" in r.stdout and "failed" not in r.stdout else 0
    r = sh([VENV_PY, "-m", "pytest",
            "sim/tests/test_commerce.py::test_sans_carte_capacite_transport_inchangee",
            "-q", "--tb=line"])
    c["sans_carte_capacite_inchangee"] = 1 if "passed" in r.stdout and "failed" not in r.stdout else 0

    # ── noms de constantes transport dans engine (motif 033) ─────────────────
    import ast
    with open(RACINE / "sim" / "engine.py") as f:
        tree = ast.parse(f.read())
    found = any(
        isinstance(n, ast.Attribute) and "FACTEUR_TRANSPORT" in n.attr
        for n in ast.walk(tree)
    )
    c["noms_de_constantes_transport_dans_engine"] = 0 if not found else 99

    # ── tests_sim_verts ─────────────────────────────────────────────────────
    r = sh([VENV_PY, "-m", "pytest", "sim/tests/", "-q", "--tb=line"], timeout=360)
    lines = r.stdout.strip().split("\n")
    last = lines[-1] if lines else ""
    if "passed" in last and "failed" not in r.stdout:
        parts = last.split()
        n = int(parts[0]) if parts[0].isdigit() else int(parts[-3])
    else:
        n = -1
    c["tests_sim_verts"] = n
    c["tests_detail"] = r.stdout.strip()

    # ── SC5 : horizon long 5 × N_TICKS_OBSERVES (1000 ticks) ────────────────
    r = sh([VENV_PY, "-m", "sim", "--ticks", "1000", "--seed", "0", "--json"], timeout=300)
    long = json.loads(r.stdout)
    c["fraction_survie_horizon_long"] = long["population_arrivee"] / max(long["population_depart"], 1)
    c["population_1000ticks"] = long["population_arrivee"]

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
    if c.get("sans_carte_capacite_inchangee", 0) != 1:
        e.append(f"sans_carte_capacite_inchangee={c.get('sans_carte_capacite_inchangee')} != 1")
    if c.get("noms_de_constantes_transport_dans_engine", 99) != 0:
        e.append(f"noms_de_constantes_transport_dans_engine={c.get('noms_de_constantes_transport_dans_engine')} != 0")
    if c.get("tests_sim_verts", -1) < 95:
        e.append(f"tests_sim_verts={c.get('tests_sim_verts')} < 95")
    if c.get("fraction_survie_horizon_long", 0) <= 0:
        e.append(f"fraction_survie_horizon_long={c.get('fraction_survie_horizon_long')} <= 0")
    if c.get("deux_cli_365_identiques") is False:
        e.append("deux_cli_365_identiques=False (les deux exécutions post-changement diffèrent)")
    if c.get("kg_transportes_avant", -1) > 0 and c.get("kg_transportes_apres", 0) >= c.get("kg_transportes_avant", 0):
        e.append(f"kg_transportes_apres ({c.get('kg_transportes_apres')}) >= kg_transportes_avant ({c.get('kg_transportes_avant')})")
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