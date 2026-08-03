#!/usr/bin/env py
"""
harness/budget.py -- execution budgets for the Générateur role.

Why this exists, measured rather than assumed: nothing in the harness bounds
how long a single agent runs. `/forge-run` stops on PASS, plateau, 3-strikes
or max-iterations -- all judgements about verdict *quality*, none about run
*length*. Brief 003 therefore produced one agent of 1,015 tool calls whose
context grew 111k -> 696k tokens and never compacted. Because every call
re-sends the accumulated context, cost is the integral under that curve: the
last 20% of calls cost 33% of the agent. Doubling the turns roughly
quadruples the bill.

The count is MEASURED, not self-reported. An agent asked to count its own
tool calls will drift, and a counter invoked once per tool call would double
the very thing it measures. So this reads the agent's live transcript --
Claude Code writes one JSONL per subagent as it runs, the same source
`harness/backends/ledger.py tokens` reads. Observing the budget therefore
costs zero extra tool calls.

The honest consequence: when the transcript cannot be found, the answer is
UNMEASURABLE (exit 2), never OK. A budget that silently reads "fine" when it
cannot see anything is worse than no budget.

Statuses (deliberately NOT gate verdicts -- see below):
  OK                 under the warning threshold
  WARN               past the warning threshold, keep going
  CHECKPOINT_DUE     write the structured checkpoint now
  BUDGET_EXHAUSTED   stop; the remaining work belongs in a fresh session
  NO_PROGRESS_STOP   too many calls since the last mechanical progress event
  UNMEASURABLE       no transcript found; nothing is being enforced

BUDGET_EXHAUSTED and NEEDS_SPLIT are NOT a REJECT. A REJECT says the work is
wrong. These say the work is unfinished and the container was too small --
the deliverables produced so far may be perfectly good. Conflating them
would teach the loop to treat a well-executed oversized brief as a failure.

Usage:
  py harness/budget.py status   --brief <dir> [--transcripts DIR]
  py harness/budget.py progress --brief <dir> --kind KIND --evidence "..."
  py harness/budget.py checkpoint --brief <dir>
  py harness/budget.py split-check --brief <dir> [--estimated-calls N]
"""
from __future__ import annotations

import argparse
import datetime
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import transcripts as transcripts_mod

REPO_ROOT = Path(__file__).resolve().parent.parent

# Tool-call thresholds, re-verified against the CORRECTED counter (an
# external audit caught the original counting 186 where the transcript held
# 1,015 -- see harness/transcripts.py). Largest single Générateur agent per
# brief, in real tool calls:
#
#   001    51      002    31      003  1,015      004   465      005   350
#
# So the band leaves the two cheap briefs untouched (51 and 31 are well under
# the 100 warning) and cuts the three expensive ones into 3-7 steps. The
# audit expected these to become ~5.5x too strict after the fix; they do not,
# because they were stated against real tool counts rather than tuned to the
# broken counter.
WARN_CALLS = 100
CHECKPOINT_CALLS = 130
HARD_STOP_CALLS = 160
NO_PROGRESS_CALLS = 35

# A progress event is one of these five, and nothing else. Each is something
# a machine can check after the fact from the evidence given -- "I made
# progress" is not on the list, on purpose.
PROGRESS_KINDS = {
    "red_to_green": "a failing test now passes",
    "failures_decreased": "the failure count went down",
    "gate_check_gained": "one more verdict_audit.py check passes",
    "deliverable_created": "an expected deliverable now exists",
    "plan_step_done": "a declared step of the brief's plan is complete",
}

EXIT_OK = 0
EXIT_UNMEASURABLE = 2
EXIT_CHECKPOINT_DUE = 3
EXIT_BUDGET_EXHAUSTED = 4
EXIT_NO_PROGRESS = 5
EXIT_USAGE = 64

BRIEF_RE = re.compile(r"harness/queue/briefs/([0-9]{3}-[a-z0-9-]+)")
BRIEF_SCAN_LINES = 4

# Subsystem roots the routing table in CLAUDE.md treats as separate concerns.
SUBSYSTEM_ROOTS = ("sim/", "pipeline/", "unity/", "harness/", "docs/", ".claude/")

