# ForgePilot — automatisation facultative

ForgePilot conserve un chemin automatisé pour préparer un plan, lancer un
outil externe dans un worktree, exécuter des tests et suivre une PR. Il ne
stocke aucune simulation et n'est pas le workflow obligatoire du projet.

Tout contributeur ou agent autorisé peut travailler directement sans
ForgePilot. Son plan, ses revues et ses diagnostics sont consultatifs ; ils ne
réservent aucune action et ne conditionnent pas la recevabilité d'un changement.

## Installation locale

```bash
python3 -m venv .venv
.venv/bin/pip install -e ./control-plane
.venv/bin/forgepilot --help
```

La configuration versionnée fournit un exemple utilisant le CLI Cursor. Ce
choix ne s'applique qu'aux commandes qui l'appellent et peut être remplacé en
ajoutant ou adaptant un connecteur, ou simplement ignoré. Aucun modèle ou
fournisseur n'est une règle du dépôt.

## Commandes utiles

```bash
forgepilot doctor --repo .
forgepilot plan <tache.md> --repo . --risk R1
forgepilot start <brief.md> --repo .
forgepilot status latest --repo .
forgepilot resume latest --repo .
forgepilot workers --repo . --json
```

Sans `--run`, les commandes qui le prévoient affichent un aperçu. Un run
durable écrit son état local sous `.forgepilot/`, ignoré par Git. Le canal
`forge-exchange/` transporte les plans et bundles vers l'outil appelé ; il est
également ignoré par Git.

Les sous-commandes historiques de revue ou de rapport peuvent encore servir à
inspecter un run. Leur sortie n'est pas une autorisation de fusion. Ouvrir,
relire ou fusionner une PR suit les droits habituels du dépôt et peut se faire
sans ForgePilot.

## Politique locale

`workflow-policy.toml` décrit uniquement les paramètres d'un run ForgePilot :
profils de tests, délais, chemins sensibles et backend configuré. Cette
politique est une configuration de l'outil, pas une distribution des droits
entre personnes ou agents.

## Worker PC

`forgepilot workers` constate la présence d'un runner Windows. Le workflow
`.github/workflows/worker-pc.yml` ne se lance que manuellement. Un PC absent ne
bloque pas le développement courant.
