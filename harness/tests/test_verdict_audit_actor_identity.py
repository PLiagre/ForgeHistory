"""L'identité des auteurs ne peut jamais faire échouer le vérificateur.

Le fichier conserve la couverture des formes historiques d'auteur : même nom,
rôles différents, backend inconnu, plusieurs entrées et champs absents.
"""

import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "verdict_audit.py"


def run_audit(brief_dir: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(brief_dir)],
        capture_output=True,
        text=True,
    )


def write_brief(bd: Path, generator: str | None, reviewer: str | None) -> None:
    (bd / "deliverables").mkdir(parents=True)
    (bd / "brief.md").write_text(
        "# Brief\n\n**Authored**: 2020-01-01T00:00:00\n", encoding="utf-8"
    )
    (bd / "eval-rubric.md").write_text(
        "# Rubrique\n\n**Authored**: 2020-01-01T00:00:01\n", encoding="utf-8"
    )
    (bd / "deliverables" / "manifest.json").write_text(
        json.dumps({"files": [], "counters": [], "waivers": []}),
        encoding="utf-8",
    )
    generator_field = f"\n**Author**: {generator}\n" if generator else "\n"
    reviewer_field = f"\n**Author**: {reviewer}\n" if reviewer else "\n"
    (bd / "deliverables" / "generator-log.md").write_text(
        "# Journal\n" + generator_field, encoding="utf-8"
    )
    (bd / "verdict.md").write_text(
        "# Compte-rendu\n" + reviewer_field, encoding="utf-8"
    )


@pytest.mark.parametrize(
    ("generator", "reviewer"),
    [
        ("meme-auteur", "meme-auteur"),
        ("forge-generateur-codex", "forge-evaluateur-codex"),
        ("forge-generateur-inconnu", "forge-evaluateur-inconnu"),
        ("auteur-a", "auteur-b"),
        (None, None),
    ],
)
def test_identity_never_changes_the_result(tmp_path, generator, reviewer):
    bd = tmp_path / "brief"
    write_brief(bd, generator, reviewer)

    result = run_audit(bd)

    assert result.returncode == 0, result.stdout + result.stderr
    assert "[PASS] actor_identity_is_neutral" in result.stdout
    assert "RESULT: COHERENT" in result.stdout


def test_multiple_authors_are_informational_only(tmp_path):
    bd = tmp_path / "brief"
    write_brief(bd, "auteur-a", "auteur-a")
    with (bd / "deliverables" / "generator-log.md").open("a", encoding="utf-8") as stream:
        stream.write("\n**Author**: auteur-b\n")
    with (bd / "verdict.md").open("a", encoding="utf-8") as stream:
        stream.write("\n**Author**: auteur-b\n")

    result = run_audit(bd)

    assert result.returncode == 0
    assert "auteur-a" in result.stdout
    assert "auteur-b" in result.stdout


def test_longer_generator_list_is_informational_only(tmp_path):
    bd = tmp_path / "brief"
    write_brief(bd, "forge-generateur-korrigan", "forge-evaluateur-korrigan")
    with (bd / "deliverables" / "generator-log.md").open(
        "a", encoding="utf-8"
    ) as stream:
        stream.write("\n**Author**: second-auteur\n")

    result = run_audit(bd)

    assert result.returncode == 0
    assert "[PASS] actor_identity_is_neutral" in result.stdout


def test_longer_report_author_list_is_informational_only(tmp_path):
    bd = tmp_path / "brief"
    write_brief(bd, "meme-auteur", "meme-auteur")
    with (bd / "verdict.md").open("a", encoding="utf-8") as stream:
        stream.write("\n**Author**: autre-auteur\n")

    result = run_audit(bd)

    assert result.returncode == 0
    assert "[PASS] actor_identity_is_neutral" in result.stdout


def test_read_all_fields_keeps_document_order(tmp_path):
    sys.path.insert(0, str(SCRIPT.parent))
    import verdict_audit

    document = tmp_path / "document.md"
    document.write_text(
        "**Author**: auteur-a\n\n**Author**: auteur-b\n", encoding="utf-8"
    )
    assert verdict_audit.read_all_fields(document, "Author") == [
        "auteur-a",
        "auteur-b",
    ]