# Why only the estimate triggers, and the textual signals merely report.
#
# Measured against every brief in the repo whose real cost is known:
#
#   brief   subsystems in Success Conditions   conditions   tool calls
#   001                     3                      10             93
#   002                     2                       9             31
#   003                     2                       9          1,015
#   004                     3                       7            763
#   005                     1                      10            737
#
# Subsystem breadth is *anti*-correlated here (001 spans three subsystems for
# 93 calls; 005 spans one for 737). Condition count is flat across a 20x
# cost range. A phrase-match on "whole"/"entire" fires on all five, because
# briefs use those words in ordinary prose. Every textual rule tried either
# flagged all five or pointed the wrong way -- and a check that always says
# NEEDS_SPLIT carries no information and will be ignored.
#
# So the one mechanical trigger is the Planificateur's own --estimated-calls.
# That is also the honest reading of the owner's criteria: "covers several
# *independent* subsystems" turns on independence, which is a judgement this
# script cannot make. The signals below are printed to inform that judgement,
# not to substitute for it. Revisit if the sample ever gets large enough to
# support a real rule.
CONDITIONS_SIGNAL = 12


def success_conditions_section(text: str) -> str:
    """The body under a '## Success Conditions' heading, up to the next one.

    Scoped on purpose: a brief mentions half the repo in its Non-Goals and
    cross-references, so counting subsystem roots across the whole file
    measures how chatty the brief is, not what it commits to touching.
    """
    lines: list[str] = []
    inside = False
    for line in text.splitlines():
        if re.match(r"^#{1,3}\s+Success Conditions\b", line, re.IGNORECASE):
            inside = True
            continue
        if inside and re.match(r"^#{1,3}\s+\S", line):
            break
        if inside:
            lines.append(line)
    return "\n".join(lines)


def count_success_conditions(text: str) -> int:
    """Top-level numbered items under the Success Conditions heading.

    Briefs write them as `1. **Copy scope.** ...`, not as sub-headings, so a
    heading-based count reports 0 or 1 on every real brief in the repo.
    """
    return len(re.findall(r"^\d+\.\s+\S", success_conditions_section(text),
                          re.MULTILINE))
GLOBAL_GOAL_RE = re.compile(
    r"\b(whole|entire|all of the|every part of|bulk[- ]port|tout le|toute la)\b",
    re.IGNORECASE,
)


def default_transcripts_dir() -> Path:
    return transcripts_mod.default_transcripts_dir()


def brief_slug(brief_dir: Path) -> str:
    return brief_dir.resolve().name


def find_agent_transcript(slug: str, transcripts: Path) -> Path | None:
    """Kept for callers that only need "is there exactly one?"; ambiguity is
    resolved by cmd_status, which must never silently pick the smallest."""
    found = transcripts_mod.agent_transcripts_for(slug, transcripts)
    return found[0] if len(found) == 1 else None


def count_calls(path: Path) -> tuple[int, int]:
    """(api_requests, tool_calls) for one transcript.

    Both are reported because they answer different questions: API requests
    are what the bill is made of, tool calls are what the budget is
    expressed in. See harness/transcripts.py for why they need two different
    counting rules -- getting this wrong is what made this budget inert.
    """
    tool_use, _tool_result = transcripts_mod.count_tool_calls(path)
    return (len(transcripts_mod.usage_by_request(path)), tool_use)


def progress_path(brief_dir: Path) -> Path:
    return brief_dir / "deliverables" / "progress.jsonl"


def load_progress(brief_dir: Path) -> list[dict]:
    path = progress_path(brief_dir)
    if not path.exists():
        return []
    events = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return events


def append_progress(brief_dir: Path, kind: str, evidence: str, tool_calls: int) -> dict:
    entry = {
        "timestamp": datetime.datetime.now().isoformat(),
        "kind": kind,
        "evidence": evidence,
        "tool_calls_at": tool_calls,
    }
    path = progress_path(brief_dir)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(entry) + "\n")
    return entry


def classify(tool_calls: int, calls_since_progress: int | None) -> str:
    if tool_calls >= HARD_STOP_CALLS:
        return "BUDGET_EXHAUSTED"
    if calls_since_progress is not None and calls_since_progress >= NO_PROGRESS_CALLS:
        return "NO_PROGRESS_STOP"
    if tool_calls >= CHECKPOINT_CALLS:
        return "CHECKPOINT_DUE"
    if tool_calls >= WARN_CALLS:
        return "WARN"
    return "OK"


STATUS_EXIT = {
    "OK": EXIT_OK,
    "WARN": EXIT_OK,
    "CHECKPOINT_DUE": EXIT_CHECKPOINT_DUE,
    "BUDGET_EXHAUSTED": EXIT_BUDGET_EXHAUSTED,
    "NO_PROGRESS_STOP": EXIT_NO_PROGRESS,
}


