"""Les trois décisions, en ligne de commande.

    python3 -m outils relecture   --depot O/R --pr N
    python3 -m outils integration --depot O/R --projet .
    python3 -m outils palier      --projet . [--ecrire]

Chacune imprime **une** ligne sur la sortie standard — celle que le
workflow lit — et son compte rendu sur l'erreur standard. Aucune n'écrit
sur GitHub : `relecture` et `integration` disent ce qui est, le workflow
fait le geste. `palier --ecrire` est la seule qui touche un fichier, et
seulement celui du registre.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

from . import github, integration, palier, registre, relecture


def _relecture(args: argparse.Namespace) -> int:
    gh = github.Github(args.depot, args.jeton)
    pr = gh.get(f"pulls/{args.pr}")
    revision = args.revision or pr["head"]["sha"]
    verdict = relecture.juger(
        revision,
        github.auteurs_du_code(gh, args.pr),
        relecture.revues_depuis_github(github.revues(gh, args.pr)),
    )
    print(f"{'PASS' if verdict.passe else 'FAIL'}  PR {args.pr} — {verdict.raison}")
    return 0 if verdict.passe else 1


def _pr_integrable(gh: github.Github, brut: dict, base: str, prefixes) -> integration.PR:
    """Une PR, avec ce qu'il faut pour décider — et pas un appel de plus.

    Un brouillon ou une branche hors périmètre est écarté avant d'aller
    chercher ses contrôles : c'est le cas ordinaire du dépôt, et il ne
    coûte rien.
    """
    minimale = integration.depuis_github(brut)
    if minimale.brouillon or not any(minimale.branche.startswith(p) for p in prefixes):
        return minimale
    detail = gh.get(f"pulls/{brut['number']}")
    sha = detail["head"]["sha"]
    return integration.depuis_github(
        brut, detail, github.controles(gh, sha), github.retard(gh, base, sha)
    )


def _integration(args: argparse.Namespace) -> int:
    racine = Path(args.projet)
    reglage = registre.integration(racine)
    base = args.base or registre.branchement(racine)["base"]
    gh = github.Github(args.depot, args.jeton)
    prs = [
        _pr_integrable(gh, brut, base, reglage["branches"])
        for brut in gh.liste("pulls", state="open", base=base)
    ]
    rapport = integration.decider(
        prs, reglage["controles"], reglage["branches"], reglage["apres_rejeu"]
    )
    for ligne in rapport.lignes:
        print(ligne, file=sys.stderr)
    decision = rapport.decision
    if decision.action == integration.RIEN or decision.pr is None:
        print("RIEN")
        print(decision.raison, file=sys.stderr)
        return 0
    print(f"{decision.action} {decision.pr}")
    print(f"→ {decision.action} PR {decision.pr} : {decision.raison}", file=sys.stderr)
    return 0


def _palier(args: argparse.Namespace) -> int:
    racine = Path(args.projet)
    branchement = registre.branchement(racine)
    feuille = registre.feuille(racine)
    for etape in palier.etapes(feuille.fiches):
        etat = "finie" if etape.finie else f"en cours : {', '.join(etape.en_cours)}"
        couverture = (
            f"couverts : {', '.join(etape.couverts) or palier.VIDE}"
            f" · à couvrir : {', '.join(etape.a_couvrir) or palier.VIDE}"
        )
        print(f"couche {etape.couche}  {etat}  ({couverture})", file=sys.stderr)

    etape = palier.due(feuille.fiches)
    if etape is None:
        print("RIEN")
        print("aucune couche finie n'attend son palier", file=sys.stderr)
        return 0

    numero = palier.numero_libre(feuille.fiches)
    souche = palier.slug(etape, numero)
    texte_fiche = palier.fiche(etape, numero, branchement["briefs"])
    print(f"palier {numero} {souche} couche={etape.couche}")
    print(
        f"→ couche {etape.couche} finie ; lots à couvrir : {', '.join(etape.a_couvrir)}",
        file=sys.stderr,
    )
    if not args.ecrire:
        print("sans --ecrire : le registre n'est pas touché.", file=sys.stderr)
        return 0
    chemin = feuille.chemin
    module = registre.atelier()
    chemin.write_text(
        palier.inserer(chemin.read_text(encoding="utf-8"), texte_fiche, module.REPERE_DEBUT),
        encoding="utf-8",
    )
    print(f"fiche {numero} écrite en tête de {chemin}", file=sys.stderr)
    return 0


def construire() -> argparse.ArgumentParser:
    parseur = argparse.ArgumentParser(prog="outils", description=__doc__)
    sous = parseur.add_subparsers(dest="commande", required=True)

    p = sous.add_parser("relecture", help="la PR a-t-elle été relue par un tiers, sur sa révision ?")
    p.add_argument("--depot", required=True, help="proprietaire/nom")
    p.add_argument("--pr", type=int, required=True)
    p.add_argument("--revision", help="la révision jugée ; par défaut la tête de la PR")
    p.add_argument("--jeton")
    p.set_defaults(faire=_relecture)

    p = sous.add_parser("integration", help="quelle PR entre dans master, ou RIEN")
    p.add_argument("--depot", required=True, help="proprietaire/nom")
    p.add_argument("--projet", default=".")
    p.add_argument("--base", help="la branche d'arrivée ; par défaut celle du branchement")
    p.add_argument("--jeton")
    p.set_defaults(faire=_integration)

    p = sous.add_parser("palier", help="une couche finie attend-elle son lot de stabilisation ?")
    p.add_argument("--projet", default=".")
    p.add_argument("--ecrire", action="store_true", help="poser la fiche dans le registre")
    p.set_defaults(faire=_palier)
    return parseur


def main(argv=None) -> int:
    args = construire().parse_args(argv)
    try:
        return args.faire(args)
    except (github.GithubErreur, registre.AtelierAbsent, registre.BranchementIncomplet) as exc:
        print(f"FAIL  {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
