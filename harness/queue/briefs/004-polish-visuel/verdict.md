# Verdict — Brief `004` (polish visuel)

**Authored**: 2026-08-01T16:20:00
**Author**: forge-evaluateur

This file is the single document of truth for brief `004`'s evaluation. It
replaces the iteration-`1` and iteration-`2` verdict texts and carries the
chronology of all three passes explicitly, so no reader has to reconstruct
the history from `feedback/`.

## Chronology

| Pass | Date | Gate | Évaluateur verdict | Rows failing |
|---|---|---|---|---|
| Iteration `1` | `2026-08-01` | ACCEPT after that file was written | **REJECT** | SC`7` (disqualifying), SC`4`'s "dump technique" P1, SC`1`, SC`3` |
| Planificateur amendment `002` | `2026-08-01` | — | — | SC`1`/SC`3` given an "Outcome B — defect absent" path; iteration `1`'s honest investigation becomes PASS |
| Iteration `2` | `2026-08-01` | ACCEPT | **REJECT** | SC`3` (banner decimal separator) |
| Iteration `3` | `2026-08-01` | ACCEPT | **PASS** | none |

Iteration `1`'s findings, verbatim in substance, so they are not lost:

- **SC`7` FAIL (disqualifying)** — the literal string `A_REVOIR_HUMAINEMENT`
  was present in `generator-log.md` and absent from `manifest.json`.
- **SC`4` P`1` "dump technique" FAIL** — `02_country_selected.png` in default
  mode rendered rows labelled `LAWMOD` (`0 EFF 0,002 %`) and `STAB`
  (`0,57 LEG 0,87`); the log declared the item already closed.
- **SC`1` and SC`3` FAIL** — on the old rubric's `> 0` counter floors only.
  The Générateur measured, found the named defects absent, and refused to
  fabricate a "before" defect. I confirmed both findings independently and
  attributed the failure to the rubric, not to the work. The Planificateur
  closed that gap in `amendment-002-absent-defect-waiver.md`.
- Everything else passed, including the HOVER gate, the P`0` confirmations,
  the reference suite, the `7` frozen legacy files and every Non-Goal.

Iteration `2`'s single finding, kept for the same reason:

- **SC`3` FAIL** — every capture in the brief, including iteration `2`'s
  freshest, rendered the player banner with a dot decimal separator
  (`Trésor -269.8`, `Dette 0.0`) while the country panel below rendered the
  same quantity as `Trésor 4,6`. `scientific_notation_before_count` had been
  measured over the fiscal panel only, a surface where the defect does not
  live, and never over the surface SC`3` names. Attribution was recorded
  then and stands now: `feedback-001.md` Issue `8` had told the Générateur
  not to work SC`3`, and my own iteration-`1` review transcribed the
  defective banner string without noticing it. The row could not be marked
  PASS, but the Générateur was not at fault for it.
- Everything else in iteration `2` passed, including both blocking items
  from `feedback-001.md`, closed and verified.

## Mechanical Gate Result

Pre-verdict run for this pass captured to
`harness/queue/briefs/004-polish-visuel/evaluateur-gate-iter3-before-verdict.log`;
post-verdict re-run to
`harness/queue/briefs/004-polish-visuel/evaluateur-gate-iter3-after-verdict.log`.
Per hard-won `rule 12` both are cited by path and their figures are not
re-typed here. Iterations `1` and `2`'s four gate logs are retained
unmodified.

Both iteration-`3` runs exit `0`, `VERDICT: ACCEPT`, every row green.

**The mechanical ACCEPT still does not make this brief a PASS by itself.**
It was ACCEPT in iteration `2` as well, and iteration `2` was a REJECT. The
gate cannot see whether a counter measured the surface its own Success
Condition names — that is what iteration `2` failed on, and it is what I
re-checked first this pass. Everything below is my own reconstruction from
source data, not a reading of `manifest.json`.

## What I Re-Executed vs What I Judged On Pièces

