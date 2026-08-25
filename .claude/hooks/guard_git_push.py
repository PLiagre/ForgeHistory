#!/usr/bin/env py
"""
PreToolUse hook (Bash matcher). Blocks `git push` if the harness's own test
suite is red.

Explicitly required by the brief (voir AGENTS.md /
project charter, section 5.5): "les hooks pour tout ce qui est mécanique et
doit se vérifier à chaque fois ... garde avant `git push`." A mechanical,
deterministic check -- no LLM inference, same philosophy as
harness/verdict_audit.py.

Reads the hook's JSON payload on stdin, checks tool_input.command for a
`git push` invocation, and if found, runs `py -m pytest harness/tests/ -q`.
Blocks (exit 2) if that suite fails; allows (exit 0) otherwise, including
when no harness/tests/ directory exists yet (nothing to guard).
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
    tests_dir = repo_root / "harness" / "tests"
    if not tests_dir.is_dir():
        return 0  # nothing to guard yet

    result = subprocess.run(
        [sys.executable, "-m", "pytest", str(tests_dir), "-q"],
        capture_output=True, text=True, cwd=repo_root,
    )

    if result.returncode != 0:
        print(
            "Blocked: `git push` while harness/tests/ is red.\n"
            f"Ran: py -m pytest harness/tests/ -q (exit {result.returncode})\n"
            "Fix the failing tests first, or run the command yourself "
            "outside this hook if you deliberately need to push a "
            "known-broken state.\n\n"
            f"{result.stdout[-2000:]}\n{result.stderr[-2000:]}",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
