# AGENTS.md

Repo-wide guidance for coding agents. The authoritative product/harness rules
live in [CLAUDE.md](CLAUDE.md), [VISION.md](VISION.md),
[docs/rules/](docs/rules/), and each brief under
`harness/queue/briefs/`. This file only adds environment/run notes on top of
those — it never paraphrases the rule files (see CLAUDE.md "Single Source of
Instruction").

## Cursor Cloud specific instructions

Scope of what actually runs on the Linux cloud VM (the update script has
already been applied on startup — it creates a repo-root `.venv` and installs
the geo stack plus `pytest` into it). The system `python3` is an
externally-managed (PEP 668) Debian interpreter, so **use `.venv/bin/python`
for anything that needs third-party packages** (including `pytest`); do not
`pip install --user` against the system python (it errors).

- **Harness (Python, stdlib + pytest)** — the primary thing to develop/run
  here. Commands are in [CLAUDE.md](CLAUDE.md) "Key Commands" and
  [harness/README.md](harness/README.md). On Linux the docs' `py` launcher is
  `.venv/bin/python` (e.g. `.venv/bin/python -m pytest harness/tests/ -v`).
  The gate/audit scripts are stdlib-only, so `.venv/bin/python
  harness/verdict_audit.py <brief_dir>` (or plain `python3` for those) both
  work. The 13 `test_run_unity.py` cases SKIP on Linux (they need
  Unity/PowerShell) — this is expected, not a failure.
- **Geo pipeline (Python, scientific stack)** — installed into a repo-root
  `.venv` (git-ignored). Run its proofs with that interpreter, from
  `pipeline/geo/`:
  `../../.venv/bin/python tests/run_proof_g2.py` and `.../run_proof_g2b.py`.
  They regenerate coastline artifacts + PNG captures under
  `pipeline/geo/{artifacts,build,capture,logs}/` (all git-ignored). Output is
  deterministic (SHA256-pinned), so a clean re-run produces byte-identical
  files and no git diff — that is the intended "green" state, not a no-op bug.
  Note: `pipeline/geo/tests/test_qa_red_g2*.py` are NOT pytest-discoverable
  (they expose `run_all_red_g2*` helpers the proof scripts import), so
  `pytest` collects 0 items there — run the `run_proof_*` scripts instead.
- **Unity game (`unity/game_unity/`)** — the only end-user-facing product, but
  it is **Windows + licensed Unity 6000.0.43f1 only**. Both launchers
  (`unity/open-game.ps1`, `unity/run-unity.ps1`) hardcode Windows paths
  (`C:\Program Files\Unity\...`). It **cannot be built or run on this Linux
  VM** — treat it as out of scope here and rely on the committed
  `unity/game_unity/Captures/**` for visual reference.
- **`sim/`** is still an empty stub (the intended core engine); there is no
  server/daemon/DB/web tier in this repo to stand up.

Lint: there is no configured linter (no ruff/flake8/pylint/eslint). The repo's
quality gates are `harness/verdict_audit.py` (per-brief mechanical gate) and
`harness/harness_audit.py` (harness self-audit). `harness_audit.py` currently
scores 23/24: the single FAIL (`no_premature_stub_content`) is a known stale
assumption in the audit tool itself — it still treats `pipeline/geo/` and
`unity/` as empty stubs even though briefs 002/003 legitimately populated them
(see [HANDOFF.md](HANDOFF.md)). Do not "clean" those dirs to satisfy it.

`harness/budget.py status` reports `UNMEASURABLE` on a fresh VM because it
reads local Claude session transcripts that do not exist here — expected, not
a defect.
