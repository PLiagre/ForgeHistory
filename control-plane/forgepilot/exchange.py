"""Le seul canal par lequel ForgePilot fait passer un fichier à un agent.

Pourquoi un module dédié plutôt qu'une constante enfouie dans `workflow` :
le dossier d'échange doit satisfaire DEUX conditions contradictoires, et
rien ne les tenait ensemble.

1. Invisible à Git — sinon `working_tree_paths()` voit la copie, le contrôle
   de périmètre `files_allowed_to_change` la refuse, et `execute` réclame un
   dépôt propre.
2. Visible à l'agent — sinon le fichier existe, est lisible par le système,
   et reste malgré tout hors de portée de celui à qui on le tend.

`.forgepilot/` tenait la première et a perdu la seconde le jour où il est
entré dans `.cursorignore` (commit b17a468) : le bundle de revue du lot 033
est devenu illisible pour son relecteur sans qu'aucun test ne rougisse.
`tests/test_exchange_channel.py` tient désormais les deux conditions
ensemble ; ce module en est la seule adresse.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from .process import PilotError


EXCHANGE_DIRNAME = ".forge-exchange"


def exchange_dir(root: Path) -> Path:
    """Le dossier d'échange d'un dépôt ou d'un worktree."""
    return Path(root) / EXCHANGE_DIRNAME


def _open_exchange_dir(root: Path) -> Path:
    """Crée le canal et le rend invisible à Git par lui-même.

    Un `.gitignore` contenant `*` DANS le dossier s'ignore lui-même et tout
    ce qu'il contient, dans n'importe quel dépôt. L'ancien canal dépendait au
    contraire d'une ligne du `.gitignore` du dépôt : un worktree, un dépôt de
    test ou un clone sans cette ligne, et la copie devenait un fichier
    inattendu que `enforce_allowed_paths` refusait à la publication.
    """
    dossier = exchange_dir(root)
    dossier.mkdir(parents=True, exist_ok=True)
    garde = dossier / ".gitignore"
    if not garde.is_file():
        garde.write_text("*\n", encoding="utf-8")
    return dossier


def stage_exchange(root: Path, source: Path, nom: str) -> str:
    """Dépose une copie de `source` dans le canal et rend son chemin relatif.

    Le corps est comparé après écriture : une copie tronquée ou concurrente
    est refusée ici, pas découverte par un agent qui lit un JSON incomplet.
    """
    if not source.is_file():
        raise PilotError(f"{nom.capitalize()} introuvable : {source}")
    corps = source.read_text(encoding="utf-8").strip()
    if not corps:
        raise PilotError(f"Le {nom} est vide.")
    dossier = _open_exchange_dir(root)
    cible = dossier / f"{nom}.json"
    cible.write_text(corps, encoding="utf-8")
    attendu = hashlib.sha256(corps.encode("utf-8")).hexdigest()
    obtenu = hashlib.sha256(cible.read_text(encoding="utf-8").encode("utf-8")).hexdigest()
    if attendu != obtenu:
        raise PilotError(
            f"Copie d'échange corrompue pour {nom} : {attendu[:12]} attendu, "
            f"{obtenu[:12]} relu."
        )
    return cible.relative_to(Path(root)).as_posix()
