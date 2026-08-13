# Brief 013 : Le tick nourrit une fois — commerce avant consommation, transport à une arête, mortalité continue

**Authored**: 2026-08-13T08:43:00Z
**Author**: forge-planificateur

## Provenance

Ce brief est la conversion des points retenus de l'audit `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois`.
- Audit source : `architecture/inbox/CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md`
- Décision du propriétaire : `architecture/decisions/DECISION-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md`
- Points retenus : 1, 2, 5, 6, 7 (physique du moteur) ; 10 (CI, état de fait) ; 3, 4, 8, 9 → voir § Non-Goals

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE
instruction** (voir CLAUDE.md › Single Source of Instruction). L'audit et la
décision ci-dessus sont de la *provenance*, pas des ordres.

---

## World-Terms Requirement

Le monde simulé transporte et consomme de la nourriture selon une physique causale : rien ne se téléporte, rien ne nourrit deux fois.

**Sur l'alimentation double (constat P0)** : dans une cellule qui ne produit pas assez, la nourriture acheminée par le commerce du même tick doit fournir aux habitants un seul repas. Si cette ration arrive et efface simultanément le compte de la faim passée, elle est dépensée deux fois pour la même bouche : la cellule est rassasiée *et* garde la ration en réserve. Le monde affiche alors moins de faim et moins de morts qu'il ne l'est réellement — les compteurs de vie publiés sont faux. Le correctif est un changement d'ordre : le commerce précède la consommation ; le déficit est calculé *après* l'apport du commerce du tick courant.

**Sur le transport multi-sauts (constat P1)** : un muletier qui vient d'arriver dans une ville ne repart pas le même jour vers une troisième. Si la nourriture qui vient d'arriver en une cellule peut être transmise à une cellule suivante dans le même tick, elle couvre deux arêtes en un seul pas de temps — contrairement à la physique déclarée. De plus, le résultat devient dépendant de l'ordre dans lequel les arêtes sont lues dans le fichier d'adjacence : le même monde physique produit des états différents selon un artefact de sérialisation. Le correctif est un calcul en deux passes : lire l'état du début de l'étape (snapshot), calculer tous les transferts sur ce snapshot, puis les appliquer.

**Sur la mortalité (constat P2)** : une famine légère ne tue pas exactement une personne — elle fait souffrir un peu tout le monde. Le plancher `max(1, ...)` réintroduit l'interrupteur binaire « tout déficit, même un gramme, cause au moins un mort », et fait dépasser le plafond documenté pour les petites populations. Par ailleurs, une seule journée d'abondance n'efface pas des mois de famine accumulés : la récupération est graduelle, pilotée par une vitesse documentée.

**Sur le compteur de transport (constat P2)** : un kilogramme qui traverse deux arêtes dans le même tick compte deux fois dans le total. Avec le transport à une arête, cette double comptabilisation disparaît par construction.

---

## Success Conditions

### SC1 — Le commerce précède la consommation ; un kilogramme transfère nourrit exactement une fois

**Ordre du tick** : la fonction `tick(world, rng)` exécute les maillons dans l'ordre suivant :
1. Production (`_apply_production`) — pour chaque cellule
2. Commerce (`_apply_commerce`) — sur le monde entier
3. Consommation (`_apply_consumption`) — pour chaque cellule
4. Faim (`_update_hunger`) — pour chaque cellule
5. Mortalité (`_apply_mortality`) — pour chaque cellule

Le maillon commerce (`_apply_commerce`) **ne modifie pas** `food_deficit_kg` : il ne touche que `food_stock_kg`. La décrémentation `cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)` est retirée. Le champ `food_deficit_kg` n'est plus écrit que par `_apply_consumption`.

**Test d'unicité de l'alimentation** (compteur `ecart_stock_temoin_vs_receveuse`) :

Construire un micro-monde à deux sous-expériences indépendantes, sans variabilité de rendement (surfaces nulles `area_km2=0.0`, ou toutes cellules productives mises à part) :

