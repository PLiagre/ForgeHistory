# Brief 022 : ForgePilot répare ce que son premier lot réel a cassé — la relecture qui déborde, et l'itération qui n'existe pas

**Authored**: 2026-08-15T12:30:00Z
**Author**: forge-planificateur

---

## Provenance

Le brief `021` (fleuves G5) a été le **premier lot réel** passé de bout en bout
par ForgePilot le `2026-08-15`. Il a livré, il a été rejeté, corrigé, puis
accepté. En chemin, il a heurté **deux défauts du pilote lui-même**, sans rapport
avec le code produit :

1. `forgepilot review` s'est arrêté sur `[Errno 7] Argument list too long` et
   n'a jamais pu relire quoi que ce soit. La relecture a dû être faite par un
   autre chemin.
2. Pour faire corriger les onze constats par le Générateur, **aucune commande
   n'existait** : `forgepilot execute` refuse un worktree déjà présent, et il n'y
   a pas d'équivalent « refais une passe sur la branche en cours ». L'itération a
   été lancée à la main, en reconstruisant l'invocation du contrat.

Ce sont les deux raisons pour lesquelles un lot ne s'enchaîne pas encore tout
seul. Ce brief les corrige. Il ne touche à rien d'autre.

Ce brief est **la seule instruction** (voir `CLAUDE.md` › Single Source of
Instruction). Tout le nécessaire est écrit ici.

---

## Ce que ce lot doit préserver

`control-plane/` est le pilote décidé par ADR-0013. Ses garanties ne sont pas
négociables et ce lot ne les assouplit sous aucun prétexte :

- **Claude Code reste en lecture seule** : `--permission-mode plan`, outils
  limités à `Read,Glob,Grep`, `mcp__*` interdits, `--safe-mode`.
- **Cursor reste le seul exécutant**, toujours avec `--sandbox enabled`,
  `--force`, `--trust` et un `--workspace` qui est un worktree isolé.
- **Le producteur ne fusionne jamais son propre travail** : aucune commande de
  ce lot n'obtient le droit de fusionner, ni de pousser sur `master`.
- **Aucune clé d'API** : `ANTHROPIC_API_KEY` reste refusé par `doctor`.

Les quatre tests existants de `control-plane/tests/test_workflow.py` doivent
rester verts **sans être modifiés** : ils encodent ces garanties.

---

## Vocabulaire (expliqué une fois)

- **argv** : la liste d'arguments passée à un programme quand on le lance. Le
  noyau Linux limite **chaque argument pris isolément** à `MAX_ARG_STRLEN`, soit
  `32` × la taille d'une page mémoire = `131072` octets (`128` Ko). C'est une
  limite **différente** d'`ARG_MAX` (`2` Mo), qui borne le total.
- **stdin** : l'entrée standard d'un programme. Elle n'est pas soumise à la
  limite ci-dessus : on peut y écrire autant qu'on veut.
- **worktree** : une seconde copie de travail du dépôt, sur sa propre branche,
  que git gère à côté de la copie principale.
- **itération** : une nouvelle passe du Générateur sur **la branche déjà
  ouverte**, pour appliquer un feedback — par opposition à un nouveau lot, qui
  repart d'une branche neuve.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment d'écrire ce brief :

- `control-plane/forgepilot/workflow.py:44` — `_claude_argv(settings, prompt)`
  place le prompt **en argument** (`"-p", prompt`).
- `control-plane/forgepilot/workflow.py:74-86` — `review_invocation` lit le diff
  complet (`git diff --no-ext-diff <base>...HEAD`), le substitue dans
  `prompts/reviewer.md` et passe le tout à `_claude_argv`. C'est là que ça casse.
- `control-plane/forgepilot/workflow.py:130-139` — `execute_invocation` accepte
  **déjà** un paramètre `stdin` et le transmet à `run_command`. Le tuyau existe,
  personne ne s'en sert pour Claude.
- `control-plane/forgepilot/process.py:37-59` — `run_command(..., stdin=...)`
  passe la valeur à `subprocess.run(input=...)`.
- `control-plane/forgepilot/workflow.py:118-127` — `create_worktree` lève
  `Le worktree existe déjà` quand le répertoire est présent, et appelle
  `ensure_clean_repo` avant.
- `control-plane/forgepilot/cli.py:104-118` — la branche `execute` crée toujours
  un worktree ; il n'existe aucune commande qui réutilise l'existant.

**Fait mesuré et reproductible** : `claude -p` lit le prompt sur stdin. Vérifié
par `echo "..." | claude -p --output-format json`, qui rend `is_error: false` et
la réponse attendue. La correction de D1 n'est donc pas une hypothèse.

---

## Décisions de conception tranchées par le Planificateur

### D1 — Le prompt de Claude Code passe par stdin, jamais par argv

`_claude_argv` ne reçoit plus le prompt. Il construit l'invocation **sans** le
texte, et le prompt voyage par stdin jusqu'à `run_command`.

Contraintes exactes :

- L'argument `-p` **reste présent** (il commande le mode non interactif), mais
  sans valeur accolée : le texte vient de stdin.
