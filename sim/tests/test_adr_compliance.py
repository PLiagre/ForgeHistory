"""
SC3 — Respect de l'ADR-0003 : cell_id comme seule clé spatiale.

Deux vérifications complémentaires :
1. Cell n'a pas de champ province_id (va ROUGE si province_id est ajouté).
2. La classe _NoBadSpatialField lève une TypeError explicite si une
   sous-classe dataclass déclare un champ province_id.
"""

import dataclasses
import inspect

import pytest

import sim.model
from sim.model import Cell, _NoBadSpatialField


def test_cell_has_no_province_id_field():
    """
    SC3 — Ce test va ROUGE si province_id (ou équivalent) est ajouté à Cell.
    Vérification par exécution sur la classe réelle.
    """
    field_names = {f.name for f in dataclasses.fields(Cell)}
    forbidden_normalised = {"provinceid"}
    found = {
        n for n in field_names
        if n.lower().replace("_", "") in forbidden_normalised
    }
    assert not found, (
        f"ADR-0003 : champs interdits trouvés dans Cell : {found}. "
        "Province est une agrégation dérivée, jamais un champ stocké."
    )


def test_province_id_field_raises_explicit_error():
    """
    SC3 — Preuve que le mécanisme de garde est actif : instancier une
    entité avec un champ province_id lève une TypeError explicite, pas
    un AttributeError silencieux.
    """
    @dataclasses.dataclass
    class BadEntity(_NoBadSpatialField):
        cell_id: int
        province_id: str  # violation ADR-0003

    with pytest.raises(TypeError, match="ADR-0003"):
        BadEntity(cell_id=1, province_id="France")


def test_province_id_variant_raises_explicit_error():
    """Variante : ProvinceId (camelCase) lève aussi une TypeError."""
    @dataclasses.dataclass
    class BadEntity2(_NoBadSpatialField):
        cell_id: int
        ProvinceId: str  # noqa: N815 — variante intentionnelle

    with pytest.raises(TypeError, match="ADR-0003"):
        BadEntity2(cell_id=1, ProvinceId="X")


def test_province_short_name_raises_explicit_error():
    """
    N3 — Forme courte 'province' lève aussi une TypeError.
    La garde couvre toute forme dont le nom normalisé commence par 'province'.
    """
    @dataclasses.dataclass
    class BadEntity3(_NoBadSpatialField):
        cell_id: int
        province: str  # forme courte — aussi interdite

    with pytest.raises(TypeError, match="ADR-0003"):
        BadEntity3(cell_id=1, province="France")


def test_province_code_raises_explicit_error():
    """province_code est aussi interdit (préfixe 'province')."""
    @dataclasses.dataclass
    class BadEntity4(_NoBadSpatialField):
        cell_id: int
        province_code: str

    with pytest.raises(TypeError, match="ADR-0003"):
        BadEntity4(cell_id=1, province_code="FR")


def test_aucune_dataclass_de_sim_model_ne_porte_de_province():
    """
    Brief 018, SC2 — cas introspectif ajouté aux cas nominatifs ci-dessus.

    Les cas précédents nomment `Cell`. Or `Person`, `Family` et `Building`
    n'existent pas encore : un contrôle nommé d'après sa cible laisserait
    passer la première entité créée après ce lot. Celui-ci découvre les
    dataclasses de `sim.model` par introspection et dérive le préfixe interdit
    de la garde elle-même.

    Compteurs : champs_province_sur_entites, dataclasses_inspectees
    (restreints ici à `sim.model` ; `sim/tests/test_province_aggregation.py`
    étend le même balayage au module d'agrégation).
    """
    prefixe = _NoBadSpatialField._FORBIDDEN_PREFIX

    classes = [
        obj
        for _nom, obj in inspect.getmembers(sim.model, inspect.isclass)
        if dataclasses.is_dataclass(obj) and obj.__module__ == sim.model.__name__
    ]

    champs_inspectes = 0
    fautifs = []
    for classe in classes:
        for champ in dataclasses.fields(classe):
            champs_inspectes += 1
            if champ.name.lower().replace("_", "").startswith(prefixe):
                fautifs.append(f"{classe.__name__}.{champ.name}")

    print(f"dataclasses_inspectees = {len(classes)} ({[c.__name__ for c in classes]})")
    print(f"champs_province_sur_entites = {len(fautifs)} / {champs_inspectes}")

    assert classes, "introspection vide : aucune dataclass trouvée dans sim.model"
    assert champs_inspectes > 0, "dénominateur nul : aucun champ regardé"
    assert not fautifs, (
        f"ADR-0003 : champs interdits trouvés par introspection : {fautifs}. "
        "Province est une agrégation dérivée, jamais un champ stocké."
    )
