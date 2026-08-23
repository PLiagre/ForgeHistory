from __future__ import annotations

import json
from pathlib import Path

from .process import PilotError, git, resolve_binary, run_command
from .review import validate_verdict_material
from .state import load_state, run_state_path


STOP_LABELS = frozenset({"do-not-merge", "no-merge", "blocked", "owner-review"})


def _material_path(state: dict[str, object], state_path: Path) -> Path:
    artifacts = state.get("artifacts")
    material_value = artifacts.get("review_material") if isinstance(artifacts, dict) else None
    if not isinstance(material_value, str):
        raise PilotError("Aucun matériau de revue archivé ; fusion refusée.")
    material_path = Path(material_value)
    if not material_path.is_absolute():
        material_path = state_path.parent / material_path
    return material_path


def _pr_snapshot(repo: Path, pull_request: str) -> dict[str, object]:
    resolve_binary("gh")
    result = run_command(
        [
            "gh",
            "pr",
            "view",
            pull_request,
            "--json",
            "url,headRefOid,labels,statusCheckRollup,reviewDecision,state",
        ],
        cwd=repo,
        timeout_seconds=120,
    )
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise PilotError("État GitHub de la PR illisible.") from exc
    if not isinstance(payload, dict):
        raise PilotError("État GitHub de la PR invalide.")
    return payload


def _labels(snapshot: dict[str, object]) -> set[str]:
    raw = snapshot.get("labels")
    names: set[str] = set()
    if isinstance(raw, list):
        for item in raw:
            if isinstance(item, dict) and isinstance(item.get("name"), str):
                names.add(item["name"].lower())
            elif isinstance(item, str):
                names.add(item.lower())
    return names


def _failing_checks(snapshot: dict[str, object]) -> list[str]:
    rollup = snapshot.get("statusCheckRollup")
    failing: list[str] = []
    if not isinstance(rollup, list):
        raise PilotError("Checks GitHub absents ; fusion refusée.")
    if not rollup:
        raise PilotError("Aucun check GitHub ; fusion refusée.")
    for item in rollup:
        if not isinstance(item, dict):
            continue
        name = str(item.get("name") or item.get("context") or "check")
        conclusion = str(item.get("conclusion") or "").upper()
        status = str(item.get("status") or "").upper()
        if status and status not in {"COMPLETED", "SUCCESS"}:
            failing.append(f"{name}:{status}")
            continue
        if conclusion and conclusion not in {"SUCCESS", "NEUTRAL", "SKIPPED"}:
            failing.append(f"{name}:{conclusion}")
    return failing


def assert_merge_ready(
    repo: Path,
    state: dict[str, object],
    state_path: Path,
) -> dict[str, object]:
    if state.get("step") != "COMPLETE":
        raise PilotError(
            f"Fusion refusée : état {state.get('step')!r} ; le juge n'a pas clos le lot."
        )
    material = validate_verdict_material(repo, state, _material_path(state, state_path))
    if material.get("verdict") != "PASS":
        raise PilotError(
            f"Fusion refusée : juge {material.get('verdict')!r}, PASS exigé."
        )
    pull_request = state.get("pull_request")
    if not isinstance(pull_request, str) or not pull_request:
        raise PilotError("Fusion refusée : PR absente.")
    worktree_value = state.get("worktree")
    cwd = Path(worktree_value) if isinstance(worktree_value, str) and worktree_value else repo
    snapshot = _pr_snapshot(cwd, pull_request)
    if snapshot.get("state") != "OPEN":
        raise PilotError(f"Fusion refusée : PR {snapshot.get('state')!r}.")
    labels = _labels(snapshot)
    blocked = labels & STOP_LABELS
    if blocked:
        raise PilotError("Fusion refusée : label d'arrêt " + ", ".join(sorted(blocked)))
    head_sha = state.get("head_sha")
    if snapshot.get("headRefOid") != head_sha:
        raise PilotError(
            "Fusion refusée : le SHA GitHub n'est plus celui jugé "
            f"({snapshot.get('headRefOid')} ≠ {head_sha})."
        )
    failing = _failing_checks(snapshot)
    if failing:
        raise PilotError("Fusion refusée : checks non verts : " + ", ".join(failing))
    return {
        "pull_request": pull_request,
        "head_sha": head_sha,
        "verdict": "PASS",
        "worktree": str(cwd),
    }


def merge_pull_request(repo: Path, pull_request: str) -> str:
    resolve_binary("gh")
    result = run_command(
        ["gh", "pr", "merge", pull_request, "--merge"],
        cwd=repo,
        timeout_seconds=180,
    )
    return result.stdout.strip() or pull_request


def merge_run(repo: Path, run_id: str, *, apply: bool) -> dict[str, object]:
    state_path = run_state_path(repo, run_id)
    state = load_state(state_path)
    ready = assert_merge_ready(repo, state, state_path)
    payload = {
        "run_id": state.get("run_id"),
        "ready": True,
        "merged": False,
        **ready,
    }
    if not apply:
        return payload
    merge_pull_request(Path(str(ready["worktree"])), str(ready["pull_request"]))
    payload["merged"] = True
    return payload
