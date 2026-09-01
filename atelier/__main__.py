"""Point d'entrée : python3 -m atelier."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from . import boite, couches, cycle, etat, porte, projet, quota, verrou
from .etat import FusionInterdite


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="atelier",
        description="Infrastructure d'agents pour exécuter des lots. Pas un agent de plus.",
    )
    sous = parser.add_subparsers(dest="commande", required=True)

    sous.add_parser("couches", help="afficher les sept couches et qui les occupe")

    doctor = sous.add_parser("doctor", help="vérifier le branchement d'un dépôt produit")
    doctor.add_argument("--projet", required=True)

    portes = sous.add_parser("portes", help="jouer la porte mécanique sur un brief")
    portes.add_argument("--brief", required=True)

    start = sous.add_parser("start", help="préparer un lot (aperçu, ou --run)")
    start.add_argument("brief")
    start.add_argument("--projet", required=True)
    start.add_argument("--run", action="store_true")

    status = sous.add_parser("status", help="lister les runs durables")
    status.add_argument("--projet", required=True)

    verrous = sous.add_parser("verrous", help="fichiers actuellement tenus")
    verrous.add_argument("--projet", required=True)

    sous.add_parser("fusionner", help="refuse toujours — le propriétaire fusionne")

    hop = sous.add_parser("hop", help="choisir un agent d'après des quotas connus")
    hop.add_argument(
        "mesure",
        nargs="+",
        help="paires agent=restant (restant=-1 si inconnu)",
    )

    prochain = sous.add_parser(
        "prochain",
        help="prochaine carte d'un rôle ; RIEN et code 0 si la boîte est vide",
    )
    prochain.add_argument("--projet", required=True)
    prochain.add_argument("--role", required=True, choices=["briefer", "planifier", "coder", "relire"])

    deposer = sous.add_parser("deposer", help="poser une carte dans une boîte")
    deposer.add_argument("--projet", required=True)
    deposer.add_argument("--etat", required=True)
    deposer.add_argument("--lot", required=True)
    deposer.add_argument("--brief", required=True)
    deposer.add_argument("--fichier", action="append", default=[])
    return parser


def _cmd_couches() -> int:
    occupees = couches.couches_occupees()
    for couche in couches.Couche:
        modules = occupees[couche]
        if not modules:
            print(f"FAIL  {couche.value} — aucun module")
            return 1
        print(f"PASS  {couche.value} — {', '.join(modules)}")
    return 0


def _cmd_doctor(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
    except projet.ProjetIncomplet as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(f"PASS  projet={produit.nom}")
    print(f"      briefs={produit.briefs}")
    print(f"      tests={produit.tests}")
    print(f"      fumee={produit.fumee}")
    print(
        f"      roles=écrire:{produit.roles.ecriture} "
        f"exécuter:{produit.roles.execution} "
        f"relire:{produit.roles.controle}"
    )
    if not produit.briefs.is_dir():
        print(f"FAIL  dossier briefs introuvable : {produit.briefs}", file=sys.stderr)
        return 1
    return 0


def _cmd_portes(args: argparse.Namespace) -> int:
    print(porte.rendre(Path(args.brief)))
    return 0 if porte.passer(Path(args.brief)) else 1


def _cmd_start(args: argparse.Namespace) -> int:
    brief = Path(args.brief)
    try:
        apercu = cycle.preparer(brief, args.projet)
    except (cycle.CycleErreur, projet.ProjetIncomplet) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(f"lot        {apercu.lot}")
    print(f"branche    {apercu.branche}")
    print(f"worktree   {apercu.worktree}")
    print(f"fichiers   {', '.join(apercu.fichiers)}")
    print(f"écrire     {apercu.ecrivain}")
    print(f"exécuter   {apercu.executant} → {apercu.commande_executant}")
    print(f"relire     {apercu.relecteur} → {apercu.commande_relecteur}")
    print(f"isolation  {apercu.commande_worktree}")
    print()
    print(apercu.portes)
    print()
    print(apercu.note)
    if not args.run:
        print("poursuivre : relancer avec --run")
        return 0
    try:
        run = cycle.lancer(brief, args.projet)
    except (cycle.CycleErreur, verrou.Collision) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(f"RUN {run.id}  etape={run.etape.value}  auteur≠relecteur")
    return 0


def _cmd_status(args: argparse.Namespace) -> int:
    runs = etat.lister(Path(args.projet))
    if not runs:
        print("aucun run")
        return 0
    for run in runs:
        print(f"{run.id}  {run.lot}  {run.etape.value}  {run.auteur_code}≠{run.relecteur}")
    return 0


def _cmd_verrous(args: argparse.Namespace) -> int:
    try:
        tableau = verrou.charger(Path(args.projet))
    except verrou.Collision as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if not tableau.poses:
        print("aucun verrou")
        return 0
    for pose in tableau.poses:
        print(f"{pose.lot}  {', '.join(sorted(pose.fichiers))}")
    return 0


def _cmd_fusionner() -> int:
    try:
        etat.fusionner(None)  # type: ignore[arg-type]
    except FusionInterdite as exc:
        print(f"REFUS  {exc}", file=sys.stderr)
        return 2
    print("FAIL  fusionner n'a pas refusé", file=sys.stderr)
    return 1


def _cmd_hop(args: argparse.Namespace) -> int:
    mesures: list[quota.Quota] = []
    for piece in args.mesure:
        if "=" not in piece:
            print(f"FAIL  mesure mal formée : {piece} (attendu agent=restant)", file=sys.stderr)
            return 1
        agent, restant = piece.split("=", 1)
        try:
            mesures.append(quota.Quota(agent=agent, restant=int(restant)))
        except ValueError:
            print(f"FAIL  restant non entier : {restant}", file=sys.stderr)
            return 1
    try:
        choisi = quota.hop(mesures)
    except ValueError as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(choisi.agent)
    return 0


def _cmd_prochain(args: argparse.Namespace) -> int:
    try:
        carte = boite.prochain(Path(args.projet), args.role)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if carte is None:
        print("RIEN")
        return 0
    print(json.dumps(carte.vers_dict(), ensure_ascii=False))
    return 0


def _cmd_deposer(args: argparse.Namespace) -> int:
    try:
        cible = boite.deposer(
            Path(args.projet),
            args.etat,
            boite.Carte(lot=args.lot, brief=args.brief, fichiers=list(args.fichier)),
        )
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if args.commande == "couches":
        return _cmd_couches()
    if args.commande == "doctor":
        return _cmd_doctor(args)
    if args.commande == "portes":
        return _cmd_portes(args)
    if args.commande == "start":
        return _cmd_start(args)
    if args.commande == "status":
        return _cmd_status(args)
    if args.commande == "verrous":
        return _cmd_verrous(args)
    if args.commande == "fusionner":
        return _cmd_fusionner()
    if args.commande == "hop":
        return _cmd_hop(args)
    if args.commande == "prochain":
        return _cmd_prochain(args)
    if args.commande == "deposer":
        return _cmd_deposer(args)
    return 1


if __name__ == "__main__":
    sys.exit(main())
