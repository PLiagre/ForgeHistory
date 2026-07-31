# Amendment 002 — Brief 003 (port VictoriaProject's Unity game)

**Authored**: 2026-07-31T22:00:00
**Author**: forge-planificateur
**Context**: iteration 2 (`deliverables/generator-log.md`'s "Iteration 2 —
amendment-001 remediation" section, plus `deliverables/evidence/
failed-test-cases.txt`) reports Success Conditions 3 and 5 now genuinely
green, and Success Condition 4 at 256/274 (17 failed, 1 skipped), resolved
into three causally distinct, individually evidenced clusters. This
amendment authorizes narrow, additional remediation for two of the three
clusters and a diagnostic-only mandate for the third. It does not edit
`brief.md` or `eval-rubric.md`, for the same reason amendment-001 did not:
already-correct deliverables (the copy, the ADR, the README/launcher, the
now-green compile and capture proofs) must not be put at risk of a
stale-relative-to-brief finding on re-audit. Read this together with
`brief.md`, `eval-rubric.md`, and `amendment-001.md` — it supersedes neither.

## What is NOT reopened

Success Conditions 1, 2, 6, 7, 8, 9, and amendment-001's own remediation
(77/77 HEAD-restored, 72/72 untracked strays removed, VictoriaProject proven
untouched at two independent checkpoints) all stand. Success Condition 3
(compile, exit 0, 0 `error CS####`) and Success Condition 5 (capture pair
genuinely regenerated, fresh mtimes, log shows the real code path executing)
are confirmed green in iteration 2 and are not re-litigated here. None of
that needs to be redone.

## Cluster B (5 failures) — `Logs/` exclusion caught tracked proof archives it never meant to catch

