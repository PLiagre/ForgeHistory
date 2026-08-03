#!/usr/bin/env py
"""
harness/backends/ledger.py -- honest, minimal usage ledger for the
Générateur role's backend split (Claude vs Cursor).

Why not ECC's cost-tracking skill directly: that skill reads
~/.claude/metrics/costs.jsonl, written by ECC's own `stop:cost-tracker`
hook -- which is tightly coupled to ECC's plugin-root resolution machinery
and only tracks Claude Code's own dollar cost, never Cursor's (Cursor has
no equivalent local log this session can read). Porting that machinery
would add a real dependency for a metric it can't even fully answer.

What this tracks instead, and why it's honest: invocation counts per
backend per brief -- a real, deterministic, directly-observable proxy for
"is spend actually being spread across Claude and Cursor," which is the
project owner's actual goal. It does not claim to know dollar costs it
cannot measure.

The `tokens` subcommand extends that with the one thing that IS directly
observable: Claude Code writes a full per-request usage record to its own
session transcripts (~/.claude/projects/<slug>/*.jsonl, plus one file per
subagent under <session-id>/subagents/). Reading those gives real token
counts per role, per brief, per agent -- measured, not estimated. The
Cursor backend has no equivalent local log in this environment, so it is
reported as "not observable" rather than folded into a total that would
then be a lie. Same principle as above: report what is measurable, name
what is not.

Usage:
  py harness/backends/ledger.py append --backend claude|cursor --brief <dir> [--event NAME]
  py harness/backends/ledger.py report
  py harness/backends/ledger.py tokens [--transcripts DIR] [--top N] [--json]
"""
from __future__ import annotations

import argparse
import datetime
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import transcripts as transcripts_mod

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
LEDGER_PATH = REPO_ROOT / "harness" / "queue" / "cost-ledger.jsonl"

# --- transcript reading -------------------------------------------------
#
# Published Anthropic API list prices, USD per million tokens, in the order
# (input, cache_write_5m, cache_read, output). Cache multipliers are the
# documented x1.25 / x0.1 of base input price.
#
# This table is a STATED ASSUMPTION, not a measurement: the transcripts
# record tokens, never prices, and a Claude Code subscription is not billed
# per token at all. A model absent from this table is counted in tokens and
# listed as unpriced -- never silently priced at zero.
PRICES_AS_OF = "2026-08-03"
PRICES: dict[str, tuple[float, float, float, float]] = {
    "claude-opus-5": (5.0, 6.25, 0.50, 25.0),
    "claude-fable-5": (10.0, 12.50, 1.00, 50.0),
    "claude-sonnet-5": (3.0, 3.75, 0.30, 15.0),
    "claude-haiku-4-5": (1.0, 1.25, 0.10, 5.0),
}

BRIEF_RE = re.compile(r"harness/queue/briefs/([0-9]{3}-[a-z0-9-]+)")
# How many leading transcript lines to scan for the brief path. The brief
# directory is named in the agent's own spawning prompt, so it appears in
# the first user turn -- this is direct evidence from the agent's input,
# not inference from wall-clock proximity to a ledger entry.
BRIEF_SCAN_LINES = 4


def append_entry(backend: str, brief: str, event: str) -> None:
    entry = {
        "timestamp": datetime.datetime.now().isoformat(),
        "backend": backend,
        "brief": brief,
        "event": event,
    }
    LEDGER_PATH.parent.mkdir(parents=True, exist_ok=True)
    with LEDGER_PATH.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry) + "\n")
    print(f"logged: {entry}")


def load_entries() -> list[dict]:
    if not LEDGER_PATH.exists():
        return []
    entries = []
    for line in LEDGER_PATH.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            entries.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return entries


def report() -> None:
    entries = load_entries()
    if not entries:
        print(f"No usage logged yet (ledger not found or empty: {LEDGER_PATH}).")
        return

    by_backend: dict[str, int] = {}
    by_brief: dict[str, dict[str, int]] = {}
    for e in entries:
        backend = e.get("backend", "(unknown)")
        brief = e.get("brief", "(unknown)")
        by_backend[backend] = by_backend.get(backend, 0) + 1
        by_brief.setdefault(brief, {})
        by_brief[brief][backend] = by_brief[brief].get(backend, 0) + 1

    print(f"=== Générateur invocation counts ({len(entries)} logged runs) ===")
    print("\nBy backend:")
    for backend, count in sorted(by_backend.items(), key=lambda kv: -kv[1]):
        print(f"  {count:4d}  {backend}")

    print("\nBy brief:")
    for brief, counts in sorted(by_brief.items()):
        parts = ", ".join(f"{k}={v}" for k, v in sorted(counts.items()))
        print(f"  {brief}: {parts}")


