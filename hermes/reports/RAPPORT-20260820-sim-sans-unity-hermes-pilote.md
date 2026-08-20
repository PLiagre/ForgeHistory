---
author: cursor-cloud
kind: rapport
created_at: 2026-08-20T09:45:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Mise en ordre : sim/ sans Unity, Hermes pilote et propose

Décision propriétaire du 2026-08-20, enregistrée par l’exécutant (Cursor)
parce que la session n’était pas une session Hermes.

## Livré

- ADR-0016 accepté ; ADR-0015 accepté (crons de lecture, pas de fusion).
- Contrat Hermes élargi : propositions, cron quotidien, skill.
- `python -m sim` lance le monde sans Unity.
- Documents d’état alignés (ROADMAP, CLAUDE, HANDOFF, README Unity /
  architecture / sim).

## Ouvert

- Installer le cron sur le VPS.
- Prochaine couche de `sim/` (brief à écrire par Claude, pas par Hermes).
- Unity reste gelé jusqu’à réveil explicite.
