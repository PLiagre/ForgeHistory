# ADR-0021: Claude reste manuel et sort de toute orchestration Hermes

**Date**: 2026-08-26
**Status**: accepted
**Deciders**: le propriétaire (décision explicite), Hermes (mise en œuvre)

## Contexte

ADR-0019 confie à Claude la rédaction des briefs, mais les instructions actives
demandaient aussi à Hermes de lancer Claude pour obtenir ou corriger ces briefs
et pour servir de témoin en cas de non-convergence. Le 26 août 2026, cette
orchestration a consommé une part importante du quota Claude du propriétaire
sans produire une seule modification du lot 035.

Le propriétaire veut continuer à utiliser Claude lui-même, avec accès au dépôt,
pour écrire des briefs ou demander des revues. Ce droit manuel ne doit plus être
confondu avec un droit d'invocation délégué à Hermes, ForgePilot, un cron, un
skill ou un sous-agent.

## Décision

**Claude reste un outil manuel du propriétaire. Hermes et tous les mécanismes
qu'il pilote ne lancent jamais Claude ni un service Anthropic.**

La frontière est la suivante :

| acteur | ce qu'il fait | ce qu'il ne fait pas |
|---|---|---|
| **Propriétaire** | décide les objectifs et les arbitrages ; peut lancer Claude manuellement dans le dépôt ; remet à Hermes les briefs ou revues qu'il souhaite intégrer ; fusionne | ne délègue implicitement à Hermes aucun appel Claude |
| **Claude manuel** | sur action directe du propriétaire, peut lire le dépôt, écrire ou amender un brief, tenir le modèle, ou produire une revue consultative | n'a aucun rôle automatique, aucun cron, aucun backend ForgePilot et aucun témoin lancé par Hermes |
| **Hermes** | pilote l'état, mesure, prépare les faits, lance les seuls workflows autorisés, rend compte et remet les blocages au propriétaire | ne lance jamais `claude`, un provider Anthropic, un skill d'orchestration Claude, ni un intermédiaire chargé de le faire ; n'écrit pas les briefs et ne juge pas les lots |
| **Cursor/Grok** | planifie et relit automatiquement en lecture seule selon la politique de risque | ne produit pas le code et ne fusionne pas |
| **Cursor/Composer** | exécute le brief dans le worktree borné et produit le candidat | ne prononce pas la recevabilité de son propre travail et ne fusionne pas |
| **ForgePilot** | orchestre uniquement les backends Cursor déclarés ; s'arrête honnêtement si le brief manque ou si le lot ne converge pas | n'expose aucun backend, commande, contrôle d'authentification ou repli Claude |

Un brief ou une revue créé manuellement par Claude est accepté comme **entrée
fournie par le propriétaire**. Hermes peut ensuite le lire, le versionner et le
faire passer dans les contrôles ordinaires, mais il ne démarre ni ne reprend la
session Claude qui l'a produit.

En cas de brief absent, de relecture de brief en échec ou de plateau après deux
itérations sans amélioration, Hermes rassemble les preuves et remet le dossier
au propriétaire. Le propriétaire peut alors utiliser Claude manuellement,
choisir un autre moyen ou abandonner le lot. Aucun choix n'est fait à sa place.

Les fichiers destinés à l'usage manuel de Claude (`CLAUDE.md`, `.claude/**` et
les skills Claude propres au dépôt) restent disponibles. Cette décision retire
les **invocations par Hermes** ; elle ne désinstalle pas Claude et ne restreint
pas l'usage direct du propriétaire.

## Application technique

- `workflow-policy.toml` fixe `[witness].backend = "none"`.
- Le chargeur de politique refuse `claude` pour tous les rôles.
- ForgePilot ne construit plus d'argv Claude et n'expose plus `forgepilot witness`.
- `forgepilot doctor --check-auth` vérifie Cursor et GitHub, pas Claude.
- Le plateau de non-convergence nomme le retour au propriétaire sans proposer
  une commande automatique.
- Les skills Hermes du profil courant ne doivent contenir aucun chemin actif
  d'invocation Claude.
- Les journaux et ADR historiques peuvent mentionner Claude ; ils ne sont pas
  des instructions exécutables.

## ADR amendés

Cette décision amende les parties opérationnelles de :

- ADR-0013 et ADR-0014, qui autorisaient Hermes à lancer Claude ;
- ADR-0017, qui exposait `forgepilot witness` ;
- ADR-0019, uniquement sur le mécanisme de demande : Claude peut toujours écrire
  les briefs, mais la session est lancée manuellement par le propriétaire et
  jamais par Hermes.

Le reste des contraintes de ces décisions demeure historique ou inchangé tant
qu'il ne contredit pas la présente frontière.

## Alternatives considérées

### Garder Claude automatique avec un plafond plus strict

- **Avantage** : Hermes pourrait continuer à débloquer seul les briefs.
- **Inconvénient** : un plafond réduit le dommage sans supprimer le risque de
  consommation non désirée ni l'ambiguïté d'autorité.
- **Rejet** : le propriétaire interdit explicitement l'invocation automatique.

### Désinstaller Claude ou supprimer `.claude/**`

- **Avantage** : barrière machine totale.
- **Inconvénient** : empêcherait aussi l'usage manuel voulu par le propriétaire.
- **Rejet** : la frontière porte sur l'orchestrateur, pas sur l'outil manuel.

### Remplacer silencieusement Claude par Hermes pour écrire les briefs

- **Avantage** : continuité nocturne apparente.
- **Inconvénient** : viole la séparation des rôles et fabrique une autorité non
  accordée.
- **Rejet** : en l'absence de brief, Hermes attend une entrée propriétaire.

## Conséquences

### Positives

- aucune consommation Claude ne peut être initiée par Hermes ou ForgePilot ;
- le propriétaire conserve son usage manuel complet ;
- les responsabilités et les coûts sont attribuables ;
- un blocage devient visible au lieu de déclencher un fournisseur coûteux.

### Négatives

- Hermes ne peut plus produire ou corriger un brief en autonomie complète ;
- une intervention manuelle du propriétaire est nécessaire lorsque la file ne
  contient plus de brief recevable ou qu'un brief doit être réécrit.

### Risques et atténuations

- **Dérive documentaire** : les anciens ADR mentionnent encore les anciens
  rôles. Atténuation : ADR-0021 est l'amendement le plus récent et les documents
  actifs pointent vers lui.
- **Invocation indirecte par un skill générique** : atténuation par neutralisation
  des instructions actives dans les skills Hermes et contrôle de recherche.
- **Confusion entre revue consultative et gate automatique** : une revue Claude
  manuelle est une entrée du propriétaire ; le verdict automatique reste celui
  du relecteur Cursor désigné par la politique.