def cmd_status(brief_dir: Path, transcripts: Path, as_json: bool,
               agent: str = "") -> int:
    slug = brief_slug(brief_dir)
    candidates = transcripts_mod.agent_transcripts_for(slug, transcripts)

    if agent:
        candidates = [p for p in candidates if agent in p.name]

    transcript = None
    reason = ""
    if len(candidates) == 1:
        transcript = candidates[0]
    elif not candidates:
        reason = f"no agent transcript naming {slug} under {transcripts}"
    else:
        # Never resolve this by mtime. Doing so is what reported OK with
        # tool_calls: 0 for brief 003 -- the newest transcript naming it was
        # a 4-request agent, while the real run was 1,015 tool calls. A
        # budget that answers OK on the most expensive brief in the repo
        # protects nothing, and a false OK is worse than no answer.
        listing = ", ".join(
            f"{p.name} ({transcripts_mod.count_tool_calls(p)[0]} tool calls)"
            for p in candidates)
        reason = (f"{len(candidates)} transcripts name {slug}: {listing}. "
                  f"Disambiguate with --agent <substring>.")

    if transcript is None:
        status = "UNMEASURABLE" if not candidates else "AMBIGUOUS"
        payload = {"status": status, "brief": slug, "reason": reason}
        if as_json:
            print(json.dumps(payload, indent=2))
        else:
            print(f"status     : {status}")
            print(f"reason     : {reason}")
            print("Nothing is being enforced. This is not OK -- it is unmeasured.")
        return EXIT_UNMEASURABLE

    requests, tools = count_calls(transcript)
    events = load_progress(brief_dir)
    last_progress_at = events[-1]["tool_calls_at"] if events else 0
    since = tools - last_progress_at
    status = classify(tools, since)

    payload = {
        "status": status,
        "brief": slug,
        "transcript": str(transcript),
        "tool_calls": tools,
        "api_requests": requests,
        "progress_events": len(events),
        "tool_calls_since_progress": since,
        "thresholds": {
            "warn": WARN_CALLS,
            "checkpoint": CHECKPOINT_CALLS,
            "hard_stop": HARD_STOP_CALLS,
            "no_progress": NO_PROGRESS_CALLS,
        },
    }
    if as_json:
        print(json.dumps(payload, indent=2))
    else:
        print(f"status     : {status}")
        print(f"brief      : {slug}")
        print(f"transcript : {transcript.name}  (most recent naming this brief)")
        print(f"tool_calls : {tools}   (warn {WARN_CALLS} / checkpoint "
              f"{CHECKPOINT_CALLS} / stop {HARD_STOP_CALLS})")
        print(f"api_calls  : {requests}")
        print(f"progress   : {len(events)} event(s), {since} tool call(s) since the last "
              f"(stop at {NO_PROGRESS_CALLS})")
        if not events:
            # Otherwise NO_PROGRESS_STOP at call 35 looks like a bug rather
            # than the rule: with an empty ledger the clock runs from zero,
            # because zero recorded progress *is* zero measurable progress.
            print("             (no events recorded, so the clock runs from call 0 --"
                  " record progress\n              as it happens: py harness/budget.py"
                  " progress --brief <dir> --kind ... --evidence ...)")
        if status == "CHECKPOINT_DUE":
            print("\n-> Write the checkpoint now: py harness/budget.py checkpoint --brief "
                  f"{brief_dir}")
        elif status in ("BUDGET_EXHAUSTED", "NO_PROGRESS_STOP"):
            print(f"\n-> STOP. Write the checkpoint and hand the remainder to a fresh "
                  f"session.\n   This is NOT a REJECT: what you built may be correct; the "
                  f"brief was too big for one run.")
    return STATUS_EXIT[status]


