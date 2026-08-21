---
author: hermes
kind: demande
created_at: 2026-08-21T15:26:43Z
concerns: brief 024, phase F1
status: HANDED_TO_CTO
---
# Compléter la couverture DEM du relief G6

Le propriétaire a décidé le 2026-08-21 que le relief G6 doit disposer des tuiles DEM nécessaires pour couvrir toute la carte pilote.

## Constat déclencheur

La relecture de la PR #122 a établi que les 179 tuiles déclarées couvrent seulement une partie de la maille. Hors emprise, l’implémentation actuelle borde les coordonnées vers les tuiles disponibles et publie des altitudes `0,0 m`. Ces valeurs ne sont pas des mesures et produisent de faux reliefs, barrières et cols.

## Décision propriétaire

La correction doit compléter la source DEM afin de couvrir l’ensemble des cellules et des échantillons nécessaires au relief de la fenêtre pilote. La solution ne doit ni limiter silencieusement G6 à l’emprise actuelle, ni marquer durablement le reste du monde comme non mesuré, ni conserver un repli vers une tuile voisine.

La provenance, la licence, les empreintes, le volume, le cache local et la vérification collective doivent rester explicites et rejouables. Aucun téléchargement ou artefact DEM ne doit être committé dans Git.

## Étude et amendement attendus

Grok 4.6 doit d’abord mesurer en lecture seule l’emprise réellement requise, les tuiles présentes et manquantes, les volumes et les changements nécessaires. Claude Code intervient ensuite uniquement comme Planificateur critique pour valider la stratégie et amender le brief 024. Cursor reste l’unique exécutant du code et des données de preuve.

Cette décision n’autorise pas la fusion de la PR #122 en l’état. Elle n’autorise pas non plus le lancement parallèle du lot 026. Le lot 024 doit être corrigé, rejoué et relu avant fusion.
