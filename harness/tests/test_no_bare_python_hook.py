"""
Tests for .claude/hooks/no_bare_python.py.

The hook enforces hard-won rule 1 (`py`, never `python` — the Store alias is
a fake stub). It was itself an instance of hard-won rule 6: a substring match
is too coarse, and blocked commands nobody meant — you could not `grep` for
the word, mention it in a commit message, or put it in a string.

Both directions are asserted, because either failure is real: a missed
invocation gives a confusing stub error, and a spurious block costs a tool
call and teaches people to route around the guard. Every MUST-BLOCK case is
a shell construct that genuinely executes the interpreter; every MUST-ALLOW
case is one that observably does not.

Invokes the hook as a real subprocess with the actual payload shape, so the
CLI contract (exit 2 blocks, exit 0 allows) is exercised, not just a regex.
"""
import json
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HOOK = REPO_ROOT / ".claude" / "hooks" / "no_bare_python.py"


def run_hook(command) -> subprocess.CompletedProcess:
    payload = json.dumps({"tool_input": {"command": command}})
    return subprocess.run(
        [sys.executable, str(HOOK)],
        input=payload, capture_output=True, text=True, timeout=60,
    )


MUST_BLOCK = [
    "python foo.py",
    "python -c 'print(1)'",
    "python",
    "ls && python foo.py",
    "ls; python foo.py",
    "cat data.txt | python -",
    "FORGE_ENV=1 python foo.py",
    "sudo python foo.py",
    "time python foo.py",
    "if python check.py; then echo ok; fi",
    "for f in *.py; do python $f; done",
    "$(python -c 'print(1)')",
    'bash -c "python foo.py"',
    "bash -lc 'python foo.py'",
    "py setup.py && python teardown.py",
]

MUST_ALLOW = [
    # The invocations the rule actually wants.
    "py foo.py",
    "py -m pytest harness/tests/ -q",
    "python3 foo.py",
    "py .claude/hooks/no_bare_python.py",
    # The word as data -- every one of these was blocked by the old check.
    "grep -rn python docs/",
    "grep -c python harness/backends/ledger.py",
    'git commit -m "drop python fallback"',
    "py -c \"print('arbitrary python, on one machine')\"",
    "echo 'we use python here'",
    "ls python_tools/",
    "which python",
    "# run python here later",
    "cat notes.md  # mentions python",
    "./python --version",
    "rg 'python' --type md",
]


@pytest.mark.parametrize("command", MUST_BLOCK)
def test_blocks_real_invocations(command):
    result = run_hook(command)
    assert result.returncode == 2, f"should have blocked: {command!r}\n{result.stdout}"
    assert "bare `python`" in result.stderr


@pytest.mark.parametrize("command", MUST_ALLOW)
def test_allows_the_word_as_data(command):
    result = run_hook(command)
    assert result.returncode == 0, (
        f"false positive on: {command!r}\n{result.stderr}"
    )


def test_heredoc_body_is_not_a_command():
    """A heredoc body's lines start at a line boundary, which is otherwise a
    command position -- prose about python inside one is still just prose."""
    command = (
        "cat > notes.md <<'EOF'\n"
        "python was considered and rejected here\n"
        "EOF\n"
    )
    assert run_hook(command).returncode == 0


def test_heredoc_does_not_hide_a_real_invocation_after_it():
    """Stripping the body must not swallow the rest of the command."""
    command = (
        "cat > notes.md <<'EOF'\n"
        "some notes\n"
        "EOF\n"
        "python build.py\n"
    )
    assert run_hook(command).returncode == 2


def test_list_form_command_is_handled():
    result = run_hook(["python", "foo.py"])
    assert result.returncode == 2


def test_malformed_payload_allows_rather_than_crashes():
    """Not this hook's job to police the payload -- but it must not block on
    one either, or a harness change would wedge every Bash call."""
    result = subprocess.run(
        [sys.executable, str(HOOK)],
        input="not json", capture_output=True, text=True, timeout=60,
    )
    assert result.returncode == 0


def test_missing_command_key_allows():
    result = subprocess.run(
        [sys.executable, str(HOOK)],
        input=json.dumps({"tool_input": {}}), capture_output=True, text=True, timeout=60,
    )
    assert result.returncode == 0
