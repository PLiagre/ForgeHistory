"""Boîte aux lettres : chaque rôle prend une carte et s'arrête.

Personne n'attend personne. Si la boîte d'un rôle est vide, le cron
sort 0 avec `RIEN`. Un échec va dans `echec/`, il ne bloque pas
l'autre rôle.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path


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


class BoiteErreur(ValueError):
    pass


@dataclass(frozen=True)
class Carte:
    lot: str
    brief: str
    fichiers: list[str]
    pr: int | None = None
    note: str = ""

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
    cible = racine_boite(projet) / nom
    cible.mkdir(parents=True, exist_ok=True)
    return cible


def _chemin(projet: Path, etat: str, lot: str) -> Path:
    return _dossier(projet, etat) / f"{lot}.json"


def deposer(projet: Path, etat: str, carte: Carte) -> Path:
    if not carte.lot:
        raise BoiteErreur("une carte sans lot n'en est pas une")
    cible = _chemin(projet, etat, carte.lot)
    if cible.exists():
        raise BoiteErreur(f"carte déjà là : {cible}")
    cible.write_text(json.dumps(carte.vers_dict(), indent=2, ensure_ascii=False) + "\n")
    return cible


def lister(projet: Path, etat: str) -> list[Carte]:
    dossier = _dossier(projet, etat)
    cartes: list[Carte] = []
    for fichier in sorted(dossier.glob("*.json")):
        brut = json.loads(fichier.read_text(encoding="utf-8"))
        if not brut:
            raise BoiteErreur(f"carte vide : {fichier}")
        try:
            cartes.append(
                Carte(
                    lot=brut["lot"],
                    brief=brut["brief"],
                    fichiers=list(brut.get("fichiers") or []),
                    pr=brut.get("pr"),
                    note=brut.get("note") or "",
                )
            )
        except KeyError as exc:
            raise BoiteErreur(f"carte incomplète {fichier} : {exc.args[0]}") from exc
    return cartes


def prochain(projet: Path, role: str) -> Carte | None:
    if role not in BOITE_DU_ROLE:
        raise BoiteErreur(f"rôle inconnu : {role} (connus : {', '.join(ROLES)})")
    cartes = lister(projet, BOITE_DU_ROLE[role])
    return cartes[0] if cartes else None


def avancer(projet: Path, role: str, lot: str, **champs: object) -> Path:
    etat = BOITE_DU_ROLE[role]
    source = _chemin(projet, etat, lot)
    if not source.is_file():
        raise BoiteErreur(f"pas de carte {lot} dans {etat}")
    brut = json.loads(source.read_text(encoding="utf-8"))
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
