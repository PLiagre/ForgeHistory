---
author: hermes
kind: demande
created_at: 2026-08-21T09:53:31Z
concerns: projet
status: OPEN
---
# Répartir les rôles entre Grok 4.6, Composer et Claude

Le propriétaire souhaite réduire fortement l’usage de Claude Code et le réserver aux tâches critiques. Il dispose d’un volume important de crédits Cursor et demande que ForgeHistory utilise davantage les modèles Cursor, notamment Grok 4.6.

## Direction propriétaire

- Claude ne doit plus absorber la planification courante, la rédaction exhaustive de tous les briefs et les relectures ordinaires.
- Grok 4.6 doit recevoir des rôles précis correspondant à ses points forts, et pas être utilisé indistinctement sur chaque tâche.
- Composer 2.5 reste disponible pour les exécutions bornées et rapides lorsqu’un modèle plus coûteux n’apporte pas de gain attendu.
- Les décisions d’architecture, de sécurité, de gouvernance et les invariants fondamentaux doivent conserver un niveau de contrôle renforcé.
- La nouvelle répartition doit être mesurée sur des lots ForgeHistory réels avant généralisation.

## État observé

La configuration actuelle appelle toujours Claude Opus 5 pour les rôles `planner` et `reviewer`, et Cursor Composer 2.5 pour `executor`. Le CLI Cursor authentifié expose Grok 4.6 aux efforts `low`, `medium`, `high` et `xhigh`.

Le backend est aujourd’hui implicite par rôle : changer uniquement le nom de modèle ne permet pas de faire planifier ou relire par Cursor. Une évolution technique de ForgePilot est donc nécessaire pour rendre le backend configurable par rôle.

## Étude technique demandée

La solution doit examiner au minimum les rôles suivants : analyse préparatoire, planification courante, exécution bornée, exécution complexe, itération, pré-relecture adversariale et verdict critique. Elle doit conserver la source unique d’instruction, l’indépendance du jugement critique, le veto propriétaire sur la fusion et la mesure honnête des coûts, durées, itérations et défauts uniques détectés.

La recommandation issue de l’analyse initiale est un pilote hybride : Grok 4.6 High pour l’analyse, la planification courante, l’exécution complexe et la pré-relecture ; Composer 2.5 pour l’exécution bornée et les itérations rapides ; Claude Opus 5 réservé au verdict des lots critiques pendant le pilote.

Cette demande n’active pas immédiatement un nouveau backend, ne modifie pas `control-plane/config.toml`, ne change pas ADR-0014 et ne remplace pas le reviewer du lot 025 actuellement en attente. Ces changements nécessitent un brief et les décisions de gouvernance correspondantes.
