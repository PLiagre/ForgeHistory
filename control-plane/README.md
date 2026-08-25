# ForgePilot — pilote durable Hermes / Cursor / Claude

ForgePilot est le pilote réversible du workflow. Il ne stocke aucune
simulation. Hermes, configuré sur Nous Portal, contrôle la cadence ; Grok
4.6 planifie et juge la PR ; Composer exécute ; Claude Opus 5 n'est qu'un
témoin rare (ADR-0017). Le contrat détaillé du chantier durable est le
brief 029. Ce fichier documente uniquement les commandes et les formats
opératoires.

## Frontières

| composant | responsabilité | accès en écriture |
|---|---|---|
| Hermes / Nous Portal | dialogue, lancement, suivi (`openai/gpt-5.4`) | aucun code, aucun jugement, aucune fusion |
| Cursor Grok 4.6 | plan, puis juge de PR (`xhigh`, invocation neuve) | aucun (plan / ask) |
| Cursor Composer 2.5 | implémentation dans un worktree `agent/*` | worktree du lot |
| Claude Opus 5 | témoin rare (`forgepilot witness`) | aucun |
| ForgePilot | commit, push, draft PR, `merge` mécanique | branche `agent/*` |
| CI portable | tests ForgeHistory et contrôles sans Unity | artefacts de CI seulement |
| worker Unity Windows | import, compilation et tests du commit CityLab exact | résultats et artefacts Unity seulement |
| propriétaire | label d'arrêt, témoin, veto | `do-not-merge` |

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

## Unity

Unity est archivé (ADR-0018) : `unity/` est sorti de l'arbre de travail, au
commit `da1596d`. ForgePilot ne pilote rien du côté Unity, et le contrat de
worker Windows qui était décrit ici a été supprimé avec le reste. Rouvrir ce
sujet demande une décision, donc un ADR — pas la relecture d'un document
disparu.

VictoriaCityLab étant public, le runner personnel ne répond jamais directement
à `pull_request` et n'exécute jamais le code d'un fork. Pendant le pilote, seule
une validation `workflow_dispatch` d'une branche contrôlée par le propriétaire
est autorisée. La vérification visuelle des scènes reste humaine.

## Chemin durable recommandé

Passer le brief autoritaire, jamais une proposition Hermes :

```bash
forgepilot doctor --repo /srv/ForgeHistory --check-auth
forgepilot start /srv/ForgeHistory/harness/queue/briefs/NNN-slug/brief.md \
    --repo /srv/ForgeHistory --run
forgepilot status latest --repo /srv/ForgeHistory
forgepilot resume latest --repo /srv/ForgeHistory
forgepilot verdict latest --repo /srv/ForgeHistory
forgepilot merge latest --repo /srv/ForgeHistory
forgepilot merge latest --repo /srv/ForgeHistory --run
```

Sans `--run`, `start` enregistre le lot sans lancer d'agent. Il imprime
l'identifiant stable à réutiliser à la place de `latest`. `resume` repart de
la première étape incomplète. Une étape est écrite dans l'état avant l'effet
suivant ; une branche ou un worktree déjà créés sont récupérés, pas recréés.

L'état atomique vit dans :

```text
.forgepilot/runs/<RUN_ID>/state.json
```

Le même dossier contient le plan normalisé, les sorties filtrées, le feedback,
le bundle et le matériau de revue liés au SHA. Les prompts et secrets n'y sont
jamais archivés. `verdict --comment-pr` rend le matériau visible sur la PR ; il
ne fusionne rien. Le corps de PR porte `Forge-Risk: Rn` pour le contrôle CI.
Une certification lourde reste interdite par défaut. Lorsque le cache requis
est réellement disponible, `resume <RUN_ID> --allow-heavy` l'autorise et
persiste cette décision explicite ; elle n'est jamais déduite d'un succès
partiel.

`enchaine` reste disponible comme façade compatible et aperçu. `start` est le
chemin recommandé dès qu'une reprise ou une itération est possible.

Les sous-commandes une par une restent disponibles pour un dépannage
(`iterate` après une revue) :

```bash
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory --run
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --run
forgepilot iterate /chemin/vers/plan.json --feedback /chemin/feedback.json --task-name fh-001 --repo /srv/ForgeHistory --run
forgepilot publish --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --title "fh-001" --plan /chemin/plan.json --run
forgepilot review /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master --run
forgepilot witness /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master
```

Sans `--run`, une commande affiche son invocation normalisée et ne lance aucun
agent. Les sorties réelles vont dans `.forgepilot/runs/`, ignoré par Git.
Le prompt de Claude Code (`plan`, `review`) passe par stdin, car le noyau
limite chaque argument à 128 Ko.

## Politique effective R0 / R1 / R2

[`workflow-policy.toml`](workflow-policy.toml) est l'unique politique de
workflow versionnée. `config.toml` pointe vers elle. Elle nomme le contrôleur,
les backends compatibles, les modèles, les efforts, le profil de tests et les
quatre délais distincts de chaque risque. `doctor` et les aperçus impriment sa
valeur effective avant tout agent.

Le risque demandé est un plancher. Le classement des chemins peut uniquement
l'élever. Après le plan Claude, ForgePilot reclasse aussi
`files_allowed_to_change` avant de démarrer Cursor. Une politique absente,
invalide ou incompatible bloque `doctor`, `start` et `resume`.

Cursor n'a pas de drapeau d'effort séparé : l'effort est cuit dans le nom du
modèle (`gpt-5.3-codex-high`, etc.), donc `--effort` sur `execute` /
`iterate`, ou une clé `effort` sous `[roles.executor]`, est refusé avec
explication.

L'aperçu sans `--run` affiche le modèle et l'effort retenus ; le prompt reste
masqué à `<prompt>`.

## Publication et itération

Le plan JSON est validé avant exécution. `blocked: true` arrête le lot avant la
création du worktree. Avant commit, chaque chemin modifié est comparé à
`files_allowed_to_change`, puis seuls ces chemins sont ajoutés explicitement à
l'index Git.

Une revue `FAIL` crée un fichier de feedback lisible. `resume` le transmet à
Cursor et ajoute `--resume <session>` lorsque le CLI a fourni un identifiant.
Les tests `fast` du routeur sont exécutés avant de pousser la correction. La
revue suivante reçoit le delta et les constats antérieurs ; si Cursor déclare
`approach_changed: true`, elle redevient complète. Deux itérations sans
amélioration arrêtent le lot.

Le bundle de revue contient les SHA, le plan, les diffs écrits à la main, les
empreintes d'artefacts et les résultats mécaniques. Il exclut la conclusion de
Cursor et refuse tout dépassement de taille sans troncature.

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
