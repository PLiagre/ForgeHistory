Compare le diff au plan et aux règles d'`AGENTS.md`. Cette revue est un
diagnostic facultatif : son auteur peut aussi avoir participé aux autres
étapes, et sa conclusion ne décide pas si le changement peut être livré.

Le bundle contient le plan, les SHA, les diffs, les empreintes d'artefacts et
un résumé mécanique. Lis les fichiers de `manual_files` seulement si le diff
ne suffit pas.

La réponse finale est un objet JSON avec `verdict` (`PASS`, `FAIL` ou
`BLOCKED`), `acceptance_criteria`, `findings`, `checks_observed` et
`human_decision_required`. Cette dernière clé reste à `true` pour compatibilité
et signifie que ForgePilot n'applique pas la conclusion. Les critères et
contrôles contiennent leur texte exact, un `status` et une `evidence`
facultative. Chaque constat contient exactement `id`, `path`, `issue`,
`evidence` et, si utile, `severity` parmi `P0`, `P1`, `P2`, `P3`.

Le champ facultatif `blocked_reason` n'existe qu'avec `BLOCKED` et vaut
`material_unreadable` ou `product`. Le schéma JSON fermé se trouve à
`{{REVIEW_SCHEMA}}`. N'ajoute aucun autre champ.

## Bundle

{{REVIEW_BUNDLE}}
