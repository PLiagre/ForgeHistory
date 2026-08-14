"""Preuve durable : la provenance du littoral que les cellules declarent (v1_051).

La garde repond a une seule question, et elle la repose a chaque execution : la
terre que le manifeste des cellules declare avoir consommee est-elle celle que
la chaine produit aujourd'hui, et les manifestes qui relisent cette declaration
disent-ils la meme chose ?

Elle est nommee d'apres ce qu'elle **derive** — la provenance du littoral — et
non d'apres le fichier qu'elle surveille (regles durement acquises n^o 2 et
n^o 3).

Ce qu'elle fait, en lecture seule sur les artefacts :

- elle recalcule l'empreinte de `artifacts/coastline_1400.json` a chaque
  execution ; elle ne porte **aucune** valeur attendue en dur, sans quoi elle se
  controlerait elle-meme ;
- elle lit les declarations depuis les manifestes presents sur le disque :
  l'entree `inputs.coastline_1400` de `MANIFEST_g3.json`, la sortie que
  `MANIFEST_g2b.json` declare pour ce fichier, puis les trois champs de
  provenance que G4 relit ;
- les deux drapeaux de G4 ne sont pas compares a une constante ecrite ici : ils
  doivent egaler la comparaison que la garde vient elle-meme de calculer. Un
  drapeau qui affirmerait l'egalite quand la declaration ment est rouge.

Elle n'imprime aucune valeur d'empreinte (regle n^o 12) : seulement des noms de
source et des resultats de comparaison.

Elle ecrit sa sortie verte dans `logs/v1_051_provenance_vert.txt` et un rapport
lisible dans `logs/v1_051_provenance.json`.

Usage, depuis pipeline/geo/ :
  ../../.venv/bin/python tests/run_proof_coastline_provenance.py

Codes de sortie :
  0 — toutes les declarations designent le littoral vivant ;
  1 — ecart mesure, avec un message nommant les sources en desaccord ;
  2 — une source manque du disque, avec la commande qui la regenere ; jamais 1,
      pour qu'une absence ne soit jamais confondue avec un ecart mesure
      (regle n^o 10).
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from io_util import read_json, sha256_file, write_json  # noqa: E402

ARTIFACTS = ROOT / "artifacts"
LOGS = ROOT / "logs"

LITTORAL_VIVANT = ARTIFACTS / "coastline_1400.json"
MANIFESTE_G2B = ARTIFACTS / "MANIFEST_g2b.json"
MANIFESTE_G3 = ARTIFACTS / "MANIFEST_g3.json"
MANIFESTE_G4 = ARTIFACTS / "MANIFEST_g4.json"
STATS_G4 = ARTIFACTS / "stats_g4.json"

RAPPORT_VERT = LOGS / "v1_051_provenance_vert.txt"
RAPPORT_JSON = LOGS / "v1_051_provenance.json"

SOURCE_VIVANT = "artifacts/coastline_1400.json (empreinte calculee a l'execution)"
SOURCE_G3 = "MANIFEST_g3.json inputs.coastline_1400"
SOURCE_G2B = "MANIFEST_g2b.json outputs[artifacts/coastline_1400.json]"
SOURCE_G4_ENTREE = "MANIFEST_g4.json inputs.coastline_1400"
SOURCE_G4_COPIE = "MANIFEST_g4.json coastline_1400_sha_declared_by_g3"
SOURCE_G4_DRAPEAU = "MANIFEST_g4.json coastline_1400_sha_equal"
SOURCE_STATS_G4_DRAPEAU = "stats_g4.json coastline_1400_sha_equals_g3_input"

COMMANDE_REGEN = (
    "depuis pipeline/geo/ : ../../.venv/bin/python tests/run_proof_g2b.py"
)

CODE_VERT = 0
CODE_ECART = 1
CODE_ABSENCE = 2

SOURCES_ATTENDUES = (
    (LITTORAL_VIVANT, "artifacts/coastline_1400.json", True),
    (MANIFESTE_G2B, "artifacts/MANIFEST_g2b.json", True),
    (MANIFESTE_G3, "artifacts/MANIFEST_g3.json", False),
    (MANIFESTE_G4, "artifacts/MANIFEST_g4.json", False),
    (STATS_G4, "artifacts/stats_g4.json", False),
)


def _ecrire_rapport(comparaisons: list[tuple[str, str, bool]], code: int) -> None:
    """Rapport lisible : les sources comparees et le resultat, aucune empreinte."""
    LOGS.mkdir(parents=True, exist_ok=True)
    write_json(
        RAPPORT_JSON,
        {
            "preuve": "provenance du littoral corrige de 1400",
            "pipeline_version": "1.5.1-g3b-v1_051",
            "fixed_timestamp": "1970-01-01T00:00:00Z",
            "comparaisons": [
                {"gauche": gauche, "droite": droite, "concordent": bool(vrai)}
                for gauche, droite, vrai in comparaisons
            ],
            "comparaisons_concordantes": sum(1 for *_, vrai in comparaisons if vrai),
            "comparaisons_totales": len(comparaisons),
            "code_sortie": code,
            "empreintes_citees": 0,
        },
    )


def verifier() -> int:
    lignes: list[str] = []

    def dire(texte: str) -> None:
        lignes.append(texte)
        print(texte)

    for chemin, nom, regenerable in SOURCES_ATTENDUES:
        if not chemin.is_file():
            print(f"ABSENCE : {nom} manque du disque.")
            if regenerable:
                print("Le regenerer avant de conclure quoi que ce soit --"
                      f" {COMMANDE_REGEN}")
            else:
                print("Ce fichier est suivi par git : le restaurer depuis le"
                      " depot.")
            return CODE_ABSENCE

    empreinte_vivante = sha256_file(LITTORAL_VIVANT)
    entree_g3 = str(read_json(MANIFESTE_G3)["inputs"]["coastline_1400"])
    sortie_g2b = str(read_json(MANIFESTE_G2B)["outputs"][
        "artifacts/coastline_1400.json"])
    manifeste_g4 = read_json(MANIFESTE_G4)
    entree_g4 = str(manifeste_g4["inputs"]["coastline_1400"])
    copie_g4 = str(manifeste_g4["coastline_1400_sha_declared_by_g3"])
    drapeau_g4 = manifeste_g4["coastline_1400_sha_equal"]
    drapeau_stats_g4 = read_json(STATS_G4)["coastline_1400_sha_equals_g3_input"]

    concordance = int(entree_g3 == empreinte_vivante)

    comparaisons: list[tuple[str, str, bool]] = [
        (SOURCE_VIVANT, SOURCE_G3, entree_g3 == empreinte_vivante),
        (SOURCE_VIVANT, SOURCE_G2B, sortie_g2b == empreinte_vivante),
        (SOURCE_VIVANT, SOURCE_G4_ENTREE, entree_g4 == empreinte_vivante),
        (SOURCE_G3, SOURCE_G4_COPIE, copie_g4 == entree_g3),
        (
            SOURCE_G4_DRAPEAU,
            "la comparaison recalculee par cette garde",
            drapeau_g4 == concordance,
        ),
        (
            SOURCE_STATS_G4_DRAPEAU,
            "la comparaison recalculee par cette garde",
            drapeau_stats_g4 == concordance,
        ),
    ]

    dire("PREUVE : provenance du littoral corrige de 1400, empreintes calculees"
         " a l'execution et jamais imprimees.")
    for gauche, droite, vrai in comparaisons:
        dire(f"  {'concordent' if vrai else 'EN DESACCORD'} : {gauche} vs"
             f" {droite}")

    concordantes = sum(1 for *_, vrai in comparaisons if vrai)
    dire(f"comparaisons_concordantes: {concordantes} / {len(comparaisons)}")

    if concordantes != len(comparaisons):
        dire("ECART : le monde a deux reponses a la question « quelle terre ? ».")
        dire("Les sources en desaccord sont nommees ci-dessus ; realigner la"
             " declaration d'entree des cellules avant de continuer -- depuis"
             " pipeline/geo/ : ../../.venv/bin/python"
             " steps/03b_align_coastline_provenance.py")
        _ecrire_rapport(comparaisons, CODE_ECART)
        return CODE_ECART

    dire("VERT : la terre declaree par les cellules est la terre que la chaine"
         " produit, et G4 relit la meme declaration.")
    _ecrire_rapport(comparaisons, CODE_VERT)
    LOGS.mkdir(parents=True, exist_ok=True)
    RAPPORT_VERT.write_text("\n".join(lignes) + "\n", encoding="utf-8")
    return CODE_VERT


if __name__ == "__main__":
    sys.exit(verifier())
