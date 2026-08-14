# Brief 017 : Le seuil de survie honnête — mortalité comptée et récupération physique

**Authored**: 2026-08-13T20:31:00Z
**Author**: forge-planificateur

> **Note de transparence (conformément au contrat du Planificateur) :** ce
> brief est rédigé par le rôle `forge-planificateur` orchestré par un agent
> Cursor Cloud remplaçant le CTO Claude (quota/plafond atteint). Le champ
> `Author` désigne le rôle natif ; l'acteur réel est Cursor Cloud.

---

## Provenance

Ce brief est la **fusion de deux graines d'audit** portant sur le même thème
causal (le seuil de survie et le décompte des morts), conformément à la
décision d'orchestration du CTO :

- **Graine 015** : conversion de l'audit
  `CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite`
  (`architecture/inbox/`, points retenus 1–8,
  décision `architecture/decisions/DECISION-CURSOR-0e98199-...md`)
- **Graine 016** : conversion de l'audit
  `CURSOR-29913c0-pr69-seuil-survie-non-borne`
  (`architecture/inbox/`, points retenus 1–15,
  décision `architecture/decisions/DECISION-CURSOR-29913c0-...md`)

**Décision de fusion CTO** : un seul brief exécutable `017-sim-seuil-survie-honnete`.
Les graines 015 et 016 restent des artefacts de conversion (archives) et
portent désormais un encadré de pointage vers le présent brief. Ce n'est pas
un `NEEDS_SPLIT` : un seul sous-système (`sim/`), un seul thème causal.

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE
instruction** (voir `CLAUDE.md` › Single Source of Instruction). Les audits
et décisions ci-dessus sont de la *provenance*, pas des ordres.

---

## World-Terms Requirement

Le monde simulé ne peut certifier qu'il « vit » que si les critères qui
mesurent sa survie suivent la physique causale réelle du moteur.

**Fil A — Garde de survie aveugle à la mortalité :**
Un habitant meurt parce qu'il a faim depuis assez longtemps et que son
déficit par tête dépasse un seuil critique, selon la constante
`HUNGER_DEATH_SCALE` et le plafond `MAX_DEATH_RATE_PER_TICK`. Si le critère
qui certifie que « suffisamment d'habitants survivent » ne dépend pas de ces
constantes, il peut affirmer que le monde vit alors même qu'une famine bien
plus mortelle (HDS plus grand) l'aurait tué : la garde ne mesure pas la
survie, elle la décrète. Un critère honnête prédit combien de personnes
survivent en tenant compte de tous les paramètres qui gouvernent la mortalité,
et juge un ÉCART entre la mesure et cette prédiction — pas un plancher
indépendant de la cause. La prédiction doit également rester valide si la
simulation tourne plus longtemps (indépendance à l'horizon de test).

**Fil B — Mortalité qui oublie ses morts :**
La troncature `int(population × death_rate)` arrondit à zéro dès que
`death_rate × population < 1`. Pour une cellule de 5 habitants en famine
totale, le taux est 0,5 → `int(2.5) = 2` morts par tick quand la population
est grande, mais `int(0.5) = 0` quand la population est petite. Ces 0,5 mort
fractionnaire sont jetés à chaque tick. Sur de nombreux ticks, ce sont des
centaines de milliers de morts jetées à la virgule. Un moteur honnête reporte
la fraction non appliquée au tick suivant, de sorte que personne ne soit
immortel par arrondi.

**Fil C — « Affamée » signifie en manque, pas garde-manger vide :**
Une cellule ravitaillée EXACTEMENT à son besoin par le commerce termine le
tick avec un stock nul et un déficit nul : elle a mangé sa ration. La
compter comme affamée (parce que son stock est à zéro) confond le garde-manger
vide avec la sous-alimentation. Le compteur de cellules affamées doit compter
les cellules qui ont MANQUÉ de nourriture ce tick, pas celles dont le stock
est épuisé après consommation.

**Fil D — Récupération du déficit sans contrepartie physique :**
La dette alimentaire d'un habitant représente les kilogrammes de nourriture
qui lui ont manqué par le passé. Cette dette ne peut diminuer que si des
kilogrammes réels sont consommés en sus du besoin d'entretien (principe 3 :
rien ne se téléporte). Le mécanisme actuel efface 10 % de la dette
indépendamment du surplus réel — un surplus d'un nanogramme efface autant
qu'une récolte pléthorique. C'est un kilogramme de dette qui disparaît sans
contrepartie physique.

