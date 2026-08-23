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


def _argument_size_bound() -> int:
    """Borne prudente portable : ARG_MAX Unix, CreateProcess Windows."""
    if hasattr(os, "sysconf"):
        return 32 * os.sysconf("SC_PAGESIZE")
    return 32_768


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
        bound = _argument_size_bound()
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
        bound = _argument_size_bound()
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

    # --- additions brief 023 (modèle / effort par rôle) -------------------
    # Imports des symboles 023 volontairement locaux : la preuve rouge
    # restaure config/workflow/cli d'avant ; un import module-level ferait
    # échouer tout le fichier avant les trois tests non négociables.

    def _write_config(self, root: Path, body: str) -> Path:
        path = root / "config.toml"
        path.write_text(body, encoding="utf-8")
        return path

    def _base_toml(self, *, tools: str = "", roles: str = "") -> str:
        return (
            "[project]\n"
            'id = "test"\n'
            'engine_repository = "owner/engine"\n'
            'city_repository = "owner/city"\n'
            'default_base_ref = "origin/main"\n'
            'default_base_branch = "main"\n'
            "\n"
            "[tools]\n"
            'claude_binary = "claude"\n'
            'cursor_binary = "agent"\n'
            f"{tools}"
            'timeout_seconds = 30\n'
            f"{roles}"
        )

    def test_settings_ten_fields_still_constructible(self):
        """D2 / D7 : Settings reste constructible avec exactement dix champs.

        Jamais rouge sur le code d'avant : la garde D2 vérifie une propriété
        déjà vraie avant le lot (champ `roles` à défaut). Documenté ici pour
        la règle n° 4 — ce n'est pas un oubli de preuve rouge.
        """
        settings = Settings(
            "t",
            "o/e",
            "o/c",
            "origin/main",
            "main",
            "claude",
            "agent",
            "",
            "auto",
            30,
        )
        self.assertEqual({}, getattr(settings, "roles", {}))
        self.assertEqual("", settings.claude_model)

    def test_two_claude_roles_can_carry_distinct_models(self):
        """D7.1 : plan et review portent deux --model différents."""
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles=(
                        "\n[roles.planner]\n"
                        'model = "model-planner-test-a"\n'
                        'effort = "high"\n'
                        "\n[roles.reviewer]\n"
                        'model = "model-reviewer-test-b"\n'
                        'effort = "low"\n'
                    ),
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            task.write_text("deux modèles", encoding="utf-8")
            plan = root / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            plan_inv = plan_invocation(settings, root, task)
            with patch("forgepilot.workflow.git", return_value="diff court"):
                review_inv = review_invocation(settings, root, plan, "origin/main")

        plan_model = plan_inv.argv[plan_inv.argv.index("--model") + 1]
        review_model = review_inv.argv[review_inv.argv.index("--model") + 1]
        self.assertNotEqual(plan_model, review_model)
        self.assertEqual("model-planner-test-a", plan_model)
        self.assertEqual("model-reviewer-test-b", review_model)

    def test_cli_model_flag_beats_roles_section(self):
        """D7.2 : --model passé à l'appel l'emporte sur [roles.*]."""
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = "tools-fallback"\ncursor_model = "auto"\n',
                    roles=(
                        "\n[roles.planner]\n"
                        'model = "roles-planner"\n'
                        'effort = "medium"\n'
                    ),
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            task.write_text("priorité cli", encoding="utf-8")
            override = "cli-override-model"
            invocation = plan_invocation(
                settings, root, task, model=override, effort="low"
            )
            self.assertEqual(override, invocation.argv[invocation.argv.index("--model") + 1])
            self.assertNotEqual(
                settings.roles["planner"].model,
                invocation.argv[invocation.argv.index("--model") + 1],
            )

    def test_effort_refused_on_cursor_execute_and_iterate(self):
        """D7.3 : --effort sur execute/iterate rend 2 via main, avec explication."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            plan = repo / "plan.json"
            plan.write_text('{"task":"effort"}', encoding="utf-8")
            for command in ("execute", "iterate"):
                err = io.StringIO()
                argv = [
                    command,
                    str(plan),
                    "--task-name",
                    "effort-xyz",
                    "--repo",
                    str(repo),
                    "--effort",
                    "high",
                ]
                try:
                    with patch("sys.stderr", err):
                        code = main(argv)
                except SystemExit as exc:
                    self.fail(
                        f"SystemExit({exc.code}) : {command} --effort doit rendre 2 "
                        "via PilotError, pas SystemExit argparse."
                    )
                self.assertEqual(2, code)
                message = err.getvalue()
                self.assertIn("cuit", message.lower())
                self.assertIn("modèle", message.lower())

    def test_effort_key_refused_under_roles_executor(self):
        """D4 : clé effort sous [roles.executor] refusée au chargement."""
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles=(
                        "\n[roles.executor]\n"
                        'model = "composer-test"\n'
                        'effort = "high"\n'
                    ),
                ),
            )
            with self.assertRaises(PilotError) as ctx:
                load_settings(cfg)
            self.assertIn("cuit", str(ctx.exception).lower())

    def test_unknown_role_refused_naming_three_valid(self):
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles='\n[roles.zorglub]\nmodel = "x"\n',
                ),
            )
            with self.assertRaises(PilotError) as ctx:
                load_settings(cfg)
            message = str(ctx.exception)
            self.assertIn("planner", message)
            self.assertIn("reviewer", message)
            self.assertIn("executor", message)

    def test_effort_transmitted_to_both_claude_roles(self):
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles=(
                        "\n[roles.planner]\n"
                        'model = "m-plan"\n'
                        'effort = "xhigh"\n'
                        "\n[roles.reviewer]\n"
                        'model = "m-review"\n'
                        'effort = "low"\n'
                    ),
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            task.write_text("effort", encoding="utf-8")
            plan = root / "plan.json"
            plan.write_text('{"task":"e"}', encoding="utf-8")
            plan_inv = plan_invocation(settings, root, task)
            with patch("forgepilot.workflow.git", return_value="d"):
                review_inv = review_invocation(settings, root, plan, "origin/main")
        self.assertEqual("xhigh", plan_inv.argv[plan_inv.argv.index("--effort") + 1])
        self.assertEqual("low", review_inv.argv[review_inv.argv.index("--effort") + 1])

    def test_all_five_effort_levels_accepted_sixth_refused(self):
        from forgepilot.config import EFFORT_LEVELS, load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for level in EFFORT_LEVELS:
                cfg = self._write_config(
                    root,
                    self._base_toml(
                        tools='claude_model = ""\ncursor_model = "auto"\n',
                        roles=(
                            f"\n[roles.planner]\nmodel = \"m\"\neffort = \"{level}\"\n"
                        ),
                    ),
                )
                settings = load_settings(cfg)
                self.assertEqual(level, settings.roles["planner"].effort)
            bad = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles='\n[roles.planner]\nmodel = "m"\neffort = "ultra"\n',
                ),
            )
            with self.assertRaises(PilotError):
                load_settings(bad)

    def test_cli_effort_ultra_refused_on_plan(self):
        """SC2c / F1 : --effort ultra sur plan rend 2 (priorité 1 de D3)."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            task = root / "task.md"
            task.write_text("effort invalide", encoding="utf-8")
            err = io.StringIO()
            argv = [
                "plan",
                str(task),
                "--repo",
                str(root),
                "--effort",
                "ultra",
            ]
            try:
                with patch("sys.stderr", err):
                    code = main(argv)
            except SystemExit as exc:
                self.fail(
                    f"SystemExit({exc.code}) : plan --effort ultra doit rendre 2 "
                    "via PilotError, pas SystemExit argparse."
                )
            self.assertEqual(2, code)
            message = err.getvalue()
            self.assertIn("invalide", message.lower())
            self.assertIn("ultra", message)

    def test_role_beats_tools_claude_model(self):
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = "from-tools"\ncursor_model = "auto"\n',
                    roles='\n[roles.planner]\nmodel = "from-roles"\neffort = "medium"\n',
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            task.write_text("priorité rôle", encoding="utf-8")
            invocation = plan_invocation(settings, root, task)
            self.assertEqual(
                "from-roles", invocation.argv[invocation.argv.index("--model") + 1]
            )

    def test_fallback_to_tools_without_roles(self):
        """Sans [roles.*], comportement identique à avant (repli [tools])."""
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = "tools-only-model"\ncursor_model = "auto"\n',
                ),
            )
            settings = load_settings(cfg)
            self.assertEqual({}, settings.roles)
            task = root / "task.md"
            task.write_text("repli", encoding="utf-8")
            invocation = plan_invocation(settings, root, task)
            self.assertEqual(
                "tools-only-model",
                invocation.argv[invocation.argv.index("--model") + 1],
            )
            self.assertNotIn("--effort", invocation.argv)

    def test_no_model_flag_when_nothing_declared(self):
        """Sans modèle déclaré, aucun --model — déjà vrai avant le lot.

        Jamais rouge sur le code d'avant : comportement préexistant documenté
        pour la règle n° 4 (précédent lot 022).
        """
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            task.write_text("rien", encoding="utf-8")
            invocation = plan_invocation(settings, root, task)
            self.assertNotIn("--model", invocation.argv)

    def test_preview_shows_model_and_effort_hides_prompt(self):
        from forgepilot.config import load_settings

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cfg = self._write_config(
                root,
                self._base_toml(
                    tools='claude_model = ""\ncursor_model = "auto"\n',
                    roles=(
                        "\n[roles.planner]\n"
                        'model = "preview-model"\n'
                        'effort = "high"\n'
                    ),
                ),
            )
            settings = load_settings(cfg)
            task = root / "task.md"
            marker = "PROMPT_LEAK_023_UNIQUE_c4d2"
            task.write_text(f"tâche avec {marker}", encoding="utf-8")
            invocation = plan_invocation(settings, root, task)
            formatted = format_invocation(invocation)
            payload = json.loads(formatted)
            self.assertEqual("preview-model", payload.get("model"))
            self.assertEqual("high", payload.get("effort"))
            self.assertEqual("<prompt>", payload.get("prompt"))
            self.assertNotIn(marker, formatted)

    def test_resolve_role_priority_order(self):
        from forgepilot.config import RoleSettings
        from forgepilot.workflow import resolve_role

        settings = Settings(
            "t",
            "o/e",
            "o/c",
            "origin/main",
            "main",
            "claude",
            "agent",
            "tools-model",
            "auto",
            30,
            roles={"planner": RoleSettings(model="roles-model", effort="medium")},
        )
        cli = resolve_role(settings, "planner", model="cli-model", effort="max")
        self.assertEqual("cli-model", cli.model)
        self.assertEqual("max", cli.effort)
        role = resolve_role(settings, "planner")
        self.assertEqual("roles-model", role.model)
        bare = Settings(
            "t",
            "o/e",
            "o/c",
            "origin/main",
            "main",
            "claude",
            "agent",
            "tools-model",
            "auto",
            30,
        )
        fallback = resolve_role(bare, "planner")
        self.assertEqual("tools-model", fallback.model)


