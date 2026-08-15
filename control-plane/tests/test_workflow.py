from __future__ import annotations

import io
import json
import os
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
    format_invocation,
    plan_invocation,
    publish_preview,
    review_invocation,
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

    def test_review_keeps_argv_under_system_arg_limit(self):
        """SC1 : aucun élément d'argv ne dépasse 32 × SC_PAGESIZE (lu du système)."""
        bound = 32 * os.sysconf("SC_PAGESIZE")
        marker = "MARKER_OVERFLOW_022_UNIQUE_a7f3"
        synthetic = marker + ("D" * (bound + 64 - len(marker)))
        self.assertGreater(len(synthetic.encode()), bound)

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan = root / "plan.json"
            plan.write_text('{"task":"mesure overflow"}', encoding="utf-8")
            with patch("forgepilot.workflow.git", return_value=synthetic):
                invocation = review_invocation(SETTINGS, root, plan, "origin/master")

        max_arg = max(len(a.encode()) for a in invocation.argv)
        self.assertLess(max_arg, bound)
        for arg in invocation.argv:
            self.assertNotIn(marker, arg)

    def test_iterate_without_worktree_refuses_naming_execute(self):
        """SC3 : iterate sans worktree rend 2 et nomme execute (pas SystemExit)."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            plan = repo / "plan.json"
            plan.write_text('{"task":"absent"}', encoding="utf-8")
            err = io.StringIO()
            try:
                with patch("sys.stderr", err):
                    code = main(
                        [
                            "iterate",
                            str(plan),
                            "--task-name",
                            "absent-xyz",
                            "--repo",
                            str(repo),
                        ]
                    )
            except SystemExit as exc:
                self.fail(
                    f"SystemExit({exc.code}) : iterate doit rendre 2 via main, "
                    "pas lever SystemExit (sous-commande absente = code d'avant)."
                )
            self.assertEqual(2, code)
            self.assertIn("execute", err.getvalue())

    def test_iterate_reuses_existing_worktree(self):
        """SC3 : iterate ne crée aucun répertoire sous .forgepilot/worktrees/."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=repo, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=repo, check=True)
            (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
            subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)
            create_worktree(repo, "iter-reuse", "main")
            worktrees = repo / ".forgepilot" / "worktrees"
            before = sorted(p.name for p in worktrees.iterdir())
            plan = repo / "plan.json"
            plan.write_text('{"task":"reuse"}', encoding="utf-8")
            with patch("sys.stdout", new_callable=io.StringIO):
                code = main(
                    [
                        "iterate",
                        str(plan),
                        "--task-name",
                        "iter-reuse",
                        "--repo",
                        str(repo),
                    ]
                )
            after = sorted(p.name for p in worktrees.iterdir())
            self.assertEqual(0, code)
            self.assertEqual(before, after)

    def test_iterate_carries_sandbox_and_does_not_run_without_flag(self):
        """SC3 : --sandbox enabled présent ; sans --run rien n'est exécuté."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            subprocess.run(["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=repo, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=repo, check=True)
            (repo / "tracked.txt").write_text("initial\n", encoding="utf-8")
            subprocess.run(["git", "add", "tracked.txt"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "initial"], cwd=repo, check=True, capture_output=True)
            create_worktree(repo, "iter-sandbox", "main")
            plan = repo / "plan.json"
            plan.write_text('{"task":"sandbox"}', encoding="utf-8")
            out = io.StringIO()
            with patch("forgepilot.cli.execute_invocation", side_effect=AssertionError("execute_invocation appelé")):
                with patch("forgepilot.workflow.run_command", side_effect=AssertionError("run_command appelé")):
                    with patch("sys.stdout", out):
                        code = main(
                            [
                                "iterate",
                                str(plan),
                                "--task-name",
                                "iter-sandbox",
                                "--repo",
                                str(repo),
                            ]
                        )
            self.assertEqual(0, code)
            text = out.getvalue()
            brace = text.find("{")
            self.assertGreaterEqual(brace, 0)
            payload = json.loads(text[brace:])
            argv = payload["argv"]
            sandbox_index = argv.index("--sandbox")
            self.assertEqual("enabled", argv[sandbox_index + 1])

    def test_claude_flags_order_unchanged_after_dash_p(self):
        """SC2 : ordre exact des drapeaux Claude après -p (identique à 75b3dd0)."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            task = root / "task.md"
            task.write_text("Ordre des drapeaux.", encoding="utf-8")
            invocation = plan_invocation(SETTINGS, root, task)

        p_index = invocation.argv.index("-p")
        expected = [
            "--output-format",
            "json",
            "--permission-mode",
            "plan",
            "--tools",
            READ_ONLY_CLAUDE_TOOLS,
            "--disallowedTools",
            "mcp__*",
            "--safe-mode",
            "--disable-slash-commands",
            "--no-chrome",
            "--no-session-persistence",
        ]
        self.assertEqual(list(invocation.argv[p_index + 1 : p_index + 1 + len(expected)]), expected)

    def test_format_invocation_hides_prompt_keeps_output_format(self):
        """Garde de non-régression : filtre startswith('--') après -p.

        Sur le code d'avant, format_invocation masquait déjà l'élément suivant
        -p ; ce test ne prouve donc pas le lot 1 en rouge. Il garde le filtre
        startswith('--') du nouveau format_invocation : sans lui,
        --output-format juste après -p serait remplacé par <prompt>.
        """
        bound = 32 * os.sysconf("SC_PAGESIZE")
        marker = "PROMPT_LEAK_MARKER_022_b9e1"
        synthetic = marker + ("E" * 256)
        self.assertLess(len(synthetic.encode()), bound)

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plan = root / "plan.json"
            plan.write_text('{"task":"no leak"}', encoding="utf-8")
            with patch("forgepilot.workflow.git", return_value=synthetic):
                invocation = review_invocation(SETTINGS, root, plan, "origin/master")

        formatted = format_invocation(invocation)
        self.assertNotIn(marker, formatted)
        payload = json.loads(formatted)
        self.assertIn("-p", payload["argv"])
        p_index = payload["argv"].index("-p")
        # Réfutable : sans startswith("--"), l'élément suivant -p devient
        # "<prompt>" et --output-format disparaît de argv.
        self.assertEqual("--output-format", payload["argv"][p_index + 1])
        self.assertEqual("json", payload["argv"][p_index + 2])
        self.assertNotIn("<prompt>", payload["argv"])
        self.assertEqual("<prompt>", payload.get("prompt"))


if __name__ == "__main__":
    unittest.main()
