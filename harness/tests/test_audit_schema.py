"""
Tests for harness/audit_schema.py -- the CI schema gate for audits.

Hard-won rule 4: prove red first. Each test breaks one schema rule and
proves the validator catches it:

  1. A *_authorized flag set to true is rejected -- the one rule that stops
     an auditor from self-granting implementation rights.
  2. A short/dirty target_commit is rejected -- freshness needs a real SHA.
  3. An audit_id that disagrees with the filename is rejected -- the ledger
     join key must be trustworthy.
  4. A missing required field is rejected.
  5. The two real Cursor audits in the repo pass, so the gate is calibrated
     to reality, not just to fixtures.
"""
import subprocess
import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
REPO_ROOT = HARNESS.parent
SCRIPT = HARNESS / "audit_schema.py"

sys.path.insert(0, str(HARNESS))
import audit_schema  # noqa: E402

GOOD = """---
audit_id: CURSOR-abc-topic
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---
# corps
"""


def _write(tmp_path, text, name="CURSOR-abc-topic.md"):
    p = tmp_path / name
    p.write_text(text, encoding="utf-8")
    return p


def test_good_audit_has_no_errors(tmp_path):
    assert audit_schema.validate_audit(_write(tmp_path, GOOD)) == []


def test_self_authorised_flag_rejected(tmp_path):
    bad = GOOD.replace("implementation_authorized: false", "implementation_authorized: true")
    errors = audit_schema.validate_audit(_write(tmp_path, bad))
    assert any("implementation_authorized" in e for e in errors)


def test_short_commit_rejected(tmp_path):
    bad = GOOD.replace("target_commit: 623118671dd98543a197b06415a240b9912999af", "target_commit: 6231186")
    errors = audit_schema.validate_audit(_write(tmp_path, bad))
    assert any("target_commit" in e for e in errors)


def test_audit_id_must_match_filename(tmp_path):
    errors = audit_schema.validate_audit(_write(tmp_path, GOOD, name="CURSOR-different-name.md"))
    assert any("filename stem" in e for e in errors)


def test_status_must_be_proposed(tmp_path):
    bad = GOOD.replace("status: PROPOSED", "status: APPROVED")
    errors = audit_schema.validate_audit(_write(tmp_path, bad))
    assert any("status" in e for e in errors)


def test_missing_field_rejected(tmp_path):
    bad = "\n".join(l for l in GOOD.splitlines() if not l.startswith("auditor:"))
    errors = audit_schema.validate_audit(_write(tmp_path, bad))
    assert any("auditor" in e for e in errors)


def test_no_frontmatter_rejected(tmp_path):
    errors = audit_schema.validate_audit(_write(tmp_path, "# just prose\n"))
    assert errors and "frontmatter" in errors[0]


def test_real_cursor_audits_pass():
    """The two audits actually in the repo must validate -- the gate is
    calibrated to reality, not only to fixtures."""
    inbox = REPO_ROOT / "architecture" / "inbox"
    results = audit_schema.validate_inbox(inbox)
    assert results, "expected the two real Cursor audits present in inbox"
    for name, errors in results.items():
        assert errors == [], f"{name} failed schema: {errors}"


def test_cli_exit_zero_on_valid(tmp_path):
    _write(tmp_path, GOOD)
    r = subprocess.run([sys.executable, str(SCRIPT), "--inbox", str(tmp_path)],
                       capture_output=True, text=True)
    assert r.returncode == 0, r.stdout + r.stderr


def test_cli_exit_one_on_invalid(tmp_path):
    _write(tmp_path, GOOD.replace("ci_changes_authorized: false", "ci_changes_authorized: true"))
    r = subprocess.run([sys.executable, str(SCRIPT), "--inbox", str(tmp_path)],
                       capture_output=True, text=True)
    assert r.returncode == 1
    assert "FAIL" in r.stdout
