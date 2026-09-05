"""La couture avec GitHub : les noms de champs, et ce qu'un blanc veut dire.

Les décisions sont éprouvées ailleurs. Ici on éprouve ce qui les
alimente — l'endroit où une clé mal orthographiée rend un brouillon
éveillé ou une PR conflictuelle fusionnable, sans que rien ne rougisse.
"""

from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import threading
from urllib.parse import parse_qs, urlparse

import pytest

from outils import integration, relecture


@contextmanager
def _api(routes):
    """Un GitHub de banc : `chemin` ou `(chemin, page)` → (statut, corps, en-têtes)."""

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args):
            return

        def do_GET(self):
            parsed = urlparse(self.path)
            page = parse_qs(parsed.query).get("page", ["1"])[0]
            chemin = parsed.path.lstrip("/")
            cle = (chemin, page) if (chemin, page) in routes else chemin
            if cle not in routes:
                self.send_response(404)
                corps = b'{"message":"absent"}'
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(corps)))
                self.end_headers()
                self.wfile.write(corps)
                return
            statut, corps, extra = routes[cle]
            payload = corps if isinstance(corps, bytes) else json.dumps(corps).encode("utf-8")
            self.send_response(statut)
            self.send_header("Content-Type", "application/json")
            for nom, valeur in extra.items():
                self.send_header(nom, valeur)
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

    serveur = HTTPServer(("127.0.0.1", 0), Handler)
    fil = threading.Thread(target=serveur.serve_forever, daemon=True)
    fil.start()
    host, port = serveur.server_address[:2]
    try:
        yield f"http://{host}:{port}"
    finally:
        serveur.shutdown()
        serveur.server_close()


def test_une_pr_ecartee_n_est_lue_qu_a_moitie():
    """Sans détail, la fusionnabilité est inconnue — pas vraie."""
    pr = integration.depuis_github(
        {"number": 215, "head": {"ref": "cursor/essai"}, "draft": True}
    )
    assert pr.numero == 215
    assert pr.branche == "cursor/essai"
    assert pr.brouillon is True
    assert pr.fusionnable is None
    assert pr.controles == ()


def test_un_brouillon_absent_du_json_n_est_pas_un_brouillon():
    pr = integration.depuis_github({"number": 1, "head": {"ref": "agent/049-x"}})
    assert pr.brouillon is False


def test_les_controles_arrivent_avec_leur_etat_traduit():
    pr = integration.depuis_github(
        {"number": 216, "head": {"ref": "agent/049-x"}},
        {"mergeable": True, "head": {"sha": "a" * 40}},
        [("sim", "completed", "success"), ("viewer", "in_progress", None),
         ("gitleaks", "completed", "failure")],
        retard=2,
    )
    par_nom = {c.nom: c.etat for c in pr.controles}
    assert par_nom == {
        "sim": integration.VERT,
        "viewer": integration.EN_COURS,
        "gitleaks": integration.ROUGE,
    }
    assert pr.retard == 2
    assert pr.fusionnable is True


def test_une_fusionnabilite_que_github_n_a_pas_calculee_reste_inconnue():
    pr = integration.depuis_github(
        {"number": 216, "head": {"ref": "agent/049-x"}},
        {"head": {"sha": "a" * 40}},
    )
    assert pr.fusionnable is None


def test_une_approbation_que_personne_ne_signe_ne_compte_pas():
    """GitHub rend `user: null` pour un compte supprimé. Un auteur vide
    n'est pas un tiers : il n'est personne."""
    revues = relecture.revues_depuis_github(
        [{"user": None, "state": "APPROVED", "commit_id": "a" * 40}]
    )
    assert revues[0].auteur == ""
    assert not relecture.juger("a" * 40, ["cursor[bot]"], revues).passe


def test_une_revue_sans_revision_est_lue_comme_vide():
    revues = relecture.revues_depuis_github(
        [{"user": {"login": "claude[bot]"}, "state": "APPROVED", "commit_id": None}]
    )
    assert revues[0].revision == ""
    assert not relecture.juger("a" * 40, ["cursor[bot]"], revues).passe


def _brancher(dossier, corps: str):
    (dossier / "atelier.toml").write_text(
        '[projet]\nnom = "Essai"\nfeuille = "ROADMAP.md"\nbranche_base = "master"\n' + corps,
        encoding="utf-8",
    )
    return dossier


def test_le_branchement_rend_ce_que_le_projet_declare(tmp_path):
    from outils import registre

    _brancher(tmp_path, "")
    assert registre.branchement(tmp_path)["feuille"] == "ROADMAP.md"
    assert registre.branchement(tmp_path)["base"] == "master"


def test_sans_section_integration_on_refuse_au_lieu_de_deviner(tmp_path):
    from outils import registre

    _brancher(tmp_path, "")
    with pytest.raises(registre.BranchementIncomplet) as refus:
        registre.integration(tmp_path)
    assert "[integration]" in str(refus.value)


def test_une_liste_de_controles_vide_est_un_branchement_incomplet(tmp_path):
    """Une liste vide ferait entrer n'importe quoi. Elle se refuse à la
    lecture, avant même que la décision ait à s'en méfier."""
    from outils import registre

    _brancher(tmp_path, '\n[integration]\ncontroles = []\nbranches = ["agent/"]\n')
    with pytest.raises(registre.BranchementIncomplet) as refus:
        registre.integration(tmp_path)
    assert "controles" in str(refus.value)


def test_les_controles_tardifs_sont_facultatifs(tmp_path):
    from outils import registre

    _brancher(tmp_path, '\n[integration]\ncontroles = ["sim"]\nbranches = ["agent/"]\n')
    assert registre.integration(tmp_path)["apres_rejeu"] == ()


