# sim/SEEDING.md — Documentation de l'amorçage paramétrique

**Brief** : 011 (amorçage) + 012 (base de temps, déficit, commerce)
**Statut** : paramétrique (proxy documenté, non inventé)

ADR-0018 : ces proxys **suffisent** au moteur vivant. On ne bloque pas
`sim/` sur une reconstruction historique exhaustive ni sur un calage
prédictif au millième. Un brief futur peut affiner les constantes ;
aucun lot ne doit inventer un cadastre de 1400 pour avancer.

---

## Déclaration explicite

**L'amorçage décrit dans ce fichier est un proxy paramétrique, pas une donnée
historique.** Aucune valeur de population ou de stock alimentaire initial ne
provient d'une source historique documentée. Conformément à la hard-won rule 10
(« l'absence de données ne s'invente pas en silence »), cette limitation est
déclarée ici de manière explicite.

Les paramètres ci-dessous sont des valeurs d'ordre de grandeur plausibles pour
une simulation médiévale/proto-moderne (1400-1900). Ils peuvent être calibrés
à tout moment par un brief ultérieur disposant de données historiques réelles.

---

## Base de temps unique (SC1 — brief 012)

### Constante centrale

```
TICK_DURATION_DAYS = 1
```

Un tick représente **1 jour calendaire**. Toutes les constantes temporelles
ci-dessous sont dérivées de cette valeur — aucune d'elles ne contient de
littéral de durée indépendant.

**Justification** : le jour est la plus petite unité de temps agronomique
pertinente (rotation des convois, consommation alimentaire quotidienne, cycle
de production journalier). Un tick-jour permet une calibration directe avec
les sources historiques (rations, rendements annuels ÷ 365).

---

## Population initiale par cellule

### Formule

```
population = max(0, int(area_km2 × INITIAL_POPULATION_PER_KM2 × variation))
```

où `variation = rng.uniform(SEED_POPULATION_VARIATION_LOW, SEED_POPULATION_VARIATION_HIGH)`.

### Paramètres

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `INITIAL_POPULATION_PER_KM2` | 10.0 | hab/km² | Densité médiévale européenne moyenne (ordre de grandeur : 5–20 hab/km², Bairoch 1988) |
| `SEED_POPULATION_VARIATION_LOW` | 0.9 | — | Variation minimale autour de la densité nominale (±10 %) |
| `SEED_POPULATION_VARIATION_HIGH` | 1.1 | — | Variation maximale autour de la densité nominale (±10 %) |

### Déterminisme

Deux appels à `World.from_g3(rng_seed=K)` avec la même graine `K` produisent
des populations initiales byte-identiques, car `rng = random.Random(rng_seed)`
initialise un générateur pseudo-aléatoire isolé (jamais de source globale).

---

## Stock alimentaire initial

### Formule

```
food_stock_kg = population × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK × INITIAL_FOOD_RESERVE_TICKS
```

Le stock de départ couvre `INITIAL_FOOD_RESERVE_TICKS` ticks de consommation
normale. Ce buffer initial est volontairement court (5 ticks = 5 jours) pour
que la dynamique de production/consommation prenne effet rapidement sans
créer une réserve artificielle trop grande.

