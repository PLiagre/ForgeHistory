# Journal du Générateur — brief 025 (C1 déterminants physiques du climat)

**Author**: forge-generateur
**Date**: 2026-08-21

## Ordre d'exécution

1. Provision de la pile scientifique : `python3 -m venv .venv` puis
   `.venv/bin/pip install -r pipeline/geo/requirements.txt pytest`.
2. Instantanés pré-édition copiés dans `deliverables/pre-edit/`.
3. Bloc C1 ajouté en fin de `constants.py` (aucune ligne supprimée).
4. Module `steps/c1_climate_drivers.py` : insolation D3, distances mer, sauts littoral.
5. Contrôles `qa/checks_c1.py` (import de `CheckResult` et `q10_determinism`).
6. `tests/run_proof_c1.py` et `tests/test_qa_red_c1.py`.
7. Crochet `pipeline.py --source climate_drivers`.
8. Section C1 dans `README.md`.
9. Preuves générées et script `measure_c1_025.py`.

## Résultats

- `tests/run_proof_c1.py` : code 0, 7/7 contrôles verts, 7/7 preuves rouges non vides.
- `pipeline.py --source climate_drivers` : code 0.
- `pipeline.py --source rivers` : non-régression OK (code 0).
- `ecretages_polaires_total` = 0 (mesuré, pas supposé).
- `contact_ponctuel_sans_arete_land_sea` = 0.
- `cellules_atteintes_par_strait_seulement` = 21 (îles lacustres, conforme au contexte planificateur).
- `cellules_centroide_hors_polygone` = 12 (mesuré, publié dans stats_c1.json).

## Captures (regardées)

- **Insolation** (`v1_080_insolation_window.png`) : dégradé nord-sud continu — violet
  foncé au nord (≈60°N), vert puis jaune au sud (≈30°N). Aucune bande artificiale ;
  l'énergie croît régulièrement vers le sud comme attendu pour l'insolation astronomique.
- **Continentalité** (`v1_080_continentality_window.png`) : côtes jaune pâle (distance
  quasi nulle), progression orange vers l'intérieur, cœur continental en rouge foncé
  (Europe de l'Est / intérieur des terres) nettement séparé des littoraux.

## Écart SC5 documenté

Le brief demande `branches_source_preexistantes_identiques` = 8/8, mais le dépôt ne
porte que **7** branches `if args.source == "..."` plus un chemin de repli `fixture`
en fin de `main()`. Mesure obtenue : **7/7** branches byte-identiques ; le dénominateur
8 du brief n'est pas satisfaisable sans inventer une huitième branche.

## Non livré (volontaire)

Température, précipitations, saisons, classification climatique, valeur `--source climate`.
