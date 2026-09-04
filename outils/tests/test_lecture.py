"""La couture avec GitHub : les noms de champs, et ce qu'un blanc veut dire.

Les décisions sont éprouvées ailleurs. Ici on éprouve ce qui les
alimente — l'endroit où une clé mal orthographiée rend un brouillon
éveillé ou une PR conflictuelle fusionnable, sans que rien ne rougisse.
"""

import pytest

from outils import integration, relecture


def test_une_pr_ecartee_n_est_lue_qu_a_moitie():
    """Sans détail, la fusionnabilité est inconnue — pas vraie."""
    pr = integration.depuis_github(
        {"number": 215, "head": {"ref": "cursor/essai"}, "draft": True}
    )
    assert pr.numero == 215
    assert pr.branche == "cursor/essai"
    assert pr.brouillon is True
    assert pr.fusionnable is None
    assert pr.controles == ()


def test_un_brouillon_absent_du_json_n_est_pas_un_brouillon():
    pr = integration.depuis_github({"number": 1, "head": {"ref": "agent/049-x"}})
    assert pr.brouillon is False


def test_les_controles_arrivent_avec_leur_etat_traduit():
    pr = integration.depuis_github(
        {"number": 216, "head": {"ref": "agent/049-x"}},
        {"mergeable": True, "head": {"sha": "a" * 40}},
        [("sim", "completed", "success"), ("viewer", "in_progress", None),
         ("gitleaks", "completed", "failure")],
        retard=2,
    )
    par_nom = {c.nom: c.etat for c in pr.controles}
    assert par_nom == {
        "sim": integration.VERT,
        "viewer": integration.EN_COURS,
        "gitleaks": integration.ROUGE,
    }
    assert pr.retard == 2
    assert pr.fusionnable is True


def test_une_fusionnabilite_que_github_n_a_pas_calculee_reste_inconnue():
    pr = integration.depuis_github(
        {"number": 216, "head": {"ref": "agent/049-x"}},
        {"head": {"sha": "a" * 40}},
    )
    assert pr.fusionnable is None


def test_une_approbation_que_personne_ne_signe_ne_compte_pas():
    """GitHub rend `user: null` pour un compte supprimé. Un auteur vide
    n'est pas un tiers : il n'est personne."""
    revues = relecture.revues_depuis_github(
        [{"user": None, "state": "APPROVED", "commit_id": "a" * 40}]
    )
    assert revues[0].auteur == ""
    assert not relecture.juger("a" * 40, ["cursor[bot]"], revues).passe


def test_une_revue_sans_revision_est_lue_comme_vide():
    revues = relecture.revues_depuis_github(
        [{"user": {"login": "claude[bot]"}, "state": "APPROVED", "commit_id": None}]
    )
    assert revues[0].revision == ""
    assert not relecture.juger("a" * 40, ["cursor[bot]"], revues).passe


def _brancher(dossier, corps: str):
    (dossier / "atelier.toml").write_text(
        '[projet]\nnom = "Essai"\nfeuille = "ROADMAP.md"\nbranche_base = "master"\n' + corps,
        encoding="utf-8",
    )
    return dossier


def test_le_branchement_rend_ce_que_le_projet_declare(tmp_path):
    from outils import registre

    _brancher(tmp_path, "")
    assert registre.branchement(tmp_path)["feuille"] == "ROADMAP.md"
    assert registre.branchement(tmp_path)["base"] == "master"


def test_sans_section_integration_on_refuse_au_lieu_de_deviner(tmp_path):
    from outils import registre

    _brancher(tmp_path, "")
    with pytest.raises(registre.BranchementIncomplet) as refus:
        registre.integration(tmp_path)
    assert "[integration]" in str(refus.value)


def test_une_liste_de_controles_vide_est_un_branchement_incomplet(tmp_path):
    """Une liste vide ferait entrer n'importe quoi. Elle se refuse à la
    lecture, avant même que la décision ait à s'en méfier."""
    from outils import registre

    _brancher(tmp_path, '\n[integration]\ncontroles = []\nbranches = ["agent/"]\n')
    with pytest.raises(registre.BranchementIncomplet) as refus:
        registre.integration(tmp_path)
    assert "controles" in str(refus.value)


def test_les_controles_tardifs_sont_facultatifs(tmp_path):
    from outils import registre

    _brancher(tmp_path, '\n[integration]\ncontroles = ["sim"]\nbranches = ["agent/"]\n')
    assert registre.integration(tmp_path)["apres_rejeu"] == ()


def test_un_atelier_toml_absent_se_dit(tmp_path):
    from outils import registre

    with pytest.raises(registre.BranchementIncomplet):
        registre.branchement(tmp_path)
