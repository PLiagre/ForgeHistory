"""Point d'entrée : python3 -m atelier.

Ce fichier ne connaît aucune commande. Il construit le parseur, laisse
`atelier.commandes` y poser ce qu'elle découvre, et appelle ce que
l'utilisateur a nommé.

Il portait les vingt-huit sous-parseurs du programme, et leur table de
dispatch. Le verrou de l'atelier tient des fichiers : tout lot qui
apportait une commande devait donc écrire ici, et aucun de ces lots
n'était disjoint d'un autre. Ajouter une commande, maintenant, c'est
ajouter un fichier.
"""

from __future__ import annotations

import argparse
import sys

from . import commandes


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="atelier",
        description="Infrastructure d'agents pour exécuter des lots. Pas un agent de plus.",
    )
    sous = parser.add_subparsers(dest="commande", required=True)
    commandes.poser(sous)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    faire = commandes.table().get(args.commande)
    if faire is None:
        # Le parseur refuse déjà un nom inconnu ; on n'arrive ici que si
        # une commande est posée sans être appelable, et c'est un défaut
        # de ce dépôt, pas une faute de l'utilisateur.
        print(f"FAIL  commande posée mais introuvable : {args.commande}", file=sys.stderr)
        return 1
    return faire(args)


if __name__ == "__main__":
    sys.exit(main())
