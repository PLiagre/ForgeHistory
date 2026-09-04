"""Le verdict est une donnée, pas une prose.

Chaque refus part d'un verdict valide et casse **exactement une chose**.
Un contrôle qui partirait d'un fichier vague ne prouverait pas ce qu'il
croit prouver : il rougirait pour n'importe quelle raison.
"""

from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys

import pytest

from atelier import verdict as module_verdict
from atelier.verdict import Verdict, VerdictErreur

RACINE = Path(__file__).resolve().parent.parent

# Deux révisions complètes, distinctes. Elles ne désignent aucun commit
# réel : ce module ne parle ni à git ni à GitHub.
SHA = "e5589e3aa1b2c3d4e5f60718293a4b5c6d7e8f90"
AUTRE_SHA = "d8a640712345678901234567890abcdef1234567"

AUTEUR_CODE = "cursor"
RELECTEUR = "codex"


def _valide(**changements) -> dict:
    """Un verdict qui passe. Chaque contrôle en casse un morceau, un seul."""
    corps = {
        "objet": module_verdict.DIFF,
        "lot": "046-la-mer-est-un-port-commun",
        "pr": 206,
        "sha": SHA,
        "auteur": RELECTEUR,
        "verdict": module_verdict.PASS,
        "motifs": [],
    }
    corps.update(changements)
    return corps


def _deposer(tmp_path: Path, corps, nom: str = "verdict.json") -> Path:
    cible = tmp_path / nom
    if isinstance(corps, str):
        cible.write_text(corps, encoding="utf-8")
    else:
        cible.write_text(json.dumps(corps, ensure_ascii=False), encoding="utf-8")
    return cible


def _commande(chemin: Path, *, sha: str = SHA, auteur: str = AUTEUR_CODE):
    return subprocess.run(
        [sys.executable, "-m", "atelier", "verdict", "lire",
         "--fichier", str(chemin), "--sha", sha, "--auteur-code", auteur],
        capture_output=True, text=True, cwd=RACINE,
    )


# ------------------------------------------- SC1 : trois codes de sortie


def test_codes_de_sortie_pass_fail_inconnu(tmp_path: Path):
    passe = _deposer(tmp_path, _valide(), "pass.json")
    echoue = _deposer(
        tmp_path,
        _valide(verdict=module_verdict.FAIL, motifs=["SC3 n'est pas mesurée"]),
        "fail.json",
    )
    absent = tmp_path / "rien.json"

    assert _commande(passe).returncode == 0
    assert _commande(echoue).returncode == 1
    assert _commande(absent).returncode == 2


def test_codes_un_fail_imprime_ses_motifs(tmp_path: Path):
    motifs = ["SC3 n'est pas mesurée", "le périmètre déborde sur sim/engine.py"]
    chemin = _deposer(tmp_path, _valide(verdict=module_verdict.FAIL, motifs=motifs))
    fin = _commande(chemin)
    assert fin.returncode == 1
    for motif in motifs:
        assert motif in fin.stdout


def test_codes_un_inconnu_ne_dit_jamais_pass(tmp_path: Path):
    """2 n'est pas un feu vert, et ne doit pas en avoir l'air."""
    fin = _commande(tmp_path / "rien.json")
    assert fin.returncode == 2
    assert module_verdict.INCONNU in fin.stdout
    assert module_verdict.PASS not in fin.stdout


# ------------------------------------------ SC2 : le rouge sur les quatre refus


def test_refus_absent(tmp_path: Path):
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(tmp_path / "rien.json")
    assert "absent" in str(exc.value)


def test_refus_illisible(tmp_path: Path):
    chemin = _deposer(tmp_path, '{"objet": "diff", ')
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "JSON" in str(exc.value)


def test_refus_perime(tmp_path: Path):
    """Le cas de l'auteur qui repousse après la relecture."""
    chemin = _deposer(tmp_path, _valide())
    fin = _commande(chemin, sha=AUTRE_SHA)
    assert fin.returncode == 2
    assert "périmé" in fin.stderr
    assert SHA[:12] in fin.stderr and AUTRE_SHA[:12] in fin.stderr