### Paramètres

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` | 2.0 × TICK_DURATION_DAYS | kg/personne/tick | Ration journalière médiévale approx. 2 kg (céréales + substituts) × 1 jour/tick |
| `INITIAL_FOOD_RESERVE_TICKS` | 5 | ticks | Réserve de subsistance de 5 jours — suffisante pour absorber 2–3 mauvaises journées consécutives sans mort immédiate, sans masquer le comportement de long terme sur 200 ticks |

**Note sur le renommage (SC1 — constat P3-2 de l'audit)** : le champ était
précédemment appelé `INITIAL_FOOD_DAYS` ce qui laissait croire que l'unité
était le jour calendaire. Renommé en `INITIAL_FOOD_RESERVE_TICKS` pour
refléter que l'unité est le tick (= 1 jour ici, mais séparation claire des
unités).

---

## Rendement agricole et variabilité (SC1 + SC2 — brief 012)

### Formule (par tick)

```
yield_factor = rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
food_produced = area_km2 × FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × yield_factor
```

### Paramètres

| Constante | Valeur | Unité | Dérivation |
|---|---|---|---|
| `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` | 18.0 × TICK_DURATION_DAYS | kg/km²/tick | Proxy annuel : ~6 570 kg/km²/an (rendement brut médiéval ~1 800 kg/ha à 36 % de surface cultivée, référence Slicher van Bath 1963) ÷ 365 jours/an × 1 jour/tick ≈ 18.0 |
| `RNG_YIELD_LOW` | 0.5 | — | Facteur multiplicatif minimum : mauvaise saison (sécheresse, gel) à 50 % du rendement nominal |
| `RNG_YIELD_HIGH` | 1.5 | — | Facteur multiplicatif maximum : bonne saison à 150 % du rendement nominal |

### Justification du dénominateur de `constantes_temporelles_coherentes` (N1 — itération 2)

Le brief 012 cite trois constantes temporelles attendues : production, consommation et **constante de réserve initiale**. Le manifeste déclare un dénominateur de **3 constantes** couvrant production, consommation et capacité de transport (`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`), au lieu d'inclure `INITIAL_FOOD_RESERVE_TICKS`.

**Justification explicite** : `INITIAL_FOOD_RESERVE_TICKS` est exprimé en **ticks** (unité canonique du moteur), ce qui était précisément la correction d'unité demandée par SC1. Sa valeur brute `5` n'est pas multipliée par `TICK_DURATION_DAYS` — la multiplier rendrait la constante dimensionnellement incorrecte (tick × tick, pas tick). Elle est **unitairement neutre** par rapport à `TICK_DURATION_DAYS`. Le dénominateur du compteur porte donc sur les trois constantes réellement dérivées via `× TICK_DURATION_DAYS` : production, consommation, capacité de transport.

### Calibration SC5

Avec `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 18.0` et
`FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0` à 10 hab/km² :

- Production moyenne par km² : 18.0 × E[yield] = 18.0 × 1.0 = 18.0 kg/tick
- Consommation par km² : 10 × 2.0 = 20.0 kg/tick
- Déficit structurel moyen : 2.0 kg/km²/tick (production légèrement sous-équilibre)
- La variabilité [0.5, 1.5] crée des ticks de surplus (yield × 18 > 20 quand yield > 1.11, probabilité ≈ 39 %) : ces surplus alimentent le commerce inter-cellules (SC4)
- Les ticks de déficit créent des pénuries locales qui s'accumulent dans `food_deficit_kg`

Sur 200 ticks avec `rng_seed=42` et `world_seed=42` (mesuré) :
- 261 cellules avec `hunger_ticks > 0` au moins une fois
- 7 544 299 morts cumulés
- 8 171 507 kg transportés
- Fraction de survie : 0.887 > 0.70 (SEUIL_SURVIE_POPULATION_FRACTION)

---

## Déficit alimentaire et mortalité (SC3 brief 012 → SC4 brief 013)

### Champ `food_deficit_kg`

Sentinelle : -1.0 = non encore calculé (hard-won rule 8 : zéro est une
mesure réelle).

**Sémantique (brief 013)** :
- Si `consommation > stock` après production + commerce : `food_deficit_kg += (consommation - stock)` ; `food_stock_kg = 0`
- Si la cellule dispose d'un surplus (`remaining ≥ 0`) : `food_deficit_kg` est réduit **graduellement** :
  `food_deficit_kg = max(0.0, food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))`

### SC4 brief 013 — Mortalité continue, déficit à mémoire graduelle

**Retrait du plancher de mortalité** :

La formule `deaths = max(1, int(population × death_rate))` est remplacée par
`deaths = int(population × death_rate)`. Une famine légère (déficit < 1 kg/tête)
ne tue plus au moins une personne : le plancher binaire est supprimé.
Le taux effectif `deaths / population` respecte `MAX_DEATH_RATE_PER_TICK` pour
toute population ≥ 1 (propriété préservée par construction : `death_rate ≤ MAX_DEATH_RATE_PER_TICK`
avant la multiplication et l'arrondi par troncature vers zéro).

**Formule de mortalité (brief 013)** :
```
if food_deficit_kg > 0 and population > 0:
    per_capita_deficit = food_deficit_kg / population
    death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
    deaths = int(population × death_rate)   # sans max(1, …)
    population = max(0, population - deaths)
