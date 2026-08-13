# Tableau de bord — ForgeHistory

> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —
> **ne jamais l'éditer à la main**, elle est réécrite à chaque
> poussée sur `master` et toutes les 6 heures par
> `.github/workflows/hermes-dashboard.yml`.
>
> Générée le 2026-08-13 20:21 UTC.

## En bref

- **Mode du pipeline** : `full_auto` — la boucle tourne sans intervention humaine (hors fusion finale).
- **Dépense CI ce mois-ci** : 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD. En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.
- **Audits en cours** : 39 — boucles closes : 9.

## Ce qui attend le propriétaire

- Fusionner (ou refuser) la PR #99 — « ADR-0012 : audit et contre-audit par grandes étapes — plus jamais par PR (décision propriétaire, empilée sur #92) » (branche `forge/adr-0012-audit-par-etapes-e180`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Fusionner (ou refuser) la PR #92 — « Clôture de session 2026-08-13 (après-midi) : addendum HANDOFF + correction factuelle ROADMAP » (branche `forge/cloture-session-20260813-e180`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Fusionner (ou refuser) la PR #89 — « Tenue de registre : conversions des audits moteur de la PR #69 — graines de briefs 015 et 016 (empilée sur #77) » (branche `forge/conversions-briefs-015-016-e180`). L'auto-fusion GitHub est indisponible sur ce plan : le clic final est humain.
- Convertir l'audit retenu `CURSOR-cdc683f-hermes-workflow-quatre-acteurs` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-e849633-hermes-demande-pilotage` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-0269d8e-hermes-console-droit-executer` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-16ff5ac-contre-audit-perdu-a-la-publication` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-4c45718-pr65-ledger-recupere-a-la-main` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-9e35764-pr63-contre-audit-jamais-enregistre` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-29913c0-pr69-seuil-survie-non-borne` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-827d54e-contre-audit-paye-jamais-publie` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite` en brief (`/forge-audit-convert`).
- Convertir l'audit retenu `CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre` en brief (`/forge-audit-convert`).

## Activité GitHub récente

| quand (UTC) | workflow | déclencheur | branche | résultat |
|---|---|---|---|---|
| 2026-08-13 20:21:15 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:21:15 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:21:15 | pipeline-failure-escalate | workflow_run | master | queued |
| 2026-08-13 20:21:13 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:21:09 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:21:04 | harness-ci | push | master | in_progress |
| 2026-08-13 20:21:04 | audit-guard | push | master | in_progress |
| 2026-08-13 20:21:04 | pipeline-audit | push | master | in_progress |
| 2026-08-13 20:21:04 | security | push | master | in_progress |
| 2026-08-13 20:21:01 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:20:59 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:20:58 | pipeline-failure-escalate | workflow_run | master | skipped |
| 2026-08-13 20:20:58 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:20:57 | hermes-observer | workflow_run | master | queued |
| 2026-08-13 20:20:56 | security | push | master | success |

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
| CURSOR-a600532-fusion-sans-contre-audit | converti en brief — travail à produire | 2026-08-13 08:40 |
| CURSOR-16ff5ac-contre-audit-perdu-a-la-publication | retenu — à convertir en brief | 2026-08-13 11:00 |
| CURSOR-4c45718-pr65-ledger-recupere-a-la-main | retenu — à convertir en brief | 2026-08-13 11:01 |
| CURSOR-9e35764-pr63-contre-audit-jamais-enregistre | retenu — à convertir en brief | 2026-08-13 11:03 |
| CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion | retenu — à convertir en brief | 2026-08-13 11:04 |
| CURSOR-29913c0-pr69-seuil-survie-non-borne | retenu — à convertir en brief | 2026-08-13 12:50 |
| CURSOR-827d54e-contre-audit-paye-jamais-publie | retenu — à convertir en brief | 2026-08-13 12:51 |
| CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite | retenu — à convertir en brief | 2026-08-13 12:53 |
| CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre | retenu — à convertir en brief | 2026-08-13 12:55 |
| CURSOR-063d7eb-pr35-challenge-perte-decision | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-1da49ea-pr43-challenge-verdicts-sans-preuve | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-2a4f808-decision-auto-ledger | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-3ce7947-pr36-hermes-skill-versionnee | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-4822662-pr31-verdicts-non-analysables | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-48a5659-push-master-pat-contournement | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-587ee82-pr84-contre-audit-sans-pouvoir-de-refus | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-70380c6-pr83-etat-refus-sans-lecteur | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-70380c6-pr83-porte-desarmee-par-la-derive-de-sha | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-786ec32-pr74-verdicts-fantomes-au-registre | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-7e5244b-ledger-post-fusion-poussee-master | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-8894f15-pr71-arbitrage-proprietaire-efface | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-949ecf1-pr42-revue-non-consommable | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-9626e9b-pr85-p0-perdu-a-la-decision | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-a7d1c57-pr76-approbation-sans-conversion | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-bb8fe11-hermes-console-adr-0011 | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-c296c47-pr86-revue-sans-preuve-citable | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-c348018-pr89-justification-hors-registre | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-dcbe815-pr87-registre-refute-un-point-fantome | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-e2896e7-pr44-challenge-bb8fe11 | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |
| CURSOR-ff9b53b-pr92-etat-de-la-boucle-recopie | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |

(9 boucle(s) close(s) non listée(s) — détail : `architecture/audit-ledger.jsonl`.)

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
| 013-sim-tick-nourrit-une-fois | dernier verdict tracé : ACCEPT |
| 014-pipeline-contre-audit-porte | dernier verdict tracé : ACCEPT |

« État apparent » = dernière mention `VERDICT:` tracée dans le `verdict.md` du brief ; l'autorité reste le fichier lui-même et `HANDOFF.md` pour le contexte.

## Utilisation des backends Générateur

| backend | runs cumulés | dernier run (UTC) |
|---|---|---|
| claude | 29 | 2026-08-13 12:32 |
| cursor | 11 | 2026-08-13 12:46 |
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
