# Generator log — Brief 021 (G5 fleuves)

**Author**: forge-generateur
**Date**: 2026-08-15

## Lot 1 (itération 1)

- Instantané pré-édition : `deliverables/pre-edit/pipeline-geo-README.md.orig`
- Nouveau module `pipeline/geo/steps/05_rivers.py` (`run_rivers()` sans argument)
- Preuves `tests/run_proof_g5.py` + `tests/test_qa_red_g5.py` (6 rouges)
- Artefacts G5, registre, journaux v1_060, deux captures
- README mis à jour (G5 livré ; G5-bis / G5-ter / relief / climat / ressources / villes non livrés ; G5-ter non sourcée)
- `deliverables/measure_g5_021.py` pour les compteurs rejouables
- Classification D3 implémentée telle que tranchée : land-land touchée → artery / crossing / both

## Lot 2 (itération 2 — feedback-001 + amendement 001)

Corrections des onze points de `feedback/feedback-001.md` ; D3 et `artery_count=72` inchangés.

1. `derive_mouths` : zone la plus proche parmi toutes les zones ; booléen d'adjacence **calculé** ; embouchures non adjacentes émises (pas supprimées) ; compteur `embouchures_zone_non_adjacente`
2. `sea_zone_name` conservé avec déclaration de proxy G4 dans le commentaire de `mouths_g5.json` et le README
3. Frontmatter `**Author**: forge-generateur` ajouté
4. Compteurs déclarés dans `manifest.json` + `measure_g5_021.py` listé
5–6. Compteurs « fichier intact » dérivés de `git diff origin/master...HEAD` ; `git()` vérifie `returncode`
7. `rebuild_land=False` lève (ne reconstruit plus en silence)
8. Code mort `ctx_g4` / `land_land_total` retiré
9. Extrémités sur `window_ll.exterior` exclues des embouchures
10. Captures : anneaux intérieurs (lacs) peints en mer ; descriptions reécrites
11. README + `v1_060_rivers.log` : formulation amendement 001 (« ce que cette classification ne dit pas », 3 %)
- Cas rouge G5-D naturel (plus une mutation de drapeau)

## Non fait (périmètre)

- Pas de G5-bis / G5-ter, pas de modification de `constants.py`, `qa/checks.py`, `pipeline.py`, ni de `adjacency_g4.json`
- Pas de seuil géométrique pour `artery` (amendement 001)
- Pas de `verdict.md` (rôle Évaluateur)
- Pas de commit / push / merge (orchestrateur)