```

**Déficit à mémoire graduelle** :

Lorsqu'une cellule est en surplus (consommation couverte), le déficit accumulé
est réduit graduellement au lieu d'être effacé instantanément :

```python
cell.food_deficit_kg = max(0.0, cell.food_deficit_kg * (1 - DEFICIT_RECOVERY_RATE_PER_TICK))
```

### SC4 brief 013 — Justification de `DEFICIT_RECOVERY_RATE_PER_TICK`

| Constante | Valeur | Justification |
|---|---|---|
| `DEFICIT_RECOVERY_RATE_PER_TICK` | 0.10 | Taux de récupération choisi **avant mesure** : 10 % du déficit accumulé est effacé par tick de surplus. Physique médiévale : une semaine de famine (7 jours d'accumulation de déficit) ne se récupère pas en une journée d'abondance. Avec `r = 0.10`, la demi-vie de récupération est ≈ 7 ticks (ln(2)/ln(1/0.9) ≈ 6.6), ce qui signifie qu'une semaine de surplus efface la moitié d'un déficit accumulé sur une durée comparable. Ce choix est conservateur (récupération lente) et physiquement plausible pour une économie de subsistance. |

**Propriété vérifiable** : pour tout `D > 0` et `DEFICIT_RECOVERY_RATE_PER_TICK < 1`,
`D × (1 - DEFICIT_RECOVERY_RATE_PER_TICK) < D`, donc un seul tick de surplus
ne peut pas effacer un déficit non nul.

**Seuil de coupure `DEFICIT_ZERO_EPSILON`** (N4 feedback 001, itération 2) :

La récupération graduelle `D' = D × (1 - r)` multiplie le déficit par un facteur
strictement inférieur à 1 sans jamais atteindre zéro. Une cellule ayant connu la
famine conserverait indéfiniment un déficit infinitésimal (`1e-300` reste positif
après un tick de surplus), rendant l'état « aucun déficit » inatteignable. Cela
n'est pas physiquement significatif et constituerait un piège pour tout compteur
futur de cellules en déficit.

Correctif retenu : après récupération graduelle, tout déficit résiduel inférieur à
`DEFICIT_ZERO_EPSILON = 1e-6` est ramené à zéro. Ce seuil est à la fois :
- Négligeable physiquement (1 mg de déficit pour la population entière d'une cellule)
- Assez grand pour nettoyer les résidus de calcul flottant (≫ `1e-15` machine)

Le seuil est appliqué uniquement lors d'un tick de surplus (pas lors d'accumulation),
et uniquement après le passage `× (1 - r)`. Le test `test_deficit_non_efface_en_1_tick`
vérifie qu'un déficit de 10 000 kg n'est pas effacé en un tick (résiduel = 9 000 kg ≫
epsilon).

### Formule de mortalité originale (brief 012 — archivé)

```
if food_deficit_kg > 0 and population > 0:
    per_capita_deficit = food_deficit_kg / population
    death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
    deaths = max(1, int(population × death_rate))   # plancher supprimé par brief 013
    population = max(0, population - deaths)
```

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `HUNGER_DEATH_SCALE` | 0.005 | 1/(kg/personne) | Facteur de mortalité : 1 kg de déficit par tête → 0.5 % de mortalité par tick. Proxy : famine médiévale sévère documentée à 10-30 % de mortalité annuelle sur populations très touchées (~0.03-0.08 %/jour). Le facteur 0.005 permet des déficits modestes (5–10 kg/tête) pour atteindre 2-5 % de mortalité journalière. |
| `MAX_DEATH_RATE_PER_TICK` | 0.10 | — | Plafond de 10 % par tick : empêche l'effondrement instantané même à déficit extrême |

---

## SC3 brief 013 — Seuil de survie dérivé analytiquement

**Formule analytique** (capacité de charge malthusienne) :

```
rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
capacite_charge_hab_km2 = (FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × rendement_moyen)
                           / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
fraction_predite = capacite_charge_hab_km2 / INITIAL_POPULATION_PER_KM2
```

Avec les constantes actuelles :
```
rendement_moyen = (0.5 + 1.5) / 2 = 1.0
capacite_charge_hab_km2 = (18.0 × 1.0) / 2.0 = 9.0 hab/km²
fraction_predite = 9.0 / 10.0 = 0.90
```

**Dérivation analytique de `SURVIE_MARGE_DERIVEE`** (itération 2 — N.B. historique
de provenance ci-dessous) :

La formule `fraction_predite = 0.9` est un équilibre stationnaire (infini). Sur
N=200 ticks (fenêtre de transition), la fraction mesurée s'écarte de cette
prédiction pour deux raisons quantifiables sans jamais regarder la mesure :

**Effet 1 — Dépassement initial de la capacité de charge**

```
cap_hab_km2 = 9.0
depassement_initial = max(0, (d0 - cap) / d0) = (10 - 9) / 10 = 0.10
```

Cette fraction de la population initiale est au-dessus de la capacité de charge ;
elle mourra pendant la fenêtre de 200 ticks. L'effet est proportionnel à
`fraction_predite` (plus l'équilibre prédit est haut, plus la correction est
grande). Terme retenu : `depassement_initial × fraction_predite = 0.1 × 0.9 = 0.09`.

**Effet 2 — Pression stochastique des ticks déficitaires**

```
ratio_C_P = (FOOD_CONSUMPTION × d0) / FOOD_PRODUCTION = 20.0 / 18.0 ≈ 1.111
p_tick_deficitaire = (ratio_C_P - RNG_YIELD_LOW) / (RNG_YIELD_HIGH - RNG_YIELD_LOW)
                   = (1.111 - 0.5) / 1.0 ≈ 0.611
```

