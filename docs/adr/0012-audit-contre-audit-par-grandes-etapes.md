# ADR-0012: Audit et contre-audit par grandes étapes — plus jamais par PR

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-13
**Status**: accepted
**Deciders**: propriétaire du projet (session Cursor Cloud du 2026-08-13,
13:43 UTC — `hermes/requests/DEMANDE-20260813-audit-par-grandes-etapes.md`) ;
rédaction déléguée à l'orchestrateur Cursor (remplaçant du CTO).

## Context

ADR-0010 a fait de Cursor le critique de **chaque** pull request, et
ADR-0005/0006 déclenchaient en plus un audit post-fusion à **chaque** push
sur `master`. Chaque audit déposé déclenche à son tour un contre-audit
Claude (`pipeline-challenge.yml`). Le coût réel, mesuré par la boucle
elle-même : `2.4342555` USD pour un seul contre-audit, `7.2771804` USD de
transcripts sur la seule journée du 2026-08-13 (audit `CURSOR-827d54e`,
points 1 et 4), et le **plafond mensuel de l'abonnement Claude atteint deux
fois en vingt-quatre heures** (onze échecs `429` consécutifs le 2026-08-12,
rechute le 2026-08-13 à 11:14 UTC). La boucle produisait aussi des audits
sur ses propres artefacts (critiques de PRs de tenue de registre, de PRs de
revues…), soit des allers-retours dont le coût dépasse la valeur. Le
propriétaire tranche : l'audit et le contre-audit ont prouvé leur valeur
(P0 moteur attrapé sur la PR #60), mais leur **cadence** doit suivre les
grandes étapes du projet, pas chaque PR.

## Decision

1. **L'audit Cursor et le contre-audit Claude ne se déclenchent plus ni à
   l'ouverture d'une PR ni à chaque push sur `master`.** Ils se déclenchent
   uniquement :
   - à la **clôture d'une grande étape** — matérialisée par la fusion sur
     `master` d'un fichier-jalon `hermes/milestones/ETAPE-NN-<slug>.md`
     (contrat : `hermes/milestones/README.md`) ;
   - ou par **`workflow_dispatch` explicite** (audit ponctuel qu'un humain
     juge crucial — changement structurel du harnais entériné par ADR,
     incident, doute).
2. **Les grandes étapes sont définies dans `ROADMAP.md`**
   (§ « Grandes étapes — jalons d'audit »), tenues par Hermes. Un jalon
   d'audit clôt chaque phase/couche : fondations monde (F1), couche 1
   « le monde vivant compte juste » (F2), puis chaque couche du jeu
   (villes, états, armées, batailles + rendu).
3. **Ce qui reste à chaque PR** : uniquement les contrôles mécaniques
   gratuits — CI (`harness-ci`, `security`, `audit-guard` avec le job
   `audit-check` du brief 014), gate `verdict_audit.py`, merge-bot. Aucune
   invocation d'agent facturée.
4. **Le contre-audit ne change pas de mécanique** : `pipeline-challenge.yml`
   se déclenche toujours au dépôt d'un audit dans `architecture/inbox/` —
   c'est la cadence amont qui devient rare. Ses gardes (kill-switch, mode,
   `ci_budget_guard`, plafond par appel, état de refus fournisseur et repli
   du brief 014) sont inchangés.

## Alternatives Considered

### Alternative 1: garder la critique par PR mais avec un plafond plus bas
- **Pros**: aucune modification de câblage ; couverture maximale.
- **Cons**: le plafond coupe en silence au milieu du mois (c'est exactement
  la panne vécue) ; la dépense continue de partir sur des PRs documentaires.
- **Why not**: un plafond n'est pas une cadence — il transforme le
  trop-plein en panne au lieu de l'empêcher (le propriétaire veut moins
  d'allers-retours, pas des allers-retours interrompus).

### Alternative 2: critiquer une PR sur N (échantillonnage)
- **Pros**: dépense divisée par N.
- **Cons**: arbitraire ; une PR cruciale peut tomber dans les N-1 ignorées
  pendant qu'une PR de registre est auditée.
- **Why not**: le moment crucial est une propriété du **projet** (une étape
  qui se clôt), pas du hasard.

## Consequences

### Positive
- La dépense Claude/Cursor suit le rythme des jalons (quelques audits par
  phase), plus le rythme des PRs (plusieurs par jour).
- Un audit d'étape voit le **cumul** d'une étape — plus de constats
  redondants entre audits de PRs voisines (constaté : trois audits distincts
  sur le même thème du seuil de survie en une journée).
- L'arriéré structurel disparaît : plus de « contre-audit dû » par audit de
  PR. Les 15 audits `PROPOSED` hérités ne sont plus une dette individuelle :
  ils seront adjugés en lot au prochain jalon ou purgés `STALE` (motivés).

### Negative
- Un défaut introduit en début d'étape n'est vu par l'audit qu'à la clôture
  du jalon (atténué : le gate mécanique, la CI et le job `audit-check`
  restent à chaque PR ; un `workflow_dispatch` reste possible à tout
  moment).
- La porte observable du brief 014 (`pr_audit_guard`) sera verte sur la
  plupart des PRs (aucun audit ne les ciblera plus individuellement) — elle
  garde sa valeur au moment des jalons et pour les audits ponctuels.

## Amendements aux décisions antérieures

- **ADR-0010** : la ligne « Cursor relit chaque PR » est remplacée par
  « Cursor audite chaque grande étape (jalon) et sur dispatch ». Le reste
  (rôles, interdits, quatre acteurs) est reconduit.
- **ADR-0005/0006** : la cadence « audit post-merge à chaque push master »
  est remplacée par la cadence par jalons. Le cycle de vie d'un audit
  (PROPOSED → CHALLENGED → décision → …) est inchangé.
- `harness/pipeline/config.yaml` : `cursor_review_on_pr: false`,
  `cursor_audit_on_master_push: false`, `cursor_audit_on_milestone: true`.
