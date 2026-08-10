#!/usr/bin/env py
"""
Measures `config_mode_single_commit_transition_count` (brief 009, Lot 009a
Required Counters table) -- the ONLY counter in this lot that cannot be
computed until the orchestrator has committed this lot's tree in a single
commit (this Générateur role never commits, per CLAUDE.md's own working
rule; see deliverables/generator-log.md for why this script exists instead
of a fabricated number).

Definition (brief 009, verbatim source of truth is brief.md's own Required
Counters row -- this docstring restates the MECHANISM, not the row's
prose): restrict `git log -p` to this lot's own commit range against
`harness/pipeline/config.yaml`, and count the number of DISTINCT values the
`mode:` line takes across the +/- diff lines in that range. Must equal 2
(the old value `full_auto` removed once, the new value
`full_auto_decision_only` added once) -- a THIRD distinct value anywhere in
that range would mean an intermediate commit left a bare value in between,
which SC3 forbids.

Usage (run AFTER the orchestrator's commit exists):
  py deliverables/measure_config_mode_transitions.py <commit-range>

Example, once the lot's commit sha is known:
  py deliverables/measure_config_mode_transitions.py <sha>~1..<sha>

Or, to scope to "everything since this branch diverged from the point brief
009 started" (if the lot lands as more than one commit for any reason):
  py deliverables/measure_config_mode_transitions.py <base-sha>..HEAD
"""
from __future__ import annotations

import re
import subprocess
import sys

MODE_LINE_RE = re.compile(r"^[+-]mode:\s*(\S+)\s*$")


def count_distinct_mode_values(commit_range: str, path: str = "harness/pipeline/config.yaml") -> tuple[int, list[str]]:
    result = subprocess.run(
        ["git", "log", "-p", commit_range, "--", path],
        capture_output=True, text=True, check=True,
    )
    values: list[str] = []
    for line in result.stdout.splitlines():
        match = MODE_LINE_RE.match(line)
        if match:
            values.append(match.group(1))
    distinct = sorted(set(values))
    return len(distinct), distinct


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__)
        return 2
    commit_range = argv[1]
    count, distinct = count_distinct_mode_values(commit_range)
    print(f"config_mode_single_commit_transition_count = {count}")
    print(f"distinct values seen: {distinct}")
    if count != 2:
        print(
            "FAIL: expected exactly 2 (old value removed once, new value "
            "added once) -- an intermediate bare commit or an unexpected "
            "diff shape is present.",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