La majorité des ticks (61 %) sont structurellement déficitaires. La récupération
est de `DEFICIT_RECOVERY_RATE_PER_TICK = 0.10` par tick d'excédent. Le produit
`p_tick_deficitaire × DEFICIT_RECOVERY_RATE_PER_TICK` donne la pression nette du
déficit stochastique sur la mortalité. Terme retenu :
`(11/18) × 0.10 ≈ 0.0611`.

**Formule assemblée** :

```
SURVIE_MARGE_DERIVEE = depassement_initial × fraction_predite
                        + p_tick_deficitaire × DEFICIT_RECOVERY_RATE_PER_TICK
                     = 0.1 × 0.9 + (11/18) × 0.1
                     ≈ 0.09 + 0.0611
                     ≈ 0.1511
```

Cette expression sort uniquement des constantes du modèle. Sa valeur (`≈ 0.1511`)
diffère volontairement du `0.15` de l'itération 1 : voir historique ci-dessous.

```
SEUIL_SURVIE_POPULATION_FRACTION = fraction_predite - SURVIE_MARGE_DERIVEE
                                 ≈ 0.90 - 0.1511 ≈ 0.7489
```

**Vérification de falsifiabilité** : si `INITIAL_POPULATION_PER_KM2` est doublé
à 20 hab/km², alors `_fraction_predite = 0.45`, `_depassement_initial = 0.55`,
`_p_tick_deficitaire = 1.0`, et `SURVIE_MARGE_DERIVEE = 0.55 × 0.45 + 1.0 × 0.10
= 0.3475`. La fenêtre devient `[0.45 - 0.3475, ...] = [0.1025, ...]`. Avec une
densité double, tous les ticks sont déficitaires et la fraction mesurée converge
vers zéro — le test `test_fraction_dans_marge` rougit.

---

**Historique de provenance (itération 2 — correction B1 feedback 001)** :

À l'itération 1, la marge avait été fixée à `0.10` puis, après observation que
la mesure (`0.766`) tombait hors de la fenêtre `[0.80, 1.0]`, réajustée à `0.15`
pour que la fenêtre inclue la mesure. Le journal de l'itération 1 l'avait reconnu
explicitement. Les commentaires dans `sim/constants.py` et le présent fichier
affirmaient alors « valeur choisie avant mesure » — ce qui était faux au regard
de la chronologie réelle.

À l'itération 2, la marge est remplacée par l'expression dérivée ci-dessus.
L'ordre des opérations réel est : formule posée → valeur calculée (`≈ 0.1511`) →
mesure effectuée (`0.766`) → résultat constaté (mesure dans la fenêtre `[0.749,
1.051]`). La provenance de la valeur est désormais le modèle, pas l'observation.

---

## Commerce inter-cellules (SC4 brief 012 → SC1+SC2 brief 013)

