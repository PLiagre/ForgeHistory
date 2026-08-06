#!/usr/bin/env py
"""
harness/pipeline/forge_run_preflight.py -- SC15 (brief 006, Lot 006c).

`/forge-run`'s own "Execution Budgets" section already told a human reader
to "honour a NEEDS_SPLIT" -- but `py harness/budget.py split-check` is
documented as advisory ON PURPOSE (see budget.py's own module docstring,
`cmd_split_check`) and its CLI always exits 0 (`EXIT_OK`) regardless of the
verdict it prints. Nothing actually stopped `/forge-run` from launching the
Générateur on a brief `split-check` itself had just called NEEDS_SPLIT --
that was CURSOR-6231186-execution-budgets' FINDING-ARCH-001.

This script does not change that advisory contract (budget.py is not
rewritten here, only imported): it calls `budget.cmd_split_check` itself --
the ONE place the NEEDS_SPLIT trigger rule is computed -- and reads back its
own printed `advisory   : <VERDICT>` line, so the trigger logic is never
duplicated a second time (a second copy could disagree with the first). It
then turns that verdict into a blocking exit for the ONE caller that must
never proceed past NEEDS_SPLIT: `/forge-run`'s own Phase 0, before Phase 1
launches any Générateur.

`--estimated-calls` is REQUIRED here (unlike budget.py's own optional flag,
default -1/NO_ESTIMATE) -- `/forge-run` must always state an estimate
before it may launch, per the brief's own "en première action" obligation.

Usage:
  py harness/pipeline/forge_run_preflight.py --brief <dir> --estimated-calls N
Exit 0 on SIZE_OK; exit 1 (BLOCKED) on NEEDS_SPLIT.
"""
from __future__ import annotations

import argparse
import contextlib
import io
import re
import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
import budget  # noqa: E402

VERDICT_RE = re.compile(r"^advisory\s*:\s*(\S+)", re.MULTILINE)


def compute_verdict(brief_dir: Path, estimated_calls: int) -> tuple[str, str]:
    """Call budget.cmd_split_check itself and parse its own printed verdict
    line back out. Never re-implements the NEEDS_SPLIT trigger rule."""
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        budget.cmd_split_check(brief_dir, estimated_calls)
    text = buf.getvalue()
    match = VERDICT_RE.search(text)
    return (match.group(1) if match else "UNKNOWN"), text


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--brief", required=True, type=Path)
    parser.add_argument(
        "--estimated-calls", type=int, required=True,
        help="required, not optional here -- /forge-run must always state "
        "an estimate before it may launch a Générateur",
    )
    args = parser.parse_args(argv)

    verdict, text = compute_verdict(args.brief, args.estimated_calls)
    print(text, end="" if text.endswith("\n") else "\n")

    if verdict == "NEEDS_SPLIT":
        print(
            "\nBLOCKED (forge-run preflight, brief 006 SC15): split-check "
            "returned NEEDS_SPLIT -- /forge-run MUST NOT launch the "
            "Générateur. Split into atomic lots first (see the guidance "
            "printed above).",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
