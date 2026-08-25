# Crons Hermes

Autorités : [ADR-0015](../../docs/adr/0015-capacites-hermes-sous-agents-crons-issues.md),
[ADR-0016](../../docs/adr/0016-sim-sans-unity-hermes-pilote-et-propose.md) et
[runbook du workflow](../../docs/operations/workflow-acceleration.md). Le brief
le lot 029 (archivé sous `archive/2026-08`) reste
l'unique instruction du lot qui a créé cette veille.

`quotidien.sh` lance directement `veille.py` : aucun modèle ni agent n'est
appelé. Sur le chemin vert, stdout et stderr restent vides. Le rapport local
git-ignoré est actualisé sous
`hermes/propositions/DERNIERE-VEILLE.md`. Une alerte produit un code non nul et
une seule synthèse sur stderr, que le contrôleur peut relayer à Discord.

Mesure explicite sans tests produit :

```bash
.venv/bin/python hermes/crons/veille.py --repo . --metrics-only --json
```

La sortie inclut l'état git, les worktrees, l'espace disque, l'âge du tableau
de bord et deux contrôles du jeu. Le script ne nettoie rien.

La surveillance du cache de tuiles d'altitude a été retirée : elle ne pouvait
produire aucune alerte quel que soit l'état du cache, et faisait tomber la
veille quand `tools/map/sources.lock` manquait. `tools/map/` est hors du
chemin quotidien depuis ADR-0018.

## Installation VPS

Le planificateur Hermes doit enregistrer cette commande comme tâche
`no_agent=true` (ou équivalent « script seul »). L'entrée crontab équivalente
est :

```cron
15 6 * * * /home/ubuntu/src/ForgeHistory/hermes/crons/quotidien.sh
```

Le journal cron ne reçoit donc que les alertes. Les preuves lourdes et
ForgePilot ne sont jamais lancés par cette tâche quotidienne.
