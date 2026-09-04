"""Auteur ≠ relecteur. L'atelier ne fusionne pas. Sans --run, rien n'est écrit."""

from pathlib import Path
import subprocess
import sys

import pytest

from atelier import etat, projet
from atelier.__main__ import main
from atelier.cycle import preparer, CycleErreur
from atelier.etat import FusionInterdite
from tests.test_porte import BRIEF_SAIN


def _produit(tmp_path: Path) -> Path:
    (tmp_path / "briefs").mkdir()
    (tmp_path / "atelier.toml").write_text(
        """
[projet]
nom = "JeuTest"
briefs = "briefs"
tests = "python3 -m pytest tests/ -q"
fumee = "python3 -m jeu --ticks 0"
branche_base = "master"
prefixe_branche = "agent/"

[roles]
ecriture = "claude"
execution = "cursor"
controle = "codex"
""".lstrip(),
        encoding="utf-8",
    )
    brief = tmp_path / "briefs" / "001-un-changement.md"
    brief.write_text(BRIEF_SAIN, encoding="utf-8")
    return tmp_path


def test_roles_identiques_refuses(tmp_path: Path):
    (tmp_path / "atelier.toml").write_text(
        """
[projet]
nom = "X"
briefs = "briefs"
tests = "true"
fumee = "true"
branche_base = "master"
prefixe_branche = "agent/"

[roles]
ecriture = "cursor"
execution = "cursor"
controle = "cursor"
""".lstrip(),
        encoding="utf-8",
    )
    with pytest.raises(projet.ProjetIncomplet):
        projet.charger(tmp_path)


def test_apercu_sans_ecriture(tmp_path: Path):
    racine = _produit(tmp_path)
    apercu = preparer(racine / "briefs" / "001-un-changement.md", racine)
    assert apercu.executant == "cursor"
    assert apercu.relecteur == "codex"
    assert apercu.executant != apercu.relecteur
    assert apercu.worktree == str(racine.parent / f"{racine.name}-001-un-changement")
    assert not (racine / ".atelier").exists()
    assert list(racine.glob("atelier-echange/**/*")) == []


def test_start_sans_run_n_ecrit_rien(tmp_path: Path):
    racine = _produit(tmp_path)
    code = main(
        [
            "start",
            str(racine / "briefs" / "001-un-changement.md"),
            "--projet",
            str(racine),
        ]
    )
    assert code == 0
    assert not (racine / ".atelier").exists()


def test_fusionner_refuse():
    with pytest.raises(FusionInterdite):
        etat.fusionner(
            etat.nouveau(
                lot="001",
                brief=Path("briefs/001.md"),
                branche="agent/001",
                worktree=Path("/tmp/wt"),
                auteur_code="cursor",
                relecteur="codex",
                fichiers=["src/foo.py"],
            )
        )


