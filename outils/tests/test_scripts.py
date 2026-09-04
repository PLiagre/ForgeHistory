"""Les gestes des workflows, joués sur le banc.

Un seul de ces contrôles aurait suffi à voir la panne du 4 septembre 2026 :
`relecture.sh` mourait sur `errexit` avant de poser son état. Le banc joue
les scripts avec `bash -e`, comme GitHub, avec de faux `gh`, `git` et
`python` — et il affirme le **geste**, pas le message.
"""

import pytest

from outils.tests.banc import Banc

TETE = "a" * 40


@pytest.fixture
def banc(tmp_path):
    return Banc(tmp_path)


# ------------------------------------------------------------ relecture


def test_un_verdict_defavorable_pose_quand_meme_son_etat(banc):
    """Le cas exact de la PR 225 : la relecture manque, la commande sort 1,
    et c'est justement là que l'état doit être posé — en rouge."""
    banc.poser("python", "FAIL  PR 225 — aucune approbation : relecture absente", code=1)
    banc.poser("gh")
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE)

    assert resultat.returncode == 0, resultat.stderr
    pose = banc.appel("gh", "statuses")
    assert pose is not None, banc.appels
    assert banc.valeur(pose, "state") == "failure"
    assert banc.valeur(pose, "context") == "relecture"
    assert "relecture absente" in (banc.valeur(pose, "description") or "")


def test_un_verdict_favorable_pose_un_etat_vert(banc):
    banc.poser("python", "PASS  PR 225 — approuvée sur aaaaaaa par pliagre", code=0)
    banc.poser("gh")
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE)

    assert resultat.returncode == 0
    assert banc.valeur(banc.appel("gh", "statuses"), "state") == "success"


def test_la_revision_absente_se_demande(banc):
    banc.poser("python", "PASS  PR 225 — approuvée", code=0)
    banc.poser("gh", sortie=TETE)
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION="")

    assert resultat.returncode == 0
    assert banc.appel("gh pr view 225") is not None
    # L'état est posé sur la révision qu'on vient de demander, pas ailleurs.
    assert banc.appel("gh", f"statuses/{TETE}") is not None


def test_sans_revision_lisible_on_refuse_au_lieu_de_poser_n_importe_ou(banc):
    banc.poser("python", "PASS", code=0)
    banc.poser("gh", sortie="", code=1)
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION="")

    assert resultat.returncode == 1
    assert banc.appel("gh", "statuses") is None
    assert "introuvable" in resultat.stderr


def test_un_etat_refuse_par_github_fait_rougir_le_travail(banc):
    """Ce script rend compte de lui-même : s'il n'a pas posé l'état, il
    doit rougir — sinon un contrôle absent passerait pour un contrôle
    qu'on a choisi de ne pas poser."""
    banc.poser("python", "FAIL  PR 225 — relecture absente", code=1)
    banc.poser("gh", code=1)
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE)

    assert resultat.returncode == 1
    assert "non posé" in resultat.stderr


def test_un_outil_muet_n_approuve_rien(banc):
    """Un verdict vide avec un code 0 est le pire des cas : il poserait un
    « success » que personne n'a prononcé. On refuse."""
    banc.poser("python", "", code=0)
    banc.poser("gh")
    resultat = banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE)

    assert resultat.returncode == 1
    assert banc.appel("gh", "statuses") is None
    assert "vide" in resultat.stderr


def test_le_lien_du_journal_accompagne_l_etat(banc):
    banc.poser("python", "PASS  PR 225 — approuvée", code=0)
    banc.poser("gh")
    banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE,
               LIEN="https://github.com/o/r/actions/runs/1")
    pose = banc.appel("gh", "statuses")
    assert banc.valeur(pose, "target_url") == "https://github.com/o/r/actions/runs/1"


def test_sans_lien_l_etat_se_pose_quand_meme(banc):
    """Un `target_url` vide laisserait un lien mort sur le contrôle."""
    banc.poser("python", "PASS  PR 225 — approuvée", code=0)
    banc.poser("gh")
    banc.jouer("relecture.sh", DEPOT="o/r", PR="225", REVISION=TETE, LIEN="")
    pose = banc.appel("gh", "statuses")
    assert pose is not None
    assert banc.valeur(pose, "target_url") is None


# ----------------------------------------------------------- intégration


def test_fusionner_fusionne_range_la_branche_et_relance_le_tour(banc):
    banc.poser("gh", sortie="agent/049-fabriquer")
    resultat = banc.jouer(
        "integrer.sh", DEPOT="o/r", DECISION="fusionner 217", BASE="master",
        SORTIE=str(banc.dossier / "sortie.txt"),
    )

    assert resultat.returncode == 0, resultat.stderr
    assert banc.appel("gh pr merge 217 --merge") is not None
    assert banc.appel("gh api -X DELETE", "agent/049-fabriquer") is not None
    assert banc.appel("gh workflow run integration.yml") is not None
    assert "fusionnee=217" in (banc.dossier / "sortie.txt").read_text(encoding="utf-8")


