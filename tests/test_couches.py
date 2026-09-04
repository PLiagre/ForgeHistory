"""Un module, une couche. Les sept couches sont occupées."""

import importlib
from pathlib import Path
import sys
import textwrap

import pytest

from atelier import couches
from atelier.couches import Couche, MODULES, couches_occupees
from atelier.skills_index import SKILLS, chemins

RACINE = Path(__file__).resolve().parent.parent


def test_chaque_module_a_une_couche():
    assert MODULES, "échantillon vide"
    for nom, couche in MODULES.items():
        assert isinstance(couche, Couche), nom


def test_les_sept_couches_sont_occupees():
    occupees = couches_occupees()
    vides = [c.value for c, mods in occupees.items() if not mods]
    assert not vides, f"couches sans module : {vides}"


def test_aucun_module_en_double():
    vus: dict[str, Couche] = {}
    for nom, couche in MODULES.items():
        assert nom not in vus, f"{nom} déclaré deux fois"
        vus[nom] = couche


def test_chaque_skill_existe():
    index = chemins()
    assert set(index) == set(SKILLS)
    manquants = [nom for nom, chemin in index.items() if not chemin.is_file()]
    assert not manquants, f"skills sans SKILL.md : {manquants}"


def test_skills_vivent_dans_ce_depot():
    racine = Path(__file__).resolve().parent.parent
    for chemin in chemins().values():
        assert racine in chemin.parents


# ---------------------------------- la couche se déclare chez le module


def _paquet_jetable(tmp_path: Path, nom: str, modules: dict[str, str]):
    """Un paquet importable, monté dans un répertoire temporaire.

    La découverte s'éprouve ici : un contrôle qui écrirait dans
    `atelier/` pour se prouver finirait par mesurer sa propre saleté.
    """
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


def test_le_registre_ne_cite_plus_aucun_module():
    """La table centrale a disparu : plus personne n'a à écrire ici."""
    texte = (RACINE / "atelier" / "couches.py").read_text(encoding="utf-8")
    noms = [nom.rsplit(".", 1)[-1] for nom in MODULES]
    assert noms, "échantillon vide"
    cites = [n for n in noms if f'"atelier.{n}"' in texte or f"atelier.{n}," in texte]
    assert not cites, f"le registre cite encore des modules : {cites}"


def test_le_compte_des_modules_derive_du_disque_des_deux_cotes():
    dossier = RACINE / "atelier"
    sur_le_disque = {
        f.stem for f in dossier.glob("*.py")
        if not f.stem.startswith("__") and f.stem not in couches.SANS_COUCHE
    }
    declares = {nom.rsplit(".", 1)[-1] for nom in MODULES}
    assert sur_le_disque, "aucun module sur le disque — échantillon vide"
    assert declares == sur_le_disque, {
        "sur le disque sans déclaration": sorted(sur_le_disque - declares),
        "déclarés sans fichier": sorted(declares - sur_le_disque),
    }


def test_sans_couche_un_module_est_nomme(tmp_path: Path):
    """Le rouge, prouvé : un oubli ne passe pas en silence."""
    for paquet in _paquet_jetable(
        tmp_path, "jetable_muet", {"muet.py": "VALEUR = 1\n"}
    ):
        with pytest.raises(couches.CoucheErreur) as exc:
            couches.decouvrir(paquet)
        assert "jetable_muet.muet" in str(exc.value)
        assert couches.ATTRIBUT in str(exc.value)


def test_une_couche_inconnue_est_refusee_et_nommee(tmp_path: Path):
    for paquet in _paquet_jetable(
        tmp_path, "jetable_inconnu", {"faux.py": 'COUCHE = "cuisine"\n'}
    ):
        with pytest.raises(couches.CoucheErreur) as exc:
            couches.decouvrir(paquet)
        assert "jetable_inconnu.faux" in str(exc.value)
        assert "cuisine" in str(exc.value)


def test_un_paquet_sans_aucune_declaration_echoue(tmp_path: Path):
    """Un échantillon vide échoue : il ne rend pas un registre vide."""
    for paquet in _paquet_jetable(tmp_path, "jetable_desert", {}):
        with pytest.raises(couches.CoucheErreur) as exc:
            couches.decouvrir(paquet)
        assert "échantillon vide" in str(exc.value)


def test_la_decouverte_ne_descend_pas_dans_un_sous_paquet(tmp_path: Path):
    """La surface du programme n'occupe pas de couche.

    Un module de commande appelle, il ne raisonne pas. Sans cette règle,
    ce lot et celui qui découpe le point d'entrée ne seraient pas
    disjoints.
    """
    racine = tmp_path / "jetable_paquet"
    for paquet in _paquet_jetable(
        tmp_path, "jetable_paquet", {"composant.py": 'COUCHE = "outils"\n'}
    ):
        surface = racine / "surface"
        surface.mkdir()
        (surface / "__init__.py").write_text("", encoding="utf-8")
        (surface / "commande.py").write_text("VALEUR = 1\n", encoding="utf-8")
        trouves = couches.decouvrir(paquet)
        assert set(trouves) == {"jetable_paquet.composant"}


def test_une_couche_sans_module_se_voit(tmp_path: Path):
    """C'est ce rouge que VISION.md invoque pour la huitième couche."""
    for paquet in _paquet_jetable(
        tmp_path, "jetable_seul", {"un.py": 'COUCHE = "outils"\n'}
    ):
        trouves = couches.decouvrir(paquet)
        occupees = {c: [n for n, v in trouves.items() if v == c] for c in Couche}
        vides = [c.value for c, mods in occupees.items() if not mods]
        assert Couche.OUTILS.value not in vides
        assert len(vides) == len(Couche) - 1, "une couche occupée, les autres vides"
