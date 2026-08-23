---
author: hermes
kind: demande
created_at: 2026-08-21T20:30:00Z
concerns: sim, présentation
status: CLOSED
---
# Insérer V0 — visualiseur web mince après le lot 024

## Constat

ForgeHistory possède déjà une carte de 596 cellules, des données géographiques, une simulation cellulaire et des captures de preuve. Il ne possède pourtant aucun rendu interactif : `python -m sim --json` n'exporte qu'un résumé global et Unity est volontairement en veille jusqu'à une étape lointaine.

Attendre E6 pour voir le monde rend la progression difficile à percevoir et retarde les retours produit et visuels.

## Décision propriétaire

Après la correction et la revue du lot 024, avant le lot 026 ressources, le projet livre un jalon transversal **V0 — Monde visible** en deux lots bornés :

1. **V0-A — snapshot cellulaire** : `sim/` exporte un état déterministe par cellule pour un tick et une graine donnés, avec la géométrie et les couches disponibles. La source de vérité reste le moteur.
2. **V0-B — visualiseur web mince** : carte locale interactive avec zoom, déplacement, sélection d'une cellule, choix des couches disponibles et comparaison de snapshots. Le visualiseur lit les exports ; il ne calcule aucune règle métier.

Le visualiseur doit fonctionner sans Unity et ne constitue pas une seconde simulation. Il est conçu pour recevoir progressivement relief, climat, population, nourriture, famine, ressources, villes, routes et États.

## Ordre produit

1. corriger et relire le lot 024 ;
2. livrer V0-A ;
3. livrer V0-B ;
4. reprendre le lot 026 et la fin d'E1.

Unity reste en veille. Son réveil demeure une décision distincte.
