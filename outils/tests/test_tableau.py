"""La page montre ce que le registre dit, et rien d'autre."""

from dataclasses import dataclass, field

import pytest

from outils import tableau
from outils.tableau import LignePR


@dataclass(frozen=True)
class FicheFactice:
    numero: str
    etat: str
    couche: str | None = None
    titre: str = "Un lot"
    chemin: str = ""
    depend_de: tuple[str, ...] = field(default=())
    prs: tuple[int, ...] = field(default=())

    def __post_init__(self) -> None:
        if not self.chemin:
            object.__setattr__(self, "chemin", f"briefs/{self.numero}-un-lot.md")


MOMENT = "07/09/2026 à 11h00 UTC"


def test_un_registre_vide_est_une_erreur_pas_une_page_vide():
    """Un échantillon vide échoue ; il ne s'affiche pas en blanc."""
    with pytest.raises(ValueError):
        tableau.rendre([], [], MOMENT)


def test_chaque_lot_apparait_avec_son_etat():
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1", titre="La mer est un port commun"),
         FicheFactice("050", "a-briefer", "1", titre="On migre aussi par la mer")],
        [], MOMENT,
    )
    assert "La mer est un port commun" in page
    assert "On migre aussi par la mer" in page
    assert "livre" in page and "a-briefer" in page


def test_la_couche_porte_son_nom_et_son_avancement():
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1"), FicheFactice("050", "pret", "1")], [], MOMENT
    )
    assert "Monde vivant" in page
    assert "1 lot(s) sur 2" in page
    assert "1 lot(s) en cours" in page


def test_une_couche_finie_et_deja_stabilisee_se_voit():
    """« Finie » veut dire : plus rien n'avance, et le palier est passé.
    Tant qu'il ne l'est pas, la page dit « palier dû » — ce n'est pas la
    même chose, et c'est la seule des deux qui appelle une action."""
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1"),
         FicheFactice("055", "livre", "1", chemin="briefs/055-stabilisation-couche-1.md",
                      depend_de=("046",))],
        [], MOMENT,
    )
    assert "finie" in page
    assert "palier dû" not in page


def test_un_palier_du_se_voit():
    """C'est l'information qui appelle une action : la couche est finie et
    son lot de stabilisation n'est pas encore au registre."""
    page = tableau.rendre([FicheFactice("046", "livre", "1")], [], MOMENT)
    assert "palier dû" in page


def test_les_lots_sans_couche_ne_sont_pas_perdus():
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1"), FicheFactice("048", "livre", None, titre="Le bandeau")],
        [], MOMENT,
    )
    assert "Hors couche" in page
    assert "Le bandeau" in page


def test_chaque_proposition_dit_ce_qui_la_bloque():
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1")],
        [LignePR(226, "brief/049-fabriquer", "rien", "contrôle absent : relecture")],
        MOMENT,
    )
    assert "#226" in page
    assert "brief/049-fabriquer" in page
    assert "contrôle absent : relecture" in page


def test_aucune_proposition_ouverte_se_dit():
    page = tableau.rendre([FicheFactice("046", "livre", "1")], [], MOMENT)
    assert "Aucune proposition ouverte" in page


def test_le_moment_de_l_ecriture_est_sur_la_page():
    """Une page sans date laisse croire qu'elle est d'aujourd'hui."""
    assert MOMENT in tableau.rendre([FicheFactice("046", "livre", "1")], [], MOMENT)


def test_un_titre_qui_porte_du_balisage_ne_casse_pas_la_page():
    page = tableau.rendre(
        [FicheFactice("046", "livre", "1", titre="Le <script>alerte</script> & la mer")],
        [], MOMENT,
    )
    assert "<script>alerte</script>" not in page
    assert "&lt;script&gt;" in page and "&amp;" in page


def test_le_lien_de_demande_pointe_vers_le_formulaire():
    page = tableau.rendre([FicheFactice("046", "livre", "1")], [], MOMENT, "PLiagre/ForgeHistory")
    assert "PLiagre/ForgeHistory/issues/new?template=nouveau-lot.yml" in page


def test_sans_depot_la_page_se_rend_quand_meme():
    page = tableau.rendre([FicheFactice("046", "livre", "1")], [], MOMENT)
    assert "issues/new" not in page
