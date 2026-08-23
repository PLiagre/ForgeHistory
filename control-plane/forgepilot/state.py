from __future__ import annotations

from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import secrets
import socket
import threading
import time
from typing import Any

from .process import PilotError


STATE_SCHEMA_VERSION = 1
TERMINAL_STEPS = {"COMPLETE", "BLOCKED", "ERROR", "CANCELLED"}
LOCK_HEARTBEAT_SECONDS = 5.0
_FORBIDDEN_STATE_KEYS = re.compile(
    r"(?:prompt|authorization|api[_-]?key|access[_-]?token|secret|password)",
    re.IGNORECASE,
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _parse_time(value: str) -> datetime:
    try:
        return datetime.fromisoformat(value)
    except (TypeError, ValueError) as exc:
        raise PilotError(f"Horodatage d'état invalide : {value!r}") from exc


def sanitize_error(value: object) -> str:
    text = str(value)
    for name, secret_value in os.environ.items():
        if secret_value and re.search(r"(?:key|token|secret|password|authorization)", name, re.I):
            text = text.replace(secret_value, "<secret>")
    return text[:4000]


def _assert_secret_free(value: Any, path: str = "state") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if _FORBIDDEN_STATE_KEYS.search(str(key)):
                raise PilotError(f"Champ secret interdit dans l'état : {path}.{key}")
            _assert_secret_free(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _assert_secret_free(child, f"{path}[{index}]")
    elif isinstance(value, str):
        for name, secret_value in os.environ.items():
            if (
                len(secret_value) >= 8
                and re.search(r"(?:key|token|secret|password|authorization)", name, re.I)
                and secret_value in value
            ):
                raise PilotError(f"Valeur secrète interdite dans l'état : {path}")


def atomic_write_json(path: Path, payload: dict[str, object]) -> None:
    """Écrit un JSON remplaçable d'un seul coup, même après interruption.

    Le fichier temporaire réside dans le même répertoire : `os.replace` reste
    donc atomique sur le système de fichiers du run. L'ancien état n'est
    jamais supprimé avant que le nouveau contenu ait été synchronisé.
    """

    _assert_secret_free(payload)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp-{os.getpid()}-{secrets.token_hex(4)}")
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, indent=2, ensure_ascii=False, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        # La synchronisation du répertoire n'existe pas partout (Windows).
        if os.name != "nt":
            descriptor = os.open(path.parent, os.O_RDONLY)
            try:
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def _slug(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug[:48] or "task"


def new_run_id(task_name: str, now: datetime | None = None) -> str:
    instant = now or datetime.now(timezone.utc)
    stamp = instant.strftime("%Y%m%dT%H%M%S.%fZ")
    return f"{stamp}-{_slug(task_name)}-{secrets.token_hex(3)}"


def create_state(
    repo: Path,
    *,
    run_id: str,
    task: Path,
    task_name: str,
    base_ref: str,
    base_sha: str,
    requested_risk: str,
    derived_risk: str,
    effective_risk: str,
    policy_summary: dict[str, object],
    profile_summary: dict[str, object],
    metadata: dict[str, object] | None = None,
) -> tuple[Path, dict[str, object]]:
    run_dir = repo / ".forgepilot" / "runs" / run_id
    state_path = run_dir / "state.json"
    if run_dir.exists():
        raise PilotError(f"Identifiant de lot déjà utilisé : {run_id}")
    run_dir.mkdir(parents=True, exist_ok=False)
    created = utc_now()
    state: dict[str, object] = {
        "schema_version": STATE_SCHEMA_VERSION,
        "run_id": run_id,
        "task": str(task.resolve()),
        "task_name": task_name,
        "base_ref": base_ref,
        "base_sha": base_sha,
        "head_sha": None,
        "candidate": None,
        "risk": {
            "requested": requested_risk,
            "derived": derived_risk,
            "effective": effective_risk,
        },
        "step": "CREATED",
        "active_role": None,
        "created_at": created,
        "updated_at": created,
        "step_started_at": created,
        "durations_seconds": {},
        "total_duration_seconds": None,
        "step_history": [],
        "failures": {},
        "effective_models": profile_summary.get("roles", {}),
        "timeouts_seconds": profile_summary.get("timeouts", {}),
        "test_profile": profile_summary.get("test_profile"),
        "policy": {
            "path": policy_summary.get("path"),
            "version": policy_summary.get("version"),
            "sha256": policy_summary.get("sha256"),
        },
        "worktree": None,
        "branch": None,
        "pull_request": None,
        "proofs": [],
        "artifacts": {},
        "error": None,
        "iteration": {
            "count": 0,
            "plateau_count": 0,
            "last_finding_count": None,
            "last_findings": [],
        },
        "executor_session": None,
        "fusion": False,
    }
    if metadata:
        state.update(deepcopy(metadata))
    atomic_write_json(state_path, state)
    return state_path, state


def load_state(path: Path) -> dict[str, object]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise PilotError(f"État de lot introuvable : {path}") from exc
    except json.JSONDecodeError as exc:
        raise PilotError(f"État de lot JSON invalide : {path}: {exc}") from exc
    if not isinstance(payload, dict) or payload.get("schema_version") != STATE_SCHEMA_VERSION:
        raise PilotError(f"État de lot incompatible : {path}")
    if payload.get("run_id") != path.parent.name:
        raise PilotError("État incohérent : l'identifiant ne correspond pas à son répertoire.")
    _assert_secret_free(payload)
    return payload


def save_state(path: Path, state: dict[str, object]) -> dict[str, object]:
    updated = deepcopy(state)
    updated["updated_at"] = utc_now()
    atomic_write_json(path, updated)
    return updated


def transition(
    path: Path,
    state: dict[str, object],
    step: str,
    *,
    role: str | None = None,
    error: object | None = None,
    updates: dict[str, object] | None = None,
) -> dict[str, object]:
    changed = deepcopy(state)
    now = utc_now()
    previous = str(changed.get("step", "UNKNOWN"))
    started_at = str(changed.get("step_started_at", changed.get("updated_at", now)))
    duration = max(0.0, (_parse_time(now) - _parse_time(started_at)).total_seconds())
    history = changed.setdefault("step_history", [])
    if isinstance(history, list):
        history.append(
            {
                "step": previous,
                "started_at": started_at,
                "ended_at": now,
                "duration_seconds": round(duration, 6),
            }
        )
    durations = changed.setdefault("durations_seconds", {})
    if isinstance(durations, dict):
        durations[previous] = round(float(durations.get(previous, 0.0)) + duration, 6)
    changed["step"] = step
    changed["active_role"] = role
    changed["step_started_at"] = now
    changed["updated_at"] = now
    changed["error"] = sanitize_error(error) if error is not None else None
    failures = changed.get("failures")
    if (
        isinstance(failures, dict)
        and previous not in {"ERROR", "BLOCKED"}
        and step not in {"ERROR", "BLOCKED"}
        and step != previous
    ):
        record = failures.get(previous)
        if isinstance(record, dict) and record.get("consecutive", 0):
            record["consecutive"] = 0
    if step in TERMINAL_STEPS:
        created_at = str(changed.get("created_at", started_at))
        changed["total_duration_seconds"] = round(
            max(0.0, (_parse_time(now) - _parse_time(created_at)).total_seconds()),
            6,
        )
    else:
        changed["total_duration_seconds"] = None
    if updates:
        changed.update(deepcopy(updates))
    atomic_write_json(path, changed)
    return changed


def _pid_is_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False
    return True


def _lock_owner(run_id: str, kind: str) -> dict[str, object]:
    now = utc_now()
    return {
        "schema_version": 1,
        "kind": kind,
        "run_id": run_id,
        "hostname": socket.gethostname(),
        "process_id": os.getpid(),
        "created_at": now,
        "heartbeat_at": now,
    }


def _release_lock(lock_dir: Path) -> None:
    try:
        for child in lock_dir.iterdir():
            if child.is_file():
                child.unlink()
        lock_dir.rmdir()
    except FileNotFoundError:
        pass


def _try_reclaim_dead_local_lock(lock_dir: Path) -> bool:
    owner_path = lock_dir / "owner.json"
    try:
        owner = json.loads(owner_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError, OSError):
        return False
    if owner.get("hostname") != socket.gethostname():
        return False
    pid = owner.get("process_id")
    if not isinstance(pid, int) or _pid_is_alive(pid):
        return False
    tombstone = lock_dir.with_name(
        f".{lock_dir.name}.stale-{os.getpid()}-{secrets.token_hex(3)}"
    )
    try:
        os.replace(lock_dir, tombstone)
    except OSError:
        return False
    _release_lock(tombstone)
    return True


def _acquire_lock(lock_dir: Path, owner: dict[str, object]) -> None:
    lock_dir.parent.mkdir(parents=True, exist_ok=True)
    for _ in range(2):
        try:
            lock_dir.mkdir()
        except FileExistsError:
            if _try_reclaim_dead_local_lock(lock_dir):
                continue
            try:
                visible_owner = (lock_dir / "owner.json").read_text(encoding="utf-8").strip()
            except OSError:
                visible_owner = "propriétaire illisible"
            raise PilotError(
                f"Verrou ForgePilot déjà détenu : {lock_dir} ({visible_owner}). "
                "Un verrou distant, vivant ou ambigu n'est jamais récupéré automatiquement."
            )
        try:
            atomic_write_json(lock_dir / "owner.json", owner)
        except Exception:
            _release_lock(lock_dir)
            raise
        return
    raise PilotError(f"Impossible d'acquérir le verrou ForgePilot : {lock_dir}")


@contextmanager
def execution_locks(repo: Path, run_id: str):
    """Exclusivité globale des agents et verrou propre au run, cross-platform."""

    lock_dirs = (
        repo / ".forgepilot" / "locks" / "agents.lock",
        repo / ".forgepilot" / "runs" / run_id / "resume.lock",
    )
    owners = (
        _lock_owner(run_id, "global-agents"),
        _lock_owner(run_id, "run-resume"),
    )
    acquired: list[Path] = []
    stop = threading.Event()
    heartbeat: threading.Thread | None = None
    try:
        for lock_dir, owner in zip(lock_dirs, owners, strict=True):
            _acquire_lock(lock_dir, owner)
            acquired.append(lock_dir)

        def refresh() -> None:
            while not stop.wait(LOCK_HEARTBEAT_SECONDS):
                now = utc_now()
                for lock_dir, owner in zip(lock_dirs, owners, strict=True):
                    refreshed = dict(owner)
                    refreshed["heartbeat_at"] = now
                    try:
                        atomic_write_json(lock_dir / "owner.json", refreshed)
                    except OSError:
                        # La perte du verrou sera détectée par les contrôles
                        # d'identité avant toute publication ; ne jamais recréer
                        # silencieusement un répertoire disparu.
                        return

        heartbeat = threading.Thread(
            target=refresh,
            name=f"forgepilot-lock-{run_id}",
            daemon=True,
        )
        heartbeat.start()
        yield
    finally:
        stop.set()
        if heartbeat is not None:
            heartbeat.join(timeout=1)
        for lock_dir in reversed(acquired):
            _release_lock(lock_dir)


def run_state_path(repo: Path, run_id: str) -> Path:
    if run_id == "latest":
        runs = repo / ".forgepilot" / "runs"
        candidates = sorted(
            (path for path in runs.glob("*/state.json") if path.is_file()),
            key=lambda path: path.parent.name,
        ) if runs.exists() else []
        if not candidates:
            raise PilotError("Aucune exécution ForgePilot enregistrée.")
        return candidates[-1]
    if not re.fullmatch(r"[A-Za-z0-9._-]+", run_id):
        raise PilotError(f"Identifiant de lot invalide : {run_id!r}")
    return repo / ".forgepilot" / "runs" / run_id / "state.json"


def status_snapshot(state: dict[str, object]) -> dict[str, object]:
    snapshot = deepcopy(state)
    if snapshot.get("step") in TERMINAL_STEPS:
        snapshot["current_step_elapsed_seconds"] = 0.0
        return snapshot
    started = str(snapshot.get("step_started_at", snapshot.get("updated_at", utc_now())))
    snapshot["current_step_elapsed_seconds"] = round(
        max(0.0, (_parse_time(utc_now()) - _parse_time(started)).total_seconds()),
        6,
    )
    return snapshot
