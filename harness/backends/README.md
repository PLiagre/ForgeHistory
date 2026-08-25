# Pluggable Générateur Backends

Le rôle d'exécution (voir `AGENTS.md`) peut tourner comme
native Claude Code `forge-generateur` agent (default, in-session, no wrapper
needed), or be delegated to another backend. This directory holds wrappers
for the alternative backends.

## The Contract

**Any** backend wrapper, given a `brief_dir` (a directory containing
`brief.md` and `eval-rubric.md`), must produce:

- `deliverables/manifest.json` — same schema `verdict_audit.py` reads:
  `files[]` (`path`, optional `must_differ_from` *or* `must_differ_from_git`,
  the latter a `<rev>:<path>` git reference and the form to use whenever git
  already tracks the pre-state), `counters[]` (`name`,
  `value`, `sample_size`, `command`), `waivers[]` (`claim`, `command`,
  `error`).
- `deliverables/generator-log.md` — with
  `**Author**: forge-generateur-<backend>` (e.g. `forge-generateur-cursor`),
  narrating what was built and how each counter was measured.

A backend wrapper must **never** write `verdict.md` — it does not judge its
own work. The verdict belongs to an independent Évaluateur under ADR-0008,
never to the session that ran the Générateur.

This is what keeps `harness/verdict_audit.py` completely unchanged and
backend-agnostic: it only ever reads the brief-directory contract above, not
which backend produced it.

## Backends

| Backend | Wrapper | Status |
|---|---|---|
| Claude Code (default) | none needed — native `forge-generateur` agent, in-session | working |
| Cursor CLI | `run_cursor_generator.sh` | written; end-to-end run against the real `cursor-agent` binary requires the project owner's own Cursor login — l'exécution de bout en bout demande le compte Cursor du propriétaire |
| Codex CLI | `run_codex_generator.sh` | official wrapper; uses stable non-interactive `codex exec`, signs `forge-generateur-codex`, and runs the shared anti-auto-judgment preflight before writing |

## Usage

```bash
bash harness/backends/run_cursor_generator.sh <brief_dir> [extra_dirs_colon_separated]
bash harness/backends/run_codex_generator.sh <brief_dir> [extra_dirs_colon_separated]
```

Requires `cursor-agent` installed and either `CURSOR_API_KEY` set or a prior
`cursor-agent login`. The script checks for both up front and exits with the
exact missing command if either is absent — it never silently skips or
guesses (hard-won rule 9).

The Codex wrapper requires a working Codex CLI. It uses the documented
non-interactive `codex exec` interface with a `workspace-write` sandbox and
JSONL output; it reports the exact CLI error instead of silently falling back.
Official reference: https://developers.openai.com/codex/cli/reference/
