---
author: hermes
kind: demande
created_at: 2026-08-21T20:00:00Z
concerns: workflow
status: ACCEPTED
---
# Adapter les vérifications au risque R0 / R1 / R2

## Décision propriétaire

Le propriétaire active un workflow de vérification proportionné au risque. La chaîne maximale ne doit plus être appliquée mécaniquement à tous les lots.

## Niveaux

- **R0 — documentaire simple** : texte, rapport ou correction factuelle sans instruction exécutable ni changement de gouvernance. Contrôles mécaniques adaptés ; pas de reviewer IA systématique.
- **R1 — produit borné** : changement local dans `sim/`, `pipeline/geo/` ou le harnais, avec brief précis et invariants existants. Grok 4.6 combine analyse et planification ; Composer exécute et lance les tests ciblés ; la draft PR est ensuite soumise en parallèle à la CI complète, à GPT-5.6 Sol XHigh en lecture seule et aux reconstructions déterministes ciblées d'Hermes.
- **R2 — critique** : architecture, sécurité, gouvernance, provenance, données massives, invariant fondamental ou lot ayant déjà produit un faux vert. Chaîne renforcée, preuves indépendantes étendues et témoin Claude lorsque disponible.

## Optimisations obligatoires

- Analyse et planification ordinaires sont regroupées dans une seule invocation Grok.
- La CI et la revue indépendante tournent en parallèle après publication de la draft PR.
- Une correction rejoue d'abord les tests ciblés ; la CI complète contrôle le commit final.
- Après une correction bornée, le reviewer relit le delta et la résolution de ses constats. Une revue complète ne repart que si l'approche change substantiellement.
- Le rapport final est écrit après fusion, pas avant.
- Les étapes de correction et d'itération sont conditionnelles : elles ne font pas partie du chemin nominal vert.

## Application immédiate

Le correctif du lot 024 reste **R2** : données DEM massives, provenance et empreintes, défaut antérieur resté vert, puis fusion avant correction. Les prochains lots sont classés avant exécution ; R1 est le niveau par défaut, R0 et R2 doivent être justifiés par les faits du lot.

Le veto propriétaire sur la fusion reste inchangé.
