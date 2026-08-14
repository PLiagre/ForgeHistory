# ADR-0013: pilote Hermes léger, Grok en lecture et Cursor exécutant

**Date**: 2026-08-14
**Status**: accepted
**Deciders**: propriétaire du projet

## Contexte

La chaîne Hermes → Claude → Codex → Claude → Cursor → challenge a accumulé des
files d'attente, des écritures de tableau de bord et des états difficiles à
réconcilier. L'observateur Hermes sur le PC Windows consomme des ressources en
continu. Le propriétaire veut tester un système neuf où Hermes pilote, Cursor
exécute et la puissance de raisonnement est distante via l'offre SuperGrok.

La vérification des interfaces donne une limite structurante : Hermes expose un
serveur ACP, mais ne possède pas encore de client ACP générique pour piloter un
agent externe. Grok Build et Cursor fournissent tous deux un mode CLI headless.

Références vérifiées le 2026-08-14 :

- Hermes, commandes CLI et serveur ACP :
  <https://hermes-agent.nousresearch.com/docs/reference/cli-commands> ;
- absence actuelle de client ACP externe générique :
  <https://github.com/NousResearch/hermes-agent/issues/36057> ;
- Grok Build headless, JSON et ACP :
  <https://docs.x.ai/build/cli/headless-scripting> ;
- sandbox et désactivation de l'écriture Grok :
  <https://docs.x.ai/build/settings/reference> ;
- Cursor CLI headless avec écriture explicite `--force` :
  <https://cursor.com/docs/cli/headless>.

## Décision

Créer `control-plane/` comme projet Python autonome, sans dépendance runtime.
Hermes en est la console légère. Il lance Grok Build en subprocessus pour le
plan et la revue, avec sandbox et outils d'écriture désactivés. Il lance Cursor
CLI comme unique exécutant dans un worktree `agent/*`. La CI juge les faits et
le propriétaire reste seul décideur de la fusion.

Pendant trois lots pilotes : aucun cron, aucun auto-merge et une seule tâche
active. L'ancien pipeline passe en mode `manual`; ses archives restent lisibles
pour permettre un retour arrière.

## Solutions envisagées

### Grok comme backend API direct d'Hermes

- **Avantage** : intégration fournisseur native.
- **Inconvénient** : l'API xAI est facturée séparément de SuperGrok.
- **Rejet** : ne teste pas l'offre d'abonnement que le propriétaire souhaite
  évaluer.

### Hermes client ACP de Grok

- **Avantage** : protocole structuré et sessions riches.
- **Inconvénient** : Hermes ne fournit pas encore ce mode client générique.
- **Rejet** : dépendrait d'une fonctionnalité non livrée.

### Conserver le full-auto à quatre acteurs

- **Avantage** : nombreuses preuves historiques déjà produites.
- **Inconvénient** : coût opérationnel et complexité disproportionnés.
- **Rejet** : ne répond pas à la décision de simplification.

## Conséquences

### Positives

- aucune inférence locale lourde sur le PC du propriétaire ;
- deux agents seulement et une séparation visible entre produire et juger ;
- commandes prévisibles, sans `shell=True`, sorties enregistrées hors Git ;
- migration réversible tant que les anciens workflows restent archivés.

### Négatives

- Hermes a encore besoin d'un petit fournisseur distant pour sa propre
  conversation ; SuperGrok via CLI est un délégué, pas son backend ACP ;
- le premier login Grok et Cursor doit être effectué sur le serveur persistant ;
- le pilote ne déploie pas lui-même le VPS et ne fusionne aucune PR.

### Risques

- **Cursor headless écrit largement** : worktree isolé, sandbox activée, aucun
  secret de production et revue du diff avant merge.
- **Grok modifie le dépôt** : `GROK_WRITE_FILE=0`, sandbox `read-only`, nouvelle
  invocation pour la revue.
- **abonnement confondu avec API** : `doctor` et le runbook exigent un login
  Grok de compte ; une demande de `XAI_API_KEY` bloque le pilote.
- **retour de complexité** : aucune nouvelle étape ou cadence avant le bilan
  écrit des trois lots.
