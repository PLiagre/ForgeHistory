"""
Tests for the multi-backend self-authorship fixes to
harness/verdict_audit.py's `verdict_is_not_self_authored` check (brief 010,
Lot 010a).

Two independent blind spots, closed together:

1. Actor vs. role (SC3/SC4). The check used to compare the raw role
   strings (`gen != ver`): `forge-generateur-codex` != `forge-evaluateur-codex`
   as strings, even though the same actor -- Codex -- wrote both. Fixed by
   deriving the actor from each side's backend suffix, generically (no
   hardcoded backend list -- SC4 proves this with an actor name that
   appears nowhere else in the repository).

2. First pair only (SC3b). `read_field` uses `re.search`, which only ever
   returns the earliest `**Author**:` line. On a multi-lot brief (each lot
   appends its own signed section to generator-log.md and to verdict.md),
   every author pair past the first was invisible -- self-authored or not,
   simply never examined. Fixed by `read_all_fields` (re.finditer) plus a
   pairing rule over every collected author, not just the first.

These tests were written and run red (against the code as it stood before
this fix) before verdict_audit.py was touched. The captured red output is
reproduced in deliverables/generator-log.md, together with the green output
after the fix -- this file alone does not carry the red proof, per the
brief's own warning that a test written only after the fix proves nothing
about a defect that is an *absence* of refusal.
"""
import json
import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "verdict_audit.py"


def run_audit(brief_dir: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(brief_dir)],
        capture_output=True, text=True,
    )


def _write_minimal_brief(bd: Path) -> None:
    (bd / "deliverables").mkdir(parents=True, exist_ok=True)
    (bd / "brief.md").write_text(
        "# Brief\n\n**Authored**: 2020-01-01T00:00:00\n**Author**: forge-planificateur\n",
        encoding="utf-8",
    )
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2020-01-01T00:00:01\n", encoding="utf-8",
    )
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps({"files": [], "counters": [], "waivers": []}), encoding="utf-8",
    )


# --- SC3: actor, not role string -----------------------------------------

def test_same_actor_different_role_string_is_refused(tmp_path):
    """forge-generateur-codex / forge-evaluateur-codex: different ROLE
    strings, same ACTOR (Codex). Must be refused. Red before the fix (see
    generator-log.md for the captured `[PASS]` it used to produce)."""
    bd = tmp_path / "sc3"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur-codex\n\nCodex produced this alone.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur-codex\n\nCodex also wrote this verdict.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "forge-generateur-codex==forge-evaluateur-codex" in result.stdout
    assert result.returncode == 1


# --- SC3b: every pair, not just the first ---------------------------------

def test_self_judged_pair_in_second_lot_is_no_longer_invisible(tmp_path):
    """Two lots: Lot A honestly cross-judged (Claude/Claude), Lot B
    self-judged by Codex end to end. Under the old re.search-based read,
    only Lot A's pair (the first `**Author**:` line in each file) was ever
    examined -- Lot B's self-judgment passed unnoticed. Must now be
    refused."""
    bd = tmp_path / "sc3b"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n**Date**: 2020-01-02\n\n"
        "## Lot 777a\n\nProduced by Claude.\n\n---\n\n## Lot 777b\n\n"
        "**Author**: forge-generateur-codex\n**Date**: 2020-01-03\n\n"
        "Produced by Codex alone.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict -- lot 777a\n\n**Authored**: 2020-01-04T00:00:00\n"
        "**Author**: forge-evaluateur\n\nIndependent judgment of Lot 777a.\n\n"
        "---\n\n# Verdict -- lot 777b\n\n**Authored**: 2020-01-05T00:00:00\n"
        "**Author**: forge-evaluateur-codex\n\n"
        "Codex also judged the Lot 777b it produced itself.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "1/2 examined pair(s)" in result.stdout
    assert "forge-generateur-codex==forge-evaluateur-codex" in result.stdout
    assert result.returncode == 1


def test_two_honest_lots_both_examined_and_both_pass(tmp_path):
    """Control: two lots, each cross-judged by a different actor than its
    producer. Both pairs must be examined and both must pass -- a check
    that refuses everything once it looks past the first pair is as broken
    as one that never looked."""
    bd = tmp_path / "sc3b_honest"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n**Date**: 2020-01-02\n\n"
        "## Lot 777a\n\nProduced by Claude.\n\n---\n\n## Lot 777b\n\n"
        "**Author**: forge-generateur-codex\n**Date**: 2020-01-03\n\n"
        "Produced by Codex.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict -- lot 777a\n\n**Authored**: 2020-01-04T00:00:00\n"
        "**Author**: forge-evaluateur-codex\n\nJudged by Codex (different actor).\n\n"
        "---\n\n# Verdict -- lot 777b\n\n**Authored**: 2020-01-05T00:00:00\n"
        "**Author**: forge-evaluateur\n\nJudged by Claude (different actor).\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[PASS] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "2 examined pair(s)" in result.stdout
    assert result.returncode == 0


