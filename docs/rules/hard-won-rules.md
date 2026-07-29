# Hard-Won Rules

Each rule cost a real, measured defect in VictoriaProject. Written verbatim —
do not paraphrase (see failure mode #6 in
[simulation-principles.md](simulation-principles.md): a control that names
its own reference).

1. `py`, never `python` (the Microsoft Store alias is a fake stub on this
   Windows machine).
2. A check derives, it is never named after its target. (6 recurrences
   historically.)
3. A counter derives too.
4. Prove red first. A check that cannot go red proves nothing.
5. A guard placed after the effect it's meant to prevent protects nothing.
6. A check that's too coarse costs as much as a lax one.
7. Presence is not function.
8. A zero can be a real measurement — use sentinel `-1`, never `0`, for
   "not computed."
9. An impossibility is tested before being invoked: a command + an error
   message, or it's not a finding but an abdication.
10. When data is missing, the agent invents it silently by default — so
    absence must be DECLARABLE and the code must refuse to guess.
11. Look at captures yourself. Four major defects were found by eye that
    100%-green suites never caught.
12. A parity fingerprint is cited by NAME, never by VALUE — it will get
    rebased someday, and the doc holding the dead constant traps every
    subsequent brief.

## Enforcement in F0

- Rule 1 → `verdict_audit.py` check `no_bare_python_alias`, plus the live
  `PreToolUse` hook (`.claude/hooks/no_bare_python.py`).
- Rules 2/3/6 → check `no_empty_sample_pass` refuses hardcoded/degenerate
  sample sizes rather than trusting a named threshold.
- Rule 4 → `harness/tests/test_verdict_audit.py` proves each check goes red
  before any test proves it accepts.
- Rule 8 → sentinel `-1` convention for "not computed" fields repo-wide.
- Rule 9 → check `waivers_have_command_and_error`.
- Rule 12 → ADRs/docs cite fingerprints by pointer (e.g. "the log at
  `harness/demo/fake_brief_001/run_demo.log`"), never inline hex/values.
