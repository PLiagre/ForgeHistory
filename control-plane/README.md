# ForgePilot — control plane minimal

ForgePilot est le pilote réversible du nouveau workflow. Il ne remplace ni
ForgeHistory ni VictoriaCityLab et ne stocke aucune simulation. Il donne à
Hermes cinq commandes déterministes : vérifier, planifier, exécuter, publier
une draft PR, relire.

## Frontières

| composant | responsabilité | accès en écriture |
|---|---|---|
| Hermes | dialogue propriétaire, choix de la tâche, lancement des commandes | aucun code |
| Grok Build | plan avant le code, puis revue d'un diff dans une nouvelle invocation | aucun (`read-only`) |
| Cursor CLI | implémentation dans un worktree `agent/*` isolé | worktree du lot |
| ForgePilot | commit, push et ouverture déterministe d'une draft PR | branche `agent/*` |
| CI | tests mécaniques | artefacts de CI seulement |
| propriétaire | décision de fusion | bouton de merge |

ACP n'est pas utilisé pour le pilote. Hermes sait servir ACP, mais ne sait pas
encore piloter un agent externe comme client ACP générique. Hermes lance donc
les modes headless documentés de Grok et Cursor avec des arguments, jamais une
commande construite par le modèle et passée à un shell.

## Installation sur un petit serveur Linux persistant

Le serveur conserve les sessions d'abonnement sans laisser Hermes ou un modèle
chargé sur le PC du propriétaire.

```bash
python3 -m venv .venv
.venv/bin/pip install -e ./control-plane
curl -fsSL https://hermes-agent.nousresearch.com/install.sh | bash
curl https://cursor.com/install -fsS | bash
sudo apt-get install gh
hermes setup
grok login --device-auth
agent login
gh auth login
```

Dans `hermes setup`, choisir un fournisseur distant léger, pas un modèle local :
Hermes ne fait que dialoguer et lancer ForgePilot. Grok Build doit être
authentifié avec le compte SuperGrok. Si `grok` réclame
une `XAI_API_KEY`, le pilote consommerait l'API facturée séparément : arrêter et
corriger l'authentification au lieu de continuer silencieusement.

## Premier essai

Créer un fichier de tâche court qui pointe vers l'identifiant autoritaire de la
roadmap ou de l'issue, puis exécuter :

```bash
forgepilot doctor --repo /srv/ForgeHistory --check-auth
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory --run
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --run
forgepilot publish --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --title "fh-001" --run
forgepilot review /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master --run
```

Sans `--run`, une commande affiche son invocation normalisée et ne lance aucun
agent. Les sorties réelles vont dans `.forgepilot/runs/`, ignoré par Git.

## Conditions du pilote

- une seule tâche à la fois ;
- aucun cron pendant les trois premiers lots ;
- aucune fusion automatique ;
- le plan et la revue Grok sont en lecture seule ;
- Cursor ne travaille que dans un worktree propre ;
- un contrôle absent bloque la fusion ;
- après trois lots, comparer qualité, latence, consommation et interventions
  manuelles avant d'étendre le système.

Tests locaux :

```bash
cd control-plane
python3 -m unittest discover -s tests -v
```
