"""Point d'entrée : python3 -m atelier."""

from __future__ import annotations

import argparse
import json
import shlex
import sys
from pathlib import Path

from . import backends, boite, couches, cycle, etat, porte, projet, quota, verrou
from .etat import FusionInterdite


ROLES = list(boite.ROLES)
ROLES_INVOCABLES = list(backends.ROLES_INVOCABLES)


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
    prochain.add_argument("--role", required=True, choices=ROLES)
    prochain.add_argument(
        "--champ",
        choices=["lot", "brief", "pr", "note"],
        help="n'imprimer qu'un champ (le shell n'a pas à lire du JSON)",
    )

    deposer = sous.add_parser("deposer", help="poser une carte dans une boîte")
    deposer.add_argument("--projet", required=True)
    deposer.add_argument("--etat", required=True)
    deposer.add_argument("--lot", required=True)
    deposer.add_argument("--brief", required=True)
    deposer.add_argument("--fichier", action="append", default=[])

    avancer = sous.add_parser("avancer", help="passer une carte au rôle suivant")
    avancer.add_argument("--projet", required=True)
    avancer.add_argument("--role", required=True, choices=ROLES)
    avancer.add_argument("--lot", required=True)
    avancer.add_argument("--pr", type=int)
    avancer.add_argument("--note")
    avancer.add_argument("--fichier", action="append", default=[])

    echouer = sous.add_parser("echouer", help="ranger une carte dans echec/")
    echouer.add_argument("--projet", required=True)
    echouer.add_argument("--role", required=True, choices=ROLES)
    echouer.add_argument("--lot", required=True)
    echouer.add_argument("--raison", required=True)

    invocation = sous.add_parser(
        "invocation", help="l'argv d'un rôle — imprimé, jamais lancé"
    )
    invocation.add_argument("--role", required=True, choices=ROLES_INVOCABLES)
    invocation.add_argument("--projet", required=True)
    invocation.add_argument("--lot")
    invocation.add_argument("--brief")
    invocation.add_argument(
        "--nul",
        action="store_true",
        help="séparer les arguments par NUL, pour un tableau shell",
    )

    verrouiller = sous.add_parser(
        "verrouiller", help="tenir les fichiers d'une carte avant d'écrire"
    )
    verrouiller.add_argument("--projet", required=True)
    verrouiller.add_argument("--role", required=True, choices=ROLES)
    verrouiller.add_argument("--lot", required=True)

    lever = sous.add_parser("lever", help="rendre les fichiers d'un lot")
    lever.add_argument("--projet", required=True)
    lever.add_argument("--lot", required=True)

    fumee = sous.add_parser("fumee", help="la commande de fumée du dépôt produit")
    fumee.add_argument("--projet", required=True)

    poste = sous.add_parser(
        "poste", help="qui tient un rôle, d'après le branchement du produit"
    )
    poste.add_argument("--projet", required=True)
    poste.add_argument("--role", required=True, choices=ROLES_INVOCABLES)
    poste.add_argument(
        "--champ",
        choices=["backend", "binaire", "abo", "modele", "lecture_seule"],
        help="n'imprimer qu'un champ (le shell n'a pas à tenir de table)",
    )
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
    except (boite.BoiteErreur, verrou.Collision) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if carte is None:
        print("RIEN")
        return 0
    if args.champ:
        valeur = carte.vers_dict()[args.champ]
        print("" if valeur is None else valeur)
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


def _fichiers_de_la_carte(racine: Path, carte: boite.Carte) -> list[str]:
    """Le périmètre d'une carte vient du brief, pas de la carte.

    Une carte déposée par le pilote peut ne rien nommer : le brief
    n'existait pas encore. Dès qu'il existe, sa section Périmètre fait
    foi — c'est la seule source d'instruction, verrou compris.
    """
    if carte.fichiers:
        return list(carte.fichiers)
    chemin = Path(carte.brief)
    if not chemin.is_absolute():
        chemin = Path(racine) / carte.brief
    if not chemin.is_file():
        return []
    # Le lecteur de périmètre vit dans `cycle` ; ce lot n'a pas le droit
    # d'écrire dans ce fichier, donc on l'appelle tel qu'il est plutôt
    # que d'en recopier une seconde version ici.
    return cycle._fichiers_du_perimetre(chemin)


