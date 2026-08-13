# Journal du Générateur — Brief 012

**Authored**: 2026-08-13T07:00:00Z
**Author**: forge-generateur

---

## Note de transparence

Le rôle Générateur a tourné comme sous-agent hébergé par Cursor, en remplacement de Claude indisponible directement, sur instruction du propriétaire, dans une session distincte de celles du Planificateur et de l'Évaluateur. Conformément au contrat de harnais trois-rôles (harness-roles.md), le Générateur ne prononce pas la recevabilité de son propre travail.

**Normalisation de la signature (itération 2 — décision d'orchestration).** La ligne `**Author**:` de l'itération 1 portait `forge-generateur-cursor`. Sur décision de l'orchestrateur, elle a été normalisée en `forge-generateur` (rôle natif), afin que le contrôle `verdict_is_not_self_authored` puisse distinguer l'acteur Générateur de l'acteur Évaluateur par leur rôle, non par leur suffixe de backend. L'acteur réel (sous-agent hébergé par Cursor) reste déclaré en prose dans cette note. L'angle mort structurel — un couple `forge-generateur` / `forge-evaluateur` sans suffixe de backend ne peut pas être distingué mécaniquement si les deux roulent sous le même backend natif — est documenté et sa fermeture mécanique (traçage d'acteur hors chaînes auto-déclarées) est différée au brief de harnais issu du point 1 de l'audit `CURSOR-3b47ffe-pr57-monde-sans-faim` (voir Non-Goals du brief 012).

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

---

## Itération 2 — Corrections B1, B2, N1, N2, N3

**Authored**: 2026-08-13T07:40:00Z
**Author**: forge-generateur

Itération déclenchée par le feedback `feedback-001.md` (REJECT itération 1).
Les cinq points ci-dessous correspondent aux issues B1, B2, N1, N2, N3.
R4 reste optionnel, non traité.

### B1 — Restauration du compteur `lignes_differentes_preuve_rouge_iter1` dans le manifeste du lot 011

**Problème** : l'entrée avait été retirée, laissant du JSON invalide (virgule orpheline). La porte 2 (retrait autorisé) n'était pas satisfaite car le verdict.md et le generator-log.md du lot 011 citent tous deux cette valeur.

**Correction** : restauration de l'entrée avec la commande git-archivée qui lit les fichiers dans l'état du commit d'itération 1 du lot 011 (`aec84f1`).

Commande exécutée :
```
diff <(git show aec84f1:sim/tests/proof_red/run_sabotage.txt) <(git show aec84f1:sim/tests/proof_red/run_correct.txt) | wc -l
```

Sortie : `70` — valeur archivée confirmée.

Validation gate 011 :
```
.venv/bin/python -c "import json; json.load(open('harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json')); print('JSON valide')"
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage
```

Sortie gate 011 : JSON valide, `VERDICT: ACCEPT` (tous les contrôles au vert).

### B2 — Correction de la commande `cellules_affamees_monde_reel`

**Problème** : la commande en ligne incorporait l'appel à `tick()` dans l'expression génératrice, ce qui avançait le monde une fois par cellule et court-circuitait l'opérateur `or` sur la valeur retournée par tick (kg transportés). Elle affichait 579 au lieu de 261.

**Correction** : script de mesure déposé dans `deliverables/measure_cellules_affamees.py`, qui sépare correctement la boucle de ticks de l'accumulation des cellules affamées.

Commande exécutée :
```
.venv/bin/python harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/deliverables/measure_cellules_affamees.py
```

Sortie : `261` — valeur du compteur confirmée.

### N1 — Correction de la commande `constantes_temporelles_coherentes`

**Problème** : la commande testait la présence de `TICK_DURATION_DAYS` dans l'ensemble du fichier, pas dans la ligne d'affectation de chaque constante. Elle retournait 3 même si aucune dérivation n'était présente.

**Correction** : vérification ligne par ligne — pour chaque nom de constante, la ligne qui commence par ce nom doit contenir `TICK_DURATION_DAYS`.

