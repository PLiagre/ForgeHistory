# Journal du Générateur — Brief 023

**Author**: forge-generateur
**Date**: 2026-08-16

**Rôle** : Générateur (Cursor), lot `agent/023-forgepilot-modele-et-effort-par-role`.

## Base Git constatée

```
$ git rev-parse HEAD
e6fdd28f901e019280f8b2463cb03b9f1fdcb4f2

$ git status --porcelain
(arbre propre au démarrage)

$ git log -1 --oneline
e6fdd28 ADR-0014 accepté : Hermes déclenche et rend compte, Claude juge
```

Base réelle : `master` @ `e6fdd28` (identique à `origin/master`). Le lot 022
est déjà dans master (`93ecb11`, `75b3dd0`) ; `workflow.py` passe le prompt par
stdin. Situation applicable : « master une fois #108 fusionnée ». Pas de départ
depuis `agent/forgepilot-stdin`.

## Hypothèse retenue (risque D3 / --effort)

`--effort` est **déclaré** sur les quatre sous-commandes `plan`, `review`,
`execute`, `iterate`, mais n'est **accepté** que par `plan` et `review`. Sur
`execute` et `iterate`, le drapeau ne sert qu'à produire un `PilotError`
explicatif (code 2 via `main`), pas un `SystemExit` argparse.

## Rouge avant correction (règle n° 4)

Copie jetable hors dépôt ; `config.py`, `workflow.py` et `cli.py` restaurés
depuis `e6fdd28` ; tests neufs conservés. Aucun `git stash` dans le worktree.

Commande :

```bash
STAGING=$(mktemp -d /tmp/mesure023-red-XXXXXX)
cp -a control-plane "$STAGING/control-plane"
# restauration e6fdd28 des trois modules
cd "$STAGING/control-plane"
PYTHONPATH="$STAGING/control-plane" python3 -m unittest discover -s tests -v
```

Résultat global : `Ran 25 tests` — `FAILED (failures=5, errors=6)` ;
parmi les tests ajoutés, **11** échecs/erreurs (compteur dérivé, pas une
recherche de mots dans ce journal).

Sortie d'échec des trois tests non négociables de D7 (recopiée) :

```
======================================================================
ERROR: test_cli_model_flag_beats_roles_section (test_workflow.WorkflowTests.test_cli_model_flag_beats_roles_section)
D7.2 : --model passé à l'appel l'emporte sur [roles.*].
----------------------------------------------------------------------
Traceback (most recent call last):
  File "/tmp/mesure023-red-4vi0aF/control-plane/tests/test_workflow.py", line 389, in test_cli_model_flag_beats_roles_section
    invocation = plan_invocation(
                 ^^^^^^^^^^^^^^^^
TypeError: plan_invocation() got an unexpected keyword argument 'model'

======================================================================
ERROR: test_two_claude_roles_can_carry_distinct_models (test_workflow.WorkflowTests.test_two_claude_roles_can_carry_distinct_models)
D7.1 : plan et review portent deux --model différents.
----------------------------------------------------------------------
Traceback (most recent call last):
  File "/tmp/mesure023-red-4vi0aF/control-plane/tests/test_workflow.py", line 362, in test_two_claude_roles_can_carry_distinct_models
    plan_model = plan_inv.argv[plan_inv.argv.index("--model") + 1]
                               ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
ValueError: tuple.index(x): x not in tuple

======================================================================
FAIL: test_effort_refused_on_cursor_execute_and_iterate (test_workflow.WorkflowTests.test_effort_refused_on_cursor_execute_and_iterate)
D7.3 : --effort sur execute/iterate rend 2 via main, avec explication.
----------------------------------------------------------------------
Traceback (most recent call last):
  File "/tmp/mesure023-red-4vi0aF/control-plane/tests/test_workflow.py", line 418, in test_effort_refused_on_cursor_execute_and_iterate
    code = main(argv)
           ^^^^^^^^^^
  File "/tmp/mesure023-red-4vi0aF/control-plane/forgepilot/cli.py", line 84, in main
    args = parser().parse_args(argv)
           ^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 1911, in parse_args
    self.error(msg % ' '.join(argv))
  File "/usr/lib/python3.12/argparse.py", line 2677, in error
    self.exit(2, _('%(prog)s: error: %(message)s\n') % args)
  File "/usr/lib/python3.12/argparse.py", line 2664, in exit
    _sys.exit(status)
SystemExit: 2

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File "/tmp/mesure023-red-4vi0aF/control-plane/tests/test_workflow.py", line 420, in test_effort_refused_on_cursor_execute_and_iterate
    self.fail(
AssertionError: SystemExit(2) : execute --effort doit rendre 2 via PilotError, pas SystemExit argparse.
```

## Après correction

Commande suite :

```bash
cd control-plane
PYTHONPATH=. python3 -m unittest discover -s tests -v
```

Résultat : `Ran 25 tests in 0.077s` — `OK`.

`Settings` à dix champs : constructible sans `TypeError`.

Aperçu `plan` avec `--model claude-opus-5 --effort xhigh` : payload porte
`model`, `effort`, `prompt` = `<prompt>`, argv contient `--model` et
`--effort`.

`execute` / `iterate` avec `--effort high` : code 2, message
« Cursor cuit l'effort dans le nom du modèle… ».

`[roles.zorglub]` et `effort` sous `[roles.executor]` : refus `PilotError`
lus à l'œil.

## harness/tests/

Collecté : **364** tests.
Exécution actuelle : **9 failed, 355 passed** (tous dans `test_run_unity.py`).

Rejoué sur `e6fdd28` (worktree détaché) : **9 failed, 7 passed** sur
`test_run_unity.py` — mêmes neuf échecs préexistants (binaire Unity absent
sous WSL2). Pas de régression nouvelle.

## Mesure

```bash
python3 harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/deliverables/measure_023.py
```

Vingt-et-un compteurs imprimés avec dénominateur ; `tests_rouges_avant_correction=11`
(dénominateur tests ajoutés = 13) ; `suite_control_plane_verte=1`
(dénominateur tests exécutés = 25) ; `fichiers_hors_perimetre_modifies=0`.

## Fichiers touchés (périmètre D8)

- `control-plane/forgepilot/config.py`
- `control-plane/forgepilot/workflow.py`
- `control-plane/forgepilot/cli.py`
- `control-plane/config.toml`
- `control-plane/tests/test_workflow.py` (additions seulement ; 0 ligne `-` hors en-têtes)
- `control-plane/README.md`
- `harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/deliverables/**`
- `harness/queue/cost-ledger.jsonl` (une ligne)

## Non fait

Pas de fusion, pas de push, pas de modification de secret, pas de verdict
d'Évaluateur (hors rôle).
