#!/usr/bin/env py
"""Mesure les PR fusionnées récentes contre le vrai workflow merge-bot."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys


REPO_ROOT = Path(__file__).resolve().parents[6]
sys.path.insert(0, str(REPO_ROOT))

from harness.merge_bot_policy import DEFAULT_WORKFLOW, load_merge_bot_policy  # noqa: E402


def _gh_json(arguments: list[str]):
    completed = subprocess.run(
        ["gh", *arguments],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return json.loads(completed.stdout)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit", type=int, default=20)
    parser.add_argument("--workflow", type=Path, default=DEFAULT_WORKFLOW)
    args = parser.parse_args(argv)
    if args.limit <= 0:
        parser.error("--limit doit être positif")

    policy = load_merge_bot_policy(args.workflow)
    prs = _gh_json(
        [
            "pr",
            "list",
            "--state",
            "merged",
            "--limit",
            str(args.limit),
            "--json",
            "number,headRefName,mergedAt,title,url",
        ]
    )

    automergeable = 0
    print(f"requested={args.limit}")
    print(f"returned={len(prs)}")
    print("branch_prefixes=" + json.dumps(policy.branch_prefixes, ensure_ascii=False))
    print(
        "allowed_path_prefixes="
        + json.dumps(policy.allowed_path_prefixes, ensure_ascii=False)
    )

    for pr in prs:
        detail = _gh_json(["pr", "view", str(pr["number"]), "--json", "files"])
        paths = [item["path"] for item in detail["files"]]
        reasons = policy.refusal_reasons(pr["headRefName"], paths)
        compact_reasons = []
        for reason in reasons:
            if ": " not in reason:
                compact_reasons.append(reason)
                continue
            label, items = reason.split(": ", 1)
            compact_reasons.append(f"{label} ({len(items.split(', '))} chemin(s))")
        accepted = not reasons
        automergeable += int(accepted)
        result = {
            "number": pr["number"],
            "head": pr["headRefName"],
            "changed_paths": len(paths),
            "automergeable": accepted,
            "reasons": compact_reasons,
        }
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))

    print(f"recent_prs_automergeable_count={automergeable}")
    print(f"sample_size={len(prs)}")
    if len(prs) < args.limit:
        print(
            "cohort_note="
            f"GitHub ne contient que {len(prs)} PR fusionnées; "
            f"le dénominateur demandé {args.limit} n'existe pas encore."
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
