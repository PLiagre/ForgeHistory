"""Qui tient quel rôle, avec quoi, et est-ce qu'on peut armer.

Le poste d'un rôle d'après le branchement, l'`argv` qu'on lui
construirait, la branche de son lot, et l'état des lieux avant de poser
`ATELIER_INVOQUER=1`.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import shlex
import shutil
import sys

from .. import backends, boite, feuille, projet, worktree
from . import Commande, feuille_relative, roles_du_produit


def _poser_poste(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--role", required=True, choices=list(backends.ROLES_INVOCABLES))
    p.add_argument(
        "--champ",
        choices=["backend", "binaire", "abo", "modele", "lecture_seule"],
        help="n'imprimer qu'un champ (le shell n'a pas à tenir de table)",
    )


def _poste(args: argparse.Namespace) -> int:
    try:
        poste = backends.poste_du_role(args.role, roles_du_produit(args.projet))
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


def _poser_invocation(p: argparse.ArgumentParser) -> None:
    p.add_argument("--role", required=True, choices=list(backends.ROLES_INVOCABLES))
    p.add_argument("--projet", required=True)
    p.add_argument("--lot")
    p.add_argument("--brief")
    p.add_argument("--pr", type=int, help="la PR à relire, si on la connaît")
    p.add_argument(
        "--decision",
        help="pour le pilote : la sortie de `atelier piloter`, transmise telle quelle",
    )
    p.add_argument(
        "--nul",
        action="store_true",
        help="séparer les arguments par NUL, pour un tableau shell",
    )


def _invocation(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
        branche = produit.branche_du_lot(args.lot) if args.lot else None
        argv = backends.argv_du_role(
            args.role,
            roles=produit.roles.vers_dict(),
            projet=args.projet,
            lot=args.lot,
            brief=args.brief,
            pr=args.pr,
            branche=branche,
            feuille=feuille_relative(produit),
            decision=args.decision,
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


def _poser_pret(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)


def _pret(args: argparse.Namespace) -> int:
    """Ce qu'on regarde avant de poser ATELIER_INVOQUER=1.

    On lit le PATH et le disque. On n'invoque personne : regarder n'est
    pas dépenser. Un inconnu se marque `?`, jamais `FAIL` — l'atelier ne
    compte pas ce qu'il n'a pas mesuré.
    """
    lignes: list[tuple[str, str]] = []

    def dire(marque: str, texte: str) -> None:
        lignes.append((marque, texte))

    try:
        produit = projet.charger(args.projet)
    except projet.ProjetIncomplet as exc:
        print(f"FAIL  branchement — {exc}")
        return 1
    dire("PASS", f"branchement — {produit.nom} ({produit.racine}/atelier.toml)")

    roles = produit.roles.vers_dict()
    tenus: dict[str, list[str]] = {}
    for role in backends.ROLES_INVOCABLES:
        try:
            poste = backends.poste_du_role(role, roles)
        except backends.BackendErreur as exc:
            dire("FAIL", f"rôle {role} — {exc}")
            continue
        tenus.setdefault(poste.binaire, []).append(role)
    for binaire, roles_tenus in sorted(tenus.items()):
        porte = ", ".join(roles_tenus)
        chemin = shutil.which(binaire)
        if chemin:
            dire("PASS", f"binaire {binaire} ({porte}) — {chemin}")
        else:
            dire("FAIL", f"binaire {binaire} ({porte}) — introuvable dans le PATH")

    try:
        garde = backends.poste_du_role("relire", roles).lecture_seule
    except backends.BackendErreur:
        garde = "non-tenue"
    if garde == "tenue":
        dire("PASS", "le relecteur n'a pas la main qui écrit")
    else:
        dire("?", "le relecteur garde la main qui écrit — garde non-tenue")

    for outil in ("flock", "timeout"):
        if shutil.which(outil):
            dire("PASS", f"{outil} — présent")
        else:
            dire("FAIL", f"{outil} — absent : le cron tournerait sans garde")

    # Regarder n'est pas créer : si le dossier n'existe pas encore, on
    # demande à son plus proche parent s'il accepterait qu'on l'y pose.
    verrous = Path(os.environ.get("ATELIER_VERROUS") or os.environ.get("TMPDIR") or "/tmp")
    ancetre = verrous
    while not ancetre.exists() and ancetre != ancetre.parent:
        ancetre = ancetre.parent
    if os.access(ancetre, os.W_OK):
        suffixe = "" if verrous.is_dir() else " (sera créé au premier tour)"
        dire("PASS", f"dossier des verrous — {verrous}{suffixe}")
    else:
        dire("FAIL", f"dossier des verrous — {verrous} : {ancetre} n'est pas inscriptible")

    if os.environ.get("ATELIER_QUOTA_CMD") or shutil.which("llmquota"):
        dire("PASS", "quota — lisible")
    else:
        dire("?", "quota — non lisible ; un inconnu ne se compte pas pour zéro")

    for role in boite.ROLES:
        nom = f"ATELIER_WORKDIR_{role}"
        chemin = os.environ.get(nom)
        if not chemin:
            continue
        if Path(chemin).is_dir():
            dire("PASS", f"{nom} — {chemin}")
        else:
            dire("FAIL", f"{nom} — {chemin} n'existe pas")

    boites = (
        "a-briefer", boite.SUIVANT["briefer"], "a-planifier", "a-coder", "a-relire",
        "faite", feuille.BOITE_FUSIONNEE, "echec",
    )
    for etat in boites:
        try:
            combien = len(boite.lister(produit.racine, etat))
        except boite.BoiteErreur as exc:
            dire("FAIL", f"boîte {etat} — {exc}")
            continue
        dire("PASS", f"boîte {etat} — {combien} carte(s)")

    # Le pilote décide d'après la feuille de route : sans elle, il n'a
    # rien à décider, et on le dit avant d'armer.
    if produit.feuille is None:
        dire("?", "feuille de route — [projet].feuille non nommé : le pilote ne déposera rien")
    else:
        try:
            f = feuille.lire(produit.feuille)
            erreurs = feuille.verifier(f, produit.racine, produit.briefs)
            erreurs += feuille.verifier_cartes(f, produit.racine)
        except feuille.FeuilleErreur as exc:
            dire("FAIL", f"feuille de route — {exc}")
        else:
            if erreurs:
                dire("FAIL", f"feuille de route — {len(erreurs)} incohérence(s) : `atelier feuille valider`")
            else:
                dire("PASS", f"feuille de route — {feuille_relative(produit)}, {len(f.fiches)} lot(s), cohérente")

    if os.environ.get("ATELIER_INVOQUER") == "1":
        dire("PASS", "ATELIER_INVOQUER=1 — l'invocation est armée")
    else:
        dire("?", "ATELIER_INVOQUER n'est pas posé — mode à sec")

    for marque, texte in lignes:
        print(f"{marque:<5} {texte}")
    return 1 if any(m == "FAIL" for m, _ in lignes) else 0


def _poser_branche(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--lot", required=True)
    p.add_argument(
        "--worktree",
        help="worktree du rôle ; obligatoire avec --run (jamais le clone du produit)",
    )
    p.add_argument(
        "--run",
        action="store_true",
        help="créer ou extraire la branche dans le worktree ; sans --run, on imprime",
    )


def _branche(args: argparse.Namespace) -> int:
    """Sans --run : imprime le nom. Avec --run : l'extrait dans le worktree du rôle."""
    try:
        produit = projet.charger(args.projet)
        nom = produit.branche_du_lot(args.lot)
    except projet.ProjetIncomplet as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if not args.run:
        print(nom)
        return 0
    if not args.worktree:
        print(
            "FAIL  --run exige --worktree : l'atelier ne bascule pas la branche "
            "du clone du produit",
            file=sys.stderr,
        )
        return 1
    cible = Path(args.worktree).resolve()
    if cible == produit.racine.resolve():
        print(
            "FAIL  le worktree du rôle ne peut pas être le clone du produit "
            f"({cible}) : pose ATELIER_WORKDIR_coder",
            file=sys.stderr,
        )
        return 1
    try:
        worktree.preparer_lot(cible, nom, produit.branche_base)
    except worktree.WorktreeErreur as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    print(nom)
    return 0


def commandes() -> list[Commande]:
    return [
        Commande("poste", "qui tient un rôle, d'après le branchement du produit",
                 _poser_poste, _poste),
        Commande("invocation", "l'argv d'un rôle — imprimé, jamais lancé",
                 _poser_invocation, _invocation),
        Commande("pret", "tout est-il en place pour poser ATELIER_INVOQUER=1 ?",
                 _poser_pret, _pret),
        Commande("branche", "la branche du lot, dérivée de prefixe_branche ; --run l'extrait",
                 _poser_branche, _branche),
    ]
