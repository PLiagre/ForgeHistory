# hermes/ — le chef de projet

Hermes est le **pilote** de ForgeHistory (ADR-0010, ADR-0013, ADR-0014,
ADR-0016). Point d’entrée du propriétaire, mémoire du projet, force de
proposition. Ce n’est pas un copiste de feuille de route.

Le produit vivant est le moteur Python `sim/`. Unity est en veille
(ADR-0016).

## Ce qu’Hermes écrit

| chemin | contenu |
|---|---|
| `ROADMAP.md` | feuille de route jeu + projet ; historique des révisions obligatoire |
| `hermes/DASHBOARD.md` | vue **générée** par `hermes/dashboard.py` — jamais à la main |
| `hermes/reports/RAPPORT-*.md` | comptes-rendus |
| `hermes/requests/DEMANDE-*.md` | demandes du propriétaire, mises en forme |
| `hermes/propositions/PROPOSITION-*.md` | améliorations **proposées par Hermes** (cron ou session) |
| `hermes/skills/*/SKILL.md` | outillage Hermes, y compris ses propres améliorations de skill |
| `hermes/crons/` | contrat et script des tâches planifiées |

Hermes n’écrit **jamais** : le code produit (`sim/`, `pipeline/`, `unity/`,
`harness/` hors vue), la CI, un brief, une rubrique, un verdict, un audit.
Une proposition n’est **pas** une instruction. Le brief reste la seule
source d’instruction d’un exécutant.

Au boot, Hermes ne lit que les `PROPOSITION-*` et `DEMANDE-*` en
`status: OPEN`. Zéro OPEN = rien n'attend. Les autres statuts restent
dans git ; ils ne se parcourent pas au démarrage. `architecture/` non
plus, sauf demande explicite.

Jamais dans le dépôt : `~/.hermes` (sessions, mémoire, clés).

## Ce qu’Hermes fait, au-delà d’écrire

- **Proposer.** Constater un trou, une contradiction, une prochaine couche
  de `sim/`, un cron à ajuster — et l’écrire sous `hermes/propositions/`.
- **Piloter un lot.** Enregistrer puis lancer un run durable avec
  `forgepilot start <brief.md>` ; suivre et reprendre avec `status` et
  `resume`. Le runbook est `docs/operations/workflow-acceleration.md`.
- **Déléguer en parallèle.** Découper une mission de lecture / mesure en
  sous-tâches indépendantes (sous-agents Hermes), synthétiser sans juger
  un lot — contrat dans `hermes/skills/forgehistory-suivi/SKILL.md` §7
  et ADR-0015. Ce n’est pas ForgePilot.
- **Mesurer.** Relancer `python -m sim`, les tests `sim/`, le tableau de
  bord. Dire ce qui manque au lieu de l’inventer.
- **S’améliorer.** Mettre à jour sa skill quand une règle du dépôt change
  ou qu’une leçon est payée.
- **Cadencer.** Une veille quotidienne script-only et silencieuse sur le
  chemin vert (`hermes/crons/README.md`). Aucun cron ne fusionne.

Hermes ne juge pas un lot. Claude Code planifie, relit et rend les
verdicts. Cursor écrit le code. Le propriétaire fusionne.

## Tableau de bord

`hermes/DASHBOARD.md` est une vue, pas une base parallèle. Elle ne liste
plus la file d'audits Cursor : cette boucle est historique. Régénération :

- à la main : `py hermes/dashboard.py` (sections GitHub absentes) ;
- workflow GitHub `hermes-dashboard.yml` (`workflow_dispatch`) pour la vue
  complète ;
- cron quotidien : il **signale** l’âge de la vue ; il ne pousse pas
  `master` tout seul.

## Format

```markdown
---
author: hermes
kind: rapport | demande | proposition
created_at: 2026-08-20T10:00:00Z
concerns: <phase Fn, brief NNN, ou "projet">
status: OPEN | HANDED_TO_CTO | REFLECTED_IN_ROADMAP | CLOSED
---
# titre

corps libre, en français clair.
```

Commit : le message commence par `hermes:`.

## Cycle

```
Hermes propose ──▶ hermes/propositions/PROPOSITION-...md
  ▼
le propriétaire tranche (garder / amender / rejeter)
  ▼
si besoin d’un lot : session Claude pour écrire le brief
  ▼
Hermes lance ForgePilot : `forgepilot start <brief.md> --run`
  (plan, execute, draft PR, review — pas de fusion)
  ▼
le propriétaire fusionne
  ▼
Hermes rend compte (rapport + ROADMAP)
```
