#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 024 (G6 — relief).

Chaque compteur est imprimé avec **son dénominateur**, dérivé à l'exécution
des artefacts, du manifeste, des journaux, des constantes et de l'état git.

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/024-geo-relief-g6/deliverables/measure_g6_024.py
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
LOCK = GEO / "sources.lock"
CACHE = GEO / "sources" / "dem_cache"
BRIEF = REPO / "harness" / "queue" / "briefs" / "024-geo-relief-g6"
MANIFEST_PATH = BRIEF / "deliverables" / "manifest.json"
PRE_EDIT_LOCK = BRIEF / "deliverables" / "pre-edit" / "pipeline-geo-sources.lock.orig"
EVAL_RUBRIC = BRIEF / "eval-rubric.md"
GEN_LOG = BRIEF / "deliverables" / "generator-log.md"
PY = REPO / ".venv" / "bin" / "python"

NOT_COMPUTED = -1
BASE_REF = "origin/master"

ROWS: list[tuple[str, object, str]] = []
MEASURED: dict[str, object] = {}


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))
    MEASURED[name] = value


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def git(*args: str) -> str:
    proc = subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True
    )
    if proc.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} a echoue (code {proc.returncode}): "
            f"{(proc.stderr or proc.stdout or '').strip()}"
        )
    return proc.stdout


def paths_changed_vs_base(*paths: str) -> list[str] | int:
    try:
        out = git("diff", f"{BASE_REF}...HEAD", "--name-only", "--", *paths)
    except RuntimeError:
        return NOT_COMPUTED
    return [line.strip() for line in out.splitlines() if line.strip()]


def git_porcelain(*paths: str) -> list[str] | int:
    try:
        out = git("status", "--porcelain", "--", *paths)
    except RuntimeError:
        return NOT_COMPUTED
    return [line.strip() for line in out.splitlines() if line.strip()]


def manifest_proof_paths() -> list[str]:
    manifest = load(MANIFEST_PATH)
    paths: list[str] = []
    base = MANIFEST_PATH.parent.parent  # racine du brief, pas deliverables/
    for entry in manifest.get("files", []):
        rel = entry.get("path", "")
        if not rel:
            continue
        resolved = (base / rel).resolve()
        try:
            paths.append(str(resolved.relative_to(REPO)).replace("\\", "/"))
        except ValueError:
            paths.append(str(resolved))
    return paths


def git_tracked_proofs(proof_paths: list[str]) -> tuple[int, int]:
    try:
        tracked = set(git("ls-files", "--", *proof_paths).splitlines())
        missing = len(proof_paths) - len(tracked)
        return len(tracked), missing
    except RuntimeError:
        return NOT_COMPUTED, NOT_COMPUTED


def count_raster_synthesis_functions() -> int:
    """Fonctions du dépôt capables d'écrire ou synthétiser un raster DEM (D20).

    Dérivation par AST : définitions nommées ``synthes*``, ``*_from_bounds`` hors
    détection CRS, et appels ``rasterio.open(..., mode=écriture)`` — pas de grep
    lexical sur ``from_bounds`` (faux positif ``detect_crs_from_bounds``).
    """
    import ast

    geo = REPO / "pipeline" / "geo"
    write_modes = frozenset({"w", "a", "w+", "r+"})
    hits: set[tuple[str, str]] = set()

    for path in sorted(geo.rglob("*.py")):
        rel = path.relative_to(geo).as_posix()
        if rel.startswith("tests/test_qa_red") or "__pycache__" in rel:
            continue
        try:
            tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        except SyntaxError:
            continue

        for node in tree.body:
            if not isinstance(node, ast.FunctionDef):
                continue
            name = node.name
            if "synthes" in name.lower():
                hits.add((rel, name))
                continue
            if name.endswith("_from_bounds") and not name.startswith("detect_crs"):
                hits.add((rel, name))
                continue

            for child in ast.walk(node):
                if not isinstance(child, ast.Call):
                    continue
                func = child.func
                if not (
                    isinstance(func, ast.Attribute)
                    and func.attr == "open"
                    and isinstance(func.value, ast.Name)
                    and func.value.id == "rasterio"
                ):
                    continue
                mode: str | None = None
                if len(child.args) >= 2 and isinstance(child.args[1], ast.Constant):
                    mode = str(child.args[1].value)
                for kw in child.keywords:
                    if kw.arg == "mode" and isinstance(kw.value, ast.Constant):
                        mode = str(kw.value.value)
                if mode in write_modes:
                    hits.add((rel, name))
                    break

    return len(hits)


