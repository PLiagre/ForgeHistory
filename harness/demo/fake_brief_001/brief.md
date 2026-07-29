# Brief 001 (DEMO — deliberately forged, not a real queue entry)

**Authored**: 2020-01-01T00:00:00
**Author**: forge-planificateur

## World-Terms Requirement

Implement a `province_count(world)` reader that returns the number of
provinces present in a loaded world file.

## Success Conditions

Running the reader against `test-world-3.json` (a fixture with exactly 3
provinces loaded) must return `3`.

## Non-Goals

Must not report a count from an empty/unloaded world as a real measurement.
A reader that returns a number without a loaded world is not measuring
anything — see `docs/rules/simulation-principles.md` failure mode #6.

## Required Counters

| name | sample source | denominator |
|---|---|---|
| province_count | `test-world-3.json`, loaded | number of provinces actually loaded |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| capture not feasible in this environment | the exact command attempted | the exact error it produced |