def test_une_branche_qu_on_ne_peut_pas_supprimer_n_annule_pas_la_fusion(banc):
    """Un rangement raté ne coûte pas le tour : la fusion est faite, et le
    tour suivant doit partir quand même."""
    banc.poser("gh", selon=[
        (["--json headRefName"], "agent/049-fabriquer"),
        (["api", "-X", "DELETE"], "", 1),
    ])
    resultat = banc.jouer(
        "integrer.sh", DEPOT="o/r", DECISION="fusionner 217",
        SORTIE=str(banc.dossier / "sortie.txt"),
    )

    assert resultat.returncode == 0, resultat.stderr
    assert "fusionnee=217" in (banc.dossier / "sortie.txt").read_text(encoding="utf-8")
    assert banc.appel("gh workflow run integration.yml") is not None
    assert "non supprimée" in resultat.stdout


def test_une_fusion_refusee_fait_rougir_le_tour(banc):
    """L'inverse du cas précédent : si c'est la fusion qui échoue, rien
    n'est fusionné et le tour doit le dire."""
    banc.poser("gh", selon=[
        (["--json headRefName"], "agent/049-fabriquer"),
        (["pr", "merge"], "", 1),
    ])
    resultat = banc.jouer(
        "integrer.sh", DEPOT="o/r", DECISION="fusionner 217",
        SORTIE=str(banc.dossier / "sortie.txt"),
    )

    assert resultat.returncode != 0
    assert not (banc.dossier / "sortie.txt").exists()


def test_rebaser_attend_que_la_tete_ait_bouge_avant_de_redemander(banc):
    """`update-branch` rend 202 : GitHub accepte et pousse plus tard. Un
    contrôle demandé trop tôt s'épingle sur l'ANCIENNE révision, et la
    nouvelle n'en a jamais — bloquée sans rien de rouge à montrer."""
    banc.poser("gh", selon=[
        (["--json headRefName"], "agent/049-fabriquer"),
        # La tête ne bouge qu'au troisième coup d'œil.
        (["--json headRefOid"], ["a" * 40, "a" * 40, "b" * 40]),
    ])
    resultat = banc.jouer("integrer.sh", DEPOT="o/r", DECISION="rebaser 217", BASE="master")

    assert resultat.returncode == 0, resultat.stderr
    assert banc.appel("gh api -X PUT", "update-branch") is not None
    # Les trois contrôles sont demandés APRÈS le dernier coup d'œil.
    rangs = {a[0] + " " + " ".join(a[1:4]): i for i, a in enumerate(banc.brut)}
    dernier_coup = max(i for i, a in enumerate(banc.brut) if "headRefOid" in " ".join(a))
    for travail in ("tests.yml", "security.yml", "relecture.yml"):
        demande = next(i for i, a in enumerate(banc.brut) if travail in " ".join(a))
        assert demande > dernier_coup, f"{travail} demandé trop tôt"
    assert rangs is not None
    assert banc.valeur(banc.appel("gh workflow run relecture.yml"), "pr") == "217"


def test_une_tete_qui_ne_bouge_pas_rougit_au_lieu_de_demander_pour_rien(banc):
    banc.poser("gh", selon=[
        (["--json headRefName"], "agent/049-fabriquer"),
        (["--json headRefOid"], "a" * 40),
    ])
    resultat = banc.jouer("integrer.sh", DEPOT="o/r", DECISION="rebaser 217", BASE="master")

    assert resultat.returncode == 1
    assert "n'a pas bougé" in resultat.stderr
    assert banc.appel("gh workflow run tests.yml") is None


def test_rien_ne_touche_a_rien(banc):
    banc.poser("gh")
    resultat = banc.jouer("integrer.sh", DEPOT="o/r", DECISION="RIEN")

    assert resultat.returncode == 0
    assert banc.appels == []


def test_une_decision_illisible_rougit_au_lieu_de_ne_rien_faire(banc):
    """« Rien » et « je n'ai pas compris » sont deux réponses différentes.
    Les confondre arrêterait la file sans que personne le sache."""
    banc.poser("gh")
    resultat = banc.jouer("integrer.sh", DEPOT="o/r", DECISION="fusionnez-moi ça")

    assert resultat.returncode == 1
    assert "illisible" in resultat.stderr
    assert banc.appels == []


# ---------------------------------------------------------------- palier


def _projet(banc):
    (banc.dossier / "ROADMAP.md").write_text("# registre\n", encoding="utf-8")
    (banc.dossier / "palier.log").write_text(
        "couche 1  finie  (couverts : — · à couvrir : 046, 050)\n", encoding="utf-8"
    )


