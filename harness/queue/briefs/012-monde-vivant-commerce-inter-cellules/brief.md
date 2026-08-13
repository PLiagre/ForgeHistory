# Brief 012 : Le monde vivant vit — base de temps, équilibre alimentaire mesuré et commerce inter-cellules

**Authored**: 2026-08-13T06:27:00Z
**Author**: forge-planificateur

## Provenance

Ce brief est la conversion des points retenus de l'audit `CURSOR-3b47ffe-pr57-monde-sans-faim`.
- Audit source : `architecture/inbox/CURSOR-3b47ffe-pr57-monde-sans-faim.md`
- Décision du propriétaire : `architecture/decisions/DECISION-CURSOR-3b47ffe-pr57-monde-sans-faim.md`
- Points retenus : 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE
instruction** (voir CLAUDE.md › Single Source of Instruction). L'audit et la
décision ci-dessus sont de la *provenance*, pas des ordres.

---

## World-Terms Requirement

Le monde simulé est composé de cellules géographiques (unités spatiales de base, identifiées par `cell_id`). Chaque cellule produit de la nourriture par tick — un « tick » est la plus petite unité de temps du moteur, dont la durée est définie en un seul endroit documenté. Le rendement agricole varie d'un tick à l'autre selon une source d'aléa fournie à l'appelant (`rng: random.Random`) ; cette variabilité est physiquement causée par les fluctuations climatiques et agronomiques saisonnières.

Cette nourriture est stockée dans la cellule. La population consomme du stock. Lorsque la consommation dépasse le stock disponible, le manque est mesuré en kilogrammes et **accumulé** dans un champ persisté de la cellule : rien ne disparaît sans être compté. Lorsque le déficit accumulé est suffisant, des habitants meurent — en nombre proportionnel à l'ampleur et à la durée du manque, jamais selon un interrupteur binaire (`si hunger_ticks ≥ seuil alors −N%`).

