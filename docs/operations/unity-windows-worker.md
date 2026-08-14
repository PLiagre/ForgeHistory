# Contrat du worker Unity Windows

**Statut : architecture acceptée, implémentation différée à une PR
VictoriaCityLab dédiée.** Cette PR ForgeHistory ne crée aucun runner et ne
modifie pas VictoriaCityLab.

## Faits autoritaires

- dépôt : `PLiagre/VictoriaCityLab`, branche par défaut `main` ;
- dépôt public ;
- Unity : `6000.0.43f1` ;
- Unity Test Framework : `1.4.6` ;
- assets lourds gérés par Git LFS ;
- scènes et prefabs sérialisés en texte, mais toute modification reste soumise
  à l'import et à la validation Unity.

## Frontière

Cursor peut écrire du C#, des tests et des scripts Editor depuis le VPS ou un
Cloud Agent. Il ne prononce jamais la compatibilité Unity. Le worker Windows
récupère le commit exact de la branche, restaure Git LFS, ouvre le projet en
batchmode et publie les preuves. Claude Code relit le diff et les preuves ; le
propriétaire fusionne.

## Machine et sécurité

Le runner GitHub Actions est installé nativement sous Windows comme service :

- compte Windows dédié, sans droits administrateur et sans données personnelles ;
- Unity installé et licencié pour ce compte ;
- Git et Git LFS disponibles ;
- aucun secret fourni au prompt ou au worktree ;
- aucun port entrant exposé ;
- un seul job Unity à la fois.

VictoriaCityLab étant public, le workflow ne se déclenche jamais sur
`pull_request`, `pull_request_target` ou le code d'un fork. Pendant le pilote,
seul `workflow_dispatch` est autorisé, après vérification que le SHA appartient
à une branche contrôlée par `PLiagre`. Une indisponibilité du runner produit un
état en attente ou bloqué, jamais un succès.

Référence de sécurité :
<https://docs.github.com/actions/hosting-your-own-runners/adding-self-hosted-runners>.

## Séquence obligatoire

1. Cursor pousse une branche `agent/*` et ouvre une draft PR.
2. Le propriétaire ou Hermes présente le SHA et demande explicitement la
   validation Unity.
3. GitHub envoie le job au runner Windows identifié par les labels
   `self-hosted`, `windows`, `x64`, `unity`.
4. Le worker checkout le SHA exact avec Git LFS.
5. Il exécute au minimum l'import/compilation et les tests EditMode.
6. Il publie `unity.log`, le XML NUnit et un résumé machine lisible.
7. Le check `unity-windows` doit réussir sur le même SHA avant la revue finale.
8. Une vérification humaine reste requise pour le rendu, les scènes et le
   gameplay lorsque le lot modifie leur comportement visuel.

## Commande de référence

Le script versionné dans VictoriaCityLab utilisera PowerShell et le chemin exact
de l'éditeur :

```powershell
$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe'
$Results = Join-Path $env:RUNNER_TEMP 'editmode.xml'
$Log = Join-Path $env:RUNNER_TEMP 'unity.log'

& $Unity `
  -batchmode `
  -nographics `
  -projectPath $env:GITHUB_WORKSPACE `
  -runTests `
  -testPlatform EditMode `
  -testResults $Results `
  -logFile $Log

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path $Results)) { throw 'Résultats Unity absents' }
```

Ne pas ajouter `-quit` à une exécution `-runTests` : Unity Test Framework ne
le supporte pas pendant les tests. Le workflow doit aussi analyser le XML et
refuser tout échec, test absent ou fichier de résultats manquant.

Références Unity :

- <https://docs.unity3d.com/6000.5/Documentation/Manual/EditorCommandLineArguments.html> ;
- <https://docs.unity3d.com/6000.5/Documentation/Manual/test-framework/reference-command-line.html>.

## Limites assumées

- EditMode et compilation peuvent tourner sans présence humaine.
- PlayMode graphique peut exiger une session Windows interactive ou une
  configuration supplémentaire ; il sera ajouté après le premier gate stable.
- Une scène visuellement correcte ne se prouve pas par un exit code : une revue
  locale ou par bureau distant reste nécessaire.
- Si le PC est éteint, Hermes et Cursor peuvent continuer les tâches portables,
  mais la PR CityLab reste non fusionnable.

## PR VictoriaCityLab suivante

La première PR CityLab d'infrastructure devra ajouter, dans cet ordre :

1. un script PowerShell versionné qui vérifie la version Unity ;
2. un smoke test EditMode et une preuve rouge/verte ;
3. un workflow manuel limité aux branches du propriétaire ;
4. checkout Git LFS, cache contrôlé de `Library/` et collecte des artefacts ;
5. le check `unity-windows` ;
6. seulement après plusieurs exécutions stables, l'éventuel statut requis.

Aucun auto-merge, Wake-on-LAN, PlayMode graphique ou Unity Build Automation
n'est inclus dans cette première PR.
