from __future__ import annotations

from dataclasses import dataclass
from collections import deque
import json
import os
from pathlib import Path
import queue
import signal
import shutil
import subprocess
import threading
import time
from typing import Callable, Iterable, Mapping, Sequence


class PilotError(RuntimeError):
    pass


@dataclass(frozen=True)
class CommandResult:
    argv: tuple[str, ...]
    returncode: int
    stdout: str
    stderr: str
    last_event: object | None = None

    def json(self) -> object:
        if self.last_event is not None:
            return self.last_event
        try:
            return json.loads(self.stdout)
        except json.JSONDecodeError as exc:
            raise PilotError("La commande a réussi sans produire le JSON attendu.") from exc


def resolve_binary(name: str) -> str:
    resolved = shutil.which(name)
    if resolved is None:
        raise PilotError(f"Binaire introuvable : {name}")
    return resolved


def _process_group_options(platform_name: str | None = None) -> dict[str, object]:
    """Options Popen portables permettant de tuer toute l'invocation agent."""

    selected = os.name if platform_name is None else platform_name
    if selected == "nt":
        return {"creationflags": getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)}
    return {"start_new_session": True}


def run_command(
    argv: Sequence[str],
    *,
    cwd: Path,
    timeout_seconds: int,
    env: Mapping[str, str] | None = None,
    stdin: str | None = None,
    remove_env: Iterable[str] = (),
) -> CommandResult:
    child_env = os.environ.copy()
    if env:
        child_env.update(env)
    for name in remove_env:
        child_env.pop(name, None)
    try:
        completed = subprocess.run(
            list(argv),
            cwd=cwd,
            env=child_env,
            input=stdin,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            check=False,
            shell=False,
        )
    except subprocess.TimeoutExpired as exc:
        raise PilotError(f"Délai dépassé après {timeout_seconds} secondes.") from exc

    result = CommandResult(tuple(argv), completed.returncode, completed.stdout, completed.stderr)
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "aucun détail"
        raise PilotError(f"Commande en échec ({result.returncode}) : {detail}")
    return result


