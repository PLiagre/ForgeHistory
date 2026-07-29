#!/usr/bin/env py
"""
PreToolUse hook (Bash matcher). Blocks bare `python` invocations.

Hard-won rule 1: `py`, never `python` — the Microsoft Store alias for
`python` on this machine is a fake stub that exits non-zero instead of
running the interpreter. Reads the hook's JSON payload on stdin, checks
tool_input.command for a bare `python` token, exits 2 (block) if found,
else exits 0 (allow).
"""
import json
import re
import sys

BARE_PYTHON = re.compile(r'(?<![\w./])python(?!3)\b')


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # malformed payload is not this hook's problem to police

    command = (payload.get("tool_input") or {}).get("command", "")
    if isinstance(command, list):
        command = " ".join(str(c) for c in command)

    if BARE_PYTHON.search(command):
        print(
            "Blocked: bare `python` invocation detected. Use `py` instead "
            "(see docs/rules/hard-won-rules.md, rule 1 — the Microsoft "
            "Store `python` alias on this machine is a fake stub).",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
