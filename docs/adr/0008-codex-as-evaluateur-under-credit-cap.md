# ADR-0008: Codex may hold the Évaluateur role, only in a third-party-triggered session, only when Claude is credit-capped

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-11
**Status**: accepted
**Deciders**: project owner (2026-08-11 decision, converted by brief 010 Lot 010a)

## Context

`docs/rules/harness-roles.md` reserves Planificateur and Évaluateur to
Claude and gives its reason: that reservation is what keeps
`verdict_audit.py`'s `verdict_is_not_self_authored` check meaningful — as
long as only one backend ever wrote a verdict, comparing the two role
strings (`forge-generateur` vs. `forge-evaluateur`) was enough to prove two
different sessions had acted.

The project owner decided on 2026-08-11 that Codex develops the project
**and** may replace Claude as judge once Claude's own credit cap is reached.
This decision is legitimate and makes the loop survivable — without it, all
work stops the moment Claude is capped. But applied silently it hits the
repository in two places: the written rule above (silently violated the
moment Codex judges anything), and the mechanical check itself, which
compares role strings, not actors — a gap independently closed in the same
lot by brief 010's SC3/SC3b/SC4 (see `harness/verdict_audit.py`,
`check_verdict_not_self_authored`). This ADR records the *rule* side of the
decision; the *mechanical* side is the code change, not this document.

## Decision

Four things, all in force together:

**(a) Codex may hold the Évaluateur role.** Not only Générateur (already
established by ADR-0002) — Codex may also write `verdict.md` for a brief or
lot it did not itself produce.

**(b) Only in a session distinct from, and triggered by a party other than,
the one that produced the lot.** The trigger is CI or the project owner —
never the Générateur session itself, and never a sub-agent that session
spawns. A session that just finished generating a lot does not get to turn
around and evaluate it, on any backend, under any name.

**(c) The "Générateur-spawned evaluation sub-agent" option is explicitly
rejected**, and not merely left unmentioned. A sub-agent launched by the
Générateur session is not independent of it: the producer of the work
frames its own judge — it writes the sub-agent's instructions, chooses what
evidence to show it, and consolidates its answer into the final verdict.
That framing power is exactly what makes a sub-agent-judge indistinguishable
from self-judgment in every way that matters, regardless of which model
happens to run inside the sub-agent. This is not a hypothetical: it is the
one shape of self-authorship a role-string check could never catch even
before Codex existed, and it stays rejected under this ADR.

**(d) The triggering fact is Claude's credit cap being reached** — not
convenience, not speed, not a preference for Codex's judgment on a given
lot. Codex-as-Évaluateur is the fallback that keeps the loop alive when
Claude cannot judge, not a parallel judging path chosen for other reasons.

`docs/rules/harness-roles.md` is updated to state (a)-(d) as the rule (see
that file); this ADR records the decision and its reasoning, it does not
duplicate the rule as a second instruction surface.

## Alternatives Considered

### Alternative 1: Leave Évaluateur reserved to Claude, unconditionally
- **Pros**: No change to the rule or the mechanical check; the simplest
  read of ADR-0001 stands.
- **Cons**: The moment Claude is credit-capped, the harness has no
  Évaluateur at all — work stops, not because a judgment was withheld for
  cause, but because the only judge ran out of budget. That is exactly the
  survivability gap the owner's decision exists to close.
- **Why not**: A harness that stops dead at the first credit cap does not
  survive contact with a real subscription limit.

### Alternative 2: Let the Générateur session spawn its own evaluation sub-agent when Claude is capped
- **Pros**: No third-party trigger needed; the loop never blocks on an
  external actor being available.
- **Cons**: This is self-judgment wearing a different name. The producer
  chooses the sub-agent's brief, its evidence, and its final wording — the
  three levers that make a verdict trustworthy are all held by the party
  being judged.
- **Why not**: This is (c) above — rejected explicitly, not by omission.

### Alternative 3: Let any backend hold Évaluateur at any time, not just under the credit-cap condition
- **Pros**: Maximum flexibility; no condition to track or enforce.
- **Cons**: Removes the one constraint that keeps Codex-as-judge a
  *fallback* rather than a *routine* path — nothing would stop a producer
  from picking whichever backend judges most favorably.
- **Why not**: The owner's decision names a specific triggering fact (the
  credit cap), not a general license; recording anything broader would
  overstate what was decided.

## Consequences

### Positive
- The loop survives a Claude credit cap without silently violating the
  written rule the whole harness depends on.
- The mechanical check now enforces actor independence directly (brief 010
  Lot 010a), so this ADR's rule and the gate that enforces it agree with
  each other.
- The rejected sub-agent option (c) is on the record, not just implied — a
  future session tempted by it has to argue against a decision already
  made, not invent the question from scratch.

### Negative
- One more conditional path in the harness rule for a future reader to
  learn: "Évaluateur is Claude, except Codex, except only when triggered by
  CI/owner, except only under the credit cap."
- Verifying condition (b) (session distinct from and triggered by a party
  other than the producer) is not yet mechanically checked by
  `verdict_audit.py` — this ADR records the rule; enforcing the trigger
  itself mechanically is not in this lot's scope and is left for a future
  brief if the owner wants it.

### Risks
- **A future session invokes Codex-as-Évaluateur for a reason other than
  the credit cap** (e.g. convenience), and nothing but this document's
  prose stops it. Mitigation: `verdict_is_not_self_authored` still refuses
  same-actor judgment regardless of the reason invoked, so the one
  guarantee that matters mechanically (no self-judgment) holds even if this
  ADR's narrower condition (d) is honored only in good faith.
- **A sub-agent gets relabeled to look like an independent session** (e.g.
  given its own `forge-evaluateur-codex` author line while still spawned
  and framed by the Générateur session). Mitigation: this ADR states (c) in
  writing, precisely so that shape of evasion has to be argued against a
  recorded decision, not slipped past silence.
