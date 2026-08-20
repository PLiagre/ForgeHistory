# ADR-0013: pilote Hermes léger, Claude Code en lecture et Cursor exécutant

**Date**: 2026-08-14
**Status**: accepted
**Deciders**: propriétaire du projet

## Contexte

La chaîne Hermes → Claude → Codex → Claude → Cursor → challenge a accumulé des
files d'attente, des écritures de tableau de bord et des états difficiles à
réconcilier. L'observateur Hermes sur le PC Windows consomme des ressources en
continu. Le propriétaire veut tester un système neuf où Hermes pilote, Cursor
exécute et Claude Code planifie et relit via son abonnement Pro actif jusqu'en
avril 2027.

La vérification des interfaces donne une limite structurante : Hermes expose un
serveur ACP, mais ne possède pas encore de client ACP générique pour piloter un
agent externe. Claude Code et Cursor fournissent tous deux un mode CLI headless.
Hermes ne peut pas utiliser directement l'OAuth Anthropic comme provider avec
un abonnement Pro : ce mode exige Max et des crédits d'usage supplémentaires.
L'abonnement Pro reste en revanche accepté par le CLI officiel Claude Code.

Références vérifiées le 2026-08-14 :

- Hermes, commandes CLI et serveur ACP :
  <https://hermes-agent.nousresearch.com/docs/reference/cli-commands> ;
- absence actuelle de client ACP externe générique :
  <https://github.com/NousResearch/hermes-agent/issues/36057> ;
- orchestration Claude Code depuis Hermes :
  <https://github.com/NousResearch/hermes-agent/blob/main/skills/autonomous-ai-agents/claude-code/SKILL.md> ;
- Claude Code headless, outils autorisés et mode `plan` :
  <https://code.claude.com/docs/en/headless> et
  <https://code.claude.com/docs/en/cli-reference> ;
- limite Claude Pro pour le provider Anthropic natif de Hermes :
  <https://hermes-agent.nousresearch.com/docs/reference/environment-variables> ;
- Cursor CLI headless avec écriture explicite `--force`, worktrees locaux et
  transfert Cloud distinct : <https://cursor.com/docs/cli/using> ;
- Unity en batchmode et Unity Test Framework :
  <https://docs.unity3d.com/6000.5/Documentation/Manual/EditorCommandLineArguments.html>
  et
  <https://docs.unity3d.com/6000.5/Documentation/Manual/test-framework/reference-command-line.html> ;
- runner GitHub auto-hébergé et risque des dépôts publics :
  <https://docs.github.com/actions/hosting-your-own-runners/adding-self-hosted-runners>.

## Décision

Créer `control-plane/` comme projet Python autonome, sans dépendance runtime.
Hermes en est la console légère facultative. Il lance Claude Code en
subprocessus pour le plan et la revue, avec `--permission-mode plan` et une
liste fermée d'outils de lecture. Il lance Cursor CLI comme unique exécutant
dans un worktree `agent/*`. La CI portable juge ForgeHistory. Tout lot qui
touche VictoriaCityLab exige en plus une validation du commit exact par Unity
6000.0.43f1 sur un worker Windows. Le propriétaire reste seul décideur de la
fusion.

Pendant trois lots pilotes : aucun cron, aucun auto-merge et une seule tâche
active. L'ancien pipeline passe en mode `manual`; ses archives restent lisibles
pour permettre un retour arrière.

Unity est installé nativement sous Windows ; le double démarrage vers la
partition Linux rendrait donc le worker Unity indisponible. Les trois lots
commencent sans VPS sur Windows, avec WSL2 facultatif pour les outils Linux.
Aucun lot CityLab n'est autorisé avant l'ajout d'un worker Windows sécurisé.

Si Hermes est conservé, la cible est un VPS Linux 4 Go/2 vCPU/40 Go avec 2 Go
de swap. Le VPS garde Hermes, ForgePilot et les tâches ordinaires ; le PC
Windows garde Unity, Git LFS et les tests lourds. Lorsqu'il est éteint, le
contrôle Unity reste en attente et la fusion est bloquée. Render est écarté
pour Hermes. Les runbooks vivent dans `docs/operations/forgepilot-hosting.md`
et `docs/operations/unity-windows-worker.md`.

## Solutions envisagées

### Claude comme provider principal d'Hermes

- **Avantage** : Claude raisonnerait directement dans la boucle Hermes.
- **Inconvénient** : l'OAuth natif de Hermes ne fonctionne pas avec Claude Pro ;
  une clé API consommerait les crédits séparément.
- **Rejet** : ne profiterait pas de l'allocation Claude Code déjà payée.

### Hermes comme client ACP de Claude Code

- **Avantage** : protocole structuré et sessions riches.
- **Inconvénient** : Hermes ne fournit pas encore ce mode client générique et
  Claude Code fournit déjà un CLI headless supporté.
- **Rejet** : dépendrait d'une fonctionnalité non livrée.

### Conserver le full-auto à quatre acteurs

- **Avantage** : nombreuses preuves historiques déjà produites.
- **Inconvénient** : coût opérationnel et complexité disproportionnés.
- **Rejet** : ne répond pas à la décision de simplification.

