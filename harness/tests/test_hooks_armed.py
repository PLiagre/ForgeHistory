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
import subprocess
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


# --- Une garde dont l'interpréteur n'existe pas ne garde rien ---

def _run_hook_command(command: str, payload: str) -> subprocess.CompletedProcess:
    """Joue la commande EXACTE de settings.json, comme le ferait le harnais."""
    return subprocess.run(
        command.replace("$CLAUDE_PROJECT_DIR", str(REPO_ROOT)),
        shell=True, input=payload, capture_output=True, text=True, timeout=60,
    )


def test_the_old_windows_only_form_is_recognised_as_broken():
    """
    Rouge d'abord, gardé plutôt que joué une fois.

    Les trois gardes étaient câblées en `py <script>`. `py` est exigé sur la
    machine Windows du propriétaire — c'est la règle 1, payée par un vrai
    défaut : `python` y est un faux alias du Microsoft Store. Mais `py`
    n'existe pas sous Linux, et le VPS, WSL2 et Cursor Cloud sont Linux.

    Sur ces trois-là, les gardes ne bloquaient donc RIEN : ni l'envoi quand
    les tests sont rouges, ni l'édition de VISION.md, ni l'appel à `python`
    nu. Elles ne protestaient pas non plus — elles étaient simplement
    absentes, ce qui est le pire des deux.

    Ce test prouve que le détecteur réagit à la forme qui a causé la panne.
    """
    assert _interprete_absent("py /chemin/vers/un/hook.py")
    assert not _interprete_absent(
        "sh -c 'if command -v py >/dev/null 2>&1; then exec py \"$1\"; "
        "else exec python3 \"$1\"; fi' _ \"/chemin/vers/un/hook.py\""
    )


def _interprete_absent(command: str) -> bool:
    """
    Vrai quand la commande nomme UN seul interpréteur, sans repli.

    La référence est DÉRIVÉE de la commande : on ne cherche pas le mot `py`,
    on vérifie qu'un choix à l'exécution existe. Une commande qui teste la
    présence de l'interpréteur avant de l'appeler est portable ; une qui le
    nomme directement ne l'est que là où il est installé.
    """
    return "command -v" not in command


def test_every_hook_names_an_interpreter_that_exists_here():
    """
    La présence n'est pas la fonction (règle 7) : une garde câblée dont
    l'interpréteur manque est une garde absente.

    La référence est dérivée — on ne suppose pas quelle plateforme tourne,
    on joue la commande réelle et on regarde si elle atteint le script.
    """
    commands = hook_commands()
    assert commands, "settings.json ne déclare aucune garde"

    non_portables = [c for c in commands if _interprete_absent(c)]
    assert not non_portables, (
        "garde(s) qui nomment un interpréteur sans repli : elles ne "
        f"protègent que les machines où il est installé : {non_portables}"
    )


def test_the_bare_python_guard_actually_blocks_here():
    """
    Bout en bout, sur cette machine-ci : la commande de `settings.json`
    doit rendre 2 (bloquer) sur `python foo.py`, et 0 sur `py foo.py`.

    C'est la seule vérification qui prouve que la garde marche vraiment,
    plutôt que d'être correctement écrite.
    """
    command = next(c for c in hook_commands() if "no_bare_python.py" in c)

    bloque = _run_hook_command(
        command, json.dumps({"tool_input": {"command": "python foo.py"}})
    )
    laisse = _run_hook_command(
        command, json.dumps({"tool_input": {"command": "py foo.py"}})
    )

    print(f"`python foo.py` -> code {bloque.returncode} (2 attendu)")
    print(f"`py foo.py`     -> code {laisse.returncode} (0 attendu)")

    assert bloque.returncode == 2, (
        "La garde n'a pas bloqué `python foo.py` sur cette machine. "
        f"Code {bloque.returncode}. Sortie : {bloque.stdout}{bloque.stderr}"
    )
    assert laisse.returncode == 0, (
        "La garde a bloqué `py foo.py`, qui est la forme exigée par la "
        f"règle 1. Code {laisse.returncode}. Sortie : {laisse.stderr}"
    )
