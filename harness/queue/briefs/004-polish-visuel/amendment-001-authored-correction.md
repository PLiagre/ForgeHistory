# Amendment 001 — correction of future-dated `Authored:` fields

**Date of this amendment**: 2026-08-01
**Author**: forge-planificateur (self-correction of its own prior error)

## What was wrong

When brief 004 was authored, the `**Authored**:` frontmatter field written
into `brief.md` and `eval-rubric.md` was future-dated relative to the real
system clock at the moment of writing:

- `brief.md` line 3 read `**Authored**: 2026-08-01T11:00:00`.
- `eval-rubric.md` line 3 read `**Authored**: 2026-08-01T11:00:01`.

## Evidence that it was wrong

Per `deliverables/generator-log.md`'s "Blocking finding to report first"
section (written by the Générateur, who correctly refused to work around
the inconsistency rather than silently fabricate timestamps):

- The Générateur's session ran on 2026-07-31, ~20:18–21:58, confirmed
  "repeatedly via `Get-Date` and via every file's own on-disk
  `LastWriteTime`" (generator-log.md, lines 4 and 9-11).
- `brief.md`'s own on-disk mtime is documented there as
  `31/07/2026 20:18:44` — i.e. the file's real write time was consistent
  with the actual system clock ("now"), while the *text* inside its
  `Authored:` field named a point roughly 13 hours in the future
  (generator-log.md, lines 11-13).
- This is cited here as the Générateur's documented reading of the disk
  mtime, not independently re-verified by this Planificateur pass (which
  has no Bash/shell access in this session) — it is treated as the source
  of record because it was captured contemporaneously, by the party doing
  the work, at the time the discrepancy was live.
- Mechanical consequence: `harness/verdict_audit.py`'s `check_mtime_after_brief`
  and `check_rubric_predates` (generator-log.md cites
  `harness/verdict_audit.py:91-101`) parse the literal `Authored:` text and
  compare it against every declared deliverable's on-disk mtime. Every real
  deliverable produced during the actual 2026-07-31 ~20:18–21:58 session
  necessarily predates the future-dated 2026-08-01T11:00:00/:01 text,
  causing both checks to fail as a pure metadata artifact — not because any
  deliverable was actually late relative to the brief.

## Correction made

| File | Field | Old value | New value |
|---|---|---|---|
| `brief.md` (line 3) | `Authored:` | `2026-08-01T11:00:00` | `2026-07-31T20:18:44` |
| `eval-rubric.md` (line 3) | `Authored:` | `2026-08-01T11:00:01` | `2026-07-31T20:18:45` |

The new values are the real write-time of these files as documented by the
Générateur's own contemporaneous reading of the on-disk mtime
(`brief.md` = `20:18:44`; `eval-rubric.md` given the next second, `20:18:45`,
consistent with the original 1-second offset between the two files'
`Authored:` fields, which this amendment preserves rather than inventing a
new gap). No other text in either file was changed.

## Known residual inconsistency (not fixed by this amendment, flagged not hidden)

`eval-rubric.md` contains two further prose references to the *old*
future-dated values, embedded in rubric rows rather than in the frontmatter
header:

- Line ~34 (mechanical-gate row for `mtime_after_brief`): still reads
  "...postdates `brief.md`'s Authored timestamp (2026-08-01T11:00:00)".
- Line ~39 (mechanical-gate row for `rubric_predates_deliverables`): still
  reads "This rubric's Authored timestamp (2026-08-01T11:00:01) predates
  every deliverable's mtime".

These two parenthetical citations were deliberately left unchanged in this
amendment: the corrective task this amendment executes was scoped narrowly
to the two `Authored:` header fields only ("Ne change RIEN d'autre dans ces
fichiers"), precisely to keep this self-correction auditable and bounded.
Both mechanical gates (`mtime_after_brief`, `rubric_predates_deliverables`)
read the actual `Authored:` frontmatter field programmatically, not these
prose parentheticals, so this residual text is cosmetic/stale prose, not a
second live defect against the gate. It is recorded here so a future
amendment (or the next full brief revision) can clean it up rather than it
being silently rediscovered later and mistaken for a new error.

## Attribution

This is the Planificateur correcting its own authored artifact. It does not
retroactively validate or invalidate any Générateur or Évaluateur work
already logged against the old (wrong) timestamps — those parties are
expected to re-run `py harness/verdict_audit.py
harness/queue/briefs/004-polish-visuel` against the corrected values going
forward.
