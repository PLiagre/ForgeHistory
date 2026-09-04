"""Ce que GitHub publie, lu par une commande injectable.

Le numéro de PR déposé dans le canal d'échange, le verdict des contrôles
obligatoires, et l'état d'une PR. Une doctrine commune : **un inconnu
n'est ni un oui ni un non**, il rend 2, et 2 n'est jamais un feu vert.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .. import echange
from . import Commande


def _poser_pr(p: argparse.ArgumentParser) -> None:
    p.add_argument("--fichier", required=True, help="chemin de atelier-echange/pr.txt")
    p.add_argument(
        "--branche",
        help="si gh répond, la PR doit être sur cette branche ; sinon la sonde se tait",
    )
    p.add_argument(
        "--worktree",
        help="dépôt depuis lequel sonder gh (remote origin) ; ignoré sans --branche",
    )


def _pr(args: argparse.Namespace) -> int:
    try:
        numero = echange.lire_numero_pr(Path(args.fichier))
        if args.branche:
            racine = Path(args.worktree) if args.worktree else Path(args.fichier).parent.parent
            echange.verifier_pr_branche_optionnel(numero, args.branche, racine)
    except echange.EchangeErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(numero)
    return 0


def _poser_ci(p: argparse.ArgumentParser) -> None:
    p.add_argument("--pr", type=int, required=True)
    p.add_argument(
        "--worktree",
        default=".",
        help="dépôt depuis lequel sonder (la commande vient de ATELIER_CI_CMD, sinon gh)",
    )


def _ci(args: argparse.Namespace) -> int:
    """Trois verdicts, trois codes. L'inconnu n'est pas un vert.

    Le rouge nomme ses fautifs, un par ligne : c'est le nom qui dit au
    propriétaire quoi regarder, jamais un compte.
    """
    verdict = echange.verdict_ci(args.pr, Path(args.worktree))
    if verdict.etat == echange.ROUGE:
        for nom in verdict.fautifs:
            print(nom)
        print(f"rouge : {len(verdict.fautifs)} contrôle(s) obligatoire(s) en échec",
              file=sys.stderr)
        return 1
    if verdict.etat == echange.INCONNU:
        print(echange.INCONNU)
        print(f"inconnue : {verdict.raison}", file=sys.stderr)
        return 2
    print(echange.VERT)
    return 0


def _poser_pr_etat(p: argparse.ArgumentParser) -> None:
    p.add_argument("--pr", type=int, required=True)
    p.add_argument(
        "--worktree",
        default=".",
        help="dépôt depuis lequel sonder (la commande vient de ATELIER_PR_CMD, sinon gh)",
    )


def _pr_etat(args: argparse.Namespace) -> int:
    """Quatre réponses, et l'inconnu en est une. Elle rend 2, jamais 0."""
    etat = echange.etat_pr(args.pr, Path(args.worktree))
    print(etat)
    return 2 if etat == echange.INCONNU else 0


def commandes() -> list[Commande]:
    return [
        Commande("pr", "lire un numéro de PR dans un fichier d'échange ; refuse tout autre format",
                 _poser_pr, _pr),
        Commande("ci", "le verdict des contrôles obligatoires d'une PR : 0 vert, 1 rouge, 2 inconnu",
                 _poser_ci, _ci),
        Commande("pr-etat", "l'état d'une PR : ouverte, fusionnee, fermee — ou inconnue (code 2)",
                 _poser_pr_etat, _pr_etat),
    ]
