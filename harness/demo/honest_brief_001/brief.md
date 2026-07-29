# Brief 001 (DEMO — honest control case)

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

## Required Counters

| name | sample source | denominator |
|---|---|---|
| province_count | `test-world-3.json`, loaded | number of provinces actually loaded |

## Acceptable Waivers (if any claim of infeasibility arises)

| claim | required command | required error |
|---|---|---|
| (none arose in this run) | n/a | n/a |
