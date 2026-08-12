# hermes/ — le chef de projet et son contrat d'écriture

Hermes est le **chef de projet** de ForgeHistory (décision propriétaire,
[ADR-0010](../docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md)) :
le point d'entrée du propriétaire, le porteur du contexte global, et le
teneur de la feuille de route. C'est par lui que passent les demandes
d'évolution ; c'est lui qui reflète les décisions dans
[ROADMAP.md](../ROADMAP.md).

Ce dossier est la mise en œuvre de l'arbitrage n°4 du 2026-08-11
(« dossier dédié, versionné, format imposé, auteur traçable »), étendu par
ADR-0010 du statut d'observateur à celui de chef de projet.

## Ce qu'Hermes écrit — et rien d'autre

| chemin | contenu | format |
|---|---|---|
| `ROADMAP.md` (racine) | la feuille de route jeu + projet | libre, mais l'« Historique des révisions » en bas est obligatoire |
| `hermes/DASHBOARD.md` | **le tableau de bord** : où en est la boucle, qui attend quoi, ce que ça consomme | **généré** par `hermes/dashboard.py` — jamais édité à la main |
| `hermes/reports/RAPPORT-AAAAMMJJ-<slug>.md` | compte-rendu d'état (après une session, un jalon, un incident) | frontmatter ci-dessous |
| `hermes/requests/DEMANDE-AAAAMMJJ-<slug>.md` | demande d'évolution formulée par le propriétaire, mise en forme par Hermes | frontmatter ci-dessous |
| `hermes/skills/<nom>/SKILL.md` | l'outillage de l'Hermes local (hermes-agent), versionné ici et lu sur le PC du propriétaire via une jonction depuis `~/.hermes/skills/` | frontmatter hermes-agent (`name`, `description`) |

Jamais dans le dépôt : le reste de `~/.hermes` (sessions, mémoire, clés,
`state.db`) — ce sont des données privées de la machine du propriétaire.

## Le tableau de bord

`hermes/DASHBOARD.md` est **l'endroit où le propriétaire regarde d'abord**.
C'est une vue calculée depuis les sources de vérité du dépôt (ledger
d'audits, ledgers de coût, config du pipeline, briefs) plus les données
vivantes GitHub/Cursor quand la CI le régénère — jamais une base de données
parallèle, jamais un texte rédigé à la main.

- Régénéré automatiquement par `.github/workflows/hermes-dashboard.yml` à
  chaque poussée sur `master` et toutes les 6 heures.
- Régénérable à la main : `py hermes/dashboard.py` (vue locale, sections
  GitHub marquées « non disponible »).
- Une donnée absente est **dite absente** — le tableau n'invente rien.

Hermes n'écrit **jamais** : du code, de la CI, un brief, une rubrique, un
verdict, un audit. Un fichier Hermes est une **entrée** pour le CTO (Claude),
jamais une instruction pour un Générateur — la seule source d'instruction
d'un agent reste le brief (`CLAUDE.md` › Single Source of Instruction).
Aucun workflow n'exécute ce que Hermes écrit.

## Ce qu'Hermes peut exécuter (ADR-0011)

Depuis [ADR-0011](../docs/adr/0011-hermes-console-du-proprietaire.md),
Hermes est aussi la **console du propriétaire** : il peut exécuter, sur
ordre explicite du propriétaire et jamais de sa propre initiative, quatre
actions qui appartiennent au propriétaire — fusionner/refuser une PR,
poser/retirer le label `pipeline/pause`, déclencher `pipeline-forge-run`
sur un brief, déposer une demande. Périmètre, garde-fous (confirmation,
jeton minimal, trace dans `hermes/reports/`) et interdits inchangés : voir
l'ADR — ce fichier ne les paraphrase pas.

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
Claude (CTO) écrit le ou les briefs      (status: HANDED_TO_CTO)
  ▼
la boucle harnais fait le reste          (status: CLOSED une fois fusionné)
```

## Pourquoi ces bornes

Le périmètre étroit (`ROADMAP.md` + `hermes/**`) est la contrepartie du
droit d'écriture : un chef de projet qui toucherait au code ou aux briefs
cumulerait pilotage et production, ce que tout le harnais existe à empêcher.
Ces chemins ne figurent pas dans l'allowlist du merge-bot : une PR Hermes
est toujours relue par le propriétaire (ou son délégué) avant fusion.
