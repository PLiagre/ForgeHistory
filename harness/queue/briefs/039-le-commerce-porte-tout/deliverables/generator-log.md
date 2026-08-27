# Generator log — brief 039

## Rouge prouvé (SC2)

Sur le SHA de base `476f78cfa266efcaa3c56b6103c0337d7785593a`, le contrôle AST
du maillon `_apply_commerce` compte **5** occurrences de `MARCHANDISE_NOURRITURE`
ou du littéral `"nourriture"`. Après généralisation : **0**.

## Fichiers modifiés

- `sim/engine.py` — maillon commerce joué par marchandise, capacité d'arête partagée ;
- `sim/constants.py` — accès nommé `consommation_kg_par_habitant_par_tick` (seul lieu
  qui distingue les marchandises pour la consommation) ;
- `sim/tests/test_commerce.py` — six cas ajoutés (SC2–SC7), assertions existantes intactes ;
- `harness/queue/briefs/039-le-commerce-porte-tout/deliverables/*` — mesureur, manifeste,
  archives CLI pré-édition et sorties après.

## Commandes jouées

```bash
git rev-parse HEAD   # 476f78cfa266efcaa3c56b6103c0337d7785593a
.venv/bin/python -m pytest sim/tests/test_commerce.py -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m sim --ticks 20 --seed 0 --json
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python harness/queue/briefs/039-le-commerce-porte-tout/deliverables/measure_039.py --write-manifest
```

## Résultats

- CLI 20 et 365 ticks / graine 0 : byte-identiques au SHA de base ;
- `maillons_commerce_dans_sim` = 1 ;
- `kg_mineraux_ayant_change_de_cellule` = 0 ;
- `ecart_de_masse_par_marchandise` = 0 ;
- `somme_transferts_sur_arete_partagee` = capacité lue (200 kg) ;
- `modifications_de_dette_par_le_commerce` = 0 ;
- `ordres_d_insertion_essayes` = 2, résultats identiques.

## Limites

- La marchandise d'essai SC3 est nommée localement dans les tests et le mesureur
  (monkeypatch de l'accès nommé), pas dans `constants.py` — évite une constante
  terminale ;
- `sim/MODELE.md` § commerce non mis à jour (hors périmètre, dette architecte).