def test_un_atelier_toml_absent_se_dit(tmp_path):
    from outils import registre

    with pytest.raises(registre.BranchementIncomplet):
        registre.branchement(tmp_path)


def test_une_feuille_non_nommee_se_refuse(tmp_path):
    """Sans [projet].feuille, on ne cherche pas le registre au hasard."""
    from outils import registre

    (tmp_path / "atelier.toml").write_text('[projet]\nnom = "Essai"\n', encoding="utf-8")
    with pytest.raises(registre.BranchementIncomplet) as refus:
        registre.feuille(tmp_path)
    assert "feuille" in str(refus.value)


def test_un_depot_sans_slash_se_refuse():
    from outils import github

    with pytest.raises(github.GithubErreur) as refus:
        github.Github("pas-un-depot", jeton="x")
    assert "proprietaire/nom" in str(refus.value)


def test_sans_jeton_on_refuse_plutot_que_de_lire_un_403_comme_rien(monkeypatch):
    """Un 403 lu comme « rien à faire » arrêterait la boucle en silence."""
    from outils import github

    monkeypatch.delenv("GITHUB_TOKEN", raising=False)
    with pytest.raises(github.GithubErreur) as refus:
        github.Github("O/R", jeton=None)
    assert "jeton" in str(refus.value)


def test_un_403_n_est_pas_une_liste_vide():
    from outils import github

    with _api({"repos/O/R/pulls": (403, {"message": "Forbidden"}, {})}) as api:
        gh = github.Github("O/R", jeton="x", api=api)
        with pytest.raises(github.GithubErreur) as refus:
            gh.liste("pulls")
    assert "403" in str(refus.value)


def test_une_page_oubliee_ne_fait_pas_une_liste_complete():
    """`liste` enchaîne les pages ; s'arrêter à 100 ment par omission."""
    from outils import github

    premiere = [{"number": i} for i in range(100)]
    suite = [{"number": 100}]
    routes = {
        ("repos/O/R/pulls", "1"): (
            200,
            premiere,
            {"Link": '<http://x?page=2>; rel="next"'},
        ),
        ("repos/O/R/pulls", "2"): (200, suite, {}),
    }
    with _api(routes) as api:
        gh = github.Github("O/R", jeton="x", api=api)
        obtenu = gh.liste("pulls")
    assert [p["number"] for p in obtenu] == list(range(101))


def test_une_collection_qui_n_est_pas_une_liste_se_refuse():
    from outils import github

    with _api({"repos/O/R/pulls": (200, {"message": "pas une liste"}, {})}) as api:
        gh = github.Github("O/R", jeton="x", api=api)
        with pytest.raises(github.GithubErreur) as refus:
            gh.liste("pulls")
    assert "liste" in str(refus.value)


def test_les_controles_lisent_check_runs_et_statuts():
    """En lire un seul laisserait un contrôle requis introuvable, donc bloquant."""
    from outils import github

    class Faux:
        def get(self, chemin, **_):
            if "check-runs" in chemin:
                return {
                    "check_runs": [
                        {"name": "sim", "status": "completed", "conclusion": "success"}
                    ]
                }
            if chemin.endswith("/status") or "/status?" in chemin or chemin.endswith("status"):
                return {"statuses": [{"context": "viewer", "state": "pending"}]}
            raise AssertionError(chemin)

    trouves = github.controles(Faux(), "a" * 40)
    assert ("sim", "completed", "success") in trouves
    assert ("viewer", "in_progress", "pending") in trouves


def test_les_auteurs_dedoublonnent_author_et_committer():
    from outils import github

    class Faux:
        def liste(self, _chemin):
            return [
                {"author": {"login": "alice"}, "committer": {"login": "alice"}},
                {"author": {"login": "bob"}, "committer": {"login": "web-flow"}},
                {"author": None, "committer": {"login": "bob"}},
            ]

    assert github.auteurs_du_code(Faux(), 1) == ["alice", "bob", "web-flow"]


def test_le_retard_lit_behind_by():
    from outils import github

    class Faux:
        def get(self, _chemin, **_):
            return {"behind_by": 3}

    assert github.retard(Faux(), "master", "abc") == 3


def test_un_brouillon_n_appelle_pas_le_detail():
    """Hors préfixe ou brouillon : on n'invente pas une fusionnabilité, on n'appelle pas."""
    from outils.__main__ import _pr_integrable

    class Faux:
        def __init__(self):
            self.appels = []

        def get(self, chemin, **_):
            self.appels.append(chemin)
            return {"head": {"sha": "a" * 40}, "mergeable": True}

    faux = Faux()
    brouillon = _pr_integrable(
        faux, {"number": 1, "head": {"ref": "agent/049-x"}, "draft": True},
        "master", ("agent/",),
    )
    assert brouillon.brouillon is True
    assert brouillon.fusionnable is None
    assert faux.appels == []

    hors = _pr_integrable(
        faux, {"number": 2, "head": {"ref": "cursor/essai"}},
        "master", ("agent/",),
    )
    assert hors.branche == "cursor/essai"
    assert hors.fusionnable is None
    assert faux.appels == []


def test_cli_depot_mal_forme_sort_en_echec():
    import subprocess
    import sys

    proc = subprocess.run(
        [
            sys.executable, "-m", "outils", "relecture",
            "--depot", "pas-un-depot", "--pr", "1", "--jeton", "x",
        ],
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 1
    assert "FAIL" in proc.stderr
    assert proc.stdout == ""
