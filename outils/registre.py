"""Le registre des lots, et le branchement de l'intégration.

Le registre a **un** lecteur : celui de ForgeAtelier. Ce fichier ne le
réécrit pas, il l'appelle — deux analyseurs du même format finiraient
par ne pas dire la même chose du même fichier, et c'est le genre de
désaccord qu'on découvre le jour où il coûte cher.

S'il est hors de portée, on le dit et on s'arrête. On ne devine pas.
"""

from __future__ import annotations

from pathlib import Path
import tomllib

# Ce que `[integration]` doit nommer. Rien n'a de valeur par défaut :
# une liste de contrôles devinée serait une porte qu'on ne se souvient
# pas d'avoir ouverte.
CLES = ("controles", "branches")


class AtelierAbsent(RuntimeError):
    pass


class BranchementIncomplet(ValueError):
    pass


def atelier():
    """Le module `feuille` de l'atelier, ou un refus qui dit quoi faire."""
    try:
        from atelier import feuille
    except ModuleNotFoundError as exc:
        raise AtelierAbsent(
            "ForgeAtelier n'est pas sur le PYTHONPATH : le registre des lots ne se "
            "lit que par lui. Voir docs/WORKFLOW.md § « L'atelier »."
        ) from exc
    return feuille


def feuille(racine: Path):
    """Le registre du produit, tel que l'atelier le lit."""
    projet = branchement(racine)
    if not projet["feuille"]:
        raise BranchementIncomplet(
            "atelier.toml ne nomme pas [projet].feuille : le registre des lots "
            "ne se cherche pas au hasard"
        )
    return atelier().lire(Path(racine) / projet["feuille"])


def _brut(racine: Path) -> dict:
    fichier = Path(racine) / "atelier.toml"
    if not fichier.is_file():
        raise BranchementIncomplet(f"atelier.toml introuvable : {fichier}")
    with fichier.open("rb") as fh:
        return tomllib.load(fh)


def branchement(racine: Path) -> dict:
    """`[projet]` du branchement : où vit le registre, où arrivent les PR."""
    projet = _brut(racine).get("projet", {})
    return {
        "feuille": projet.get("feuille"),
        "base": projet.get("branche_base", "master"),
        "briefs": projet.get("briefs", "briefs"),
    }


def integration(racine: Path) -> dict:
    """`[integration]` : ce qui gouverne l'entrée dans la branche de base.

    C'est un réglage du **produit**, pas du workflow : la liste des
    contrôles requis se lit dans le dépôt, en clair, dans le même fichier
    que le reste du branchement — pas dans une case d'options que personne
    ne relit jamais.
    """
    bloc = _brut(racine).get("integration")
    if bloc is None:
        raise BranchementIncomplet(
            "atelier.toml ne porte pas de section [integration] : "
            "l'intégration ne devine ni les contrôles requis ni les branches "
            f"qu'elle fusionne (attendu : {', '.join(CLES)})"
        )
    manquants = [cle for cle in CLES if not bloc.get(cle)]
    if manquants:
        raise BranchementIncomplet(
            f"[integration] incomplet, champs vides : {', '.join(manquants)}"
        )
    return {
        "controles": tuple(str(c) for c in bloc["controles"]),
        "branches": tuple(str(b) for b in bloc["branches"]),
    }