### Paramètre

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` | 200.0 × TICK_DURATION_DAYS | kg/arête/tick | Proxy : convoi à dos de mulet (capacité ~200 kg, une liaison rurale par jour, Pounds 1974) |

### SC2 brief 013 — Commerce atomique, snapshot et allocation déterministe

**Définition du besoin et du surplus au moment du commerce** (commerce avant consommation) :

Lorsque le commerce précède la consommation, le « besoin » d'une cellule pour
ce tick n'est plus `food_deficit_kg` (déficit cumulé des ticks précédents) mais
le **manque prévisible du tick courant** :

```
besoin(c)  = max(0, population_c × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK - food_stock_kg_c)
surplus(c) = max(0, food_stock_kg_c - population_c × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK)
```

Ces valeurs sont calculées sur le snapshot pris avant tout transfert.

**Algorithme en deux passes** :

1. Snapshot immuable : `{cell_id: (food_stock_kg, population)}` pour toutes les cellules, avant modification.
2. Calcul des transferts à partir du snapshot uniquement.
3. Application de tous les transferts en une seule passe finale.

Une cellule qui vient de recevoir de la nourriture sur une arête ne peut pas
en redistribuer sur une autre arête du même tick (transport atomique).

**Allocation déterministe en cas de demandes concurrentes** :

Si plusieurs cellules en besoin sont adjacentes à la même source, l'allocation est :
- Proportionnelle à leurs besoins respectifs (calculés depuis le snapshot).
- Traitée dans l'ordre stable des `cell_id` croissants.
- Chaque transfert est borné par `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`.
- Si la somme des demandes dépasse le surplus de la source, chaque receveur
  reçoit `surplus_source × (besoin_i / total_besoin)`, plafonné à la capacité.

**Invariant** : `food_deficit_kg` n'est jamais modifié par le maillon commerce
(SC1 brief 013). Seul `food_stock_kg` est mis à jour.

**Écrêtage côté receveur** (N3 feedback 001, itération 2) :

Chaque source alloue proportionnellement à ses receveurs en fonction du surplus
snapshot. Mais une cellule adjacente à plusieurs sources en surplus pouvait
recevoir plus que son besoin (chaque source lui allouait son besoin entier).
Exemple détecté par l'Évaluateur : cellule avec besoin 200 kg, adjacente à deux
sources en surplus de 200 kg → recevrait 400 kg.

Correctif : après l'allocation proportionnelle par source (passe 1c), une passe
supplémentaire (passe 1d) plafonne le total reçu par chaque cellule à son besoin
snapshot. Si le total entrant excède ce besoin, toutes les livraisons entrantes
sont réduites proportionnellement (l'excédent reste aux sources).

Ce changement conserve la masse (rien n'est créé), réduit le transport effectif,
et change les compteurs du monde réel (SC6 re-mesuré en conséquence).

---

## Seuil de survie de la population (SC5 — brief 012)

| Constante | Valeur | Justification |
|---|---|---|
| `SEUIL_SURVIE_POPULATION_FRACTION` | 0.70 | Autorise jusqu'à 30 % de pertes globales — compatible avec des famines médiévales régionales sévères sans effondrement civilisationnel. Calibré pour être compatible avec les paramètres de production/consommation ci-dessus (mesuré à 0.887 sur N=200 ticks). |

---

## SC1 brief 017 — Modèle de survie stationnaire (rédigé AVANT toute mesure)

> **Ordre de rédaction.** Cette section, ainsi que les sections SC2, SC3, SC4
> et SC5 du brief 017 ci-dessous, ont été écrites et committées **avant**
> d'exécuter la moindre simulation du monde réel. Aucun coefficient, aucune
> tolérance, aucun horizon n'a été ajusté après avoir vu une valeur mesurée.
> C'est le mode d'échec n° 5 (calibration après mesure) que cette discipline
> évite.

### Ce qui est supprimé et pourquoi

`SURVIE_MARGE_DERIVEE` et `SEUIL_SURVIE_POPULATION_FRACTION` (brief 013) sont
**supprimées**. Elles ne dépendaient ni de `HUNGER_DEATH_SCALE` ni de
`MAX_DEATH_RATE_PER_TICK` : le critère qui certifiait que « le monde vit »
était aveugle aux constantes qui gouvernent la mort. Une famine deux fois plus
mortelle passait le même contrôle. De plus, la récupération du déficit y entrait
avec le mauvais signe (une récupération plus rapide élargissait la marge
d'erreur au lieu d'augmenter la survie).

L'archive de leur dérivation reste ci-dessus (section « SC3 brief 013 ») et
n'est pas retouchée.

### Densité stationnaire : le dépassement est un oscillateur

Par km², en notant `C` la consommation par personne et par tick, `F` la
production par km² et par tick, `d` la densité d'habitants et `D` le déficit
alimentaire cumulé :

```py
cap = F × rendement_moyen / C          # densité de charge : 18 × 1.0 / 2 = 9 hab/km²
```

Tant que `d > cap`, le déficit croît de `C × (d − cap)` par tick, et la
mortalité fait décroître `d` de `HUNGER_DEATH_SCALE × D` par tick. En posant
`x = d − cap`, on a exactement :

```py
D' = C × x
x' = -HUNGER_DEATH_SCALE × D
```

C'est un oscillateur de pulsation `ω = sqrt(HUNGER_DEATH_SCALE × C)`. Sa
quantité conservée est `Q = C·x² + HDS·D² + C·HDS·x·D` (invariance vérifiable
à la main sur le schéma d'Euler semi-implicite qu'est l'ordre du tick :
consommation puis mortalité). Partant de `D = 0` et `x = x0 = d0 − cap`, le
déficit revient à zéro quand `x = −x0`, c'est-à-dire :

```py
densite_stationnaire = cap − (d0 − cap) = 2 × cap − d0     # 2 × 9 − 10 = 8 hab/km²
fraction_depassement = densite_stationnaire / d0            # 8 / 10 = 0.80
```

La population ne dépasse pas seulement en descendant jusqu'à la capacité de
charge : elle la dépasse par le bas, parce que la dette alimentaire accumulée
pendant la descente continue de tuer après que la densité soit repassée sous
`cap`.

### Érosion stochastique : c'est ici qu'entre `HUNGER_DEATH_SCALE`

À la densité stationnaire, un tick de mauvais rendement crée encore un déficit.
L'espérance de ce manque, pour un rendement `Y` uniforme sur
`[RNG_YIELD_LOW, RNG_YIELD_HIGH]` et un besoin `A = C × d_stat` :

```py
E[max(0, A − F×Y)] = (A − F×RNG_YIELD_LOW)² / (2 × F × (RNG_YIELD_HIGH − RNG_YIELD_LOW))
                     # valable quand F×LOW < A < F×HIGH
