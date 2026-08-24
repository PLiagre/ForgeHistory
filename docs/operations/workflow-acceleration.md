# Exploiter le workflow durable sur le VPS

Ce document est un runbook, pas une seconde politique. Les décisions vivent
dans [la politique versionnée](../../control-plane/workflow-policy.toml),
[ADR-0018](../adr/0018-hermes-sol-briefs-cursor-parallele.md) pour le
chemin quotidien, et [la règle du harnais](../rules/harness-roles.md)
pour l'archive optionnelle. Un brief sous `harness/queue/briefs/` reste
l'unique instruction d'un lot nommé.

Le chemin par défaut n'est plus ForgePilot : un agent Cursor Cloud
découpe et exécute. Les commandes ci-dessous servent quand on veut
quand même une reprise durable.

## Démarrer et reprendre un lot

Depuis un dépôt propre et synchronisé :

```bash
.venv/bin/forgepilot doctor --repo . --check-auth
.venv/bin/forgepilot start harness/queue/briefs/NNN-slug/brief.md --repo . --run
.venv/bin/forgepilot status latest --repo .
```

Une interruption se reprend sans recréer la branche ni le worktree :

```bash
.venv/bin/forgepilot resume latest --repo .
```

L'état local est sous `.forgepilot/runs/<RUN_ID>/state.json`. Les autres
fichiers de ce même dossier sont des matériaux de travail liés au run ; ils ne
sont ni une autorité versionnée ni une permission de fusion.

Hermes relaie à Discord uniquement les changements d'étape retournés par
`status` : identifiant du run, étape, SHA, résultat mécanique ou blocage. Il ne
transforme jamais une sortie Cursor en verdict. La revue quotidienne est Grok 4.6 `xhigh` (contexte neuf). Claude
Opus 5 n'est qu'un témoin rare (`forgepilot witness`). Si le juge
rend `PASS` et que les checks sont verts sur ce SHA,
`forgepilot merge --run` fusionne ; un label `do-not-merge` bloque.

## Vérifier le risque et préparer les tests

Une PR gérée par ce workflow porte exactement une ligne dans son corps :

```text
Forge-Risk: R1
```

Le niveau reste celui demandé ou est augmenté par la classification
autoritaire. Pour reconstruire la porte localement :

```bash
git diff --name-only <BASE_SHA> <HEAD_SHA> > /tmp/forgehistory-paths.txt
.venv/bin/python harness/workflow_risk_gate.py \
  --repo . --declared-risk R1 --paths-from /tmp/forgehistory-paths.txt
```

Le routeur construit un plan JSON sans lancer de suite :

```bash
.venv/bin/python harness/workflow_test_router.py plan \
  --repo . --risk R1 --paths-from /tmp/forgehistory-paths.txt
```

`run` exécute ensuite les commandes en série et rend un résumé avec code,
durée et preuve ciblée. Une certification lourde exige le SHA final et le
drapeau explicite `--allow-heavy`; son absence ne peut donc pas déclencher la
preuve Europe par accident.

## Exploitation sur 8 Gio / 100 Gio

- Ne lancer qu'une preuve lourde à la fois. Le routeur verrouille par défaut le
  Git common dir, partagé par tous les worktrees. Si plusieurs clones vivent
  sur le VPS, définir `FORGEPILOT_HEAVY_LOCK` vers un même chemin absolu hors
  des clones avant `run --allow-heavy`.
- Monter le cache partagé via `FORGEHISTORY_DEM_CACHE_ROOT`. Le code du cache
  choisit ensuite le sous-répertoire lié à `sources.lock`.
- Exécuter Cursor et les preuves sous un utilisateur sans jeton GitHub ou
  Discord. Le contrôleur Hermes conserve seul les identifiants nécessaires à
  la notification et à la publication.
- Ne pas installer de runner GitHub auto-hébergé générique sur ce dépôt
  public. Les checks portables restent sur les runners GitHub hébergés.
- Ne jamais activer `full_auto`. La configuration historique reste en mode
  manuel et la fusion reste une action du propriétaire.

## Veille silencieuse

La procédure quotidienne et son installation vivent dans
[`hermes/crons/README.md`](../../hermes/crons/README.md). Elle mesure le disque,
les worktrees et le cache sans suppression. Le contrôleur ne transmet rien à
Discord quand le code de sortie vaut zéro et que la sortie est vide ; une
alerte non vide est transmise telle quelle, sans appeler de modèle.

## Après fusion : checks protégés

Une fois les nouveaux workflows présents sur `master`, les checks
**vitaux** d'une PR sont : `harness-tests`, `sim-tests`,
`forgepilot-tests`, `actionlint`, `gitleaks`. `f0-demo` reste un
contrôle réel et bon marché. `risk-gate` et `audit-check` existent
encore sous le même nom (protection de branche déjà déclarée) mais
réussissent sans prétendre protéger le lot (ADR-0018).

`audit-schema` tourne encore : il valide le frontmatter s'il y a des
audits, sans bloquer une PR produit.

Vérifier d'abord l'état courant, puis appliquer le changement avec un compte
administrateur :

```bash
gh api repos/PLiagre/ForgeHistory/branches/master/protection/required_status_checks
gh api --method PATCH \
  repos/PLiagre/ForgeHistory/branches/master/protection/required_status_checks \
  -F strict=true \
  -f 'contexts[]=harness-tests' -f 'contexts[]=sim-tests' \
  -f 'contexts[]=forgepilot-tests' -f 'contexts[]=actionlint' \
  -f 'contexts[]=gitleaks'
```

## Matériau de verdict

Après la revue indépendante :

```bash
.venv/bin/forgepilot verdict latest --repo . --output /tmp/verdict.md
```

`--comment-pr` rend le matériau visible sur la PR. La fusion quotidienne :

```bash
.venv/bin/forgepilot merge latest --repo .
.venv/bin/forgepilot merge latest --repo . --run
```

Comparer le SHA du juge, celui de la PR et celui du run. Témoin Claude
(haute valeur seulement) : `forgepilot witness <plan.json> --repo .`.
