# Simulation Principles (Non-Negotiable)

Unchanged from VictoriaProject's vision (see [VISION.md](../../VISION.md)).
F0 does not implement the simulation — it exists to wire countermeasures into
the harness/tests from day one so later milestones cannot silently violate
them.

## 1. One Source of Truth

World -> Country -> Province -> City -> District -> Building -> Family ->
Person. Views read this hierarchy; they never become parallel databases.

## 2. World-Terms Reasoning

Forbidden: "if famine then +20% crime."
Required: "they're hungry -> they seek -> some steal -> crime rises."

## 3. Physical Economy

Nothing teleports. Everything has origin, transport, storage, destination.

## The Seven Diagnosed Failure Modes

| # | Failure mode | Structural countermeasure |
|---|---|---|
| 1 | Double primary key (sim ProvinceId vs geometry cell_id) | ONE spatial primary key decided before any code (F1 ADR); any coexisting ID systems get a same-day, single-location, test-guarded translation |
| 2 | Declared field nobody writes | write-coverage test per model field: writer + reader, red otherwise |
| 3 | Terminal variable (computed, read by nobody) | before opening a lever, verify its consequence reaches something measurable outside its own module |
| 4 | Presentation re-implementing the simulation | presentation READS, never decides; single render path or proven equivalence |
| 5 | Hardcoded counters | a counter derives from the data, or it doesn't exist |
| 6 | Control that names its own reference (empty sample, silent pass) | reference DERIVED from measurement; empty-sample checks must FAIL, never PASS |
| 7 | Producer pronounces its own acceptability | 3 roles, never one agent — see [harness-roles.md](harness-roles.md) |

F1 implements #1's ADR. F0 makes #7 real and #2/#3/#5/#6 mechanically
checkable in principle (`harness/verdict_audit.py`) even before sim code
exists.

## Operational fidelity (ADR-0018)

The three principles and the seven failure modes still hold. They do
not require census-grade historical inputs at t0, nor a closed-form
prediction that the running engine must match to a tight tolerance.
Order-of-magnitude proxies documented in `sim/SEEDING.md` are enough
for the living engine. The three-role harness remains available ; it
is no longer the default path of an ordinary product lot.
