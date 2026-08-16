---
author: hermes
kind: demande
created_at: 2026-08-15T20:00:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Hermes pilote, Claude juge, Cursor exécute — vers un pipeline long et supervisé

Le propriétaire tranche une répartition des rôles qui avait dérivé dans les
faits, et fixe la direction à terme.

## Ce que le propriétaire veut

- **Hermes** est le point d'entrée principal, la mémoire du projet et le chef
  de projet. C'est lui qui pilote.
- **Claude Code** planifie et orchestre. Il n'est pas le point d'entrée.
- **Cursor** exécute les développements.
- À terme : un pipeline qui tourne **longtemps sans intervention**, sur un VPS
  portant Hermes, avec des **sessions longues supervisées** par le
  propriétaire — il lit des comptes-rendus et intervient quand ça compte, il ne
  pilote plus chaque étape à la main.

## Le dérapage constaté

Entre le `2026-08-13` et le `2026-08-15` (briefs `019` à `022`), le propriétaire
s'est adressé directement à Claude Code et Hermes n'a rien reçu en retour :

- `hermes/reports/` ne contenait qu'**un seul** rapport, daté du `2026-08-12`.
- `hermes/DASHBOARD.md` datait du `2026-08-14 12:47 UTC` — périmé de plus d'un
  jour, montrant un état faux du projet. Le workflow qui le régénère est passé
  en `workflow_dispatch` seul par ADR-0013, sans que personne ne soit désigné
  pour le déclencher.
- Le brief `022` a été écrit par Claude de sa propre initiative, sans passer par
  `hermes/requests/`.

Le propriétaire tranche : **ce n'est pas la pratique qui a raison, c'est
l'ADR-0010.** Il faut corriger le dérapage, pas l'entériner.

## La mesure qui commande l'architecture

Le coût réel du travail Claude a été mesuré sur cette session, par
`harness/backends/ledger.py tokens` :

| poste | USD (équivalent tarif API) | appels | contexte moyen par appel |
|---|---|---|---|
| session d'orchestration (Claude Code) | `59.70` | `434` | `213 801` |
| sous-agents divers | `5.02` | `61` | — |
| sous-agent Évaluateur (mort sur le plafond) | `1.97` | `37` | `51 520` |
| **total observable** | **`68.66`** | | |

Le coût de Cursor n'est pas mesurable par ce registre — il le dit explicitement
et ne le suppose pas nul.

**Là où part l'argent n'est pas là où on croyait.** Le plan a coûté `1.08` USD,
la relecture du lot `1.96`. L'orchestration, elle, pèse **`87` %** du total. Le
registre nomme lui-même le levier : `213 801` jetons de contexte moyen par
appel — chaque appel de l'orchestrateur renvoie une conversation devenue énorme.

Le plafond mensuel de l'abonnement Claude a été atteint pendant cette session,
pour la troisième fois depuis le `2026-08-13`.

## Ce que le propriétaire demande de décider

1. **Hermes déclenche et rend compte ; Claude juge.** Hermes tient l'état,
   lance les lots, agrège et écrit au propriétaire. Il ne juge rien. Claude est
   appelé à la demande pour planifier, relire et rendre un verdict. Cursor
   exécute.
2. **Qui régénère le tableau de bord**, maintenant qu'ADR-0013 a coupé
   l'automatisme sans désigner de responsable.
3. **La hiérarchie entre `HANDOFF.md` et `hermes/`** comme mémoire du projet :
   les deux se disputent le rôle aujourd'hui et les deux pourrissent.
4. **Un budget mensuel Claude assumé**, dont se déduit la cadence des jugements.

## Ce que le propriétaire ne demande pas

- Pas d'auto-fusion. La fusion reste humaine.
- Pas de cron réveillant l'ancien full-auto.
- Pas de VPS avant que la répartition des rôles ne soit décidée et éprouvée en
  local.
