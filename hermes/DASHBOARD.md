# Tableau de bord — ForgeHistory

> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —
> **ne jamais l'éditer à la main**, elle est réécrite à chaque
> poussée sur `master` et toutes les 6 heures par
> `.github/workflows/hermes-dashboard.yml`.
>
> Générée le 2026-08-12 12:56 UTC.

## En bref

- **Mode du pipeline** : `full_auto` — la boucle tourne sans intervention humaine (hors fusion finale).
- **Dépense CI ce mois-ci** : 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD. En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.
- **Audits en cours** : 7 — boucles closes : 7.

## Ce qui attend le propriétaire

- Fusionner (ou refuser) la PR #41 — « audit: critique de la PR #36 (skill Hermes versionnée) — CURSOR-3ce7947 » (branche `cursor/audit-de-la-pr-36-3dd2`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Fusionner (ou refuser) la PR #40 — « audit: critique de la PR #35 (cursor-auditor, six lentilles) » (branche `cursor/cursor-audit-pr-35-88f0`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Fusionner (ou refuser) la PR #31 — « challenge: revue de l'audit CURSOR-65c3ac1-dashboard-hermes-modele-auditeur » (branche `forge-bot/review-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur-31594124761`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Convertir l'audit retenu `CURSOR-cdc683f-hermes-workflow-quatre-acteurs` en brief (`/forge-audit-convert`).

## Activité GitHub récente

| quand (UTC) | workflow | déclencheur | branche | résultat |
|---|---|---|---|---|
| 2026-08-12 12:56:28 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:27 | pipeline-failure-escalate | workflow_run | master | in_progress |
| 2026-08-12 12:56:27 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:22 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:18 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:15 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:15 | pipeline-failure-escalate | workflow_run | master | skipped |
| 2026-08-12 12:56:10 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:10 | hermes-observer | workflow_run | master | queued |
| 2026-08-12 12:56:10 | audit-guard | push | master | success |
| 2026-08-12 12:56:10 | pipeline-audit | push | master | success |
| 2026-08-12 12:56:10 | security | push | master | success |
| 2026-08-12 12:56:10 | pipeline-challenge | push | master | in_progress |
| 2026-08-12 12:56:10 | harness-ci | push | master | in_progress |
| 2026-08-12 12:56:10 | hermes-dashboard | push | master | in_progress |

## Agents lancés récemment (Cursor Cloud)

Non disponible dans cette génération (API Cursor non interrogée).

## La boucle d'audit, audit par audit

| audit | où il en est | dernier événement (UTC) |
|---|---|---|
| CURSOR-cdc683f-hermes-workflow-quatre-acteurs | retenu — à convertir en brief | 2026-08-12 11:41 |
| CURSOR-73022bd-hermes-dashboard-modele-auditeur | contre-audit rendu — attend la décision | 2026-08-12 11:55 |
| CURSOR-779d97c-revue-verdicts-illisibles | contre-audit rendu — attend la décision | 2026-08-12 12:30 |
| CURSOR-0269d8e-hermes-console-droit-executer | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-65c3ac1-dashboard-hermes-modele-auditeur | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-bb8fe11-hermes-console-adr-0011 | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-e849633-hermes-demande-pilotage | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |

(7 boucle(s) close(s) non listée(s) — détail : `architecture/audit-ledger.jsonl`.)

## Briefs (les commandes de travail)

| brief | état apparent |
|---|---|
| 001-spatial-primary-key-adr | dernier verdict tracé : REJECT |
| 002-geo-pipeline-coastline-1400 | dernier verdict tracé : REJECT |
| 003-port-unity-game | dernier verdict tracé : ACCEPT |
| 004-polish-visuel | dernier verdict tracé : ACCEPT |
| 005-refonte-visuelle-carte | dernier verdict tracé : REJECT |
| 006-full-auto-agent-pipeline | dernier verdict tracé : ACCEPT |
| 007-geo-pipeline-cells-adjacency | dernier verdict tracé : REJECT |
| 008-contexte-opus5-right-sizing | pas encore de verdict |
| 008-full-auto-automation-gaps | dernier verdict tracé : ACCEPT |
| 009-full-auto-agent-invocation | dernier verdict tracé : ACCEPT |
| 010-repartition-roles-full-auto | dernier verdict tracé : ACCEPT |

« État apparent » = dernière mention `VERDICT:` tracée dans le `verdict.md` du brief ; l'autorité reste le fichier lui-même et `HANDOFF.md` pour le contexte.

## Utilisation des backends Générateur

| backend | runs cumulés | dernier run (UTC) |
|---|---|---|
| claude | 28 | 2026-08-11 14:07 |
| cursor | 4 | 2026-07-29 17:31 |
| codex | 2 | 2026-08-11 23:16 |

## Comment lire ce tableau

La chaîne nominale (ADR-0010) : une **demande** entre par Hermes
(`hermes/requests/`) → le propriétaire tranche → `ROADMAP.md` est mise
à jour → Claude (CTO) écrit un **brief** → Codex produit → le gate
mécanique juge → Claude ouvre la **PR** → Cursor la **critique**
(audit) → Claude **contre-audite** l'audit → décision → la boucle se
clôt (`AUDIT_ARCHIVED`).

- Direction et étapes suivantes : [ROADMAP.md](../ROADMAP.md)
- Dernier état de session détaillé : [HANDOFF.md](../HANDOFF.md)
- Marche/arrêt de la boucle : `docs/rules/full-auto-pipeline.md`
  (arrêt d'urgence : label `pipeline/pause`, ou `mode: manual`).
