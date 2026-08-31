"""Neutralité des fournisseurs.

Le nom historique du fichier est conservé pour la continuité de la suite.
Il ne teste plus l'exclusion d'un fournisseur.
"""

import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.cli import main, parser
from forgepilot.config import load_settings
from forgepilot.policy import load_policy
from forgepilot.workflow import missing_binaries


class ProviderNeutralityTests(unittest.TestCase):
    def test_default_controller_has_no_reserved_capability(self):
        settings = load_settings()
        assert settings.policy is not None
        controller = settings.policy.controller
        self.assertEqual("configurable", controller.provider)
        self.assertTrue(controller.can_plan)
        self.assertTrue(controller.can_review)
        self.assertTrue(controller.can_merge)

    def test_policy_parser_accepts_an_unlisted_backend(self):
        source = Path(__file__).parents[1] / "workflow-policy.toml"
        text = source.read_text(encoding="utf-8")
        text = text.replace(
            '[risks.R1.roles.planner]\nbackend = "cursor"',
            '[risks.R1.roles.planner]\nbackend = "outil-libre"',
            1,
        )
        with tempfile.TemporaryDirectory() as tmp:
            policy_path = Path(tmp) / "policy.toml"
            policy_path.write_text(text, encoding="utf-8")
            policy = load_policy(policy_path)
        self.assertEqual("outil-libre", policy.profile("R1").roles["planner"].backend)

    def test_policy_parser_accepts_any_controller_provider(self):
        source = Path(__file__).parents[1] / "workflow-policy.toml"
        text = source.read_text(encoding="utf-8").replace(
            'provider = "configurable"', 'provider = "fournisseur-libre"', 1
        )
        with tempfile.TemporaryDirectory() as tmp:
            policy_path = Path(tmp) / "policy.toml"
            policy_path.write_text(text, encoding="utf-8")
            policy = load_policy(policy_path)
        self.assertEqual("fournisseur-libre", policy.controller.provider)

    def test_cli_help_reserves_no_action_to_a_provider(self):
        helps = "\n".join(
            choice.help or ""
            for action in parser()._actions
            for choice in getattr(action, "_choices_actions", ())
        ).lower()
        self.assertNotIn("par claude", helps)
        self.assertNotIn("réservé à", helps)

    def test_prompts_do_not_require_separate_people(self):
        prompts = Path(__file__).parents[1] / "prompts"
        combined = "\n".join(
            path.read_text(encoding="utf-8").lower()
            for path in sorted(prompts.glob("*.md"))
        )
        self.assertNotIn("unique exécutant", combined)
        self.assertNotIn("relecteur indépendant", combined)
        self.assertNotIn("ne juge pas son propre", combined)
        self.assertIn("peut aussi avoir participé", combined)

    def test_missing_binaries_checks_only_the_configured_helper(self):
        settings = load_settings()
        checked: list[str] = []

        def record(name: str) -> str:
            checked.append(name)
            return name

        with patch("forgepilot.workflow.resolve_binary", side_effect=record):
            self.assertEqual([], list(missing_binaries(settings)))
        self.assertEqual(["git", "gh", settings.cursor_binary], checked)

    def test_doctor_checks_only_configured_authentication_helpers(self):
        calls: list[list[str]] = []
        output = io.StringIO()
        with tempfile.TemporaryDirectory() as tmp, patch(
            "forgepilot.cli.missing_binaries", return_value=[]
        ), patch("forgepilot.cli.git", return_value="master"), patch(
            "forgepilot.cli.run_command",
            side_effect=lambda command, **_: calls.append(list(command)),
        ), patch("sys.stdout", output):
            code = main(["doctor", "--repo", tmp, "--check-auth"])
        self.assertEqual(0, code)
        self.assertEqual([["agent", "status"], ["gh", "auth", "status"]], calls)

    def test_no_extra_witness_orchestration_is_added(self):
        subparsers = next(
            action
            for action in parser()._actions
            if hasattr(action, "choices") and action.choices
        )
        self.assertNotIn("witness", subparsers.choices)


if __name__ == "__main__":
    unittest.main()
