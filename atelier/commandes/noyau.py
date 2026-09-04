"""Ce qui ne dépend ni d'une carte, ni d'une PR, ni d'un poste.

Les couches, le branchement, la porte mécanique, le cycle, les runs, le
refus de fusionner, le tirage de quota, la fumée du produit, le canal
d'échange et le rangement d'un worktree.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .. import couches, cycle, echange, etat, porte, projet, quota, verrou, worktree
from ..etat import FusionInterdite
from . import Commande


def _poser_couches(p: argparse.ArgumentParser) -> None:
    del p


def _couches(args: argparse.Namespace) -> int:
    del args
    occupees = couches.couches_occupees()
    for couche in couches.Couche:
        modules = occupees[couche]
        if not modules:
            print(f"FAIL  {couche.value} — aucun module")
            return 1
        print(f"PASS  {couche.value} — {', '.join(modules)}")
    return 0


def _poser_doctor(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)


def _doctor(args: argparse.Namespace) -> int:
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


def _poser_portes(p: argparse.ArgumentParser) -> None:
    p.add_argument("--brief", required=True)


def _portes(args: argparse.Namespace) -> int:
    print(porte.rendre(Path(args.brief)))
    return 0 if porte.passer(Path(args.brief)) else 1


def _poser_start(p: argparse.ArgumentParser) -> None:
    p.add_argument("brief")
    p.add_argument("--projet", required=True)
    p.add_argument("--run", action="store_true")


def _start(args: argparse.Namespace) -> int:
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


def _poser_status(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)


def _status(args: argparse.Namespace) -> int:
    runs = etat.lister(Path(args.projet))
    if not runs:
        print("aucun run")
        return 0
    for run in runs:
        print(f"{run.id}  {run.lot}  {run.etape.value}  {run.auteur_code}≠{run.relecteur}")
    return 0


def _poser_fusionner(p: argparse.ArgumentParser) -> None:
    del p


def _fusionner(args: argparse.Namespace) -> int:
    del args
    try:
        etat.fusionner(None)  # type: ignore[arg-type]
    except FusionInterdite as exc:
        print(f"REFUS  {exc}", file=sys.stderr)
        return 2
    print("FAIL  fusionner n'a pas refusé", file=sys.stderr)
    return 1


def _poser_hop(p: argparse.ArgumentParser) -> None:
    p.add_argument(
        "mesure",
        nargs="+",
        help="paires agent=restant (restant=-1 si inconnu)",
    )


def _hop(args: argparse.Namespace) -> int:
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


def _poser_fumee(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)


def _fumee(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
    except projet.ProjetIncomplet as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(produit.fumee)
    return 0


def _poser_canal(p: argparse.ArgumentParser) -> None:
    p.add_argument("--worktree", required=True)


def _canal(args: argparse.Namespace) -> int:
    """Le canal d'échange, avec sa garde, dans le worktree du rôle.

    Sans elle, un agent qui enregistre tout ce qui traîne emporte le
    fichier de PR ; le tour le supprime ensuite, et le worktree reste
    sale. Le lot suivant butait alors sur `preparer_lot`. Le tour
    *nominal* empoisonnait le lot d'après : mesuré, puis fermé ici.
    """
    cible = Path(args.worktree)
    if not cible.is_dir():
        print(f"FAIL  worktree introuvable : {cible}", file=sys.stderr)
        return 1
    try:
        dossier = echange.ouvrir(cible)
    except OSError as exc:
        print(f"FAIL  canal impossible dans {cible} : {exc}", file=sys.stderr)
        return 1
    if not echange.git_ignore_le_canal(cible):
        print(f"FAIL  le canal {dossier} n'a pas sa garde git", file=sys.stderr)
        return 1
    print(dossier)
    return 0


def _poser_ranger(p: argparse.ArgumentParser) -> None:
    p.add_argument("--worktree", required=True)
    p.add_argument("--message", default="atelier : ce que le tour précédent a laissé")


def _ranger(args: argparse.Namespace) -> int:
    cible = Path(args.worktree)
    if not cible.is_dir():
        print(f"FAIL  worktree introuvable : {cible}", file=sys.stderr)
        return 1
    try:
        ranges = worktree.ranger(cible, args.message)
    except worktree.WorktreeErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if not ranges:
        print("worktree propre : rien à ranger")
        return 0
    print(f"rangé dans {worktree.courante(cible)} : {', '.join(ranges)}")
    return 0


def commandes() -> list[Commande]:
    return [
        Commande("couches", "afficher les sept couches et qui les occupe",
                 _poser_couches, _couches),
        Commande("doctor", "vérifier le branchement d'un dépôt produit",
                 _poser_doctor, _doctor),
        Commande("portes", "jouer la porte mécanique sur un brief",
                 _poser_portes, _portes),
        Commande("start", "préparer un lot (aperçu, ou --run)",
                 _poser_start, _start),
        Commande("status", "lister les runs durables",
                 _poser_status, _status),
        Commande("fusionner", "refuse toujours — le propriétaire fusionne",
                 _poser_fusionner, _fusionner),
        Commande("hop", "choisir un agent d'après des quotas connus",
                 _poser_hop, _hop),
        Commande("fumee", "la commande de fumée du dépôt produit",
                 _poser_fumee, _fumee),
        Commande("canal", "ouvrir le canal d'échange d'un worktree, avec sa garde git",
                 _poser_canal, _canal),
        Commande("ranger", "enregistrer ce qui traîne dans un worktree, sans rien effacer",
                 _poser_ranger, _ranger),
    ]
