# Eval Rubric — Brief 011 : Amorçage du moteur de simulation `sim/`

**Authored**: 2026-08-12T15:57:00Z
**Author**: forge-planificateur

Écrite avant tout travail du Générateur. Ne sera pas révisée après avoir vu
les livrables : une grille réécrite pour épouser ce qui a été livré ne juge
plus rien.

## Comment l'Évaluateur se sert de cette grille

Le gate mécanique est **nécessaire et non suffisant**. L'Évaluateur
reconstruit **chaque** compteur par sa propre commande. Un chiffre repris
du manifeste sans être recalculé n'est pas une vérification.

**Règle de preuve inversée propre à ce brief :** SC3, SC8 et SC10 portent
sur des refus ou des échecs de test. On ne prouve pas un refus en montrant
qu'un test passe — on le prouve en montrant que le test **échoue avant**
le correctif (sortie rouge datée), puis réussit après (sortie verte).
Toute condition portant sur un refus livrée sans sa sortie rouge est
comptée comme non satisfaite.

## Grille de vérification

| SC | Ce qui est exigé | Ce qui rend la condition NON satisfaite |
|---|---|---|
| SC1 | `.venv/bin/python -c "import sim; print(sim.__version__)"` s'exécute sans erreur, affiche une version non vide. `sim/README.md` décrit le paquet, ses modules, la commande de test, la source des données. | Importation qui échoue. `__version__` vide ou absente. README qui paraphrase `VISION.md` ou les ADR au lieu de les pointer. |
| SC2 | `World.from_g3()` charge les deux artefacts G3. Le test compare `len(world.cells)` à `stats_g3.json["cell_count"]` — les deux valeurs sont affichées côte à côte dans la sortie recopiée. Le compteur `cells_chargees` est produit par le test, pas saisi à la main. | Valeur 596 écrite en dur dans le test ou le code de chargement. Compteur absent du journal. Comparaison non recopiée dans le journal. |
| SC3 | Aucune entité du modèle ne possède de champ `province_id` ou équivalent. Le test qui vérifie cela **échoue** si un tel champ est ajouté à la dataclass — preuve faite sur une branche sabotée ou en sous-test paramétrique. | Test qui réussit même si `province_id` est présent. Vérification par lecture de code au lieu d'exécution. Province traitée comme un champ stocké quelque part. |
| SC4 | `sim/SEEDING.md` est committé, non vide, documente formule + paramètres + unités + déclaration explicite que l'amorçage est paramétrique (non inventé). Deux runs avec la même graine produisent des populations initiales identiques. Le compteur `amorçage_deterministe_valide` est présent dans le journal avec sa commande. | `sim/SEEDING.md` absent, vide, ou qui ne déclare pas explicitement que les données sont paramétriques. Résultats non déterministes. Compteur annoncé sans être reconstruit. |
| SC5 | Les condensés SHA256 des deux runs (même graine, N ≥ 10 ticks) sont affichés par leurs noms de variable et déclarés égaux. Le compteur `ticks_deterministes_valides` est présent. | Condensés codés en dur dans le test (hard-won rule 12). Aléa global non contrôlé (ex. `random.random()` sans graine passée). Un seul run affiché. |
| SC6 | `food_stock_kg` et `hunger_ticks` sont des champs nommés (ou équivalents documentés) sur `Cell`, avec valeurs sentinelles `-1` pour « non calculé/initialisé ». Production écrite avant consommation. Consommation modifie le champ — pas de calcul à la volée hors champ. | Champs absents ou en lecture seule. Sentinelle `0` au lieu de `-1` pour « non calculé ». Bilan calculé sans passer par le champ persisté. |
| SC7a | Test unitaire : une cellule avec production < consommation voit `food_stock_kg` baisser après un tick. État initial construit à la main, un seul maillon testé. | Test qui appelle le tick complet au lieu d'isoler le maillon production–stock. |
| SC7b | Test unitaire : une cellule avec `food_stock_kg` ≤ 0 voit `hunger_ticks` progresser après un tick. État initial construit à la main. | Test dépendant de SC7a pour son état initial. |
| SC7c | Test unitaire : une cellule avec `hunger_ticks` ≥ seuil voit `population` diminuer après un tick. Le seuil est lu depuis une constante nommée, pas un littéral inline. | Seuil codé en dur dans le test. Test dépendant d'autres maillons. |
| SC7d | Test d'intégration : une cellule avec rendement = 0 et population initiale > 0 a une population strictement inférieure après N ticks suffisants. Le test passe par `tick()` — pas par appel direct à la règle de mort. | Population qui ne baisse jamais malgré la famine. Test qui court-circuite le tick. |
| SC8 | `test_write_coverage.py` vérifie chaque champ des dataclasses : un site d'écriture ET un site de lecture existent. Ce test **échoue** (preuve rouge) si un champ est déclaré sans écrivain. Compteur `champs_modele_couverts` produit par le test, recopié dans le journal. | Test qui passe sur une dataclass vide (no_empty_sample_pass). Compteur codé en dur. Preuve rouge absente. |
| SC9 | `compteurs_en_dur_trouves` = 0 dans la sortie du test d'inspection statique. La commande ayant produit cette valeur est recopiée dans le journal. | Valeur 0 annoncée sans commande. Inspection limitée à un sous-ensemble des fichiers `sim/`. |
| SC10 | `sim/tests/proof_red/run_sabotage.txt` et `sim/tests/proof_red/run_correct.txt` existent, sont committés, et sont différents. La sortie rouge montre au moins un test FAILED. La sortie verte montre tous les tests PASSED. | Les deux fichiers sont identiques. `run_sabotage.txt` montre tous les tests verts (la sabotage n'a pas fonctionné). Fichiers non committés (donc vérifiables seulement localement). |
| SC11 | `.venv/bin/python -m pytest sim/tests/ -v` retourne exit code 0, tous les tests PASSED. Sortie réelle recopiée dans le journal. | Un ou plusieurs tests FAILED ou ERROR. Sortie absente du journal. |
| SC12 | `sim/README.md` mentionne les modules, la commande de test, la source des données, et l'histoire du stub (vide jusqu'au brief 011). Il pointe vers `VISION.md` et les ADR sans les paraphraser. | README inchangé ou qui réécrit le contenu d'un autre document. |

