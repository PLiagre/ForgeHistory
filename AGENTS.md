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
- **Geo pipeline (Python, scientific stack)** — **archive (ADR-0019), not
 daily work.** G6 is frozen (failure accepted). Do not re-run G6 Europe
 proofs, SHA recertification, climate-observed, or R1 consumption lots.
 `sim/` still reads G3 cells already in git. The geo stack remains in
 `.venv` so the archive can be inspected; that is not a prompt to resume
 geo as a parallel product. Historical G2 proof scripts still exist under
 `pipeline/geo/tests/` if someone explicitly reconstructs the archive.
- **Unity game (`unity/game_unity/`)** — **en veille (ADR-0016).** Le
  produit vivant est `sim/` (`python -m sim`). Unity 6000.0.43f1 n'est
  pas requis sur cette VM. Les captures committées restent une référence
  visuelle gelée ; ne pas lancer de lots Unity ici.

- **`sim/`** est le moteur : couche 1 livrée, entrée `python -m sim`.
  Pas de serveur/daemon/DB.

Lint: there is no configured linter (no ruff/flake8/pylint/eslint). The repo's
quality gates are `harness/verdict_audit.py` (per-brief mechanical gate) and
`harness/harness_audit.py` (harness self-audit). `harness_audit.py` currently
scores 20/24 on a fresh clone (demo log gitignored; `no_premature_stub_content` still treats `sim/`, `pipeline/geo/` and `unity/` as stubs).

`harness/budget.py status` reports `UNMEASURABLE` on a fresh VM because it
reads local Claude session transcripts that do not exist here — expected, not
a defect.
