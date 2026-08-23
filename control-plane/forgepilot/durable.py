from __future__ import annotations

from copy import deepcopy
from dataclasses import asdict
import hashlib
import importlib.util
import json
from pathlib import Path
import re
from typing import Iterable

from .config import Settings
from .policy import RISK_LEVELS, effective_risk
from .process import PilotError, git, resolve_binary, run_command
from .protocol import (
    extract_session_id,
    findings_signatures,
    validate_executor,
    validate_plan,
    validate_review,
    write_normalized_json,
)
from .publication import changed_paths, enforce_allowed_paths, stage_explicit_paths, working_tree_paths
from .review import archive_review_material, build_review_bundle, write_feedback
from .state import (
    create_state,
    execution_locks,
    load_state,
    new_run_id,
    run_state_path,
    sanitize_error,
    save_state,
    transition,
)
from .workflow import (
    assert_not_api_billing,
    assert_task_is_instruction,
    create_worktree,
    execute_invocation,
    executor_invocation,
    existing_worktree,
    missing_binaries,
    plan_invocation,
    review_invocation,
)


def declared_risk(task: Path) -> str | None:
    try:
        text = task.read_text(encoding="utf-8")
    except FileNotFoundError:
        return None
    match = re.search(
        r"(?im)^\s*(?:\*\*)?risque(?:\*\*)?\s*:\s*(?:\*\*)?(R[012])",
        text,
    )
    if not match:
        match = re.search(r"(?im)^\s*risque\s*:\s*(R[012])", text)
    return match.group(1) if match else None


def _profile_summary(settings: Settings, risk: str) -> dict[str, object]:
    if settings.policy is None:
        raise PilotError("Politique de workflow absente ; exécution durable refusée.")
    profile = settings.policy.profile(risk)
    return {
        "test_profile": profile.test_profile,
        "timeouts": asdict(profile.timeouts),
        "roles": {name: asdict(role) for name, role in profile.roles.items()},
    }


def register_run(
    settings: Settings,
    repo: Path,
    task: Path,
    task_name: str,
    *,
    requested_risk: str | None = None,
    changed_paths: Iterable[str] = (),
    base_ref: str | None = None,
    base_branch: str | None = None,
    title: str | None = None,
    allow_heavy: bool = False,
) -> tuple[Path, dict[str, object]]:
    assert_not_api_billing()
    assert_task_is_instruction(task)
    if not task.is_file() or not task.read_text(encoding="utf-8").strip():
        raise PilotError(f"Tâche introuvable ou vide : {task}")
    if settings.policy is None:
        raise PilotError("Politique de workflow absente ; start est refusé.")
    requested = requested_risk or declared_risk(task) or "R1"
    if requested not in RISK_LEVELS:
        raise PilotError(f"Risque demandé invalide : {requested!r}")
    path_hints = tuple(changed_paths)
    effective, derived = effective_risk(settings.policy, requested, path_hints)
    if effective == "R0":
        raise PilotError(
            "Lot R0 refusé avant création : aucun agent n'est requis. "
            "Exécuter directement le profil mécanique fast du routeur."
        )
    selected_base = base_ref or settings.default_base_ref
    base_sha = git(repo, "rev-parse", selected_base)
    runs_root = repo / ".forgepilot" / "runs"
    if runs_root.exists():
        for existing_path in runs_root.glob("*/state.json"):
            existing = load_state(existing_path)
            if (
                existing.get("task_name") == task_name
                and existing.get("step") not in {"COMPLETE", "BLOCKED", "ERROR", "CANCELLED"}
            ):
                raise PilotError(
                    f"Un lot actif porte déjà le nom {task_name!r} : {existing.get('run_id')}."
                )
    run_id = new_run_id(task_name)
    profile_summary = _profile_summary(settings, effective)
    state_path, state = create_state(
        repo,
        run_id=run_id,
        task=task,
        task_name=task_name,
        base_ref=selected_base,
        base_sha=base_sha,
        requested_risk=requested,
        derived_risk=derived,
        effective_risk=effective,
        policy_summary=settings.policy.summary(),
        profile_summary=profile_summary,
        metadata={
            "base_branch": base_branch or settings.default_base_branch,
            "title": (title or task_name).strip() or task_name,
            "changed_path_hints": list(path_hints),
            "review_base_sha": base_sha,
            "allow_heavy": bool(allow_heavy),
            "task_sha256": hashlib.sha256(task.read_bytes()).hexdigest(),
        },
    )
    return state_path, state


def _state_risk(state: dict[str, object]) -> str:
    risk = state.get("risk")
    if not isinstance(risk, dict) or risk.get("effective") not in RISK_LEVELS:
        raise PilotError("État incohérent : risque effectif absent.")
    return str(risk["effective"])


def _artifact_path(state_path: Path, state: dict[str, object], name: str) -> Path | None:
    artifacts = state.get("artifacts")
    value = artifacts.get(name) if isinstance(artifacts, dict) else None
    if not isinstance(value, str):
        return None
    path = Path(value)
    if not path.is_absolute():
        path = state_path.parent / path
    return path


def _set_artifact(
    state_path: Path,
    state: dict[str, object],
    name: str,
    path: Path,
) -> dict[str, object]:
    changed = deepcopy(state)
    artifacts = changed.setdefault("artifacts", {})
    if not isinstance(artifacts, dict):
        raise PilotError("État incohérent : artifacts n'est pas un objet.")
    try:
        stored = str(path.relative_to(state_path.parent))
    except ValueError:
        stored = str(path)
    artifacts[name] = stored
    return save_state(state_path, changed)


