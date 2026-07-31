# Amendment 003 — Brief 003 (port VictoriaProject's Unity game)

**Authored**: 2026-08-01T09:00:00
**Author**: forge-planificateur
**Context**: iteration 3 (`deliverables/generator-log.md`) reports Cluster A
resolved (the `sandbox/geo/artifacts/coordinate_correction_proposal_v1_072.json`
bridge, SHA256-identical both sides). Two new facts require this amendment:
`amendment-002.md`'s Cluster B diagnosis was factually wrong and the
Générateur proved it; and Cluster C's 8 remaining failures most plausibly
trace to what VictoriaProject's own "green" citations actually meant, not to
anything this port broke. Same constraint as amendment-001/002: `brief.md`
and `eval-rubric.md` are not edited. Read this together with `brief.md`,
`eval-rubric.md`, `amendment-001.md`, and `amendment-002.md` — it corrects
one part of the last of those and extends it, it does not restate any of
the rest.

## Correction to `amendment-002.md` — recorded explicitly, not silently

**`amendment-002.md`'s Cluster B section is factually wrong about where the
five `V1042SuiteBudgetTests` baselines live, and the error is recorded here
rather than quietly dropped.** It said these files were VictoriaProject's
own committed proof archives, parallel to `.gitignore`'s treatment of
`Library/`/`Temp/`/etc. as pure build cache. The Générateur checked this
directly and disproved it:
- `git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD -- game_unity/Logs`
  returns **nothing** — `game_unity/Logs/` has never had a single tracked
  file at HEAD.
- `git -C C:\Users\liagr\VictoriaProject log --all --diff-filter=A -- game_unity/Logs/v1_041_tests.xml` (and the `v1_077_large.xml` equivalent) find **no
  commit, ever**, that added either file.
- `C:\Users\liagr\VictoriaProject\.gitignore:35` ignores `game_unity/Logs/`
  wholesale.
- The files exist only as local, untracked, on-disk artifacts — 491 files
  present under VictoriaProject's own `game_unity/Logs/` at last count, none
  of them ever part of any commit.

So `Logs/` was, in fact, exactly what Success Condition 1 originally assumed
it was for **every** file inside it: regenerable/local, not committed state.
The correction is not "restore tracked files" (there are none) — it is a
narrower version of the same bridge mechanism `amendment-002.md`'s Cluster A
already authorized and iteration 3 already executed successfully.

## Cluster B (5 failures) — corrected authorization: read-only bridge, same mechanic as Cluster A

**Authorized remediation**, replacing `amendment-002.md`'s Cluster B section
in full (that section's `git ls-tree`/`git show HEAD:` mechanic for `Logs/`
is withdrawn — there is nothing at HEAD to extract):

1. **Derive the full set of files `V1042SuiteBudgetTests`'s 5 failing cases
   actually read**, from the test's own source
   (`Assets/Tests/V1042SuiteBudgetTests.cs`), not only from the assertion
   messages — read every method involved
   (`V1042_Suite_Budget_Fails_On_Precut_V1041_Xml`,
   `V1042_Suite_Budget_Holds_On_Latest_Xml`,
   `V1042_Suite_Budget_Holds_On_Session_Large_Xml`,
   `V1078_A_PerCase_Budget_Reds_On_Artificially_Slowed_Case`,
   `V1078_B_PerCase_Budget_Holds_When_Adding_Normal_Cost_Cases`) in full.
   List every distinct `Logs/...`-relative path referenced, one by one, in
   `generator-log.md` (already confirmed present:
   `Logs/v1_041_tests.xml`, `Logs/v1_077_large.xml` — do not assume this is
   complete without reading all five methods; some read a "latest"
   `*_large.xml`/`*_tests.xml` by glob pattern rather than one fixed name —
   if a method resolves its target by scanning `Logs/` for a pattern rather
   than a literal path, that scanning logic itself, and what it would
   actually find once the bridge files are in place, must be documented).
2. **Copy exactly those files, read-only, from VictoriaProject's own local
   `game_unity/Logs/` (not from any git ref — there is none) into
   `unity/game_unity/Logs/`** at the resolved path. Declare each
   individually in `manifest.json` with SHA256 matching the VictoriaProject
   source file exactly — same discipline as Cluster A's bridge.