---

## Success Conditions

### SC1 — Modèle de survie prédit : cible l'état stationnaire, dépend des constantes de mortalité

#### Contexte

`SURVIE_MARGE_DERIVEE` (brief 013) n'est plus le critère principal de
certification de la couche F2. Les audits démontrent qu'elle ignore
`HUNGER_DEATH_SCALE`, que `DEFICIT_RECOVERY_RATE_PER_TICK` y entre avec le
mauvais signe, et qu'elle est verte à N=200 mais rouge à N≥1600 sans
régression du moteur (`fraction_survie` converge vers ≈ 0.7464, sous la borne
0.7489 actuelle). Ces faits sont de la provenance des audits, à traiter comme
des mesures établies.

#### Choix du Planificateur pour l'indépendance à l'horizon (A.4)

**Issue retenue : prédiction ciblant l'état stationnaire.** Le test de
conformité se joue à un horizon N_STAT_SURVIE ≥ 1000 ticks, horizon documenté
comme étant au-delà du transitoire initial. Le Générateur documente AVANT
mesure (dans `sim/SEEDING.md` section SC1 brief 017) le critère analytique qui
justifie ce choix d'horizon : borner la durée du transitoire initial à partir
de `INITIAL_POPULATION_PER_KM2`, `cap_hab_km2`, et `MAX_DEATH_RATE_PER_TICK`
(sans regarder la valeur mesurée de `fraction_survie`). Le test inclut
également une vérification de convergence (ci-dessous).

#### Nouvelles constantes à définir dans `sim/constants.py`

Toutes les constantes ci-dessous sont des **expressions Python** (jamais des
littéraux numériques calibrés). Toutes sont documentées dans `sim/SEEDING.md`
section SC1 brief 017 AVANT toute citation de compteur mesuré.

| constante | rôle | contraintes |
|---|---|---|
| `N_STAT_SURVIE` | horizon de convergence (entier, ≥ 1000) | justification de l'horizon documentée avant mesure |
| `SURVIE_FRACTION_PREDITE_STATIONNAIRE` | fraction de survie prédite à l'état stationnaire | expression dépendant EXPLICITEMENT de `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK`, `DEFICIT_RECOVERY_RATE_PER_TICK` (ou son successeur), `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, `INITIAL_POPULATION_PER_KM2`, `RNG_YIELD_LOW`, `RNG_YIELD_HIGH` |
| `SURVIE_TOLERANCE_STATIONNAIRE` | tolérance sur &#124;mesuré − prédit&#124; | dérivée des constantes du modèle (variance stochastique ou autre), valeur dans (0.0, 0.5) |
| `SURVIE_CONVERGENCE_DELTA` | tolérance &#124;s(N_STAT) − s(N_STAT÷2)&#124; | dérivée des constantes, valeur dans (0.0, 0.1) |

**Propriétés de signe (non négociables, vérifiées par SC2) :**
- `HUNGER_DEATH_SCALE × 2` → `SURVIE_FRACTION_PREDITE_STATIONNAIRE` **diminue**.
- `DEFICIT_RECOVERY_RATE_PER_TICK × 2` (ou son successeur) → `SURVIE_FRACTION_PREDITE_STATIONNAIRE` **augmente ou reste stable**.
- `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × 2` → `SURVIE_FRACTION_PREDITE_STATIONNAIRE` **augmente**.

Le Générateur choisit librement la forme analytique du modèle (discret,
continu, borne stationnaire, modèle de mortalité intégrée). Le Planificateur
n'impose ni les coefficients ni la forme exacte — uniquement les propriétés.

#### Test de conformité (compteur `fraction_survie_dans_tolerance_stationnaire`)

Monde réel `World.from_g3(rng_seed=42)`, `random.Random(42)` :

