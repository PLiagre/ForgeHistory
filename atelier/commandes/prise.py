"""Prendre une carte, lire un champ, rendre la carte.

`prendre` fait ce que `prochain` puis `verrouiller` faisaient en deux
temps. `prochain` reste : il **regarde** sans prendre, et un aperçu qui
prendrait ne serait plus un aperçu.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

from .. import boite, prise as module_prise
from . import Commande


RIEN = "RIEN"


def _poser_prendre(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))


def _prendre(args: argparse.Namespace) -> int:
    """Le lot pris, ou RIEN. Code 0 dans les deux cas : une file vide
    n'est pas une panne, et un tour qui n'a rien à faire sort bien."""
    try:
        carte = module_prise.prendre(Path(args.projet), args.role)
    except module_prise.ServrureTenue as exc:
        print(RIEN)
        print(str(exc), file=sys.stderr)
        return 0
    except (module_prise.PriseErreur, boite.BoiteErreur) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if carte is None:
        print(RIEN)
        return 0
    print(carte.lot)
    return 0


def _poser_carte(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--lot", required=True)
    p.add_argument(
        "--etat",
        help="la boîte où lire la carte ; par défaut, celle où elle dort",
    )
    p.add_argument(
        "--champ",
        choices=["lot", "brief", "pr", "note", "cause", "essais", "role"],
        help="n'imprimer qu'un champ (le shell n'a pas à lire du JSON)",
    )


def _carte(args: argparse.Namespace) -> int:
    """Ce que porte une carte, où qu'elle soit.

    `prochain` répond « que prendrait ce rôle ? ». Celle-ci répond
    « que dit cette carte-là ? » — la question du tour qui vient de la
    prendre et qui a besoin de son brief.
    """
    racine = Path(args.projet)
    etat = args.etat or boite.ou_est(racine, args.lot)
    if etat is None:
        print(f"FAIL  aucune boîte ne porte la carte {args.lot}", file=sys.stderr)
        return 1
    try:
        carte = boite.lire(racine, etat, args.lot)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if args.champ:
        valeur = carte.vers_dict()[args.champ]
        print("" if valeur is None else valeur)
        return 0
    print(json.dumps(carte.vers_dict(), ensure_ascii=False))
    return 0


def _poser_rendre(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))
    p.add_argument("--lot", required=True)


def _rendre(args: argparse.Namespace) -> int:
    try:
        cible = module_prise.rendre(Path(args.projet), args.role, args.lot)
    except (module_prise.PriseErreur, boite.BoiteErreur) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def commandes() -> list[Commande]:
    return [
        Commande(
            "prendre",
            "prendre une carte et ses fichiers en un seul geste ; RIEN si rien n'est libre",
            _poser_prendre, _prendre,
        ),
        Commande(
            "carte",
            "ce que porte une carte, où qu'elle soit",
            _poser_carte, _carte,
        ),
        Commande(
            "rendre",
            "remettre une carte de en-cours/ dans la boîte de son rôle",
            _poser_rendre, _rendre,
        ),
    ]