- *Cellule témoin* : possède au départ exactement `besoin_kg = population × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, aucun déficit antérieur, aucune adjacence active.
- *Cellule receveuse* : possède au départ un stock nul et un déficit `food_deficit_kg = besoin_kg`, reliée à une source en surplus suffisant (stock ≥ `besoin_kg`, pas de déficit), adjacence unique. La source dispose exactement de `besoin_kg` de surplus disponible.

Après un tick complet (production désactivée par `area_km2=0.0`), les deux cellules doivent terminer dans **le même état** : même `food_stock_kg`, même `food_deficit_kg`. L'écart absolu de stock doit être ≤ 1×10⁻⁹ kg.

```
# commande de mesure du compteur (depuis la racine)
.venv/bin/python -m pytest sim/tests/test_tick_nourrit_une_fois.py::test_ecart_temoin_vs_receveuse -v
```

Résultat attendu : PASSED.

---

### SC2 — Le transport franchit exactement une arête par tick ; l'état final est invariant à l'ordre des arêtes

**Calcul en deux passes** : `_apply_commerce` prend un snapshot (dict immuable) de l'état `{cell_id: (food_stock_kg, food_deficit_kg)}` de toutes les cellules *avant* toute modification. Tous les transferts sont calculés à partir de ce snapshot, puis appliqués en une seule passe à la fin. Une cellule qui reçoit sur une arête ne peut pas redonner sur une autre arête pendant le même appel à `_apply_commerce`.

**Allocation déterministe quand plusieurs voisins demandent au même excédent** : si plusieurs cellules en déficit sont adjacentes à la même source, l'allocation est proportionnelle à leurs déficits (pris dans le snapshot) et traitée dans l'ordre stable des `cell_id` croissants. Cette règle est documentée dans `sim/SEEDING.md` (nouvelle section SC2 brief 013).

**Test chaîne 1—2—3** (compteur `cellule_3_stock_apres_1_tick_chaine_1_2_3`) :

Monde à trois cellules : seule la cellule 1 a du stock ; les cellules 2 et 3 sont en déficit ; la cellule 3 **n'est pas** adjacente à la cellule 1 (arêtes : uniquement 1–2 et 2–3). Après un tick complet (production désactivée par `area_km2=0.0` ou stock initial fixé, `rng` déterministe), `world.cells[3].food_stock_kg` doit être exactement `0.0`.

**Test d'invariance d'ordre** (compteur `etat_final_invariant_ordre_aretes`) :

Même monde à trois cellules, simulé deux fois : une fois avec les arêtes dans l'ordre `[1-2, 2-3]`, une fois dans l'ordre `[2-3, 1-2]`. Les stocks finaux des trois cellules doivent être identiques dans les deux exécutions (écart ≤ 1×10⁻⁹ kg pour chaque cellule).

```
.venv/bin/python -m pytest sim/tests/test_tick_nourrit_une_fois.py::test_chaine_1_2_3 sim/tests/test_tick_nourrit_une_fois.py::test_invariance_ordre_aretes -v
```

Résultats attendus : PASSED pour les deux.

---

### SC3 — Le seuil de survie est dérivé analytiquement et peut échouer

**Calcul analytique de la capacité de charge** :

Le moteur est à l'équilibre malthusien quand la production par km² couvre la consommation par km². La fraction de survie analytiquement prédite est :

```
rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
capacite_charge_hab_km2 = (FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × rendement_moyen)
                          / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