1. Exécuter N_STAT_SURVIE ticks → mesurer `s_stat`.
2. Exécuter N_STAT_SURVIE ÷ 2 ticks → mesurer `s_demi`.
3. Vérifier convergence : `|s_stat − s_demi| ≤ SURVIE_CONVERGENCE_DELTA`.
4. Vérifier conformité : `|s_stat − SURVIE_FRACTION_PREDITE_STATIONNAIRE| ≤ SURVIE_TOLERANCE_STATIONNAIRE`.

```py
.venv/bin/python -m pytest sim/tests/test_survie_stationnaire.py -v
```

Résultat attendu : PASSED.

#### Devenir de `SURVIE_MARGE_DERIVEE` et `SEUIL_SURVIE_POPULATION_FRACTION`

Ces deux constantes peuvent être conservées comme archive ou supprimées, au
choix du Générateur — avec motivation écrite dans le journal. Le test
`test_survie_derivee.py::test_fraction_dans_marge` est **adapté ou remplacé**
(motivation écrite dans le journal). Il ne doit plus être le seul test de
conformité de la couche F2 au sens du brief 017.

---

### SC2 — Sensibilité prouvée : mesure et prédiction bougent dans le même sens

Une prédiction qui ne répond pas aux paramètres qu'elle prétend modéliser
n'est pas une prédiction — c'est une constante qui se déguise.

**Test de sensibilité à `HUNGER_DEATH_SCALE`
(compteurs `sensibilite_hds_05_passe`, `sensibilite_hds_2_passe`) :**

Monde réel `World.from_g3(rng_seed=42)`, `random.Random(42)`, N=200 ticks.
Trois régimes : HDS nominal, HDS × 0.5, HDS × 2.0 (en mémoire, jamais par
écriture dans le fichier source). Pour chaque paire de régimes :

- **(a) direction** : `s_mesure(HDS×0.5) > s_mesure(HDS_nom) > s_mesure(HDS×2)`
  ET `SURVIE_FRACTION_PREDITE(HDS×0.5) > SURVIE_FRACTION_PREDITE(HDS_nom) > SURVIE_FRACTION_PREDITE(HDS×2)`.
- **(b) tolérance** : `|s_mesure(régime) − SURVIE_FRACTION_PREDITE(régime)| ≤ SURVIE_TOLERANCE_SENSIBILITE`.

`SURVIE_TOLERANCE_SENSIBILITE` est une nouvelle constante dans `sim/constants.py`
(expression, pas littéral), documentée dans `sim/SEEDING.md` section SC2
brief 017 AVANT toute mesure. Elle peut être plus large que
`SURVIE_TOLERANCE_STATIONNAIRE` pour rendre compte du transitoire à N=200.

**Test de sensibilité à `DEFICIT_RECOVERY_RATE_PER_TICK`
(compteur `sensibilite_drr_direction_passe`) :**

DRR nominal vs DRR × 2 en mémoire, même monde, N=200 :
`SURVIE_FRACTION_PREDITE(DRR×2) ≥ SURVIE_FRACTION_PREDITE(DRR_nom)`.

```py
.venv/bin/python -m pytest sim/tests/test_sensibilite_survie.py -v
```

Résultat attendu : PASSED.

---

### SC3 — Accumulateur de mortalité fractionnaire

Un habitant en famine qui meurt à 0,3 par tick ne peut pas être immortel par
arrondi. La fraction non appliquée doit s'accumuler jusqu'à produire une mort
entière.

#### Nouveau champ `mortality_remainder` sur `Cell`

- Type `float`, valeur par défaut `-1.0` (sentinelle — hard-won rule 8 :
  zéro est une mesure réelle, pas un « non calculé »).
- Signification : partie fractionnaire de mort non encore appliquée depuis le
  tick précédent.

#### Formule dans `_apply_mortality()`

```py
remainder = cell.mortality_remainder if cell.mortality_remainder >= 0.0 else 0.0
raw = cell.population * death_rate + remainder
deaths = int(raw)
cell.mortality_remainder = raw - deaths
cell.population = max(0, cell.population - deaths)
```

(Le Générateur peut utiliser une formulation équivalente — la propriété est
que `raw - deaths` est persisté et utilisé au tick suivant.)

#### Test 1 — Famine totale et population petite
(compteur `famine_tue_cellule_5hab`)

