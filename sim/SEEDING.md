# sim/SEEDING.md — Documentation de l'amorçage paramétrique

**Brief** : 011 — Amorçage du moteur de simulation `sim/`
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

## Population initiale par cellule

### Formule

```
population = max(0, int(area_km2 × INITIAL_POPULATION_PER_KM2 × variation))
```

où `variation = rng.uniform(SEED_POPULATION_VARIATION_LOW, SEED_POPULATION_VARIATION_HIGH)`.

### Paramètres (définis dans `sim/constants.py`)

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `INITIAL_POPULATION_PER_KM2` | 10.0 | hab/km² | Densité médiévale européenne moyenne (ordre de grandeur : 5-20 hab/km², Bairoch 1988) |
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
food_stock_kg = population × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK × INITIAL_FOOD_DAYS
```

Le stock de départ couvre `INITIAL_FOOD_DAYS` ticks de consommation normale,
quelle que soit la production locale. Cela évite une famine artificielle dès
le premier tick dans les cellules à faible rendement.

### Paramètres

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` | 2.0 | kg/(personne·tick) | Ration journalière médiévale approx. 2 kg (céréales + substituts) |
| `INITIAL_FOOD_DAYS` | 30 | ticks | Un mois de réserves : stock de subsistance minimal documenté |

---

## Rendement agricole

### Formule (par tick)

```
food_produced = area_km2 × FOOD_PRODUCTION_KG_PER_KM2_PER_TICK
```

### Paramètre

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` | 50.0 | kg/(km²·tick) | Rendement pré-industriel : ~500 kg/ha/an ÷ 10 (portion cultivée) ÷ 100 (ticks/an proxy) |

---

## Faim et mortalité

| Constante | Valeur | Description |
|---|---|---|
| `HUNGER_DEATH_THRESHOLD` | 3 | Ticks consécutifs de faim avant que la mortalité commence |
| `HUNGER_DEATH_RATE_PER_TICK` | 0.05 | Fraction de la population qui meurt par tick de famine prolongée |

Ces valeurs sont paramétriques. Elles émergent des états intermédiaires
(`hunger_ticks`) lus et écrits à chaque tick — elles ne sont jamais codées
directement comme « si faim alors +N% de mortalité » dans les tests.

---

## Référence de code

Tous les paramètres ci-dessus sont définis comme constantes nommées dans
`sim/constants.py`. Aucun littéral numérique de ces valeurs n'apparaît dans
les fonctions de calcul de `sim/engine.py` ou `sim/world.py` (vérifiable
via `sim/tests/test_no_hardcoded.py`).
