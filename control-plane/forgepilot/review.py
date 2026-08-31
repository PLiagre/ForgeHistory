from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
from typing import Iterable

from .policy import WorkflowPolicy, path_matches
from .process import PilotError, git, resolve_binary, run_command
from .protocol import findings_signatures, write_normalized_json
from .publication import changed_paths


def _fingerprint(repo: Path, path: str) -> dict[str, object]:
    target = repo / path
    if target.is_symlink():
        link = os.readlink(target)
        encoded = link.encode("utf-8", errors="surrogateescape")
        return {
            "path": path,
            "sha256": hashlib.sha256(encoded).hexdigest(),
            "bytes": len(encoded),
            "status": "symlink",
        }
    if not target.is_file():
        return {"path": path, "sha256": None, "bytes": 0, "status": "deleted"}
    digest = hashlib.sha256()
    size = 0
    with target.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
    return {"path": path, "sha256": digest.hexdigest(), "bytes": size, "status": "present"}


def build_review_bundle(
    repo: Path,
    *,
    base_sha: str,
    head_sha: str,
    plan: dict[str, object],
    policy: WorkflowPolicy,
    mechanical_results: Iterable[dict[str, object]] = (),
    review_context: dict[str, object] | None = None,
) -> dict[str, object]:
    actual_head = git(repo, "rev-parse", "HEAD")
    if actual_head != head_sha:
        raise PilotError(
            f"Bundle refusé : HEAD vaut {actual_head}, pas le SHA annoncé {head_sha}."
        )
    paths = changed_paths(repo, base_sha)
    tree_sha = git(repo, "rev-parse", "HEAD^{tree}")
    # La classification vient uniquement de la politique versionnée. Un plan
    # ne peut donc pas étiqueter du code comme « généré » pour masquer son diff.
    generated_patterns = tuple(policy.generated_artifacts)
    generated = [
        path for path in paths if any(path_matches(path, pattern) for pattern in generated_patterns)
    ]
    manual = [path for path in paths if path not in generated]

    diffs: dict[str, str] = {}
    for path in manual:
        diffs[path] = git(repo, "diff", "--no-ext-diff", f"{base_sha}...{head_sha}", "--", path)
    bundle: dict[str, object] = {
        "schema_version": 1,
        "base_sha": base_sha,
        "head_sha": head_sha,
        "tree_sha": tree_sha,
        "plan": plan,
        "manual_files": manual,
        "manual_diffs": diffs,
        "generated_artifacts": [_fingerprint(repo, path) for path in generated],
        "mechanical_results": list(mechanical_results),
        "review_context": review_context or {"mode": "full"},
        "producer_conclusions_included": False,
    }
    bundle["bundle_bytes"] = 0
    encoded = b""
    for _ in range(4):
        encoded = (json.dumps(bundle, ensure_ascii=False, sort_keys=True) + "\n").encode("utf-8")
        measured = len(encoded)
        if bundle["bundle_bytes"] == measured:
            break
        bundle["bundle_bytes"] = measured
    encoded = (json.dumps(bundle, ensure_ascii=False, sort_keys=True) + "\n").encode("utf-8")
    if len(encoded) > policy.review_bundle_max_bytes:
        raise PilotError(
            "Bundle de revue excessif "
            f"({len(encoded)} octets > {policy.review_bundle_max_bytes}) ; "
            "scinder le lot ou fournir un accès ciblé. Aucun contenu n'a été tronqué."
        )
    bundle["bundle_bytes"] = len(encoded)
    return bundle


def archive_review_material(
    run_dir: Path,
    *,
    base_sha: str,
    head_sha: str,
    tree_sha: str,
    review: dict[str, object],
    bundle_path: Path,
) -> Path:
    material = {
        "schema_version": 1,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "base_sha": base_sha,
        "head_sha": head_sha,
        "tree_sha": tree_sha,
        "review": "optional-diagnostic",
        "bundle": str(bundle_path),
        "verdict": review.get("verdict"),
        "acceptance_criteria": review.get("acceptance_criteria", []),
        "findings": review.get("findings", []),
        "checks_observed": review.get("checks_observed", []),
        "human_decision_required": review.get("human_decision_required", True),
        "fusion": False,
    }
    return write_normalized_json(run_dir / f"review-material-{head_sha}.json", material)


def write_feedback(
    run_dir: Path,
    *,
    head_sha: str,
    review: dict[str, object],
    iteration: int,
) -> Path:
    payload = {
        "schema_version": 1,
        "head_sha_reviewed": head_sha,
        "iteration": iteration,
        "verdict": review.get("verdict"),
        "findings": review.get("findings", []),
        "finding_signatures": findings_signatures(review.get("findings", [])),
        "acceptance_criteria": review.get("acceptance_criteria", []),
        "checks_observed": review.get("checks_observed", []),
        "instruction": "Corriger les constats puis exécuter les contrôles ciblés ; ne pas rejouer le plan sans ce feedback.",
    }
    return write_normalized_json(run_dir / f"feedback-{iteration}.json", payload)