Re-executed / re-derived myself this pass:

- `py harness/verdict_audit.py`, twice; logs cited above.
- The **full** `git diff` of iteration `3`'s two changed files, read line by
  line, plus the diff of the three files iteration `2` changed, to confirm
  they are untouched this pass.
- `grep` for `Fmt1`/`Fmt0`'s definitions in `WorldMetrics.cs`, plus
  `git status` scoped to that file, to prove the formatter itself is
  unchanged; plus a tree-wide listing of every file calling
  `WorldMetrics.Fmt1`/`Fmt0`, and a search for consumers of
  `FormatPanelLine`.
- Independent re-derivation of `scientific_notation_before_count` and
  `_after_count` from the two cited evidence logs, by extracting every
  `info=` field and counting dot-decimals — and, separately, an exhaustive
  scan of **all** dot-decimals anywhere in both iteration-`3` logs, not just
  the banner field, to check for a surface the Générateur might have missed.
- Independent re-count of the `editorial_probe`/`editorial_forbidden` `scope=`
  annotations, and of the forbidden-token battery in both iteration-`3` logs
  (default and debug).
- `sha256sum` over every member of all `7` declared `must_differ_from` pairs,
  over the `01_world_neutral`/`01_world_neutral_b` duplicates in both
  iteration-`3` galleries, and over all `7` legacy-attributed test files —
  plus `git rev-parse` of each legacy file's blob at `HEAD` against
  `git hash-object` of the working tree, and `git log` scoped to
  `Assets/Tests/`.
- Full stdlib `xml.etree` re-parse of the fresh `v004c_test-results.xml`,
  counting `test-case` elements individually rather than trusting the root
  `total=` attribute, and re-listing every non-`Passed` `fullname`.
- `grep -c "error CS"` across all `14` declared Unity Editor logs, one file
  at a time, plus the four standalone-player capture logs that are correctly
  excluded from that denominator.
- Re-read of `deliverables/evidence/unity-lock-checks.log` stanza by stanza,
  correlated against each named invocation's own log mtime and, for the test
  run, against the XML's own `start-time`.
- Re-derivation of the accent battery from `v004_accent_capture.log`.
- The `generator_git_commits_count` query verbatim; `git log -1` to confirm
  `HEAD` is unmoved.
- Hex-literal anchor scan and full-context `ADOPTE`/`ADOPTÉ` scan of both
  deliverables.
- mtime ordering of the whole iteration: source edits → build → captures →
  evidence logs → test XML → diagnostic, to prove the "before" evidence
  genuinely predates the fix and the "after" evidence genuinely postdates it.
- Direct visual inspection of **every** image in the iteration-`3` gallery:
  `v004_after3_default/{01,02,03,04,05,06,07}` and
  `v004_after3_debug/{01,02,03,04,05,06,07}`, plus
  `v004_after2_default/04_pause_active.png` as the before-state. The two
  `01_world_neutral_b.png` files are byte-identical to their
  `01_world_neutral` siblings in each gallery — verified by hash rather than
  opened twice.

Judged on pièces, not re-executed, per the rubric's Session-Cost
Calibration:

- The EditMode suite was not re-run by me. I judged it on the fresh
  machine-generated XML with start- and end-time present, every case counted
  individually, and on the untouched state of `Assets/Tests/` proven three
  independent ways. No Unity process was launched by this pass; no
  `Temp/UnityLockfile` was created or contended.

## Independent Reconstruction of Every Counter

Every value below was reconstructed from source data by me. Counters that are
new or changed this iteration are marked.