# --- SC4: generalizes to an unseen actor, no code change ------------------

def test_unseen_actor_name_is_refused_without_naming_it_in_the_control(tmp_path):
    """The refusal must fire for an actor the control has never seen,
    proving it derives actors generically rather than matching a hardcoded
    list of known backend names.

    D3 (brief 010 feedback, iteration 1): the fixture used to be 'gemini',
    checked absent only from verdict_audit.py -- but SC4 asks for a name
    absent from the whole repository, and 'gemini' already appears in
    docs/adr/0002-pluggable-generator-backend.md. The Évaluateur established
    generality independently with a different name ('korrigan') and did not
    count this against the lot, but flagged it as a one-line fix. Fixed by
    (a) using an actor name invented for this test alone
    ('ptarmigana' -- chosen for having no plausible collision with a real
    backend or tool name) and (b) checking its absence across every tracked
    file in the repository via `git grep`, not only verdict_audit.py.
    Excluded from that repo-wide scan: this test file itself (which must
    name the fixture to use it) and this brief's own generator-log.md
    (which documents the fixture in prose, as this docstring does)."""
    actor = "ptarmigana"
    repo_root = SCRIPT.resolve().parent.parent
    exempt_suffixes = (
        "harness/tests/test_verdict_audit_actor_identity.py",
        "harness/queue/briefs/010-repartition-roles-full-auto/deliverables/generator-log.md",
    )
    grep = subprocess.run(
        ["git", "grep", "-il", actor],
        cwd=repo_root, capture_output=True, text=True,
    )
    # returncode 1 == no match anywhere (git grep's own convention); 0 means
    # it found the string somewhere and every hit must be one of the two
    # files above documenting the fixture, never the control under test.
    assert grep.returncode in (0, 1), grep.stderr
    hits = [h.strip() for h in grep.stdout.splitlines() if h.strip()]
    unexpected = [h for h in hits if not h.replace("\\", "/").endswith(exempt_suffixes)]
    assert not unexpected, (
        f"fixture actor '{actor}' must not already appear elsewhere in the "
        f"repo (found in: {unexpected})"
    )

    bd = tmp_path / "sc4"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        f"# Generator Log\n\n**Author**: forge-generateur-{actor}\n\n{actor} produced this alone.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        f"# Verdict\n\n**Author**: forge-evaluateur-{actor}\n\n{actor} also wrote this verdict.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert f"forge-generateur-{actor}==forge-evaluateur-{actor}" in result.stdout
    assert result.returncode == 1


# --- SC6: legitimate cross-actor judgment keeps passing --------------------

def test_cross_actor_judgment_still_passes(tmp_path):
    """Control mirroring the real brief 009 shape: a Claude-authored lot
    later re-judged by a different actor. Different actors, so a legitimate
    independent judge -- must not be refused just because the check now
    looks past role strings."""
    bd = tmp_path / "sc6"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n\nProduced by Claude.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur-codex\n\n"
        "Judged by Codex against a journal signed forge-generateur.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[PASS] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert result.returncode == 0


# --- D1 (iteration 2): whole-list identity, regardless of position -------
#
# Found by the Évaluateur on iteration 1 (feedback-010a.md, D1): the k-window
# pairing rule above, introduced to fix SC3/SC3b, made the check STRICTLY
# MORE PERMISSIVE than the single-pair code it replaced on this class of
# case -- disqualifying under this brief's own non-goal 7 ("toute
# modification qui ferait passer un cas aujourd'hui refusé est
# disqualifiante"). Appending an unjudged, not-yet-reviewed lot to
# generator-log.md was enough to push a plain self-signed verdict (the
# producer signs verdict.md with their own generator-log author string) out
# of the k-window and silence the refusal.
#
# Both cases below were run against the pre-iteration-2 code (a copy of
# harness/verdict_audit.py as it stood at commit 62a0fe2, captured outside
# the repo) and produced [PASS] / VERDICT: ACCEPT -- the red proof is
# reproduced verbatim in deliverables/generator-log.md. They are written
# here, after the fix, only because the red proof already exists elsewhere
# and duplicating a red run inside a green test suite is not meaningful --
# the point of this file is to make the fixed behaviour permanent, so the
# next rewrite of the pairing rule cannot reopen the same door silently.

