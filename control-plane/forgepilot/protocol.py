from __future__ import annotations

import json
import os
from pathlib import Path
import re
from typing import Any, Iterable

from .process import PilotError
from .state import SECRET_MIN_LENGTH


PLAN_FIELDS = {
    "task",
    "scope",
    "acceptance_criteria",
    "files_to_read",
    "files_allowed_to_change",
    "checks",
    "risks",
    "blocked",
}
REVIEW_FIELDS = {
    "verdict",
    "acceptance_criteria",
    "findings",
    "checks_observed",
    "human_decision_required",
}
BRIEF_REVIEW_FIELDS = {
    "verdict",
    "findings",
    "lot_unique",
    "criteres_verifiables",
    "human_decision_required",
}
# Une panne de transport n'est pas un jugement sur le produit. Sans ce champ,
# un relecteur incapable de lire son bundle rendait BLOCKED, et le harnais
# archivait un verdict produit là où il n'y avait qu'un fichier illisible
# (lot 033). `material_unreadable` nomme la panne ; `durable.py` la rejoue au
# lieu de la figer.
REVIEW_OPTIONAL_FIELDS = {"blocked_reason"}
BLOCKED_REASONS = {"material_unreadable", "product"}
# Schéma fermé imposé au reviewer. Cursor n'a pas de drapeau --json-schema ;
# l'invocation le déclare dans le prompt et le canal d'échange, puis
# validate_review() reste obligatoire après la réponse fournisseur.
REVIEW_JSON_SCHEMA: dict[str, Any] = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "ForgePilotReview",
    "type": "object",
    "additionalProperties": False,
    "required": [
        "verdict",
        "acceptance_criteria",
        "findings",
        "checks_observed",
        "human_decision_required",
    ],
    "properties": {
        "verdict": {"type": "string", "enum": ["PASS", "FAIL", "BLOCKED"]},
        "human_decision_required": {"type": "boolean", "const": True},
        "blocked_reason": {
            "type": "string",
            "enum": ["material_unreadable", "product"],
        },
        "acceptance_criteria": {
            "type": "array",
            "minItems": 1,
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["criterion", "status"],
                "properties": {
                    "criterion": {"type": "string", "minLength": 1},
                    "status": {"type": "string", "enum": ["PASS", "FAIL", "BLOCKED"]},
                    "evidence": {"type": "string", "minLength": 1},
                },
            },
        },
        "checks_observed": {
            "type": "array",
            "minItems": 1,
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["check", "status"],
                "properties": {
                    "check": {"type": "string", "minLength": 1},
                    "status": {"type": "string", "enum": ["PASS", "FAIL", "BLOCKED"]},
                    "evidence": {"type": "string", "minLength": 1},
                },
            },
        },
        "findings": {
            "type": "array",
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["id", "path", "issue", "evidence"],
                "properties": {
                    "id": {"type": "string", "minLength": 1},
                    "path": {"type": "string", "minLength": 1},
                    "issue": {"type": "string", "minLength": 1},
                    "evidence": {"type": "string", "minLength": 1},
                    "severity": {"type": "string", "enum": ["P0", "P1", "P2", "P3"]},
                },
            },
        },
    },
}
REVIEW_SCHEMA_RETRY_HINT = (
    "Ta réponse précédente a été refusée par le contrat JSON, pas jugée "
    "sur le produit. Recommence à zéro, sans te souvenir de la tentative. "
    "`acceptance_criteria` est une liste d'objets "
    '`{"criterion":"...","status":"PASS"}`, jamais une liste de chaînes '
    'ni un objet indexé `{"0":{...}}`. `checks_observed` de même avec `check`.'
)
BRIEF_REVIEW_JSON_SCHEMA: dict[str, Any] = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "ForgePilotBriefReview",
    "type": "object",
    "additionalProperties": False,
    "required": sorted(BRIEF_REVIEW_FIELDS),
    "properties": {
        "verdict": {"type": "string", "enum": ["PASS", "FAIL", "BLOCKED"]},
        "lot_unique": {"type": "boolean"},
        "criteres_verifiables": {"type": "boolean"},
        "human_decision_required": {"type": "boolean", "const": True},
        "findings": {
            "type": "array",
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["id", "defaut", "citation", "consequence", "correction"],
                "properties": {
                    "id": {"type": "string", "minLength": 1},
                    "defaut": {"type": "integer", "minimum": 1, "maximum": 6},
                    "citation": {"type": "string", "minLength": 1},
                    "consequence": {"type": "string", "minLength": 1},
                    "correction": {"type": "string", "minLength": 1},
                },
            },
        },
    },
}
BRIEF_REVIEW_SCHEMA_RETRY_HINT = (
    "Ta réponse précédente a été refusée par le contrat JSON. Recommence la "
    "lecture depuis le brief et rends uniquement l'objet conforme au schéma, "
    "sans prose ni bloc Markdown."
)
EXECUTOR_FIELDS = {"summary", "files_modified", "checks", "blockages"}
EXECUTOR_OPTIONAL_FIELDS = {"approach_changed", "session_id"}
_TRANSPORT_FIELDS = {"session_id"}
_STATUSES = {"PASS", "FAIL", "BLOCKED"}


