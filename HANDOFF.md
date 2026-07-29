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

`py -m pytest harness/tests/ -v` — **13 passed** (verified again this
session, 2026-07-29, after all additions below — re-run it yourself rather
than trusting this line indefinitely).

`py harness/harness_audit.py` — **24/24** (this only checks presence of
pieces, not that they work correctly — see caveat under Open TODOs).

## What Exists

Everything from the original F0 pass (repo skeleton, 3-role agents, gate,
tests, demo pair, Cursor backend wrapper, ADRs 0001/0002, VISION.md copied
verbatim), **plus**, added this session after an explicit request to reuse
more of ECC's automation patterns (multi-editor adapters excluded — not
needed):

- **`.claude/commands/forge-run.md`** — the orchestration loop
  (Planificateur -> Générateur[claude|cursor] -> gate -> Évaluateur ->
  feedback, with plateau detection and 3-strikes escalation). Adapted from
  ECC's `commands/gan-build.md`. **Not yet run against a real brief** — it's
  written and internally consistent with the agents/gate it calls, but its
  first live execution (via an actual `/forge-run` invocation in a Claude
  Code session) hasn't happened. Verify it end-to-end before trusting it
  fully.
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
- [ ] **Cursor backend still not end-to-end tested** — `cursor-agent` is not
      installed on this machine. Wrapper's error paths are verified; the
      happy path needs the project owner's own Cursor login.
- [ ] **New hooks and `/forge-run` are untested inside a real Claude Code
      session** — everything above was verified by direct script invocation
      (feeding hand-built JSON on stdin, or running the Python/bash directly),
      not by triggering the actual PreToolUse/Stop events or a real
      `/forge-run` slash-command invocation. This is the same category of
      gap as the original `no_bare_python.py` hook. Exercise these for real
      in a ForgeHistory-rooted session before fully trusting the wiring.
- [ ] F1 not started: `pipeline/geo/`, `sim/` remain empty stubs.
- [ ] Tier-3 gate (adversarial re-review) intentionally not built yet.
- [ ] No README.md (human-facing) exists yet — only CLAUDE.md
      (agent-facing). Flagged to the project owner, not yet resolved either
      way.
- [ ] `.claude/commands/forge-run.md` describes an orchestration loop as
      *instructions for Claude to follow*, not a standalone script — it
      depends on Claude correctly following multi-step markdown instructions
      (launching Task-tool subagents, parsing gate output, counting PASS
      lines for the plateau check). This is inherently less mechanically
      verifiable than `verdict_audit.py`. Watch its first few real runs
      closely rather than assuming it's as reliable as the gate itself.

## Known Risks

- Never fabricate VictoriaProject content beyond what was actually cloned
  and read.
- `pytest` was installed via `py -m pip install --user pytest` on this
  machine; not vendored/pinned in this repo.
- The usage ledger (`cost-ledger.jsonl`) currently has zero real entries —
  it was smoke-tested and then cleared. `/forge-cost-report` will correctly
  report "no usage logged yet" until a real brief runs through `/forge-run`.

## Last Session Summary (2026-07-29)

Built F0 from scratch (repo skeleton, 3-role harness, 9-check mechanical
gate with 13 passing tests, proven fake/honest demo pair, Cursor backend
wrapper, 2 ADRs, VISION.md copied verbatim). Then, per an explicit
follow-up request to reuse more of ECC's automation patterns (multi-editor
adapters and the ~300-skill/~90-command library were evaluated and
deliberately excluded as irrelevant to a single game-simulation project),
added: the `/forge-run` loop orchestrator, a git-push test-gate hook, a
VISION.md edit-guard hook, a stale-HANDOFF warn hook, a backend usage
ledger + cost-report command, and a harness-maturity self-audit script +
command. All mechanical pieces (gate, tests, demos, ledger, harness_audit)
were run and verified this session; hook/command wiring that requires a
live Claude Code session firing real tool-use events was smoke-tested via
direct invocation only, not via an actual live session — tracked honestly
above, not claimed as fully proven. Repo is **not yet committed**
(`git status` shows everything untracked) — awaiting the project owner's
go-ahead.
