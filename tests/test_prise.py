"""La prise d'une carte et de ses fichiers est un seul geste.

Les contrôles de concurrence lancent de vrais processus et les font
attendre le **même instant d'horloge** (`ATELIER_PRISE_RENDEZ_VOUS`)
après avoir listé la boîte. Un simple délai aurait mesuré l'écart de
démarrage des deux processus : le contrôle serait passé ou non selon la
machine, et un contrôle qui dépend de la chance ne prouve rien.

Le rouge se prouve en retirant la serrure (`ATELIER_PRISE_SANS_SERRURE`)
et en exigeant que la course se produise alors.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import time

import pytest

from atelier import boite, prise, verrou

RACINE = Path(__file__).resolve().parent.parent


def _carte(racine: Path, lot: str, fichiers: list[str], boite_nom: str = "a-coder") -> None:
    boite.deposer(
        racine, boite_nom,
        boite.Carte(lot=lot, brief=f"briefs/{lot}.md", fichiers=fichiers),
    )


def _env(**sup) -> dict[str, str]:
    """L'environnement d'un tour : sans les variables de l'atelier, et en UTF-8.

    Sans `PYTHONIOENCODING`, une phrase accentuée traverse le tuyau
    dans l'encodage de la console — et un contrôle qui cherche
    « déjà là » mesure alors la machine, pas le code.
    """
    env = {k: v for k, v in os.environ.items() if not k.startswith("ATELIER_")}
    env["PYTHONIOENCODING"] = "utf-8"
    env.update({k: str(v) for k, v in sup.items()})
    return env


def _prendre(racine: Path, role: str = "coder", **env_sup) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-m", "atelier", "prendre", "--projet", str(racine), "--role", role],
        capture_output=True, text=True, encoding="utf-8", cwd=RACINE,
        env=_env(**env_sup),
    )


def _en_parallele(racine: Path, role: str = "coder", *, dans: float = 4.0, **env_sup):
    """Deux prises qui se disputent la boîte au même instant.

    Rend (lot pris, code, stderr) pour chacune. `dans` doit dépasser le
    temps de démarrage d'un interpréteur : c'est ce qui garantit que les
    deux ont listé la boîte avant que l'une agisse.
    """
    env_sup.setdefault("ATELIER_PRISE_RENDEZ_VOUS", repr(time.time() + dans))
    argv = [sys.executable, "-m", "atelier", "prendre",
            "--projet", str(racine), "--role", role]
    env = _env(**env_sup)
    lances = [
        subprocess.Popen(argv, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                         text=True, encoding="utf-8", cwd=RACINE, env=env)
        for _ in range(2)
    ]
    tours = []
    for lance in lances:
        sortie, erreur = lance.communicate()
        tours.append((sortie.strip(), lance.returncode, erreur))
    return tours


def _lots_pris(tours) -> list[str]:
    return sorted(lot for lot, _, _ in tours)


# ---------------------------------------------- SC1 : deux prises, deux cartes


def test_concurrence_deux_prises_ne_rendent_pas_la_meme_carte(tmp_path: Path):
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    _carte(tmp_path, "047-bourg", ["sim/bourg.py"])
    tours = _en_parallele(tmp_path)
    assert all(code == 0 for _, code, _ in tours), tours
    assert _lots_pris(tours) == ["046-mer", "047-bourg"], tours


def test_concurrence_une_seule_carte_l_autre_rend_rien(tmp_path: Path):
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    tours = _en_parallele(tmp_path)
    assert all(code == 0 for _, code, _ in tours), tours
    assert _lots_pris(tours) == ["046-mer", "RIEN"], tours


# ------------------------- SC2 : le rouge, prouvé en retirant la serrure


def test_sans_serrure_la_collision_se_produit(tmp_path: Path):
    """Sans ce cas, rien ne dirait que la serrure sert à quelque chose.

    Un contrôle de concurrence qui passerait aussi sans la garde ne
    prouve rien : il mesure la chance de l'ordonnanceur.
    """
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    _carte(tmp_path, "047-bourg", ["sim/bourg.py"])
    tours = _en_parallele(tmp_path, ATELIER_PRISE_SANS_SERRURE="1")
    # Deux cartes libres, deux tours : l'issue saine est celle du
    # contrôle précédent — chacun repart avec la sienne, et les deux
    # sortent 0. Sans serrure, elle n'est pas atteinte : les deux lisent
    # la même tête de file et se disputent la même place.
    sain = all(code == 0 for _, code, _ in tours) and _lots_pris(tours) == [
        "046-mer", "047-bourg"
    ]
    assert not sain, (
        "sans serrure, les deux tours ne devraient pas se répartir proprement "
        f"les deux cartes libres : {tours}"
    )
    assert any(code != 0 for _, code, _ in tours), tours
    # Le tour perdant échoue à l'endroit exact où l'autre l'a devancé :
    # au dépôt dans `en-cours`, ou au retrait de la boîte du rôle, selon
    # qui a gagné. Ce qui ne varie pas, c'est la carte qu'ils se
    # disputaient — affirmer la phrase du perdant serait affirmer l'ordre
    # d'un ordonnanceur.
    assert any("046-mer" in erreur for _, _, erreur in tours), tours


# ------------------------------------------------- SC3 : tout ou rien


def test_tout_ou_rien_un_verrou_refuse_ne_deplace_rien(tmp_path: Path):
    verrou.poser(tmp_path, "044-mineur", ["sim/mer.py"])
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    assert prise.prendre(tmp_path, "coder") is None
    assert [c.lot for c in boite.lister(tmp_path, "a-coder")] == ["046-mer"]
    assert prise.en_cours(tmp_path) == []
    assert {v.lot for v in verrou.charger(tmp_path).poses} == {"044-mineur"}


def test_tout_ou_rien_une_carte_sans_ressource_ne_bouge_pas(tmp_path: Path):
    """Une carte sans périmètre et sans brief n'est pas prenable."""
    _carte(tmp_path, "049-vide", [])
    assert prise.prendre(tmp_path, "coder") is None
    assert [c.lot for c in boite.lister(tmp_path, "a-coder")] == ["049-vide"]
    assert prise.en_cours(tmp_path) == []


