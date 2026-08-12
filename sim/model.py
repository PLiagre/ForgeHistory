"""
Modèle de données du moteur de simulation.

Règle ADR-0003 : cell_id est la seule clé spatiale. Province est
une agrégation dérivée — jamais un champ stocké sur une entité.
"""

from dataclasses import dataclass, field


class _NoBadSpatialField:
    """
    Classe de base qui applique l'ADR-0003 : aucun champ province_id
    (ou équivalent) ne peut être déclaré sur une entité spatiale.

    Toute sous-classe dataclass qui déclare un tel champ lèvera une
    TypeError explicite à l'instanciation (via __post_init__).
    """

    # Tout champ dont le nom normalisé (minuscules, sans tirets bas) commence
    # par "province" est interdit — couvre province_id, province_code, province, etc.
    _FORBIDDEN_PREFIX = "province"

    def __post_init__(self):
        for name in self.__dataclass_fields__:  # type: ignore[attr-defined]
            normalised = name.lower().replace("_", "")
            if normalised.startswith(self._FORBIDDEN_PREFIX):
                raise TypeError(
                    f"ADR-0003 : le champ '{name}' est interdit. "
                    "Province est une agrégation dérivée, jamais un champ "
                    "stocké. Utilisez cell_id comme seule clé spatiale."
                )


@dataclass
class Cell(_NoBadSpatialField):
    """
    Unité géographique de base du monde simulé.

    Champs :
        cell_id        : identifiant unique de la cellule (clé spatiale, ADR-0003).
        area_km2       : superficie en km² (lecture seule après chargement).
        population     : nombre d'habitants (agrégat, modifié par le moteur).
        food_stock_kg  : stock de nourriture disponible en kg.
                         Sentinelle -1 = non calculé (hard-won rule 8).
        hunger_ticks   : ticks consécutifs sans nourriture suffisante.
                         Sentinelle -1 = non initialisé (hard-won rule 8).
    """

    cell_id: int
    area_km2: float
    population: int
    food_stock_kg: float = field(default=-1.0)
    hunger_ticks: int = field(default=-1)
