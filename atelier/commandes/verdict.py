"""Le verdict d'une relecture : trois réponses, trois codes de sortie.

    0  PASS valide      la suite est autorisée
    1  FAIL valide      le travail retourne à son auteur, avec ses motifs
    2  tout le reste    absent, illisible, périmé, interdit

Le code 2 n'est jamais un feu vert. C'est la doctrine des sondes de
`atelier/echange.py`, appliquée à ce que le relecteur dépose : une porte
qui s'ouvre quand la sonde se tait cède exactement quand elle ne répond
plus.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .. import verdict as module_verdict
from . import Commande


def _poser_verdict(p: argparse.ArgumentParser) -> None:
    sous = p.add_subparsers(dest="action", required=True)
    lire = sous.add_parser(
        "lire",
        help="0 si PASS valide, 1 si FAIL valide, 2 si absent, illisible, périmé ou interdit",
    )
    lire.add_argument(
        "--fichier", required=True,
        help="le verdict déposé par le relecteur, dans le canal d'échange",
    )
    lire.add_argument(
        "--sha", required=True,
        help="la révision courante de la branche relue : un verdict qui porte sur une autre est périmé",
    )
    lire.add_argument(
        "--auteur-code", required=True, dest="auteur_code",
        help="qui a écrit le code : il ne peut pas signer son propre verdict",
    )


def _lire(args: argparse.Namespace) -> int:
    try:
        rendu = module_verdict.lire_et_valider(
            Path(args.fichier), sha=args.sha, auteur_code=args.auteur_code
        )
    except module_verdict.VerdictErreur as exc:
        # Ni vert ni rouge : on ne sait pas. Et on ne parie pas dessus.
        print(module_verdict.INCONNU)
        print(f"FAIL  {exc}", file=sys.stderr)
        return 2
    if rendu.passe:
        print(module_verdict.PASS)
        return 0
    for motif in rendu.motifs:
        print(motif)
    print(f"{module_verdict.FAIL} : {len(rendu.motifs)} motif(s)", file=sys.stderr)
    return 1


_ACTIONS = {"lire": _lire}


def _verdict(args: argparse.Namespace) -> int:
    return _ACTIONS[args.action](args)


def commandes() -> list[Commande]:
    return [
        Commande(
            "verdict",
            "lire le verdict d'une relecture : 0 PASS, 1 FAIL, 2 inconnu",
            _poser_verdict,
            _verdict,
        ),
    ]
