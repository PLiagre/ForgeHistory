---
description: Run a ForgeHistory brief through the full three-role harness loop (Planificateur -> Générateur -> mechanical gate -> Évaluateur) until it passes or plateaus.
argument-hint: <brief-slug-or-path> [--backend claude|cursor] [--max-iterations N]
allowed-tools: Task, Read, Write, Bash, Grep, Glob
---

# /forge-run

Adapted from ECC's `commands/gan-build.md` loop-with-plateau-detection
pattern, re-scoped for ForgeHistory's binary mechanical gate (ACCEPT/REJECT
over 9 checks) instead of a 1-10 weighted score, and for a
backend-pluggable Générateur (see `docs/adr/0002-pluggable-generator-backend.md`).

## Arguments

`$ARGUMENTS`:
- `<brief-slug-or-path>` (required) — either `NNN-<slug>` (resolved under
  `harness/queue/briefs/`) or a full path to a brief directory.
- `--backend claude|cursor` (default `claude`) — which Générateur backend
  runs this brief's iterations.
- `--max-iterations N` (default `5`).

## Execution Budgets (`BUDGET_EXHAUSTED`, `NEEDS_SPLIT`)

Two loop outcomes exist alongside PASS / PLATEAU / ESCALATED /
MAX_ITERATIONS, and neither is a REJECT:

| outcome | meaning | what to do |
|---|---|---|
| `NEEDS_SPLIT` | The Planificateur judged the brief too large before generation. | Do not run the Générateur. Report the lots it produced; each is its own `/forge-run` in a fresh session. |
| `BUDGET_EXHAUSTED` | A Générateur hit its tool-call budget (160, or 35 calls without measurable progress). | The iteration ends. Its `deliverables/checkpoint-NNN.md` is the handoff. Do not spend the remaining iterations re-running the same oversized brief. |

`REJECT` means the work is wrong; these mean the container was too small and
the work is unfinished. Recording a budget stop as a REJECT would teach the
loop that a well-executed oversized brief is defective, and would burn the
remaining iterations on the same overflow.

Before Phase 1, run the advisory size check and honour a `NEEDS_SPLIT`:

```bash
py harness/budget.py split-check --brief <BRIEF_DIR>
```

The Générateur checks `py harness/budget.py status --brief <BRIEF_DIR>`
itself; the orchestrator's job is only to read the outcome and stop cleanly.

## Phase 0: Resolve the Brief Directory

1. Resolve `BRIEF_DIR` from the argument (prefix with
   `harness/queue/briefs/` if a bare slug was given).
2. If `BRIEF_DIR/brief.md` does not exist: this is a new brief. Launch the
   `forge-planificateur` agent (via Task tool) with the user's one-line
   request to write `brief.md` and `eval-rubric.md` first. Do not proceed
   until both exist — a brief with no rubric cannot be judged.
3. Ensure `BRIEF_DIR/deliverables/` and `BRIEF_DIR/feedback/` exist.

## Phase 1: The Loop

```
iteration = 0
scores = []   # count of PASS checks out of 9, per iteration

while iteration < max_iterations:
    iteration += 1

    # --- Générateur ---
    if backend == "cursor":
        run: bash harness/backends/run_cursor_generator.sh <BRIEF_DIR>
        if this fails (missing binary/login): STOP, report the exact
        error to the user -- do not silently fall back to Claude.
    else:
        launch forge-generateur agent via Task tool. On iteration > 1,
        it must read feedback/feedback-{iteration-1}.md first (this is
        already in its own instructions, but confirm the file exists
        before launching so you can tell it explicitly which file to read).

    # --- Mechanical gate (always runs, both backends) ---
    run: py harness/verdict_audit.py <BRIEF_DIR>
    parse exit code and count PASS lines -> score
    scores.append(score)

    if exit_code == 1 (REJECT):
        # Per forge-evaluateur.md's own rule: do not proceed to manual
        # review of a mechanically rejected brief. Write the gate's own
        # FAIL lines as feedback/feedback-{iteration}.md so the Générateur
        # has something concrete to read next iteration, then check the
        # plateau/escalation conditions below and continue the loop.
        write feedback/feedback-{iteration}.md citing every FAIL line verbatim
    else:
        # --- Évaluateur (only after mechanical ACCEPT) ---
        launch forge-evaluateur agent via Task tool
        it writes verdict.md and, if it finds issues beyond the mechanical
        checks, feedback/feedback-{iteration}.md

        if verdict.md's Overall Verdict is PASS:
            STOP. Report success with the verdict.md path.

    # --- Plateau detection (hard-won discipline, see docs/rules/harness-roles.md) ---
    if iteration >= 3 and scores[-1] <= scores[-2] and scores[-2] <= scores[-3]:
        STOP. Report "PLATEAU detected at iteration {iteration} -- stopping
        early, escalating to a human" with the score history and the last
        feedback file's path. Do not keep iterating on a question nobody
        can answer.

    # --- 3-strikes escalation (independent of plateau) ---
    if iteration >= 3 consecutive REJECTs in a row (never once ACCEPTed):
        STOP. Report "3 failed retries -- escalating to a human" per
        docs/rules/harness-roles.md.

if iteration == max_iterations without a PASS verdict:
    STOP. Report max iterations reached, current score, and the path to
    the latest feedback file.
```

## Phase 2: Final Report

Write `<BRIEF_DIR>/run-report.md`:

```markdown
# Run Report — <brief-slug>

**Backend**: claude | cursor
**Iterations**: N
**Score history**: [6, 7, 7, 9]  (out of 9 mechanical checks)
**Outcome**: PASS | PLATEAU | ESCALATED | MAX_ITERATIONS

## Per-Iteration Summary
| Iteration | Gate Verdict | Score | Évaluateur Verdict | Notes |
|---|---|---|---|---|

## Final Artifacts
- verdict.md: <path, if it exists>
- latest feedback: <path>
```

## Rules This Command Must Obey

- Never let the Générateur and Évaluateur be the same invocation/pass.
- Never skip the mechanical gate, regardless of backend.
- Never treat a mechanical REJECT as something the Évaluateur can override.
- Never silently switch backends mid-run — if `--backend cursor` was
  requested and the wrapper fails (missing binary/login), stop and report
  it; do not fall back to Claude without telling the user.
- Cite `run-report.md` and `feedback/*.md` paths by name in your final
  report to the user, never inline-paste their content as if it were the
  source of truth (hard-won rule 12).