Cellule de 5 habitants, déficit alimentaire énorme (au moins
`5 / HUNGER_DEATH_SCALE` kg, pour atteindre le plafond), pas de stock,
pas de commerce. Vérifier : au moins 1 mort en ≤ `N_BOUND_MORT` ticks.

`N_BOUND_MORT` est une constante dérivée et documentée AVANT mesure.
Borne analytique : `N_BOUND_MORT = ceil(1.0 / MAX_DEATH_RATE_PER_TICK) = 10`
(au plafond, `raw` vaut `population × MAX_DEATH_RATE = 5 × 0.1 = 0.5` par tick ;
après 2 ticks, `remainder = 0 + 0.5 = 0.5`, puis `raw = 0.5 + 0.5 = 1.0`,
`deaths = 1`). Documenter cette dérivation dans le test ou dans SEEDING.md.

#### Test 2 — Précision sur N ticks
(compteur `mortalite_precision_n_ticks`)

Sur N_STAT_SURVIE ticks d'un micro-monde déterministe à ≥ 3 cellules
(population ≥ 50, déficit constant non nul) :
`|somme_morts_appliques − somme_exacte(pop × death_rate)| ≤ 1` par cellule.

L'« exacte » est la somme en virgule flottante avant troncature, accumulée
tick par tick.

```py
.venv/bin/python -m pytest sim/tests/test_mortalite_accumulateur.py -v
```

Résultat attendu : PASSED.

---

### SC4 — « Affamée » = en manque ce tick, pas garde-manger vide

Le compteur `cellules_affamees` doit compter les cellules qui ont réellement
MANQUÉ de nourriture ce tick — c'est-à-dire dont `_apply_consumption` a
enregistré une pénurie (`shortage > 0`).

#### Critère causal retenu

`hunger_ticks` est incrémenté si et seulement si la cellule a manqué de
nourriture **ce tick** : la pénurie dans `_apply_consumption` était positive
(`shortage = tick_need - food_stock_kg_avant_consommation > 0`).

Le mécanisme d'implémentation est laissé au Générateur (valeur de retour de
`_apply_consumption`, indicateur dans l'état, comparaison avant/après déficit
— au choix). La propriété est : `food_stock_kg == 0 ET food_deficit_kg == 0`
après consommation → `hunger_ticks` **non incrémenté**.

#### Test (compteur `hunger_ticks_cellule_ravitaillee`)

Scénario témoin/receveuse du brief 013 (ration exacte fournie par le commerce,
pas de déficit avant ce tick) :
- Après un tick complet, `receveuse.hunger_ticks == 0`.
- Après un tick complet, `temoin.hunger_ticks == 0` (possédait sa ration,
  stock nul après consommation, déficit nul).

```py
.venv/bin/python -m pytest sim/tests/test_hunger_criterion.py -v
```

Résultat attendu : PASSED.

#### Re-mesure du compteur monde réel

`cellules_affamees_monde_reel_017` est RE-MESURÉ avec la nouvelle définition.
La valeur peut différer des 536 du brief 013 — c'est légitime et attendu.
L'archive 013 (`cellules_affamees_monde_reel_re = 536`) est intangible.

---

### SC5 — Récupération du déficit : physique (voie préférée)

#### Choix du Planificateur (D)

**Voie physique retenue.** La dette alimentaire ne diminue que par des
kilogrammes effectivement consommés en sus du besoin d'entretien. Un surplus
de S kg permet de rembourser au plus S kg (ou S × ratio nommé) de dette.
Un surplus infinitésimal n'efface pas une dette de 10 000 kg.

Cette voie est compatible avec une estimation à 130 appels d'outils — voir
§ Execution Contract.

#### Invariant physique requis

Pour tout appel à `_apply_consumption` avec surplus ≥ 0 :

```
deficit_reduction_this_tick ≤ food_stock_kg_after_consumption
```

c'est-à-dire ≤ `remaining` (le stock résiduel après avoir couvert le besoin
d'entretien).

#### Implémentation

Le Générateur choisit parmi :
- (a) `deficit_reduction = min(food_deficit_kg, remaining)` — ratio 1:1,
  `DEFICIT_RECOVERY_RATE_PER_TICK` disparaît, documenté ;
