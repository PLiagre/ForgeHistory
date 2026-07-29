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

Usage:
  py harness/backends/ledger.py append --backend claude|cursor --brief <dir> [--event NAME]
  py harness/backends/ledger.py report
"""
from __future__ import annotations

import argparse
import datetime
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
LEDGER_PATH = REPO_ROOT / "harness" / "queue" / "cost-ledger.jsonl"


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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    p_append = sub.add_parser("append")
    p_append.add_argument("--backend", required=True, choices=["claude", "cursor"])
    p_append.add_argument("--brief", required=True)
    p_append.add_argument("--event", default="generator-run")

    sub.add_parser("report")

    args = parser.parse_args()

    if args.command == "append":
        append_entry(args.backend, args.brief, args.event)
    elif args.command == "report":
        report()

    return 0


if __name__ == "__main__":
    sys.exit(main())
