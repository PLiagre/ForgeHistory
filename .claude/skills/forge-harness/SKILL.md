---
name: forge-harness
description: Run a ForgeHistory brief through the three-role harness (Planificateur/Générateur/Évaluateur) with a mechanical gate and a pluggable Générateur backend (Claude or Cursor).
metadata:
  origin: ForgeHistory
---

# ForgeHistory Harness

## When to Activate

When someone **explicitly** wants the three-role archive (brief + rubric +
mechanical gate + verdict). ADR-0018 : the default product path is Hermes
(grandes étapes) then Cursor (brief large, parallel sub-tasks, one PR).
Do not start this loop for an ordinary product change.

## Core Concepts

**"Celui qui produit ne prononce pas la recevabilité."** Three roles, never
one agent in the same pass: Planificateur writes the brief + rubric first;
Générateur builds and measures; Évaluateur judges against the pre-written
rubric. See `docs/rules/harness-roles.md` for the full contract.

Générateur is **backend-pluggable**: Claude Code by default, or Cursor CLI
via `harness/backends/run_cursor_generator.sh`. Either way, the deliverables
contract is identical (`deliverables/manifest.json` +
`deliverables/generator-log.md`), so the gate and the Évaluateur don't need
to know or care which backend ran.

## How the Loop Runs (optional path)

If the harness is **not** requested, stop here and follow ADR-0018.

```
1. forge-planificateur writes harness/queue/briefs/NNN-<slug>/{brief.md,eval-rubric.md}
2. Générateur (Claude forge-generateur, OR Cursor via run_cursor_generator.sh)
   implements it, writes deliverables/{manifest.json,generator-log.md}
3. py harness/verdict_audit.py harness/queue/briefs/NNN-<slug>
   -> REJECT: stop, the Évaluateur's feedback (if any) explains why, Générateur fixes and retries
   -> ACCEPT: proceed to Évaluateur review
4. forge-evaluateur independently reconstructs every counter, looks at
   artifacts itself, writes verdict.md (+ feedback/feedback-NNN.md if iterating)
5. Plateau rule: if 2 iterations in a row don't improve, STOP — escalate to
   a human instead of replaying the same prompt
```

## Anti-Patterns

- **Évaluateur overriding a mechanical REJECT** because the deliverables
  "look right" — the whole point of the gate is that it can't be talked out
  of a finding. Never do this.
- **Évaluateur praising its own suggested fixes** — it critiques, the
  Générateur fixes, never the reverse.
- **Feedback passed inline instead of as a file** — always
  `feedback/feedback-NNN.md`, never a conversational summary that isn't
  saved anywhere the next iteration can read it.
- **Trusting a `manifest.json` number without reconstructing it** — presence
  is not function (hard-won rule 7).

## Best Practices

- Every brief gets its own `harness/queue/briefs/NNN-<slug>/` directory —
  never reuse one across unrelated work.
- Route a brief to the Cursor backend when you want a genuinely independent
  second implementer, or simply to spread cost — not a default, a deliberate
  choice recorded in the brief itself.
- Cite log/report paths by name in `verdict.md`, never inline-paste a
  value/hash that could rot when something upstream is rebased.

## Related Skills / Docs

- `docs/rules/harness-roles.md` — the full role contract.
- `docs/rules/hard-won-rules.md` — the 12 rules every check/counter must obey.
- `harness/backends/README.md` — the pluggable-Générateur contract.