def _cmd_avancer(args: argparse.Namespace) -> int:
    racine = Path(args.projet)
    champs: dict[str, object] = {}
    if args.pr is not None:
        champs["pr"] = args.pr
    if args.note:
        champs["note"] = args.note
    try:
        carte = boite.lire(racine, boite.BOITE_DU_ROLE[args.role], args.lot)
        if not carte.fichiers:
            deduits = list(args.fichier) or _fichiers_de_la_carte(racine, carte)
            if deduits:
                champs["fichiers"] = deduits
        cible = boite.avancer(racine, args.role, args.lot, **champs)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def _cmd_echouer(args: argparse.Namespace) -> int:
    try:
        cible = boite.echouer(Path(args.projet), args.role, args.lot, args.raison)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def _roles_du_produit(chemin: str) -> dict[str, str]:
    """Qui tient quel poste. Une seule réponse, et elle est dans le produit."""
    return projet.charger(chemin).roles.vers_dict()


def _cmd_invocation(args: argparse.Namespace) -> int:
    try:
        argv = backends.argv_du_role(
            args.role,
            roles=_roles_du_produit(args.projet),
            projet=args.projet,
            lot=args.lot,
            brief=args.brief,
        )
    except (backends.BackendErreur, projet.ProjetIncomplet, KeyError) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if args.nul:
        sys.stdout.write("\0".join(argv) + "\0")
        sys.stdout.flush()
        return 0
    print(shlex.join(argv))
    return 0


def _cmd_verrouiller(args: argparse.Namespace) -> int:
    racine = Path(args.projet)
    try:
        carte = boite.lire(racine, boite.BOITE_DU_ROLE[args.role], args.lot)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    fichiers = _fichiers_de_la_carte(racine, carte)
    if not fichiers:
        print(
            f"FAIL  la carte {args.lot} ne nomme aucun fichier et son brief "
            f"({carte.brief}) n'en donne pas : l'atelier ne devine pas un périmètre",
            file=sys.stderr,
        )
        return 1
    try:
        pose = verrou.poser(racine, args.lot, fichiers)
    except verrou.Collision as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(" ".join(sorted(pose.fichiers)))
    return 0


def _cmd_lever(args: argparse.Namespace) -> int:
    verrou.lever(Path(args.projet), args.lot)
    print(f"verrou levé : {args.lot}")
    return 0


def _cmd_poste(args: argparse.Namespace) -> int:
    try:
        poste = backends.poste_du_role(args.role, _roles_du_produit(args.projet))
    except (backends.BackendErreur, projet.ProjetIncomplet, KeyError) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    champs = {
        "backend": poste.backend,
        "binaire": poste.binaire,
        "abo": poste.abo,
        "modele": poste.modele or "",
        "lecture_seule": poste.lecture_seule,
    }
    if args.champ:
        print(champs[args.champ])
        return 0
    print(f"role          {poste.role}")
    for nom, valeur in champs.items():
        print(f"{nom:<13} {valeur}")
    return 0


def _cmd_fumee(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
    except projet.ProjetIncomplet as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(produit.fumee)
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
    if args.commande == "avancer":
        return _cmd_avancer(args)
    if args.commande == "echouer":
        return _cmd_echouer(args)
    if args.commande == "invocation":
        return _cmd_invocation(args)
    if args.commande == "verrouiller":
        return _cmd_verrouiller(args)
    if args.commande == "lever":
        return _cmd_lever(args)
    if args.commande == "fumee":
        return _cmd_fumee(args)
    if args.commande == "poste":
        return _cmd_poste(args)
    return 1


if __name__ == "__main__":
    sys.exit(main())
