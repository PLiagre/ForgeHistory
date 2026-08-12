---
author: hermes
kind: demande
created_at: 2026-08-12T08:19:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Demande propriétaire — workflow complet à quatre acteurs

Demande exprimée par le propriétaire (session Cursor Cloud, 2026-08-12),
reformulée sans en changer le fond :

1. **Hermes** pilote globalement le projet : point d'entrée, suivi global,
   contexte ; les évolutions et la feuille de route passent par lui.
2. **Claude Code** est le CTO : il prend la feuille de route, pilote et
   orchestre.
3. **Codex** est l'exécutant (modèle GPT-5.6 Sol).
4. **Claude** reprend la main pour faire les PR.
5. **Cursor** relit chaque PR et la critique, en s'appuyant sur les bonnes
   pratiques d'ingénierie IA sourcées sur internet.
6. Tout le workflow fonctionne en automatique.
7. Le dépôt est plus propre : une feuille de route claire du jeu et du
   projet ; plus de branche ni de point de validation en attente.

## Traitement

- Décision enregistrée : ADR-0010.
- Feuille de route : ROADMAP.md (création).
- Câblage : `pipeline-forge-run.yml`, `pipeline-challenge.yml`,
  `pipeline-audit.yml` (critique de PR incluse).
- Guide de critique sourcé : `architecture/review-guidelines.md`.
- Nettoyage : branches fusionnées supprimées, PR #1 et #12 fermées
  (le contenu utile de #1 est repris ; #12 est remplacée par ADR-0010),
  audits obsolètes archivés.