# ------------------------------------------------- SC4 et SC5 : collision, disjoints


def test_collision_le_second_lot_attend_et_on_dit_qui_tient(tmp_path: Path):
    verrou.poser(tmp_path, "044-mineur", ["sim/commun.py"])
    _carte(tmp_path, "046-mer", ["sim/commun.py"])
    fin = _prendre(tmp_path)
    assert fin.returncode == 0
    assert fin.stdout.strip() == "RIEN"
    assert "044-mineur" in fin.stderr
    assert "sim/commun.py" in fin.stderr


def test_collision_la_carte_suivante_est_prise_si_elle_est_libre(tmp_path: Path):
    """« 044 occupe commun.py ? Le cron prend 047 s'il est disjoint. »"""
    verrou.poser(tmp_path, "044-mineur", ["sim/commun.py"])
    _carte(tmp_path, "046-mer", ["sim/commun.py"])
    _carte(tmp_path, "047-bourg", ["sim/bourg.py"])
    carte = prise.prendre(tmp_path, "coder")
    assert carte is not None and carte.lot == "047-bourg"


def test_disjoints_deux_verrous_coexistent(tmp_path: Path):
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    _carte(tmp_path, "047-bourg", ["sim/bourg.py"])
    assert prise.prendre(tmp_path, "coder").lot == "046-mer"
    assert prise.prendre(tmp_path, "coder").lot == "047-bourg"
    assert len(verrou.charger(tmp_path).poses) == 2
    assert sorted(c.lot for c in prise.en_cours(tmp_path)) == ["046-mer", "047-bourg"]


# ------------------------------------------- SC6 : une serrure abandonnée


