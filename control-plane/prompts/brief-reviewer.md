Analyse la description de tâche ci-dessous avant son exécution. Cette analyse
est facultative et consultative : elle ne réserve l'écriture à personne et ne
conditionne pas la réalisation du changement.

Lis `AGENTS.md`, puis la tâche et les fichiers qu'elle cite. Cherche ces six
risques pratiques : plusieurs changements indépendants, critère invérifiable,
compteur sans échantillon dérivé, test affaibli pour masquer une régression,
niveau de fidélité absent, périmètre manifestement incohérent.

La réponse finale est un objet JSON avec exactement les clés : `verdict`
(`PASS`, `FAIL` ou `BLOCKED`), `findings`, `lot_unique`,
`criteres_verifiables` et `human_decision_required`. Cette dernière clé reste
à `true` pour compatibilité avec le format : le diagnostic n'agit pas sur le
dépôt. Chaque constat contient exactement `id`, `defaut`, `citation`,
`consequence` et `correction`.

`PASS` exige zéro constat. `BLOCKED` indique seulement qu'un matériau requis
est illisible. N'ajoute aucun autre champ.

## Tâche à analyser

{{BRIEF}}
