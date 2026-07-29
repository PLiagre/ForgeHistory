"""
Repo-level invariant: exactly one document says what an agent must do for a
brief -- the brief.md file itself. Any other .md may point to it, never
paraphrase its actual structural headings outside a documentation code
fence (agent definitions legitimately show the brief.md *schema* inside a
```markdown fence; that's a template, not a paraphrase of a specific brief).
"""
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent

FORBIDDEN_HEADINGS = [
    "## Success Conditions",
    "## Non-Goals",
]

CODE_FENCE = re.compile(r"```.*?```", re.DOTALL)


def strip_code_fences(text: str) -> str:
    return CODE_FENCE.sub("", text)


def find_violations() -> list[tuple[str, str]]:
    violations = []
    for md_file in REPO_ROOT.rglob("*.md"):
        if ".git" in md_file.parts:
            continue
        if md_file.name == "brief.md":
            continue  # the one legitimate home for these headings
        text = md_file.read_text(encoding="utf-8", errors="ignore")
        stripped = strip_code_fences(text)
        for heading in FORBIDDEN_HEADINGS:
            if heading in stripped:
                violations.append((str(md_file.relative_to(REPO_ROOT)), heading))
    return violations


def test_no_paraphrased_brief_headings_outside_brief_md():
    violations = find_violations()
    assert not violations, (
        "Found brief.md's structural headings restated outside brief.md "
        f"(or outside a code fence): {violations}. A second document may "
        "point to the brief, never paraphrase it."
    )