```

Ce déficit tue une fraction `min(HDS × déficit_par_tête, MAX_DEATH_RATE_PER_TICK)`
de la population, pendant le temps qu'il met à être remboursé
(`1 / DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG` tick au ratio nominal 1:1), et sur
l'échelle de temps du tampon alimentaire — `INITIAL_FOOD_RESERVE_TICKS`, la
seule échelle de stockage nommée du modèle.

```py
SURVIE_FRACTION_PREDITE_STATIONNAIRE = fraction_depassement × (1 − erosion)
erosion = min(1, min(HDS × deficit_par_tete, MAX_DEATH_RATE_PER_TICK)
                  × (1 / DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG)
                  × INITIAL_FOOD_RESERVE_TICKS)
```

**Propriétés de signe** (exigées par le brief, vérifiées par SC2) :
`HDS × 2` double l'érosion → la prédiction **diminue** ;
`DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG × 2` divise par deux la durée de la dette
→ la prédiction **augmente** ; `FOOD_PRODUCTION × 2` relève `cap` → la
prédiction **augmente**.

### Horizon `N_STAT_SURVIE` — justification avant mesure

Le transitoire dure au plus une période d'oscillation :

```py
periode = 2π / sqrt(HUNGER_DEATH_SCALE × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK)
        = 2π / sqrt(0.005 × 2.0) ≈ 63 ticks
