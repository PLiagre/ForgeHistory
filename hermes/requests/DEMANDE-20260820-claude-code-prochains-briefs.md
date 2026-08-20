---
author: hermes
kind: demande
created_at: 2026-08-20T20:30:31Z
concerns: projet
status: HANDED_TO_CTO
---
# Exploiter pleinement Claude Code pour préparer les prochains lots

Le propriétaire souhaite que Claude Code soit mobilisé au maximum de ses capacités pour étudier ForgeHistory et préparer les prochains briefs exécutables du projet.

## Direction demandée

Claude Code doit reprendre les sources de vérité du dépôt, examiner l’état réel du produit et préparer une succession cohérente de prochains lots. La priorité produit déjà inscrite dans `ROADMAP.md` reste la clôture de F1 : après le relief G6 actuellement en cours, poursuivre avec le climat puis les ressources.

Le propriétaire privilégie ici la profondeur et la qualité du travail de Claude Code plutôt que l’économie de jetons. L’abandon d’une enveloppe mensuelle Claude comme préalable est déjà consigné dans la demande du 2026-08-20 et dans `ROADMAP.md` ; cette demande ne rouvre pas cet arbitrage.

## Décisions déjà en vigueur

- ADR-0014 reste applicable : Hermes déclenche et rend compte, Claude Code planifie et juge, Cursor exécute, et le propriétaire garde le veto sur la fusion.
- Un brief demeure l’unique source d’instruction de l’exécutant.
- Un seul lot est exécuté à la fois ; préparer une suite de briefs n’autorise pas leur exécution simultanée.
- Tout lot VictoriaCityLab ou Unity demeure bloqué tant que le worker Windows dédié ne fournit pas sa preuve.

## État observé

Au moment de la demande, `master` est propre au commit `5ba96bc`. Le lot 024 — relief G6 — est en cours d’itération dans un worktree Cursor isolé. La feuille de route place ensuite le climat et les ressources dans F1. `forgepilot doctor --check-auth` confirme que Claude Code, Cursor et GitHub sont authentifiés.

## Questions confiées à Claude Code

Claude Code doit déterminer, à partir des contrats, dépendances et preuves réellement présents dans le dépôt, le découpage technique le plus sûr et le plus mesurable pour le climat puis les ressources. Il doit signaler toute décision produit ou d’architecture qui ne peut pas être déduite des décisions existantes, au lieu de la prendre silencieusement.

Cette demande n’active ni full-auto, ni cron, ni auto-fusion, ni extension des droits d’écriture d’Hermes. Elle n’autorise pas non plus l’exécution simultanée de plusieurs lots.
