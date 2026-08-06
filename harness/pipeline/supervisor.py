#!/usr/bin/env py
"""
harness/pipeline/supervisor.py -- SC14 (brief 006, Lot 006c).

Why this exists, measured rather than assumed: `harness/budget.py` answers
"how many tool calls has this agent made" but it is a COOPERATIVE check --
the Générateur has to remember to call `status` itself. Nothing in the
harness has ever refused the agent's next tool call. Brief 003's 1,015-call
run is the proof: a supervisor that watches from OUTSIDE the LLM process and
actually terminates it is the missing piece, named explicitly by the Cursor
post-merge audit `CURSOR-6231186-execution-budgets` (FINDING-ARCH-002, "the
call ceiling is cooperative").

This module does NOT rewrite `harness/budget.py` -- it imports
`budget.HARD_STOP_CALLS` (the single source of the threshold) and
`harness/transcripts.py`'s own tool-call reader (the single source of "how
many tool calls has this agent made", per that module's own docstring on
why a second copy of that parsing previously made the budget inert). A
supervisor that reimplemented either would be a second place that could
disagree with the first.

What it is: a parent process that polls a child's live transcript file on
an interval and sends it `signal.SIGTERM` the moment the measured tool-call
count reaches `HARD_STOP_CALLS` -- not `status`-on-request, an external
enforcement point the child cannot forget to call. `decide()` is the pure,
directly-testable rule (`tool_calls, hard_stop -> "CONTINUE"|"SIGTERM"|
"UNMEASURABLE"`); `supervise()` is the polling loop around it; `main()` is
a thin CLI so this is a runnable parent process, not only a library.

Portability, stated honestly rather than assumed: `subprocess.Popen.
send_signal(signal.SIGTERM)` exists on both POSIX and Windows in CPython's
stdlib (`signal.SIGTERM` is defined on Windows too), but on Windows it is
implemented as `TerminateProcess` under the hood -- there is no POSIX-style
signal handler for the child to intercept there. That is exactly the same
underlying syscall `Popen.terminate()` uses on Windows, so `terminate_child`
below does not need two code paths to behave correctly on both platforms;
it uses `send_signal(SIGTERM)` where the `signal` module exposes it (which
is everywhere CPython runs) and falls back to `.terminate()` only on the
theoretical platform where it does not. The test asserts on the REAL,
portable, observable outcome -- the child process is no longer alive after
the call returns -- rather than asserting a POSIX-only signal-delivery
mechanism that would not hold on this dev machine (Windows 11).

Usage:
  py harness/pipeline/supervisor.py --transcript <path> --hard-stop N \
      -- <command to launch the child> [args...]
"""
from __future__ import annotations

import argparse
import json
import signal
import subprocess
import sys
import time
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
import budget  # noqa: E402
import transcripts as transcripts_mod  # noqa: E402

# The one source of the threshold -- never redefined here. If budget.py's
# own HARD_STOP_CALLS changes, this module moves with it automatically.
HARD_STOP_CALLS = budget.HARD_STOP_CALLS

DECISION_CONTINUE = "CONTINUE"
DECISION_SIGTERM = "SIGTERM"
DECISION_UNMEASURABLE = "UNMEASURABLE"


def measure_tool_calls(transcript_path: Path | None) -> int:
    """Real tool-call count from a live transcript, via the ONE shared
    reader (harness/transcripts.py:count_tool_calls). Sentinel -1 (never a
    bare 0, which would read as "zero calls so far") when there is nothing
    to measure yet -- e.g. the child has not written its transcript file."""
    if transcript_path is None:
        return -1
    path = Path(transcript_path)
    if not path.exists():
        return -1
    return transcripts_mod.count_tool_calls(path)[0]


def decide(tool_calls: int, hard_stop: int = HARD_STOP_CALLS) -> str:
    """Pure decision, no I/O: SIGTERM at or above the threshold, CONTINUE
    below it, UNMEASURABLE for the -1 sentinel. Kept separate from
    `supervise()` so the threshold logic itself is unit-testable without a
    real child process."""
    if tool_calls < 0:
        return DECISION_UNMEASURABLE
    return DECISION_SIGTERM if tool_calls >= hard_stop else DECISION_CONTINUE


def terminate_child(child) -> str:
    """Terminate `child` (anything with `.send_signal`/`.terminate`, so a
    test can pass a real subprocess.Popen). Returns which call was made, for
    the caller's own report -- see module docstring for why SIGTERM and
    .terminate() are the same underlying effect on every platform CPython
    runs on."""
    if hasattr(signal, "SIGTERM") and hasattr(child, "send_signal"):
        child.send_signal(signal.SIGTERM)
        return "SIGTERM"
    child.terminate()
    return "terminate"


def supervise(
    child,
    *,
    transcript_path: Path | None,
    hard_stop: int = HARD_STOP_CALLS,
    poll_interval: float = 2.0,
    max_polls: int | None = None,
) -> dict:
    """Poll `child` (a subprocess.Popen-shaped object: `.poll()`,
    `.send_signal()`/`.terminate()`) until it exits on its own, or its
    transcript's measured tool-call count reaches `hard_stop`, whichever
    comes first. Returns a dict describing the outcome; never raises on a
    missing/unreadable transcript (that is UNMEASURABLE, not a crash) and
    never prints -- callers own their own reporting, so this stays testable
    as a pure function of its inputs plus the child's real state.

    `max_polls`, when given, bounds the loop for tests -- it is not a
    production safety feature (the real stop condition is always
    `hard_stop` or the child exiting on its own).
    """
    polls = 0
    while True:
        if child.poll() is not None:
            return {
                "decision": "child_exited",
                "tool_calls": measure_tool_calls(transcript_path),
                "polls": polls,
            }
        tool_calls = measure_tool_calls(transcript_path)
        decision = decide(tool_calls, hard_stop)
        if decision == DECISION_SIGTERM:
            action = terminate_child(child)
            return {
                "decision": DECISION_SIGTERM,
                "action": action,
                "tool_calls": tool_calls,
                "hard_stop": hard_stop,
                "polls": polls,
            }
        polls += 1
        if max_polls is not None and polls >= max_polls:
            return {
                "decision": "max_polls_reached",
                "tool_calls": tool_calls,
                "polls": polls,
            }
        time.sleep(poll_interval)


# --- CLI --------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Parent process that SIGTERMs a child at HARD_STOP_CALLS "
        "(brief 006 SC14). Launches the given command and supervises it."
    )
    parser.add_argument(
        "--transcript", required=True, type=Path,
        help="path to the child's live transcript (an agent-*.jsonl file "
        "under ~/.claude/projects/<slug>/*/subagents/), read on every poll",
    )
    parser.add_argument("--hard-stop", type=int, default=HARD_STOP_CALLS)
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument(
        "command", nargs=argparse.REMAINDER,
        help="the child command to launch and supervise, e.g. -- claude ...",
    )
    args = parser.parse_args(argv)

    cmd = args.command
    if cmd and cmd[0] == "--":
        cmd = cmd[1:]
    if not cmd:
        print("error: no child command given after --", file=sys.stderr)
        return 2

    child = subprocess.Popen(cmd)
    outcome = supervise(
        child,
        transcript_path=args.transcript,
        hard_stop=args.hard_stop,
        poll_interval=args.poll_interval,
    )
    print(json.dumps(outcome, default=str))
    return 3 if outcome.get("decision") == DECISION_SIGTERM else 0


if __name__ == "__main__":
    raise SystemExit(main())