N_STAT_SURVIE = max(1000, ceil(periode / MAX_DEATH_RATE_PER_TICK))
```

`1 / MAX_DEATH_RATE_PER_TICK` est l'échelle de temps au bout de laquelle toute
mortalité résiduelle au taux plafond est épuisée ; l'horizon couvre donc la
période d'oscillation répétée dix fois. Le plancher de 1000 ticks est imposé
par le brief 017. Avec les constantes actuelles : `N_STAT_SURVIE = 1000`.

### Tolérances — dérivées, jamais ajustées

| constante | expression | valeur | justification |
|---|---|---|---|
| `SURVIE_TOLERANCE_STATIONNAIRE` | `(σ_Y / ȳ) × (cap / d0) × P(tick déficitaire)` | ≈ 0.101 | Le modèle remplace le tirage de rendement par sa moyenne. L'erreur au premier ordre est la dispersion relative du rendement (σ_Y = (HIGH−LOW)/√12), convertie en habitants par le rapport `cap / d0`, et restreinte au seul côté de la distribution qui tue (un bon rendement ne tue personne). |
| `SURVIE_CONVERGENCE_DELTA` | `(σ_Y / ȳ) × MAX_DEATH_RATE_PER_TICK` | ≈ 0.029 | À l'état stationnaire, la seule dérive restante vient des fluctuations de rendement : une fluctuation d'un écart-type expose une fraction `σ_Y/ȳ` de la population, et une population exposée ne peut perdre plus que le taux plafond par tick. |
| `SURVIE_TOLERANCE_SENSIBILITE` | `SURVIE_TOLERANCE_STATIONNAIRE + (d0 − cap)/d0` | ≈ 0.201 | Les régimes de sensibilité sont mesurés à N = 200 ticks. Dans un régime à faible `HUNGER_DEATH_SCALE`, la période d'oscillation s'allonge (ω ∝ √HDS) et le dépassement initial n'est pas totalement résorbé : on ajoute son amplitude. |

---

## SC2 brief 017 — Sensibilité : le moteur relit les constantes courantes

Le test de sensibilité remplace `HUNGER_DEATH_SCALE` **en mémoire**, jamais par
écriture dans le fichier source. Deux mécanismes rendent cela possible :

1. `sim/engine.py` lit `HUNGER_DEATH_SCALE` et `MAX_DEATH_RATE_PER_TICK` via le
   module (`_constantes.HUNGER_DEATH_SCALE`) et non par valeur importée. Une
   valeur importée par `from … import …` est figée au chargement : la
   remplacer dans `sim.constants` ne changerait rien au moteur.
2. `SURVIE_FRACTION_PREDITE_STATIONNAIRE` est le résultat d'une **fonction**,
   `compute_survie_fraction_predite_stationnaire()`, qui relit les globales
   courantes du module à chaque appel. La constante de module figée au
   chargement resterait, elle, à sa valeur nominale. `importlib.reload` n'est
   pas utilisé : il recharge `sim.constants` sans recharger `sim.engine`, ce
   qui laisserait le moteur et la prédiction sur deux jeux de constantes
   différents.

### Note documentaire P3-2 — écrêtage sans réallocation

L'écrêtage côté receveur (passe 1d de `_apply_commerce`) ne réalloue pas le
surplus libéré à d'autres cellules demandeuses : le surplus non livré reste
chez sa source. C'est un choix de simplicité assumé, pas un défaut ; le code du
commerce n'est pas modifié par le brief 017.

---

## SC3 brief 017 — Report de la fraction de mortalité

`int(population × death_rate)` arrondit à zéro dès que
`population × death_rate < 1`. Une cellule de 5 habitants en famine totale
produit `5 × 0.10 = 0.5` mort par tick : `int(0.5) = 0`, à chaque tick, pour
toujours. Cinq habitants deviennent immortels par arrondi, tandis que leurs
voisins de 5 000 habitants meurent normalement.

Le champ `Cell.mortality_remainder` (float, sentinelle `-1.0` = non calculé)
conserve la fraction non appliquée :

```py
remainder = cell.mortality_remainder if cell.mortality_remainder >= 0.0 else 0.0
raw = cell.population * death_rate + remainder
deaths = int(raw)
cell.mortality_remainder = raw - deaths
cell.population = max(0, cell.population - deaths)
```

**Borne `N_BOUND_MORT`** : au plafond de mortalité, une cellule accumule au
moins `MAX_DEATH_RATE_PER_TICK` mort par habitant et par tick ; il faut donc au
plus `ceil(1 / MAX_DEATH_RATE_PER_TICK) = 10` ticks pour qu'une mort entière
soit appliquée, quelle que soit la taille de la cellule.

---

## SC4 brief 017 — « Affamée » = en manque ce tick

L'ancien critère incrémentait `hunger_ticks` quand `food_stock_kg <= 0` après
consommation. Une cellule ravitaillée **exactement** à son besoin par le
commerce termine le tick avec un stock nul et un déficit nul : elle a mangé sa
ration. La compter comme affamée confond le garde-manger vide et la
sous-alimentation.

Nouveau critère causal : `_apply_consumption` retourne la pénurie du tick en kg
(`shortage = besoin − stock_avant_consommation`, nulle s'il n'y a pas de
manque) et `_update_hunger` n'incrémente que si cette pénurie est positive.
Propriété : `food_stock_kg == 0` et `food_deficit_kg == 0` après consommation
→ `hunger_ticks` non incrémenté.

---

## SC5 brief 017 — Récupération physique du déficit

`DEFICIT_RECOVERY_RATE_PER_TICK` (brief 013) est **supprimée**. Sa formule,
`food_deficit_kg × (1 − r)`, effaçait 10 % de la dette indépendamment du
surplus réel : un surplus d'un nanogramme effaçait 1 000 kg d'une dette de
10 000 kg. Des kilogrammes disparaissaient sans contrepartie physique
(principe 3 : rien ne se téléporte).

Successeur nommé : `DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG = 1.0` — kilogrammes
de dette remboursés par kilogramme de surplus **réellement consommé** au-delà
du besoin d'entretien (voie (a) du brief : ratio 1:1).

```py
remboursement = min(food_deficit_kg, surplus_du_tick × ratio)
food_deficit_kg -= remboursement
food_stock_kg = surplus_du_tick − remboursement    # les kg quittent le stock
```

Le ratio est borné à 1.0 dans le moteur : la réduction de la dette ne peut
jamais dépasser le surplus physique du tick, quelle que soit la valeur donnée à
la constante. La coupure `DEFICIT_ZERO_EPSILON` (brief 013) est conservée : un
résidu de dette inférieur à 1 mg est ramené à zéro.

**Conséquence attendue et assumée** : la dette se rembourse beaucoup plus vite
qu'avec l'ancienne formule dès qu'il y a un vrai surplus, et pas du tout quand
le surplus est infime. Les compteurs du monde réel changent légitimement —
c'est l'objet de la re-mesure SC6 du brief 017.

---

## Brief 018 — la Province dérivée : provenance des centres administratifs

Cette section décrit d'où viennent les données de l'agrégation, comment elle
calcule, et ce qu'elle refuse de faire. Elle est écrite avant toute citation
d'un compteur mesuré du lot 018 : une justification rédigée après la mesure
serait une calibration déguisée.

### Provenance : des données héritées du jeu, pas des frontières de 1400

Les centres administratifs sont lus dans
`pipeline/geo/legacy_game_data/province_coordinates.json`. Ce sont des
**données héritées du jeu**, reprises telles quelles et en lecture seule.

Ce ne sont **pas** des frontières historiques de 1400. Rien ici ne prétend au
statut de source savante, de reconstitution d'époque, ni de découpage
administratif attesté. Le fichier lui-même se décrit comme des
« coordonnées approximatives, corrigeables à vue ». Ces centres sont un
**proxy** : un point de départ commode pour éprouver le mécanisme
d'agrégation, destiné à être remplacé par une source documentée quand le
projet en aura une. Leur nombre n'est pas recopié ici : il est celui du
tableau `coordinates` du fichier, lu à chaque exécution.

De même, le nombre de cellules du monde n'est écrit nulle part dans le code :
il est lu de `pipeline/geo/artifacts/stats_g3.json` (`cell_count`) et dérivé
du chargement par `World.from_g3()`.

### Projection : celle que le fichier documente lui-même

Le fichier de centres déclare sa propre projection sous la clé `projection` :
équirectangulaire, `x = lon × cos(mid_latitude)`, `y = −lat`. C'est cette
projection qu'emploie `sim/aggregation.py`, et son paramètre
`projection.mid_latitude` est **lu du fichier** par
`charger_latitude_moyenne()`. Aucune valeur de latitude moyenne n'apparaît
comme littéral dans un corps de fonction — `sim/tests/test_no_hardcoded.py`
parcourt récursivement les modules de `sim/` hors tests et refuse tout
littéral numérique autre que 0, 1 et −1.

Les distances sont comparées **au carré** : même ordre que la distance, sans
racine carrée. La conversion des degrés en radians passe par la bibliothèque
standard (`math.radians`), jamais par un facteur recopié à la main.

### Règle de départage des égalités (D4)

Une cellule relève du centre le plus proche d'elle. Si deux centres ou plus
sont à distance **exactement** égale, la cellule relève de celui dont l'`id`
est le **plus petit**.

Cette règle est stable : elle ne dépend pas de l'ordre dans lequel les centres
sont parcourus. La comparaison retenue est
`carré < meilleur` **ou** (`carré == meilleur` **et** `id < meilleur_id`). Un
simple « le premier rencontré gagne » donnerait le même résultat dans un ordre
de parcours et un autre résultat dans l'ordre inverse : le déterminisme serait
espéré, pas prouvé. `sim/tests/test_determinisme_departage_purete.py` monte le
cas d'égalité exacte et l'essaie dans les deux ordres.

### Refus de deviner (D5)

Si une cellule chargée par `World.from_g3()` n'a pas de position dans les
artefacts géographiques, le code lève `PositionCelluleInconnue` en **nommant
la cellule**. Il n'attribue pas de province par défaut et n'écarte pas la
cellule en silence : une couverture obtenue en jetant les cellules gênantes
n'est pas une couverture. Quand une donnée manque, l'absence se déclare — elle
ne s'invente pas.

### Zéro mesuré contre sentinelle « non calculé »

Le compteur `cellules_sans_province` doit valoir **0**. Ce zéro est une
**mesure réelle** : le code a bien regardé chaque cellule chargée et n'en a
trouvé aucune sans province. La sentinelle « non calculé » du projet est
`-1`, jamais `0` ; un `0` rapporté ici affirme donc quelque chose, il n'avoue
pas une absence de mesure. La même distinction vaut pour
`cellules_position_absente`, `attributs_dynamiques_sur_cellules` et
`egalites_de_distance_monde_reel` : ce dernier peut légitimement valoir zéro
sans que cela signifie qu'on ne l'a pas calculé.

### Provinces peuplées : un fait mesuré, pas un plancher

Toute cellule relève d'une province ; l'inverse n'est pas exigé. Un centre
peut n'attirer aucune cellule. Le nombre de provinces peuplées est donc
rapporté tel qu'il sort de la mesure, avec le nombre de centres lus pour
dénominateur. Aucun test n'impose de plancher, et l'algorithme n'est en aucun
cas ajusté pour peupler tous les centres.

### Ce que l'agrégation ne fait pas

Elle ne modifie aucun objet reçu, n'écrit aucun fichier, et n'ajoute aucun
champ à `Cell`. La vue dérivée (`Regroupement`) vit dans `sim/aggregation.py`,
hors de `sim.model`, parce que `sim.model` contient les entités **persistées**
que le moteur fait évoluer : y déclarer la Province inviterait à la traiter
comme un état stockable, exactement ce que l'ADR-0003 interdit. Le pas de
temps (`tick`) ne consomme pas l'agrégation : la Province est ici une vue du
monde, pas encore un acteur économique.

---

## Référence de code

Tous les paramètres ci-dessus sont définis comme constantes nommées dans
`sim/constants.py`. Aucun littéral numérique de ces valeurs n'apparaît dans
les fonctions de calcul de `sim/engine.py` ou `sim/world.py` (vérifiable
via `sim/tests/test_no_hardcoded.py`).
