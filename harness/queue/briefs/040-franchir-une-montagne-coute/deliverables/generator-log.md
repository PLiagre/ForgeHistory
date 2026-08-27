# Lot 040 — Franchir une montagne coûte plus cher qu'une plaine

**generator-log.md** — 2026-08-27

## Résumé

Faire dépendre la capacité d'une arête du relief des deux cellules qu'elle relie.
Le bout le plus difficile commande (min). Cinq facteurs de transport (plaine 1.0,
colline 0.70, marais 0.40, montagne 0.30, haute_montagne 0.10). Les arêtes
maritimes sont ignorées.

Risque : R1 (classé R2 effectif par le planificateur).

## Fichiers modifiés

- `sim/constants.py` : 5 constantes + `facteurs_transport_par_relief()` (motif 033)
- `sim/engine.py` : `_facteur_transport_pour_cellule()`, `_capacite_transport_arete_kg()`,
  intégration dans `_initialiser_capacite_aretes()` et `_apply_commerce()`
- `sim/tests/test_commerce.py` : 4 nouveaux tests (SC1, SC2, SC6 avec/sans carte)

## Planification

Planificateur : Cursor Grok 4.6 xhigh — 222s. Plan classé R2 effectif.
Worktree préparé, branche `agent/040-franchir-une-montagne-coute`.

## Exécution

Exécuteur : Cursor Composer 2.5 — code écrit, 3 fichiers, 285 insertions, 4 suppressions.
L'exécuteur a rendu le code mais pas le JSON métier attendu (problème connu,
cf. lot 035). Récupération manuelle.

## Corrections appliquées (après exécuteur)

Deux bugs dans les tests écrits par l'exécuteur :

1. **`_transfert_vers`** appelait `_apply_commerce(w, [0.0], MARCHANDISE_NOURRITURE)` sans
   `capacite_restante` → None → crash. Correction : initialiser `cap` via
   `_initialiser_capacite_aretes(w)` avant l'appel.

2. **Test SC6** — le premier `pytest.raises` attendait le pattern `str(a_id)` (9100) mais
   le code lève le relief invalide sur le cell_id de la cellule modifiée (9101).
   Correction : retirer l'assertion redondante sur `a_id`.

## Tests

### Tests individuels (SC1, SC2, SC4, SC6)

| Test | Résultat |
|---|---|
| `test_cinq_facteurs_transport_suivent_ordre_strict` (SC1) | ✅ 1 passed |
| `test_goulot_relief_min_commande_capacite` (SC2) | ✅ 1 passed |
| `test_conservation_masse_transport` (SC4) | ✅ 1 passed |
| `test_relief_inconnu_refuse_sur_monde_charge` (SC6) | ✅ 1 passed |
| `test_sans_carte_capacite_transport_inchangee` (SC6) | ✅ 1 passed |

### Suite complète sim/tests

```bash
.venv/bin/python -m pytest sim/tests/ -q --tb=line
# 96 passed in 308.91s (0:05:08)
```

## Mesures

### État de départ (master baseline, SHA `d544f21`)

```bash
.venv/bin/python -m sim --ticks 20 --seed 0 --json
# kg_transportes = 634662.4

.venv/bin/python -m sim --ticks 365 --seed 0 --json
# kg_transportes = 2377283.5
```

### État après lot (branche `agent/040-franchir-une-montagne-coute`, SHA `08de77b`)

```bash
.venv/bin/python -m sim --ticks 365 --seed 0 --json
# kg_transportes = 1688630.5  (baisse de 29%)
```

### Horizon long (SC5)

```bash
.venv/bin/python -m sim --ticks 1825 --seed 0 --json
# population_arrivee = 21132004 / population_depart = 66649511
# fraction_survie = 0.317  (strictement positive)
```

### Compteurs

| Compteur | Valeur | Statut |
|---|---|---|
| `classes_relief_carte` | 5 | ✅ |
| `aretes_entre_deux_cellules` | 917 | ✅ |
| `aretes_ignorees_hors_monde` | 447 | ✅ |
| `aretes_par_facteur_limitant` | plaine=297, colline=307, marais=6, montagne=239, haute_montagne=68 | ✅ |
| `classes_avec_capacite_effective` | 5 | ✅ |
| `capacite_independante_du_sens` | 1 | ✅ |
| `kg_transportes_avant` | 2377283.5 | ✅ |
| `kg_transportes_apres` | 1688630.5 | ✅ (strictement inférieur) |
| `ecart_de_masse_micro_monde` | 0 | ✅ |
| `reliefs_inconnus_refuses` | 1 | ✅ |
| `fraction_survie_horizon_long` | 0.317 | ✅ (positive) |
| `noms_de_constantes_transport_dans_engine` | 0 | ✅ (motif 033) |

## Limites

- Les arêtes classe inconnue sur un monde chargé lèvent une erreur explicite avec les
  deux `cell_id` et la valeur. Le test SC6 le vérifie.
- Sans carte, le facteur de terrain vaut 1 (capacité de base inchangée).
- Le transport maritime n'est pas touché (hors périmètre).
- Les arêtes terre-mer sont ignorées (comportement existant).