"""Une demande de lot se lit, ou se refuse. Jamais elle ne se devine."""

import pytest

from outils import saisie

GABARIT = """### Titre du lot

{titre}

### Couche

{couche}

### Dépend de

{depend}

### Ce que ça ouvre, et pourquoi maintenant

peu importe
"""


def demande(titre="Les routes relient les villes", couche="2 — Villes", depend="_No response_"):
    return saisie.lire(GABARIT.format(titre=titre, couche=couche, depend=depend))


def test_une_demande_complete_se_lit():
    d = demande(depend="047, 051")
    assert d.titre == "Les routes relient les villes"
    assert d.couche == "2"
    assert d.depend_de == ("047", "051")


def test_la_couche_se_lit_dans_le_libelle_du_formulaire():
    """Le formulaire affiche « 2 — Villes » ; la fiche ne porte que « 2 »."""
    assert demande(couche="4 — Armées").couche == "4"
    assert demande(couche="1 — Monde vivant").couche == "1"


def test_aucune_couche_est_une_reponse():
    assert demande(couche="aucune").couche is None


def test_une_couche_inconnue_est_un_refus():
    with pytest.raises(saisie.DemandeIllisible, match="couche"):
        demande(couche="9 — la couche des rêves")


def test_sans_dependance_la_liste_est_vide():
    assert demande(depend="_No response_").depend_de == ()
    assert demande(depend="").depend_de == ()
    assert demande(depend="—").depend_de == ()


def test_une_dependance_illisible_est_un_refus():
    with pytest.raises(saisie.DemandeIllisible, match="illisible"):
        demande(depend="le lot de la mer")


def test_un_suffixe_bis_reste_une_dependance_valable():
    assert demande(depend="043-bis").depend_de == ("043-bis",)


def test_une_dependance_repetee_ne_compte_qu_une_fois():
    assert demande(depend="047, 047").depend_de == ("047",)


def test_un_lot_sans_titre_n_est_pas_un_lot():
    with pytest.raises(saisie.DemandeIllisible, match="titre"):
        demande(titre="_No response_")


def test_un_formulaire_d_un_autre_gabarit_est_un_refus():
    """Une demande écrite à la main, ou avec un gabarit modifié, ne doit
    pas produire une fiche à moitié remplie."""
    with pytest.raises(saisie.DemandeIllisible, match="formulaire"):
        saisie.lire("Je voudrais que les villes aient des routes, merci")


def test_le_titre_sur_plusieurs_lignes_se_recolle():
    assert demande(titre="Les routes\nrelient les villes").titre == "Les routes relient les villes"


def test_le_slug_perd_les_accents_et_la_ponctuation():
    assert saisie.slug("L'été : la mer, enfin !") == "l-ete-la-mer-enfin"


def test_le_slug_ne_deborde_pas():
    """Un nom trop long finit tronqué par un outil ou un autre, et ce
    jour-là la fiche et le brief ne se ressemblent plus."""
    long = saisie.slug("Les habitants construisent des maisons quand ils ont de quoi "
                       "les payer et de la place où les mettre")
    assert len(long) <= 48
    assert not long.endswith("-")


def test_un_titre_sans_lettre_ne_donne_pas_de_nom():
    with pytest.raises(saisie.DemandeIllisible):
        saisie.slug("!!! ???")


def test_la_fiche_produite_est_relue_par_le_lecteur_du_registre():
    feuille = pytest.importorskip(
        "atelier.feuille",
        reason="ForgeAtelier hors du PYTHONPATH : le lecteur du registre manque",
    )
    from outils import palier

    d = demande(depend="047")
    texte = (
        f"# titre\n\n{feuille.REPERE_DEBUT}\n\n"
        "### [047 — Le bourg](briefs/047-le-bourg.md)\n"
        "état : livre · couche : 2 · dépend de : — · PR : 214\n\n"
        f"{feuille.REPERE_FIN}\n"
    )
    nouveau = palier.inserer(texte, saisie.fiche(d, "055"), feuille.REPERE_DEBUT)
    fiche = feuille.lire_texte(nouveau).fiche("055")
    assert fiche is not None
    assert fiche.etat == "a-briefer"
    assert fiche.couche == "2"
    assert fiche.depend_de == ("047",)
    assert fiche.chemin == "briefs/055-les-routes-relient-les-villes.md"