def _ensure_worktree(repo: Path, state: dict[str, object]) -> tuple[Path, str]:
    task_name = str(state["task_name"])
    slug = re.sub(r"[^a-z0-9]+", "-", task_name.lower()).strip("-")[:48] or "task"
    expected_path = repo / ".forgepilot" / "worktrees" / slug
    expected_branch = f"agent/{expected_path.name}"
    if expected_path.exists():
        worktree, branch, status = existing_worktree(repo, task_name)
        if status or git(worktree, "rev-parse", "HEAD") != state.get("base_sha"):
            raise PilotError(
                "Worktree homonyme déjà utilisé par un autre lot ; reprise refusée."
            )
        return worktree, branch
    branch_exists = bool(
        git(repo, "branch", "--list", expected_branch)
    )
    if branch_exists:
        if git(repo, "rev-parse", expected_branch) != state.get("base_sha"):
            raise PilotError(
                "Branche homonyme déjà utilisée par un autre lot ; reprise refusée."
            )
        expected_path.parent.mkdir(parents=True, exist_ok=True)
        git(repo, "worktree", "add", str(expected_path), expected_branch)
        return expected_path, expected_branch
    return create_worktree(repo, task_name, str(state["base_sha"]))


def _state_worktree(repo: Path, state: dict[str, object]) -> Path:
    value = state.get("worktree")
    if not isinstance(value, str) or not value:
        worktree, _ = _ensure_worktree(repo, state)
        return worktree
    worktree = Path(value)
    if not worktree.exists():
        raise PilotError(f"Reprise incohérente : worktree absent {worktree}.")
    branch = git(worktree, "branch", "--show-current")
    if branch != state.get("branch"):
        raise PilotError(
            f"Reprise incohérente : branche {branch!r}, état {state.get('branch')!r}."
        )
    return worktree


def _executor_effect_is_ambiguous(worktree: Path, expected_head: object) -> bool:
    """Vrai si Cursor a pu écrire sans que son résultat final soit archivé."""

    if not isinstance(expected_head, str) or not expected_head:
        raise PilotError("État incohérent : SHA attendu absent avant Cursor.")
    return bool(working_tree_paths(worktree)) or git(worktree, "rev-parse", "HEAD") != expected_head


def _role_timeout(settings: Settings, risk: str, role: str) -> int:
    assert settings.policy is not None
    return settings.policy.profile(risk).timeouts.for_role(role)


def _raise_risk_from_plan(
    settings: Settings,
    state_path: Path,
    state: dict[str, object],
    plan: dict[str, object],
) -> dict[str, object]:
    assert settings.policy is not None
    current = _state_risk(state)
    planned = plan.get("files_allowed_to_change", [])
    if not isinstance(planned, list):
        raise PilotError("Plan invalide : périmètre absent pour classifier le risque.")
    return _raise_risk_from_paths(
        settings,
        state_path,
        state,
        planned,
        source="plan",
    )


def _raise_risk_from_paths(
    settings: Settings,
    state_path: Path,
    state: dict[str, object],
    paths: Iterable[str],
    *,
    source: str,
) -> dict[str, object]:
    assert settings.policy is not None
    current = _state_risk(state)
    classified_paths = sorted(set(paths))
    elevated, derived = effective_risk(settings.policy, current, classified_paths)
    if RISK_LEVELS.index(elevated) < RISK_LEVELS.index(current):
        raise PilotError("Invariant violé : le classement mécanique a abaissé le risque.")
    changed = deepcopy(state)
    risk = changed.get("risk")
    if not isinstance(risk, dict):
        raise PilotError("État incohérent : risque absent.")
    risk[f"derived_after_{source}"] = derived
    risk["last_classified_paths"] = classified_paths
    if elevated == current:
        return save_state(state_path, changed)
    risk["effective"] = elevated
    profile = _profile_summary(settings, elevated)
    changed["effective_models"] = profile["roles"]
    changed["timeouts_seconds"] = profile["timeouts"]
    changed["test_profile"] = profile["test_profile"]
    return save_state(state_path, changed)


def _run_agent(
    invocation: object,
    settings: Settings,
    *,
    risk: str,
    role: str,
) -> object:
    # L'événement est volontairement éphémère : l'état durable reçoit les
    # transitions et le résultat normalisé, jamais le prompt ni le flux brut.
    return execute_invocation(
        invocation,  # type: ignore[arg-type]
        settings,
        timeout_seconds=_role_timeout(settings, risk, role),
        stream=True,
    )


def _query_existing_pr(worktree: Path) -> str | None:
    branch = git(worktree, "branch", "--show-current")
    try:
        result = run_command(
            ["gh", "pr", "view", branch, "--json", "url", "--jq", ".url"],
            cwd=worktree,
            timeout_seconds=60,
        )
    except PilotError:
        return None
    return result.stdout.strip() or None


def _candidate_identity(worktree: Path) -> dict[str, str]:
    return {
        "head_sha": git(worktree, "rev-parse", "HEAD"),
        "tree_sha": git(worktree, "rev-parse", "HEAD^{tree}"),
        "index_tree_sha": git(worktree, "write-tree"),
    }


def _assert_candidate_identity(
    worktree: Path,
    candidate: dict[str, object],
) -> dict[str, str]:
    expected_head = candidate.get("head_sha")
    expected_tree = candidate.get("tree_sha")
    if not isinstance(expected_head, str) or not isinstance(expected_tree, str):
        raise PilotError("Candidat durable invalide : SHA ou tree absent.")
    actual = _candidate_identity(worktree)
    dirty = working_tree_paths(worktree)
    if (
        dirty
        or actual["head_sha"] != expected_head
        or actual["tree_sha"] != expected_tree
        or actual["index_tree_sha"] != expected_tree
    ):
        detail = ", ".join(dirty) if dirty else "HEAD/index/tree modifié"
        raise PilotError(
            "Preuve périmée : le candidat Git a changé depuis son identité archivée "
            f"({detail})."
        )
    return actual


