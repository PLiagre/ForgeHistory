# Feedback 001 — Brief 003 (port VictoriaProject's Unity game)

**Authored**: 2026-07-31T20:40:00
**Author**: forge-evaluateur

Brief 003's verdict is **PASS** (`verdict.md`; mechanical gate ACCEPT, exit
code 0). Nothing below blocks that verdict. These are real defects and real
process lessons found during independent reconstruction, recorded so the next
brief inherits them rather than rediscovering them.

Each item states what is wrong, how it was found, and specifically how to fix
it.

---

## 1. `PresentationCache/` was excluded although it holds committed content

**What.** VictoriaProject tracks `51` files at HEAD under
`game_unity/PresentationCache/`. `9` of them are absent from the ported tree:

- `PresentationCache/README.md`
- `PresentationCache/Sprites/unit_cog_1400.png` + `.stamp`
- `PresentationCache/Sprites/unit_galley_1400.png` + `.stamp`
- `PresentationCache/Sprites/unit_carrack_1450.png` + `.stamp`
- `PresentationCache/Sprites/unit_galleon_1550.png` + `.stamp`

`unity/game_unity/Assets/Scripts/Presentation/MapSpriteOverlay.cs:138` names
exactly those four sprite keys in its generated-sprite list.

**How found.** Comparing every HEAD-tracked blob under `game_unity/` against
the ported tree:

```
git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD --format='%(objectname) %(path)' -- game_unity
```

then `git hash-object --no-filters` on each corresponding ported file.

**Whose defect this is.** Not the Générateur's. `brief.md`'s Success
Condition 1 fixed the eight-directory exclusion list and forbade extending or
narrowing it silently. The Générateur complied exactly. This is a planning
defect: the list was justified as "eight regenerable directories," and for
`PresentationCache/` that is only partly true — which is precisely the same
class of error `amendment-002.md` made about `Logs/` (there it was wrong;
here it is right, and nobody checked).

**Mitigating measurement.** `42` files under that directory regenerated
themselves during this brief's own Unity runs, so the sprite cache is largely
self-healing. The clearly non-regenerable loss is the tracked `README.md`.

**How to fix, specifically.** In the next brief, either:
(a) bridge the `9` missing files read-only from VictoriaProject using the
exact mechanic already proven twice in this brief (Cluster A's
`sandbox/geo/artifacts/` bridge and Cluster B's `Logs/` bridge), declaring
each with a SHA256 matching the source; or
(b) record in `ADR-0004` that they are deliberately left to regenerate, and
say what regenerates them and when.
Do not leave it implicit.

**Process rule to carry forward.** Before excluding any directory from a bulk
port, run `git ls-tree -r HEAD -- <dir>` **on every excluded directory**, not
just the one an amendment happens to force you to check. It is one command
per directory and it would have surfaced this in iteration 1.

---

## 2. The decisive Cluster C artifact was never consulted

**What.** `C:\Users\liagr\VictoriaProject\game_unity\Logs\testresults_full.xml`
is VictoriaProject's own unfiltered EditMode run (start-time `2026-07-28
18:17:32Z`). Its root attributes report the same total this port produced,
`274`, with `7` failed and `1` skipped — and its failing test `fullname`s are
**exactly** the seven this port attributes as legacy, with the same single
skipped case (`V1015CollapseDiagnostic`). Its sibling
`Logs\testresults_orient.xml` is the `25/25` orientation run `HANDOFF.md`
cites, all green.

**Why it matters.** That one file proves, from VictoriaProject's own
measurement rather than from date correlation, that the port introduced zero
regressions and that the `7` legacy reds were already red upstream. The
Générateur reached the right conclusion, but via `git log --follow` dates
plus v1_090's commit message — several iterations of work for something one
`ls -lat *.xml` in VictoriaProject's own `Logs/` would have answered.

**Note the irony worth internalising:** `amendment-002.md`/`amendment-003.md`
directed attention to `Logs/` twice (wrongly about tracked files, then
correctly about the bridge), and the answer to the hardest remaining question
was sitting in that same directory the whole time.

**How to fix, specifically.** When attributing any failure to "pre-existing
upstream," first search the upstream project's own artifact/log directory for
a run that measured it (`ls -lat *.xml`, newest first) and cite that run's
root attributes. Only fall back to `git log` date correlation if no such run
exists.

---

## 3. A premise in `unity/README.md` is disproven by the evidence above

**What.** `unity/README.md` states the unfiltered `-testPlatform EditMode`
run is one "which VictoriaProject itself may never have run in one pass."
`testresults_full.xml` shows it did, on `2026-07-28`, at the same total.

The hedge originated in `amendment-003.md`, which said "may"; the deliverable
repeats it as settled framing.

**How to fix, specifically.** Replace that clause with a citation of
`testresults_full.xml` and the fact that this port reproduces its result case
for case. That converts the weakest sentence in the README into the port's
single strongest piece of evidence.

---

## 4. Two `manifest.json` notes describe their own evidence inaccurately

**a. `robocopy_files_pending_copy_count`.** Its note says the measurement is
"unaffected by later remediation." It is not reproducible today: my own
list-only pass reports `201` files to copy, because `amendment-001`
deliberately made the port diverge from VictoriaProject's dirty working tree
(`77` files restored to HEAD content, `72` untracked strays removed) and
because the suite regenerated `Captures/`. The value of 0 was true when
measured and the copy **is** complete (verified by whole-tree blob
comparison) — the note is what is wrong.

*Fix:* restate it as an explicitly point-in-time iteration-1 measurement that
`amendment-001`'s remediation intentionally superseded, so a later reader does
not try to reproduce 0, fail, and conclude the copy is broken.

**b. `cluster_c_legacy_attributed_count`.** Its note says v1_090's commit
message is cited "not by inline hex fingerprint value." The verbatim capture
in `evidence/cluster-c-legacy-attribution-git-log.txt` does contain the
fingerprint, because it quotes the commit message in full. That is fine and
correct — raw captured output is exactly what hard-won rule `12` wants cited
rather than re-typed, and the rubric's grep row covers only the ADR and
`generator-log.md`, both of which are clean.

*Fix:* say the message is captured verbatim, fingerprint included as raw
output, rather than claiming the value is not inlined.

---

## 5. Minor: a counter denominator counts non-recursively

`captures_dir_test_reference_count` uses `86` as its denominator (top-level
`.cs` files under `Assets/Tests/`); recursively there are `91`. The
numerator, `28`, is identical either way and the row's floor is >= `1`, so
nothing turns on it.

*Fix:* state the denominator as "top-level `.cs` files" or count recursively.

---

## What to keep doing

Stated because it is genuinely well done and the loop should not be
calibrated only on defects:

- Recording `amendment-002.md`'s wrong Cluster B diagnosis explicitly,
  disproving it with three independent commands, and keeping the retired
  counters in `manifest.json` at their proven value instead of quietly
  swapping them.
- Keeping `V1095GpuMapTests` **inside** the reference suite and diagnosing it
  separately, when excluding it as "legacy" alongside the other seven would
  have been the easy path to a cleaner number. The dedicated no-`-nographics`
  diagnostic produced a decisive causal separation
  (`graphicsDeviceType = Direct3D11` and all `6` verdicts VERT, versus
  `graphicsDeviceType = Null` and ROUGE under the mandated invocation).
- Refusing a single "it's green" headline in `unity/README.md` and pointing
  at the real per-cluster state instead.
- Byte-verified, individually declared bridges for Clusters A and B, both
  consigned as temporary with the real future fix named.
