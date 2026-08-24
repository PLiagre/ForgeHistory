from __future__ import annotations

from dataclasses import replace
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.cli import main
from forgepilot.config import load_settings
from forgepilot.durable import (
    _candidate_paths,
    _commit_push_and_pr,
    _ensure_worktree,
    register_run,
    resume_run,
)
from forgepilot.policy import derive_risk, effective_risk, load_policy
from forgepilot.process import PilotError, run_command_stream
from forgepilot.protocol import validate_plan, write_normalized_json
from forgepilot.publication import enforce_allowed_paths, stage_explicit_paths, working_tree_paths
from forgepilot.review import (
    archive_review_material,
    build_review_bundle,
    render_verdict_material,
    write_feedback,
)
from forgepilot.state import atomic_write_json, load_state, save_state, transition
from forgepilot.workflow import Invocation, execute_invocation, executor_invocation, format_invocation
from forgepilot.workflow import create_worktree, publish_preview


def valid_plan(*, blocked: bool = False, allowed: list[str] | None = None) -> dict[str, object]:
    return {
        "task": "lot test",
        "scope": "test",
        "acceptance_criteria": ["preuve"],
        "files_to_read": ["CLAUDE.md"],
        "files_allowed_to_change": allowed or ["feature.txt"],
        "checks": ["test ciblé"],
        "risks": ["test"],
        "blocked": blocked,
    }


def valid_review(verdict: str = "PASS", findings: list[object] | None = None) -> dict[str, object]:
    normalized_findings: list[dict[str, str]] = []
    for index, finding in enumerate(findings or []):
        if isinstance(finding, dict):
            normalized_findings.append(
                {
                    "id": str(finding.get("id", f"F{index + 1}")),
                    "path": str(finding.get("path", "feature.txt")),
                    "issue": str(finding.get("issue", finding.get("message", "corriger"))),
                    "evidence": str(finding.get("evidence", finding.get("message", "constat"))),
                }
            )
    status = verdict
    return {
        "verdict": verdict,
        "acceptance_criteria": [{"criterion": "preuve", "status": status}],
        "findings": normalized_findings,
        "checks_observed": [
            {"check": "test ciblé", "status": "PASS", "evidence": "code 0"}
        ],
        "human_decision_required": True,
    }


def valid_executor(
    *,
    session_id: str = "cursor-session-29",
    approach_changed: bool | None = None,
) -> dict[str, object]:
    payload: dict[str, object] = {
        "summary": "fait",
        "files_modified": ["feature.txt"],
        "checks": [{"check": "scope", "status": "PASS", "evidence": "ok"}],
        "blockages": [],
        "session_id": session_id,
    }
    if approach_changed is not None:
        payload["approach_changed"] = approach_changed
    return payload


class GitRepoMixin:
    def git_repo(self, root: Path) -> None:
        subprocess.run(["git", "init", "-b", "main"], cwd=root, check=True, capture_output=True)
        subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
        subprocess.run(["git", "config", "user.name", "Test"], cwd=root, check=True)
        (root / ".gitignore").write_text(".forgepilot/\n", encoding="utf-8")
        (root / "seed.txt").write_text("base\n", encoding="utf-8")
        subprocess.run(["git", "add", ".gitignore", "seed.txt"], cwd=root, check=True)
        subprocess.run(["git", "commit", "-m", "base"], cwd=root, check=True, capture_output=True)

    def commit(self, repo: Path, message: str) -> str:
        subprocess.run(["git", "add", "--all"], cwd=repo, check=True)
        subprocess.run(["git", "commit", "-m", message], cwd=repo, check=True, capture_output=True)
        return subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=repo, check=True, capture_output=True, text=True
        ).stdout.strip()