| Counter | Manifest | My independent reconstruction | Agrees? |
|---|---|---|---|
| `compile_error_cs_count` (denominator widened) | `0` / `14` | zero occurrences of `error CS` in each of the `14` declared Editor logs, counted one file at a time; the four excluded standalone-player logs are also clean, and their exclusion matches the counter's own stated scope | yes |
| `unity_lockfile_checked_before_invocation_count_evidenced` (**new**) | `7` / `7` | the `7` stanzas the `command` field names all exist in `unity-lock-checks.log` and each precedes its invocation's own log; the `v004c_tests` stanza precedes the XML's own `start-time` by seconds. Value reproducible from the named list | yes — but see Feedback `1` on the note's arithmetic |
| `unity_lockfile_checked_before_invocation_count_asserted` (**new**) | `7` / `7` | iteration `1`'s `7` Editor invocations, resting on contemporaneous prose only, correctly labelled asserted and no longer summed with the evidenced half | yes |
| `accent_defect_present_before_count` | `0` / `11` | `v004_accent_capture.log`: exactly `11` `sanitize_battery` name lines, every one `unmapped_count=0`, trailer `sample=11`; iteration `3`'s diff touches no accent-fold file | yes |
| `accent_defect_present_after_count` | `0` / `11` | same battery, same run | yes |
| `debug_leak_default_mode_count` (re-measured) | `0` / `7` | my own token grep over the fresh `ui_003_visual-after3_default.log` returns **empty**; confirmed by eye on `after3_default/02` and `/07` | yes |
| `debug_leak_explicit_debug_mode_count` (re-measured) | `3` / `7` | same grep over `-after3_debug.log` returns `HOVER`, `TICK` and `ZOOM Pays C0` and nothing else; confirmed by eye — `Tick 5 … HOVER Île-de-France` in the debug banner, `Pays C0` in the view label | yes |
| `p1_lawmod_row_gate_toggle_flag` (re-verified) | `1` / `1` | opened both `02_country_selected.png`: default's `Indicateurs` block ends at `Armée` with no `Modificateur des lois` / `Taux effectif` row; debug shows both, in French. A real toggle | yes |
| `p1_country_panel_technical_id_leak_default_count` (re-measured) | `0` / `4` | whole-word count of the `4` raw tokens in the default `tag=02` editorial block returns `0`; by eye the panel reads `Stabilité 0,58` / `Légitimité 0,87` | yes |
| `p1_lois_panel_lawmod_leak_default_count` (re-measured) | `0` / `1` | `Lois` panel in default `02` reads `En vigueur : (aucune)`, no suffix, read by eye | yes |
| `p1_lois_panel_lawmod_leak_debug_reachable_flag` (re-measured) | `1` / `1` | `Lois` panel in debug `02` reads `En vigueur : (aucune)  ·  lawmod=0`, read by eye | yes |
| `editorial_probe_scope_annotation_present_count` (**new**) | `8` / `8` | I listed the annotated lines myself: `2` lines × `4` `AssertEditorial` calls, each carrying `scope=CountryPanel+TaxStatus+TaxButtons` or `scope=ProvincePanel+TaxStatus+TaxButtons` | yes |
| `scientific_notation_before_count` (**re-scoped**) | `16` / `18` | I extracted all `8` `info=` fields from `ui_003_visual-after2_default.log` and counted dot-decimals: `16`. All `8` tags carry a dotted `Trésor` and a dotted `Dette`. The `2` fiscal-panel `Taux` fields in the same log are already comma-formatted, so `0` of `2` — total `16` of `18` | yes |
| `scientific_notation_after_count` (**re-scoped**) | `0` / `18` | same extraction on `ui_003_visual-after3_default.log`: `0`. Beyond the counter's own method I scanned **every** dot-decimal anywhere in that log: the only hits are the Copernicus/`CC BY` licence version in the credits string and the run's own start timestamp. No numeric quantity anywhere in the default-mode HUD still uses a dot | yes |
| `p1_pause_ambiguity_addressed_flag` (re-verified) | `0` / `1` | fresh `after3_default/04_pause_active.png` opened: red `EN PAUSE` badge distinct in position, colour and text from the `Lecture` action button, banner reads `EN PAUSE` | yes |
| `p0_regression_check_count` (re-verified) | `2` / `2` | `source=standalone framebuffer`, `composer=NONE`, `debug_ids=False` in the fresh default log header; `01`, `02`, `03` opened — real UI Toolkit chrome, exactly one panel, no bitmap diagnostic painted into the map texture | yes |
| `visual_proof_pairs_distinct_count` (**revised**) | `7` / `7` | the manifest declares exactly `7` `must_differ_from` keys, one per pair, no direction declared twice. I re-hashed all `14` members: every pair differs, and the three new hashes match the Générateur's own quoted prefixes exactly | yes |
| `reference_suite_total_count` | `266` | `274` counted `test-case` elements minus `7` legacy fixtures minus `1` `Skipped` reproduces `266` | yes |
| `reference_suite_passed_count` | `266` | `265` directly-passing plus V`1095` under its documented correct invocation (`6` verdicts `VERT`, agreement figure re-derived from the fresh diagnostic, not cited by value) reproduces `266` | yes |
| `test_total_count` | `274` | `274` `test-case` elements counted individually; root attributes agree | yes |
| `test_failed_count` | `8` | `8` `Failed`, `1` `Skipped`, rest `Passed`; the `8` fullnames match brief `003`'s attributed set exactly | yes |
| `legacy_attributed_test_files_unchanged_count` | `7` | all `7` SHA256 identical to the iteration-`2` file, **and** git blob IDs identical at `HEAD` and in the working tree, **and** the only commit that ever touched `Assets/Tests/` is brief `003`'s own port commit | yes — proven more strongly than claimed |
| `generator_git_commits_count` | `0` / `1` | the query returns `0`; `HEAD` is unmoved at brief `004`'s pre-iteration checkpoint; only unstaged working-tree changes exist | yes |

