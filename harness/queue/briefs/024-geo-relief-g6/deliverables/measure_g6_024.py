#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 024 (G6 — relief).

Chaque compteur est imprimé avec **son dénominateur**, dérivé à l'exécution
des artefacts, des journaux, des constantes et de l'état git. Aucune valeur
n'est écrite à la main ici.

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
PY = REPO / ".venv" / "bin" / "python"

NOT_COMPUTED = -1
BASE_REF = "origin/master"

ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


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


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesures du brief 024")
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    lock = load(LOCK)
    dem_tiles = lock["dem"]["tiles"]
    expected_tile_count = int(lock["dem"]["tile_count"])
    expected_collective = lock["dem"]["collective_sha256"]

    cells_g3 = load(ART / "cells_g3.json")["cells"]
    relief_doc = load(ART / "cells_relief_g6.json")
    cell_relief = relief_doc["cells"]
    adj5 = load(ART / "adjacency_g5.json")["adjacency"]
    adj6 = load(ART / "adjacency_g6.json")["adjacency"]
    passes = load(ART / "passes_g6.json")["passes"]
    stats = load(ART / "stats_g6.json")
    qa = load(LOGS / "v1_052_qa.json")

    verified = 0
    tile_shas: dict[str, str] = {}
    for tile_name in sorted(dem_tiles):
        meta = dem_tiles[tile_name]
        stem = Path(tile_name).stem
        path = CACHE / stem / tile_name
        if path.is_file() and sha256_of(path) == meta["sha256"]:
            verified += 1
            tile_shas[tile_name] = meta["sha256"]

    report(
        "tuiles_verifiees",
        verified,
        f"{expected_tile_count} = len(dem.tiles) dans sources.lock",
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

    declared_proofs = [
        "pipeline/geo/steps/06_relief.py",
        "pipeline/geo/tools/fetch_dem_tiles.py",
        "pipeline/geo/tests/run_proof_g6.py",
        "pipeline/geo/tests/test_qa_red_g6.py",
        "pipeline/geo/artifacts/cells_relief_g6.json",
        "pipeline/geo/artifacts/adjacency_g6.json",
        "pipeline/geo/artifacts/passes_g6.json",
        "pipeline/geo/artifacts/stats_g6.json",
        "pipeline/geo/artifacts/MANIFEST_g6.json",
        "pipeline/geo/registry/relief_registry.json",
        "pipeline/geo/logs/v1_052_qa.json",
        "pipeline/geo/logs/v1_052_relief.log",
        "pipeline/geo/capture/v1_052_elevation_window.png",
        "pipeline/geo/capture/v1_052_barriers_passes.png",
    ]
    try:
        tracked = set(git("ls-files", *declared_proofs).splitlines())
        report(
            "fichiers_preuve_suivis_par_git",
            len(tracked),
            f"{len(declared_proofs)} preuves declarees sous pipeline/geo/ (git ls-files)",
        )
    except RuntimeError as exc:
        report(
            "fichiers_preuve_suivis_par_git",
            NOT_COMPUTED,
            f"git ls-files a echoue : {exc}",
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
