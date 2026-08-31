"""Le workflow courant accepte la roadmap comme les briefs existants."""

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent


def test_agents_authorizes_every_contributor_for_every_stage():
    text = (REPO_ROOT / "AGENTS.md").read_text(encoding="utf-8")
    assert "peut lire et modifier n'importe quel fichier" in text
    assert "planifier, coder, tester" in text
    assert "Une même personne peut" in text


def test_current_workflow_accepts_roadmap_or_existing_brief():
    for relative in ("AGENTS.md", "docs/MODE-EMPLOI.md", "ROADMAP.md"):
        text = (REPO_ROOT / relative).read_text(encoding="utf-8")
        assert "ROADMAP.md" in text or relative == "ROADMAP.md"
        assert "brief" in text.lower()


def test_automation_is_explicitly_optional():
    for relative in (
        "AGENTS.md",
        "docs/MODE-EMPLOI.md",
        "harness/README.md",
        "control-plane/README.md",
    ):
        text = (REPO_ROOT / relative).read_text(encoding="utf-8").lower()
        assert "facultati" in text, relative
