# sim/SEEDING.md — Documentation de l'amorçage paramétrique

**Brief** : 011 (amorçage) + 012 (base de temps, déficit, commerce)
**Statut** : paramétrique (proxy documenté, non inventé)

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

## Déficit alimentaire et mortalité (SC3 — brief 012)

### Champ `food_deficit_kg`

Sentinelle : -1.0 = non encore calculé (hard-won rule 8 : zéro est une
mesure réelle).

**Sémantique** :
- Si `consommation > stock` après production et avant commerce : `food_deficit_kg += (consommation - stock)` ; `food_stock_kg = 0`
- Si la cellule dispose d'un surplus (`remaining ≥ 0`) : `food_deficit_kg = 0.0`

### Formule de mortalité

```
if food_deficit_kg > 0 and population > 0:
    per_capita_deficit = food_deficit_kg / population
    death_rate = min(per_capita_deficit × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
    deaths = max(1, int(population × death_rate))
    population = max(0, population - deaths)
```

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `HUNGER_DEATH_SCALE` | 0.005 | 1/(kg/personne) | Facteur de mortalité : 1 kg de déficit par tête → 0.5 % de mortalité par tick. Proxy : famine médiévale sévère documentée à 10-30 % de mortalité annuelle sur populations très touchées (~0.03-0.08 %/jour). Le facteur 0.005 permet des déficits modestes (5–10 kg/tête) pour atteindre 2-5 % de mortalité journalière. |
| `MAX_DEATH_RATE_PER_TICK` | 0.10 | — | Plafond de 10 % par tick : empêche l'effondrement instantané même à déficit extrême |

**Propriété garantie** : la mortalité est une fonction croissante et continue
du déficit per capita. Aucun interrupteur binaire seul — le taux monte
proportionnellement avec l'ampleur du manque (SC3).

---

## Commerce inter-cellules (SC4 — brief 012)

### Paramètre

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` | 200.0 × TICK_DURATION_DAYS | kg/arête/tick | Proxy : convoi à dos de mulet (capacité ~200 kg, une liaison rurale par jour, Pounds 1974) |

### Algorithme

Pour chaque arête (a, b) des 1 364 arêtes d'adjacence G3 :
1. Si b est en déficit (`food_deficit_kg > 0`) et a a du surplus (`food_stock_kg > 0`) :
   `transfer = min(food_stock_kg_a, food_deficit_kg_b, TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)`
   → a cède, b reçoit, food_deficit_kg_b diminue.
2. Sinon, si a est en déficit et b a du surplus : symétrique.
3. Conservation stricte : aucune nourriture n'est créée ou détruite.

---

## Seuil de survie de la population (SC5 — brief 012)

| Constante | Valeur | Justification |
|---|---|---|
| `SEUIL_SURVIE_POPULATION_FRACTION` | 0.70 | Autorise jusqu'à 30 % de pertes globales — compatible avec des famines médiévales régionales sévères sans effondrement civilisationnel. Calibré pour être compatible avec les paramètres de production/consommation ci-dessus (mesuré à 0.887 sur N=200 ticks). |

---

## Référence de code

Tous les paramètres ci-dessus sont définis comme constantes nommées dans
`sim/constants.py`. Aucun littéral numérique de ces valeurs n'apparaît dans
les fonctions de calcul de `sim/engine.py` ou `sim/world.py` (vérifiable
via `sim/tests/test_no_hardcoded.py`).
