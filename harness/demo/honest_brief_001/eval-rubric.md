# Eval Rubric — Brief 001 (DEMO — honest control case)

**Authored**: 2020-01-01T00:00:01

| Success Condition | Checked by |
|---|---|
| `province_count(test-world-3.json)` returns 3, measured against a loaded world (sample_size > 0) | mechanical gate: `no_empty_sample_pass` |
| Captures/logs are genuine (not identical before/after) | mechanical gate: `captures_differ_when_should` |
| Any infeasibility claim carries a real command + error | mechanical gate: `waivers_have_command_and_error` |
| Verdict is authored independently of the generator | mechanical gate: `verdict_is_not_self_authored` |
