"""Auteur ≠ relecteur. L'atelier ne fusionne pas. Sans --run, rien n'est écrit."""

from pathlib import Path
import subprocess
import sys

import pytest

from atelier import etat, projet
from atelier.__main__ import main
from atelier.cycle import preparer, CycleErreur
from atelier.etat import FusionInterdite
from tests.test_porte import BRIEF_SAIN


def _produit(tmp_path: Path) -> Path:
    (tmp_path / "briefs").mkdir()
    (tmp_path / "atelier.toml").write_text(
        """
[projet]
nom = "JeuTest"
briefs = "briefs"
tests = "python3 -m pytest tests/ -q"
fumee = "python3 -m jeu --ticks 0"
branche_base = "master"
prefixe_branche = "agent/"

[roles]
ecriture = "claude"
execution = "cursor"
controle = "codex"
""".lstrip(),
        encoding="utf-8",
    )
    brief = tmp_path / "briefs" / "001-un-changement.md"
    brief.write_text(BRIEF_SAIN, encoding="utf-8")
    return tmp_path


def test_roles_identiques_refuses(tmp_path: Path):
    (tmp_path / "atelier.toml").write_text(
        """
[projet]
nom = "X"
briefs = "briefs"
tests = "true"
fumee = "true"
branche_base = "master"
prefixe_branche = "agent/"

[roles]
ecriture = "cursor"
execution = "cursor"
controle = "cursor"
""".lstrip(),
        encoding="utf-8",
    )
    with pytest.raises(projet.ProjetIncomplet):
        projet.charger(tmp_path)


def test_apercu_sans_ecriture(tmp_path: Path):
    racine = _produit(tmp_path)
    apercu = preparer(racine / "briefs" / "001-un-changement.md", racine)
    assert apercu.executant == "cursor"
    assert apercu.relecteur == "codex"
    assert apercu.executant != apercu.relecteur
    assert not (racine / ".atelier").exists()
    assert list(racine.glob("atelier-echange/**/*")) == []


def test_start_sans_run_n_ecrit_rien(tmp_path: Path):
    racine = _produit(tmp_path)
    code = main(
        [
            "start",
            str(racine / "briefs" / "001-un-changement.md"),
            "--projet",
            str(racine),
        ]
    )
    assert code == 0
    assert not (racine / ".atelier").exists()


def test_fusionner_refuse():
    with pytest.raises(FusionInterdite):
        etat.fusionner(
            etat.nouveau(
                lot="001",
                brief=Path("briefs/001.md"),
                branche="agent/001",
                worktree=Path("/tmp/wt"),
                auteur_code="cursor",
                relecteur="codex",
                fichiers=["src/foo.py"],
            )
        )


def test_cli_fusionner_code_2():
    proc = subprocess.run(
        [sys.executable, "-m", "atelier", "fusionner"],
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 2
    assert "ne fusionne pas" in proc.stderr


def test_auteur_egal_relecteur_refuse():
    with pytest.raises(ValueError, match="relecteur"):
        etat.nouveau(
            lot="001",
            brief=Path("briefs/001.md"),
            branche="agent/001",
            worktree=Path("/tmp/wt"),
            auteur_code="cursor",
            relecteur="cursor",
            fichiers=["src/foo.py"],
        )


def test_brief_infirme_bloque_le_cycle(tmp_path: Path):
    racine = _produit(tmp_path)
    brief = racine / "briefs" / "001-un-changement.md"
    brief.write_text("# Brief 001\n\nrien.\n", encoding="utf-8")
    with pytest.raises(CycleErreur, match="porte"):
        preparer(brief, racine)
