---
etape: 02
slug: monde-vivant-compte-juste
closed_at: 2026-08-14T07:15:00Z
decided_by: propriétaire (instruction de passe 2026-08-14 — clôturer E2 si 018 PASS et critères réunis) ; rédaction déléguée à l'orchestrateur Cursor remplaçant le CTO Claude
---

# Étape 02 — Le monde vivant compte juste

> **Archive historique.** Les déclenchements d'audit, séparations de rôles et
> portes mentionnés ci-dessous décrivent le workflow de 2026. Ils sont
> obsolètes et n'imposent aucune procédure actuelle.

Ce fichier **constate** la clôture du jalon E2 défini dans `ROADMAP.md`
§ « Grandes étapes — jalons d'audit ». Il n'instruit aucun brief (voir
`CLAUDE.md` › Single Source of Instruction). Sa fusion sur `master`
déclenche l'audit Cursor de l'étape, puis le contre-audit Claude
(ADR-0012).

## Ce que l'étape devait réunir

Copié de `ROADMAP.md` au moment de la clôture :

> seuil de survie honnête (graines 015/016 traitées) ; agrégation
> Province dérivée (ADR-0003) ; monde mesuré stable et falsifiable sur
> les 596 cellules réelles.

## Ce qui est effectivement livré

### Seuil de survie honnête ✓

Brief 017 (`harness/queue/briefs/017-sim-seuil-survie-honnete/`), fusion
des graines 015 et 016 issues des audits de la PR #69. Verdict PASS à
l'itération 1, porte ACCEPT dix sur dix. **PR #101** fusionnée le
2026-08-14 à 05:53 UTC, **sans squash**.

Le critère de survie n'est plus une fenêtre aveugle à la mortalité : la
prédiction vise l'état stationnaire et dépend des constantes qui tuent ;
un accumulateur reporte les morts fractionnaires ; « affamée » signifie
une pénurie réelle, pas un garde-manger vide ; la dette alimentaire ne
diminue que par des kilogrammes réellement consommés.

Les graines 015/016 ne s'exécutent plus : elles pointent vers 017.

### Agrégation Province dérivée ✓

Brief 018 (`harness/queue/briefs/018-sim-province-derivee/`). Verdict
PASS à l'itération 1, porte ACCEPT dix sur dix. **PR #102** (à fusionner
avant celle-ci, sans squash).

`cell_id` reste la seule clé spatiale. La Province est un regroupement
calculé des cellules — jamais un champ stocké. Source déclarée comme
proxy : 50 centroïdes hérités du jeu, pas des frontières historiques de
1400. Toute cellule G3 a exactement une province (596/596). Un redessin
de centroïde en mémoire change l'agrégat (22 cellules) sans réécrire les
cellules.

### Monde mesuré stable et falsifiable sur les 596 cellules réelles ✓

Déjà établi par les briefs 012, 013 et 017 (`World.from_g3`, graines
42/42, compteurs de faim, morts, kilogrammes transportés, fraction de
survie). Le brief 018 s'appuie sur le même échantillon : 596 cellules
chargées, égalité à `cell_count` lu dans `stats_g3.json`, jamais un
monde à la main. Les suites `sim/tests/` restent vertes (65 passed après
018).

## Ce qui a été volontairement reporté

- **Réserve N1 du verdict 017** : la prédiction de survie dépend de
  `HUNGER_DEATH_SCALE` mais faiblement — le test de conformité ne
  discrimine pas encore les régimes. Brief ultérieur, pas un motif de
  refuser E2.
- **F1 geo** : relief, climat, ressources — jalon E1, pas E2.
- **Briefs de harnais** (traçage d'acteur, gate sur fichiers hors
  dossier de brief, etc.).
- **Arriéré d'audits PROPOSED** : adjudication en lot au présent jalon
  (ou purge `STALE` motivée), conformément à ADR-0012. Ce fichier ne
  tranche aucun audit.
- **Réparation du déclenchement d'étape sans jalon** (PR #100 /
  `CURSOR-546a9d4`) : hors périmètre du lot 018, à juger avec le reste
  de l'arriéré.
- **La Province n'est pas encore un acteur économique** : pas de
  fiscalité, pas de commerce inter-provinces, pas de `Person`. C'est une
  vue. Les réserves N1–N7 du verdict 018 décrivent ce qu'il faudra
  durcir avant qu'elle le devienne.

## PRs et commits d'étape (depuis E1, inexistant — depuis l'origine moteur)

Les livrables moteur de la couche 1, dans l'ordre :

| brief | PR | fusion | objet |
|---|---|---|---|
| 011 | (session 2026-08-12) | 2026-08-12 | amorçage `sim/` |
| 012 | #60 | 2026-08-13 | monde vivant + commerce |
| 013 | (empilée, fusionnée le 2026-08-13) | 2026-08-13 | un kilogramme nourrit une fois |
| 014 | #83 | 2026-08-13 | pipeline (hors couche 1, même période) |
| 017 | #101 | 2026-08-14 | seuil de survie honnête |
| 018 | #102 | à fusionner avant ce jalon | Province dérivée |

L'audit déclenché par la fusion de ce fichier couvre **tout ce qui est
entré sur `master` depuis l'origine** (premier jalon).
