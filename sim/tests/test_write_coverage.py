"""
Couverture d'écriture — brief 012, SC7 R2+R3 + extension.

Vérifie par analyse statique (AST) que :
1. Chaque champ déclaré dans TOUTES les dataclasses de sim.model
   (découvertes par introspection, pas nommées en dur — R2) possède
   au moins un site d'écriture ET au moins un site de lecture.
   La détection de site d'écriture vérifie que la variable cible a un
   nom conventionnel associé à la classe scrutée (ex. 'cell' pour Cell)
   — R3 : une affectation sur un objet d'un type différent ne compte pas.

2. Si une deuxième dataclass est ajoutée sans écrivain, le test échoue.
   (Contre-preuve R2 : introspection attrape les nouvelles dataclasses.)

3. World.adjacency est lu dans au moins un module du moteur.
   (Extension SC7 brief 012 — contre-preuve : si le maillon commerce
   est retiré, la vérification de lecture échoue.)

Deux tests unitaires séparés :
- test_all_dataclass_fields_have_write_and_read_sites (R2 + R3)
- test_adjacency_is_read_by_engine (extension World.adjacency)
- test_write_coverage_counter_etendu (compteur champs_modele_couverts_etendu)
"""

import ast
import dataclasses
import inspect
import pathlib

import sim.model as _sim_model

_SIM_DIR = pathlib.Path(__file__).parent.parent
_ENGINE_FILE = _SIM_DIR / "engine.py"
_WORLD_FILE = _SIM_DIR / "world.py"
_MODEL_FILE = _SIM_DIR / "model.py"

_SIM_SOURCE_FILES = [_ENGINE_FILE, _WORLD_FILE, _MODEL_FILE]


def _discover_dataclasses():
    """
    Découvre par introspection toutes les dataclasses de sim.model.
    R2 : jamais nommées en dur ici.
    Retourne une liste de (nom_classe → nom_conventionnel_variable).
    Convention : variable = nom_classe.lower() (ex. Cell → 'cell').
    """
    result = []
    for _name, obj in inspect.getmembers(_sim_model, inspect.isclass):
        if dataclasses.is_dataclass(obj):
            result.append((obj, obj.__name__.lower()))
    return result


def _scan_writes_typed(files: list, field_names: set, target_var_names: set) -> set:
    """
    Scanne les fichiers Python listés et retourne l'ensemble des noms
    de champs ayant un site d'écriture sur une variable dont le nom
    appartient à target_var_names (R3 : filtre par nom conventionnel).

    Détecte :
    - Affectation d'attribut : VAR.FIELD = expr  (VAR dans target_var_names)
    - Argument nommé du constructeur : ClassName(FIELD=expr)
      → tous les constructeurs des dataclasses de sim.model sont considérés.
    """
    all_writes: set = set()

    for filepath in files:
        source = filepath.read_text(encoding="utf-8")
        tree = ast.parse(source, filename=str(filepath))

        for node in ast.walk(tree):
            # Site d'écriture 1 : VAR.FIELD = expr, VAR dans target_var_names
            if isinstance(node, (ast.Assign, ast.AugAssign)):
                targets = (
                    node.targets if isinstance(node, ast.Assign) else [node.target]
                )
                for t in targets:
                    if (
                        isinstance(t, ast.Attribute)
                        and t.attr in field_names
                        and isinstance(t.value, ast.Name)
                        and t.value.id in target_var_names
                    ):
                        all_writes.add(t.attr)

            # Site d'écriture 2 : constructeur nommé (ClassName(FIELD=expr))
            if isinstance(node, ast.Call):
                func = node.func
                is_dataclass_ctor = (
                    isinstance(func, ast.Name) and func.id in {
                        cls.__name__
                        for cls, _ in _discover_dataclasses()
                    }
                ) or (
                    isinstance(func, ast.Attribute) and func.attr in {
                        cls.__name__
                        for cls, _ in _discover_dataclasses()
                    }
                )
                if is_dataclass_ctor:
                    for kw in node.keywords:
                        if kw.arg and kw.arg in field_names:
                            all_writes.add(kw.arg)

    return all_writes


def _scan_reads(files: list, field_names: set) -> set:
    """Retourne l'ensemble des noms de champs lus (contexte Load)."""
    all_reads: set = set()
    for filepath in files:
        source = filepath.read_text(encoding="utf-8")
        tree = ast.parse(source, filename=str(filepath))
        for node in ast.walk(tree):
            if (
                isinstance(node, ast.Attribute)
                and node.attr in field_names
                and not isinstance(node.ctx, ast.Store)
            ):
                all_reads.add(node.attr)
    return all_reads


