"""Worktree : un agent, un répertoire, une branche."""

from __future__ import annotations

from pathlib import Path
import subprocess


class WorktreeErreur(RuntimeError):
    pass


def _git(racine: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=racine,
        text=True,
        capture_output=True,
        check=False,
    )


def apercu(racine: Path, branche: str, destination: Path) -> str:
    return (
        f"git -C {racine} worktree add {destination} -b {branche} origin/HEAD"
    )


def creer(racine: Path, branche: str, destination: Path, *, base: str = "HEAD") -> Path:
    destination = Path(destination)
    if destination.exists():
        raise WorktreeErreur(f"destination déjà là : {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    resultat = _git(racine, "worktree", "add", str(destination), "-b", branche, base)
    if resultat.returncode != 0:
        raise WorktreeErreur(resultat.stderr.strip() or resultat.stdout.strip())
    return destination


def retirer(racine: Path, destination: Path) -> None:
    resultat = _git(racine, "worktree", "remove", "--force", str(destination))
    if resultat.returncode != 0:
        raise WorktreeErreur(resultat.stderr.strip() or resultat.stdout.strip())
