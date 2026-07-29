---
description: Rewrite HANDOFF.md's Status and Last Session Summary from live command output — the actual end-of-session state, not a narrated guess.
argument-hint: (no arguments)
allowed-tools: Read, Edit, Bash
---

# /forge-checkpoint

Adapted from ECC's `/checkpoint` / `/save-session` / `/resume-session`
family, but re-scoped: ForgeHistory does not keep a separate
`checkpoints.log` alongside `HANDOFF.md` — that would be two documents
describing state, which is the same anti-pattern the single-source-of-
instruction rule exists to prevent for briefs. `HANDOFF.md` **is** the
checkpoint file (see the repo skeleton in the project charter). This
command updates it in place.

## What This Command Does

1. Gather real, current state — do not narrate from memory:
   ```bash
   git status --short
   git log --oneline -10
   py -m pytest harness/tests/ -q
   py harness/harness_audit.py
   ```
2. If any brief is currently in `harness/queue/briefs/` with an open
   iteration, note its `run-report.md` status (if `/forge-run` has been used
   on it) — cite the path, do not paste its content inline.
3. Rewrite `HANDOFF.md`'s **Status** and **Last Session Summary** sections
   using the actual output above — pass/fail counts, real paths, not vague
   claims like "everything works." If tests are failing, say so; this
   command's job is to make the true state legible, not to make it look
   good.
4. Update **Open TODOs** — remove any that were resolved this session
   (only if verified resolved, not assumed), add any newly discovered ones.
5. Leave every other section of `HANDOFF.md` (Current Milestone,
   Done-Criterion, What Exists, Known Risks) untouched unless something
   actually changed structurally this session.

## Rule

Never write "F0 DONE" or similar unless the verification commands above
actually confirm it right now, in this run — a checkpoint that repeats a
stale claim is worse than no checkpoint.