## Contrôles de gate mécanique (les dix)

`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`verdict_numbers_traceable`, `no_bare_python_alias`,
`verdict_is_not_self_authored`, `rubric_predates_deliverables`,
`declared_files_are_tracked`.

Note sur `captures_differ_when_should` : la paire
`sim/tests/proof_red/run_sabotage.txt` /
`sim/tests/proof_red/run_correct.txt` doit être déclarée dans
`deliverables/manifest.json` sous `must_differ_from`. Si cette déclaration
est absente, le gate ne vérifie pas la différence et la preuve rouge n'est
pas vérifiable mécaniquement.

Note sur `no_bare_python_alias` : le journal du Générateur et le verdict ne
doivent pas contenir `python` en position de commande (début de ligne ou
après un opérateur shell). `.venv/bin/python`, `python3` et `py` sont sûrs.

## Échecs disqualifiants

1. Écrire le verdict d'un lot qu'on a produit soi-même.
2. Coder 596 (ou toute autre constante G3) en dur dans le code de
   chargement ou dans un test.
3. Recopier une valeur de condensé SHA256 en dur dans un test ou un
   document.
4. Produire un compteur sans la commande qui l'a mesuré.
5. Annoncer un comportement dans `sim/SEEDING.md` ou `sim/README.md` qu'aucun
   code de `sim/` n'exécute.
6. Faire passer SC8 en retirant des champs du modèle plutôt qu'en ajoutant
   des écrivains et des lecteurs.
7. Fabriquer un fichier `proof_red/run_sabotage.txt` qui montre des tests
   verts : la sabotage n'a alors pas été faite, et la preuve est invalide.
8. Modifier `VISION.md`, `pipeline/geo/`, `harness/`, `docs/adr/` ou tout
   fichier hors `sim/` et `sim/README.md`.

## Ce qui vaut ACCEPT

Un lot est ACCEPTÉ quand **toutes** les SC ci-dessus sont satisfaites, que
chaque compteur a été reconstruit par l'Évaluateur avec ses propres
commandes, que la paire `must_differ_from` est déclarée dans le manifeste
et que les deux fichiers diffèrent effectivement, et qu'aucun échec
disqualifiant n'est présent. Une condition « satisfaite en substance » ne
l'est pas.
