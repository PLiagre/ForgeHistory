"""
Tests for unity/run-unity.ps1 -- the single-tool-call Unity wait wrapper.

Hard-won rule 4: prove red first. The wrapper exists to delete a measured
defect (586 `wc -l` polls of one log file inside a single Générateur), so
the tests assert the properties that make the deletion safe rather than
merely that the script runs:

  - it returns exactly once, and its stdout is a bounded summary, never the
    log body (the whole point is to keep the log out of the context);
  - a timeout kills the process tree, leaving no orphan (a wrapper that
    abandons a wedged Unity is worse than the polling it replaces);
  - Unity's exit code reaches the caller unchanged, so the gate still sees
    real failures.

Never needs a Unity install: -UnityExe is a seam, and every test points it
at a stand-in PowerShell script.
"""
import os
import shutil
import subprocess
import sys
import textwrap
import time
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPT = REPO_ROOT / "unity" / "run-unity.ps1"

POWERSHELL = shutil.which("powershell.exe") or shutil.which("powershell")

pytestmark = pytest.mark.skipif(
    POWERSHELL is None, reason="powershell.exe not available on this platform"
)

EXIT_TIMEOUT = 124
EXIT_PRECONDITION = 125


def make_fake_unity(tmp_path: Path) -> Path:
    """A stand-in for Unity.exe: writes a log, optionally lingers, exits.

    Deliberately mimics the traits the wrapper has to cope with -- it writes
    its log lazily (so the log is absent for a moment after launch) and its
    exit code is caller-chosen.
    """
    fake = tmp_path / "fake-unity.ps1"
    fake.write_text(
        textwrap.dedent(
            """
            param(
                [string]$LogFile,
                [int]$ExitCode = 0,
                [int]$SleepSec = 0,
                [int]$LogLines = 3,
                [int]$CompileErrors = 0,
                [switch]$NoLog,
                [switch]$SpawnChild
            )
            # Unity does not create the log instantly; neither do we.
            Start-Sleep -Milliseconds 300
            if ($SpawnChild) {
                # Stands in for the compiler workers a real Unity leaves
                # behind: killing only the parent would orphan this.
                $child = Start-Process -FilePath (Get-Process -Id $PID).Path `
                    -ArgumentList '-NoProfile -Command "Start-Sleep -Seconds 300"' `
                    -PassThru -WindowStyle Hidden
                Set-Content -LiteralPath "$LogFile.childpid" -Value $child.Id
            }
            if (-not $NoLog) {
                $lines = @()
                for ($i = 1; $i -le $LogLines; $i++) {
                    $lines += "UNIQUELOGBODY line $i of routine Unity chatter"
                }
                for ($i = 1; $i -le $CompileErrors; $i++) {
                    $lines += "Assets/Scripts/Thing$i.cs(10,5): error CS0103: fake error $i"
                }
                Set-Content -LiteralPath $LogFile -Value $lines -Encoding UTF8
            }
            if ($SleepSec -gt 0) { Start-Sleep -Seconds $SleepSec }
            exit $ExitCode
            """
        ).strip(),
        encoding="utf-8",
    )
    return fake


def pid_alive(pid: int) -> bool:
    probe = subprocess.run(
        [POWERSHELL, "-NoProfile", "-Command",
         f"if (Get-Process -Id {pid} -ErrorAction SilentlyContinue)"
         " { 'ALIVE' } else { 'GONE' }"],
        capture_output=True, text=True, timeout=60,
    )
    return probe.stdout.strip() == "ALIVE"


def summary_field(stdout: str, key: str) -> str:
    for line in stdout.splitlines():
        if line.startswith(key):
            return line.split(":", 1)[1].strip()
    raise AssertionError(f"{key!r} missing from summary:\n{stdout}")


