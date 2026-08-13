# Tableau de bord — ForgeHistory

> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —
> **ne jamais l'éditer à la main**, elle est réécrite à chaque
> poussée sur `master` et toutes les 6 heures par
> `.github/workflows/hermes-dashboard.yml`.
>
> Générée le 2026-08-13 08:28 UTC.

## En bref

- **Mode du pipeline** : `full_auto` — la boucle tourne sans intervention humaine (hors fusion finale).
- **Dépense CI ce mois-ci** : 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD. En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.
- **Audits en cours** : 20 — boucles closes : 7.

## Ce qui attend le propriétaire

- Convertir l'audit retenu `CURSOR-cdc683f-hermes-workflow-quatre-acteurs` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-e849633-hermes-demande-pilotage` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-0269d8e-hermes-console-droit-executer` en brief (`/forge-audit-convert`).

## Activité GitHub récente

| quand (UTC) | workflow | déclencheur | branche | résultat |
|---|---|---|---|---|
| 2026-08-13 08:28:19 | pipeline-orchestrate | push | master | in_progress |
| 2026-08-13 08:28:19 | harness-ci | push | master | queued |
| 2026-08-13 08:28:19 | audit-guard | push | master | in_progress |
| 2026-08-13 08:28:19 | pipeline-audit | push | master | in_progress |
| 2026-08-13 08:28:19 | hermes-dashboard | push | master | in_progress |
| 2026-08-13 08:28:19 | security | push | master | in_progress |
| 2026-08-13 08:28:19 | hermes-observer | pull_request_target | forge/012-monde-vivant-commerce-ddda | queued |
| 2026-08-13 08:23:07 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 08:22:58 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 08:22:56 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 08:22:47 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 08:22:45 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 08:22:45 | pipeline-failure-escalate | workflow_run | master | skipped |
| 2026-08-13 08:22:40 | harness-ci | push | forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615 | success |
| 2026-08-13 08:22:40 | audit-guard | push | forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615 | success |

## Agents lancés récemment (Cursor Cloud)

Non disponible dans cette génération (API Cursor non interrogée).

## La boucle d'audit, audit par audit

| audit | où il en est | dernier événement (UTC) |
|---|---|---|
| CURSOR-cdc683f-hermes-workflow-quatre-acteurs | retenu — à convertir en brief | 2026-08-12 11:41 |
| CURSOR-65c3ac1-dashboard-hermes-modele-auditeur | contre-audit rendu — attend la décision | 2026-08-12 12:01 |
| CURSOR-73022bd-hermes-dashboard-modele-auditeur | contre-audit rendu — attend la décision | 2026-08-12 11:55 |
| CURSOR-779d97c-revue-verdicts-illisibles | contre-audit rendu — attend la décision | 2026-08-12 12:30 |
| CURSOR-e849633-hermes-demande-pilotage | retenu — à convertir en brief | 2026-08-12 15:32 |
| CURSOR-0269d8e-hermes-console-droit-executer | retenu — à convertir en brief | 2026-08-12 15:49 |
| CURSOR-3b47ffe-pr57-monde-sans-faim | converti en brief — travail à produire | 2026-08-13 06:25 |
| CURSOR-063d7eb-pr35-challenge-perte-decision | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-1da49ea-pr43-challenge-verdicts-sans-preuve | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-2a4f808-decision-auto-ledger | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-3ce7947-pr36-hermes-skill-versionnee | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-4822662-pr31-verdicts-non-analysables | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-48a5659-push-master-pat-contournement | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-7e5244b-ledger-post-fusion-poussee-master | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-949ecf1-pr42-revue-non-consommable | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-a600532-fusion-sans-contre-audit | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-bb8fe11-hermes-console-adr-0011 | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-e2896e7-pr44-challenge-bb8fe11 | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |

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
| 011-sim-monde-vivant-amorcage | dernier verdict tracé : ACCEPT |
| 012-monde-vivant-commerce-inter-cellules | dernier verdict tracé : ACCEPT |

« État apparent » = dernière mention `VERDICT:` tracée dans le `verdict.md` du brief ; l'autorité reste le fichier lui-même et `HANDOFF.md` pour le contexte.

## Utilisation des backends Générateur

| backend | runs cumulés | dernier run (UTC) |
|---|---|---|
| claude | 28 | 2026-08-11 14:07 |
| cursor | 7 | 2026-08-13 06:59 |
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