def _prepare_candidate(
    worktree: Path,
    state: dict[str, object],
    allowed: list[str],
    *,
    update_only: bool,
) -> dict[str, object]:
    comparison_sha = state.get("head_sha") if update_only else state.get("base_sha")
    if not isinstance(comparison_sha, str) or not comparison_sha:
        raise PilotError("État incohérent : SHA de comparaison absent avant publication.")
    current_head = git(worktree, "rev-parse", "HEAD")
    committed_paths = (
        [] if current_head == comparison_sha else changed_paths(worktree, comparison_sha)
    )
    if committed_paths:
        # Cursor n'est pas censé committer lui-même, mais un commit existant ne
        # doit jamais contourner le périmètre simplement parce que le worktree
        # paraît propre au moment où ForgePilot reprend la main.
        enforce_allowed_paths(committed_paths, allowed)
    paths = working_tree_paths(worktree)
    if paths:
        selected = enforce_allowed_paths(paths, allowed)
        git(worktree, "diff", "--check")
        stage_explicit_paths(worktree, selected)
        git(worktree, "diff", "--cached", "--check")
        suffix = "" if not update_only else f" (itération {state['iteration']['count']})"  # type: ignore[index]
        git(worktree, "commit", "-m", f"{state['title']}{suffix}")
    elif current_head == comparison_sha:
        message = (
            "Itération interrompue sans correction récupérable."
            if update_only
            else "Publication interrompue sans commit ni changement récupérable."
        )
        raise PilotError(message)

    identity = _candidate_identity(worktree)
    if working_tree_paths(worktree) or identity["index_tree_sha"] != identity["tree_sha"]:
        raise PilotError("Candidat local incohérent : index ou worktree non propre après commit.")
    final_paths = enforce_allowed_paths(
        changed_paths(worktree, comparison_sha),
        allowed,
    )
    if not final_paths:
        raise PilotError("Candidat local vide après commit.")
    return {
        "base_sha": comparison_sha,
        "head_sha": identity["head_sha"],
        "tree_sha": identity["tree_sha"],
        "paths": final_paths,
        "iteration": (
            int(state.get("iteration", {}).get("count", 0))
            if isinstance(state.get("iteration"), dict)
            else 0
        ),
    }


def _push_candidate_and_pr(
    worktree: Path,
    state: dict[str, object],
    candidate: dict[str, object],
    *,
    update_only: bool,
) -> str | None:
    resolve_binary("gh")
    _assert_candidate_identity(worktree, candidate)
    branch = git(worktree, "branch", "--show-current")
    git(worktree, "push", "-u", "origin", branch, timeout_seconds=300)
    pull_request = state.get("pull_request") if isinstance(state.get("pull_request"), str) else None
    if update_only:
        if not pull_request:
            pull_request = _query_existing_pr(worktree)
        if not pull_request:
            raise PilotError("PR existante introuvable pendant l'itération.")
    elif not pull_request:
        pull_request = _query_existing_pr(worktree)
        if not pull_request:
            branch = git(worktree, "branch", "--show-current")
            effective_risk = _state_risk(state)
            body = (
                "Produit par Cursor dans ForgePilot. "
                "Fusion mécanique si juge PASS et checks verts (ADR-0017).\n\n"
                f"Forge-Risk: {effective_risk}\n"
                f"Forge-Brief: {state['task_name']}"
            )
            argv = (
                "gh",
                "pr",
                "create",
                "--draft",
                "--base",
                str(state["base_branch"]),
                "--head",
                branch,
                "--title",
                str(state["title"]),
                "--body",
                body,
            )
            result = run_command(argv, cwd=worktree, timeout_seconds=120)
            pull_request = result.stdout.strip()
    _assert_candidate_identity(worktree, candidate)
    return pull_request


def _commit_push_and_pr(
    worktree: Path,
    state: dict[str, object],
    allowed: list[str],
    *,
    update_only: bool,
) -> tuple[str, str | None]:
    """Compatibilité interne ; le chemin durable sépare désormais preuve et push."""

    candidate = _prepare_candidate(worktree, state, allowed, update_only=update_only)
    pull_request = _push_candidate_and_pr(
        worktree,
        state,
        candidate,
        update_only=update_only,
    )
    return str(candidate["head_sha"]), pull_request


def run_test_profile(
    worktree: Path,
    *,
    paths: list[str],
    profile: str,
    output_path: Path,
    risk: str | None = None,
    base_sha: str | None = None,
    head_sha: str | None = None,
    allow_heavy: bool = False,
) -> dict[str, object]:
    router = worktree / "harness" / "workflow_test_router.py"
    if not router.is_file():
        raise PilotError(f"Routeur de tests ciblés introuvable : {router}")
    spec = importlib.util.spec_from_file_location("forgepilot_workflow_test_router", router)
    if spec is None or spec.loader is None:
        raise PilotError(f"Routeur de tests impossible à charger : {router}")
    try:
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        plan = module.build_plan(
            worktree,
            paths,
            profile,
            risk=risk,
            policy_path=worktree / "control-plane" / "workflow-policy.toml",
            base_sha=base_sha,
            head_sha=head_sha,
        )
        summary = module.run_plan(plan, worktree, allow_heavy=allow_heavy)
    except Exception as exc:
        raise PilotError(f"Routeur de tests {profile} en refus : {exc}") from exc
    if not isinstance(summary, dict):
        raise PilotError("Le routeur de tests n'a pas produit de résumé JSON.")
    write_normalized_json(output_path, summary)
    code = summary.get("returncode", summary.get("code", 1))
    if code != 0:
        raise PilotError(f"Tests ciblés en échec (code {code}).")
    return summary


