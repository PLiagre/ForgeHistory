# Harness Roles — Three Separate Roles, Never One Agent

Core principle: "Celui qui produit ne prononce pas la recevabilité"
(whoever produces does not pronounce acceptability).

| Role | Agent file | Writes | Never |
|---|---|---|---|
| Planificateur | `.claude/agents/forge-planificateur.md` | brief + rubric, BEFORE code | codes |
| Générateur | `.claude/agents/forge-generateur.md` (or a backend under `harness/backends/`) | code, measurements, logs | pronounces "acceptable" |
| Évaluateur | `.claude/agents/forge-evaluateur.md` | verdict, vs. the pre-written rubric | modifies code |

The rubric is written before the work, by someone other than whoever is
judged — the only protection against a verdict that grades what was done
rather than what was asked.

## Backend-Pluggable Générateur

The Générateur role may be executed by the native Claude Code agent, or
delegated to another backend (currently: Cursor CLI, see
`harness/backends/README.md`) to spread cost across subscriptions and to get
a genuinely independent implementer. Planificateur and Évaluateur stay on
Claude regardless of which backend runs the Générateur — this preserves
judge independence and is what keeps `verdict_audit.py`'s
`verdict_is_not_self_authored` check meaningful.

## Gate Tiers

1. **Mechanical** (free, instant) — `harness/verdict_audit.py`.
2. **Automatic** — compile/test, bit-identical parity, two-pass determinism.
3. **Adversarial** (NOT built in F0) — deferred until briefs touch the
   world/what-the-player-sees.

## Loop / Stop Discipline

- Plateau: two iterations without improvement -> STOP, don't replay.
- Feedback passed as a readable FILE (`harness/queue/briefs/*/feedback/`),
  never a log to dig through.
- Three failed retries -> escalate to a human.

## Single Source of Instruction

Exactly one document says what to do: the brief. A second document may only
point to it. Checked by
`harness/tests/test_single_source_of_instruction.py`.
