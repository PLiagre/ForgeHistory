# Brief 023 : ForgePilot choisit son modèle et son effort, rôle par rôle

**Authored**: 2026-08-15T21:00:00Z
**Author**: forge-planificateur

---

## Provenance

Le lot `022` a réparé la relecture et ajouté l'itération. En le passant, une
question est restée sans réponse : **comment un pilote choisit-il le modèle et
l'effort de chaque rôle ?** Aujourd'hui il ne le peut pas.

Le coût mesuré de la session du `2026-08-15`
(`harness/backends/ledger.py tokens`) : `68.66` USD d'équivalent tarif API pour
un lot, dont `59.70` pour la seule orchestration, sur `434` appels à `213 801`
jetons de contexte moyen. Le plan a coûté `1.08`, la relecture `1.96`. Le
plafond mensuel de l'abonnement a été atteint pendant la session.

ADR-0014 propose de sortir l'orchestration de Claude pour la confier à Hermes.
**Cet ADR est inapplicable tant que ce lot n'existe pas** : un pilote qui ne
peut pas choisir modèle et effort par rôle ne pilote rien. Ce brief construit le
bouton. Il ne choisit pas les valeurs par la mesure — c'est un lot ultérieur.

Ce brief est **la seule instruction** (voir `CLAUDE.md` › Single Source of
Instruction). Tout le nécessaire est écrit ici.

---

## Dépendance de séquence

Ce lot touche `control-plane/forgepilot/workflow.py`, `cli.py`, `config.py` et
`control-plane/tests/test_workflow.py` — les mêmes fichiers que le lot `022`,
dont la PR `#108` n'est **pas encore fusionnée** au moment d'écrire ce brief.

Ce lot part donc de la branche `agent/forgepilot-stdin` (commit `1eade7a`) ou
de `master` une fois `#108` fusionnée, **jamais d'un `master` antérieur** : il
casserait la relecture par stdin. Le Générateur constate laquelle des deux
situations s'applique et le déclare dans `generator-log.md`.

---

## Ce que ce lot doit préserver

Les garanties d'ADR-0013 ne sont pas négociables et ce lot ne les assouplit sous
aucun prétexte :

- **Claude Code reste en lecture seule** : `--permission-mode plan`, outils
  limités à `Read,Glob,Grep`, `mcp__*` interdits, `--safe-mode`.
- **Cursor reste le seul exécutant**, toujours avec `--sandbox enabled`,
  `--force`, `--trust` et un `--workspace` qui est un worktree isolé.
- **Le producteur ne fusionne jamais son propre travail.**
- **Aucune clé d'API** : `ANTHROPIC_API_KEY` reste refusé par `doctor`.
- **Le prompt de Claude reste sur stdin** (acquis du lot `022`) : aucun élément
  d'argv ne redevient un porteur de prompt.

Les **douze** tests de `control-plane/tests/test_workflow.py` (six d'origine +
six ajoutés par le lot `022`) doivent rester verts **sans être modifiés**.
Seules des additions sont recevables.

---

## Vocabulaire (expliqué une fois)

- **rôle** : une des trois invocations que le pilote sait produire —
  `planner` (Claude prépare un plan), `reviewer` (Claude relit un diff),
  `executor` (Cursor modifie le code). Le verdict n'est pas un rôle du pilote :
  il tourne aujourd'hui hors de ForgePilot, en sous-agent.
- **effort** : chez Claude, un niveau de profondeur de raisonnement passé par le
  drapeau `--effort`, indépendant du modèle. Valeurs : `low`, `medium`, `high`,
  `xhigh`, `max`. Le défaut du CLI est `high`.
- **modèle paramétré** : chez Cursor, l'effort est **cuit dans le nom du
  modèle** (`gpt-5.3-codex-low`, `-high`, `-xhigh`). Il n'existe aucun drapeau
  d'effort séparé. `composer-2.5` n'a pas de variante d'effort.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment d'écrire ce brief :

- `control-plane/forgepilot/config.py:9-19` — `Settings` est un dataclass gelé
  de **dix champs, tous obligatoires**, dont `claude_model` et `cursor_model`.
- `control-plane/config.toml` — `claude_model = ""` (donc aucun `--model` n'est
  passé) et `cursor_model = "auto"`.
- `control-plane/forgepilot/workflow.py` — `plan_invocation` et
  `review_invocation` appellent **tous deux** `_claude_argv(settings)` : les
  deux rôles Claude ne peuvent pas différer.
