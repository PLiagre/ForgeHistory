"""
Tests for harness/budget.py.

Hard-won rule 4: prove red first. The properties under test are the ones
that make the budget trustworthy rather than merely present:

  - an unmeasurable budget reports UNMEASURABLE, never OK (a budget that
    reads "fine" when it can see nothing is worse than no budget);
  - a progress marker without evidence is refused, because it would
    silently reset the no-progress clock on a claim rather than a fact;
  - BUDGET_EXHAUSTED and NEEDS_SPLIT carry their own exit codes, distinct
    from the gate's REJECT, so an oversized-but-correct brief is never
    recorded as defective work.
"""
import json
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPT = REPO_ROOT / "harness" / "budget.py"

sys.path.insert(0, str(REPO_ROOT / "harness"))
import budget  # noqa: E402

SLUG = "042-a-test-brief"


def make_transcript(root: Path, slug: str, tool_calls: int,
                    calls_per_request: int = 1) -> Path:
    """A transcript naming `slug` and carrying `tool_calls` tool_use blocks."""
    session = root / "sess-1" / "subagents"
    session.mkdir(parents=True, exist_ok=True)
    path = session / "agent-aaa.jsonl"

    lines = [json.dumps({
        "type": "user",
        "message": {"role": "user",
                    "content": f"Work harness/queue/briefs/{slug} to completion."},
    })]
    emitted = 0
    index = 0
    while emitted < tool_calls:
        batch = min(calls_per_request, tool_calls - emitted)
        lines.append(json.dumps({
            "type": "assistant",
            "requestId": f"req_{index}",
            "message": {
                "id": f"msg_{index}",
                "model": "claude-sonnet-5",
                "usage": {"input_tokens": 1, "cache_creation_input_tokens": 0,
                          "cache_read_input_tokens": 100, "output_tokens": 1},
                "content": [{"type": "tool_use", "name": "Bash", "input": {}}
                            for _ in range(batch)],
            },
        }))
        emitted += batch
        index += 1
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return path


def make_brief(tmp_path: Path, body: str = "") -> Path:
    brief_dir = tmp_path / "briefs" / SLUG
    (brief_dir / "deliverables").mkdir(parents=True)
    (brief_dir / "brief.md").write_text(body or "# Brief\n", encoding="utf-8")
    return brief_dir


def run(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run([sys.executable, str(SCRIPT), *args],
                          capture_output=True, text=True, timeout=120)


# --- thresholds ---------------------------------------------------------


def test_thresholds_are_the_agreed_numbers():
    assert (budget.WARN_CALLS, budget.CHECKPOINT_CALLS,
            budget.HARD_STOP_CALLS, budget.NO_PROGRESS_CALLS) == (100, 130, 160, 35)


def test_classify_walks_the_bands():
    assert budget.classify(0, 0) == "OK"
    assert budget.classify(99, 0) == "OK"
    assert budget.classify(100, 0) == "WARN"
    assert budget.classify(129, 0) == "WARN"
    assert budget.classify(130, 0) == "CHECKPOINT_DUE"
    assert budget.classify(159, 0) == "CHECKPOINT_DUE"
    assert budget.classify(160, 0) == "BUDGET_EXHAUSTED"


def test_hard_stop_outranks_no_progress():
    """Both conditions true -> the harder stop wins, so the reported reason
    is the one that actually ends the run."""
    assert budget.classify(200, 99) == "BUDGET_EXHAUSTED"


def test_no_progress_stop_fires_below_the_call_ceiling():
    """RED if the no-progress clock is ignored: 40 calls is well under 100,
    so only the stalled-progress rule can catch this."""
    assert budget.classify(40, 40) == "NO_PROGRESS_STOP"
    assert budget.classify(40, 34) == "OK"


def test_status_reports_bands_end_to_end(tmp_path: Path):
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=135)
    # Record progress first: with an empty ledger the no-progress clock runs
    # from call 0 and would stop the run long before the checkpoint band.
    run("progress", "--brief", str(brief), "--kind", "gate_check_gained",
        "--evidence", "verdict_audit 7/9 -> 8/9", "--transcripts", str(transcripts))
    result = run("status", "--brief", str(brief), "--transcripts", str(transcripts))
    assert result.returncode == budget.EXIT_CHECKPOINT_DUE, result.stdout
    assert "status     : CHECKPOINT_DUE" in result.stdout
    assert "tool_calls : 135" in result.stdout


def test_empty_progress_ledger_stalls_at_35_and_explains_why(tmp_path: Path):
    """Zero recorded progress is zero measurable progress -- the clock runs
    from call 0. Correct, but surprising enough that the output must say so."""
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=40)
    result = run("status", "--brief", str(brief), "--transcripts", str(transcripts))
    assert result.returncode == budget.EXIT_NO_PROGRESS
    assert "clock runs from call 0" in result.stdout


