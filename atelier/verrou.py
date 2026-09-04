"""Une ressource n'est pas dans deux lots actifs.

Une ressource, c'est un **fichier** — ou une **fiche** dans un fichier :

    sim/aggregation.py          un fichier
    ROADMAP.md#047-le-bourg     la fiche du lot 047 dans le registre

La distinction n'est pas un raffinement. La fiche d'un lot fait partie du
périmètre implicite de sa PR : c'est par elle que le registre passe à
`livre` au moment exact de l'intégration. Tout brief la nomme donc, et
tout brief nomme le même fichier. Tant que la fiche était « le fichier de
la feuille », **aucun lot n'était jamais disjoint d'aucun autre** — pas
par prudence, par une confusion de granularité. Rien ne le signalait : la
file avançait simplement une carte à la fois, pour une raison que
personne n'avait décidée.

Le format du registre avait déjà tout prévu. Une fiche tient sur deux
lignes, séparée de la suivante par une ligne vide, « et c'est cette ligne
vide qui permet à deux PR de lots voisins de fusionner sans conflit ».
Git savait déjà. C'est le verrou qui ne savait pas les distinguer.

Deux ressources entrent en collision quand elles portent sur le même
fichier **et** que l'une au moins couvre l'autre : le fichier entier
couvre chacune de ses fiches, une fiche ne couvre qu'elle-même.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import json

COUCHE = "coordination"

# Ce qui sépare un fichier de la fiche qu'on y tient. Il s'écrit ici et
# se lit ailleurs : un document ou un test qui le recopie fige une
# constante qui sera renommée un jour.
SEPARATEUR = "#"


class Collision(ValueError):
    pass


@dataclass(frozen=True)
class Ressource:
    """Un fichier, ou une fiche dans ce fichier. Jamais autre chose."""

    fichier: str
    # `None` = tout le fichier. Un lot qui le tient tient aussi chacune
    # de ses fiches — c'est ce qui rend un lot d'exploitation seul.
    fiche: str | None = None

    @classmethod
    def depuis(cls, brut: str) -> "Ressource":
        texte = str(brut).strip()
        if not texte:
            raise Collision("une ressource vide n'en est pas une")
        fichier, _, fiche = texte.partition(SEPARATEUR)
        fichier = Path(fichier).as_posix()
        if not fichier:
            raise Collision(f"ressource sans fichier : « {texte} »")
        if SEPARATEUR in texte and not fiche.strip():
            raise Collision(
                f"ressource « {texte} » : le {SEPARATEUR} annonce une fiche, "
                "et il n'y en a pas"
            )
        return cls(fichier=fichier, fiche=fiche.strip() or None)

    def __str__(self) -> str:
        return self.fichier if self.fiche is None else f"{self.fichier}{SEPARATEUR}{self.fiche}"

    @property
    def entiere(self) -> bool:
        """Tient-elle tout le fichier ?"""
        return self.fiche is None

    def couvre(self, autre: "Ressource") -> bool:
        if self.fichier != autre.fichier:
            return False
        return self.entiere or self.fiche == autre.fiche

    def heurte(self, autre: "Ressource") -> bool:
        return self.couvre(autre) or autre.couvre(self)


def ressources(bruts) -> tuple[Ressource, ...]:
    """Les ressources que ces chaînes désignent, sans doublon, dans l'ordre."""
    vues: list[Ressource] = []
    for brut in bruts:
        ressource = Ressource.depuis(brut)
        if ressource not in vues:
            vues.append(ressource)
    return tuple(vues)


@dataclass
class Verrou:
    lot: str
    # Les ressources, sous leur forme écrite. Le type ne change pas :
    # ce qui change, c'est ce qu'une de ces chaînes peut désigner.
    fichiers: frozenset[str]

    @property
    def ressources(self) -> tuple[Ressource, ...]:
        return ressources(sorted(self.fichiers))


@dataclass
class Tableau:
    racine: Path
    poses: list[Verrou] = field(default_factory=list)

    @property
    def fichier(self) -> Path:
        return self.racine / ".atelier" / "verrous.json"


def charger(racine: Path) -> Tableau:
    tableau = Tableau(racine=Path(racine))
    if not tableau.fichier.is_file():
        return tableau
    brut = json.loads(tableau.fichier.read_text(encoding="utf-8"))
    if not brut:
        raise Collision("verrous.json présent mais vide — un échantillon vide échoue")
    for entree in brut:
        tableau.poses.append(
            Verrou(lot=entree["lot"], fichiers=frozenset(entree["fichiers"]))
        )
    return tableau


def _sauver(tableau: Tableau) -> None:
    tableau.fichier.parent.mkdir(parents=True, exist_ok=True)
    payload = [{"lot": v.lot, "fichiers": sorted(v.fichiers)} for v in tableau.poses]
    tableau.fichier.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n")


def qui_tient(racine: Path, demandees, *, sauf: str = "") -> list[tuple[str, str]]:
    """Ce qui bloque : (ressource demandée, lot qui la tient).

    Vide = tout est libre. `sauf` est le lot qui demande : il ne se
    bloque pas lui-même.
    """
    voulues = ressources(demandees)
    pris: list[tuple[str, str]] = []
    for autre in charger(racine).poses:
        if autre.lot == sauf:
            continue
        for tenue in autre.ressources:
            for voulue in voulues:
                if voulue.heurte(tenue):
                    pris.append((str(voulue), autre.lot))
    return pris


def poser(racine: Path, lot: str, fichiers: list[str]) -> Verrou:
    if not fichiers:
        raise Collision("un verrou sans fichier n'en est pas un")
    voulues = ressources(fichiers)
    pris = qui_tient(racine, fichiers, sauf=lot)
    if pris:
        raise Collision(
            f"lot {lot} entre en collision avec "
            + ", ".join(f"{autre} ({ressource})" for ressource, autre in sorted(pris))
        )
    tableau = charger(racine)
    verrou = Verrou(lot=lot, fichiers=frozenset(str(r) for r in voulues))
    tableau.poses = [v for v in tableau.poses if v.lot != lot]
    tableau.poses.append(verrou)
    _sauver(tableau)
    return verrou


def lever(racine: Path, lot: str) -> None:
    tableau = charger(racine)
    tableau.poses = [v for v in tableau.poses if v.lot != lot]
    if tableau.poses:
        _sauver(tableau)
    elif tableau.fichier.is_file():
        tableau.fichier.unlink()
