"""Mesure rejouable du lot 020 : provenance du littoral declaree par les cellules.

Chaque compteur du tableau du brief est imprime avec son denominateur, et chacun
est **derive** : lu des artefacts, lu des constantes, lu de l'etat du depot, ou
obtenu en rejouant une commande et en relevant son code de sortie. Aucune valeur
n'est recopiee a la main.

Le script n'imprime aucune valeur d'empreinte (regle durement acquise n^o 12) :
seulement des noms de source et des resultats de comparaison. L'epsilon de
surface est **lue** de `pipeline/geo/constants.py` et n'apparait en litteral
nulle part.

Ce qu'il execute, et pourquoi ce n'est pas une regeneration :

- l'alignement `steps/03b_align_coastline_provenance.py` est rejoue deux fois,
  parce que son denominateur (« combien de fichiers ecrit-il ? ») se lit de sa
  sortie et que son determinisme se mesure en comparant deux passes. Il est
  idempotent : ces deux passes ne changent aucun octet.
- la garde `tests/run_proof_coastline_provenance.py` est rejouee sur le depot,
  puis sur une copie **hors du depot** dont la declaration d'entree est mutee ;
  la copie est detruite ensuite et aucun de ses fichiers n'entre dans le depot.
- ni la maille des cellules ni le graphe G4 ne sont rejoues.

Usage, depuis la racine du depot :
  .venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/measure_g3_provenance_020.py
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile

BRIEF_DIR = pathlib.Path(__file__).resolve().parents[1]
DELIVRABLES = BRIEF_DIR / "deliverables"
PRE_EDIT = DELIVRABLES / "pre-edit"
REPO = BRIEF_DIR.parents[3]
GEO = REPO / "pipeline" / "geo"
ARTIFACTS = GEO / "artifacts"

sys.path.insert(0, str(GEO))

from constants import G3_AREA_EPS_M2  # noqa: E402
from io_util import read_json, sha256_file, write_json  # noqa: E402

INTERPRETEUR = sys.executable

SENTINELLE_NON_CALCULE = -1

# Le mot de l'alias nu est assemble en deux morceaux : ecrit d'un seul tenant,
# il compterait comme sa propre infraction lors du balayage ci-dessous.
MOT_ALIAS = "py" + "thon"
MOTIF_ALIAS = re.compile(r"(?<![\w./\\-])" + MOT_ALIAS + r"(?![\w.-])")
MOTIF_CHEMIN_WINDOWS = re.compile(r"\.venv[/\\]Scripts[/\\]")
MOTIF_HEXADECIMAL = re.compile(r"[0-9a-f]{32,}")

FICHIERS_MAILLE = [
    "pipeline/geo/artifacts/cells_g3.json",
    "pipeline/geo/artifacts/adjacency_g3.json",
    "pipeline/geo/artifacts/stats_g3.json",
    "pipeline/geo/registry/cell_registry.json",
]

FICHIERS_G4_ALIGNES = [
    "pipeline/geo/artifacts/stats_g4.json",
    "pipeline/geo/artifacts/MANIFEST_g4.json",
]

PREUVES_SOUS_GEO = [
    "pipeline/geo/steps/03b_align_coastline_provenance.py",
    "pipeline/geo/tests/run_proof_coastline_provenance.py",
    "pipeline/geo/logs/v1_051_provenance.json",
    "pipeline/geo/logs/v1_051_provenance_vert.txt",
    "pipeline/geo/logs/v1_051_provenance_rouge.txt",
    "pipeline/geo/artifacts/MANIFEST_g3.json",
    "pipeline/geo/artifacts/stats_g4.json",
    "pipeline/geo/artifacts/MANIFEST_g4.json",
    "pipeline/geo/README.md",
]

# Perimetre de D10 : tout chemin modifie doit tomber dans cette liste.
PERIMETRE_AUTORISE = [
    "pipeline/geo/steps/03b_align_coastline_provenance.py",
    "pipeline/geo/tests/run_proof_coastline_provenance.py",
    "pipeline/geo/logs/v1_051_",
    "pipeline/geo/artifacts/MANIFEST_g3.json",
    "pipeline/geo/artifacts/stats_g4.json",
    "pipeline/geo/artifacts/MANIFEST_g4.json",
    "pipeline/geo/README.md",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/",
    "harness/queue/cost-ledger.jsonl",
]

# Fichiers de texte et de code produits ou modifies par ce lot. Les artefacts
# JSON de la chaine sont exclus du balayage parce que porter des empreintes est
# leur metier ; les deux instantanes `pre-edit/*.orig` le sont aussi parce que
# ce sont des copies machine d'artefacts, non des citations dans de la prose.
FICHIERS_BALAYES = [
    "pipeline/geo/steps/03b_align_coastline_provenance.py",
    "pipeline/geo/tests/run_proof_coastline_provenance.py",
    "pipeline/geo/logs/v1_051_provenance.json",
    "pipeline/geo/logs/v1_051_provenance_vert.txt",
    "pipeline/geo/logs/v1_051_provenance_rouge.txt",
    "pipeline/geo/README.md",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "check_provenance_coastline_020.py",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "measure_g3_provenance_020.py",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "check_provenance_apres.txt",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "generator-log.md",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "manifest.json",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "pre-edit/check_provenance_avant.txt",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "pre-edit/cell_ids_actifs.txt",
    "harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/"
    "pre-edit/pipeline-geo-README.md.orig",
]

COMPTEURS: list[tuple[str, object, object, str]] = []


def rapporter(nom: str, valeur: object, denominateur: object, note: str) -> None:
    COMPTEURS.append((nom, valeur, denominateur, note))
    print(f"{nom}: {valeur} / {denominateur}  ({note})")


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True, check=True
    ).stdout


def porcelain(chemins: list[str]) -> list[str]:
    if not chemins:
        return []
    sortie = git("status", "--porcelain", "--", *chemins)
    return [ligne for ligne in sortie.splitlines() if ligne.strip()]


def suivis(prefixe: str) -> list[str]:
    return [ligne for ligne in git("ls-files", prefixe).splitlines() if ligne.strip()]


def empreinte_octets(chemin: pathlib.Path) -> str:
    return hashlib.sha256(chemin.read_bytes()).hexdigest()


def feuilles(objet, prefixe: str = ""):
    if isinstance(objet, dict):
        for cle, valeur in objet.items():
            yield from feuilles(valeur, f"{prefixe}.{cle}")
    elif isinstance(objet, list):
        for index, valeur in enumerate(objet):
            yield from feuilles(valeur, f"{prefixe}[{index}]")
    else:
        yield prefixe, objet


def compte_constats_ouverts(chemin: pathlib.Path) -> int:
    texte = chemin.read_text(encoding="utf-8")
    bloc = texte.split("Constats ouverts", 1)[1].split("\n## ", 1)[0]
    return len(re.findall(r"^- \*\*", bloc, re.M))


def resume_pytest(cible: str) -> tuple[int, int]:
    """(tests PASSED, tests collectes) lus de la ligne de resume de pytest."""
    execution = subprocess.run(
        [INTERPRETEUR, "-m", "pytest", cible, "-q"],
        cwd=REPO, capture_output=True, text=True,
    )
    passes = 0
    total = 0
    for issue in ("passed", "failed", "error", "errors", "skipped",
                  "xfailed", "xpassed"):
        trouve = re.findall(rf"(\d+) {issue}\b", execution.stdout)
        if not trouve:
            continue
        nombre = int(trouve[-1])
        total += nombre
        if issue == "passed":
            passes = nombre
    return passes, total


# --------------------------------------------------------------------------
# SC1 — le diagnostic geometrique, rejoue
# --------------------------------------------------------------------------
print("== SC1 : diagnostic geometrique rejoue ==")

from shapely.geometry import shape  # noqa: E402
from shapely.ops import unary_union  # noqa: E402

littoral = read_json(ARTIFACTS / "coastline_1400.json")
terre = shape(littoral["geometry"])
cells = read_json(ARTIFACTS / "cells_g3.json")["cells"]
stats_g3 = read_json(ARTIFACTS / "stats_g3.json")

union_cellules = unary_union([shape(cellule["geometry"]) for cellule in cells])

terre_vivante_m2 = round(terre.area, 3)
depassement_m2 = round(union_cellules.difference(terre).area, 3)
non_couverte_m2 = round(terre.difference(union_cellules).area, 3)
epsilon_m2 = float(G3_AREA_EPS_M2)

rapporter("cellules_lues_g3", len(cells), stats_g3["cell_count"],
          "denominateur lu : cell_count de artifacts/stats_g3.json")
rapporter("terre_vivante_m2", terre_vivante_m2, 1,
          "1 mesure geometrique sur la terre du littoral vivant, projection"
          f" EPSG:3035 ; recoupement lu de l'artefact : land_area_km2 ="
          f" {littoral['land_area_km2']} soit"
          f" {round(terre_vivante_m2 / 1e6, 2)} km2 mesures")
rapporter("depassement_cellules_hors_terre_m2", depassement_m2, terre_vivante_m2,
          "denominateur : terre_vivante_m2 ; comparee a l'epsilon lue"
          f" epsilon_surface_g3_m2 = {epsilon_m2}")
rapporter("terre_non_couverte_m2", non_couverte_m2, terre_vivante_m2,
          "denominateur : terre_vivante_m2 ; comparee a l'epsilon lue"
          f" epsilon_surface_g3_m2 = {epsilon_m2}")
rapporter("epsilon_surface_g3_m2", epsilon_m2, 1,
          "1 valeur LUE de pipeline/geo/constants.py (G3_AREA_EPS_M2), jamais"
          " un litteral de ce script")

ecart_est_serialisation = int(
    depassement_m2 <= epsilon_m2 and non_couverte_m2 <= epsilon_m2
)
rapporter("ecart_est_serialisation", ecart_est_serialisation, 1,
          "1 comparaison composee : les deux aires mesurees confrontees a"
          " l'epsilon lue")

avant_txt = (PRE_EDIT / "check_provenance_avant.txt").read_text(encoding="utf-8")
premier_mot = avant_txt.split(":", 1)[0].strip()
code_avant = {"ECART": 1, "EGALITE": 0, "ABSENCE": 2}[premier_mot]
rapporter("code_sortie_ecart_avant", code_avant, 1,
          "1 execution, code derive du verdict imprime dans"
          f" deliverables/pre-edit/check_provenance_avant.txt ({premier_mot})")

execution_apres = subprocess.run(
    [INTERPRETEUR, str(DELIVRABLES / "check_provenance_coastline_020.py")],
    cwd=REPO, capture_output=True, text=True,
)
rapporter("code_sortie_ecart_apres", execution_apres.returncode, 1,
          "1 execution de deliverables/check_provenance_coastline_020.py"
          " rejouee a l'instant")

# --------------------------------------------------------------------------
# SC2 — la maille n'a pas bouge
# --------------------------------------------------------------------------
print("== SC2 : maille gelee, sim/ en lecture seule ==")

instantane = {
    int(ligne)
    for ligne in (PRE_EDIT / "cell_ids_actifs.txt").read_text(
        encoding="utf-8").split()
}
actuels = {cellule["cell_id"] for cellule in cells}

rapporter("cellules_actives_instantane", len(instantane), stats_g3["cell_count"],
          "denominateur lu : cell_count de artifacts/stats_g3.json ;"
          " instantane pris avant toute ecriture")
rapporter("cellules_actives_inchangees", len(instantane & actuels),
          len(instantane), "denominateur : identifiants de l'instantane")
rapporter("cellules_actives_ajoutees", len(actuels - instantane), len(instantane),
          "zero MESURE par difference d'ensembles, jamais la sentinelle"
          f" {SENTINELLE_NON_CALCULE}")
rapporter("cellules_actives_retirees", len(instantane - actuels), len(instantane),
          "zero MESURE par difference d'ensembles, jamais la sentinelle"
          f" {SENTINELLE_NON_CALCULE}")

maille_modifiee = {ligne[3:].strip() for ligne in porcelain(FICHIERS_MAILLE)}
rapporter("artefacts_maille_diff_vides",
          sum(1 for chemin in FICHIERS_MAILLE if chemin not in maille_modifiee),
          len(FICHIERS_MAILLE),
          "denominateur : les 4 fichiers de maille verifies par"
          " git status --porcelain")

sim_suivis = suivis("sim")
rapporter("fichiers_sim_modifies", len(porcelain(["sim"])), len(sim_suivis),
          "denominateur : fichiers suivis sous sim/, comptes par git ls-files")

sim_passes, sim_total = resume_pytest("sim/tests/")
rapporter("tests_sim_passed_020", sim_passes, sim_total,
          "denominateur : tests collectes dans sim/tests/, lu du resume pytest")

# --------------------------------------------------------------------------
# SC3 — la declaration d'entree de G3
# --------------------------------------------------------------------------
print("== SC3 : MANIFEST_g3.json declare le littoral que la chaine produit ==")

empreinte_vivante = sha256_file(ARTIFACTS / "coastline_1400.json")
manifeste_g3 = read_json(ARTIFACTS / "MANIFEST_g3.json")
entree_g3 = str(manifeste_g3["inputs"]["coastline_1400"])
sortie_g2b = str(
    read_json(ARTIFACTS / "MANIFEST_g2b.json")["outputs"][
        "artifacts/coastline_1400.json"]
)

rapporter("empreinte_entree_g3_egale_vivant",
          int(entree_g3 == empreinte_vivante), 1,
          "1 comparaison : empreinte du littoral vivant calculee a l'execution"
          " vs MANIFEST_g3.json inputs.coastline_1400")
rapporter("empreinte_vivant_egale_sortie_g2b",
          int(sortie_g2b == empreinte_vivante), 1,
          "1 comparaison : meme empreinte vs la sortie declaree par"
          " MANIFEST_g2b.json pour ce fichier")

sorties_g3 = manifeste_g3["outputs"]
conformes_g3 = sum(
    1 for rel, declaree in sorties_g3.items()
    if (GEO / rel).is_file() and sha256_file(GEO / rel) == declaree
)
rapporter("sorties_g3_conformes", conformes_g3, len(sorties_g3),
          "denominateur : entrees du bloc outputs lues de MANIFEST_g3.json ;"
          " empreintes recalculees a l'execution")

orig_g3 = json.loads(
    (PRE_EDIT / "MANIFEST_g3.json.orig").read_text(encoding="utf-8"))
avant_feuilles = dict(feuilles(orig_g3))
apres_feuilles = dict(feuilles(manifeste_g3))
chemins_modifies = sorted(
    cle for cle in set(avant_feuilles) | set(apres_feuilles)
    if avant_feuilles.get(cle) != apres_feuilles.get(cle)
)
rapporter("champs_manifeste_g3_modifies", len(chemins_modifies),
          len(apres_feuilles),
          "denominateur : feuilles JSON du manifeste publie ; chemins"
          f" differant de l'instantane pre-edit : {chemins_modifies} ;"
          f" fixed_timestamp conserve = {manifeste_g3['fixed_timestamp']}")

# --------------------------------------------------------------------------
# SC5 (partie alignement) — determinisme, denominateur lu de la sortie
# --------------------------------------------------------------------------
print("== SC5 : alignement deterministe, garde vue verte et vue rouge ==")


def jouer_alignement() -> tuple[int, list[str]]:
    execution = subprocess.run(
        [INTERPRETEUR, "steps/03b_align_coastline_provenance.py"],
        cwd=GEO, capture_output=True, text=True,
    )
    ecrits = [
        ligne.split("ecrit:", 1)[1].strip()
        for ligne in execution.stdout.splitlines() if "ecrit:" in ligne
    ]
    return execution.returncode, ecrits


code_passe1, ecrits = jouer_alignement()
chemins_ecrits = [GEO / rel for rel in ecrits]
empreintes_passe1 = {rel: empreinte_octets(GEO / rel) for rel in ecrits}
code_passe2, ecrits_passe2 = jouer_alignement()
empreintes_passe2 = {rel: empreinte_octets(GEO / rel) for rel in ecrits_passe2}

identiques = sum(
    1 for rel in ecrits if empreintes_passe1[rel] == empreintes_passe2.get(rel)
)
rapporter("passes_alignement_identiques", identiques, len(ecrits),
          "denominateur LU de la sortie de l'alignement (fichiers_ecrits) ;"
          f" codes de sortie des deux passes : {code_passe1} et {code_passe2}")

changes_par_seconde_passe = len(ecrits) - identiques
lignes_porcelain_artifacts = porcelain(["pipeline/geo/artifacts"])
rapporter("diff_apres_seconde_passe", changes_par_seconde_passe, len(ecrits),
          "denominateur : fichiers ecrits par l'alignement ; zero MESURE ="
          " la seconde passe ne change aucun octet par rapport a l'etat"
          " post-premiere-passe. Les lignes que git status --porcelain --"
          " pipeline/geo/artifacts montre encore sont la reparation"
          " elle-meme, non committee :"
          f" {[ligne[3:].strip() for ligne in lignes_porcelain_artifacts]}")

roundtrip_neutres = 0
with tempfile.TemporaryDirectory(prefix="020-roundtrip-") as bac:
    for rel in ecrits:
        source = GEO / rel
        copie = pathlib.Path(bac) / pathlib.Path(rel).name
        write_json(copie, read_json(source))
        roundtrip_neutres += int(source.read_bytes() == copie.read_bytes())
rapporter("roundtrip_serialisation_neutre", roundtrip_neutres, len(ecrits),
          "denominateur : artefacts reecrits par l'alignement ; chacun relu"
          " puis reecrit par io_util.write_json sans changer aucune valeur,"
          " vers une destination hors du depot, et compare octet pour octet")

garde_verte = subprocess.run(
    [INTERPRETEUR, "tests/run_proof_coastline_provenance.py"],
    cwd=GEO, capture_output=True, text=True,
)
rapporter("code_sortie_garde_verte", garde_verte.returncode, 1,
          "1 execution de tests/run_proof_coastline_provenance.py sur le depot")

with tempfile.TemporaryDirectory(prefix="020-sabotage-") as bac:
    copie = pathlib.Path(bac)
    (copie / "artifacts").mkdir()
    (copie / "tests").mkdir()
    shutil.copyfile(GEO / "io_util.py", copie / "io_util.py")
    shutil.copyfile(GEO / "tests" / "run_proof_coastline_provenance.py",
                    copie / "tests" / "run_proof_coastline_provenance.py")
    for nom in ("coastline_1400.json", "MANIFEST_g2b.json", "MANIFEST_g3.json",
                "MANIFEST_g4.json", "stats_g4.json"):
        shutil.copyfile(ARTIFACTS / nom, copie / "artifacts" / nom)
    sabote = read_json(copie / "artifacts" / "MANIFEST_g3.json")
    # Le sabotage porte sur la DECLARATION, jamais sur le code de la garde. La
    # valeur mutee est derivee d'une phrase, pour n'ecrire aucune empreinte en
    # litteral dans ce fichier.
    sabote["inputs"]["coastline_1400"] = hashlib.sha256(
        b"sabotage hors depot du lot 020").hexdigest()
    write_json(copie / "artifacts" / "MANIFEST_g3.json", sabote)
    garde_rouge = subprocess.run(
        [INTERPRETEUR, "tests/run_proof_coastline_provenance.py"],
        cwd=copie, capture_output=True, text=True,
    )
rapporter("code_sortie_garde_rouge_hors_depot", garde_rouge.returncode, 1,
          "1 execution de la meme garde sur une copie hors depot dont la"
          " declaration d'entree de G3 est mutee ; strictement positif attendu")

# --------------------------------------------------------------------------
# SC4 — G4 relit la provenance reparee sans que son graphe bouge
# --------------------------------------------------------------------------
print("== SC4 : G4 relit la provenance, son graphe ne bouge pas ==")

manifeste_g4 = read_json(ARTIFACTS / "MANIFEST_g4.json")
stats_g4 = read_json(ARTIFACTS / "stats_g4.json")
entree_g3 = str(read_json(ARTIFACTS / "MANIFEST_g3.json")["inputs"][
    "coastline_1400"])

rapporter("provenance_g4_egale_entree_g3",
          int(str(manifeste_g4["coastline_1400_sha_declared_by_g3"])
              == entree_g3), 1,
          "1 comparaison : MANIFEST_g4.json"
          " coastline_1400_sha_declared_by_g3 vs MANIFEST_g3.json"
          " inputs.coastline_1400")
rapporter("drapeau_egalite_manifeste_g4",
          manifeste_g4["coastline_1400_sha_equal"], 1,
          "1 champ lu de MANIFEST_g4.json (entier, derive par l'alignement)")
rapporter("drapeau_egalite_stats_g4",
          stats_g4["coastline_1400_sha_equals_g3_input"], 1,
          "1 champ lu de stats_g4.json (entier, derive par l'alignement)")

sorties_g4 = manifeste_g4["outputs"]
conformes_g4 = sum(
    1 for rel, declaree in sorties_g4.items()
    if (GEO / rel).is_file() and sha256_file(GEO / rel) == declaree
)
rapporter("sorties_g4_conformes", conformes_g4, len(sorties_g4),
          "denominateur : entrees du bloc outputs lues de MANIFEST_g4.json ;"
          " empreintes recalculees a l'execution")

g4_suivis = [
    chemin for chemin in suivis("pipeline/geo")
    if "g4" in chemin.lower() or "sea_zone" in chemin.lower()
]
g4_modifies = {ligne[3:].strip() for ligne in porcelain(g4_suivis)}
rapporter("artefacts_g4_modifies_hors_liste",
          len(g4_modifies - set(FICHIERS_G4_ALIGNES)), len(g4_suivis),
          "denominateur : fichiers G4 suivis par git (git ls-files, noms"
          " portant g4 ou sea_zone) ; les deux fichiers alignes par ce lot"
          " sont exclus du numerateur")

fichiers_graphe_g4 = [
    "pipeline/geo/artifacts/sea_zones_g4.json",
    "pipeline/geo/artifacts/adjacency_g4.json",
    "pipeline/geo/artifacts/topology_links_g4.json",
    "pipeline/geo/artifacts/adjacency_divergence_g4.json",
    "pipeline/geo/registry/sea_zone_registry.json",
] + [chemin for chemin in suivis("pipeline/geo/logs") if "v1_050" in chemin] \
  + suivis("pipeline/geo/capture")
graphe_modifie = {ligne[3:].strip() for ligne in porcelain(fichiers_graphe_g4)}
rapporter("graphe_g4_diff_vides",
          sum(1 for chemin in fichiers_graphe_g4
              if chemin not in graphe_modifie),
          len(fichiers_graphe_g4),
          "denominateur : fichiers de graphe G4 listes en D5 (artefacts,"
          " registre, journaux v1_050 et captures suivis par git)")

# --------------------------------------------------------------------------
# SC6 — le README
# --------------------------------------------------------------------------
print("== SC6 : le README ferme le constat de 019 sans sur-revendiquer ==")

constats_apres = compte_constats_ouverts(GEO / "README.md")
constats_avant = compte_constats_ouverts(PRE_EDIT / "pipeline-geo-README.md.orig")
rapporter("constats_ouverts_README", constats_apres, constats_avant,
          "denominateur : meme compte pris sur pre-edit/"
          "pipeline-geo-README.md.orig ; strictement inferieur attendu")
rapporter("readme_differe_instantane",
          int(empreinte_octets(GEO / "README.md")
              != empreinte_octets(PRE_EDIT / "pipeline-geo-README.md.orig")), 1,
          "1 comparaison d'empreintes calculees a l'execution, aucune imprimee")

# --------------------------------------------------------------------------
# SC7 — perimetre, suivi git, suites, registre
# --------------------------------------------------------------------------
print("== SC7 : perimetre tenu, preuves suivies, suites vertes ==")

hex_cites = 0
alias_nus = 0
balayes = 0
for rel in FICHIERS_BALAYES:
    chemin = REPO / rel
    if not chemin.is_file():
        continue
    balayes += 1
    texte = chemin.read_text(encoding="utf-8", errors="replace")
    hex_cites += len(MOTIF_HEXADECIMAL.findall(texte))
    alias_nus += len(MOTIF_ALIAS.findall(texte))
    alias_nus += len(MOTIF_CHEMIN_WINDOWS.findall(texte))
rapporter("valeurs_hexadecimales_citees", hex_cites, balayes,
          "denominateur : fichiers de texte et de code balayes ; artefacts"
          " JSON de la chaine et instantanes pre-edit/*.orig exclus, parce"
          " que ce sont des copies machine d'artefacts")
rapporter("alias_python_nu", alias_nus, balayes,
          "denominateur : les memes fichiers balayes ; l'alias nu de"
          " l'interpreteur et les chemins de lanceur Windows (repertoire"
          " Scripts sous .venv, machine Linux ici) sont recherches ensemble."
          " Ces deux motifs ne sont pas ecrits en clair dans ce script : un"
          " balayage qui contient sa propre cible se compte lui-meme")

toutes_lignes = porcelain(["."])
hors_perimetre = [
    ligne for ligne in toutes_lignes
    if not any(ligne[3:].strip().startswith(autorise)
               for autorise in PERIMETRE_AUTORISE)
]
rapporter("fichiers_hors_perimetre_modifies", len(hors_perimetre),
          len(toutes_lignes),
          "denominateur : lignes totales de git status --porcelain ; chemins"
          f" hors perimetre D10 : {[l[3:].strip() for l in hors_perimetre]}")

tous_suivis = set(suivis("pipeline/geo"))
rapporter("fichiers_preuve_suivis_par_git",
          sum(1 for chemin in PREUVES_SOUS_GEO if chemin in tous_suivis),
          len(PREUVES_SOUS_GEO),
          "denominateur : preuves declarees sous pipeline/geo/ ; suivi prouve"
          " par git ls-files (logs/ et artifacts/ sont exclus par .gitignore,"
          " l'ajout est donc force)")

harness_passes, harness_total = resume_pytest("harness/tests/")
rapporter("tests_harness_passed_020", harness_passes, harness_total,
          "denominateur : tests collectes dans harness/tests/, lu du resume"
          " pytest ; les SKIP Unity propres a Linux sont comptes dans le"
          " denominateur et declares")

lignes_ledger = [
    ligne for ligne in (REPO / "harness" / "queue" / "cost-ledger.jsonl")
    .read_text(encoding="utf-8").splitlines() if ligne.strip()
]
derniere = json.loads(lignes_ledger[-1]) if lignes_ledger else {}
rapporter("ligne_ledger_ajoutee",
          int(derniere.get("event") == "generator-run"
              and derniere.get("backend") == "cursor"
              and "020" in str(derniere.get("brief", ""))), 1,
          "1 ligne verifiee : la derniere de harness/queue/cost-ledger.jsonl")

print()
print(f"compteurs imprimes : {len(COMPTEURS)}")
