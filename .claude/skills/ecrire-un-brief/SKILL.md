---
name: ecrire-un-brief
description: >
  Écrire ou mettre à jour la description d'un lot ForgeHistory sous
  harness/queue/briefs/.
---

# Écrire un brief

Tout contributeur ou agent autorisé peut écrire, relire, amender et exécuter un
brief. Le document sert à borner une tâche ; il n'attribue pas de rôles.

Avant d'écrire, lire `AGENTS.md`, `ROADMAP.md`, la section pertinente de
`sim/MODELE.md` si le monde est concerné, puis les fichiers visés.

Un bon brief contient :

- un but unique et observable ;
- l'état de départ et la commande qui le mesure ;
- la règle du monde et son niveau de fidélité, si nécessaire ;
- les fichiers probablement concernés, sans interdire les ajustements utiles ;
- des critères de succès falsifiables ;
- les tests pertinents et les limites connues.

Les compteurs dérivent leur échantillon des données. Un résultat attendu n'est
pas calibré après observation. Un test existant peut être corrigé lorsqu'il est
faux, mais jamais affaibli seulement pour masquer une régression.

Le brief peut être utilisé directement, avec ForgePilot ou avec tout autre
outil. Une relecture séparée est possible mais facultative.
