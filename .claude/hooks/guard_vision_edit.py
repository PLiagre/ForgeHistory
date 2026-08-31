#!/usr/bin/env py
"""
Bloque les modifications de VISION.md (hook PreToolUse, Edit/Write).

VISION.md est gelé : c'est la source de vérité de la vision produit, et elle
ne se répare pas en passant. Une garde placée après l'effet qu'elle doit
empêcher ne protège rien — celle-ci existe avant, pas après.

Contournement explicite : poser FORGE_ALLOW_VISION_EDIT=1 dans
l'environnement de la commande qui doit vraiment la changer. C'est une
action délibérée et visible, pas un passage en silence.
"""
import json
import os
import sys


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    tool_input = payload.get("tool_input") or {}
    file_path = str(tool_input.get("file_path") or "")

    if not file_path.replace("\\", "/").endswith("VISION.md"):
        return 0

    if os.environ.get("FORGE_ALLOW_VISION_EDIT") == "1":
        return 0

    print(
        "Bloqué : VISION.md ne se modifie pas sans contournement explicite.\n"
        "C'est la source de vérité de la vision produit : elle prime sur tout "
        "autre document, et un changement discret s'y propage partout.\n"
        "Pour la changer volontairement, poser FORGE_ALLOW_VISION_EDIT=1 sur "
        "la commande, et dire pourquoi dans le message de commit.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
