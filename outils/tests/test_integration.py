"""L'intégration lit des contrôles, jamais un avis — et un inconnu retient."""

from outils import integration
from outils.integration import Controle, PR

REQUIS = ("sim", "viewer", "feuille", "gitleaks")
PREFIXES = ("agent/", "brief/", "feuille/")


def verts(*noms):
    return tuple(Controle(nom, integration.VERT) for nom in noms)


def pr(**kw):
    defaut = dict(
        numero=200,
        branche="agent/049-fabriquer",
        brouillon=False,
        fusionnable=True,
        retard=0,
        controles=verts(*REQUIS),
        relue=True,
        motif_relecture="approuvée sur aaaaaaa par pliagre",
    )
    defaut.update(kw)
    return PR(**defaut)


def test_une_pr_verte_a_jour_et_relue_entre():
    decision = integration.examiner(pr(), REQUIS, PREFIXES)
    assert decision.action == integration.FUSIONNER


def test_un_controle_requis_absent_n_est_pas_un_controle_vert():
    incomplet = verts(*[n for n in REQUIS if n != "gitleaks"])
    decision = integration.examiner(pr(controles=incomplet), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "gitleaks" in decision.raison


def test_un_controle_rouge_retient():
    controles = verts(*REQUIS[:-1]) + (Controle("gitleaks", integration.ROUGE),)
    decision = integration.examiner(pr(controles=controles), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "rouge" in decision.raison


def test_un_controle_en_cours_retient():
    controles = verts(*REQUIS[:-1]) + (Controle("gitleaks", integration.EN_COURS),)
    decision = integration.examiner(pr(controles=controles), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "en cours" in decision.raison


def test_un_controle_hors_de_la_liste_ne_change_rien():
    """La liste requise gouverne seule ; un contrôle facultatif rouge
    ne bloque pas, sinon la liste ne voudrait plus rien dire."""
    controles = verts(*REQUIS) + (Controle("un-essai", integration.ROUGE),)
    assert integration.examiner(pr(controles=controles), REQUIS, PREFIXES).action == integration.FUSIONNER


def test_une_fusionnabilite_inconnue_retient():
    decision = integration.examiner(pr(fusionnable=None), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "inconnue" in decision.raison


def test_un_conflit_retient():
    assert integration.examiner(pr(fusionnable=False), REQUIS, PREFIXES).action == integration.RIEN


def test_un_brouillon_n_entre_pas():
    assert integration.examiner(pr(brouillon=True), REQUIS, PREFIXES).action == integration.RIEN


def test_une_branche_hors_prefixe_reste_au_proprietaire():
    decision = integration.examiner(pr(branche="cursor/un-essai"), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "propriétaire" in decision.raison


def test_une_pr_en_retard_est_rejouee_avant_d_entrer():
    decision = integration.examiner(pr(retard=3), REQUIS, PREFIXES)
    assert decision.action == integration.REBASER


def test_une_pr_en_retard_et_rouge_n_est_pas_rejouee_pour_rien():
    controles = verts(*REQUIS[:-1]) + (Controle("gitleaks", integration.ROUGE),)
    decision = integration.examiner(pr(retard=3, controles=controles), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN


def test_une_seule_pr_avance_par_tour():
    rapport = integration.decider(
        [pr(numero=210), pr(numero=205)], REQUIS, PREFIXES
    )
    assert rapport.decision.action == integration.FUSIONNER
    assert rapport.decision.pr == 205  # la plus ancienne d'abord
    assert len(rapport.lignes) == 2  # les deux sont dites, une seule avance


def test_une_pr_qui_attend_ne_bloque_pas_la_suivante():
    rapport = integration.decider(
        [pr(numero=205, fusionnable=False), pr(numero=210)], REQUIS, PREFIXES
    )
    assert rapport.decision.pr == 210


def test_sans_controle_requis_declare_rien_n_entre():
    rapport = integration.decider([pr()], (), PREFIXES)
    assert rapport.decision.action == integration.RIEN
    assert "aucun contrôle requis" in rapport.decision.raison


def test_aucune_pr_ouverte_n_est_pas_une_erreur():
    rapport = integration.decider([], REQUIS, PREFIXES)
    assert rapport.decision.action == integration.RIEN
    assert rapport.decision.pr is None


def test_un_controle_qui_n_a_pas_fini_n_est_ni_vert_ni_rouge():
    assert integration.etat_du_controle("in_progress", None) == integration.EN_COURS
    assert integration.etat_du_controle("queued", None) == integration.EN_COURS


def test_un_controle_ignore_n_a_rien_prouve():
    assert integration.etat_du_controle("completed", "skipped") == integration.ROUGE
    assert integration.etat_du_controle("completed", "cancelled") == integration.ROUGE
    assert integration.etat_du_controle("completed", "failure") == integration.ROUGE
    assert integration.etat_du_controle("completed", "success") == integration.VERT


def test_une_pr_sans_relecture_n_entre_pas():
    decision = integration.examiner(
        pr(relue=False, motif_relecture="aucune approbation : relecture absente"),
        REQUIS, PREFIXES,
    )
    assert decision.action == integration.RIEN
    assert "relecture absente" in decision.raison


def test_une_relecture_inconnue_retient():
    """La PR n'a pas été interrogée : un blanc n'est pas une approbation."""
    decision = integration.examiner(pr(relue=None), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "inconnue" in decision.raison


def test_une_pr_en_retard_se_rejoue_avant_qu_on_demande_la_relecture():
    """Le rejeu périme la relecture : la demander avant, c'est la payer
    deux fois, et la deuxième pour rien."""
    decision = integration.examiner(pr(retard=1, relue=False), REQUIS, PREFIXES)
    assert decision.action == integration.REBASER
    assert "relecture" in decision.raison


def test_un_controle_de_ci_rouge_ne_se_rejoue_pas_pour_autant():
    """Un rejeu ne répare pas un test rouge : il coûte un tour de CI pour
    rougir au même endroit."""
    controles = (Controle("sim", integration.ROUGE),) + verts(
        *[n for n in REQUIS if n != "sim"]
    )
    decision = integration.examiner(pr(retard=1, controles=controles), REQUIS, PREFIXES)
    assert decision.action == integration.RIEN
    assert "sim" in decision.raison


def test_la_raison_de_la_fusion_dit_qui_a_relu():
    decision = integration.examiner(pr(), REQUIS, PREFIXES)
    assert decision.action == integration.FUSIONNER
    assert "pliagre" in decision.raison
