"""CLI : couches, hop, doctor, ci, pr-etat."""

from pathlib import Path
import os
import re
import shutil
import subprocess
import sys

import pytest

from tests.test_cycle import _produit

RACINE = Path(__file__).resolve().parent.parent
besoin_bash = pytest.mark.skipif(shutil.which("bash") is None, reason="bash absent")


def _atelier(*args: str, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-m", "atelier", *args],
        capture_output=True,
        text=True,
        env=env,
    )


def _avec(variable: str, chemin: Path) -> dict[str, str]:
    env = {k: v for k, v in os.environ.items() if not k.startswith("ATELIER_")}
    env[variable] = str(chemin)
    return env


def _fausse_commande(dossier: Path, corps: str) -> Path:
    dossier.mkdir(parents=True, exist_ok=True)
    cible = dossier / "faux-gh"
    cible.write_text("#!/usr/bin/env bash\n" + corps, encoding="utf-8")
    cible.chmod(0o755)
    return cible


def test_couches_vertes():
    proc = _atelier("couches")
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.count("PASS") == 7


def test_hop_choisit():
    proc = _atelier("hop", "claude=-1", "cursor=40", "codex=5")
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.strip() == "cursor"


def test_hop_inconnu_refuse():
    proc = _atelier("hop", "claude=-1", "cursor=-1")
    assert proc.returncode == 1
    assert "inconnu" in proc.stderr


def test_doctor(tmp_path: Path):
    racine = _produit(tmp_path)
    proc = _atelier("doctor", "--projet", str(racine))
    assert proc.returncode == 0, proc.stderr
    assert "JeuTest" in proc.stdout


def test_doctor_sans_toml(tmp_path: Path):
    proc = _atelier("doctor", "--projet", str(tmp_path))
    assert proc.returncode == 1
    assert "atelier.toml" in proc.stderr


# ------------------------------------------ trois verdicts, trois codes


@besoin_bash
def test_ci_vert_rend_zero(tmp_path: Path):
    faux = _fausse_commande(tmp_path, 'printf "sim\\tpass\\t6m\\turl\\n"\n')
    proc = _atelier("ci", "--pr", "206", "--worktree", str(tmp_path),
                    env=_avec("ATELIER_CI_CMD", faux))
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.strip() == "vert"


@besoin_bash
def test_ci_rouge_rend_un_et_nomme(tmp_path: Path):
    faux = _fausse_commande(tmp_path, 'printf "sim\\tfail\\t6m\\turl\\n"\n')
    proc = _atelier("ci", "--pr", "206", "--worktree", str(tmp_path),
                    env=_avec("ATELIER_CI_CMD", faux))
    assert proc.returncode == 1
    assert proc.stdout.splitlines() == ["sim"]


@besoin_bash
def test_ci_inconnue_rend_deux(tmp_path: Path):
    """Un inconnu n'est pas un vert : il a son propre code."""
    faux = _fausse_commande(tmp_path, "exit 8\n")
    proc = _atelier("ci", "--pr", "206", "--worktree", str(tmp_path),
                    env=_avec("ATELIER_CI_CMD", faux))
    assert proc.returncode == 2
    assert proc.stdout.strip() == "inconnue"


@besoin_bash
def test_pr_etat_rend_ce_que_github_publie(tmp_path: Path):
    faux = _fausse_commande(tmp_path, 'printf \'{"state": "OPEN"}\'\n')
    proc = _atelier("pr-etat", "--pr", "206", "--worktree", str(tmp_path),
                    env=_avec("ATELIER_PR_CMD", faux))
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.strip() == "ouverte"


@besoin_bash
def test_pr_etat_inconnue_rend_deux(tmp_path: Path):
    faux = _fausse_commande(tmp_path, "exit 1\n")
    proc = _atelier("pr-etat", "--pr", "206", "--worktree", str(tmp_path),
                    env=_avec("ATELIER_PR_CMD", faux))
    assert proc.returncode == 2
    assert proc.stdout.strip() == "inconnue"


# ------------------------------------- un document qui vieillit se voit


def test_le_document_du_workflow_ne_cite_aucune_commande_fausse():
    """Une commande inventée fait rougir ce test.

    C'est la seule façon qu'un document ait de vieillir en se faisant
    remarquer.
    """
    document = RACINE / "docs" / "LE-WORKFLOW.md"
    assert document.is_file(), "docs/LE-WORKFLOW.md manque"
    connues = _sous_commandes()
    assert connues, "le parseur n'a listé aucune sous-commande"
    texte = document.read_text(encoding="utf-8")
    # Un appel se cite entre accents graves ou en tête de ligne dans un
    # bloc de code. « l'atelier lit » n'est pas un appel : on ne le lit pas.
    citees = set(re.findall(
        r"(?:^|`)(?:python3 -m )?atelier ([a-z][a-z-]*)", texte, re.M
    ))
    assert citees, "le document ne cite aucune commande — il ne décrit rien"
    inventees = sorted(c for c in citees if c not in connues)
    assert not inventees, f"commandes citées mais inconnues : {inventees}"


def _sous_commandes() -> set[str]:
    import argparse

    from atelier.__main__ import _parser

    for action in _parser()._actions:
        if isinstance(action, argparse._SubParsersAction):
            return set(action.choices)
    return set()


def _commandes_citees(documents: list[Path]) -> set[str]:
    """Les commandes que les documents du dépôt nomment.

    Le dénominateur vient des documents, pas d'une liste recopiée : une
    commande qu'on déplacerait sans la reposer disparaîtrait de la
    ligne de commande sans que rien ne le dise.
    """
    citees: set[str] = set()
    for document in documents:
        assert document.is_file(), f"{document} manque"
        citees |= set(re.findall(
            r"(?:^|`)(?:python3 -m )?atelier ([a-z][a-z-]*)",
            document.read_text(encoding="utf-8"),
            re.M,
        ))
    return citees


def test_chaque_commande_citee_par_les_documents_repond_encore():
    """Le découpage du point d'entrée n'a perdu aucune commande.

    On ne compare pas à une liste écrite dans ce fichier : on prend ce
    que les deux documents de règles citent, et on exige que chacune
    réponde. Un échantillon vide échoue.
    """
    citees = _commandes_citees([RACINE / "AGENTS.md", RACINE / "docs" / "LE-WORKFLOW.md"])
    assert citees, "les documents ne citent aucune commande — il n'y a rien à vérifier"
    muettes = []
    for nom in sorted(citees):
        proc = _atelier(nom, "--help")
        if proc.returncode != 0:
            muettes.append((nom, proc.returncode, proc.stderr.strip()[:80]))
    assert not muettes, f"commandes citées qui ne répondent plus : {muettes}"
