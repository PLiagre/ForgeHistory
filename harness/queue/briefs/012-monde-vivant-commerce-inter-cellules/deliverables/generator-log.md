# Journal du Générateur — Brief 012

**Authored**: 2026-08-13T07:00:00Z
**Author**: forge-generateur-cursor

---

## Note de transparence

Le rôle Générateur a tourné comme sous-agent hébergé par Cursor (forge-generateur-cursor), en remplacement de Claude indisponible directement, sur instruction du propriétaire, dans une session distincte de celles du Planificateur et de l'Évaluateur. Conformément au contrat de harnais trois-rôles (harness-roles.md), le Générateur ne prononce pas la recevabilité de son propre travail.

---

## Budget d'exécution — déclaration pré-vol

Commande : `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules`

Sortie obtenue :
```
status     : UNMEASURABLE
reason     : no agent transcript naming 012-monde-vivant-commerce-inter-cellules under /home/ubuntu/.claude/projects/-workspace
Nothing is being enforced. This is not OK -- it is unmeasured.
```

Dérogation appliquée : « le budget d'exécution n'est pas mesurable sur cette machine (hors session Claude locale) ». La dérogation est déclarée dans `waivers` du manifeste avec la commande et la sortie exigées (brief § Acceptable Waivers).

Commande de pré-vol : `.venv/bin/python harness/budget.py split-check --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules --estimated-calls 120` → résultat : `SIZE_OK`.

---

## Travail effectué par Success Condition

### SC1 — Base de temps unique, constantes alignées, noms corrigés

**`sim/constants.py`** : ajout de `TICK_DURATION_DAYS = 1`. Toutes les constantes temporelles multiplient cette valeur :

- `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 18.0 * TICK_DURATION_DAYS`
- `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0 * TICK_DURATION_DAYS`
- `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK = 200.0 * TICK_DURATION_DAYS`

