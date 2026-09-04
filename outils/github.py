"""Lire GitHub, et rien d'autre.

Ce module **lit**. Il n'approuve pas, ne fusionne pas, ne pousse rien :
les gestes qui écrivent vivent dans les workflows, où ils se voient dans
un journal. C'est la même séparation que dans le jeu — la vue lit, elle
ne décide jamais — appliquée à l'infrastructure.

Bibliothèque standard seule, comme le moteur.
"""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.github.com"


class GithubErreur(RuntimeError):
    pass


class Github:
    def __init__(self, depot: str, jeton: str | None = None, api: str = API) -> None:
        if "/" not in depot:
            raise GithubErreur(f"dépôt attendu sous la forme « proprietaire/nom », reçu « {depot} »")
        self.depot = depot
        self.api = api.rstrip("/")
        self.jeton = jeton if jeton is not None else os.environ.get("GITHUB_TOKEN", "")
        if not self.jeton:
            raise GithubErreur(
                "aucun jeton : poser GITHUB_TOKEN. Sans lui l'API répond 403 sur "
                "les PR, et un 403 lu comme « rien à faire » arrêterait la boucle en silence"
            )

    def _get(self, chemin: str, **params) -> tuple[object, dict[str, str]]:
        url = f"{self.api}/repos/{self.depot}/{chemin.lstrip('/')}"
        if params:
            url += "?" + urllib.parse.urlencode(params)
        requete = urllib.request.Request(
            url,
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self.jeton}",
                "X-GitHub-Api-Version": "2022-11-28",
                "User-Agent": "forgehistory-outils",
            },
        )
        try:
            with urllib.request.urlopen(requete, timeout=30) as reponse:
                return json.loads(reponse.read().decode("utf-8")), dict(reponse.headers)
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")[:400]
            raise GithubErreur(f"{exc.code} sur {url} : {detail}") from exc
        except urllib.error.URLError as exc:
            raise GithubErreur(f"GitHub injoignable ({url}) : {exc.reason}") from exc

    def get(self, chemin: str, **params):
        return self._get(chemin, **params)[0]

    def liste(self, chemin: str, **params) -> list:
        """Toutes les pages d'une collection. Une page oubliée ment par omission."""
        resultat: list = []
        page = 1
        while True:
            lot, entetes = self._get(chemin, per_page=100, page=page, **params)
            if not isinstance(lot, list):
                raise GithubErreur(f"{chemin} ne rend pas une liste")
            resultat.extend(lot)
            if len(lot) < 100 or 'rel="next"' not in entetes.get("Link", ""):
                return resultat
            page += 1


def controles(gh: Github, sha: str) -> list[tuple[str, str, str | None]]:
    """Les contrôles posés sur une révision : (nom, statut, conclusion).

    Les `check-runs` des workflows et les `statuses` d'un outil externe
    sont deux supports du même fait ; en lire un seul laisserait un
    contrôle requis introuvable, donc absent, donc bloquant sans raison.
    """
    trouves: list[tuple[str, str, str | None]] = []
    reponse = gh.get(f"commits/{sha}/check-runs", per_page=100)
    for run in reponse.get("check_runs", []):
        trouves.append((run["name"], run.get("status", ""), run.get("conclusion")))
    etat = gh.get(f"commits/{sha}/status", per_page=100)
    for statut in etat.get("statuses", []):
        brut = statut.get("state", "")
        trouves.append(
            (statut["context"], "completed" if brut != "pending" else "in_progress",
             "success" if brut == "success" else brut)
        )
    return trouves


def auteurs_du_code(gh: Github, numero: int) -> list[str]:
    """Qui a écrit les commits d'une PR — connexions GitHub, pas noms déclarés."""
    logins: list[str] = []
    for commit in gh.liste(f"pulls/{numero}/commits"):
        for cle in ("author", "committer"):
            qui = commit.get(cle) or {}
            login = qui.get("login")
            if login and login not in logins:
                logins.append(login)
    return logins


def revues(gh: Github, numero: int) -> list[dict]:
    return gh.liste(f"pulls/{numero}/reviews")


def retard(gh: Github, base: str, tete: str) -> int:
    """Combien de commits de `base` manquent à `tete`."""
    comparaison = gh.get(f"compare/{urllib.parse.quote(base)}...{urllib.parse.quote(tete)}")
    return int(comparaison.get("behind_by", 0))
