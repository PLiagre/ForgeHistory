# hermes/milestones/ — les jalons d'audit (ADR-0012)

Un fichier ici = une **grande étape close**. Sa fusion sur `master` est le
seul déclencheur automatique de l'audit Cursor (puis du contre-audit Claude
qui suit tout dépôt d'audit). La liste des étapes et leur contenu vivent
dans `ROADMAP.md` § « Grandes étapes — jalons d'audit » — ce dossier ne
définit rien, il **constate**.

## Format

Un fichier par jalon : `ETAPE-NN-<slug>.md`, écrit par Hermes (ou le CTO
sur décision du propriétaire), avec ce frontmatter :

```
---
etape: NN
slug: <slug>
closed_at: <ISO 8601 UTC>
decided_by: <propriétaire | référence de la décision>
---
```

Le corps dit, en français clair : ce que l'étape devait réunir (copié
depuis `ROADMAP.md` au moment de la clôture), ce qui est effectivement
livré (avec les PRs/commits), et ce qui a été volontairement reporté.

## Ce que la fusion déclenche

1. `pipeline-audit.yml` lance `cursor-auditor` (avec `cursor-qa-scout`)
   sur l'étape entière : tout ce qui est entré sur `master` depuis le
   jalon précédent (ou depuis l'origine pour le premier).
2. (Historique — la boucle audit / contre-audit est supprimée par ADR-0018.)
   L'audit déposé dans `architecture/inbox/` déclenchait le contre-audit
   (`pipeline-challenge.yml`), puis la décision (`pipeline-orchestrate`) —
   mécanique inchangée, cadence par jalon.

Pour un audit **hors jalon** (incident, changement structurel entériné par
ADR, doute) : `workflow_dispatch` de `pipeline-audit.yml` — c'est la voie
prévue, pas une dérogation.
