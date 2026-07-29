#!/usr/bin/env py
"""
PreToolUse hook (Edit/Write/MultiEdit matcher). Blocks edits to VISION.md
unless explicitly overridden.

Mechanically enforces a rule we'd otherwise only have as documentation
(HANDOFF.md: "do not silently rewrite VISION.md to fix [dead links]") --
hard-won rule 5: a guard placed after the effect it should prevent protects
nothing, so this exists before anyone edits it, not after.

Override: set FORGE_ALLOW_VISION_EDIT=1 in the environment for the command
that needs to intentionally change VISION.md (e.g. a deliberate, reviewed
re-import from a new VictoriaProject revision). This is an explicit,
auditable action, not a silent bypass (hard-won rule 9: an impossibility is
tested before being invoked, command + error, never a silent skip -- here,
the "command" is the override itself).
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
        "Blocked: VISION.md may not be edited without an explicit override.\n"
        "VISION.md is a verbatim copy of VictoriaProject's vision -- see "
        "HANDOFF.md for known gaps (e.g. dead ADR links) that are tracked "
        "as decisions to make explicitly, not silent fixes.\n"
        "To intentionally change it, set FORGE_ALLOW_VISION_EDIT=1 for this "
        "command and record why in HANDOFF.md or an ADR.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