Commande exécutée :
```
.venv/bin/python -c "import pathlib; src=pathlib.Path('sim/constants.py').read_text(); names=['FOOD_PRODUCTION_KG_PER_KM2_PER_TICK','FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK','TRADE_CAPACITY_KG_PER_EDGE_PER_TICK']; print(sum(1 for line in src.splitlines() for n in names if line.startswith(n) and 'TICK_DURATION_DAYS' in line))"
```

Sortie : `3`.

**Validation red** : dans une copie hors dépôt, suppression du facteur `* TICK_DURATION_DAYS` de `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` → la commande retourne `2` (pas `3`). La garde est opérante.

**Justification de la substitution `INITIAL_FOOD_RESERVE_TICKS`** : voir `sim/SEEDING.md` § « Justification du dénominateur de `constantes_temporelles_coherentes` ». En résumé : la constante est désormais exprimée en ticks (unité canonique du moteur — c'est précisément la correction demandée par SC1). La multiplier par `TICK_DURATION_DAYS` produirait une dimension tick×tick, incorrect. Le dénominateur du compteur porte donc sur les trois constantes réellement dérivées.

### N2 — Correction des superficies de test dans `test_causal_chain.py`

**Problème** : SC7a utilisait `area_km2=0.001` sans annotation ; SC7b utilisait `area_km2=0.0` sans annotation.

**Correction** :
- SC7a : superficie remontée à `1.0` km², population ajustée à `5000` pour conserver l'écart production/consommation (production max ≈ 27 kg/tick << consommation 10 000 kg/tick).
- SC7b : superficie remontée à `1.0` km² (la superficie n'est pas lue par `_update_hunger`, le test conserve son sens, le plancher est respecté).

Validation : `.venv/bin/python -m pytest sim/tests/test_causal_chain.py -v` → 4 PASSED.

### N3 — Régénération de la paire proof rouge/vert couverture étendue

**Problème** : le sabotage de l'itération 1 ajoutait un champ fantôme à la dataclass existante (`Cell`), ce qui testait la capacité du lot 011, pas la capacité nouvelle R2 (découverte d'une dataclass entière par introspection).

**Correction** : nouveau sabotage dans `/tmp/sabotage-012/workspace/sim/model.py` — ajout d'une **nouvelle dataclass** `SabotageDataclass` avec un champ `orphan_field` sans écrivain ni lecteur. Le test nommant la classe et le champ fautifs (`SabotageDataclass.orphan_field`), la capacité R2 est exercée.

Sortie rouge (sabotage) :
```
FAILED sim/tests/test_write_coverage.py::test_all_dataclass_fields_have_write_and_read_sites
FAILED sim/tests/test_write_coverage.py::test_write_coverage_counter_etendu
AssertionError: Couverture d'écriture incomplète :
  - SabotageDataclass.orphan_field : aucun site d'écriture
  - SabotageDataclass.orphan_field : aucun site de lecture
2 failed, 1 passed
```

Sortie verte (code correct) :
```
3 passed
```

Diff des fichiers : `diff sim/tests/proof_red/run_coverage_ext_red.txt sim/tests/proof_red/run_coverage_ext_green.txt | wc -l` → `136`.

Pour la traçabilité du verdict itération 1 (qui cite `132`), le compteur `lignes_differentes_couverture_ext_rouge_vert` pointe vers les fichiers archivés dans le commit `444ec45` (iter 1) et rend `132`. Un second compteur `lignes_differentes_couverture_ext_rouge_vert_iter2` pointe vers les fichiers courants et rend `136`.

### État des gates et suites après corrections

**Gate lot 011** : `VERDICT: ACCEPT` (10 contrôles au vert).

**Gate lot 012** :
```
[PASS] files_declared_exist
[PASS] mtime_after_brief
[PASS] captures_differ_when_should
[PASS] waivers_have_command_and_error
[PASS] no_empty_sample_pass
[PASS] verdict_numbers_traceable
[PASS] no_bare_python_alias
[PASS] verdict_is_not_self_authored
[PASS] rubric_predates_deliverables
[PASS] declared_files_are_tracked
VERDICT: ACCEPT
```

**Suite `sim/tests/`** : 25 tests PASSED.

**Suite `harness/tests/`** : 314 passed, 16 skipped (les skips sont les tests Unity/PowerShell, attendus sur Linux).

### Confirmation

Ni committé, ni poussé, ni aucune branche créée dans cette itération.
