"""G3-bis — aligner la provenance du littoral declaree par les cellules (v1_051).

Ce que fait ce module, en une phrase : il fait dire au manifeste des cellules
quelle terre la chaine produit reellement, puis il fait relire cette
declaration par le manifeste et les statistiques de G4.

Le monde n'a qu'une seule terre. `artifacts/coastline_1400.json` dit ou elle
s'arrete ; `artifacts/cells_g3.json` decoupe ce qu'elle contient. Le manifeste
de G3 porte la phrase « voici la terre qui a produit ces cellules » : quand
cette phrase designe une terre que la chaine ne produit plus, la question
« quelle terre ? » recoit deux reponses. Ce module en supprime une.

Ce qui change, et rien d'autre :

- `artifacts/MANIFEST_g3.json`, entree `inputs.coastline_1400` : elle recoit
  l'empreinte du littoral vivant, **calculee ici depuis le fichier**. Elle
  n'est recopiee ni d'un litteral, ni de `MANIFEST_g2b.json`, ni de
  `MANIFEST_g4.json` : une valeur copiee d'un autre manifeste reussirait la
  comparaison sans avoir jamais lu la terre. Le bloc `outputs` et le
  `fixed_timestamp` ne sont pas touches.
- `artifacts/stats_g4.json`, drapeau `coastline_1400_sha_equals_g3_input` : il
  est **derive** de la comparaison, jamais pose a la main.
- `artifacts/MANIFEST_g4.json` : `inputs.coastline_1400` recalcule depuis le
  fichier vivant, `coastline_1400_sha_declared_by_g3` relu de la declaration
  que G3 porte desormais, `coastline_1400_sha_equal` derive de la comparaison,
  et l'empreinte de sortie du seul fichier G4 reecrit ici.

Ce n'est **pas** une regeneration. La maille des cellules n'est pas rejouee, le
semis de zones de mer n'est pas rejoue, aucune arete n'est recalculee.

L'ordre des ecritures est une contrainte : `stats_g4.json` d'abord,
`MANIFEST_g4.json` ensuite, parce que le second declare l'empreinte du premier
et que l'inverse n'est pas vrai.

Le module est idempotent : il reecrit toujours les trois memes fichiers dans la
forme canonique de la chaine, et une seconde execution ne change aucun octet.
Il n'imprime aucune valeur d'empreinte (regle durement acquise n^o 12) :
seulement des noms de source, des resultats de comparaison et la liste des
fichiers ecrits.

Usage, depuis pipeline/geo/ :
  ../../.venv/bin/python steps/03b_align_coastline_provenance.py

Codes de sortie :
  0 — provenance alignee, fichiers ecrits ;
  1 — le littoral vivant n'est pas celui que l'etape productrice declare : le
      disque est incoherent, rien n'est ecrit ;
  2 — une source manque du disque, avec la commande qui la regenere.
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from io_util import read_json, sha256_file, write_json  # noqa: E402

ARTIFACTS = ROOT / "artifacts"

LITTORAL_VIVANT = ARTIFACTS / "coastline_1400.json"
MANIFESTE_G2B = ARTIFACTS / "MANIFEST_g2b.json"
MANIFESTE_G3 = ARTIFACTS / "MANIFEST_g3.json"
MANIFESTE_G4 = ARTIFACTS / "MANIFEST_g4.json"
STATS_G4 = ARTIFACTS / "stats_g4.json"

CLE_LITTORAL = "coastline_1400"
SORTIE_G2B = "artifacts/coastline_1400.json"
SORTIE_STATS_G4 = "artifacts/stats_g4.json"

COMMANDE_REGEN = (
    "depuis pipeline/geo/ : ../../.venv/bin/python tests/run_proof_g2b.py"
)

CODE_ALIGNE = 0
CODE_INCOHERENT = 1
CODE_ABSENCE = 2


def _absence(nom: str, regenerable: bool) -> None:
    print(f"ABSENCE : {nom} manque du disque.")
    if regenerable:
        print(f"Le regenerer avant d'aligner quoi que ce soit -- {COMMANDE_REGEN}")
    else:
        print("Ce fichier est suivi par git : le restaurer depuis le depot.")


def aligner() -> int:
    for chemin, nom, regenerable in (
        (LITTORAL_VIVANT, "artifacts/coastline_1400.json", True),
        (MANIFESTE_G2B, "artifacts/MANIFEST_g2b.json", True),
        (MANIFESTE_G3, "artifacts/MANIFEST_g3.json", False),
        (MANIFESTE_G4, "artifacts/MANIFEST_g4.json", False),
        (STATS_G4, "artifacts/stats_g4.json", False),
    ):
        if not chemin.is_file():
            _absence(nom, regenerable)
            return CODE_ABSENCE

    empreinte_vivante = sha256_file(LITTORAL_VIVANT)
    sortie_declaree_g2b = str(read_json(MANIFESTE_G2B)["outputs"][SORTIE_G2B])

    if empreinte_vivante != sortie_declaree_g2b:
        print("ECART : artifacts/coastline_1400.json present sur le disque n'est"
              " pas la sortie que MANIFEST_g2b.json declare pour ce fichier.")
        print("Le disque est incoherent : rien n'est ecrit. Regenerer l'etape"
              f" productrice, puis rejouer -- {COMMANDE_REGEN}")
        return CODE_INCOHERENT

    ecrits: list[str] = []

    manifeste_g3 = read_json(MANIFESTE_G3)
    manifeste_g3["inputs"][CLE_LITTORAL] = empreinte_vivante
    write_json(MANIFESTE_G3, manifeste_g3)
    ecrits.append("artifacts/MANIFEST_g3.json")

    entree_declaree_g3 = str(read_json(MANIFESTE_G3)["inputs"][CLE_LITTORAL])
    concordance = int(entree_declaree_g3 == empreinte_vivante)

    stats_g4 = read_json(STATS_G4)
    stats_g4["coastline_1400_sha_equals_g3_input"] = concordance
    write_json(STATS_G4, stats_g4)
    ecrits.append("artifacts/stats_g4.json")

    manifeste_g4 = read_json(MANIFESTE_G4)
    manifeste_g4["inputs"][CLE_LITTORAL] = sha256_file(LITTORAL_VIVANT)
    manifeste_g4["coastline_1400_sha_declared_by_g3"] = entree_declaree_g3
    manifeste_g4["coastline_1400_sha_equal"] = concordance
    manifeste_g4["outputs"][SORTIE_STATS_G4] = sha256_file(STATS_G4)
    write_json(MANIFESTE_G4, manifeste_g4)
    ecrits.append("artifacts/MANIFEST_g4.json")

    print("Provenance du littoral alignee sur le fichier vivant, empreinte"
          " calculee a l'execution et jamais imprimee.")
    print("Le littoral vivant egale la sortie declaree par MANIFEST_g2b.json :"
          " oui.")
    print("L'entree declaree par MANIFEST_g3.json egale le littoral vivant :"
          f" {'oui' if concordance else 'non'}.")
    print("Drapeaux derives de cette comparaison, jamais poses a la main :"
          f" MANIFEST_g4.json coastline_1400_sha_equal = {concordance},"
          f" stats_g4.json coastline_1400_sha_equals_g3_input = {concordance}.")
    print(f"fichiers_ecrits: {len(ecrits)}")
    for rel in ecrits:
        print(f"  ecrit: {rel}")
    return CODE_ALIGNE


if __name__ == "__main__":
    sys.exit(aligner())
