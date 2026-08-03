#!/usr/bin/env py
"""
PreToolUse hook (Bash matcher). Blocks bare `python` invocations.

Hard-won rule 1: `py`, never `python` — the Microsoft Store alias for
`python` on this machine is a fake stub that exits non-zero instead of
running the interpreter. Reads the hook's JSON payload on stdin, checks
tool_input.command, exits 2 (block) if found, else exits 0 (allow).

The matching itself lives in `harness/bare_python.py`, shared with
`verdict_audit.py`'s `no_bare_python_alias` check — see that module for why
the match is positional rather than a substring scan, and what it
deliberately does not catch.

On a missing or broken matcher this hook **fails closed** (exit 2) rather
than waving everything through. The only way that import fails is a
repository whose `harness/` is gone or unreadable, which is not a state to
keep working in; and a guard that disappears silently is worse than one that
stops you with a message naming the file to restore.
"""
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO_ROOT / "harness"))

try:
    from bare_python import find_invocation
except Exception as exc:  # noqa: BLE001 -- a broken guard must be loud, never silent
    print(
        f"Blocked: the bare-`python` guard could not load its matcher "
        f"({REPO_ROOT / 'harness' / 'bare_python.py'}): {exc}. "
        "Restore that file; the guard fails closed rather than allowing "
        "unchecked commands.",
        file=sys.stderr,
    )
    sys.exit(2)


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # malformed payload is not this hook's problem to police

    command = (payload.get("tool_input") or {}).get("command", "")
    if isinstance(command, list):
        command = " ".join(str(c) for c in command)

    if find_invocation(command):
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