def run_targeted_tests(
    worktree: Path,
    *,
    paths: list[str],
    output_path: Path,
) -> dict[str, object]:
    """Façade conservée pour les appels/tests existants de la boucle fast."""
    return run_test_profile(
        worktree,
        paths=paths,
        profile="fast",
        output_path=output_path,
    )


def _append_test_proof(
    state_path: Path,
    state: dict[str, object],
    *,
    profile: str,
    summary: dict[str, object],
    candidate: dict[str, object],
    tested_base_sha: str,
    paths: list[str],
    iteration: int | None = None,
) -> dict[str, object]:
    changed = deepcopy(state)
    proofs = changed.setdefault("proofs", [])
    if not isinstance(proofs, list):
        raise PilotError("État incohérent : proofs n'est pas une liste.")
    marker = {
        "kind": "test-profile",
        "profile": profile,
        "base_sha": tested_base_sha,
        "head_sha": candidate["head_sha"],
        "tree_sha": candidate["tree_sha"],
        "iteration": iteration,
    }
    if not any(
        isinstance(item, dict)
        and all(item.get(key) == value for key, value in marker.items())
        for item in proofs
    ):
        proofs.append({**marker, "paths": paths, "result": summary})
    return save_state(state_path, changed)


def _candidate_paths(
    worktree: Path,
    state: dict[str, object],
    allowed: list[str],
    *,
    update_only: bool,
) -> list[str]:
    """Chemins du candidat, y compris les commits éventuellement faits par Cursor."""

    comparison_sha = state.get("head_sha") if update_only else state.get("base_sha")
    if not isinstance(comparison_sha, str) or not comparison_sha:
        raise PilotError("État incohérent : SHA de comparaison absent pour les tests.")
    paths = set(working_tree_paths(worktree))
    if git(worktree, "rev-parse", "HEAD") != comparison_sha:
        paths.update(changed_paths(worktree, comparison_sha))
    return enforce_allowed_paths(paths, allowed)


def _state_candidate(state: dict[str, object]) -> dict[str, object]:
    candidate = state.get("candidate")
    if not isinstance(candidate, dict):
        raise PilotError("État incohérent : candidat Git exact absent.")
    for key in ("base_sha", "head_sha", "tree_sha", "paths"):
        if key not in candidate:
            raise PilotError(f"État incohérent : candidat sans {key}.")
    if not isinstance(candidate["paths"], list):
        raise PilotError("État incohérent : chemins du candidat invalides.")
    return candidate


def _require_exact_proof(
    state: dict[str, object],
    profile: str,
    candidate: dict[str, object],
) -> dict[str, object]:
    proofs = state.get("proofs")
    if isinstance(proofs, list):
        for proof in proofs:
            if (
                isinstance(proof, dict)
                and proof.get("kind") == "test-profile"
                and proof.get("profile") == profile
                and proof.get("head_sha") == candidate.get("head_sha")
                and proof.get("tree_sha") == candidate.get("tree_sha")
                and isinstance(proof.get("result"), dict)
                and proof["result"].get("returncode", proof["result"].get("code", 1)) == 0
            ):
                return proof
    raise PilotError(
        f"Publication refusée : preuve {profile} exacte absente pour le candidat SHA/tree."
    )


def _run_exact_test_profile(
    state_path: Path,
    state: dict[str, object],
    worktree: Path,
    *,
    profile: str,
    paths: list[str],
    tested_base_sha: str,
    iteration: int | None,
    allow_heavy: bool = False,
) -> dict[str, object]:
    candidate = _state_candidate(state)
    _assert_candidate_identity(worktree, candidate)
    head_sha = str(candidate["head_sha"])
    tree_sha = str(candidate["tree_sha"])
    normalized_paths = sorted(set(paths))
    identity = {
        "base_sha": tested_base_sha,
        "head_sha": head_sha,
        "tree_sha": tree_sha,
        "paths": normalized_paths,
    }
    output_path = state_path.parent / (
        f"{profile}-{head_sha[:16]}-{tree_sha[:16]}.json"
    )
    summary: dict[str, object] | None = None
    if output_path.is_file():
        cached = json.loads(output_path.read_text(encoding="utf-8"))
        if (
            isinstance(cached, dict)
            and cached.get("schema_version") == 1
            and cached.get("profile") == profile
            and cached.get("candidate") == identity
            and isinstance(cached.get("result"), dict)
        ):
            cached_result = cached["result"]
            code = cached_result.get("returncode", cached_result.get("code", 1))
            if code == 0:
                summary = cached_result
    if summary is None:
        summary = run_test_profile(
            worktree,
            paths=normalized_paths,
            profile=profile,
            output_path=output_path,
            risk=_state_risk(state),
            base_sha=tested_base_sha,
            head_sha=head_sha,
            allow_heavy=allow_heavy,
        )
        _assert_candidate_identity(worktree, candidate)
        write_normalized_json(
            output_path,
            {
                "schema_version": 1,
                "profile": profile,
                "candidate": identity,
                "result": summary,
            },
        )
    _assert_candidate_identity(worktree, candidate)
    return _append_test_proof(
        state_path,
        state,
        profile=profile,
        summary=summary,
        candidate=candidate,
        tested_base_sha=tested_base_sha,
        paths=normalized_paths,
        iteration=iteration,
    )


def _run_pr_tests(
    settings: Settings,
    state_path: Path,
    state: dict[str, object],
    worktree: Path,
    plan: dict[str, object],
    *,
    iteration: int | None,
) -> dict[str, object]:
    del settings, plan
    candidate = _state_candidate(state)
    return _run_exact_test_profile(
        state_path,
        state,
        worktree,
        profile="pr",
        paths=list(candidate["paths"]),  # type: ignore[arg-type]
        tested_base_sha=str(candidate["base_sha"]),
        iteration=iteration,
    )


