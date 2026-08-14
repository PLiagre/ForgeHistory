#!/usr/bin/env python
"""Preuve G4 / v1_050 : zones de mer + adjacence typée, deux passes, huit contrôles.

Déroulé (D11 du brief 019) :
  1. les entrées et la terre corrigée de 1400 sont chargées **une fois** ;
  2. la dérivation **et l'export complet** tournent **deux fois**, liens actifs ;
  3. les empreintes des deux passes sont comparées une à une (`q10_determinism`) ;
  4. une troisième passe, **liens coupés**, fournit le rouge naturel de `G4-B` ;
  5. `logs/v1_050_qa.json` et `logs/v1_050_adjacency.log` sont écrits ;
  6. le code de sortie vaut 0 si et seulement si les huit contrôles sont verts,
     chacun avec une preuve rouge non vide, et les deux passes identiques.

Usage, depuis `pipeline/geo/` :
  ../../.venv/bin/python tests/run_proof_g4.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import (  # noqa: E402
    G3_AREA_EPS_M2,
    G4_PIPELINE_VERSION,
    G4_SEA_LLOYD_ITERATIONS,
    G4_SEA_MASTER_SEED,
    G4_SEA_R_CEIL_M,
    G4_SEA_R_FLOOR_M,
    G4_STRAIT_MAX_WIDTH_M,
    SEA_ZONE_COUNT_MAX,
    SEA_ZONE_COUNT_MIN,
    SEA_ZONE_ID_BASE,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402
from qa.checks import run_g4_green  # noqa: E402
from tests.test_qa_red_g4 import run_all_red_g4  # noqa: E402

LOGS = ROOT / "logs"
ARTIFACTS = ROOT / "artifacts"


def _load_adjacency():
    path = ROOT / "steps" / "04_adjacency.py"
    spec = importlib.util.spec_from_file_location("adjacency_g4", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _capture_shas(captures: dict) -> dict:
    return {
        f"capture/{Path(p).name}": sha256_file(Path(p))
        for p in sorted(str(v) for v in captures.values())
    }


def _zone_docs(run: dict) -> list:
    """Les zones telles que les contrôles les lisent : identifiant + géométrie."""
    return [
        {
            "zone_id": int(z["zone_id"]),
            "name": z.get("name"),
            "enclosed": bool(z["enclosed"]),
            "geometry": z["geometry"],
        }
        for z in sorted(run["zones"], key=lambda z: int(z["zone_id"]))
    ]


def main() -> int:
    LOGS.mkdir(parents=True, exist_ok=True)
    adj = _load_adjacency()
    t_all = time.perf_counter()

    # 1) chargement UNIQUE des entrées et de la terre corrigée de 1400.
    context = adj.load_context()

    # 2) deux passes complètes, liens actifs.
    run1 = adj.run_adjacency(True, context=context, export=True, captures=True)
    shas1 = dict(run1["shas"])
    shas1.update(_capture_shas(run1["captures"]))

    run2 = adj.run_adjacency(True, context=context, export=True, captures=True)
    shas2 = dict(run2["shas"])
    shas2.update(_capture_shas(run2["captures"]))

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }
    determinism_match = all(
        len(pair) == 2 and pair[0] == pair[1] and bool(pair[0])
        for pair in sha_pairs.values()
    ) and len(sha_pairs) > 0

    # 3) troisième passe, liens coupés : rouge NATUREL de G4-B (jamais une mutation).
    run_off = adj.run_adjacency(False, context=context, export=False, captures=True)
    unreachable_off = list(run_off["reachability"]["unreachable_named"])
    # Nommer les bassins par leur eau historique, pas seulement par le nom herite.
    historical_of = {int(link["zone_a"]): link for link in run2["links"]}
    for basin in unreachable_off:
        link = historical_of.get(int(basin["zone_id"]))
        basin["historical_name"] = link["enclosed_water_name"] if link else None
        basin["cut_declaration_id"] = link["id"] if link else None

    metrics = run2["metrics"]
    zones = _zone_docs(run2)
    sea_geom = run2["sea"]["sea_geom"]
    adjacency = run2["adjacency"]
    coastal_ids = metrics["coastal_cell_ids"]

    green = run_g4_green(
        land_cells=context["cells"],
        sea_zones=zones,
        sea_geom=sea_geom,
        adjacency=adjacency,
        coastal_ids=coastal_ids,
        unreachable_enclosed=run2["reachability"]["unreachable_enclosed_zone_ids"],
        sha_pairs=sha_pairs,
        area_eps=G3_AREA_EPS_M2,
    )
    reds = run_all_red_g4(
        land_cells=context["cells"],
        sea_zones=zones,
        sea_geom=sea_geom,
        adjacency=adjacency,
        coastal_ids=coastal_ids,
        sha_pairs=sha_pairs,
        unreachable_when_links_cut=unreachable_off,
        sea_id_base=SEA_ZONE_ID_BASE,
        area_eps=G3_AREA_EPS_M2,
    )

    checks_out = []
    all_green_ok = True
    all_red_ok = True
    for check in green:
        proof = reds.get(check.id, {})
        became_red = bool(proof.get("became_red"))
        red_proof = str(proof.get("case") or "") if became_red else ""
        if not became_red:
            all_red_ok = False
        if not check.passed:
            all_green_ok = False
        checks_out.append(
            {
                "id": check.id,
                "name": check.name,
                "passed": bool(check.passed and became_red),
                "detail": check.detail,
                "red_proof": red_proof,
                "green_ok": bool(check.passed),
                "red_ok": became_red,
            }
        )

    # --- faits structurels mesurés (SC1 à SC6), tous dérivés, aucun littéral ---
    cell_ids = set(int(c["cell_id"]) for c in context["cells"])
    zone_ids = [int(z["zone_id"]) for z in zones]
    collisions = sorted(set(zone_ids) & cell_ids)
    below_base = [z for z in zone_ids if z < SEA_ZONE_ID_BASE]
    count_in_range = SEA_ZONE_COUNT_MIN <= len(zones) <= SEA_ZONE_COUNT_MAX
    components_covered = (
        metrics["sea_component_covered_count"] == metrics["sea_component_count"]
    )
    kinds_non_empty = sum(1 for v in metrics["by_kind"].values() if v > 0)
    links_applied_ok = metrics["declared_topology_link_count"] == len(
        context["topology_corrections"]
    )
    reachable_on = run2["reachability"]["all_enclosed_reachable"]
    reachable_off = run_off["reachability"]["all_enclosed_reachable"]

    # Le fichier de divergence est lu ICI, et nulle part ailleurs (D10).
    divergence = read_json(ARTIFACTS / "adjacency_divergence_g4.json")

    structural = {
        "zones_mer_denombrees_dans_la_fourchette_lue": count_in_range,
        "composantes_mer_couvertes_egales_totales": components_covered,
        "collisions_id_mer_terre_nulles": len(collisions) == 0,
        "ids_mer_sous_la_base_nuls": len(below_base) == 0,
        "copie_sea_zones_identique": context["copy_sea_zones_identical"] == 1,
        "cellules_lues_egales_cell_count_g3": len(context["cells"])
        == context["cell_count_declared"],
        "quatre_kinds_non_vides": kinds_non_empty == len(metrics["by_kind"]),
        "aucune_arete_avec_id_mer_placeholder": metrics[
            "edges_with_g3_sea_placeholder"
        ]
        == 0,
        "detroits_entre_masses_differentes": metrics["strait_between_distinct_masses"]
        > 0,
        "liens_declares_tous_appliques": links_applied_ok,
        "cible_nom_atteste_trouvee": metrics["target_attested_name_found"] > 0,
        "bassins_atteignables_liens_actifs": bool(reachable_on),
        "bassins_injoignables_liens_coupes": (not reachable_off)
        and len(unreachable_off) > 0,
        "divergence_qa_only": bool(divergence.get("qa_only")) is True,
        "divergence_comparaison_non_vide": int(divergence["legacy_edge_count"]) > 0,
    }
    structural_ok = all(structural.values())
    ok = all_green_ok and all_red_ok and determinism_match and structural_ok

    qa_report = {
        "pipeline_version": G4_PIPELINE_VERSION,
        "exit_code": 0 if ok else 1,
        "g4b": {
            "enclosed_component_count": metrics["enclosed_component_count"],
            "enclosed_zone_ids": run2["reachability"]["enclosed_zone_ids"],
            "unreachable_links_on": run2["reachability"][
                "unreachable_enclosed_zone_ids"
            ],
            "unreachable_links_off": run_off["reachability"][
                "unreachable_enclosed_zone_ids"
            ],
            "unreachable_links_off_named": unreachable_off,
        },
        "checks": [
            {
                "id": c["id"],
                "name": c["name"],
                "passed": c["passed"],
                "detail": c["detail"],
                "red_proof": c["red_proof"],
            }
            for c in checks_out
        ],
        "determinism": {
            "runs": 2,
            "match": determinism_match,
            "sha256": sha_pairs,
        },
        "structural": structural,
        "coastline_1400_sha_equals_g3_declared_input": bool(
            context["coastline_sha_equal"]
        ),
    }
    write_json(LOGS / "v1_050_qa.json", qa_report)

    # --- sorties du contrôle G4-B, liens actifs / liens coupés (couple qui diffère) ---
    on_lines = [
        "G4-B — atteignabilite des bassins enfermes, liens topologiques declares ACTIFS",
        f"pipeline_version: {G4_PIPELINE_VERSION}",
        f"liens_declares_appliques: {metrics['declared_topology_link_count']}"
        f" / {len(context['topology_corrections'])} declarations lues",
    ]
    for link in run2["links"]:
        on_lines.append(
            f"  lien {link['id']} : {link['enclosed_water_name']} (zone {link['zone_a']})"
            f" <-> {link['attested_target_name']} (zone {link['zone_b']})"
            f" | date={link['date']} certitude={link['certainty']}"
        )
    on_lines += [
        f"bassins_enfermes: {run2['reachability']['enclosed_zone_ids']}",
        f"bassins_injoignables: {run2['reachability']['unreachable_enclosed_zone_ids']}",
        f"all_enclosed_reachable: {reachable_on}",
        "constat: chaque bassin enferme rejoint la mer exterieure par un chemin d'eau.",
    ]
    (LOGS / "v1_050_g4b_links_on.txt").write_bytes(
        ("\n".join(on_lines) + "\n").encode("utf-8")
    )

    off_lines = [
        "G4-B — atteignabilite des bassins enfermes, liens topologiques declares COUPES",
        f"pipeline_version: {G4_PIPELINE_VERSION}",
        "liens_declares_appliques: 0"
        f" / {len(context['topology_corrections'])} declarations lues",
        f"bassins_enfermes: {run_off['reachability']['enclosed_zone_ids']}",
        f"bassins_injoignables: "
        f"{run_off['reachability']['unreachable_enclosed_zone_ids']}",
    ]
    for basin in unreachable_off:
        origine = (
            f" | eau historique : {basin['historical_name']}"
            f", declaration {basin['cut_declaration_id']} coupee"
            if basin.get("historical_name")
            else ""
        )
        off_lines.append(
            f"  injoignable : zone {basin['zone_id']} « {basin['name']} »"
            f" | {basin['area_km2']} km2 | lon={basin['lon']} lat={basin['lat']}"
            f"{origine}"
        )
    off_lines += [
        f"all_enclosed_reachable: {reachable_off}",
        "constat: sans la declaration historique, le monde se referme — "
        "les navires entrent dans ces bassins et n'en ressortent pas.",
    ]
    (LOGS / "v1_050_g4b_links_off.txt").write_bytes(
        ("\n".join(off_lines) + "\n").encode("utf-8")
    )

    # --- journal lisible ---
    elapsed = time.perf_counter() - t_all
    areas = metrics["area_km2"]
    comps = metrics["compactness_polsby_popper"]
    lines = [
        "PIPELINE G4 / v1_050 — zones de mer et adjacence typee",
        f"pipeline_version: {G4_PIPELINE_VERSION}",
        f"projection: {run2['projection'].epsg} (fallback={run2['projection'].fallback})",
        "",
        "=== entrees lues (une seule fois) ===",
        f"  cellules lues: {len(context['cells'])}"
        f" | cell_count declare par stats_g3: {context['cell_count_declared']}",
        f"  aretes terre-terre lues de adjacency_g3.json: {metrics['by_kind']['land-land']}",
        f"  noms de mer attestes lus: {metrics['names_attested_read']}",
        f"  copie sea_zones.json identique au fichier Unity: "
        f"{context['copy_sea_zones_identical']} (empreintes calculees a l'execution)",
        f"  empreinte du littoral 1400 employe = entree declaree par MANIFEST_g3: "
        f"{bool(context['coastline_sha_equal'])}",
        "",
        "=== la mer de 1400 (D2) ===",
        f"  composantes d'eau examinees: {metrics['water_component_count']}",
        f"  composantes retenues comme mer: {metrics['sea_component_count']}"
        f" (dont enfermees: {metrics['enclosed_component_count']})",
        f"  composantes portant au moins une zone: {metrics['sea_component_covered_count']}",
        f"  plans d'eau exclus (lacs): {metrics['excluded_lake_count']}",
        f"  eclats sous la tolerance geometrique ecartes: {metrics['excluded_sliver_count']}",
        f"  eaux enclavees non declarees mer: {metrics['excluded_undeclared_count']}",
        f"  surface de mer: {metrics['sea_area_km2']} km2",
        "",
        "=== semis, Lloyd, Voronoi (D3, parametres LUS de constants.py) ===",
        f"  r_floor_m={G4_SEA_R_FLOOR_M} r_ceil_m={G4_SEA_R_CEIL_M}"
        f" master_seed={G4_SEA_MASTER_SEED} lloyd_iterations={G4_SEA_LLOYD_ITERATIONS}",
        f"  distance de reference mesuree d_ref_m: "
        f"{metrics['spacing_reference_distance_m']}",
        f"  germes: {metrics['seed_count']}"
        f" (dont obligatoires, un par composante: {metrics['seed_mandatory_count']})",
        f"  plafond de compte atteint: {metrics['seed_saturated_at_ceiling']}",
        "",
        "=== zones de mer (D4) ===",
        f"  zones: {metrics['sea_zone_count']}"
        f" | fourchette lue [{SEA_ZONE_COUNT_MIN}, {SEA_ZONE_COUNT_MAX}]"
        f" | dans la fourchette: {count_in_range}",
        f"  identifiants: {metrics['id_range']['min']} .. {metrics['id_range']['max']}"
        f" (base lue: {SEA_ZONE_ID_BASE})",
        f"  collisions avec un cell_id: {len(collisions)}"
        f" | identifiants sous la base: {len(below_base)}",
        f"  surfaces km2 min/mediane/max: {areas['min']} / {areas['median']} / {areas['max']}",
        f"  compacite Polsby-Popper min/mediane/max: "
        f"{comps['min']} / {comps['median']} / {comps['max']}",
        "",
        "=== noms herites (D5 — proxy de localisation, jamais une cle spatiale) ===",
        f"  zones nommees: {metrics['zones_named']} / {metrics['sea_zone_count']}",
        f"  noms distincts employes: {metrics['names_used_distinct']}"
        f" / {metrics['names_attested_read']}",
        f"  noms attestes non employes: {metrics['names_attested_unused']}"
        f" / {metrics['names_attested_read']}",
        f"  noms hors liste attestee: {metrics['names_outside_attested_list']}",
        "",
        "=== graphe typé (D6, D7) ===",
        f"  aretes totales: {metrics['adjacency_count']} | par type: {metrics['by_kind']}",
        f"  cellules littorales (derivees des aretes terre-mer): "
        f"{metrics['coastal_cell_count']} / {len(context['cells'])}",
        f"  aretes portant encore l'identifiant fourre-tout de mer G3: "
        f"{metrics['edges_with_g3_sea_placeholder']}",
        f"  seuil de detroit lu: {G4_STRAIT_MAX_WIDTH_M} m",
        f"  ecart de detroit min/max mesure: {metrics['strait_gap_min_m']}"
        f" / {metrics['strait_gap_max_m']} m",
        f"  detroits entre deux masses terrestres distinctes: "
        f"{metrics['strait_between_distinct_masses']} / {metrics['by_kind']['strait']}",
        "",
        "=== liens topologiques declares (D8) ===",
        f"  declarations lues: {len(context['topology_corrections'])}"
        f" | appliquees: {metrics['declared_topology_link_count']}",
        f"  liens actifs  -> all_enclosed_reachable={reachable_on}"
        f" injoignables={run2['reachability']['unreachable_enclosed_zone_ids']}",
        f"  liens coupes  -> all_enclosed_reachable={reachable_off}"
        f" injoignables={run_off['reachability']['unreachable_enclosed_zone_ids']}",
        "",
        "=== confrontation QA au graphe herite (D10 — jamais une autorite spatiale) ===",
        f"  aretes heritees lues: {divergence['legacy_edge_count']}",
        f"  confirmees: {divergence['confirmed_count']}"
        f" | contredites: {divergence['contradicted_count']}"
        f" | manquantes: {divergence['missing_count']}",
        f"  qa_only: {divergence['qa_only']}",
        "",
        "=== bornes d'intention (D13 — rapportees, non bloquantes) ===",
        f"  zones hors bornes d'intention: {metrics['zones_out_of_intent_bounds']}"
        f" / {metrics['sea_zone_count']}",
        f"  zones exemptees (bassin enferme entier): "
        f"{metrics['zones_exempt_whole_enclosed_basin']} / {metrics['sea_zone_count']}",
        f"  bornes lues: {metrics['intent_bounds']}",
        "",
        "=== controles verts (donnee saine) ===",
    ]
    for c in checks_out:
        lines.append(
            f"  {c['id']} green={c['green_ok']} red_ok={c['red_ok']}"
            f" passed={c['passed']} detail={c['detail']}"
        )
    lines.append("")
    lines.append("=== preuves rouges (un cas par controle ; G4-B = cas naturel) ===")
    for qid in sorted(reds.keys()):
        lines.append(
            f"  {qid}: became_red={reds[qid]['became_red']} case={reds[qid]['case']}"
        )
    lines.append("")
    lines.append("=== determinisme SHA256 (passe 1 vs passe 2) ===")
    for path, pair in sorted(sha_pairs.items()):
        lines.append(f"  {path}: match={pair[0] == pair[1]}")
        lines.append(f"    passe1={pair[0]}")
        lines.append(f"    passe2={pair[1]}")
    lines.append("")
    lines.append("=== faits structurels mesures ===")
    for key in sorted(structural):
        lines.append(f"  {key}: {structural[key]}")
    lines.append("")
    lines.append(f"captures: {json.dumps(run2['captures'], ensure_ascii=False)}")
    lines.append(
        f"capture liens coupes: {json.dumps(run_off['captures'], ensure_ascii=False)}"
    )
    lines.append(f"duree totale de la preuve: {elapsed:.3f} s")
    lines.append("")

    if not bool(context["coastline_sha_equal"]):
        lines.append(
            "CONSTAT OUVERT (escalade, D2/SC7) : l'empreinte du littoral corrige de "
            "1400 regenere ici differe de celle que MANIFEST_g3.json declare comme "
            "entree des cellules. Elle est en revanche EGALE a celle que "
            "MANIFEST_g2b.json declare comme sortie de l'etape qui la produit — "
            "les deux valeurs sont calculees a l'execution, aucune n'est recopiee. "
            "Aucune borne n'est deplacee, aucun artefact G3 n'est touche."
        )
    if metrics["zones_out_of_intent_bounds"] > 0:
        lines.append(
            "CONSTAT OUVERT (D13) : "
            f"{metrics['zones_out_of_intent_bounds']} zones sur "
            f"{metrics['sea_zone_count']} sortent des bornes d'INTENTION de surface "
            "ou de compacite. Ces bornes ne sont pas bloquantes et ne sont pas "
            "deplacees. Cause mesuree : la mer retenue fait "
            f"{metrics['sea_area_km2']} km2 pour au plus {SEA_ZONE_COUNT_MAX} zones "
            "(borne d'acceptation lue), soit une surface moyenne tres au-dessus du "
            "plafond d'intention."
        )

    verdict = (
        f"MESURE : {metrics['sea_zone_count']} zones de mer "
        f"(fourchette lue [{SEA_ZONE_COUNT_MIN}, {SEA_ZONE_COUNT_MAX}]), "
        f"{metrics['sea_component_covered_count']}/{metrics['sea_component_count']} "
        f"composantes d'eau couvertes, {metrics['adjacency_count']} aretes "
        f"{metrics['by_kind']}, {metrics['coastal_cell_count']} cellules littorales "
        f"derivees, {metrics['strait_between_distinct_masses']} detroits entre masses "
        f"distinctes (ecart min {metrics['strait_gap_min_m']} m, seuil lu "
        f"{G4_STRAIT_MAX_WIDTH_M} m), {metrics['declared_topology_link_count']} liens "
        f"declares appliques ; liens actifs -> tout bassin atteignable, liens coupes "
        f"-> {len(unreachable_off)} bassins injoignables ; "
        f"{sum(1 for c in checks_out if c['passed'])}/{len(checks_out)} controles verts "
        f"et rouges constates ; deux passes identiques en SHA256="
        f"{'OK' if determinism_match else 'FAIL'} sur {len(sha_pairs)} fichiers"
    )
    lines.append(verdict if ok else "FAIL — " + verdict)

    text = "\n".join(lines) + "\n"
    (LOGS / "v1_050_adjacency.log").write_bytes(text.encode("utf-8"))
    try:
        print(text)
    except UnicodeEncodeError:  # pragma: no cover
        sys.stdout.buffer.write(text.encode("utf-8", errors="replace"))

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