def test_tool_calls_counted_not_api_requests(tmp_path: Path):
    """Several tool calls can ride one API request. The budget is expressed
    in tool calls, so batching must not hide them."""
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=160, calls_per_request=4)
    result = run("status", "--brief", str(brief), "--transcripts", str(transcripts),
                 "--json")
    payload = json.loads(result.stdout)
    assert payload["tool_calls"] == 160
    assert payload["api_requests"] == 40
    assert payload["status"] == "BUDGET_EXHAUSTED"
    assert result.returncode == budget.EXIT_BUDGET_EXHAUSTED


# --- the unmeasurable case ----------------------------------------------


def test_missing_transcript_is_unmeasurable_never_ok(tmp_path: Path):
    """RED if absence is treated as zero calls: the run would look healthy
    precisely when nothing is being enforced."""
    brief = make_brief(tmp_path)
    result = run("status", "--brief", str(brief),
                 "--transcripts", str(tmp_path / "nowhere"))
    assert result.returncode == budget.EXIT_UNMEASURABLE
    assert "UNMEASURABLE" in result.stdout
    assert "not OK" in result.stdout
    assert "status     : OK" not in result.stdout


def test_unmeasurable_exit_code_differs_from_every_enforced_status():
    """A caller branching on exit code must be able to tell "budget fine"
    from "budget unknown"."""
    assert budget.EXIT_UNMEASURABLE not in (
        budget.EXIT_CHECKPOINT_DUE, budget.EXIT_BUDGET_EXHAUSTED,
        budget.EXIT_NO_PROGRESS, budget.EXIT_OK,
    )


# --- progress ledger ----------------------------------------------------


def test_progress_accepts_only_the_five_mechanical_kinds(tmp_path: Path):
    brief = make_brief(tmp_path)
    transcripts = tmp_path / "t"
    make_transcript(transcripts, SLUG, tool_calls=10)
    for kind in budget.PROGRESS_KINDS:
        result = run("progress", "--brief", str(brief), "--kind", kind,
                     "--evidence", "py -m pytest x -q -> 3 passed",
                     "--transcripts", str(transcripts))
        assert result.returncode == budget.EXIT_OK, result.stderr


def test_progress_rejects_a_narrative_kind(tmp_path: Path):
    """'I made progress' is not a mechanical event."""
    brief = make_brief(tmp_path)
    result = run("progress", "--brief", str(brief), "--kind", "felt_productive",
                 "--evidence", "lots done")
    assert result.returncode == budget.EXIT_USAGE
    assert "Unknown progress kind" in result.stderr


def test_progress_requires_evidence(tmp_path: Path):
    """RED if empty evidence is accepted: the no-progress clock would reset
    on an assertion instead of a fact."""
    brief = make_brief(tmp_path)
    result = run("progress", "--brief", str(brief), "--kind", "red_to_green",
                 "--evidence", "   ")
    assert result.returncode == budget.EXIT_USAGE
    assert "evidence" in result.stderr.lower()


def test_progress_resets_the_no_progress_clock(tmp_path: Path):
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=50)

    stalled = run("status", "--brief", str(brief), "--transcripts", str(transcripts),
                  "--json")
    assert json.loads(stalled.stdout)["status"] == "NO_PROGRESS_STOP"

    run("progress", "--brief", str(brief), "--kind", "failures_decreased",
        "--evidence", "EditMode failures 8 -> 7", "--transcripts", str(transcripts))

    after = run("status", "--brief", str(brief), "--transcripts", str(transcripts),
                "--json")
    payload = json.loads(after.stdout)
    assert payload["tool_calls_since_progress"] == 0
    assert payload["status"] == "OK"


def test_progress_without_transcript_records_sentinel_not_zero(tmp_path: Path):
    """Hard-won rule 8: a zero can be real. 'unmeasured' must be -1."""
    brief = make_brief(tmp_path)
    result = run("progress", "--brief", str(brief), "--kind", "deliverable_created",
                 "--evidence", "manifest.json written",
                 "--transcripts", str(tmp_path / "nowhere"))
    assert result.returncode == budget.EXIT_OK
    events = budget.load_progress(brief)
    assert events[-1]["tool_calls_at"] == -1


# --- checkpoint ---------------------------------------------------------


def test_checkpoint_has_all_nine_required_sections(tmp_path: Path):
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=170)
    run("progress", "--brief", str(brief), "--kind", "plan_step_done",
        "--evidence", "step 2 of the plan complete", "--transcripts", str(transcripts))

    result = run("checkpoint", "--brief", str(brief), "--transcripts", str(transcripts))
    assert result.returncode == budget.EXIT_OK, result.stderr

    written = sorted((brief / "deliverables").glob("checkpoint-*.md"))
    assert len(written) == 1
    text = written[0].read_text(encoding="utf-8")
    for heading in ("Objectif du lot", "Travail terminé", "Fichiers modifiés",
                    "Tests exécutés et résultats", "Décisions prises",
                    "Problèmes ouverts", "Prochaine action exacte",
                    "Commande de reprise", "Contexte minimal nécessaire"):
        assert heading in text, f"missing checkpoint section: {heading}"
    # Measured numbers are pre-filled, and the progress ledger travels with it.
    assert "| tool calls | 170 |" in text
    assert "plan_step_done" in text
    # It must say plainly that this is not a verdict.
    assert "not a verdict" in text