def _certify_if_required(
    settings: Settings,
    state_path: Path,
    state: dict[str, object],
    worktree: Path,
) -> dict[str, object]:
    assert settings.policy is not None
    risk = _state_risk(state)
    if settings.policy.profile(risk).test_profile != "certify":
        return state
    candidate = _state_candidate(state)
    _assert_candidate_identity(worktree, candidate)
    head_sha = str(candidate["head_sha"])
    tree_sha = str(candidate["tree_sha"])
    proofs = state.get("proofs", [])
    if isinstance(proofs, list) and any(
        isinstance(item, dict)
        and item.get("kind") == "test-profile"
        and item.get("profile") == "certify"
        and item.get("head_sha") == head_sha
        and item.get("tree_sha") == tree_sha
        and isinstance(item.get("result"), dict)
        and item["result"].get("returncode", item["result"].get("code", 1)) == 0
        for item in proofs
    ):
        return state
    state = transition(state_path, state, "CERTIFYING", role=None)
    paths = changed_paths(worktree, str(state["base_sha"]))
    state = _run_exact_test_profile(
        state_path,
        state,
        worktree,
        profile="certify",
        paths=paths,
        tested_base_sha=str(state["base_sha"]),
        iteration=None,
        allow_heavy=bool(state.get("allow_heavy", False)),
    )
    return transition(state_path, state, "CERTIFIED", role=None)


def _review_current_head(
    settings: Settings,
    repo: Path,
    state_path: Path,
    state: dict[str, object],
    plan: dict[str, object],
) -> dict[str, object]:
    assert settings.policy is not None
    risk = _state_risk(state)
    worktree = _state_worktree(repo, state)
    candidate = _state_candidate(state)
    _assert_candidate_identity(worktree, candidate)
    head_sha = str(candidate["head_sha"])
    tree_sha = str(candidate["tree_sha"])
    expected = state.get("head_sha")
    if expected and expected != head_sha:
        raise PilotError(f"Preuve périmée : l'état vise {expected}, HEAD vaut {head_sha}.")
    review_base = str(state.get("review_base_sha") or state["base_sha"])
    proofs = state.get("proofs", [])
    mechanical = (
        [
            proof
            for proof in proofs
            if isinstance(proof, dict)
            and proof.get("head_sha") == head_sha
            and proof.get("tree_sha") == tree_sha
        ]
        if isinstance(proofs, list)
        else []
    )
    review_context: dict[str, object] = {
        "mode": state.get("review_mode", "full"),
    }
    feedback_path = _artifact_path(state_path, state, "feedback")
    if feedback_path is not None and feedback_path.is_file():
        feedback_payload = json.loads(feedback_path.read_text(encoding="utf-8"))
        if feedback_payload.get("head_sha_reviewed") == state.get("last_reviewed_head"):
            review_context["prior_feedback"] = feedback_payload
    bundle = build_review_bundle(
        worktree,
        base_sha=review_base,
        head_sha=head_sha,
        plan=plan,
        policy=settings.policy,
        mechanical_results=[item for item in mechanical if isinstance(item, dict)],
        review_context=review_context,
    )
    bundle_path = write_normalized_json(state_path.parent / f"review-bundle-{head_sha}.json", bundle)
    state = _set_artifact(state_path, state, "review_bundle", bundle_path)
    state = transition(state_path, state, "REVIEWING", role="reviewer")
    output_path = state_path.parent / f"review-output-{head_sha}.json"
    if output_path.is_file():
        review = validate_review(
            json.loads(output_path.read_text(encoding="utf-8")),
            expected_criteria=plan["acceptance_criteria"],  # type: ignore[arg-type]
        )
    else:
        invocation = review_invocation(
            settings,
            worktree,
            _artifact_path(state_path, state, "plan") or bundle_path,
            review_base,
            risk=risk,
            bundle_path=bundle_path,
        )
        result = _run_agent(invocation, settings, risk=risk, role="reviewer")
        review = validate_review(
            result,
            expected_criteria=plan["acceptance_criteria"],  # type: ignore[arg-type]
            forbidden_prompt=invocation.prompt,
        )
        write_normalized_json(
            output_path,
            review,
            forbidden_texts=(invocation.prompt or "",),
        )
    material_path = archive_review_material(
        state_path.parent,
        base_sha=review_base,
        head_sha=head_sha,
        tree_sha=tree_sha,
        review=review,
        bundle_path=bundle_path,
    )
    state = _set_artifact(state_path, state, "review", output_path)
    state = _set_artifact(state_path, state, "review_material", material_path)

    signatures = findings_signatures(review.get("findings", []))
    iteration = deepcopy(state.get("iteration", {}))
    if not isinstance(iteration, dict):
        iteration = {}
    previous = iteration.get("last_findings", [])
    correction_count = int(iteration.get("count", 0))
    plateau = int(iteration.get("plateau_count", 0))
    if correction_count > 0 and review["verdict"] == "FAIL":
        improved = bool(previous) and set(signatures) < set(previous)
        plateau = 0 if improved else plateau + 1
    iteration.update(
        {
            "plateau_count": plateau,
            "last_finding_count": len(signatures),
            "last_findings": signatures,
        }
    )
    state["iteration"] = iteration
    state["last_reviewed_head"] = head_sha
    state = save_state(state_path, state)

    if review["verdict"] == "PASS":
        state = _certify_if_required(settings, state_path, state, worktree)
        material = _artifact_path(state_path, state, "review_material")
        if material is not None and material.is_file():
            material_payload = json.loads(material.read_text(encoding="utf-8"))
            proofs = state.get("proofs", [])
            if isinstance(proofs, list):
                material_payload["post_review_proofs"] = [
                    proof
                    for proof in proofs
                    if isinstance(proof, dict)
                    and proof.get("profile") == "certify"
                    and proof.get("head_sha") == head_sha
                    and proof.get("tree_sha") == tree_sha
                ]
                write_normalized_json(material, material_payload)
        return transition(
            state_path,
            state,
            "COMPLETE",
            updates={"active_role": None, "error": None},
        )
    if review["verdict"] == "BLOCKED":
        return transition(
            state_path,
            state,
            "BLOCKED",
            error="Le reviewer a déclaré le lot BLOCKED.",
        )
    if plateau >= 2:
        return transition(
            state_path,
            state,
            "BLOCKED",
            error="Deux itérations sans amélioration ; arrêt honnête du lot.",
        )
    feedback_path = write_feedback(
        state_path.parent,
        head_sha=head_sha,
        review=review,
        iteration=correction_count + 1,
    )
    state = _set_artifact(state_path, state, "feedback", feedback_path)
    return transition(state_path, state, "NEEDS_FIX", updates={"active_role": None})


