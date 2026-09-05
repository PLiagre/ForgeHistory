#!/usr/bin/env py
"""
Bloque `git push` si les tests sont rouges (hook PreToolUse, Bash).

Rien de rouge ne remonte sur une branche. La CI le revérifie, mais après
coup ; ici c'est avant, et ça épargne un tour d'intégration. Ce qui suit —
la relecture, la fusion — ne passe plus par personne : `AGENTS.md`
§ « Le workflow ».

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
    candidates = (
        repo_root / "sim" / "tests",
        repo_root / "viewer" / "tests",
        repo_root / "outils" / "tests",
    )
    suites = [d for d in candidates if d.is_dir()]
    if not suites:
        return 0  # rien à garder

    result = subprocess.run(
        [sys.executable, "-m", "pytest", *(str(d) for d in suites), "-q"],
        capture_output=True, text=True, cwd=repo_root,
    )

    if result.returncode != 0:
        print(
            "Bloqué : `git push` alors que les tests sont rouges.\n"
            "Joué : py -m pytest " + " ".join(d.relative_to(repo_root).as_posix() + "/" for d in suites)
            + f" -q (sortie {result.returncode})\n"
            "Réparer d'abord, ou lancer la commande soi-même hors de ce hook "
            "pour pousser un état sciemment cassé.\n\n"
            f"{result.stdout[-2000:]}\n{result.stderr[-2000:]}",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
