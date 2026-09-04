"""Canal d'échange : git-invisible, lisible par l'agent.

Les deux conditions, ou aucune. Un dossier dans `.gitignore` du dépôt
et filtré par l'agent (`.cursorignore`) a déjà rendu un bundle de
revue illisible (lot 033 de ForgeHistory).
"""

from __future__ import annotations

from dataclasses import dataclass, field
import hashlib
import json
import os
import re
import shutil
import subprocess
from pathlib import Path


NOM_DOSSIER = "atelier-echange"
FICHIER_PR = "pr.txt"
# Un entier positif, et rien d'autre. On ne concatène pas les chiffres
# d'un texte quelconque : « PR #123 » n'est pas un numéro.
_ENTIER_POSITIF = re.compile(r"^[1-9][0-9]*$")


class EchangeErreur(ValueError):
    pass


def dossier(racine: Path) -> Path:
    return Path(racine) / NOM_DOSSIER


def ouvrir(racine: Path) -> Path:
    cible = dossier(racine)
    cible.mkdir(parents=True, exist_ok=True)
    garde = cible / ".gitignore"
    if not garde.is_file():
        # Un `.gitignore` contenant `*` s'ignore lui-même : le canal
        # ne dépend pas d'une ligne du `.gitignore` du dépôt produit.
        garde.write_text("*\n", encoding="utf-8")
    return cible


def deposer_texte(racine: Path, nom: str, corps: str) -> Path:
    if not corps.strip():
        raise EchangeErreur(f"{nom} est vide")
    cible = ouvrir(racine) / nom
    cible.write_text(corps, encoding="utf-8")
    attendu = hashlib.sha256(corps.encode("utf-8")).hexdigest()
    obtenu = hashlib.sha256(cible.read_text(encoding="utf-8").encode("utf-8")).hexdigest()
    if attendu != obtenu:
        raise EchangeErreur(
            f"copie corrompue pour {nom} : {attendu[:12]} attendu, {obtenu[:12]} relu"
        )
    return cible


def deposer(racine: Path, source: Path, nom: str) -> Path:
    if not source.is_file():
        raise EchangeErreur(f"{nom} introuvable : {source}")
    return deposer_texte(racine, nom, source.read_text(encoding="utf-8"))


def retirer(racine: Path, nom: str) -> None:
    cible = dossier(racine) / nom
    if cible.is_file():
        cible.unlink()


def git_ignore_le_canal(racine: Path) -> bool:
    """Le canal a sa propre garde `*`. Il ne s'appuie pas sur le dépôt."""
    garde = dossier(racine) / ".gitignore"
    return garde.is_file() and "*" in garde.read_text(encoding="utf-8")


def chemin_pr(racine: Path) -> Path:
    return dossier(racine) / FICHIER_PR


def lire_numero_pr(fichier: Path) -> int:
    """Le fichier, après trim, ne porte qu'un entier positif.

    Absent, vide, « PR #123 », « 0 », plusieurs lignes : refus.
    On n'extrait pas les chiffres d'un texte libre.
    """
    cible = Path(fichier)
    if not cible.is_file():
        raise EchangeErreur(f"{FICHIER_PR} est absent : pas de numéro de PR")
    texte = cible.read_text(encoding="utf-8").strip()
    if not texte:
        raise EchangeErreur(f"{FICHIER_PR} est vide : pas de numéro de PR")
    if not _ENTIER_POSITIF.fullmatch(texte):
        apercu = texte.replace("\n", "\\n")
        if len(apercu) > 80:
            apercu = apercu[:77] + "..."
        raise EchangeErreur(
            f"{FICHIER_PR} ne porte pas un entier positif unique (reçu {apercu!r})"
        )
    return int(texte)


