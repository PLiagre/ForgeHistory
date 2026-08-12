# Tableau de bord — ForgeHistory

> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —
> **ne jamais l'éditer à la main**, elle est réécrite à chaque
> poussée sur `master` et toutes les 6 heures par
> `.github/workflows/hermes-dashboard.yml`.
>
> Générée le 2026-08-12 10:13 UTC.

## En bref

- **Mode du pipeline** : `full_auto` — la boucle tourne sans intervention humaine (hors fusion finale).
- **Dépense CI ce mois-ci** : 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD. En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.
- **Audits en cours** : 1 — boucles closes : 7.

## Ce qui attend le propriétaire

- Rien : aucune PR ouverte connue, aucun audit en attente de décision.

## Activité GitHub récente

Non disponible dans cette génération (données GitHub non fournies au script).

## Agents lancés récemment (Cursor Cloud)

Non disponible dans cette génération (API Cursor non interrogée).

## La boucle d'audit, audit par audit

| audit | où il en est | dernier événement (UTC) |
|---|---|---|
| CURSOR-cdc683f-hermes-workflow-quatre-acteurs | déposé — attend le contre-audit de Claude | — (fichier inbox, pas encore au ledger) |

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
