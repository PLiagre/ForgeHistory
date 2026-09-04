"""Les commandes de l'atelier. Chacune vit chez elle ; le centre découvre.

`atelier/__main__.py` portait les vingt-cinq `add_parser` du programme.
Le verrou de l'atelier tient des fichiers : tout lot qui apportait une
commande devait écrire dans ce fichier-là, donc aucun de ces lots
n'était disjoint d'un autre. Le goulot n'était pas la plomberie, c'était
un registre que tout le monde devait éditer.

Un module de ce paquet déclare ses commandes par `commandes()`. Personne
ne les inscrit ailleurs : ajouter une commande, c'est ajouter un
fichier.

Un module de commande **n'occupe pas de couche**. Il ne raisonne pas, il
appelle : c'est la surface du programme, pas un composant. La découverte
des couches ne descend donc pas dans ce paquet.
"""

from __future__ import annotations

import argparse
from collections.abc import Callable
from dataclasses import dataclass
import importlib
import pkgutil
from types import ModuleType

from .. import projet


class CommandeErreur(ValueError):
    """Un module de ce paquet ne respecte pas le contrat. On le nomme."""


@dataclass(frozen=True)
class Commande:
    """Un nom, une aide, de quoi poser ses arguments et de quoi la faire.

    `poser` reçoit le sous-parseur de la commande et y ajoute ses
    arguments. `faire` reçoit les arguments analysés et rend le code de
    sortie. Rien d'autre ne circule : une commande ne connaît pas le
    parseur global, et le parseur global ne connaît aucune commande.
    """

    nom: str
    aide: str
    poser: Callable[[argparse.ArgumentParser], None]
    faire: Callable[[argparse.Namespace], int]


# Le nom de la fonction que chaque module doit exposer. Il s'écrit une
# fois : le message d'erreur le cite, il ne le recopie pas.
CONTRAT = "commandes"


def modules(paquet: ModuleType | None = None) -> list[ModuleType]:
    """Les modules de commande, dans l'ordre de leur nom.

    `paquet` sert aux contrôles : ils éprouvent la découverte sur un
    paquet jetable plutôt que d'écrire dans celui-ci.
    """
    paquet = paquet or importlib.import_module(__name__)
    trouves: list[ModuleType] = []
    for info in sorted(pkgutil.iter_modules(paquet.__path__), key=lambda i: i.name):
        if info.name.startswith("_"):
            continue
        trouves.append(importlib.import_module(f"{paquet.__name__}.{info.name}"))
    return trouves


def commandes_du_module(module: ModuleType) -> list[Commande]:
    """Ce qu'un module déclare. Un module muet est un défaut, pas un vide.

    Ignorer un module qui ne déclare rien ferait disparaître une
    commande en silence le jour d'une faute de frappe : la commande
    n'existerait plus, et rien ne le dirait.
    """
    declarer = getattr(module, CONTRAT, None)
    if declarer is None:
        raise CommandeErreur(
            f"{module.__name__} ne déclare pas ses commandes : "
            f"un module de ce paquet expose `{CONTRAT}()`"
        )
    trouvees = declarer()
    if not trouvees:
        raise CommandeErreur(
            f"{module.__name__}.{CONTRAT}() ne déclare aucune commande — "
            "un échantillon vide échoue"
        )
    for commande in trouvees:
        if not isinstance(commande, Commande):
            raise CommandeErreur(
                f"{module.__name__}.{CONTRAT}() rend {type(commande).__name__}, "
                "pas une Commande"
            )
    return list(trouvees)


def toutes(paquet: ModuleType | None = None) -> list[Commande]:
    """Toutes les commandes déclarées. Un nom en double est un défaut."""
    vues: dict[str, str] = {}
    resultat: list[Commande] = []
    for module in modules(paquet):
        for commande in commandes_du_module(module):
            if commande.nom in vues:
                raise CommandeErreur(
                    f"la commande « {commande.nom} » est déclarée deux fois : "
                    f"{vues[commande.nom]} et {module.__name__}"
                )
            vues[commande.nom] = module.__name__
            resultat.append(commande)
    if not resultat:
        raise CommandeErreur("aucune commande découverte — un échantillon vide échoue")
    return resultat


def poser(sous: argparse._SubParsersAction) -> None:
    """Pose chaque commande sur le sous-parseur. Le seul `add_parser` du programme."""
    for commande in toutes():
        commande.poser(sous.add_parser(commande.nom, help=commande.aide))


def table() -> dict[str, Callable[[argparse.Namespace], int]]:
    """Quel nom appelle quoi."""
    return {commande.nom: commande.faire for commande in toutes()}


# ------------------------------------------------- ce que deux modules partagent


def roles_du_produit(chemin: str) -> dict[str, str]:
    """Qui tient quel poste. Une seule réponse, et elle est dans le produit."""
    return projet.charger(chemin).roles.vers_dict()


def feuille_relative(produit: projet.Projet) -> str | None:
    """Le chemin de la feuille tel qu'un prompt le cite : relatif au produit."""
    if produit.feuille is None:
        return None
    try:
        return produit.feuille.relative_to(produit.racine).as_posix()
    except ValueError:
        return produit.feuille.as_posix()
