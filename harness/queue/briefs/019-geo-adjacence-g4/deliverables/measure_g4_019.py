#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 019 (G4 — adjacence maritime).

Chaque compteur est imprimé avec **son dénominateur**, et chaque valeur est
dérivée à l'exécution des artefacts, des journaux, des constantes et de l'état
git. Aucune valeur n'est écrite à la main ici, aucune empreinte hexadécimale
n'est recopiée : les empreintes se comparent en les calculant.

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py

Options :
  --rerun-proof   rejoue réellement `tests/run_proof_g4.py` et mesure son code
                  de sortie au lieu de lire celui que la preuve a enregistré.
  --no-pytest     n'exécute pas la suite du harnais (compteur non calculé, -1).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
GEO = REPO / "pipeline" / "geo"
ART = GEO / "artifacts"
LOGS = GEO / "logs"
CAPTURE = GEO / "capture"
REGISTRY = GEO / "registry"
LEGACY = GEO / "legacy_game_data"
BRIEF = REPO / "harness" / "queue" / "briefs" / "019-geo-adjacence-g4"
PY = REPO / ".venv" / "bin" / "python"

# Sentinelle « non calculé » du projet (règle durement acquise n° 8).
NOT_COMPUTED = -1

if str(GEO) not in sys.path:
    sys.path.insert(0, str(GEO))

