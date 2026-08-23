from __future__ import annotations

from fnmatch import fnmatchcase
from pathlib import Path
from typing import Iterable

from .policy import normalize_repo_path
from .process import PilotError, git


def working_tree_paths(repo: Path) -> list[str]:
    commands = (
        ("diff", "--no-renames", "--name-only", "-z"),
        ("diff", "--cached", "--no-renames", "--name-only", "-z"),
        ("ls-files", "--others", "--exclude-standard", "-z"),
    )
    paths: set[str] = set()
    for command in commands:
        raw = git(repo, *command)
        for value in raw.split("\0"):
            if value:
                paths.add(normalize_repo_path(value))
    return sorted(paths)


def changed_paths(repo: Path, base: str) -> list[str]:
    raw = git(repo, "diff", "--no-renames", "--name-only", "-z", f"{base}...HEAD")
    return sorted(normalize_repo_path(value) for value in raw.split("\0") if value)


def staged_paths(repo: Path) -> list[str]:
    """Retourne les deux côtés des renommages présents dans l'index."""

    raw = git(repo, "diff", "--cached", "--no-renames", "--name-only", "-z")
    return sorted(normalize_repo_path(value) for value in raw.split("\0") if value)


def _matches_allowed(path: str, pattern: str) -> bool:
    normalized = pattern.replace("\\", "/")
    while normalized.startswith("./"):
        normalized = normalized[2:]
    if path == normalized:
        return True
    if normalized.endswith("/") and path.startswith(normalized):
        return True
    candidates = {normalized, normalized.replace("/**/", "/")}
    if normalized.startswith("**/"):
        candidates.add(normalized[3:])
    return any(fnmatchcase(path, candidate) for candidate in candidates)


def enforce_allowed_paths(paths: Iterable[str], allowed: Iterable[str]) -> list[str]:
    normalized_paths = sorted({normalize_repo_path(path) for path in paths})
    patterns: list[str] = []
    for pattern in allowed:
        normalized = str(pattern).replace("\\", "/")
        while normalized.startswith("./"):
            normalized = normalized[2:]
        patterns.append(normalized)
    if not patterns:
        raise PilotError("Publication refusée : files_allowed_to_change est vide.")
    unexpected = [
        path for path in normalized_paths if not any(_matches_allowed(path, pattern) for pattern in patterns)
    ]
    if unexpected:
        raise PilotError(
            "Publication refusée : fichier(s) hors files_allowed_to_change : "
            + ", ".join(unexpected)
        )
    return normalized_paths


def stage_explicit_paths(repo: Path, paths: Iterable[str]) -> list[str]:
    selected = sorted({normalize_repo_path(path) for path in paths})
    if not selected:
        raise PilotError("Aucun changement à publier.")
    # Lots bornés pour éviter la limite de taille d'argv sans construire de shell.
    for start in range(0, len(selected), 100):
        git(
            repo,
            "--literal-pathspecs",
            "add",
            "-f",
            "--",
            *selected[start : start + 100],
        )
    # Le commit consommera tout l'index, pas seulement le dernier `git add`.
    # Relire l'index ferme donc aussi les changements pré-indexés et les deux
    # côtés d'un renommage avant que l'appelant ne puisse committer.
    selected_set = set(selected)
    unexpected = [path for path in staged_paths(repo) if path not in selected_set]
    if unexpected:
        raise PilotError(
            "Publication refusée : l'index final contient un chemin non sélectionné : "
            + ", ".join(unexpected)
        )
    return selected
