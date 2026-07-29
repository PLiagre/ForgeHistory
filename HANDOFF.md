# HANDOFF.md

State-of-play. Rewritten at the end of every session (via `/forge-checkpoint`
going forward) — not a changelog, but "what a new agent needs to pick up
exactly where the last session left off."

## Current Milestone

F0 — "le socle qui refuse de mentir" — **DONE**, plus an automation-tooling
extension requested by the project owner mid-session (2026-07-29).

## Done-Criterion: Verified

"Un brief bidon volontairement faux doit être refusé par la porte." Proven:
`py harness/demo/fake_brief_001/run_demo.py` exits 0, writes
`harness/demo/fake_brief_001/run_demo.log` showing `VERDICT: REJECT` with
exactly 5 of 9 checks failing. Control case
`py harness/verdict_audit.py harness/demo/honest_brief_001` exits 0,
`VERDICT: ACCEPT`, all 9 pass.

`py -m pytest harness/tests/ -v` — **16 passed** (re-verified 2026-07-29,
end of the session that ran the first real brief — 3 new tests added this
session for the gate-defect fix below; re-run it yourself rather than
trusting this line indefinitely).

`py harness/harness_audit.py` — **24/24** (re-verified same session; still
only checks presence of pieces, not that they work correctly — see caveat
under Open TODOs).

## What Exists

Everything from the original F0 pass (repo skeleton, 3-role agents, gate,
tests, demo pair, Cursor backend wrapper, ADRs 0001/0002, VISION.md copied
verbatim), **plus**, added this session after an explicit request to reuse
more of ECC's automation patterns (multi-editor adapters excluded — not
needed):

- **`.claude/commands/forge-run.md`** — the orchestration loop
  (Planificateur -> Générateur[claude|cursor] -> gate -> Évaluateur ->
  feedback, with plateau detection and 3-strikes escalation). Adapted from
  ECC's `commands/gan-build.md`. **Run for real this session** (2026-07-29)
  against brief `001-spatial-primary-key-adr`, reaching a genuine
  `VERDICT: ACCEPT`. Its stated phase order turned out not to match
  `verdict_audit.py`'s actual checks — see Open TODOs — `forge-run.md` itself
  not yet corrected for that.
- **`.claude/hooks/guard_git_push.py`** — blocks `git push` if
  `harness/tests/` is red. Explicitly required by the project charter
  (§5.5). Smoke-tested both ways this session (red test -> exit 2 verified,
  then reverted; green -> exit 0). **Not yet exercised via a real PreToolUse
  firing inside a live session** — only invoked directly with a hand-built
  JSON payload on stdin.
- **`.claude/hooks/guard_vision_edit.py`** — blocks edits to `VISION.md`
  unless `FORGE_ALLOW_VISION_EDIT=1`. Smoke-tested (blocked / allowed via
  override / unrelated file passes). Same live-session caveat as above.
- **`.claude/hooks/remind_handoff_stale.py`** (Stop hook, warn-only) — flags
  when `.claude/`/`docs/`/`harness/*.py` etc. are newer than `HANDOFF.md`.
  Verified it correctly warned earlier this session when `HANDOFF.md` was
  stale relative to the new files; will re-verify silence once this rewrite
  makes it current.
