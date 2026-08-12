---
**Author**: forge-generateur
---

# Journal du Générateur — Brief 011 : Amorçage du moteur de simulation `sim/`

## Note de transparence

Ce run a été exécuté par un sous-agent hébergé par Cursor Cloud, en
remplacement de Claude (indisponible ce jour), sur instruction du propriétaire
du projet (2026-08-12). L'orchestration est externe au harnais habituel, mais
les trois rôles séparés (Planificateur / Générateur / Évaluateur) sont
maintenus : le Planificateur a écrit le brief, le Générateur (ce run) produit
les livrables, et l'Évaluateur écrira le verdict de façon indépendante.

---

## Ce qui a été construit

### SC1 — Paquet `sim/` importable

Fichiers créés :
- `sim/__init__.py` — expose `__version__ = "0.1.0"`.
- `sim/constants.py` — constantes paramétriques nommées (documentées dans `sim/SEEDING.md`).
- `sim/model.py` — dataclass `Cell`, classe de garde `_NoBadSpatialField` (ADR-0003).
- `sim/world.py` — classe `World` avec `from_g3()` et `to_dict()`.
- `sim/engine.py` — `tick(world, rng)` et les quatre fonctions de maillon isolées.
- `sim/SEEDING.md` — documentation de l'amorçage paramétrique.
- `sim/README.md` — mis à jour (stub vide jusqu'au brief 011).

Vérification :

```
.venv/bin/python -c "import sim; print(sim.__version__)"
```

Sortie : `0.1.0`

### SC2 — Chargement du monde

`World.from_g3()` charge `cells_g3.json` (596 cellules) et
`adjacency_g3.json` (1364 arêtes). Le nombre de cellules est dérivé du
fichier JSON au moment du chargement via `len(raw_cells_doc["cells"])` —
jamais codé en dur.

### SC3 — ADR-0003 (cell_id seule clé spatiale)

La classe `_NoBadSpatialField` inspecte `__dataclass_fields__` dans
`__post_init__` et lève `TypeError` si un champ nommé `province_id` (ou
variante) est déclaré. `Cell` hérite de cette classe. Deux tests
complémentaires : l'un vérifie que `Cell` n'a pas de tel champ, l'autre
prouve que la garde lève bien l'erreur à l'instanciation.

### SC4 — Amorçage déterministe

`World.from_g3(rng_seed=K)` utilise `random.Random(rng_seed)` (générateur
isolé). Deux appels avec la même graine produisent des populations
byte-identiques. La variation paramétrée (±10 %) est définie par les
constantes `SEED_POPULATION_VARIATION_LOW = 0.9` et
`SEED_POPULATION_VARIATION_HIGH = 1.1`.

### SC5 — Tick déterministe

`tick(world, rng)` prend un `random.Random` fourni par l'appelant. Deux runs
de 10 ticks avec la même graine produisent des condensés SHA256 identiques.
Les condensés sont affichés par leur nom de variable (`hash_run_A`,
`hash_run_B`) — jamais codés en dur (hard-won rule 12).

### SC6 — Économie physique

`Cell` possède `food_stock_kg` (sentinelle -1) et `hunger_ticks` (sentinelle -1).
La production est calculée et écrite dans `food_stock_kg` par `_apply_production`
avant toute consommation. La consommation lit puis modifie ce même champ via
`_apply_consumption`. Rien n'est calculé hors du champ persisté.

### SC7 — Chaîne causale (maillons unitaires + intégration)

Chaque maillon est une fonction séparée dans `engine.py`, testée en isolation :
- SC7a : `_apply_production` + `_apply_consumption` → stock baisse si prod < conso.
- SC7b : `_update_hunger` → `hunger_ticks` progresse si stock ≤ 0.
- SC7c : `_apply_mortality` → population baisse si `hunger_ticks ≥ HUNGER_DEATH_THRESHOLD`.
- SC7d (intégration) : `tick()` complet, cellule à rendement nul → population diminue après 20 ticks.

### SC8 — Couverture d'écriture

`test_write_coverage.py` analyse statiquement `engine.py` par AST. Pour chaque
attribut écrit sur `cell`, il vérifie qu'il est déclaré dans
`Cell.__dataclass_fields__` ET qu'il a aussi un site de lecture. Ce test va
ROUGE si `hunger_ticks` est retiré de Cell (preuve SC10).

### SC9 — Pas de littéraux codés en dur

`test_no_hardcoded.py` inspecte les corps de fonctions de `sim/*.py` et
vérifie l'absence de littéraux numériques en dehors de {0, 0.0, 1, -1, -1.0, 1.0}.
Toutes les valeurs paramétriques sont dans `constants.py`.

### SC10 — Preuve rouge d'abord

Sabotage effectué sur `sim/model.py` (retrait du champ `hunger_ticks`) avec
engine.py inchangé. `test_write_coverage.py` a échoué sur
`test_engine_writes_only_declared_fields` : "hunger_ticks écrits sur 'cell'
dans engine.py mais non déclarés dans Cell.__dataclass_fields__". Sortie
rouge sauvegardée dans `sim/tests/proof_red/run_sabotage.txt`.

Après restauration, les 3 tests de couverture passent. Sortie verte dans
`sim/tests/proof_red/run_correct.txt`. Les deux fichiers diffèrent de 70 lignes.

---

## Mesure de chaque compteur

### `cells_chargees`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_world.py::test_cells_count_matches_stats -v -s
```

Sortie :
```
cells_chargees (chargées) = 596
cell_count (stats_g3.json) = 596
cells_chargees == cell_count : True
PASSED
```

Valeur : **596** — sample_size : 596

### `aretes_adjacence_chargees`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_world.py::test_adjacency_count_matches_file -v -s
```

Sortie :
```
aretes_adjacence_chargees (chargées) = 1364
longueur adjacency_g3.json = 1364
aretes_adjacence_chargees == len(adjacency) : True
PASSED
```

Valeur : **1364** — sample_size : 1364

### `champs_modele_couverts`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_write_coverage_counter -v -s
```

Sortie :
```
champs déclarés dans Cell : ['area_km2', 'cell_id', 'food_stock_kg', 'hunger_ticks', 'population']
champs écrits dans engine.py : ['food_stock_kg', 'hunger_ticks', 'population']
champs lus dans engine.py : ['area_km2', 'food_stock_kg', 'hunger_ticks', 'population']
champs_modele_couverts = 3 / 5
PASSED
```

Valeur : **3** — sample_size : 5 (total champs déclarés dans Cell)

### `compteurs_en_dur_trouves`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_no_hardcoded.py -v -s
```

Sortie :
```
fichiers inspectés : ['constants.py', 'engine.py', 'model.py', 'world.py']
compteurs_en_dur_trouves = 0
PASSED
```

Valeur : **0** — sample_size : 4 (fichiers inspectés)

### `amorçage_deterministe_valide`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_seeding.py::test_seeding_determinisme -v -s
```

Sortie :
```
amorçage_deterministe_valide = 1
cellules divergentes = 0
PASSED
```

Valeur : **1** (identique) — sample_size : 1

### `ticks_deterministes_valides`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_engine.py::test_tick_determinisme -v -s
```

Sortie :
```
hash_run_A = 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
hash_run_B = 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
égaux : True
ticks_deterministes_valides = 1
PASSED
```

Note : les condensés sont affichés par leur nom de variable. L'égalité est
affirmée par comparaison de variables — jamais par recopie d'une valeur en dur
dans un test (hard-won rule 12). La valeur ci-dessus est la sortie réelle du test.

Valeur : **1** — sample_size : 1

### `maillons_chaine_causale_testes_unitairement`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_causal_chain.py::test_sc7a_stock_decreases_when_production_lt_consumption sim/tests/test_causal_chain.py::test_sc7b_hunger_ticks_increments_when_stock_empty sim/tests/test_causal_chain.py::test_sc7c_population_decreases_when_hunger_above_threshold -v -s
```

Sortie :
```
stock_before = 1000.0, stock_after = 800.05   → SC7a PASSED
hunger_before = 0, hunger_after = 1            → SC7b PASSED
population_before = 100, population_after = 95, HUNGER_DEATH_THRESHOLD = 3  → SC7c PASSED
3 passed
```

Valeur : **3** — sample_size : 3 (maillons attendus)

### `test_integration_bout_en_bout_resultat`

Commande :
```
.venv/bin/python -m pytest sim/tests/test_causal_chain.py::test_sc7d_zero_yield_leads_to_population_decline -v -s
```

Sortie :
```
population_initiale = 200
population_finale = 85
test_integration_bout_en_bout_resultat = PASS
PASSED
```

Valeur : **1** (PASS) — sample_size : 1

### `lignes_differentes_preuve_rouge`

Commande :
```
diff sim/tests/proof_red/run_sabotage.txt sim/tests/proof_red/run_correct.txt | wc -l
```

Sortie : `70`

Valeur : **70** — sample_size : 70

---

## Sortie complète des trois auto-contrôles

### 1. `.venv/bin/python -m pytest sim/tests/ -v`

```
============================= test session starts ==============================
platform linux -- Python 3.12.3, pytest-9.1.1, pluggy-1.6.0 -- /workspace/.venv/bin/python
cachedir: .pytest_cache
rootdir: /workspace
collecting ... collected 18 items

sim/tests/test_adr_compliance.py::test_cell_has_no_province_id_field PASSED [  5%]
sim/tests/test_adr_compliance.py::test_province_id_field_raises_explicit_error PASSED [ 11%]
sim/tests/test_adr_compliance.py::test_province_id_variant_raises_explicit_error PASSED [ 16%]
sim/tests/test_causal_chain.py::test_sc7a_stock_decreases_when_production_lt_consumption PASSED [ 22%]
sim/tests/test_causal_chain.py::test_sc7b_hunger_ticks_increments_when_stock_empty PASSED [ 27%]
sim/tests/test_causal_chain.py::test_sc7c_population_decreases_when_hunger_above_threshold PASSED [ 33%]
sim/tests/test_causal_chain.py::test_sc7d_zero_yield_leads_to_population_decline PASSED [ 38%]
sim/tests/test_engine.py::test_tick_determinisme PASSED                  [ 44%]
sim/tests/test_engine.py::test_tick_different_seeds_differ PASSED        [ 50%]
sim/tests/test_no_hardcoded.py::test_no_hardcoded_numeric_literals PASSED [ 55%]
sim/tests/test_seeding.py::test_seeding_determinisme PASSED              [ 61%]
sim/tests/test_seeding.py::test_different_seeds_give_different_populations PASSED [ 66%]
sim/tests/test_world.py::test_cells_count_matches_stats PASSED           [ 72%]
sim/tests/test_world.py::test_adjacency_count_matches_file PASSED        [ 77%]
sim/tests/test_world.py::test_cells_have_required_fields PASSED          [ 83%]
sim/tests/test_write_coverage.py::test_engine_writes_only_declared_fields PASSED [ 88%]
sim/tests/test_write_coverage.py::test_engine_written_fields_also_have_read_sites PASSED [ 94%]
sim/tests/test_write_coverage.py::test_write_coverage_counter PASSED     [100%]

============================== 18 passed in 0.35s ==============================
```

Exit code 0. Tous les 18 tests PASSED.

### 2. `.venv/bin/python -m pytest harness/tests/ -q`

```
314 passed, 16 skipped in 16.75s
```

Exit code 0. Aucun test du harnais cassé.

Note : la baseline citée par l'orchestrateur était « 305 passed, 16 skipped ». La
différence (314 vs 305) correspond aux 9 tests ajoutés par le brief 010 lors de
la session précédente, avant ce run. Aucun test cassé.

### 3. `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`

```
# verdict_audit report for harness/queue/briefs/011-sim-monde-vivant-amorcage
# generated_at: 2026-08-12T16:23:23.919419
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[FAIL] verdict_numbers_traceable: verdict.md missing
[PASS] no_bare_python_alias: no bare `python` invocations found
[FAIL] verdict_is_not_self_authored: Author frontmatter missing on generator-log.md or verdict.md
[PASS] rubric_predates_deliverables: rubric (2026-08-12 15:57:00) predates earliest deliverable (2026-08-12 16:13:25.717449)
[PASS] declared_files_are_tracked: all 2 in-brief declared files are tracked; 18 declared outside the brief dir, not checked

VERDICT: REJECT
```

Les deux FAIL (`verdict_numbers_traceable` et `verdict_is_not_self_authored`) sont attendus :
`verdict.md` n'existe pas encore — l'Évaluateur l'écrira après ce run.
Les 8 autres contrôles sont tous PASS.
