# Journal du Générateur — Brief 028

**Author**: forge-generateur-cursor
**Date**: 2026-08-23

Rôle : Générateur (Cursor Cloud). Première tranche visible du regard mince,
exécutée après le snapshot 027, sur instruction propriétaire d'étendre le
lot 029.

Aucune conclusion de recevabilité ici.

## Ce que l'œil voit

Le fichier `deliverables/proofs/carte_population.svg` montre la fenêtre
pilote en polygones EPSG:3035. Les cellules forment le territoire
européen et méditerranéen de la maille G3. Le dégradé de population n'est
pas plat : certaines côtes et le nord ressortent plus chauds. La légende
distingue le zéro mesuré, l'absent et le non calculé.

`carte_comparaison.svg` compare tick 0 et tick 5 (même graine). Les
cellules incomparables ne sont pas peintes comme un zéro : elles portent
un gris distinct légendé « incomparable ».

Un PNG de la même carte (`carte_population.png`) est dérivé des mêmes
cellules et de la même palette, sans navigateur.

## Commandes

```
.venv/bin/python -m viewer --snapshot harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/snapshot_a.json --proof-svg harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/carte_population.svg
.venv/bin/python -m viewer --snapshot harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/snapshot_a.json --proof-svg harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/carte_population_b.svg
.venv/bin/python -m viewer --snapshot harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/snapshot_a.json --compare harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/snapshot_b.json --proof-svg harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/carte_comparaison.svg
.venv/bin/python -m pytest viewer/tests/test_viewer_v0b.py -q
```

Deux passes population identiques. Deux passes comparaison identiques.
Pas de serveur lancé pour la preuve.
