# Brief 037-bis — L'assertion de couche non consommée vise une couche inexistante

**Authored**: 2026-08-27T10:45:00Z
**Author**: Hermes
**Risque**: R0 — documentation seule, pas de code produit, pas de modification du moteur.

## But unique

Déplacer l'assertion `_couche_consommee("gisements")` dans
`sim/tests/test_monde.py` vers `_couche_consommee("couche_inexistante")`,
pour que le test `test_la_consommation_des_couches_est_mesuree_pas_declaree`
continue de vérifier la capacité de la sonde à détecter une couche non
consommée, sans pointer vers une couche que le lot 038 va rendre consommée.

C'est la fermeture de la chaîne entamée par le 034-bis : après ce lot,
toutes les couches `_COUCHES` (relief, climat, gisements) seront
consommées, et la sonde doit être vérifiable sur **un nom qui n'est pas
une couche réelle** plutôt que sur la dernière encore inerte.

`_alterer()` dans `sim/snapshot_export.py` ne modifie aucune cellule pour
un nom inconnu, donc les deux mondes restent identiques et
`_couche_consommee("couche_inexistante")` retourne `False`. C'est
exactement ce que le test doit vérifier : la sonde ne fabrique pas un
`True` par erreur.

Ce lot précède et débloque le brief 038 (« Les gisements sortent enfin
quelque chose »).

## Périmètre d'écriture

- `sim/tests/test_monde.py`, uniquement remplacer
  `_couche_consommee("gisements")` par `_couche_consommee("couche_inexistante")`
  dans l'assertion du bloc monkeypatch de `production_kg`. Aucun autre
  changement, et en particulier ne pas modifier le commentaire « la sonde
  pointe vers les gisements, encore inertes » (il reste vrai jusqu'à la
  fusion de ce micro-lot, et deviendra historique après).

## Conditions de succès

### SC1 — L'assertion déplacée passe encore

Avant et après modification,
`.venv/bin/python -m pytest sim/tests/test_monde.py::test_la_consommation_des_couches_est_mesuree_pas_declaree -v`
est vert.

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
