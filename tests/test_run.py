"""--run crée un worktree, un verrou, un canal, un état. Il n'invoque personne."""

from pathlib import Path
import subprocess

from atelier import echange, etat, verrou
from atelier.__main__ import main
from tests.test_cycle import _produit


def _git(racine: Path, *args: str) -> None:
    subprocess.run(
        [
            "git",
            "-c",
            "commit.gpgsign=false",
            "-c",
            "init.defaultBranch=master",
            *args,
        ],
        cwd=racine,
        check=True,
        capture_output=True,
    )


def _depot_git(tmp_path: Path) -> Path:
    racine = _produit(tmp_path)
    _git(racine, "init", "-b", "master")
    _git(racine, "config", "user.email", "atelier@test")
    _git(racine, "config", "user.name", "atelier")
    _git(racine, "add", ".")
    _git(racine, "commit", "-m", "graine")
    return racine


def test_run_prepare_sans_invoquer(tmp_path: Path):
    racine = _depot_git(tmp_path)
    code = main(
        [
            "start",
            str(racine / "briefs" / "001-un-changement.md"),
            "--projet",
            str(racine),
            "--run",
        ]
    )
    assert code == 0
    runs = etat.lister(racine)
    assert len(runs) == 1
    run = runs[0]
    assert run.auteur_code != run.relecteur
    assert run.etape is etat.Etape.CREE
    tableau = verrou.charger(racine)
    assert tableau.poses[0].lot == "001-un-changement"
    wt = Path(run.worktree)
    assert wt.is_dir()
    assert echange.git_ignore_le_canal(wt)
    assert (echange.dossier(wt) / "prompt-executant.txt").is_file()
    # Personne n'a été lancé : pas de log d'agent, pas de PR.
    assert not list(wt.glob("**/*.agent-log"))


# ------------------------------------------- un lot actif a son worktree

import pytest

from atelier import projet, worktree as worktree_mod
from atelier.__main__ import main as _main


def _lot_worktree(racine: Path, lot: str, *args: str) -> int:
    return _main(["worktree", "--projet", str(racine), "--lot", lot, *args])


def test_chemin_derive_du_produit_et_du_slug(tmp_path: Path, monkeypatch):
    """Deux lots, deux chemins. Le même lot, deux fois le même."""
    racine = _produit(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    a = worktree_mod.chemin_du_lot(racine, "046-mer")
    b = worktree_mod.chemin_du_lot(racine, "047-bourg")
    assert a != b
    assert a == worktree_mod.chemin_du_lot(racine, "046-mer")
    assert a.name.endswith("-046-mer")
    assert a.parent == (tmp_path / "wt")


def test_chemin_derive_sans_racine_declaree_est_a_cote_du_produit(tmp_path: Path, monkeypatch):
    racine = _produit(tmp_path)
    monkeypatch.delenv(worktree_mod.RACINE_WORKTREES, raising=False)
    assert worktree_mod.chemin_du_lot(racine, "046-mer").parent == racine.resolve().parent


def test_chemin_derive_un_lot_vide_est_refuse(tmp_path: Path):
    with pytest.raises(worktree_mod.WorktreeErreur):
        worktree_mod.chemin_du_lot(_produit(tmp_path), "   ")


def test_apercu_sans_run_ne_cree_rien(tmp_path: Path, monkeypatch, capsys):
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    assert _lot_worktree(racine, "046-mer") == 0
    chemin = Path(capsys.readouterr().out.strip())
    assert not chemin.exists(), "un aperçu n'est pas une dépense"


def test_run_cree_le_worktree_sur_la_branche_du_lot(tmp_path: Path, monkeypatch, capsys):
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    assert _lot_worktree(racine, "046-mer", "--run") == 0
    chemin = Path(capsys.readouterr().out.strip())
    assert chemin.is_dir()
    attendue = projet.charger(racine).branche_du_lot("046-mer")
    assert worktree_mod.courante(chemin) == attendue


def test_reprise_un_worktree_existant_garde_son_travail(tmp_path: Path, monkeypatch, capsys):
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    _lot_worktree(racine, "046-mer", "--run")
    chemin = Path(capsys.readouterr().out.strip())
    (chemin / "travail.txt").write_text("en cours\n", encoding="utf-8")
    _git(chemin, "add", "-A")
    _git(chemin, "-c", "user.email=a@b", "-c", "user.name=a", "commit", "-m", "travail")

    assert _lot_worktree(racine, "046-mer", "--run") == 0
    assert Path(capsys.readouterr().out.strip()) == chemin
    assert (chemin / "travail.txt").is_file()


def test_sale_un_worktree_modifie_n_est_pas_efface(tmp_path: Path, monkeypatch, capsys):
    """Le rouge, prouvé : l'atelier refuse, et ce qui traînait est encore là."""
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    _lot_worktree(racine, "046-mer", "--run")
    chemin = Path(capsys.readouterr().out.strip())
    sale = chemin / "pas-enregistre.txt"
    sale.write_text("du travail non enregistré\n", encoding="utf-8")

    assert _lot_worktree(racine, "046-mer", "--run") == 1
    assert "non enregistrées" in capsys.readouterr().err
    assert sale.is_file(), "l'atelier n'a rien effacé"


def test_libere_rend_le_repertoire_et_garde_la_branche(tmp_path: Path, monkeypatch, capsys):
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    _lot_worktree(racine, "046-mer", "--run")
    chemin = Path(capsys.readouterr().out.strip())
    branche = projet.charger(racine).branche_du_lot("046-mer")

    assert _lot_worktree(racine, "046-mer", "--liberer", "--run") == 0
    capsys.readouterr()
    assert not chemin.exists()
    fin = subprocess.run(
        ["git", "rev-parse", "--verify", "--quiet", f"refs/heads/{branche}"],
        cwd=racine, capture_output=True, text=True,
    )
    assert fin.returncode == 0, "la branche porte la PR : elle reste"


def test_libere_sans_run_ne_retire_rien(tmp_path: Path, monkeypatch, capsys):
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    _lot_worktree(racine, "046-mer", "--run")
    chemin = Path(capsys.readouterr().out.strip())
    assert _lot_worktree(racine, "046-mer", "--liberer") == 0
    capsys.readouterr()
    assert chemin.is_dir()


def test_deux_lots_disjoints_travaillent_dans_deux_worktrees(tmp_path: Path, monkeypatch, capsys):
    """Deux branches, deux répertoires : ce qu'on écrit dans l'un n'est pas dans l'autre."""
    racine = _depot_git(tmp_path)
    monkeypatch.setenv(worktree_mod.RACINE_WORKTREES, str(tmp_path / "wt"))
    chemins = {}
    for lot in ("046-mer", "047-bourg"):
        assert _lot_worktree(racine, lot, "--run") == 0
        chemins[lot] = Path(capsys.readouterr().out.strip())

    assert chemins["046-mer"] != chemins["047-bourg"]
    branches = {lot: worktree_mod.courante(c) for lot, c in chemins.items()}
    assert len(set(branches.values())) == 2

    (chemins["046-mer"] / "mer.txt").write_text("ports\n", encoding="utf-8")
    assert not (chemins["047-bourg"] / "mer.txt").exists()