def default_transcripts_dir() -> Path:
    """Claude Code's transcript directory for this repo.

    Claude Code slugifies the project's absolute path by replacing every
    non-alphanumeric character with '-', so `D:\\ForgeHistory` becomes
    `D--ForgeHistory`.
    """
    slug = re.sub(r"[^A-Za-z0-9]", "-", str(REPO_ROOT))
    return Path.home() / ".claude" / "projects" / slug


def _empty_counts() -> dict[str, int]:
    return {"calls": 0, "in": 0, "cache_write": 0, "cache_read": 0, "out": 0}


def _add(dst: dict[str, int], src: dict[str, int]) -> None:
    for key, value in src.items():
        dst[key] = dst.get(key, 0) + value


def scan_transcript(path: Path) -> dict[str, dict[str, int]]:
    """Per-model token counters for one transcript.

    Delegates to harness/transcripts.py. That module documents why: the
    transcript writes one record per content block, all sharing
    (message.id, requestId), and `output_tokens` is cumulative across the
    group. This function used to keep the FIRST record of each group and so
    undercounted output ~3x (56,524 vs 177,494 on one measured agent). Input
    figures repeat identically within a group, so cache_read totals -- which
    dominate this workload -- were unaffected.
    """
    return transcripts_mod.per_model_usage(path)


def brief_of(path: Path) -> str | None:
    """The brief slug named in an agent transcript's own spawning prompt."""
    return transcripts_mod.brief_of(path)


def collect_units(root: Path) -> list[dict]:
    """One entry per transcript: the main session thread, or one subagent."""
    units: list[dict] = []
    for session_file in sorted(root.glob("*.jsonl")):
        session_id = session_file.stem
        units.append(
            {
                "session": session_id,
                "role": "(session principale)",
                "brief": None,
                "name": session_id[:8],
                "models": scan_transcript(session_file),
            }
        )
        for agent_file in sorted((root / session_id / "subagents").glob("agent-*.jsonl")):
            meta_file = agent_file.with_suffix("").with_suffix(".meta.json")
            role = "(unknown agent)"
            if meta_file.exists():
                try:
                    role = json.loads(meta_file.read_text(encoding="utf-8")).get(
                        "agentType", role
                    )
                except (json.JSONDecodeError, OSError):
                    pass
            units.append(
                {
                    "session": session_id,
                    "role": role,
                    "brief": brief_of(agent_file),
                    "name": agent_file.stem,
                    "models": scan_transcript(agent_file),
                }
            )
    return units


def price_of(model: str, counts: dict[str, int]) -> float | None:
    """USD at list price, or None when the model has no published entry."""
    rates = PRICES.get(model)
    if rates is None:
        return None
    return (
        counts["in"] * rates[0]
        + counts["cache_write"] * rates[1]
        + counts["cache_read"] * rates[2]
        + counts["out"] * rates[3]
    ) / 1e6


def summarize(unit: dict) -> dict:
    """Fold a unit's per-model counters into totals + a priced/unpriced split."""
    totals = _empty_counts()
    usd = 0.0
    unpriced: set[str] = set()
    for model, counts in unit["models"].items():
        _add(totals, counts)
        cost = price_of(model, counts)
        if cost is None:
            # A model with no tokens at all (e.g. "<synthetic>" bookkeeping
            # entries) is not a pricing gap worth reporting.
            if any(counts[k] for k in ("in", "cache_write", "cache_read", "out")):
                unpriced.add(model)
        else:
            usd += cost
    totals["usd"] = usd
    totals["unpriced"] = sorted(unpriced)
    totals["context_in"] = totals["in"] + totals["cache_write"] + totals["cache_read"]
    return totals


