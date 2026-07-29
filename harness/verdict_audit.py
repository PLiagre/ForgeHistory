#!/usr/bin/env py
"""
harness/verdict_audit.py -- Tier-1 mechanical gate. Deterministic, LLM-free.

Usage: py harness/verdict_audit.py <brief_dir>
Exit:  0 ACCEPT | 1 REJECT | 2 INTERNAL ERROR (never treated as a pass)

Operates on a brief directory containing brief.md, eval-rubric.md,
verdict.md, and deliverables/manifest.json. See docs/rules/harness-roles.md
and docs/rules/hard-won-rules.md for the rules each check enforces.
"""
from __future__ import annotations

import datetime
import hashlib
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

SENTINEL_NOT_COMPUTED = -1

_BARE_PYTHON = re.compile(r'(?<![\w./])python(?!3)\b')
_FRONTMATTER_FIELD = re.compile(r'^\*\*{label}\*\*:\s*(\S+)', re.MULTILINE)
_ISO_TS = re.compile(r'\d{4}-\d{2}-\d{2}T[\d:]+')
_FRONTMATTER_LINE = re.compile(r'^\*\*(Author|Date|Verdict)\*\*:.*$', re.MULTILINE)
_NUMBER = re.compile(r'\b\d{2,}\b')


@dataclass
class CheckResult:
    name: str
    passed: bool
    evidence: str
    applicable: bool = True


