#!/usr/bin/env py
"""
Stop hook. Warn-only (never blocks) reminder that HANDOFF.md may be stale.

Same philosophy as ECC's delivery-gate: "regex heuristics can false-positive
... it never blocks on its own." This is a mtime heuristic, not a semantic
check, so it only ever warns (exit 0, message on stderr) -- never exit 2.
Run `/forge-checkpoint` to update HANDOFF.md properly; this hook cannot do
that itself, it can only notice the file looks old.
"""
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HANDOFF = REPO_ROOT / "HANDOFF.md"

# Directories/patterns whose changes plausibly warrant a HANDOFF.md update.
# Deliberately excludes harness/queue/ (routine queue/ledger churn) and
# harness/demo/ (static fixtures + their run logs) to avoid false-positive
# noise on every ordinary gate run.
WATCHED_GLOBS = [
    ".claude/**/*.md", ".claude/**/*.py", ".claude/**/*.json", ".claude/**/*.sh",
    "docs/**/*.md",
    "harness/*.py",
    "harness/backends/*.sh", "harness/backends/*.py", "harness/backends/*.md",
    "harness/tests/*.py",
]


def newest_watched_mtime() -> float | None:
    newest = None
    for pattern in WATCHED_GLOBS:
        for p in REPO_ROOT.glob(pattern):
            if not p.is_file():
                continue
            mtime = p.stat().st_mtime
            if newest is None or mtime > newest:
                newest = mtime
    return newest


def main() -> int:
    if not HANDOFF.exists():
        return 0  # nothing to compare against; not this hook's concern

    newest = newest_watched_mtime()
    if newest is None:
        return 0

    handoff_mtime = HANDOFF.stat().st_mtime
    if newest > handoff_mtime:
        print(
            "Reminder: files under .claude/, docs/, or harness/ (excluding "
            "queue/ and demo/) were modified more recently than HANDOFF.md. "
            "Consider running /forge-checkpoint before ending the session.",
            file=sys.stderr,
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
