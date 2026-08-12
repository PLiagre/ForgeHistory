"""
SC3 — Respect de l'ADR-0003 : cell_id comme seule clé spatiale.

Deux vérifications complémentaires :
1. Cell n'a pas de champ province_id (va ROUGE si province_id est ajouté).
2. La classe _NoBadSpatialField lève une TypeError explicite si une
   sous-classe dataclass déclare un champ province_id.
"""

import dataclasses
import pytest

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
