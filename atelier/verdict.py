"""Un verdict de relecture : une donnée, pas une prose.

Le 3 septembre 2026, le relecteur du lot 046 a rendu un avis en français
qui disait, en substance, que le lot n'était pas recevable. La carte a
avancé quand même : la seule porte entre « l'agent s'est arrêté » et la
boîte suivante était son code de sortie. Une relecture terminée valait
approbation.

Un avis en prose n'est pas lisible par une machine, et le rendre lisible
en le devinant serait pire : un « attention » compté pour un refus, un
« je note que » compté pour un accord. La machine ne lit pas la prose —
c'est déjà la règle pour la feuille de route.

Un verdict est donc un fichier, avec un format, et le format refuse tout
ce qu'il ne comprend pas :

    {
      "objet":   "diff",
      "lot":     "046-la-mer-est-un-port-commun",
      "pr":      206,
      "sha":     "e5589e3…",            40 hexadécimaux
      "auteur":  "codex",
      "verdict": "FAIL",
      "motifs":  ["SC3 n'est pas mesurée : le contrôle nomme sa référence"]
    }

Quatre refus, et ils comptent autant que les deux acceptations :

1. **absent** — le relecteur n'a rien déposé ;
2. **illisible** — pas du JSON, un champ manque, ou un champ inconnu
   s'y trouve ;
3. **périmé** — le `sha` du verdict n'est pas la révision courante :
   l'auteur a repoussé, et le verdict porte sur du code qui n'existe
   plus ;
4. **interdit** — l'`auteur` du verdict est celui qui a écrit le code.

Les trois premiers sont la même réponse pour la machine : *je ne sais
pas*, et un inconnu n'est ni un oui ni un non — la doctrine que
`atelier/echange.py` tient déjà pour la CI et l'état d'une PR. Le
quatrième est un refus dur : c'est la première règle non négociable de
VISION.md, et elle ne s'obtient pas en le demandant poliment dans un
prompt.

Ce module ne lance aucun processus et n'ouvre aucune connexion. Il
reçoit un chemin, un SHA et un nom d'auteur, et il répond. Ce qui parle
à GitHub vit ailleurs.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path
import re

COUCHE = "verification"


PASS = "PASS"
FAIL = "FAIL"
VERDICTS = (PASS, FAIL)

# Ce que rend la lecture quand elle ne sait pas. Ni vert ni rouge :
# une troisième réponse, et c'est elle qui retient.
INCONNU = "inconnu"

BRIEF = "brief"
DIFF = "diff"
OBJETS = (BRIEF, DIFF)

# Les clés, toutes obligatoires. Un verdict incomplet n'est pas un
# verdict partiel : c'est un verdict illisible.
CLES = ("objet", "lot", "pr", "sha", "auteur", "verdict", "motifs")

# Une révision git, écrite en entier. Un préfixe de sept caractères
# désigne un commit pour un humain ; pour une comparaison, il désigne
# une famille de commits.
_SHA = re.compile(r"^[0-9a-f]{40}$")


class VerdictErreur(ValueError):
    """Le fichier ne porte pas un verdict lisible, ou il ne vaut pas ici."""


@dataclass(frozen=True)
class Verdict:
    objet: str
    lot: str
    pr: int
    sha: str
    auteur: str
    verdict: str
    motifs: tuple[str, ...]

    @property
    def passe(self) -> bool:
        return self.verdict == PASS

    def __str__(self) -> str:
        if self.verdict == FAIL:
            return f"{FAIL} ({len(self.motifs)} motif(s)) — {self.motifs[0]}"
        return f"{PASS} sur {self.sha[:12]} par {self.auteur}"


def _exiger_texte(brut: dict, cle: str) -> str:
    valeur = brut[cle]
    if not isinstance(valeur, str) or not valeur.strip():
        raise VerdictErreur(f"« {cle} » doit être un texte non vide (reçu {valeur!r})")
    return valeur.strip()


def lire_texte(texte: str, source: str = "verdict") -> Verdict:
    """Le verdict que ce texte porte. Tout le reste est illisible."""
    try:
        brut = json.loads(texte)
    except json.JSONDecodeError as exc:
        apercu = texte.strip().replace("\n", " ")[:60]
        raise VerdictErreur(
            f"{source} n'est pas du JSON ({exc.msg}) — une prose ne verdit rien "
            f"(reçu « {apercu} »)"
        ) from exc
    if not isinstance(brut, dict):
        raise VerdictErreur(f"{source} n'est pas un objet JSON (reçu {type(brut).__name__})")

    manquantes = [c for c in CLES if c not in brut]
    if manquantes:
        raise VerdictErreur(f"{source} — champs manquants : {', '.join(manquantes)}")
    # Un lecteur qui ignore ce qu'il ne comprend pas accepte demain un
    # champ qui voulait dire non.
    inconnues = sorted(k for k in brut if k not in CLES)
    if inconnues:
        raise VerdictErreur(
            f"{source} — champs inconnus : {', '.join(inconnues)} "
            f"(connus : {', '.join(CLES)})"
        )

    objet = _exiger_texte(brut, "objet")
    if objet not in OBJETS:
        raise VerdictErreur(
            f"{source} — objet inconnu : « {objet} » (connus : {', '.join(OBJETS)})"
        )
    lot = _exiger_texte(brut, "lot")
    auteur = _exiger_texte(brut, "auteur")
    sha = _exiger_texte(brut, "sha")
    if not _SHA.match(sha):
        raise VerdictErreur(
            f"{source} — sha illisible : « {sha} » (attendu 40 hexadécimaux "
            "minuscules ; un préfixe désigne une famille de commits, pas un commit)"
        )
    rendu = _exiger_texte(brut, "verdict")
    if rendu not in VERDICTS:
        raise VerdictErreur(
            f"{source} — verdict inconnu : « {rendu} » (connus : {', '.join(VERDICTS)})"
        )

    pr = brut["pr"]
    # `True` est un entier en Python ; il n'est pas un numéro de PR.
    if isinstance(pr, bool) or not isinstance(pr, int) or pr <= 0:
        raise VerdictErreur(f"{source} — pr doit être un entier positif (reçu {pr!r})")

    motifs_bruts = brut["motifs"]
    if not isinstance(motifs_bruts, list):
        raise VerdictErreur(
            f"{source} — motifs doit être une liste (reçu {type(motifs_bruts).__name__})"
        )
    motifs: list[str] = []
    for i, motif in enumerate(motifs_bruts):
        if not isinstance(motif, str) or not motif.strip():
            raise VerdictErreur(f"{source} — motif {i + 1} vide ou non textuel : {motif!r}")
        motifs.append(motif.strip())
    if rendu == FAIL and not motifs:
        # Un refus qui ne dit pas ce qu'il refuse ne peut pas revenir à
        # son auteur : il ne lui apprendrait rien.
        raise VerdictErreur(f"{source} — un {FAIL} sans motif ne dit pas ce qu'il refuse")

    return Verdict(
        objet=objet, lot=lot, pr=pr, sha=sha, auteur=auteur,
        verdict=rendu, motifs=tuple(motifs),
    )


def lire(chemin: Path) -> Verdict:
    """Le verdict déposé là. Absent est un refus, pas un vide."""
    chemin = Path(chemin)
    if not chemin.is_file():
        raise VerdictErreur(
            f"aucun verdict déposé : {chemin} est absent — une relecture "
            "terminée n'est pas une approbation"
        )
    try:
        texte = chemin.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as exc:
        raise VerdictErreur(f"{chemin} illisible : {exc}") from exc
    if not texte.strip():
        raise VerdictErreur(f"{chemin} est vide — un échantillon vide échoue")
    return lire_texte(texte, chemin.as_posix())


def valider(verdict: Verdict, *, sha: str, auteur_code: str) -> None:
    """Ce verdict vaut-il pour cette révision, rendu par qui ?

    Deux refus, et ils ne se négocient pas dans un prompt : le verdict
    porte sur autre chose que ce qu'on s'apprête à intégrer, ou c'est
    l'auteur du code qui l'a signé.
    """
    attendu = (sha or "").strip()
    if not _SHA.match(attendu):
        raise VerdictErreur(
            f"la révision à comparer est illisible : « {attendu} » "
            "(attendu 40 hexadécimaux minuscules)"
        )
    if verdict.sha != attendu:
        raise VerdictErreur(
            f"verdict périmé : il porte sur {verdict.sha[:12]}, la révision "
            f"courante est {attendu[:12]} — le code relu n'existe plus"
        )
    signataire = (auteur_code or "").strip()
    if not signataire:
        raise VerdictErreur(
            "l'auteur du code n'est pas nommé : l'atelier ne devine pas qui a "
            "écrit ce qu'on relit"
        )
    if verdict.auteur == signataire:
        raise VerdictErreur(
            f"verdict interdit : {verdict.auteur} a écrit ce code et le signe — "
            "celui qui a écrit le code ne dit pas s'il est recevable"
        )


def lire_et_valider(chemin: Path, *, sha: str, auteur_code: str) -> Verdict:
    """Le verdict, s'il est lisible, frais, et signé par quelqu'un d'autre."""
    verdict = lire(chemin)
    valider(verdict, sha=sha, auteur_code=auteur_code)
    return verdict
