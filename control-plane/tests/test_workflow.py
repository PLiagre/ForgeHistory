from __future__ import annotations

from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.cli import main
from forgepilot.config import Settings
from forgepilot.process import PilotError
from forgepilot.workflow import (
    READ_ONLY_CLAUDE_TOOLS,
    create_worktree,
    executor_invocation,
    plan_invocation,
    publish_preview,
)


SETTINGS = Settings(
    project_id="test",
    engine_repository="owner/engine",
    city_repository="owner/city",
    default_base_ref="origin/main",
    default_base_branch="main",
    claude_binary="claude",
    cursor_binary="agent",
    claude_model="",
    cursor_model="auto",
    timeout_seconds=30,
)


class WorkflowTests(unittest.TestCase):
    def test_doctor_refuses_anthropic_api_billing(self):
        with patch.dict("os.environ", {"ANTHROPIC_API_KEY": "must-not-be-used"}, clear=False):
            self.assertEqual(2, main(["doctor"]))

    def test_claude_code_is_read_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            task = root / "task.md"
            task.write_text("Faire une chose mesurable.", encoding="utf-8")
            invocation = plan_invocation(SETTINGS, root, task)

        self.assertEqual("planner", invocation.role)
        self.assertEqual("claude", invocation.argv[0])
        self.assertEqual({}, invocation.environment)
        self.assertIn("--safe-mode", invocation.argv)
        self.assertIn("--no-session-persistence", invocation.argv)
        permission_index = invocation.argv.index("--permission-mode")
        self.assertEqual("plan", invocation.argv[permission_index + 1])
        tools_index = invocation.argv.index("--tools")
        self.assertEqual(READ_ONLY_CLAUDE_TOOLS, invocation.argv[tools_index + 1])
        self.assertNotIn("Edit", invocation.argv[tools_index + 1])
        self.assertNotIn("Bash", invocation.argv[tools_index + 1])

    def test_cursor_is_only_executor_and_uses_sandbox(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan = root / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            invocation = executor_invocation(SETTINGS, root, plan)

        self.assertEqual("executor", invocation.role)
        self.assertEqual("agent", invocation.argv[0])
        self.assertIn("--force", invocation.argv)
        sandbox_index = invocation.argv.index("--sandbox")
        self.assertEqual("enabled", invocation.argv[sandbox_index + 1])

    def test_dirty_repo_refuses_worktree(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=repo, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=repo, check=True)
            (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
            subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)
            (repo / "tracked.txt").write_text("dirty\n", encoding="utf-8")

            with self.assertRaises(PilotError):
                create_worktree(repo, "my task", "main")

    def test_worktree_branch_is_agent_scoped(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=repo, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=repo, check=True)
            (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
            subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)

            worktree, branch = create_worktree(repo, "My Safe Task", "main")

            self.assertEqual("agent/my-safe-task", branch)
            self.assertTrue(worktree.is_dir())

    def test_publish_refuses_non_agent_branch(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=repo, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=repo, check=True)
            (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
            subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)
            (repo / "tracked.txt").write_text("changed\n", encoding="utf-8")

            with self.assertRaises(PilotError):
                publish_preview(repo, "change", "main")


if __name__ == "__main__":
    unittest.main()