def test_all_dataclass_fields_have_write_and_read_sites():
    """
    R2 + R3 : pour chaque dataclass de sim.model (par introspection),
    chaque champ déclaré a au moins un site d'écriture (sur la variable
    conventionnelle, ex. 'cell' pour Cell — R3) ET un site de lecture.

    Ce test va ROUGE si :
    - Un champ est ajouté à une dataclass sans écrivain
    - Une nouvelle dataclass est ajoutée sans aucun site d'écriture
    - Une affectation sur un autre objet est la seule correspondance
      (R3 : filtrage par nom de variable conventionnel)
    """
    discovered = _discover_dataclasses()
    assert discovered, "Aucune dataclass trouvée dans sim.model — bug d'introspection"

    errors = []
    for cls, var_name in discovered:
        field_names = {f.name for f in dataclasses.fields(cls)}
        all_writes = _scan_writes_typed(_SIM_SOURCE_FILES, field_names, {var_name})
        all_reads = _scan_reads(_SIM_SOURCE_FILES, field_names)

        for f in dataclasses.fields(cls):
            if f.name not in all_writes:
                errors.append(
                    f"{cls.__name__}.{f.name} : aucun site d'écriture "
                    f"(variable cible '{var_name}' attendue)"
                )
            if f.name not in all_reads:
                errors.append(f"{cls.__name__}.{f.name} : aucun site de lecture")

        print(f"Classe {cls.__name__} (var='{var_name}'):")
        print(f"  champs : {sorted(field_names)}")
        print(f"  écrits : {sorted(all_writes & field_names)}")
        print(f"  lus    : {sorted(all_reads & field_names)}")

    assert not errors, (
        "Couverture d'écriture incomplète :\n"
        + "\n".join(f"  - {e}" for e in errors)
    )


def test_adjacency_is_read_by_engine():
    """
    Extension SC7 brief 012 : World.adjacency est lu dans au moins un
    module du moteur (engine.py ou world.py).

    Ce test va ROUGE si le maillon commerce (_apply_commerce) est retiré
    du moteur et qu'aucun autre code ne lit 'adjacency'.
    """
    found_read = False
    for filepath in _SIM_SOURCE_FILES:
        source = filepath.read_text(encoding="utf-8")
        tree = ast.parse(source, filename=str(filepath))
        for node in ast.walk(tree):
            if (
                isinstance(node, ast.Attribute)
                and node.attr == "adjacency"
                and not isinstance(node.ctx, ast.Store)
            ):
                found_read = True
                break
        if found_read:
            break

    print(f"adjacency_lu_dans_moteur = {found_read}")
    assert found_read, (
        "World.adjacency n'est lu dans aucun module du moteur "
        "(engine.py, world.py, model.py). Le maillon commerce a-t-il été retiré ?"
    )


def test_write_coverage_counter_etendu():
    """
    Compteur champs_modele_couverts_etendu (brief 012, SC7).

    = nombre de champs couverts dans toutes les dataclasses de sim.model
      (écriture ET lecture vérifiées) + 1 si World.adjacency est lu.

    Dérivé par parcours — jamais écrit en dur.
    """
    discovered = _discover_dataclasses()

    total_fields = 0
    covered_fields = 0

    for cls, var_name in discovered:
        field_names = {f.name for f in dataclasses.fields(cls)}
        all_writes = _scan_writes_typed(_SIM_SOURCE_FILES, field_names, {var_name})
        all_reads = _scan_reads(_SIM_SOURCE_FILES, field_names)

        for f in dataclasses.fields(cls):
            total_fields += 1
            if f.name in all_writes and f.name in all_reads:
                covered_fields += 1

    # +1 pour World.adjacency
    adjacency_covered = 0
    for filepath in _SIM_SOURCE_FILES:
        source = filepath.read_text(encoding="utf-8")
        tree = ast.parse(source, filename=str(filepath))
        for node in ast.walk(tree):
            if (
                isinstance(node, ast.Attribute)
                and node.attr == "adjacency"
                and not isinstance(node.ctx, ast.Store)
            ):
                adjacency_covered = 1
                break
        if adjacency_covered:
            break

    total_denominator = total_fields + 1  # +1 pour adjacency
    champs_modele_couverts_etendu = covered_fields + adjacency_covered

    print(f"dataclasses découvertes : {[cls.__name__ for cls, _ in discovered]}")
    print(f"champs dans dataclasses : {total_fields}")
    print(f"champs couverts (écriture + lecture) : {covered_fields}")
    print(f"adjacency couverte : {adjacency_covered}")
    print(f"champs_modele_couverts_etendu = {champs_modele_couverts_etendu} / {total_denominator}")

    assert champs_modele_couverts_etendu == total_denominator, (
        f"Couverture étendue incomplète : {champs_modele_couverts_etendu}/{total_denominator}"
    )
