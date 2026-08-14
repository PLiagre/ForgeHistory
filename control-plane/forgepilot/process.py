from __future__ import annotations

from dataclasses import dataclass
import json
import os
from pathlib import Path
import shutil
import subprocess
from typing import Mapping, Sequence


class PilotError(RuntimeError):
    pass


@dataclass(frozen=True)
class CommandResult:
    argv: tuple[str, ...]
    returncode: int
    stdout: str
    stderr: str

    def json(self) -> object:
        try:
            return json.loads(self.stdout)
        except json.JSONDecodeError as exc:
            raise PilotError("La commande a réussi sans produire le JSON attendu.") from exc


def resolve_binary(name: str) -> str:
    resolved = shutil.which(name)
    if resolved is None:
        raise PilotError(f"Binaire introuvable : {name}")
    return resolved


def run_command(
    argv: Sequence[str],
    *,
    cwd: Path,
    timeout_seconds: int,
    env: Mapping[str, str] | None = None,
    stdin: str | None = None,
) -> CommandResult:
    child_env = os.environ.copy()
    if env:
        child_env.update(env)
    try:
        completed = subprocess.run(
            list(argv),
            cwd=cwd,
            env=child_env,
            input=stdin,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
            shell=False,
        )
    except subprocess.TimeoutExpired as exc:
        raise PilotError(f"Délai dépassé après {timeout_seconds} secondes.") from exc

    result = CommandResult(tuple(argv), completed.returncode, completed.stdout, completed.stderr)
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "aucun détail"
        raise PilotError(f"Commande en échec ({result.returncode}) : {detail}")
    return result


def git(repo: Path, *args: str, timeout_seconds: int = 60) -> str:
    result = run_command(
        [resolve_binary("git"), *args],
        cwd=repo,
        timeout_seconds=timeout_seconds,
    )
    return result.stdout.strip()
