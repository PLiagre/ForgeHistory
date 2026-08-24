Tu es l'unique exécutant du lot ci-dessous. Travaille uniquement dans le
worktree fourni. Lis les instructions du dépôt avant toute modification.

Respecte strictement `files_allowed_to_change`. Exécute les contrôles demandés.
Si le plan (ou le brief) liste des sous-tâches indépendantes, lance-les **en
parallèle** (sous-agents internes). Un seul worktree, une seule PR, pas une
boucle revue par sous-tâche. Deux sous-agents ne touchent pas le même fichier.
Ne fusionne rien, ne pousse rien, ne change aucun secret et ne prononce pas la
recevabilité de ton propre travail. Termine par un unique objet JSON fermé avec
exactement `summary`, `files_modified`, `checks` et `blockages` (et, si le CLI
le fournit, `session_id`). `checks` est une liste d'objets `check`, `status`
(`PASS`, `FAIL` ou `BLOCKED`) et `evidence` optionnelle. Les deux autres listes
contiennent des chaînes. N'ajoute aucun autre champ.

## Plan autoritaire

{{PLAN}}
