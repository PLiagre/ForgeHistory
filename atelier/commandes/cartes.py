"""Les cartes et les fichiers qu'elles tiennent.

Prendre la prochaine, en déposer une, la faire avancer, la faire tomber,
la rappeler, la reprendre — et le tableau des verrous, qui dit quel lot
tient quel fichier.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

from .. import boite, cycle, reprise, verrou
from . import Commande


def _poser_prochain(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))
    p.add_argument(
        "--champ",
        choices=["lot", "brief", "pr", "note"],
        help="n'imprimer qu'un champ (le shell n'a pas à lire du JSON)",
    )


def _declarer_file_bloquee(racine: Path, role: str) -> None:
    """Une file vide reste silencieuse. Une file tenue par un verrou parle."""
    try:
        cartes = boite.lister(racine, boite.BOITE_DU_ROLE[role])
        poses = verrou.charger(racine).poses
    except (boite.BoiteErreur, verrou.Collision):
        return
    if not cartes:
        return
    tenus = {fichier: pose.lot for pose in poses for fichier in pose.fichiers}
    for carte in cartes:
        for fichier in sorted(carte.fichiers):
            tenu_par = tenus.get(Path(fichier).as_posix())
            if tenu_par and tenu_par != carte.lot:
                print(
                    f"{carte.lot} attend : {fichier} est tenu par {tenu_par}",
                    file=sys.stderr,
                )
    print(
        "aucune carte libre — `atelier lever --lot <lot>` après ta fusion.",
        file=sys.stderr,
    )


def _prochain(args: argparse.Namespace) -> int:
    try:
        carte = boite.prochain(Path(args.projet), args.role)
    except (boite.BoiteErreur, verrou.Collision) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if carte is None:
        if args.role == boite.ROLE_QUI_ECRIT:
            _declarer_file_bloquee(Path(args.projet), args.role)
        print("RIEN")
        return 0
    if args.champ:
        valeur = carte.vers_dict()[args.champ]
        print("" if valeur is None else valeur)
        return 0
    print(json.dumps(carte.vers_dict(), ensure_ascii=False))
    return 0


def _poser_deposer(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--etat", required=True)
    p.add_argument("--lot", required=True)
    p.add_argument("--brief", required=True)
    p.add_argument("--fichier", action="append", default=[])


def _deposer(args: argparse.Namespace) -> int:
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
    # Le lecteur de périmètre vit dans `cycle` : on l'appelle tel qu'il
    # est plutôt que d'en recopier une seconde version ici.
    return cycle._fichiers_du_perimetre(chemin)


def _poser_avancer(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))
    p.add_argument("--lot", required=True)
    p.add_argument("--pr", type=int)
    p.add_argument("--note")
    p.add_argument("--fichier", action="append", default=[])


def _avancer(args: argparse.Namespace) -> int:
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


def _poser_echouer(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))
    p.add_argument("--lot", required=True)
    p.add_argument("--raison", required=True)
    p.add_argument(
        "--cause",
        choices=list(reprise.CAUSES),
        default=reprise.INCONNUE,
        help="le mot que la machine compare pour décider si ça se retente",
    )


def _echouer(args: argparse.Namespace) -> int:
    try:
        cible = boite.echouer(
            Path(args.projet), args.role, args.lot, args.raison, args.cause
        )
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(cible)
    return 0


def _poser_rappeler(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))


def _rappeler(args: argparse.Namespace) -> int:
    """Ce qui revient seul de echec/. Le reste attend une personne, et on dit qui.

    Appelé par `crons/tour.sh` au début de chaque tour : le rôle se
    rattrape lui-même au réveil suivant, sans que le pilote arbitre et
    sans qu'une horloge de plus soit à lire — deux heures séparent déjà
    deux tours du coder.
    """
    racine = Path(args.projet)
    try:
        rappelees = boite.rappeler(racine, args.role)
        restantes = [
            c for c in boite.lister(racine, "echec")
            if c.role == args.role and c.lot not in {r.lot for r in rappelees}
        ]
    except (boite.BoiteErreur, verrou.Collision) as exc:
        # Un rappel qui échoue n'annule pas le tour : la carte reste où
        # elle est, et le rôle continue avec ce que sa file porte.
        print(f"rappel impossible : {exc}", file=sys.stderr)
        return 0
    for carte in rappelees:
        print(
            f"rappelé  {carte.lot} : echec → {boite.BOITE_DU_ROLE[args.role]} "
            f"(cause « {carte.cause} », essai {carte.essais + 1})"
        )
    for carte in restantes:
        print(
            f"reste    {carte.lot} : {reprise.raison_du_refus(carte.cause, carte.essais)}",
            file=sys.stderr,
        )
    return 0


def _poser_reprendre(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--lot", required=True)
    p.add_argument(
        "--etat",
        help="la boîte d'où sortir la carte ; par défaut, celle où elle dort",
    )


def _reprendre(args: argparse.Namespace) -> int:
    """Sortir une carte de sa boîte, quelle qu'elle soit.

    `reprendre` ne connaissait que `echec/`. Une carte coincée dans
    `faite/` — la PR fermée sans être fusionnée — ou dans `a-relire`
    n'avait aucune sortie : il fallait supprimer un fichier JSON à la
    main. Le verrou du lot tombe avec elle, sinon les fichiers qu'elle
    tenait resteraient tenus par un lot qui n'est plus nulle part.
    """
    racine = Path(args.projet)
    etat = args.etat or boite.ou_est(racine, args.lot)
    if etat is None:
        print(
            f"FAIL  aucune boîte ne porte la carte {args.lot} : rien à reprendre",
            file=sys.stderr,
        )
        return 1
    try:
        carte = boite.retirer(racine, etat, args.lot)
    except boite.BoiteErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    verrou.lever(racine, args.lot)
    raison = carte.note or "aucune"
    print(
        f"{args.lot} retiré de {etat}/ (raison : {raison}) ; verrou levé "
        "— le pilote peut le redéposer."
    )
    return 0


def _poser_verrous(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)


def _verrous(args: argparse.Namespace) -> int:
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


def _poser_verrouiller(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(boite.ROLES))
    p.add_argument("--lot", required=True)


def _verrouiller(args: argparse.Namespace) -> int:
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


def _poser_lever(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--lot", required=True)


def _lever(args: argparse.Namespace) -> int:
    verrou.lever(Path(args.projet), args.lot)
    print(f"verrou levé : {args.lot}")
    return 0


def commandes() -> list[Commande]:
    return [
        Commande("prochain", "prochaine carte d'un rôle ; RIEN et code 0 si la boîte est vide",
                 _poser_prochain, _prochain),
        Commande("deposer", "poser une carte dans une boîte",
                 _poser_deposer, _deposer),
        Commande("avancer", "passer une carte au rôle suivant",
                 _poser_avancer, _avancer),
        Commande("echouer", "ranger une carte dans echec/",
                 _poser_echouer, _echouer),
        Commande("rappeler", "remettre en circulation les cartes de echec/ qui se retentent",
                 _poser_rappeler, _rappeler),
        Commande("reprendre", "retirer la carte d'un lot de sa boîte pour que le pilote le redépose",
                 _poser_reprendre, _reprendre),
        Commande("verrous", "fichiers actuellement tenus",
                 _poser_verrous, _verrous),
        Commande("verrouiller", "tenir les fichiers d'une carte avant d'écrire",
                 _poser_verrouiller, _verrouiller),
        Commande("lever", "rendre les fichiers d'un lot",
                 _poser_lever, _lever),
    ]