def cmd_progress(brief_dir: Path, kind: str, evidence: str, transcripts: Path) -> int:
    if kind not in PROGRESS_KINDS:
        print(f"Unknown progress kind: {kind!r}", file=sys.stderr)
        print("Allowed (mechanical events only):", file=sys.stderr)
        for name, meaning in PROGRESS_KINDS.items():
            print(f"  {name:22s} {meaning}", file=sys.stderr)
        return EXIT_USAGE
    if not evidence.strip():
        # Rule 3 in spirit: a marker without evidence is a claim, not a
        # measurement, and would silently reset the no-progress clock.
        print("--evidence is required and must be non-empty: name the command "
              "or file that proves this event.", file=sys.stderr)
        return EXIT_USAGE

    transcript = find_agent_transcript(brief_slug(brief_dir), transcripts)
    tool_calls = count_calls(transcript)[1] if transcript else -1
    entry = append_progress(brief_dir, kind, evidence.strip(), tool_calls)
    if tool_calls < 0:
        # Sentinel -1, never a bare 0 that would read as "at the very start".
        print("recorded (tool_calls_at = -1: transcript not found, count unmeasured)")
    print(json.dumps(entry))
    return EXIT_OK


CHECKPOINT_TEMPLATE = """# Checkpoint -- {slug}

**Author**: forge-generateur
**Written**: {now}
**Reason**: {status} at {tool_calls} tool calls (warn {warn} / checkpoint \
{checkpoint} / stop {stop})

This is a handoff, not a verdict. `{status}` means the run hit its execution
budget, NOT that the work is wrong -- the deliverables below may be entirely
correct. The Évaluateur judges the work; this file only says where it stopped.

The next session must be able to resume from **this file plus the files in
the repository**, without reading the previous transcript.

## 1. Objectif du lot
<!-- The one outcome this run was trying to reach. One sentence. -->

## 2. Travail terminé
<!-- What is actually done and verifiable now. Not what was attempted. -->

## 3. Fichiers modifiés
<!-- Paths, each with one line on what changed. -->

## 4. Tests exécutés et résultats
<!-- Exact commands and their real output. No remembered numbers. -->

## 5. Décisions prises
<!-- Choices a fresh session would otherwise re-litigate, and why. -->

## 6. Problèmes ouverts
<!-- Known-broken, known-unverified, known-deferred. Be specific. -->

## 7. Prochaine action exacte
<!-- The single next thing to do. Not a list of directions. -->

## 8. Commande de reprise
```bash
# The exact command a fresh session runs first.
```

## 9. Contexte minimal nécessaire
<!-- The few files a fresh session must read, and why each one. Keep this
     short: a long list here recreates the context bloat this budget exists
     to prevent. -->

## Measured state at checkpoint time
| metric | value |
|---|---|
| tool calls | {tool_calls} |
| API requests | {api_requests} |
| progress events | {progress_count} |
| tool calls since last progress | {since} |

### Progress ledger
{progress_table}
"""


def cmd_checkpoint(brief_dir: Path, transcripts: Path) -> int:
    slug = brief_slug(brief_dir)
    transcript = find_agent_transcript(slug, transcripts)
    requests, tools = count_calls(transcript) if transcript else (-1, -1)
    events = load_progress(brief_dir)
    last_at = events[-1]["tool_calls_at"] if events else 0
    since = tools - last_at if tools >= 0 else -1
    status = classify(tools, since) if tools >= 0 else "UNMEASURABLE"

    if events:
        rows = ["| # | kind | tool calls | evidence |", "|---|---|---|---|"]
        for index, event in enumerate(events, 1):
            evidence = str(event.get("evidence", "")).replace("|", "\\|")[:120]
            rows.append(f"| {index} | {event.get('kind')} | "
                        f"{event.get('tool_calls_at')} | {evidence} |")
        progress_table = "\n".join(rows)
    else:
        progress_table = "_No progress events recorded._"

    body = CHECKPOINT_TEMPLATE.format(
        slug=slug,
        now=datetime.datetime.now().isoformat(timespec="seconds"),
        status=status,
        tool_calls=tools,
        api_requests=requests,
        warn=WARN_CALLS,
        checkpoint=CHECKPOINT_CALLS,
        stop=HARD_STOP_CALLS,
        progress_count=len(events),
        since=since,
        progress_table=progress_table,
    )

    out_dir = brief_dir / "deliverables"
    out_dir.mkdir(parents=True, exist_ok=True)
    existing = sorted(out_dir.glob("checkpoint-*.md"))
    out = out_dir / f"checkpoint-{len(existing) + 1:03d}.md"
    out.write_text(body, encoding="utf-8")
    print(f"wrote {out}")
    print("Fill sections 1-9. The measured table is already filled in; do not "
          "hand-edit its numbers.")
    return EXIT_OK


