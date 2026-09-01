"""Boîte aux lettres : chaque rôle prend une carte et s'arrête.

Personne n'attend personne. Si la boîte d'un rôle est vide, le cron
sort 0 avec `RIEN`. Un échec va dans `echec/`, il ne bloque pas
l'autre rôle.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path

from . import verrou


ROLES = ("briefer", "planifier", "coder", "relire")

# Ce que chaque rôle *lit*. Le planificateur est facultatif :
# le coder lit `a-coder`, pas la sortie du planificateur.
BOITE_DU_ROLE = {
    "briefer": "a-briefer",
    "planifier": "a-planifier",
    "coder": "a-coder",
    "relire": "a-relire",
}

SUIVANT = {
    "briefer": "a-coder",       # pas a-planifier : Composer n'attend pas Grok
    "planifier": "a-coder",     # s'il passe, il enrichit la même file
    "coder": "a-relire",
    "relire": "faite",
}

# Ce qu'un rôle a le droit de changer en passant la carte au suivant.
# `lot` et `brief` n'y sont pas : le brief est la seule source
# d'instruction d'un lot, et une carte qui change de brief en fait une
# seconde.
CHAMPS_MODIFIABLES = ("pr", "note", "fichiers")

# Le seul rôle qui écrit du code. Les autres relisent ou rédigent : un
# verrou de fichiers ne les suspend pas.
ROLE_QUI_ECRIT = "coder"


class BoiteErreur(ValueError):
    pass


@dataclass(frozen=True)
class Carte:
    lot: str
    brief: str
    fichiers: list[str]
    pr: int | None = None
    note: str = ""

    def __post_init__(self) -> None:
        if not self.lot.strip():
            raise BoiteErreur("une carte sans lot n'en est pas une")
        if not self.brief.strip():
            # Un échantillon vide qui traverse la boîte ferait dépenser
            # un quota sur une instruction qui n'existe pas : lot 035.
            raise BoiteErreur(
                f"la carte {self.lot} ne nomme aucun brief — "
                "le brief est la seule source d'instruction"
            )

    def vers_dict(self) -> dict:
        return {
            "lot": self.lot,
            "brief": self.brief,
            "fichiers": list(self.fichiers),
            "pr": self.pr,
            "note": self.note,
        }


def racine_boite(projet: Path) -> Path:
    return Path(projet) / ".atelier" / "boite"


def _dossier(projet: Path, nom: str) -> Path:
    """Où regarder. Regarder ne crée rien : sans écriture, rien n'est écrit."""
    return racine_boite(projet) / nom


def _ouvrir(projet: Path, nom: str) -> Path:
    """Où déposer. Seul un dépôt crée le dossier."""
    cible = _dossier(projet, nom)
    cible.mkdir(parents=True, exist_ok=True)
    return cible


def _chemin(projet: Path, etat: str, lot: str) -> Path:
    return _dossier(projet, etat) / f"{lot}.json"


def deposer(projet: Path, etat: str, carte: Carte) -> Path:
    cible = _ouvrir(projet, etat) / f"{carte.lot}.json"
    if cible.exists():
        raise BoiteErreur(f"carte déjà là : {cible}")
    cible.write_text(json.dumps(carte.vers_dict(), indent=2, ensure_ascii=False) + "\n")
    return cible


def _depuis_json(fichier: Path) -> Carte:
    brut = json.loads(fichier.read_text(encoding="utf-8"))
    if not brut:
        raise BoiteErreur(f"carte vide : {fichier}")
    try:
        return Carte(
            lot=brut["lot"],
            brief=brut["brief"],
            fichiers=list(brut.get("fichiers") or []),
            pr=brut.get("pr"),
            note=brut.get("note") or "",
        )
    except KeyError as exc:
        raise BoiteErreur(f"carte incomplète {fichier} : {exc.args[0]}") from exc


def lister(projet: Path, etat: str) -> list[Carte]:
    dossier = _dossier(projet, etat)
    if not dossier.is_dir():
        return []
    return [_depuis_json(f) for f in sorted(dossier.glob("*.json"))]


def lire(projet: Path, etat: str, lot: str) -> Carte:
    source = _chemin(projet, etat, lot)
    if not source.is_file():
        raise BoiteErreur(f"pas de carte {lot} dans {etat}")
    return _depuis_json(source)


def _fichiers_tenus(projet: Path) -> dict[str, str]:
    """Quel lot tient quel fichier, d'après le tableau des verrous."""
    tenus: dict[str, str] = {}
    for pose in verrou.charger(Path(projet)).poses:
        for fichier in pose.fichiers:
            tenus[fichier] = pose.lot
    return tenus


def prochain(projet: Path, role: str) -> Carte | None:
    if role not in BOITE_DU_ROLE:
        raise BoiteErreur(f"rôle inconnu : {role} (connus : {', '.join(ROLES)})")
    cartes = lister(projet, BOITE_DU_ROLE[role])
    if role != ROLE_QUI_ECRIT:
        return cartes[0] if cartes else None
    # « 044 occupe engine.py ? 046 attend dans a-coder. Le cron prend
    # 047 s'il est disjoint, ou sort RIEN. » Un fichier n'est pas dans
    # deux lots actifs.
    tenus = _fichiers_tenus(projet)
    for carte in cartes:
        pris = [
            f for f in carte.fichiers
            if tenus.get(Path(f).as_posix(), carte.lot) != carte.lot
        ]
        if not pris:
            return carte
    return None


def avancer(projet: Path, role: str, lot: str, **champs: object) -> Path:
    inconnus = sorted(k for k in champs if k not in CHAMPS_MODIFIABLES)
    if inconnus:
        raise BoiteErreur(
            f"une carte ne change pas de {', '.join(inconnus)} : "
            "le brief est la seule source d'instruction"
        )
    etat = BOITE_DU_ROLE[role]
    source = _chemin(projet, etat, lot)
    if not source.is_file():
        raise BoiteErreur(f"pas de carte {lot} dans {etat}")
    brut = json.loads(source.read_text(encoding="utf-8"))
    fichiers = champs.get("fichiers")
    if fichiers and brut.get("fichiers"):
        raise BoiteErreur(
            f"le périmètre de la carte {lot} est déjà posé : il ne se réécrit pas"
        )
    brut.update({k: v for k, v in champs.items() if v is not None})
    carte = Carte(
        lot=brut["lot"],
        brief=brut["brief"],
        fichiers=list(brut.get("fichiers") or []),
        pr=brut.get("pr"),
        note=brut.get("note") or "",
    )
    destination = deposer(projet, SUIVANT[role], carte)
    source.unlink()
    return destination


def echouer(projet: Path, role: str, lot: str, raison: str) -> Path:
    etat = BOITE_DU_ROLE[role]
    source = _chemin(projet, etat, lot)
    if not source.is_file():
        raise BoiteErreur(f"pas de carte {lot} dans {etat}")
    brut = json.loads(source.read_text(encoding="utf-8"))
    brut["note"] = raison
    carte = Carte(
        lot=brut["lot"],
        brief=brut["brief"],
        fichiers=list(brut.get("fichiers") or []),
        pr=brut.get("pr"),
        note=raison,
    )
    destination = deposer(projet, "echec", carte)
    source.unlink()
    return destination
