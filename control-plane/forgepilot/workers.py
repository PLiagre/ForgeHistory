"""Constat des workers GitHub auto-hébergés (ADR-0020).

Lecture seule : ce module ne dispatch aucun workflow. Un runner offline
n'est jamais un succès.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
import re
from typing import Sequence

from .process import PilotError, resolve_binary, run_command


_REPO = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
PING_SCHEMA_VERSION = 1
_SHA_MIN_LENGTH = 7


@dataclass(frozen=True)
class Worker:
    name: str
    online: bool
    busy: bool
    labels: tuple[str, ...]
    os: str = ""

    def has_labels(self, required: Sequence[str]) -> bool:
        have = {label.lower() for label in self.labels}
        return all(item.lower() in have for item in required)

    def as_dict(self) -> dict[str, object]:
        return {
            "name": self.name,
            "online": self.online,
            "busy": self.busy,
            "labels": list(self.labels),
            "os": self.os,
        }


def parse_runners(payload: object) -> tuple[Worker, ...]:
    """Dérive la liste des runners. Un payload illisible refuse, il ne devine pas."""

    if not isinstance(payload, dict):
        raise PilotError("Liste des runners GitHub invalide.")
    raw = payload.get("runners")
    if not isinstance(raw, list):
        raise PilotError("Champ runners absent ou invalide.")
    workers: list[Worker] = []
    for item in raw:
        if not isinstance(item, dict):
            raise PilotError("Entrée runner invalide.")
        name = item.get("name")
        if not isinstance(name, str) or not name.strip():
            raise PilotError("Runner sans nom.")
        labels_raw = item.get("labels")
        if not isinstance(labels_raw, list):
            raise PilotError(f"Labels absents pour le runner {name.strip()!r}.")
        labels: list[str] = []
        for label in labels_raw:
            if isinstance(label, dict):
                label_name = label.get("name")
            else:
                label_name = label
            if not isinstance(label_name, str) or not label_name.strip():
                raise PilotError(f"Label illisible pour le runner {name.strip()!r}.")
            labels.append(label_name.strip())
        workers.append(
            Worker(
                name=name.strip(),
                online=str(item.get("status") or "") == "online",
                busy=bool(item.get("busy")),
                labels=tuple(labels),
                os=str(item.get("os") or ""),
            )
        )
    return tuple(workers)


def matching_online(
    workers: Sequence[Worker],
    required: Sequence[str] = (),
) -> tuple[Worker, ...]:
    return tuple(
        worker for worker in workers if worker.online and worker.has_labels(required)
    )


def workers_snapshot(
    payload: object,
    required: Sequence[str] = (),
) -> dict[str, object]:
    workers = parse_runners(payload)
    available = matching_online(workers, required)
    return {
        "required": list(required),
        "available": [worker.name for worker in available],
        "workers": [worker.as_dict() for worker in workers],
    }


def refuse_if_absent(snapshot: dict[str, object]) -> None:
    available = snapshot.get("available")
    if not isinstance(available, list) or not available:
        required = snapshot.get("required") or []
        if required:
            besoin = ", ".join(str(item) for item in required)
        else:
            besoin = "un runner self-hosted online"
        raise PilotError(f"Worker absent : aucun runner compatible ({besoin}).")


def validate_ping(payload: object) -> dict[str, object]:
    """Un artefact ping mal formé refuse. Un zéro n'est pas « non calculé »."""

    if not isinstance(payload, dict):
        raise PilotError("Artefact ping invalide.")
    if payload.get("schema_version") != PING_SCHEMA_VERSION:
        raise PilotError("schema_version de ping absent ou inattendu.")
    hostname = payload.get("hostname")
    if not isinstance(hostname, str) or not hostname.strip():
        raise PilotError("hostname de ping absent.")
    sha = payload.get("sha")
    if not isinstance(sha, str) or len(sha.strip()) < _SHA_MIN_LENGTH:
        raise PilotError("sha de ping absent ou trop court.")
    capabilities = payload.get("capabilities")
    if not isinstance(capabilities, list) or not capabilities:
        raise PilotError("capabilities de ping absentes.")
    for item in capabilities:
        if not isinstance(item, str) or not item.strip():
            raise PilotError("capability de ping illisible.")
    return payload


def format_workers(snapshot: dict[str, object]) -> str:
    rows = snapshot.get("workers")
    if not isinstance(rows, list) or not rows:
        return "(aucun runner enregistré)"
    lines: list[str] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        name = str(row.get("name") or "?")
        state = "online" if row.get("online") else "offline"
        if row.get("busy"):
            state += "+busy"
        labels = row.get("labels")
        label_text = ", ".join(str(item) for item in labels) if isinstance(labels, list) else ""
        lines.append(f"{name}\t{state}\t{label_text}".rstrip())
    return "\n".join(lines) if lines else "(aucun runner enregistré)"


def fetch_runner_payload(repo_dir, repository: str):
    if not _REPO.match(repository):
        raise PilotError(f"Identifiant de dépôt GitHub invalide : {repository!r}")
    resolve_binary("gh")
    result = run_command(
        ["gh", "api", f"repos/{repository}/actions/runners"],
        cwd=repo_dir,
        timeout_seconds=60,
    )
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise PilotError("Liste des runners GitHub illisible.") from exc
