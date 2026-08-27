#!/usr/bin/env python3
"""Mesureur rejouable du lot 037."""

from __future__ import annotations

import ast
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

DELIVERABLES = Path(__file__).resolve().parent
REPO_ROOT = DELIVERABLES.parents[4]
SIM_DIR = REPO_ROOT / "sim"
ARCHIVES = DELIVERABLES / "archives"
FOOD_STOCK_FILES = [
    "sim/tests/test_commerce.py",
    "sim/tests/test_survie.py",
    "sim/tests/test_determinisme.py",
    "sim/tests/test_write_coverage.py",
]


def _run(cmd: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, cwd=REPO_ROOT, check=True, text=True, capture_output=True)


def sha_de_base() -> str:
    return _run(["git", "rev-parse", "HEAD"]).stdout.strip()


def modules_sim() -> list[Path]:
    fichiers = [p for p in sorted(SIM_DIR.rglob("*.py")) if "tests" not in p.relative_to(SIM_DIR).parts]
    if not fichiers:
        raise RuntimeError("parcours sim/ vide")
    return fichiers


def compter_food_stock_kg(sources: list[tuple[str, str]]) -> int:
    total = 0
    for _nom, source in sources:
        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Attribute) and node.attr == "food_stock_kg":
                total += 1
    return total


def sources_au_sha(sha: str) -> list[tuple[str, str]]:
    out = []
    for path in modules_sim():
        rel = path.relative_to(REPO_ROOT)
        out.append((str(rel), _run(["git", "show", f"{sha}:{rel}"]).stdout))
    return out


def sources_courantes() -> list[tuple[str, str]]:
    return [(str(p.relative_to(REPO_ROOT)), p.read_text(encoding="utf-8")) for p in modules_sim()]


def acces_directs_stocks_hors_modele() -> int:
    total = 0
    for path in modules_sim():
        if path.name == "model.py":
            continue
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.Attribute) and node.attr == "stocks":
                total += 1
            elif isinstance(node, ast.Subscript) and isinstance(node.value, ast.Attribute):
                if node.value.attr == "stocks":
                    total += 1
    return total


def python() -> Path:
    return REPO_ROOT / ".venv" / "bin" / "python"


def comparer_cli() -> int:
    a20 = (ARCHIVES / "cli_ticks20_seed0.json").read_text(encoding="utf-8")
    a365 = (ARCHIVES / "cli_ticks365_seed0.json").read_text(encoding="utf-8")
    p20 = _run([str(python()), "-m", "sim", "--ticks", "20", "--seed", "0", "--json"]).stdout
    p365 = _run([str(python()), "-m", "sim", "--ticks", "365", "--seed", "0", "--json"]).stdout
    if a20 != p20 or a365 != p365:
        raise AssertionError("CLI non identique")
    return len(json.loads(p20))


def comparer_snapshot() -> int:
    archive = (ARCHIVES / "snapshot_ticks0_seed0.json").read_bytes()
    with tempfile.NamedTemporaryFile(suffix=".json") as tmp:
        _run([str(python()), "-m", "sim", "--ticks", "0", "--seed", "0", "--snapshot-json", tmp.name])
        apres = Path(tmp.name).read_bytes()
    if archive != apres:
        raise AssertionError("snapshot non identique")
    return len(json.loads(archive.decode("utf-8")))


def lignes_supprimees(sha: str) -> int:
    diff = _run(["git", "diff", "--ignore-blank-lines", "-U0", sha, "--", "sim/tests/test_monde.py"]).stdout
    return sum(1 for l in diff.splitlines() if l.startswith("-") and not l.startswith("---"))


def fichiers_test_inchanges(sha: str) -> int:
    return sum(1 for rel in FOOD_STOCK_FILES if _run(["git", "diff", sha, "--", rel]).stdout == "")


def pytest_collect() -> int:
    proc = _run([str(python()), "-m", "pytest", "sim/tests/", "--collect-only", "-q"])
    m = re.search(r"(\d+) tests? collected", proc.stdout)
    if not m:
        raise RuntimeError(proc.stdout)
    return int(m.group(1))


def tests_collectes_avant(sha: str, apres: int) -> int:
    source = _run(["git", "show", f"{sha}:sim/tests/test_monde.py"]).stdout
    avant = source.count("def test_")
    courant = Path(REPO_ROOT / "sim/tests/test_monde.py").read_text(encoding="utf-8").count("def test_")
    nouveau = courant - avant
    return apres - nouveau


def rejouer_sc5() -> None:
    sys.path.insert(0, str(REPO_ROOT))
    from sim.constants import MARCHANDISE_NOURRITURE, MARCHANDISE_SONDE_037
    from sim.model import Cell, ecrire_stock_marchandise, lire_stock_marchandise
    from sim.world import World

    cell = Cell(cell_id=99, area_km2=1.0, population=10, food_stock_kg=100.0)
    assert lire_stock_marchandise(cell, MARCHANDISE_NOURRITURE) == 100.0
    ecrire_stock_marchandise(cell, MARCHANDISE_SONDE_037, 42.0)
    assert lire_stock_marchandise(cell, MARCHANDISE_SONDE_037) == 42.0
    assert lire_stock_marchandise(cell, MARCHANDISE_NOURRITURE) == 100.0
    assert World(cells={99: cell}, adjacency=[]).to_dict()["cells"]["99"]["stocks"][MARCHANDISE_SONDE_037] == 42.0


def cellules_to_dict_avec_panier() -> int:
    sys.path.insert(0, str(REPO_ROOT))
    from sim.world import World

    monde = World.charger(0)
    avec = sum(1 for e in monde.to_dict()["cells"].values() if "stocks" in e)
    chargees = len(monde.cells)
    if avec != chargees:
        raise AssertionError(f"{avec} != {chargees}")
    return avec


def main() -> int:
    sha = sha_de_base()
    compteurs = {
        "sha_de_base": sha,
        "modules_sim_parcourus": len(modules_sim()),
        "references_au_champ_supprime_avant": compter_food_stock_kg(sources_au_sha(sha)),
        "references_au_champ_supprime_apres": compter_food_stock_kg(sources_courantes()),
        "acces_directs_au_panier_hors_modele": acces_directs_stocks_hors_modele(),
        "champs_cli_identiques": comparer_cli(),
        "cles_snapshot_identiques": comparer_snapshot(),
        "cellules_to_dict_avec_panier": cellules_to_dict_avec_panier(),
        "lignes_supprimees_test_monde": lignes_supprimees(sha),
        "fichiers_test_inchanges": fichiers_test_inchanges(sha),
        "tests_collectes_apres": pytest_collect(),
    }
    compteurs["tests_collectes_avant"] = tests_collectes_avant(sha, compteurs["tests_collectes_apres"])
    rejouer_sc5()
    (DELIVERABLES / "measure_output.txt").write_text(
        "\n".join(f"{k}={v}" for k, v in compteurs.items()) + "\n", encoding="utf-8"
    )
    print(json.dumps(compteurs, indent=2, ensure_ascii=False))
    if compteurs["references_au_champ_supprime_avant"] <= 0:
        return 1
    if compteurs["references_au_champ_supprime_apres"]:
        return 1
    if compteurs["acces_directs_au_panier_hors_modele"]:
        return 1
    if compteurs["lignes_supprimees_test_monde"]:
        return 1
    if compteurs["tests_collectes_apres"] <= compteurs["tests_collectes_avant"]:
        return 1
    if compteurs["fichiers_test_inchanges"] != len(FOOD_STOCK_FILES):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
