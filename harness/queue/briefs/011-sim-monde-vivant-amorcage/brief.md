# Brief 011 : Amorçage du moteur de simulation `sim/` — couche 1 « monde vivant »

**Authored**: 2026-08-12T15:57:00Z
**Author**: forge-planificateur

## Contexte et autorisation

`sim/README.md` indiquait que l'écriture de code de simulation était
conditionnée à l'existence de l'ADR sur la clé spatiale. Cet ADR est
`docs/adr/0003-single-spatial-primary-key.md`, accepté. La condition est
levée ; ce brief est l'autorisation d'écrire du code sous `sim/`.

Il correspond à l'étape « Monde vivant » de `VISION.md` (couche 1 de la
roadmap) et au premier brief F2 de ForgeHistory.

## World-Terms Requirement

La simulation raisonne en termes de monde, jamais de règles de jeu. La
condition à satisfaire est la suivante, exprimée comme une chaîne causale
(cf. `docs/rules/simulation-principles.md`, principe 2) :

> Une cellule géographique produit de la nourriture en fonction de sa
> superficie et d'un rendement agricole documenté. Cette nourriture est
> stockée dans la cellule. La population de la cellule consomme cette
> nourriture depuis le stock. Lorsque le stock est épuisé, les habitants
> manquent de nourriture — leur état de faim progresse. Lorsque la faim
> persiste sur plusieurs pas de temps, la mortalité augmente et la
> population diminue.

Chaque maillon de cette chaîne (production, stock, consommation,
épuisement, faim, mort) est **un état réellement écrit puis lu** — jamais
calculé à la volée sans être persisté. Aucun résultat n'est codé en dur
sous la forme « si faim alors +N% de mortalité » ; la mortalité émerge
de la lecture de l'état de faim accumulé.

## Success Conditions

### SC1 — Paquet `sim/` importable et documenté

Le paquet `sim/` est un paquet Python (stdlib uniquement, sans dépendance
tierce dans le code du moteur — `pytest` est réservé aux tests) importable
depuis la racine du dépôt :

```
.venv/bin/python -c "import sim; print(sim.__version__)"
```

doit s'exécuter sans erreur et afficher une version non vide. `sim/README.md`
est mis à jour pour décrire le paquet, ses modules, la commande de lancement
des tests et la source des données d'entrée.

### SC2 — Chargement du monde depuis les artefacts G3

`sim.world.World.from_g3()` charge `pipeline/geo/artifacts/cells_g3.json`
et `pipeline/geo/artifacts/adjacency_g3.json` depuis la racine du dépôt.
Ces deux fichiers sont suivis par git et lisibles depuis un clone frais.

Le nombre de cellules chargées est **dérivé du fichier** au moment du
chargement — jamais codé en dur. Un test compare cette valeur à `cell_count`
lu dans `pipeline/geo/artifacts/stats_g3.json`. Le compteur `cells_chargees`
(voir § Required Counters) est produit par ce test et recopié dans le
journal du Générateur.

### SC3 — Respect de l'ADR-0003 : `cell_id` comme seule clé spatiale

Aucune entité du modèle (`Cell`, toute structure de population) ne stocke
un champ nommé `province_id`, `ProvinceId`, ou équivalent. La Province
(agrégation dérivée) n'est jamais un champ indépendamment écrit sur une
entité. Un test vérifie qu'instancier une entité avec un tel champ lève
une erreur explicite (pas un AttributeError silencieux).

Ce test est la preuve que le défaut décrit dans
`docs/adr/0003-single-spatial-primary-key.md` n'a pas de chemin de code
pour se reproduire dans ce paquet.

### SC4 — Amorçage documenté, déterministe, déclaré non inventé

`sim/SEEDING.md` (fichier committé, non git-ignoré) documente
**explicitement** :
- la formule d'amorçage de la population par cellule (source,
  paramètres, unités) ;