- (b) `deficit_reduction = min(food_deficit_kg, remaining × ratio_nommé)` —
  ratio ∈ (0, 1], `DEFICIT_RECOVERY_RATE_PER_TICK` est renommée et sa nouvelle
  sémantique est documentée dans `sim/SEEDING.md` section SC5 brief 017.

**Interdits :**
- Pas de nouveau littéral calibré sur la valeur de survie mesurée.
- Pas de formule `food_deficit_kg × (1 − rate)` indépendante du surplus.

Si `DEFICIT_RECOVERY_RATE_PER_TICK` est supprimée, le modèle SC1 utilise
sa valeur effective (1.0) ou la constante qui la remplace — à documenter.

#### Tests (compteurs `deficit_reduction_infinitesimal`, `deficit_reduction_proportionnel`)

**Test 1 :**
`food_deficit_kg = 10 000 kg`, surplus = 1e-9 kg (c'est-à-dire
`food_stock_kg = population × FOOD_CONSUMPTION + 1e-9`).
Après `_apply_consumption` : `food_deficit_kg > 9999.9` (réduction ≤ 1e-9 kg).

**Test 2 :**
`food_deficit_kg = 10 000 kg`, surplus = 5 000 kg (c'est-à-dire
`food_stock_kg = population × FOOD_CONSUMPTION + 5000`).
Après `_apply_consumption` : réduction proportionnelle à 5 000 kg
(exactement ou dans le ratio documenté).

```py
.venv/bin/python -m pytest sim/tests/test_deficit_physique.py -v
```

Résultat attendu : PASSED.

#### Note documentaire P3-2 (écrêtage sans réallocation)

La section SC2 de `sim/SEEDING.md` peut recevoir une seule phrase
documentaire indiquant que l'écrêtage côté receveur (passe 1d de
`_apply_commerce`) ne réalloue pas le surplus libéré — choix de simplicité
assumé, sans modifier le code du commerce.

---

### SC6 — Re-mesure du monde réel (017)

Les corrections SC3, SC4 et SC5 **changent les valeurs** des compteurs du
brief 013. Les nouvelles valeurs sont mesurées après ces corrections, committées
dans les livrables du présent brief. Les archives 012 et 013 sont intangibles.

Un script reproductible est committé sous
`harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/measure_sc6_017.py`.
Exécuté depuis la racine avec :

```py
.venv/bin/python harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/measure_sc6_017.py
```

il produit les quatre compteurs suivants sur `World.from_g3(rng_seed=42)`,
`random.Random(42)`, N = N_STAT_SURVIE ticks :

| compteur | source de l'échantillon | dénominateur | condition |
|---|---|---|---|
| `cellules_affamees_monde_reel_017` | cellules ayant eu pénurie (SC4) à au moins un tick | 596 cellules chargées par G3 | **> 0** |
| `morts_cumules_monde_reel_017` | population totale initiale − population totale finale | population totale initiale dérivée du chargement | **> 0** |
| `kg_transportes_monde_reel_017` | accumulateur `total_transported` sur tous les ticks | 1 364 arêtes × N_STAT_SURVIE ticks | **> 0** |
| `fraction_survie_monde_reel_017` | `population_finale / population_initiale` | population totale initiale | valeur tracée dans le manifeste (sans borne imposée ici — la fraction changera avec SC5) |

Les quatre premières conditions (> 0) doivent être satisfaites. La valeur de
`fraction_survie_monde_reel_017` est un fait observé, pas une borne.

---

### SC7 — Tests 013 adaptés ; suite complète verte

- Les tests `sim/tests/test_survie_derivee.py` sont **adaptés ou remplacés** :
  motivation écrite dans `deliverables/generator-log.md` section
  « Adaptation des tests des briefs précédents ».
- `test_survie_derivee.py::test_fraction_dans_marge` ne peut plus utiliser
  l'ancienne fenêtre `[SEUIL_SURVIE_POPULATION_FRACTION, borne_haute]` sans
  adaptation au nouveau modèle.
- Aucune suppression silencieuse (sans motivation écrite).

```py
.venv/bin/python -m pytest sim/tests/ -v
.venv/bin/python -m pytest harness/tests/ -q
```

Les deux commandes s'achèvent sans `FAILED`. Les `SKIP` Linux sont acceptés.

---

### SC8 — Registre de coût