def test_cli_fusionner_code_2():
    proc = subprocess.run(
        [sys.executable, "-m", "atelier", "fusionner"],
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 2
    assert "ne fusionne pas" in proc.stderr


def test_auteur_egal_relecteur_refuse():
    with pytest.raises(ValueError, match="relecteur"):
        etat.nouveau(
            lot="001",
            brief=Path("briefs/001.md"),
            branche="agent/001",
            worktree=Path("/tmp/wt"),
            auteur_code="cursor",
            relecteur="cursor",
            fichiers=["src/foo.py"],
        )


BRIEF_AVEC_INTERDITS = """# Brief 046 — la mer

## But
Une phrase.

## Règle du monde
Aucun fondement.

## Périmètre

En écriture : `sim/engine.py`, `sim/constants.py` **uniquement** pour déclarer
un panier, et `sim/tests/test_commerce.py` pour y **ajouter** des cas.

**`sim/tests/test_write_coverage.py` n'est pas modifiable.** Élargir le monde
d'épreuve n'est pas la solution retenue.

Tout autre chemin est interdit, nommément : `sim/MODELE.md`, `sim/model.py`,
`sim/aggregation.py`, la carte figée `data/world-1400.json`, et ce brief.

## Conditions de succès

### SC1 — une commande
```bash
python3 -m pytest sim/tests/ -q
```

## Hors périmètre
Rien d'autre.
"""


def test_le_perimetre_ne_compte_pas_les_fichiers_interdits(tmp_path: Path):
    """Un périmètre nomme aussi ce qu'il interdit (règle 6 du produit).

    Le verrou ne doit tenir que les fichiers autorisés : sinon le lot 046
    tiendrait `sim/aggregation.py`, qui est le périmètre du 047, et les
    deux lots « disjoints » se bloqueraient l'un l'autre.
    """
    from atelier.cycle import _fichiers_du_perimetre

    brief = tmp_path / "046-la-mer.md"
    brief.write_text(BRIEF_AVEC_INTERDITS, encoding="utf-8")
    fichiers = _fichiers_du_perimetre(brief)
    assert fichiers, "échantillon vide"
    assert fichiers == ["sim/engine.py", "sim/constants.py", "sim/tests/test_commerce.py"]
    for interdit in ("sim/aggregation.py", "sim/tests/test_write_coverage.py", "data/world-1400.json"):
        assert interdit not in fichiers


def test_brief_infirme_bloque_le_cycle(tmp_path: Path):
    racine = _produit(tmp_path)
    brief = racine / "briefs" / "001-un-changement.md"
    brief.write_text("# Brief 001\n\nrien.\n", encoding="utf-8")
    with pytest.raises(CycleErreur, match="porte"):
        preparer(brief, racine)


# ---------------------------- la fiche d'un lot n'est pas tout le fichier

from atelier import verrou
from atelier.cycle import _fichiers_du_perimetre

FEUILLE = "ROADMAP.md"


def _produit_avec_feuille(tmp_path: Path) -> Path:
    """Le même produit, mais son branchement nomme un registre."""
    racine = _produit(tmp_path)
    toml = racine / "atelier.toml"
    toml.write_text(
        toml.read_text(encoding="utf-8").replace(
            'prefixe_branche = "agent/"',
            'prefixe_branche = "agent/"\nfeuille = "ROADMAP.md"',
        ),
        encoding="utf-8",
    )
    (racine / FEUILLE).write_text("# feuille\n", encoding="utf-8")
    return racine


def _brief(racine: Path, nom: str, perimetre: str) -> Path:
    numero = nom.split("-", 1)[0]
    cible = racine / "briefs" / f"{nom}.md"
    cible.write_text(
        BRIEF_SAIN.replace("# Brief 001 —", f"# Brief {numero} —").replace(
            "Écriture autorisée : `src/foo.py`. Tout le reste est interdit.",
            perimetre,
        ),
        encoding="utf-8",
    )
    return cible


def test_fiche_implicite_le_perimetre_la_porte_sans_qu_on_la_nomme(tmp_path: Path):
    """Elle n'a plus à être écrite : c'est ce qui la rend impossible à oublier."""
    racine = _produit_avec_feuille(tmp_path)
    brief = _brief(racine, "047-bourg", "Écriture autorisée : `src/foo.py`.")
    lus = _fichiers_du_perimetre(brief, "ROADMAP.md")
    assert lus == ["src/foo.py", f"{FEUILLE}{verrou.SEPARATEUR}047-bourg"]


def test_fiche_implicite_nommer_le_registre_designe_sa_propre_fiche(tmp_path: Path):
    """« La fiche fait partie du périmètre implicite, et rien d'autre de ROADMAP.md. »

    C'est la règle du dépôt produit. Un brief qui écrit le nom du
    fichier n'obtient donc pas le fichier entier — sinon aucun brief
    existant ne serait jamais disjoint d'un autre.
    """
    racine = _produit_avec_feuille(tmp_path)
    brief = _brief(
        racine, "047-bourg",
        "Écriture autorisée : `src/foo.py` et `ROADMAP.md`, pour la fiche du lot.",
    )
    lus = _fichiers_du_perimetre(brief, FEUILLE)
    assert FEUILLE not in lus
    assert f"{FEUILLE}{verrou.SEPARATEUR}047-bourg" in lus


def test_fiche_etrangere_est_refusee_et_nomme_les_deux(tmp_path: Path):
    racine = _produit_avec_feuille(tmp_path)
    brief = _brief(
        racine, "047-bourg",
        "Écriture autorisée : `src/foo.py` et `ROADMAP.md#046-mer`.",
    )
    with pytest.raises(CycleErreur) as exc:
        _fichiers_du_perimetre(brief, FEUILLE)
    assert "046-mer" in str(exc.value)
    assert "047-bourg" in str(exc.value)


def test_sans_feuille_declaree_rien_ne_change(tmp_path: Path):
    """L'atelier ne cherche pas un registre au hasard."""
    racine = _produit(tmp_path)
    brief = _brief(racine, "047-bourg", "Écriture autorisée : `src/foo.py` et `ROADMAP.md`.")
    lus = _fichiers_du_perimetre(brief)
    assert lus == ["src/foo.py", FEUILLE]
    assert not any(verrou.SEPARATEUR in nom for nom in lus)


def test_deux_lots_disjoints_le_restent_avec_leurs_fiches(tmp_path: Path):
    """Le bout du bout : deux périmètres disjoints, deux verrous posés.

    Avant, les deux nommaient ROADMAP.md et le second était refusé.
    """
    racine = _produit_avec_feuille(tmp_path)
    a = _brief(racine, "046-mer", "Écriture autorisée : `src/mer.py` et `ROADMAP.md`.")
    b = _brief(racine, "047-bourg", "Écriture autorisée : `src/bourg.py` et `ROADMAP.md`.")
    verrou.poser(racine, "046-mer", _fichiers_du_perimetre(a, FEUILLE))
    verrou.poser(racine, "047-bourg", _fichiers_du_perimetre(b, FEUILLE))
    assert len(verrou.charger(racine).poses) == 2


def test_le_rouge_est_prouve_deux_lots_qui_partagent_un_fichier_se_heurtent(tmp_path: Path):
    """Sans ce cas, le lot aurait pu simplement désarmer le verrou."""
    racine = _produit_avec_feuille(tmp_path)
    a = _brief(racine, "046-mer", "Écriture autorisée : `src/commun.py` et `ROADMAP.md`.")
    b = _brief(racine, "047-bourg", "Écriture autorisée : `src/commun.py` et `ROADMAP.md`.")
    verrou.poser(racine, "046-mer", _fichiers_du_perimetre(a, FEUILLE))
    with pytest.raises(verrou.Collision) as exc:
        verrou.poser(racine, "047-bourg", _fichiers_du_perimetre(b, FEUILLE))
    assert "src/commun.py" in str(exc.value)


def test_la_fiche_du_lot_se_derive_du_branchement(tmp_path: Path):
    produit = projet.charger(_produit_avec_feuille(tmp_path))
    assert produit.fiche_du_lot("047-bourg") == f"{FEUILLE}{verrou.SEPARATEUR}047-bourg"
    autre = tmp_path / "autre"
    autre.mkdir()
    assert projet.charger(_produit(autre)).fiche_du_lot("047") is None


def test_un_perimetre_vide_le_reste_meme_avec_une_feuille(tmp_path: Path):
    """Sinon la garde « périmètre sans fichier nommé » ne rougirait plus.

    Un lot infirme gagnerait sa fiche, aurait donc une ressource, et
    entrerait en file. Mesuré sur le brief 048 de ForgeHistory, dont la
    section Périmètre ne nomme aucun fichier.
    """
    racine = _produit_avec_feuille(tmp_path)
    brief = racine / "briefs" / "048-sans-perimetre.md"
    brief.write_text(
        BRIEF_SAIN.replace("# Brief 001 —", "# Brief 048 —").replace(
            "Écriture autorisée : `src/foo.py`. Tout le reste est interdit.",
            "Le lot ne nomme aucun fichier.",
        ),
        encoding="utf-8",
    )
    assert _fichiers_du_perimetre(brief, FEUILLE) == []


def test_un_perimetre_entierement_exclu_reste_vide(tmp_path: Path):
    """Même chose quand les seuls fichiers nommés sont des interdits."""
    racine = _produit_avec_feuille(tmp_path)
    brief = _brief(racine, "049-tout-interdit", "Tout chemin est interdit, nommément `src/foo.py`.")
    assert _fichiers_du_perimetre(brief, FEUILLE) == []
