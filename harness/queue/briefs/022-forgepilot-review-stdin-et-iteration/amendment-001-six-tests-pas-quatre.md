# Amendement 001 — ils sont six, pas quatre

**Authored**: 2026-08-15T18:20:00Z
**Author**: forge-planificateur

> **Note de transparence.** L'acteur réel de cet amendement est Claude Code
> endossant le rôle natif `forge-planificateur`, sans suffixe ajouté à la
> signature, pour que le contrôle mécanique `verdict_is_not_self_authored`
> puisse comparer les acteurs de part et d'autre du lot. Le défaut corrigé ici
> est un défaut **du brief**, découvert avant toute écriture de code.

**Cet amendement est antérieur au code.** Aucun travail n'a encore été produit
pour le brief 022 : le Générateur n'a pas été invoqué. Il n'y a donc rien à
requalifier — seulement un texte à rendre exact avant qu'il n'instruise
quiconque.

---

## 1. Le défaut

Le brief 022 et sa rubrique parlent trois fois des « **quatre** tests
existants » de `control-plane/tests/test_workflow.py`. Le fichier en contient
**six**. Mesuré sur `master` à `93ecb11` :

```bash
cd control-plane && python3 -m unittest discover -s tests -v
```

→ `Ran 6 tests`, `OK`. Les six sont :

| test | ce qu'il encode |
|---|---|
| `test_doctor_refuses_anthropic_api_billing` | aucune clé d'API : `ANTHROPIC_API_KEY` fait refuser `doctor` |
| `test_claude_code_is_read_only` | `--permission-mode plan`, `--tools` en lecture seule, `--safe-mode` |
| `test_cursor_is_only_executor_and_uses_sandbox` | Cursor seul exécutant, `--sandbox enabled` |
| `test_dirty_repo_refuses_worktree` | un dépôt sale ne produit pas de worktree |
| `test_worktree_branch_is_agent_scoped` | la branche est `agent/<slug>` |
| `test_publish_refuses_non_agent_branch` | pas de publication hors préfixe `agent/` |

L'intention du brief n'a jamais été ambiguë — **aucun test préexistant n'est
modifié ni supprimé, seules des additions sont recevables**. Mais un nombre faux
dans une condition de succès est précisément ce qui fait dérailler un
Évaluateur qui reconstruit ses compteurs, ou qui fait rejeter un travail correct
pour une raison de forme.

## 2. Ce que l'amendement change

Partout où le brief 022 et son `eval-rubric.md` écrivent « les **quatre** tests
existants », lire « les **six** tests existants ». Les trois emplacements :

1. `brief.md` › « Ce que ce lot doit préserver », dernier paragraphe.
2. `brief.md` › SC2, compteur `tests_existants_intacts`.
3. `eval-rubric.md` › Échecs disqualifiants, point `2`.

Le compteur `tests_existants_intacts` vaut toujours `1` ou `0`, et se lit
désormais : **les six tests d'origine sont présents et non modifiés**, prouvé par
`git diff origin/master...HEAD -- control-plane/tests/test_workflow.py` ne
contenant que des additions.

Le compteur `tests_ajoutes` prend pour dénominateur le total de tests du fichier
**après** le lot ; sa base de départ est donc `6`, pas `4`.

## 3. Ce que l'amendement ne change pas

- Le fond : aucun test préexistant modifié, additions seulement.
- Les décisions D1 à D4, les six conditions de succès, les cinq non-objectifs.
- Les échecs disqualifiants autres que le point `2`.
- Les en-têtes `Authored` d'origine du `brief.md` et de l'`eval-rubric.md`.

## 4. Une observation jointe, qui n'est pas une modification du brief

`pytest` était absent du `.venv` racine de la machine du lot (il n'y vivait que
`forgepilot`), ce qui rendait SC5 et la baseline Unity impossibles à mesurer.
Réparation d'environnement, hors dépôt (`.venv` est git-ignoré) :
`.venv/bin/pip install pytest`. Baseline rejouée **avant** le lot, sur `master` à
`93ecb11` :

```bash
.venv/bin/python -m pytest harness/tests/ -q
```

→ `9 failed, 355 passed`. Les neuf échecs sont tous dans
`harness/tests/test_run_unity.py` (binaire Unity absent sous WSL2,
`powershell.exe` visible via `/mnt/c/` donc pas de SKIP). C'est le préexistant
que la rubrique demande à l'Évaluateur de constater ; il est ici daté et chiffré.
