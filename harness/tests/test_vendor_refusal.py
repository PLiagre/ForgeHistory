"""
Tests pour harness/pipeline/vendor_refusal.py -- brief 014.

Couvre les 6 scénarios requis par SC3 :
  1. Transcript 429 (is_error=true, api_error_status=429) → "vendor_refusal"
  2. Transcript succès (result=..., pas d'erreur) → "success"
  3. Transcript autre erreur (is_error=true, api_error_status=500) → "other_error"
  4. Transcript vide → "other_error"
  5. log_refusal() sur fixture → fichier d'état contient ≥ 1 ligne, champs requis présents
  6. mark_fallback_actor() sur un fichier de revue fixture → marqueur forge-challenger-codex présent

Hard-won rule 4 (prove red first) : le test test_classify_429_returns_vendor_refusal
est celui utilisé pour la preuve rouge (paire B du brief).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"
sys.path.insert(0, str(HARNESS))

from pipeline import vendor_refusal  # noqa: E402


def _write_transcript(path: Path, *lines: dict) -> None:
    """Écrit des lignes JSON dans un fichier JSONL synthétique."""
    with path.open("w", encoding="utf-8") as fh:
        for line in lines:
            fh.write(json.dumps(line) + "\n")


# --- Scénario 1 : transcript 429 → "vendor_refusal" ---

def test_classify_429_returns_vendor_refusal(tmp_path):
    """Transcript avec is_error=true et api_error_status=429 → 'vendor_refusal'.
    Ce test est utilisé comme cible de la preuve rouge paire B.
    """
    t = tmp_path / "transcript.jsonl"
    _write_transcript(
        t,
        {"type": "rate_limit_event", "rate_limit_info": {"status": "rejected"}},
        {
            "result": "You've hit your org's monthly spend limit",
            "api_error_status": 429,
            "is_error": True,
            "total_cost_usd": 0,
            "num_turns": 1,
        },
    )
    assert vendor_refusal.classify(t) == "vendor_refusal"


# --- Scénario 2 : transcript succès → "success" ---

def test_classify_success_returns_success(tmp_path):
    """Transcript avec result et is_error=false → 'success'."""
    t = tmp_path / "transcript.jsonl"
    _write_transcript(
        t,
        {"type": "assistant", "message": "revue produite"},
        {"result": "La revue est complète.", "is_error": False, "total_cost_usd": 1.23},
    )
    assert vendor_refusal.classify(t) == "success"


# --- Scénario 3 : transcript autre erreur → "other_error" ---

def test_classify_other_error_returns_other_error(tmp_path):
    """Transcript avec is_error=true et api_error_status=500 → 'other_error'."""
    t = tmp_path / "transcript.jsonl"
    _write_transcript(
        t,
        {"result": "Internal Server Error", "api_error_status": 500, "is_error": True},
    )
    assert vendor_refusal.classify(t) == "other_error"


# --- Scénario 4 : transcript vide → "other_error" ---

def test_classify_empty_transcript_returns_other_error(tmp_path):
    """Fichier vide → 'other_error' (pas de succès simulé)."""
    t = tmp_path / "transcript.jsonl"
    t.write_text("", encoding="utf-8")
    assert vendor_refusal.classify(t) == "other_error"


# --- Scénario 5 : log_refusal → fichier d'état avec ≥ 1 ligne, champs requis ---

def test_log_refusal_writes_valid_line(tmp_path):
    """log_refusal() écrit une ligne JSON valide avec les champs requis."""
    t = tmp_path / "transcript.jsonl"
    _write_transcript(
        t,
        {"result": "error", "api_error_status": 429, "is_error": True, "total_cost_usd": 0},
    )
    state = tmp_path / "vendor-refusal-state.jsonl"
    vendor_refusal.log_refusal("CURSOR-test-014", t, state)

    assert state.exists()
    lines = [l.strip() for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    assert len(lines) >= 1, f"attendu ≥ 1 ligne, obtenu {len(lines)}"

    record = json.loads(lines[0])
    assert "timestamp" in record, "champ 'timestamp' manquant"
    assert record.get("audit_id") == "CURSOR-test-014", "champ 'audit_id' incorrect"
    assert record.get("error_type") == "vendor_refusal", "champ 'error_type' incorrect"
    assert record.get("api_error_status") == 429, "champ 'api_error_status' incorrect"
    assert record.get("fallback_attempted") is False, "champ 'fallback_attempted' doit être False"


# --- Scénario 6 : mark_fallback_actor → marqueur forge-challenger-codex présent ---

def test_mark_fallback_actor_inserts_marker(tmp_path):
    """mark_fallback_actor() insère le marqueur forge-challenger-codex dans le fichier."""
    review = tmp_path / "CLAUDE-CURSOR-test-014.md"
    review.write_text(
        "---\naudit_id: CURSOR-test-014\n---\n\n# Revue\n\nTexte de la revue.\n",
        encoding="utf-8",
    )
    vendor_refusal.mark_fallback_actor(review)
    content = review.read_text(encoding="utf-8")
    assert "forge-challenger-codex" in content, (
        f"Le marqueur 'forge-challenger-codex' est absent du fichier de revue.\n"
        f"Contenu : {content[:200]}"
    )


def test_mark_fallback_actor_without_frontmatter(tmp_path):
    """mark_fallback_actor() fonctionne même sans frontmatter YAML."""
    review = tmp_path / "review.md"
    review.write_text("# Revue sans frontmatter\n\nTexte.\n", encoding="utf-8")
    vendor_refusal.mark_fallback_actor(review)
    content = review.read_text(encoding="utf-8")
    assert "forge-challenger-codex" in content


# --- Compteurs requis : mesure directe ---

def test_counter_classification_transcript_429(tmp_path):
    """Compteur classification_transcript_429 : attendu 'vendor_refusal'."""
    t = tmp_path / "t429.jsonl"
    _write_transcript(t, {"api_error_status": 429, "is_error": True, "total_cost_usd": 0})
    result = vendor_refusal.classify(t)
    assert result == "vendor_refusal", f"attendu 'vendor_refusal', obtenu {result!r}"


def test_counter_classification_transcript_succes(tmp_path):
    """Compteur classification_transcript_succes : attendu 'success'."""
    t = tmp_path / "tsuccess.jsonl"
    _write_transcript(t, {"result": "ok", "is_error": False})
    result = vendor_refusal.classify(t)
    assert result == "success", f"attendu 'success', obtenu {result!r}"


def test_counter_classification_transcript_autre(tmp_path):
    """Compteur classification_transcript_autre : attendu 'other_error'."""
    t = tmp_path / "tother.jsonl"
    _write_transcript(t, {"result": "error", "api_error_status": 500, "is_error": True})
    result = vendor_refusal.classify(t)
    assert result == "other_error", f"attendu 'other_error', obtenu {result!r}"


def test_counter_lignes_etat_refus_apres_log(tmp_path):
    """Compteur lignes_etat_refus_apres_log : ≥ 1 ligne JSON valide après log_refusal()."""
    t = tmp_path / "t.jsonl"
    t.write_text("", encoding="utf-8")
    state = tmp_path / "state.jsonl"
    vendor_refusal.log_refusal("CURSOR-test-014-counter", t, state)
    lines = [l.strip() for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    assert len(lines) >= 1, f"lignes_etat_refus_apres_log doit être ≥ 1, obtenu {len(lines)}"
    # Vérifie que chaque ligne est du JSON valide
    for line in lines:
        json.loads(line)


def test_counter_repli_codex_marque_acteur_reel(tmp_path):
    """Compteur repli_codex_marque_acteur_reel : 'forge-challenger-codex' dans le fichier."""
    review = tmp_path / "review-counter.md"
    review.write_text("---\nid: test\n---\n\n# Corps\n", encoding="utf-8")
    vendor_refusal.mark_fallback_actor(review)
    content = review.read_text(encoding="utf-8")
    assert "forge-challenger-codex" in content, "marqueur absent après mark_fallback_actor()"


# --- N1 : mark_fallback_attempted ---

def test_mark_fallback_attempted_updates_field(tmp_path):
    """mark_fallback_attempted() met fallback_attempted à True pour l'audit_id donné."""
    t = tmp_path / "t.jsonl"
    t.write_text("", encoding="utf-8")
    state = tmp_path / "state.jsonl"
    # Écrire d'abord une ligne de refus
    vendor_refusal.log_refusal("CURSOR-n1-test", t, state)
    # Vérifier que fallback_attempted est False initialement
    lines = [l for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    assert json.loads(lines[-1])["fallback_attempted"] is False
    # Appeler mark_fallback_attempted
    vendor_refusal.mark_fallback_attempted("CURSOR-n1-test", state)
    # Vérifier que fallback_attempted est maintenant True
    lines = [l for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    record = json.loads(lines[-1])
    assert record["fallback_attempted"] is True, "fallback_attempted doit être True après mark_fallback_attempted()"
    assert record["audit_id"] == "CURSOR-n1-test"


# --- B1 : test mécanique de la séquence 429 ---

def test_sequence_429_complete(tmp_path):
    """Preuve mécanique du cas 429 (critère de re-vérification B1) :
    1. classify() retourne 'vendor_refusal' sur un transcript 429
    2. log_refusal() ajoute une ligne à l'état avec fallback_attempted=False
    3. mark_fallback_attempted() passe fallback_attempted à True
    Ce test simule la séquence complète classify → log_refusal → (repli tenté) →
    mark_fallback_attempted, telle qu'elle serait exécutée par le workflow CI.
    """
    # Étape 1 : transcript 429 (modèle exact de l'audit source § 5.3)
    transcript = tmp_path / "challenge-transcript.jsonl"
    _write_transcript(
        transcript,
        {"type": "rate_limit_event", "rate_limit_info": {"status": "rejected", "rateLimitType": "five_hour"}},
        {
            "result": "You've hit your org's monthly spend limit · ask your admin to raise it at claude.ai/settings/usage",
            "api_error_status": 429,
            "is_error": True,
            "total_cost_usd": 0,
            "num_turns": 1,
        },
    )

    # Étape 2 : classify → doit retourner vendor_refusal
    classification = vendor_refusal.classify(transcript)
    assert classification == "vendor_refusal", f"classify doit retourner 'vendor_refusal', obtenu {classification!r}"

    # Étape 3 : log_refusal → ligne ajoutée, fallback_attempted=False
    state = tmp_path / "vendor-refusal-state.jsonl"
    vendor_refusal.log_refusal("CURSOR-b1-test", transcript, state)
    lines = [l for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    assert len(lines) >= 1, "état doit contenir au moins 1 ligne après log_refusal()"
    record = json.loads(lines[-1])
    assert record["audit_id"] == "CURSOR-b1-test"
    assert record["api_error_status"] == 429
    assert record["fallback_attempted"] is False

    # Étape 4 : après tentative de repli (simulée ici), mark_fallback_attempted
    vendor_refusal.mark_fallback_attempted("CURSOR-b1-test", state)
    lines = [l for l in state.read_text(encoding="utf-8").splitlines() if l.strip()]
    record = json.loads(lines[-1])
    assert record["fallback_attempted"] is True, "fallback_attempted doit être True après repli tenté"
