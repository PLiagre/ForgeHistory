"""Tests du générateur de tableau de bord Hermes (hermes/dashboard.py).

Le tableau de bord est une VUE générée depuis les sources de vérité du
dépôt — ces tests prouvent sur un dépôt-fixture jetable que la vue
reflète le mode, le budget et les items Hermes OPEN, qu'une donnée
optionnelle absente produit « non disponible », et qu'un audit déjà
décidé n'est jamais présenté comme « à faire ».
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "hermes"))
import dashboard  # noqa: E402


def _fixture_root(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    (root / "harness" / "pipeline").mkdir(parents=True)
    (root / "harness" / "queue").mkdir(parents=True)
    (root / "architecture").mkdir(parents=True)
    (root / "hermes" / "propositions").mkdir(parents=True)
    (root / "hermes" / "requests").mkdir(parents=True)

    (root / "harness" / "pipeline" / "config.yaml").write_text(
        "mode: full_auto\nmax_forge_run_iterations: 3\n", encoding="utf-8"
    )
    (root / "architecture" / "audit-ledger.jsonl").write_text(
        "\n".join([
            json.dumps({"timestamp": "2026-08-01T10:00:00Z", "audit_id": "CURSOR-aaa-clos",
                        "event": "AUDIT_STALE"}),
            json.dumps({"timestamp": "2026-08-01T10:01:00Z", "audit_id": "CURSOR-aaa-clos",
                        "event": "AUDIT_ARCHIVED"}),
            json.dumps({"timestamp": "2026-08-12T09:00:00Z", "audit_id": "CURSOR-bbb-ouvert",
                        "event": "AUDIT_PROPOSED"}),
            json.dumps({"timestamp": "2026-08-12T11:41:00Z", "audit_id": "CURSOR-cdc683f-decide",
                        "event": "AUDIT_APPROVED"}),
            "{ligne corrompue volontaire",
        ]) + "\n",
        encoding="utf-8",
    )
    (root / "architecture" / "decisions").mkdir()
    (root / "architecture" / "decisions" / "DECISION-CURSOR-cdc683f-decide.md").write_text(
        "décision déjà écrite\n", encoding="utf-8"
    )
    (root / "harness" / "pipeline" / "ci-budget-ledger.jsonl").write_text(
        json.dumps({"timestamp": "2026-08-12T09:30:00Z", "step": "challenge:x", "usd": 1.25}) + "\n",
        encoding="utf-8",
    )
    (root / "harness" / "queue" / "cost-ledger.jsonl").write_text(
        "\n".join([
            json.dumps({"timestamp": "2026-08-11T08:00:00", "backend": "codex",
                        "brief": "b", "event": "generator-run"}),
            json.dumps({"timestamp": "2026-08-11T09:00:00", "backend": "codex",
                        "brief": "b", "event": "generator-run"}),
            json.dumps({"timestamp": "2026-08-10T08:00:00", "backend": "claude",
                        "brief": "b", "event": "generator-run"}),
        ]) + "\n",
        encoding="utf-8",
    )
    (root / "hermes" / "propositions" / "PROPOSITION-20260812-ouverte.md").write_text(
        "---\nstatus: OPEN\nkind: proposition\n---\n# Proposition encore ouverte\n",
        encoding="utf-8",
    )
    (root / "hermes" / "propositions" / "PROPOSITION-20260811-close.md").write_text(
        "---\nstatus: CLOSED\nkind: proposition\n---\n# Proposition close\n",
        encoding="utf-8",
    )
    (root / "hermes" / "requests" / "DEMANDE-20260812-close.md").write_text(
        "---\nstatus: CLOSED\nkind: demande\n---\n# Demande déjà close\n",
        encoding="utf-8",
    )
    return root


def test_dashboard_reflete_les_sources_de_verite(tmp_path):
    from datetime import datetime, timezone

    root = _fixture_root(tmp_path)
    contenu = dashboard.generer(root, now=datetime(2026, 8, 12, 10, 0, tzinfo=timezone.utc))

    assert "`full_auto`" in contenu
    assert "1.25 USD" in contenu
    assert "| codex | 2 |" in contenu
    assert "| claude | 1 |" in contenu
    assert "PROPOSITION-20260812-ouverte.md" in contenu
    assert "Proposition encore ouverte" in contenu
    assert "PROPOSITION-20260811-close.md" not in contenu
    assert "DEMANDE-20260812-close.md" not in contenu


def test_dashboard_ne_presente_plus_les_audits_comme_a_faire(tmp_path):
    root = _fixture_root(tmp_path)
    contenu = dashboard.generer(root)

    assert "Convertir l'audit" not in contenu
    assert "/forge-audit-convert" not in contenu
    assert "CURSOR-bbb-ouvert" not in contenu
    assert "CURSOR-cdc683f-decide" not in contenu
    assert "CURSOR-aaa-clos" not in contenu
    assert "architecture/README.md" in contenu
    assert "historique" in contenu


def test_dashboard_rien_n_attend_sans_item_open(tmp_path):
    root = _fixture_root(tmp_path)
    (root / "hermes" / "propositions" / "PROPOSITION-20260812-ouverte.md").write_text(
        "---\nstatus: CLOSED\n---\n# close\n", encoding="utf-8"
    )
    contenu = dashboard.generer(root)
    assert "Rien n'attend." in contenu


def test_donnees_optionnelles_absentes_disent_non_disponible(tmp_path):
    root = _fixture_root(tmp_path)
    contenu = dashboard.generer(root)
    assert contenu.count("Non disponible dans cette génération") == 2


def test_donnees_optionnelles_presentes_sont_tabulees(tmp_path):
    root = _fixture_root(tmp_path)
    runs = tmp_path / "runs.json"
    runs.write_text(json.dumps([{"createdAt": "2026-08-12T09:59:19Z", "name": "pipeline-challenge",
                                 "event": "push", "headBranch": "master", "conclusion": "success"}]),
                    encoding="utf-8")
    agents = tmp_path / "agents.json"
    agents.write_text(json.dumps({"agents": [{"name": "Audit du commit master", "status": "RUNNING",
                                              "source": "api", "branchName": "cursor/audit-x"}]}),
                      encoding="utf-8")
    contenu = dashboard.generer(root, runs_json=runs, agents_json=agents)
    assert "pipeline-challenge" in contenu
    assert "Audit du commit master" in contenu
    assert "lancé automatiquement par la CI" in contenu


def test_generation_ecrit_le_fichier(tmp_path):
    root = _fixture_root(tmp_path)
    (root / "hermes").mkdir(exist_ok=True)
    code = dashboard.main(["--repo-root", str(root)])
    assert code == 0
    assert (root / "hermes" / "DASHBOARD.md").exists()