class PolicyTests(unittest.TestCase):
    def test_authoritative_policy_covers_controller_roles_profiles_and_timeouts(self):
        policy = load_policy()
        self.assertEqual("hermes", policy.controller.backend)
        self.assertEqual("nous_portal", policy.controller.provider)
        self.assertFalse(policy.controller.can_review)
        self.assertEqual("openai/gpt-5.6-sol-high", policy.controller.model)
        self.assertEqual("claude-opus-5", policy.witness.model)
        self.assertEqual("high", policy.witness.effort)
        self.assertEqual("cursor", policy.risks["R1"].roles["planner"].backend)
        self.assertEqual("cursor-grok-4.6", policy.risks["R1"].roles["planner"].model)
        self.assertEqual("xhigh", policy.risks["R1"].roles["reviewer"].effort)
        self.assertEqual({"R0", "R1", "R2"}, set(policy.risks))
        for profile in policy.risks.values():
            self.assertEqual({"planner", "executor", "reviewer"}, set(profile.roles))
            self.assertGreater(profile.timeouts.proof, 0)
            self.assertGreater(profile.timeouts.executor, 0)

    def test_invalid_backend_is_refused_before_agents(self):
        source = Path(__file__).parents[1] / "workflow-policy.toml"
        body = source.read_text(encoding="utf-8").replace(
            '[risks.R1.roles.executor]\nbackend = "cursor"',
            '[risks.R1.roles.executor]\nbackend = "claude"',
            1,
        )
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "policy.toml"
            path.write_text(body, encoding="utf-8")
            with self.assertRaisesRegex(PilotError, "Backend incompatible"):
                load_policy(path)

    def test_missing_profile_is_refused(self):
        source = Path(__file__).parents[1] / "workflow-policy.toml"
        body = source.read_text(encoding="utf-8").split("[risks.R2]", 1)[0]
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "policy.toml"
            path.write_text(body, encoding="utf-8")
            with self.assertRaisesRegex(PilotError, "R0, R1 et R2"):
                load_policy(path)

    def test_risk_can_only_rise(self):
        policy = load_policy()
        self.assertEqual("R0", derive_risk(policy, ["docs/operations/runbook.md"]))
        self.assertEqual("R1", derive_risk(policy, ["sim/model.py"]))
        self.assertEqual("R2", derive_risk(policy, ["control-plane/forgepilot/cli.py"]))
        self.assertEqual(("R2", "R0"), effective_risk(policy, "R2", ["docs/operations/runbook.md"]))
        self.assertEqual(("R2", "R2"), effective_risk(policy, "R0", ["docs/rules/security.md"]))

    def test_plan_allows_github_governance_but_not_git_internals(self):
        plan = validate_plan(valid_plan(allowed=[".github/workflows/**", ".gitignore"]))
        self.assertIn(".github/workflows/**", plan["files_allowed_to_change"])
        with self.assertRaisesRegex(PilotError, "interne interdit"):
            validate_plan(valid_plan(allowed=[".git/config"]))
        with self.assertRaisesRegex(PilotError, "universel interdit"):
            validate_plan(valid_plan(allowed=["**"]))

    def test_broad_glob_that_can_touch_governance_is_r2(self):
        policy = load_policy()
        self.assertEqual("R2", derive_risk(policy, ["docs/**"]))
        self.assertEqual("R2", derive_risk(policy, ["pipeline/geo/**"]))

    def test_doctor_prints_effective_policy_without_secret(self):
        out = io.StringIO()
        with patch("forgepilot.cli.missing_binaries", return_value=[]), patch(
            "forgepilot.cli.git", return_value="main"
        ), patch("sys.stdout", out):
            code = main(["doctor", "--repo", str(Path.cwd())])
        self.assertEqual(0, code)
        text = out.getvalue()
        self.assertIn("nous_portal", text)
        self.assertIn('"R2"', text)
        self.assertNotIn("ANTHROPIC_API_KEY", text)


