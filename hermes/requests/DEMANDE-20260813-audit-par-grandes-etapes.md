---
author: hermes
kind: demande
created_at: 2026-08-13T13:43:00Z
concerns: projet
status: CLOSED
---
# Demande propriétaire — audit et contre-audit par grandes étapes, plus par PR

Demande exprimée par le propriétaire (session Cursor Cloud, 2026-08-13,
13:43 UTC), reformulée sans en changer le fond :

1. L'audit et le contre-audit **à chaque pull request** consomment beaucoup
   trop de jetons : le quota Claude Code du propriétaire est entièrement
   parti là-dedans (plafond mensuel atteint deux fois en vingt-quatre
   heures, mesuré : `2.4342555` USD pour un seul contre-audit,
   `7.2771804` USD de transcripts sur la seule journée du 2026-08-13 —
   audit `CURSOR-827d54e`, points 1 et 4).
2. À la place : **un audit et un contre-audit à la fin de plusieurs
   grandes étapes du projet**, pas à chaque PR.
3. Définir **les grandes étapes du projet** et placer les audits aux
   **moments cruciaux**.
4. Objectif général : **moins d'allers-retours** dans la boucle.

## Traitement

- Décision enregistrée : [ADR-0012](../../docs/adr/0012-audit-contre-audit-par-grandes-etapes.md)
  (remplace la ligne « Cursor relit chaque PR » d'ADR-0010 ; amende la
  cadence d'audit post-fusion d'ADR-0005/0006).
- Grandes étapes définies : `ROADMAP.md` § « Grandes étapes — jalons
  d'audit (ADR-0012) ».
- Marqueur de jalon : un fichier `hermes/milestones/ETAPE-NN-<slug>.md`
  fusionné sur `master` déclenche l'audit d'étape (contrat :
  `hermes/milestones/README.md`).
- Câblage : `pipeline-audit.yml` (déclencheurs `pull_request` et
  push-sur-master retirés ; jalon ou `workflow_dispatch` uniquement),
  clés de politique dans `harness/pipeline/config.yaml`
  (`cursor_review_on_pr: false`, `cursor_audit_on_master_push: false`,
  `cursor_audit_on_milestone: true`).
- Le contre-audit (`pipeline-challenge.yml`) ne change pas de mécanique :
  il suit la cadence des audits déposés, donc celle des jalons.
- Ce qui reste à chaque PR : uniquement les contrôles mécaniques gratuits
  (CI, gate `verdict_audit.py`, job `audit-check` du brief 014).
