from __future__ import annotations

import importlib
import json
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
HARNESS_ROOT = REPO_ROOT / "harness"
if str(HARNESS_ROOT) not in sys.path:
    sys.path.insert(0, str(HARNESS_ROOT))

import workflow_risk_gate as gate


def _policy_text(
    *,
    r0: str = '["docs/operations/**"]',
    r2: str = '[".github/**", "control-plane/**", "pipeline/geo/**/*g6*"]',
    r2_profile: str = "certify",
) -> str:
    return f"""
[policy]
version = 1

[classification]
r0_allowlist = {r0}
r2_paths = {r2}

[risks.R0]
test_profile = "fast"
[risks.R1]
test_profile = "pr"
[risks.R2]
test_profile = "{r2_profile}"
"""


def _write_policy(tmp_path: Path, **kwargs: str) -> Path:
    path = tmp_path / "policy.toml"
    path.write_text(_policy_text(**kwargs), encoding="utf-8")
    return path


def test_r2_path_cannot_be_lowered(tmp_path):
    policy = gate.load_policy(tmp_path, _write_policy(tmp_path))
    result = gate.evaluate(policy, [".github/workflows/ci.yml"], "R1")
    assert result["accepted"] is False
    assert result["derived_risk"] == "R2"
    assert result["effective_risk"] == "R2"
    assert result["test_profile"] == "certify"


def test_r0_requires_every_path_to_match_the_narrow_allowlist(tmp_path):
    policy = gate.load_policy(tmp_path, _write_policy(tmp_path))
    assert gate.derive_risk(policy, ["docs/operations/runbook.md"]) == "R0"
    assert (
        gate.derive_risk(
            policy,
            ["docs/operations/runbook.md", "docs/product-note.md"],
        )
        == "R1"
    )


def test_unknown_path_is_r1_not_r0(tmp_path):
    policy = gate.load_policy(tmp_path, _write_policy(tmp_path))
    assert gate.derive_risk(policy, ["README.md"]) == "R1"


@pytest.mark.parametrize(
    "content, expected",
    [
        ("[policy]\nversion=1\n", "classification"),
        (_policy_text(r0="[]"), "r0_allowlist"),
        (_policy_text(r2="[]"), "r2_paths"),
        (_policy_text(r2_profile="overnight"), "test_profile"),
    ],
)
def test_invalid_policy_fails_closed(tmp_path, content, expected):
    path = tmp_path / "invalid.toml"
    path.write_text(content, encoding="utf-8")
    with pytest.raises(gate.RiskGateError, match=expected):
        gate.load_policy(tmp_path, path)


def test_policy_override_environment_is_honoured(tmp_path, monkeypatch):
    path = _write_policy(tmp_path)
    monkeypatch.setenv(gate.POLICY_ENV, str(path))
    loaded = gate.load_policy(tmp_path)
    assert loaded.path == path.resolve()


@pytest.mark.parametrize(
    "path",
    ["../secret", "/etc/passwd", "a/../../secret", "C:\\secret", ""],
)
def test_unsafe_or_empty_changed_path_is_refused(path):
    with pytest.raises(gate.RiskGateError):
        gate.normalize_paths([path])


def test_pr_declaration_is_unique_and_explicit():
    assert gate.parse_declared_risk("Titre\n\nForge-Risk: R2\n") == "R2"
    with pytest.raises(gate.RiskGateError, match="sans ligne unique"):
        gate.parse_declared_risk("Risque élevé")
    with pytest.raises(gate.RiskGateError, match="plusieurs"):
        gate.parse_declared_risk("Forge-Risk: R1\nForge-Risk: R2\n")


def test_cli_emits_json_and_nonzero_when_declared_risk_is_too_low(
    tmp_path, capsys
):
    policy = _write_policy(tmp_path)
    code = gate.main(
        [
            "--repo",
            str(tmp_path),
            "--policy",
            str(policy),
            "--declared-risk",
            "R0",
            "--path",
            "control-plane/config.toml",
        ]
    )
    payload = json.loads(capsys.readouterr().out)
    assert code == 2
    assert payload["accepted"] is False
    assert payload["derived_risk"] == "R2"


def test_changed_paths_are_rebuilt_from_the_exact_git_range(tmp_path):
    def git(*args):
        completed = subprocess.run(
            ["git", *args],
            cwd=tmp_path,
            text=True,
            capture_output=True,
            check=True,
        )
        return completed.stdout.strip()

    git("init", "-q")
    git("config", "user.name", "risk-test")
    git("config", "user.email", "risk-test@example.invalid")
    first = tmp_path / "docs" / "operations" / "runbook.md"
    first.parent.mkdir(parents=True)
    first.write_text("v1\n", encoding="utf-8")
    git("add", "docs/operations/runbook.md")
    git("commit", "-qm", "base")
    base = git("rev-parse", "HEAD")
    changed = tmp_path / ".github" / "workflows" / "ci.yml"
    changed.parent.mkdir(parents=True)
    changed.write_text("name: ci\n", encoding="utf-8")
    git("add", ".github/workflows/ci.yml")
    git("commit", "-qm", "head")
    head = git("rev-parse", "HEAD")

    assert gate.changed_paths(tmp_path, base, head) == (
        ".github/workflows/ci.yml",
    )


def test_changed_paths_keep_both_sides_of_a_rename(tmp_path):
    def git(*args):
        completed = subprocess.run(
            ["git", *args],
            cwd=tmp_path,
            text=True,
            capture_output=True,
            check=True,
        )
        return completed.stdout.strip()

    git("init", "-q")
    git("config", "user.name", "risk-test")
    git("config", "user.email", "risk-test@example.invalid")
    source = tmp_path / "SECURITY.md"
    source.write_text("secret governance\n", encoding="utf-8")
    git("add", "SECURITY.md")
    git("commit", "-qm", "base")
    base = git("rev-parse", "HEAD")
    destination = tmp_path / "docs" / "operations" / "security.md"
    destination.parent.mkdir(parents=True)
    source.rename(destination)
    git("add", "--all")
    git("commit", "-qm", "rename")
    head = git("rev-parse", "HEAD")

    assert gate.changed_paths(tmp_path, base, head) == (
        "SECURITY.md",
        "docs/operations/security.md",
    )


def test_harness_derivation_matches_authoritative_forgepilot_loader():
    """Une divergence entre les deux consommateurs doit rougir immédiatement."""

    control_plane = REPO_ROOT / "control-plane"
    sys.path.insert(0, str(control_plane))
    try:
        policy_module = importlib.import_module("forgepilot.policy")
        authoritative = policy_module.load_policy(
            control_plane / "workflow-policy.toml"
        )
    finally:
        sys.path.remove(str(control_plane))

    local = gate.load_policy(REPO_ROOT)
    cases = (
        ["docs/operations/forgepilot-hosting.md"],
        ["README.md"],
        [".github/workflows/harness-ci.yml"],
        ["pipeline/geo/tests/test_g6_acceleration.py"],
        ["docs/operations/a.md", "sim/world.py"],
        ["root.geojson"],
    )
    for paths in cases:
        assert gate.derive_risk(local, paths) == policy_module.derive_risk(
            authoritative, paths
        )
