# Verdict — Brief `009`, **LOT 009a ONLY** (mode split, fail-closed guard, ADR-`0007`)

**Authored**: 2026-08-10T22:20:00Z
**Author**: forge-evaluateur

> **Scope of this verdict.** Brief `009` is `NEEDS_SPLIT`. Only Lot 009a has
> been generated (commits `244a4f2` + `1f83231`). This document judges
> Success Conditions SC1–SC7 and the five 009a counters. SC8–SC13 (Lot 009b)
> and SC14–SC21 (Lot 009c), and their counters, are recorded as
> `NOT_IN_SCOPE_THIS_LOT` — neither passed nor failed here, simply not
> attempted yet, exactly as brief `008`'s verdict recorded its own
> ungenerated lots.
> **Brief `009` as a whole is NOT complete, and an ACCEPT on 009a would not
> close it.** This verdict is a REJECT on 009a; brief `009` remains open on
> all three lots.

## Mechanical Gate Result

Command: `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`

Run twice by me: once before this verdict existed, once after writing it.
Both outputs captured verbatim, cited by path rather than re-typed (hard-won
rule `12`), as `.txt` not `.log` (`.gitignore` excludes `*.log`):

- `harness/queue/briefs/009-full-auto-agent-invocation/deliverables/evaluateur-gate-rerun.txt`

The pre-verdict run failed exactly two rows, `verdict_numbers_traceable` and
`verdict_is_not_self_authored`, both because `verdict.md` — my own artifact —
did not exist. That is not a finding against the Générateur; the
generator-log said so itself and was right.

The post-verdict run, captured in the file above, is the operative one: every
row PASS, `VERDICT: ACCEPT`, exit `0`.

**That mechanical ACCEPT does not override the REJECT below, and cannot.** The
gate is tier-`1` only: it checks manifest shape, mtimes, capture-pair
divergence, waiver form, sample sizes, the `py`-not-bare-alias rule, verdict
authorship, and tracked-file status. It has no opinion on whether a document
still names a value the code refuses, on whether a sentence in a committed log
is true, or on whether a guard is fail-closed on an input nobody declared. A
mechanical REJECT would be final; a mechanical ACCEPT is merely necessary
(hard-won rule 7, "presence is not function").

