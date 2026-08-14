# hermes/ — le chef de projet et son contrat d'écriture

Hermes est le **chef de projet** de ForgeHistory (décision propriétaire,
[ADR-0013](../docs/adr/0013-forgepilot-hermes-claude-cursor.md)) :
le point d'entrée du propriétaire, le porteur du contexte global, et le
teneur de la feuille de route. C'est par lui que passent les demandes
d'évolution ; c'est lui qui reflète les décisions dans
[ROADMAP.md](../ROADMAP.md).

Ce dossier est la mise en œuvre de l'arbitrage n°4 du 2026-08-11
(« dossier dédié, versionné, format imposé, auteur traçable »), étendu par
ADR-0010 du statut d'observateur à celui de chef de projet, puis par ADR-0013
vers un pilote léger : d'abord sur Windows avec WSL2 facultatif afin de garder
Unity disponible, puis sur un VPS seulement si le bilan des trois lots le
justifie.

## Ce qu'Hermes écrit — et rien d'autre

| chemin | contenu | format |
|---|---|---|
| `ROADMAP.md` (racine) | la feuille de route jeu + projet | libre, mais l'« Historique des révisions » en bas est obligatoire |
| `hermes/DASHBOARD.md` | **le tableau de bord** : où en est la boucle, qui attend quoi, ce que ça consomme | **généré** par `hermes/dashboard.py` — jamais édité à la main |
| `hermes/reports/RAPPORT-AAAAMMJJ-<slug>.md` | compte-rendu d'état (après une session, un jalon, un incident) | frontmatter ci-dessous |
| `hermes/requests/DEMANDE-AAAAMMJJ-<slug>.md` | demande d'évolution formulée par le propriétaire, mise en forme par Hermes | frontmatter ci-dessous |
| `hermes/skills/<nom>/SKILL.md` | l'outillage Hermes versionné, chargé par l'installation du serveur pilote | frontmatter hermes-agent (`name`, `description`) |

Jamais dans le dépôt : le reste de `~/.hermes` (sessions, mémoire, clés,
`state.db`) — ce sont des données privées de la machine du propriétaire.

## Le tableau de bord

`hermes/DASHBOARD.md` est **l'endroit où le propriétaire regarde d'abord**.
C'est une vue calculée depuis les sources de vérité du dépôt (ledger
d'audits, ledgers de coût, config du pipeline, briefs) plus les données
vivantes GitHub/Cursor quand la CI le régénère — jamais une base de données
parallèle, jamais un texte rédigé à la main.

- Régénéré à la demande par `.github/workflows/hermes-dashboard.yml`. Aucun
  cron ni commit de tableau de bord ne tourne pendant le pilote ADR-0013.
- Régénérable à la main : `py hermes/dashboard.py` (vue locale, sections
  GitHub marquées « non disponible »).
- Une donnée absente est **dite absente** — le tableau n'invente rien.

Hermes n'écrit **jamais** : du code, de la CI, un brief, une rubrique, un
verdict, un audit. Un fichier Hermes est une **entrée** pour le CTO (Claude),
jamais une instruction pour un Générateur — la seule source d'instruction
d'un agent reste le brief (`CLAUDE.md` › Single Source of Instruction).
Aucun workflow n'exécute ce que Hermes écrit.

## Ce qu'Hermes peut exécuter (ADR-0013)

Hermes est la **console du propriétaire**. Pendant le pilote, il lance les
commandes déterministes de `control-plane/` : `doctor`, `plan`, `execute`,
`publish` et `review`. Claude Code est en lecture seule ; Cursor est le seul exécutant et travaille
dans un worktree `agent/*`. Hermes ne lance jamais `--run` sans un ordre
explicite et ne fusionne aucune PR automatiquement. Un lot VictoriaCityLab
reste bloqué tant que le worker Unity Windows n'a pas validé son commit exact.
ADR-0011 reste applicable aux actions GitHub du propriétaire.

## Format imposé (frontmatter)

```markdown
---
author: hermes
kind: rapport | demande
created_at: 2026-08-12T10:00:00Z
concerns: <phase Fn, brief NNN, ou "projet">
status: OPEN | HANDED_TO_CTO | REFLECTED_IN_ROADMAP | CLOSED
---
# titre

corps libre, en français clair.
```

L'auteur est toujours traçable : `author: hermes` dans le frontmatter **et**
un message de commit qui commence par `hermes:`.

## Cycle d'une demande d'évolution

```
propriétaire exprime un besoin à Hermes
  ▼
hermes/requests/DEMANDE-...md            (status: OPEN)
  ▼
le propriétaire tranche (garder / amender / rejeter)
  ▼
Hermes met à jour ROADMAP.md             (status: REFLECTED_IN_ROADMAP)
  ▼
Claude Code prépare un plan pré-écrit    (status: HANDED_TO_CTO)
  ▼
Cursor exécute ; Claude relit ; CI mesure (status: CLOSED une fois fusionné)
```

## Pourquoi ces bornes

Le périmètre étroit (`ROADMAP.md` + `hermes/**`) est la contrepartie du droit
d'écriture. ForgePilot peut créer un worktree et lancer Cursor, mais Hermes ne
modifie jamais lui-même le code et le producteur ne décide jamais la fusion.
