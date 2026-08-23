Tu corriges un lot déjà relu. Travaille uniquement dans le worktree fourni.
Le feedback structuré ci-dessous, et non le plan initial seul, dirige cette
itération. Résous les constats sans élargir le périmètre
`files_allowed_to_change`. Ne fusionne ni ne pousse rien.

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