class ChainTests(unittest.TestCase):
    def _git_repo(self, root: Path) -> None:
        subprocess.run(["git", "init", "-b", "main"], cwd=root, check=True, capture_output=True)
        subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
        subprocess.run(["git", "config", "user.name", "Test"], cwd=root, check=True)
        (root / "tracked.txt").write_text("initial\n", encoding="utf-8")
        (root / ".gitignore").write_text(".forgepilot/\n", encoding="utf-8")
        subprocess.run(["git", "add", "tracked.txt", ".gitignore"], cwd=root, check=True)
        subprocess.run(["git", "commit", "-m", "initial"], cwd=root, check=True, capture_output=True)

    def test_preview_does_not_run_agents_and_never_merges(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self._git_repo(repo)
            briefs = repo / "harness" / "queue" / "briefs" / "024-exemple"
            briefs.mkdir(parents=True)
            task = briefs / "brief.md"
            task.write_text("Un brief mesurable.\n", encoding="utf-8")
            out = io.StringIO()
            with patch(
                "forgepilot.workflow.execute_invocation",
                side_effect=AssertionError("execute_invocation appelé"),
            ):
                with patch(
                    "forgepilot.workflow.publish",
                    side_effect=AssertionError("publish appelé"),
                ):
                    with patch(
                        "forgepilot.workflow.create_worktree",
                        side_effect=AssertionError("create_worktree appelé"),
                    ):
                        with patch("sys.stdout", out):
                            code = main(
                                [
                                    "enchaine",
                                    str(task),
                                    "--repo",
                                    str(repo),
                                ]
                            )
            self.assertEqual(0, code)
            payload = json.loads(out.getvalue())
            self.assertEqual("enchaine", payload["command"])
            self.assertFalse(payload["run"])
            self.assertFalse(payload["fusion"])
            self.assertEqual(["plan", "execute", "publish", "review"], payload["steps"])
            self.assertEqual("024-exemple", payload["task_name"])
            self.assertTrue(payload["publish"]["draft"])
            dumped = json.dumps(payload)
            self.assertNotIn("pr merge", dumped)
            self.assertNotIn("gh merge", dumped)
            argv = payload["execute"]["argv"]
            self.assertIn("--sandbox", argv)
            self.assertEqual("enabled", argv[argv.index("--sandbox") + 1])

    def test_proposition_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            prop_dir = repo / "hermes" / "propositions"
            prop_dir.mkdir(parents=True)
            task = prop_dir / "PROPOSITION-20260820-exemple.md"
            task.write_text("Une proposition, pas un brief.\n", encoding="utf-8")
            err = io.StringIO()
            with patch("sys.stderr", err):
                code = main(["enchaine", str(task), "--repo", str(repo)])
            self.assertEqual(2, code)
            self.assertIn("proposition", err.getvalue().lower())

    def test_empty_task_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            task = repo / "task.md"
            task.write_text("   \n", encoding="utf-8")
            err = io.StringIO()
            with patch("sys.stderr", err):
                code = main(["enchaine", str(task), "--repo", str(repo)])
            self.assertEqual(2, code)
            self.assertIn("vide", err.getvalue().lower())

    def test_run_refuses_anthropic_api_key_before_agents(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            task = repo / "task.md"
            task.write_text("Faire une chose.\n", encoding="utf-8")
            err = io.StringIO()
            with patch.dict("os.environ", {"ANTHROPIC_API_KEY": "must-not-be-used"}, clear=False):
                with patch(
                    "forgepilot.workflow.execute_invocation",
                    side_effect=AssertionError("execute_invocation appelé"),
                ):
                    with patch("sys.stderr", err):
                        code = main(
                            [
                                "enchaine",
                                str(task),
                                "--repo",
                                str(repo),
                                "--run",
                            ]
                        )
            self.assertEqual(2, code)
            self.assertIn("ANTHROPIC_API_KEY", err.getvalue())

    def test_run_delegates_to_durable_controller_never_merge(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self._git_repo(repo)
            task = repo / "task.md"
            task.write_text("Lot unique mesurable.\n", encoding="utf-8")
            subprocess.run(["git", "add", "task.md"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "task"], cwd=repo, check=True, capture_output=True)
            state_path = repo / ".forgepilot" / "runs" / "run-1" / "state.json"
            registered = {"run_id": "run-1", "step": "REGISTERED", "fusion": False}
            completed = {
                "run_id": "run-1",
                "step": "COMPLETE",
                "branch": "agent/lot-chain",
                "pull_request": "https://example.test/pr/1",
                "fusion": False,
            }
            out = io.StringIO()
            with patch(
                "forgepilot.cli.register_run", return_value=(state_path, registered)
            ) as register, patch(
                "forgepilot.cli.resume_run", return_value=completed
            ) as resume, patch("sys.stdout", out):
                code = main(
                    [
                        "enchaine",
                        str(task),
                        "--repo",
                        str(repo),
                        "--task-name",
                        "lot-chain",
                        "--base",
                        "main",
                        "--run",
                    ]
                )
            self.assertEqual(0, code)
            payload = json.loads(out.getvalue())
            self.assertEqual(completed, payload["state"])
            register.assert_called_once()
            resume.assert_called_once()
            self.assertEqual((repo, "run-1"), resume.call_args.args[1:])
            self.assertNotIn("merge", json.dumps(payload).lower())

    def test_run_stops_if_plan_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self._git_repo(repo)
            task = repo / "task.md"
            task.write_text("Échec plan.\n", encoding="utf-8")
            subprocess.run(["git", "add", "task.md"], cwd=repo, check=True)
            subprocess.run(["git", "commit", "-m", "task"], cwd=repo, check=True, capture_output=True)
            err = io.StringIO()
            state_path = repo / ".forgepilot" / "runs" / "run-2" / "state.json"
            registered = {"run_id": "run-2", "step": "REGISTERED"}
            with patch(
                "forgepilot.cli.register_run", return_value=(state_path, registered)
            ), patch(
                "forgepilot.cli.resume_run", side_effect=PilotError("plan cassé")
            ), patch("sys.stderr", err):
                code = main(
                    [
                        "enchaine",
                        str(task),
                        "--repo",
                        str(repo),
                        "--run",
                    ]
                )
            self.assertEqual(2, code)
            self.assertIn("plan cassé", err.getvalue())
            worktrees = repo / ".forgepilot" / "worktrees"
            self.assertFalse(worktrees.exists())

    def test_effort_ultra_refused_on_enchaine(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            task = repo / "task.md"
            task.write_text("effort invalide\n", encoding="utf-8")
            err = io.StringIO()
            with patch("sys.stderr", err):
                code = main(
                    [
                        "enchaine",
                        str(task),
                        "--repo",
                        str(repo),
                        "--effort",
                        "ultra",
                    ]
                )
            self.assertEqual(2, code)
            self.assertIn("invalide", err.getvalue().lower())


if __name__ == "__main__":
    unittest.main()