fraction_predite = capacite_charge_hab_km2 / INITIAL_POPULATION_PER_KM2
```

Avec les constantes actuelles : `fraction_predite = (18.0 × 1.0) / (2.0 × 10.0) = 0.9`. Cette valeur est de l'arithmétique, pas un comportement émergent.

**Nouvelle constante `SURVIE_MARGE_DERIVEE`** : valeur dans (0.0, 0.5), documentée dans `sim/SEEDING.md` avec justification (écart attendu entre la prédiction déterministe et la mesure stochastique sur N=200 ticks). Le Générateur choisit cette marge de façon à ce que le test de SC6 passe sur les valeurs re-mesurées après corrections SC1/SC2 — mais ne calibre pas la marge *après* avoir mesuré (cf. échecs disqualifiants).

**`SEUIL_SURVIE_POPULATION_FRACTION`** est recalculé dans `sim/constants.py` comme :
```py
SEUIL_SURVIE_POPULATION_FRACTION = fraction_predite - SURVIE_MARGE_DERIVEE
```
où `fraction_predite` est calculé à partir des autres constantes (pas une valeur en dur). La formule de dérivation est documentée dans `sim/SEEDING.md`.

**Test du seuil dérivé** (compteur `fraction_dans_marge_predite`) :

Un test vérifie que la fraction de survie mesurée sur N=200 ticks, graines 42/42, est dans la fenêtre `[fraction_predite - SURVIE_MARGE_DERIVEE, fraction_predite + SURVIE_MARGE_DERIVEE]`. Ce test est conçu pour **pouvoir échouer** : si les constantes changent de régime (par exemple `INITIAL_POPULATION_PER_KM2` doublée → `fraction_predite = 0.45`), le test rougit sans toucher à `SURVIE_MARGE_DERIVEE`.

```
.venv/bin/python -m pytest sim/tests/test_survie_derivee.py::test_fraction_dans_marge -v
```

---

### SC4 — Mortalité continue et plafonnée pour toute population ; déficit à mémoire graduelle

**Retrait du plancher de mortalité** : la ligne `deaths = max(1, int(population × death_rate))` est remplacée par `deaths = int(population × death_rate)`. La mortalité peut être zéro pour un déficit insignifiant. Aucune mort n'est garantie par le seul fait qu'un déficit est non nul.

**Plafond tenu pour toute population** : pour toute cellule avec `population ≥ 1` et `food_deficit_kg > 0`, le taux effectif `deaths / population` est ≤ `MAX_DEATH_RATE_PER_TICK`. Il n'existe plus de valeur de `(population, food_deficit_kg)` pour laquelle le plafond est dépassé. (Compteur `max_taux_mortalite_effectif_pop_1`).

**Déficit à mémoire graduelle** : une nouvelle constante `DEFICIT_RECOVERY_RATE_PER_TICK` (valeur dans (0.0, 1.0), documentée dans `sim/SEEDING.md`) pilote la vitesse de récupération. Lorsqu'une cellule est en surplus (consommation couverte dans `_apply_consumption`), le déficit accumulé est réduit de façon graduelle :

```py
cell.food_deficit_kg = max(0.0, cell.food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))
```

(ou formule équivalente au choix du Générateur — le critère est : la constante est nommée, documentée dans SEEDING.md, et un seul tick d'excédent ne peut pas effacer un déficit accumulé dépassant `DEFICIT_RECOVERY_RATE_PER_TICK × food_deficit_kg`).

**Test du plafond** (compteur `max_taux_mortalite_effectif_pop_1`) :

Pour chaque population dans `[1, 5, 9, 20, 100, 1000]` et déficit égal à `1e-9` kg : vérifier que `deaths / population ≤ MAX_DEATH_RATE_PER_TICK`. Aucun appel à `_apply_mortality` ne dépasse le plafond.

**Test de mémoire graduelle** (compteur `deficit_non_efface_en_1_tick`) :

Construire une cellule avec `food_deficit_kg = D` (grand, par exemple `D = 10 000 kg`). Lui fournir un surplus important (stock > consommation) et appeler `_apply_consumption`. Le déficit résiduel après un tick doit être `max(0, D × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))` > 0. C'est-à-dire : la récupération est strictement partielle pour un déficit non nul.

```
.venv/bin/python -m pytest sim/tests/test_mortalite_continue.py -v
```

---

### SC5 — Le compteur de transport mesure des kilogrammes arrivés, pas des sauts

Avec le transport atomique (SC2), chaque kilogramme traverse au plus une arête par tick. Le compteur `kg_transportes_monde_reel_re` (accumulateur `total_transported` dans `tick()`) est donc identique à la somme des variations positives de `food_stock_kg` pendant l'étape commerce :

```
ecart_kg_transportes_vs_arrives = |total_transported - somme_deltas_positifs_stock_commerce|
```

Ce compteur doit être ≤ 1×10⁻⁹ kg, vérifié par un test sur un monde à plusieurs cellules non trivial (au moins 3 cellules, au moins 2 arêtes actives).

```
.venv/bin/python -m pytest sim/tests/test_kg_transportes_est_arrives.py -v
```

---

### SC6 — Re-mesure complète du monde réel après corrections

Les corrections SC1 et SC2 **changent les valeurs** des compteurs du brief 012. Les nouvelles valeurs sont mesurées après ces corrections, committées dans les livrables du présent brief, et ne remplacent nulle part les valeurs du brief 012 (archives intangibles).

Un script reproductible est committé sous `harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py`. Exécuté depuis la racine avec :

```
.venv/bin/python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
```

il produit les quatre compteurs suivants sur `World.from_g3(rng_seed=42)`, `random.Random(42)`, N=200 ticks :

| compteur | source de l'échantillon | dénominateur | condition |
|---|---|---|---|
| `cellules_affamees_monde_reel_re` | cellules ayant eu `hunger_ticks > 0` à au moins un tick | 596 cellules chargées par G3 | **> 0** |
| `morts_cumules_monde_reel_re` | population totale initiale − population totale finale | population totale initiale dérivée du chargement | **> 0** |
| `kg_transportes_monde_reel_re` | accumulateur `total_transported` sur tous les ticks | 1 364 arêtes × 200 ticks | **> 0** |
| `fraction_survie_monde_reel_re` | `population_finale / population_initiale` | population totale initiale | **> `SEUIL_SURVIE_POPULATION_FRACTION`** dérivé (SC3) |

Les quatre conditions doivent être satisfaites simultanément sur les valeurs re-mesurées. La commande exacte produisant chaque valeur est recopiée dans le journal du Générateur (hard-won rule 3).

---

### SC7 — Tests du brief 012 adaptés ; suite complète verte

**Identification et adaptation** : les tests de `sim/tests/` qui encodent l'ancien ordre du tick (production → consommation → commerce) ou l'ancienne sémantique du commerce (`food_deficit_kg` décrémenté dans `_apply_commerce`) doivent être identifiés. Chaque adaptation ou suppression est motivée test par test dans `deliverables/generator-log.md` (section dédiée « Adaptation des tests du brief 012 »). Une adaptation silencieuse (retrait d'un test sans motivation écrite) est un échec disqualifiant.

Téléchargement attendu : au minimum `sim/tests/test_causal_chain.py` doit voir son commentaire SC7c corrigé (le commentaire cite actuellement `max(1, int(…)) = 2` alors que la nouvelle formule supprime le `max(1, …)`). Le test lui-même peut rester valide si la logique passe encore avec les nouvelles constantes.

**Suite complète verte** :

```
.venv/bin/python -m pytest sim/tests/ -v
.venv/bin/python -m pytest harness/tests/ -q
```

Les deux commandes doivent s'achever sans `FAILED`. Les `SKIP` attendus sur Linux (tests Unity) restent acceptés.

---

### SC8 — Registre de coût

Une ligne est ajoutée en fin de `harness/queue/cost-ledger.jsonl` via :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois \
  --event generator-run \
  --audit-id CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
```

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Traiter les constats 3 et 9 de l'audit** (acteur faux au ledger, comptage des verdicts sur tout le texte) ni les points de harnais différés de `CURSOR-3b47ffe` (points 1 et 7) : ces défauts appartiennent au brief `014-pipeline-contre-audit-porte` (graine déjà en file). Ce brief 013 ne touche pas `harness/*.py`, `harness/pipeline/`, `architecture/`.

