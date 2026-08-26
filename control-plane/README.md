# ForgePilot — pilote durable Hermes / Cursor / Claude

ForgePilot est le pilote réversible du workflow. Il ne stocke aucune
simulation.

**Qui fait quoi est décidé par AGENTS.md, pas ici.** Ce fichier documente les
commandes et les formats opératoires de l'outil, rien d'autre : deux fichiers
de règles qui se recopient finissent toujours par se contredire (ADR-0018).
Le tableau ci-dessous dit seulement quel accès en écriture ForgePilot accorde
à chaque composant.

## Frontières

| composant | responsabilité | accès en écriture |
|---|---|---|
| Hermes | dialogue, lancement, suivi | aucun code, aucun jugement, aucune fusion |
| Cursor, plan | plan, puis relecture de PR en invocation neuve | aucun (plan / ask) |
| Cursor, code | implémentation dans un worktree `agent/*` | worktree du lot |
| Claude | regard de dernier recours (`forgepilot witness`) | aucun |
| ForgePilot | commit, push, draft PR, `merge` mécanique | branche `agent/*` |
| CI | tests ForgeHistory | artefacts de CI seulement |
| propriétaire | label d'arrêt, veto, fusion | `do-not-merge` |

Les modèles employés par chaque rôle ne sont pas recopiés ici : ils vivent
dans `control-plane/workflow-policy.toml`, qui fait foi (règle 12).

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

## Worker PC (ADR-0020)

Unity reste archivé (ADR-0018) : `unity/` au commit `da1596d`. Le PC
Windows, quand il est allumé, est un runner GitHub auto-hébergé — pas un
second ForgePilot. Constat lecture seule :

```bash
forgepilot workers --repo /srv/ForgeHistory
forgepilot workers --repo /srv/ForgeHistory --require windows --json
```

Code 2 si aucun runner online compatible. ForgePilot ne dispatch pas :
après un constat vert, `gh workflow run worker-pc.yml -f tache=ping`.
Contrat : [`docs/operations/pc-windows-worker.md`](../docs/operations/pc-windows-worker.md).

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

Sans `--run`, `start` est un aperçu : aucun état n'est écrit. Il imprime la
commande exacte de continuation (`start … --run`). `start --run` crée ensuite
le run durable et lance sa première étape. Un second `start --run` identique
reprend un run `CREATED` de même empreinte ; un lot déjà actif (planification
ou plus loin) est refusé. `resume` repart de la première étape incomplète.
Une étape est écrite dans l'état avant l'effet suivant ; une branche ou un
worktree déjà créés sont récupérés, pas recréés.

Si la relecture finale échoue sur le contrat JSON, l'état est
`ERROR` / `BLOCKED_TOOLING` (`failure_kind: review_protocol`), jamais un
`BLOCKED` produit. Rejouer uniquement la revue du même SHA :

```bash
forgepilot recover-review <RUN_ID> --repo /srv/ForgeHistory
forgepilot recover-review <RUN_ID> --repo /srv/ForgeHistory --run
```

`--result` n'accepte qu'une enveloppe brute d'invocation agent, jamais un
JSON édité à la main.

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
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory --risk R1
forgepilot plan /srv/tasks/FH-001.md --repo /srv/ForgeHistory --risk R1 --run
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --risk R1
forgepilot execute /chemin/vers/plan.json --task-name fh-001 --repo /srv/ForgeHistory --risk R1 --run
forgepilot iterate /chemin/vers/plan.json --feedback /chemin/feedback.json --task-name fh-001 --repo /srv/ForgeHistory --risk R1 --run
forgepilot publish --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --title "fh-001" --plan /chemin/plan.json --run
forgepilot review /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master --risk R2 --run
forgepilot recover-review <RUN_ID> --repo /srv/ForgeHistory --run
forgepilot witness /chemin/vers/plan.json --repo /srv/ForgeHistory/.forgepilot/worktrees/fh-001 --base origin/master
```

`--risk` n'est pas décoratif : c'est lui qui choisit le fournisseur, le modèle
et les délais dans `workflow-policy.toml`. Sans lui, ces commandes refusent au
lieu de retomber sur un défaut historique — `review` appelait Claude alors que
la politique nomme Cursor. `plan` accepte de le dériver du brief
(`Risque : R2`) ; le témoin (`witness`) reste nommé par `[witness]`,
seule exemption (ADR-0017).

Sans `--run`, une commande affiche son invocation normalisée et ne lance aucun
agent. Les sorties réelles vont dans `.forgepilot/runs/`, ignoré par Git.
Ce que ForgePilot tend à LIRE à un agent — plan, feedback, bundle de revue —
passe par `.forge-exchange/`, un canal git-ignoré mais jamais cursor-ignoré :
`.forgepilot/` étant filtré par `.cursorignore`, un bundle qui y restait était
présent, lisible par le système, et hors de portée de son relecteur. C'est le
canal livré par la PR #138 ; on ne le remplace pas. Chaque fichier de rôle
est retiré à la fin de l'étape qui l'a consommé, succès ou échec : le canal
n'est pas une archive.

Une panne pendant l'invocation, ou un JSON métier reçu puis refusé par
`validate_review()`, laisse sa trace caviardée sous
`.forgepilot/runs/<run>/traces/` — le même format, la même observabilité.
Le prompt de Claude Code (`plan`, `review`) passe par stdin, car le noyau
limite chaque argument à 128 Ko. Le prompt reste masqué à `<prompt>`.

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
