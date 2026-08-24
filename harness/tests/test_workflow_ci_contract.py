from __future__ import annotations

import re
from collections import Counter
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = REPO_ROOT / ".github" / "workflows"
PORTABLE = (
    "harness-ci.yml",
    "forgepilot-ci.yml",
    "audit-guard.yml",
    "security.yml",
)
REQUIRED_JOBS = {
    "harness-tests",
    "sim-tests",
    "forgepilot-tests",
    "audit-schema",
    "audit-check",
    "actionlint",
    "gitleaks",
    "risk-gate",
}


def _read(name: str) -> str:
    return (WORKFLOWS / name).read_text(encoding="utf-8")


def _trigger_block(text: str) -> str:
    match = re.search(r"(?ms)^on:\s*\n(.*?)(?=^[a-zA-Z][\w-]*:\s*(?:#.*)?$)", text)
    assert match is not None, "bloc on: introuvable"
    return match.group(1)


def _job_ids(text: str) -> list[str]:
    jobs = text.split("\njobs:\n", 1)
    assert len(jobs) == 2, "bloc jobs: introuvable"
    return re.findall(r"(?m)^  ([a-zA-Z][\w-]*):\s*$", jobs[1])


def test_portable_workflows_run_on_pr_and_only_push_to_master():
    for name in PORTABLE:
        triggers = _trigger_block(_read(name))
        assert re.search(r"(?m)^  pull_request:\s*$", triggers), name
        assert re.search(
            r"(?m)^  push:\s*\n    branches:\s*\[master\]\s*$", triggers
        ), name
        assert not re.search(r"(?m)^    branches:\s*\[(?!master\])", triggers), name


def test_required_check_names_are_unique_and_unambiguous():
    counts: Counter[str] = Counter()
    for path in WORKFLOWS.glob("*.yml"):
        counts.update(_job_ids(path.read_text(encoding="utf-8")))
    assert {name: counts[name] for name in REQUIRED_JOBS} == {
        name: 1 for name in REQUIRED_JOBS
    }
    assert counts["tests"] == 0
    assert counts["schema"] == 0


def test_forgepilot_required_check_is_not_skipped_by_path_filters():
    triggers = _trigger_block(_read("forgepilot-ci.yml"))
    assert "paths:" not in triggers


def test_risk_gate_is_explicitly_out_of_the_pr_path():
    workflow = _read("harness-ci.yml")
    assert "Porte de risque hors chemin PR (ADR-0018)" in workflow
    assert "harness/workflow_risk_gate.py" not in workflow


def test_audit_check_is_explicitly_out_of_the_pr_path():
    workflow = _read("audit-guard.yml")
    assert "Garde d'audits historiques hors chemin PR (ADR-0018)" in workflow
    assert "pr_audit_guard.py check" not in workflow


def test_public_pr_workflows_never_use_a_self_hosted_runner():
    for path in WORKFLOWS.glob("*.yml"):
        text = path.read_text(encoding="utf-8").lower()
        assert "runs-on: self-hosted" not in text, path.name
        assert "runs-on: [self-hosted" not in text, path.name


def test_cursor_scope_applies_only_to_inbox_deposits():
    workflow = _read("audit-guard.yml")
    assert "startsWith(github.head_ref, 'cursor/')" in workflow
    assert "aucun fichier sous architecture/inbox/" in workflow
    assert "grep -v '^architecture/inbox/'" in workflow


def test_legacy_full_auto_remains_disabled():
    config = (REPO_ROOT / "harness" / "pipeline" / "config.yaml").read_text(
        encoding="utf-8"
    )
    assert re.search(r"(?m)^mode:\s*manual\s*$", config)
    assert not re.search(r"(?m)^mode:\s*full_auto\s*$", config)


def test_cursorignore_excludes_only_named_heavy_outputs():
    ignored = (REPO_ROOT / ".cursorignore").read_text(encoding="utf-8")
    assert "unity/game_unity/Captures/" in ignored
    assert "pipeline/geo/capture/" in ignored
    assert "harness/queue/briefs/003-port-unity-game/deliverables/evidence/" in ignored
    entries = {
        line.strip()
        for line in ignored.splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    }
    forbidden = {
        "sim/",
        "pipeline/geo/",
        "pipeline/geo/tests/",
        "control-plane/",
        "docs/rules/",
        "harness/queue/briefs/029-workflow-acceleration/",
    }
    assert entries.isdisjoint(forbidden)