def cache_files_hors_lock() -> list[str]:
    lock = load(LOCK)
    lock_names = set(lock["dem"]["tiles"])
    extra: list[str] = []
    if not CACHE.is_dir():
        return extra
    for path in CACHE.rglob("*.tif"):
        if path.name not in lock_names:
            extra.append(str(path))
    return sorted(extra)


def parse_journal_counters(text: str) -> dict[str, object]:
    found: dict[str, object] = {}
    for line in text.splitlines():
        m = re.match(r"^-\s*`([^`]+)`\s*=\s*(.+)$", line.strip())
        if not m:
            continue
        key = m.group(1).strip()
        raw = m.group(2).strip()
        if "/" in raw and re.match(r"^[\d\s]+/", raw):
            num = raw.split("/", 1)[0].strip()
            try:
                found[key] = int(num)
            except ValueError:
                found[key] = num
        elif re.match(r"^-?\d+$", raw):
            found[key] = int(raw)
        elif re.match(r"^-?\d+\.\d+$", raw):
            found[key] = float(raw)
        else:
            found[key] = raw
    return found


def compare_journal_to_measure(journal: dict[str, object]) -> int:
    matches = 0
    for key, jval in journal.items():
        if key not in MEASURED:
            continue
        mval = MEASURED[key]
        if isinstance(jval, (int, float)) and isinstance(mval, (int, float)):
            if float(jval) == float(mval):
                matches += 1
        elif str(jval) == str(mval):
            matches += 1
    return matches


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesures du brief 024")
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    proof_paths = manifest_proof_paths()
    tracked_count, missing_count = git_tracked_proofs(proof_paths)

    lock = load(LOCK)
    dem_tiles = lock["dem"]["tiles"]
    expected_tile_count = int(lock["dem"]["tile_count"])
    expected_collective = lock["dem"]["collective_sha256"]
    orig_dem_licence = lock["dem"]["licence"]

    req_doc = (
        load(ART / "dem_required_tiles_g6.json")
        if (ART / "dem_required_tiles_g6.json").is_file()
        else {}
    )
    req_tiles = req_doc.get("tuiles_requises") or []
    req_counts = req_doc.get("comptes") or {}

    cells_g3 = load(ART / "cells_g3.json")["cells"]
    cell_relief = load(ART / "cells_relief_g6.json")["cells"]
    adj5 = load(ART / "adjacency_g5.json")["adjacency"]
    adj6 = load(ART / "adjacency_g6.json")["adjacency"]
    passes = load(ART / "passes_g6.json")["passes"]
    stats = load(ART / "stats_g6.json")
    qa = load(LOGS / "v1_052_qa.json")

    verified = 0
    tile_shas: dict[str, str] = {}
    sha_mismatch = 0
    for tile_name in sorted(dem_tiles):
        meta = dem_tiles[tile_name]
        stem = Path(tile_name).stem
        path = CACHE / stem / tile_name
        if path.is_file():
            actual = sha256_of(path)
            if actual == meta["sha256"]:
                verified += 1
                tile_shas[tile_name] = meta["sha256"]
            else:
                sha_mismatch += 1

    report(
        "tuiles_verifiees",
        verified,
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "sha256_saisis_a_la_main",
        sha_mismatch,
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "tuiles_requises",
        len(req_tiles),
        "len(tuiles_requises) dans dem_required_tiles_g6.json",
    )
    report(
        "tuiles_presentes_dans_le_lock",
        expected_tile_count,
        f"{len(req_tiles)} tuiles requises derivees",
    )
    report(
        "tuiles_manquantes",
        int(req_counts.get("tuiles_manquantes", NOT_COMPUTED)),
        f"{len(req_tiles)} tuiles requises",
    )
    report(
        "tuiles_excedentaires_restantes",
        int(req_counts.get("tuiles_excedentaires", NOT_COMPUTED)),
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "tuiles_ajoutees",
        len(req_doc.get("tuiles_ajoutees") or []),
        f"{len(req_tiles)} tuiles requises",
    )
    report(
        "tuiles_excedentaires_retirees",
        len(req_doc.get("tuiles_excedentaires_retirees") or []),
        "tuiles de l'instantane pre-edition",
    )

    avail_path = ART / "dem_tile_availability_g6.json"
    if avail_path.is_file():
        avail = load(avail_path)
        report(
            "tuiles_requises_absentes_du_depot_public",
            len(avail.get("tuiles_absentes_du_depot_public") or []),
            f"{len(req_tiles)} tuiles requises",
        )
    else:
        report(
            "tuiles_requises_absentes_du_depot_public",
            NOT_COMPUTED,
            "dem_tile_availability_g6.json absent",
        )

    lock_avail_path = ART / "dem_tile_availability_lock_g6.json"
    if lock_avail_path.is_file():
        lock_avail = load(lock_avail_path)
        report(
            "tuiles_du_lock_absentes_du_depot_public",
            len(lock_avail.get("tuiles_absentes_du_depot_public") or []),
            f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
        )
    else:
        report(
            "tuiles_du_lock_absentes_du_depot_public",
            NOT_COMPUTED,
            "dem_tile_availability_lock_g6.json absent",
        )

    extra_cache = cache_files_hors_lock()
    report(
        "fichiers_du_cache_hors_lock",
        len(extra_cache),
        "fichiers .tif presents dans sources/dem_cache/",
    )
    report(
        "fonctions_de_synthese_de_tuile",
        count_raster_synthesis_functions(),
        "1 ; doit valoir 0",
    )

    if verified == expected_tile_count:
        payload = "".join(f"{n}{tile_shas[n]}" for n in sorted(tile_shas))
        collective = hashlib.sha256(payload.encode("ascii")).hexdigest()
        collective_ok = collective == expected_collective
    else:
        collective_ok = False
    report(
        "empreinte_collective_egale",
        int(collective_ok),
        "1 si empreinte recalculee == dem.collective_sha256 de sources.lock",
    )
    report(
        "recettes_collectives_essayees",
        int(qa.get("dem", {}).get("recettes_collectives_essayees", NOT_COMPUTED)),
        "1 ; doit valoir 1",
    )

    if PRE_EDIT_LOCK.is_file():
        pre = load(PRE_EDIT_LOCK)
        cur = load(LOCK)
        unchanged = sum(
            1 for k in pre if k != "dem" and cur.get(k) == pre[k]
        )
        report(
            "blocs_sources_lock_hors_dem_inchanges",
            unchanged,
            f"{len([k for k in pre if k != 'dem'])} objets hors dem dans l'instantane",
        )
        report(
            "dem_licence_inchangee",
            int(cur["dem"]["licence"] == pre["dem"]["licence"]),
            "1 ; doit valoir 1",
        )
    else:
        report(
            "blocs_sources_lock_hors_dem_inchanges",
            NOT_COMPUTED,
            "instantane pre-edition absent",
        )
        report("dem_licence_inchangee", NOT_COMPUTED, "instantane pre-edition absent")

    sans_echantillon = sum(
        1 for c in cell_relief if int(c.get("sample_count") or 0) <= 0
    )
    report(
        "cellules_sans_echantillon",
        sans_echantillon,
        f"{len(cells_g3)} cellules lues de cells_g3.json",
    )
    report(
        "echantillons_exclus_hors_plage",
        int(stats.get("echantillons_exclus_hors_plage", NOT_COMPUTED)),
        "fait mesure dans stats_g6.json",
    )
    total_reads = stats.get("total_lectures_altitude", NOT_COMPUTED)
    for key, denom in [
        ("echantillons_hors_couverture_dem", f"{total_reads} lectures d'altitude"),
        ("echantillons_nodata_raster", f"{total_reads} lectures d'altitude"),
        ("points_lus_grille", "points grille dans stats_g6.json"),
        ("points_lus_centroides", f"{len(cells_g3)} cellules"),
        ("points_lus_frontieres", "points frontieres dans stats_g6.json"),
        ("cellules_non_mesurees", f"{len(cells_g3)} cellules"),
        ("zones_hautes_sous_une_zone_basse", f"len(A12_RELIEF_MUST_BE_HIGH)={len(C.A12_RELIEF_MUST_BE_HIGH)}"),
        ("lectures_hors_bornes_du_fichier", f"{total_reads} lectures d'altitude"),
        ("echantillons_valeur_zero_exact", f"{total_reads} lectures d'altitude"),
        ("cellules_altitude_min_nulle", f"{len(cells_g3)} cellules"),
        ("points_sur_ligne_de_degre", f"{total_reads} lectures d'altitude"),
        ("couverture_grille", stats.get("couverture_grille", NOT_COMPUTED)),
        ("couverture_centroides", f"{len(cells_g3)} centroïdes"),
        ("couverture_frontieres", stats.get("couverture_frontieres", NOT_COMPUTED)),
    ]:
        report(key, stats.get(key, NOT_COMPUTED), denom)

    report(
        "points_de_bord_multi_tuiles",
        stats.get("points_de_bord_multi_tuiles", NOT_COMPUTED),
        "points de bord a plusieurs tuiles indexantes",
    )
    report(
        "points_de_bord_valeurs_concordantes",
        stats.get("points_de_bord_valeurs_concordantes", NOT_COMPUTED),
        f"{stats.get('points_de_bord_multi_tuiles', NOT_COMPUTED)} points multi-tuiles",
    )

    land_sea_cells: set[int] = set()
    for edge in adj5:
        if edge.get("kind") == "land-sea":
            land_sea_cells.add(int(edge["a"]))
            land_sea_cells.add(int(edge["b"]))
    cells_sans_littoral = len(cells_g3) - len(
        {int(c["cell_id"]) for c in cells_g3 if int(c["cell_id"]) in land_sea_cells}
    )
    report(
        "cellules_sans_littoral_avec_echantillon_a_zero",
        stats.get("cellules_sans_littoral_avec_echantillon_a_zero", NOT_COMPUTED),
        f"{cells_sans_littoral} cellules sans arete land-sea dans adjacency_g5.json",
    )
    report(
        "tuiles_regle_domaine_conforme",
        stats.get("tuiles_regle_domaine_conforme", NOT_COMPUTED),
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "tuiles_registrement_homogene",
        stats.get("tuiles_registrement_homogene", NOT_COMPUTED),
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "registrement_dem_mesure",
        stats.get("registrement_dem_mesure", NOT_COMPUTED),
        "nom du registrement mesure dans stats_g6.json",
    )
    report(
        "demi_pixel_deg",
        stats.get("demi_pixel_deg", NOT_COMPUTED),
        "demi-pixel mesure dans stats_g6.json",
    )
    report(
        "tuiles_bornes_nom_vs_raster_egales",
        qa.get("tuiles_bornes_nom_vs_raster_egales", NOT_COMPUTED),
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
    )
    report(
        "cas_rouges_amendement_non_vides",
        int(qa.get("cas_rouges_amendement_non_vides", NOT_COMPUTED)),
        "7 cas rouges amendement 001+002 attendus",
    )

    land_land = sum(1 for e in adj5 if e.get("kind") == "land-land")
    barriers = sum(1 for e in adj6 if e.get("relief_barrier"))
    report("barrier_count", barriers, f"{land_land} aretes land-land de adjacency_g5.json")
    report(
        "pass_count",
        len(passes),
        f"doit egaler barrier_count={barriers}",
    )

    known_ids = {pid for pid, _, _, _ in C.G6_KNOWN_PASSES}
    named = sum(1 for p in passes if p.get("nom") and p.get("pass_id") in known_ids)
    report(
        "passes_nommes_trouves",
        named,
        f"{len(C.G6_KNOWN_PASSES)} = len(G6_KNOWN_PASSES) lu de constants.py",
    )
    report(
        "below_0_land_km2",
        stats.get("below_0_land_km2"),
        "fait mesure dans stats_g6.json (0.0 accepte)",
    )

    checks = qa.get("checks") or []
    verts = sum(1 for c in checks if c.get("passed"))
    reds = sum(1 for c in checks if str(c.get("red_proof") or "").strip())
    report("controles_g6_verts", verts, f"{len(checks)} entrees du tableau checks")
    report(
        "controles_g6_avec_preuve_rouge_non_vide",
        reds,
        f"{len(checks)} entrees du tableau checks",
    )

    sha_block = (qa.get("determinism") or {}).get("sha256") or {}
    equal = sum(
        1
        for pair in sha_block.values()
        if isinstance(pair, list) and len(pair) == 2 and pair[0] and pair[0] == pair[1]
    )
    report(
        "paires_sha_determinisme_egales",
        equal,
        f"{len(sha_block)} paires du bloc determinism.sha256",
    )

    if args.rerun_proof:
        proc = subprocess.run(
            [str(PY), "tests/run_proof_g6.py"],
            cwd=GEO,
            capture_output=True,
            text=True,
        )
        report(
            "code_sortie_run_proof_g6",
            proc.returncode,
            "1 execution de tests/run_proof_g6.py",
        )
    else:
        report(
            "code_sortie_run_proof_g6",
            int(qa.get("exit_code", NOT_COMPUTED)),
            "exit_code enregistre dans logs/v1_052_qa.json (passer --rerun-proof pour rejouer)",
        )

    changed_const = paths_changed_vs_base("pipeline/geo/constants.py")
    if changed_const == NOT_COMPUTED:
        report("constantes_g6_inchangees", NOT_COMPUTED, f"git diff {BASE_REF}...HEAD a echoue")
    else:
        report(
            "constantes_g6_inchangees",
            int(len(changed_const) == 0),
            f"1 si constants.py absent de git diff {BASE_REF}...HEAD --name-only",
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
        "pipeline/geo/steps/03b_align_coastline_provenance.py",
        "pipeline/geo/steps/04_adjacency.py",
        "pipeline/geo/steps/05_rivers.py",
    ]
    porcelain = git_porcelain(*shared)
    if porcelain == NOT_COMPUTED:
        report("fichiers_partages_modifies", NOT_COMPUTED, "git status --porcelain a echoue")
    else:
        report(
            "fichiers_partages_modifies",
            len(porcelain),
            f"{len(shared)} fichiers interdits verifies par git status --porcelain",
        )

    g5_porcelain = git_porcelain("pipeline/geo/artifacts/adjacency_g5.json")
    if g5_porcelain == NOT_COMPUTED:
        report("adjacency_g5_inchange", NOT_COMPUTED, "git status --porcelain a echoue")
    else:
        report(
            "adjacency_g5_inchange",
            int(len(g5_porcelain) == 0),
            "1 si git status --porcelain sur adjacency_g5.json est vide",
        )

    report(
        "adjacency_g6_differe_de_g5",
        int(sha256_of(ART / "adjacency_g6.json") != sha256_of(ART / "adjacency_g5.json")),
        "1 si empreintes calculees a l'execution different",
    )

    try:
        ignored = git("status", "--porcelain", "--ignored", "pipeline/geo/sources/dem_cache/")
        dem_ignored = int("!!" in ignored or "dem_cache" in ignored)
    except RuntimeError:
        dem_ignored = NOT_COMPUTED
    report(
        "dem_cache_non_suivi",
        dem_ignored,
        "1 si sources/dem_cache/ apparait comme ignore par git status --porcelain --ignored",
    )

    if tracked_count == NOT_COMPUTED:
        report(
            "fichiers_preuve_suivis_par_git",
            NOT_COMPUTED,
            "git ls-files a echoue",
        )
        report("preuves_manquantes_dans_git", NOT_COMPUTED, "git ls-files a echoue")
    else:
        report(
            "fichiers_preuve_suivis_par_git",
            tracked_count,
            f"{len(proof_paths)} preuves declarees dans deliverables/manifest.json",
        )
        report(
            "preuves_manquantes_dans_git",
            missing_count,
            f"{len(proof_paths)} preuves dans manifest.json",
        )

    gen_text = GEN_LOG.read_text(encoding="utf-8") if GEN_LOG.is_file() else ""
    recevabilite_phrases = [
        "tous conformes aux sc",
        "conformes aux sc",
        "recevable",
        "recevabilite",
    ]
    conclusions = sum(
        1 for phrase in recevabilite_phrases if phrase in gen_text.lower()
    )
    report(
        "conclusions_de_recevabilite_dans_le_journal",
        conclusions,
        "1 si le journal contenait une conclusion de recevabilite",
    )

    journal_counters = parse_journal_counters(gen_text)
    journal_matches = compare_journal_to_measure(journal_counters)
    comparable = sum(1 for k in journal_counters if k in MEASURED)
    report(
        "compteurs_du_journal_egaux_a_la_mesure",
        journal_matches,
        f"{comparable} compteurs compares entre journal et mesure",
    )

    rubric_text = EVAL_RUBRIC.read_text(encoding="utf-8") if EVAL_RUBRIC.is_file() else ""
    rubric_amended = int("2026-08-22" in rubric_text and "amend" in rubric_text.lower())
    report(
        "rubrique_amendee_apres_revue",
        rubric_amended,
        "1 si eval-rubric.md porte l'amendement 2026-08-22",
    )

    g3_ids = sorted(int(c["cell_id"]) for c in cells_g3)
    relief_ids = sorted(int(c["cell_id"]) for c in cell_relief)
    report(
        "maille_g6e_egale",
        int(g3_ids == relief_ids),
        f"{len(g3_ids)} cell_id de cells_g3.json vs cells_relief_g6.json",
    )

    if args.no_pytest:
        report("tests_harness_passed_024", NOT_COMPUTED, "non execute (--no-pytest)")
    else:
        proc = subprocess.run(
            [str(PY), "-m", "pytest", "harness/tests/", "-q"],
            cwd=REPO,
            capture_output=True,
            text=True,
        )
        tail = proc.stdout.strip().splitlines()[-1] if proc.stdout.strip() else ""
        passed = (
            int((re.search(r"(\d+) passed", tail) or [0, 0])[1]) if "passed" in tail else 0
        )
        skipped = (
            int((re.search(r"(\d+) skipped", tail) or [0, 0])[1])
            if "skipped" in tail
            else 0
        )
        failed = (
            int((re.search(r"(\d+) failed", tail) or [0, 0])[1]) if "failed" in tail else 0
        )
        collected = passed + skipped + failed
        report(
            "tests_harness_passed_024",
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
    print("compteurs mesures — brief 024 (G6 relief)")
    print(f"depot : {REPO}")
    print("-" * (width + 40))
    for name, value, denominator in ROWS:
        print(f"{name.ljust(width)} = {value}   [denominateur : {denominator}]")
    print("-" * (width + 40))
    print(f"{len(ROWS)} compteurs imprimes, chacun avec son denominateur.")
    return 0


if str(GEO) not in sys.path:
    sys.path.insert(0, str(GEO))

import constants as C  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
