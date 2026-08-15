#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 021 (G5 — fleuves).

Chaque compteur est imprimé avec **son dénominateur**, dérivé à l'exécution
des artefacts, des journaux, des constantes et de l'état git. Aucune valeur
n'est écrite à la main ici.

Usage, depuis la racine du dépôt :
  .venv/bin/python harness/queue/briefs/021-geo-fleuves-g5/deliverables/measure_g5_021.py
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
BRIEF = REPO / "harness" / "queue" / "briefs" / "021-geo-fleuves-g5"
PY = REPO / ".venv" / "bin" / "python"

NOT_COMPUTED = -1
# Référence de base pour les compteurs « fichier intact » : un changement
# *committé* sur la branche doit rougir (git status --porcelain ne le voit pas).
BASE_REF = "origin/master"

ROWS: list[tuple[str, object, str]] = []


def report(name: str, value: object, denominator: str) -> None:
    ROWS.append((name, value, denominator))


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def git(*args: str) -> str:
    """Exécute git ; en cas d'échec lève — jamais un stdout vide lu comme succès."""
    proc = subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True
    )
    if proc.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} a échoué (code {proc.returncode}): "
            f"{(proc.stderr or proc.stdout or '').strip()}"
        )
    return proc.stdout


def paths_changed_vs_base(*paths: str) -> list[str] | int:
    """Chemins parmi `paths` modifiés sur origin/master...HEAD.

    Retourne NOT_COMPUTED (-1) si git échoue — jamais une liste vide silencieuse.
    """
    try:
        out = git("diff", f"{BASE_REF}...HEAD", "--name-only", "--", *paths)
    except RuntimeError:
        return NOT_COMPUTED
    return [line.strip() for line in out.splitlines() if line.strip()]


