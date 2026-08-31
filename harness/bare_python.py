#!/usr/bin/env python3
"""
harness/bare_python.py -- détection technique des appels ambigus à `python`.

Le nom nu `python` n'est pas portable : il peut désigner un alias absent ou
le raccourci du Microsoft Store. Le dépôt emploie `python3` sous Linux et
`py` sous Windows.

Two callers enforce that rule, and both used to carry their own copy of a
plain substring match:

  * `.claude/hooks/no_bare_python.py` -- garde locale facultative ;
  * `harness/verdict_audit.py` -- diagnostic facultatif sur les commandes
    déclarées dans un compte-rendu.

A substring match is hard-won rule 6 in the flesh: too coarse costs as much
as too lax. It blocked `grep -rn python docs/`, `git commit -m "drop python
fallback"`, and the word inside a comment -- 9 false positives out of 15
non-invoking commands. Work nobody meant to block gets routed around, and a
routed-around guard protects nothing.

So the match is POSITIONAL: the word counts only where a shell would
actually execute it -- start of the command, after an operator (`;` `&&`
`||` `|` `(` `{` newline), inside a command substitution, after an env-var
assignment or a prefix word (sudo, env, time, xargs, if, do, ...), after
`eval`, or inside an interpreter's run-this-string flag (`bash -c`,
`pwsh -Command`). Anywhere else the word is data.

Le module partagé évite deux implémentations divergentes. Il reste limité à
la bibliothèque standard afin d'être utilisable dans tous les environnements
du dépôt.

Known and accepted gap: an invocation assembled at runtime
(`cmd="pyth"; ${cmd}on x`) or reached through a wrapper script is not seen.
This is a guard against the everyday mistake, not a sandbox, and it fails
toward allowing on purpose.
"""
from __future__ import annotations

import re

# Words after which the next token is a command, not an argument.
_PREFIX_WORDS = (
    "if|then|else|elif|do|done|while|until|"
    "sudo|env|time|nohup|exec|command|xargs"
)

# Interpreters whose -c / -Command argument is executed as a command line.
# The interpreter name is required: `-c` alone is not a run-this-string flag,
# and treating it as one blocked `grep -c python ...` -- the exact class of
# false positive this module exists to remove. That was the first attempt,
# and the tests caught it, not review.
_SHELLS = r"bash|sh|zsh|ksh|dash|pwsh|powershell"
_RUN_FLAGS = r"-lc|-c|-Command|-EncodedCommand"

COMMAND_POSITION = re.compile(
    r"""(?:
          ^                                    # start of the command
        | [;&|()\{\}\n]                        # after a shell operator
        | \$\(                                 # inside a command substitution
        | `                                    # ...or a backquoted one
        | \b(?:""" + _PREFIX_WORDS + r""")\b   # after a prefix word
        | \b[A-Za-z_][A-Za-z0-9_]*=\S*         # after an env-var assignment
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
    """Drop heredoc contents, keeping the line that opens them.

    A heredoc body is data handed to another program, but its lines begin at
    a line boundary -- a command position -- so prose about python inside one
    would otherwise read as an invocation.
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
        if index < len(lines):  # skip the terminator line itself
            index += 1
    return "\n".join(kept)


def find_invocation(text: str) -> re.Match | None:
    """The first bare-`python` invocation in `text`, or None.

    `python3` and `./python` are deliberately not invocations: neither is the
    Store alias this rule exists for.
    """
    return COMMAND_POSITION.search(strip_heredoc_bodies(text))