def sha256_of(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def load_json(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def read_field(p: Path, label: str) -> str | None:
    if not p.exists():
        return None
    m = re.search(_FRONTMATTER_FIELD.pattern.format(label=re.escape(label)), p.read_text(encoding="utf-8"), re.MULTILINE)
    return m.group(1) if m else None


def read_ts(p: Path, label: str = "Authored") -> datetime.datetime | None:
    v = read_field(p, label)
    if not v:
        return None
    try:
        return datetime.datetime.fromisoformat(v)
    except ValueError:
        return None


def check_files_declared_exist(bd: Path, m: dict) -> CheckResult:
    missing = [f["path"] for f in m.get("files", []) if not (bd / f["path"]).exists()]
    return CheckResult("files_declared_exist", not missing,
                        f"missing: {missing}" if missing else "all declared files present")


def check_mtime_after_brief(bd: Path, m: dict) -> CheckResult:
    brief_ts = read_ts(bd / "brief.md")
    if brief_ts is None:
        return CheckResult("mtime_after_brief", False, "brief.md missing/unparseable Authored timestamp")
    stale = []
    for f in m.get("files", []):
        p = bd / f["path"]
        if p.exists() and datetime.datetime.fromtimestamp(p.stat().st_mtime) < brief_ts:
            stale.append(f["path"])
    return CheckResult("mtime_after_brief", not stale,
                        f"predate brief.md: {stale}" if stale else "all deliverables postdate the brief")


def check_captures_differ(bd: Path, m: dict) -> CheckResult:
    bad = []
    for f in m.get("files", []):
        other = f.get("must_differ_from")
        if not other:
            continue
        p1, p2 = bd / f["path"], bd / other
        if p1.exists() and p2.exists() and sha256_of(p1) == sha256_of(p2):
            bad.append(f'{f["path"]} == {other}')
    return CheckResult("captures_differ_when_should", not bad,
                        f"identical when they should differ: {bad}" if bad else "all declared pairs differ")


def check_waivers(m: dict) -> CheckResult:
    bad = [w.get("claim", "?") for w in m.get("waivers", []) if not w.get("command") or not w.get("error")]
    return CheckResult("waivers_have_command_and_error", not bad,
                        f"missing command+error: {bad}" if bad else "all waivers carry a command and an error")


def check_no_empty_sample(m: dict) -> CheckResult:
    bad = [c["name"] for c in m.get("counters", [])
           if c.get("sample_size", SENTINEL_NOT_COMPUTED) in (0, SENTINEL_NOT_COMPUTED)]
    return CheckResult("no_empty_sample_pass", not bad,
                        f"zero/uncomputed sample_size: {bad}" if bad else "every counter has a real sample_size")


def check_verdict_numbers_traceable(bd: Path, m: dict) -> CheckResult:
    vf = bd / "verdict.md"
    if not vf.exists():
        return CheckResult("verdict_numbers_traceable", False, "verdict.md missing")
    text = vf.read_text(encoding="utf-8")
    text = _ISO_TS.sub('', text)
    text = _FRONTMATTER_LINE.sub('', text)
    cited = set(_NUMBER.findall(text))
    known = {str(c.get("value")) for c in m.get("counters", [])} | \
            {str(c.get("sample_size")) for c in m.get("counters", [])}
    untraceable = sorted(cited - known)
    return CheckResult("verdict_numbers_traceable", not untraceable,
                        f"cited but not in manifest.json: {untraceable}" if untraceable else "all cited numbers trace to manifest.json")


def check_no_bare_python(bd: Path, m: dict) -> CheckResult:
    hits = []
    for c in m.get("counters", []) + m.get("waivers", []):
        cmd = c.get("command") or ""
        if _BARE_PYTHON.search(cmd):
            hits.append(cmd)
    for pattern in ("**/*.log", "**/*.txt", "**/*.md"):
        for lf in bd.glob(pattern):
            t = lf.read_text(encoding="utf-8", errors="ignore")
            if _BARE_PYTHON.search(t):
                hits.append(str(lf.relative_to(bd)))
    return CheckResult("no_bare_python_alias", not hits,
                        f"bare `python` found in: {hits}" if hits else "no bare `python` invocations found")


def check_verdict_not_self_authored(bd: Path) -> CheckResult:
    gen = read_field(bd / "deliverables" / "generator-log.md", "Author")
    ver = read_field(bd / "verdict.md", "Author")
    if not gen or not ver:
        return CheckResult("verdict_is_not_self_authored", False, "Author frontmatter missing on generator-log.md or verdict.md")
    return CheckResult("verdict_is_not_self_authored", gen != ver,
                        f"same author on both: {gen}" if gen == ver else f"generator={gen}, evaluator={ver}")


def check_rubric_predates(bd: Path, m: dict) -> CheckResult:
    rubric_ts = read_ts(bd / "eval-rubric.md")
    if rubric_ts is None:
        return CheckResult("rubric_predates_deliverables", False, "eval-rubric.md missing/unparseable Authored timestamp")
    mtimes = [datetime.datetime.fromtimestamp((bd / f["path"]).stat().st_mtime)
              for f in m.get("files", []) if (bd / f["path"]).exists()]
    if not mtimes:
        return CheckResult("rubric_predates_deliverables", True, "no deliverables to compare against", applicable=False)
    earliest = min(mtimes)
    return CheckResult("rubric_predates_deliverables", rubric_ts <= earliest,
                        f"rubric ({rubric_ts}) written after earliest deliverable ({earliest})"
                        if rubric_ts > earliest else f"rubric ({rubric_ts}) predates earliest deliverable ({earliest})")


def run_all_checks(bd: Path) -> list[CheckResult]:
    m = load_json(bd / "deliverables" / "manifest.json") or {}
    return [
        check_files_declared_exist(bd, m),
        check_mtime_after_brief(bd, m),
        check_captures_differ(bd, m),
        check_waivers(m),
        check_no_empty_sample(m),
        check_verdict_numbers_traceable(bd, m),
        check_no_bare_python(bd, m),
        check_verdict_not_self_authored(bd),
        check_rubric_predates(bd, m),
    ]


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: py verdict_audit.py <brief_dir>", file=sys.stderr)
        return 2
    bd = Path(sys.argv[1])
    if not bd.is_dir():
        print(f"ERROR: {bd} is not a directory", file=sys.stderr)
        return 2

    try:
        checks = run_all_checks(bd)
    except Exception as e:  # noqa: BLE001 -- audit failure must be loud, never silent
        print(f"ERROR: audit itself failed: {e}", file=sys.stderr)
        return 2

    overall = all(c.passed for c in checks if c.applicable)
    print(f"# verdict_audit report for {bd}")
    print(f"# generated_at: {datetime.datetime.now().isoformat()}")
    for c in checks:
        status = "N/A" if not c.applicable else ("PASS" if c.passed else "FAIL")
        print(f"[{status}] {c.name}: {c.evidence}")
    print()
    print("VERDICT: ACCEPT" if overall else "VERDICT: REJECT")
    return 0 if overall else 1


if __name__ == "__main__":
    sys.exit(main())
