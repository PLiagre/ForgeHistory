"""Canal d'échange : git-invisible, lisible par l'agent.

Les deux conditions, ou aucune. Un dossier dans `.gitignore` du dépôt
et filtré par l'agent (`.cursorignore`) a déjà rendu un bundle de
revue illisible (lot 033 de ForgeHistory).
"""

from __future__ import annotations

import hashlib
from pathlib import Path


NOM_DOSSIER = "atelier-echange"


class EchangeErreur(ValueError):
    pass


def dossier(racine: Path) -> Path:
    return Path(racine) / NOM_DOSSIER


def ouvrir(racine: Path) -> Path:
    cible = dossier(racine)
    cible.mkdir(parents=True, exist_ok=True)
    garde = cible / ".gitignore"
    if not garde.is_file():
        # Un `.gitignore` contenant `*` s'ignore lui-même : le canal
        # ne dépend pas d'une ligne du `.gitignore` du dépôt produit.
        garde.write_text("*\n", encoding="utf-8")
    return cible


def deposer_texte(racine: Path, nom: str, corps: str) -> Path:
    if not corps.strip():
        raise EchangeErreur(f"{nom} est vide")
    cible = ouvrir(racine) / nom
    cible.write_text(corps, encoding="utf-8")
    attendu = hashlib.sha256(corps.encode("utf-8")).hexdigest()
    obtenu = hashlib.sha256(cible.read_text(encoding="utf-8").encode("utf-8")).hexdigest()
    if attendu != obtenu:
        raise EchangeErreur(
            f"copie corrompue pour {nom} : {attendu[:12]} attendu, {obtenu[:12]} relu"
        )
    return cible


def deposer(racine: Path, source: Path, nom: str) -> Path:
    if not source.is_file():
        raise EchangeErreur(f"{nom} introuvable : {source}")
    return deposer_texte(racine, nom, source.read_text(encoding="utf-8"))


def retirer(racine: Path, nom: str) -> None:
    cible = dossier(racine) / nom
    if cible.is_file():
        cible.unlink()


def git_ignore_le_canal(racine: Path) -> bool:
    """Le canal a sa propre garde `*`. Il ne s'appuie pas sur le dépôt."""
    garde = dossier(racine) / ".gitignore"
    return garde.is_file() and "*" in garde.read_text(encoding="utf-8")