def main() -> int:
    parser = argparse.ArgumentParser(description="compteurs mesurés du brief 021")
    parser.add_argument("--rerun-proof", action="store_true")
    parser.add_argument("--no-pytest", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    stats = load(ART / "stats_g5.json")
    rivers = load(ART / "rivers_g5.json")["segments"]
    mouths = load(ART / "mouths_g5.json")["mouths"]
    adj5 = load(ART / "adjacency_g5.json")["adjacency"]
    adj4 = load(ART / "adjacency_g4.json")["adjacency"]
    qa = load(LOGS / "v1_060_qa.json")

    segment_count = len(rivers)
    report("segment_count", segment_count, "troncons de artifacts/rivers_g5.json")

    nav = {"navigable": 0, "indeterminate": 0, "non_navigable": 0}
    for seg in rivers:
        rank = int(seg["scalerank"])
        if rank <= C.G5_NAV_SCALE_NAVIGABLE_MAX:
            nav["navigable"] += 1
        elif rank >= C.G5_NAV_SCALE_NON_NAV_MIN:
            nav["non_navigable"] += 1
        else:
            nav["indeterminate"] += 1
    report(
        "navigability_counts",
        nav,
        f"segment_count={segment_count} ; bornes lues "
        f"NAV_MAX={C.G5_NAV_SCALE_NAVIGABLE_MAX} NON_NAV_MIN={C.G5_NAV_SCALE_NON_NAV_MIN}",
    )
    report(
        "navigability_counts_somme",
        sum(nav.values()),
        f"{segment_count} = segment_count",
    )

    # Fleuves nommés — reconstruction indépendante sur name + alias.
    found = []
    for target in C.G5_NAMED_MAJOR_RIVERS:
        t = target.lower()
        hit = False
        for seg in rivers:
            fields = [seg.get("name") or ""] + list(seg.get("name_aliases") or [])
            if t in {str(f).strip().lower() for f in fields if f}:
                hit = True
                break
        if hit:
            found.append(target)
    report(
        "fleuves_nommes_trouves",
        len(found),
        f"{len(C.G5_NAMED_MAJOR_RIVERS)} = len(G5_NAMED_MAJOR_RIVERS) lu de constants.py",
    )

    land_land_total = sum(1 for e in adj4 if e.get("kind") == "land-land")
    with_river = [
        e
        for e in adj5
        if e.get("kind") == "land-land"
        and e.get("fluvial_class") in ("artery", "crossing", "both")
    ]
    artery = sum(1 for e in with_river if e.get("fluvial_class") == "artery")
    crossing = sum(1 for e in with_river if e.get("fluvial_class") == "crossing")
    both = sum(1 for e in with_river if e.get("fluvial_class") == "both")
    report(
        "aretes_terre_terre_avec_fleuve",
        len(with_river),
        f"{land_land_total} aretes land-land lues de adjacency_g4.json",
    )
    report("artery_count", artery, f"{len(with_river)} = aretes_terre_terre_avec_fleuve")
    report("crossing_count", crossing, f"{len(with_river)} = aretes_terre_terre_avec_fleuve")
    report("both_count", both, f"{len(with_river)} = aretes_terre_terre_avec_fleuve")
    report(
        "somme_classes_egale_aretes_avec_fleuve",
        int(artery + crossing + both == len(with_river)),
        "1 si artery+crossing+both == aretes_terre_terre_avec_fleuve",
    )
    report(
        "artery_count_positif",
        int(artery > 0),
        "1 si artery_count > 0",
    )

    # Échantillon D3 : une arête de chaque classe, confrontée à rivers_g5.
    seg_nav = {s["segment_id"]: s["navigability"] for s in rivers}
    samples = {}
    for cls in ("artery", "crossing", "both"):
        edge = next((e for e in with_river if e.get("fluvial_class") == cls), None)
        if edge is None:
            samples[cls] = "aucune"
            continue
        ids = [r["segment_id"] for r in (edge.get("artery_rivers") or [])]
        if cls == "crossing":
            ok = edge.get("fluvial_artery") in (False, None) and not edge.get(
                "artery_rivers"
            )
            samples[cls] = f"a={edge['a']} b={edge['b']} conforme_d3={int(ok)}"
        else:
            navs = {seg_nav.get(i) for i in ids}
            if cls == "artery":
                ok = navs == {"navigable"} and edge.get("fluvial_artery") is True
            else:
                ok = (
                    "navigable" in navs
                    and bool(navs - {"navigable"})
                    and edge.get("fluvial_artery") is True
                )
            samples[cls] = (
                f"a={edge['a']} b={edge['b']} navs={sorted(navs)} conforme_d3={int(ok)}"
            )
    report("echantillon_d3_par_classe", samples, "1 arete par classe confrontee a D3")

    report(
        "embouchures_mesurees",
        len(mouths),
        "mouths de artifacts/mouths_g5.json (0 accepte si G5-D vert)",
    )
    non_adj = sum(1 for m in mouths if not m.get("sea_zone_adjacent_to_river_cells"))
    report(
        "embouchures_zone_non_adjacente",
        non_adj,
        f"{len(mouths)} embouchures ; compteur calcule (jamais suppose)",
    )

    checks = qa.get("checks") or []
    verts = sum(1 for c in checks if c.get("passed"))
    reds = sum(1 for c in checks if str(c.get("red_proof") or "").strip())
    report("controles_g5_verts", verts, f"{len(checks)} entrees du tableau checks")
    report(
        "controles_g5_avec_preuve_rouge_non_vide",
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
            [str(PY), "tests/run_proof_g5.py"],
            cwd=GEO,
            capture_output=True,
            text=True,
        )
        report(
            "code_sortie_run_proof_g5",
            proc.returncode,
            "1 execution de tests/run_proof_g5.py",
        )
    else:
        report(
            "code_sortie_run_proof_g5",
            int(qa.get("exit_code", NOT_COMPUTED)),
            "exit_code enregistre dans logs/v1_060_qa.json (passer --rerun-proof pour rejouer)",
        )

    # Compteurs « intact » : diff vs base (voit les changements committés).
    shared = [
        "pipeline/geo/constants.py",
        "pipeline/geo/qa/checks.py",
        "pipeline/geo/pipeline.py",
        "pipeline/geo/io_util.py",
        "pipeline/geo/projection.py",
        "pipeline/geo/steps/02_coastline.py",
        "pipeline/geo/steps/02b_corrections_1400.py",
        "pipeline/geo/steps/03_cells.py",
        "pipeline/geo/steps/04_adjacency.py",
    ]
    changed_const = paths_changed_vs_base("pipeline/geo/constants.py")
    if changed_const == NOT_COMPUTED:
        report(
            "constantes_g5_inchangees",
            NOT_COMPUTED,
            f"git diff {BASE_REF}...HEAD --name-only a echoue",
        )
    else:
        report(
            "constantes_g5_inchangees",
            int(len(changed_const) == 0),
            f"1 si constants.py absent de git diff {BASE_REF}...HEAD --name-only",
        )

    changed_shared = paths_changed_vs_base(*shared)
    if changed_shared == NOT_COMPUTED:
        report(
            "fichiers_partages_modifies",
            NOT_COMPUTED,
            f"git diff {BASE_REF}...HEAD a echoue",
        )
    else:
        report(
            "fichiers_partages_modifies",
            len(changed_shared),
            f"{len(shared)} fichiers partages verifies par "
            f"git diff {BASE_REF}...HEAD --name-only",
        )

    changed_g4 = paths_changed_vs_base("pipeline/geo/artifacts/adjacency_g4.json")
    if changed_g4 == NOT_COMPUTED:
        report(
            "adjacency_g4_inchange",
            NOT_COMPUTED,
            f"git diff {BASE_REF}...HEAD a echoue",
        )
    else:
        report(
            "adjacency_g4_inchange",
            int(len(changed_g4) == 0),
            f"1 si adjacency_g4.json absent de git diff {BASE_REF}...HEAD --name-only",
        )

    # Preuve que ces compteurs peuvent rougir : un fichier *committé* modifié
    # sur la branche (05_rivers.py) doit apparaître dans le diff de base.
    known_changed = paths_changed_vs_base("pipeline/geo/steps/05_rivers.py")
    if known_changed == NOT_COMPUTED:
        report(
            "preuve_compteur_intact_peut_rougir",
            NOT_COMPUTED,
            "git diff a echoue",
        )
    else:
        report(
            "preuve_compteur_intact_peut_rougir",
            int(len(known_changed) > 0),
            "1 si 05_rivers.py (modifie sur la branche) apparait dans "
            f"git diff {BASE_REF}...HEAD — prouve que le compteur voit "
            "une modification commitee",
        )

    report(
        "adjacency_g5_differe_de_g4",
        int(sha256_of(ART / "adjacency_g5.json") != sha256_of(ART / "adjacency_g4.json")),
        "1 si empreintes calculees a l'execution different",
    )

    declared_proofs = [
        "pipeline/geo/steps/05_rivers.py",
        "pipeline/geo/tests/run_proof_g5.py",
        "pipeline/geo/tests/test_qa_red_g5.py",
        "pipeline/geo/artifacts/rivers_g5.json",
        "pipeline/geo/artifacts/adjacency_g5.json",
        "pipeline/geo/artifacts/mouths_g5.json",
        "pipeline/geo/artifacts/stats_g5.json",
        "pipeline/geo/artifacts/MANIFEST_g5.json",
        "pipeline/geo/registry/river_registry.json",
        "pipeline/geo/logs/v1_060_qa.json",
        "pipeline/geo/logs/v1_060_rivers.log",
        "pipeline/geo/capture/v1_060_rivers_window.png",
        "pipeline/geo/capture/v1_060_artery_crossing_both.png",
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

    if args.no_pytest:
        report("tests_harness_passed_021", NOT_COMPUTED, "non execute (--no-pytest)")
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
            "tests_harness_passed_021",
            passed,
            f"{collected} tests collectes dans harness/tests/"
            f" ({skipped} SKIP Linux/Unity declares, {failed} echecs)",
        )

    # Cohérence stats vs reconstruction
    report(
        "stats_segment_count",
        int(stats["segment_count"]),
        f"{segment_count} reconstruit depuis rivers_g5.json",
    )
    report(
        "stats_mouth_count",
        int(stats["mouth_count"]),
        f"{len(mouths)} reconstruit depuis mouths_g5.json",
    )
    report(
        "stats_embouchures_zone_non_adjacente",
        int(stats.get("embouchures_zone_non_adjacente", NOT_COMPUTED)),
        f"{non_adj} reconstruit depuis mouths_g5.json",
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
    print("compteurs mesures — brief 021 (G5 fleuves)")
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
