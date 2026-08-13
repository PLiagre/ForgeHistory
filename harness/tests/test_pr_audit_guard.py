"""
Tests pour harness/pipeline/pr_audit_guard.py -- brief 014.

Couvre les 8 scénarios requis par SC1 :
  1. Aucun audit dans l'inbox → sortie 0
  2. Un audit cible la PR, état PROPOSED implicite (aucune ligne au ledger) → sortie 1
  3. Un audit cible la PR, état CHALLENGED → sortie 1
  4. Un audit cible la PR, état APPROVED → sortie 0
  5. Un audit cible la PR, état ARCHIVED → sortie 0
  6. Deux audits ciblent la PR, l'un CHALLENGED, l'autre APPROVED → sortie 1
  7. Un audit cible par target_commit (7 premiers caractères du SHA) → détection correcte
  8. Un audit ne cible pas la PR (branch et commit différents) → sortie 0

Hard-won rule 4 (prove red first) : le test test_exits_1_when_audit_challenged
est celui utilisé pour la preuve rouge (paire A du brief).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"
sys.path.insert(0, str(HARNESS))

from pipeline import pr_audit_guard  # noqa: E402


def _make_audit(inbox: Path, audit_id: str, target_branch: str = "", target_commit: str = "") -> None:
    """Crée un fichier d'audit minimal dans l'inbox synthétique."""
    lines = ["---", f"audit_id: {audit_id}", "auditor: cursor-cloud"]
    if target_branch:
        lines.append(f"target_branch: {target_branch}")
    if target_commit:
        lines.append(f"target_commit: {target_commit}")
    lines += ["status: PROPOSED", "---", "# corps", ""]
    (inbox / f"{audit_id}.md").write_text("\n".join(lines), encoding="utf-8")


def _write_ledger(ledger: Path, *events: dict) -> None:
    """Écrit des événements dans un ledger synthétique."""
    with ledger.open("w", encoding="utf-8") as fh:
        for evt in events:
            fh.write(json.dumps(evt) + "\n")


# --- Scénario 1 : inbox vide ---

def test_exits_0_when_inbox_empty(tmp_path):
    """Aucun fichier dans l'inbox → sortie 0 (contrôle vert)."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    ledger = tmp_path / "ledger.jsonl"
    ledger.write_text("", encoding="utf-8")
    code = pr_audit_guard.check(
        head_branch="ma-branche",
        head_commit="abc1234def",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 0


# --- Scénario 2 : audit ciblant la PR, état PROPOSED implicite (aucune ligne au ledger) ---

def test_exits_1_when_audit_proposed_implicit(tmp_path):
    """Un audit cible la PR, aucune ligne au ledger (PROPOSED implicite) → sortie 1."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    _make_audit(inbox, "CURSOR-abc1234-test", target_branch="ma-branche")
    ledger = tmp_path / "ledger.jsonl"
    ledger.write_text("", encoding="utf-8")
    code = pr_audit_guard.check(
        head_branch="ma-branche",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 1


# --- Scénario 3 : audit ciblant la PR, état CHALLENGED → sortie 1 ---

def test_exits_1_when_audit_challenged(tmp_path):
    """Un audit cible la PR, état CHALLENGED → sortie 1 (utilisé pour la preuve rouge)."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-test014-challenged"
    _make_audit(inbox, audit_id, target_branch="feature-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_id, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
    )
    code = pr_audit_guard.check(
        head_branch="feature-branch",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 1


# --- Scénario 4 : audit ciblant la PR, état APPROVED → sortie 0 ---

def test_exits_0_when_audit_approved(tmp_path):
    """Un audit cible la PR, état APPROVED → sortie 0 (adjugé)."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-test014-approved"
    _make_audit(inbox, audit_id, target_branch="feature-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_id, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_APPROVED", "timestamp": "2026-08-13T02:00:00Z"},
    )
    code = pr_audit_guard.check(
        head_branch="feature-branch",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 0


# --- Scénario 5 : audit ciblant la PR, état ARCHIVED → sortie 0 ---

def test_exits_0_when_audit_archived(tmp_path):
    """Un audit cible la PR, état ARCHIVED → sortie 0 (adjugé, terminal)."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-test014-archived"
    _make_audit(inbox, audit_id, target_branch="feature-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_id, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_REJECTED", "timestamp": "2026-08-13T02:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_ARCHIVED", "timestamp": "2026-08-13T03:00:00Z"},
    )
    code = pr_audit_guard.check(
        head_branch="feature-branch",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 0


# --- Scénario 6 : deux audits, l'un CHALLENGED l'autre APPROVED → sortie 1 ---

def test_exits_1_when_one_challenged_one_approved(tmp_path):
    """Deux audits ciblent la PR : CHALLENGED et APPROVED → sortie 1 (le premier suffit)."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_challenged = "CURSOR-test014-mix-challenged"
    audit_approved = "CURSOR-test014-mix-approved"
    _make_audit(inbox, audit_challenged, target_branch="feature-branch")
    _make_audit(inbox, audit_approved, target_branch="feature-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_challenged, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_challenged, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_APPROVED", "timestamp": "2026-08-13T02:00:00Z"},
    )
    code = pr_audit_guard.check(
        head_branch="feature-branch",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 1


# --- Scénario 7 : ciblage par target_commit (7 premiers caractères) ---

def test_exits_1_when_matched_by_commit(tmp_path):
    """Un audit cible la PR par target_commit (7 premiers chars du SHA) → détection correcte."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-test014-commit-match"
    full_commit = "abc1234e714a9ff4d1b3c739859a9357884d5f81"
    _make_audit(inbox, audit_id, target_commit=full_commit)
    ledger = tmp_path / "ledger.jsonl"
    ledger.write_text("", encoding="utf-8")
    code = pr_audit_guard.check(
        head_branch="other-branch",
        head_commit="abc1234feedbeef",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 1


# --- Scénario 8 : audit ne ciblant pas la PR ---

def test_exits_0_when_audit_targets_other_branch(tmp_path):
    """Un audit cible une autre branche et un autre commit → sortie 0."""
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    _make_audit(inbox, "CURSOR-test014-other", target_branch="other-branch", target_commit="zzzzzzz1234")
    ledger = tmp_path / "ledger.jsonl"
    ledger.write_text("", encoding="utf-8")
    code = pr_audit_guard.check(
        head_branch="ma-branche",
        head_commit="abc1234feedbeef",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 0


# --- Compteurs requis : mesure directe depuis les tests ---

def test_counters_code_sortie_avec_audit_non_adjuge(tmp_path):
    """
    Mesure du compteur code_sortie_guard_pr_avec_audit_non_adjuge :
    doit être 1 sur fixture avec audit CHALLENGED ciblant la PR.
    """
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_id = "CURSOR-test014-counter-challenged"
    _make_audit(inbox, audit_id, target_branch="target-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_id, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_id, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
    )
    code = pr_audit_guard.check(
        head_branch="target-branch",
        head_commit="zzzzzzz",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 1, f"attendu 1, obtenu {code}"


def test_counters_code_sortie_sans_audit(tmp_path):
    """
    Mesure du compteur code_sortie_guard_pr_sans_audit :
    doit être 0 sur fixture avec inbox vide.
    """
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    ledger = tmp_path / "ledger.jsonl"
    ledger.write_text("", encoding="utf-8")
    code = pr_audit_guard.check(
        head_branch="any-branch",
        head_commit="abc1234",
        inbox_path=inbox,
        ledger_path=ledger,
    )
    assert code == 0, f"attendu 0, obtenu {code}"


# --- Mesure des compteurs audits_ciblant_pr et audits_non_adjuges_ciblant_pr ---

def test_counters_audits_ciblant_pr(tmp_path):
    """
    Mesure des compteurs audits_ciblant_pr et audits_non_adjuges_ciblant_pr
    sur une fixture non vide.
    """
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    audit_challenged = "CURSOR-014-count-challenged"
    audit_approved = "CURSOR-014-count-approved"
    audit_other = "CURSOR-014-count-other"
    _make_audit(inbox, audit_challenged, target_branch="feature-branch")
    _make_audit(inbox, audit_approved, target_branch="feature-branch")
    _make_audit(inbox, audit_other, target_branch="different-branch")
    ledger = tmp_path / "ledger.jsonl"
    _write_ledger(
        ledger,
        {"audit_id": audit_challenged, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_challenged, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_PROPOSED", "timestamp": "2026-08-13T00:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_CHALLENGED", "timestamp": "2026-08-13T01:00:00Z"},
        {"audit_id": audit_approved, "event": "AUDIT_APPROVED", "timestamp": "2026-08-13T02:00:00Z"},
    )
    # Compte les audits ciblants manuellement pour le compteur
    head_branch = "feature-branch"
    head_commit = "zzzzzzz"
    all_files = list(inbox.glob("*.md"))
    ciblants = 0
    non_adjuges = 0
    import sys as _sys
    _sys.path.insert(0, str(HARNESS))
    import audit_ledger as _al
    for af in all_files:
        fm = pr_audit_guard._parse_frontmatter(af.read_text(encoding="utf-8"))
        if pr_audit_guard._targets_pr(fm, head_branch, head_commit):
            ciblants += 1
            state = _al.current_state_for(af.stem, ledger)
            if not pr_audit_guard._is_adjudicated(state):
                non_adjuges += 1
    assert ciblants >= 1, f"audits_ciblant_pr doit être ≥ 1, obtenu {ciblants}"
    assert non_adjuges >= 1, f"audits_non_adjuges_ciblant_pr doit être ≥ 1, obtenu {non_adjuges}"
