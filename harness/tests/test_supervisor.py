"""
Tests for harness/pipeline/supervisor.py -- SC14, brief 006 Lot 006c.

Hard-won rule 4: prove red first. Before this module existed nothing
external ever stopped a Générateur past its tool-call ceiling -- brief
003's 1,015-call run is the proof (harness/budget.py's own docstring names
it). These tests prove the NEW enforcement point actually fires exactly at
`HARD_STOP_CALLS` and not before, against a REAL child process (not a
mock), so the assertion is the portable, OS-observable outcome (the process
is no longer alive) rather than a POSIX-only signal-delivery mechanism that
would not hold on this dev machine (Windows 11) -- per the task's own
guidance ("assert on the supervisor's decision/terminate call rather than a
real OS kill if a real kill isn't portable"): here a real kill IS portable
(`Popen.send_signal(SIGTERM)`/`.terminate()` both resolve to
`TerminateProcess` on Windows, the same effect POSIX SIGTERM has), so it is
asserted directly, which is the stronger and more honest proof.
"""
import json
import subprocess
import sys
import time
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
import budget  # noqa: E402
from pipeline import supervisor  # noqa: E402


def _write_transcript(path: Path, n_tool_use: int) -> None:
    """A minimal fixture transcript in the real shape harness/transcripts.py
    reads: one JSONL record per content block, `type: assistant` with a
    `tool_use` content block counts as one tool call."""
    lines = []
    for i in range(n_tool_use):
        lines.append(json.dumps({
            "type": "assistant",
            "requestId": f"req{i}",
            "message": {
                "id": f"msg{i}",
                "content": [{"type": "tool_use", "name": "Bash", "id": f"tu{i}", "input": {}}],
            },
        }))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# --- pure decision function ------------------------------------------------


def test_decide_below_threshold_is_continue():
    assert supervisor.decide(budget.HARD_STOP_CALLS - 1, budget.HARD_STOP_CALLS) == "CONTINUE"


def test_decide_at_threshold_is_sigterm():
    """The exact boundary: >= threshold fires, not only > threshold."""
    assert supervisor.decide(budget.HARD_STOP_CALLS, budget.HARD_STOP_CALLS) == "SIGTERM"


def test_decide_above_threshold_is_sigterm():
    assert supervisor.decide(budget.HARD_STOP_CALLS + 50, budget.HARD_STOP_CALLS) == "SIGTERM"


def test_decide_unmeasurable_sentinel_never_bare_zero():
    """Rule 8: -1 is the sentinel for "not computed", never treated as if
    it were 0 tool calls (which would wrongly read as CONTINUE forever)."""
    assert supervisor.decide(-1, budget.HARD_STOP_CALLS) == "UNMEASURABLE"


def test_measure_tool_calls_missing_transcript_is_sentinel(tmp_path):
    missing = tmp_path / "does-not-exist.jsonl"
    assert supervisor.measure_tool_calls(missing) == -1
    assert supervisor.measure_tool_calls(None) == -1


def test_measure_tool_calls_reads_real_fixture(tmp_path):
    transcript = tmp_path / "agent-x.jsonl"
    _write_transcript(transcript, 7)
    assert supervisor.measure_tool_calls(transcript) == 7


# --- the supervising loop against a REAL child process ---------------------


def test_supervise_leaves_child_running_below_threshold(tmp_path):
    transcript = tmp_path / "agent-below.jsonl"
    _write_transcript(transcript, budget.HARD_STOP_CALLS - 1)
    child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(30)"])
    try:
        outcome = supervisor.supervise(
            child, transcript_path=transcript, hard_stop=budget.HARD_STOP_CALLS,
            poll_interval=0.05, max_polls=3,
        )
        assert outcome["decision"] == "max_polls_reached"
        assert child.poll() is None, "child must still be alive -- it was never sent SIGTERM"
    finally:
        if child.poll() is None:
            child.terminate()
        child.wait(timeout=5)


def test_supervise_sigterms_child_at_hard_stop(tmp_path):
    """The disqualifying case this module exists to close: a child whose
    measured tool-call count reaches HARD_STOP_CALLS must actually stop
    running -- proven against a child started as a 60s sleep (far longer
    than this test's own timeout), not narrated."""
    transcript = tmp_path / "agent-over.jsonl"
    _write_transcript(transcript, budget.HARD_STOP_CALLS)  # exactly at threshold
    child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
    try:
        outcome = supervisor.supervise(
            child, transcript_path=transcript, hard_stop=budget.HARD_STOP_CALLS,
            poll_interval=0.05, max_polls=10,
        )
        assert outcome["decision"] == "SIGTERM"
        assert outcome["tool_calls"] == budget.HARD_STOP_CALLS
        assert outcome["action"] in ("SIGTERM", "terminate")

        deadline = time.time() + 5
        while child.poll() is None and time.time() < deadline:
            time.sleep(0.1)
        assert child.poll() is not None, "child was still alive after SIGTERM/terminate was sent"
    finally:
        if child.poll() is None:
            child.kill()
        child.wait(timeout=5)


def test_supervise_reports_unmeasurable_without_crashing(tmp_path):
    child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(30)"])
    try:
        missing = tmp_path / "never-written.jsonl"
        outcome = supervisor.supervise(
            child, transcript_path=missing, hard_stop=budget.HARD_STOP_CALLS,
            poll_interval=0.05, max_polls=3,
        )
        assert outcome["decision"] == "max_polls_reached"
        assert outcome["tool_calls"] == -1
        assert child.poll() is None  # UNMEASURABLE never terminates a child
    finally:
        if child.poll() is None:
            child.terminate()
        child.wait(timeout=5)


def test_supervise_stops_polling_when_child_exits_on_its_own(tmp_path):
    transcript = tmp_path / "agent-quick.jsonl"
    _write_transcript(transcript, 1)
    child = subprocess.Popen([sys.executable, "-c", "pass"])
    child.wait(timeout=5)
    outcome = supervisor.supervise(
        child, transcript_path=transcript, hard_stop=budget.HARD_STOP_CALLS,
        poll_interval=0.05, max_polls=3,
    )
    assert outcome["decision"] == "child_exited"


# --- CLI --------------------------------------------------------------


def test_cli_help_exits_zero():
    script = HARNESS / "pipeline" / "supervisor.py"
    result = subprocess.run([sys.executable, str(script), "--help"], capture_output=True, text=True)
    assert result.returncode == 0, result.stderr
    assert "--transcript" in result.stdout
    assert "--hard-stop" in result.stdout


def test_cli_refuses_with_no_command(tmp_path):
    script = HARNESS / "pipeline" / "supervisor.py"
    transcript = tmp_path / "t.jsonl"
    result = subprocess.run(
        [sys.executable, str(script), "--transcript", str(transcript)],
        capture_output=True, text=True,
    )
    assert result.returncode == 2
    assert "no child command" in result.stderr