def run_wrapper(log: Path, fake: Path, *, exit_code=0, sleep=0, log_lines=3,
                compile_errors=0, no_log=False, spawn_child=False, timeout_sec=60,
                test_results="", extra=None) -> subprocess.CompletedProcess:
    arg_line = (
        f'-NoProfile -ExecutionPolicy Bypass -File "{fake}" '
        f'-LogFile "{log}" -ExitCode {exit_code} -SleepSec {sleep} '
        f'-LogLines {log_lines} -CompileErrors {compile_errors}'
    )
    if no_log:
        arg_line += " -NoLog"
    if spawn_child:
        arg_line += " -SpawnChild"
    cmd = [
        POWERSHELL, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(SCRIPT),
        "-LogFile", str(log),
        "-UnityExe", POWERSHELL,
        "-UnityArgLine", arg_line,
        "-TimeoutSec", str(timeout_sec),
    ]
    if test_results:
        cmd += ["-TestResults", str(test_results)]
    if extra:
        cmd += extra
    return subprocess.run(cmd, capture_output=True, text=True, timeout=300)


# --- the three outcomes -------------------------------------------------


def test_success_returns_zero_and_says_so(tmp_path: Path):
    log = tmp_path / "unity.log"
    result = run_wrapper(log, make_fake_unity(tmp_path))
    assert result.returncode == 0, result.stderr
    assert "outcome    : success" in result.stdout
    assert "compile_errors : 0" in result.stdout


def test_unity_failure_propagates_its_exit_code(tmp_path: Path):
    """RED if the wrapper swallows the code: the gate would see a pass."""
    log = tmp_path / "unity.log"
    result = run_wrapper(log, make_fake_unity(tmp_path), exit_code=3, compile_errors=2)
    assert result.returncode == 3, result.stdout
    assert "outcome    : unity_failed" in result.stdout
    assert "compile_errors : 2" in result.stdout
    assert "error CS0103" in result.stdout  # bounded excerpt, so the agent can act


def test_exit_code_propagation_is_not_hardcoded(tmp_path: Path):
    """A second, different code -- guards against returning a constant."""
    log = tmp_path / "unity.log"
    result = run_wrapper(log, make_fake_unity(tmp_path), exit_code=17)
    assert result.returncode == 17, result.stdout


def test_timeout_exits_124_and_leaves_no_orphan(tmp_path: Path):
    """A wrapper that gives up but abandons a wedged Unity is worse than the
    polling it replaces. Asserts on real pids -- the watched process AND a
    descendant, so the taskkill /T is genuinely exercised."""
    log = tmp_path / "unity.log"
    started = time.time()
    result = run_wrapper(
        log, make_fake_unity(tmp_path), sleep=120, spawn_child=True, timeout_sec=5
    )
    elapsed = time.time() - started

    assert result.returncode == EXIT_TIMEOUT, result.stdout
    assert "outcome    : timeout" in result.stdout
    # It really waited rather than returning early, and really gave up
    # rather than sitting out the 120 s sleep.
    assert 4 <= elapsed < 60, f"elapsed {elapsed}"
    assert "KILL UNCONFIRMED" not in result.stdout, "process tree outlived the wrapper"

    watched = int(summary_field(result.stdout, "watched_pid"))
    assert not pid_alive(watched), f"orphaned watched process {watched}"

    child_file = Path(f"{log}.childpid")
    assert child_file.exists(), "fake Unity never recorded its descendant"
    child = int(child_file.read_text(encoding="utf-8").strip())
    assert not pid_alive(child), f"orphaned descendant {child} (taskkill /T did not reach it)"


# --- edge cases the real environment produces ---------------------------


def test_log_absent_at_startup_is_fine(tmp_path: Path):
    """Unity writes the log lazily; the wrapper must not require it upfront."""
    log = tmp_path / "not-yet" / "unity.log"
    log.parent.mkdir()
    assert not log.exists()
    result = run_wrapper(log, make_fake_unity(tmp_path))
    assert result.returncode == 0, result.stderr
    assert "outcome    : success" in result.stdout
    assert log.exists()


