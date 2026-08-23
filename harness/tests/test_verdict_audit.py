"""
Tests for harness/verdict_audit.py.

Hard-won rule 4: prove red first. Every check below has a fixture that trips
it into FAIL before we trust the honest fixture to pass it. Invokes the
script as a real subprocess (black-box, via `py`), not by importing
internals, so this also exercises the actual CLI contract.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "verdict_audit.py"
REPO_ROOT = Path(__file__).resolve().parent.parent.parent


def run_audit(brief_dir: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(brief_dir)],
        capture_output=True, text=True,
    )


def build_honest_brief(tmp_path: Path) -> Path:
    bd = tmp_path / "brief_dir"
    (bd / "deliverables").mkdir(parents=True)

    (bd / "brief.md").write_text(
        "# Brief\n\n**Authored**: 2020-01-01T00:00:00\n**Author**: forge-planificateur\n",
        encoding="utf-8",
    )
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2020-01-01T00:00:01\n",
        encoding="utf-8",
    )
    (bd / "deliverables" / "before.txt").write_text("before-state", encoding="utf-8")
    (bd / "deliverables" / "after.txt").write_text("after-state-different", encoding="utf-8")
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n\n"
        "Built the thing, measured 12 provinces from a loaded 12-province "
        "test world via `py province_count.py test-world-12.json`.\n",
        encoding="utf-8",
    )

    manifest = {
        "files": [
            {"path": "deliverables/before.txt"},
            {"path": "deliverables/after.txt", "must_differ_from": "deliverables/before.txt"},
        ],
        "counters": [
            {"name": "province_count", "value": 12, "sample_size": 12,
             "command": "py province_count.py test-world-12.json"},
        ],
        "waivers": [],
    }
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8",
    )
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur\n\n"
        "Measured province_count = 12 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )
    return bd


def load_manifest(bd: Path) -> dict:
    return json.loads((bd / "deliverables" / "manifest.json").read_text(encoding="utf-8"))


def save_manifest(bd: Path, manifest: dict) -> None:
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8",
    )


# --- The honest control case: proves the gate isn't a blanket-rejector ---

def test_accept_honest_brief(tmp_path):
    bd = build_honest_brief(tmp_path)
    result = run_audit(bd)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "VERDICT: ACCEPT" in result.stdout


# --- Nine red-case tests, one per check ---

def test_reject_missing_declared_file(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["files"].append({"path": "deliverables/missing.txt"})
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] files_declared_exist" in result.stdout
    assert "VERDICT: REJECT" in result.stdout


def test_reject_stale_deliverable(tmp_path):
    bd = build_honest_brief(tmp_path)
    stale_ts = 1546300800  # 2019-01-01, before brief.md's 2020-01-01 Authored date
    target = bd / "deliverables" / "before.txt"
    os.utime(target, (stale_ts, stale_ts))

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] mtime_after_brief" in result.stdout


def test_reject_captures_identical_when_should_differ(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "deliverables" / "after.txt").write_text("before-state", encoding="utf-8")  # now identical

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] captures_differ_when_should" in result.stdout


def test_reject_waiver_missing_command_or_error(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["waivers"].append({"claim": "cannot measure X in this environment", "command": None, "error": None})
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] waivers_have_command_and_error" in result.stdout


def test_reject_empty_sample_zero(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["sample_size"] = 0
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_empty_sample_pass" in result.stdout


def test_reject_empty_sample_sentinel(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["sample_size"] = -1
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_empty_sample_pass" in result.stdout


def test_reject_untraceable_verdict_number(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur\n\n"
        "Measured province_count = 99 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] verdict_numbers_traceable" in result.stdout


def test_reject_bare_python_alias(tmp_path):
    bd = build_honest_brief(tmp_path)
    m = load_manifest(bd)
    m["counters"][0]["command"] = "python province_count.py test-world-12.json"
    save_manifest(bd, m)

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_bare_python_alias" in result.stdout


def test_reject_self_authored_verdict(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-generateur\n\n"
        "Measured province_count = 12 against sample_size 12. PASS.\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] verdict_is_not_self_authored" in result.stdout


def test_reject_rubric_written_after_deliverables(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2099-01-01T00:00:00\n",
        encoding="utf-8",
    )

    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] rubric_predates_deliverables" in result.stdout


# --- Internal-error path: never treated as a pass ---

def test_nonexistent_directory_is_internal_error_not_pass(tmp_path):
    result = run_audit(tmp_path / "does-not-exist")
    assert result.returncode == 2
    assert "VERDICT: ACCEPT" not in result.stdout


# --- Regression: an offset-aware ISO-8601 Authored timestamp (trailing Z or
# +hh:mm) must not crash the gate. Found live on brief 006: its Authored field
# "2026-08-05T10:05:00Z" made read_ts return a tz-aware datetime, which
# check_mtime_after_brief / check_rubric_predates then compared against
# naive-local file mtimes, raising TypeError -> exit 2 (INTERNAL ERROR) on an
# otherwise-honest brief. The gate must accept the valid timestamp, not choke.

def test_offset_aware_authored_timestamp_does_not_crash_gate(tmp_path):
    bd = build_honest_brief(tmp_path)
    # Same instant as the honest fixture's 2020-01-01T00:00:00, but stamped in
    # UTC with an explicit Z. Predates every (just-created) deliverable, so the
    # brief is still honest -- it must ACCEPT, not error out.
    (bd / "brief.md").write_text(
        "# Brief\n\n**Authored**: 2020-01-01T00:00:00Z\n**Author**: forge-planificateur\n",
        encoding="utf-8",
    )
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2020-01-01T00:00:01+00:00\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "VERDICT: ACCEPT" in result.stdout


# --- Regression coverage: a spec/verdict document naming a check by its
# forbidden word is not the same as that word being actually invoked.
# Found live on brief 001-spatial-primary-key-adr: eval-rubric.md's own row
# describing `no_bare_python_alias` ("no bare `python` invocation") tripped
# the check it was documenting, and verdict.md citing an ADR path containing
# a leading-zero number (`docs/adr/0003-...md`) tripped
# verdict_numbers_traceable. Both are backtick-quoted mentions, not real
# invocations or measurements.

def test_bare_python_documentation_mention_not_flagged(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "eval-rubric.md").write_text(
        "# Rubric\n\n**Authored**: 2020-01-01T00:00:01\n\n"
        "No bare `python` invocation anywhere in deliverables/logs.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "[PASS] no_bare_python_alias" in result.stdout


def test_bare_python_real_invocation_logged_in_prose_is_still_flagged(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Generator Log\n\n**Author**: forge-generateur\n\n"
        "Ran `python province_count.py test-world-12.json` to measure the count.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert result.returncode == 1
    assert "[FAIL] no_bare_python_alias" in result.stdout


def test_verdict_citation_of_a_path_number_not_flagged(tmp_path):
    bd = build_honest_brief(tmp_path)
    (bd / "verdict.md").write_text(
        "# Verdict\n\n**Author**: forge-evaluateur\n\n"
        "Measured province_count = 12 against sample_size 12, reproduced via "
        "`docs/adr/0003-single-spatial-primary-key.md`. PASS.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "[PASS] verdict_numbers_traceable" in result.stdout


# --- declared_files_are_tracked -----------------------------------------
#
# Added after running the gate on a fresh clone: brief 003 was ACCEPT in the
# working tree and REJECT on a clone of the same commit, because 14 of its 54
# declared files were gitignored. A verdict nobody else can reproduce is not
# a verdict, and the failure surfaced four days late, on a clone, instead of
# at the moment the manifest was written.


def _git(bd: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=bd, capture_output=True, text=True)


def _init_repo(bd: Path) -> None:
    _git(bd, "init", "-q")
    _git(bd, "config", "user.email", "t@t")
    _git(bd, "config", "user.name", "t")


def test_tracked_declared_files_pass(tmp_path):
    bd = build_honest_brief(tmp_path)
    _init_repo(bd)
    _git(bd, "add", "-A")
    _git(bd, "commit", "-qm", "x")
    result = run_audit(bd)
    assert "[PASS] declared_files_are_tracked" in result.stdout, result.stdout
    assert result.returncode == 0


def test_gitignored_declared_file_is_rejected(tmp_path):
    """THE regression: a declared proof that git ignores must fail here, in
    the tree that produced it -- not silently pass and fail on a clone."""
    bd = build_honest_brief(tmp_path)
    _init_repo(bd)
    (bd / ".gitignore").write_text("*.log\n", encoding="utf-8")
    (bd / "deliverables" / "proof.log").write_text("measured", encoding="utf-8")
    manifest = json.loads((bd / "deliverables" / "manifest.json").read_text(encoding="utf-8"))
    manifest["files"].append({"path": "deliverables/proof.log"})
    (bd / "deliverables" / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    _git(bd, "add", "-A")
    _git(bd, "commit", "-qm", "x")

    result = run_audit(bd)
    assert "[FAIL] declared_files_are_tracked" in result.stdout, result.stdout
    assert "proof.log" in result.stdout
    assert result.returncode == 1


def test_outside_a_git_repo_the_check_is_na_not_pass(tmp_path):
    """N/A says nothing was checked. A PASS would assert something was, and
    that is the shape of the original defect."""
    bd = build_honest_brief(tmp_path)  # no git init
    result = run_audit(bd)
    assert "[N/A] declared_files_are_tracked" in result.stdout, result.stdout
    assert result.returncode == 0


def test_paths_outside_the_brief_dir_are_named_not_silently_skipped(tmp_path):
    """External references cannot be tracking-checked from here, but the gap
    must stay visible in the evidence rather than vanish."""
    bd = build_honest_brief(tmp_path)
    _init_repo(bd)
    outside = tmp_path / "elsewhere.txt"
    outside.write_text("x", encoding="utf-8")
    manifest = json.loads((bd / "deliverables" / "manifest.json").read_text(encoding="utf-8"))
    manifest["files"].append({"path": "../elsewhere.txt"})
    (bd / "deliverables" / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    _git(bd, "add", "-A")
    _git(bd, "commit", "-qm", "x")

    result = run_audit(bd)
    assert "declared outside the brief dir, not checked" in result.stdout
    assert "elsewhere.txt" in result.stdout


# --- no_bare_python_alias, after the positional rewrite ------------------
#
# The gate used the same substring scan as the live hook, so a deliverable
# that merely *mentioned* python in prose failed the gate, as did any
# captured log line containing the word -- which is most logs produced by
# Python tooling. Both directions asserted: the check still has to catch a
# Générateur that actually ran the Store stub.


def test_prose_mentioning_python_does_not_fail_the_gate(tmp_path):
    """RED under the old substring scan: naming the thing you avoided is
    not the same as having run it."""
    bd = build_honest_brief(tmp_path)
    log = bd / "deliverables" / "generator-log.md"
    log.write_text(
        log.read_text(encoding="utf-8")
        + "\n\nWe rejected python in favour of py, per hard-won rule 1.\n"
          "The interpreter reported itself as Python 3.13 at C:/Python313/python.exe.\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[PASS] no_bare_python_alias" in result.stdout, result.stdout


def test_a_real_invocation_in_a_deliverable_still_fails(tmp_path):
    """The check must keep its teeth: a reported command in command position
    is exactly what it exists to catch."""
    bd = build_honest_brief(tmp_path)
    log = bd / "deliverables" / "generator-log.md"
    log.write_text(
        log.read_text(encoding="utf-8") + "\n\nMeasured with:\n\n    python count.py\n",
        encoding="utf-8",
    )
    result = run_audit(bd)
    assert "[FAIL] no_bare_python_alias" in result.stdout, result.stdout
    assert result.returncode == 1


def test_a_bare_python_command_in_a_counter_still_fails(tmp_path):
    """counters[].command is a real shell command, not prose."""
    bd = build_honest_brief(tmp_path)
    manifest_path = bd / "deliverables" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["counters"][0]["command"] = "python province_count.py test-world-12.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    result = run_audit(bd)
    assert "[FAIL] no_bare_python_alias" in result.stdout, result.stdout


def test_grep_for_the_word_in_a_counter_command_is_allowed(tmp_path):
    """The false positive that motivated the rewrite, at gate level."""
    bd = build_honest_brief(tmp_path)
    manifest_path = bd / "deliverables" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["counters"][0]["command"] = "grep -c python harness/backends/ledger.py"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    result = run_audit(bd)
    assert "[PASS] no_bare_python_alias" in result.stdout, result.stdout


def test_gate_and_hook_share_one_matcher():
    """Two copies of this rule is how the transcript counter stayed wrong.
    RED if either caller grows its own regex again."""
    hook_src = (REPO_ROOT / ".claude" / "hooks" / "no_bare_python.py").read_text(encoding="utf-8")
    gate_src = (REPO_ROOT / "harness" / "verdict_audit.py").read_text(encoding="utf-8")
    for name, src in (("hook", hook_src), ("gate", gate_src)):
        assert "bare_python" in src, f"{name} no longer uses the shared matcher"
        assert r"re.compile(r'(?<![\w./])python" not in src, \
            f"{name} grew its own bare-python regex again"


# --- must_differ_from_git ------------------------------------------------
# A brief used to prove "this shared file was appended to, not rewritten" by
# committing a `.orig` copy of it next to the manifest. Twenty-four such
# copies, 7 209 lines, all duplicating content git already stored. The pair
# below references the pre-state through git instead. Same guarantee, no
# duplicate — and rule 4 applies: red first.


def _repo_with_pre_state(bd: Path, before: str, after: str) -> None:
    """Commit `before` as the file's pre-state, then write `after` over it."""
    _init_repo(bd)
    (bd / "deliverables" / "shared.py").write_text(before, encoding="utf-8")
    _git(bd, "add", "-A")
    _git(bd, "commit", "-qm", "pre-state")
    (bd / "deliverables" / "shared.py").write_text(after, encoding="utf-8")
    _git(bd, "add", "-A")
    _git(bd, "commit", "-qm", "post-state")


