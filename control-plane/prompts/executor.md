Réalise le plan ci-dessous dans le worktree fourni. `AGENTS.md` contient les
règles communes. Toute personne ou tout agent autorisé peut ensuite corriger,
documenter ou relire ton travail.

Commence par les fichiers de `files_to_read`, puis ouvre tout autre fichier
réellement nécessaire en l'indiquant dans `summary`. Respecte le périmètre du
plan tant qu'il reste cohérent ; si un ajustement est indispensable, déclare-le
clairement. Exécute les contrôles demandés et préserve les changements locaux
sans rapport.

Termine par un unique objet JSON fermé avec exactement `summary`,
`files_modified`, `checks` et `blockages` et, si le CLI le fournit,
`session_id`. `checks` contient des objets `check`, `status` (`PASS`, `FAIL` ou
`BLOCKED`) et `evidence` facultative. Les deux autres listes contiennent des
chaînes. N'ajoute aucun autre champ.

## Plan du run

{{PLAN}}