import constants as C  # noqa: E402

ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True
    ).stdout


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesurés du brief 019")
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    # ---------------------------------------------------------------- entrées
    unity_names = (
        REPO / "unity" / "game_unity" / "Assets" / "StreamingAssets" / "data"
        / "sea_zones.json"
    )
    copy_names = LEGACY / "sea_zones.json"
    identical = int(sha256_of(unity_names) == sha256_of(copy_names))
    report("copie_sea_zones_identique", identical, "1 comparaison d'empreintes calculees a l'execution")

    legacy_zones = load(copy_names)["sea_zones"]
    attested = sorted({str(z["name"]) for z in legacy_zones})
    report(
        "noms_attestes_lus",
        len(attested),
        f"{len(legacy_zones)} entrees du tableau sea_zones de legacy_game_data/sea_zones.json",
    )

    cells = load(ART / "cells_g3.json")["cells"]
    cell_ids = {int(c["cell_id"]) for c in cells}
    cell_count_declared = int(load(ART / "stats_g3.json")["cell_count"])
    report(
        "cellules_lues_g3",
        len(cells),
        f"{cell_count_declared} = cell_count lu de artifacts/stats_g3.json",
    )

    # ------------------------------------------------------------ zones de mer
    zones = load(ART / "sea_zones_g4.json")["sea_zones"]
    stats = load(ART / "stats_g4.json")
    zone_ids = [int(z["zone_id"]) for z in zones]
    report(
        "zones_mer_denombrees",
        len(zones),
        f"fourchette lue de constants.py [{C.SEA_ZONE_COUNT_MIN}, {C.SEA_ZONE_COUNT_MAX}]",
    )
    total_components = int(stats["sea_component_count"])
    covered = len({int(z["component_index"]) for z in zones})
    report(
        "composantes_mer_totales",
        total_components,
        f"{total_components} composantes d'eau retenues comme mer de 1400",
    )
    report(
        "composantes_mer_couvertes",
        covered,
        f"{total_components} composantes_mer_totales",
    )
    report(
        "plans_eau_exclus_lacs",
        int(stats["excluded_lake_count"]),
        f"{int(stats['water_bodies_examined_count'])} plans d'eau examines au-dessus"
        f" de la tolerance geometrique (dont"
        f" {int(stats['enclosed_water_examined_count'])} enclaves)",
    )
    report(
        "collisions_id_mer_terre",
        len(set(zone_ids) & cell_ids),
        f"{len(zones)} zones_mer_denombrees confrontees a {len(cell_ids)} cell_id",
    )
    report(
        "ids_mer_sous_la_base",
        sum(1 for z in zone_ids if z < C.SEA_ZONE_ID_BASE),
        f"{len(zones)} zones_mer_denombrees ; base lue SEA_ZONE_ID_BASE={C.SEA_ZONE_ID_BASE}",
    )

    # ---------------------------------------------------------------- le graphe
    edges = load(ART / "adjacency_g4.json")["adjacency"]
    total_edges = len(edges)
    by_kind = {k: 0 for k in ("land-land", "land-sea", "sea-sea", "strait")}
    for e in edges:
        by_kind[e["kind"]] = by_kind.get(e["kind"], 0) + 1
    report("aretes_totales", total_edges, f"{total_edges} aretes de artifacts/adjacency_g4.json")
    report("aretes_terre_terre", by_kind["land-land"], f"{total_edges} aretes_totales")
    report("aretes_terre_mer", by_kind["land-sea"], f"{total_edges} aretes_totales")
    report("aretes_mer_mer", by_kind["sea-sea"], f"{total_edges} aretes_totales")
    report("aretes_detroit", by_kind["strait"], f"{total_edges} aretes_totales")
    stats_kinds = stats["by_kind"]
    report(
        "kinds_non_vides",
        sum(1 for v in stats_kinds.values() if int(v) > 0),
        f"{len(stats_kinds)} types declares dans stats_g4.json by_kind",
    )
    report(
        "aretes_avec_id_mer_placeholder",
        sum(1 for e in edges if C.SEA_CELL_ID in (int(e["a"]), int(e["b"]))),
        f"{total_edges} aretes_totales ; identifiant fourre-tout lu SEA_CELL_ID={C.SEA_CELL_ID}",
    )
    zone_id_set = set(zone_ids)
    coastal = {
        (int(e["a"]) if int(e["a"]) not in zone_id_set else int(e["b"]))
        for e in edges
        if e["kind"] == "land-sea"
    }
    report("cellules_littorales", len(coastal), f"{len(cells)} cellules_lues_g3")

    # --------------------------------------------------------------- detroits
    report(
        "seuil_detroit_m",
        C.G4_STRAIT_MAX_WIDTH_M,
        "1 valeur lue de constants.py (G4_STRAIT_MAX_WIDTH_M)",
    )
    gaps = sorted(float(e["gap_m"]) for e in edges if e["kind"] == "strait")
    report(
        "ecart_min_detroit_m",
        gaps[0] if gaps else NOT_COMPUTED,
        f"{by_kind['strait']} aretes_detroit ; doit rester <= seuil_detroit_m",
    )
    report(
        "detroits_entre_masses_differentes",
        sum(1 for e in edges if e["kind"] == "strait" and e.get("crosses_land_masses")),
        f"{by_kind['strait']} aretes_detroit",
    )

    # ------------------------------------------------- liens declares, bassins
    corrections = [
        c
        for c in load(GEO / "data" / "corrections_1400.json")["corrections"]
        if c.get("operation") == "declare_topology_link"
    ]
    topo = load(ART / "topology_links_g4.json")
    links = topo["links"]
    report(
        "liens_topologiques_declares_appliques",
        len(links),
        f"{len(corrections)} corrections declare_topology_link lues de data/corrections_1400.json",
    )
    qa = load(LOGS / "v1_050_qa.json")
    g4b = qa["g4b"]
    basins_total = int(g4b["enclosed_component_count"])
    report(
        "bassins_enfermes_total",
        basins_total,
        f"{total_components} composantes_mer_totales",
    )
    report(
        "bassins_enfermes_non_atteignables_liens_actifs",
        len(g4b["unreachable_links_on"]),
        f"{basins_total} bassins_enfermes_total",
    )
    report(
        "bassins_enfermes_non_atteignables_liens_inactifs",
        len(g4b["unreachable_links_off"]),
        f"{basins_total} bassins_enfermes_total",
    )
    outer_named = {
        str(z["name"]): int(z["zone_id"]) for z in zones if not z["enclosed"]
    }
    target_ok = int(
        bool(links)
        and all(
            outer_named.get(str(link["attested_target_name"])) is not None
            and any(
                int(z["zone_id"]) == int(link["zone_b"])
                and not z["enclosed"]
                and str(z["name"]) == str(link["attested_target_name"])
                for z in zones
            )
            for link in links
        )
    )
    report(
        "zone_cible_nom_atteste_existe",
        target_ok,
        "1 verification : chaque lien vise une zone de la mer exterieure au nom atteste",
    )

    # ------------------------------------------------------------------- noms
    used = {str(z["name"]) for z in zones if z.get("name")}
    report("zones_nommees", sum(1 for z in zones if z.get("name")), f"{len(zones)} zones_mer_denombrees")
    report(
        "noms_distincts_employes",
        len(used & set(attested)),
        f"{len(attested)} noms_attestes_lus",
    )
    report(
        "noms_attestes_non_employes",
        len(set(attested) - used),
        f"{len(attested)} noms_attestes_lus",
    )
    report(
        "noms_hors_liste_attestee",
        len(used - set(attested)),
        f"{len(used)} noms distincts portes par une zone ; doit valoir 0",
    )

    # ------------------------------------------------------------- ADR-0003
    scanned = [
        ART / "adjacency_g4.json",
        ART / "sea_zones_g4.json",
        ART / "topology_links_g4.json",
        ART / "stats_g4.json",
        ART / "MANIFEST_g4.json",
        REGISTRY / "sea_zone_registry.json",
    ]
    hits = sum(p.read_text(encoding="utf-8").count("province") for p in scanned)
    report(
        "occurrences_province_dans_artefacts_g4",
        hits,
        f"{len(scanned)} fichiers balayes (les six artefacts G4 hors divergence)",
    )
    div_path = ART / "adjacency_divergence_g4.json"
    report(
        "occurrences_province_dans_divergence",
        div_path.read_text(encoding="utf-8").count("province"),
        "1 fichier balaye (artifacts/adjacency_divergence_g4.json)",
    )
    code_files = sorted(GEO.rglob("*.py"))
    code_files = [p for p in code_files if "__pycache__" not in p.parts]
    allowed = {GEO / "steps" / "04_adjacency.py", GEO / "tests" / "run_proof_g4.py"}
    readers = [
        p
        for p in code_files
        if "adjacency_divergence_g4" in p.read_text(encoding="utf-8")
        and p not in allowed
    ]
    report(
        "lecteurs_du_fichier_divergence_hors_qa",
        len(readers),
        f"{len(code_files)} fichiers de code balayes sous pipeline/geo/"
        " (le module qui l'ecrit et la preuve QA qui le lit sont exclus)",
    )
    div = load(div_path)
    legacy_edges = int(div["legacy_edge_count"])
    report("aretes_heritees_confirmees", int(div["confirmed_count"]), f"{legacy_edges} aretes heritees lues")
    report("aretes_heritees_contredites", int(div["contradicted_count"]), f"{legacy_edges} aretes heritees lues")
    report("aretes_heritees_manquantes", int(div["missing_count"]), f"{legacy_edges} aretes heritees lues")

    # ------------------------------------------------------- determinisme, QA
    pairs = qa["determinism"]["sha256"]
    equal_pairs = sum(
        1 for p in pairs.values() if len(p) == 2 and p[0] == p[1] and bool(p[0])
    )
    report(
        "paires_sha_determinisme_egales",
        equal_pairs,
        f"{len(pairs)} paires du bloc determinism.sha256 de logs/v1_050_qa.json",
    )
    checks = qa["checks"]
    report("controles_g4_verts", sum(1 for c in checks if c["passed"]), f"{len(checks)} entrees du tableau checks")
    report(
        "controles_g4_avec_preuve_rouge_non_vide",
        sum(1 for c in checks if str(c.get("red_proof") or "").strip()),
        f"{len(checks)} entrees du tableau checks",
    )

    if args.rerun_proof:
        proc = subprocess.run(
            [str(PY), "tests/run_proof_g4.py"], cwd=GEO, capture_output=True, text=True
        )
        exit_code = proc.returncode
        source = "1 execution reelle de tests/run_proof_g4.py (--rerun-proof)"
    else:
        exit_code = int(qa["exit_code"])
        source = (
            "1 execution : code enregistre par tests/run_proof_g4.py dans"
            " logs/v1_050_qa.json (rejouable avec --rerun-proof)"
        )
    report("code_sortie_run_proof_g4", exit_code, source)

    # ---------------------------------------- empreinte du littoral de 1400
    m3 = load(ART / "MANIFEST_g3.json")
    m4 = load(ART / "MANIFEST_g4.json")
    coast = ART / "coastline_1400.json"
    if coast.is_file():
        equal_coast = int(sha256_of(coast) == str(m3["inputs"]["coastline_1400"]))
        coast_src = (
            "1 comparaison : empreinte calculee de artifacts/coastline_1400.json"
            " contre MANIFEST_g3.json inputs.coastline_1400"
        )
    else:
        equal_coast = int(
            str(m4["inputs"]["coastline_1400"]) == str(m3["inputs"]["coastline_1400"])
        )
        coast_src = (
            "1 comparaison : MANIFEST_g4.json inputs.coastline_1400 (calcule a"
            " l'execution par le module) contre MANIFEST_g3.json inputs.coastline_1400"
        )
    report("empreinte_terre_g4_egale_entree_g3", equal_coast, coast_src)
    m2b = ART / "MANIFEST_g2b.json"
    if coast.is_file() and m2b.is_file():
        report(
            "empreinte_terre_g4_egale_sortie_declaree_g2b",
            int(
                sha256_of(coast)
                == str(load(m2b)["outputs"]["artifacts/coastline_1400.json"])
            ),
            "1 comparaison : meme empreinte calculee contre MANIFEST_g2b.json"
            " outputs (etape qui produit ce littoral)",
        )

    # -------------------------------------------------- bornes d'intention
    report(
        "zones_hors_bornes_intention",
        int(stats["zones_out_of_intent_bounds"]),
        f"{len(zones)} zones_mer_denombrees ; bornes lues de constants.py"
        f" floor={C.G4_SEA_AREA_FLOOR_KM2} ceil={C.G4_SEA_AREA_CEIL_KM2}"
        f" compacite_min={C.G4_SEA_COMPACTNESS_MIN} (non bloquantes)",
    )
    report(
        "zones_exemptees_bassin_entier",
        int(stats["zones_exempt_whole_enclosed_basin"]),
        f"{len(zones)} zones_mer_denombrees",
    )

    # ------------------------------------------------- captures et perimetre
    captures = sorted(CAPTURE.glob("v1_050_*.png"))
    log_md = BRIEF / "deliverables" / "generator-log.md"
    log_text = log_md.read_text(encoding="utf-8") if log_md.is_file() else ""
    described = sum(1 for p in captures if p.name in log_text)
    report(
        "captures_regardees_et_decrites",
        described,
        f"{len(captures)} captures produites (capture/v1_050_*.png)",
    )
    constants_dirty = git("status", "--porcelain", "--", "pipeline/geo/constants.py").strip()
    report(
        "constantes_g4_inchangees",
        int(constants_dirty == ""),
        "1 fichier verifie par git status --porcelain (pipeline/geo/constants.py)",
    )
    shared = [
        "pipeline/geo/pipeline.py",
        "pipeline/geo/qa/checks.py",
        "pipeline/geo/constants.py",
        "pipeline/geo/io_util.py",
        "pipeline/geo/projection.py",
        "pipeline/geo/steps/02_coastline.py",
        "pipeline/geo/steps/02b_corrections_1400.py",
        "pipeline/geo/steps/03_cells.py",
    ]
    dirty_shared = [p for p in shared if git("status", "--porcelain", "--", p).strip()]
    report(
        "fichiers_partages_modifies",
        len(dirty_shared),
        f"{len(shared)} fichiers partages verifies par git status --porcelain",
    )
    declared_proofs = [
        "pipeline/geo/artifacts/sea_zones_g4.json",
        "pipeline/geo/artifacts/adjacency_g4.json",
        "pipeline/geo/artifacts/topology_links_g4.json",
        "pipeline/geo/artifacts/stats_g4.json",
        "pipeline/geo/artifacts/adjacency_divergence_g4.json",
        "pipeline/geo/artifacts/MANIFEST_g4.json",
        "pipeline/geo/registry/sea_zone_registry.json",
        "pipeline/geo/logs/v1_050_qa.json",
        "pipeline/geo/logs/v1_050_adjacency.log",
        "pipeline/geo/logs/v1_050_g4b_links_on.txt",
        "pipeline/geo/logs/v1_050_g4b_links_off.txt",
        "pipeline/geo/capture/v1_050_sea_zones_window.png",
        "pipeline/geo/capture/v1_050_zuiderzee_links_on.png",
        "pipeline/geo/capture/v1_050_zuiderzee_links_off.png",
    ]
    tracked = set(git("ls-files", *declared_proofs).splitlines())
    report(
        "fichiers_preuve_suivis_par_git",
        len(tracked),
        f"{len(declared_proofs)} preuves declarees sous pipeline/geo/ (git ls-files)",
    )

    # ---------------------------------------------------- suite du harnais
    if args.no_pytest:
        report("tests_harness_passed_019", NOT_COMPUTED, "non execute (--no-pytest)")
    else:
        proc = subprocess.run(
            [str(PY), "-m", "pytest", "harness/tests/", "-q"],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        tail = proc.stdout.strip().splitlines()[-1] if proc.stdout.strip() else ""
        passed = int((re.search(r"(\d+) passed", tail) or [0, 0])[1]) if "passed" in tail else 0
        skipped = int((re.search(r"(\d+) skipped", tail) or [0, 0])[1]) if "skipped" in tail else 0
        failed = int((re.search(r"(\d+) failed", tail) or [0, 0])[1]) if "failed" in tail else 0
        collected = passed + skipped + failed
        report(
            "tests_harness_passed_019",
            passed,
            f"{collected} tests collectes dans harness/tests/"
            f" ({skipped} SKIP Linux/Unity declares, {failed} echecs)",
        )

    if args.json:
        print(
            json.dumps(
                [
                    {"name": name, "value": value, "denominator": denominator}
                    for name, value, denominator in ROWS
                ],
                ensure_ascii=False,
                indent=1,
                sort_keys=True,
            )
        )
        return 0

    width = max(len(name) for name, _, _ in ROWS)
    print("compteurs mesures — brief 019 (G4 adjacence maritime)")
    print(f"depot : {REPO}")
    print("-" * (width + 40))
    for name, value, denominator in ROWS:
        print(f"{name.ljust(width)} = {value}   [denominateur : {denominator}]")
    print("-" * (width + 40))
    print(f"{len(ROWS)} compteurs imprimes, chacun avec son denominateur.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
