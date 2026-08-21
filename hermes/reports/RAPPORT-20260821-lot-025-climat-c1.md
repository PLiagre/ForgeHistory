---
author: hermes
kind: rapport
created_at: 2026-08-21T13:35:07Z
concerns: brief 025, phase F1
status: REFLECTED_IN_ROADMAP
---
# Rapport — lot 025, déterminants physiques du climat C1

## Ce qui a été livré

Le propriétaire a fusionné la draft PR #123 le 2026-08-21 à 12:16 UTC. Le commit de fusion est `1b08ed8`.

Le lot ajoute au pipeline géographique, pour les 596 cellules existantes :

- l’insolation extraterrestre annuelle ;
- la durée du jour aux solstices d’été et d’hiver ;
- la distance du bord et du centroïde à la mer ;
- la zone de mer la plus proche ;
- le nombre de sauts jusqu’au littoral ;
- sept contrôles déterministes (`Q10`, `C1-A` à `C1-F`), leurs cas rouges, deux captures et les artefacts C1.

Le lot ne livre pas encore la température, les précipitations, l’humidité, les saisons ni une classification climatique. Il livre les déterminants physiques honnêtement dérivables depuis les données déjà committées. La ligne « climat » de F1 n’est donc pas close.

## Mesures vérifiées

Cursor Composer 2.5 a produit 22 fichiers, avec 4 080 ajouts et 2 suppressions. Les preuves ont ensuite été rejouées indépendamment par Hermes sur le worktree exact :

- `tests/run_proof_c1.py` : code `0`, 7 contrôles verts, 4 paires de déterminisme égales ;
- `pipeline.py --source climate_drivers` : code `0` ;
- `measure_c1_025.py` : code `0`, compteurs reconstruits ;
- `pytest harness/tests/ -q` : 348 réussis, 16 ignorés, 0 échec ;
- captures d’insolation et de continentalité regardées : cartes non vides, dégradés cohérents, aucun damier ni défaut évident.

La CI GitHub rejouée après synchronisation avec `master` a rendu 13 contrôles `SUCCESS` et 4 étapes `SKIPPED` attendues.

## Relecture et corrections avant fusion

Claude Opus 5 a rendu un verdict `PASS` dans le résultat ForgePilot `.forgepilot/runs/20260821T110938Z-reviewer/result.json`. Cette relecture ne pouvait pas exécuter les commandes ; les preuves ont donc été rejouées séparément avant fusion.

Deux constats moyens ont été corrigés par l’amendement `amendment-001-branches-source-et-compteurs-sc1.md` avant fusion :

1. le dépôt porte sept branches `--source` explicites et un chemin de repli `fixture`, pas huit branches explicites ;
2. les cinq compteurs de contrôle SC1 vivent dans `deliverables/manifest.json` et `logs/v1_080_qa.json`, pas dans `stats_c1.json`.

Claude a atteint sa limite fournisseur avant son résumé final d’amendement. Grok 4.6 High a alors vérifié en lecture seule l’alignement des trois documents, puis Hermes a déposé l’amendement sur `master` au commit `788b2f9`.

## Réserves non bloquantes

La relecture a signalé plusieurs constats faibles qui n’ont pas été transformés silencieusement en exigences de ce lot :

- le contrôle récursif C1-F hérite d’une troncature à 50 éléments du patron G5-bis ;
- le compteur du code de sortie peut relire l’artefact sans `--rerun-proof` ;
- une branche de départage est morte mais le résultat reste correct grâce à l’ordre de parcours ;
- certains cas rouges ne sabotent qu’un sens du contrôle.

Ces réserves doivent alimenter de futurs lots de durcissement, pas être lissées ni corrigées hors brief.

## Ce qui reste ouvert

- Le climat proprement dit exige encore une source réelle et licenciée pour température et précipitations.
- Les artefacts géographiques récents ne sont pas encore tous consommés par `sim/`.
- Le lot 026, gisements extractifs, est désormais prêt sous condition et son préalable 025 est satisfait par cette fusion.
- Le nouveau partage Grok 4.6 / Composer / Claude critique est demandé mais nécessite encore un brief ForgePilot multi-backend.

## Décision propriétaire restante

Aucune décision n’est requise pour le lot 025 : il est fusionné. Le prochain lot produit autorisé est le 026, sous la chaîne normale aperçu puis `--run`, sans fusion automatique.