def test_log_never_written_is_reported_not_crashed(tmp_path: Path):
    """A relative -logFile has produced empty logs here before; that is a
    finding to surface, not an exception to raise."""
    log = tmp_path / "never.log"
    result = run_wrapper(log, make_fake_unity(tmp_path), no_log=True)
    assert result.returncode == 0, result.stderr
    assert "log_status : ABSENT" in result.stdout


def test_already_exited_process_returns_immediately(tmp_path: Path):
    log = tmp_path / "unity.log"
    log.write_text("done earlier\n", encoding="utf-8")

    # A pid that has certainly exited.
    finished = subprocess.run(
        [POWERSHELL, "-NoProfile", "-Command", "exit 0"], capture_output=True, timeout=60
    )
    dead_pid = 999999  # not in use; Get-Process finds nothing
    assert finished.returncode == 0

    started = time.time()
    result = subprocess.run(
        [POWERSHELL, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(SCRIPT),
         "-LogFile", str(log), "-AttachPid", str(dead_pid), "-TimeoutSec", "60"],
        capture_output=True, text=True, timeout=120,
    )
    elapsed = time.time() - started
    assert result.returncode == 0, result.stdout + result.stderr
    assert "outcome    : already_exited" in result.stdout
    assert elapsed < 30, "should not have waited on a pid that will never exit"


def test_relative_logfile_is_refused(tmp_path: Path):
    result = subprocess.run(
        [POWERSHELL, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(SCRIPT),
         "-LogFile", "relative/unity.log", "-UnityExe", POWERSHELL, "-TimeoutSec", "5"],
        capture_output=True, text=True, timeout=120,
    )
    assert result.returncode == EXIT_PRECONDITION
    assert "absolute" in (result.stdout + result.stderr).lower()


def test_missing_unity_executable_is_a_precondition_failure(tmp_path: Path):
    log = tmp_path / "unity.log"
    result = subprocess.run(
        [POWERSHELL, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(SCRIPT),
         "-LogFile", str(log), "-UnityExe", str(tmp_path / "no-such-unity.exe"),
         "-TimeoutSec", "5"],
        capture_output=True, text=True, timeout=120,
    )
    assert result.returncode == EXIT_PRECONDITION
    assert "not found" in (result.stdout + result.stderr).lower()


def test_nunit_totals_are_summarized_so_the_agent_skips_the_xml(tmp_path: Path):
    log = tmp_path / "unity.log"
    xml = tmp_path / "results.xml"
    xml.write_text(
        '<?xml version="1.0" encoding="utf-8"?>'
        '<test-run total="274" passed="267" failed="7" skipped="0"></test-run>',
        encoding="utf-8",
    )
    result = run_wrapper(log, make_fake_unity(tmp_path), test_results=xml)
    assert result.returncode == 0, result.stderr
    assert "total=274" in result.stdout
    assert "failed=7" in result.stdout


# --- the anti-polling contract ------------------------------------------


def test_summary_is_bounded_and_never_echoes_the_log(tmp_path: Path):
    """The reason the wrapper exists: the log must stay on disk, out of the
    transcript. RED if anyone makes it tail or cat the log."""
    log = tmp_path / "unity.log"
    result = run_wrapper(
        log, make_fake_unity(tmp_path), log_lines=5000, compile_errors=50
    )
    assert result.returncode == 0, result.stderr
    assert "UNIQUELOGBODY" not in result.stdout, "wrapper streamed the log body"
    assert len(result.stdout) < 4000, f"summary is {len(result.stdout)} chars"
    # Error excerpt is capped, with the remainder counted rather than dumped.
    assert result.stdout.count("error CS0103") <= 10
    assert "more error line(s) in the log" in result.stdout


def test_summary_block_is_emitted_exactly_once(tmp_path: Path):
    """One return per call. Two blocks would mean a loop crept back in."""
    log = tmp_path / "unity.log"
    result = run_wrapper(log, make_fake_unity(tmp_path))
    assert result.stdout.count("=== unity-run summary ===") == 1
    assert result.stdout.count("=== end unity-run summary ===") == 1


