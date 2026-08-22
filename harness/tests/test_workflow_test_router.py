from __future__ import annotations

import subprocess
import sys
import importlib.util
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
HARNESS_ROOT = REPO_ROOT / "harness"
if str(HARNESS_ROOT) not in sys.path:
    sys.path.insert(0, str(HARNESS_ROOT))

import workflow_test_router as router


def _ids(plan):
    return [command["id"] for command in plan["commands"]]


def test_plan_is_pure_and_g6_fast_uses_only_the_sentinel(monkeypatch):
    monkeypatch.setattr(
        router.subprocess,
        "run",
        lambda *args, **kwargs: (_ for _ in ()).throw(AssertionError("processus lancé")),
    )
    plan = router.build_plan(
        REPO_ROOT,
        ["pipeline/geo/steps/06_relief.py"],
        "fast",
    )
    assert _ids(plan) == ["git-diff-check", "g6-sentinel"]
    assert plan["heavy_commands"] == []
    assert plan["serial"] is True


def test_forgepilot_can_load_router_directly_from_its_worktree_path():
    spec = importlib.util.spec_from_file_location(
        "forgepilot_workflow_test_router_test",
        REPO_ROOT / "harness" / "workflow_test_router.py",
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    plan = module.build_plan(REPO_ROOT, ["sim/world.py"], "fast")
    assert "sim-tests" in _ids(plan)


def test_certify_requires_final_sha():
    with pytest.raises(router.TestRouterError, match="SHA final"):
        router.build_plan(
            REPO_ROOT,
            ["pipeline/geo/steps/06_relief.py"],
            "certify",
        )


def test_certify_requires_base_as_well_as_final_sha():
    with pytest.raises(router.TestRouterError, match="base et le SHA final"):
        router.build_plan(
            REPO_ROOT,
            ["pipeline/geo/steps/06_relief.py"],
            "certify",
            head_sha="a" * 40,
        )


def test_g6_certify_plans_one_explicit_heavy_command():
    plan = router.build_plan(
        REPO_ROOT,
        ["pipeline/geo/steps/06_relief.py"],
        "certify",
        base_sha="b" * 40,
        head_sha="a" * 40,
    )
    assert _ids(plan) == [
        "git-diff-check",
        "g6-sentinel",
        "g6-europe-certification",
    ]
    assert plan["heavy_commands"] == ["g6-europe-certification"]


def test_g6_sources_lock_routes_to_the_sentinel_instead_of_refusing():
    plan = router.build_plan(
        REPO_ROOT,
        ["pipeline/geo/sources.lock"],
        "fast",
    )
    assert _ids(plan) == ["git-diff-check", "g6-sentinel"]
    assert plan["assignments"]["pipeline/geo/sources.lock"] == ["geo-g6"]


def test_unknown_sensitive_geo_path_fails_closed():
    with pytest.raises(router.TestRouterError, match="sans règle"):
        router.build_plan(
            REPO_ROOT,
            ["pipeline/geo/steps/99_unknown.py"],
            "fast",
        )


@pytest.mark.parametrize(
    "path",
    ["SECURITY.md", ".claude/agents/forge.md", "hermes/reports/bilan.md"],
)
def test_policy_sensitive_governance_paths_never_fall_back_to_documentation(path):
    plan = router.build_plan(REPO_ROOT, [path], "fast")
    assert plan["assignments"][path] == ["governance"]
    assert "single-source-tests" in _ids(plan)


def test_unknown_sensitive_data_suffix_fails_closed():
    with pytest.raises(router.TestRouterError, match="sans règle"):
        router.build_plan(REPO_ROOT, ["root.geojson"], "fast")


def test_plain_documentation_does_not_invent_a_test_suite():
    plan = router.build_plan(REPO_ROOT, ["docs/note.md"], "fast")
    assert _ids(plan) == ["git-diff-check"]
    assert plan["assignments"]["docs/note.md"] == ["documentation"]


def test_policy_selects_required_profile_but_allows_an_explicit_earlier_stage():
    policy_path = REPO_ROOT / "control-plane" / "workflow-policy.toml"
    plan = router.build_plan(
        REPO_ROOT,
        ["sim/world.py"],
        risk="R1",
        policy_path=policy_path,
    )
    assert plan["profile"] == "pr"
    assert plan["required_profile"] == "pr"
    staged = router.build_plan(
        REPO_ROOT,
        ["sim/world.py"],
        "fast",
        risk="R1",
        policy_path=policy_path,
    )
    assert staged["profile"] == "fast"
    assert staged["required_profile"] == "pr"
    assert staged["satisfies_policy"] is False


def test_router_itself_raises_a_requested_risk_before_selecting_profile():
    policy_path = REPO_ROOT / "control-plane" / "workflow-policy.toml"
    plan = router.build_plan(
        REPO_ROOT,
        [".github/workflows/harness-ci.yml"],
        risk="R0",
        policy_path=policy_path,
        base_sha="b" * 40,
        head_sha="a" * 40,
    )
    assert plan["requested_risk"] == "R0"
    assert plan["derived_risk"] == "R2"
    assert plan["risk"] == "R2"
    assert plan["profile"] == "certify"
    assert plan["satisfies_policy"] is True


def test_policy_change_routes_both_forgepilot_and_workflow_contracts():
    plan = router.build_plan(
        REPO_ROOT,
        ["control-plane/workflow-policy.toml"],
        "fast",
    )
    assert set(_ids(plan)) == {
        "git-diff-check",
        "forgepilot-tests",
        "workflow-contract-tests",
    }


def test_run_summary_contains_code_duration_and_targeted_proof(tmp_path):
    calls = []

    def fake_runner(argv, **kwargs):
        calls.append((argv, kwargs))
        return subprocess.CompletedProcess(argv, 0, stdout="preuve ok", stderr="")

    plan = {
        "profile": "fast",
        "head_sha": None,
        "commands": [
            {
                "id": "probe",
                "argv": ["{python}", "-V"],
                "cwd": ".",
                "proof": "interpréteur disponible",
                "heavy": False,
            }
        ],
    }
    result = router.run_plan(plan, tmp_path, runner=fake_runner)
    assert result["status"] == "passed"
    assert result["code"] == 0
    assert result["results"][0]["proof"] == "interpréteur disponible"
    assert result["results"][0]["duration_seconds"] >= 0
    assert calls[0][0][0] == sys.executable
    assert "shell" not in calls[0][1]


def test_run_stops_after_first_failure(tmp_path):
    calls = []

    def fake_runner(argv, **kwargs):
        calls.append(argv)
        return subprocess.CompletedProcess(argv, 7, stdout="", stderr="rouge")

    plan = {
        "profile": "fast",
        "head_sha": None,
        "commands": [
            {"id": "red", "argv": ["false"], "cwd": ".", "proof": "rouge", "heavy": False},
            {"id": "never", "argv": ["true"], "cwd": ".", "proof": "non lancé", "heavy": False},
        ],
    }
    result = router.run_plan(plan, tmp_path, runner=fake_runner)
    assert result["status"] == "failed"
    assert result["code"] == 7
    assert len(calls) == 1


def test_heavy_command_requires_explicit_opt_in(tmp_path):
    plan = {
        "profile": "fast",
        "head_sha": None,
        "commands": [
            {"id": "heavy", "argv": ["true"], "cwd": ".", "proof": "lourd", "heavy": True}
        ],
    }
    with pytest.raises(router.TestRouterError, match="--allow-heavy"):
        router.run_plan(plan, tmp_path)


def test_heavy_command_uses_repository_scoped_lock(monkeypatch, tmp_path):
    monkeypatch.setattr(router, "_current_head", lambda repo: "a" * 40)
    shared_lock = tmp_path / "shared" / "heavy.lock"
    monkeypatch.setattr(router, "_heavy_lock_path", lambda repo: shared_lock)

    def fake_runner(argv, **kwargs):
        return subprocess.CompletedProcess(argv, 0, stdout="ok", stderr="")

    plan = {
        "profile": "certify",
        "head_sha": "a" * 40,
        "commands": [
            {"id": "heavy", "argv": ["true"], "cwd": ".", "proof": "lourd", "heavy": True}
        ],
    }
    result = router.run_plan(plan, tmp_path, allow_heavy=True, runner=fake_runner)
    assert result["status"] == "passed"
    assert result["heavy_lock"] == str(shared_lock)


def test_heavy_lock_can_be_global_to_the_vps(tmp_path):
    target = (tmp_path / "vps-heavy.lock").resolve()
    assert router._heavy_lock_path(
        tmp_path,
        environ={"FORGEPILOT_HEAVY_LOCK": str(target)},
    ) == target
    with pytest.raises(router.TestRouterError, match="chemin absolu"):
        router._heavy_lock_path(
            tmp_path,
            environ={"FORGEPILOT_HEAVY_LOCK": "relative.lock"},
        )
