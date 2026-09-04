"""Le répertoire de travail d'un lot.

Sans `--run`, la commande imprime le chemin et ne crée rien : un aperçu
n'est pas une dépense. Avec `--run`, elle crée le worktree s'il manque et
le reprend s'il est là. Avec `--liberer --run`, elle le rend — la branche
reste, c'est elle qui porte la PR.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .. import projet, worktree as module_worktree
from . import Commande


def _poser(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--lot", required=True)
    p.add_argument(
        "--run",
        action="store_true",
        help="créer ou reprendre le worktree ; sans --run, on imprime le chemin",
    )
    p.add_argument(
        "--liberer",
        action="store_true",
        help="rendre le répertoire du lot (la branche reste) ; exige --run",
    )


def _faire(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
        branche = produit.branche_du_lot(args.lot)
        chemin = module_worktree.chemin_du_lot(produit.racine, args.lot)
    except (projet.ProjetIncomplet, module_worktree.WorktreeErreur) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1

    if not args.run:
        # Le chemin se lit sans rien créer : c'est ce qu'un script
        # affiche pour dire où il travaillerait.
        print(chemin)
        if args.liberer:
            print("sans --run : rien n'est retiré.", file=sys.stderr)
        return 0

    if args.liberer:
        try:
            rendu = module_worktree.liberer_le_lot(produit.racine, args.lot)
        except module_worktree.WorktreeErreur as exc:
            print(f"FAIL  {exc}", file=sys.stderr)
            return 1
        if rendu is None:
            print(f"aucun worktree pour {args.lot} : rien à rendre", file=sys.stderr)
            return 0
        print(rendu)
        return 0

    try:
        cible = module_worktree.preparer_le_lot(
            produit.racine, args.lot, branche, produit.branche_base
        )
    except module_worktree.WorktreeErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def commandes() -> list[Commande]:
    return [
        Commande(
            "worktree",
            "le répertoire de travail d'un lot, dérivé ; --run le crée ou le reprend",
            _poser, _faire,
        ),
    ]