def _github_sans_palier(banc, url="https://github.com/o/r/pull/231"):
    """Aucune PR de palier ouverte, aucune branche qui traîne."""
    banc.poser("gh", selon=[(["pr list"], "")], sortie=url)
    banc.poser("git", selon=[(["ls-remote"], "", 2)])


def test_le_palier_ouvre_sa_branche_son_commit_et_sa_pr(banc):
    _projet(banc)
    _github_sans_palier(banc)
    resultat = banc.jouer(
        "palier.sh", DEPOT="o/r", LIGNE="palier 055 055-stabilisation-couche-1 couche=1",
        BASE="master", COMPTE_RENDU="palier.log",
    )

    assert resultat.returncode == 0, resultat.stderr
    assert banc.appel("git checkout -b feuille/055-stabilisation-couche-1") is not None
    assert banc.appel("git add ROADMAP.md") is not None
    assert banc.appel("git commit --file message.txt") is not None
    assert banc.appel("git push -u origin feuille/055-stabilisation-couche-1") is not None
    assert banc.appel("gh pr create") is not None
    # Le numéro de la PR se lit dans l'URL rendue, pas dans un second appel.
    assert banc.valeur(banc.appel("gh workflow run relecture.yml"), "pr") == "231"


def test_le_commit_du_palier_porte_une_connexion_que_github_sait_relier(banc):
    """Une adresse que GitHub ne relie à personne rend `auteurs_du_code`
    vide : la relecture refuse avant même de regarder les approbations, et
    la PR du palier ne peut plus jamais devenir intégrable."""
    _projet(banc)
    _github_sans_palier(banc)
    banc.jouer("palier.sh", DEPOT="o/r",
               LIGNE="palier 055 055-stabilisation-couche-1 couche=1", COMPTE_RENDU="palier.log")

    courriel = banc.appel("git config user.email")
    assert courriel is not None
    assert courriel[-1].endswith("users.noreply.github.com"), courriel
    assert "[bot]" in banc.appel("git config user.name")[-1]


def test_le_corps_de_la_pr_reprend_le_compte_rendu_du_registre(banc):
    _projet(banc)
    _github_sans_palier(banc)
    banc.jouer(
        "palier.sh", DEPOT="o/r", LIGNE="palier 055 055-stabilisation-couche-1 couche=1",
        COMPTE_RENDU="palier.log",
    )
    corps = (banc.dossier / "corps.txt").read_text(encoding="utf-8")
    assert "à couvrir : 046, 050" in corps
    assert "a-briefer" in corps
    message = (banc.dossier / "message.txt").read_text(encoding="utf-8")
    assert message.startswith("Palier couche 1 : le lot 055 entre au registre.")


def test_une_pr_de_palier_ouverte_empeche_le_second_depot(banc):
    """La garde porte sur la couche : le numéro libre peut avoir changé
    entre deux tours, la couche non."""
    _projet(banc)
    banc.poser("gh", selon=[(["pr list"], "231")])
    banc.poser("git")
    resultat = banc.jouer(
        "palier.sh", DEPOT="o/r", LIGNE="palier 057 057-stabilisation-couche-1 couche=1",
        COMPTE_RENDU="palier.log",
    )

    assert resultat.returncode == 0
    assert "déjà" in resultat.stdout
    assert banc.appel("gh pr create") is None
    assert banc.appel("git checkout") is None


def test_une_branche_sans_pr_ouverte_ne_bloque_pas_le_palier(banc):
    """Une PR fermée laissait sa branche derrière elle, et cette branche
    bloquait la couche pour toujours, en silence. Refuser un palier se
    fait en passant sa fiche à « abandonne », pas en fermant sa PR."""
    _projet(banc)
    banc.poser("gh", selon=[(["pr list"], "")], sortie="https://github.com/o/r/pull/240")
    banc.poser("git", selon=[(["ls-remote"], "abc\trefs/heads/feuille/055-stabilisation-couche-1", 0)])
    resultat = banc.jouer(
        "palier.sh", DEPOT="o/r", LIGNE="palier 055 055-stabilisation-couche-1 couche=1",
        COMPTE_RENDU="palier.log",
    )

    assert resultat.returncode == 0, resultat.stderr
    assert banc.appel("git push origin --delete feuille/055-stabilisation-couche-1") is not None
    assert banc.appel("gh pr create") is not None


def test_rien_a_deposer_ne_touche_ni_git_ni_github(banc):
    _projet(banc)
    banc.poser("git")
    banc.poser("gh")
    resultat = banc.jouer("palier.sh", DEPOT="o/r", LIGNE="RIEN")

    assert resultat.returncode == 0
    assert banc.appels == []