def _iterate(
    settings: Settings,
    repo: Path,
    state_path: Path,
    state: dict[str, object],
    plan: dict[str, object],
) -> dict[str, object]:
    risk = _state_risk(state)
    worktree = _state_worktree(repo, state)
    feedback = _artifact_path(state_path, state, "feedback")
    if feedback is None or not feedback.is_file():
        raise PilotError("Feedback structuré absent ; itération refusée.")
    payload = json.loads(feedback.read_text(encoding="utf-8"))
    feedback_target = (
        state.get("iteration_base_sha")
        if state.get("iteration_active")
        else state.get("head_sha")
    )
    if payload.get("head_sha_reviewed") != feedback_target:
        raise PilotError("Feedback périmé : il ne vise pas le head SHA courant.")
    plan_path = _artifact_path(state_path, state, "plan")
    if plan_path is None or not plan_path.is_file():
        raise PilotError("Plan durable absent ; itération refusée.")
    iteration = deepcopy(state.get("iteration", {}))
    if not isinstance(iteration, dict):
        iteration = {}

    if state["step"] == "NEEDS_FIX":
        state["iteration_active"] = True
        state["iteration_base_sha"] = str(state["head_sha"])
        state = save_state(state_path, state)
        state = transition(state_path, state, "ITERATING", role="executor")

    if state["step"] == "ITERATING":
        next_iteration = int(iteration.get("count", 0)) + 1
        output_path = state_path.parent / f"executor-iteration-{next_iteration}.json"
        if output_path.is_file():
            result = validate_executor(
                json.loads(output_path.read_text(encoding="utf-8")),
                iteration=True,
            )
        else:
            if _executor_effect_is_ambiguous(worktree, state.get("head_sha")):
                return transition(
                    state_path,
                    state,
                    "BLOCKED",
                    error=(
                        "Reprise Cursor ambiguë : des écritures existent sans résultat final "
                        "archivé ; elles ne sont pas rejouées automatiquement."
                    ),
                )
            session = state.get("executor_session")
            assert settings.policy is not None
            resume_supported = settings.policy.profile(risk).roles["executor"].resume
            invocation = executor_invocation(
                settings,
                worktree,
                plan_path,
                risk=risk,
                feedback=feedback,
                resume_session=str(session) if resume_supported and session else None,
            )
            raw_result = _run_agent(invocation, settings, risk=risk, role="executor")
            result = validate_executor(
                raw_result,
                iteration=True,
                forbidden_prompt=invocation.prompt,
            )
            write_normalized_json(
                output_path,
                result,
                forbidden_texts=(invocation.prompt or "",),
            )

        iteration["count"] = next_iteration
        state["iteration"] = iteration
        state["iteration_approach_changed"] = result["approach_changed"]
        state["executor_session"] = extract_session_id(result) or state.get("executor_session")
        state = _set_artifact(state_path, state, "executor", output_path)
        state = transition(state_path, state, "ITERATED", role=None)

    if state["step"] in {"ITERATED", "ITERATION_CANDIDATE_PREPARING"}:
        paths = _candidate_paths(
            worktree,
            state,
            plan["files_allowed_to_change"],  # type: ignore[arg-type]
            update_only=True,
        )
        state = _raise_risk_from_paths(
            settings,
            state_path,
            state,
            paths,
            source="actual_changes",
        )
        state = transition(
            state_path,
            state,
            "ITERATION_CANDIDATE_PREPARING",
            role=None,
        )
        candidate = _prepare_candidate(
            worktree,
            state,
            plan["files_allowed_to_change"],  # type: ignore[arg-type]
            update_only=True,
        )
        state = _raise_risk_from_paths(
            settings,
            state_path,
            state,
            candidate["paths"],  # type: ignore[arg-type]
            source="candidate",
        )
        state = transition(
            state_path,
            state,
            "ITERATION_CANDIDATE_READY",
            role=None,
            updates={"candidate": candidate, "head_sha": candidate["head_sha"]},
        )

    if state["step"] in {"ITERATION_CANDIDATE_READY", "TESTING"}:
        state = transition(state_path, state, "TESTING", role=None)
        iteration = state.get("iteration", {})
        count = int(iteration.get("count", 0)) if isinstance(iteration, dict) else 0
        candidate = _state_candidate(state)
        state = _run_exact_test_profile(
            state_path,
            state,
            worktree,
            profile="fast",
            paths=list(candidate["paths"]),  # type: ignore[arg-type]
            tested_base_sha=str(candidate["base_sha"]),
            iteration=count,
        )
        state = transition(state_path, state, "TESTED", role=None)

    if state["step"] in {"TESTED", "ITERATION_PR_TESTING"}:
        state = transition(state_path, state, "ITERATION_PR_TESTING", role=None)
        iteration = state.get("iteration", {})
        count = int(iteration.get("count", 0)) if isinstance(iteration, dict) else 0
        state = _run_pr_tests(
            settings,
            state_path,
            state,
            worktree,
            plan,
            iteration=count,
        )
        state = transition(state_path, state, "ITERATION_PR_TESTED", role=None)

    if state["step"] in {"ITERATION_PR_TESTED", "PUBLISHING"}:
        candidate = _state_candidate(state)
        _assert_candidate_identity(worktree, candidate)
        _require_exact_proof(state, "fast", candidate)
        _require_exact_proof(state, "pr", candidate)
        state = _raise_risk_from_paths(
            settings,
            state_path,
            state,
            candidate["paths"],  # type: ignore[arg-type]
            source="publication",
        )
        state = transition(state_path, state, "PUBLISHING", role=None)
        pull_request = _push_candidate_and_pr(
            worktree,
            state,
            candidate,
            update_only=True,
        )
        full_review = state.get("iteration_approach_changed") is True
        old_head = str(state.get("iteration_base_sha") or candidate["base_sha"])
        state = transition(
            state_path,
            state,
            "PUBLISHED",
            updates={
                "head_sha": candidate["head_sha"],
                "pull_request": pull_request,
                "review_base_sha": str(state["base_sha"]) if full_review else old_head,
                "review_mode": "full-approach-changed" if full_review else "delta-feedback",
                "iteration_active": False,
            },
        )
    return _review_current_head(settings, repo, state_path, state, plan)