def render_verdict_material(material_path: Path, output: Path) -> Path:
    try:
        material = json.loads(material_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError) as exc:
        raise PilotError(f"Matériau de revue illisible : {material_path}") from exc
    lines = [
        "# Diagnostic facultatif ForgePilot",
        "",
        "> Généré par ForgePilot comme diagnostic facultatif. "
        "Ce résultat ne conditionne pas la livraison.",
        "",
        f"- SHA de base : `{material.get('base_sha')}`",
        f"- SHA évalué : `{material.get('head_sha')}`",
        f"- Tree Git évalué : `{material.get('tree_sha')}`",
        f"- Résultat déclaré : **{material.get('verdict')}**",
        "- Effet automatique sur le dépôt : **aucun**",
        "",
        "## Constats",
        "",
    ]
    findings = material.get("findings", [])
    if isinstance(findings, list) and findings:
        lines.extend(f"- {json.dumps(item, ensure_ascii=False) if isinstance(item, dict) else item}" for item in findings)
    else:
        lines.append("- Aucun constat déclaré.")
    lines.extend(["", "## Contrôles observés", ""])
    checks = material.get("checks_observed", [])
    if isinstance(checks, list) and checks:
        lines.extend(f"- {json.dumps(item, ensure_ascii=False) if isinstance(item, dict) else item}" for item in checks)
    else:
        lines.append("- Aucun contrôle observé.")
    lines.extend(["", "## Preuves de certification", ""])
    certifications = material.get("post_review_proofs", [])
    if isinstance(certifications, list) and certifications:
        for proof in certifications:
            lines.append(
                "- "
                + (
                    json.dumps(proof, ensure_ascii=False, sort_keys=True)
                    if isinstance(proof, dict)
                    else str(proof)
                )
            )
    else:
        lines.append("- Aucune certification post-revue requise ou disponible.")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return output


def validate_verdict_material(
    repo: Path,
    state: dict[str, object],
    material_path: Path,
) -> dict[str, object]:
    if state.get("step") != "COMPLETE":
        raise PilotError(
            f"Matériau refusé : état {state.get('step')!r} non terminal ou périmé."
        )
    try:
        material = json.loads(material_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError) as exc:
        raise PilotError(f"Matériau de revue illisible : {material_path}") from exc
    if not isinstance(material, dict):
        raise PilotError("Matériau de revue invalide : objet JSON attendu.")
    candidate = state.get("candidate")
    if not isinstance(candidate, dict):
        raise PilotError("Matériau refusé : candidat Git absent de l'état.")
    head_sha = state.get("head_sha")
    tree_sha = candidate.get("tree_sha")
    if (
        not isinstance(head_sha, str)
        or not isinstance(tree_sha, str)
        or material.get("head_sha") != head_sha
        or material.get("tree_sha") != tree_sha
    ):
        raise PilotError("Matériau refusé : SHA/tree du reviewer périmé par rapport à l'état.")
    branch = state.get("branch")
    if not isinstance(branch, str) or git(repo, "rev-parse", branch) != head_sha:
        raise PilotError("Matériau refusé : la branche du lot ne vise plus le head SHA évalué.")
    actual_tree = git(repo, "rev-parse", f"{head_sha}^{{tree}}")
    if actual_tree != tree_sha:
        raise PilotError("Matériau refusé : le tree Git ne correspond plus à la preuve.")
    worktree_value = state.get("worktree")
    if isinstance(worktree_value, str) and Path(worktree_value).exists():
        if git(Path(worktree_value), "rev-parse", "HEAD") != head_sha:
            raise PilotError("Matériau refusé : le worktree ne vise plus le head SHA évalué.")
    bundle_value = material.get("bundle")
    if not isinstance(bundle_value, str):
        raise PilotError("Matériau refusé : bundle de revue absent.")
    bundle_path = Path(bundle_value)
    if not bundle_path.is_absolute():
        bundle_path = material_path.parent / bundle_path
    try:
        bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError) as exc:
        raise PilotError("Matériau refusé : bundle de revue illisible.") from exc
    if (
        not isinstance(bundle, dict)
        or bundle.get("head_sha") != head_sha
        or bundle.get("tree_sha") != tree_sha
    ):
        raise PilotError("Matériau refusé : bundle lié à un autre candidat.")

    risk = state.get("risk")
    requires_certify = isinstance(risk, dict) and risk.get("effective") == "R2"
    if requires_certify and state.get("step") == "COMPLETE":
        state_proofs = state.get("proofs", [])
        material_proofs = material.get("post_review_proofs", [])

        def exact_certify(value: object) -> bool:
            return (
                isinstance(value, dict)
                and value.get("profile") == "certify"
                and value.get("head_sha") == head_sha
                and value.get("tree_sha") == tree_sha
            )

        if not (
            isinstance(state_proofs, list)
            and any(exact_certify(item) for item in state_proofs)
            and isinstance(material_proofs, list)
            and any(exact_certify(item) for item in material_proofs)
        ):
            raise PilotError("Matériau refusé : certification exacte absente pour ce SHA/tree.")
    return material


def comment_review_on_pr(repo: Path, pull_request: str, material_markdown: Path) -> None:
    resolve_binary("gh")
    run_command(
        ["gh", "pr", "comment", pull_request, "--body-file", str(material_markdown)],
        cwd=repo,
        timeout_seconds=120,
    )
