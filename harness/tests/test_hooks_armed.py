"""
The hooks must actually be in place, not merely committed.

`harness/backends/run_cursor_generator.sh` moves `.claude/settings.json`
aside for the duration of a Cursor run, because hooks break under
cursor-agent, and restores it via `trap`. The trap covers a normal exit, an
error exit, and INT/TERM/HUP/QUIT -- it cannot cover SIGKILL, `taskkill /F`,
a power cut or a host crash. Any of those leaves the repository with NO
hooks: the bare-`python` block, the git-push-when-red guard and the
VISION.md guard all disappear at once, silently.

The script now refuses to start a second run on top of that state. This test
makes the state visible to the rest of the harness too, so a disarmed
repository fails the ordinary test run rather than waiting for someone to
notice a missing file.
"""
import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SETTINGS = REPO_ROOT / ".claude" / "settings.json"
PARKED = REPO_ROOT / ".claude" / "settings.json.cursor-hook-bug-disabled"

HOOKS_DIR = REPO_ROOT / ".claude" / "hooks"


def hook_scripts_on_disk() -> set[str]:
    """
    La liste attendue DÉRIVE du disque (règle 2 : un contrôle dérive, il
    n'est jamais nommé d'après sa cible). Une liste écrite en dur ici
    devenait fausse à chaque hook ajouté ou retiré.
    """
    return {p.name for p in HOOKS_DIR.glob("*.py")}


def test_settings_json_is_not_parked():
    """RED exactly when a Cursor run died without restoring the file."""
    assert not PARKED.exists(), (
        f"{PARKED.name} exists: a Cursor run left this repository with no hooks. "
        f"Recover with: mv '{PARKED}' '{SETTINGS}'"
    )
    assert SETTINGS.exists(), f"{SETTINGS} is missing entirely"


def test_every_hook_script_is_still_wired():
    """A hook file on disk that nothing references protects nothing --
    hard-won rule 5, applied to the wiring rather than the placement."""
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    wired = json.dumps(config.get("hooks", {}))
    scripts = hook_scripts_on_disk()
    assert scripts, "aucun hook sur le disque : le dépôt est désarmé"

    missing = sorted(name for name in scripts if name not in wired)
    assert not missing, f"hooks présents sur le disque mais non branchés dans settings.json : {missing}"


def test_settings_json_stays_parseable():
    """The permissions block is hand-edited; a syntax error there would take
    the hooks down with it, which is the same outage this file guards."""
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    assert "hooks" in config, "settings.json parsed but has no hooks block"


def hook_commands() -> list[str]:
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    return [
        hook["command"]
        for group in config.get("hooks", {}).values()
        for entry in group
        for hook in entry.get("hooks", [])
        if hook.get("type") == "command"
    ]


def cwd_dependent(command: str) -> bool:
    """True when the command names a hook script by a path that only resolves
    from the repository root."""
    return ".claude/hooks/" in command and "$CLAUDE_PROJECT_DIR" not in command


def test_the_old_relative_form_is_recognised_as_broken():
    """Red-first, kept permanently rather than performed once.

    2026-08-11: a session ran `cd` into a brief directory. Every hook was
    wired as `py .claude/hooks/<script>.py` -- a relative path resolved
    against the *shell's* working directory, not the repository. Python could
    no longer find the scripts, so every Bash, Edit and Write call died in
    PreToolUse. The failure looks fail-closed and is worse than it looks: the
    guards were not enforcing their rules, they were simply absent, and they
    took the whole session's tool access down with them. Subagents inherit
    the working directory, so no subagent could repair it either.

    This asserts the detector actually fires on the shape that caused the
    outage. Without it, the test below could pass against a file where the
    marker string appears in a comment and prove nothing."""
    assert cwd_dependent("py .claude/hooks/no_bare_python.py")
    assert not cwd_dependent('py "$CLAUDE_PROJECT_DIR/.claude/hooks/no_bare_python.py"')


def test_hook_commands_resolve_from_any_working_directory():
    """A guard that only exists when the shell happens to sit at the
    repository root is not a guard -- it is a coincidence."""
    commands = hook_commands()
    assert commands, "settings.json declares no command hooks at all"

    broken = [c for c in commands if cwd_dependent(c)]
    assert not broken, (
        "hook command(s) use a working-directory-relative script path and will "
        f"break as soon as any tool runs from a subdirectory: {broken}"
    )