No counter was found fabricated, and this pass — unlike iteration `2` — found
no counter measured against a surface other than the one its Success
Condition names.

## The One Thing I Was Asked to Check Hardest: `Fmt1` Invariance

The instruction to fix SC`3` at the call site only existed because
`WorldMetrics.Fmt1` feeds parity/determinism log lines that tests read
byte-for-byte. I verified the constraint was honoured, three ways:

1. `WorldMetrics.cs` is **not** in `git status`. `Fmt0` and `Fmt1` are still
   `ToString("F0"/"F1", CultureInfo.InvariantCulture)`, unchanged.
2. `MapDisplaySystem.cs` now contains exactly one `WorldMetrics.Fmt*` call —
   the `Armée` line, still `Fmt0`. `Armée` is an integer format with no
   decimal point, so it never could carry a separator defect; leaving it is
   correct, not an omission. The `Trésor`/`Dette` appends now go through
   `HudValueFormatter.FormatNumber(value, "0.0")`, which is the same function
   the UI Toolkit panels already use, and which formats `Invariant` then
   replaces `.` with `,` — so precision is identical to `F1` and only the
   separator changes.
3. The `12`-plus test files that call `Fmt1`/`Fmt0` are untouched and their
   results are unchanged: `274`/`265`/`8`/`1`, same `8` attributed fullnames
   as brief `003`. If the formatter had moved, those parity comparisons
   against hard-coded `"0.0"`-style reference strings would have moved with
   it. They did not.

`FormatPanelLine` has exactly one caller, inside `MapDisplaySystem` itself,
and no test reads it. The blast radius of the change is the player banner and
nothing else.

Correction to my own record, since a verdict should not carry a wrong quote:
`feedback-002.md` described `Fmt1` as `ToString("0.0", …)`. It is
`ToString("F1", …)`. The Générateur quoted it correctly in its log. My
paraphrase was the inaccurate one.

## The Hash-Caveat Question

The Générateur volunteered that pairs E/F/G's hash differences do not by
themselves prove the fix, because the world keeps ticking between two
separately-launched player runs, so two captures of the same scenario differ
even with no code change — and it stated that the load-bearing proof is the
textual reading of the `info=` field. I was asked to rule on whether that
caveat is handled honestly and whether the pairs remain honest under it.