def _record_step_failure(
    state_path: Path,
    state: dict[str, object],
    step: str,
    error: object,
) -> dict[str, object]:
    changed = deepcopy(state)
    failures = changed.setdefault("failures", {})
    if not isinstance(failures, dict):
        raise PilotError("État incohérent : compteur d'échecs invalide.")
    clean_error = sanitize_error(error)
    signature = hashlib.sha256(
        f"{type(error).__name__}:{clean_error}".encode("utf-8")
    ).hexdigest()
    previous = failures.get(step)
    same = isinstance(previous, dict) and previous.get("signature") == signature
    consecutive = int(previous.get("consecutive", 0)) + 1 if same else 1
    total = int(previous.get("total", 0)) + 1 if isinstance(previous, dict) else 1
    failures[step] = {
        "signature": signature,
        "consecutive": consecutive,
        "total": total,
        "last_error": clean_error,
        "last_failed_at": state.get("updated_at"),
    }
    changed = save_state(state_path, changed)
    if consecutive >= 3:
        return transition(
            state_path,
            changed,
            "BLOCKED",
            error=(
                f"Trois échecs identiques à l'étape {step}; intervention humaine requise: "
                f"{clean_error}"
            ),
            updates={"resume_from": None},
        )
    return transition(
        state_path,
        changed,
        "ERROR",
        error=error,
        updates={"resume_from": step},
    )


def resume_run(
    settings: Settings,
    repo: Path,
    run_id: str,
    *,
    allow_heavy: bool = False,
) -> dict[str, object]:
    state_path = run_state_path(repo, run_id)
    stable_run_id = state_path.parent.name
    with execution_locks(repo, stable_run_id):
        return _resume_run_locked(
            settings,
            repo,
            stable_run_id,
            allow_heavy=allow_heavy,
        )


