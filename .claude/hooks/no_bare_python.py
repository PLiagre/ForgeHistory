#!/usr/bin/env py
"""
Bloque les invocations de `python` nu (hook PreToolUse, matcher Bash).

Sur la machine Windows du propriétaire, `python` est un faux alias du
Microsoft Store qui sort en erreur au lieu de lancer l'interpréteur. La
règle est `py`, ou `python3` sous Linux.

Le mot n'est bloqué qu'en POSITION DE COMMANDE — là où un shell
l'exécuterait vraiment. Une simple recherche de sous-chaîne bloquait
`grep -rn python`, `git commit -m "drop python fallback"` et le mot dans un
commentaire : 9 faux positifs sur 15. Une garde qu'on contourne ne protège
rien.
"""
import json
import re
import sys

# Mots après lesquels le jeton suivant est une commande, pas un argument.
_PREFIXES = "if|then|else|elif|do|done|while|until|sudo|env|time|nohup|exec|command|xargs"

# Interpréteurs dont l'argument -c / -Command est exécuté comme une commande.
# Le nom de l'interpréteur est obligatoire : `-c` seul n'est pas un drapeau
# d'exécution, et le traiter ainsi bloquait `grep -c python`.
_SHELLS = r"bash|sh|zsh|ksh|dash|pwsh|powershell"
_RUN_FLAGS = r"-lc|-c|-Command|-EncodedCommand"

COMMAND_POSITION = re.compile(
    r"""(?:
          ^                                    # début de la commande
        | [;&|()\{\}\n]                        # après un opérateur shell
        | \$\(                                 # dans une substitution
        | `                                    # ...ou une substitution à quotes inverses
        | \b(?:""" + _PREFIXES + r""")\b       # après un mot préfixe
        | \b[A-Za-z_][A-Za-z0-9_]*=\S*         # après une affectation de variable
        | \beval\b\s*["']?                     # eval "python ..."
        | \b(?:""" + _SHELLS + r""")\b         # bash -c "python ..."
          (?:\s+-\S+)*?\s+(?:""" + _RUN_FLAGS + r""")\s*["']?
      )
      \s*
      python(?!3)\b
    """,
    re.VERBOSE,
)

_HEREDOC_START = re.compile(r"""<<-?\s*(?P<q>['"]?)(?P<delim>\w+)(?P=q)""")


def strip_heredoc_bodies(command: str) -> str:
    """Retire le corps des heredocs, garde la ligne qui les ouvre.

    Un corps de heredoc est de la donnée passée à un autre programme, mais
    ses lignes commencent en début de ligne — une position de commande.
    """
    lines = command.split("\n")
    kept: list[str] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        kept.append(line)
        index += 1
        match = _HEREDOC_START.search(line)
        if not match:
            continue
        delimiter = match.group("delim")
        while index < len(lines) and lines[index].strip() != delimiter:
            index += 1
        if index < len(lines):  # sauter la ligne de terminaison elle-même
            index += 1
    return "\n".join(kept)


def find_invocation(text: str) -> "re.Match | None":
    """La première invocation de `python` nu dans `text`, ou None.

    `python3` et `./python` n'en sont pas : ni l'un ni l'autre n'est l'alias
    du Store que cette règle vise.
    """
    return COMMAND_POSITION.search(strip_heredoc_bodies(text))


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # une charge utile malformée n'est pas l'affaire de ce hook

    command = (payload.get("tool_input") or {}).get("command", "")
    if isinstance(command, list):
        command = " ".join(str(c) for c in command)

    if find_invocation(command):
        print(
            "Bloqué : invocation de `python` nu. Employer `py` (AGENTS.md, "
            "règle 1 — l'alias `python` du Microsoft Store est un faux stub). "
            "Le mot n'est bloqué qu'en position de commande : s'il s'agit du "
            "mot et non de la commande, le citer ou le reformuler.",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
