"""Tests du générateur de tableau de bord Hermes (hermes/dashboard.py).

Le tableau de bord est une VUE générée depuis les sources de vérité du
dépôt (ledgers, config, briefs) — ces tests prouvent sur un dépôt-fixture
jetable que la vue reflète fidèlement ce que disent les fichiers, qu'une
donnée optionnelle absente produit « non disponible » (jamais une
invention), et qu'une ligne de ledger corrompue n'abat pas la génération.
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
    (root / "harness" / "queue" / "briefs" / "001-fixture").mkdir(parents=True)
    (root / "architecture").mkdir(parents=True)

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
            "{ligne corrompue volontaire",
        ]) + "\n",
        encoding="utf-8",
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
    (root / "harness" / "queue" / "briefs" / "001-fixture" / "verdict.md").write_text(
        "corps\n\nVERDICT: REJECT\n\npuis correction\n\nVERDICT: ACCEPT\n", encoding="utf-8"
    )
    # Un audit déposé dans l'inbox SANS ligne au ledger : la convention du
    # dépôt le traite comme AUDIT_PROPOSED implicite -- la vue doit le lister.
    (root / "architecture" / "inbox").mkdir()
    (root / "architecture" / "inbox" / "CURSOR-ccc-inbox-seul.md").write_text(
        "---\naudit_id: CURSOR-ccc-inbox-seul\nstatus: PROPOSED\n---\n# corps\n", encoding="utf-8"
    )
    return root


def test_dashboard_reflete_les_sources_de_verite(tmp_path):
    from datetime import datetime, timezone

    root = _fixture_root(tmp_path)
    contenu = dashboard.generer(root, now=datetime(2026, 8, 12, 10, 0, tzinfo=timezone.utc))

    # Le mode vient de config.yaml, pas d'une constante.
    assert "`full_auto`" in contenu
    # L'audit ouvert est listé avec son état humain ; le clos est compté, pas listé.
    assert "CURSOR-bbb-ouvert" in contenu
    assert "attend le contre-audit" in contenu
    # L'audit présent dans l'inbox mais absent du ledger est listé aussi
    # (AUDIT_PROPOSED implicite), jamais passé sous silence.
    assert "CURSOR-ccc-inbox-seul" in contenu
    assert "CURSOR-aaa-clos" not in contenu.split("## La boucle d'audit")[1].split("##")[0].replace(
        "boucle(s) close(s)", "")
    # Budget du mois courant : la ligne d'août est comptée.
    assert "1.25 USD" in contenu
    # Le dernier verdict tracé du brief fixture est ACCEPT (le REJECT antérieur
    # ne masque pas la correction).
    assert "dernier verdict tracé : ACCEPT" in contenu
    # Usage backends : 2 runs codex, 1 run claude.
    assert "| codex | 2 |" in contenu
    assert "| claude | 1 |" in contenu


def test_donnees_optionnelles_absentes_disent_non_disponible(tmp_path):
    root = _fixture_root(tmp_path)
    contenu = dashboard.generer(root)
    # Sans données GitHub/Cursor fournies, la vue le DIT au lieu d'inventer.
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
    (root / "hermes").mkdir()
    code = dashboard.main(["--repo-root", str(root)])
    assert code == 0
    assert (root / "hermes" / "DASHBOARD.md").exists()
