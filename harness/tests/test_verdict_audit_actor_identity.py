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
    """'gemini' appears nowhere in verdict_audit.py, harness/backends/, or
    any ADR naming a known backend -- the refusal must still fire, proving
    the check derives actors generically rather than matching a hardcoded
    list of known backend names."""
    src = SCRIPT.read_text(encoding="utf-8")
    assert "gemini" not in src.lower(), \
        "fixture actor must not already be named in the control under test"

    bd = tmp_path / "sc4"
    _write_minimal_brief(bd)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur-gemini\n\nGemini produced this alone.\n",
        encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur-gemini\n\nGemini also wrote this verdict.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout, result.stdout
    assert "forge-generateur-gemini==forge-evaluateur-gemini" in result.stdout
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
