# Brief 034-bis — L'assertion de couche non consommée vise les gisements

**Authored**: 2026-08-26T15:45:00Z
**Author**: Hermes (propriétaire, micro-lot avant 035)
**Risque**: R0 — documentation seule, pas de code produit, pas de modification du moteur.

## But unique

Déplacer l'assertion `_couche_consommee("climat")` dans
`sim/tests/test_monde.py` vers `_couche_consommee("gisements")`, pour que le
test `test_la_consommation_des_couches_est_mesuree_pas_declaree` continue de
vérifier la capacité de la sonde à détecter une couche non consommée, sans
pointer vers une couche que le lot 035 va rendre consommée.

Ce lot précède et débloque le brief 035 (« la saison joue dans le rendement »).

## Périmètre d'écriture

- `sim/tests/test_monde.py`, uniquement remplacer
  `_couche_consommee("climat")` par `_couche_consommee("gisements")` dans les
  assertions du bloc monkeypatch de `production_kg`. Aucun autre changement.

## Conditions de succès

### SC1 — L'assertion déplacée passe encore

Avant et après modification, `.venv/bin/python -m pytest sim/tests/test_monde.py::test_la_consommation_des_couches_est_mesuree_pas_declaree -v` est vert.

### SC2 — Aucune régression

`.venv/bin/python -m pytest sim/tests/ -q` est aussi vert après qu'avant.

## Compteurs

| compteur | source | dénominateur |
|---|---|---|
| `assertions_deplacees` | `git diff sim/tests/test_monde.py` | 1 attendu |
| `tests_monde_verts_avant` | pytest collect + run | nombre collecté |
| `tests_monde_verts_apres` | pytest collect + run | nombre collecté |

## Hors périmètre

- Tout code dans `sim/engine.py`, `sim/constants.py`, `sim/__main__.py`
- Tout test hors de `sim/tests/test_monde.py`
- `sim/snapshot_export.py`, `sim/world.py`, `sim/aggregation.py`
- La carte, le viewer, le pipeline, ForgePilot
