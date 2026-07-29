#!/usr/bin/env py
"""
harness/harness_audit.py -- deterministic maturity audit of ForgeHistory's
OWN harness (not of any individual brief; that's verdict_audit.py's job).

Adapted from ECC's scripts/harness-audit.js: a points-weighted, boolean,
file-existence/content-pattern rubric. No LLM judgment -- "presence is not
function" (hard-won rule 7), so every check here tests something concrete
(a file exists, a pattern is wired, a count clears a threshold), never
"does this look complete."

Usage: py harness/harness_audit.py
Exit: 0 always (this is a report, not a gate) -- read the printed score.
"""
from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

RUBRIC_VERSION = "2026-07-29"
REPO_ROOT = Path(__file__).resolve().parent.parent


@dataclass
class AuditCheck:
    name: str
    weight: int
    passed: bool
    evidence: str


def exists(*parts: str) -> bool:
    return (REPO_ROOT / Path(*parts)).exists()


def count_matches(path: Path, pattern: str) -> int:
    if not path.exists():
        return 0
    return len(re.findall(pattern, path.read_text(encoding="utf-8", errors="ignore"), re.MULTILINE))


def check_three_role_coverage() -> AuditCheck:
    agents = ["forge-planificateur.md", "forge-generateur.md", "forge-evaluateur.md"]
    missing = [a for a in agents if not exists(".claude", "agents", a)]
    return AuditCheck("three_role_agents_exist", 3, not missing,
                       f"missing: {missing}" if missing else "all 3 role agents present")


def check_mechanical_gate() -> AuditCheck:
    ok = exists("harness", "verdict_audit.py")
    return AuditCheck("mechanical_gate_exists", 3, ok, "harness/verdict_audit.py present" if ok else "missing")


def check_gate_test_coverage() -> AuditCheck:
    p = REPO_ROOT / "harness" / "tests" / "test_verdict_audit.py"
    n = count_matches(p, r"^def test_")
    ok = n >= 9  # one red-case test per check, minimum
    return AuditCheck("gate_test_coverage", 2, ok, f"{n} test functions found (need >= 9)")


def check_single_source_test() -> AuditCheck:
    ok = exists("harness", "tests", "test_single_source_of_instruction.py")
    return AuditCheck("single_source_of_instruction_test_exists", 1, ok,
                       "present" if ok else "missing")


def check_demo_pair() -> AuditCheck:
    fake_ok = exists("harness", "demo", "fake_brief_001", "run_demo.py")
    honest_ok = exists("harness", "demo", "honest_brief_001", "verdict.md")
    ran_ok = exists("harness", "demo", "fake_brief_001", "run_demo.log")
    ok = fake_ok and honest_ok and ran_ok
    missing = [n for n, v in [("fake demo script", fake_ok), ("honest demo", honest_ok),
                               ("run_demo.log (has it been run?)", ran_ok)] if not v]
    return AuditCheck("fake_honest_demo_pair", 3, ok,
                       f"missing: {missing}" if missing else "both demos present, fake demo has been run")


def check_hooks_wired() -> AuditCheck:
    p = REPO_ROOT / ".claude" / "settings.json"
    required_ids = ["pre:bash:no-bare-python", "pre:bash:guard-git-push", "pre:edit:guard-vision"]
    text = p.read_text(encoding="utf-8") if p.exists() else ""
    missing = [i for i in required_ids if i not in text]
    return AuditCheck("hooks_wired", 3, not missing,
                       f"missing hook ids: {missing}" if missing else "all 3 hooks wired in settings.json")


def check_backend_pluggability() -> AuditCheck:
    ok = exists("harness", "backends", "README.md") and exists("harness", "backends", "run_cursor_generator.sh")
    return AuditCheck("backend_pluggability", 2, ok, "backends/README.md + wrapper present" if ok else "missing")


def check_usage_ledger() -> AuditCheck:
    ok = exists("harness", "backends", "ledger.py")
    return AuditCheck("usage_ledger_exists", 1, ok, "present" if ok else "missing")


def check_docs_coverage() -> AuditCheck:
    rule_files = ["hard-won-rules.md", "simulation-principles.md", "harness-roles.md"]
    missing_rules = [f for f in rule_files if not exists("docs", "rules", f)]
    adr_dir = REPO_ROOT / "docs" / "adr"
    adr_count = len([p for p in adr_dir.glob("[0-9][0-9][0-9][0-9]-*.md")]) if adr_dir.exists() else 0
    ok = not missing_rules and adr_count >= 1
    return AuditCheck("docs_coverage", 2, ok,
                       f"missing rule files: {missing_rules}, ADR count: {adr_count}")


def check_orchestration_command() -> AuditCheck:
    ok = exists(".claude", "commands", "forge-run.md")
    return AuditCheck("orchestration_command_exists", 2, ok, "present" if ok else "missing")


def check_handoff_nontrivial() -> AuditCheck:
    p = REPO_ROOT / "HANDOFF.md"
    ok = p.exists() and len(p.read_text(encoding="utf-8")) > 200
    return AuditCheck("handoff_nontrivial", 1, ok,
                       "HANDOFF.md exists and is non-trivial" if ok else "missing or too short")


def check_no_stub_leakage() -> AuditCheck:
    leaked = []
    for d in ["sim", "pipeline/geo", "unity"]:
        dp = REPO_ROOT / d
        if not dp.exists():
            continue
        extra = [str(f.relative_to(REPO_ROOT)) for f in dp.rglob("*") if f.is_file() and f.name != "README.md"]
        leaked.extend(extra)
    return AuditCheck("no_premature_stub_content", 1, not leaked,
                       f"unexpected files in stub dirs: {leaked}" if leaked else "sim/, pipeline/geo/, unity/ contain only README.md")


def run_all() -> list[AuditCheck]:
    return [
        check_three_role_coverage(),
        check_mechanical_gate(),
        check_gate_test_coverage(),
        check_single_source_test(),
        check_demo_pair(),
        check_hooks_wired(),
        check_backend_pluggability(),
        check_usage_ledger(),
        check_docs_coverage(),
        check_orchestration_command(),
        check_handoff_nontrivial(),
        check_no_stub_leakage(),
    ]


def main() -> int:
    checks = run_all()
    total_weight = sum(c.weight for c in checks)
    earned = sum(c.weight for c in checks if c.passed)

    print(f"# ForgeHistory harness audit (rubric {RUBRIC_VERSION})")
    print(f"# {REPO_ROOT}\n")
    for c in checks:
        status = "PASS" if c.passed else "FAIL"
        print(f"[{status}] ({c.weight} pt) {c.name}: {c.evidence}")
    print(f"\nSCORE: {earned}/{total_weight}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