**The gate is necessary but not sufficient** (hard-won rule 7, "presence is
not function"). Every counter below was re-derived by my own command against
source data, not read off `manifest.json`.

## Per-Rubric-Line Verdict — Lot 009a

Rubric rows are `eval-rubric.md` § "Lot 009a". Evidence is what **I**
personally ran, in this session, on this machine.

| # | Success Condition | PASS/FAIL | Evidence I personally ran |
|---|---|---|---|
| 1 | bare `full_auto` refused while forge-run unwired | **PASS** | `py -m pytest harness/tests/test_mode_guard.py -v` → all `9` tests pass, including `test_bare_full_auto_refused_while_forgerun_unwired`, which passes the **real** `.github/workflows/pipeline-forge-run.yml` (I confirmed independently that the real file still contains `TODO(operator`, count `1`). Red-first proved from **outside** the repo: I copied `harness/`, `.github/`, `docs/` to a scratch tree, inserted an unconditional early `return` at the top of `validate_mode`, and the test went red with `Failed: DID NOT RAISE ModeGuardError` — the correct reason. Working tree never mutated (`git status --porcelain` empty before and after). |
| 2 | `full_auto` accepted once the fixture shows forge-run wired | **PASS** | Same run; `test_full_auto_accepted_once_forgerun_wired` uses a real `tmp_path` **file copy** of the workflow with the marker replaced, and passes that path into the guard, which does a genuine `read_text` on it — **no monkeypatch, no stub of the file read, and never the real workflow file**. Red-first proved in the same scratch tree by hardcoding a permanent refusal inside the `CONDITIONALLY_VALID` branch: `test_full_auto_accepted_once_forgerun_wired` went red. Both branches of the pair are therefore genuinely exercised — the 008a iteration-1 defect is **not** repeated here. |
| 3 | single-commit transition of `mode:` | **PASS** | Reconstructed by my own commands, without running the Générateur's script. `git rev-list --reverse 244a4f2~1..HEAD` returns two commits; only `244a4f2` touches `harness/pipeline/config.yaml` (`git log --oneline 244a4f2~1..HEAD -- harness/pipeline/config.yaml`). `git show <c>:harness/pipeline/config.yaml` per commit: parent = `full_auto`, `244a4f2` = `full_auto_decision_only`, `1f83231` = `full_auto_decision_only`. Diff lines in the range: exactly `-mode: full_auto` / `+mode: full_auto_decision_only`. No intermediate bare value exists in the lot's own range. The guard module and the config rewrite are in the **same** commit (`git show --stat 244a4f2` lists both `harness/pipeline/full_auto_mode_guard.py` (A) and `harness/pipeline/config.yaml` (M)) — SC3's real constraint holds on the history, not only in the narrative. |
| 4 | `auto_policy.yaml`'s documentation scalar updated | **PASS** | The file's top-level scalar now reads `full_auto_decision_only`, matching `config.yaml`. I verified the premise still holds after this lot: `policy_loader.load_auto_policy` does parse the top-level scalar into its returned dict, but a repo-wide grep for `["mode"]` / `.get("mode")` finds **no** consumer of it — the only readers of a `mode` key anywhere in production code are the new guard's own `main()` and its tests. Nothing began enforcing `auto_policy.yaml`'s scalar in this lot, so the two-file consistency remains documentation-level, as SC4 assumes. |
| 5 | ADR-`0007` written; ADR-`0006` not rewritten | **PASS** | Verified by blob, not by reading: `docs/adr/0006-full-auto-agent-pipeline.md` hashes to the **same** object at `244a4f2~1`, at `244a4f2`, at `HEAD`, and in the worktree (`git rev-parse` ×`3` + `git hash-object`); `git log --all` shows that file has exactly one commit in its whole history (`8be10d8`, brief `006`). ADR-`0007` carries a non-blank `**Status**:` line and follows `docs/adr/template.md` section-for-section (Context / Decision / Alternatives Considered ≥`1` / Consequences → Positive, Negative, Risks), plus Date/Status/Deciders frontmatter. Its Decision section states explicitly that ADR-`0006` is narrowed, not reversed. |
| 5b | `docs/adr/README.md` rows | **PASS** | `git show 244a4f2 -- docs/adr/README.md`: exactly two `+` lines, zero `-` lines, both appended to the existing table body immediately after the `0005` row, one for `0006` and one for `0007`, each with the four columns the table already uses. They are real table rows, not text elsewhere in the file. |
| 6 | activation doc corrected | **FAIL** | The mechanical half passes: the pre/post pair for `docs/rules/full-auto-pipeline.md` genuinely differs (blob `e03bcb5…` → `b576944…`), and step `3`'s literal text now names `full_auto_decision_only`. The manual half fails on SC6's second clause — "the doc must not keep telling a reader to set a value the code now refuses". My own `grep -n "full_auto" docs/rules/full-auto-pipeline.md` shows the section **heading** at line `77` still reads ``## How to activate `mode: full_auto` `` — the title of the very activation procedure whose step `3` now sets a different value — and line `109` still reads "This is the same file `mode: full_auto` sets". Worse, `deliverables/generator-log.md` asserts of this exact file: "`grep -n "full_auto"` after the edit shows exactly the one corrected line; no other stale mention of the bare value remains anywhere in the file". That claim is false against the command it cites. See Record Integrity below. |
| 7 | full suite green | **PASS** | I re-ran it myself: `py -m pytest harness/tests/ -q` → `280` passed, `0` failed. `--collect-only` confirms `280` collected. `git diff --name-status 244a4f2~1..HEAD -- harness/tests/` shows a single line, `A harness/tests/test_mode_guard.py`: **no pre-existing test was modified, weakened, or deleted** to make the suite green. Baseline `271` + `9` new = `280`, arithmetic consistent. The selection `py -m pytest harness/tests/ -k "mode_guard or mode_split or full_auto" -q` → `12` passed, `268` deselected. |
| — | `must_differ_from` pairs (`config.yaml`, `auto_policy.yaml`, `full-auto-pipeline.md`) | **PASS** | Recomputed with `git hash-object` on each `.orig` and each live file: all three pairs differ. Snapshot honesty independently verified — each `.orig` is **byte-identical to the blob at `244a4f2~1`**, i.e. to the true pre-lot state of this branch. |

## Reconstructed counters — claimed vs. my own reconstruction

Each re-derived by a command of mine. For `config_mode_single_commit_transition_count`
I deliberately did **not** execute `deliverables/measure_config_mode_transitions.py`;
I read it only to understand the definition, then measured with `git rev-list` /
`git show` / `git log -p` directly.

| counter | claimed | my reconstruction | agree? |
|---|---|---|---|
| `mode_full_auto_bare_rejected_test_count` | 1 | 1 — `test_bare_full_auto_refused_while_forgerun_unwired`, re-run green, proved red when the guard is neutralised | yes |
| `mode_full_auto_accepted_when_forgerun_wired_test_count` | 1 | 1 — `test_full_auto_accepted_once_forgerun_wired`, real fixture file, proved red when refusal is hardcoded | yes |
| `config_mode_single_commit_transition_count` | 2 | 2 — one commit touches `config.yaml` in `244a4f2~1..HEAD`; the `mode:` line takes exactly the two distinct values `full_auto` (removed once) and `full_auto_decision_only` (added once) | yes |
| `adr_0007_status_field_present` | 1 | 1 — one non-blank `**Status**:` line in `docs/adr/0007-full-auto-mode-split.md` | yes |
| `adr_readme_rows_added_count` | 2 | 2 — exactly two added table rows in `docs/adr/README.md`, zero removed | yes |

All five agree. The fifth counter's provenance (measured by the orchestrator
after the commit, then re-run by the Générateur session) is disclosed in both
`manifest.json`'s `command` field and the generator-log addendum. **Ruling: the
disclosure is correct and the refusal to claim it during the session was the
right behaviour** — the counter's own definition needs a commit range that did
not exist yet, and hard-won rule 8's "declare, never guess" applies. I accepted
neither party's number: mine is independent and matches.