### Déployer immédiatement sur Render

- **Avantage** : Background Worker managé et disponible en continu.
- **Inconvénient** : abonnement workspace, compute et disque sont séparés ; le
  palier 4 Go est disproportionné pour ce pilote.
- **Rejet** : commencer localement ne coûte rien et un VPS offre ensuite un
  meilleur rapport coût/contrôle.

## Conséquences

### Positives

- aucune inférence locale lourde sur le PC du propriétaire ;
- deux agents de travail seulement et une séparation visible entre produire et
  juger ;
- commandes prévisibles, sans `shell=True`, sorties enregistrées hors Git ;
- migration réversible tant que les anciens workflows restent archivés.

### Négatives

- Hermes a encore besoin d'un petit fournisseur compatible pour sa propre
  conversation ; Claude Code via CLI est un délégué, pas son provider ;
- le premier login Claude Code et Cursor doit être effectué sur le serveur
  d'exécution retenu ;
- le pilote ne déploie pas lui-même le VPS et ne fusionne aucune PR ;
- l'installation et l'activation initiales de Unity restent une opération
  humaine sur Windows ; les contrôles visuels ne sont pas automatisés.

### Risques

- **Cursor headless écrit largement** : worktree isolé, sandbox activée, aucun
  secret de production et revue du diff avant merge.
- **Claude Code modifie le dépôt** : mode `plan`, outils limités à
  `Read,Glob,Grep`, MCP et commandes personnalisées désactivés, nouvelle
  invocation pour la revue.
- **abonnement confondu avec API** : `doctor` et le runbook exigent un login
  Claude.ai ; la présence de `ANTHROPIC_API_KEY` bloque le pilote.
- **runner personnel sur dépôt public** : aucun déclenchement automatique sur
  `pull_request` ou code de fork ; validation manuelle d'une branche du
  propriétaire pendant le pilote.
- **Unity indisponible** : état bloqué explicite, jamais succès supposé ; les
  tâches ForgeHistory sans Unity peuvent continuer sur le VPS.
- **retour de complexité** : aucune nouvelle étape ou cadence avant le bilan
  écrit des trois lots.

---

## Amendement 001 — le VPS a précédé son bilan

**Amended**: 2026-08-19
**Author**: Claude Code (rôle CTO), sur constat

Cet ADR posait une condition : le VPS n'est loué qu'**après** un bilan écrit des
trois premiers lots réels. Le `2026-08-19`, le propriétaire a installé Hermes sur
un VPS Linux dédié. Les trois lots existent bien (`021`, `022`, `023`, le dernier
fusionné le `2026-08-16`), mais **le bilan n'est pas écrit**. L'ordre prescrit
n'a donc pas été tenu.

C'est consigné ici plutôt que passé sous silence, pour trois raisons :

1. **La condition n'était pas décorative.** Elle existait pour qu'une décision
   d'hébergement s'appuie sur des mesures — qualité des plans, retouches
   humaines, durée, coût, plafonds atteints — et non sur une impression de
   confort. Ces mesures ne sont toujours pas rassemblées.
2. **Le bilan reste dû, et il n'est pas rétroactif.** Il doit pouvoir conclure
   *retirer le pilote*. Un bilan écrit après coup pour ratifier une décision
   déjà prise n'en est pas un ; celui-ci doit être écrit comme si l'hébergement
   restait ouvert, parce que sur le fond il l'est.
3. **Ce n'est pas une dérogation générale.** La règle « mesurer avant d'étendre »
   d'ADR-0013 reste valable pour toute étape suivante, à commencer par les crons
   et l'auto-fusion. Cette exception ne la périme pas.

Aucune autre clause d'ADR-0013 n'est amendée. En particulier, « pendant trois
lots pilotes : aucun cron, aucun auto-merge et une seule tâche » reste en
vigueur jusqu'à ce que le bilan prononce la clôture du pilote — voir ADR-0015,
règle `2`.

---

## Amendement 002 — une commande enchaîne le lot, sans fusionner

**Amended**: 2026-08-20
**Author**: Cursor (exécutant), sur ordre du propriétaire

Le propriétaire veut lancer Hermes et qu'un lot parte sans six `--run`
successifs. `forgepilot enchaine <brief>` enchaîne plan → execute → draft PR
→ review. Sans `--run`, aperçu seulement. Avec `--run`, les agents tournent.

Inchangé : une proposition n'est pas un brief ; Claude planifie et relit en
lecture seule ; Cursor exécute dans `agent/*` ; **aucune fusion** ; une
seule tâche de code à la fois ; `ANTHROPIC_API_KEY` reste un refus.

---

## Amendement 003 — Hermes lance Claude pour écrire le brief

**Amended**: 2026-08-20
**Author**: Cursor (exécutant), sur ordre du propriétaire

Si un brief manque, Hermes ne le rédige toujours pas. Il lance
`forgepilot lot <proposition.md>` : Claude Code (lecture seule) produit le
texte ; ForgePilot l’écrit sous `harness/queue/briefs/` dans le worktree ;
puis la chaîne execute / draft PR / review. Le propriétaire fusionne.

`hermes/prompts/ENCHAINER.md` est le message d’ordre à coller dans Hermes.
