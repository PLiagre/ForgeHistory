"""Le palier se déclenche sur une mesure, jamais sur une intention."""

from dataclasses import dataclass, field

import pytest

from outils import palier


@dataclass(frozen=True)
class FicheFactice:
    """Ce que le lecteur de l'atelier rend, réduit à ce que le palier lit."""

    numero: str
    etat: str
    couche: str | None = None
    chemin: str = ""
    depend_de: tuple[str, ...] = field(default=())

    def __post_init__(self) -> None:
        if not self.chemin:
            object.__setattr__(self, "chemin", f"briefs/{self.numero}-un-lot.md")


def stabilisation(numero: str, couche: str, etat: str, couvre=()):
    return FicheFactice(
        numero=numero,
        etat=etat,
        couche=couche,
        chemin=f"briefs/{numero}-stabilisation-couche-{couche}.md",
        depend_de=tuple(couvre),
    )


def test_une_couche_dont_un_lot_avance_n_est_pas_finie():
    fiches = [
        FicheFactice("046", "livre", "1"),
        FicheFactice("050", "pret", "1"),
    ]
    assert palier.due(fiches) is None


def test_une_couche_entierement_livree_appelle_son_palier():
    fiches = [
        FicheFactice("046", "livre", "1"),
        FicheFactice("050", "livre", "1"),
    ]
    etape = palier.due(fiches)
    assert etape is not None
    assert etape.couche == "1"
    assert etape.a_couvrir == ("046", "050")


def test_un_lot_abandonne_ne_retient_pas_la_couche_et_ne_se_couvre_pas():
    fiches = [
        FicheFactice("046", "livre", "1"),
        FicheFactice("050", "abandonne", "1"),
    ]
    etape = palier.due(fiches)
    assert etape is not None
    assert etape.a_couvrir == ("046",)


def test_une_couche_sans_rien_de_livre_n_est_pas_une_couche_finie():
    """Un échantillon vide échoue : il ne passe pas."""
    fiches = [FicheFactice("050", "abandonne", "1")]
    assert palier.due(fiches) is None
    assert palier.etapes(fiches)[0].finie is False


def test_le_palier_ne_se_redeclenche_pas_sur_les_lots_qu_il_couvre():
    fiches = [
        FicheFactice("046", "livre", "1"),
        FicheFactice("050", "livre", "1"),
        stabilisation("055", "1", "livre", couvre=("046", "050")),
    ]
    assert palier.due(fiches) is None


def test_un_lot_livre_apres_le_palier_en_appelle_un_autre():
    fiches = [
        FicheFactice("046", "livre", "1"),
        FicheFactice("050", "livre", "1"),
        stabilisation("055", "1", "livre", couvre=("046", "050")),
        FicheFactice("058", "livre", "1"),
    ]
    etape = palier.due(fiches)
    assert etape is not None
    assert etape.a_couvrir == ("058",)
    assert etape.couverts == ("046", "050")


def test_un_palier_en_attente_retient_sa_couche():
    """La fiche déposée est elle-même de la couche : tant qu'elle n'est
    pas livrée, la couche n'est pas finie — c'est ce qui empêche la
    boucle de déposer deux fois le même palier."""
    fiches = [
        FicheFactice("046", "livre", "1"),
        stabilisation("055", "1", "a-briefer", couvre=("046",)),
    ]
    assert palier.due(fiches) is None


def test_les_couches_partent_dans_l_ordre():
    fiches = [
        FicheFactice("047", "livre", "2"),
        FicheFactice("046", "livre", "1"),
    ]
    etape = palier.due(fiches)
    assert etape is not None
    assert etape.couche == "1"


def test_une_fiche_sans_couche_n_appartient_a_aucun_palier():
    fiches = [FicheFactice("048", "livre", None), FicheFactice("054", "livre", None)]
    assert palier.etapes(fiches) == ()
    assert palier.due(fiches) is None


def test_le_numero_est_le_premier_libre_au_dessus_du_plus_grand():
    fiches = [FicheFactice("046", "livre", "1"), FicheFactice("054", "idee", "2")]
    assert palier.numero_libre(fiches) == "055"


def test_un_suffixe_bis_ne_compte_pas_pour_un_numero_de_plus():
    fiches = [FicheFactice("043-bis", "archive", "1"), FicheFactice("043", "archive", "1")]
    assert palier.numero_libre(fiches) == "044"


def test_un_registre_vide_refuse_de_rendre_un_numero():
    with pytest.raises(ValueError):
        palier.numero_libre([])


def test_la_fiche_ecrite_est_relue_par_le_lecteur_du_registre():
    """La preuve qui compte : ce qu'on écrit, l'atelier le relit.

    Sans lui, ce contrôle ne se joue pas — et il le dit, il ne passe
    pas en silence.
    """
    feuille = pytest.importorskip(
        "atelier.feuille",
        reason="ForgeAtelier hors du PYTHONPATH : le lecteur du registre manque",
    )
    fiches = [FicheFactice("046", "livre", "1"), FicheFactice("050", "livre", "1")]
    etape = palier.due(fiches)
    texte = (
        "# titre\n\n"
        f"{feuille.REPERE_DEBUT}\n\n"
        "### [046 — La mer](briefs/046-la-mer.md)\n"
        "état : livre · couche : 1 · dépend de : — · PR : 206\n\n"
        "### [050 — La migration](briefs/050-la-migration.md)\n"
        "état : livre · couche : 1 · dépend de : — · PR : 210\n\n"
        f"{feuille.REPERE_FIN}\n"
    )
    nouveau = palier.inserer(texte, palier.fiche(etape, "055"), feuille.REPERE_DEBUT)
    relu = feuille.lire_texte(nouveau)
    fiche = relu.fiche("055")
    assert fiche is not None
    assert fiche.etat == "a-briefer"
    assert fiche.couche == "1"
    assert fiche.depend_de == ("046", "050")
    assert fiche.prs == ()
    # L'ordre est la priorité : le palier passe devant ce qui attend.
    assert relu.fiches[0].numero == "055"


def test_la_fiche_d_un_palier_se_reconnait_a_son_chemin():
    assert palier.couche_stabilisee("briefs/055-stabilisation-couche-1.md") == "1"
    assert palier.couche_stabilisee("briefs/049-fabriquer.md") is None


def test_on_n_ecrit_pas_un_palier_qui_ne_couvre_rien():
    etape = palier.Etape(couche="1", en_cours=(), couverts=("046",), a_couvrir=())
    with pytest.raises(ValueError):
        palier.fiche(etape, "055")


def test_inserer_refuse_un_registre_sans_repere():
    with pytest.raises(ValueError):
        palier.inserer("# rien\n", "### [055 — x](briefs/055-x.md)", "<!-- lots:debut -->")


def test_un_lot_archive_ne_reclame_pas_de_palier():
    """Son brief et ses preuves vivent au tag : aucun lot de
    stabilisation ne pourrait les citer. Il ne retient rien, il
    n'appelle rien."""
    fiches = [FicheFactice("033", "archive", "1"), FicheFactice("038", "archive", "1")]
    etape = palier.etapes(fiches)[0]
    assert etape.finie
    assert etape.a_couvrir == ()
    assert palier.due(fiches) is None


def test_un_lot_livre_a_cote_d_archives_appelle_seul_le_palier():
    fiches = [FicheFactice("033", "archive", "1"), FicheFactice("046", "livre", "1")]
    etape = palier.due(fiches)
    assert etape is not None
    assert etape.a_couvrir == ("046",)
    assert etape.couverts == ("033",)
