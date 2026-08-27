# Journal du lot 039 — Le commerce cesse de ne connaître que la nourriture

## Rouge prouvé (SC2)

Sur le SHA de base `476f78cfa266efcaa3c56b6103c0337d7785593a`, le contrôle AST
du maillon `_apply_commerce` compte **5** occurrences du nom de marchandise
alimentaire (constante `MARCHANDISE_NOURRITURE` et littéraux dans le corps).
Après généralisation : **0**.

## Fichiers modifiés

- `sim/engine.py` — maillon commerce paramétré par marchandise, plafond d'arête
  partagé, dérivation des marchandises du monde.
- `sim/constants.py` — accès nommé `consommation_kg_par_habitant_par_tick` et
  marchandise d'essai SC3.
- `sim/tests/test_commerce.py` — six cas ajoutés (SC2 à SC7).
- `harness/queue/briefs/039-le-commerce-porte-tout/deliverables/*` — mesureur,
  manifeste, archives CLI pré-édition.

## Commandes jouées

```bash
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/test_commerce.py -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python harness/queue/briefs/039-le-commerce-porte-tout/deliverables/measure_039.py
```

## Résultats

- SC1 : sorties CLI 20 et 365 ticks byte-identiques au SHA de base.
- SC2 : `occurrences_nourriture_dans_le_maillon_apres=0`, rouge avant prouvé (5).
- SC3 : marchandise d'essai circule ; somme des transferts sur arête partagée = 200 kg (capacité).
- SC4 : `kg_mineraux_ayant_change_de_cellule=0` sur 25 cellules minières.
- SC5 : `ecart_de_masse_par_marchandise=0`.
- SC6 : `modifications_de_dette_par_le_commerce=0`.
- SC7 : deux ordres d'insertion, résultats identiques ; CLI déterministe.
- SC8 : 92 tests collectés, 92 verts ; `maillons_commerce_dans_sim=1`.

## Limites

- Seule la nourriture et la marchandise d'essai ont une consommation non nulle.
- Le minerai ne circule pas tant qu'aucun consommateur n'existe (comportement voulu).
