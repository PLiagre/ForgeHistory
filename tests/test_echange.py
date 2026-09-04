"""Git-invisible et lisible : les deux, ou aucune."""

from pathlib import Path
import shutil

import pytest

from atelier import echange

besoin_bash = pytest.mark.skipif(shutil.which("bash") is None, reason="bash absent")


def test_deposer_et_relire(tmp_path: Path):
    source = tmp_path / "corps.json"
    source.write_text('{"verdict": "PASS"}', encoding="utf-8")
    cible = echange.deposer(tmp_path, source, "revue.json")
    assert cible.is_file()
    assert cible.read_text(encoding="utf-8") == '{"verdict": "PASS"}'
    assert echange.git_ignore_le_canal(tmp_path)


def test_garde_independante_du_depot(tmp_path: Path):
    echange.ouvrir(tmp_path)
    garde = echange.dossier(tmp_path) / ".gitignore"
    assert garde.read_text(encoding="utf-8") == "*\n"


def test_vide_refuse(tmp_path: Path):
    with pytest.raises(echange.EchangeErreur):
        echange.deposer_texte(tmp_path, "vide.txt", "   \n")


def test_retirer_n_est_pas_une_archive(tmp_path: Path):
    echange.deposer_texte(tmp_path, "prompt.txt", "exécute le brief")
    echange.retirer(tmp_path, "prompt.txt")
    assert not (echange.dossier(tmp_path) / "prompt.txt").exists()


# ------------------------------------- ce que GitHub publie, ou son silence
#
# Aucun test d'ici ne touche le réseau : la commande est nommée par
# l'environnement, et c'est le test qui la pose.


def _fausse_commande(dossier: Path, corps: str) -> Path:
    dossier.mkdir(parents=True, exist_ok=True)
    cible = dossier / "faux-gh"
    cible.write_text("#!/usr/bin/env bash\n" + corps, encoding="utf-8")
    cible.chmod(0o755)
    return cible


def _poser(monkeypatch, variable: str, chemin: Path) -> None:
    monkeypatch.setenv(variable, str(chemin))


@besoin_bash
def test_ci_vert(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, 'printf "sim\\tpass\\t6m\\turl\\n"\n')
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    assert echange.verdict_ci(206, tmp_path).etat == echange.VERT


@besoin_bash
def test_ci_rouge_nomme_les_fautifs(tmp_path: Path, monkeypatch):
    """Un compte sans nom ne dit pas au propriétaire quoi regarder."""
    faux = _fausse_commande(
        tmp_path,
        'printf "sim\\tfail\\t6m\\turl\\nviewer\\tpass\\t31s\\turl\\n'
        'gitleaks\\tfail\\t5s\\turl\\n"\n',
    )
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    verdict = echange.verdict_ci(206, tmp_path)
    assert verdict.etat == echange.ROUGE
    assert verdict.fautifs == ("sim", "gitleaks")
    assert not verdict.vert


@besoin_bash
def test_ci_demande_les_controles_obligatoires(tmp_path: Path, monkeypatch):
    """La même liste que le bouton de fusion de GitHub, pas une autre."""
    trace = tmp_path / "argv.txt"
    faux = _fausse_commande(tmp_path, f'printf "%s\\n" "$*" > "{trace}"\nprintf "sim\\tpass\\t1s\\turl\\n"\n')
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    echange.verdict_ci(206, tmp_path)
    recu = trace.read_text(encoding="utf-8")
    assert "pr checks 206" in recu
    assert "--required" in recu


@besoin_bash
def test_ci_inconnue_commande_muette(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, "exit 8\n")
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    verdict = echange.verdict_ci(206, tmp_path)
    assert verdict.etat == echange.INCONNU
    assert not verdict.vert and not verdict.connu


def test_ci_inconnue_commande_absente(tmp_path: Path, monkeypatch):
    monkeypatch.setenv("ATELIER_CI_CMD", str(tmp_path / "personne"))
    assert echange.verdict_ci(206, tmp_path).etat == echange.INCONNU


@besoin_bash
def test_ci_inconnue_controles_en_cours(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, 'printf "sim\\tpending\\t\\turl\\n"\n')
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    verdict = echange.verdict_ci(206, tmp_path)
    assert verdict.etat == echange.INCONNU
    assert "en cours" in verdict.raison


@besoin_bash
def test_ci_skipping_n_est_pas_un_echec(tmp_path: Path, monkeypatch):
    """GitHub ne les compte pas non plus pour son bouton de fusion."""
    faux = _fausse_commande(
        tmp_path, 'printf "bugbot\\tskipping\\t3m\\turl\\nsim\\tpass\\t6m\\turl\\n"\n'
    )
    _poser(monkeypatch, "ATELIER_CI_CMD", faux)
    assert echange.verdict_ci(206, tmp_path).etat == echange.VERT


@besoin_bash
@pytest.mark.parametrize(
    "publie, attendu",
    [
        ("OPEN", echange.OUVERTE),
        ("MERGED", echange.FUSIONNEE),
        ("CLOSED", echange.FERMEE),
    ],
)
def test_etat_pr_lit_ce_que_github_publie(tmp_path: Path, monkeypatch, publie, attendu):
    faux = _fausse_commande(tmp_path, f'printf \'{{"state": "{publie}"}}\'\n')
    _poser(monkeypatch, "ATELIER_PR_CMD", faux)
    assert echange.etat_pr(206, tmp_path) == attendu


@besoin_bash
def test_etat_pr_inconnue_ne_dit_jamais_ouverte(tmp_path: Path, monkeypatch):
    for corps in ("exit 1\n", "printf ''\n", "printf 'pas du json'\n",
                  'printf \'{"state": "SURPRISE"}\'\n'):
        faux = _fausse_commande(tmp_path, corps)
        _poser(monkeypatch, "ATELIER_PR_CMD", faux)
        assert echange.etat_pr(206, tmp_path) == echange.INCONNU, corps


@besoin_bash
def test_pr_ouverte_sur_une_branche(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, 'printf \'[{"number": 206}]\'\n')
    _poser(monkeypatch, "ATELIER_PR_CMD", faux)
    assert echange.pr_ouverte_sur("agent/046", tmp_path) == (echange.OUVERTE, 206)


@besoin_bash
def test_aucune_pr_sur_une_branche(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, "printf '[]'\n")
    _poser(monkeypatch, "ATELIER_PR_CMD", faux)
    assert echange.pr_ouverte_sur("agent/999", tmp_path) == (echange.AUCUNE, None)


@besoin_bash
def test_pr_sur_une_branche_inconnue_si_la_sonde_se_tait(tmp_path: Path, monkeypatch):
    faux = _fausse_commande(tmp_path, "exit 1\n")
    _poser(monkeypatch, "ATELIER_PR_CMD", faux)
    assert echange.pr_ouverte_sur("agent/046", tmp_path) == (echange.INCONNU, None)


def test_une_branche_qui_n_existe_pas_ne_coute_aucun_appel(tmp_path: Path):
    """Un lot neuf n'a pas de branche : le cas ordinaire reste gratuit."""
    assert not echange.branche_existe("agent/jamais-vue", tmp_path)
