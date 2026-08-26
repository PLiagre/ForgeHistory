"""Les deux garde-fous que le lot 035 a rendus nécessaires.

Un lot ne peut plus consommer un agent hors de la machine à états, et ce
qu'il consomme laisse une trace lisible au même endroit que son état.
"""

from __future__ import annotations

import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from forgepilot.cli import main
from forgepilot.config import load_settings
from forgepilot.process import CommandResult
from forgepilot.workflow import (
    Invocation,
    execute_invocation,
    persist_usage,
    usage_summary,
)


def _invocation(role: str = "executor") -> Invocation:
    return Invocation(
        role,
        ("agent", "-p", "<prompt>"),
        ".",
        {},
        prompt="<prompt>",
        model="composer-2.5",
        backend="cursor",
    )


class UnCoupRefuseTests(unittest.TestCase):
    """`--run` sur une invocation d'agent isolée n'existe plus."""

    def _refus(self, argv: list[str]) -> str:
        err = io.StringIO()
        with tempfile.TemporaryDirectory() as tmp, patch("sys.stderr", err):
            code = main(argv + ["--repo", tmp])
        self.assertEqual(2, code, err.getvalue())
        return err.getvalue()

    def test_plan_run_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            tache = Path(tmp) / "brief.md"
            tache.write_text("# lot", encoding="utf-8")
            message = self._refus(["plan", str(tache), "--run"])
        self.assertIn("Invocation directe désactivée", message)
        self.assertIn("start --run", message)

    def test_execute_run_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = Path(tmp) / "plan.json"
            plan.write_text("{}", encoding="utf-8")
            message = self._refus(
                ["execute", str(plan), "--task-name", "direct", "--run"]
            )
        self.assertIn("Invocation directe désactivée", message)

    def test_review_run_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = Path(tmp) / "plan.json"
            plan.write_text("{}", encoding="utf-8")
            message = self._refus(["review", str(plan), "--run"])
        self.assertIn("Invocation directe désactivée", message)

    def test_previews_still_work_without_run(self):
        """Le refus porte sur la dépense, pas sur la lisibilité."""
        with tempfile.TemporaryDirectory() as tmp:
            tache = Path(tmp) / "brief.md"
            tache.write_text("# lot", encoding="utf-8")
            sortie = io.StringIO()
            with patch("sys.stdout", sortie):
                code = main(["plan", str(tache), "--repo", tmp, "--risk", "R1"])
            self.assertEqual(0, code)
            apercu = json.loads(sortie.getvalue())
            self.assertEqual("planner", apercu["role"])


class ComptabiliteTests(unittest.TestCase):
    """L'enveloppe du fournisseur était jetée : le coût d'un lot était nul."""

    def test_envelope_is_kept_without_the_business_payload(self):
        with tempfile.TemporaryDirectory() as tmp:
            cible = persist_usage(
                Path(tmp),
                _invocation(),
                {
                    "type": "result",
                    "duration_ms": 1234,
                    "is_error": False,
                    "usage": {"input_tokens": 900, "output_tokens": 100},
                    "result": "le JSON métier, archivé ailleurs",
                },
            )
            self.assertIsNotNone(cible)
            assert cible is not None
            garde = json.loads(cible.read_text(encoding="utf-8"))
            self.assertEqual("executor", garde["role"])
            self.assertEqual("composer-2.5", garde["model"])
            self.assertEqual(1234, garde["envelope"]["duration_ms"])
            self.assertEqual(900, garde["envelope"]["usage"]["input_tokens"])
            self.assertNotIn("result", garde["envelope"])

    def test_summary_adds_up_by_role_and_stays_silent_on_the_unknown(self):
        with tempfile.TemporaryDirectory() as tmp:
            for _ in range(2):
                persist_usage(
                    Path(tmp),
                    _invocation("executor"),
                    {"duration_ms": 10, "usage": {"input_tokens": 5}},
                )
            persist_usage(
                Path(tmp), _invocation("reviewer"), {"duration_ms": 7}
            )
            total = usage_summary(Path(tmp))
        self.assertEqual(3, total["invocations"])
        executant = total["par_role"]["executor"]
        self.assertEqual(2, executant["invocations"])
        self.assertEqual(20, executant["duration_ms"])
        self.assertEqual(10, executant["usage.input_tokens"])
        # Le relecteur n'a pas déclaré de jetons : aucun zéro n'est inventé.
        self.assertNotIn("usage.input_tokens", total["par_role"]["reviewer"])

    def test_a_provider_without_counters_writes_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertIsNone(persist_usage(Path(tmp), _invocation(), {"result": "x"}))
            self.assertEqual({"invocations": 0, "par_role": {}}, usage_summary(Path(tmp)))


    def test_execute_invocation_measures_before_it_unwraps(self):
        """L'ordre est le tout : dépouillée, l'enveloppe n'existe plus."""
        enveloppe = {
            "type": "result",
            "duration_ms": 42,
            "usage": {"input_tokens": 12},
            "result": json.dumps(
                {
                    "summary": "fait",
                    "files_modified": [],
                    "checks": [],
                    "blockages": [],
                }
            ),
        }
        invocation = Invocation(
            "executor",
            ("agent", "-p", "<prompt>", "--output-format", "json"),
            ".",
            {},
            prompt="<prompt>",
            model="composer-2.5",
            backend="cursor",
        )
        with tempfile.TemporaryDirectory() as tmp, patch(
            "forgepilot.workflow.resolve_binary", return_value="agent"
        ), patch(
            "forgepilot.workflow.run_command",
            return_value=CommandResult(("agent",), 0, json.dumps(enveloppe), ""),
        ):
            produit = execute_invocation(
                invocation, load_settings(), usage_dir=Path(tmp)
            )
            total = usage_summary(Path(tmp))

        self.assertEqual("fait", produit["summary"])
        self.assertEqual(1, total["invocations"])
        self.assertEqual(42, total["par_role"]["executor"]["duration_ms"])
        self.assertEqual(12, total["par_role"]["executor"]["usage.input_tokens"])


if __name__ == "__main__":
    unittest.main()