def test_self_signed_verdict_masked_by_unjudged_later_lot_is_refused(tmp_path):
    """D1, case 1. Journal: Lot 1 by forge-generateur, Lot 2 (not yet
    judged) by forge-generateur-korrigan. Verdict: signed forge-generateur --
    the producer of Lot 1 signing their own verdict with their own
    generator-log author string. The k-window (k=1) used to pair only
    ('forge-generateur-korrigan', 'forge-generateur') and see two different
    actors ('korrigan' vs. none) -- ACCEPT. The raw-string-identity check
    (independent of position) must catch it."""
    bd = tmp_path / "d1_case1"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Journal\n\n**Author**: forge-generateur\n\nLot 1 par Claude.\n\n"
        "## Lot 2\n\n**Author**: forge-generateur-korrigan\n\n"
        "Lot 2 par Korrigan, pas encore juge.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict lot 1\n\n**Author**: forge-generateur\n\n"
        "Le producteur signe son propre verdict.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "identical author string appears in both generator-log.md and verdict.md" in result.stdout
    assert "forge-generateur" in result.stdout
    assert result.returncode == 1


def test_self_signed_verdict_masked_by_unjudged_later_lot_is_refused_symmetric(tmp_path):
    """D1, case 2 -- the mirror shape: the suffixed actor is first in the
    journal instead of second. Journal: Lot 1 by forge-generateur-korrigan,
    Lot 2 (not yet judged) by forge-generateur. Verdict: signed
    forge-generateur-korrigan -- Korrigan signing their own verdict. Same
    bug, opposite ordering -- must be caught the same way."""
    bd = tmp_path / "d1_case2"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Journal\n\n**Author**: forge-generateur-korrigan\n\nLot 1 par Korrigan.\n\n"
        "## Lot 2\n\n**Author**: forge-generateur\n\n"
        "Lot 2 par Claude, pas encore juge.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict lot 1\n\n**Author**: forge-generateur-korrigan\n\n"
        "Le producteur (Korrigan) signe son propre verdict.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "identical author string appears in both generator-log.md and verdict.md" in result.stdout
    assert "forge-generateur-korrigan" in result.stdout
    assert result.returncode == 1


# --- D2 (iteration 2): dropped-entry same-actor pair, differing role strings

def test_self_judged_pair_dropped_by_k_window_is_refused(tmp_path):
    """D2. Journal: Lot 1 by forge-generateur-korrigan, Lot 2 by
    forge-generateur. Verdict: a single entry, forge-evaluateur-korrigan --
    Korrigan judging the very Lot 1 they produced. Because the journal has
    2 authors and the verdict has 1, k=1 pairs only
    ('forge-generateur', 'forge-evaluateur-korrigan') -- different actors,
    PASS -- while the actual self-judged pair
    (forge-generateur-korrigan / forge-evaluateur-korrigan) is dropped from
    the journal side and never looked at. Unlike D1, the two role strings
    differ ('forge-generateur-korrigan' vs. 'forge-evaluateur-korrigan'), so
    the raw-string-identity check does not fire here -- only the
    dropped-entry cross-check (confronting the dropped author against every
    verdict author, by actor) can catch it."""
    bd = tmp_path / "d2"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Journal\n\n**Author**: forge-generateur-korrigan\n\nLot 1 par Korrigan.\n\n"
        "## Lot 2\n\n**Author**: forge-generateur\n\nLot 2 par Claude.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict lot 1\n\n**Author**: forge-evaluateur-korrigan\n\n"
        "Korrigan juge son propre lot 1.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "forge-generateur-korrigan==forge-evaluateur-korrigan" in result.stdout
    assert result.returncode == 1


def test_read_all_fields_returns_every_occurrence_in_order(tmp_path):
    """Direct unit check on the new helper: re.finditer, not re.search --
    every `**Author**:` line, in document order, not only the first."""
    sys.path.insert(0, str(SCRIPT.parent))
    import verdict_audit  # noqa: E402

    p = tmp_path / "doc.md"
    p.write_text(
        "**Author**: forge-generateur\n\ntext\n\n**Author**: forge-generateur-codex\n",
        encoding="utf-8",
    )
    assert verdict_audit.read_all_fields(p, "Author") == [
        "forge-generateur", "forge-generateur-codex",
    ]