3. **This restoration is additive** to `Logs/`, which already holds this
   brief's own `v003_*` evidence (Success Condition 3/4/5 logs) plus
   whatever a future reference-suite run below adds — confirm no filename
   collision before writing (VictoriaProject's `v1_NNN_*.xml`/`.log` naming
   is disjoint from this brief's `v003_*` prefix).
4. **Consign this as a temporary bridge**, same as Cluster A — these are
   local VictoriaProject artifacts a specific, narrow set of tests happens
   to read, not a durable ForgeHistory data source.
5. **Alternative, per test, if warranted**: declare a specific case NOT
   APPLICABLE with a named, causal reason, same discipline as Cluster A —
   default is to supply the artifact. One relevant, honest data point to
   weigh, not a predetermined outcome: `HANDOFF.md` (line 741, VictoriaProject's own words) already flags this exact
   mechanism as weak — "⚠️ Le budget de temps livré avec, lui, ne mord pas"
   (the delivered time-budget check itself doesn't bite), said about the
   very v1_042 guardrail these 5 tests belong to. That is a real,
   VictoriaProject-authored data point for judging whether any of the 5
   cases is better declared NOT APPLICABLE than bridged — it does not by
   itself decide the question, and either resolution (bridge or a
   specifically-justified NOT APPLICABLE) is acceptable per case, following
   the same "default is to supply, exception is named and per-case" rule
   Cluster A already used.

## Cluster C (8 failures) — the reference suite VictoriaProject actually maintained, established by proof

**The premise behind treating these 8 as straightforward "parity drift" was
itself unexamined.** VictoriaProject never ran, and never claimed to run,
the full EditMode suite as its acceptance bar. Its own citations were
curated, per-brief filters:
`cursor_tasks/done/v1_093_result.json`'s `tests.filter` field reads
literally `"LARGE (v1_092 + V1093, parité V1009 incluse)"` — an additive,
brief-specific set, not a fixed NUnit category — and `HANDOFF.md` separately
cites a distinct `"Filtre orientation + cartes : 25/25 verts"` for the
`v1_095b` orientation work. `v1_042`'s own section states three sweep tests
were deliberately taken **out of** the default EditMode filter and made
callable only via their own `BatchRunner`. This port's Success Condition 4,
by contrast, runs the unfiltered `-testPlatform EditMode` set — something
VictoriaProject itself may never have run in one pass at this project's
current size. `total = 274` may simply have never been a number
VictoriaProject ever produced or accepted as a bar.

**One structural fact discovered in the course of this investigation, worth
flagging on its own**: `Assets/Tests/V1095GpuMapTests.cs`'s own doc comment
on `V1095BatchRunner` reads literally `-executeMethod
VictoriaGame.Tests.V1095BatchRunner.Run (SANS -nographics)` — i.e.
VictoriaProject's own source already documents that this specific test
requires a graphics-enabled batchmode invocation, because it asserts on a
real GPU shader path (`MapGpuRenderer.IsAvailable`,
`SystemInfo.graphicsDeviceType`). **Every Unity invocation Success
Conditions 3/4/5 specify includes `-nographics`.** This is a plausible,
concrete, pre-existing, VictoriaProject-documented reason
`V1095_Artifacts_And_Verdict` fails under this brief's mandated invocation —
independent of, and more specific than, the general "maybe HEAD-restoration
re-exposed something" hypothesis `generator-log.md` raised in iteration 2.

**Mandated procedure:**

1. **Establish the reference suite by proof**, not assumption. Search
   `C:\Users\liagr\VictoriaProject\automation\` (`run_queue.py`,
   `proof_kind.py`, `asset_runner.py`, `auto_runner.py`) and
   `Assets/Tests/*BatchRunner` classes for the exact filters/entry points
   VictoriaProject actually used for its last cited green runs at or near
   HEAD (`06c2e59`). Document, by path, every source actually found —
   including if the honest conclusion is that no single canonical filter
   file exists and the reference suite must be reconstructed from several
   partial sources (`cursor_tasks/done/*_result.json`'s `tests.filter`
   fields, `HANDOFF.md`'s prose citations, the `*BatchRunner` classes'
   own doc comments). That reconstruction, cited source by source, **is**
   an acceptable answer to this step — it does not need to resolve to one
   single file to satisfy this mandate, but it must be evidenced, not
   asserted from memory of what "LARGE" probably meant.
2. **Run that reconstructed reference suite from the ported, fully
   remediated (Clusters A+B) tree.** Requirement: **100% green** on this
   reference suite specifically — this is the bar VictoriaProject itself
   actually cleared before `v1_095b`, and it is the bar this port must
   clear to honestly claim "the functional code that was green now runs
   from its new location."
3. **For each of the 8 (or however many remain after 1-2), establish by
   proof whether it belongs to the reference suite** — and if not, since
   when it has been outside it:
   ```
   git -C C:\Users\liagr\VictoriaProject log --follow -- game_unity/Assets/Tests/<TestFile>.cs
   ```
   compared against the dates of any anchor-rebasing work the log
   identifies (e.g. `v1_090`'s rebase, cited by name/section only, never by
   inline hex value — hard-won rule 12). **If a failing test is legacy —
   outside the maintained reference suite, and its anchor predates a rebase
   the reference suite's own maintained tests moved past** — attribute it
   individually: "legacy, hors suite maintenue, ancre antérieure au
   rebasage <name>," and **leave the test exactly as it is**: not weakened,
   not deleted, not rebased. The red is assumed and documented, not hidden
   and not fixed. **If any of the 8 turns out to belong to the reference
   suite** (i.e. it was part of what VictoriaProject actually kept green),
   that is a real port defect — diagnosis continues for that specific case
   using the measured-bisection method `amendment-002.md` already mandated
   (compare the specific diverging file/data against a fresh, independent
   `git show HEAD:...` extraction), still without weakening the test or
   rebasing its anchor absent the three-part proof `amendment-002.md`
   already requires (understood, documented, proven deliberate), and still
   without any `.cs` change — this brief's Non-Goals remain in force.
4. **`V1095_Artifacts_And_Verdict` gets a specific, additional diagnostic
   step** before it is folded into the reference-suite-membership question
   above: run its own documented entry point,
   `-executeMethod VictoriaGame.Tests.V1095BatchRunner.Run`, **without**
   `-nographics`, as an isolated, diagnostic-only invocation (this does
   **not** change Success Condition 4's own mandated command, which keeps
   `-nographics` — this is a separate, single-purpose measurement). If it
   passes without `-nographics`: the honest attribution is "environment/
   invocation mismatch, pre-existing and self-documented in VictoriaProject's
   own source (`V1095GpuMapTests.cs`'s `SANS -nographics` comment), not a
   port defect and not a stale anchor" — and this is reported regardless of
   whether `V1095` also turns out to be inside or outside the reconstructed
   reference suite from step 1, since either way the causal explanation is
   now attributable, not merely hypothesized. If it **still** fails without
   `-nographics`, that rules out the invocation-flag hypothesis and the
   failure is diagnosed the same way as any other reference-suite member
   under step 3.
5. **Sentinel, unchanged from `amendment-002.md`**: if a cause genuinely
   cannot be established after real effort, report it as unknown, by name,
   with what was tried — no cosmetic green, no silent omission.

## Required Counters — additions and one correction (brief.md's table is not edited; these are additive)

| name | sample source | denominator |
|---|---|---|
| logs_bridge_artifacts_identified_count | distinct `Logs/...`-relative file paths found by reading, in full, the 5 failing `V1042SuiteBudgetTests` methods' source (not just their assertion messages) | same count (must be >= 1 — `v1_041_tests.xml` and `v1_077_large.xml` alone already satisfy this floor) |
| logs_bridge_artifacts_restored_count | count of identified files copied read-only into `unity/game_unity/Logs/` with SHA256 matching the VictoriaProject source exactly | `logs_bridge_artifacts_identified_count` minus any case explicitly declared NOT APPLICABLE with a named reason (must equal after that exclusion) |
| cluster_b_tests_not_applicable_count | count of the 5 cluster-B cases explicitly declared NOT APPLICABLE, each with a named reason | 5 (expected value 0; any nonzero carries one written, case-specific reason, informed by but not dictated by the `HANDOFF.md` "ne mord pas" note above) |
| reference_suite_definition_sources_count | count of distinct, cited-by-path sources actually consulted and found relevant under `automation/` and `Assets/Tests/*BatchRunner` (plus `cursor_tasks/done/*_result.json`'s `tests.filter` fields and `HANDOFF.md`'s prose citations, if used) to reconstruct VictoriaProject's own last-cited-green filter | same count (must be >= 1; a reconstruction from several partial sources, each cited, is an acceptable and expected outcome, not a failure to find "the" file) |
| reference_suite_total_count / reference_suite_passed_count | `<test-run>` attributes (or the reconstructed filter's own pass/total accounting if run via a `BatchRunner` rather than native `-runTests`) of the reference suite run against the fully remediated ported tree | reference_suite_passed_count must equal reference_suite_total_count (100% green — this is the actual bar VictoriaProject cleared) |
| cluster_c_in_reference_suite_count | cross-reference of the 8 original cluster-C `fullname`s against the reconstructed reference suite's own test membership | 8 (report the measured value honestly; the working hypothesis is 0, but this is not assumed) |
| cluster_c_legacy_attributed_count | count of the 8 individually attributed as "legacy, hors suite maintenue, ancre antérieure au rebasage <name>," each with a `git log`-derived date comparison in `generator-log.md` | 8 minus `cluster_c_in_reference_suite_count` (must equal — every non-reference-suite failure gets this specific attribution, none left generic) |
| v1095_diagnostic_without_nographics_pass_count | single diagnostic invocation of `V1095BatchRunner.Run` without `-nographics`; 1 if the run's own log shows the assertion passing, 0 if it genuinely still fails (both are real, computed measurements — sentinel `-1` is reserved only if the diagnostic invocation itself could not be run at all, with a command+error proving why) | 1 (a single diagnostic run; report the true outcome, 1 or 0, not an assumed one) |
| final_test_total_count / final_test_passed_count / final_test_failed_count | `<test-run>` root attributes of the freshest full-EditMode-suite NUnit XML written after Clusters A+B remediation (the 274-case run `amendment-002.md` already required) | must be internally consistent; every failing case in this XML must appear in `cluster_c_legacy_attributed_count`'s or `cluster_c_in_reference_suite_count`'s accounting (or Cluster A/B's, if either regresses) — an unattributed failure remains a FAIL regardless of aggregate pass rate, per `amendment-002.md`'s existing bar |

`amendment-002.md`'s `logs_tracked_at_head_count` and
`logs_head_restored_count` entries are **retired, not deleted from the
record** — if already present in `manifest.json`, they stay with their
proven value (0 tracked files, hence 0 restorable) and a note pointing at
this amendment's correction; they are not silently replaced with the new
`logs_bridge_*` counters as if the first attempt never happened.

## Eval-rubric addendum (`eval-rubric.md` is not edited; this section is authoritative alongside it)

| # | What | Check type | How it is checked |
|---|---|---|---|
| 4-cluster-B-corrected | `amendment-002.md`'s Cluster B diagnosis is recorded as wrong, not silently dropped, in `generator-log.md` | Manual | Évaluateur confirms `generator-log.md` explicitly states the `git ls-tree`/`log --diff-filter=A`/`.gitignore:35` findings that disproved the original diagnosis, before describing the corrected bridge |
| 4-cluster-B-corrected | Every file the 5 cluster-B tests actually read is identified (from source, not just messages) and, by default, bridged read-only, SHA256-declared | Mechanical + Manual | mechanical: `logs_bridge_artifacts_restored_count` accounting; manual: Évaluateur reads all 5 failing methods' full source and confirms no `Logs/...`-relative read (literal or pattern-matched) was missed |
| 4-cluster-C-reference | Reference suite established by proof, sources cited by path | Mechanical + Manual | `reference_suite_definition_sources_count` >= 1; manual: Évaluateur opens each cited source and confirms it genuinely supports the reconstructed filter's definition, not an invented one |
| 4-cluster-C-reference | Reference suite is 100% green from the ported tree | Mechanical | `reference_suite_passed_count` == `reference_suite_total_count`, both > 0 |
| 4-cluster-C-membership | Each of the 8 attributed as either reference-suite member (real defect, diagnosis continues) or legacy-out-of-filter (documented, left red) | Mechanical + Manual | `cluster_c_in_reference_suite_count` + `cluster_c_legacy_attributed_count` == 8; manual: Évaluateur spot-checks at least 3 of the 8 `git log --follow` date comparisons against the actual commit history |
| 4-V1095-diagnostic | V1095's `-nographics` hypothesis tested, not assumed | Mechanical + Manual | `v1095_diagnostic_without_nographics_pass_count` is 0 or 1 (not -1 without a command+error justifying it); manual: Évaluateur reads the diagnostic invocation's own log and confirms the stated pass/fail is what it shows |
| 4-final-updated | Full-suite counters re-measured after all of this amendment's remediation; every failure attributed | Mechanical | same check as `amendment-002.md`'s "4-final" row, re-applied to the freshest XML |

The existing rubric rows from `eval-rubric.md`, `amendment-001.md`, and
`amendment-002.md` (except the superseded Cluster B mechanic above) are
unchanged and apply exactly as written.

## Why this is a separate file, not an edit to `brief.md`/`eval-rubric.md`/prior amendments

Same reasoning as amendments 001 and 002. This file corrects one section of
`amendment-002.md` explicitly (see "Correction," above) rather than editing
that file in place, for the same mtime-preservation reason, and so the
record of what was believed at each point stays legible rather than being
overwritten.

## Plateau note

This is iteration 4 for brief 003. Cluster A is closed. Cluster B is
fix-and-verify, expected closed in one further pass, same mechanic already
proven on Cluster A. Cluster C is diagnose-and-attribute by mandate: full
resolution to 100% green on the *unfiltered* 274-case suite is explicitly
**not** required by this amendment if the honest, evidenced outcome is that
some of the 8 were never part of what VictoriaProject itself maintained as
green — that is not a plateau, it is the correct terminal state for those
specific cases, and it must be reported as such rather than chased through
further iterations. Only a genuine reference-suite member among the 8 (or a
`V1095` that still fails without `-nographics`) is unresolved-and-blocking;
if diagnosis on that narrower set stalls for two iterations without
progress, escalate to the owner per `docs/rules/harness-roles.md`'s plateau
rule rather than a further speculative attempt.
