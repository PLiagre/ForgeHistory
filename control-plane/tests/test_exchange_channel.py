"""Le canal d'échange doit être invisible à Git ET visible à l'agent.

Personne ne tenait ces deux conditions ensemble. `.forgepilot/` satisfaisait
la première ; il est entré dans `.cursorignore` (commit b17a468) et a perdu la
seconde sans qu'aucun test ne rougisse. Le bundle de revue du lot 033 est
devenu illisible pour son relecteur, qui a rendu BLOCKED — un verdict produit
pour une panne de tuyau.

Le test précédent (`test_adr0017_routing`) vérifiait que `--add-dir` figurait
dans `argv` : une présence, pas une fonction. Ici on vérifie la propriété
elle-même, mécaniquement, sans lancer Cursor.
"""

from __future__ import annotations

from fnmatch import fnmatchcase
from pathlib import Path
import tempfile
import unittest

from forgepilot.config import load_settings
from forgepilot.exchange import EXCHANGE_DIRNAME, exchange_dir, stage_exchange
from forgepilot.process import PilotError
from forgepilot.workflow import review_invocation


DEPOT = Path(__file__).resolve().parent.parent.parent


def _motifs(fichier: Path) -> list[str]:
    if not fichier.is_file():
        return []
    motifs = []
    for ligne in fichier.read_text(encoding="utf-8").splitlines():
        nu = ligne.split("#", 1)[0].strip()
        if nu:
            motifs.append(nu.rstrip("/"))
    return motifs


def _ignore(motifs: list[str], chemin: str) -> bool:
    nu = chemin.rstrip("/")
    return any(
        fnmatchcase(nu, motif) or fnmatchcase(nu, f"{motif}/*") or nu.startswith(f"{motif}/")
        for motif in motifs
    )


class CanalEchangeTests(unittest.TestCase):
    def test_le_canal_n_est_jamais_cursor_ignore(self):
        """La condition qu'aucun test ne tenait, et qui a coûté le lot 033."""
        motifs = _motifs(DEPOT / ".cursorignore")
        self.assertTrue(motifs, "`.cursorignore` introuvable ou vide")
        self.assertFalse(
            _ignore(motifs, EXCHANGE_DIRNAME),
            f"{EXCHANGE_DIRNAME} est cursor-ignoré : l'agent ne pourra pas lire "
            "ce que ForgePilot lui tend.",
        )

    def test_le_canal_reste_git_ignore(self):
        """Sinon la copie salit l'arbre et le contrôle de périmètre la refuse."""
        motifs = _motifs(DEPOT / ".gitignore")
        self.assertTrue(_ignore(motifs, EXCHANGE_DIRNAME))

    def test_l_ancien_canal_montre_pourquoi_le_test_existe(self):
        """`.forgepilot/` est bien cursor-ignoré : la panne était réelle."""
        motifs = _motifs(DEPOT / ".cursorignore")
        self.assertTrue(
            _ignore(motifs, ".forgepilot"),
            "Si `.forgepilot/` cesse d'être cursor-ignoré, ce test perd son "
            "témoin — le relire avant de le supprimer.",
        )

    def test_copie_verifiee_et_chemin_relatif(self):
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            source = racine / "ailleurs" / "bundle.json"
            source.parent.mkdir(parents=True)
            source.write_text('{"plan": "x"}', encoding="utf-8")
            relatif = stage_exchange(racine, source, "review-bundle")
        self.assertEqual(f"{EXCHANGE_DIRNAME}/review-bundle.json", relatif)

    def test_source_absente_refusee(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(PilotError):
                stage_exchange(Path(tmp), Path(tmp) / "absent.json", "plan")

    def test_source_vide_refusee(self):
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            vide = racine / "vide.json"
            vide.write_text("   \n", encoding="utf-8")
            with self.assertRaises(PilotError):
                stage_exchange(racine, vide, "plan")


class BundleDuRelecteurTests(unittest.TestCase):
    def test_le_bundle_passe_par_le_canal_et_non_par_add_dir(self):
        """Le dossier du run contient `state.json` : il ne s'ouvre plus."""
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            plan = racine / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            dossier_run = racine / ".forgepilot" / "runs" / "run-1"
            dossier_run.mkdir(parents=True)
            (dossier_run / "state.json").write_text('{"secret":"état"}', encoding="utf-8")
            marqueur = "BUNDLE_LONG_SECRET_MARKER"
            bundle = dossier_run / "review-bundle-abc.json"
            bundle.write_text(marqueur + ("x" * 250_000), encoding="utf-8")

            invocation = review_invocation(
                settings, racine, plan, "HEAD", risk="R1", bundle_path=bundle
            )
            copie = exchange_dir(racine) / "review-bundle.json"
            self.assertTrue(copie.is_file())
            self.assertTrue(copie.read_text(encoding="utf-8").startswith(marqueur))

        prompt = invocation.argv[invocation.argv.index("-p") + 1]
        self.assertIn(f"{EXCHANGE_DIRNAME}/review-bundle.json", prompt)
        self.assertIn("Lis intégralement", prompt)
        self.assertNotIn(marqueur, prompt)
        self.assertNotIn("--add-dir", invocation.argv)
        self.assertNotIn(str(dossier_run), " ".join(invocation.argv))

    def test_le_prompt_nomme_la_sortie_attendue_si_le_bundle_est_illisible(self):
        settings = load_settings()
        with tempfile.TemporaryDirectory() as tmp:
            racine = Path(tmp)
            plan = racine / "plan.json"
            plan.write_text('{"task":"x"}', encoding="utf-8")
            bundle = racine / "bundle.json"
            bundle.write_text('{"plan":"x"}', encoding="utf-8")
            invocation = review_invocation(
                settings, racine, plan, "HEAD", risk="R1", bundle_path=bundle
            )
        prompt = invocation.argv[invocation.argv.index("-p") + 1]
        self.assertIn("material_unreadable", prompt)


if __name__ == "__main__":
    unittest.main()