- Tous les autres drapeaux restent **inchangés et dans le même ordre** :
  `--output-format json`, `--permission-mode plan`, `--tools <lecture seule>`,
  `--disallowedTools mcp__*`, `--safe-mode`, `--disable-slash-commands`,
  `--no-chrome`, `--no-session-persistence`.
- `Invocation` doit pouvoir transporter ce prompt jusqu'à l'exécution. Le
  Générateur choisit comment (champ supplémentaire sur `Invocation`, ou retour
  d'un couple) — mais `format_invocation` **ne doit jamais imprimer le prompt en
  clair** : l'aperçu sans `--run` affiche déjà `<prompt>`, ce comportement est
  conservé.
- Les trois rôles Claude (`plan`, `review`, et tout futur appel Claude) passent
  par ce même chemin. Aucun appel Claude ne conserve un prompt en argv.

**Ce qui n'est pas touché** : l'invocation de Cursor (`agent -p "<prompt>"`).
Son prompt est le plan, pas un diff ; il n'a pas débordé et le corriger sortirait
du périmètre de ce lot. C'est un non-objectif déclaré, pas un oubli.

### D2 — Une commande `iterate` qui réutilise le worktree existant

Nouvelle sous-commande :

```
forgepilot iterate <plan> --task-name <id> --repo <dépôt> [--run]
```

Contrat tranché ici :

- Elle **exige** que le worktree `.forgepilot/worktrees/<slug>` existe déjà et
  que sa branche soit `agent/<slug>`. S'il est absent : refus explicite disant
  d'employer `execute`. Elle ne crée **jamais** de worktree.
- Elle n'appelle **pas** `ensure_clean_repo` sur le dépôt principal : itérer sur
  une branche agent n'a aucune raison d'exiger que `master` soit propre. Elle
  vérifie en revanche que le worktree lui-même est dans un état git connu et le
  **rapporte** avant d'agir.
- Elle produit exactement la même invocation que `execute` (même prompt exécuteur,
  mêmes drapeaux, `--sandbox enabled` compris) — seule la création du worktree
  est omise. Le Générateur ne duplique pas le code : `executor_invocation` est
  réemployé tel quel.
- Sans `--run`, elle imprime l'invocation normalisée et ne lance rien, comme les
  autres commandes.
- Son résultat est persisté sous `.forgepilot/runs/` avec le rôle `executor`,
  comme `execute`.

### D3 — Preuve rouge d'abord, sur la vraie limite du système

Les tests ajoutés doivent **échouer avant la correction et passer après**. Deux
d'entre eux ne sont pas négociables :

1. **Le test de débordement.** Il construit un diff synthétique de plus de
   `131072` octets, fabrique l'invocation de relecture, et vérifie qu'**aucun
   élément d'argv ne dépasse `131072` octets**. Avant D1 il échoue ; après, il
   passe. Ce test lit la limite depuis le système
   (`32 * os.sysconf("SC_PAGESIZE")`), il ne recopie pas `131072` en dur.
2. **Le test d'itération sans worktree.** `iterate` sur un worktree absent doit
   refuser proprement (erreur du pilote), pas planter.

S'y ajoutent : un test que `iterate` réutilise le worktree existant sans en créer
un second, et un test que l'invocation d'`iterate` porte bien `--sandbox enabled`
(la garantie ADR-0013 ne doit pas s'évaporer sur le nouveau chemin).

### D4 — Périmètre de fichiers

**Autorisé :**

- `control-plane/forgepilot/workflow.py`
- `control-plane/forgepilot/cli.py`
- `control-plane/forgepilot/process.py` (seulement si stdin l'exige réellement)
- `control-plane/tests/test_workflow.py` — **uniquement pour ajouter** des tests
- `control-plane/README.md` — documenter `iterate` et le passage par stdin
- `harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables/**`
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée)

**Interdit :** les quatre tests existants de `test_workflow.py` (ils peuvent être
lus, jamais modifiés ni supprimés) ; `control-plane/prompts/**` ;
`control-plane/config.toml` ; `pipeline/**` ; `sim/**` ; `harness/*.py` ;
`harness/pipeline/**` ; `.github/**` ; `docs/adr/**` ; `VISION.md` ;
`ROADMAP.md` ; `HANDOFF.md` ; tout autre brief.

---

## Success Conditions

### SC1 — La relecture ne déborde plus, et la preuve est mesurée

- `longueur_argv_max_relecture` est la taille du plus grand élément d'argv
  produit par `review_invocation` sur un diff synthétique de plus de `131072`
  octets. Elle doit être **strictement inférieure** à
  `32 * os.sysconf("SC_PAGESIZE")`, cette borne étant **lue du système**, jamais
  recopiée.
- `octets_diff_du_test` est la taille du diff synthétique employé, rapportée avec
  la borne système pour dénominateur ; elle doit la dépasser, sinon le test ne
  prouve rien.
- `prompt_absent_de_argv` vaut `1` : le texte du prompt n'apparaît dans aucun
  élément d'argv.

### SC2 — Les garanties d'ADR-0013 survivent au changement

- `drapeaux_claude_inchanges` vaut `1` : l'invocation de relecture porte toujours
  `--permission-mode plan`, `--tools` en lecture seule, `--disallowedTools mcp__*`,
  `--safe-mode`, `--disable-slash-commands`, `--no-chrome`,
  `--no-session-persistence`, dans le même ordre qu'avant.
- `tests_existants_intacts` vaut `1` : les quatre tests d'origine de
  `test_workflow.py` sont présents et non modifiés, prouvé par
  `git diff origin/master...HEAD` sur ce fichier (seules des additions).
- `format_invocation_ne_fuit_pas_le_prompt` vaut `1` : l'aperçu sans `--run`
  n'imprime pas le texte du prompt.

### SC3 — `iterate` existe, réutilise, et refuse proprement

- `iterate_reutilise_worktree` vaut `1` : appelée sur un worktree existant, la
  commande ne crée aucun nouveau répertoire sous `.forgepilot/worktrees/`.
- `iterate_refuse_sans_worktree` vaut `1` : appelée sans worktree, elle rend une
  erreur du pilote avec un message qui nomme `execute` comme la commande à
  employer.
- `iterate_porte_le_sandbox` vaut `1` : son invocation contient
  `--sandbox enabled`.
- `iterate_sans_run_ne_lance_rien` vaut `1` : sans `--run`, elle imprime
  l'invocation et n'exécute aucun agent.

### SC4 — Preuve rouge d'abord

- `tests_ajoutes` est le nombre de tests ajoutés à `test_workflow.py`, rapporté
  avec le total de tests du fichier pour dénominateur.
- `tests_rouges_avant_correction` vaut au moins `2` : le test de débordement et
  le test d'itération sans worktree ont été **vus échouer** sur le code d'avant,
  et la sortie de cet échec est recopiée dans `deliverables/generator-log.md`.
  Un test qui n'a jamais échoué ne prouve rien (règle durement acquise n° `4`).
- `suite_control_plane_verte` : `python3 -m unittest discover -s control-plane/tests`
  passe, avec le nombre de tests exécutés pour dénominateur.

### SC5 — Le reste du dépôt ne bouge pas

- `fichiers_hors_perimetre_modifies` vaut `0`, mesuré par
  `git diff origin/master...HEAD --name-only` confronté à la liste D4.
- `harness/tests/` reste vert : `.venv/bin/python -m pytest harness/tests/ -q`,
  rapporté avec le nombre de tests collectés. Les neuf échecs connus de
  `test_run_unity.py` sous WSL2 (binaire Unity absent) sont **préexistants** et
  ne comptent pas comme régression ; les rapporter séparément.

### SC6 — La documentation dit ce que le pilote fait

- `control-plane/README.md` documente `iterate` dans la séquence du premier essai,
  et dit en une phrase que le prompt de Claude Code passe par stdin **et pourquoi**
  (la limite de `128` Ko par argument). Un instantané pré-édition est committé
  sous `deliverables/pre-edit/control-plane-README.md.orig`, déclaré en couple
  `must_differ_from` avec le README publié.

---

## Non-objectifs

1. **Ne pas toucher à l'invocation de Cursor.** Son prompt est le plan, pas un
   diff ; il n'a pas débordé.
2. **Ne pas ajouter de découpage ni de résumé du diff.** Si un diff est énorme,
   il passe entier par stdin. Le résumer serait une décision de fond non tranchée.
3. **Ne pas ajouter d'auto-fusion, ni de cron, ni de boucle.** Le pilote reste
   manuel (ADR-0013, `ROADMAP.md` étape `2`).
4. **Ne pas modifier `harness/pipeline/config.yaml`** ni réveiller le full-auto.
5. **Ne pas corriger les neuf tests Unity** en échec sous WSL2 : préexistants,
   hors sujet.

---

## Required Counters

`longueur_argv_max_relecture`, `octets_diff_du_test`, `prompt_absent_de_argv`,
`drapeaux_claude_inchanges`, `tests_existants_intacts`,
`format_invocation_ne_fuit_pas_le_prompt`, `iterate_reutilise_worktree`,
`iterate_refuse_sans_worktree`, `iterate_porte_le_sandbox`,
`iterate_sans_run_ne_lance_rien`, `tests_ajoutes`,
`tests_rouges_avant_correction`, `suite_control_plane_verte`,
`fichiers_hors_perimetre_modifies`.

Chacun doit être imprimé **avec son dénominateur** par un script committé sous
`deliverables/measure_022.py`, exécuté depuis la racine du dépôt, et déclaré dans
`deliverables/manifest.json` avec un `sample_size` réel.

---

## Dérogations acceptables

Une dérogation n'est recevable qu'accompagnée de **la commande rejouée et de sa
sortie d'erreur**. Cas prévu : si `claude -p` refusait le prompt par stdin sur la
machine du lot, la dérogation doit porter la commande exacte et le message. À
l'écriture de ce brief, ce chemin est **vérifié fonctionnel** :
`echo "..." | claude -p --output-format json` rend `is_error: false`.

Aucune dérogation ne peut porter sur : le maintien des quatre tests existants, la
lecture seule de Claude Code, le sandbox de Cursor, ou l'interdiction de fusion
automatique.
