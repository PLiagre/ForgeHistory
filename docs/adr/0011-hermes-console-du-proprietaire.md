# ADR-0011: Hermes console du propriétaire — un droit d'agir borné, sur ordre explicite

**Date**: 2026-08-12
**Status**: accepted
**Deciders**: propriétaire du projet (« ok pour tout », session Cursor Cloud
du 2026-08-12, en réponse aux cinq arbitrages de
`hermes/requests/DEMANDE-20260812-hermes-tableau-de-bord-pilotage.md`) ;
rédaction déléguée à Cursor.

## Context

ADR-0010 fait d'Hermes le chef de projet avec un droit d'écriture borné
(`ROADMAP.md` + `hermes/**`) mais **aucun droit d'exécution** : « aucun
workflow n'exécute ce que Hermes écrit ». Or le propriétaire veut piloter
le projet **depuis** son Hermes local (l'installation hermes-agent qui sert
le tableau `http://127.0.0.1:9119` et reçoit déjà les événements GitHub via
`hermes-observer.yml`). Aujourd'hui, chaque action de pilotage — fusionner
une PR, mettre la boucle en pause, lancer un brief — exige qu'il quitte
Hermes pour aller cliquer dans GitHub. Le « clic final humain » de la
fusion (décision du 2026-08-11, porte conditionnelle) reste requis mais
n'est exécutable que dans l'interface GitHub.

## Decision

Hermes devient la **console du propriétaire** : il peut **exécuter des
actions qui appartiennent au propriétaire**, uniquement sur ordre explicite
de celui-ci dans une conversation, jamais de sa propre initiative. Le
périmètre est fermé — quatre actions, rien d'autre :

1. **fusionner ou refuser une pull request** — c'est le « clic final
   humain » existant, délégué à l'outil qui reçoit l'ordre ; les conditions
   de fusion elles-mêmes (CI verte, gate ACCEPT, verdict d'un acteur
   différent du producteur, audit Cursor) ne sont ni levées ni affaiblies ;
2. **poser ou retirer le label `pipeline/pause`** — l'arrêt d'urgence
   documenté de `docs/rules/full-auto-pipeline.md` ;
3. **déclencher `pipeline-forge-run`** (`workflow_dispatch`) sur un brief
   existant de `harness/queue/briefs/` ;
4. **déposer une demande** dans `hermes/requests/` — déjà dans son contrat
   d'écriture (ADR-0010), rappelé ici pour que la liste soit complète.

Garde-fous, tous obligatoires :

- **ordre explicite + confirmation** : Hermes reformule l'action et son
  effet, et n'exécute qu'après un « oui » du propriétaire dans la même
  conversation ; jamais d'action déclenchée par un cron, un événement reçu
  ou une inférence ;
- **jeton dédié à permissions minimales** : un fine-grained PAT GitHub
  limité à ce dépôt et aux permissions strictement nécessaires (contents,
  pull-requests, actions), distinct de tout autre identifiant ;
- **trace écrite** : chaque action exécutée est consignée dans un rapport
  `hermes/reports/` (quoi, quand, sur ordre de qui) ;
- **les interdits d'ADR-0010 demeurent** : Hermes n'écrit jamais de code,
  de CI, de brief, de rubrique, de verdict, d'audit — exécuter une action
  du propriétaire n'est pas produire du contenu de la boucle ;
- **surface réseau inchangée** : le tableau 9119 reste lié à `127.0.0.1` ;
  aucune exposition réseau sans couche d'authentification.

Deux décisions annexes, tranchées dans le même « ok pour tout » :

- **Modèle d'Hermes** : statu quo sur son fournisseur actuel ; pour les
  analyses lourdes, Hermes **délègue au Codex CLI local** connecté au
  compte ChatGPT du propriétaire — l'abonnement est exploité sans clé API,
  en cohérence avec la décision du 2026-08-12 (« quota d'abonnement,
  jamais de crédit API »).
- **Phase « shadow »** de l'Hermes local : la sortie reste prévue au
  2026-08-24 ; elle peut être avancée une fois le branchement en lecture
  et la présente console en place.

Le câblage concret (skill locale, outils, jeton) est de la configuration
de l'installation locale — hors dépôt. Si un jour une partie de ce câblage
doit entrer dans le dépôt, elle passera par un brief, comme tout code.

## Alternatives Considered

### Alternative 1 : donner à Hermes un droit d'écriture et d'exécution large
- **Pros** : « tout pilotable » au sens le plus littéral.
- **Cons** : cumulerait pilotage et production — exactement ce que le
  harnais existe à empêcher (raison des bornes d'ADR-0010).
- **Why not** : le besoin réel est de déplacer les **clics du
  propriétaire** dans Hermes, pas de créer un cinquième producteur.

### Alternative 2 : statu quo (Hermes lit, le propriétaire clique dans GitHub)
- **Pros** : aucun changement de contrat, aucune surface nouvelle.
- **Cons** : le propriétaire doit tenir deux interfaces ; l'objectif « un
  seul tableau à suivre » n'est pas atteint.
- **Why not** : c'est précisément le problème constaté dans la demande.

### Alternative 3 : élargir le merge-bot au lieu de déléguer le clic
- **Pros** : plus d'automatisation, zéro action humaine.
- **Cons** : la denylist du merge-bot est la seule barrière réelle
  (protection de branche indisponible sur ce plan GitHub, `HTTP 403`
  vérifié) ; l'élargir affaiblirait la porte conditionnelle du 2026-08-11.
- **Why not** : hors du périmètre de cette demande ; toute évolution de la
  porte de fusion exige sa propre décision écrite.

## Consequences

### Positive
- Le propriétaire pilote depuis un seul endroit : lire l'état (tableau de
  bord) et agir (quatre actions) dans la même interface.
- Le « clic final humain » reste humain : c'est toujours un ordre du
  propriétaire, seule la main qui l'exécute change.
- Chaque action laisse une trace versionnée dans `hermes/reports/`.

### Negative
- Une surface d'action nouvelle sur le dépôt (un jeton de plus à garder,
  à faire tourner, à révoquer en cas de doute).
- La frontière « ordre explicite » repose sur la discipline de
  l'installation locale, que le dépôt ne peut pas vérifier mécaniquement.

### Risks
- **Hermes agit sans ordre** (bug, prompt-injection via un événement
  reçu) → la confirmation conversationnelle est obligatoire avant chaque
  action ; le jeton minimal limite le rayon d'action ; le label
  `pipeline/pause` et la révocation du jeton restent les coupe-circuits.
- **La délégation du clic banalise la fusion** → la porte conditionnelle
  (quatre preuves) est inchangée ; Hermes doit refuser d'exécuter une
  fusion si une preuve manque et le dire au propriétaire.
- **Dérive du périmètre** (« encore une petite action ») → toute action
  au-delà des quatre listées exige un nouvel ADR.
