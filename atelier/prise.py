"""Prendre une carte et ses fichiers : un seul geste, indivisible.

`crons/tour.sh` faisait deux appels : `atelier prochain` lisait la
première carte admissible, puis `atelier verrouiller` posait le verrou.
Il y avait un intervalle entre les deux, et il était visible dans le
script.

Tant qu'un seul coder tournait à la fois, l'intervalle ne coûtait rien :
le `flock` par rôle garantissait qu'aucun autre tour du même rôle ne s'y
glissait. Le cycle automatique retire cette garantie — plusieurs lots
avancent de front, donc plusieurs tours du même rôle tournent en même
temps. Alors deux tours lisent la même tête de file et la prennent tous
les deux. Ce n'est pas une course rare : la boîte est triée, et deux
tours réveillés à la même minute lisent le même premier fichier. C'est
le cas nominal.

On ne lit pas puis on écrit : **on prend**. Sous une même serrure :

1. lister les cartes de la boîte du rôle ;
2. déplacer la première candidate vers `en-cours/` ;
3. poser le verrou de ses ressources — et si le verrou est refusé, tout
   remettre en place et essayer la suivante ;
4. rendre la carte, ou `RIEN`.

Le verrou **est** le filtre. Un pré-tri qui déciderait avant lui aurait
sa propre idée de ce qui se heurte, et deux idées finissent toujours par
diverger.

La serrure est un **répertoire** : `os.mkdir` est atomique partout,
`open(…, "x")` sur un partage réseau ne l'est pas toujours, et l'atelier
ne dépend pas de `fcntl` — ses contrôles tournent aussi sur une machine
qui ne l'a pas.

`en-cours/` n'est pas une boîte de plus dans le chemin d'une carte :
c'est là qu'elle séjourne pendant qu'un agent travaille dessus. Une
carte qui y dort après la fin d'un tour est un tour qui n'a pas rangé sa
carte — et c'est visible, ce qui est tout l'intérêt.
"""

from __future__ import annotations

from contextlib import contextmanager
import os
from pathlib import Path
import shutil
import sys
import time

from . import boite, cycle, verrou

COUCHE = "coordination"


# Là où séjourne une carte pendant qu'un agent travaille dessus.
EN_COURS = "en-cours"

# La serrure : un répertoire, parce que `os.mkdir` est atomique partout.
NOM_SERRURE = "prise.lock"

# Combien de temps on attend qu'une serrure se libère avant de renoncer.
# Renoncer n'est pas un échec : la carte sera là au prochain réveil.
ATTENTE = "ATELIER_PRISE_ATTENTE"
ATTENTE_DEFAUT = 10.0

# Passé ce délai, une serrure est celle d'un tour qui a été tué. On la
# reprend, et on le dit — une serrure éternelle bloque la file pour
# toujours, et personne ne saurait pourquoi.
PERIME = "ATELIER_PRISE_PERIME"
PERIME_DEFAUT = 900.0

# Deux variables qui n'existent que pour prouver le rouge. Une garde de
# concurrence dont le contrôle passerait aussi sans elle ne prouve rien :
# il faut pouvoir la retirer et voir la collision se produire.
SANS_SERRURE = "ATELIER_PRISE_SANS_SERRURE"

# Un instant d'horloge — pas un délai. Deux tours attendent le même, et
# la course a lieu quel que soit le temps qu'ils ont mis à démarrer.
# Un simple délai mesurait l'écart de démarrage des deux processus : le
# contrôle passait ou non selon la machine, et un contrôle qui dépend de
# la chance ne prouve rien.
RENDEZ_VOUS = "ATELIER_PRISE_RENDEZ_VOUS"


class PriseErreur(ValueError):
    pass


class ServrureTenue(PriseErreur):
    """Un autre tour tient la serrure. On se recouche, on ne force pas."""


def _secondes(variable: str, defaut: float) -> float:
    brut = os.environ.get(variable, "").strip()
    if not brut:
        return defaut
    try:
        return max(0.0, float(brut))
    except ValueError:
        print(
            f"{variable} illisible ({brut!r}) : on garde {defaut}s",
            file=sys.stderr,
        )
        return defaut


def _attendre_le_rendez_vous() -> None:
    """Le rendez-vous des contrôles de concurrence. Hors contrôle, ne fait rien.

    La carte est déjà listée quand on arrive ici : deux tours qui
    attendent le même instant d'horloge se disputent donc la même tête
    de file, quel que soit le temps qu'ils ont mis à démarrer.
    """
    brut = os.environ.get(RENDEZ_VOUS, "").strip()
    if not brut:
        return
    try:
        instant = float(brut)
    except ValueError:
        print(f"{RENDEZ_VOUS} illisible ({brut!r}) : on n'attend rien", file=sys.stderr)
        return
    reste = instant - time.time()
    if reste > 0:
        time.sleep(reste)


