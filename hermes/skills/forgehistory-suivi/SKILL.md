---
name: forgehistory-suivi
description: >
  Piloter ForgeHistory avec ForgePilot. Utiliser quand le propriétaire demande
  l'état du projet, veut préparer une tâche avec Claude Code, la faire exécuter
  par Cursor, relire le diff ou décider d'une fusion.
---

# Pilotage ForgeHistory

Agir comme console légère. Ne jamais écrire de code. Utiliser le projet
`control-plane/` du clone ForgeHistory ; conserver les sessions lourdes sur le
serveur pilote, pas sur le PC du propriétaire.

## Sources autoritaires

Lire d'abord `ROADMAP.md`, puis la tâche ou l'issue explicitement nommée. Lire
`control-plane/README.md` pour le transport et ADR-0013 pour les frontières.
Dire qu'une donnée est absente au lieu de la déduire.

## Séquence obligatoire

1. Exécuter `forgepilot doctor --repo <clone> --check-auth`.
2. Vérifier qu'une seule tâche est active et qu'elle possède des critères
   mesurables. Sinon, arrêter et demander le choix au propriétaire.
3. Prévisualiser `forgepilot plan <task.md> --repo <clone>`.
4. Sur ordre explicite, relancer avec `--run`. Montrer le plan produit avant
   toute exécution.
5. Prévisualiser `forgepilot execute <plan.json> --task-name <id> --repo <clone>`.
6. Sur ordre explicite, relancer avec `--run`. Cursor est le seul producteur et
   travaille dans le worktree `agent/<id>` créé par ForgePilot.
7. Attendre les tests mécaniques. Ne jamais transformer une absence de test en
   succès. Publier seulement une draft PR avec
   `forgepilot publish --repo <worktree> --title <titre> --run`.
8. Lancer `forgepilot review <plan.json> --repo <worktree> --base <base> --run`.
   Cette commande ouvre une nouvelle invocation Claude Code en lecture seule.
9. Présenter le verdict, les contrôles et le diff au propriétaire. Ne jamais
   fusionner automatiquement.

## Transport et sécurité

- Appeler Claude Code en CLI headless. Ne pas essayer de le connecter à Hermes
  comme provider ou client ACP : Claude Pro fonctionne via `claude -p`, pas via
  l'OAuth Anthropic natif de Hermes.
- Refuser `ANTHROPIC_API_KEY` ; Claude Code doit être connecté au compte
  Claude.ai Pro et non à la Console facturée à l'API.
- Ne pas employer de cron pendant les trois lots pilotes.
- Pendant ces trois lots, exécuter sur la partition Linux du propriétaire. Ne
  pas provisionner Render ni louer un VPS avant le bilan écrit. Si le VPS est
  ensuite retenu, traiter le PC comme worker SSH facultatif et bloquer
  explicitement toute tâche qui l'exige lorsqu'il est hors ligne.
- Ne jamais transmettre de secret au prompt, au résultat ou au worktree Cursor.
- Ne jamais réactiver `mode: full_auto` sans nouvelle décision propriétaire.

## Critères du bilan après trois lots

Rapporter : qualité des plans, nombre de retouches humaines, durée, limites
d'usage, erreurs d'authentification et incidents de sécurité. Proposer ensuite
de conserver, ajuster ou retirer le pilote ; ne pas ajouter spontanément de
nouvel acteur.
