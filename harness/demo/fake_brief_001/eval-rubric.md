# Eval Rubric — Brief 001 (DEMO)

**Authored**: 2020-01-01T00:00:01

| Success Condition | Checked by |
|---|---|
| `province_count(test-world-3.json)` returns 3, measured against a loaded world (sample_size > 0) | mechanical gate: `no_empty_sample_pass` |
| Captures/logs, if any, are genuine (not identical before/after) | mechanical gate: `captures_differ_when_should` |
| Any infeasibility claim carries a real command + error | mechanical gate: `waivers_have_command_and_error` |
| L'identité déclarée reste informative | vérificateur : `actor_identity_is_neutral` |
