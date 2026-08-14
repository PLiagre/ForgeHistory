"""Commande d'ecart du lot 020 : la provenance du littoral corrige de 1400.

Le script repond a une seule question, en lecture seule : le littoral que la
chaine produit aujourd'hui est-il celui que les cellules declarent avoir
consomme ?

Trois sources sont lues, et aucune n'est ecrite :

- `pipeline/geo/artifacts/coastline_1400.json`, le littoral vivant, dont
  l'empreinte est calculee ici a l'execution ;
- `pipeline/geo/artifacts/MANIFEST_g3.json`, entree `inputs.coastline_1400` ;
- `pipeline/geo/artifacts/MANIFEST_g2b.json`, sortie declaree pour ce meme
  fichier, c'est-a-dire l'etape qui le produit.

Le script n'imprime que des noms de source et des resultats de comparaison :
aucune valeur d'empreinte n'est ecrite, ni en sortie, ni dans ce fichier
(regles durement acquises n^o 9 et n^o 12 tenues ensemble).

Codes de sortie :

- 0 : le vivant egale l'entree declaree par G3 ;
- 1 : ecart mesure contre G3, suivi du resultat de la comparaison au
      producteur G2-bis ;
- 2 : une source manque du disque, avec la commande qui la regenere -- jamais
      confondu avec un ecart mesure (regle n^o 10).
"""

import hashlib
import json
import pathlib
import sys

BRIEF_DIR = pathlib.Path(__file__).resolve().parents[1]
REPO = BRIEF_DIR.parents[3]
ARTIFACTS = REPO / "pipeline" / "geo" / "artifacts"

LITTORAL_VIVANT = ARTIFACTS / "coastline_1400.json"
MANIFESTE_G3 = ARTIFACTS / "MANIFEST_g3.json"
MANIFESTE_G2B = ARTIFACTS / "MANIFEST_g2b.json"

SOURCE_VIVANT = "artifacts/coastline_1400.json (empreinte calculee a l'execution)"
SOURCE_G3 = "MANIFEST_g3.json inputs.coastline_1400"
SOURCE_G2B = "MANIFEST_g2b.json outputs[artifacts/coastline_1400.json]"

COMMANDE_REGEN = (
    "depuis pipeline/geo/ : ../../.venv/bin/python tests/run_proof_g2b.py"
)

CODE_EGALITE = 0
CODE_ECART = 1
CODE_ABSENCE = 2


def empreinte(chemin: pathlib.Path) -> str:
    """Empreinte SHA256 du fichier, calculee ici, jamais imprimee."""
    condense = hashlib.sha256()
    with chemin.open("rb") as flux:
        for bloc in iter(lambda: flux.read(1 << 20), b""):
            condense.update(bloc)
    return condense.hexdigest()


def valeur_declaree(manifeste: pathlib.Path, section: str, cle: str) -> str:
    """Valeur declaree lue dans un manifeste, ni reecrite ni imprimee."""
    contenu = json.loads(manifeste.read_text(encoding="utf-8"))
    return str(contenu[section][cle])


def main() -> int:
    if not LITTORAL_VIVANT.is_file():
        print("ABSENCE : artifacts/coastline_1400.json manque du disque"
              " (il est ignore par git, donc absent d'un clone frais).")
        print(f"Le regenerer avant de conclure quoi que ce soit -- {COMMANDE_REGEN}")
        return CODE_ABSENCE
    if not MANIFESTE_G2B.is_file():
        print("ABSENCE : artifacts/MANIFEST_g2b.json manque du disque"
              " (il est ignore par git, donc absent d'un clone frais).")
        print(f"Le regenerer avant de conclure quoi que ce soit -- {COMMANDE_REGEN}")
        return CODE_ABSENCE
    if not MANIFESTE_G3.is_file():
        print("ABSENCE : artifacts/MANIFEST_g3.json manque du disque ; ce"
              " fichier est suivi par git, le restaurer depuis le depot.")
        return CODE_ABSENCE

    vivant = empreinte(LITTORAL_VIVANT)
    entree_g3 = valeur_declaree(MANIFESTE_G3, "inputs", "coastline_1400")
    sortie_g2b = valeur_declaree(
        MANIFESTE_G2B, "outputs", "artifacts/coastline_1400.json"
    )

    if vivant == entree_g3:
        print(f"EGALITE : {SOURCE_VIVANT} egale l'entree declaree par"
              f" {SOURCE_G3}.")
        return CODE_EGALITE

    print(f"ECART : {SOURCE_VIVANT} et {SOURCE_G3} ne designent pas le meme"
          " fichier.")
    reponse = "oui" if vivant == sortie_g2b else "non"
    print(f"Le meme littoral vivant egale-t-il la sortie declaree par"
          f" {SOURCE_G2B} ? {reponse}.")
    return CODE_ECART


if __name__ == "__main__":
    sys.exit(main())