- **`harness/backends/ledger.py`** — append/report usage ledger
  (`harness/queue/cost-ledger.jsonl`), tracking Générateur invocation counts
  by backend (Claude vs Cursor) as an honest proxy for spend distribution —
  deliberately does NOT claim dollar costs it can't measure (see its
  docstring for why ECC's own cost-tracking skill wasn't ported directly:
  it's coupled to ECC's plugin-root resolution machinery and only tracks
  Claude's own cost, never Cursor's). Wired into `run_cursor_generator.sh`
  (tested — appends correctly) and instructed into `forge-generateur.md`
  (not independently testable here, it's an agent instruction).
- **`.claude/commands/forge-cost-report.md`** — wraps `ledger.py report`.
- **`harness/harness_audit.py`** + **`.claude/commands/forge-harness-audit.md`**
  — deterministic maturity checklist over the harness's own structure
  (role/gate/test/hook/doc coverage), adapted from ECC's
  `scripts/harness-audit.js`. One bug found and fixed during this session's
  own verification (missing `re.MULTILINE` caused a false FAIL on
  `gate_test_coverage`) — proof that "run it and look," not just "write it,"
  matters even for a report tool.
- **`.claude/commands/forge-checkpoint.md`** — rewrites this file's Status /
  Last Session Summary from live command output; this very rewrite follows
  its own instructions.

## Open TODOs

- [ ] **VISION.md's internal links are dead** (links to VictoriaProject's
      own `ADR-001`/`ADR-002`, different numbering than this repo's
      `0001`/`0002`). Not fixed — decide explicitly in a future ADR, don't
      silently edit VISION.md (now also mechanically blocked by
      `guard_vision_edit.py` without `FORGE_ALLOW_VISION_EDIT=1`).
- [x] ~~Cursor backend still not end-to-end tested~~ — **resolved
      2026-07-29**: `cursor-agent` installed (`irm
      'https://cursor.com/install?win32=true' | iex`) and authenticated
      (`cursor-agent.cmd login`, logged in as `liagre.pe@outlook.com`). Two
      real bugs found and fixed in `harness/backends/run_cursor_generator.sh`
      while getting a genuine end-to-end run working on Windows: (1) the
      preflight/invocation used bare `cursor-agent`, which Git Bash can't
      resolve to the installed `cursor-agent.cmd` shim — now tries
      `cursor-agent`, `cursor-agent.cmd`, `cursor-agent.exe` in order; (2) the
      prompt was passed as a `-p` command-line argument, which blew past
      Windows's command-line length limit for any real brief-sized prompt
      (`La ligne de commande est trop longue`) — now piped via stdin instead.
      Proven with a real, throwaway smoke-test brief run through the actual
      `cursor-agent` API (not mocked): produced correct
      `deliverables/{hello.txt,manifest.json,generator-log.md}`,
      `**Author**: forge-generateur-cursor`, no `verdict.md` written (per
      contract), real token usage
      (`inputTokens:170126, outputTokens:4052`). Smoke-test artifacts and its
      stray ledger entry were cleaned up afterward — not part of any real
      brief. `CURSOR_API_KEY` is still not set as an env var (auth is stored
      by `cursor-agent login` instead); on this machine, bash sessions
      started before the Cursor install don't see it on PATH automatically —
      prefix `export PATH="$PATH:/c/Users/liagr/AppData/Local/cursor-agent"`
      when invoking the wrapper directly from a shell, or use `/forge-run
      ... --backend cursor` which should inherit this once a fresh shell
      picks up the updated PATH.
- [x] ~~New hooks and `/forge-run` are untested inside a real Claude Code
      session~~ — **partially resolved 2026-07-29**: the `/forge-run` loop
      (Planificateur -> Générateur -> gate -> feedback -> Générateur
      iteration 2 -> gate) was exercised for real this session on brief
      `001-spatial-primary-key-adr` and reached a genuine `VERDICT: ACCEPT`
      (9/9). The `no_bare_python.py` `PreToolUse` hook also fired for real
      (blocked a live Bash command mid-session). Still not exercised live:
      `guard_git_push.py`, `guard_vision_edit.py`, `remind_handoff_stale.py`.
