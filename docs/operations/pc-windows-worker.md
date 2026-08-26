# Worker PC Windows — contrat

Autorité : [ADR-0020](../adr/0020-pc-worker-opportuniste.md). Ce fichier
dit comment le PC s'annonce et exécute une tâche **machine**. Il n'est
pas une instruction pour Cursor : les lots de code restent un `brief.md`.

Le VPS ne dépend pas de ce worker. PC éteint = tâche machine **refusée**,
jamais un succès.

---

## Rôles

| machine | rôle | ne fait pas |
|---|---|---|
| VPS Linux | Hermes chef + ForgePilot + Cursor | n'infère pas, n'exécute pas Unity |
| PC Windows | runner GitHub auto-hébergé | n'écrit pas `ROADMAP.md`, n'installe pas ForgePilot comme second pilote |
| GitHub | présence (`online`/`offline`), file `workflow_dispatch`, artefacts | n'est pas un second chef |

Hermes Desktop, depuis le PC, se branche sur le Hermes du **VPS**
(Settings → Gateways → Remote gateway). Le backend local de Desktop, s'il
tourne, est un atelier : autre profil, pas la skill `forgehistory-suivi`.

Sécurité Desktop : Basic Auth uniquement derrière Tailscale / VPN. Si le
port 9119 est joignable depuis Internet, OAuth Nous Portal. Jamais
d'exposition en clair. Les secrets restent dans `~/.hermes/.env` sur le
VPS, jamais dans git.

---

## Labels = capacités

Le runner s'enregistre avec exactement ces labels (plus ceux que GitHub
ajoute tout seul : `self-hosted`, `x64`, …) :

| label | aujourd'hui | job |
|---|---|---|
| `windows` | vrai | `worker-pc.yml` (ping) |
| `high-memory` | 32 Go | ping le constate si la RAM mesurée ≥ 24 Go |
| `unity` | **réservé** | aucun tant qu'un ADR ne rouvre pas Unity |
| `local-llm` | **réservé** | aucun tant que le rôle du modèle n'est pas décidé |

Un job Unity ou LLM qui partirait aujourd'hui est un défaut, pas une
raccourci.

---

## Présence — constat, pas un scheduler

Sur le VPS, après `gh auth login` avec le droit de **lire** les runners
(Administration du dépôt, ou équivalent) :

```bash
.venv/bin/forgepilot workers --repo /srv/ForgeHistory
.venv/bin/forgepilot workers --repo /srv/ForgeHistory --json
.venv/bin/forgepilot workers --repo /srv/ForgeHistory --require windows --require high-memory
```

- au moins un runner **online** compatible → code 0 et la liste ;
- sinon → `REFUS : Worker absent : …` (code 2). Les runners offline
  restent visibles dans la liste / le JSON, pour le diagnostic.

`forgepilot workers` **ne lance aucun workflow**. Après un constat vert,
et seulement alors :

```bash
gh workflow run worker-pc.yml --repo PLiagre/ForgeHistory -f tache=ping
gh run list --workflow=worker-pc.yml --repo PLiagre/ForgeHistory --limit 1
```

Sans pré-contrôle, un `runs-on` sans runner matching reste en file
GitHub indéfiniment. C'est interdit : on refuse avant.

---

## Tâche ping (slice 1)

Workflow : [`.github/workflows/worker-pc.yml`](../../.github/workflows/worker-pc.yml).

- `on: workflow_dispatch` **uniquement** — jamais `push`, `pull_request`,
  `schedule`, ni code de fork.
- `permissions: contents: read` + `actions: write` (artefact).
- `runs-on: [self-hosted, windows]`.
- `concurrency.group: worker-pc` / `cancel-in-progress: false` : pas deux
  ping en parallèle ; le second attend.
- `timeout-minutes: 10` : PC qui disparaît en cours de route → échec,
  pas un succès.
- artefact `worker-ping` : `ping.json` (`schema_version`, `hostname`,
  `sha`, `capabilities` mesurées).

Le VPS récupère le résultat :

```bash
gh run download <RUN_ID> --name worker-ping --repo PLiagre/ForgeHistory --dir /tmp/worker-ping
```

Un ping vert prouve que le runner a tourné. Il ne prouve ni Unity, ni
un LLM, ni qu'un lot de code est recevable.

---

## Enregistrement du runner (une fois, hors git)

Compte Windows **sans** droits administrateur. Le runner parle vers
GitHub en **sortie** ; aucun port domestique n'est ouvert.

1. GitHub → Settings → Actions → Runners → New self-hosted runner →
   Windows.
2. Le jeton d'enregistrement s'affiche **une fois**. Il reste sur le PC.
   Il n'entre jamais dans le dépôt, un gist, un brief, un rapport.
3. Labels custom : `windows`, `high-memory`, `unity`, `local-llm`.
4. Service Windows du runner, démarré avec la session (ou au boot),
   arrêté quand le PC s'éteint — c'est le mode opportuniste.

Le VPS n'a pas besoin de Tailscale pour ce bus. Tailscale sert Desktop.

---

## Cas à rejouer à la main (le runner réel n'est pas la CI)

La CI `ubuntu-latest` teste le parseur. Ces cas se jouent sur le PC.

| cas | attendu |
|---|---|
| Worker disponible | `forgepilot workers --require windows` code 0 ; ping → artefact JSON valide |
| Worker absent (PC éteint ou runner stoppé) | `REFUS : Worker absent` ; le VPS `python -m sim --ticks 0` continue |
| Worker disparaît pendant le ping | le job tombe au timeout ; conclusion ≠ success |
| Tâche incompatible (`--require unity` alors qu'aucun job Unity n'existe, ou label manquant) | REFUS avant tout `workflow run` |
| Double exécution | pas deux jobs `worker-pc` en parallèle (concurrency) |
| Résultat invalide | un `ping.json` sans `schema_version` / `hostname` / `sha` est refusé par `validate_ping` |
| Permissions GitHub | `workflow_dispatch` depuis un compte sans écriture → GitHub refuse ; un fork ne déclenche rien |
| Absence de secrets | `gitleaks` vert ; le jeton runner n'est pas dans git |
| Reprise après erreur | re-constater, puis re-dispatcher ; pas de retry cron |

---

## Interdits

- second ForgePilot sur le PC comme poste de pilotage ;
- cron qui dispatch des jobs worker ;
- auto-merge, full-auto ;
- secrets dans git ;
- considérer un runner offline comme un succès ;
- installer Ollama ou Unity « pour voir » sans ADR.
