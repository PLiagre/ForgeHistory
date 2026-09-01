"""Adaptateurs d'intelligence : ils nomment une commande, ils ne raisonnent pas.

v0 n'invoque personne. Un lancement hors run durable a déjà coûté
un lot. Ces objets existent pour que `start` imprime l'invocation
exacte, et pour qu'un lot futur puisse les exécuter sans inventer
un backend dans le cycle.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Backend:
    nom: str
    binaire: str
    role: str


POSTES = {
    "claude": Backend(nom="claude", binaire="claude", role="ecriture"),
    "cursor": Backend(nom="cursor", binaire="agent", role="execution"),
    "codex": Backend(nom="codex", binaire="codex", role="controle"),
    "hermes": Backend(nom="hermes", binaire="hermes", role="console"),
}


def invocation(backend: Backend, prompt: str) -> str:
    """La commande qu'on *imprimerait*. Personne ne la lance ici."""
    # Le prompt n'entre pas dans argv : trop gros, et les traces le
    # masqueraient. On nomme le binaire et le rôle, le corps passe
    # par le canal d'échange.
    return f"{backend.binaire}  # rôle={backend.role}  prompt=<déposé dans atelier-echange/>"


def pour(nom: str) -> Backend:
    if nom not in POSTES:
        connus = ", ".join(sorted(POSTES))
        raise KeyError(f"backend inconnu : {nom} (connus : {connus})")
    return POSTES[nom]