La nourriture se déplace aussi entre cellules voisines (reliées par les arêtes d'adjacence géographique chargées depuis `pipeline/geo/artifacts/adjacency_g3.json`). Ce déplacement part d'une cellule en excédent vers une cellule en déficit, en quantité bornée par la capacité physique de transport de l'arête par tick, et sans créer ni détruire de nourriture : la somme des stocks est inchangée par la seule étape de transport.

Pour que tous ces maillons soient cohérents entre eux, la durée d'un tick est définie **en un seul endroit** (une constante nommée dans `sim/constants.py`), et toutes les constantes temporelles du moteur sont exprimées dans cette même durée, avec leur justification explicite dans `sim/SEEDING.md`.

---

## Success Conditions

### SC1 — Base de temps unique, constantes alignées, noms corrigés

Une constante nommée (par exemple `TICK_DURATION_DAYS`) est déclarée dans `sim/constants.py` et précise la durée d'un tick en jours. Sa valeur est documentée dans `sim/SEEDING.md` avec une justification sourcée (proxy paramétrique, non inventé — conformément à la hard-won rule 10 : l'absence de données ne s'invente pas en silence).

Toutes les constantes temporelles du moteur (`FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, la constante de réserve initiale) sont recalculées à partir de `TICK_DURATION_DAYS` et documentées dans `sim/SEEDING.md` avec leur dérivation explicite.

Les noms trompeurs identifiés dans l'audit (constat P3-2) sont corrigés :
- `INITIAL_FOOD_DAYS` (nom « jours » pour une valeur en ticks) est renommé pour supprimer l'ambiguïté d'unité.
- La variable locale `daily_need` dans `sim/world.py` est renommée pour refléter l'unité réelle (ex. `tick_need`).

Condition vérifiable :

```
.venv/bin/python -c "from sim.constants import TICK_DURATION_DAYS; assert TICK_DURATION_DAYS > 0; print('tick =', TICK_DURATION_DAYS, 'jour(s)')"
```

s'exécute sans erreur et affiche une valeur strictement positive.

### SC2 — La production varie réellement par tick (le rng est consommé)

Le `rng: random.Random` fourni à `tick(world, rng)` est effectivement consommé à chaque tick. Par exemple, le rendement de chaque cellule est multiplié par un facteur tiré de `rng` (distribution et paramètres documentés dans `sim/SEEDING.md`). L'état interne du générateur change après chaque appel à `tick()`.

Trois propriétés sont vérifiées par des tests distincts :

1. **État du rng modifié** : `rng.getstate()` avant dix ticks sur `World.from_g3(rng_seed=42)` est différent de `rng.getstate()` après. Le compteur `rng_etat_change_apres_tick` documente ce résultat.

2. **Déterminisme à graine fixe** : deux runs de N = 200 ticks avec la même graine world et la même graine rng produisent des états de monde dont les condensés SHA256 sont égaux. Les condensés sont cités dans le journal par leurs **noms de variables** (`hash_run_A`, `hash_run_B`) et leur égalité est affirmée par comparaison — jamais par recopie d'une valeur hexadécimale en dur (hard-won rule 12).

3. **Sensibilité à la graine rng** : deux runs de N = 200 ticks avec la même graine world mais des graines rng différentes (ex. 42 et 999) produisent des condensés différents, et cet écart provient du chemin du tick (pas uniquement de l'amorçage).

La variabilité ainsi introduite, combinée à la calibration de SC1, doit créer des déficits locaux réels sur le monde réel — condition vérifiée par SC5.

### SC3 — Le déficit alimentaire est un état persisté

Un nouveau champ `food_deficit_kg: float` (sentinelle `-1.0` = non encore calculé, hard-won rule 8 : zéro peut être une mesure réelle) est déclaré sur `Cell` dans `sim/model.py`.

**Sémantique imposée** :
- Lorsque la consommation d'une cellule dépasse son stock disponible (après l'éventuel apport du commerce du tick courant), le manque en kilogrammes est **ajouté** à `food_deficit_kg`. La ligne `remaining if remaining >= 0.0 else 0.0` du brief 011 est remplacée : le manque est compté, non écrasé.
- Lorsque la cellule dispose d'un surplus (stock suffisant après consommation), `food_deficit_kg` est remis à zéro.
- La mortalité **lit `food_deficit_kg`** et en émerge : une cellule avec un déficit plus profond ou plus durable perd une fraction de population plus grande qu'une cellule légèrement en manque. L'interrupteur binaire `if hunger_ticks >= HUNGER_DEATH_THRESHOLD` seul est interdit — la mortalité doit être une fonction croissante de l'ampleur du déficit accumulé.

La formule de mortalité est documentée dans `sim/SEEDING.md` avec sa justification paramétrique.

### SC4 — Commerce inter-cellules physique (les arêtes sont enfin lues)

`World.adjacency` — les 1364 arêtes chargées depuis `pipeline/geo/artifacts/adjacency_g3.json` — est **lu** par le moteur. Un maillon commerce est ajouté au tick et s'applique après l'étape de production/consommation locale :

- Pour chaque arête, si une cellule voisine a un excédent et que l'autre est en déficit (`food_deficit_kg > 0`), une quantité de nourriture est transférée de la cellule source vers la cellule destination.
- La quantité transférée par arête et par tick est bornée par `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` (constante nommée, documentée dans `sim/SEEDING.md`).
- La quantité transférée est aussi bornée par le surplus réel de la source et par le déficit réel de la destination.
- **Conservation stricte de la masse** : `sum(cell.food_stock_kg for cell in world.cells.values())` est identique avant et après la seule étape de commerce, à 1×10⁻⁹ kg près. Cela vaut pour tout état du monde.
- Pas de prix, pas de monnaie, pas de marché : le flux est purement physique.

Preuve mécanique de conservation : un test unitaire construit un mini-monde de deux cellules adjacentes (une en surplus, une en déficit) et vérifie l'invariant de somme avant et après une étape de commerce. Ce test constitue la paire de preuve rouge déclarée en § Execution Contract.

### SC5 — Le monde vit, mesuré sur les 596 cellules réelles

Après N = 200 ticks (valeur déclarée comme constante ou documentée dans `sim/SEEDING.md`), simulés sur `World.from_g3(rng_seed=42)`, **toutes** les conditions suivantes sont simultanément vraies, mesurées sur les 596 cellules chargées depuis les artefacts G3 :

1. `cellules_affamees_monde_reel` **> 0** : au moins une cellule a eu `hunger_ticks > 0` à au moins un tick de la simulation.
2. `morts_cumules_monde_reel` **> 0** : la population totale finale est strictement inférieure à la population totale initiale, la différence étant due à la mortalité (ce brief ne modélisant pas la natalité).
3. `kg_transportes_monde_reel` **> 0** : des kilogrammes de nourriture ont transité entre cellules adjacentes.
4. `population_finale_positive` **> `SEUIL_SURVIE_POPULATION_FRACTION`** : la population totale finale représente au moins `SEUIL_SURVIE_POPULATION_FRACTION` (par exemple 10 %) de la population initiale. Cette fraction est déclarée comme constante dans `sim/SEEDING.md` ; le Générateur choisit et justifie une valeur qui autorise des pertes locales réelles sans déclencher l'effondrement global.

Chaque compteur est produit par un script rejoué sur `World.from_g3()` depuis la racine, avec la commande exacte recopiée dans le journal (hard-won rule 3 : un chiffre sans commande n'est pas un compteur).

**Cas structurellement inatteignable.** Le test d'intégration SC7d du brief 011 utilisait `area_km2 = 0.0`, superficie impossible dans les données G3 (minimum réel : 1,444877 km²). Si ce test est conservé, il doit être annoté comme « cas hors données G3, conservé uniquement pour tester la limite de la fonction ». Tous les tests unitaires construisant des cellules à la main utilisent `area_km2 ≥ 1.0` km² (arrondi conservateur du minimum réel). Ce cas inatteignable ne peut pas être utilisé comme preuve d'un compteur SC5.

### SC6 — `sim/tests/` tourne en intégration continue

`.github/workflows/harness-ci.yml` est modifié pour qu'un job (existant ou nouveau) collecte et exécute `sim/tests/`. La commande exacte que la CI lancera est spécifiée dans le fichier de workflow.

Preuves exigées :
1. Le fichier `.github/workflows/harness-ci.yml` modifié est suivi par git et vérifiable depuis un clone.
2. La commande CI rejouée localement depuis la racine produit un code de sortie nul.

Le compteur `ci_sim_tests_collectes` est produit par :

```
.venv/bin/python -m pytest sim/tests/ --collect-only -q
```

et doit être strictement positif.

### SC7 — Réserves R1-R4 du verdict 011 fermées, couverture d'écriture étendue

**R1 — Commande du compteur d'archive corrigée.**

Dans `harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json`, l'entrée `"name": "lignes_differentes_preuve_rouge_iter1"` déclare comme commande un diff des fichiers de preuve courants — or ces fichiers ont été régénérés en itération 2 et produisent 53 lignes, pas 70. La commande doit être remplacée par une commande reproductible produisant réellement 70, par exemple en utilisant les versions extraites du commit d'itération 1 via `git show <hash>:sim/tests/proof_red/run_sabotage.txt` et `git show <hash>:sim/tests/proof_red/run_correct.txt`, le hash étant identifié par `git log --oneline` et recopié dans la commande. Si aucun document sous `harness/queue/briefs/011-*/` ne cite le nombre 70 en référence à ce compteur, l'entrée peut être retirée à la place.

**R2 — Le test de couverture découvre les dataclasses par introspection.**

`sim/tests/test_write_coverage.py` ne doit plus nommer explicitement la classe `Cell`. Il découvre toutes les dataclasses du module `sim.model` par introspection (par exemple avec `inspect.getmembers(sim.model, inspect.isclass)` filtré par `dataclasses.is_dataclass()`). Contre-preuve attendue : si une deuxième dataclass est ajoutée au module sans aucun site d'écriture, le test de couverture échoue.

**R3 — La détection des sites d'écriture vérifie le type ou le nom de l'objet cible.**

La reconnaissance d'un site d'écriture doit exiger que la variable cible soit identifiée comme une instance de l'entité scrutée (au minimum par son nom conventionnel dans les signatures de fonctions, par exemple `cell` pour `Cell`). Une affectation `autre_objet.food_stock_kg = X` sur un objet d'un type différent ne doit pas compter comme site d'écriture pour `Cell.food_stock_kg`.

**R4 — Consolidation des fichiers de preuve dupliqués (optionnel, encouragé).**

Les deux fichiers de preuve verte du brief 011 (`run_correct.txt` et `run_phantom_green.txt`) ont le même contenu octet pour octet. Ils peuvent être consolidés en un seul fichier référencé par les deux paires `must_differ_from`, à condition que les deux paires restent valides (rouge ≠ vert pour chacune). Ce point est optionnel et ne bloque pas l'acceptation du lot.

**Extension de la couverture aux nouveaux champs et à `World.adjacency`.**

Le test de couverture d'écriture, une fois corrigé pour R2 et R3, doit aussi couvrir :
- Le nouveau champ `food_deficit_kg` de `Cell` (ajouté par SC3).
- Tout autre nouveau champ déclaré sur `Cell` dans ce brief.
- L'attribut `World.adjacency` : le test vérifie qu'au moins un site du moteur (maillon commerce) lit `world.adjacency`. `World` n'est pas une dataclass ; la vérification se fait par une assertion spécifique (recherche de lecture d'`adjacency` dans les modules du moteur). Contre-preuve : si le maillon commerce est retiré du moteur, la vérification de lecture de `adjacency` doit échouer.

### SC8 — Registre de coût

Une ligne est ajoutée à `harness/queue/cost-ledger.jsonl` (en fin de fichier, sans modification des lignes existantes) via la commande exacte :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules --event generator-run --audit-id CURSOR-3b47ffe-pr57-monde-sans-faim
```

Le nom de l'événement utilise un tiret (`generator-run`, constat P3-1 de l'audit : le défaut de l'outil utilise le tiret).

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Implémenter l'agrégation Province, les villes, les familles ou les personnes.** ADR-0003 fait de `cell_id` la seule clé spatiale ; Province est une vue dérivée. La population reste un agrégat par cellule.
2. **Implémenter la natalité ou la migration.** La population peut diminuer (mortalité) mais aucun afflux de nouveaux habitants n'est modélisé.
3. **Implémenter prix, monnaie ou marchés.** Le commerce est un flux physique de nourriture entre cellules adjacentes sans mécanisme de prix.
4. **Modifier `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, le code du harnais (`harness/*.py`, `harness/pipeline/`, `harness/backends/*.py`).** Les seules exceptions autorisées, nommément :
   - `.github/workflows/harness-ci.yml` (câblage CI — SC6) ;
   - `harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json` (correction R1 uniquement — SC7) ;
   - `harness/queue/cost-ledger.jsonl` (ajout d'une ligne en fin de fichier — SC8) ;
   - le dossier `harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/` (livrables du présent lot).
5. **Fermer les points de harnais et de gouvernance différés.** Les points 1 (traçage mécanique de l'acteur des rôles), 7 (forme `must_differ_from` divergente entre brief 011 et gate ; gate ne contrôlant que 2 fichiers sur 22 déclarés hors du dossier de brief), et la moitié « outillage » des points 2 et 8 de l'audit sont nommément différés vers un brief de harnais ultérieur. Ce brief ne les traite pas.
6. **Rapporter un compteur issu d'un monde non chargé.** Tout compteur exigeant le monde réel est mesuré sur les 596 cellules effectivement chargées par `World.from_g3()`.
7. **Inventer des données historiques.** Toutes les constantes sont des proxies paramétriques documentés dans `sim/SEEDING.md`.

---

## Required Counters

| nom | source de l'échantillon | dénominateur |
|-----|-------------------------|--------------|
| `tick_duration_days` | valeur de `TICK_DURATION_DAYS` lue dans `sim/constants.py` au chargement | 1 constante déclarée (doit être > 0) |
| `constantes_temporelles_coherentes` | inspection par test ou script de `sim/constants.py` et `sim/SEEDING.md` : chaque constante temporelle cite explicitement `TICK_DURATION_DAYS` dans sa dérivation | nombre de constantes temporelles déclarées dans `sim/constants.py` (production, consommation, stock initial) |
| `rng_etat_change_apres_tick` | `rng.getstate()` avant et après 10 ticks sur `World.from_g3(rng_seed=42)` : état interne différent = `True` | 1 comparaison |
| `ticks_deterministes_meme_graine` | condensés SHA256 de l'état complet du monde après N = 200 ticks, deux runs avec mêmes graines world = 42 et rng = 42 ; condensés égaux = `True` | 1 comparaison |
| `ticks_differents_graines_rng_differentes` | condensés SHA256 de l'état du monde après N = 200 ticks, deux runs avec même graine world = 42 et graines rng différentes (42 et 999) ; condensés différents = `True` | 1 comparaison |
| `food_deficit_kg_ecrit_quand_manque` | test unitaire : cellule construite à la main (`area_km2 ≥ 1.0`, consommation > stock) ; `food_deficit_kg > 0` après un tick complet | 1 test (PASS ou FAIL) |
| `conservation_masse_transport` | test unitaire : mini-monde de 2 cellules adjacentes (une en surplus, une en déficit) ; `sum(food_stock_kg)` avant étape commerce = `sum(food_stock_kg)` après (à 1×10⁻⁹ kg près) | 1 comparaison |
| `cellules_affamees_monde_reel` | `World.from_g3(rng_seed=42)` simulé N = 200 ticks ; nombre de cellules ayant eu `hunger_ticks > 0` à au moins un pas de temps | 596 cellules chargées |
| `morts_cumules_monde_reel` | `World.from_g3(rng_seed=42)` simulé N = 200 ticks ; population totale initiale − population totale finale (ce brief ne modélisant pas la natalité) | population totale initiale dérivée du chargement |
| `kg_transportes_monde_reel` | `World.from_g3(rng_seed=42)` simulé N = 200 ticks ; somme des kilogrammes échangés par le maillon commerce sur toutes les arêtes et tous les ticks | les 1364 arêtes × N ticks |
| `population_finale_positive` | `World.from_g3(rng_seed=42)` simulé N = 200 ticks ; fraction `population_finale / population_initiale` | > `SEUIL_SURVIE_POPULATION_FRACTION` documenté dans `sim/SEEDING.md` |
| `ci_sim_tests_collectes` | `.venv/bin/python -m pytest sim/tests/ --collect-only -q` depuis la racine ; nombre de tests collectés | > 0 tests collectés |
| `champs_modele_couverts_etendu` | test de couverture d'écriture sur toutes les dataclasses de `sim.model` découvertes par introspection, plus assertion dédiée sur `World.adjacency` | nombre total de champs déclarés dans toutes les dataclasses de `sim.model` + 1 pour `World.adjacency` |
| `lignes_differentes_transport_rouge_vert` | diff ligne à ligne entre `sim/tests/proof_red/run_transport_red.txt` et `sim/tests/proof_red/run_transport_green.txt` | > 0 lignes différentes |
| `lignes_differentes_couverture_ext_rouge_vert` | diff ligne à ligne entre `sim/tests/proof_red/run_coverage_ext_red.txt` et `sim/tests/proof_red/run_coverage_ext_green.txt` | > 0 lignes différentes |

---

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|-----------------------------|-----------------|-----------------|
| « le budget d'exécution n'est pas mesurable sur cette machine (hors session Claude locale) » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules` | la sortie contient la chaîne `UNMEASURABLE` |
| « les artefacts G3 d'adjacence ne sont pas lisibles depuis ce chemin » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g3.json'))"` depuis la racine du dépôt | le message d'erreur Python exact (FileNotFoundError, JSONDecodeError ou équivalent) affiché sans modification |
| « le moteur `sim/` requiert une dépendance tierce pour fonctionner » | `.venv/bin/python -c "import sim"` depuis la racine du dépôt | le message ImportError exact, avec le nom du module manquant |

Aucune autre dérogation n'est recevable. En particulier :
- « il est impossible de créer des déficits locaux sans effondrement global » **n'est pas une dérogation** : la calibration des constantes (production, consommation, stock initial, capacité de transport, fraction de survie) est précisément le travail de ce brief. Invoquer cette impossibilité sans calibration tentée est une abdication (hard-won rule 9).
- « la preuve rouge d'abord est trop coûteuse » **n'est pas une dérogation** : les deux paires de preuve (transport-conservatif, couverture étendue) sont des conditions de succès.

---

## Execution Contract

- Ce brief couvre principalement le sous-système `sim/` plus quatre fichiers périphériques : `.github/workflows/harness-ci.yml`, `harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json`, `harness/queue/cost-ledger.jsonl`, et le dossier du brief 012 lui-même. Aucune étape Unity.

- **Estimation d'appels d'outils : 120.** Ancres : un brief ADR = 108 appels (source : historique du harnais) ; brief 011 (amorçage `sim/` de zéro, 28 fichiers, +3098 lignes, 1 sous-système) a utilisé une estimation similaire. Le présent brief touche un sous-système déjà initialisé avec des modifications substantielles (nouveau champ, nouveau maillon commerce, calibration pour SC5, CI, proof red × 2 paires). La calibration peut exiger des itérations sur le monde réel. Plafond dur : 160 appels ; checkpoint obligatoire à 130.

  Commande de vérification pré-génération (à exécuter avant tout travail de fond) :
  ```
  .venv/bin/python harness/budget.py split-check --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules --estimated-calls 120
  ```

  Le Générateur déclare dans son journal, avant de commencer le travail de fond, soit la valeur mesurée du budget, soit la dérogation `UNMEASURABLE` (avec la sortie de la commande budget.py status à l'appui).

- **Preuve rouge d'abord (hard-won rule 4) pour deux nouvelles gardes structurelles.** Chaque paire est produite depuis une copie de travail sabotée hors du dépôt (pas de modification du dépôt durant la phase de sabotage). Les sorties sont committées sous `sim/tests/proof_red/` (fichiers `.txt`, jamais `.log` — `.gitignore` exclut `*.log`).

  **Paire 1 — transport-conservatif** :
  - `sim/tests/proof_red/run_transport_red.txt` : sortie du test de conservation sur copie sabotée (le transport crée de la masse) → doit contenir au moins un `FAILED`.
  - `sim/tests/proof_red/run_transport_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.
  - Commandes type (depuis la copie hors dépôt) :
    ```
    .venv/bin/python -m pytest sim/tests/test_commerce.py::test_conservation_masse -v 2>&1 | tee /chemin/hors/depot/run_transport_red.txt
    ```

  **Paire 2 — couverture étendue** :
  - `sim/tests/proof_red/run_coverage_ext_red.txt` : sortie du test de couverture sur copie sabotée (dataclass avec champ fantôme sans écrivain) → doit contenir au moins un `FAILED`.
  - `sim/tests/proof_red/run_coverage_ext_green.txt` : même test sur code correct → doit contenir seulement des `PASSED`.

  Forme `must_differ_from` dans `deliverables/manifest.json` : **par fichier** (la forme que lit `harness/verdict_audit.py`, lignes 203-213 — constat P2-2 de l'audit ; ne pas utiliser la forme « liste de paires à la racine du manifeste ») :
  ```json
  {
    "path": "../../../../sim/tests/proof_red/run_transport_green.txt",
    "must_differ_from": "../../../../sim/tests/proof_red/run_transport_red.txt"
  }
  ```

- **Fichiers dans `deliverables/manifest.json` : tous sous version control.** Les quatre fichiers de preuve rouge/vert sont committés avant l'écriture du journal.

- **Interdictions pour le Générateur :**
  - Ne pas committer, ne pas pousser, ne pas modifier `brief.md`, `eval-rubric.md` ni `verdict.md`.
  - Ne pas utiliser `python` nu — toujours `.venv/bin/python`.
  - Ne pas recopier de valeur hexadécimale de condensé SHA256 dans un test ou un document (hard-won rule 12).

- **Fin de lot.** Le gate mécanique doit répondre ACCEPT :
  ```
  .venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules
  ```
  La suite complète doit être verte :
  ```
  .venv/bin/python -m pytest harness/tests/ -q
  .venv/bin/python -m pytest sim/tests/ -v
  ```
  Les deux sorties réelles sont recopiées dans le journal — pas seulement déposées dans un fichier annexe.

- **Celui qui produit ne prononce pas la recevabilité.**
