"""Deux lots ne tiennent pas le même fichier. Un verrou vide échoue."""

from pathlib import Path

import pytest

from atelier import verrou


def test_poser_et_lire(tmp_path: Path):
    pose = verrou.poser(tmp_path, "044-mineur", ["sim/engine.py", "sim/constants.py"])
    assert "sim/engine.py" in pose.fichiers
    tableau = verrou.charger(tmp_path)
    assert len(tableau.poses) == 1


def test_collision(tmp_path: Path):
    verrou.poser(tmp_path, "044-mineur", ["sim/engine.py"])
    with pytest.raises(verrou.Collision, match="044-mineur"):
        verrou.poser(tmp_path, "046-mer", ["sim/engine.py", "sim/world.py"])


def test_perimetres_disjoints_passent(tmp_path: Path):
    verrou.poser(tmp_path, "044-mineur", ["sim/engine.py"])
    verrou.poser(tmp_path, "047-bourg", ["sim/aggregation.py"])
    assert len(verrou.charger(tmp_path).poses) == 2


def test_verrou_sans_fichier_echoue(tmp_path: Path):
    with pytest.raises(verrou.Collision):
        verrou.poser(tmp_path, "vide", [])


def test_fichier_vide_echoue(tmp_path: Path):
    cible = tmp_path / ".atelier"
    cible.mkdir()
    (cible / "verrous.json").write_text("[]\n", encoding="utf-8")
    with pytest.raises(verrou.Collision, match="vide"):
        verrou.charger(tmp_path)


def test_lever(tmp_path: Path):
    verrou.poser(tmp_path, "044-mineur", ["sim/engine.py"])
    verrou.lever(tmp_path, "044-mineur")
    assert verrou.charger(tmp_path).poses == []


# -------------------------------- une fiche n'est pas tout le fichier

FEUILLE = "ROADMAP.md"


def _fiche(lot: str) -> str:
    return f"{FEUILLE}{verrou.SEPARATEUR}{lot}"


def test_deux_fiches_du_meme_registre_ne_sont_pas_en_collision(tmp_path: Path):
    """Le défaut le plus cher, et le moins visible.

    Tant que la fiche d'un lot était « le fichier de la feuille », aucun
    lot n'était jamais disjoint d'aucun autre — et rien ne le signalait.
    """
    verrou.poser(tmp_path, "046-mer", ["sim/ports.py", _fiche("046-mer")])
    verrou.poser(tmp_path, "047-bourg", ["sim/aggregation.py", _fiche("047-bourg")])
    assert len(verrou.charger(tmp_path).poses) == 2


def test_feuille_entiere_reste_en_collision_avec_une_fiche(tmp_path: Path):
    """Le rouge, prouvé : sans lui, le lot aurait désarmé le verrou.

    Un lot d'exploitation qui réorganise le registre a le droit
    d'exister ; il a juste le droit d'être seul.
    """
    verrou.poser(tmp_path, "047-bourg", [_fiche("047-bourg")])
    with pytest.raises(verrou.Collision) as exc:
        verrou.poser(tmp_path, "exploitation", [FEUILLE])
    assert "047-bourg" in str(exc.value)
    assert FEUILLE in str(exc.value)


def test_une_fiche_ne_passe_pas_derriere_une_feuille_entiere(tmp_path: Path):
    """La collision se voit dans les deux sens, quel que soit l'ordre."""
    verrou.poser(tmp_path, "exploitation", [FEUILLE])
    with pytest.raises(verrou.Collision) as exc:
        verrou.poser(tmp_path, "047-bourg", [_fiche("047-bourg")])
    assert "exploitation" in str(exc.value)


def test_deux_lots_sur_la_meme_fiche_se_heurtent(tmp_path: Path):
    verrou.poser(tmp_path, "047-bourg", [_fiche("047-bourg")])
    with pytest.raises(verrou.Collision):
        verrou.poser(tmp_path, "047-bis", [_fiche("047-bourg")])


def test_un_fichier_ordinaire_ne_heurte_pas_une_fiche_d_un_autre_fichier(tmp_path: Path):
    verrou.poser(tmp_path, "046-mer", [_fiche("046-mer")])
    verrou.poser(tmp_path, "044-mineur", ["ROADMAP.txt"])
    assert len(verrou.charger(tmp_path).poses) == 2


def test_une_ressource_se_lit_et_se_reecrit_a_l_identique():
    for brut in ("sim/engine.py", _fiche("047-bourg"), "docs/a.b.c.md"):
        assert str(verrou.Ressource.depuis(brut)) == brut


def test_un_separateur_sans_fiche_est_refuse():
    with pytest.raises(verrou.Collision) as exc:
        verrou.Ressource.depuis(f"{FEUILLE}{verrou.SEPARATEUR}")
    assert verrou.SEPARATEUR in str(exc.value)


def test_une_ressource_vide_est_refusee():
    with pytest.raises(verrou.Collision):
        verrou.Ressource.depuis("   ")


def test_qui_tient_nomme_la_ressource_et_le_lot(tmp_path: Path):
    verrou.poser(tmp_path, "046-mer", ["sim/ports.py"])
    pris = verrou.qui_tient(tmp_path, ["sim/ports.py", "sim/libre.py"], sauf="047-bourg")
    assert pris == [("sim/ports.py", "046-mer")]


def test_qui_tient_ne_bloque_pas_le_lot_qui_demande(tmp_path: Path):
    verrou.poser(tmp_path, "046-mer", ["sim/ports.py"])
    assert verrou.qui_tient(tmp_path, ["sim/ports.py"], sauf="046-mer") == []


def test_le_compte_des_ressources_tenues_se_derive(tmp_path: Path):
    """Le total se compte sur ce qui est tenu, il ne s'écrit pas."""
    poses = {
        "046-mer": ["sim/ports.py", _fiche("046-mer")],
        "047-bourg": ["sim/aggregation.py", "sim/villes.py", _fiche("047-bourg")],
    }
    for lot, fichiers in poses.items():
        verrou.poser(tmp_path, lot, fichiers)
    tableau = verrou.charger(tmp_path)
    attendu = sum(len(f) for f in poses.values())
    tenues = [r for pose in tableau.poses for r in pose.ressources]
    assert tenues, "échantillon vide"
    assert len(tenues) == attendu
    fiches = [r for r in tenues if not r.entiere]
    assert len(fiches) == len(poses), "une fiche par lot, dérivée des deux côtés"