- l'affirmation explicite que cette formule est un proxy paramétrique
  et non une donnée historique inventée (cf. hard-won rule 10 : l'absence
  de données ne s'invente pas en silence).

Deux exécutions avec la même graine `rng_seed` produisent des populations
initiales byte-identiques. Le compteur `amorçage_deterministe_valide`
est produit par le test de comparaison (voir § Required Counters).

### SC5 — Boucle de tick déterministe

`sim.engine.tick(world, rng)` fait avancer l'état du monde d'un pas de
temps. `rng` est une instance de `random.Random` initialisée avec une
graine fournie à l'appelant — jamais une source d'aléa globale non
contrôlée.

Deux runs complets de N ticks (N ≥ 10) avec la même graine produisent des
états de monde dont le condensé SHA256 (calculé sur la sérialisation
canonique de l'état) est identique. Le compteur `ticks_deterministes_valides`
compare les deux condensés et recopie leur valeur dans le journal.

**Règle de prise d'empreinte :** le condensé est cité dans le journal par
son **nom de variable** (`hash_run_A`, `hash_run_B`) et leur égalité est
affirmée par comparaison — jamais par recopie d'une valeur hexadécimale en
dur dans un test ou un document (hard-won rule 12).

### SC6 — Économie physique de la nourriture (principe 3)

Chaque `Cell` possède les champs suivants (conventions de nommage libres,
mais sémantique imposée) :
- un champ **stock de nourriture** (ex. `food_stock_kg`) : quantité
  disponible dans la cellule, en kilogrammes, valeur initiale ≥ 0,
  sentinelle `-1` pour « non calculé » (hard-won rule 8) ;
- un champ **ticks de faim** (ex. `hunger_ticks`) : nombre de pas de
  temps consécutifs sans nourriture suffisante, entier ≥ 0, sentinelle
  `-1` pour « non initialisé ».

La production de nourriture est calculée à partir des données de la cellule
(superficie, rendement paramétrique documenté dans `sim/SEEDING.md`) et
**écrite** dans le champ stock avant toute consommation.
La consommation est **lue** depuis ce même champ et **le modifie** — rien
ne se téléporte, rien n'est calculé hors du champ persisté.

### SC7 — Chaîne causale testée maillon par maillon ET de bout en bout

Chaque maillon de la chaîne causale est testé **unitairement** (un seul
maillon par test, état initial construit à la main) :

- **SC7a.** Production insuffisante → `food_stock_kg` baisse après un
  tick avec population > 0 et production < consommation.
- **SC7b.** `food_stock_kg` ≤ 0 → `hunger_ticks` progresse d'au moins 1
  après un tick.
- **SC7c.** `hunger_ticks` ≥ seuil → `population` diminue après un tick.
  Le seuil est lu depuis une constante documentée, jamais codé en ligne
  dans le test.

Un test d'**intégration de bout en bout** (SC7d) : une cellule avec une
production nulle (rendement = 0) et une population initiale > 0 finit par
avoir une population strictement inférieure après suffisamment de ticks.
Ce résultat doit émerger des états intermédiaires lus et écrits — le test
ne peut pas invoquer directement la règle de mort, il doit passer par le
tick complet.

### SC8 — Couverture d'écriture sur tous les champs du modèle (mode d'échec n°2)

Un test de couverture d'écriture (`test_write_coverage.py`) inspecte
chaque champ déclaré dans les dataclasses du modèle `sim/` et vérifie
qu'il existe au moins :
- un **site d'écriture** (un code qui affecte ce champ) ;
- un **site de lecture** (un code qui lit ce champ pour en déduire un
  effet).

Ce test est écrit pour aller **rouge** si un champ est déclaré sans
écrivain ou sans lecteur (mode d'échec n°2 de
`docs/rules/simulation-principles.md`).

Le compteur `champs_modele_couverts` est dérivé de ce test — il n'est pas
codé en dur dans le test lui-même.

### SC9 — Aucun compteur codé en dur (mode d'échec n°5)

Aucun agrégat de population, de stock ou de mortalité n'est une constante
littérale dans le code `sim/` (hors constantes nommées et documentées dans
`sim/SEEDING.md`). Un test d'inspection statique vérifie l'absence de
littéraux numériques non nommés dans les fonctions de calcul. Le compteur
`compteurs_en_dur_trouves` est produit par ce test et doit valoir 0.

### SC10 — Preuve rouge d'abord (hard-won rule 4)

La preuve se fait en deux étapes séquentielles, depuis deux états distincts
du code, et les sorties sont committées :

**SC10a — État sabotage :**
Depuis une **copie de travail sabotée** (un seul champ retiré de la
dataclass `Cell` — le champ `hunger_ticks`), le test de couverture
d'écriture de SC8 **échoue**. La sortie exacte de cette exécution est
sauvegardée sous `sim/tests/proof_red/run_sabotage.txt` (committé,
non git-ignoré).

La commande à exécuter pour produire ce fichier :
```
.venv/bin/python -m pytest sim/tests/test_write_coverage.py -v 2>&1 | tee sim/tests/proof_red/run_sabotage.txt
```
exécutée depuis la racine du dépôt, sur la copie sabotée.

**SC10b — État corrigé :**
Après restauration du champ, le même test **réussit**. La sortie est
sauvegardée sous `sim/tests/proof_red/run_correct.txt` (committé).

```
.venv/bin/python -m pytest sim/tests/test_write_coverage.py -v 2>&1 | tee sim/tests/proof_red/run_correct.txt
```

**Paire `must_differ_from` :**
`sim/tests/proof_red/run_sabotage.txt` et
`sim/tests/proof_red/run_correct.txt` constituent la paire déclarée dans
`deliverables/manifest.json` sous la clé `must_differ_from`. Le gate
`captures_differ_when_should` vérifiera que ces deux fichiers sont
différents.

### SC11 — Suite de tests entièrement verte

```
.venv/bin/python -m pytest sim/tests/ -v
```
depuis la racine du dépôt, doit se terminer sans erreur (`exit code 0`),
avec tous les tests collectés en état PASSED. Les fichiers
`proof_red/run_sabotage.txt` et `proof_red/run_correct.txt` ne font PAS
partie de la suite principale (ils sont des artefacts de preuve, pas des
tests à collecter).

### SC12 — `sim/README.md` mis à jour

`sim/README.md` décrit : le paquet et ses modules, la commande de
lancement des tests, la source des données (artefacts G3), et le fait
que ce stub était vide jusqu'au brief 011. Il ne paraphrase pas `VISION.md`
ni les ADR — il pointe vers eux.

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Implémenter le commerce inter-cellules.** La nourriture est produite,
   stockée et consommée dans la même cellule. Le transport entre cellules
   appartient à un brief ultérieur.
2. **Implémenter la couche Ville / Province / Pays.** Province est une
   agrégation dérivée ; aucun objet Province n'est créé ici.
3. **Implémenter les familles ou les personnes.** La population est un
   agrégat par cellule à ce stade — pas encore une collection d'individus.
4. **Inventer des données historiques.** L'amorçage est paramétrique et
   documenté. Toute revendication d'utilisation de données historiques
   non présentes dans les artefacts G3 est hors périmètre.
5. **Modifier `pipeline/geo/`**, `unity/`, `harness/`, `docs/adr/` ou
   tout autre sous-système hors `sim/`.
6. **Modifier `VISION.md`.**
7. **Rapporter un compteur issu d'un monde vide ou non chargé comme une
   vraie mesure.** Tout compteur nécessite que le monde soit effectivement
   chargé ; un test sur une dataclass vide n'est pas une mesure du chargement.

## Required Counters

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `cells_chargees` | tableau `cells` de `cells_g3.json` compté lors du chargement | valeur de `cell_count` lue dans `stats_g3.json` |
| `aretes_adjacence_chargees` | tableau `adjacency` de `adjacency_g3.json` compté lors du chargement | longueur totale du tableau `adjacency` |
| `champs_modele_couverts` | résultats du test `test_write_coverage.py` sur les dataclasses de `sim/` | nombre total de champs déclarés dans les dataclasses du modèle |
| `compteurs_en_dur_trouves` | inspection statique du code `sim/` par le test SC9 | 0 (valeur attendue — un écart est un échec) |
| `amorçage_deterministe_valide` | comparaison des états initiaux de deux runs avec la même graine | 1 comparaison (résultat : identique ou non, valeur booléenne) |
| `ticks_deterministes_valides` | comparaison des condensés SHA256 de l'état du monde après N ticks (N ≥ 10) sur deux runs avec la même graine | 1 comparaison (les deux condensés sont affichés et comparés) |
| `maillons_chaine_causale_testes_unitairement` | tests unitaires de SC7a, SC7b, SC7c comptés dans `test_causal_chain.py` | 3 maillons attendus |
| `test_integration_bout_en_bout_resultat` | résultat du test SC7d : `population_finale < population_initiale` pour une cellule à production nulle | 1 test, résultat PASS ou FAIL |
| `lignes_differentes_preuve_rouge` | diff ligne à ligne entre `proof_red/run_sabotage.txt` et `proof_red/run_correct.txt` | nombre de lignes différant entre les deux fichiers (doit être > 0) |

Chaque compteur est accompagné dans le journal du Générateur de la commande
réelle qui l'a produit et de la valeur obtenue. Un chiffre sans commande
n'est pas un compteur (hard-won rule 3).

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur exigée |
|---|---|---|
| « les artefacts G3 ne sont pas lisibles depuis ce chemin » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/cells_g3.json'))"` depuis la racine | le message d'erreur Python exact (FileNotFoundError, JSONDecodeError ou équivalent) |
| « une dépendance tierce est requise pour le moteur `sim/` » | `.venv/bin/python -c "import sim"` depuis la racine | le message ImportError exact, avec le nom du module manquant |
| « la sérialisation canonique de l'état ne peut pas être déterministe » | `.venv/bin/python -c "import sim; w=sim.world.World.from_g3(); import hashlib, json; print(hashlib.sha256(json.dumps(w.to_dict(), sort_keys=True).encode()).hexdigest())"` deux fois | le texte exact des deux hachés, ou le message d'erreur si l'appel échoue |

Aucune autre dérogation n'est recevable. En particulier :
- « les données historiques de population sont indisponibles » **n'est pas une
  dérogation** : l'amorçage paramétrique documenté dans `sim/SEEDING.md` est
  précisément prévu pour ce cas. Invoquer l'absence de données historiques
  sans proposer l'amorçage paramétrique est une abdication (hard-won rule 9).
- « je n'ai pas pu faire la preuve rouge d'abord » **n'est pas une
  dérogation** : SC10 est inatteignable sans elle.

## Execution Contract

- Ce brief couvre un seul sous-système (`sim/`) et produit des livrables
  chacun vérifiable indépendamment. Estimation : **≤ 120 appels d'outils**
  (ancre : un brief ADR = 108 ; ce brief est plus large mais reste dans un
  seul sous-système Python sans Unity). Si le budget atteint 130, écrire un
  checkpoint et s'arrêter ; le gate dur est à 160.

  Commande de vérification pré-génération :
  ```
  .venv/bin/python harness/budget.py split-check --brief harness/queue/briefs/011-sim-monde-vivant-amorcage --estimated-calls 120
  ```

- Aucune étape Unity. Ce brief n'invoque pas `unity/run-unity.ps1`.

- **Fichiers dans `deliverables/manifest.json` : tous sous version control.**
  `.gitignore` exclut `*.log` et `unity/game_unity/Logs/`. Les preuves
  d'exécution sont des fichiers `.txt` committés, jamais des `.log`.
  En particulier : `sim/tests/proof_red/run_sabotage.txt` et
  `sim/tests/proof_red/run_correct.txt` sont committés.

- **Paire `must_differ_from`** déclarée dans `deliverables/manifest.json` :
  ```json
  "must_differ_from": [
    ["sim/tests/proof_red/run_sabotage.txt",
     "sim/tests/proof_red/run_correct.txt"]
  ]
  ```

- **Fin de lot.** Le gate mécanique doit répondre ACCEPT :
  ```
  .venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage
  ```
  et la suite complète doit être verte :
  ```
  .venv/bin/python -m pytest harness/tests/ -q
  .venv/bin/python -m pytest sim/tests/ -v
  ```
  Les deux sorties réelles sont recopiées dans le journal — pas seulement
  déposées dans un fichier annexe.

- **Qui produit, qui juge.** Celui qui produit ce lot n'écrit pas son
  verdict.