```py
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/017-sim-seuil-survie-honnete \
  --event generator-run \
  --audit-id CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
```

(Le Générateur peut utiliser l'un ou l'autre audit_id — les deux sont la
source de ce brief.)

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Traiter les points hors-périmètre** des deux audits :
   - Constat 3 de 0e98199 (plafond MAX_DEATH_RATE jamais atteint) — informationnel.
   - Constat 5 de 0e98199 (CI/job Hermes pending) — processus.
   - P2-3 de 29913c0 (taille de PR, base, squash) — processus.
   - P3-1 de 29913c0 (arêtes dupliquées) — latent côté géo, brief ultérieur.
   - P3-3 de 29913c0 (budget Cursor UNMEASURABLE) — harnais, pas ce lot.

2. **Implémenter natalité, migration, prix, marchés, Province, villes,
   agrégation** — hors périmètre.

3. **Modifier `harness/*.py`, `harness/pipeline/`, `architecture/`,
   `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`,
   `.github/workflows/`**.

4. **Retoucher les archives des briefs 011, 012, 013, 014** (fichiers de ces
   dossiers sont intangibles).

5. **Recalibrer `FOOD_PRODUCTION_*`, `FOOD_CONSUMPTION_*`,
   `INITIAL_POPULATION_PER_KM2`, `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK`,
   `TRADE_CAPACITY_*`** pour faire rentrer un compteur.

6. **Réintroduire `max(1, …)` sur la mortalité** — propriété acquise brief 013.

7. **Modifier l'ordre du tick 013** (production → commerce → consommation →
   faim → mortalité) ni remettre `food_deficit_kg` dans `_apply_commerce`.

8. **Rapporter un compteur SC6 depuis un monde construit à la main ou
   depuis zéro cellule** — l'échantillon doit être les 596 cellules chargées
   par `World.from_g3()`.

9. **Imputer P3-2 (écrêtage sans réallocation) à un défaut** — c'est un choix
   de simplicité assumé, documentable en une phrase dans SEEDING.md.

---

## Required Counters

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `N_STAT_SURVIE` | constante dans `sim/constants.py` (dérivée) | entier ≥ 1000 |
| `SURVIE_FRACTION_PREDITE_STATIONNAIRE` | expression depuis `sim/constants.py` | valeur dans (0.0, 1.0) |
| `SURVIE_TOLERANCE_STATIONNAIRE` | expression depuis `sim/constants.py` | valeur dans (0.0, 0.5) |
| `SURVIE_CONVERGENCE_DELTA` | expression depuis `sim/constants.py` | valeur dans (0.0, 0.1) |
| `fraction_survie_dans_tolerance_stationnaire` | `World.from_g3(rng_seed=42)`, `random.Random(42)`, N_STAT_SURVIE ticks | fraction sur 596 cellules ; `|mesurée − prédite| ≤ SURVIE_TOLERANCE_STATIONNAIRE` ET `|s(N) − s(N÷2)| ≤ SURVIE_CONVERGENCE_DELTA` |
| `sensibilite_hds_05_passe` | monkeypatch HDS×0.5 en mémoire, même monde, N=200 ticks | sens mesuré et prédit concordants |
| `sensibilite_hds_2_passe` | monkeypatch HDS×2 en mémoire, même monde, N=200 ticks | sens mesuré et prédit concordants |
| `sensibilite_drr_direction_passe` | monkeypatch DRR×2 en mémoire, même monde, N=200 ticks | prédiction non décroissante |
| `famine_tue_cellule_5hab` | cellule pop=5, déficit énorme, pas de stock, N_BOUND_MORT ticks | ≥ 1 mort en ≤ N_BOUND_MORT ticks (N_BOUND_MORT dérivé de MAX_DEATH_RATE) |
| `mortalite_precision_n_ticks` | micro-monde ≥ 3 cellules pop≥50, déficit constant, N_STAT_SURVIE ticks | `|morts_appliqués − somme_exacte| ≤ 1` par cellule |
| `hunger_ticks_cellule_ravitaillee` | scénario témoin/receveuse brief 013 SC1, 1 tick | `hunger_ticks == 0` pour les deux cellules |
| `deficit_reduction_infinitesimal` | cellule food_deficit_kg=10000, surplus=1e-9 kg, 1 tick | réduction ≤ 1e-9 kg |
| `deficit_reduction_proportionnel` | cellule food_deficit_kg=10000, surplus=5000 kg, 1 tick | réduction ∝ surplus (5000 kg ou fraction documentée) |
| `cellules_affamees_monde_reel_017` | `World.from_g3(rng_seed=42)`, N=N_STAT_SURVIE ; pénurie réelle (SC4) | 596 cellules chargées ; **> 0** |
| `morts_cumules_monde_reel_017` | même simulation ; pop_init − pop_fin | pop totale initiale dérivée du chargement ; **> 0** |
| `kg_transportes_monde_reel_017` | même simulation ; accumulateur total_transported | 1 364 arêtes × N_STAT_SURVIE ticks ; **> 0** |
| `fraction_survie_monde_reel_017` | même simulation ; pop_fin / pop_init | pop totale initiale ; valeur tracée (pas de borne basse) |

