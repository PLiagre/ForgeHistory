"""Trois silences que le pilote transformait en décisions.

1. Sans `--risk`, `workflow-policy.toml` ne décidait plus rien : le rôle
   retombait sur un défaut historique codé en dur. C'est ainsi que
   `forgepilot review` appelait Claude alors que la politique nomme Cursor.
2. Un relecteur incapable de lire son bundle rendait BLOCKED, et le harnais
   archivait ce verdict comme un jugement sur le produit.
3. Une réponse fournisseur refusée partait dans un message d'erreur et nulle
   part ailleurs, donc restait indiagnosticable.
"""

from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest
import unittest.mock

from forgepilot.config import load_settings
from forgepilot.process import PilotError
from forgepilot.protocol import validate_review
from forgepilot.workflow import (
    Invocation,
    persist_failure_trace,
    plan_invocation,
    review_invocation,
    witness_invocation,
)


def _revue(**extra: object) -> dict[str, object]:
    revue = {
        "verdict": "BLOCKED",
        "acceptance_criteria": [{"criterion": "c1", "status": "PASS"}],
        "findings": [],
        "checks_observed": [{"check": "tests", "status": "PASS"}],
        "human_decision_required": True,
    }
    revue.update(extra)
    return revue


class PolitiqueFermeeTests(unittest.TestCase):
    def setUp(self):
        self.settings = load_settings()
        self.assertIsNotNone(self.settings.policy)

    def test_revue_sans_risque_refusee(self):
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            plan = racine / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            with self.assertRaises(PilotError) as capture:
                review_invocation(self.settings, racine, plan, "HEAD")
        self.assertIn("Aucun risque déclaré", str(capture.exception))

    def test_plan_sans_risque_refuse(self):
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            tache = racine / "task.md"
            tache.write_text("Lot mesurable.", encoding="utf-8")
            with self.assertRaises(PilotError):
                plan_invocation(self.settings, racine, tache)

    def test_temoin_reste_exempte(self):
        """ADR-0017 : le témoin est nommé par [witness], pas par le profil."""
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            plan = racine / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            bundle = racine / "bundle.json"
            bundle.write_text('{"plan":"x"}', encoding="utf-8")
            invocation = witness_invocation(
                self.settings, racine, plan, "HEAD", bundle_path=bundle
            )
        self.assertEqual("claude", invocation.backend)


class CauseDeBlocageTests(unittest.TestCase):
    def test_materiel_illisible_accepte_sans_critere_bloque(self):
        revue = validate_review(_revue(blocked_reason="material_unreadable"))
        self.assertEqual("material_unreadable", revue["blocked_reason"])

    def test_materiel_illisible_dispense_de_la_liste_des_criteres(self):
        """Le plan vit DANS le bundle : illisible, ses critères le sont aussi."""
        revue = validate_review(
            _revue(blocked_reason="material_unreadable"),
            expected_criteria=["un critère que le relecteur n'a jamais pu lire"],
        )
        self.assertEqual("BLOCKED", revue["verdict"])

    def test_blocage_produit_reste_tenu_aux_criteres(self):
        with self.assertRaises(PilotError):
            validate_review(_revue(blocked_reason="product"))

    def test_cause_inconnue_refusee(self):
        with self.assertRaises(PilotError):
            validate_review(_revue(blocked_reason="parce que"))

    def test_cause_sans_verdict_bloque_refusee(self):
        with self.assertRaises(PilotError):
            validate_review(
                _revue(
                    verdict="PASS",
                    blocked_reason="product",
                    acceptance_criteria=[{"criterion": "c1", "status": "PASS"}],
                )
            )


class TraceBruteTests(unittest.TestCase):
    def _invocation(self) -> Invocation:
        return Invocation(
            "reviewer",
            ("agent", "-p", "PROMPT SECRET", "--mode", "ask"),
            ".",
            {},
            "PROMPT SECRET",
            backend="cursor",
        )

    def test_sortie_brute_archivee_et_prompt_caviarde(self):
        erreur = PilotError(
            "Cursor a réussi sans rendre le JSON métier attendu.",
            raw="voici PROMPT SECRET puis de la prose au lieu du JSON",
        )
        with tempfile.TemporaryDirectory() as tmp:
            cible = persist_failure_trace(Path(tmp), "reviewer", self._invocation(), erreur)
            self.assertIsNotNone(cible)
            corps = cible.read_text(encoding="utf-8")
            enveloppe = json.loads(
                next(Path(tmp).glob("traces/*-envelope.json")).read_text(encoding="utf-8")
            )
        self.assertIn("de la prose au lieu du JSON", corps)
        self.assertNotIn("PROMPT SECRET", corps)
        self.assertEqual("cursor", enveloppe["backend"])
        self.assertIn("JSON métier", enveloppe["error"])

    def test_message_stable_pour_le_compteur_d_echecs(self):
        """Deux échecs identiques doivent produire le MÊME message.

        `_record_step_failure` compte les échecs consécutifs par signature du
        message. Un chemin horodaté dedans, et « trois fois la même panne »
        ne se déclenche plus jamais.
        """
        from forgepilot.config import load_settings
        from forgepilot.workflow import execute_invocation

        settings = load_settings()
        invocation = self._invocation()
        messages = []
        with tempfile.TemporaryDirectory() as tmp:
            for _ in range(2):
                with unittest.mock.patch(
                    "forgepilot.workflow.resolve_binary", return_value="agent"
                ), unittest.mock.patch(
                    "forgepilot.workflow.run_command",
                    side_effect=PilotError("panne", raw="prose au lieu du JSON"),
                ):
                    with self.assertRaises(PilotError) as capture:
                        execute_invocation(invocation, settings, trace_dir=Path(tmp))
                messages.append(str(capture.exception))
            traces = sorted((Path(tmp) / "traces").glob("*-raw.txt"))
        self.assertEqual(messages[0], messages[1])
        self.assertEqual(2, len(traces))

    def test_sans_sortie_brute_aucune_trace(self):
        with tempfile.TemporaryDirectory() as tmp:
            cible = persist_failure_trace(
                Path(tmp), "reviewer", self._invocation(), PilotError("sec")
            )
            self.assertIsNone(cible)
            self.assertFalse((Path(tmp) / "traces").exists())


if __name__ == "__main__":
    unittest.main()