def test_refus_auteur_interdit(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide(auteur=AUTEUR_CODE))
    fin = _commande(chemin)
    assert fin.returncode == 2
    assert "interdit" in fin.stderr
    assert AUTEUR_CODE in fin.stderr


def test_refus_les_quatre_partent_du_meme_verdict_valide(tmp_path: Path):
    """Sans ce contrôle, un cas pourrait rougir pour une autre raison.

    Le verdict de référence passe ; chaque refus est donc bien dû à ce
    que son cas a cassé, et à rien d'autre.
    """
    assert _commande(_deposer(tmp_path, _valide())).returncode == 0


# ---------------------------------------------- SC3 : la prose ne verdit rien


def test_prose_rend_deux(tmp_path: Path):
    """L'avis du 3 septembre 2026, dans sa forme.

    Un avis textuel bloquant ne peut jamais valoir accord : il n'est pas
    un verdict, et l'atelier ne devine pas ce qu'il voulait dire.
    """
    avis = (
        "J'ai relu le diff du lot 046. Le périmètre est respecté, mais la\n"
        "condition SC3 n'est pas réellement mesurée : le contrôle nomme sa\n"
        "propre référence. À confirmer par pytest. Je ne recommande pas la\n"
        "fusion en l'état.\n"
    )
    chemin = _deposer(tmp_path, avis, "avis.txt")
    fin = _commande(chemin)
    assert fin.returncode == 2
    assert "prose" in fin.stderr


def test_prose_un_avis_favorable_ne_passe_pas_davantage(tmp_path: Path):
    """Le refus tient à la forme, pas au sentiment du texte."""
    chemin = _deposer(tmp_path, "Tout est bon, PASS, on peut fusionner.\n", "avis.txt")
    assert _commande(chemin).returncode == 2


def test_prose_un_fichier_vide_echoue(tmp_path: Path):
    chemin = _deposer(tmp_path, "   \n", "vide.json")
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "vide" in str(exc.value)


# ------------------------------------------- SC4 : un champ inconnu est un refus


def test_inconnu_un_champ_de_plus_est_refuse(tmp_path: Path):
    corps = _valide()
    corps["confiance"] = "haute"
    chemin = _deposer(tmp_path, corps)
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "confiance" in str(exc.value)


def test_inconnu_un_champ_manquant_est_refuse(tmp_path: Path):
    corps = _valide()
    del corps["motifs"]
    chemin = _deposer(tmp_path, corps)
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "motifs" in str(exc.value)


def test_inconnu_un_verdict_hors_liste_est_refuse(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide(verdict="PEUT-ÊTRE"))
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "PEUT-ÊTRE" in str(exc.value)


def test_inconnu_un_objet_hors_liste_est_refuse(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide(objet="capture"))
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "capture" in str(exc.value)


def test_inconnu_le_compte_des_cles_derive_du_module(tmp_path: Path):
    """Le dénominateur vient du module, pas d'une liste recopiée ici."""
    corps = _valide()
    assert set(corps) == set(module_verdict.CLES)
    assert module_verdict.CLES, "échantillon vide"


# ---------------------------------------- SC5 : un FAIL sans motif est illisible


def test_motifs_un_fail_sans_motif_rend_deux(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide(verdict=module_verdict.FAIL, motifs=[]))
    fin = _commande(chemin)
    assert fin.returncode == 2, "un FAIL muet n'est pas un refus, c'est un illisible"


def test_motifs_un_motif_vide_est_refuse(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide(verdict=module_verdict.FAIL, motifs=["  "]))
    with pytest.raises(VerdictErreur):
        module_verdict.lire(chemin)


def test_motifs_un_pass_sans_motif_passe(tmp_path: Path):
    assert _commande(_deposer(tmp_path, _valide(motifs=[]))).returncode == 0


# -------------------------------------- SC6 : l'auteur du code ne signe pas


