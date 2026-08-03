#!/usr/bin/env py
"""
harness/transcripts.py -- the one reader for Claude Code session transcripts.

Exists because getting this wrong is not hypothetical: `budget.py` and
`backends/ledger.py` each grew their own copy of the parsing, and the copy
was wrong in a way that made the execution budget inert. An external audit
caught it; this module is the single place it can be wrong again.

## The transcript shape, measured rather than assumed

Claude Code writes **one JSONL record per content block**, and every record
of the same assistant turn repeats the same `(message.id, requestId)`. A
turn that emitted a thinking block and two tool calls is three records.

Verified on `agent-ac1e1d121a9a32b1b.jsonl` (Générateur, brief 003):

    assistant records                     1857
    tool_use blocks, no dedup             1015
    tool_result blocks (independent xref) 1015   <- agrees
    distinct (message.id, requestId)       982
    tool_use blocks after that dedup       186   <- the bug

So one rule cannot serve both counts:

  * **tool calls** -- count every `tool_use` block, across all records.
    Deduplicating by message drops the sibling records that carry the other
    calls, and undercounts by ~5.5x. The `tool_result` count is a free
    cross-check: they must match.

  * **token usage** -- one API request is billed once, so usage must be
    taken per `(message.id, requestId)` group, not summed across records.
    Within a group the input figures repeat identically (cache_read is
    stable under first/last/max), but `output_tokens` is cumulative and
    only the LAST record carries the final value. Taking the first
    undercounts output ~3x (56,524 vs 177,494 on the agent above).

Input dominates so heavily in this workload that the output error barely
moves a dollar total -- but "barely moves the total" is not a reason to
report a number that is wrong by 3x.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

BRIEF_RE = re.compile(r"harness/queue/briefs/([0-9]{3}-[a-z0-9-]+)")
# The brief directory is named in the agent's spawning prompt, so it appears
# in the first user turn. Direct evidence from the agent's input, not an
# inference from wall-clock proximity to a ledger entry.
BRIEF_SCAN_LINES = 4


def default_transcripts_dir() -> Path:
    """Claude Code slugifies the project path, replacing every
    non-alphanumeric character with '-': D:\\ForgeHistory -> D--ForgeHistory."""
    slug = re.sub(r"[^A-Za-z0-9]", "-", str(REPO_ROOT))
    return Path.home() / ".claude" / "projects" / slug


def _records(path: Path):
    try:
        handle = path.open(encoding="utf-8", errors="replace")
    except OSError:
        return
    with handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError:
                continue


def count_tool_calls(path: Path) -> tuple[int, int]:
    """(tool_use blocks, tool_result blocks) -- every record, no dedup.

    The two are returned together on purpose: they are produced by different
    sides of the conversation, so a mismatch means the parse drifted from
    the format. Callers should treat a large gap as a reason to distrust the
    number rather than to report it.
    """
    tool_use = 0
    tool_result = 0
    for record in _records(path):
        kind = record.get("type")
        blocks = (record.get("message") or {}).get("content") or []
        if not isinstance(blocks, list):
            continue
        for block in blocks:
            if not isinstance(block, dict):
                continue
            if kind == "assistant" and block.get("type") == "tool_use":
                tool_use += 1
            elif kind == "user" and block.get("type") == "tool_result":
                tool_result += 1
    return tool_use, tool_result


def usage_by_request(path: Path) -> list[tuple[str, dict]]:
    """One usage record per billed API request, in order.

    Groups by `(message.id, requestId)` and keeps the LAST record's usage --
    output_tokens is cumulative across the group; input figures are stable.
    """
    order: list[tuple] = []
    latest: dict[tuple, tuple[str, dict]] = {}
    for record in _records(path):
        if record.get("type") != "assistant":
            continue
        message = record.get("message") or {}
        usage = message.get("usage") or {}
        if not usage:
            continue
        key = (message.get("id"), record.get("requestId"))
        if key not in latest:
            order.append(key)
        latest[key] = (message.get("model", "(unknown)"), usage)
    return [latest[key] for key in order]


def per_model_usage(path: Path) -> dict[str, dict[str, int]]:
    """Token counters per model for one transcript."""
    per_model: dict[str, dict[str, int]] = {}
    for model, usage in usage_by_request(path):
        counts = per_model.setdefault(model, {
            "calls": 0, "in": 0, "cache_write": 0, "cache_read": 0, "out": 0,
        })
        counts["calls"] += 1
        counts["in"] += usage.get("input_tokens", 0)
        counts["cache_write"] += usage.get("cache_creation_input_tokens", 0)
        counts["cache_read"] += usage.get("cache_read_input_tokens", 0)
        counts["out"] += usage.get("output_tokens", 0)
    return per_model


def brief_of(path: Path) -> str | None:
    """The brief slug named in an agent transcript's own spawning prompt."""
    for index, record in enumerate(_records(path)):
        if index >= BRIEF_SCAN_LINES:
            break
        match = BRIEF_RE.search(json.dumps(record))
        if match:
            return match.group(1)
    return None


def agent_transcripts_for(slug: str, transcripts: Path) -> list[Path]:
    """Every agent transcript whose spawning prompt names this brief.

    Returns all of them, newest first, rather than picking one. Choosing by
    mtime is what produced a false `OK` on the most expensive brief in the
    repo: the newest transcript naming brief 003 was a 4-request agent, so
    the budget reported 0 tool calls for a 1,015-call run. Callers must
    decide explicitly what to do with ambiguity -- and "report the smallest
    candidate as if it were the whole run" is not one of the options.
    """
    if not transcripts.is_dir():
        return []
    found = [path for path in transcripts.glob("*/subagents/agent-*.jsonl")
             if brief_of(path) == slug]
    return sorted(found, key=lambda p: p.stat().st_mtime, reverse=True)