**It is handled honestly, and the pairs remain honest.** Reasoning:

- A `must_differ_from` declaration asserts that two files differ. They do; I
  re-hashed all `14` members myself. It does not assert *why* they differ,
  and nothing in the manifest claims it does.
- The caveat is written into `manifest.json`'s own counter note — the place a
  reader of the number will actually look — not buried in prose. That is the
  correct location. Volunteering it before I raised it is the behaviour I
  asked for in `feedback-002.md` Issue `6` applied one step ahead of the
  feedback.
- The substantive proof is independently verifiable and I verified it two
  ways, without relying on the hashes at all: mechanically, `16` dotted
  banner fields before and `0` after over identical field names in identical
  scenario tags; and by eye, `after2_default/04_pause_active.png` reading
  `Trésor -269.8  Dette 0.0` against `after3_default/04_pause_active.png`
  reading `Trésor -652,1  Dette 0,0` in the same badge state, same panel
  layout, same scenario.
- The before-state evidence genuinely predates the fix: the iteration-`2`
  captures and their log were written at `11:09` local, the two source files
  were edited at `12:08`/`12:09`, the player was rebuilt at `12:11`, and the
  iteration-`3` captures and log were written at `12:16`–`12:17`. I checked
  the mtime chain rather than taking the ordering on trust.

The alternative — a magnitude that matched between before and after — would
in fact have been the suspicious result, since it would imply the capture was
not freshly re-run.

## Per-Rubric-Line Verdict

Judged against `eval-rubric.md` **as amended by
`amendment-002-absent-defect-waiver.md`**.

