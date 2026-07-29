# Pluggable Générateur Backends

The Générateur role (see `docs/rules/harness-roles.md`) may run as the
native Claude Code `forge-generateur` agent (default, in-session, no wrapper
needed), or be delegated to another backend. This directory holds wrappers
for the alternative backends.

## The Contract

**Any** backend wrapper, given a `brief_dir` (a directory containing
`brief.md` and `eval-rubric.md`), must produce:

- `deliverables/manifest.json` — same schema `verdict_audit.py` reads:
  `files[]` (`path`, optional `must_differ_from`), `counters[]` (`name`,
  `value`, `sample_size`, `command`), `waivers[]` (`claim`, `command`,
  `error`).
- `deliverables/generator-log.md` — with
  `**Author**: forge-generateur-<backend>` (e.g. `forge-generateur-cursor`),
  narrating what was built and how each counter was measured.

A backend wrapper must **never** write `verdict.md` — it does not judge its
own work; that stays the Évaluateur's job on Claude, regardless of which
backend ran the Générateur (see ADR-0002).

This is what keeps `harness/verdict_audit.py` completely unchanged and
backend-agnostic: it only ever reads the brief-directory contract above, not
which backend produced it.

## Backends

| Backend | Wrapper | Status |
|---|---|---|
| Claude Code (default) | none needed — native `forge-generateur` agent, in-session | working |
| Cursor CLI | `run_cursor_generator.sh` | written; end-to-end run against the real `cursor-agent` binary requires the project owner's own Cursor login — see `HANDOFF.md` for whether it's actually been tested here |

## Usage

```bash
bash harness/backends/run_cursor_generator.sh <brief_dir>
```

Requires `cursor-agent` installed and either `CURSOR_API_KEY` set or a prior
`cursor-agent login`. The script checks for both up front and exits with the
exact missing command if either is absent — it never silently skips or
guesses (hard-won rule 9).
