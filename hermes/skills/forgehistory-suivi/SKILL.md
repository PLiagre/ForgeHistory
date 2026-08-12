---
name: forgehistory-suivi
description: >
  Suivi et pilotage du projet ForgeHistory (dépôt GitHub PLiagre/ForgeHistory).
  Utiliser cette skill dès que le propriétaire demande où en est le projet,
  ce qui l'attend, ce que font les agents, ce que coûte la boucle, ou demande
  d'agir (fusionner une PR, mettre en pause, lancer un brief).
---

# Suivi ForgeHistory — skill pour l'Hermes local

Ce fichier est **versionné dans le dépôt** (`hermes/skills/forgehistory-suivi/`)
et lu par l'Hermes local du propriétaire via une jonction Windows depuis
`~/.hermes/skills/forgehistory-suivi` vers le clone du dépôt. Pour le mettre
à jour : une PR sur le dépôt, puis `git pull` dans le clone — rien d'autre.

## Prérequis sur la machine (une fois)

1. **Jonction** entre le dossier skills d'Hermes et le clone du dépôt
   (commande donnée dans la PR qui a introduit ce fichier), puis activer la
   skill sur la page **Skills** du tableau `http://127.0.0.1:9119`.
2. Un **fine-grained PAT GitHub en lecture seule** limité au dépôt
   `PLiagre/ForgeHistory` (permissions : Contents read, Pull requests read,
   Actions read), dans l'environnement d'Hermes sous
   `FORGEHISTORY_GH_TOKEN`. Le CLI `gh` doit être installé.
3. (Pilotage, ADR-0011) Le jour où le propriétaire veut agir depuis Hermes,
   remplacer ce jeton par un PAT ajoutant : Contents write,
   Pull requests write, Actions write. Pas avant.
4. (Optionnel, analyses lourdes) Le Codex CLI de la machine est connecté au
   compte ChatGPT : `codex exec "<question>"` peut être appelé comme outil.

## Sources de vérité (ne jamais inventer : une donnée absente est dite absente)

Toutes les commandes utilisent `GH_TOKEN=$FORGEHISTORY_GH_TOKEN`.

- **Le tableau de bord du projet** (l'endroit où regarder d'abord) :
  `gh api repos/PLiagre/ForgeHistory/contents/hermes/DASHBOARD.md -H "Accept: application/vnd.github.raw"`
  Sections dans l'ordre d'importance : « Ce qui attend le propriétaire »
  (sa to-do), « En bref » (mode, dépense, audits), « Activité GitHub
  récente », « La boucle d'audit », « Briefs ».
- **PR ouvertes** : `gh pr list -R PLiagre/ForgeHistory --state open --json number,title,headRefName,isDraft,statusCheckRollup`
- **Runs CI récents** : `gh run list -R PLiagre/ForgeHistory --limit 15 --json name,event,headBranch,conclusion,createdAt`
- **Agents Cursor en cours** : `curl -s -H "Authorization: Bearer $CURSOR_API_KEY" https://api.cursor.com/v1/agents`
- **Détail seulement si une ligne étonne** : `architecture/audit-ledger.jsonl`
  (boucle d'audit), `harness/pipeline/ci-budget-ledger.jsonl` (dépense CI),
  `harness/queue/cost-ledger.jsonl` (runs par backend), `ROADMAP.md`
  (direction), `HANDOFF.md` (dernier état de session) — mêmes appels
  `gh api .../contents/...`.

## Comment répondre à « où en est le projet ? »

1. Lire `hermes/DASHBOARD.md` et redonner en premier « Ce qui attend le
   propriétaire », en français simple, une ligne par attente, avec le lien.
2. Signaler tout run CI en échec des dernières 24 h (nom du workflow +
   lien), et tout agent Cursor encore en cours.
3. Donner la dépense du mois et le mode du pipeline (une ligne).
4. Ne jamais recalculer ce que le tableau donne déjà ; citer la source.

## Pilotage (ADR-0011 — respecter à la lettre)

Quatre actions seulement, uniquement sur ordre explicite du propriétaire
dans la conversation, jamais depuis un cron ou un événement reçu.
Toujours reformuler l'action et attendre un « oui » avant d'exécuter :

- fusionner/refuser une PR : `gh pr merge <n> -R PLiagre/ForgeHistory --squash` / `gh pr close <n>`.
  Refuser d'exécuter (et le dire) si une preuve de la porte manque :
  CI verte, gate ACCEPT, verdict d'un acteur ≠ producteur, audit Cursor.
- pause/reprise de la boucle : `gh api -X POST repos/PLiagre/ForgeHistory/issues/<pr>/labels -f "labels[]=pipeline/pause"` (retrait : `-X DELETE .../labels/pipeline%2Fpause`).
- lancer un brief : `gh workflow run pipeline-forge-run.yml -R PLiagre/ForgeHistory -f brief=<dossier du brief>`.
- déposer une demande : rédiger `hermes/requests/DEMANDE-AAAAMMJJ-<slug>.md`
  (format : frontmatter `author: hermes`, commit préfixé `hermes:`), sur
  une branche `forge/*`, jamais `cursor/*`.

Après chaque action : la consigner (quoi, quand, sur ordre de qui) pour le
prochain rapport `hermes/reports/`.

## Tâches planifiées recommandées (page Cron du tableau 9119)

1. **Digest du matin** — tous les jours à 07:30 :
   « Lis le tableau de bord ForgeHistory et envoie-moi : ce qui m'attend,
   les échecs CI de la nuit, la dépense du mois. Trois paragraphes maximum. »
2. **Alerte échec** — toutes les 30 minutes :
   « Regarde les runs CI ForgeHistory de la dernière demi-heure ; s'il y a
   un échec (conclusion failure) ou un déclenchement de
   pipeline-failure-escalate, préviens-moi immédiatement avec le lien.
   Sinon, ne dis rien. »