2. **Traiter le constat 4** (absence de maillon indépendant dans la chaîne de vérification) : il s'agit d'une question de gouvernance portée au propriétaire, pas d'un objet de code.

3. **Modifier les constantes de calibration existantes** (`FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, `INITIAL_POPULATION_PER_KM2`, `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`, `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK`) : elles sont déclarées et assumées dans `sim/SEEDING.md`. Elles ne doivent pas être retouchées pour masquer les changements de compteurs — les compteurs changeront légitimement à cause des corrections SC1/SC2.

4. **Retoucher les archives du brief 012** : `harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/` (brief.md, eval-rubric.md, verdict.md, manifest.json, preuves) sont intangibles. Les nouvelles valeurs mesurées vivent uniquement dans le présent dossier brief 013 et ses livrables.

5. **Implémenter natalité, migration, prix, marchés, Province, villes** : hors périmètre.

6. **Modifier `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, `.github/workflows/`** : la CI `sim-tests` existe déjà ; aucune modification des workflows n'est requise.

7. **Rapporter un compteur SC6 depuis un monde construit à la main ou depuis zéro cellule** : tout compteur exigeant le monde réel est mesuré sur les 596 cellules effectivement chargées par `World.from_g3()`.

8. **Recalibrer `SEUIL_SURVIE_POPULATION_FRACTION` ou `SURVIE_MARGE_DERIVEE` après avoir mesuré la fraction re-mesurée** : la marge est justifiée avant la mesure (rapport production/consommation/densité → dispersion stochastique attendue), pas après (cf. échecs disqualifiants).

---

## Required Counters

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `ecart_stock_temoin_vs_receveuse` | cellule_témoin vs cellule_receveuse construites à la main, 1 tick complet, production désactivée (`area_km2=0`) | 1 comparaison ; doit être ≤ 1×10⁻⁹ kg |
| `cellule_3_stock_apres_1_tick_chaine_1_2_3` | monde 3 cellules, arêtes [1-2, 2-3], production désactivée, 1 tick | 1 valeur ; doit être = 0.0 |
| `etat_final_invariant_ordre_aretes` | même monde 3 cellules, exécuté avec ordre d'arêtes [1-2, 2-3] et avec [2-3, 1-2] | 3 comparaisons (une par cellule) ; chaque écart ≤ 1×10⁻⁹ kg |
| `fraction_predite_analytique` | calculé depuis `sim/constants.py` par la formule `(FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × rendement_moyen) / (FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK × INITIAL_POPULATION_PER_KM2)` | 1 valeur ; doit être dans (0.0, 1.0) |
| `fraction_dans_marge_predite` | `World.from_g3(rng_seed=42)`, `random.Random(42)`, N=200 ticks | fraction sur 596 cellules ; doit être dans `[fraction_predite - SURVIE_MARGE_DERIVEE, fraction_predite + SURVIE_MARGE_DERIVEE]` |
| `max_taux_mortalite_effectif_pop_1` | populations [1, 5, 9, 20, 100, 1000], déficit = 1e-9 kg, appel `_apply_mortality` | 6 appels ; taux effectif = deaths/population ≤ MAX_DEATH_RATE_PER_TICK dans tous les cas |
| `deficit_non_efface_en_1_tick` | cellule construite avec `food_deficit_kg = 10 000 kg`, 1 tick avec surplus | 1 comparaison ; déficit résiduel > 0 |
| `ecart_kg_transportes_vs_arrives` | monde ≥ 3 cellules, ≥ 2 arêtes actives, N ticks | `|total_transported − somme_deltas_positifs|` ; doit être ≤ 1×10⁻⁹ kg |
| `cellules_affamees_monde_reel_re` | `World.from_g3(rng_seed=42)`, `random.Random(42)`, N=200 ticks ; cellules ayant eu `hunger_ticks > 0` | 596 cellules chargées ; **> 0** |
| `morts_cumules_monde_reel_re` | même simulation ; population initiale − finale | population totale initiale dérivée du chargement ; **> 0** |
| `kg_transportes_monde_reel_re` | même simulation ; accumulateur `total_transported` | 1 364 arêtes × 200 ticks ; **> 0** |
| `fraction_survie_monde_reel_re` | même simulation ; `pop_finale / pop_initiale` | population totale initiale ; **> `SEUIL_SURVIE_POPULATION_FRACTION`** |
| `ci_sim_tests_collectes_013` | `.venv/bin/python -m pytest sim/tests/ --collect-only -q` depuis la racine | nombre de tests collectés ; **> 0** |

---

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois` | la sortie contient la chaîne `UNMEASURABLE` |
| « les artefacts G3 d'adjacence ne sont pas lisibles depuis ce chemin » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g3.json'))"` depuis la racine | le message d'erreur Python exact (FileNotFoundError ou équivalent) |
| « le moteur `sim/` requiert une dépendance tierce » | `.venv/bin/python -c "import sim"` depuis la racine | le message ImportError exact, avec le nom du module manquant |

Aucune autre dérogation n'est recevable. En particulier :
- « Il est impossible de respecter le plafond MAX_DEATH_RATE_PER_TICK sans le plancher max(1, …) » **n'est pas une dérogation** : le plancher n'est pas nécessaire pour le plafond — ce sont deux mécanismes indépendants. Un taux de zéro respecte le plafond.
- « La fraction de survie re-mesurée est inférieure au seuil analytique » **n'est pas une dérogation** : si la fraction mesurée sort de la fenêtre `[fraction_predite ± SURVIE_MARGE_DERIVEE]`, c'est une information sur le monde simulé, pas une impossibilité. La marge doit être justifiée avant la mesure.
- « La remesure SC6 est trop coûteuse à rejouer » **n'est pas une dérogation** : le script `measure_sc6_013.py` est committé et reproductible, comme `measure_cellules_affamees.py` l'a été pour le brief 012.

---

## Execution Contract

### Périmètre autorisé

Ce brief couvre exclusivement :
- `sim/engine.py`, `sim/constants.py`, `sim/SEEDING.md` (fichiers moteur existants)
- `sim/tests/` (nouveaux tests + adaptations motivées des tests 012)
- `sim/tests/proof_red/` (preuves rouges nouvelles — voir ci-dessous)
- `harness/queue/briefs/013-sim-tick-nourrit-une-fois/` (livrables du présent lot)
- `harness/queue/cost-ledger.jsonl` (ajout d'une seule ligne en fin de fichier, SC8)

Fichiers interdits : tout fichier sous `harness/*.py`, `harness/pipeline/`, `architecture/`, `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, `.github/workflows/`, et tout fichier sous `harness/queue/briefs/012-*/`.

### Estimation d'appels d'outils

**Estimation : 125 appels.** Ancres : brief 012 (sous-système `sim/` de zéro, 5 maillons nouveaux) a utilisé ~120 outils. Le présent brief touche un sous-système déjà peuplé — les fichiers existent, les constantes existent — avec 5 défauts corrélés. Corrections principales : ordre du tick (1 fichier), commerce atomique (1 fichier), mortalité (1 fichier), 2-3 nouveaux fichiers de test, 2 paires de preuves rouges, 1 script de mesure, 1 manifest, 1 log. La re-mesure sur le monde réel exige 200 ticks sur 596 cellules (rapide). Plafond dur : 160 appels ; checkpoint obligatoire à 130.

Commande de vérification pré-génération (à exécuter avant tout travail de fond) :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois \
  --estimated-calls 125
```

Le Générateur déclare dans son journal, avant de commencer le travail de fond, soit la valeur mesurée du budget, soit la dérogation `UNMEASURABLE` (avec la sortie de `harness/budget.py status` à l'appui).

### Preuve rouge d'abord (hard-won rule 4) — deux paires obligatoires

Chaque paire est produite depuis une copie de travail sabotée hors du dépôt. Les sorties sont committées sous `sim/tests/proof_red/` (`.txt`, jamais `.log`).

**Paire A — sabotage « ordre du tick inversé » :**
- Sabotage : dans la copie hors dépôt, remettre `_apply_commerce` après `_apply_consumption` dans `tick()` (ancien ordre du brief 012).
- Test affecté : `test_tick_nourrit_une_fois.py::test_ecart_temoin_vs_receveuse`.
- `sim/tests/proof_red/run_ordre_tick_red.txt` : sortie avec le sabotage → doit contenir au moins un `FAILED`.
- `sim/tests/proof_red/run_ordre_tick_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

**Paire B — sabotage « transferts appliqués au fil de la boucle » :**
- Sabotage : dans la copie hors dépôt, retirer le mécanisme de snapshot et appliquer les transferts en place dans la boucle (ancien comportement de `_apply_commerce`).
- Test affecté : `test_tick_nourrit_une_fois.py::test_invariance_ordre_aretes`.
- `sim/tests/proof_red/run_transport_atomique_red.txt` : sortie avec le sabotage → doit contenir au moins un `FAILED`.
- `sim/tests/proof_red/run_transport_atomique_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

Forme `must_differ_from` dans `deliverables/manifest.json` : **par fichier** (forme lue par `harness/verdict_audit.py`, lignes 203-213) :

```json
{
  "path": "../../../../sim/tests/proof_red/run_ordre_tick_green.txt",
  "must_differ_from": "../../../../sim/tests/proof_red/run_ordre_tick_red.txt"
}
```

(idem pour la paire B). Les quatre fichiers de preuve sont committés avant l'écriture du journal.

### Interdictions pour le Générateur

- Ne pas committer, ne pas pousser, ne pas créer de branche.
- Ne pas modifier `brief.md`, `eval-rubric.md` ni `verdict.md`.
- Jamais `python` nu — toujours `.venv/bin/python`.
- Ne pas recopier de valeur hexadécimale de condensé SHA256 (hard-won rule 12).
- Ne pas supprimer un test du brief 012 sans motivation écrite dans le journal.
- Ne pas retoucher les archives du brief 012.
- Ne pas recalibrer `SURVIE_MARGE_DERIVEE` ou `DEFICIT_RECOVERY_RATE_PER_TICK` après avoir vu les valeurs mesurées (SC3/SC4 doivent être justifiées avant la mesure).

### Fin de lot

Le gate mécanique doit répondre `ACCEPT` :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
```

La suite complète doit être verte :

```
.venv/bin/python -m pytest harness/tests/ -q
.venv/bin/python -m pytest sim/tests/ -v
```

Les deux sorties réelles sont recopiées dans le journal — pas seulement déposées dans un fichier annexe.

**Celui qui produit ne prononce pas la recevabilité.**

---

_Amendement de forme — 2026-08-13T09:31:00Z : balises de blocs de code remplacées (py au lieu de la balise de langage complète) pour corriger un faux positif du détecteur no_bare_python_alias. Aucun changement de fond._