def tokens_report(root: Path, top: int, as_json: bool) -> int:
    if not root.is_dir():
        print(f"No Claude transcripts found at: {root}")
        print("Nothing measured. Pass --transcripts <dir> if they live elsewhere.")
        return 1

    units = [u for u in collect_units(root) if any(u["models"].values())]
    units = [u for u in units if summarize(u)["calls"]]
    if not units:
        print(f"Transcript directory exists but holds no usage records: {root}")
        return 1

    rows = [(u, summarize(u)) for u in units]
    grand = _empty_counts()
    grand_usd = 0.0
    unpriced: set[str] = set()
    for _, s in rows:
        _add(grand, {k: s[k] for k in _empty_counts()})
        grand_usd += s["usd"]
        unpriced.update(s["unpriced"])

    def bucket(key) -> dict[str, dict]:
        out: dict[str, dict] = {}
        for unit, s in rows:
            name = key(unit)
            if name is None:
                continue
            acc = out.setdefault(name, {**_empty_counts(), "usd": 0.0})
            _add(acc, {k: s[k] for k in _empty_counts()})
            acc["usd"] += s["usd"]
        return out

    by_role = bucket(lambda u: u["role"])
    by_brief = bucket(lambda u: u["brief"])
    by_session = bucket(lambda u: u["session"])

    if as_json:
        print(
            json.dumps(
                {
                    "transcripts": str(root),
                    "prices_as_of": PRICES_AS_OF,
                    "total": {**grand, "usd": round(grand_usd, 2)},
                    "unpriced_models": sorted(unpriced),
                    "by_role": by_role,
                    "by_brief": by_brief,
                    "by_session": by_session,
                    "units": [
                        {
                            "name": u["name"],
                            "session": u["session"],
                            "role": u["role"],
                            "brief": u["brief"],
                            **s,
                        }
                        for u, s in rows
                    ],
                },
                indent=2,
                default=str,
            )
        )
        return 0

    context_in = grand["in"] + grand["cache_write"] + grand["cache_read"]
    print(f"=== Claude token usage ({root}) ===")
    print(f"{len(rows)} transcripts  |  {grand['calls']:,} API calls")
    print(
        f"input {context_in:,} tok "
        f"(cache-read {grand['cache_read']:,} = "
        f"{100 * grand['cache_read'] / max(context_in, 1):.1f}%)  |  "
        f"output {grand['out']:,} tok"
    )
    print(f"~ ${grand_usd:,.2f} at published list prices ({PRICES_AS_OF}).")
    print(
        "   Sonnet 5 is priced here at its standard $3/$15 rate; the\n"
        "   introductory $2/$10 rate in effect through 2026-08-31 makes the\n"
        "   real figure lower. A Claude Code subscription is not billed per\n"
        "   token at all -- read this as relative weight, not an invoice."
    )

    def table(title: str, data: dict[str, dict], label: str) -> None:
        if not data:
            return
        print(f"\nBy {title}:")
        print(f"  {label:<28} {'USD':>9} {'calls':>7} {'mean ctx/call':>14}")
        for name, acc in sorted(data.items(), key=lambda kv: -kv[1]["usd"]):
            ctx = acc["in"] + acc["cache_write"] + acc["cache_read"]
            mean = ctx // max(acc["calls"], 1)
            print(f"  {name:<28} {acc['usd']:>9,.2f} {acc['calls']:>7,} {mean:>14,}")

    table("role", by_role, "role")
    table("brief", by_brief, "brief")
    table("session", by_session, "session")

    if top:
        print(f"\nTop {top} transcripts by cost (mean ctx/call is the lever):")
        print(f"  {'transcript':<22} {'role':<20} {'USD':>9} {'calls':>7} {'mean ctx/call':>14}")
        for unit, s in sorted(rows, key=lambda r: -r[1]["usd"])[:top]:
            mean = s["context_in"] // max(s["calls"], 1)
            print(
                f"  {unit['name'][:22]:<22} {unit['role'][:20]:<20} "
                f"{s['usd']:>9,.2f} {s['calls']:>7,} {mean:>14,}"
            )

    if unpriced:
        print("\nUnpriced models (tokens counted, USD excluded from the total):")
        for model in sorted(unpriced):
            print(f"  {model}")

    cursor_runs = sum(
        1 for e in load_entries() if e.get("backend") == "cursor"
    )
    print(
        f"\nCursor backend: {cursor_runs} logged Générateur run(s), token cost "
        "NOT observable from this environment -- excluded from the total above, "
        "not assumed to be zero."
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    p_append = sub.add_parser("append")
    p_append.add_argument("--backend", required=True, choices=["claude", "cursor"])
    p_append.add_argument("--brief", required=True)
    p_append.add_argument("--event", default="generator-run")

    sub.add_parser("report")

    p_tokens = sub.add_parser("tokens")
    p_tokens.add_argument(
        "--transcripts",
        type=Path,
        default=None,
        help="Claude transcript dir (default: ~/.claude/projects/<repo slug>)",
    )
    p_tokens.add_argument("--top", type=int, default=10)
    p_tokens.add_argument("--json", action="store_true", dest="as_json")

    args = parser.parse_args()

    if args.command == "append":
        append_entry(args.backend, args.brief, args.event)
    elif args.command == "report":
        report()
    elif args.command == "tokens":
        return tokens_report(
            args.transcripts or default_transcripts_dir(), args.top, args.as_json
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