def _resume_run_locked(
    settings: Settings,
    repo: Path,
    run_id: str,
    *,
    allow_heavy: bool = False,
) -> dict[str, object]:
    state_path = run_state_path(repo, run_id)
    state = load_state(state_path)
    if allow_heavy and not state.get("allow_heavy"):
        state["allow_heavy"] = True
        state = save_state(state_path, state)
    if settings.policy is None:
        raise PilotError("Politique de workflow absente ; reprise refusée.")
    policy = state.get("policy")
    if (
        not isinstance(policy, dict)
        or policy.get("version") != settings.policy.version
        or policy.get("sha256") != settings.policy.sha256
    ):
        raise PilotError("Reprise refusée : politique différente de celle du démarrage.")
    task_path = Path(str(state.get("task", "")))
    if not task_path.is_file() or hashlib.sha256(task_path.read_bytes()).hexdigest() != state.get("task_sha256"):
        raise PilotError("Reprise refusée : le brief a changé depuis le démarrage.")
    if state.get("fusion") is not False:
        raise PilotError("État invalide : ForgePilot ne possède jamais le droit de fusion.")
    risk = _state_risk(state)
    if risk == "R0":
        return transition(
            state_path,
            state,
            "BLOCKED",
            error=(
                "Ancien lot R0 terminalisé sans agent ; utiliser le profil mécanique fast."
            ),
        )
    missing = list(missing_binaries(settings))
    if missing:
        raise PilotError("Binaires manquants : " + ", ".join(missing))

    if state["step"] == "ERROR":
        resume_from = state.get("resume_from")
        if not isinstance(resume_from, str):
            raise PilotError("État ERROR sans étape de reprise.")
        state = transition(state_path, state, resume_from, updates={"resume_from": None})
    if state["step"] in {"COMPLETE", "BLOCKED", "CANCELLED"}:
        return state

    try:
        plan_path = _artifact_path(state_path, state, "plan")
        plan: dict[str, object] | None = None
        if plan_path and plan_path.is_file():
            plan = validate_plan(json.loads(plan_path.read_text(encoding="utf-8")))

        if state["step"] in {"CREATED", "PLANNING"}:
            state = transition(state_path, state, "PLANNING", role="planner")
            invocation = plan_invocation(
                settings,
                repo,
                Path(str(state["task"])),
                risk=risk,
            )
            result = _run_agent(invocation, settings, risk=risk, role="planner")
            plan = validate_plan(result, forbidden_prompt=invocation.prompt)
            plan_path = write_normalized_json(
                state_path.parent / "plan.json",
                plan,
                forbidden_texts=(invocation.prompt or "",),
            )
            state = _set_artifact(state_path, state, "plan", plan_path)
            state = _raise_risk_from_plan(settings, state_path, state, plan)
            risk = _state_risk(state)
            if plan["blocked"]:
                return transition(
                    state_path,
                    state,
                    "BLOCKED",
                    error="Le planificateur a déclaré blocked=true ; Cursor n'a pas été lancé.",
                )
            state = transition(state_path, state, "PLANNED", role=None)

        if plan is None:
            raise PilotError("Plan durable absent ou invalide.")

        if state["step"] in {"PLANNED", "PREPARING_WORKTREE"}:
            state = transition(state_path, state, "PREPARING_WORKTREE", role=None)
            worktree, branch = _ensure_worktree(repo, state)
            state = transition(
                state_path,
                state,
                "WORKTREE_READY",
                updates={"worktree": str(worktree), "branch": branch},
            )

        if state["step"] in {"WORKTREE_READY", "EXECUTING"}:
            worktree = _state_worktree(repo, state)
            state = transition(state_path, state, "EXECUTING", role="executor")
            output_path = state_path.parent / "executor.json"
            if output_path.is_file():
                result = validate_executor(
                    json.loads(output_path.read_text(encoding="utf-8"))
                )
            else:
                if _executor_effect_is_ambiguous(worktree, state.get("base_sha")):
                    return transition(
                        state_path,
                        state,
                        "BLOCKED",
                        error=(
                            "Reprise Cursor ambiguë : des écritures existent sans résultat final "
                            "archivé ; elles ne sont pas rejouées automatiquement."
                        ),
                    )
                invocation = executor_invocation(settings, worktree, plan_path, risk=risk)  # type: ignore[arg-type]
                raw_result = _run_agent(invocation, settings, risk=risk, role="executor")
                result = validate_executor(
                    raw_result,
                    forbidden_prompt=invocation.prompt,
                )
                write_normalized_json(
                    output_path,
                    result,
                    forbidden_texts=(invocation.prompt or "",),
                )
            state["executor_session"] = extract_session_id(result)
            state = _set_artifact(state_path, state, "executor", output_path)
            state = transition(state_path, state, "EXECUTED", role=None)

        if state["step"] in {"EXECUTED", "CANDIDATE_PREPARING"}:
            worktree = _state_worktree(repo, state)
            paths = _candidate_paths(
                worktree,
                state,
                plan["files_allowed_to_change"],  # type: ignore[arg-type]
                update_only=False,
            )
            state = _raise_risk_from_paths(
                settings,
                state_path,
                state,
                paths,
                source="actual_changes",
            )
            state = transition(state_path, state, "CANDIDATE_PREPARING", role=None)
            candidate = _prepare_candidate(
                worktree,
                state,
                plan["files_allowed_to_change"],  # type: ignore[arg-type]
                update_only=False,
            )
            state = _raise_risk_from_paths(
                settings,
                state_path,
                state,
                candidate["paths"],  # type: ignore[arg-type]
                source="candidate",
            )
            state = transition(
                state_path,
                state,
                "CANDIDATE_READY",
                role=None,
                updates={"candidate": candidate, "head_sha": candidate["head_sha"]},
            )

        if state["step"] in {"CANDIDATE_READY", "PR_TESTING"}:
            worktree = _state_worktree(repo, state)
            state = transition(state_path, state, "PR_TESTING", role=None)
            state = _run_pr_tests(
                settings,
                state_path,
                state,
                worktree,
                plan,
                iteration=None,
            )
            state = transition(state_path, state, "PR_TESTED", role=None)

        if state["step"] in {"PR_TESTED", "PUBLISHING"} and not state.get("iteration_active"):
            worktree = _state_worktree(repo, state)
            candidate = _state_candidate(state)
            _assert_candidate_identity(worktree, candidate)
            _require_exact_proof(state, "pr", candidate)
            state = _raise_risk_from_paths(
                settings,
                state_path,
                state,
                candidate["paths"],  # type: ignore[arg-type]
                source="publication",
            )
            state = transition(state_path, state, "PUBLISHING", role=None)
            pull_request = _push_candidate_and_pr(
                worktree,
                state,
                candidate,
                update_only=False,
            )
            state = transition(
                state_path,
                state,
                "PUBLISHED",
                updates={"head_sha": candidate["head_sha"], "pull_request": pull_request},
            )

        if state["step"] in {"PUBLISHED", "CERTIFYING", "CERTIFIED", "REVIEWING"}:
            state = _review_current_head(settings, repo, state_path, state, plan)
        elif state["step"] in {
            "NEEDS_FIX",
            "ITERATING",
            "ITERATED",
            "ITERATION_CANDIDATE_PREPARING",
            "ITERATION_CANDIDATE_READY",
            "TESTING",
            "TESTED",
            "ITERATION_PR_TESTING",
            "ITERATION_PR_TESTED",
        } or (
            state["step"] == "PUBLISHING" and state.get("iteration_active")
        ):
            state = _iterate(settings, repo, state_path, state, plan)
        return state
    except (KeyError, OSError, TypeError, ValueError, PilotError) as exc:
        # Un helper imbriqué peut avoir persisté plusieurs transitions avant
        # l'échec. Recharger évite qu'un état local périmé les écrase.
        current = load_state(state_path)
        failed_step = str(current.get("step", "UNKNOWN"))
        _record_step_failure(state_path, current, failed_step, exc)
        raise