def run_command_stream(
    argv: Sequence[str],
    *,
    cwd: Path,
    timeout_seconds: int,
    env: Mapping[str, str] | None = None,
    stdin: str | None = None,
    on_event: Callable[[object], None] | None = None,
    max_buffered_lines: int = 100,
    max_line_bytes: int = 1024 * 1024,
    remove_env: Iterable[str] = (),
) -> CommandResult:
    """Lit stdout ligne par ligne et ne conserve qu'une fenêtre bornée.

    Le contrat est JSONL strict : toute ligne stdout non vide doit être un
    objet JSON complet. Les événements peuvent être persistés par `on_event`
    sans que le processus soit retenu intégralement en mémoire.
    """

    if timeout_seconds <= 0:
        raise PilotError("Le délai de streaming doit être strictement positif.")
    if max_buffered_lines <= 0 or max_line_bytes <= 0:
        raise PilotError("Les bornes du flux JSON doivent être strictement positives.")

    child_env = os.environ.copy()
    if env:
        child_env.update(env)
    for name in remove_env:
        child_env.pop(name, None)
    group_options = _process_group_options()
    try:
        process = subprocess.Popen(
            list(argv),
            cwd=cwd,
            env=child_env,
            stdin=subprocess.PIPE if stdin is not None else subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            bufsize=0,
            shell=False,
            **group_options,
        )
    except OSError as exc:
        raise PilotError(f"Impossible de démarrer {argv[0]!r} : {exc}") from exc

    # Le démarrage des threads et l'écriture d'un gros prompt font partie du
    # délai : aucun blocage sur stdin ne doit précéder la deadline.
    deadline = time.monotonic() + timeout_seconds

    # La file elle-même est bornée, pas seulement les tails conservés : un
    # producteur très bavard subit ainsi une contre-pression au lieu de faire
    # croître la RAM entre les threads lecteurs et le consommateur JSON.
    messages: queue.Queue[tuple[str, str | None, bool]] = queue.Queue(
        maxsize=max(4, max_buffered_lines * 2)
    )

    def read_pipe(kind: str, pipe: object) -> None:
        pending = bytearray()
        discarding_oversized_line = False

        def emit(line: bytes | None, *, oversized: bool = False) -> None:
            if line is None:
                messages.put((kind, None, False))
                return
            messages.put(
                (
                    kind,
                    line.decode("utf-8", errors="replace").rstrip("\r"),
                    oversized,
                )
            )

        try:
            reader = getattr(pipe, "read1", None) or getattr(pipe, "read")
            while True:
                chunk = reader(64 * 1024)
                if not chunk:
                    break
                cursor = 0
                while cursor < len(chunk):
                    newline = chunk.find(b"\n", cursor)
                    end = len(chunk) if newline < 0 else newline
                    segment = chunk[cursor:end]
                    if not discarding_oversized_line:
                        remaining = max_line_bytes + 1 - len(pending)
                        pending.extend(segment[:remaining])
                        if len(segment) > remaining or len(pending) > max_line_bytes:
                            pending.clear()
                            discarding_oversized_line = True
                            emit(b"", oversized=True)
                    if newline < 0:
                        break
                    if not discarding_oversized_line:
                        emit(bytes(pending))
                    pending.clear()
                    discarding_oversized_line = False
                    cursor = newline + 1
            if pending and not discarding_oversized_line:
                emit(bytes(pending))
        finally:
            emit(None)

    def write_stdin(pipe: object, value: str) -> None:
        encoded = value.encode("utf-8")
        try:
            for start in range(0, len(encoded), 64 * 1024):
                pipe.write(encoded[start : start + 64 * 1024])  # type: ignore[union-attr]
        except (BrokenPipeError, OSError, ValueError):
            pass
        finally:
            try:
                pipe.close()  # type: ignore[union-attr]
            except (OSError, ValueError):
                pass

    def kill_process_tree() -> None:
        parent_alive = process.poll() is None
        if os.name == "nt":
            if parent_alive:
                try:
                    subprocess.run(
                        ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                        stdin=subprocess.DEVNULL,
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                        timeout=5,
                        check=False,
                        shell=False,
                    )
                except (OSError, subprocess.TimeoutExpired):
                    pass
        else:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except (OSError, ProcessLookupError):
                pass
        if process.poll() is None:
            try:
                process.kill()
            except OSError:
                pass

    def cleanup_threads(*threads: threading.Thread) -> None:
        cleanup_deadline = time.monotonic() + 2
        while any(thread.is_alive() for thread in threads) and time.monotonic() < cleanup_deadline:
            try:
                messages.get(timeout=0.05)
            except queue.Empty:
                pass
        for pipe in (process.stdout, process.stderr):
            if pipe is not None:
                try:
                    pipe.close()
                except (OSError, ValueError):
                    pass
        for thread in threads:
            thread.join(timeout=0.2)

    assert process.stdout is not None and process.stderr is not None
    stdout_thread = threading.Thread(target=read_pipe, args=("stdout", process.stdout), daemon=True)
    stderr_thread = threading.Thread(target=read_pipe, args=("stderr", process.stderr), daemon=True)
    stdout_thread.start()
    stderr_thread.start()
    stdin_thread: threading.Thread | None = None
    if stdin is not None and process.stdin is not None:
        stdin_thread = threading.Thread(
            target=write_stdin,
            args=(process.stdin, stdin),
            daemon=True,
        )
        stdin_thread.start()

    stdout_tail: deque[str] = deque(maxlen=max_buffered_lines)
    stderr_tail: deque[str] = deque(maxlen=max_buffered_lines)
    last_event: object | None = None
    invalid_line: str | None = None
    closed: set[str] = set()
    all_threads = tuple(
        thread for thread in (stdout_thread, stderr_thread, stdin_thread) if thread is not None
    )
    try:
        while len(closed) < 2 or process.poll() is None:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                kill_process_tree()
                try:
                    process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    pass
                raise PilotError(f"Délai dépassé après {timeout_seconds} secondes.")
            try:
                kind, line, oversized = messages.get(timeout=min(0.1, remaining))
            except queue.Empty:
                continue
            if line is None:
                closed.add(kind)
                continue
            if kind == "stderr":
                stderr_tail.append(
                    f"<ligne supérieure à {max_line_bytes} octets>" if oversized else line[:4000]
                )
                continue
            if oversized:
                invalid_line = invalid_line or f"ligne JSON supérieure à {max_line_bytes} octets"
                continue
            if not line.strip():
                continue
            stdout_tail.append(line[:4000])
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                invalid_line = invalid_line or line[:500]
                continue
            last_event = event
            if on_event is not None:
                on_event(event)

        returncode = process.wait()
    except BaseException:
        kill_process_tree()
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            pass
        cleanup_threads(*all_threads)
        raise
    cleanup_threads(*all_threads)
    result = CommandResult(
        tuple(argv),
        returncode,
        "\n".join(stdout_tail),
        "\n".join(stderr_tail),
        last_event=last_event,
    )
    if returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "aucun détail"
        raise PilotError(f"Commande en échec ({returncode}) : {detail}")
    if invalid_line is not None:
        raise PilotError(f"Flux JSON invalide : {invalid_line}")
    if last_event is None:
        raise PilotError("La commande a réussi sans produire le JSON attendu.")
    return result


def git(repo: Path, *args: str, timeout_seconds: int = 60) -> str:
    result = run_command(
        [resolve_binary("git"), *args],
        cwd=repo,
        timeout_seconds=timeout_seconds,
    )
    return result.stdout.strip()
