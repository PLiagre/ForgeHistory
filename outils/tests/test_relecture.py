"""Celui qui a écrit le code ne dit pas s'il est recevable."""

from outils import relecture
from outils.relecture import Revue

TETE = "a" * 40
AVANT = "b" * 40


def test_une_approbation_d_un_tiers_sur_la_tete_passe():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [Revue("claude[bot]", "APPROVED", TETE)])
    assert verdict.passe


def test_sans_approbation_rien_ne_passe():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [])
    assert not verdict.passe
    assert "absente" in verdict.raison


def test_une_approbation_sur_une_revision_anterieure_est_perimee():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [Revue("claude[bot]", "APPROVED", AVANT)])
    assert not verdict.passe
    assert "périmée" in verdict.raison


def test_l_auteur_du_code_ne_s_approuve_pas_lui_meme():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [Revue("cursor[bot]", "APPROVED", TETE)])
    assert not verdict.passe
    assert "recevable" in verdict.raison


def test_la_casse_de_la_connexion_ne_contourne_pas_la_regle():
    verdict = relecture.juger(TETE, ["Cursor[bot]"], [Revue("cursor[BOT]", "APPROVED", TETE)])
    assert not verdict.passe


def test_un_auteur_parmi_d_autres_approbateurs_ne_suffit_pas_a_bloquer():
    verdict = relecture.juger(
        TETE, ["cursor[bot]"],
        [Revue("cursor[bot]", "APPROVED", TETE), Revue("PLiagre", "APPROVED", TETE)],
    )
    assert verdict.passe
    assert "pliagre" in verdict.raison


def test_des_changements_demandes_bloquent():
    verdict = relecture.juger(
        TETE, ["cursor[bot]"],
        [Revue("PLiagre", "APPROVED", TETE), Revue("claude[bot]", "CHANGES_REQUESTED", TETE)],
    )
    assert not verdict.passe
    assert "changements demandés" in verdict.raison


def test_des_changements_demandes_puis_leves_ne_bloquent_plus():
    verdict = relecture.juger(
        TETE, ["cursor[bot]"],
        [Revue("claude[bot]", "CHANGES_REQUESTED", TETE), Revue("claude[bot]", "APPROVED", TETE)],
    )
    assert verdict.passe


def test_des_changements_demandes_sur_une_revision_anterieure_ne_bloquent_plus():
    """Le code a bougé : ce refus-là parlait d'un autre code. C'est la
    même règle que pour l'approbation, dans l'autre sens."""
    verdict = relecture.juger(
        TETE, ["cursor[bot]"],
        [Revue("claude[bot]", "CHANGES_REQUESTED", AVANT), Revue("PLiagre", "APPROVED", TETE)],
    )
    assert verdict.passe


def test_un_commentaire_ne_verdit_rien():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [Revue("claude[bot]", "COMMENTED", TETE)])
    assert not verdict.passe


def test_une_revue_sans_revision_ne_porte_sur_rien():
    verdict = relecture.juger(TETE, ["cursor[bot]"], [Revue("claude[bot]", "APPROVED", "")])
    assert not verdict.passe


def test_sans_auteur_connu_la_regle_ne_peut_pas_etre_tenue():
    """Rule 10 : une donnée absente ne se devine pas. Si on ne sait pas
    qui a écrit le code, on ne peut pas affirmer que le relecteur n'en
    est pas — et on refuse au lieu de supposer."""
    verdict = relecture.juger(TETE, [], [Revue("claude[bot]", "APPROVED", TETE)])
    assert not verdict.passe
    assert "auteur" in verdict.raison


def test_sans_revision_connue_il_n_y_a_rien_a_relire():
    assert not relecture.juger("", ["cursor[bot]"], []).passe
