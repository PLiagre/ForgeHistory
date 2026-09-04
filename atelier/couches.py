"""Les sept couches. Un module en déclare une, chez lui.

Ce fichier tenait la liste des modules du dépôt. C'était une seconde
source : elle pouvait dire d'un module ce que le module ne disait pas de
lui-même, un module supprimé y restait, un module neuf s'y oubliait. Et
tout lot qui ajoutait un composant devait écrire ici — donc aucun de ces
lots n'était disjoint d'un autre.

VISION.md dit : « Chaque composant de ce dépôt **déclare** une couche. »
*Déclare*, pas *est inscrit ailleurs*. Un module pose donc, en tête :

    COUCHE = "orchestration"

Une chaîne, pas l'énumération : un module qui importerait `couches`
fermerait un cycle avec la découverte, qui l'importe. Le registre la
valide contre `Couche` — un mot inconnu est refusé, et il est nommé.
"""

from __future__ import annotations

from enum import Enum
import importlib
import pkgutil
from types import ModuleType


class Couche(str, Enum):
    INTELLIGENCE = "intelligence"
    OUTILS = "outils"
    MEMOIRE = "memoire"
    EXECUTION = "execution"
    ORCHESTRATION = "orchestration"
    COORDINATION = "coordination"
    VERIFICATION = "verification"


# Le nom de l'attribut que chaque module pose. Il s'écrit une fois : les
# messages d'erreur le citent, ils ne le recopient pas.
ATTRIBUT = "COUCHE"

# Ce qui n'est pas un composant, et ne déclare donc rien : le point
# d'entrée, qui appelle et ne raisonne pas, et ce registre, qui ne peut
# pas s'inscrire lui-même. Un paquet n'est pas parcouru non plus — la
# surface du programme n'occupe pas de couche.
#
# Trois noms, et ils ne se multiplient pas : tout autre module sans
# déclaration est un défaut, pas une exception qu'on ajoute ici.
SANS_COUCHE = ("__main__", "couches")


class CoucheErreur(ValueError):
    """Un module ne déclare pas sa couche, ou en déclare une inconnue."""


def decouvrir(paquet: ModuleType | None = None) -> dict[str, Couche]:
    """Ce que les modules déclarent. Un oubli est un défaut, pas un vide.

    `paquet` sert aux contrôles : ils éprouvent la découverte sur un
    paquet jetable plutôt que d'abîmer celui-ci.
    """
    paquet = paquet or importlib.import_module(__package__ or "atelier")
    trouves: dict[str, Couche] = {}
    for info in sorted(pkgutil.iter_modules(paquet.__path__), key=lambda i: i.name):
        if info.ispkg or info.name in SANS_COUCHE:
            continue
        nom = f"{paquet.__name__}.{info.name}"
        module = importlib.import_module(nom)
        brut = getattr(module, ATTRIBUT, None)
        if brut is None:
            raise CoucheErreur(
                f"{nom} ne déclare pas sa couche : un module de ce paquet pose "
                f"`{ATTRIBUT} = \"<couche>\"` en tête "
                f"(connues : {', '.join(c.value for c in Couche)})"
            )
        try:
            trouves[nom] = Couche(brut)
        except ValueError as exc:
            raise CoucheErreur(
                f"{nom} déclare la couche « {brut} », qui n'existe pas "
                f"(connues : {', '.join(c.value for c in Couche)})"
            ) from exc
    if not trouves:
        raise CoucheErreur(
            f"aucun module de {paquet.__name__} ne déclare de couche — "
            "un échantillon vide échoue"
        )
    return trouves


# Le registre, dérivé au chargement. Il rend ce que la table rendait ;
# c'est la même valeur, lue à sa source.
MODULES: dict[str, Couche] = decouvrir()


def couche_de(module: str) -> Couche:
    if module not in MODULES:
        raise KeyError(f"module sans couche déclarée : {module}")
    return MODULES[module]


def couches_occupees() -> dict[Couche, list[str]]:
    occupees: dict[Couche, list[str]] = {c: [] for c in Couche}
    for nom, couche in MODULES.items():
        occupees[couche].append(nom)
    return occupees
