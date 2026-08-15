# Journal du Générateur — Brief 022

**Author**: forge-generateur
**Date**: 2026-08-15

**Rôle** : Générateur (Cursor), lot `agent/forgepilot-stdin`.
**Amendement 001** : les tests d'origine à préserver sont **six**, pas quatre
(`test_doctor_refuses_anthropic_api_billing`, `test_claude_code_is_read_only`,
`test_cursor_is_only_executor_and_uses_sandbox`, `test_dirty_repo_refuses_worktree`,
`test_worktree_branch_is_agent_scoped`, `test_publish_refuses_non_agent_branch`).
Le brief.md et eval-rubric.md portent encore le mot « quatre » ; on lit six.

## Rouge avant correction (règle durement acquise n° 4)

Les six tests ajoutés ont été écrits **avant** toute modification de
`workflow.py` / `cli.py`. Les deux non négociables ont été rejoués sur le code
d'avant (prompt encore en argv ; sous-commande `iterate` absente).

Commande :

```bash
cd /home/liagrep/src/ForgeHistory/.forgepilot/worktrees/forgepilot-stdin
PYTHONPATH=control-plane python3 -m unittest \
  control-plane.tests.test_workflow.WorkflowTests.test_review_keeps_argv_under_system_arg_limit \
  control-plane.tests.test_workflow.WorkflowTests.test_iterate_without_worktree_refuses_naming_execute \
  -v
```

Sortie d'échec (recopiée telle quelle) :

```
test_review_keeps_argv_under_system_arg_limit (control-plane.tests.test_workflow.WorkflowTests.test_review_keeps_argv_under_system_arg_limit)
SC1 : aucun élément d'argv ne dépasse 32 × SC_PAGESIZE (lu du système). ... FAIL
test_iterate_without_worktree_refuses_naming_execute (control-plane.tests.test_workflow.WorkflowTests.test_iterate_without_worktree_refuses_naming_execute)
SC3 : iterate sans worktree rend 2 et nomme execute (pas SystemExit). ... FAIL

======================================================================
FAIL: test_review_keeps_argv_under_system_arg_limit (control-plane.tests.test_workflow.WorkflowTests.test_review_keeps_argv_under_system_arg_limit)
SC1 : aucun élément d'argv ne dépasse 32 × SC_PAGESIZE (lu du système).
----------------------------------------------------------------------
Traceback (most recent call last):
  File "/home/liagrep/src/ForgeHistory/.forgepilot/worktrees/forgepilot-stdin/control-plane/tests/test_workflow.py", line 135, in test_review_keeps_argv_under_system_arg_limit
    self.assertLess(max_arg, bound)
AssertionError: 131633 not less than 131072

======================================================================
FAIL: test_iterate_without_worktree_refuses_naming_execute (control-plane.tests.test_workflow.WorkflowTests.test_iterate_without_worktree_refuses_naming_execute)
SC3 : iterate sans worktree rend 2 et nomme execute (pas SystemExit).
----------------------------------------------------------------------
Traceback (most recent call last):
  File "/usr/lib/python3.12/argparse.py", line 1941, in parse_known_args
    namespace, args = self._parse_known_args(args, namespace)
                      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 2144, in _parse_known_args
    positionals_end_index = consume_positionals(start_index)
                            ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 2121, in consume_positionals
    take_action(action, args)
  File "/usr/lib/python3.12/argparse.py", line 2001, in take_action
    argument_values = self._get_values(action, argument_strings)
                      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 2554, in _get_values
    self._check_value(action, value[0])
  File "/usr/lib/python3.12/argparse.py", line 2600, in _check_value
    raise ArgumentError(action, msg % args)
argparse.ArgumentError: argument command: invalid choice: 'iterate' (choose from 'doctor', 'plan', 'execute', 'review', 'publish')

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File "/home/liagrep/src/ForgeHistory/.forgepilot/worktrees/forgepilot-stdin/control-plane/tests/test_workflow.py", line 148, in test_iterate_without_worktree_refuses_naming_execute
    code = main(
           ^^^^^
  File "/home/liagrep/src/ForgeHistory/.forgepilot/worktrees/forgepilot-stdin/control-plane/forgepilot/cli.py", line 74, in main
    args = parser().parse_args(argv)
           ^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 1908, in parse_args
    args, argv = self.parse_known_args(args, namespace)
                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "/usr/lib/python3.12/argparse.py", line 1943, in parse_known_args
    self.error(str(err))
  File "/usr/lib/python3.12/argparse.py", line 2677, in error
    self.exit(2, _('%(prog)s: error: %(message)s\n') % args)
  File "/usr/lib/python3.12/argparse.py", line 2664, in exit
    _sys.exit(status)
SystemExit: 2

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File "/home/liagrep/src/ForgeHistory/.forgepilot/worktrees/forgepilot-stdin/control-plane/tests/test_workflow.py", line 159, in test_iterate_without_worktree_refuses_naming_execute
    self.fail(
AssertionError: SystemExit(2) : iterate doit rendre 2 via main, pas lever SystemExit (sous-commande absente = code d'avant).

----------------------------------------------------------------------
Ran 2 tests in 0.005s

FAILED (failures=2)
```

Rejoué ensuite par `git stash push -- control-plane/forgepilot/workflow.py
control-plane/forgepilot/cli.py` puis les mêmes deux tests : même forme d'échec
(overflow 131633 ≥ 131072 ; `SystemExit` argparse faute de sous-commande
`iterate`). `git stash pop` restaure le vert (12 OK).

Borne système lue au même moment :

```bash
python3 -c "import os; print(32*os.sysconf('SC_PAGESIZE'))"
# → 131072
```

## Vert après correction

Implémentation D1 (prompt Claude via champ `Invocation.prompt` + stdin) et D2
(`existing_worktree` + sous-commande `iterate`). `process.py` non modifié.
Cursor garde `-p "<prompt>"` en argv.

```bash
cd control-plane && python3 -m unittest discover -s tests -v
# Ran 12 tests … OK

.venv/bin/python -m unittest discover -s control-plane/tests -v
# Ran 12 tests … OK
```

Note SC4 : `python3 -m unittest discover -s control-plane/tests` depuis la
racine **sans** `PYTHONPATH` importe le `forgepilot` editable du dépôt parent
(code d'avant) et échoue. Commande réellement verte :
`.venv/bin/python -m unittest discover -s control-plane/tests` après
`.venv/bin/pip install -e ./control-plane`.

## Fichiers touchés (périmètre D4)

- `control-plane/forgepilot/workflow.py`
- `control-plane/forgepilot/cli.py`
- `control-plane/tests/test_workflow.py` (additions seulement)
- `control-plane/README.md`
- `harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables/**`
- `harness/queue/cost-ledger.jsonl` (une ligne)

## Mesure

```bash
.venv/bin/python harness/queue/briefs/022-forgepilot-review-stdin-et-iteration/deliverables/measure_022.py
```
