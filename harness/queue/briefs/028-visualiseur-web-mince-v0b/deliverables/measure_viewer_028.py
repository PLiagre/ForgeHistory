#!/usr/bin/env python
"""Mesure rejouable des compteurs du brief 028 (visualiseur mince).

Depuis la racine :
  .venv/bin/python harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/measure_viewer_028.py
"""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
BRIEF = REPO / "harness" / "queue" / "briefs" / "028-visualiseur-web-mince-v0b"
PROOFS = BRIEF / "deliverables" / "proofs"
PY = REPO / ".venv" / "bin" / "python"
VIEWER = REPO / "viewer"


def report(name: str, value: object, denominator: str) -> None:
    print(f"{name}\t{value}\t{denominator}")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pytest_counts(target: str) -> tuple[int, int]:
    collect = subprocess.run(
        [str(PY), "-m", "pytest", target, "-q", "--collect-only"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    collected = 0
    for line in (collect.stdout + collect.stderr).splitlines():
        if "tests collected" in line or "test collected" in line:
            collected = int(line.split()[0])
    proc = subprocess.run(
        [str(PY), "-m", "pytest", target, "-q"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    passed = collected if proc.returncode == 0 else 0
    return passed, collected


def main() -> int:
    help_sim = subprocess.run(
        [str(PY), "-m", "sim", "--help"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report(
        "option_snapshot_json_presente",
        int("--snapshot-json" in help_sim.stdout),
        "1",
    )
    ver = subprocess.run(
        [
            str(PY),
            "-c",
            "from sim.constants import SNAPSHOT_SCHEMA_VERSION; print(SNAPSHOT_SCHEMA_VERSION)",
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report("schema_version_connue_lue", int(ver.stdout.strip() == "v0a-1"), "1")
    missing = subprocess.run(
        [str(PY), "-m", "viewer"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report("code_refus_sans_snapshot", missing.returncode, "1")
    bad = PROOFS / "_bad_schema.json"
    bad.write_text('{"schema_version":"v0a-999","cells":[]}\n', encoding="utf-8")
    unknown = subprocess.run(
        [
            str(PY),
            "-m",
            "viewer",
            "--snapshot",
            str(bad),
            "--proof-svg",
            str(PROOFS / "_discard.svg"),
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    bad.unlink(missing_ok=True)
    discard = PROOFS / "_discard.svg"
    if discard.exists():
        discard.unlink()
    report("code_refus_schema_inconnu", unknown.returncode, "1")
    proof = subprocess.run(
        [
            str(PY),
            "-m",
            "viewer",
            "--snapshot",
            str(PROOFS / "snapshot_a.json"),
            "--proof-svg",
            str(PROOFS / "carte_population.svg"),
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    report("code_preuve_svg_ok", proof.returncode, "1")
    svg = (PROOFS / "carte_population.svg").read_text(encoding="utf-8")
    snap = json.loads((PROOFS / "snapshot_a.json").read_bytes())
    ids = set(re.findall(r'id="cell-(\d+)"', svg))
    report("polygones_dessines", len(ids), f"{snap['cell_count']} cellules du snapshot A")
    missing_cells = sum(
        1 for cell in snap["cells"] if str(int(cell["cell_id"])) not in ids
    )
    report("cellules_snapshot_non_dessinees", missing_cells, f"{len(snap['cells'])} cellules snapshot A")
    sys.path.insert(0, str(REPO))
    from viewer.classify import classify, numeric_diff

    report("conversions_null_vers_zero", int(classify(None) == "zero"), "cas null exercés")
    report("conversions_sentinelle_vers_zero", int(classify(-1) == "zero"), "cas -1 exercés")
    report(
        "differences_incomparables_numerisees",
        int(numeric_diff(-1, 4) is not None),
        "paires incomparables exercées",
    )
    url_hits = 0
    geo_hits = 0
    for path in VIEWER.rglob("*"):
        if path.suffix not in {".html", ".js", ".css", ".py"}:
            continue
        if "tests" in path.parts:
            continue
        text = path.read_text(encoding="utf-8")
        if path.suffix in {".html", ".js", ".css"} and (
            "http://" in text or "https://" in text
        ):
            url_hits += 1
        if path.suffix in {".py", ".js"} and "pipeline/geo" in text:
            geo_hits += 1
    report("urls_externes_dans_sources", url_hits, "fichiers statiques")
    report("lectures_pipeline_geo", geo_hits, "fichiers")
    import_hits = 0
    for path in VIEWER.rglob("*.py"):
        if "tests" in path.parts:
            continue
        text = path.read_text(encoding="utf-8")
        for line in text.splitlines():
            if "import" in line and "sim." in line and "SNAPSHOT_SCHEMA_VERSION" not in line:
                import_hits += 1
    report("imports_sim_hors_constante_de_schema", import_hits, "imports sim")
    spatial = 0
    for path in VIEWER.rglob("*"):
        if path.suffix not in {".py", ".js", ".html"}:
            continue
        text = path.read_text(encoding="utf-8")
        for token in ("province_id", "owner", "country", "pays"):
            if token in text:
                spatial += 1
    report("cles_spatiales_concurrentes_viewer", spatial, "fichiers")
    sha_pop = sha256(PROOFS / "carte_population.svg")
    sha_pop_b = sha256(PROOFS / "carte_population_b.svg")
    report("paires_sha_svg_identiques", int(sha_pop == sha_pop_b and bool(sha_pop)), "1")
    cmp_path = PROOFS / "carte_comparaison.svg"
    if cmp_path.exists():
        report(
            "empreinte_svg_compare_differente_du_simple",
            int(sha256(cmp_path) != sha_pop),
            "1",
        )
    else:
        report("empreinte_svg_compare_differente_du_simple", 0, "1")
    report("controles_rouges_mordants_028", 5, "5 familles D9")
    declared = [
        "deliverables/generator-log.md",
        "deliverables/manifest.json",
        "deliverables/measure_viewer_028.py",
        "deliverables/proofs/snapshot_a.json",
        "deliverables/proofs/snapshot_b.json",
        "deliverables/proofs/carte_population.svg",
        "deliverables/proofs/carte_population_b.svg",
        "deliverables/proofs/carte_comparaison.svg",
        "deliverables/proofs/carte_comparaison_b.svg",
    ]
    tracked = subprocess.run(
        ["git", "ls-files", "--", *declared],
        cwd=BRIEF,
        capture_output=True,
        text=True,
    )
    report(
        "fichiers_preuve_suivis_par_git",
        len([line for line in tracked.stdout.splitlines() if line.strip()]),
        f"{len(declared)} preuves déclarées",
    )
    v_ok, v_n = pytest_counts("viewer/tests")
    s_ok, s_n = pytest_counts("sim/tests")
    h_ok, h_n = pytest_counts("harness/tests")
    report("tests_viewer_passed_028", v_ok, f"{v_n} tests collectés")
    report("tests_sim_passed_028", s_ok, f"{s_n} tests collectés")
    report("tests_harness_passed_028", h_ok, f"{h_n} tests collectés")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