class AtomicStateTests(unittest.TestCase, GitRepoMixin):
    def test_interrupted_replace_preserves_previous_state(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "state.json"
            atomic_write_json(path, {"schema_version": 1, "value": "old"})
            with patch("forgepilot.state.os.replace", side_effect=OSError("coupure")):
                with self.assertRaises(OSError):
                    atomic_write_json(path, {"schema_version": 1, "value": "new"})
            self.assertEqual("old", json.loads(path.read_text(encoding="utf-8"))["value"])
            self.assertEqual([], list(path.parent.glob(".state.json.tmp-*")))

    def test_state_refuses_secret_fields(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(PilotError, "secret interdit"):
                atomic_write_json(Path(tmp) / "state.json", {"api_key": "x"})

    def test_register_and_status_have_required_durable_fields(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R2.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "durable", base_ref="main", base_branch="main"
            )
            self.assertTrue(state_path.is_file())
            for field in (
                "base_sha",
                "head_sha",
                "risk",
                "step",
                "active_role",
                "effective_models",
                "durations_seconds",
                "worktree",
                "branch",
                "pull_request",
                "proofs",
                "error",
            ):
                self.assertIn(field, state)
            self.assertNotIn("prompt", state_path.read_text(encoding="utf-8").lower())
            out = io.StringIO()
            with patch("sys.stdout", out):
                self.assertEqual(
                    0,
                    main(["status", str(state["run_id"]), "--repo", str(repo)]),
                )
            self.assertEqual(state["run_id"], json.loads(out.getvalue())["state"]["run_id"])

    def test_register_refuses_a_second_active_run_with_the_same_task_name(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, first = register_run(
                load_settings(), repo, task, "unique-name", base_ref="main", base_branch="main"
            )
            with self.assertRaisesRegex(PilotError, "porte déjà le nom"):
                register_run(
                    load_settings(),
                    repo,
                    task,
                    "unique-name",
                    base_ref="main",
                    base_branch="main",
                )
            runs = list((repo / ".forgepilot" / "runs").glob("*/state.json"))
            self.assertEqual(1, len(runs))
            self.assertEqual(first["run_id"], load_state(runs[0])["run_id"])

    def test_transition_records_duration_and_role(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "run" / "state.json"
            path.parent.mkdir()
            state = {
                "schema_version": 1,
                "run_id": "run",
                "step": "CREATED",
                "step_started_at": "2026-01-01T00:00:00+00:00",
                "updated_at": "2026-01-01T00:00:00+00:00",
                "step_history": [],
                "durations_seconds": {},
            }
            atomic_write_json(path, state)
            changed = transition(path, state, "PLANNING", role="planner")
            self.assertEqual("planner", changed["active_role"])
            self.assertGreater(changed["durations_seconds"]["CREATED"], 0)

    def test_resume_refuses_changed_brief(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(), repo, task, "drift", base_ref="main", base_branch="main"
            )
            task.write_text("brief modifié\n", encoding="utf-8")
            with self.assertRaisesRegex(PilotError, "brief a changé"):
                resume_run(load_settings(), repo, str(state["run_id"]))

    def test_existing_worktree_is_recovered_without_recreation(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            sha = subprocess.run(
                ["git", "rev-parse", "HEAD"], cwd=repo, check=True, capture_output=True, text=True
            ).stdout.strip()
            expected, branch = create_worktree(repo, "recover", sha)
            with patch(
                "forgepilot.durable.create_worktree",
                side_effect=AssertionError("ne doit pas recréer le worktree"),
            ):
                actual, actual_branch = _ensure_worktree(
                    repo, {"task_name": "recover", "base_sha": sha}
                )
            self.assertEqual(expected, actual)
            self.assertEqual(branch, actual_branch)


class StreamTests(unittest.TestCase):
    def test_jsonl_stream_is_bounded_and_observable(self):
        with tempfile.TemporaryDirectory() as tmp:
            seen: list[object] = []
            code = "import json; [print(json.dumps({'n': i}), flush=True) for i in range(250)]"
            result = run_command_stream(
                [sys.executable, "-c", code],
                cwd=Path(tmp),
                timeout_seconds=10,
                on_event=seen.append,
                max_buffered_lines=3,
            )
            self.assertEqual(250, len(seen))
            self.assertEqual({"n": 249}, result.json())
            self.assertLessEqual(len(result.stdout.splitlines()), 3)

    def test_invalid_json_line_is_visible_refusal(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(PilotError, "Flux JSON invalide"):
                run_command_stream(
                    [sys.executable, "-c", "print('pas-json', flush=True)"],
                    cwd=Path(tmp),
                    timeout_seconds=10,
                )

    def test_persisted_agent_output_redacts_prompt_and_environment_secret(self):
        with tempfile.TemporaryDirectory() as tmp, patch.dict(
            os.environ, {"TEST_ACCESS_TOKEN": "TOKEN-UNIQUE-029"}, clear=False
        ):
            path = write_normalized_json(
                Path(tmp) / "output.json",
                {"prompt": "corps intégral", "message": "TOKEN-UNIQUE-029"},
            )
            text = path.read_text(encoding="utf-8")
            self.assertNotIn("corps intégral", text)
            self.assertNotIn("TOKEN-UNIQUE-029", text)

    def test_agent_process_does_not_inherit_controller_tokens(self):
        with tempfile.TemporaryDirectory() as tmp, patch.dict(
            os.environ,
            {"GITHUB_TOKEN": "GH-TOKEN-UNIQUE-029", "DISCORD_BOT_TOKEN": "DC-TOKEN-UNIQUE-029"},
            clear=False,
        ):
            code = (
                "import json, os; "
                "print(json.dumps({'github': 'GITHUB_TOKEN' in os.environ, "
                "'discord': 'DISCORD_BOT_TOKEN' in os.environ}), flush=True)"
            )
            invocation = Invocation(
                "executor",
                (sys.executable, "-c", code),
                tmp,
                {},
                backend="cursor",
            )
            result = execute_invocation(
                invocation,
                load_settings(),
                timeout_seconds=10,
                stream=True,
            )
            self.assertEqual({"github": False, "discord": False}, result)

    def test_stream_keeps_cursor_session_from_early_event(self):
        with tempfile.TemporaryDirectory() as tmp:
            code = (
                "import json; "
                "print(json.dumps({'type':'system','session_id':'session-early'}), flush=True); "
                "print(json.dumps({'summary':'done'}), flush=True)"
            )
            invocation = Invocation(
                "executor", (sys.executable, "-c", code), tmp, {}, backend="cursor"
            )
            result = execute_invocation(
                invocation, load_settings(), timeout_seconds=10, stream=True
            )
            self.assertEqual("done", result["summary"])
            self.assertEqual("session-early", result["session_id"])

    def test_stream_unwraps_cursor_result_event_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            expected = valid_plan()
            code = (
                "import json; "
                f"payload={expected!r}; "
                "print(json.dumps({'type':'result','subtype':'success','is_error':False,"
                "'result':json.dumps(payload),'session_id':'session-result'}), flush=True)"
            )
            invocation = Invocation(
                "planner", (sys.executable, "-c", code), tmp, {}, backend="cursor"
            )
            result = execute_invocation(
                invocation, load_settings(), timeout_seconds=10, stream=True
            )
            self.assertEqual(expected, result)

    def test_stream_unwraps_single_fenced_cursor_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            expected = valid_plan()
            fenced = "```json\n" + json.dumps(expected) + "\n```"
            code = (
                "import json; "
                f"text={fenced!r}; "
                "print(json.dumps({'type':'result','subtype':'success','is_error':False,"
                "'result':text,'session_id':'session-fenced'}), flush=True)"
            )
            invocation = Invocation(
                "planner", (sys.executable, "-c", code), tmp, {}, backend="cursor"
            )
            result = execute_invocation(
                invocation, load_settings(), timeout_seconds=10, stream=True
            )
            self.assertEqual(expected, result)

    def test_stream_unwraps_one_fenced_cursor_json_after_prose(self):
        with tempfile.TemporaryDirectory() as tmp:
            expected = valid_plan()
            text = "Préalables vérifiés. Voici le plan.\n\n```json\n" + json.dumps(expected) + "\n```"
            code = (
                "import json; "
                f"text={text!r}; "
                "print(json.dumps({'type':'result','subtype':'success','is_error':False,"
                "'result':text,'session_id':'session-prose'}), flush=True)"
            )
            invocation = Invocation(
                "planner", (sys.executable, "-c", code), tmp, {}, backend="cursor"
            )
            result = execute_invocation(
                invocation, load_settings(), timeout_seconds=10, stream=True
            )
            self.assertEqual(expected, result)

    def test_stream_unwraps_cursor_json_before_terminal_redacted_marker(self):
        with tempfile.TemporaryDirectory() as tmp:
            expected = {
                "summary": "done",
                "files_modified": [],
                "checks": [{"check": "proof", "status": "PASS"}],
                "blockages": [],
            }
            text = json.dumps(expected) + "\n\n[REDACTED]"
            code = (
                "import json; "
                f"text={text!r}; "
                "print(json.dumps({'type':'result','subtype':'success','is_error':False,"
                "'result':text,'session_id':'session-redacted'}), flush=True)"
            )
            invocation = Invocation(
                "executor", (sys.executable, "-c", code), tmp, {}, backend="cursor"
            )
            result = execute_invocation(
                invocation, load_settings(), timeout_seconds=10, stream=True
            )
            self.assertEqual(expected | {"session_id": "session-redacted"}, result)


class ScopeAndReviewTests(unittest.TestCase, GitRepoMixin):
    def test_scope_rejects_one_unexpected_path(self):
        self.assertEqual(
            ["control-plane/a.py"],
            enforce_allowed_paths(["control-plane/a.py"], ["control-plane/**"]),
        )
        with self.assertRaisesRegex(PilotError, "hors files_allowed_to_change"):
            enforce_allowed_paths(
                ["control-plane/a.py", "sim/escape.py"], ["control-plane/**"]
            )

    def test_explicit_staging_handles_untracked_without_add_all(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            (repo / "allowed.txt").write_text("oui\n", encoding="utf-8")
            (repo / "outside.txt").write_text("non\n", encoding="utf-8")
            paths = enforce_allowed_paths(["allowed.txt"], ["allowed.txt"])
            stage_explicit_paths(repo, paths)
            cached = subprocess.run(
                ["git", "diff", "--cached", "--name-only"],
                cwd=repo,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.splitlines()
            self.assertEqual(["allowed.txt"], cached)
            self.assertIn("outside.txt", working_tree_paths(repo))

    def test_a_commit_created_by_cursor_cannot_bypass_scope_or_targeted_tests(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            base = subprocess.run(
                ["git", "rev-parse", "HEAD"],
                cwd=repo,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip()
            (repo / "outside.txt").write_text("hors périmètre\n", encoding="utf-8")
            self.commit(repo, "cursor committed directly")
            state = {
                "base_sha": base,
                "head_sha": None,
                "title": "scope",
                "iteration": {"count": 0},
            }
            self.assertEqual(
                ["outside.txt"],
                _candidate_paths(repo, state, ["outside.txt"], update_only=False),
            )
            with patch("forgepilot.durable.resolve_binary", return_value="gh"):
                with self.assertRaisesRegex(PilotError, "hors files_allowed_to_change"):
                    _commit_push_and_pr(
                        repo,
                        state,
                        ["allowed.txt"],
                        update_only=False,
                    )

    def test_publish_source_never_uses_git_add_all(self):
        source = (Path(__file__).parents[1] / "forgepilot" / "workflow.py").read_text(encoding="utf-8")
        self.assertNotIn('git(repo, "add", "-A")', source)

    def test_pr_body_exposes_one_effective_risk_line(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            worktree, _ = create_worktree(repo, "risk-body", "main")
            (worktree / "feature.txt").write_text("x\n", encoding="utf-8")
            invocation = publish_preview(
                worktree,
                "lot",
                "main",
                risk="R2",
                brief="029-workflow-acceleration",
            )
            body = invocation.argv[invocation.argv.index("--body") + 1]
            self.assertEqual(1, body.count("Forge-Risk: R2"))
            self.assertIn("Forge-Brief: 029-workflow-acceleration", body)

    def test_review_bundle_excludes_generated_diff_and_producer_conclusions(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            (repo / "feature.txt").write_text("base\n", encoding="utf-8")
            generated = repo / "artifacts" / "measure.generated.json"
            generated.parent.mkdir()
            generated.write_text("{}\n", encoding="utf-8")
            base = self.commit(repo, "files")
            (repo / "feature.txt").write_text("main\n", encoding="utf-8")
            generated.write_text('{"large": true}\n', encoding="utf-8")
            head = self.commit(repo, "changes")
            bundle = build_review_bundle(
                repo,
                base_sha=base,
                head_sha=head,
                plan=valid_plan(allowed=["feature.txt", "artifacts/**"]),
                policy=load_policy(),
                mechanical_results=[{"check": "ok", "code": 0}],
            )
            self.assertIn("feature.txt", bundle["manual_diffs"])
            self.assertNotIn("artifacts/measure.generated.json", bundle["manual_diffs"])
            artifact = bundle["generated_artifacts"][0]
            self.assertEqual(64, len(artifact["sha256"]))
            self.assertFalse(bundle["producer_conclusions_included"])
            self.assertNotIn("executor", json.dumps(bundle).lower())

    def test_review_bundle_refuses_excess_without_truncation(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            base = subprocess.run(
                ["git", "rev-parse", "HEAD"], cwd=repo, check=True, capture_output=True, text=True
            ).stdout.strip()
            (repo / "feature.txt").write_text("x" * 20_000, encoding="utf-8")
            head = self.commit(repo, "large")
            tiny = replace(load_policy(), review_bundle_max_bytes=4096)
            with self.assertRaisesRegex(PilotError, "Aucun contenu n'a été tronqué"):
                build_review_bundle(
                    repo,
                    base_sha=base,
                    head_sha=head,
                    plan=valid_plan(),
                    policy=tiny,
                )

    def test_review_material_and_feedback_are_sha_linked(self):
        with tempfile.TemporaryDirectory() as tmp:
            run_dir = Path(tmp)
            bundle = run_dir / "bundle.json"
            bundle.write_text("{}", encoding="utf-8")
            review = valid_review("FAIL", [{"id": "F1", "message": "corriger"}])
            material = archive_review_material(
                run_dir,
                base_sha="a" * 40,
                head_sha="b" * 40,
                tree_sha="c" * 40,
                review=review,
                bundle_path=bundle,
            )
            feedback = write_feedback(
                run_dir, head_sha="b" * 40, review=review, iteration=1
            )
            self.assertEqual(
                "b" * 40,
                json.loads(feedback.read_text(encoding="utf-8"))["head_sha_reviewed"],
            )
            markdown = render_verdict_material(material, run_dir / "verdict.md")
            self.assertIn("b" * 40, markdown.read_text(encoding="utf-8"))


class ControllerRouterTests(unittest.TestCase):
    def test_test_profile_uses_controller_router_but_runs_in_worktree(self):
        from forgepilot.durable import run_test_profile

        with tempfile.TemporaryDirectory() as tmp:
            worktree = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=worktree, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.invalid"], cwd=worktree, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=worktree, check=True)
            (worktree / "harness").mkdir()
            (worktree / "harness" / "workflow_test_router.py").write_text(
                "def build_plan(*args, **kwargs): raise RuntimeError('routeur périmé chargé')\n",
                encoding="utf-8",
            )
            (worktree / "control-plane").mkdir()
            source_policy = Path(__file__).resolve().parents[1] / "workflow-policy.toml"
            (worktree / "control-plane" / "workflow-policy.toml").write_bytes(
                source_policy.read_bytes()
            )
            proof = worktree / "pipeline" / "geo" / "tests" / "run_proof_r1.py"
            proof.parent.mkdir(parents=True)
            proof.write_text("print('R1 OK')\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=worktree, check=True)
            subprocess.run(["git", "commit", "-m", "fixture"], cwd=worktree, check=True, capture_output=True)

            result = run_test_profile(
                worktree,
                paths=["pipeline/geo/tests/run_proof_r1.py"],
                profile="fast",
                output_path=worktree / "result.json",
            )

            self.assertEqual(0, result["code"])
            results = result["results"]
            self.assertIsInstance(results, list)
            self.assertEqual(["git-diff-check", "r1-proof"], [item["id"] for item in results])


class DurableFlowTests(unittest.TestCase, GitRepoMixin):
    def _fake_publish(
        self,
        worktree: Path,
        state: dict[str, object],
        candidate: dict[str, object],
        *,
        update_only: bool,
    ) -> str:
        self.assertEqual(
            candidate["head_sha"],
            subprocess.run(
                ["git", "rev-parse", "HEAD"],
                cwd=worktree,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip(),
        )
        return "https://example.test/pr/29"

    def test_blocked_plan_stops_before_cursor_and_worktree(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R2.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(), repo, task, "blocked", base_ref="main", base_branch="main"
            )
            with patch("forgepilot.durable.missing_binaries", return_value=[]), patch(
                "forgepilot.durable._run_agent", return_value=valid_plan(blocked=True)
            ), patch(
                "forgepilot.durable.create_worktree",
                side_effect=AssertionError("Cursor/worktree ne doit pas démarrer"),
            ):
                final = resume_run(
                    load_settings(), repo, str(state["run_id"]), allow_heavy=True
                )
            self.assertEqual("BLOCKED", final["step"])
            self.assertIsNone(final["worktree"])
            self.assertTrue(final["allow_heavy"])

    def test_resume_never_replays_ambiguous_cursor_writes(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "ambiguous", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "ambiguous", str(state["base_sha"]))
            plan_path = state_path.parent / "plan.json"
            plan_path.write_text(json.dumps(valid_plan()), encoding="utf-8")
            state["artifacts"] = {"plan": "plan.json"}
            state["worktree"] = str(worktree)
            state["branch"] = branch
            state["step"] = "EXECUTING"
            save_state(state_path, state)
            (worktree / "feature.txt").write_text("écriture Cursor non archivée\n", encoding="utf-8")
            with patch("forgepilot.durable.missing_binaries", return_value=[]), patch(
                "forgepilot.durable._run_agent",
                side_effect=AssertionError("Cursor ne doit pas être rejoué"),
            ):
                final = resume_run(load_settings(), repo, str(state["run_id"]))
            self.assertEqual("BLOCKED", final["step"])
            self.assertIn("ne sont pas rejouées", str(final["error"]))

    def test_recover_executor_archives_valid_result_and_reopens_ambiguous_run(self):
        from forgepilot.durable import recover_executor_result

        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "recover", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "recover", str(state["base_sha"]))
            plan_path = state_path.parent / "plan.json"
            plan_path.write_text(json.dumps(valid_plan()), encoding="utf-8")
            state.update({
                "artifacts": {"plan": "plan.json"},
                "worktree": str(worktree),
                "branch": branch,
                "step": "BLOCKED",
                "error": "Reprise Cursor ambiguë : des écritures existent sans résultat final archivé ; elles ne sont pas rejouées automatiquement.",
            })
            save_state(state_path, state)
            (worktree / "feature.txt").write_text("livré\n", encoding="utf-8")
            recovered = repo / "recovered.json"
            recovered.write_text(json.dumps(valid_executor(session_id="recovered-session")), encoding="utf-8")

            final = recover_executor_result(repo, str(state["run_id"]), recovered)

            self.assertEqual("EXECUTING", final["step"])
            self.assertEqual("recovered-session", final["executor_session"])
            self.assertTrue((state_path.parent / "executor.json").is_file())

    def test_recover_iteration_archives_valid_result_and_reopens_stale_candidate(self):
        from forgepilot.durable import recover_iteration_result

        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "recover-iteration", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "recover-iteration", str(state["base_sha"]))
            base = str(state["base_sha"])
            state.update({
                "worktree": str(worktree),
                "branch": branch,
                "head_sha": base,
                "candidate": {"base_sha": base, "head_sha": base, "tree_sha": subprocess.run(["git", "rev-parse", "HEAD^{tree}"], cwd=worktree, check=True, capture_output=True, text=True).stdout.strip(), "paths": [], "iteration": 0},
                "step": "ERROR",
                "resume_from": "PR_TESTING",
                "error": "Preuve périmée : le candidat Git a changé depuis son identité archivée (feature.txt).",
            })
            save_state(state_path, state)
            (worktree / "feature.txt").write_text("fix\n", encoding="utf-8")
            recovered = repo / "iteration.json"
            recovered.write_text(json.dumps(valid_executor(approach_changed=False)), encoding="utf-8")

            final = recover_iteration_result(repo, str(state["run_id"]), recovered)

            self.assertEqual("ITERATED", final["step"])
            self.assertEqual(1, final["iteration"]["count"])
            self.assertTrue((state_path.parent / "executor-iteration-1.json").is_file())

    def test_iterated_recovery_does_not_require_feedback_again(self):
        from forgepilot.durable import _iterate

        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "iterated-no-feedback", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "iterated-no-feedback", str(state["base_sha"]))
            plan = valid_plan()
            plan_path = state_path.parent / "plan.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            (worktree / "feature.txt").write_text("fix\n", encoding="utf-8")
            state.update({
                "artifacts": {"plan": "plan.json", "executor": "executor-iteration-1.json"},
                "worktree": str(worktree),
                "branch": branch,
                "head_sha": str(state["base_sha"]),
                "step": "ITERATED",
                "iteration": {"count": 1, "plateau_count": 0, "last_findings": [], "last_finding_count": None},
                "iteration_approach_changed": False,
            })
            save_state(state_path, state)

            with patch("forgepilot.durable._prepare_candidate", side_effect=PilotError("candidate reached")):
                with self.assertRaisesRegex(PilotError, "candidate reached"):
                    _iterate(load_settings(), repo, state_path, state, plan)

    def test_first_publication_after_pre_pr_iteration_creates_draft_pr(self):
        from forgepilot.durable import _iterate

        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "first-pr-after-iteration", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "first-pr-after-iteration", str(state["base_sha"]))
            plan = valid_plan()
            plan_path = state_path.parent / "plan.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            candidate = {
                "base_sha": str(state["base_sha"]),
                "head_sha": str(state["base_sha"]),
                "tree_sha": subprocess.run(["git", "rev-parse", "HEAD^{tree}"], cwd=worktree, check=True, capture_output=True, text=True).stdout.strip(),
                "paths": [],
                "iteration": 1,
            }
            state.update({
                "artifacts": {"plan": "plan.json"},
                "worktree": str(worktree),
                "branch": branch,
                "head_sha": candidate["head_sha"],
                "candidate": candidate,
                "step": "ITERATION_PR_TESTED",
                "proofs": [
                    {"kind": "test-profile", "profile": profile, "head_sha": candidate["head_sha"], "tree_sha": candidate["tree_sha"], "result": {"code": 0}}
                    for profile in ("fast", "pr")
                ],
                "pull_request": None,
                "iteration": {"count": 1, "plateau_count": 0, "last_findings": [], "last_finding_count": None},
            })
            save_state(state_path, state)
            seen: list[bool] = []

            def fake_publish(worktree, state, candidate, *, update_only):
                seen.append(update_only)
                raise PilotError("publish reached")

            with patch("forgepilot.durable._push_candidate_and_pr", side_effect=fake_publish):
                with self.assertRaisesRegex(PilotError, "publish reached"):
                    _iterate(load_settings(), repo, state_path, state, plan)
            self.assertEqual([False], seen)

    def test_continue_after_external_merge_rebases_feedback_onto_new_branch(self):
        from forgepilot.durable import continue_after_external_merge

        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            old_head = self.commit(repo, "task")
            state_path, state = register_run(
                load_settings(), repo, task, "external-merge", base_ref="main", base_branch="main"
            )
            worktree, branch = create_worktree(repo, "external-merge", old_head)
            (repo / "merged.txt").write_text("merged\n", encoding="utf-8")
            new_base = self.commit(repo, "external merge")
            feedback = state_path.parent / "feedback.json"
            feedback.write_text(json.dumps({"head_sha_reviewed": old_head, "findings": []}), encoding="utf-8")
            state.update({
                "artifacts": {"feedback": "feedback.json"},
                "worktree": str(worktree),
                "branch": branch,
                "head_sha": old_head,
                "step": "NEEDS_FIX",
                "pull_request": "https://example.test/pr/closed",
                "iteration": {"count": 1, "plateau_count": 0, "last_findings": [], "last_finding_count": 0},
            })
            save_state(state_path, state)

            final = continue_after_external_merge(repo, str(state["run_id"]), new_base)

            self.assertEqual("ITERATING", final["step"])
            self.assertEqual(new_base, final["head_sha"])
            self.assertIsNone(final["pull_request"])
            self.assertEqual(old_head, final["iteration_base_sha"])
            self.assertEqual(new_base, subprocess.run(["git", "rev-parse", "HEAD"], cwd=worktree, check=True, capture_output=True, text=True).stdout.strip())
            self.assertIn("-fix-2", final["branch"])

    def test_complete_run_persists_sha_pr_models_and_durations(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R2.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(), repo, task, "complete", base_ref="main", base_branch="main"
            )

            def fake_agent(invocation, settings, *, risk, role):
                if role == "planner":
                    return valid_plan()
                if role == "executor":
                    (Path(invocation.cwd) / "feature.txt").write_text("livré\n", encoding="utf-8")
                    return valid_executor(session_id="cursor-session-29")
                return valid_review("PASS")

            profiles: list[tuple[str, bool]] = []

            def fake_profile(worktree, *, profile, allow_heavy=False, **kwargs):
                profiles.append((profile, allow_heavy))
                return {"code": 0, "profile": profile, "duration_seconds": 0.01}

            with patch("forgepilot.durable.missing_binaries", return_value=[]), patch(
                "forgepilot.durable._run_agent", side_effect=fake_agent
            ), patch(
                "forgepilot.durable._push_candidate_and_pr", side_effect=self._fake_publish
            ), patch(
                "forgepilot.durable.run_test_profile",
                side_effect=fake_profile,
            ):
                final = resume_run(load_settings(), repo, str(state["run_id"]))
            self.assertEqual("COMPLETE", final["step"])
            self.assertEqual("https://example.test/pr/29", final["pull_request"])
            self.assertEqual(40, len(final["head_sha"]))
            self.assertEqual("cursor-session-29", final["executor_session"])
            self.assertFalse(final["fusion"])
            self.assertIn("REVIEWING", final["durations_seconds"])
            self.assertEqual("composer-2.5", final["effective_models"]["executor"]["model"])
            self.assertEqual([("pr", False), ("certify", False)], profiles)
            self.assertEqual(
                1,
                sum(
                    1
                    for proof in final["proofs"]
                    if proof.get("profile") == "certify" and proof.get("head_sha") == final["head_sha"]
                ),
            )

    def test_failed_review_feedback_drives_resumed_cursor_session(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R2.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(), repo, task, "iterate", base_ref="main", base_branch="main"
            )
            review_count = 0
            resumed: list[str] = []

            def fake_agent(invocation, settings, *, risk, role):
                nonlocal review_count
                if role == "planner":
                    return valid_plan()
                if role == "executor":
                    target = Path(invocation.cwd) / "feature.txt"
                    target.write_text(target.read_text(encoding="utf-8") + "fix\n" if target.exists() else "v1\n", encoding="utf-8")
                    if "--resume" in invocation.argv:
                        resumed.append(invocation.argv[invocation.argv.index("--resume") + 1])
                    return valid_executor(
                        session_id="cursor-session-iterate",
                        approach_changed=False if "--resume" in invocation.argv else None,
                    )
                review_count += 1
                return (
                    valid_review("FAIL", [{"id": "F1", "message": "corriger"}])
                    if review_count == 1
                    else valid_review("PASS")
                )

            patches = (
                patch("forgepilot.durable.missing_binaries", return_value=[]),
                patch("forgepilot.durable._run_agent", side_effect=fake_agent),
                patch("forgepilot.durable._push_candidate_and_pr", side_effect=self._fake_publish),
                patch(
                    "forgepilot.durable.run_targeted_tests",
                    return_value={"returncode": 0, "duration_seconds": 0.01},
                ),
                patch(
                    "forgepilot.durable.run_test_profile",
                    return_value={"returncode": 0, "duration_seconds": 0.01},
                ),
            )
            with patches[0], patches[1], patches[2], patches[3], patches[4]:
                first = resume_run(load_settings(), repo, str(state["run_id"]))
                self.assertEqual("NEEDS_FIX", first["step"])
                feedback = Path(first["artifacts"]["feedback"])
                if not feedback.is_absolute():
                    feedback = repo / ".forgepilot" / "runs" / str(state["run_id"]) / feedback
                self.assertTrue(feedback.is_file())
                final = resume_run(load_settings(), repo, str(state["run_id"]))
            self.assertEqual("COMPLETE", final["step"])
            self.assertEqual(["cursor-session-iterate"], resumed)
            self.assertEqual(1, final["iteration"]["count"])
            self.assertTrue(any(proof.get("profile") == "fast" for proof in final["proofs"]))

    def test_plan_paths_raise_risk_before_cursor(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R1.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(),
                repo,
                task,
                "elevate",
                changed_paths=["sim/model.py"],
                base_ref="main",
                base_branch="main",
            )
            observed: list[tuple[str, str]] = []

            def fake_agent(invocation, settings, *, risk, role):
                observed.append((role, risk))
                if role == "planner":
                    return valid_plan(allowed=["control-plane/feature.py"])
                if role == "executor":
                    target = Path(invocation.cwd) / "control-plane" / "feature.py"
                    target.parent.mkdir()
                    target.write_text("ok = True\n", encoding="utf-8")
                    return valid_executor(session_id="risk-session")
                return valid_review("PASS")

            with patch("forgepilot.durable.missing_binaries", return_value=[]), patch(
                "forgepilot.durable._run_agent", side_effect=fake_agent
            ), patch(
                "forgepilot.durable._push_candidate_and_pr", side_effect=self._fake_publish
            ), patch(
                "forgepilot.durable.run_test_profile",
                return_value={"code": 0, "duration_seconds": 0.01},
            ):
                final = resume_run(load_settings(), repo, str(state["run_id"]))
            self.assertEqual("R2", final["risk"]["effective"])
            self.assertEqual(("planner", "R1"), observed[0])
            self.assertIn(("executor", "R2"), observed)
            self.assertIn(("reviewer", "R2"), observed)
            self.assertEqual("xhigh", final["effective_models"]["reviewer"]["effort"])

    def test_two_iterations_without_improvement_stop(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            task = repo / "task.md"
            task.write_text("**Risque : R2.**\n", encoding="utf-8")
            self.commit(repo, "task")
            _, state = register_run(
                load_settings(), repo, task, "plateau", base_ref="main", base_branch="main"
            )
            reviews = 0

            def fake_agent(invocation, settings, *, risk, role):
                nonlocal reviews
                if role == "planner":
                    return valid_plan()
                if role == "executor":
                    target = Path(invocation.cwd) / "feature.txt"
                    previous = target.read_text(encoding="utf-8") if target.exists() else ""
                    target.write_text(previous + "tentative\n", encoding="utf-8")
                    return valid_executor(
                        session_id="plateau-session", approach_changed=False
                    )
                reviews += 1
                return valid_review("FAIL", [{"id": "stable", "message": "toujours présent"}])

            with patch("forgepilot.durable.missing_binaries", return_value=[]), patch(
                "forgepilot.durable._run_agent", side_effect=fake_agent
            ), patch(
                "forgepilot.durable._push_candidate_and_pr", side_effect=self._fake_publish
            ), patch(
                "forgepilot.durable.run_targeted_tests",
                return_value={"code": 0, "duration_seconds": 0.01},
            ), patch(
                "forgepilot.durable.run_test_profile",
                return_value={"code": 0, "duration_seconds": 0.01},
            ):
                first = resume_run(load_settings(), repo, str(state["run_id"]))
                second = resume_run(load_settings(), repo, str(state["run_id"]))
                final = resume_run(load_settings(), repo, str(state["run_id"]))
            self.assertEqual("NEEDS_FIX", first["step"])
            self.assertEqual("NEEDS_FIX", second["step"])
            self.assertEqual("BLOCKED", final["step"])
            self.assertEqual(2, final["iteration"]["plateau_count"])
            self.assertEqual(3, reviews)

    def test_iterate_run_refuses_missing_feedback(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.git_repo(repo)
            subprocess.run(
                ["git", "worktree", "add", "-b", "agent/direct", str(repo / ".forgepilot" / "worktrees" / "direct"), "main"],
                cwd=repo,
                check=True,
                capture_output=True,
            )
            plan = repo / "plan.json"
            plan.write_text(json.dumps(valid_plan()), encoding="utf-8")
            err = io.StringIO()
            with patch("sys.stderr", err):
                code = main(
                    [
                        "iterate",
                        str(plan),
                        "--repo",
                        str(repo),
                        "--task-name",
                        "direct",
                        "--run",
                    ]
                )
            self.assertEqual(2, code)
            self.assertIn("Feedback structuré absent", err.getvalue())


if __name__ == "__main__":
    unittest.main()
