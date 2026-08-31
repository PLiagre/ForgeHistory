"""Câblage des hooks techniques facultatifs de l'environnement Claude."""

import json
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SETTINGS = REPO_ROOT / ".claude" / "settings.json"
HOOKS_DIR = REPO_ROOT / ".claude" / "hooks"
CODES_DU_PROTOCOLE = (0, 2)


def hook_scripts_on_disk() -> set[str]:
    return {path.name for path in HOOKS_DIR.glob("*.py")}


def hook_commands() -> list[str]:
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    return [
        hook["command"]
        for group in config.get("hooks", {}).values()
        for entry in group
        for hook in entry.get("hooks", [])
        if hook.get("type") == "command"
    ]


def play(command: str, payload: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        command.replace("$CLAUDE_PROJECT_DIR", str(REPO_ROOT)),
        shell=True,
        input=payload,
        capture_output=True,
        text=True,
        timeout=60,
    )


def test_settings_is_parseable_and_every_script_is_wired():
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    wired = json.dumps(config.get("hooks", {}))
    scripts = hook_scripts_on_disk()
    assert scripts
    assert not [name for name in scripts if name not in wired]


def test_no_governance_hook_is_wired():
    wired = " ".join(hook_commands())
    assert "guard_git_push" not in wired
    assert "guard_vision_edit" not in wired


def test_hook_paths_do_not_depend_on_current_directory():
    commands = hook_commands()
    assert commands
    assert not [
        command
        for command in commands
        if ".claude/hooks/" in command and "$CLAUDE_PROJECT_DIR" not in command
    ]


def test_every_hook_command_reaches_its_script():
    payload = json.dumps(
        {"tool_input": {"command": "ls -la", "file_path": "README.md"}}
    )
    failures = []
    for command in hook_commands():
        result = play(command, payload)
        if result.returncode not in CODES_DU_PROTOCOLE:
            failures.append((command, result.returncode, result.stderr[-120:]))
    assert not failures


def test_bare_python_hook_blocks_only_the_problematic_command():
    command = next(item for item in hook_commands() if "no_bare_python.py" in item)
    blocked = play(
        command, json.dumps({"tool_input": {"command": "python fichier.py"}})
    )
    allowed = play(
        command, json.dumps({"tool_input": {"command": "python3 fichier.py"}})
    )
    assert blocked.returncode == 2
    assert allowed.returncode == 0