def verifier_pr_branche_optionnel(numero: int, branche: str, racine: Path) -> str | None:
    """Si gh répond, la PR doit être sur `branche`. Sinon on se tait.

    La sonde parle à GitHub : elle exige un remote, un binaire `gh`, et
    souvent une authentification. Un échec de sonde n'est pas un défaut
    du lot — on ne bloque pas le tour. Un désaccord *confirmé* l'est.
    Retourne None si la sonde n'a pas tranché, la branche distante si
    elle correspond, et lève si elle diffère.
    """
    if shutil.which("gh") is None:
        return None
    origine = subprocess.run(
        ["git", "remote", "get-url", "origin"],
        cwd=racine, text=True, capture_output=True, check=False,
    )
    if origine.returncode != 0 or "github.com" not in origine.stdout:
        return None
    try:
        vue = subprocess.run(
            ["gh", "pr", "view", str(numero), "--json", "headRefName"],
            cwd=racine, text=True, capture_output=True, check=False, timeout=15,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None
    if vue.returncode != 0 or not vue.stdout.strip():
        return None
    try:
        nom = json.loads(vue.stdout).get("headRefName")
    except json.JSONDecodeError:
        return None
    if not isinstance(nom, str) or not nom:
        return None
    if nom != branche:
        raise EchangeErreur(
            f"la PR {numero} est sur la branche {nom}, pas {branche}"
        )
    return nom


# --------------------------------------------------- ce que GitHub publie
#
# Deux sondes, une doctrine. `atelier/quota.py` : « un inconnu vaut -1,
# jamais 0 ». Ici l'inconnu n'est ni vert ni rouge, ni ouverte ni fermée
# — c'est une troisième réponse, et c'est elle qui retient. Une garde qui
# s'ouvrirait quand la sonde se tait cède exactement quand elle ne répond
# plus.
#
# La commande est nommée par l'environnement, pas recopiée : sans cette
# couture, aucun test ne s'écrit sans compte GitHub.

VERT = "vert"
ROUGE = "rouge"
INCONNU = "inconnue"

OUVERTE = "ouverte"
FUSIONNEE = "fusionnee"
FERMEE = "fermee"

# Ce que `gh pr checks` écrit dans sa deuxième colonne. `skipping` et
# `cancel` ne sont pas des échecs : GitHub ne les compte pas non plus
# pour son bouton de fusion.
_ETAT_ROUGE = frozenset({"fail"})
_ETAT_ATTENTE = frozenset({"pending"})


@dataclass(frozen=True)
class Verdict:
    """Vert, rouge ou inconnu — et, si rouge, qui l'a rendu rouge."""

    etat: str
    fautifs: tuple[str, ...] = field(default=())
    raison: str = ""

    @property
    def vert(self) -> bool:
        return self.etat == VERT

    @property
    def connu(self) -> bool:
        return self.etat != INCONNU

    def __str__(self) -> str:
        if self.etat == ROUGE:
            return "rouge : " + ", ".join(self.fautifs)
        if self.etat == INCONNU:
            return f"inconnue ({self.raison})"
        return VERT


def _commande(variable: str) -> list[str] | None:
    """La commande de la sonde, ou None si elle est hors de portée."""
    brut = os.environ.get(variable, "").strip()
    if brut:
        argv = brut.split()
    else:
        argv = ["gh"]
    if shutil.which(argv[0]) is None and not Path(argv[0]).is_file():
        return None
    return argv


def _sonder(argv: list[str], racine: Path) -> subprocess.CompletedProcess[str] | None:
    try:
        return subprocess.run(
            argv, cwd=str(racine), text=True, capture_output=True,
            check=False, timeout=30,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None


def verdict_ci(numero: int, racine: Path) -> Verdict:
    """Le verdict des contrôles obligatoires de la PR `numero`.

    C'est la liste qui gouverne le bouton de fusion de GitHub : la porte
    de l'atelier et celle de GitHub disent alors la même chose, et un
    contrôle tiers instable ne bloque pas un lot que GitHub accepterait.

    Le `gh` livré ici n'a pas de `--json` sur `pr checks` : on lit ses
    colonnes, qui sont son interface publique depuis toujours — nom,
    état, durée, adresse. On ne lit que les deux premières.
    """
    argv = _commande("ATELIER_CI_CMD")
    if argv is None:
        return Verdict(INCONNU, raison="aucune commande pour lire les contrôles")
    fin = _sonder([*argv, "pr", "checks", str(numero), "--required"], racine)
    if fin is None:
        return Verdict(INCONNU, raison="la commande n'a pas répondu")
    lignes = [l for l in fin.stdout.splitlines() if l.strip()]
    if not lignes:
        return Verdict(INCONNU, raison="aucun contrôle obligatoire lu")
    fautifs: list[str] = []
    en_cours: list[str] = []
    for ligne in lignes:
        colonnes = ligne.split("\t")
        if len(colonnes) < 2:
            return Verdict(INCONNU, raison=f"ligne illisible : {ligne.strip()[:60]}")
        nom, etat = colonnes[0].strip(), colonnes[1].strip().lower()
        if etat in _ETAT_ROUGE:
            fautifs.append(nom)
        elif etat in _ETAT_ATTENTE:
            en_cours.append(nom)
    if fautifs:
        return Verdict(ROUGE, fautifs=tuple(fautifs))
    if en_cours:
        return Verdict(INCONNU, raison="contrôles en cours : " + ", ".join(en_cours))
    return Verdict(VERT)


def etat_pr(numero: int, racine: Path) -> str:
    """`ouverte`, `fusionnee`, `fermee` — ou `inconnue`, qui retient.

    L'état lu est celui que GitHub publie, pas celui que la feuille
    déclare. Une fiche dit ce que le propriétaire a décidé ; une PR dit
    ce qui existe.
    """
    argv = _commande("ATELIER_PR_CMD")
    if argv is None:
        return INCONNU
    fin = _sonder([*argv, "pr", "view", str(numero), "--json", "state"], racine)
    if fin is None or fin.returncode != 0 or not fin.stdout.strip():
        return INCONNU
    try:
        brut = json.loads(fin.stdout).get("state")
    except (json.JSONDecodeError, AttributeError):
        return INCONNU
    if not isinstance(brut, str):
        return INCONNU
    return {
        "OPEN": OUVERTE,
        "MERGED": FUSIONNEE,
        "CLOSED": FERMEE,
    }.get(brut.strip().upper(), INCONNU)


AUCUNE = "aucune"


def branche_existe(branche: str, racine: Path) -> bool:
    """Ici, et sans réseau. Un lot neuf n'a pas de branche : il ne coûte
    donc aucun appel à la sonde, et le cas ordinaire reste gratuit."""
    for ref in (f"refs/heads/{branche}", f"refs/remotes/origin/{branche}"):
        fin = _sonder(["git", "rev-parse", "--verify", "--quiet", ref], racine)
        if fin is not None and fin.returncode == 0:
            return True
    return False


def pr_ouverte_sur(branche: str, racine: Path) -> tuple[str, int | None]:
    """(`ouverte`, numéro), (`aucune`, None) ou (`inconnue`, None).

    C'est la question que l'atelier ne posait jamais : *ce travail
    existe-t-il déjà quelque part ?* La fiche ne peut pas y répondre —
    la ligne qui la passe à `livre` vit dans la PR non fusionnée.
    """
    argv = _commande("ATELIER_PR_CMD")
    if argv is None:
        return (INCONNU, None)
    fin = _sonder(
        [*argv, "pr", "list", "--head", branche, "--state", "open",
         "--json", "number", "--limit", "1"],
        racine,
    )
    if fin is None or fin.returncode != 0:
        return (INCONNU, None)
    texte = fin.stdout.strip()
    if not texte:
        return (INCONNU, None)
    try:
        trouvees = json.loads(texte)
    except json.JSONDecodeError:
        return (INCONNU, None)
    if not isinstance(trouvees, list):
        return (INCONNU, None)
    if not trouvees:
        return (AUCUNE, None)
    numero = trouvees[0].get("number") if isinstance(trouvees[0], dict) else None
    if not isinstance(numero, int):
        return (INCONNU, None)
    return (OUVERTE, numero)
