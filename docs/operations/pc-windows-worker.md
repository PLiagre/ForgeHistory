# Worker PC Windows — outil facultatif

Le workflow [`.github/workflows/worker-pc.yml`](../../.github/workflows/worker-pc.yml)
permet de vérifier manuellement qu'un runner Windows auto-hébergé répond. Il
ne distribue aucun rôle et n'est pas requis pour développer ou livrer le jeu.

## Capacités

Le runner s'annonce avec les labels réellement disponibles, par exemple
`windows` et `high-memory`. Les labels `unity` ou `local-llm` ne sont ajoutés
que si une tâche correspondante existe réellement.

Constat local facultatif :

```bash
.venv/bin/forgepilot workers --repo .
.venv/bin/forgepilot workers --repo . --json
.venv/bin/forgepilot workers --repo . --require windows
```

Déclenchement manuel :

```bash
gh workflow run worker-pc.yml -f tache=ping
gh run list --workflow=worker-pc.yml --limit 1
```

Un runner absent produit un diagnostic d'absence ; il ne change pas l'état du
produit. Un ping réussi prouve seulement que la machine a exécuté le workflow.

## Sécurité

- conserver `workflow_dispatch` comme seul déclencheur du workflow ;
- ne jamais exécuter automatiquement le code d'un fork sur le PC ;
- utiliser un compte Windows sans droits administrateur lorsque possible ;
- garder les jetons d'enregistrement et autres secrets hors du dépôt ;
- borner la durée et la concurrence des tâches ;
- valider le contenu de l'artefact `ping.json` avant de l'utiliser.

## Cas de diagnostic

| cas | observation attendue |
|---|---|
| worker disponible | la commande de présence et le ping réussissent |
| worker absent | absence signalée ; `python3 -m sim --ticks 0` continue |
| disparition pendant le ping | le job échoue ou expire |
| résultat incomplet | `validate_ping` refuse l'artefact |
| double lancement | la règle de concurrence sérialise les jobs |

Les anciennes descriptions du PC comme acteur ou comme élément d'une chaîne
de fusion sont historiques et obsolètes.
