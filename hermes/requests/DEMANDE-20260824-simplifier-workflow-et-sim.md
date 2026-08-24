---
author: owner
recorded_by: cursor-cloud
kind: demande
created_at: 2026-08-24T12:20:00Z
concerns: projet
status: CLOSED
---
# Simplifier le workflow de revue/test et le jeu

## Situation exprimée par le propriétaire

Trop d'itérations : harnais trois rôles, checks PR, revues qui
bouclent. La simulation exige trop de vérité historique et de
précision prédictive. On garde ce qui marche.

## Ce que cette demande tranche

1. Hermes tourne sur GPT Sol 5.6. Il suit, prépare les briefs / grandes
   étapes, clarifie roadmap et vision. Pas de code produit.
2. Cursor prend un brief large, le découpe, exécute en parallèle, ouvre
   une PR. Moins d'allers-retours.
3. Moins de checks PR. On ne garde que ce qui protège une régression
   grave.
4. La sim reste le produit vivant. On relâche la dépendance aux données
   historiques absolument valides et aux mesures prédictives
   ultra-précises. On ne vide pas `sim/`. Unity reste en veille.

## Décision écrite

ADR-0018.