## Adversarial probes of `validate_mode` — my own, not the orchestrator's

I re-ran every probe the orchestrator reported, plus `18` it did not, driving
the real module (no stubbing). Refused as claimed: real repo state, default
argument, missing file, path pointing at a directory, empty-string path,
`None`, `"FULL_AUTO"`, `"Full_Auto"`, trailing space, trailing newline,
trailing tab, leading non-breaking space, a Cyrillic-homoglyph `full_аuto`,
`True`, `0`, a `list`, `bytes`, a `str` **subclass** carrying `full_auto`, an
unrelated file containing `TODO(operator` only in a comment, and the empty
string. Accepted, correctly: `manual` and `full_auto_decision_only`.

**One permissive acceptance found, and it is one the record claims does not
exist.** With `forge_run_workflow` pointing at an existing but **empty** file,
`validate_mode("full_auto", …)` returns `None` — a silent acceptance. Same for
a whitespace-only file, and for any truncated workflow file that no longer
carries the marker. Both `244a4f2`'s commit message ("refuses on every degraded
path: … a path pointing at an empty file") and `deliverables/generator-log.md`'s
addendum ("probed against four degraded workflow-file inputs … and refused on
all four") state the opposite of what the code does. Reproduction and required
fix: `feedback/feedback-009a.md`, blocker B2.

Second, lower-severity: a workflow file that is not valid UTF-`8` raises
`UnicodeDecodeError`, which is not an `OSError` and so escapes the module's own
`except OSError` fail-closed handler. The outcome is still a refusal (uncaught
exception, non-zero exit), so it is **not** permissive — but a caller catching
`ModeGuardError`, which is the contract the docstring publishes, gets a
traceback instead of a clean refusal.

## Is the guard actually plugged in? — the question I was asked not to soften

Honest answer, from my own grep of every `.py`, `.yml` and `.yaml` in the repo:
**no `pipeline-*.yml` workflow, no `orchestrator.py` path, and no
`policy_loader.py` path calls `validate_mode`.** The guard has exactly two
automatic invocation routes today:

1. `harness/tests/test_mode_guard.py::test_config_yaml_current_mode_is_now_full_auto_decision_only`,
   which runs the guard against the **live** `config.yaml` — and
   `.github/workflows/harness-ci.yml` runs the whole harness suite on every
   `push` and `pull_request`. So setting `config.yaml`'s `mode` back to a bare
   `full_auto` **does** turn CI red today. That is a real, non-vacuous
   enforcement path, and it is the reason I do not fail SC1 on wiring.
2. A `main()` CLI in the module itself, which nothing invokes.

**But the promise is narrower than a reader of ADR-`0007` would assume**: no
workflow consults `mode` at run time, so `mode` is still not a kill switch for
any `pipeline-*.yml` job. Brief `009` says so itself ("points jugés
sous-spécifiés" (c)) and assigns that work to Lot 009c SC15. It is deferred by
design, not skipped — but it must not be described as done anywhere until 009c
lands, and one line in this lot already comes close to describing it as done
(blocker B3).

## Boundary Violations / Non-Goals — checked by diff, not by declaration

`git diff --name-status 244a4f2~1..HEAD` returns `16` paths, all inside this
lot's declared file set plus this brief's own `deliverables/` and one appended
line to `harness/queue/cost-ledger.jsonl` (ordinary harness bookkeeping).
Specifically verified:

- **No `.github/` file touched at all** (`git diff --name-only … -- .github/`
  → empty). Lot 009a's Non-Goal holds; `pipeline-audit.yml` and
  `pipeline-forge-run.yml` invocation bodies are untouched.
- **No Lot 009b/009c file touched** (`ci_budget_guard.py`,
  `ci-budget-ledger.jsonl`, `pipeline-challenge.yml` → empty diff).
- **`docs/adr/0006-full-auto-agent-pipeline.md` byte-identical**, proved by
  blob hash at four points in history.
- **No `gh issue create` anywhere** in this lot's files.
- **No PyYAML import added.** The single `import yaml` match under
  `harness/**.py` is inside `policy_loader.py`'s docstring, from `8be10d8`,
  and explains why PyYAML is *not* used.
- No waiver claimed; `manifest.json`'s `waivers` array is present and empty,
  which is correct — both waiver rows in brief.md are scoped to Lot 009c.

**No Non-Goal violation found.**

## Record Integrity — where this submission fails

Two statements in committed deliverables are false against the artifacts they
describe. I reproduced both.

1. `deliverables/generator-log.md`: "no other stale mention of the bare value
   remains anywhere in the file" (about `docs/rules/full-auto-pipeline.md`).
   Lines `77` and `109` of that file are exactly such mentions, and line `77`
   is the activation procedure's own heading. This is a verification claim
   presented as the result of a `grep` that does not produce it.
2. `244a4f2`'s commit message and `generator-log.md`'s addendum: the guard
   "refused on all four" degraded inputs including an empty file. It accepts
   the empty file.

Item `2` is relayed from the orchestrator and honestly attributed as such by
the Générateur, who explicitly declined to treat it as self-certifying. That
attribution is good practice and I credit it. It does not make the sentence
true, and the sentence is now in the permanent record of a lot whose entire
purpose is fail-closed behaviour.

## Overall Verdict: **REJECT** (Lot 009a)

The functional core of this lot is genuinely solid and does **not** need to be
redone: the guard is correct on every input the brief actually specifies, both
branches of the SC1/SC2 pair are proven and I proved each red from outside the
repo, the single-commit constraint holds on real history, ADR-`0006` is
untouched to the byte, all five counters reconstruct exactly, no Non-Goal is
violated, and the suite is `280`/`0` with no pre-existing test edited. That is
a materially better first submission than 008a's first iteration.

It is rejected on rubric row `6` plus record integrity: the activation document
still names the refused value in its own procedure heading, the log claims a
grep result that the grep contradicts, and the committed record asserts a
fail-closed behaviour that I disproved by executing the module. Three blockers,
all small and surgical, in `feedback/feedback-009a.md`.

## Lots 009b and 009c

| lot | Success Conditions | status |
|---|---|---|
| 009b | SC8–SC13, `6` counters | `NOT_IN_SCOPE_THIS_LOT` |
| 009c | SC14–SC21, `5` counters | `NOT_IN_SCOPE_THIS_LOT` |

Neither passed nor failed here. Per `eval-rubric.md`'s own Overall Verdict
Rule, 009c may not be evaluated until **both** 009a and 009b have ACCEPTed.

## What Improved Since Last Iteration

This is 009a's first iteration, so the comparison is against the previous
brief's lesson rather than a previous submission of this one:

- **The 008a iteration-1 defect is genuinely closed by construction.** That
  REJECT was for a guard whose branches were not both exercised. Here both
  branches exist, use the real code path, and I independently proved each one
  red by neutralising the guard in the opposite direction.
- **A counter that could not honestly be measured was declared, not
  fabricated.** The Générateur omitted `config_mode_single_commit_transition_count`
  rather than guessing it, wrote the exact post-commit command down, and first
  validated its measuring script against an unrelated, genuine single-commit
  transition already in history (brief `006`) so the script could not be
  coincidentally right. That is hard-won rules 8 and `10` applied correctly and
  it deserves saying.
- **Pre-fix snapshots were compared to the right baseline.** I checked the
  reasoning rather than accepting it: the branch is `17` ahead / `8` behind
  `origin/master`, `auto_policy.yaml` legitimately diverges there because of
  lot 008b, and all three `.orig` files are byte-identical to the blob at
  `244a4f2~1`. Comparing to `HEAD` was correct and hid nothing.

## What Regressed Since Last Iteration

Nothing regressed. `280`/`0` with no pre-existing test touched, and no
previously-accepted artifact was altered.

## Feedback for Next Iteration

Full detail, with my exact reproductions, in
`harness/queue/briefs/009-full-auto-agent-invocation/feedback/feedback-009a.md`.
Summary: fix the two residual bare-`full_auto` mentions in
`docs/rules/full-auto-pipeline.md` and correct the log sentence that claims
they are absent (B1); make the guard refuse an empty/whitespace-only workflow
file and add the test, then correct the record that says it already does (B2);
scope `config.yaml`'s new comment so it stops saying the challenge maillon is
wired when Lot 009c has not run (B3). Do not touch the guard's accepted
inputs, the tests, the counters, or the commit shape — they are correct.

---

# Réévaluation — lot 009a, itération 2 (`a16b18c`)

**Authored**: 2026-08-10T20:59:27Z
**Author**: forge-evaluateur-codex

Cette section s'ajoute au REJECT précédent ; elle ne le remplace pas. Je juge
ici la correction postérieure `a16b18c` et reconstruis les preuves sans
utiliser le script de mesure du Générateur.

## Gate et tests rejoués

- `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`
  → dix lignes `[PASS]`, `VERDICT: ACCEPT`, sortie conservée dans
  `deliverables/evaluateur-iteration-2-gate.txt`.
- `py -m pytest harness/tests/ -k "mode_guard or mode_split or full_auto" -q`
  → `16 passed, 268 deselected in 0.34s`.
- `py -m pytest harness/tests/ -q` → `284 passed in 22.11s`, sortie complète
  conservée dans `deliverables/evaluateur-iteration-2-tests.txt`.

Le gate mécanique est nécessaire mais ne tranche pas les deux défauts de
traçabilité décrits plus bas.

## Vérification des trois blocages précédents

| blocage | résultat | preuve indépendante |
|---|---|---|
| B1 — mentions d'activation périmées | **FERMÉ** | `rg -n "full_auto" docs/rules/full-auto-pipeline.md` montre désormais le titre `mode: full_auto_decision_only` à la ligne `77` et l'étape `3` avec la même valeur. Les autres mentions décrivent un état, une valeur refusée ou le concept général ; aucune n'ordonne de régler le mode sur la valeur nue. `rg -n "How to activate" docs HANDOFF.md` ne trouve qu'une référence générique dans `ADR-0007`, sans renvoi cassé. |
| B2 — fichier vide/tronqué accepté | **CAS DEMANDÉS FERMÉS, GARANTIE PLUS LARGE FAUSSE** | J'ai appelé directement `validate_mode("full_auto", ...)` avec quatre fichiers jetables : vide, espaces seuls, tronqué avant `jobs:`, non UTF-8. Les quatre lèvent `ModeGuardError`. Les `13` tests du fichier passent. Dans une copie Git jetable hors de l'arbre du dépôt, j'ai remplacé uniquement le refus du fichier vide par un retour permissif : le test `test_empty_forge_run_workflow_refuses_full_auto_fail_closed` devient rouge avec `Failed: DID NOT RAISE ModeGuardError`; après restauration, il repasse vert (`1 passed, 12 deselected`). Une recherche plus adverse trouve toutefois trois acceptations silencieuses contraires à la garantie de complétude ajoutée dans le module ; voir C3. |
| B3 — challenge annoncé comme déjà câblé | **PHRASE CIBLÉE CORRIGÉE, OVERCLAIM OPÉRATIONNEL RESTANT** | `pipeline-challenge.yml` porte encore le stub, tandis que `config.yaml` et le document disent désormais que 009c câblera ce maillon. Mais ces mêmes textes affirment encore que le mode active ou arrête une moitié de boucle qui ne le lit pas ; voir C4. |

## Conditions et compteurs reconstruits

| élément | valeur reconstruite | résultat |
|---|---:|---|
| `mode_full_auto_bare_rejected_test_count` | 1 fonction exacte, test rejoué | PASS |
| `mode_full_auto_accepted_when_forgerun_wired_test_count` | 1 fonction exacte, test rejoué | PASS |
| `config_mode_single_commit_transition_count` | 2 valeurs sur `244a4f2~1..a16b18c` : une suppression `full_auto`, un ajout `full_auto_decision_only` | PASS sur le fond |
| `adr_0007_status_field_present` | 1 ligne non vide | PASS |
| `adr_readme_rows_added_count` | 2 lignes ajoutées, `ADR-0006` et `ADR-0007` | PASS |

Les trois paires `must_differ_from` diffèrent toutes. Chaque snapshot `.orig`
est byte-identique au blob à `244a4f2~1`, donc il s'agit bien du véritable état
avant le lot. Le diff complet `244a4f2~1..a16b18c` ne touche aucun fichier sous
`.github/`, aucun fichier 009b/009c, et `ADR-0006` reste byte-identique.

## Quatre défauts nouveaux

1. **La sortie complète actuelle n'est pas recopiée dans le journal.**
   `deliverables/pytest-full-output.txt` contient une exécution à `284` tests,
   mais `rg -n "284 passed" deliverables/generator-log.md` ne retourne rien.
   Le journal ne contient que la sortie complète de l'itération 1 à `280` tests.
   Le contrat de fin de lot demande expressément la sortie complète dans le
   journal ; un fichier annexe seulement ne satisfait pas cette obligation.
2. **La commande déclarée pour le compteur de transition est devenue trop
   courte.** Le manifeste mesure encore `244a4f2~1..244a4f2`, alors que le lot
   livré va jusqu'à `a16b18c`. Le journal reconnaît lui-même que la plage
   élargie devait être rejouée après création du commit. Ma reconstruction
   prouve que la valeur reste bien `2`, mais elle ne transforme pas la commande
   obsolète du manifeste en preuve produite par le Générateur.
3. **Le garde accepte encore des faux workflows malgré son nouveau contrat de
   « preuve positive ».** J'ai reproduit directement, sans doublure du module :

   ```text
   commentaires_seuls: ACCEPTED returned None
   tronque_apres_runs_on: ACCEPTED returned None
   workflow_echo_sans_agent: ACCEPTED returned None
   ```

   Le premier fichier ne contient que des commentaires `# jobs:` et
   `# runs-on:` ; le deuxième est tronqué juste après `runs-on:` ; le troisième
   ne fait qu'un `echo no-agent`. Le code recherche deux sous-chaînes et
   l'absence du marqueur de stub. Il ne peut donc pas soutenir les phrases
   « real, complete workflow », « positively proves forge-run is wired » et
   « no fourth, silently-permissive outcome » introduites dans le module et le
   journal. La paire SC1/SC2 reste verte ; c'est la garantie élargie et la
   traçabilité qui sont fausses.
4. **Le document décrit encore le mode comme un contrôle opérationnel.**
   `rg -n "config\.yaml|full_auto_decision_only|full_auto_mode_guard|mode:"
   .github/workflows` ne retourne rien. En parallèle, les trois workflows
   audit/challenge/forge-run contiennent chacun `TODO(operator`. Malgré cela,
   `full-auto-pipeline.md` dit encore que 009a « activates the audit ->
   owner-decision half », que cette moitié « runs unattended » et que
   `mode: manual` « stops the loop » ; `config.yaml` qualifie aussi cette clé
   de « Emergency kill-switch ». Le brief réserve précisément le premier
   contrôle de mode à l'exécution à 009c SC15. Ces phrases restent donc en
   avance sur le code réel.

Les deux premiers points ne remettent pas en cause la correction
fonctionnelle. Les deux suivants montrent que le texte ajouté ou conservé par
l'itération promet davantage que le contrôle réel. Ensemble, ils empêchent la
livraison de satisfaire son contrat de preuve et son exigence de vérité sur le
monde réel.
Le détail de la correction attendue est dans
`feedback/feedback-009a-002.md`.

## Contre-lecture déléguée, limitée à la lecture

Une lecture secondaire a été lancée uniquement pour reconstruire les
compteurs et chercher un contre-exemple, jamais pour juger. Elle a exécuté
`git grep` sur les deux fonctions SC1/SC2, `git show` sur le statut `ADR-0007`,
et `git diff --unified=0 244a4f2~1..a16b18c` sur le README et `config.yaml`.
Sa sortie reconstruite est `1`, `1`, `1`, `2`, `2`, identique à la mienne.
Elle a aussi produit les trois sorties `ACCEPTED returned None` de C3. Je les
ai ensuite reproduites moi-même contre le module de l'arbre courant avant de
les retenir ici. La lecture secondaire n'a modifié aucun fichier et n'a rendu
aucun verdict.

Commande exacte de sa recherche adverse et sortie exacte :

```powershell
@'
import subprocess
from pathlib import Path
source = subprocess.check_output(
    ["git", "show", "a16b18c:harness/pipeline/full_auto_mode_guard.py"],
    text=True,
    encoding="utf-8",
)
ns = {
    "__name__": "guard_probe",
    "__file__": str(Path.cwd() / "harness/pipeline/full_auto_mode_guard.py"),
}
exec(compile(source, ns["__file__"], "exec"), ns)

class MemoryWorkflow:
    def __init__(self, name, content):
        self.name = name
        self.content = content
    def read_text(self, encoding="utf-8"):
        return self.content
    def __str__(self):
        return self.name

cases = {
    "commentaires-seuls": "# jobs:\n# runs-on:\n# aucune invocation forge-run\n",
    "tronque-apres-runs-on": "name: incomplete\njobs:\n  forge:\n    runs-on: ubuntu-latest\n",
    "workflow-echo-sans-agent": "name: fake\njobs:\n  forge:\n    runs-on: ubuntu-latest\n    steps:\n      - run: echo no-agent\n",
}
for name, content in cases.items():
    ns["Path"] = lambda _value, n=name, c=content: MemoryWorkflow(n, c)
    try:
        result = ns["validate_mode"]("full_auto", forge_run_workflow=name)
    except Exception as exc:
        print(f"{name}: REFUSED {type(exc).__name__}: {exc}")
    else:
        print(f"{name}: ACCEPTED returned {result!r}")
'@ | py -
```

```text
commentaires-seuls: ACCEPTED returned None
tronque-apres-runs-on: ACCEPTED returned None
workflow-echo-sans-agent: ACCEPTED returned None
```

## Verdict global de l'itération 2 : **REJECT** (lot 009a)

B1 et les cas précis exigés par B2 sont fermés ; la phrase challenge visée par
B3 est corrigée. Les tests sont verts, les cinq valeurs requises sont
reconstructibles et les frontières de périmètre sont respectées. Le lot reste
rejeté sur C1 à C4 : deux preuves non mises à jour, une garantie de garde plus
large que son implémentation, et une description opérationnelle encore en
avance sur les workflows. Le lot 009b reste indépendant et peut être produit.
Le lot 009c reste bloqué jusqu'à un ACCEPT explicite de 009a et un gate
mécanique vert de 009b.

---

# Évaluation — lot 009b (plafond budgétaire CI), commit `cd89141`

**Authored**: 2026-08-11T09:40:00Z
**Author**: forge-evaluateur

Cette section s'ajoute aux précédentes ; elle n'en efface aucune. Je juge ici
le lot 009b, produit par `forge-generateur-codex`. Je ne l'ai pas produit :
acteurs différents, donc juge recevable. Aucun chiffre du manifeste n'a été
repris sans être recalculé par mes propres commandes.

## Conditions de succès

| SC | Résultat | Preuve reconstruite par l'Évaluateur |
|---|---|---|
| SC8 — module autonome, ledger JSONL committé, prix importés | **SATISFAITE** | `grep -nE "PRICES\|per_million\|1_000_000"` sur le module ne rend que deux lignes, toutes deux des *références* à `backend_ledger` (`PRICES_AS_OF`, docstring) : aucune table de prix recopiée. `git ls-files --error-unmatch harness/pipeline/ci-budget-ledger.jsonl` réussit — le fichier est bien suivi. |
| SC9 — refus fail-closed au plafond, deux branches | **SATISFAITE** | Appel direct de `precheck_monthly_budget` sur des ledgers jetables : total `200.0` → `BudgetExceededError` ; total `199.999999` → PROCEED. La frontière est bien `>=`, testée à l'unité près et non à la dizaine. |
| SC10 — la bascule ne touche QUE la ligne `mode:` | **SATISFAITE** | Copie jetable du vrai `config.yaml`, comparaison octet à octet ligne par ligne après refus : **`54` lignes avant, `54` après, une seule différente (index `25`)** — `b'mode: full_auto_decision_only\n'` → `b'mode: manual\n'`. Commentaires et lignes voisines byte-identiques. |
| SC11 — marquage `over_cap` post-hoc, plafond paramétrable | **SATISFAITE** | Deux valeurs de plafond réellement distinctes dans les fixtures, extraites par AST et non par lecture du nom des tests : `5.0` et `50.0`. Le caractère post-hoc est documenté dans la docstring du module (`lignes 9-16`), comme exigé. |
| SC12 — les mois antérieurs ne comptent pas | **SATISFAITE** | Fixture `199.0` le mois précédent + `10.0` le mois courant ; `current_month_total_usd` rend `10.0`. |
| SC13 — le ledger n'est pas exclu par `.gitignore` | **SATISFAITE** | `git check-ignore --quiet -- harness/pipeline/ci-budget-ledger.jsonl` → **exit 1**, c'est-à-dire non ignoré. Sortie conservée par le Générateur dans `deliverables/git-check-ignore-009b.txt`. |

## Preuve red-first, rejouée par l'Évaluateur

Copie jetable du dépôt hors de l'arbre de travail (`git archive HEAD | tar -x`),
arbre du dépôt jamais modifié — vérifié par `git status --short harness/pipeline/`,
sortie vide.

**Première tentative invalide, et je la consigne plutôt que de la taire.**
J'ai d'abord lancé `pytest` depuis la racine du dépôt en pointant le fichier
de test de la copie. Les quatre sabotages sont restés **verts**. La cause
n'était pas le module : le répertoire courant plaçait le vrai dépôt en tête
de `sys.path`, si bien que les tests de la copie importaient le module
**intact** du dépôt. Un red-first mené ainsi ne prouve rien du tout, et
aurait ici « prouvé » l'inverse de la vérité. Rejoué depuis la copie
(`cd <copie> && py -m pytest ...`) :

| sabotage | tests devenus rouges |
|---|---|
| `total >= cap` → `total > cap` | `test_monthly_precheck_refuses_at_or_above_cap`, `test_ambiguous_config_refuses_kill_switch_rewrite` |
| `"over_cap": usd > cap` → `False` | `test_record_marks_over_cap_for_challenge_cap`, `test_record_marks_over_cap_for_forge_run_cap` |
| filtre de mois civil neutralisé | `test_prior_month_entries_do_not_count_toward_current_month` |
| appel à `_set_mode_manual` supprimé | `test_budget_refusal_changes_only_mode_line_bytes`, `test_ambiguous_config_refuses_kill_switch_rewrite` |

Restauration après chaque sabotage : `10 passed`. Les quatre comportements
sont donc **indépendamment porteurs** : aucun test ne survit à la
suppression de ce qu'il prétend garder.

## Frontières de périmètre

`git show --stat cd89141 -- docs/adr .github/workflows harness/pipeline/config.yaml`
ne rend **rien** : le lot n'a touché aucun ADR, aucun workflow et aucune
valeur de configuration, conformément à ses non-objectifs. Les douze fichiers
modifiés sont le module, son ledger, son fichier de tests et neuf livrables.

Le défaut C1 reproché au lot 009a n'est **pas** reproduit ici : la sortie
complète de la suite figure bien dans le journal lui-même
(`generator-log.md`, section 009b, `294 passed in 21.09s` sous sa commande),
et pas seulement dans le fichier annexe. Je l'avais soupçonné à tort sur un
`grep` tronqué ; vérification faite, le journal est conforme.

## Trois constats, aucun bloquant

1. **Un ledger absent ou vide vaut « budget remis à zéro ».** Sondé
   directement : fichier inexistant → `PROCEED, total=0.0` ; fichier de zéro
   octet → `PROCEED, total=0.0`. Le ledger livré est précisément dans cet
   état (un seul octet, `\n`). Ce n'est **pas** une violation : aucune
   condition ne l'exige, et refuser sur ledger vide rendrait le garde
   inutilisable à sa première exécution légitime. Mais c'est la famille de
   défaut qui a déjà coûté deux rejets au dépôt — « une entrée vide vaut
   permissif » — et ici la conséquence est monétaire. Un lot ultérieur
   devrait distinguer « n'a jamais existé » (on continue) de « fichier suivi
   par Git et désormais manquant » (on refuse). À noter que le cas
   *corrompu* est déjà traité, lui, et testé.
2. **Le contrôle d'auteur du gate est aveugle à ce lot.** `read_field`
   utilise `re.search`, qui rend la **première** occurrence. Le journal porte
   `forge-generateur` en ligne 1 (lot 009a, Claude) et
   `forge-generateur-codex` en ligne `596` (lot `009b`, Codex) ; le verdict porte
   `forge-evaluateur` en ligne 4 et `forge-evaluateur-codex` en ligne `252`. Le
   gate a donc comparé le couple du **lot 009a** et n'a rien vérifié du lot
   009b. Sur un brief multi-lots dont les lots sont produits par des acteurs
   différents, le contrôle ne voit que le premier. Ce n'est pas un défaut de
   009b — déclarer son auteur en tête de section est la bonne pratique, et
   Codex l'a fait. C'est un défaut du contrôle, désormais couvert par le
   brief `010`, lot `010a`.
3. **La prémisse de la dérogation s'est révélée fausse, et le Générateur l'a
   dit.** Le brief prévoyait d'accepter la forme post-hoc « si aucun plafond
   natif n'est trouvé ». `claude --help` en expose un
   (`--max-budget-usd <amount>`, sortie réelle conservée dans
   `deliverables/claude-help-budget-excerpt.txt`). Le Générateur ne s'est pas
   abrité derrière la formulation de la dérogation : il a écrit la
   découverte, conservé la forme post-hoc que SC11 exige littéralement, et
   renvoyé au lot 009c la décision d'ajouter aussi le plafond natif. C'est le
   bon découpage. Le propriétaire doit trancher ce point avant 009c.

## Verdict : **LOT_009b: ACCEPT**

Les six conditions SC8 à SC13 sont satisfaites, chacune reconstruite par mes
propres commandes ; la preuve red-first est valide après correction de mon
propre protocole ; les frontières de périmètre sont tenues ; aucun échec
disqualifiant n'est présent. Les trois constats ci-dessus sont des suites à
donner, pas des motifs de rejet.

Rappel de l'état du brief `009` : le lot 009a reste **REJETÉ** (défauts C1-C4,
`feedback/feedback-009a-002.md`). Le lot 009c reste bloqué jusqu'à un ACCEPT
explicite de 009a — l'acceptation de 009b lève l'une de ses deux conditions,
pas les deux.
