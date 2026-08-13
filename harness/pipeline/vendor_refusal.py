#!/usr/bin/env py
"""
harness/pipeline/vendor_refusal.py -- classification du flux Claude et consignation
du refus fournisseur (HTTP 429) comme état explicite du pipeline.

Module stdlib-only, directement testable par pytest.

Fonctions exportées :
  classify(transcript_path) -> str
    Lit un fichier JSONL stream-json produit par le CLI Claude
    (--output-format stream-json). Retourne :
      "vendor_refusal" -- is_error=true ET api_error_status=429
      "success"        -- result présent, pas d'erreur fatale
      "other_error"    -- is_error=true mais api_error_status != 429,
                         ou fichier vide/inexistant

  log_refusal(audit_id, transcript_path, state_path) -> None
    Ajoute une ligne JSON à state_path (JSONL) avec les champs requis.

  mark_fallback_actor(review_path, actor="forge-challenger-codex") -> None
    Insère un encart d'identification de l'acteur au début du corps du fichier
    de revue (après le frontmatter YAML).
"""
from __future__ import annotations

import datetime
import json
from pathlib import Path

VENDOR_REFUSAL_STATE = Path(__file__).resolve().parent / "vendor-refusal-state.jsonl"

FALLBACK_MARKER_TEMPLATE = """\
> **Acteur réel** : `{actor}` (repli fournisseur — Claude a retourné HTTP 429).
> Ce contre-audit a été produit par le CLI Codex en remplacement du CLI Claude dont
> le plafond mensuel de l'organisation était atteint (ADR-0008, ADR-0009).
"""


def classify(transcript_path: "Path | str") -> str:
    """Lit un fichier JSONL stream-json Claude et retourne la classification.

    Retourne :
      "vendor_refusal" si is_error=true et api_error_status=429
      "success"        si result présent et pas d'erreur fatale
      "other_error"    sinon (fichier vide, inexistant, ou autre erreur)
    """
    path = Path(transcript_path)
    if not path.exists():
        return "other_error"

    has_success = False
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            continue
        if obj.get("is_error") is True:
            if obj.get("api_error_status") == 429:
                return "vendor_refusal"
            return "other_error"
        if "result" in obj and obj.get("is_error") is not True:
            has_success = True

    return "success" if has_success else "other_error"


def _utc_now_iso() -> str:
    """ISO 8601, UTC, précision à la seconde."""
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def log_refusal(
    audit_id: str,
    transcript_path: "Path | str",
    state_path: "Path | str",
) -> None:
    """Ajoute une ligne JSON au fichier d'état persistant.

    Champs écrits : timestamp, audit_id, error_type, api_error_status,
    fallback_attempted (initialement false).
    """
    state_path = Path(state_path)
    state_path.parent.mkdir(parents=True, exist_ok=True)
    record = {
        "timestamp": _utc_now_iso(),
        "audit_id": audit_id,
        "error_type": "vendor_refusal",
        "api_error_status": 429,
        "fallback_attempted": False,
        "transcript_path": str(transcript_path),
    }
    with state_path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(record, ensure_ascii=False) + "\n")


def mark_fallback_attempted(
    audit_id: str,
    state_path: "Path | str",
) -> None:
    """Met à jour le champ `fallback_attempted` à True pour l'audit_id donné.

    Reécrit le fichier d'état en remplaçant la dernière ligne portant cet
    audit_id par une version avec `fallback_attempted: true`. Si aucune
    ligne ne correspond, ne fait rien (idempotent).
    """
    state_path = Path(state_path)
    if not state_path.exists():
        return
    lines = [l for l in state_path.read_text(encoding="utf-8").splitlines() if l.strip()]
    updated = False
    # Parcours en sens inverse pour ne mettre à jour que la dernière occurrence
    for i in range(len(lines) - 1, -1, -1):
        try:
            record = json.loads(lines[i])
        except json.JSONDecodeError:
            continue
        if record.get("audit_id") == audit_id:
            record["fallback_attempted"] = True
            lines[i] = json.dumps(record, ensure_ascii=False)
            updated = True
            break
    if updated:
        with state_path.open("w", encoding="utf-8") as fh:
            for line in lines:
                fh.write(line + "\n")


def mark_fallback_actor(
    review_path: "Path | str",
    actor: str = "forge-challenger-codex",
) -> None:
    """Insère le marqueur d'acteur réel au début du corps du fichier de revue.

    Le corps commence après le frontmatter YAML (délimité par ---). Si aucun
    frontmatter n'est détecté, l'encart est inséré en tête du fichier.
    """
    review_path = Path(review_path)
    text = review_path.read_text(encoding="utf-8")
    lines = text.splitlines(keepends=True)

    marker = FALLBACK_MARKER_TEMPLATE.format(actor=actor)

    # Cherche la fin du frontmatter YAML (second ---)
    end_fm = -1
    if lines and lines[0].strip() == "---":
        for i, line in enumerate(lines[1:], start=1):
            if line.strip() == "---":
                end_fm = i
                break

    if end_fm >= 0:
        # Insère après le frontmatter
        insert_at = end_fm + 1
        new_lines = lines[:insert_at] + ["\n", marker, "\n"] + lines[insert_at:]
    else:
        # Pas de frontmatter : insère en tête
        new_lines = [marker, "\n"] + lines

    review_path.write_text("".join(new_lines), encoding="utf-8")