def test_git_ref_identical_to_published_file_is_rejected(tmp_path):
    """RED: the file was declared as changed and git says it never changed."""
    bd = build_honest_brief(tmp_path)
    _repo_with_pre_state(bd, "SAME = 1\n", "SAME = 1\n")
    manifest = load_manifest(bd)
    manifest["files"].append({
        "path": "deliverables/shared.py",
        "must_differ_from_git": "HEAD~1:deliverables/shared.py",
    })
    save_manifest(bd, manifest)
    result = run_audit(bd)
    assert "[FAIL] captures_differ_when_should" in result.stdout, result.stdout
    assert "shared.py" in result.stdout
    assert result.returncode == 1


def test_git_ref_differing_from_published_file_passes(tmp_path):
    """GREEN, and only once the red above is proven."""
    bd = build_honest_brief(tmp_path)
    _repo_with_pre_state(bd, "BEFORE = 1\n", "BEFORE = 1\nAFTER = 2\n")
    manifest = load_manifest(bd)
    manifest["files"].append({
        "path": "deliverables/shared.py",
        "must_differ_from_git": "HEAD~1:deliverables/shared.py",
    })
    save_manifest(bd, manifest)
    result = run_audit(bd)
    assert "[PASS] captures_differ_when_should" in result.stdout, result.stdout
    assert result.returncode == 0