POLL_MARKERS = (
    "poll the log", "poll the -logfile", "polling the log",
    "poll the log file", "re-check the log every", "check the log every",
)
# A mention of polling is allowed only where it is being forbidden. Anything
# else is a prescription, whatever the surrounding prose intends.
NEGATIONS = ("do not", "don't", "never", "rather than", "instead of",
             "not a poll", "no polling", "stop ")


def _normalize(text: str) -> str:
    """Strip markdown emphasis so 'do **not**' reads as 'do not'."""
    return text.lower().replace("*", "").replace("`", "").replace("_", "")


# Closed briefs are historical record, not live instruction. Brief 003 is
# gated ACCEPT 9/9 and its verdict cites its own text; rewriting it would
# falsify the record the gate checks. Each entry names the file and why, and
# a companion test fails once an entry stops being load-bearing -- so this
# list can only shrink, never quietly absorb a new brief.
GRANDFATHERED_POLLING = {
    "harness/queue/briefs/003-port-unity-game/brief.md":
        "closed at ACCEPT 9/9 on 2026-07-31; its verdict cites this text verbatim",
}


def _polling_offenders(paths) -> list[str]:
    offenders = []
    for path in paths:
        rel = path.relative_to(REPO_ROOT).as_posix()
        if rel in GRANDFATHERED_POLLING:
            continue
        for lineno, raw in enumerate(
            path.read_text(encoding="utf-8", errors="replace").splitlines(), 1
        ):
            line = _normalize(raw)
            if not any(marker in line for marker in POLL_MARKERS):
                continue
            if any(neg in line for neg in NEGATIONS):
                continue
            offenders.append(f"{rel}:{lineno}: {raw.strip()[:90]}")
    return offenders


def test_no_brief_prescribes_polling():
    """The gap an external audit found: `brief.md` is the single source of
    instruction, and briefs 003-005 are exactly where the polling was
    prescribed -- but the original check never looked at them."""
    briefs = sorted((REPO_ROOT / "harness" / "queue" / "briefs").rglob("*.md"))
    assert briefs, "no briefs found -- the check would pass vacuously"
    offenders = _polling_offenders(briefs)
    assert not offenders, "polling prescribed in a brief:\n  " + "\n  ".join(offenders)


def test_every_grandfathered_brief_still_needs_its_exemption():
    """An exemption that is no longer load-bearing is a hole left open. If a
    grandfathered file stops tripping the rule, delete its entry."""
    stale = []
    for rel, reason in list(GRANDFATHERED_POLLING.items()):
        path = REPO_ROOT / rel
        if not path.exists():
            stale.append(f"{rel} (file gone; drop the exemption)")
            continue
        # Temporarily un-exempt it and confirm it would still trip.
        saved = GRANDFATHERED_POLLING.pop(rel)
        try:
            if not _polling_offenders([path]):
                stale.append(f"{rel} (no longer trips; drop it -- {reason})")
        finally:
            GRANDFATHERED_POLLING[rel] = saved
    assert not stale, "stale polling exemptions: " + "; ".join(stale)


def test_no_instruction_file_still_prescribes_polling():
    """The wrapper is only half the fix: the instructions that told agents
    to poll must stop saying so, or the next Générateur polls anyway.

    Deliberately not a bare substring ban -- HANDOFF, unity/README.md and
    the Planificateur all have to *name* the pattern in order to forbid it.
    The mechanical rule is therefore: a line that mentions polling must also
    carry a negation on that same line."""
    targets = [REPO_ROOT / "HANDOFF.md", REPO_ROOT / "CLAUDE.md",
               REPO_ROOT / "unity" / "README.md"]
    targets += sorted((REPO_ROOT / ".claude" / "agents").glob("*.md"))
    targets += sorted((REPO_ROOT / ".claude" / "commands").glob("*.md"))
    targets += sorted((REPO_ROOT / ".claude" / "skills").rglob("*.md"))
    offenders = _polling_offenders(targets)
    assert not offenders, "polling prescribed (not forbidden) at:\n  " + "\n  ".join(offenders)
