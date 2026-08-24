---
author: owner
recorded_by: cursor-cloud
kind: demande
created_at: 2026-08-24T12:40:00Z
concerns: projet
status: CLOSED
---
# Geler G6, reculer le scope, sim mince

## Situation exprimée par le propriétaire

Les dernières itérations de G6 ont pris un temps fou pour finir en
fail. Le projet cherche trop loin. Gros travail de simplification.

## Ce que cette demande tranche

1. Arrêter de sauver G6. Couper, geler, ou sortir du quotidien tout ce
   qui relance le relief, le calage géographique ultra-précis, les
   re-preuves SHA, les lots qui ne nourrissent pas `sim/`.
2. Reculer le scope jusqu'au jeu qui tourne déjà : `python -m sim`,
   couche 1 mince, snapshot `v0a-1`. Pas un cadastre 1400, pas un
   climat observé, pas une consommation 026, pas Unity.
3. Simplifier encore : `sim/` mince ; `pipeline/geo/` archive ;
   briefs trop loin abandonnés ; grandes étapes Hermes courtes.

## Décision écrite

ADR-0019.