- `_claude_argv` ajoute `--model` en dernier, et seulement si
  `settings.claude_model` est non vide. Aucune notion d'effort n'existe dans
  `control-plane/`.
- `executor_invocation` ajoute `--model` en dernier si `settings.cursor_model`.
- `cli.py` n'expose que `--config`, qui échange le fichier de configuration
  **entier**.

**Faits mesurés sur la machine du lot** :

- `claude --help` expose `--model <model>` **et** `--effort <level>`.
- `agent models` liste, entre autres : `auto`, `composer-2.5`,
  `claude-opus-5-thinking-high`, `gpt-5.3-codex-{low,high,xhigh}`,
  `gpt-5.6-sol-{high,xhigh}`, chacun avec une variante `-fast`.

---

## Décisions de conception tranchées par le Planificateur

### D1 — Un réglage par rôle, dans `config.toml`

Nouvelle section, une sous-table par rôle :

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

Les trois noms de rôle sont `planner`, `reviewer`, `executor` — pas d'autres.
Un rôle inconnu dans `config.toml` est un refus explicite, pas un silence.

### D2 — `Settings` reste rétrocompatible, sans exception

**Contrainte dure, vérifiée** : `control-plane/tests/test_workflow.py:21-32`
construit `Settings(...)` avec exactement les dix champs actuels. Ce fichier de
test **ne peut pas être modifié** (voir « Ce que ce lot doit préserver »).

Donc : tout champ ajouté à `Settings` **porte une valeur par défaut**. Un champ
obligatoire supplémentaire fait exploser les douze tests d'un coup avec un
`TypeError`, et c'est un échec disqualifiant.

`claude_model` et `cursor_model` restent présents et fonctionnels : ils servent
de repli quand aucun `[roles.*]` ne couvre le rôle.

### D3 — Ordre de priorité, du plus fort au plus faible

1. Le drapeau passé à l'appel (`--model`, `--effort`).
2. La section `[roles.<rôle>]` de `config.toml`.
3. Les champs hérités `[tools] claude_model` / `cursor_model`.
4. Le défaut du binaire (aucun drapeau ajouté).

C'est le niveau `1` qui rend Hermes capable de décider **sans écrire sur le
disque** : le choix voyage dans la commande, pas dans un fichier réécrit avant
chaque lancement.

Les sous-commandes `plan`, `review`, `execute` et `iterate` reçoivent `--model`.
Seules `plan` et `review` reçoivent `--effort`.

### D4 — `--effort` sur un rôle Cursor est un refus explicite

Cursor n'a pas de drapeau d'effort. Un `--effort` passé à `execute` ou
`iterate`, ou une clé `effort` sous `[roles.executor]`, produit une erreur du
pilote qui **dit pourquoi** : l'effort est cuit dans le nom du modèle Cursor.

Un réglage silencieusement ignoré est pire qu'une absence de réglage : il fait
croire à un contrôle qui n'existe pas.

### D5 — L'aperçu montre le choix

`format_invocation` fait apparaître le modèle et l'effort effectivement retenus.
Sans cela, Hermes ne peut pas vérifier ce qu'il a demandé avant de lancer, et le
réglage n'est pas auditable dans `.forgepilot/runs/`.

Le prompt reste masqué à `<prompt>` — acquis du lot `022`, non négociable.

### D6 — Valeurs par défaut livrées : celles de la documentation, pas d'une intuition

Le lot livre les valeurs ci-dessous et **rien de plus** ; les affiner par la
mesure est un lot ultérieur (non-objectif `3`).

| rôle | modèle | effort | source |
|---|---|---|---|
| `planner` | `claude-opus-5` | `xhigh` | Documentation Opus 5 : commencer à `xhigh` pour le travail de codage et d'agent, `high` ailleurs. |
| `reviewer` | `claude-opus-5` | `low` | Documentation Opus 5, relecture de code : « haute précision et haut rappel… **reste précis à effort plus bas** ». |
| `executor` | `composer-2.5` | — | Décision du propriétaire du `2026-08-15`. Cursor n'a pas d'effort séparé. |

Le choix `reviewer` à `low` est **contre-intuitif et assumé** : la
documentation dit explicitement que la relecture Opus 5 tient à effort bas, et
la relecture mesurée du lot `022` a coûté `1.96` USD à l'effort par défaut
(`high`).

### D7 — Preuve rouge d'abord

Les tests ajoutés doivent **échouer avant la correction et passer après**. Trois
ne sont pas négociables :