Renommages (constat P3-2 de l'audit) :
- `INITIAL_FOOD_DAYS` → `INITIAL_FOOD_RESERVE_TICKS` (l'unité est un tick, pas un jour calendaire)
- Variable locale `daily_need` dans `sim/world.py` → `tick_need`

Vérification :
```
.venv/bin/python -c "from sim.constants import TICK_DURATION_DAYS; assert TICK_DURATION_DAYS > 0; print('tick =', TICK_DURATION_DAYS, 'jour(s)')"
tick = 1 jour(s)
```

Valeur du compteur `tick_duration_days = 1` (1 constante déclarée).
Valeur du compteur `constantes_temporelles_coherentes = 3` (3 constantes × TICK_DURATION_DAYS sur 3 déclarées).

### SC2 — Le rng est consommé à chaque tick

**`sim/engine.py`** : `_apply_production(cell, rng)` multiplie le rendement par `rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)`. Le `rng` est passé à `tick(world, rng)` et consommé pour chaque cellule à chaque tick.

Nouveau fichier `sim/tests/test_rng.py` avec 3 tests :

1. **`test_rng_etat_change_apres_tick`** : `rng.getstate()` avant ≠ après 10 ticks → `rng_etat_change_apres_tick = True`
2. **`test_ticks_deterministes_meme_graine`** : deux runs, world_seed=42, rng_seed=42, N=200 → condensés égaux (cités par nom de variable `hash_run_A` et `hash_run_B` — hard-won rule 12) → `ticks_deterministes_meme_graine = True`
3. **`test_ticks_differents_graines_rng_differentes`** : world_seed=42, rng_seed=42 vs 999, N=200 → condensés différents (`hash_graine_42` ≠ `hash_graine_999`) → `ticks_differents_graines_rng_differentes = True`

Sortie de la mesure (`.venv/bin/python -m pytest sim/tests/test_rng.py -v -s`) :
```
sim/tests/test_rng.py::test_rng_etat_change_apres_tick PASSED
sim/tests/test_rng.py::test_ticks_deterministes_meme_graine PASSED
sim/tests/test_rng.py::test_ticks_differents_graines_rng_differentes PASSED
3 passed in 0.55s
```

### SC3 — Le déficit alimentaire est un état persisté

**`sim/model.py`** : ajout de `food_deficit_kg: float = field(default=-1.0)` (sentinelle -1.0 = non encore calculé, hard-won rule 8).

**`sim/engine.py`** — `_apply_consumption` :
- Si surplus : `food_deficit_kg = 0.0` (reset)
- Si manque : `food_deficit_kg += shortage`

**`sim/engine.py`** — `_apply_mortality` (SC3, formule proportionnelle — pas d'interrupteur binaire seul) :
```
per_capita_deficit = food_deficit_kg / population
death_rate = min(per_capita_deficit * HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
deaths = max(1, int(population * death_rate))
```

Paramètres : `HUNGER_DEATH_SCALE = 0.005`, `MAX_DEATH_RATE_PER_TICK = 0.10`.

Compteur `food_deficit_kg_ecrit_quand_manque = 1` (PASS) — mesuré par `.venv/bin/python -m pytest sim/tests/test_commerce.py::test_deficit_accumule_quand_manque -v -s`.

### SC4 — Commerce inter-cellules physique

**`sim/engine.py`** — nouveau maillon `_apply_commerce(world, total_transported)` :
- Itère sur les 1 364 arêtes de `world.adjacency` (format G3 : champs `'a'` et `'b'`)
- Direction bidirectionnelle : si cellule_b en déficit et cellule_a en surplus → transfert a→b ; sinon inverse
- `transfer = min(surplus_source, deficit_destination, TRADE_CAPACITY_KG_PER_EDGE_PER_TICK)`
- Conservation stricte : `food_stock_kg_source -= transfer`, `food_stock_kg_destination += transfer`, `food_deficit_kg_destination -= transfer`

Compteur `conservation_masse_transport = 1` (PASS) — mesuré par `.venv/bin/python -m pytest sim/tests/test_commerce.py::test_conservation_masse_transport -v -s` :
```
somme_avant = 5000.0
somme_apres = 5000.0
écart = 0.0
conservation_masse_transport = True
kg_transportes = 200.0
PASSED
```

### SC5 — Le monde vit, mesuré sur les 596 cellules réelles

**Calibration choisie et justifiée** (voir sim/SEEDING.md pour les dérivations) :
- `TICK_DURATION_DAYS = 1` (1 jour/tick)
- `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 18.0` (proxy : ~6 570 kg/km²/an ÷ 365 ≈ 18 kg/km²/jour)
- `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0` (ration journalière médiévale 2 kg)
- `INITIAL_FOOD_RESERVE_TICKS = 5` (5 jours de réserves — buffer court pour que la dynamique prenne effet)
- Rendement aléatoire : `rng.uniform(0.5, 1.5)` → à 10 hab/km², production moyenne = 18 kg < consommation 20 kg, avec 39 % des ticks en surplus (trade possible)
- `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK = 200.0` (convoi à dos de mulet, Pounds 1974)
- Mortalité : `HUNGER_DEATH_SCALE = 0.005` (0,5 %/tick par kg de déficit per capita)
- `SEUIL_SURVIE_POPULATION_FRACTION = 0.70` (choix documenté dans SEEDING.md)

**Mesure réelle (script rejoué depuis la racine)** :
```
# .venv/bin/python calibration_script.py (voir ci-dessous)
import random; from sim.world import World; from sim.engine import tick
world=World.from_g3(rng_seed=42); rng=random.Random(42)
p0=sum(c.population for c in world.cells.values())
cellules_avec_faim=set(); kg_total=0.0
for t in range(200):
    kg_t=tick(world,rng); kg_total+=kg_t
    for cid,c in world.cells.items():
        if c.hunger_ticks>0: cellules_avec_faim.add(cid)
pf=sum(c.population for c in world.cells.values())
```

Résultats :
```
cellules_affamees_monde_reel = 261   (> 0 ✓)
morts_cumules_monde_reel     = 7 544 299  (> 0 ✓)
kg_transportes_monde_reel    = 8 171 507.4  (> 0 ✓)
population_finale_positive   = 0.887172  (> 0.70 ✓)
```

Toutes les 4 conditions SC5 sont simultanément satisfaites.

### SC6 — sim/tests/ dans la CI

**`.github/workflows/harness-ci.yml`** : ajout d'un job `sim-tests` :
Le job `sim-tests` dans `.github/workflows/harness-ci.yml` installe pytest via pip et exécute `pytest sim/tests/ -v` avec l'interpréteur fourni par `actions/setup-python` (Python 3.13).

Commande CI rejouée localement : `.venv/bin/python -m pytest sim/tests/ -v` → exit code 0, 25 tests PASSED.

Compteur `ci_sim_tests_collectes = 25` (mesuré par `.venv/bin/python -m pytest sim/tests/ --collect-only -q`).

### SC7 — Réserves R1-R4 fermées, couverture étendue

**R1** : entrée `lignes_differentes_preuve_rouge_iter1` retirée de `harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json` (commande non reproductible citant un compteur obsolète).

**R2** : `test_write_coverage.py` réécrit. `_discover_dataclasses()` utilise `inspect.getmembers(sim.model, inspect.isclass)` filtré par `dataclasses.is_dataclass()` — aucune classe nommée en dur.

**R3** : `_scan_writes_typed()` exige que la variable cible (`target.value.id`) appartienne à l'ensemble `{var_name}` (ex. `'cell'` pour `Cell`). Une affectation `autre_objet.food_stock_kg = X` ne compte pas.

**Extension** :
- `food_deficit_kg` de `Cell` couvert (écriture dans `engine.py`, lecture dans `engine.py`)
- `World.adjacency` : test `test_adjacency_is_read_by_engine` vérifie qu'au moins un module lit `adjacency` en contexte `Load`

Compteur `champs_modele_couverts_etendu = 7` (6 champs Cell + 1 adjacency) sur 7 total.

Sortie :
```
dataclasses découvertes : ['Cell']
champs dans dataclasses : 6
champs couverts (écriture + lecture) : 6
adjacency couverte : 1
champs_modele_couverts_etendu = 7 / 7
PASSED
```

### Paires de preuve rouge (hard-won rule 4)

**Paire 1 — transport-conservatif** :
- Sabotage : dans `_apply_commerce`, la ligne `cell_a.food_stock_kg -= transfer` (direction a→b) supprimée → le transfert crée de la masse.
- Résultat rouge : `test_conservation_masse_transport FAILED` (écart = 200.0 kg, masse non conservée)
- Résultat vert : `test_conservation_masse_transport PASSED` (écart = 0.0)
- `lignes_differentes_transport_rouge_vert = 78` (`diff run_transport_red.txt run_transport_green.txt | wc -l`)

**Paire 2 — couverture étendue** :
- Sabotage : ajout d'un champ fantôme `phantom_field: float` dans `Cell` sans écrivain ni lecteur
- Résultat rouge : `test_all_dataclass_fields_have_write_and_read_sites FAILED` + `test_write_coverage_counter_etendu FAILED`
- Résultat vert : les mêmes tests PASSED sur code correct
- `lignes_differentes_couverture_ext_rouge_vert = 132` (`diff run_coverage_ext_red.txt run_coverage_ext_green.txt | wc -l`)

Les deux sabotages ont été effectués dans `/tmp/sabotage-012/workspace/` (copie hors dépôt — l'Exécution Contract exige de ne pas modifier le dépôt durant la phase de sabotage). Les sorties ont été recopiées dans `sim/tests/proof_red/`.

### SC8 — Registre de coût

```
.venv/bin/python harness/backends/ledger.py append --backend cursor --brief harness/queue/briefs/012-monde-vivant-commerce-inter-cellules --event generator-run --audit-id CURSOR-3b47ffe-pr57-monde-sans-faim
```

Sortie : `logged: {'timestamp': '2026-08-13T06:59:08.650798', 'backend': 'cursor', ...}`

---

## Sorties des suites de tests (complètes)

### `.venv/bin/python -m pytest harness/tests/ -q`

```
........................................................................ [ 43%]
........................................................................ [ 65%]
...........................ssssssssssssssss............................. [ 87%]
..........................................                               [100%]
314 passed, 16 skipped in 17.64s
```

(Les 16 skips sont les tests Unity/PowerShell — comportement attendu sur Linux.)

### `.venv/bin/python -m pytest sim/tests/ -v`

```
sim/tests/test_adr_compliance.py::test_cell_has_no_province_id_field PASSED
sim/tests/test_adr_compliance.py::test_province_id_field_raises_explicit_error PASSED
sim/tests/test_adr_compliance.py::test_province_id_variant_raises_explicit_error PASSED
sim/tests/test_adr_compliance.py::test_province_short_name_raises_explicit_error PASSED
sim/tests/test_adr_compliance.py::test_province_code_raises_explicit_error PASSED
sim/tests/test_causal_chain.py::test_sc7a_stock_decreases_when_production_lt_consumption PASSED
sim/tests/test_causal_chain.py::test_sc7b_hunger_ticks_increments_when_stock_empty PASSED
sim/tests/test_causal_chain.py::test_sc7c_population_decreases_when_deficit_positive PASSED
sim/tests/test_causal_chain.py::test_sc7d_zero_yield_leads_to_population_decline PASSED
sim/tests/test_commerce.py::test_deficit_accumule_quand_manque PASSED
sim/tests/test_commerce.py::test_conservation_masse_transport PASSED
sim/tests/test_engine.py::test_tick_determinisme PASSED
sim/tests/test_engine.py::test_tick_different_seeds_differ PASSED
sim/tests/test_no_hardcoded.py::test_no_hardcoded_numeric_literals PASSED
sim/tests/test_rng.py::test_rng_etat_change_apres_tick PASSED
sim/tests/test_rng.py::test_ticks_deterministes_meme_graine PASSED
sim/tests/test_rng.py::test_ticks_differents_graines_rng_differentes PASSED
sim/tests/test_seeding.py::test_seeding_determinisme PASSED
sim/tests/test_seeding.py::test_different_seeds_give_different_populations PASSED
sim/tests/test_world.py::test_cells_count_matches_stats PASSED
sim/tests/test_world.py::test_adjacency_count_matches_file PASSED
sim/tests/test_world.py::test_cells_have_required_fields PASSED
sim/tests/test_write_coverage.py::test_all_dataclass_fields_have_write_and_read_sites PASSED
sim/tests/test_write_coverage.py::test_adjacency_is_read_by_engine PASSED
sim/tests/test_write_coverage.py::test_write_coverage_counter_etendu PASSED
25 passed in 0.89s
```

---

## État attendu du gate mécanique

`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules`

L'état attendu avant que l'Évaluateur écrive `verdict.md` est REJECT avec exactement les contrôles `verdict_numbers_traceable` et `verdict_is_not_self_authored` en échec (absence de `verdict.md`), et `declared_files_are_tracked` peut aussi échouer si l'orchestrateur n'a pas encore committé. Cet état est normal — le Générateur ne committe pas.

---

## Décisions de calibration

| Paramètre | Valeur choisie | Raison |
|---|---|---|
| `TICK_DURATION_DAYS` | 1 | Unité naturelle agronomique ; toutes les constantes temporelles dérivées |
| `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` | 18.0 | ≈ 6 570 kg/km²/an ÷ 365, légèrement sous la consommation pour créer déficits locaux |
| `INITIAL_FOOD_RESERVE_TICKS` | 5 | Buffer court pour que les dynamiques agissent en 200 ticks |
| `RNG_YIELD_LOW / HIGH` | 0.5 / 1.5 | Variabilité ±50 % : crée surplus (39 % des ticks) pour le trade et déficits |
| `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` | 200.0 | Proxy convoi à mulet (Pounds 1974) |
| `HUNGER_DEATH_SCALE` | 0.005 | 1 kg/tête de déficit → 0,5 % de mortalité par tick ; calibré pour ~11 % de pertes globales sur 200 ticks |
| `MAX_DEATH_RATE_PER_TICK` | 0.10 | Plafond empirique, évite l'effondrement immédiat |
| `SEUIL_SURVIE_POPULATION_FRACTION` | 0.70 | Marge ample (mesuré à 0.887), autorise des pertes locales sévères |

---

## Écarts et impossibilités

Aucun écart au brief. Aucune dérogation autre que celle sur le budget UNMEASURABLE (ci-dessus).

---

**Celui qui produit ne prononce pas la recevabilité.**