def test_checkpoints_do_not_overwrite_each_other(tmp_path: Path):
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=165)
    run("checkpoint", "--brief", str(brief), "--transcripts", str(transcripts))
    run("checkpoint", "--brief", str(brief), "--transcripts", str(transcripts))
    written = sorted((brief / "deliverables").glob("checkpoint-*.md"))
    assert [p.name for p in written] == ["checkpoint-001.md", "checkpoint-002.md"]


# --- NEEDS_SPLIT --------------------------------------------------------


BIG_BRIEF = (
    "# Brief\n\nPort the whole Unity game.\n\n"
    "## Success Conditions\n\n"
    "1. **A.** touch `sim/x`\n2. **B.** touch `pipeline/y`\n3. **C.** touch `unity/z`\n"
)


def test_split_check_flags_an_over_estimate(tmp_path: Path):
    """The estimate is the one trigger that survived calibration."""
    brief = make_brief(tmp_path, body="# Brief\n\nA narrow change in `docs/`.\n")
    result = run("split-check", "--brief", str(brief), "--estimated-calls", "400")
    assert "NEEDS_SPLIT" in result.stdout
    assert "400" in result.stdout


def test_split_check_passes_a_small_brief_despite_loud_signals(tmp_path: Path):
    """RED if the textual signals still trigger. Measured on the 5 briefs
    with known cost, subsystem breadth is anti-correlated with cost (001:
    3 subsystems / 108 calls; 005: 1 subsystem / 766), and 'whole' fires on
    all five. A check that flags everything carries no information, so this
    brief -- global-goal wording AND three subsystems -- must still pass on
    a small estimate."""
    brief = make_brief(tmp_path, body=BIG_BRIEF)
    result = run("split-check", "--brief", str(brief), "--estimated-calls", "40")
    assert "SIZE_OK" in result.stdout
    assert "NEEDS_SPLIT" not in result.stdout


def test_split_check_still_reports_those_signals(tmp_path: Path):
    """Demoted to signals, not deleted: the Planificateur needs them to make
    the independence judgement the script cannot."""
    brief = make_brief(tmp_path, body=BIG_BRIEF)
    result = run("split-check", "--brief", str(brief), "--estimated-calls", "40")
    assert "subsystems in Success Conditions : 3" in result.stdout
    assert "success conditions               : 3" in result.stdout
    assert "NOT triggers" in result.stdout
    assert "INDEPENDENT" in result.stdout


def test_split_check_without_an_estimate_refuses_to_conclude(tmp_path: Path):
    """RED if a missing estimate silently reads as SIZE_OK -- that would be
    the same 'unmeasured looks fine' failure the status command avoids."""
    brief = make_brief(tmp_path, body=BIG_BRIEF)
    result = run("split-check", "--brief", str(brief))
    assert "NO_ESTIMATE" in result.stdout
    assert "SIZE_OK" not in result.stdout
    assert "NEEDS_SPLIT" not in result.stdout


def test_subsystem_signal_is_scoped_to_success_conditions(tmp_path: Path):
    """Counting roots across the whole file measures how chatty a brief is,
    not what it commits to touching."""
    brief = make_brief(tmp_path, body=(
        "# Brief\n\n## Non-Goals\n\nDoes not touch `sim/` or `pipeline/`.\n\n"
        "## Success Conditions\n\n1. **A.** change `docs/adr/0009-x.md`\n"
    ))
    result = run("split-check", "--brief", str(brief), "--estimated-calls", "40")
    assert "subsystems in Success Conditions : 1" in result.stdout
    assert "docs/" in result.stdout


def test_split_check_is_labelled_advisory(tmp_path: Path):
    """Whether two subsystems are truly independent is a judgement this
    script cannot make; it must not present itself as the decision."""
    brief = make_brief(tmp_path, body=BIG_BRIEF)
    result = run("split-check", "--brief", str(brief), "--estimated-calls", "40")
    assert "advisory" in result.stdout.lower()
    assert "Planificateur decides" in result.stdout


# --- statuses are not gate verdicts -------------------------------------


def test_budget_statuses_never_collide_with_a_gate_reject(tmp_path: Path):
    """verdict_audit.py exits 1 on REJECT. A budget stop must not produce
    that code, or a loop branching on exit status would file an oversized
    brief as defective work."""
    for code in (budget.EXIT_BUDGET_EXHAUSTED, budget.EXIT_NO_PROGRESS,
                 budget.EXIT_CHECKPOINT_DUE, budget.EXIT_UNMEASURABLE):
        assert code != 1, "budget status collides with verdict_audit REJECT"


def test_stop_output_says_it_is_not_a_reject(tmp_path: Path):
    transcripts = tmp_path / "t"
    brief = make_brief(tmp_path)
    make_transcript(transcripts, SLUG, tool_calls=200)
    result = run("status", "--brief", str(brief), "--transcripts", str(transcripts))
    assert result.returncode == budget.EXIT_BUDGET_EXHAUSTED
    assert "NOT a REJECT" in result.stdout