1. **Deux rôles Claude, deux modèles.** `plan` et `review` produisent des
   invocations portant des modèles différents. Avant D1, impossible : ils
   partagent `claude_model`.
2. **Le drapeau l'emporte sur le fichier.** Un `--model` passé à l'appel gagne
   contre `[roles.*]` et contre `[tools]`.
3. **`--effort` sur Cursor refuse.** Erreur du pilote, pas un plantage, pas un
   silence.

S'y ajoute un test que `Settings` reste constructible avec ses dix champs
d'origine — la garde de D2.

### D8 — Périmètre de fichiers

**Autorisé :**

- `control-plane/forgepilot/config.py`
- `control-plane/forgepilot/workflow.py`
- `control-plane/forgepilot/cli.py`
- `control-plane/config.toml`
- `control-plane/tests/test_workflow.py` — **uniquement pour ajouter** des tests
- `control-plane/README.md`
- `harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/deliverables/**`
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée)

**Interdit :** les douze tests existants de `test_workflow.py` ;
`control-plane/prompts/**` ; `pipeline/**` ; `sim/**` ; `harness/*.py` ;
`harness/pipeline/**` ; `.github/**` ; `docs/adr/**` ; `VISION.md` ;
`ROADMAP.md` ; `HANDOFF.md` ; `hermes/**` ; tout autre brief.

---

## Success Conditions

### SC1 — Chaque rôle a son modèle, et ils peuvent différer

- `modeles_par_role_distincts` vaut `1` : avec un `config.toml` déclarant deux
  modèles différents pour `planner` et `reviewer`, les deux invocations portent
  bien deux `--model` différents.
- `roles_declares` est le nombre de rôles lus depuis `config.toml`, rapporté sur
  `3` (le nombre de rôles que le pilote connaît).
- `role_inconnu_refuse` vaut `1` : une section `[roles.n-importe-quoi]` produit
  une erreur du pilote nommant les trois rôles valides.

### SC2 — L'effort atteint Claude et seulement Claude

- `effort_transmis_claude` vaut `1` : `--effort <niveau>` apparaît dans l'argv
  des invocations `planner` et `reviewer`.
- `effort_refuse_sur_cursor` vaut `1` : `--effort` sur `execute` ou `iterate`,
  et une clé `effort` sous `[roles.executor]`, rendent chacun une erreur du
  pilote dont le message explique que Cursor cuit l'effort dans le nom du
  modèle.
- `niveaux_effort_acceptes` est le nombre de niveaux validés par le pilote,
  rapporté sur `5` (`low`, `medium`, `high`, `xhigh`, `max`). Un niveau hors
  liste est refusé.

### SC3 — La priorité à l'appel fonctionne

- `priorite_cli_sur_role` vaut `1` : un `--model` passé à l'appel l'emporte sur
  `[roles.*]`.
- `priorite_role_sur_tools` vaut `1` : `[roles.*]` l'emporte sur
  `[tools] claude_model`.
- `repli_sur_tools` vaut `1` : sans section `[roles.*]`, le comportement
  d'aujourd'hui est retrouvé à l'identique.
- `aucun_drapeau_si_rien_declare` vaut `1` : avec `claude_model = ""` et aucun
  `[roles.*]`, aucun `--model` n'est ajouté — le défaut du binaire est respecté.

### SC4 — Les garanties d'ADR-0013 et du lot 022 survivent

- `drapeaux_lecture_seule_intacts` vaut `1` : `--permission-mode plan`,
  `--tools Read,Glob,Grep`, `--disallowedTools mcp__*`, `--safe-mode` présents
  et inchangés sur les deux rôles Claude.
- `sandbox_intact` vaut `1` : `--sandbox enabled` présent sur `execute` **et**
  `iterate`.
- `prompt_absent_de_argv` vaut `1` : l'acquis du lot `022` tient toujours.
- `settings_retrocompatible` vaut `1` : `Settings` reste constructible avec ses
  dix champs d'origine et rien d'autre.
- `tests_existants_intacts` vaut `1` : les douze tests d'origine sont présents
  et non modifiés, prouvé par un `git diff` de `test_workflow.py` ne contenant
  que des additions.

### SC5 — L'aperçu est auditable

- `apercu_montre_modele_et_effort` vaut `1` : l'aperçu sans `--run` fait
  apparaître le modèle et l'effort retenus.
- `apercu_ne_fuit_pas_le_prompt` vaut `1` : le texte du prompt n'y apparaît pas.

### SC6 — Preuve rouge d'abord

