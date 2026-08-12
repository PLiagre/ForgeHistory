#!/usr/bin/env py
"""Refuse un run Codex si son auteur hypothétique auto-jugerait le brief."""
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import sys
import tempfile


HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
import verdict_audit  # noqa: E402


CODEX_GENERATOR_AUTHOR = "forge-generateur-codex"


def check(brief_dir: Path) -> tuple[bool, str]:
    """Réutilise le contrôle SC3 sur l'auteur que le wrapper ajouterait."""
    verdict = brief_dir / "verdict.md"
    if not verdict.exists() or not verdict_audit.read_all_fields(verdict, "Author"):
        return True, "aucun verdict signé existant"

    with tempfile.TemporaryDirectory(prefix="forge-codex-preflight-") as temp:
        fixture = Path(temp)
        deliverables = fixture / "deliverables"
        deliverables.mkdir()

        source_log = brief_dir / "deliverables" / "generator-log.md"
        if source_log.exists():
            shutil.copyfile(source_log, deliverables / "generator-log.md")
        else:
            (deliverables / "generator-log.md").write_text("", encoding="utf-8")
        with (deliverables / "generator-log.md").open("a", encoding="utf-8") as handle:
            handle.write(f"\n**Author**: {CODEX_GENERATOR_AUTHOR}\n")
        shutil.copyfile(verdict, fixture / "verdict.md")

        result = verdict_audit.check_verdict_not_self_authored(fixture)
    return result.passed, result.evidence


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("brief_dir", type=Path)
    args = parser.parse_args(argv)
    brief_dir = args.brief_dir.resolve()
    if not brief_dir.is_dir():
        print(f"ERROR: brief directory not found: {brief_dir}", file=sys.stderr)
        return 2

    passed, evidence = check(brief_dir)
    if not passed:
        print(
            "REFUSING TO RUN: a forge-generateur-codex section would be "
            f"self-authored against the existing verdict ({evidence}).",
            file=sys.stderr,
        )
        return 2
    print(f"PREFLIGHT OK: {evidence}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
