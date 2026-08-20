---
author: owner
recorded_by: cursor-cloud
kind: demande
created_at: 2026-08-20T09:08:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# La simulation Python est le produit ; Hermes pilote et propose

## Situation exprimée par le propriétaire

Le visuel Unity est en veille. Il faut une simulation complète qui tourne
sans Unity. La seule organisation à respecter est qu’Hermes pilote tout.

Hermes ne doit plus être bridé au rôle de teneur de feuille de route. Il
est fait pour s’auto-améliorer. Il doit pouvoir proposer des améliorations,
y compris des tâches quotidiennes (crons).

## Ce que cette demande tranche

1. **`sim/` est le produit vivant.** Le moteur Python doit pouvoir tourner
   sans Unity. Compléter cette simulation (couches de `VISION.md`) est le
   travail à venir. Unity reste dans le dépôt comme client visuel gelé,
   pas comme source de vérité.
2. **Hermes est le cerveau opérationnel.** Il propose, planifie la
   cadence, tient la mémoire, lance ForgePilot. Il n’écrit toujours pas
   le code produit, ni un brief, ni un verdict, et il ne fusionne pas.
3. **Les crons quotidiens sont autorisés** dès cette décision : lecture,
   mesure, proposition, compte-rendu. Aucun cron ne fusionne, n’écrit du
   code, ni n’instruit un exécutant à la place d’un brief.

## Ce que cette demande ne tranche pas

- le plafond mensuel Claude (toujours ouvert dans ADR-0014) ;
- la réactivation de `mode: full_auto` ;
- la date de réveil du visuel Unity.
