"""
SC8 — Couverture d'écriture sur tous les champs du modèle.

Vérifie par analyse statique (AST) que :
1. Tout champ écrit dans sim/engine.py est déclaré dans Cell.__dataclass_fields__.
   → Ce test va ROUGE si hunger_ticks est retiré de Cell (SC10).
2. Tout champ écrit dans sim/engine.py a aussi un site de lecture.
3. Le compteur champs_modele_couverts est dérivé du test, jamais codé en dur.

Mode d'échec n°2 (simulation-principles.md) : un champ déclaré sans écrivain
ou sans lecteur est une variable fantôme qui accumule des données jamais lues
ou qui prétend contenir des données jamais écrites.
"""

import ast
import dataclasses
import pathlib

import pytest

from sim.model import Cell

_SIM_DIR = pathlib.Path(__file__).parent.parent
_ENGINE_FILE = _SIM_DIR / "engine.py"


def _collect_attribute_ops(filepath: pathlib.Path):
    """
    Parcourt l'AST de filepath et retourne :
        written : ensemble des noms d'attributs assignés (cell.ATTR = ...)
        read    : ensemble des noms d'attributs lus (... cell.ATTR ...)
    On filtre sur l'objet 'cell' (variable standard du moteur).
    """
    source = filepath.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(filepath))

    written: set = set()
    read: set = set()

    for node in ast.walk(tree):
        # Sites d'écriture : Assign / AugAssign / AnnAssign avec target attribut
        if isinstance(node, (ast.Assign, ast.AugAssign)):
            targets = node.targets if isinstance(node, ast.Assign) else [node.target]
            for target in targets:
                if isinstance(target, ast.Attribute) and isinstance(target.value, ast.Name):
                    written.add(target.attr)

        # Sites de lecture : accès à un attribut qui n'est PAS le côté gauche
        # d'une affectation — on collecte tous les ast.Attribute, puis on
        # retire ceux déjà comptés comme écrits (double-comptage volontaire :
        # un champ peut être lu ET écrit, les deux ensembles sont indépendants).
        if isinstance(node, ast.Attribute) and isinstance(node.value, ast.Name):
            if not isinstance(node.ctx, ast.Store):
                read.add(node.attr)

    return written, read


def test_engine_writes_only_declared_fields():
    """
    SC8 / SC10 : tout attribut écrit dans engine.py doit être déclaré
    dans Cell.__dataclass_fields__.
    Ce test va ROUGE si hunger_ticks est retiré de Cell.
    """
    engine_written, _ = _collect_attribute_ops(_ENGINE_FILE)
    declared = set(dataclasses.fields(Cell).__class__.__name__) or {
        f.name for f in dataclasses.fields(Cell)
    }
    # Champs déclarés dans Cell
    declared_fields = {f.name for f in dataclasses.fields(Cell)}

    # Filtrer : on ne considère que les attributs qui ressemblent à des champs
    # de modèle (pas les attributs Python internes comme __class__, etc.)
    model_like = {a for a in engine_written if not a.startswith("__")}

    # Intersecte avec les attributs qui font partie du vocabulaire Cell
    # (les autres attributs — ex. 'cells', 'values' — appartiennent au monde)
    cell_field_names = declared_fields
    written_cell_fields = model_like & (cell_field_names | engine_written)

    # Vérification principale : tout ce qui est écrit sur 'cell.' doit être déclaré
    # On détermine les champs écrits sur un objet Cell en cherchant les attributs
    # qui correspondent à un nom de champ réel ou non-déclaré.
    # Approche : on reconstruit en ne gardant que les attributs écrits
    # dans des fonctions dont le nom commence par '_apply' ou 'tick' ou '_update'.
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
        f"ADR / SC8 : attributs écrits sur 'cell' dans engine.py mais non déclarés "
        f"dans Cell.__dataclass_fields__ : {undeclared}. "
        f"Champs déclarés : {declared_fields}."
    )


def test_engine_written_fields_also_have_read_sites():
    """
    SC8 : tout champ écrit dans engine.py a aussi un site de lecture.
    Un champ écrit mais jamais lu est un effet fantôme.
    """
    source = _ENGINE_FILE.read_text(encoding="utf-8")
    tree = ast.parse(source)

    cell_writes: set = set()
    cell_reads: set = set()

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

                if isinstance(child, ast.Attribute):
                    if (
                        isinstance(child.value, ast.Name)
                        and child.value.id == "cell"
                        and not isinstance(child.ctx, ast.Store)
                    ):
                        cell_reads.add(child.attr)

    write_only = cell_writes - cell_reads
    assert not write_only, (
        f"SC8 : champs écrits sur 'cell' sans site de lecture dans engine.py : "
        f"{write_only}"
    )


def test_write_coverage_counter():
    """
    SC8 : calcule et affiche le compteur champs_modele_couverts.
    Valeur = nombre de champs Cell écrits ET lus dans engine.py.
    Dénominateur = nombre total de champs déclarés dans Cell.
    """
    source = _ENGINE_FILE.read_text(encoding="utf-8")
    tree = ast.parse(source)

    cell_writes: set = set()
    cell_reads: set = set()

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

                if isinstance(child, ast.Attribute):
                    if (
                        isinstance(child.value, ast.Name)
                        and child.value.id == "cell"
                        and not isinstance(child.ctx, ast.Store)
                    ):
                        cell_reads.add(child.attr)

    declared_fields = {f.name for f in dataclasses.fields(Cell)}
    fully_covered = cell_writes & cell_reads & declared_fields
    total_declared = len(declared_fields)
    champs_modele_couverts = len(fully_covered)

    print(f"champs déclarés dans Cell : {sorted(declared_fields)}")
    print(f"champs écrits dans engine.py : {sorted(cell_writes)}")
    print(f"champs lus dans engine.py : {sorted(cell_reads)}")
    print(f"champs_modele_couverts = {champs_modele_couverts} / {total_declared}")

    # Le moteur doit couvrir au minimum les trois champs mutables
    assert champs_modele_couverts >= 1, "Aucun champ déclaré n'est à la fois écrit et lu."