---

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/017-sim-seuil-survie-honnete` | la sortie contient la chaîne `UNMEASURABLE` |
| « les artefacts G3 d'adjacence ne sont pas lisibles depuis ce chemin » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/adjacency_g3.json'))"` depuis la racine | le message d'erreur Python exact (FileNotFoundError ou équivalent) |
| « le moteur `sim/` requiert une dépendance tierce absente » | `.venv/bin/python -c "import sim"` depuis la racine | le message ImportError exact avec le nom du module manquant |
| « N_STAT_SURVIE ticks dépassent le temps disponible » | `.venv/bin/python -c "import time, random; from sim.world import World; from sim.engine import tick; w=World.from_g3(rng_seed=42); r=random.Random(42); t=time.time(); [tick(w,r) for _ in range(200)]; print(time.time()-t)"` | temps ≥ 60 s (seulement si la durée mesurée excède effectivement 60 s pour 200 ticks) |

Aucune autre dérogation n'est recevable. En particulier :
- « Il est impossible de dériver `SURVIE_FRACTION_PREDITE_STATIONNAIRE`
  sans calibrer sur la mesure » **n'est pas une dérogation** : une
  approximation analytique bornée (borne supérieure ou inférieure) est
  suffisante — ce qui est interdit est d'ajuster les coefficients après
  avoir vu la mesure.
- « La voie physique de récupération du déficit change trop les compteurs »
  **n'est pas une dérogation** : les compteurs changeront légitimement.

---

## Execution Contract

### Périmètre autorisé

Ce brief couvre exclusivement :
- `sim/engine.py`, `sim/constants.py`, `sim/model.py`, `sim/SEEDING.md`
- `sim/tests/` (nouveaux tests + adaptations motivées des tests 013)
- `sim/tests/proof_red/` (preuves rouges nouvelles — voir ci-dessous)
- `harness/queue/briefs/017-sim-seuil-survie-honnete/` (livrables du présent lot)
- `harness/queue/cost-ledger.jsonl` (ajout d'une seule ligne en fin de fichier, SC8)

**Fichiers interdits** : tout fichier sous `harness/*.py`, `harness/pipeline/`,
`architecture/`, `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`,
`.github/workflows/`, et tout fichier sous
`harness/queue/briefs/011-*/`,
`harness/queue/briefs/012-*/`,
`harness/queue/briefs/013-*/`,
`harness/queue/briefs/014-*/`.

### Estimation d'appels d'outils

**Estimation : 130 appels.** Ancres : brief 013 (sous-système `sim/` déjà
peuplé, 5 défauts corrélés) a utilisé ~125 outils. Le présent brief touche le
même sous-système avec 4 corrections distinctes (modèle de survie, accumulateur
de mortalité, critère de faim, récupération physique), 2 paires de preuves
rouges, 1 script de mesure, 1 manifest, 1 log, et une re-mesure sur
N_STAT_SURVIE ticks (plus long que 200 mais toujours rapide sur 596 cellules).
Plafond dur : 160 appels ; checkpoint obligatoire à 130.

Commande de vérification pré-génération (à exécuter après création du dossier,
avant tout travail de fond) :

```py
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/017-sim-seuil-survie-honnete \
  --estimated-calls 130
```

Note : si le script retourne `NEEDS_SPLIT` uniquement à cause de l'estimé
(130 < 150), l'instruction est de l'ignorer et de procéder — cf. décision
CTO dans la Provenance.

### Preuve rouge d'abord (hard-won rule 4) — deux paires obligatoires

Chaque paire est produite depuis une copie de travail sabotée hors du dépôt.
Les sorties sont committées sous `sim/tests/proof_red/` (`.txt`, jamais
`.log`).

**Paire A — Sabotage « prédiction aveugle à HUNGER_DEATH_SCALE » :**
- Sabotage : dans la copie hors dépôt, remplacer `HUNGER_DEATH_SCALE` par un
  littéral dans l'expression de `SURVIE_FRACTION_PREDITE_STATIONNAIRE` (de
  sorte que la prédiction ne varie plus quand HDS change).
