#!/usr/bin/env py
"""
Bloque `git push` si les tests du jeu sont rouges (hook PreToolUse, Bash).

Le workflow V1 tient sur une seule garde mécanique : rien de rouge ne
remonte sur une branche. La CI le revérifie, mais après coup ; ici c'est
avant. Le propriétaire lit ensuite le diff et fusionne — ça, aucune machine
ne le fait à sa place.

Sortie 2 (bloqué) si la suite échoue, 0 sinon.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

GIT_PUSH = re.compile(r'\bgit\b(?:(?!&&|;).)*\bpush\b')


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    command = (payload.get("tool_input") or {}).get("command", "")
    if isinstance(command, list):
        command = " ".join(str(c) for c in command)

    if not GIT_PUSH.search(command):
        return 0

    repo_root = Path(__file__).resolve().parent.parent.parent
    suites = [d for d in (repo_root / "sim" / "tests", repo_root / "viewer" / "tests") if d.is_dir()]
    if not suites:
        return 0  # rien à garder

    result = subprocess.run(
        [sys.executable, "-m", "pytest", *(str(d) for d in suites), "-q"],
        capture_output=True, text=True, cwd=repo_root,
    )

    if result.returncode != 0:
        print(
            "Bloqué : `git push` alors que les tests sont rouges.\n"
            f"Joué : py -m pytest sim/tests/ viewer/tests/ -q (sortie {result.returncode})\n"
            "Réparer d'abord, ou lancer la commande soi-même hors de ce hook "
            "pour pousser un état sciemment cassé.\n\n"
            f"{result.stdout[-2000:]}\n{result.stderr[-2000:]}",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