def test_unresolvable_git_ref_is_rejected_not_skipped(tmp_path):
    """A reference nobody can read checked nothing. Silence here would be the
    same defect as the gitignored-proof one: a claim that looks verified."""
    bd = build_honest_brief(tmp_path)
    _repo_with_pre_state(bd, "BEFORE = 1\n", "BEFORE = 1\nAFTER = 2\n")
    manifest = load_manifest(bd)
    manifest["files"].append({
        "path": "deliverables/shared.py",
        "must_differ_from_git": "no-such-rev:deliverables/shared.py",
    })
    save_manifest(bd, manifest)
    result = run_audit(bd)
    assert "[FAIL] captures_differ_when_should" in result.stdout, result.stdout
    assert "unresolvable" in result.stdout
    assert result.returncode == 1


def test_git_ref_outside_a_repo_is_rejected_not_passed(tmp_path):
    """Same reasoning, the other way a reference becomes unreadable."""
    bd = build_honest_brief(tmp_path)  # no git init
    (bd / "deliverables" / "shared.py").write_text("AFTER = 2\n", encoding="utf-8")
    manifest = load_manifest(bd)
    manifest["files"].append({
        "path": "deliverables/shared.py",
        "must_differ_from_git": "HEAD~1:deliverables/shared.py",
    })
    save_manifest(bd, manifest)
    result = run_audit(bd)
    assert "[FAIL] captures_differ_when_should" in result.stdout, result.stdout
    assert result.returncode == 1


def test_pair_the_gate_cannot_find_is_not_a_silent_pass(tmp_path):
    """RED: the defect brief 026 shipped. Its manifest declared three
    `must_differ_from` pairs by repo-root path; the gate resolves paths from
    the brief dir, found neither side, and reported PASS -- "all declared
    pairs differ" -- while comparing nothing. Rule 7: presence is not
    function. A pair that was not compared must say so."""
    bd = build_honest_brief(tmp_path)
    manifest = load_manifest(bd)
    manifest["files"].append({
        "path": "deliverables/absent.txt",
        "must_differ_from": "deliverables/also-absent.txt",
    })
    save_manifest(bd, manifest)
    result = run_audit(bd)
    assert "[FAIL] captures_differ_when_should" in result.stdout, result.stdout
    assert "not compared" in result.stdout
    assert result.returncode == 1
