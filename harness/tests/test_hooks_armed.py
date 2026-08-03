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

EXPECTED_HOOK_SCRIPTS = {
    "no_bare_python.py",
    "guard_git_push.py",
    "guard_vision_edit.py",
    "remind_handoff_stale.py",
}


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
    missing = [name for name in EXPECTED_HOOK_SCRIPTS if name not in wired]
    assert not missing, f"hook scripts present on disk but not wired in settings.json: {missing}"

    for name in EXPECTED_HOOK_SCRIPTS:
        assert (REPO_ROOT / ".claude" / "hooks" / name).exists(), \
            f"settings.json wires {name} but the script is gone"


def test_settings_json_stays_parseable():
    """The permissions block is hand-edited; a syntax error there would take
    the hooks down with it, which is the same outage this file guards."""
    config = json.loads(SETTINGS.read_text(encoding="utf-8"))
    assert "hooks" in config, "settings.json parsed but has no hooks block"
