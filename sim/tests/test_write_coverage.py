"""
SC8 — Couverture d'écriture sur tous les champs du modèle.

Vérifie par analyse statique (AST) que chaque champ déclaré dans les
dataclasses de sim/ possède au moins un site d'écriture ET au moins
un site de lecture, en scannant sim/engine.py, sim/world.py et
sim/model.py.

Deux assertions complémentaires (deux familles de preuve rouge) :

1. test_all_declared_fields_have_write_and_read_sites
   Itère sur dataclasses.fields(Cell) et exige, pour chaque champ,
   un site d'écriture et un site de lecture dans le périmètre analysé.
   → ROUGE si un champ est déclaré sans écrivain ou sans lecteur
     (mode d'échec n°2 de simulation-principles.md).
   → Preuve rouge correspondante : run_phantom_red.txt (champ fantôme).

2. test_engine_writes_only_declared_fields
   Vérifie que tout attribut écrit sur 'cell' dans sim/engine.py est
   bien déclaré dans Cell.__dataclass_fields__.
   → ROUGE si un champ est retiré de Cell mais encore écrit dans engine.py.
   → Preuve rouge correspondante : run_sabotage.txt (SC10).

Le compteur champs_modele_couverts doit égaler le total déclaré.
"""

import ast
import dataclasses
import pathlib

import pytest

from sim.model import Cell

_SIM_DIR = pathlib.Path(__file__).parent.parent
_ENGINE_FILE = _SIM_DIR / "engine.py"
_WORLD_FILE = _SIM_DIR / "world.py"
_MODEL_FILE = _SIM_DIR / "model.py"

_SIM_SOURCE_FILES = [_ENGINE_FILE, _WORLD_FILE, _MODEL_FILE]


def _scan_writes_and_reads(files: list, field_names: set) -> tuple:
    """
    Scanne les fichiers Python listés et retourne :
        all_writes : set des noms de champs ayant un site d'écriture
                     (affectation d'attribut VAR.FIELD = ... OU
                      argument nommé du constructeur Cell(FIELD=...))
        all_reads  : set des noms de champs ayant un site de lecture
                     (accès à un attribut VAR.FIELD en contexte Load)
    """
    all_writes: set = set()
    all_reads: set = set()

    for filepath in files:
        source = filepath.read_text(encoding="utf-8")
        tree = ast.parse(source, filename=str(filepath))

        for node in ast.walk(tree):
            # Site d'écriture 1 : affectation d'attribut  (VAR.FIELD = expr)
            if isinstance(node, (ast.Assign, ast.AugAssign)):
                targets = (
                    node.targets if isinstance(node, ast.Assign) else [node.target]
                )
                for t in targets:
                    if isinstance(t, ast.Attribute) and t.attr in field_names:
                        all_writes.add(t.attr)

            # Site d'écriture 2 : argument nommé du constructeur Cell(FIELD=expr)
            if isinstance(node, ast.Call):
                func = node.func
                is_cell_ctor = (
                    isinstance(func, ast.Name) and func.id == "Cell"
                ) or (
                    isinstance(func, ast.Attribute) and func.attr == "Cell"
                )
                if is_cell_ctor:
                    for kw in node.keywords:
                        if kw.arg and kw.arg in field_names:
                            all_writes.add(kw.arg)

            # Site de lecture : accès à un attribut en contexte Load
            if (
                isinstance(node, ast.Attribute)
                and node.attr in field_names
                and not isinstance(node.ctx, ast.Store)
            ):
                all_reads.add(node.attr)

    return all_writes, all_reads


def test_all_declared_fields_have_write_and_read_sites():
    """
    SC8 — Itère sur dataclasses.fields(Cell) et exige pour chaque champ
    déclaré au moins un site d'écriture ET au moins un site de lecture.

    Ce test va ROUGE si un champ est déclaré sans écrivain ou sans lecteur
    (mode d'échec n°2, simulation-principles.md).
    Preuve rouge : run_phantom_red.txt (champ fantôme ajouté à Cell).
    """
    field_names = {f.name for f in dataclasses.fields(Cell)}
    all_writes, all_reads = _scan_writes_and_reads(_SIM_SOURCE_FILES, field_names)

    errors = []
    for f in dataclasses.fields(Cell):
        if f.name not in all_writes:
            errors.append(f"'{f.name}' : aucun site d'écriture trouvé")
        if f.name not in all_reads:
            errors.append(f"'{f.name}' : aucun site de lecture trouvé")

    print(f"champs déclarés : {sorted(field_names)}")
    print(f"sites d'écriture détectés : {sorted(all_writes & field_names)}")
    print(f"sites de lecture détectés : {sorted(all_reads & field_names)}")

    assert not errors, (
        "SC8 : champs déclarés sans couverture complète :\n"
        + "\n".join(f"  - {e}" for e in errors)
    )


def test_engine_writes_only_declared_fields():
    """
    SC8 / SC10 — Tout attribut écrit sur 'cell' dans engine.py doit être
    déclaré dans Cell.__dataclass_fields__.

    Ce test va ROUGE si hunger_ticks est retiré de Cell (sabotage SC10).
    Preuve rouge : run_sabotage.txt.
    """
    declared_fields = {f.name for f in dataclasses.fields(Cell)}

    source = _ENGINE_FILE.read_text(encoding="utf-8")
    tree = ast.parse(source)

    cell_writes: set = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            for child in ast.walk(node):
                if isinstance(child, (ast.Assign, ast.AugAssign)):
                    targets = (
                        child.targets if isinstance(child, ast.Assign)
                        else [child.target]
                    )
                    for t in targets:
                        if (
                            isinstance(t, ast.Attribute)
                            and isinstance(t.value, ast.Name)
                            and t.value.id == "cell"
                        ):
                            cell_writes.add(t.attr)

    undeclared = cell_writes - declared_fields
    assert not undeclared, (
        f"ADR-0003 / SC8 : attributs écrits sur 'cell' dans engine.py "
        f"mais non déclarés dans Cell.__dataclass_fields__ : {undeclared}. "
        f"Champs déclarés : {sorted(declared_fields)}."
    )


def test_write_coverage_counter():
    """
    SC8 — Le compteur champs_modele_couverts doit être égal au nombre total
    de champs déclarés dans Cell. Dérivé du parcours — jamais écrit en dur.
    """
    field_names = {f.name for f in dataclasses.fields(Cell)}
    total_declared = len(field_names)

    all_writes, all_reads = _scan_writes_and_reads(_SIM_SOURCE_FILES, field_names)

    fully_covered = sorted(
        f.name for f in dataclasses.fields(Cell)
        if f.name in all_writes and f.name in all_reads
    )
    champs_modele_couverts = len(fully_covered)

    print(f"champs déclarés dans Cell : {sorted(field_names)}")
    print(f"sites d'écriture : {sorted(all_writes & field_names)}")
    print(f"sites de lecture : {sorted(all_reads & field_names)}")
    print(f"champs_modele_couverts = {champs_modele_couverts} / {total_declared}")
    print(f"champs couverts : {fully_covered}")

    assert champs_modele_couverts == total_declared, (
        f"SC8 : couverture incomplète — {champs_modele_couverts}/{total_declared} champs "
        f"ont un écrivain ET un lecteur. "
        f"Non couverts : {sorted(set(field_names) - set(fully_covered))}"
    )