def chemin_serrure(racine: Path) -> Path:
    return Path(racine) / ".atelier" / NOM_SERRURE


@contextmanager
def serrure(racine: Path):
    """La serrure, ou l'attente, ou le renoncement. Jamais le passage en force."""
    cible = chemin_serrure(racine)
    if os.environ.get(SANS_SERRURE) == "1":
        # Uniquement pour prouver le rouge. Un tour qui tourne sans
        # serrure prendra la même carte qu'un autre : c'est le défaut
        # qu'on veut voir, pas un mode de fonctionnement.
        print(
            f"{SANS_SERRURE}=1 : la prise n'est plus indivisible — "
            "cette variable n'existe que pour prouver le rouge.",
            file=sys.stderr,
        )
        yield None
        return

    cible.parent.mkdir(parents=True, exist_ok=True)
    limite = time.monotonic() + _secondes(ATTENTE, ATTENTE_DEFAUT)
    perime = _secondes(PERIME, PERIME_DEFAUT)
    while True:
        try:
            os.mkdir(cible)
            break
        except FileExistsError:
            age = time.time() - cible.stat().st_mtime if cible.exists() else 0.0
            if age > perime:
                print(
                    f"serrure abandonnée depuis {int(age)}s : on la reprend "
                    f"({cible})",
                    file=sys.stderr,
                )
                shutil.rmtree(cible, ignore_errors=True)
                continue
            if time.monotonic() >= limite:
                raise ServrureTenue(
                    f"un autre tour tient la prise depuis {int(age)}s ({cible}) : "
                    "on se recouche, la carte sera là au prochain réveil"
                )
            time.sleep(0.05)
    try:
        (cible / "tenue-par").write_text(f"{os.getpid()}\n", encoding="utf-8")
        yield cible
    finally:
        shutil.rmtree(cible, ignore_errors=True)


def ressources_de(racine: Path, carte: boite.Carte) -> list[str]:
    """Ce qu'une carte tient. La carte le porte, ou son brief le dit.

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
    return cycle._fichiers_du_perimetre(chemin)


def _essayer(racine: Path, role: str, carte: boite.Carte) -> boite.Carte | None:
    """Déplace la carte et pose son verrou, ou ne laisse aucune trace."""
    source = boite.BOITE_DU_ROLE[role]
    destination = boite.deposer(racine, EN_COURS, carte, ecraser=False)
    try:
        if role == boite.ROLE_QUI_ECRIT:
            ressources = ressources_de(racine, carte)
            if not ressources:
                raise verrou.Collision(
                    f"la carte {carte.lot} ne nomme aucune ressource et son brief "
                    f"({carte.brief}) n'en donne pas : l'atelier ne devine pas un périmètre"
                )
            verrou.poser(racine, carte.lot, ressources)
    except verrou.Collision as exc:
        # Tout ou rien : l'état après un échec est l'état d'avant.
        destination.unlink(missing_ok=True)
        print(f"{carte.lot} attend : {exc}", file=sys.stderr)
        return None
    boite.retirer(racine, source, carte.lot)
    return carte


def prendre(racine: Path, role: str) -> boite.Carte | None:
    """La première carte que ce rôle peut prendre, prise. Ou None."""
    racine = Path(racine)
    if role not in boite.BOITE_DU_ROLE:
        raise PriseErreur(f"rôle inconnu : {role} (connus : {', '.join(boite.ROLES)})")
    with serrure(racine):
        cartes = boite.lister(racine, boite.BOITE_DU_ROLE[role])
        _attendre_le_rendez_vous()
        for carte in cartes:
            prise = _essayer(racine, role, carte)
            if prise is not None:
                return prise
    return None


def rendre(racine: Path, role: str, lot: str) -> Path:
    """Remet la carte dans la boîte de son rôle, telle qu'elle était.

    `avancer` et `echouer` lisent la boîte du rôle : la carte y retourne
    avant qu'on les appelle. Le séjour dans `en-cours/` n'ajoute donc
    aucun état au chemin d'une carte — il dit seulement qu'un agent
    travaille dessus en ce moment.
    """
    racine = Path(racine)
    if role not in boite.BOITE_DU_ROLE:
        raise PriseErreur(f"rôle inconnu : {role} (connus : {', '.join(boite.ROLES)})")
    with serrure(racine):
        carte = boite.lire(racine, EN_COURS, lot)
        cible = boite.deposer(racine, boite.BOITE_DU_ROLE[role], carte, ecraser=True)
        boite.retirer(racine, EN_COURS, lot)
    return cible


def en_cours(racine: Path) -> list[boite.Carte]:
    """Les cartes qu'un agent travaille en ce moment."""
    return boite.lister(Path(racine), EN_COURS)