- [ ] **`forge-run.md`'s stated phase order doesn't match `verdict_audit.py`'s
      actual checks** — discovered running the first real brief. The command
      doc says the Évaluateur runs "only after mechanical ACCEPT," but the
      gate itself checks `verdict.md`'s traceability/authorship, so it can
      never ACCEPT before `verdict.md` exists — and only the Évaluateur
      writes that file. What actually worked: gate after Générateur (catches
      Générateur-side defects cheaply), Évaluateur regardless of that first
      exit code, then a real final gate re-run once `verdict.md` exists. See
      `harness/queue/briefs/001-spatial-primary-key-adr/run-report.md`'s
      "Process Deviation" section. `forge-run.md` itself not yet corrected —
      flagged, not silently fixed, since it's process documentation.
- [ ] F1 not started: `pipeline/geo/`, `sim/` remain empty stubs, but the
      blocking ADR now exists (see What Exists) — F1 code can begin under a
      new brief whenever the project owner wants it.
- [ ] Tier-3 gate (adversarial re-review) intentionally not built yet.
- [ ] No README.md (human-facing) exists yet — only CLAUDE.md
      (agent-facing). Flagged to the project owner, not yet resolved either
      way.
- [ ] **Brief 001's work is staged but not committed** — `git status --short`
      shows it all as staged/untracked (ADR-0003, README unblocks, gate fix,
      3 new tests, brief/deliverables/verdict/run-report). Awaiting the
      project owner's explicit go-ahead to commit.

## Known Risks

- Never fabricate VictoriaProject content beyond what was actually cloned
  and read.
- `pytest` was installed via `py -m pip install --user pytest` on this
  machine; not vendored/pinned in this repo.
- The usage ledger (`cost-ledger.jsonl`) now has one real entry from brief
  001's Générateur run (previously zero, smoke-tested-then-cleared).
- **A Générateur subagent committed to git unprompted during brief 001's
  iteration 2**, with no instruction telling it to. Caught and undone via
  `git reset --soft HEAD~1` (files preserved) at the project owner's
  request. Never let a Générateur (or any) subagent commit unless the brief
  or the human running the loop explicitly says to — re-state this in
  agent prompts if it recurs.

## Last Session Summary (2026-07-29)

Two sessions rolled into this file. First: built F0 from scratch (repo
skeleton, 3-role harness, 9-check mechanical gate, proven fake/honest demo
pair, Cursor backend wrapper, 2 ADRs, VISION.md copied verbatim), then added
the automation-tooling extension (`/forge-run`, 3 hooks, cost ledger,
harness self-audit). That work was committed across 6 commits
(`4bbf4ad`..`abb8004`) at some point between that session and this one —
`git log` in this session found the working tree already at 6 commits, so
the prior write-up's "not yet committed" note is now stale/resolved.

This session: ran the harness's **first real (non-demo) brief**,
`001-spatial-primary-key-adr` — write ADR-0003 deciding the single spatial
primary key (cell as the key, province as a derived aggregation) and unblock
`sim/README.md`/`pipeline/geo/README.md`. Planificateur wrote brief+rubric;
Générateur iteration 1 built the ADR and README edits; the gate REJECTed
(3/9 fails, 2 expected because `verdict.md` didn't exist yet); Évaluateur
independently verified all 11 rubric rows PASS on substance and found 4 real
minor defects plus 2 gate false-positive bugs; Générateur iteration 2 fixed
the 4 real defects; the gate then REJECTed on exactly one remaining check, a
confirmed false positive. With the project owner's explicit go-ahead, both
gate bugs were fixed in `harness/verdict_audit.py` (inline-code-span content
is now masked before the bare-`python` and cited-number checks), red-first
per hard-won rule 4 (3 new tests, 16 total, all passing) — the brief then
gates **ACCEPT, 9/9**. Also found and corrected: a Générateur subagent made
an unauthorized git commit mid-run (undone via `git reset --soft HEAD~1`,
files kept); and `forge-run.md`'s stated Générateur/Évaluateur/gate ordering
doesn't actually match how `verdict_audit.py` works (see Open TODOs). Full
detail: `harness/queue/briefs/001-spatial-primary-key-adr/run-report.md`.
Nothing from this session is committed yet — awaiting the project owner's
go-ahead.
