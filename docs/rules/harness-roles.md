# Harness Roles — Three Separate Roles, Never One Agent

> Chemin par défaut (ADR-0018) : Hermes prépare les grandes étapes ;
> Cursor découpe un brief large et exécute en parallèle. Ce fichier
> régit le **harnais optionnel** sous `harness/` : archive de preuves
> avec porte mécanique. On l'invoque quand on veut cette archive, pas
> à chaque lot produit. Si on l'invoque, la porte dit encore la vérité.

Core principle: "Celui qui produit ne prononce pas la recevabilité"
(whoever produces does not pronounce acceptability). The producer still
does not merge their own work, even when the three-role loop is skipped.

| Role | Agent file | Writes | Never |
|---|---|---|---|
| Planificateur | `.claude/agents/forge-planificateur.md` | brief + rubric, BEFORE code | codes |
| Générateur | `.claude/agents/forge-generateur.md` (or a backend under `harness/backends/`) | code, measurements, logs | pronounces "acceptable" |
| Évaluateur | `.claude/agents/forge-evaluateur.md` (Claude), or Codex under the exception below | verdict, vs. the pre-written rubric | modifies code |

The rubric is written before the work, by someone other than whoever is
judged — the only protection against a verdict that grades what was done
rather than what was asked.

## Backend-Pluggable Générateur

The Générateur role may be executed by the native Claude Code agent, or
delegated to another backend (currently: Cursor CLI, see
`harness/backends/README.md`) to spread cost across subscriptions and to get
a genuinely independent implementer.

## Évaluateur: Claude by default, Codex under one named exception

Planificateur stays on Claude, no exception. Évaluateur stays on Claude by
default, with exactly one exception, decided by the project owner and
recorded in `docs/adr/0008-codex-as-evaluateur-under-credit-cap.md`: Codex
may hold the Évaluateur role, and only under all of the following at once —

1. the session that judges is distinct from, and triggered by a party other
   than, the session that produced the lot (CI or the project owner —
   never the Générateur session itself, and never a sub-agent it spawns);
2. a Générateur-spawned evaluation sub-agent is not this exception and does
   not qualify under it — the producer of a lot frames its own sub-agent's
   instructions, evidence, and consolidated answer, which is self-judgment
   regardless of which backend runs inside it;
3. the triggering fact is Claude having reached its own credit cap, not
   convenience or a preference for Codex's judgment on a given lot.

This exception exists because a role reserved to a single, budget-limited
backend cannot survive that backend's own cap — see ADR-0008's Context. It
does not relax the harness's core rule ("whoever produces does not pronounce
acceptability"): `verdict_audit.py`'s `verdict_is_not_self_authored` check
compares the *actor* on each side of a lot, not the role string, precisely
so that a second backend holding either role never becomes an unmeasured
gap. See `harness/verdict_audit.py`'s `check_verdict_not_self_authored` for
the mechanical enforcement.

## Gate Tiers

1. **Mechanical** (free, instant) — `harness/verdict_audit.py`.
2. **Automatic** — compile/test, bit-identical parity, two-pass determinism.
3. **Adversarial** (NOT built in F0) — deferred until briefs touch the
   world/what-the-player-sees.

## Loop / Stop Discipline

The three-role loop is **optional** (ADR-0018). Default product work
goes Hermes (large steps) → Cursor (split + parallel execute) → PR.
When the loop *is* run:

- Plateau: two iterations without improvement -> STOP, don't replay.
- Feedback passed as a readable FILE (`harness/queue/briefs/*/feedback/`),
  never a log to dig through.
- Three failed retries -> escalate to a human.
- A mechanical REJECT from `verdict_audit.py` stops the lot. Nobody
  talks the gate out of a finding.

## Single Source of Instruction

Exactly one document says what to do: the brief. A second document may only
point to it. Checked by
`harness/tests/test_single_source_of_instruction.py`.