**Diagnosis, confirmed correct.** Success Condition 1's exclusion list names
`Logs/` alongside seven other directories with one stated purpose:
regenerable Unity/build caches (`brief.md`'s Success Condition 1: "these
eight regenerable directories"). `V1042SuiteBudgetTests`'s five failing
cases do not read a regenerable cache — they read specific, named,
historical proof-archive files (`v1_041_tests.xml`, `v1_077_large.xml`) that
VictoriaProject committed to git as part of its own evidence trail (per the
project's "regarder les captures/preuves soi-même" discipline). Excluding
`Logs/` wholesale caught both classes at once; only the cache class was ever
intended.

**Authorized remediation.** For every file **tracked at HEAD** under
`game_unity/Logs/` in VictoriaProject (read-only):
```
git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD --name-only -- game_unity/Logs
```
extract each one into the ported tree the same way amendment-001's step 2
already established:
```
git -C C:\Users\liagr\VictoriaProject show HEAD:game_unity/Logs/<path> > D:\ForgeHistory\unity\game_unity\Logs\<path>
```
This is **additive** to `unity/game_unity/Logs/`, which Success Condition 3
already populated with this brief's own evidence
(`v003_compile.log`, `v003_tests.log`, `v003_test-results.xml`,
`v003_capture.log`, and iteration 2's `v003_compile_remediated.log`
equivalents). VictoriaProject's own historical archive filenames follow its
`v1_NNN_*.xml`/`.log` convention, disjoint from this brief's `v003_`-prefixed
names — confirm no collision before writing (a genuine collision would be a
new, separate finding, not silently resolved by overwrite). Files under
`game_unity/Logs/` that exist on VictoriaProject's disk but are **not**
tracked at HEAD remain excluded, unchanged from Success Condition 1's
original intent — this remediation restores the committed proof trail, not
every ephemeral log VictoriaProject's own working tree happens to hold.

## Cluster A (4 failures) — a narrow, read-only bridge to `sandbox/geo/` artifacts

**Diagnosis, confirmed correct.** `V1037CityPlacementTests` and
`V1080CoordinatesTests` (2 cases each) resolve their file path via
`Path.Combine(GameUnityRoot, "..", "sandbox", "geo", "artifacts", ...)` —
i.e. relative to the ported project's own parent directory, exactly
mirroring VictoriaProject's own layout where `sandbox/` is a sibling of
`game_unity/`. At the new location this resolves to
`D:\ForgeHistory\unity\sandbox\geo\artifacts\...` (confirmed by the
`DirectoryNotFoundException`'s literal path in
`deliverables/evidence/failed-test-cases.txt`). `sandbox/geo/` was never in
Success Condition 1's copy scope (only `game_unity/` was named), and it is
**not** the same tree as `pipeline/geo/` (brief 002 ported only G2 —
coastline — into `pipeline/geo/`; VictoriaProject's `sandbox/geo/artifacts/`
holds many more artifacts than G2 produces, including this
`coordinate_correction_proposal_v1_072.json` file from a later step in
VictoriaProject's own pipeline that has no ForgeHistory equivalent yet).

**Authorized remediation — a temporary, narrow, read-only bridge, not a
second geo-pipeline port.**

1. For each of the 4 failing cases, **derive the full set of files it
   actually reads from the test's own source code**, not only from the
   first exception's path — a test can read more than one file across
   sequential assertions even though only the first missing one throws.
   Read `V1037CityPlacementTests.cs` and `V1080CoordinatesTests.cs` in full
   (the specific failing methods: `V1081_Artifacts_And_Verdict`,
   `V1081_C_V1080_Acquis_Hold`, `V1080_Artifacts_And_Verdict`,
   `V1080_B_PositionsMatchArbitratedProposal`) and list every distinct
   `sandbox/...`-relative path referenced, one by one, in
   `generator-log.md`. (Already confirmed present in both files:
   `coordinate_correction_proposal_v1_072.json`,
   `C:\Users\liagr\VictoriaProject\sandbox\geo\artifacts\coordinate_correction_proposal_v1_072.json`
   — but do not assume this is the only one without reading both methods in
   full; a second referenced file discovered only after the first is
   restored is not a new finding requiring another amendment, it is this
   same authorization applied to whatever the source code actually reads.)
2. **Copy exactly those files, read-only from VictoriaProject, into
   `unity/sandbox/geo/artifacts/`** (or wherever else the test source
   resolves them relative to the ported project root — derive the
   destination from the same `Path.Combine` expression the failing test
   itself uses, do not guess a different layout). Declare each one
   individually in `manifest.json` with its SHA256, matching the
   VictoriaProject source file's SHA256 exactly (a byte-identical, read-only
   copy — the same discipline brief 002 already used for
   `legacy_game_data/`).
3. **Consign this as a temporary bridge, explicitly, in `generator-log.md`
   and in `unity/README.md`'s existing `## game_unity/` section**: these
   files exist at `unity/sandbox/geo/` solely so the ported test suite can
   read what it already expects; ForgeHistory's own geo-pipeline port
   (`harness/queue/geo-pipeline-port-plan.md`'s brief slots for G3 cells
   onward) is what will eventually regenerate the equivalent artifact inside
   `pipeline/geo/` on its own terms, and a non-divergence test between the
   two will make sense to write **at that time**, not now — this brief does
   not create that test, only the bridge that lets the already-ported suite
   run today.
4. **Alternative, per test, if inspection shows the comparison itself is
   legacy-only and has no ForgeHistory referent** (e.g. if a test's
   assertion is intrinsically about VictoriaProject's own historical
   arbitration process rather than about any state ForgeHistory will ever
   reproduce): declare that specific test case **NOT APPLICABLE**,
   explicitly, by name, with the causal reason written in
   `generator-log.md` — never by silently supplying a placeholder file or
   by leaving it unexplained. **The default is to supply the real artifact
   (steps 1-3); NOT APPLICABLE is the exception and must be justified per
   case, not applied as a shortcut to avoid steps 1-3.**

## Cluster C (8 failures) — diagnose before touching anything

**Mandate: diagnosis first, and only after A and B are resolved.** After
Cluster A's bridge and Cluster B's `Logs/` restoration are both in place,
**re-run the full Success Condition 4 suite fresh** (not a filtered subset —
resolving A/B may change which cases even reach the parity/canary
assertions). If any of the original 8 cluster-C failures persist:

1. **Root-cause each one by measured bisection**, not by inspection alone:
   identify, with a command and its output, which specific file or data
   difference between the ported tree (post-A/B-remediation, post
   amendment-001 HEAD-restoration) and VictoriaProject's own HEAD state
   (the state that produced VictoriaProject's own last-measured green run)
   actually drives the mismatch. `generator-log.md`'s own stated hypothesis
   — that HEAD-restoration (amendment-001) re-exposed an anchor/HEAD
   mismatch the uncommitted `v1_096`/`v1_096b` WIP had incidentally papered
   over — is a **starting hypothesis to test, not a conclusion to assert**.
   Test it by, at minimum: comparing the specific data/anchor values each
   failing assertion reads (not the whole tree) between the ported copy and
   a fresh, independent extraction of the same paths from VictoriaProject's
   own HEAD via `git show`, isolating whether the divergence is in ported
   data, in the restored `.cs` logic, or in something the port introduced
   that neither of those explains.
2. **No test is weakened and no parity/determinism anchor is rebased to
   reach green**, regardless of what the bisection finds, unless **all
   three** hold: (a) the causal difference is fully understood and named,
   (b) it is documented in `generator-log.md` with the measured evidence
   from step 1, and (c) the difference is proven **deliberate** — i.e. an
   intentional change this brief (or a prior, cited one) actually made, not
   an accident being rationalized after the fact. This is the same bar
   VictoriaProject itself applied the one time it legitimately rebased an
   anchor (`HANDOFF.md`'s `v1_090` section — cited by name/section only,
   never by inline hex value, per hard-won rule 12: a fingerprint is cited
   by name, it will get rebased someday). **Even where all three hold, this
   brief's Non-Goals still forbid any `.cs` change beyond the one narrow,
   inapplicable path-adjustment exception — rebasing an anchor is a `.cs`
   change.** If diagnosis concludes a rebase is the correct fix, that
   conclusion is reported as a finding for a future brief, not applied now.
3. **Sentinel: if, after genuine bisection effort, the root cause for a
   given failure remains unknown, it is reported as unknown — explicitly,
   by name, with what was tried and what it ruled out.** No cosmetic green.
   A failure whose cause is "not yet understood" is a worse thing to hide
   than to report; hiding it here is exactly the failure mode
   `docs/rules/simulation-principles.md` names as failure mode #7 wearing a
   different hat (the producer deciding, on its own, that a red result is
   acceptable to present as resolved).

## Required Counters — additions (brief.md's table is not edited; these are additive)

| name | sample source | denominator |
|---|---|---|
| logs_tracked_at_head_count | `git -C C:\Users\liagr\VictoriaProject ls-tree -r HEAD --name-only -- game_unity/Logs` (line count) | same count (must be > 0) |
| logs_head_restored_count | SHA256 comparison of each file now under `unity/game_unity/Logs/` that corresponds to a tracked-at-HEAD path, against a fresh, independent `git show HEAD:game_unity/Logs/<path>` read | `logs_tracked_at_head_count` (must be equal — every tracked archive fully restored) |
| sandbox_bridge_artifacts_identified_count | distinct `sandbox/...`-relative file paths found by reading, in full, the source of the 4 failing test methods (not just their exception messages) | same count (must be >= 1 — `coordinate_correction_proposal_v1_072.json` alone already satisfies this floor; report honestly if more are found) |
| sandbox_bridge_artifacts_restored_count | count of identified files copied read-only into the ported tree with SHA256 matching the VictoriaProject source exactly | `sandbox_bridge_artifacts_identified_count` minus any test explicitly declared NOT APPLICABLE per its own named, causal exception (must equal after that exclusion — no unexplained gap) |
| cluster_a_tests_not_applicable_count | count of the 4 cluster-A test cases explicitly declared NOT APPLICABLE, each with a named reason in `generator-log.md` | 4 (total cluster-A cases; expected value 0 — the default is to supply the artifact; any nonzero value must carry one written reason per case) |
| cluster_c_failures_after_ab_remediation_count | fresh NUnit XML's failing cases, filtered to the 8 original cluster-C `fullname`s (plus any new failure that appears only after A/B remediation, listed separately, not folded into this count silently) | 8 (the original cluster-C size; report the fresh number even if it differs from 8, and explain any difference) |
| cluster_c_root_cause_identified_count | count of `cluster_c_failures_after_ab_remediation_count`'s failures for which `generator-log.md` records a measured bisection finding (command + evidence naming the specific diverging file/data) | `cluster_c_failures_after_ab_remediation_count` minus any explicitly declared cause-unknown per the sentinel rule (must equal after that exclusion — no failure left silently unaddressed, whether by cause or by an honest "unknown, here is what was tried") |
| final_test_total_count / final_test_passed_count / final_test_failed_count | `<test-run>` root attributes of the freshest NUnit XML written after all of this amendment's remediation | must be internally consistent (`passed + failed + skipped + inconclusive == total`); `final_test_failed_count` need not be 0, but every failing case must appear in `cluster_c_root_cause_identified_count`'s accounting (root-caused or explicitly cause-unknown) — an attributed failure is not the same as a passing test, but an *unattributed* one is a FAIL of this amendment's own bar |

These join, not replace, the counters already in `brief.md` and
`amendment-001.md`. `test_total_count`/`test_passed_count`/
`test_failed_count` must be **re-measured from the final fresh run** after
this amendment's remediation, not left at iteration 2's 274/256/17.

## Eval-rubric addendum (`eval-rubric.md` is not edited; this section is authoritative alongside it)

| # | What | Check type | How it is checked |
|---|---|---|---|
| 4-cluster-B | Every `Logs/`-tracked-at-HEAD file cluster B's 5 failures depend on is restored, byte-identical to HEAD | Mechanical | `logs_head_restored_count` == `logs_tracked_at_head_count`; Évaluateur independently re-runs `git ls-tree -r HEAD --name-only -- game_unity/Logs` against VictoriaProject and re-hashes each restored file against a fresh `git show`, not trusting the Générateur's own SHA256 claim alone |
| 4-cluster-B | Restoration did not overwrite or collide with this brief's own `v003_*` evidence | Manual | Évaluateur confirms `v003_compile.log`, `v003_tests.log`, `v003_test-results.xml`, `v003_capture.log` (and iteration 2's remediated-compile equivalent) are unchanged in content/mtime by this amendment's Logs restoration step |
| 4-cluster-A | Every file the 4 cluster-A tests actually read (derived from test source, not just the exception message) is identified and, by default, supplied read-only at the exact path the ported test resolves | Mechanical + Manual | mechanical: `sandbox_bridge_artifacts_restored_count` accounting per the Required Counters row; manual: Évaluateur reads both failing test fixtures' full source and confirms no `sandbox/...`-relative read was missed |
| 4-cluster-A | Any test declared NOT APPLICABLE carries a specific, causal, named reason — never silence, never a placeholder file | Manual | Évaluateur reads `generator-log.md`'s stated reason for each; a reason that could be pasted onto an unrelated test unchanged (generic filler) FAILS this row |
| 4-cluster-A | The bridge is consigned as temporary, pointing at the real future fix (`pipeline/geo/` port continuation), not presented as a permanent second geo-artifact source | Manual | Évaluateur reads `unity/README.md`'s updated section and `generator-log.md`; FAILS if either implies `unity/sandbox/geo/` is a durable, intended part of the architecture rather than a stated bridge |
| 4-cluster-C | Diagnosis attempted only after A+B are in place, on a fresh full re-run | Manual | Évaluateur confirms, from `generator-log.md`'s own sequencing and timestamps, that cluster-C bisection followed cluster A/B remediation and a fresh full-suite run, not a stale or filtered one |
| 4-cluster-C | No test weakened, no anchor rebased, without the three-part proof (understood, documented, proven deliberate) — and even then, no `.cs` change is made in this brief | Mechanical + Manual | mechanical: `git status --porcelain` on any `.cs`/anchor-bearing file remains empty (same check as the standing Non-Goals row); manual: Évaluateur reads the bisection narrative for each cluster-C failure and confirms it is evidence, not assertion |
| 4-cluster-C | Every cluster-C failure is either root-caused or explicitly declared cause-unknown with what was tried | Mechanical + Manual | `cluster_c_root_cause_identified_count` == `cluster_c_failures_after_ab_remediation_count` (post cause-unknown exclusion, which must itself be named); manual spot-check of at least 2 of the 8 narratives against the underlying evidence files cited |
| 4-final | Final suite is 100% green, or every remaining failure is individually attributed with proof, and `test_total`/`passed`/`failed` are re-measured from the final fresh XML | Mechanical + Manual | `final_test_total_count`/`final_test_passed_count`/`final_test_failed_count` internally consistent and read from the newest XML's mtime (must postdate all remediation steps); every failing `fullname` in that XML appears in `generator-log.md`'s per-cluster accounting — an unlisted failure is an automatic FAIL of this row regardless of the aggregate pass rate |

The existing rubric rows for Success Conditions 1, 2, 3, 5, 6, 7, 8, 9 (and
amendment-001's own addendum rows) are unchanged and apply exactly as
written; only Success Condition 4's rows are extended by the table above.

## Why this is a separate file, not an edit to `brief.md`/`eval-rubric.md`

Same reasoning as `amendment-001.md`: this file is additive, narrowly
scoped to what iteration 2 actually discovered, and does not restate or
paraphrase anything `brief.md`/`eval-rubric.md`/`amendment-001.md` already
say — where it is silent, those documents still govern in full.

## Plateau note

This is iteration 3 for brief 003. Clusters A and B are fix-and-verify —
expect them resolved in one further pass. Cluster C is diagnose-first by
mandate; if bisection genuinely cannot identify a cause after real effort,
report it as unknown (per the sentinel rule above) rather than attempting a
third or fourth speculative fix — that would be exactly the "replay the same
prompt without transmitting the prior failure" pattern
`docs/rules/harness-roles.md`'s plateau rule exists to stop. If cluster C's
root cause turns out to require a `.cs`/anchor change to actually resolve
(as opposed to merely being understood), that resolution is out of this
brief's authority entirely and must be raised as a distinct, future
Planificateur pass — not folded into a fourth iteration of this one.
