#!/usr/bin/env py
"""
PreToolUse hook (Bash matcher). Blocks bare `python` invocations.

Hard-won rule 1: `py`, never `python` — the Microsoft Store alias for
`python` on this machine is a fake stub that exits non-zero instead of
running the interpreter. Reads the hook's JSON payload on stdin, checks
tool_input.command, exits 2 (block) if found, else exits 0 (allow).

## Why this is not a plain substring match any more

The first version matched `python` anywhere in the command string. That is
hard-won rule 6 in the flesh — a check too coarse costs as much as a lax
one. It blocked, among others:

    grep -rn python docs/                  # you could not search for the word
    git commit -m "drop python fallback"   # nor mention it in a message
    py -c "print('arbitrary python')"      # nor put it in a string

each of which costs a tool call and a retry, and none of which runs the
stub. A guard that fires on commands nobody meant teaches people to work
around the guard.

So the match is now *positional*: `python` counts only where a shell would
actually execute it — at the start of the command, after an operator
(`;` `&&` `||` `|` `(` `{` newline), inside a command substitution, after an
env-var assignment or a prefix word (`sudo`, `env`, `time`, `xargs`, `if`,
`do`, …), or immediately inside an explicit run-this-string flag (`-c`,
`-lc`, `-Command`, `eval`). Anywhere else the word is data, not a command.

Heredoc bodies are stripped first: their lines start at a line boundary, so
a document that merely *talks* about python would otherwise look like a
command at the start of a line.

Two deliberate calls:

  * A run-this-string flag counts only when an interpreter precedes it.
    `bash -c "python x"` is blocked; `grep -c python docs/` is not. Keying
    on a bare `-c` was the first attempt and reintroduced the same class of
    false positive it was meant to remove — caught by the tests, not by
    review.
  * `./python` and `python3` stay allowed, as before: neither is the Store
    alias this rule exists for.

Known and accepted gap: a form this positional match does not see, such as
an invocation assembled at runtime (`cmd="pyth"; ${cmd}on x`) or reached
through a wrapper script. The hook is a guard against the everyday mistake,
not a sandbox — and it fails toward allowing, because a guard that blocks
work nobody meant gets routed around and then protects nothing at all.
"""
import json
import re
import sys

# Words after which the next token is a command, not an argument.
_PREFIX_WORDS = (
    "if|then|else|elif|do|done|while|until|"
    "sudo|env|time|nohup|exec|command|xargs"
)

# Interpreters whose -c / -Command argument is executed as a command line.
# The shell name is required: `-c` on its own is not a run-this-string flag,
# and treating it as one blocked `grep -c python ...` — the exact class of
# false positive this rewrite exists to remove.
_SHELLS = r"bash|sh|zsh|ksh|dash|pwsh|powershell"
_RUN_FLAGS = r"-lc|-c|-Command|-EncodedCommand"

BARE_PYTHON = re.compile(
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

    A heredoc body is data the shell hands to another program, but its lines
    begin at a line boundary — which is a command position — so prose about
    python inside one would read as an invocation.
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


def offending_match(command: str) -> re.Match | None:
    return BARE_PYTHON.search(strip_heredoc_bodies(command))


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # malformed payload is not this hook's problem to police

    command = (payload.get("tool_input") or {}).get("command", "")
    if isinstance(command, list):
        command = " ".join(str(c) for c in command)

    if offending_match(command):
        print(
            "Blocked: bare `python` invocation detected. Use `py` instead "
            "(see docs/rules/hard-won-rules.md, rule 1 — the Microsoft "
            "Store `python` alias on this machine is a fake stub). "
            "If you meant the word rather than the command, it is only "
            "blocked in command position — quote it or reword.",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