- Test affecté : `test_sensibilite_survie.py::test_sensibilite_hds`.
- `sim/tests/proof_red/run_sensibilite_hds_red.txt` : sortie avec le
  sabotage → au moins un `FAILED`.
- `sim/tests/proof_red/run_sensibilite_hds_green.txt` : même test sur code
  correct → uniquement `PASSED`.

**Paire B — Sabotage « int() sans accumulateur » :**
- Sabotage : dans la copie hors dépôt, retirer `mortality_remainder` et
  revenir à `deaths = int(cell.population * death_rate)`.
- Test affecté : `test_mortalite_accumulateur.py::test_famine_tue_en_borne_de_ticks`.
- `sim/tests/proof_red/run_accumulateur_mort_red.txt` : sortie avec le
  sabotage → au moins un `FAILED`.
- `sim/tests/proof_red/run_accumulateur_mort_green.txt` : même test sur code
  correct → uniquement `PASSED`.

Forme `must_differ_from` dans `deliverables/manifest.json` (par fichier) :

```json
{
  "path": "../../../../sim/tests/proof_red/run_sensibilite_hds_green.txt",
  "must_differ_from": "../../../../sim/tests/proof_red/run_sensibilite_hds_red.txt"
}
```

(idem pour la paire B). Les quatre fichiers de preuve sont committés avant
l'écriture du journal.

### Deliverables obligatoires

Le dossier `harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/`
doit contenir :
- `manifest.json` (format standard, tous les fichiers sous version control)
- `measure_sc6_017.py` (script reproductible, `.venv/bin/python` uniquement)
- `generator-log.md` (journal d'exécution, rédigé par le Générateur)
- `.gitkeep` (pour que le dossier existe dès maintenant)

### Interdictions pour le Générateur

- **Ne pas committer, ne pas pousser, ne pas créer de branche.**
- Ne pas modifier `brief.md`, `eval-rubric.md` ni `verdict.md`.
- Jamais `python` nu — toujours `.venv/bin/python`.
- Ne pas recopier de valeur hexadécimale de condensé SHA256 (hard-won rule 12).
- Ne pas supprimer un test du brief 013 sans motivation écrite dans le journal.
- Ne pas retoucher les archives des briefs 011, 012, 013, 014.
- Ne pas calibrer `SURVIE_FRACTION_PREDITE_STATIONNAIRE`, `SURVIE_TOLERANCE_STATIONNAIRE`,
  `SURVIE_CONVERGENCE_DELTA` ou `SURVIE_TOLERANCE_SENSIBILITE` après avoir vu
  les valeurs mesurées.
- Ne pas recalibrer `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK`,
  `FOOD_PRODUCTION_*`, `FOOD_CONSUMPTION_*`, `TRADE_CAPACITY_*` pour faire
  rentrer un compteur.
- Ne pas réintroduire `max(1, …)` sur la mortalité.
- Ne pas modifier l'ordre du tick 013 ni remettre `food_deficit_kg` dans
  `_apply_commerce`.

### Fin de lot

Le gate mécanique doit répondre `ACCEPT` :

```py
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/017-sim-seuil-survie-honnete
```

La suite complète doit être verte :

```py
.venv/bin/python -m pytest harness/tests/ -q
.venv/bin/python -m pytest sim/tests/ -v
```

Les deux sorties réelles sont recopiées dans le journal.

**Celui qui produit ne prononce pas la recevabilité.**