def _json_object(value: object, *, context: str) -> dict[str, Any]:
    if isinstance(value, dict):
        return value
    if isinstance(value, str):
        try:
            decoded = json.loads(value)
        except json.JSONDecodeError as exc:
            raise PilotError(f"{context} n'est pas un objet JSON valide.") from exc
        if isinstance(decoded, dict):
            return decoded
    raise PilotError(f"{context} n'est pas un objet JSON.")


def is_agent_envelope(payload: object) -> bool:
    """Vrai si le JSON a une provenance d'invocation agent, pas un avis édité.

    `recover-review --result` refuse un objet revue nu : ce serait contourner
    le juge. Une enveloppe Cursor/Claude porte `type=result` ou un
    `session_id` avec un champ métier (`result` / `output` / `content`).
    """

    if not isinstance(payload, dict):
        return False
    if payload.get("type") == "result" and any(
        key in payload for key in ("result", "output", "content")
    ):
        return True
    session = payload.get("session_id") or payload.get("sessionId")
    if isinstance(session, str) and session.strip():
        return any(key in payload for key in ("result", "output", "content"))
    return False


def unwrap_agent_result(result: object, *, context: str) -> dict[str, Any]:
    """Extrait l'objet métier d'une enveloppe Claude/Cursor JSON ou JSONL."""

    current = _json_object(result, context=context)
    # Les CLIs utilisent selon leur version `result`, `output` ou `content`.
    for _ in range(4):
        for key in ("result", "output", "content"):
            candidate = current.get(key)
            if isinstance(candidate, (dict, str)):
                try:
                    current = _json_object(candidate, context=context)
                except PilotError:
                    continue
                break
        else:
            return current
    return current


def _assert_forbidden_text(
    value: object,
    forbidden_text: str | None,
    *,
    context: str,
) -> None:
    """Refuse l'écho exact d'un prompt avant toute normalisation/archivage."""

    if not forbidden_text:
        return
    if isinstance(value, dict):
        for child in value.values():
            _assert_forbidden_text(child, forbidden_text, context=context)
    elif isinstance(value, list):
        for child in value:
            _assert_forbidden_text(child, forbidden_text, context=context)
    elif isinstance(value, str) and forbidden_text in value:
        raise PilotError(f"{context} contient une copie du prompt ; archivage refusé.")


def _closed_object(
    payload: dict[str, Any],
    required: set[str],
    *,
    context: str,
    optional: set[str] | None = None,
) -> dict[str, Any]:
    allowed = required | (optional or set())
    missing = sorted(required - set(payload))
    if missing:
        raise PilotError(f"{context} incomplet ; champs absents : " + ", ".join(missing))
    extra = sorted(set(payload) - allowed)
    if extra:
        raise PilotError(f"{context} contient des champs non autorisés : " + ", ".join(extra))
    return payload


