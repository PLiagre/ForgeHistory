# Journal d'exécution — brief 034

SHA de base : `8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1`

## Rouge prouvé avant correction de `sim/engine.py`

### SC1 — instructions `global` dans le moteur

Parcours AST de `sim/engine.py` sur le SHA de base :

```
fonctions_moteur_inspectees=12
fonctions_avec_global=2
fonctions_fautives=['production_moyenne_kg_par_tick', 'tick']
```

Le contrôle `test_aucune_instruction_global_dans_le_moteur` échoue sur ce SHA
avec : `instructions global interdites dans : ['production_moyenne_kg_par_tick', 'tick']`.

### SC2 — point d'entrée absent et état de module pendant le tick

Import de `production_du_tick_kg` sur le SHA de base :

```
ImportError: cannot import name 'production_du_tick_kg' from 'sim.engine'
```

Instrumentation de `world.carte` pendant un tick complet sur le SHA de base :

```
lectures_de_carte_pendant_le_tick=596
lectures_voyant_un_etat_de_module=596
```

Chaque lecture voit `_carte_du_tick` renseigné par `tick()` — rouge attendu pour SC2c.

## Fichiers modifiés

- `sim/engine.py` : ajout de `production_du_tick_kg` ; suppression des `global`
  dans `tick` et `production_moyenne_kg_par_tick` ; `_apply_production` reçoit
  la carte en argument optionnel ; le tick ne pose plus `_carte_du_tick`.
- `sim/tests/test_monde.py` : ajouts en fin de fichier uniquement (SC1, SC2a–c).
- `harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/` : mesureur,
  manifeste, archives CLI et ce journal.

## Commandes jouées

```bash
.venv/bin/python -m pytest sim/tests/test_monde.py -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 200 --seed 42 --json
.venv/bin/python harness/queue/briefs/034-moteur-sans-etat-cache/deliverables/measure_034.py
grep -n '_carte_du_tick\|global ' sim/engine.py
git diff 8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1 -- sim/tests/test_survie.py
git diff 8bc3ce03a25dc2452eab3eebf5bb49fd511b0ad1 -- sim/tests/test_monde.py
```

## Résultats

- 69 tests `sim/tests/` verts (dont 4 nouveaux contrôles brief 034).
- Sorties CLI 20 ticks graine 0 et 200 ticks graine 42 byte-identiques au SHA
  de base (comparaison via archives `pre-edit/` lues depuis git).
- `sim/tests/test_survie.py` inchangé (diff vide).
- `fonctions_avec_global=0`, `lectures_voyant_un_etat_de_module=0` après correction.

## Limites

- `_carte_du_tick` subsiste pour le repli de `production_kg()` et le test du
  lot 033 ; aucune ligne de `sim/` ne l'écrit plus.
- Le climat et les gisements ne sont pas dans le périmètre.