def test_auteur_un_pass_signe_par_l_auteur_du_code_rend_deux(tmp_path: Path):
    """Le refus se déclenche sur l'égalité des noms, pas sur le verdict."""
    chemin = _deposer(tmp_path, _valide(auteur=AUTEUR_CODE, verdict=module_verdict.PASS))
    assert _commande(chemin).returncode == 2


def test_auteur_un_fail_signe_par_l_auteur_du_code_rend_deux_aussi(tmp_path: Path):
    chemin = _deposer(
        tmp_path,
        _valide(auteur=AUTEUR_CODE, verdict=module_verdict.FAIL, motifs=["un motif"]),
    )
    assert _commande(chemin).returncode == 2


def test_auteur_un_auteur_de_code_absent_est_declare(tmp_path: Path):
    """Une absence se déclare : l'atelier ne devine pas qui a écrit."""
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.valider(
            module_verdict.lire(_deposer(tmp_path, _valide())),
            sha=SHA, auteur_code="   ",
        )
    assert "ne devine pas" in str(exc.value)


# ------------------------------------------- SC7 : le SHA est comparé, pas lu


def test_sha_le_module_ne_lance_aucun_processus():
    """Il reçoit une révision, il ne va pas la chercher.

    Un composant qui interrogerait git ou GitHub serait deux couches à
    la fois, et ne s'éprouverait plus sans réseau.
    """
    texte = (RACINE / "atelier" / "verdict.py").read_text(encoding="utf-8")
    for interdit in ("subprocess", "shutil", "os.environ", "urllib"):
        assert interdit not in texte, f"verdict.py touche à {interdit}"


def test_sha_un_prefixe_ne_vaut_pas_une_revision(tmp_path: Path):
    """Sept caractères désignent une famille de commits, pas un commit."""
    chemin = _deposer(tmp_path, _valide(sha=SHA[:7]))
    with pytest.raises(VerdictErreur) as exc:
        module_verdict.lire(chemin)
    assert "40" in str(exc.value)


def test_sha_la_revision_a_comparer_doit_etre_lisible(tmp_path: Path):
    chemin = _deposer(tmp_path, _valide())
    fin = _commande(chemin, sha="HEAD")
    assert fin.returncode == 2
    assert "illisible" in fin.stderr


def test_sha_une_revision_identique_passe(tmp_path: Path):
    rendu = module_verdict.lire_et_valider(
        _deposer(tmp_path, _valide()), sha=SHA, auteur_code=AUTEUR_CODE
    )
    assert isinstance(rendu, Verdict)
    assert rendu.passe


# ------------------------------------------------------------- le brief aussi


def test_un_verdict_de_brief_se_lit_comme_un_verdict_de_diff(tmp_path: Path):
    """`objet` distingue déjà les deux : aucun champ n'est à ajouter."""
    chemin = _deposer(tmp_path, _valide(objet=module_verdict.BRIEF))
    rendu = module_verdict.lire(chemin)
    assert rendu.objet == module_verdict.BRIEF
    assert _commande(chemin).returncode == 0


def test_refus_les_quatre_se_nomment_sur_stderr(tmp_path: Path):
    """Un refus qu'on doit deviner ne dit rien à celui qui répare.

    Les quatre passent par la commande, pas par l'exception : c'est ce
    que `crons/tour.sh` lira dans le journal.
    """
    cas = {
        "absent": (tmp_path / "rien.json", SHA, AUTEUR_CODE),
        "prose": (_deposer(tmp_path, "un avis en français", "avis.txt"), SHA, AUTEUR_CODE),
        "périmé": (_deposer(tmp_path, _valide(), "frais.json"), AUTRE_SHA, AUTEUR_CODE),
        "interdit": (
            _deposer(tmp_path, _valide(auteur=AUTEUR_CODE), "signe.json"), SHA, AUTEUR_CODE
        ),
    }
    assert cas, "échantillon vide"
    muets = []
    for mot, (chemin, sha, auteur) in cas.items():
        fin = _commande(chemin, sha=sha, auteur=auteur)
        if fin.returncode != 2 or mot not in fin.stderr:
            muets.append((mot, fin.returncode, fin.stderr.strip()[:70]))
    assert not muets, f"refus qui ne se nomment pas : {muets}"