def _nonempty_string(value: object, *, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise PilotError(f"{context} doit être une chaîne non vide.")
    return value.strip()


def _validate_observations(
    value: object,
    *,
    context: str,
    label_key: str,
    allow_empty: bool = False,
) -> list[dict[str, str]]:
    if not isinstance(value, list) or (not allow_empty and not value):
        raise PilotError(f"{context} doit être une liste non vide d'objets.")
    if allow_empty and not value:
        return []
    normalized: list[dict[str, str]] = []
    seen: set[str] = set()
    for index, item in enumerate(value):
        if not isinstance(item, dict):
            raise PilotError(f"{context}[{index}] doit être un objet.")
        _closed_object(
            item,
            {label_key, "status"},
            optional={"evidence"},
            context=f"{context}[{index}]",
        )
        label = _nonempty_string(item[label_key], context=f"{context}[{index}].{label_key}")
        if label in seen:
            raise PilotError(f"{context} contient un doublon : {label!r}.")
        seen.add(label)
        status = item["status"]
        if status not in _STATUSES:
            raise PilotError(f"{context}[{index}].status invalide : {status!r}.")
        entry = {label_key: label, "status": str(status)}
        if "evidence" in item:
            entry["evidence"] = _nonempty_string(
                item["evidence"], context=f"{context}[{index}].evidence"
            )
        normalized.append(entry)
    return normalized


def _has_glob(value: str) -> bool:
    return any(marker in value for marker in ("*", "?", "["))


def _has_useful_glob_prefix(value: str) -> bool:
    wildcard = min(
        (index for index in (value.find("*"), value.find("?"), value.find("[")) if index >= 0),
        default=len(value),
    )
    return bool(value[:wildcard].rstrip("/"))


def validate_plan(
    result: object,
    *,
    forbidden_prompt: str | None = None,
) -> dict[str, Any]:
    _assert_forbidden_text(result, forbidden_prompt, context="La sortie du planificateur")
    plan = unwrap_agent_result(result, context="La sortie du planificateur")
    transport = {key: plan.pop(key) for key in tuple(plan) if key in _TRANSPORT_FIELDS}
    _closed_object(plan, PLAN_FIELDS, context="Plan")
    if not isinstance(plan["blocked"], bool):
        raise PilotError("Plan invalide : blocked doit être un booléen.")
    for field in ("acceptance_criteria", "files_to_read", "files_allowed_to_change", "checks", "risks"):
        if not isinstance(plan[field], list) or not all(isinstance(item, str) for item in plan[field]):
            raise PilotError(f"Plan invalide : {field} doit être une liste de chaînes.")
    if not isinstance(plan["task"], str) or not isinstance(plan["scope"], (str, list, dict)):
        raise PilotError("Plan invalide : task/scope incorrect.")
    allowed: list[str] = []
    for pattern in plan["files_allowed_to_change"]:
        # Un glob est admis, mais jamais un chemin absolu ou une remontée.
        normalized = str(pattern).replace("\\", "/")
        while normalized.startswith("./"):
            normalized = normalized[2:]
        if (
            not normalized
            or Path(normalized).is_absolute()
            or re.match(r"^[A-Za-z]:/", normalized)
            or normalized.startswith("//")
            or ".." in Path(normalized).parts
        ):
            raise PilotError(f"Plan invalide : périmètre dangereux {pattern!r}.")
        if (
            normalized == ".git"
            or normalized.startswith(".git/")
            or normalized == ".forgepilot"
            or normalized.startswith(".forgepilot/")
        ):
            raise PilotError(f"Plan invalide : périmètre interne interdit {pattern!r}.")
        if normalized in {"*", "**", "**/*", "."}:
            raise PilotError(
                f"Plan invalide : périmètre universel interdit {pattern!r} ; scinder le lot."
            )
        if _has_glob(normalized) and not _has_useful_glob_prefix(normalized):
            raise PilotError(
                f"Plan invalide : glob sans préfixe de dépôt utile {pattern!r} ; scinder le lot."
            )
        allowed.append(normalized)
    plan["files_allowed_to_change"] = allowed
    # La métadonnée de transport connue ne fait jamais partie du plan archivé.
    del transport
    return plan


def validate_brief_review(
    result: object,
    *,
    forbidden_prompt: str | None = None,
) -> dict[str, Any]:
    """Ferme le contrat de la relecture préalable du brief."""

    _assert_forbidden_text(result, forbidden_prompt, context="La relecture du brief")
    review = unwrap_agent_result(result, context="La relecture du brief")
    for key in tuple(review):
        if key in _TRANSPORT_FIELDS:
            review.pop(key)
    _closed_object(review, BRIEF_REVIEW_FIELDS, context="Relecture du brief")
    verdict = review["verdict"]
    if verdict not in {"PASS", "FAIL", "BLOCKED"}:
        raise PilotError(f"Verdict de brief invalide : {verdict!r}.")
    for field in ("lot_unique", "criteres_verifiables", "human_decision_required"):
        if not isinstance(review[field], bool):
            raise PilotError(f"Relecture du brief.{field} doit être un booléen.")
    if review["human_decision_required"] is not True:
        raise PilotError(
            "Relecture du brief invalide : human_decision_required doit rester true."
        )
    raw_findings = review["findings"]
    if not isinstance(raw_findings, list):
        raise PilotError("Relecture du brief.findings doit être une liste.")
    findings: list[dict[str, object]] = []
    identifiers: set[str] = set()
    fields = {"id", "defaut", "citation", "consequence", "correction"}
    for index, item in enumerate(raw_findings):
        if not isinstance(item, dict):
            raise PilotError(f"Relecture du brief.findings[{index}] doit être un objet.")
        _closed_object(item, fields, context=f"Relecture du brief.findings[{index}]")
        identifier = _nonempty_string(
            item["id"], context=f"Relecture du brief.findings[{index}].id"
        )
        if identifier in identifiers:
            raise PilotError(f"Relecture du brief : identifiant dupliqué {identifier!r}.")
        identifiers.add(identifier)
        defect = item["defaut"]
        if isinstance(defect, bool) or not isinstance(defect, int) or not 1 <= defect <= 6:
            raise PilotError(
                f"Relecture du brief.findings[{index}].defaut doit valoir de 1 à 6."
            )
        findings.append(
            {
                "id": identifier,
                "defaut": defect,
                "citation": _nonempty_string(
                    item["citation"],
                    context=f"Relecture du brief.findings[{index}].citation",
                ),
                "consequence": _nonempty_string(
                    item["consequence"],
                    context=f"Relecture du brief.findings[{index}].consequence",
                ),
                "correction": _nonempty_string(
                    item["correction"],
                    context=f"Relecture du brief.findings[{index}].correction",
                ),
            }
        )
    if verdict == "PASS" and (
        findings or not review["lot_unique"] or not review["criteres_verifiables"]
    ):
        raise PilotError(
            "PASS de brief incohérent : constat présent, lot multiple ou critère invérifiable."
        )
    if verdict == "FAIL" and not findings:
        raise PilotError("FAIL de brief incohérent : aucun constat.")
    review["findings"] = findings
    return review


def validate_review(
    result: object,
    *,
    expected_criteria: Iterable[str] | None = None,
    forbidden_prompt: str | None = None,
) -> dict[str, Any]:
    _assert_forbidden_text(result, forbidden_prompt, context="La sortie du reviewer")
    review = unwrap_agent_result(result, context="La sortie du reviewer")
    for key in tuple(review):
        if key in _TRANSPORT_FIELDS:
            review.pop(key)
    _closed_object(review, REVIEW_FIELDS, optional=REVIEW_OPTIONAL_FIELDS, context="Revue")
    if review["verdict"] not in {"PASS", "FAIL", "BLOCKED"}:
        raise PilotError(f"Verdict invalide : {review['verdict']!r}.")
    blocked_reason: str | None = None
    if "blocked_reason" in review:
        blocked_reason = _nonempty_string(
            review["blocked_reason"], context="Revue.blocked_reason"
        )
        if blocked_reason not in BLOCKED_REASONS:
            raise PilotError(f"Cause de blocage invalide : {blocked_reason!r}.")
        if review["verdict"] != "BLOCKED":
            raise PilotError(
                "Revue invalide : blocked_reason n'accompagne qu'un verdict BLOCKED."
            )
        review["blocked_reason"] = blocked_reason
    material_unreadable = blocked_reason == "material_unreadable"
    if material_unreadable:
        review.setdefault("acceptance_criteria", [])
        review.setdefault("checks_observed", [])
    criteria = _validate_observations(
        review["acceptance_criteria"],
        context="Revue.acceptance_criteria",
        label_key="criterion",
        allow_empty=material_unreadable,
    )
    checks = _validate_observations(
        review["checks_observed"],
        context="Revue.checks_observed",
        label_key="check",
        allow_empty=material_unreadable,
    )
    expected = list(expected_criteria) if expected_criteria is not None else None
    # Un relecteur qui n'a pas pu lire son bundle n'a pas pu y lire les
    # critères du plan : le tenir à leur énumération exacte transformerait
    # une panne signalée honnêtement en sortie invalide, donc en silence.
    if expected is not None and not material_unreadable:
        if not all(isinstance(item, str) and item.strip() for item in expected):
            raise PilotError("Critères attendus invalides.")
        observed = [item["criterion"] for item in criteria]
        if set(observed) != {item.strip() for item in expected} or len(observed) != len(expected):
            raise PilotError("Revue incomplète : les critères ne correspondent pas au plan.")

    findings_value = review["findings"]
    if not isinstance(findings_value, list):
        raise PilotError("Revue.findings doit être une liste.")
    findings: list[dict[str, str]] = []
    finding_ids: set[str] = set()
    for index, item in enumerate(findings_value):
        if not isinstance(item, dict):
            raise PilotError(f"Revue.findings[{index}] doit être un objet.")
        _closed_object(
            item,
            {"id", "path", "issue", "evidence"},
            optional={"severity"},
            context=f"Revue.findings[{index}]",
        )
        normalized = {
            key: _nonempty_string(item[key], context=f"Revue.findings[{index}].{key}")
            for key in ("id", "path", "issue", "evidence")
        }
        if normalized["id"] in finding_ids:
            raise PilotError(f"Identifiant de constat dupliqué : {normalized['id']!r}.")
        finding_ids.add(normalized["id"])
        if "severity" in item:
            severity = _nonempty_string(
                item["severity"], context=f"Revue.findings[{index}].severity"
            )
            if severity not in {"P0", "P1", "P2", "P3"}:
                raise PilotError(f"Sévérité de constat invalide : {severity!r}.")
            normalized["severity"] = severity
        findings.append(normalized)

    if review["human_decision_required"] is not True:
        raise PilotError("Revue invalide : human_decision_required doit rester true.")
    statuses = [item["status"] for item in criteria + checks]
    verdict = review["verdict"]
    if verdict == "PASS" and (findings or any(status != "PASS" for status in statuses)):
        raise PilotError("PASS incohérent : constat ou contrôle non PASS présent.")
    if verdict == "FAIL" and not findings and "FAIL" not in statuses:
        raise PilotError("FAIL incohérent : aucun constat ni statut FAIL.")
    if verdict == "BLOCKED" and "BLOCKED" not in statuses and not material_unreadable:
        raise PilotError("BLOCKED incohérent : aucun critère ou contrôle BLOCKED.")
    review["acceptance_criteria"] = criteria
    review["checks_observed"] = checks
    review["findings"] = findings
    return review


def validate_executor(
    result: object,
    *,
    iteration: bool = False,
    forbidden_prompt: str | None = None,
) -> dict[str, Any]:
    """Normalise la seule sortie Cursor archivable par ForgePilot."""

    _assert_forbidden_text(result, forbidden_prompt, context="La sortie de l'exécuteur")
    session_id = extract_session_id(result)
    payload = unwrap_agent_result(result, context="La sortie de l'exécuteur")
    _closed_object(
        payload,
        EXECUTOR_FIELDS,
        optional=EXECUTOR_OPTIONAL_FIELDS,
        context="Sortie exécuteur",
    )
    payload["summary"] = _nonempty_string(payload["summary"], context="Sortie exécuteur.summary")
    for field in ("files_modified", "blockages"):
        value = payload[field]
        if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
            raise PilotError(f"Sortie exécuteur.{field} doit être une liste de chaînes.")
        payload[field] = [item.strip() for item in value if item.strip()]
    payload["checks"] = _validate_observations(
        payload["checks"], context="Sortie exécuteur.checks", label_key="check"
    )
    if iteration and not isinstance(payload.get("approach_changed"), bool):
        raise PilotError("Sortie exécuteur d'itération sans booléen approach_changed.")
    if "approach_changed" in payload and not isinstance(payload["approach_changed"], bool):
        raise PilotError("Sortie exécuteur.approach_changed doit être un booléen.")
    if session_id:
        payload["session_id"] = session_id
    elif "session_id" in payload:
        payload["session_id"] = _nonempty_string(
            payload["session_id"], context="Sortie exécuteur.session_id"
        )
    return payload


def extract_session_id(result: object) -> str | None:
    def walk(value: object) -> Iterable[str]:
        if isinstance(value, dict):
            for key in ("session_id", "sessionId", "conversation_id", "conversationId", "chat_id", "chatId"):
                candidate = value.get(key)
                if isinstance(candidate, str) and candidate.strip():
                    yield candidate.strip()
            for child in value.values():
                yield from walk(child)
        elif isinstance(value, list):
            for child in value:
                yield from walk(child)

    return next(iter(walk(result)), None)


def findings_signatures(findings: object) -> list[str]:
    if not isinstance(findings, list):
        return []
    signatures: list[str] = []
    for finding in findings:
        if isinstance(finding, str):
            signatures.append(finding.strip())
        elif isinstance(finding, dict):
            identifier = finding.get("id")
            if isinstance(identifier, str) and identifier.strip():
                signatures.append(identifier.strip())
                continue
            stable = {
                key: finding[key]
                for key in sorted(finding)
                if key not in {"timestamp", "observed_at"}
            }
            signatures.append(json.dumps(stable, sort_keys=True, ensure_ascii=False))
        else:
            signatures.append(repr(finding))
    return sorted(item for item in signatures if item)


def write_normalized_json(
    path: Path,
    payload: object,
    *,
    forbidden_texts: Iterable[str] = (),
) -> Path:
    for forbidden_text in forbidden_texts:
        _assert_forbidden_text(
            payload,
            forbidden_text,
            context=f"Le contenu destiné à {path.name}",
        )

    def redact(value: object) -> object:
        if isinstance(value, dict):
            cleaned: dict[str, object] = {}
            for key, child in value.items():
                if re.search(r"(?:prompt|authorization|api[_-]?key|access[_-]?token|secret|password)", str(key), re.I):
                    cleaned[str(key)] = "<redacted>"
                else:
                    cleaned[str(key)] = redact(child)
            return cleaned
        if isinstance(value, list):
            return [redact(child) for child in value]
        if isinstance(value, str):
            result = value
            for name, secret in os.environ.items():
                if (
                    len(secret) >= SECRET_MIN_LENGTH
                    and re.search(r"(?:key|token|secret|password|authorization)", name, re.I)
                ):
                    result = result.replace(secret, "<secret>")
            return result
        return value

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(redact(payload), indent=2, ensure_ascii=False, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return path
