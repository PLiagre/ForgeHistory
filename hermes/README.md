# `hermes/` — suivi facultatif du projet

Ce dossier regroupe des vues et des traces de suivi. Il ne possède aucune
autorité sur le code, la roadmap ou la livraison. Tout contributeur ou agent
autorisé peut lire et modifier ces fichiers comme le reste du dépôt.

| chemin | contenu |
|---|---|
| `DASHBOARD.md` | vue générée par `dashboard.py` |
| `reports/` | comptes-rendus historiques |
| `requests/` | demandes et décisions enregistrées |
| `propositions/` | pistes de travail, sans caractère obligatoire |
| `milestones/` | constats de jalons historiques |
| `crons/` | veille locale installable ou lançable manuellement |
| `skills/` | aides facultatives pour les outils compatibles |

Les documents datés conservent leur contexte d'époque. Les répartitions de
rôles et procédures qu'ils décrivent sont historiques ; seules les règles
courantes d'`AGENTS.md` s'appliquent.

## Tableau de bord

```bash
python3 hermes/dashboard.py
```

`DASHBOARD.md` est une vue factuelle, jamais une base de données parallèle.
Sa régénération est facultative et ne conditionne aucun changement.

## Format conseillé pour une nouvelle trace

```markdown
---
author: <nom libre>
kind: rapport | demande | proposition
created_at: <ISO 8601 UTC>
concerns: <sujet>
status: OPEN | CLOSED
---
# Titre
```

Le champ `author` sert à la traçabilité. Il ne donne ni ne retire aucun droit.
