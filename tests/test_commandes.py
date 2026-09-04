"""Une commande vit dans son propre fichier, et le centre découvre.

Ce que ces contrôles tiennent : le point d'entrée ne porte plus de
table, les deux comptes s'accordent en étant dérivés des deux côtés, un
module qui ne respecte pas le contrat est **nommé** au lieu d'être
ignoré, et une commande neuve se découvre sans qu'on touche au
répartiteur.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
import textwrap

import pytest

from atelier import commandes
from atelier.__main__ import _parser

RACINE = Path(__file__).resolve().parent.parent
REPARTITEUR = RACINE / "atelier" / "__main__.py"


def _paquet_jetable(tmp_path: Path, nom: str, modules: dict[str, str]):
    """Un paquet importable, monté dans un répertoire temporaire.

    Les contrôles éprouvent la découverte ici plutôt que d'écrire dans
    `atelier/commandes/` : un contrôle qui salit le paquet qu'il mesure
    finit par mesurer sa propre saleté.
    """
    import importlib

    racine = tmp_path / nom
    racine.mkdir()
    (racine / "__init__.py").write_text("", encoding="utf-8")
    for fichier, corps in modules.items():
        (racine / fichier).write_text(textwrap.dedent(corps), encoding="utf-8")
    sys.path.insert(0, str(tmp_path))
    try:
        yield importlib.import_module(nom)
    finally:
        sys.path.remove(str(tmp_path))
        for cle in [c for c in sys.modules if c == nom or c.startswith(f"{nom}.")]:
            del sys.modules[cle]


# ------------------------------------------------- le centre ne tient plus rien


def test_le_repartiteur_ne_pose_aucun_parseur():
    """Un seul `add_parser` dans le programme, et il est dans la découverte."""
    texte = REPARTITEUR.read_text(encoding="utf-8")
    assert "add_parser" not in texte, (
        "atelier/__main__.py pose encore un parseur : le goulot est revenu"
    )


def test_le_repartiteur_ne_cite_aucune_commande():
    texte = REPARTITEUR.read_text(encoding="utf-8")
    noms = [c.nom for c in commandes.toutes()]
    assert noms, "échantillon vide"
    cites = [nom for nom in noms if f'"{nom}"' in texte or f"'{nom}'" in texte]
    assert not cites, f"le répartiteur cite des commandes : {cites}"


# --------------------------------------------------- les deux comptes dérivent


def _sous_commandes_du_parseur() -> set[str]:
    for action in _parser()._actions:
        if isinstance(action, argparse._SubParsersAction):
            return set(action.choices)
    return set()


def test_le_parseur_expose_exactement_ce_que_les_modules_declarent_derive():
    exposees = _sous_commandes_du_parseur()
    declarees = {c.nom for c in commandes.toutes()}
    assert exposees, "le parseur n'expose aucune sous-commande — échantillon vide"
    assert declarees, "aucun module ne déclare de commande — échantillon vide"
    assert exposees == declarees, {
        "posées mais non déclarées": sorted(exposees - declarees),
        "déclarées mais non posées": sorted(declarees - exposees),
    }


def test_le_compte_des_modules_derive_du_disque():
    dossier = RACINE / "atelier" / "commandes"
    sur_le_disque = {
        f.stem for f in dossier.glob("*.py") if not f.stem.startswith("_")
    }
    decouverts = {m.__name__.rsplit(".", 1)[-1] for m in commandes.modules()}
    assert sur_le_disque, "aucun module de commande sur le disque — échantillon vide"
    assert decouverts == sur_le_disque


def test_chaque_commande_repond_a_son_aide():
    """Une commande posée sans arguments valides n'est pas une commande."""
    noms = [c.nom for c in commandes.toutes()]
    assert noms, "échantillon vide"
    parser = _parser()
    for nom in noms:
        with pytest.raises(SystemExit) as sortie:
            parser.parse_args([nom, "--help"])
        assert sortie.value.code == 0, nom


# ------------------------------------------------------- le rouge, prouvé


def test_refus_un_module_sans_contrat_est_nomme(tmp_path: Path):
    for paquet in _paquet_jetable(
        tmp_path, "jetable_muet", {"muet.py": "VALEUR = 1\n"}
    ):
        with pytest.raises(commandes.CommandeErreur) as exc:
            commandes.toutes(paquet)
        assert "jetable_muet.muet" in str(exc.value)
        assert commandes.CONTRAT in str(exc.value)


def test_refus_un_module_qui_ne_declare_rien_echoue(tmp_path: Path):
    """Un échantillon vide échoue : il ne passe pas en silence."""
    for paquet in _paquet_jetable(
        tmp_path, "jetable_vide", {"vide.py": "def commandes():\n    return []\n"}
    ):
        with pytest.raises(commandes.CommandeErreur) as exc:
            commandes.toutes(paquet)
        assert "jetable_vide.vide" in str(exc.value)


def test_refus_un_module_qui_rend_autre_chose_qu_une_commande(tmp_path: Path):
    for paquet in _paquet_jetable(
        tmp_path, "jetable_faux", {"faux.py": "def commandes():\n    return ['truc']\n"}
    ):
        with pytest.raises(commandes.CommandeErreur) as exc:
            commandes.toutes(paquet)
        assert "jetable_faux.faux" in str(exc.value)


def test_refus_deux_modules_qui_declarent_le_meme_nom(tmp_path: Path):
    corps = """
        from atelier.commandes import Commande

        def commandes():
            return [Commande("pareil", "aide", lambda p: None, lambda a: 0)]
        """
    for paquet in _paquet_jetable(
        tmp_path, "jetable_double", {"un.py": corps, "deux.py": corps}
    ):
        with pytest.raises(commandes.CommandeErreur) as exc:
            commandes.toutes(paquet)
        assert "pareil" in str(exc.value)
        assert "deux fois" in str(exc.value)


# ------------------------------------------- une commande neuve se découvre


def test_decouverte_une_commande_neuve_ne_touche_pas_le_repartiteur(tmp_path: Path):
    corps = """
        from atelier.commandes import Commande

        def _poser(p):
            p.add_argument("--valeur", required=True)

        def _faire(args):
            print(args.valeur)
            return 0

        def commandes():
            return [Commande("neuve", "une commande de contrôle", _poser, _faire)]
        """
    for paquet in _paquet_jetable(tmp_path, "jetable_neuf", {"neuve.py": corps}):
        trouvees = commandes.toutes(paquet)
        assert [c.nom for c in trouvees] == ["neuve"]
        # Elle est appelable telle quelle : le contrat suffit.
        parser = argparse.ArgumentParser()
        sous = parser.add_subparsers(dest="commande", required=True)
        neuve = trouvees[0]
        neuve.poser(sous.add_parser(neuve.nom, help=neuve.aide))
        args = parser.parse_args(["neuve", "--valeur", "42"])
        assert neuve.faire(args) == 0
        # Et le répartiteur n'a pas eu à la connaître.
        assert "neuve" not in REPARTITEUR.read_text(encoding="utf-8")