def cmd_split_check(brief_dir: Path, estimated_calls: int) -> int:
    """Advisory pre-flight for the Planificateur.

    Deliberately advisory, and labelled so: whether two subsystems are
    genuinely independent is a judgement this script cannot make. What it can
    do is count the signals the owner named and put them in front of the
    Planificateur before generation starts, rather than after a 1,000-call
    agent has already run.
    """
    brief_file = brief_dir / "brief.md"
    if not brief_file.exists():
        print(f"No brief.md at {brief_file}", file=sys.stderr)
        return EXIT_USAGE
    text = brief_file.read_text(encoding="utf-8", errors="replace")

    section = success_conditions_section(text)
    subsystems = sorted({root for root in SUBSYSTEM_ROOTS
                         if root in set(re.findall(r"`([a-z.][a-z0-9_.-]*/)", section))})
    conditions = count_success_conditions(text)
    global_goal = GLOBAL_GOAL_RE.search(text)

    # Mechanical triggers. Exactly one, deliberately -- see CONDITIONS_SIGNAL.
    triggers = []
    if estimated_calls > 150:
        triggers.append(f"estimated {estimated_calls} tool calls (> 150, and the "
                        f"Générateur stops at {HARD_STOP_CALLS})")

    verdict = "NEEDS_SPLIT" if triggers else (
        "SIZE_OK" if estimated_calls >= 0 else "NO_ESTIMATE")

    print(f"advisory   : {verdict}   (advisory -- the Planificateur decides)")
    print(f"brief      : {brief_slug(brief_dir)}")
    print(f"estimated  : {estimated_calls if estimated_calls >= 0 else 'NOT SUPPLIED'}")

    print("\nsignals (reported, NOT triggers -- see the note in budget.py: on the 5")
    print("briefs whose real cost is known, none of these separated cheap from")
    print("expensive, and subsystem breadth pointed the wrong way):")
    print(f"  subsystems in Success Conditions : {len(subsystems)}  {subsystems}")
    print(f"  success conditions               : {conditions}"
          f"{'  (unusually many)' if conditions >= CONDITIONS_SIGNAL else ''}")
    print(f"  global-goal phrasing             : "
          f"{global_goal.group(0)!r} present" if global_goal else
          "  global-goal phrasing             : none")

    print("\nJudge these yourself, they are not counted for you:")
    print("  - are the subsystems genuinely INDEPENDENT of each other?")
    print("  - could any deliverable be validated on its own?")
    print("  - does the brief read as a global goal ('port the whole game')?")

    if verdict == "NEEDS_SPLIT":
        print("\ntriggers   :")
        for trigger in triggers:
            print(f"  - {trigger}")
        print("\n-> Split into atomic lots before generation. Each lot needs: id, "
              "objective,\n   dependencies, files/subsystems, acceptance criteria, "
              "validation command,\n   definition of done -- and is planned for a fresh "
              "session.")
    elif verdict == "NO_ESTIMATE":
        print("\n-> No verdict: pass --estimated-calls N. The estimate is the only "
              "signal\n   that has held up, so without it this check cannot conclude.")
    return EXIT_OK


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    def add_brief(p):
        p.add_argument("--brief", required=True, type=Path)
        return p

    p_status = add_brief(sub.add_parser("status"))
    p_status.add_argument("--transcripts", type=Path, default=None)
    p_status.add_argument("--json", action="store_true", dest="as_json")
    p_status.add_argument("--agent", default="",
                          help="substring of the agent transcript name, to "
                               "disambiguate when several name this brief")

    p_progress = add_brief(sub.add_parser("progress"))
    p_progress.add_argument("--kind", required=True)
    p_progress.add_argument("--evidence", required=True)
    p_progress.add_argument("--transcripts", type=Path, default=None)

    p_check = add_brief(sub.add_parser("checkpoint"))
    p_check.add_argument("--transcripts", type=Path, default=None)

    p_split = add_brief(sub.add_parser("split-check"))
    p_split.add_argument("--estimated-calls", type=int, default=-1)

    args = parser.parse_args()
    transcripts = getattr(args, "transcripts", None) or default_transcripts_dir()

    if args.command == "status":
        return cmd_status(args.brief, transcripts, args.as_json, args.agent)
    if args.command == "progress":
        return cmd_progress(args.brief, args.kind, args.evidence, transcripts)
    if args.command == "checkpoint":
        return cmd_checkpoint(args.brief, transcripts)
    if args.command == "split-check":
        return cmd_split_check(args.brief, args.estimated_calls)
    return EXIT_USAGE


if __name__ == "__main__":
    sys.exit(main())