| # | Success Condition (rubric row) | PASS/FAIL | Evidence |
|---|---|---|---|
| Precondition | No Unity work before the orchestrator's release signal | PASS | Every iteration-`2` and iteration-`3` Editor invocation is preceded by a timestamped lockfile/process stanza in a declared evidence file; the `v004c_tests` stanza precedes the XML's own start-time. The iteration-`1` half is now explicitly labelled asserted rather than silently summed with the evidenced half |
| `1` | Accent transliteration — Outcome B (defect absent) | PASS | Unchanged and unregressed. `sample_size` is real and non-empty (`11` named accented labels with their folded outputs, from a cited, actually-run battery over the game's own `StreamingAssets` data); I re-derived the battery again this pass. Iteration `3`'s diff touches no accent-fold code path — I checked by file, not by claim. Confirmed by eye on the fresh gallery: the hover tooltip renders `Île-de-France` complete, and the province panel title likewise |
| `1` | No already-correct transliteration touched | PASS | The full iteration-`3` diff is two hunks: `FormatPanelLine`'s two appends plus a comment, and `AssertEditorial`'s scope tracking. `RestorePresentationName`, `MapSnapshotExporter.cs`, `MapLabelLayout.cs`, `CityPresentation.cs` remain untouched across all three iterations |
| `2` | Debug leakage hidden by default, still reachable in debug mode | PASS | Both declared pairs re-hashed and distinct; all four frames opened in iteration `2` and the equivalents re-opened fresh this pass. Default `after3` carries none of the `7` token categories — my own grep returns empty; debug `after3` carries `TICK`, `HOVER Île-de-France` and `Pays C0`. Iteration `3` touched the file that hosts this gate and did not disturb it |
| `3` | Player-banner decimals in French format, no scientific notation | **PASS** | **Iteration `2`'s single failing row is closed.** Judged under the rubric's Outcome A: `scientific_notation_before_count` = `16` > `0` and `_after_count` = `0`, both re-derived by me from the two cited logs; three declared `must_differ_from` pairs, all re-hashed and distinct. Manual check, non-waivable: I opened the after-capture's fiscal panel directly — `Taux 0,002 % · plage 0 % – 0,02 %`, comma decimal, no exponent — **and** the surface the condition actually names, the player banner, which now reads `Trésor -652,1  Dette 0,0` where iteration `2`'s identical scenario read `Trésor -269.8  Dette 0.0`. Beyond the counter's own sample I scanned every dot-decimal in the whole default-mode log: none remains on any numeric quantity. The fix is at the call site only; `WorldMetrics.Fmt1`/`Fmt0` are byte-identical and the parity tests that read their output are unmoved |
| `4` | Two P`0`s confirmed still closed | PASS | Re-confirmed on fresh iteration-`3` captures: `source=standalone framebuffer`, `composer=NONE`; `01`, `02`, `03` opened — real UI Toolkit chrome, exactly one panel, no bitmap diagnostic in the map texture |
| `4` | Pause-ambiguity P`1` inspected first, fixed only if open | PASS | Flag is `0`, never omitted; re-confirmed on a fresh iteration-`3` capture, where the red `EN PAUSE` badge and the `Lecture` action button are distinct in position, colour and text — and the same frame now also carries the comma decimal |
| `4` | No defect fixed outside the four named Success Conditions | PASS | Iteration `3`'s entire source footprint is `2` files under `Assets/Scripts/Presentation/`. `MapDisplaySystem.cs` is `2` changed lines plus a `6`-line comment, all inside `FormatPanelLine`, traceable to SC`3`. `UiStandaloneCaptureHarness.cs` is a log-annotation change inside `AssertEditorial` — it adds a `scope=` string and changes no collected text, so it fixes nothing and hides nothing; it closes `feedback-002.md` Issue `2` by disclosure rather than by widening behaviour, which was the lower-risk of the two options I offered. The `Investir` block and `Promulguer land_tax` were correctly left untouched |
| `5` | Gallery fresh, from the ported location, all three fixes visible | PASS | I opened all `16` iteration-`3` gallery images (`14` distinct; the two `_b` duplicates verified byte-identical by hash). Accent fold visible in the live tooltip and the province panel title; hidden debug tokens visible as a default/debug contrast in `02` and `07`; French comma decimals now visible in the banner **and** the panels of every frame. All mtimes postdate the corrected `Authored` time |
| `5` | Standalone chain used if buildable, waiver honest if not | PASS | The chain was rebuilt and re-run again this iteration; `waivers` is correctly empty |
| `6` | Reference suite `100%` green, fresh, after the changes | PASS | `266` of `266` re-derived by me from the fresh iteration-`3` XML, whose run window postdates this iteration's source edits and captures. Zero regression from the two-file change |
| `6` | The `7` legacy-attributed files untouched | PASS | Proven three independent ways again: identical SHA256, identical git blob IDs at `HEAD` and in the working tree, and no commit other than brief `003`'s own port has ever touched `Assets/Tests/` |
| `7` | Artistic verdict literally `A_REVOIR_HUMAINEMENT` in **both** files | PASS | Still a top-level `artistic_verdict` key in `manifest.json` and present in `generator-log.md`. Every `ADOPTE`/`ADOPTÉ` occurrence in either file, read with context, is a permitted quotation of `task_v1_056.json`'s constraint or an explicit negation. The status was held through a negative human verdict and through a successful iteration — the two opposite temptations — and was not flipped in either direction |
| Non-Goal | No new screen/panel/view, no external asset | PASS | Iteration `3` added no file at all — two modified `.cs` files and two capture directories |
| Non-Goal | No test weakened/deleted/skipped | PASS | The fresh total equals brief `003`'s exactly; `Assets/Tests/` byte-identical and never committed against |
| Non-Goal | Zero simulation-logic lines; no new coupling | PASS | I read both diffs line by line. Every changed line is under `Assets/Scripts/Presentation/`. `FormatPanelLine` now depends on `HudValueFormatter`, a sibling presentation class already used for the same purpose by `HudDetailPresenter` — no new layer crossing, and no read or write reaches `Core`/`World`. `AssertEditorial`'s change is local string bookkeeping |
| Non-Goal | No anchor rebased or cited by value | PASS | No hex literal in either deliverable; the V`1095` diagnostic was re-run fresh and its verdicts re-derived rather than cited from the previous run |
| Non-Goal | No hand-written `.meta` | PASS | No new file, therefore no new `.meta`; `git status` scoped to `unity/` shows none |
| Non-Goal | No git commit by the Générateur | PASS | `HEAD` is unmoved; the query window returns `0`; only unstaged working-tree changes exist |
| — | Mechanical gate, remaining rows | PASS | See the cited logs |

## Overall Verdict: PASS

The rubric's Overall Verdict Rule is "ACCEPT only if every numbered row
passes AND the gate exits `0`". The gate exits `0`. Every numbered row
passes. **PASS.**

I say that having tried to break it: I re-derived every counter from source
rather than reading the manifest, I checked the surface SC`3` names rather
than the surface the counter samples — which is exactly the trap that made
iteration `2` a REJECT — I scanned for dot-decimals beyond the counter's own
method in case a second emission point existed, and I opened every image in
both galleries. Nothing came back open.

The plateau rule was consulted and is moot: SC`3` was marked FAIL twice for
two different reasons, and the second one had a specific named fix, which was
applied and works. The row moved.

## Boundary Violations

None. Iteration `3` is the tightest-scoped of the three: one blocking row,
one call site, plus a log-annotation change that alters no behaviour and no
collected text.

Two observations that are not violations:

1. `Promulguer land_tax` still renders a raw snake_case law identifier in
   both modes. It is correctly **not** fixed and correctly **reported** as an
   open finding this iteration, which is what I asked for. It is brief-`005`
   input.
2. In the iteration-`3` debug run, `05_tax_min` shows `Taux 0,002 %` where
   the default run's `05_tax_min` shows `Taux 0 %` — the scenario's tax-down
   step did not land identically in the two runs. It affects no counter
   (`scientific_notation_*` is measured on the default run, and both values
   are comma-or-integer, neither dotted), and no rubric row depends on the
   debug run's fiscal values. Recorded so a future reader does not mistake it
   for evidence of anything.

## What Improved Since Last Iteration

Named specifically, because the feedback loop needs calibration in both
directions and these are real:

- **The blocking row is genuinely closed, at the call site, exactly as
  constrained.** The temptation was to change `Fmt1` once and be done;
  the Générateur instead enumerated every consumer of `Fmt1`/`Fmt0` first,
  found the parity tests that compare its output against hard-coded
  reference strings, and left the formatter alone. It also declined to
  touch the `Armée` line and said why — `F0` has no decimal point, so
  changing it would have been an unjustified extra edit. That is the correct
  reading of a bounded brief.
- **It chose `"0.0"` over `FormatMoney`'s `"0.#"` deliberately, and checked
  first**, because `"0.#"` renders a zero debt as `0` rather than `0,0` and
  would have silently changed the banner's precision while fixing its
  separator. Verifying a format string's edge case before committing to it is
  not a step most passes take.
- **It corrected me on my own quotation.** `feedback-002.md` mis-transcribed
  `Fmt1`'s format string; the log quotes the real one. A Générateur that
  copies the Évaluateur's wrong quote back is worth less than one that reads
  the source.
- **The caveat on pairs E/F/G was volunteered, in the manifest, before I
  raised it.** It is the same class of finding as my own Issue `6`, applied
  one step ahead of the feedback, and placed where a reader of the number
  will see it rather than in prose.
- **Every closure from iterations `1` and `2` was re-measured on fresh
  iteration-`3` evidence rather than carried forward as an assertion** —
  correctly, since the fix touched the file hosting the HOVER gate and the
  file hosting `AssertEditorial`. That instinct is what iteration `1` lacked.
- **Issue `5`'s blended counter was split rather than re-worded**, and the
  blended one was removed rather than kept alongside, so nobody can re-sum
  the two halves by accident.
- **Issue `3` was requalified honestly.** "No fresh capture proves that part
  open" became "a raw law id is rendered, in both modes, not fixed because
  X". Getting a finding's statement right is what lets brief `005` scope it.
- **Issue `4`'s reasoning was corrected without reversing the decision**, and
  the corrected reasoning cites `brief.md` and `REVUE-v1_054.md` rather than
  a feedback file — which was the whole point.
- **Unclaimed, and I checked it myself:** the iteration-`3` default and debug
  `02_country_selected.png` captures both select `Bourgogne`, where
  iteration `2`'s selected `Bourgogne` and `Champagne`. The toggle
  demonstration now holds the entity constant in the frames a reader will
  actually open, even though the declared pair still points at the older
  captures. The Générateur reported Issue `6`(b) as not done; in the frames
  that matter it effectively is.

## What Regressed Since Last Iteration

Nothing. I checked every iteration-`1` and iteration-`2` PASS that iteration
`3` could plausibly have disturbed, and checked them on fresh evidence rather
than by re-reading the old verdict: the HOVER/TICK gate still gates in both
directions, the `LAWMOD`/`EFF` row gate and the `lawmod=` suffix gate both
still toggle, the accent fold is untouched and its source files are absent
from the diff, the fiscal-panel comma decimals are unchanged, the pause badge
is unchanged, both P`0`s still hold, the reference suite and the failing-test
attribution set are byte-for-byte where brief `003` left them, the `7` frozen
files are unmoved, and `HEAD` has not advanced.

## Feedback for Next Iteration

There is no next iteration for this brief. Two residual items are recorded
for the record and for brief `005`, neither blocking, neither affecting any
row above:

1. `unity_lockfile_checked_before_invocation_count_evidenced`'s note names
   `2` excluded stanzas, but `3` of the log's `10` stanzas are excluded to
   reach `7` — the `v004b_capture_after2_default` stanza is dropped silently,
   for the same legitimate reason as the other standalone-player stanza.
   The value is still fully reproducible from the `command` field's explicit
   list, which is why this is not a FAIL. **Fix, if this counter recurs in a
   later brief**: enumerate the exclusions exhaustively, or state the count
   of stanzas excluded, so the note's own arithmetic closes without the
   reader deriving the missing one.
2. Standalone-player runs are checked against the lockfile in some cases and
   not others, and are correctly outside the Editor-invocation denominator.
   That distinction is sound but is only visible to a reader who already
   knows it. **Fix, if this recurs**: state the denominator's scope as
   "Editor invocations only" in the counter's `name` or its first sentence,
   not only in the exclusion note.

Carried forward to brief `005`, unchanged from `feedback-002.md` and
re-corroborated by my own inspection of this iteration's gallery: the live
map is still presented vertically flipped with mirror-inverted labels; the
`Lois` panel still overlaps and clips the `Impôt` heading's circumflex; the
`Investir` block's raw development dump, the `Promulguer land_tax` button
label, the `Sat 0,798` abbreviation and the war-row format remain open.

## Owner's Artistic Verdict — recorded as fact, unchanged

`owner-verdict-2026-08-01.md` records the owner's human judgement.

**Artistic status: not adopted — « visuellement c'est pas encore ça ».**

The eight grievances — inverted in-game map, initial view badly centred,
stutter on zoom, coarse border strokes, ugly red war-front edging, in-game
render differing from the captures, cluttered UI, suspected tick-rate
problems — fall outside Success Conditions `1`-`4` and are the input to brief
`005`. I have not used any of them to requalify Success Conditions `1`-`4`,
and I have not used this brief's PASS to soften any of them.

**This PASS is a rubric verdict on brief `004`'s stated conditions, and
nothing more.** Brief `004` did what it was scoped to do: three named
presentation-fidelity defects proven and closed, bounded, with no
embellishment and no simulation change. The owner's artistic rejection is a
separate, already-recorded fact, and it stands.
