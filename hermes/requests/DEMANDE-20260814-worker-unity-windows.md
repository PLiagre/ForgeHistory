---
author: hermes
kind: demande
created_at: 2026-08-14T12:59:13Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Intégrer Unity Windows sans dépendre du poste de contrôle

Le propriétaire précise que Unity est installé nativement sous Windows. Le
double démarrage sur la partition Linux ne peut donc pas constituer le worker
Unity : lorsque Linux tourne, Unity Windows est indisponible.

Décision reflétée dans ADR-0013 et la roadmap :

- le pilote initial garde Windows démarré et utilise WSL2 seulement si utile ;
- si le bilan des trois lots est positif, Hermes et ForgePilot migrent sur un
  VPS Linux léger ;
- le PC Windows devient un worker Unity séparé ;
- Cursor peut écrire depuis le VPS ou un Cloud Agent, mais Unity valide toujours
  le commit exact avant toute fusion CityLab ;
- PC éteint signifie validation Unity en attente, jamais réussite ;
- VictoriaCityLab étant public, le runner personnel n'exécute que des branches
  du propriétaire après déclenchement manuel.

L'implémentation du runner et du workflow appartient à une PR VictoriaCityLab
séparée. La présente PR ForgeHistory fixe le contrat et bloque toute affirmation
de validation Unity tant que cette PR d'infrastructure n'existe pas.
