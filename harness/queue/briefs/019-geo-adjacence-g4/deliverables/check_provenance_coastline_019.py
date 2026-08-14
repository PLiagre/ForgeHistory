"""Escalade SC7 du brief 019 : provenance du littoral corrige de 1400.

Compare l'empreinte du fichier vivant `pipeline/geo/artifacts/coastline_1400.json`,
calculee a l'execution, a deux valeurs declarees par des manifestes anterieurs au
lot :

- l'entree `inputs.coastline_1400` de `pipeline/geo/artifacts/MANIFEST_g3.json` ;
- la sortie que `pipeline/geo/artifacts/MANIFEST_g2b.json` declare pour ce meme
  fichier, c'est-a-dire l'etape qui le produit.

Le script est en lecture seule et n'imprime que des noms de source et des
resultats de comparaison : aucune valeur d'empreinte n'est ecrite, ni en sortie,
ni dans ce fichier (regles durement acquises n^o 9 et n^o 12 tenues ensemble).

Codes de sortie :

- 0 : le vivant egale l'entree declaree par G3 ;
- 1 : ecart mesure contre G3, suivi du resultat de la comparaison au producteur
      G2-bis (source du compteur `empreinte_terre_g4_egale_sortie_declaree_g2b`) ;
- 2 : une source est absente du disque -- jamais confondu avec un ecart mesure.
"""

import hashlib
import json
import pathlib
import sys

REPO = pathlib.Path(__file__).resolve().parents[5]
ARTIFACTS = REPO / "pipeline" / "geo" / "artifacts"

LIVE = ARTIFACTS / "coastline_1400.json"
MANIFEST_G3 = ARTIFACTS / "MANIFEST_g3.json"
MANIFEST_G2B = ARTIFACTS / "MANIFEST_g2b.json"

NAME_LIVE = "artifacts/coastline_1400.json"
NAME_G3 = "MANIFEST_g3.json inputs.coastline_1400"
NAME_G2B = "MANIFEST_g2b.json outputs[artifacts/coastline_1400.json]"

REGEN_G2B = "depuis pipeline/geo/ : ../../.venv/bin/python tests/run_proof_g2b.py"

ABSENT = 2
ECART = 1
EGALITE = 0


def fingerprint(path: pathlib.Path) -> str:
    """Empreinte SHA256 du fichier, calculee ici et jamais imprimee."""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def declared(manifest: pathlib.Path, section: str, key: str) -> str:
    """Valeur declaree lue dans un manifeste, sans la reecrire ni l'imprimer."""
    return str(json.loads(manifest.read_text(encoding="utf-8"))[section][key])


def main() -> int:
    if not LIVE.is_file():
        print(f"ABSENCE : {NAME_LIVE} n'est pas sur le disque (ignore par git).")
        print(f"Le regenerer avant de conclure quoi que ce soit -- {REGEN_G2B}")
        return ABSENT
    if not MANIFEST_G2B.is_file():
        print("ABSENCE : artifacts/MANIFEST_g2b.json n'est pas sur le disque"
              " (ignore par git).")
        print(f"Le regenerer avant de conclure quoi que ce soit -- {REGEN_G2B}")
        return ABSENT
    if not MANIFEST_G3.is_file():
        print("ABSENCE : artifacts/MANIFEST_g3.json n'est pas sur le disque ;"
              " ce fichier est suivi par git, le restaurer depuis le depot.")
        return ABSENT

    live = fingerprint(LIVE)
    entree_g3 = declared(MANIFEST_G3, "inputs", "coastline_1400")
    sortie_g2b = declared(MANIFEST_G2B, "outputs", "artifacts/coastline_1400.json")

    if live == entree_g3:
        print(f"EGALITE : l'empreinte calculee de {NAME_LIVE} egale l'entree"
              f" declaree par {NAME_G3}.")
        return EGALITE

    print(f"ECART : ecart entre {NAME_LIVE} calcule et {NAME_G3}.")
    reponse = "oui" if live == sortie_g2b else "non"
    print(f"Le meme fichier vivant egale-t-il la sortie declaree par {NAME_G2B} ?"
          f" {reponse}.")
    return ECART


if __name__ == "__main__":
    sys.exit(main())
