from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.config import load_settings
from forgepilot.merge import assert_merge_ready
from forgepilot.process import PilotError
from forgepilot.workflow import grok_model_for_effort, plan_invocation, review_invocation, witness_invocation


class GrokEffortTests(unittest.TestCase):
    def test_suffix_is_appended_once(self):
        self.assertEqual("cursor-grok-4.6-high", grok_model_for_effort("cursor-grok-4.6", "high"))
        self.assertEqual(
            "cursor-grok-4.6-xhigh", grok_model_for_effort("cursor-grok-4.6-xhigh", "high")
        )
        self.assertEqual("cursor-grok-4.6-xhigh", grok_model_for_effort("cursor-grok-4.6", "max"))
        self.assertEqual("composer-2.5", grok_model_for_effort("composer-2.5", "high"))


class PolicyRoutingTests(unittest.TestCase):
    def test_r1_plan_and_review_use_cursor_grok(self):
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            task = root / "task.md"
            task.write_text("Lot mesurable.", encoding="utf-8")
            plan = root / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            bundle = root / "bundle.json"
            bundle.write_text('{"plan":"x"}', encoding="utf-8")
            plan_inv = plan_invocation(settings, root, task, risk="R1")
            review_inv = review_invocation(
                settings, root, plan, "HEAD", risk="R1", bundle_path=bundle
            )

        self.assertEqual("cursor", plan_inv.backend)
        self.assertEqual("ask", plan_inv.argv[plan_inv.argv.index("--mode") + 1])
        self.assertEqual(
            "cursor-grok-4.6-high",
            plan_inv.argv[plan_inv.argv.index("--model") + 1],
        )
        self.assertEqual("cursor", review_inv.backend)
        self.assertEqual("ask", review_inv.argv[review_inv.argv.index("--mode") + 1])
        self.assertEqual(
            "cursor-grok-4.6-xhigh",
            review_inv.argv[review_inv.argv.index("--model") + 1],
        )

    def test_large_cursor_review_bundle_is_referenced_by_path(self):
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan = root / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            bundle_dir = root / ".forgepilot" / "runs" / "run-1"
            bundle_dir.mkdir(parents=True)
            marker = "BUNDLE_LONG_SECRET_MARKER"
            bundle = bundle_dir / "bundle.json"
            bundle.write_text(marker + ("x" * 250_000), encoding="utf-8")

            invocation = review_invocation(
                settings, root, plan, "HEAD", risk="R1", bundle_path=bundle
            )

        prompt = invocation.argv[invocation.argv.index("-p") + 1]
        self.assertIn(str(bundle), prompt)
        self.assertIn("Lis intégralement", prompt)
        self.assertNotIn(marker, prompt)
        self.assertIn("--add-dir", invocation.argv)
        self.assertEqual(str(bundle_dir), invocation.argv[invocation.argv.index("--add-dir") + 1])

    def test_large_cursor_planning_brief_is_referenced_by_repo_path(self):
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            task = root / "harness" / "queue" / "briefs" / "026-large" / "brief.md"
            task.parent.mkdir(parents=True)
            marker = "AUTORITE_LONGUE_026"
            task.write_text(marker + "\n" + ("x" * 80_000), encoding="utf-8")

            invocation = plan_invocation(settings, root, task, risk="R1")

        prompt = invocation.argv[invocation.argv.index("-p") + 1]
        self.assertIn("harness/queue/briefs/026-large/brief.md", prompt)
        self.assertIn("Lis intégralement", prompt)
        self.assertNotIn(marker, prompt)

    def test_witness_stays_claude_opus_5_high(self):
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan = root / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            bundle = root / "bundle.json"
            bundle.write_text('{"plan":"x"}', encoding="utf-8")
            invocation = witness_invocation(
                settings, root, plan, "HEAD", bundle_path=bundle
            )
        self.assertEqual("witness", invocation.role)
        self.assertEqual("claude", invocation.backend)
        self.assertEqual("claude-opus-5", invocation.model)
        self.assertEqual("high", invocation.effort)
        self.assertEqual("claude", invocation.argv[0])


class MergeGateTests(unittest.TestCase):
    def _ready_state(self, repo: Path, verdict: str = "PASS") -> tuple[dict, Path]:
        material = {
            "verdict": verdict,
            "head_sha": "abc",
            "tree_sha": "tree",
            "bundle": "bundle.json",
        }
        material_path = repo / "review.json"
        material_path.write_text(json.dumps(material), encoding="utf-8")
        (repo / "bundle.json").write_text(
            json.dumps({"head_sha": "abc", "tree_sha": "tree"}), encoding="utf-8"
        )
        state = {
            "step": "COMPLETE",
            "head_sha": "abc",
            "pull_request": "https://example.test/pr/1",
            "worktree": str(repo),
            "branch": "agent/demo",
            "candidate": {"tree_sha": "tree"},
            "artifacts": {"review_material": str(material_path)},
            "risk": {"effective": "R1"},
        }
        return state, material_path

    def test_merge_refuses_non_pass(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            state, _ = self._ready_state(repo, verdict="FAIL")
            with patch(
                "forgepilot.merge.validate_verdict_material",
                return_value={"verdict": "FAIL", "head_sha": "abc"},
            ):
                with self.assertRaisesRegex(PilotError, "juge"):
                    assert_merge_ready(repo, state, repo / "state.json")

    def test_merge_refuses_stop_label_and_red_checks(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            state, _ = self._ready_state(repo)
            snapshot = {
                "state": "OPEN",
                "headRefOid": "abc",
                "labels": [{"name": "do-not-merge"}],
                "statusCheckRollup": [
                    {"name": "sim-tests", "status": "COMPLETED", "conclusion": "SUCCESS"}
                ],
            }
            with patch(
                "forgepilot.merge.validate_verdict_material",
                return_value={"verdict": "PASS", "head_sha": "abc"},
            ), patch("forgepilot.merge._pr_snapshot", return_value=snapshot):
                with self.assertRaisesRegex(PilotError, "label"):
                    assert_merge_ready(repo, state, repo / "state.json")

            snapshot["labels"] = []
            snapshot["statusCheckRollup"] = [
                {"name": "sim-tests", "status": "COMPLETED", "conclusion": "FAILURE"}
            ]
            with patch(
                "forgepilot.merge.validate_verdict_material",
                return_value={"verdict": "PASS", "head_sha": "abc"},
            ), patch("forgepilot.merge._pr_snapshot", return_value=snapshot):
                with self.assertRaisesRegex(PilotError, "checks"):
                    assert_merge_ready(repo, state, repo / "state.json")

    def test_merge_accepts_green_pass(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            state, _ = self._ready_state(repo)
            snapshot = {
                "state": "OPEN",
                "headRefOid": "abc",
                "labels": [],
                "statusCheckRollup": [
                    {"name": "sim-tests", "status": "COMPLETED", "conclusion": "SUCCESS"}
                ],
            }
            with patch(
                "forgepilot.merge.validate_verdict_material",
                return_value={"verdict": "PASS", "head_sha": "abc"},
            ), patch("forgepilot.merge._pr_snapshot", return_value=snapshot):
                ready = assert_merge_ready(repo, state, repo / "state.json")
        self.assertEqual("PASS", ready["verdict"])
        self.assertEqual("abc", ready["head_sha"])
