#!/usr/bin/env py
"""
Lot 008b's own measurement script for its four Required Counters
(pipeline_job_failed_policy_rule_count, pipeline_job_failed_handler_test_count,
pipeline_workflow_run_trigger_coverage_count,
run_31085883052_style_escalation_regression_count). Real commands, not
narrated numbers -- run this, don't trust the manifest's copy-pasted output
without re-running it (hard-won rule 5: measurement, never narration).

Usage: py harness/queue/briefs/008-full-auto-automation-gaps/deliverables/measure_pipeline_job_failed_counters.py
(run from the repo root)
"""
from __future__ import annotations

import ast
import pathlib
import re
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parents[5]
HARNESS = REPO_ROOT / "harness"
sys.path.insert(0, str(HARNESS))
from pipeline import policy_loader  # noqa: E402


def pipeline_job_failed_policy_rule_count() -> int:
    policy = policy_loader.load_auto_policy(HARNESS / "pipeline" / "auto_policy.yaml")
    matches = [r for r in policy["rules"] if r.get("event") == "pipeline_job_failed"]
    return len(matches)


def pipeline_job_failed_handler_test_count() -> tuple[int, list[str]]:
    src = (HARNESS / "tests" / "test_orchestrator.py").read_text(encoding="utf-8")
    tree = ast.parse(src)
    funcs = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name.startswith("test_")]
    hits = []
    for n in funcs:
        body = ast.get_source_segment(src, n)
        if "pipeline_job_failed" in body and "escalate_pipeline_stuck" in body:
            hits.append(n.name)
    return len(hits), hits


def pipeline_workflow_run_trigger_coverage_count() -> tuple[int, list[str], list[str]]:
    wf_path = REPO_ROOT / ".github" / "workflows" / "pipeline-failure-escalate.yml"
    text = wf_path.read_text(encoding="utf-8")
    m = re.search(r"workflow_run:\s*\n\s*workflows:\s*\n((?:\s*-\s*\S+\n)+)", text)
    names = re.findall(r"-\s*(\S+)", m.group(1)) if m else []
    existing = sorted(
        p.stem for p in (REPO_ROOT / ".github" / "workflows").glob("pipeline-*.yml")
        if p.name != "pipeline-failure-escalate.yml"
    )
    covered = [n for n in names if n in existing]
    return len(covered), sorted(covered), existing


def run_31085883052_style_escalation_regression_count() -> tuple[int, list[str]]:
    src = (HARNESS / "tests" / "test_orchestrator.py").read_text(encoding="utf-8")
    tree = ast.parse(src)
    funcs = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name.startswith("test_")]
    hits = []
    for n in funcs:
        body = ast.get_source_segment(src, n)
        if (
            "'conclusion': 'failure'" in body.replace('"', "'")
            and "'pipeline-orchestrate'" in body.replace('"', "'")
            and "escalate_pipeline_stuck" in body
        ):
            hits.append(n.name)
    return len(hits), hits


if __name__ == "__main__":
    n1 = pipeline_job_failed_policy_rule_count()
    print(f"pipeline_job_failed_policy_rule_count = {n1}")

    n2, names2 = pipeline_job_failed_handler_test_count()
    print(f"pipeline_job_failed_handler_test_count = {n2} {names2}")

    n3, covered, existing = pipeline_workflow_run_trigger_coverage_count()
    print(f"pipeline_workflow_run_trigger_coverage_count = {n3} covered={covered} existing={existing}")

    n4, names4 = run_31085883052_style_escalation_regression_count()
    print(f"run_31085883052_style_escalation_regression_count = {n4} {names4}")
