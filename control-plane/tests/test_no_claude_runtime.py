from __future__ import annotations

import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.cli import main, parser
from forgepilot.config import load_settings
from forgepilot.workflow import missing_binaries


class NoClaudeRuntimeTests(unittest.TestCase):
    def test_policy_disables_witness_and_rejects_every_claude_backend(self):
        settings = load_settings()
        self.assertIsNotNone(settings.policy)
        assert settings.policy is not None
        self.assertEqual("none", settings.policy.witness.backend)
        backends = {
            role.backend
            for profile in settings.policy.risks.values()
            for role in profile.roles.values()
        }
        backends.add(settings.policy.witness.backend)
        self.assertNotIn("claude", backends)

    def test_runtime_and_config_contain_no_claude_invocation_settings(self):
        root = Path(__file__).parents[1]
        runtime = "\n".join(
            (root / "forgepilot" / name).read_text(encoding="utf-8")
            for name in ("workflow.py", "cli.py", "config.py")
        )
        runtime += "\n" + (root / "config.toml").read_text(encoding="utf-8")
        self.assertNotIn("claude_binary", runtime)
        self.assertNotIn("claude_model", runtime)
        self.assertNotIn("_claude_argv(", runtime)
        self.assertNotIn('["claude",', runtime)

    def test_missing_binaries_never_checks_claude(self):
        settings = load_settings()
        checked: list[str] = []

        def record(name: str) -> str:
            checked.append(name)
            return name

        with patch("forgepilot.workflow.resolve_binary", side_effect=record):
            self.assertEqual([], list(missing_binaries(settings)))
        self.assertEqual(["git", "gh", settings.cursor_binary], checked)

    def test_doctor_check_auth_never_calls_claude_or_anthropic(self):
        calls: list[list[str]] = []
        with tempfile.TemporaryDirectory() as tmp, patch(
            "forgepilot.cli.missing_binaries", return_value=[]
        ), patch("forgepilot.cli.git", return_value="master"), patch(
            "forgepilot.cli.run_command",
            side_effect=lambda command, **_: calls.append(list(command)),
        ), patch.dict("os.environ", {"ANTHROPIC_API_KEY": "ignored"}, clear=False), patch(
            "sys.stdout", new_callable=io.StringIO
        ):
            code = main(["doctor", "--repo", tmp, "--check-auth"])

        self.assertEqual(0, code)
        flattened = " ".join(part for call in calls for part in call).lower()
        self.assertNotIn("claude", flattened)
        self.assertNotIn("anthropic", flattened)
        self.assertEqual([["agent", "status"], ["gh", "auth", "status"]], calls)

    def test_witness_entry_point_is_removed(self):
        subparsers = next(
            action for action in parser()._actions if hasattr(action, "choices") and action.choices
        )
        assert subparsers.choices is not None
        self.assertNotIn("witness", subparsers.choices)

    def test_witness_invocation_api_is_absent(self):
        from forgepilot import workflow

        self.assertFalse(hasattr(workflow, "witness_invocation"))


if __name__ == "__main__":
    unittest.main()
