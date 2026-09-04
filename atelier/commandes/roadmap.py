"""La feuille de route du produit, et la décision qu'on en dérive.

La valider, la lire, y marquer une fiche — et le pilote, qui calcule ce
qu'il y a à déposer. Rien ici n'invoque personne : la feuille dit ce qui
est, `piloter` dit ce qu'un cron devrait déposer.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import subprocess
import sys

from .. import boite, echange, feuille, projet, verrou
from . import Commande, feuille_relative


def _charger_feuille(chemin_projet: str) -> tuple[projet.Projet, feuille.Feuille]:
    produit = projet.charger(chemin_projet)
    return produit, feuille.lire(produit.feuille_ou_refus())


def _feuille_de_base(produit: projet.Projet, base: str) -> feuille.Feuille:
    """La feuille telle que la révision `base` la porte, lue par git."""
    rel = feuille_relative(produit)
    proc = subprocess.run(
        ["git", "-C", str(produit.racine), "show", f"{base}:{rel}"],
        capture_output=True, text=True,
    )
    if proc.returncode != 0:
        raise feuille.FeuilleErreur(
            f"impossible de lire {rel} à la révision {base} : {proc.stderr.strip()}"
        )
    return feuille.lire_texte(proc.stdout, Path(f"{base}:{rel}"))


def _valider(args: argparse.Namespace) -> int:
    try:
        produit, apres = _charger_feuille(args.projet)
        erreurs = feuille.verifier(apres, produit.racine, produit.briefs)
        erreurs += feuille.verifier_cartes(apres, produit.racine)
        note_base = ""
        if args.base:
            try:
                avant = _feuille_de_base(produit, args.base)
            except feuille.FeuilleErreur as exc:
                if feuille.REPERE_DEBUT not in str(exc):
                    raise
                # La révision de base n'a pas encore de registre : c'est la
                # première feuille. On ne vérifie pas des transitions depuis
                # rien, et on le dit plutôt que de faire semblant.
                note_base = f" ; {args.base} sans registre : première feuille, transitions non vérifiées"
            else:
                erreurs += feuille.transitions(
                    avant, apres,
                    prefixe_branche=produit.prefixe_branche,
                    branche=args.branche,
                    pr=args.pr,
                )
    except (projet.ProjetIncomplet, feuille.FeuilleErreur) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if not apres.fiches:
        # Un registre vide n'est pas un registre cohérent : c'est un
        # échantillon vide, et il échoue.
        erreurs.append(f"{apres.chemin} — le registre ne porte aucune fiche")
    for erreur in erreurs:
        print(f"FAIL  {erreur}", file=sys.stderr)
    if erreurs:
        return 1
    print(f"PASS  {feuille_relative(produit)} — {len(apres.fiches)} lot(s), feuille cohérente{note_base}")
    return 0


def _etat(args: argparse.Namespace) -> int:
    try:
        produit, f = _charger_feuille(args.projet)
        lignes = [
            (fiche.lot, fiche.etat, feuille.etat_effectif(fiche, f, produit.racine))
            for fiche in f.fiches
        ]
    except (projet.ProjetIncomplet, feuille.FeuilleErreur, verrou.Collision) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    if not lignes:
        print("FAIL  le registre ne porte aucune fiche", file=sys.stderr)
        return 1
    largeur = max(len(lot) for lot, _, _ in lignes)
    for lot, ecrit, derive in lignes:
        print(f"{lot:<{largeur}}  {ecrit:<10} {derive}")
    return 0


def _marquer(args: argparse.Namespace) -> int:
    try:
        produit = projet.charger(args.projet)
        chemin = produit.feuille_ou_refus()
        texte = chemin.read_text(encoding="utf-8")
        nouveau = feuille.marquer(texte, args.lot, args.etat, tuple(args.pr), chemin)
    except (projet.ProjetIncomplet, feuille.FeuilleErreur, OSError) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    chemin.write_text(nouveau, encoding="utf-8")
    fiche = feuille.lire(chemin).fiche(args.lot)
    assert fiche is not None
    prs = ", ".join(map(str, fiche.prs)) or feuille.VIDE
    print(f"{fiche.lot}  état : {fiche.etat} · PR : {prs}")
    return 0


def _poser_feuille(p: argparse.ArgumentParser) -> None:
    sous = p.add_subparsers(dest="action", required=True)
    valider = sous.add_parser(
        "valider", help="FAIL sur toute incohérence feuille / briefs / cartes ; 0 sinon"
    )
    valider.add_argument("--projet", required=True)
    valider.add_argument(
        "--base", help="révision git de référence : vérifie aussi les transitions"
    )
    valider.add_argument("--branche", help="la branche de la PR, pour reconnaître une PR de lot")
    valider.add_argument("--pr", type=int, help="le numéro de la PR, s'il est connu")
    etat_p = sous.add_parser("etat", help="chaque lot, son état écrit et son état dérivé")
    etat_p.add_argument("--projet", required=True)
    marquer = sous.add_parser("marquer", help="faire avancer la fiche d'un lot")
    marquer.add_argument("--projet", required=True)
    marquer.add_argument("--lot", required=True, help="le numéro (046) ou le slug du lot")
    marquer.add_argument("--etat", required=True, choices=list(feuille.ETATS))
    marquer.add_argument("--pr", type=int, action="append", default=[])


# Le sous-parseur exige déjà une action : cette table n'a pas de défaut,
# et n'aurait rien à en faire.
_ACTIONS = {"valider": _valider, "etat": _etat, "marquer": _marquer}


def _feuille(args: argparse.Namespace) -> int:
    return _ACTIONS[args.action](args)


def _sonde_pr_ouverte(produit: projet.Projet):
    """Ce travail existe-t-il déjà ? La question que l'atelier ne posait pas.

    Un lot neuf n'a pas de branche : il ne coûte alors aucun appel, et
    le cas ordinaire reste gratuit.
    """
    def sonde(lot: str) -> tuple[str, int | None]:
        branche = produit.branche_du_lot(lot)
        if not echange.branche_existe(branche, produit.racine):
            return (echange.AUCUNE, None)
        return echange.pr_ouverte_sur(branche, produit.racine)
    return sonde


def _poser_piloter(p: argparse.ArgumentParser) -> None:
    p.add_argument("--projet", required=True)
    p.add_argument("--run", action="store_true")


def _piloter(args: argparse.Namespace) -> int:
    """La décision du matin, calculée. Sans --run, rien n'est déposé."""
    try:
        produit, f = _charger_feuille(args.projet)
        racine = produit.racine
        lignes: list[str] = []
        retenues: list[str] = []
        for r in feuille.rapprochements(
            f, racine, etat_pr=lambda n: echange.etat_pr(n, racine)
        ):
            if args.run:
                feuille.appliquer(racine, r)
            verbe = "rapproché " if args.run else "rapprocher"
            levee = " ; verrou levé" if r.lever_verrou and args.run else ""
            lignes.append(f"{verbe} {r.lot} : {r.source} → {r.destination} ({r.raison}){levee}")
        # Une carte qu'un rapprochement déplace n'est pas une incohérence :
        # `verifier_cartes` ne la compte pas, à sec comme sous --run.
        erreurs = feuille.verifier(f, racine, produit.briefs)
        erreurs += feuille.verifier_cartes(f, racine)
        if erreurs:
            for ligne in lignes:
                print(ligne)
            for erreur in erreurs:
                print(f"FAIL  {erreur}", file=sys.stderr)
            print("FAIL  feuille incohérente : aucune carte déposée", file=sys.stderr)
            return 1
        if not f.fiches:
            print(f"FAIL  {f.chemin} — le registre ne porte aucune fiche", file=sys.stderr)
            return 1
        for d in feuille.decider(
            f, racine, pr_ouverte=_sonde_pr_ouverte(produit), retenues=retenues
        ):
            if args.run:
                feuille.deposer(racine, d)
            verbe = "déposé   " if args.run else "déposer  "
            fichiers = f"  ({', '.join(d.fichiers)})" if d.fichiers else ""
            lignes.append(f"{verbe} {d.boite:<10} {d.lot}  {d.brief}{fichiers}")
    except (projet.ProjetIncomplet, feuille.FeuilleErreur, boite.BoiteErreur, verrou.Collision) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1
    # Une carte retenue n'est pas une carte déposée : elle va sur stderr,
    # et `RIEN` reste ce que le pilote dit quand il n'a rien fait — c'est
    # ce mot que `crons/pilote.sh` lit pour ne pas payer Hermes.
    for retenue in retenues:
        print(f"retenu   {retenue}", file=sys.stderr)
    if not lignes:
        print("RIEN")
        return 0
    for ligne in lignes:
        print(ligne)
    if not args.run:
        print("sans --run : rien n'est déposé.")
    return 0


def commandes() -> list[Commande]:
    return [
        Commande("feuille", "le registre des lots de la feuille de route du produit",
                 _poser_feuille, _feuille),
        Commande("piloter", "ce que la feuille de route commande de déposer ; --run dépose",
                 _poser_piloter, _piloter),
    ]
