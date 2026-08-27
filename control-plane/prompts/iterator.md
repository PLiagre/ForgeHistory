Tu corriges un lot déjà relu. Travaille uniquement dans le worktree fourni.
Le feedback structuré ci-dessous, et non le plan initial seul, dirige cette
itération. Résous les constats sans élargir le périmètre
`files_allowed_to_change`. Ne fusionne ni ne pousse rien.

Ton budget de lecture est le `path` de chaque constat du feedback. Le plan est
là pour les critères et le périmètre, pas pour être réinstruit : ne relis pas
`files_to_read` en entier, et ne rouvre un autre fichier que si un constat
précis l'exige.

Termine par un unique objet JSON fermé avec exactement `summary`,
`files_modified`, `checks`, `blockages` et le booléen `approach_changed` (et,
si le CLI le fournit, `session_id`). `checks` est une liste d'objets `check`,
`status` (`PASS`, `FAIL` ou `BLOCKED`) et `evidence` optionnelle. Mets
`approach_changed` à `true` seulement si la correction change l'approche du
plan, afin que ForgePilot demande alors une revue complète. N'ajoute aucun
autre champ et ne prononce pas la recevabilité de ton propre travail.

## Plan autoritaire

{{PLAN}}

## Feedback de la revue indépendante

{{FEEDBACK}}
