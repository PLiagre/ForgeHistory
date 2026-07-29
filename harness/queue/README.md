# Brief Queue

One directory per brief: `briefs/NNN-<slug>/`. See
`docs/rules/harness-roles.md` for the lifecycle (Planificateur writes
brief+rubric -> Générateur builds -> mechanical gate -> Évaluateur judges).

`queue.md` in this directory tracks the current queue state (which briefs
are pending/in-progress/done, and which backend ran each Générateur pass).
Empty until the first real brief is queued.