def test_perimee_une_serrure_vieille_est_reprise(tmp_path: Path, monkeypatch, capsys):
    cible = prise.chemin_serrure(tmp_path)
    cible.parent.mkdir(parents=True, exist_ok=True)
    cible.mkdir()
    os.utime(cible, (0, 0))
    monkeypatch.setenv(prise.PERIME, "1")
    monkeypatch.setenv(prise.ATTENTE, "0")
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    carte = prise.prendre(tmp_path, "coder")
    assert carte is not None and carte.lot == "046-mer"
    assert "abandonnée" in capsys.readouterr().err


def test_perimee_une_serrure_fraiche_fait_renoncer(tmp_path: Path, monkeypatch):
    cible = prise.chemin_serrure(tmp_path)
    cible.parent.mkdir(parents=True, exist_ok=True)
    cible.mkdir()
    monkeypatch.setenv(prise.PERIME, "3600")
    monkeypatch.setenv(prise.ATTENTE, "0")
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    with pytest.raises(prise.ServrureTenue):
        prise.prendre(tmp_path, "coder")
    assert [c.lot for c in boite.lister(tmp_path, "a-coder")] == ["046-mer"]


def test_perimee_renoncer_n_est_pas_une_panne(tmp_path: Path):
    """Le tour sort 0 : la carte sera là au prochain réveil."""
    cible = prise.chemin_serrure(tmp_path)
    cible.parent.mkdir(parents=True, exist_ok=True)
    cible.mkdir()
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    fin = _prendre(tmp_path, ATELIER_PRISE_ATTENTE="0", ATELIER_PRISE_PERIME="3600")
    assert fin.returncode == 0
    assert fin.stdout.strip() == "RIEN"
    assert "se recouche" in fin.stderr


# ------------------------------------------------- SC7 : l'aperçu ne prend rien


def test_apercu_prochain_ne_deplace_rien_et_ne_verrouille_rien(tmp_path: Path):
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    fin = subprocess.run(
        [sys.executable, "-m", "atelier", "prochain",
         "--projet", str(tmp_path), "--role", "coder"],
        capture_output=True, text=True, cwd=RACINE,
    )
    assert fin.returncode == 0
    assert json.loads(fin.stdout)["lot"] == "046-mer"
    assert [c.lot for c in boite.lister(tmp_path, "a-coder")] == ["046-mer"]
    assert prise.en_cours(tmp_path) == []
    assert verrou.charger(tmp_path).poses == []


# ------------------------------------------------- rendre : la carte revient


def test_rendre_remet_la_carte_dans_la_boite_de_son_role(tmp_path: Path):
    """`avancer` et `echouer` lisent la boîte du rôle : elle y retourne."""
    _carte(tmp_path, "046-mer", ["sim/mer.py"])
    prise.prendre(tmp_path, "coder")
    assert boite.lister(tmp_path, "a-coder") == []
    prise.rendre(tmp_path, "coder", "046-mer")
    assert [c.lot for c in boite.lister(tmp_path, "a-coder")] == ["046-mer"]
    assert prise.en_cours(tmp_path) == []


def test_rendre_conserve_ce_que_la_carte_portait(tmp_path: Path):
    boite.deposer(
        tmp_path, "a-relire",
        boite.Carte(lot="046-mer", brief="briefs/046-mer.md", fichiers=["sim/mer.py"], pr=206),
    )
    prise.prendre(tmp_path, "relire")
    prise.rendre(tmp_path, "relire", "046-mer")
    assert boite.lire(tmp_path, "a-relire", "046-mer").pr == 206


def test_un_role_qui_n_ecrit_pas_ne_verrouille_rien(tmp_path: Path):
    """Le relecteur prend une carte ; il ne tient aucun fichier."""
    boite.deposer(
        tmp_path, "a-relire",
        boite.Carte(lot="046-mer", brief="briefs/046-mer.md", fichiers=["sim/mer.py"]),
    )
    assert prise.prendre(tmp_path, "relire").lot == "046-mer"
    assert verrou.charger(tmp_path).poses == []


def test_un_role_inconnu_est_refuse(tmp_path: Path):
    with pytest.raises(prise.PriseErreur):
        prise.prendre(tmp_path, "eclaireur")
