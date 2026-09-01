"""Crons indépendants : une boîte vide n'est pas un échec."""

from pathlib import Path

import pytest

from atelier import boite
from atelier.__main__ import main


def _carte(lot: str = "044-mineur") -> boite.Carte:
    return boite.Carte(
        lot=lot,
        brief=f"briefs/{lot}.md",
        fichiers=["sim/engine.py"],
    )


def test_prochain_vide_est_rien(tmp_path: Path):
    assert boite.prochain(tmp_path, "coder") is None


def test_coder_ne_depend_pas_du_planificateur(tmp_path: Path):
    # Grok a une carte en attente : Composer s'en fiche.
    boite.deposer(tmp_path, "a-planifier", _carte("046-mer"))
    boite.deposer(tmp_path, "a-coder", _carte("044-mineur"))
    prise = boite.prochain(tmp_path, "coder")
    assert prise is not None
    assert prise.lot == "044-mineur"
    assert boite.prochain(tmp_path, "planifier") is not None


def test_echec_ne_bloque_pas_l_autre_role(tmp_path: Path):
    boite.deposer(tmp_path, "a-briefer", _carte("044-mineur"))
    boite.deposer(tmp_path, "a-coder", _carte("047-bourg"))
    boite.echouer(tmp_path, "briefer", "044-mineur", "quota claude épuisé")
    assert boite.prochain(tmp_path, "briefer") is None
    assert boite.prochain(tmp_path, "coder").lot == "047-bourg"
    assert boite.lister(tmp_path, "echec")[0].note == "quota claude épuisé"


def test_avancer_briefer_va_au_coder_pas_au_planificateur(tmp_path: Path):
    boite.deposer(tmp_path, "a-briefer", _carte())
    cible = boite.avancer(tmp_path, "briefer", "044-mineur")
    assert cible.parent.name == "a-coder"
    assert boite.prochain(tmp_path, "planifier") is None


def test_cli_rien_sort_zero(tmp_path: Path):
    code = main(["prochain", "--projet", str(tmp_path), "--role", "coder"])
    assert code == 0


def test_cli_rien_affiche_rien(tmp_path: Path, capsys: pytest.CaptureFixture[str]):
    main(["prochain", "--projet", str(tmp_path), "--role", "relire"])
    assert capsys.readouterr().out.strip() == "RIEN"


def test_carte_vide_echoue(tmp_path: Path):
    dossier = boite._dossier(tmp_path, "a-coder")
    (dossier / "vide.json").write_text("{}\n", encoding="utf-8")
    with pytest.raises(boite.BoiteErreur):
        boite.lister(tmp_path, "a-coder")