- `tests_ajoutes` est le nombre de tests ajoutés, rapporté avec le total du
  fichier après le lot pour dénominateur.
- `tests_rouges_avant_correction` vaut au moins `3` : les trois tests non
  négociables de D7 ont été **vus échouer** sur le code d'avant, et la sortie de
  cet échec est recopiée dans `deliverables/generator-log.md`. Le compteur est
  **dérivé** en rejouant la suite contre le code d'avant dans une copie jetable
  hors du dépôt — jamais en cherchant des mots dans le journal (règle durement
  acquise n° `3`).
- `suite_control_plane_verte` : la suite passe, avec le nombre de tests exécutés
  pour dénominateur et **la commande réellement jouée** rapportée.

### SC7 — Le reste du dépôt ne bouge pas

- `fichiers_hors_perimetre_modifies` vaut `0`, mesuré par un `git diff
  --name-only` contre la base réelle du lot, confronté à la liste D8.
- `harness/tests/` reste vert. Les neuf échecs connus de `test_run_unity.py`
  sous WSL2 (binaire Unity absent) sont **préexistants** et ne comptent pas
  comme régression ; les rapporter séparément, avec le nombre de tests
  collectés.

### SC8 — La documentation dit comment choisir

- `control-plane/README.md` documente la section `[roles.*]`, l'ordre de
  priorité de D3, et dit en une phrase **pourquoi Cursor n'a pas d'effort**.
- Un instantané pré-édition est committé sous
  `deliverables/pre-edit/control-plane-README.md.orig`, déclaré en couple
  `must_differ_from` avec le README publié.

---

## Non-objectifs

1. **Ne pas ajouter de rôle au pilote.** Le verdict tourne hors de ForgePilot ;
   lui donner une sous-commande est un autre lot.
2. **Ne pas toucher à l'orchestration.** Le déplacement vers Hermes est
   ADR-0014, pas ce lot.
3. **Ne pas choisir les valeurs par la mesure.** Ce lot livre les défauts
   documentés de D6. Comparer `composer-2.5` aux autres exécutants, ou mesurer
   le couple qualité/coût de `reviewer` à `low` contre `high`, est un lot de
   mesure ultérieur qui s'appuiera sur le verdict de référence du lot `022`.
4. **Ne pas ajouter d'auto-fusion, ni de cron, ni de boucle.**
5. **Ne pas corriger les neuf tests Unity** en échec sous WSL2.
6. **Ne pas toucher au transport du prompt par stdin.** Acquis du lot `022`.

---

## Required Counters

`modeles_par_role_distincts`, `roles_declares`, `role_inconnu_refuse`,
`effort_transmis_claude`, `effort_refuse_sur_cursor`, `niveaux_effort_acceptes`,
`priorite_cli_sur_role`, `priorite_role_sur_tools`, `repli_sur_tools`,
`aucun_drapeau_si_rien_declare`, `drapeaux_lecture_seule_intacts`,
`sandbox_intact`, `prompt_absent_de_argv`, `settings_retrocompatible`,
`tests_existants_intacts`, `apercu_montre_modele_et_effort`,
`apercu_ne_fuit_pas_le_prompt`, `tests_ajoutes`,
`tests_rouges_avant_correction`, `suite_control_plane_verte`,
`fichiers_hors_perimetre_modifies`.

Chacun doit être imprimé **avec son dénominateur** par un script committé sous
`deliverables/measure_023.py`, exécuté depuis la racine du dépôt, et déclaré
dans `deliverables/manifest.json` avec un `sample_size` réel.

Le script **n'écrit pas le manifeste par défaut** : l'écriture est une option
explicite (`--write-manifest`). C'est une leçon du lot `022`, où la
reconstruction de l'Évaluateur salissait un livrable committé.

---

## Dérogations acceptables

Une dérogation n'est recevable qu'accompagnée de **la commande rejouée et de sa
sortie d'erreur**. Cas prévu : si `claude --effort` ou `agent --model
composer-2.5` étaient refusés sur la machine du lot, la dérogation doit porter
la commande exacte et le message.

À l'écriture de ce brief, les deux chemins sont **vérifiés présents** :
`claude --help` expose `--effort <level>`, et `agent models` liste
`composer-2.5`.

Aucune dérogation ne peut porter sur : le maintien des douze tests existants, la
rétrocompatibilité de `Settings`, la lecture seule de Claude Code, le sandbox de
Cursor, le transport du prompt par stdin, ou l'interdiction de fusion
automatique.
