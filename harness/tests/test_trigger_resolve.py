"""
Tests for harness/pipeline/trigger_resolve.py -- brief 008, Lot 008a.

Reproduces, in a way `py -m pytest` can replay without any GitHub Actions
context, the exact shape of incident run 31085883052: a push whose diff
names exactly one changed `architecture/reviews/*.md` file, whose audit_id
is already `AUDIT_ARCHIVED` (terminal). Pre-fix, `pipeline-orchestrate.yml`
built `event=review_recorded` from that file alone, with no ledger
consultation, and `audit_decision.decide_auto` -> `audit_ledger.append_event`
correctly refused (TransitionError) -- but the job died silently.

Hard-won rule 4 (prove red first): `test_terminal_audit_excluded_...` fails
against the pre-fix bash logic transplanted 1:1 (it would set
event=review_recorded for the terminal audit_id); it passes only because
`resolve_push` now reads the ledger first (SC2) and excludes the terminal
candidate (SC3) before any payload can be built.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"

sys.path.insert(0, str(HARNESS))
import audit_decision  # noqa: E402
import audit_ledger  # noqa: E402
from pipeline import orchestrator  # noqa: E402
from pipeline import trigger_resolve  # noqa: E402


def _write_ledger(tmp_path: Path, *lines: dict) -> Path:
    ledger = tmp_path / "ledger.jsonl"
    with ledger.open("w", encoding="utf-8") as fh:
        for line in lines:
            fh.write(json.dumps(line) + "\n")
    return ledger


# --- SC3: exact incident shape -- terminal audit, zero transition attempts ---


def test_terminal_audit_excluded_from_candidate_set(tmp_path):
    """The SC2 exclusion itself: a diff naming exactly one review file whose
    audit_id is already AUDIT_ARCHIVED resolves to event="" and names the
    audit_id + terminal state in a ::notice::, reproducing the exact shape
    of incident run 31085883052 (the review file there was
    CURSOR-FIXTURE-full-auto-demo, already AUDIT_ARCHIVED)."""
    ledger = _write_ledger(
        tmp_path,
        {
            "timestamp": "2026-08-08T21:00:00Z",
            "audit_id": "FIXTURE-008a-terminal",
            "event": "AUDIT_ARCHIVED",
        },
    )
    outcome = trigger_resolve.resolve_push(
        ["architecture/reviews/CLAUDE-FIXTURE-008a-terminal.md"], ledger_path=ledger
    )
    assert outcome.event == ""
    assert outcome.payload is None
    assert any(
        "FIXTURE-008a-terminal" in n and "AUDIT_ARCHIVED" in n for n in outcome.notices
    ), outcome.notices


def test_terminal_audit_regression_zero_transition_attempts(tmp_path, monkeypatch):
    """SC3's own required proof, not prose: fixture ledger with an
    AUDIT_ARCHIVED line for a fixture audit_id + fixture diff naming that
    same review file -> zero calls reach audit_decision.decide_auto /
    audit_ledger.append_event for that audit_id. Mirrors the real
    workflow's own `if: steps.resolve.outputs.event != ''` guard on the
    "Run orchestrator" step: orchestrator.run_event is never even invoked
    when resolve_push() returns event=""."""
    ledger = _write_ledger(
        tmp_path,
        {
            "timestamp": "2026-08-08T21:00:00Z",
            "audit_id": "FIXTURE-008a-terminal",
            "event": "AUDIT_ARCHIVED",
        },
    )
    outcome = trigger_resolve.resolve_push(
        ["architecture/reviews/CLAUDE-FIXTURE-008a-terminal.md"], ledger_path=ledger
    )
    assert outcome.event == ""

    decide_auto_calls: list[tuple] = []
    append_event_calls: list[tuple] = []
    monkeypatch.setattr(
        audit_decision,
        "decide_auto",
        lambda *a, **k: decide_auto_calls.append((a, k)),
    )
    monkeypatch.setattr(
        audit_ledger,
        "append_event",
        lambda *a, **k: append_event_calls.append((a, k)),
    )

    # Same guard the workflow's "Run orchestrator" step applies.
    if outcome.event:  # pragma: no cover -- must not be reached for this fixture
        orchestrator.run_event(outcome.event, outcome.payload, ledger_path=ledger)

    assert decide_auto_calls == [], "decide_auto must never be called for a terminal audit_id"
    assert append_event_calls == [], "append_event must never be called for a terminal audit_id"


# --- SC4: non-terminal candidate still dispatches, not a blanket skip ---


def test_non_terminal_audit_still_resolves_to_review_recorded(tmp_path):
    """A diff naming exactly one changed review file whose audit_id is
    genuinely non-terminal (only AUDIT_CHALLENGED, no prior AUDIT_ARCHIVED
    line) still resolves to event=review_recorded -- the fix is not a
    blanket skip."""
    ledger = _write_ledger(
        tmp_path,
        {
            "timestamp": "2026-08-08T21:00:00Z",
            "audit_id": "FIXTURE-008a-nonterminal",
            "event": "AUDIT_CHALLENGED",
        },
    )
    outcome = trigger_resolve.resolve_push(
        ["architecture/reviews/CLAUDE-FIXTURE-008a-nonterminal.md"], ledger_path=ledger
    )
    assert outcome.event == "review_recorded"
    assert outcome.payload == {"audit_id": "FIXTURE-008a-nonterminal"}


def test_non_terminal_dispatch_still_reaches_decide_auto(tmp_path, monkeypatch):
    """SC4's own required proof: the transition IS attempted -- resolving
    to review_recorded and reaching orchestrator.py's dispatch, which calls
    audit_decision.decide_auto for the non-terminal audit_id, exactly as
    before this fix."""
    ledger = _write_ledger(
        tmp_path,
        {
            "timestamp": "2026-08-08T21:00:00Z",
            "audit_id": "FIXTURE-008a-nonterminal",
            "event": "AUDIT_CHALLENGED",
        },
    )
    outcome = trigger_resolve.resolve_push(
        ["architecture/reviews/CLAUDE-FIXTURE-008a-nonterminal.md"], ledger_path=ledger
    )
    assert outcome.event == "review_recorded"

    decide_auto_calls: list[str] = []

    def _fake_decide_auto(audit_id, **kwargs):
        decide_auto_calls.append(audit_id)
        return {"event": "AUDIT_APPROVED", "audit_id": audit_id, "actor": "policy:auto"}

    monkeypatch.setattr(audit_decision, "decide_auto", _fake_decide_auto)

    result = orchestrator.run_event(outcome.event, outcome.payload, ledger_path=ledger)

    assert decide_auto_calls == ["FIXTURE-008a-nonterminal"], (
        "the transition must be attempted for a non-terminal audit_id"
    )
    assert result["action"] == "decide_auto"


# --- SC5: ambiguous-diff fallback unchanged (0 or >1 non-terminal candidates) ---


def test_zero_changed_files_falls_back_to_skip(tmp_path):
    ledger = _write_ledger(tmp_path)
    outcome = trigger_resolve.resolve_push([], ledger_path=ledger)
    assert outcome.event == ""
    assert any("0 reviews" in n for n in outcome.notices)


def test_two_non_terminal_changed_files_falls_back_to_skip(tmp_path):
    ledger = _write_ledger(
        tmp_path,
        {"timestamp": "2026-08-08T21:00:00Z", "audit_id": "FIXTURE-008a-a", "event": "AUDIT_CHALLENGED"},
        {"timestamp": "2026-08-08T21:00:01Z", "audit_id": "FIXTURE-008a-b", "event": "AUDIT_CHALLENGED"},
    )
    outcome = trigger_resolve.resolve_push(
        [
            "architecture/reviews/CLAUDE-FIXTURE-008a-a.md",
            "architecture/reviews/CLAUDE-FIXTURE-008a-b.md",
        ],
        ledger_path=ledger,
    )
    assert outcome.event == ""
    assert any("2 non-terminal" in n for n in outcome.notices)


# --- SC6: audit_decision.py's FSM guard is untouched by this lot ---


def test_audit_decision_module_still_raises_transition_error_on_terminal(tmp_path):
    """Independent of trigger_resolve.py entirely: calling
    audit_decision.decide_auto directly on an audit whose ledger state is
    not AUDIT_CHALLENGED (e.g. AUDIT_ARCHIVED) still raises DecisionError --
    the guard-rail this lot's SC6 promises not to touch is still doing its
    own job underneath the new trigger layer."""
    ledger = _write_ledger(
        tmp_path,
        {"timestamp": "2026-08-08T21:00:00Z", "audit_id": "FIXTURE-008a-guard", "event": "AUDIT_ARCHIVED"},
    )
    with pytest.raises(audit_decision.DecisionError):
        audit_decision.decide_auto("FIXTURE-008a-guard", ledger_path=ledger)


# --- SC1: entry point is callable standalone, and the CLI wires it end to end ---


def test_resolve_prioritises_explicit_payload_over_diff(tmp_path):
    ledger = _write_ledger(tmp_path)
    outcome = trigger_resolve.resolve(
        in_event="gate_reject",
        in_payload=json.dumps({"brief_dir": "harness/queue/briefs/999-x", "reject_streak": 3}),
        changed_review_files=["architecture/reviews/CLAUDE-should-be-ignored.md"],
        ledger_path=ledger,
    )
    assert outcome.event == "gate_reject"
    assert outcome.payload == {"brief_dir": "harness/queue/briefs/999-x", "reject_streak": 3}


def test_resolve_prioritises_explicit_audit_id_over_diff(tmp_path):
    ledger = _write_ledger(tmp_path)
    outcome = trigger_resolve.resolve(
        in_event="review_recorded",
        in_audit_id="FIXTURE-manual",
        changed_review_files=["architecture/reviews/CLAUDE-should-be-ignored.md"],
        ledger_path=ledger,
    )
    assert outcome.event == "review_recorded"
    assert outcome.payload == {"audit_id": "FIXTURE-manual"}


def test_resolve_falls_through_to_push_diff_when_no_manual_input(tmp_path):
    ledger = _write_ledger(
        tmp_path,
        {"timestamp": "2026-08-08T21:00:00Z", "audit_id": "FIXTURE-008a-nonterminal", "event": "AUDIT_CHALLENGED"},
    )
    outcome = trigger_resolve.resolve(
        changed_review_files=["architecture/reviews/CLAUDE-FIXTURE-008a-nonterminal.md"],
        ledger_path=ledger,
    )
    assert outcome.event == "review_recorded"
    assert outcome.payload == {"audit_id": "FIXTURE-008a-nonterminal"}


def test_cli_help_exits_zero():
    import subprocess

    script = HARNESS / "pipeline" / "trigger_resolve.py"
    r = subprocess.run([sys.executable, str(script), "--help"], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    r2 = subprocess.run([sys.executable, str(script), "resolve", "--help"], capture_output=True, text=True)
    assert r2.returncode == 0, r2.stderr
    assert "--in-event" in r2.stdout


def test_cli_end_to_end_writes_github_output(tmp_path):
    """Replays the incident scenario through the actual CLI subprocess,
    stdin-to-github-output, exactly as pipeline-orchestrate.yml's resolve
    step invokes it -- proving SC1 (no GitHub Actions context needed)."""
    import subprocess

    ledger = _write_ledger(
        tmp_path,
        {"timestamp": "2026-08-08T21:00:00Z", "audit_id": "FIXTURE-008a-terminal", "event": "AUDIT_ARCHIVED"},
    )
    github_output = tmp_path / "github_output.txt"
    github_output.write_text("", encoding="utf-8")
    script = HARNESS / "pipeline" / "trigger_resolve.py"

    r = subprocess.run(
        [
            sys.executable,
            str(script),
            "resolve",
            "--in-event", "",
            "--in-audit-id", "",
            "--in-payload", "",
            "--ledger", str(ledger),
            "--github-output", str(github_output),
        ],
        input="architecture/reviews/CLAUDE-FIXTURE-008a-terminal.md\n",
        capture_output=True,
        text=True,
    )
    assert r.returncode == 0, r.stderr
    assert "::notice::" in r.stdout
    assert "FIXTURE-008a-terminal" in r.stdout
    output_text = github_output.read_text(encoding="utf-8")
    assert output_text.strip() == "event="
