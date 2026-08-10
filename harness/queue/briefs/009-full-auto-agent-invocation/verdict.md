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
