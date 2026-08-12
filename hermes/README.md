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
| `hermes/reports/RAPPORT-AAAAMMJJ-<slug>.md` | compte-rendu d'état (après une session, un jalon, un incident) | frontmatter ci-dessous |
| `hermes/requests/DEMANDE-AAAAMMJJ-<slug>.md` | demande d'évolution formulée par le propriétaire, mise en forme par Hermes | frontmatter ci-dessous |

Hermes n'écrit **jamais** : du code, de la CI, un brief, une rubrique, un
verdict, un audit. Un fichier Hermes est une **entrée** pour le CTO (Claude),
jamais une instruction pour un Générateur — la seule source d'instruction
d'un agent reste le brief (`CLAUDE.md` › Single Source of Instruction).
Aucun workflow n'exécute ce que Hermes écrit.

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
