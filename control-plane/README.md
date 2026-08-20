# ForgePilot — control plane minimal

ForgePilot est le pilote réversible du nouveau workflow. Il ne remplace ni
ForgeHistory ni VictoriaCityLab et ne stocke aucune simulation. Il donne à
Hermes six commandes déterministes : vérifier, planifier, exécuter, itérer,
publier une draft PR, relire.

## Frontières

| composant | responsabilité | accès en écriture |
|---|---|---|
| Hermes | dialogue propriétaire, choix de la tâche, lancement des commandes | aucun code |
| Claude Code | plan avant le code, puis revue d'un diff dans une nouvelle invocation | aucun (`Read,Glob,Grep`) |
| Cursor CLI | implémentation dans un worktree `agent/*` isolé | worktree du lot |
| ForgePilot | commit, push et ouverture déterministe d'une draft PR | branche `agent/*` |
| CI portable | tests ForgeHistory et contrôles sans Unity | artefacts de CI seulement |
| worker Unity Windows | import, compilation et tests du commit CityLab exact | résultats et artefacts Unity seulement |
| propriétaire | décision de fusion | bouton de merge |

ACP n'est pas utilisé pour le pilote. Hermes sait servir ACP, mais ne sait pas
encore piloter un agent externe comme client ACP générique. Hermes lance donc
les modes headless documentés de Claude Code et Cursor avec des arguments,
jamais une commande construite par le modèle et passée à un shell. `agent -p`
s'exécute sur la machine qui lance ForgePilot ; un transfert explicite vers un
Cursor Cloud Agent serait un autre mode et n'est pas activé par ce pilote.

## Installation locale, puis VPS éventuel

Le pilote commence sans VPS sur le PC Windows du propriétaire. Les outils
peuvent tourner nativement ou dans WSL2, tandis qu'Unity reste natif sous
Windows. Le VPS n'est loué qu'après trois lots concluants ; le choix
d'hébergement est détaillé dans
[`docs/operations/forgepilot-hosting.md`](../docs/operations/forgepilot-hosting.md).

Les commandes Linux suivantes s'appliquent à WSL2 puis au futur VPS :

```bash
python3 -m venv .venv
.venv/bin/pip install -e ./control-plane
curl -fsSL https://hermes-agent.nousresearch.com/install.sh | bash
npm install -g @anthropic-ai/claude-code
curl https://cursor.com/install -fsS | bash
sudo apt-get install gh
hermes setup
claude auth login
agent login
gh auth login
```

Dans `hermes setup`, choisir un fournisseur distant léger, pas un modèle local :
Hermes ne fait que dialoguer et lancer ForgePilot. Ce fournisseur n'est pas
Claude Pro : l'OAuth Anthropic natif de Hermes exige Claude Max et de l'usage
supplémentaire. Hermes peut aussi rester facultatif et ForgePilot être lancé
directement.

Claude Code doit être authentifié avec le compte Claude.ai Pro. Ne pas utiliser
`claude auth login --console` et ne pas définir `ANTHROPIC_API_KEY` : ces deux
chemins basculent vers la facturation API. ForgePilot utilise `claude -p` sans
mode `--bare`, car le mode bare ignore l'authentification d'abonnement.

## Gate Unity Windows

Cette PR n'installe rien dans VictoriaCityLab. Avant le premier lot qui touche
ce dépôt, une PR dédiée doit implémenter le contrat
[`docs/operations/unity-windows-worker.md`](../docs/operations/unity-windows-worker.md).
Le worker récupère le commit exact et Git LFS, exécute Unity 6000.0.43f1 en
batchmode, puis publie ses preuves. Tant que le worker est absent, hors ligne
ou en échec, une modification CityLab reste bloquée.

VictoriaCityLab étant public, le runner personnel ne répond jamais directement
à `pull_request` et n'exécute jamais le code d'un fork. Pendant le pilote, seule
une validation `workflow_dispatch` d'une branche contrôlée par le propriétaire
est autorisée. La vérification visuelle des scènes reste humaine.

## Premier essai

Créer un fichier de tâche court qui pointe vers l'identifiant autoritaire de la
roadmap ou de l'issue, puis exécuter :

```bash
forgepilot doctor --repo /srv/ForgeHistory --check-auth
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory --run
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --run
forgepilot iterate /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory
forgepilot iterate /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --run
forgepilot publish --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --title "fh-001" --run
forgepilot review /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master --run
```

Sans `--run`, une commande affiche son invocation normalisée et ne lance aucun
agent. Les sorties réelles vont dans `.forgepilot/runs/`, ignoré par Git.
Le prompt de Claude Code (`plan`, `review`) passe par stdin, car le noyau
limite chaque argument à 128 Ko.

## Modèle et effort par rôle

Chaque rôle a sa section dans `config.toml` :

```toml
[roles.planner]
model  = "claude-opus-5"
effort = "xhigh"

[roles.reviewer]
model  = "claude-opus-5"
effort = "low"

[roles.executor]
model  = "composer-2.5"
```

Ordre de priorité (du plus fort au plus faible) : drapeau `--model` /
`--effort` passé à l'appel, puis `[roles.<rôle>]`, puis
`[tools] claude_model` / `cursor_model`, puis le défaut du binaire (aucun
drapeau ajouté). Les sous-commandes `plan`, `review`, `execute` et `iterate`
acceptent `--model` ; seules `plan` et `review` acceptent `--effort`.

Cursor n'a pas de drapeau d'effort séparé : l'effort est cuit dans le nom du
modèle (`gpt-5.3-codex-high`, etc.), donc `--effort` sur `execute` /
`iterate`, ou une clé `effort` sous `[roles.executor]`, est refusé avec
explication.

L'aperçu sans `--run` affiche le modèle et l'effort retenus ; le prompt reste
masqué à `<prompt>`.

## Conditions du pilote

- une seule tâche de **code** à la fois ;
- un cron quotidien de lecture / mesure / proposition est autorisé
  (`hermes/crons/`) ; **aucun cron ne fusionne** ;
- aucune fusion automatique ;
- le plan et la revue Claude Code sont en lecture seule (`--permission-mode
  plan`, outils `Read,Glob,Grep`, MCP et commandes personnalisées désactivés) ;
- Cursor ne travaille que dans un worktree propre ;
- Unity est en veille (ADR-0016) : un lot CityLab / Unity se refuse ;
- un contrôle absent bloque la fusion ;

Tests locaux :

```bash
cd control-plane
python3 -m unittest discover -s tests -v
```
