# Lot 043-bis — Le monde d'épreuve exerce longueur et repli

**generator-log.md** — 2026-08-30

## Portillon SC1 — avant toute édition

Base produit : `4b732778fc7970ce3e0e108369adc5ff60b5a2a5`
HEAD réel : `e9fae56f7cd93da9dd99454697ae35d8849e480e`

```bash
git rev-parse HEAD
# e9fae56f7cd93da9dd99454697ae35d8849e480e

git merge-base --is-ancestor 4b732778fc7970ce3e0e108369adc5ff60b5a2a5 HEAD
# exit 0

git diff --cached --name-only
# (vide)

git diff --name-only 4b732778fc7970ce3e0e108369adc5ff60b5a2a5 -- . ':(exclude)harness/queue/briefs/043-bis-monde-epreuve-longueurs/brief.md'
# (vide)

git status --short --untracked-files=all -- . ':(exclude)harness/queue/briefs/043-bis-monde-epreuve-longueurs/brief.md'
# (vide)
```

Rouge de base (commande ciblée avant édition) :

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q
```

```
FAILED sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde
AssertionError: Remplacer ces constantes en mémoire ne change rien au monde d'épreuve : ['DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK'].
constantes_du_moteur_atteignables = 9 / 10
```

## Correction appliquée

Fichier unique de harnais : `sim/tests/test_write_coverage.py`, fixture `_MondeEpreuve` :

- arête 1-2 : `shared_length_m=1000.0` (mètres) — exerce `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` ;
- arête 1-3 : sans `shared_length_m` — exerce le repli `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` ;
- cellule 3 : population `50` → `1000` (besoin de commerce que le repli de 200 kg ne couvre pas entièrement ; valeur anti-faux-vert du fixture, pas une règle produit) ;
- docstring : « en équilibre » remplacé par « fort besoin de commerce (repli arête 1-3) ».

Diff borné contre `4b732778` :

```diff
-    et une troisième voisine en équilibre.
+    et une troisième voisine avec un fort besoin de commerce (repli arête 1-3).
-            3: Cell(cell_id=3, area_km2=10.0, population=50,
+            3: Cell(cell_id=3, area_km2=10.0, population=1000,
-        self.adjacency = [{"a": 1, "b": 2}, {"a": 1, "b": 3}]
+        self.adjacency = [{"a": 1, "b": 2, "shared_length_m": 1000.0}, {"a": 1, "b": 3}]
```

Aucune modification de `sim/engine.py`, `sim/constants.py` ni du lot 044.

## Preuve ciblée verte (SC3)

```bash
.venv/bin/python -m pytest sim/tests/test_write_coverage.py::test_chaque_constante_du_moteur_change_le_monde -q -s
```

```
constantes_du_moteur_atteignables = 10 / 10
1 passed in 0.03s
```

Le rapport `constantes_du_moteur_atteignables` est dérivé par le contrôle lui-même. Assertions, facteurs `(0.1, 3.0, 1e6)`, `_constantes_consultees_par_le_moteur` et logique `inertes` inchangés.

## Suite complète (SC4)

```bash
.venv/bin/python -m pytest sim/tests/ -q
```

```
127 passed in 2991.60s (0:49:51)
```

Collecte : 127 tests (identique à la base `4b732778`). Aucun test sauté, xfail, filtré ou supprimé.
