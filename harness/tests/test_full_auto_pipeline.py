"""
Full-chain integration test -- SC17, brief 006 Lot 006c.

Proves the WHOLE loop closes with NO human input, end to end, in one
process: inbox audit -> review (Claude's contre-audit) -> auto-decide
(--policy auto, no owner accept/reject call anywhere in this file) ->
convert (audit -> brief seed) -> forge-run MOCK (a Générateur run is
simulated, never actually invoked -- this test does not spawn Claude/Cursor)
-> gate ACCEPT -> Évaluateur PASS (simulated the same way) ->
orchestrator.evaluateur_pass -> AUDIT_IMPLEMENTED -> AUDIT_VERIFIED ->
AUDIT_ARCHIVED.

Every step uses the SAME real modules the production pipeline uses
(audit_review, audit_decision.decide_auto, audit_convert,
pipeline.orchestrator, audit_ledger) against a disposable tmp_path --
nothing here is narrated or hand-typed as if it were the module's output.
The only thing "mocked" is the Générateur/Évaluateur agent invocation
itself (SC17 explicitly calls this "forge-run mock" in the brief) --
everything ledger/FSM/decision-related is the real code path.

Hard-won rule 4 (prove red first): `test_no_human_decision_call_anywhere_
in_this_file` statically greps this file's own source for
`audit_decision.decide(` (the human accept/reject entry point, as opposed
to `decide_auto`) -- if a future edit accidentally introduced a human call,
this test would catch it mechanically, not by someone remembering to look.
"""
from __future__ import annotations

import sys
from pathlib import Path

HARNESS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HARNESS))
import audit_convert  # noqa: E402
import audit_decision  # noqa: E402
import audit_ledger  # noqa: E402
import audit_review  # noqa: E402
from pipeline import orchestrator  # noqa: E402

AUDIT_ID = "CURSOR-e2e123-full-chain"

AUDIT_DOC = f"""---
audit_id: {AUDIT_ID}
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-05T20:00:00Z
status: PROPOSED
---
# corps fixture -- pas un audit reel
"""

# A real, filled review (no <<TODO>> left) with two CONFIRMED points -- this
# is what audit_review.record_challenge requires before it will log
# AUDIT_CHALLENGED, and what audit_decision.decide_auto needs to retain
# points and reach APPROVED.
REVIEW_FILLED = f"""---
review_of: {AUDIT_ID}
reviewer: claude-code
target_commit: 623118671dd98543a197b06415a240b9912999af
reviewed_at: 2026-08-05T20:05:00Z
---
| # | Point | Verdict | Preuve |
|---|---|---|---|
| 1 | budget non impose | CONFIRMED | supervisor.py existe, test_supervisor.py passe |
| 2 | split-check non bloquant | CONFIRMED | forge_run_preflight.py existe, tests passent |
"""


def _env(tmp_path: Path) -> dict:
    inbox = tmp_path / "inbox"
    inbox.mkdir()
    (inbox / f"{AUDIT_ID}.md").write_text(AUDIT_DOC, encoding="utf-8")
    return {
        "inbox": inbox,
        "reviews": tmp_path / "reviews",
        "decisions": tmp_path / "decisions",
        "briefs": tmp_path / "briefs",
        "ledger": tmp_path / "audit-ledger.jsonl",
    }


def test_full_chain_no_human_input(tmp_path):
    env = _env(tmp_path)

    # 1. Cursor auditor's finding lands in inbox -- proposal is implicit
    #    (auto_policy.yaml rule audit_pr_merge_ci_green: AUDIT_PROPOSED is
    #    optional). No ledger event needed to start.

    # 2. Claude challenger writes and records a REAL, filled review --
    #    scaffold() then a real fill (never a human typing into a template
    #    interactively; this test writes the filled text directly, exactly
    #    what a headless `claude` invocation in pipeline-challenge.yml would
    #    produce as output).
    scaffold_path = audit_review.write_scaffold(AUDIT_ID, inbox=env["inbox"], reviews_dir=env["reviews"])
    assert scaffold_path.exists()
    scaffold_path.write_text(REVIEW_FILLED, encoding="utf-8")  # the "fill" step
    challenged = audit_review.record_challenge(
        AUDIT_ID, inbox=env["inbox"], reviews_dir=env["reviews"], ledger_path=env["ledger"],
    )
    assert challenged["event"] == "AUDIT_CHALLENGED"

    # 3. AUTO-DECIDE -- --policy auto, NOT the human accept/reject path.
    approved = audit_decision.decide_auto(
        AUDIT_ID, inbox=env["inbox"], decisions_dir=env["decisions"], ledger_path=env["ledger"],
    )
    assert approved["event"] == "AUDIT_APPROVED"
    assert approved["retained_points"] == [1, 2]
    assert approved["actor"] == "policy:auto"

    # 4. Convert: APPROVED -> CONVERTED, brief seed written.
    converted = audit_convert.convert(
        AUDIT_ID, inbox=env["inbox"], briefs_dir=env["briefs"], ledger_path=env["ledger"],
    )
    assert converted["event"] == "AUDIT_CONVERTED"
    assert converted["briefs"][0]  # non-empty -- audit_convert's own guarantee
    seeded_dirs = list(env["briefs"].iterdir())
    assert len(seeded_dirs) == 1
    assert (seeded_dirs[0] / "brief.md").exists()
    assert (seeded_dirs[0] / "eval-rubric.md").exists()

    # 5. forge-run MOCK: no Claude/Cursor process is spawned. This
    #    represents claude-planificateur filling the seed, claude-developer
    #    producing deliverables, the mechanical gate ACCEPTing, and
    #    claude-evaluator PASSing -- all of which are real LLM/agent
    #    invocations in production and are explicitly OUT of scope for this
    #    test (SC17 names this "forge-run mock"). What IS real: the
    #    orchestrator event this outcome triggers next.
    mock_gate_accept = True
    mock_evaluateur_pass = True
    assert mock_gate_accept and mock_evaluateur_pass

    # 6. Évaluateur PASS -> orchestrator.evaluateur_pass -> IMPLEMENTED, VERIFIED.
    outcome = orchestrator.run_event(
        "evaluateur_pass", {"audit_id": AUDIT_ID}, ledger_path=env["ledger"],
    )
    assert outcome["action"] == "ledger_append_chain"
    events = [e["event"] for e in audit_ledger.read_events(env["ledger"])]
    assert events[-2:] == ["AUDIT_IMPLEMENTED", "AUDIT_VERIFIED"]

    # 7. Archive -- the terminal step of the happy path.
    archived = audit_ledger.append_event(AUDIT_ID, "AUDIT_ARCHIVED", ledger_path=env["ledger"], actor="policy:auto")
    assert archived["event"] == "AUDIT_ARCHIVED"

    final_events = [e["event"] for e in audit_ledger.read_events(env["ledger"])]
    assert final_events == [
        "AUDIT_CHALLENGED", "AUDIT_APPROVED", "AUDIT_CONVERTED",
        "AUDIT_IMPLEMENTED", "AUDIT_VERIFIED", "AUDIT_ARCHIVED",
    ]
    # Nothing after ARCHIVED -- terminal, per the Lot 006a FSM.
    import pytest
    with pytest.raises(audit_ledger.TransitionError):
        audit_ledger.append_event(AUDIT_ID, "AUDIT_STALE", ledger_path=env["ledger"])


def test_full_chain_never_calls_the_human_decision_path(tmp_path):
    """The FSM guarantee alone is not enough to prove "no human input" --
    a human accept/reject call could reach the SAME ledger states through a
    different door. This asserts the actual DECIDED-BY actor recorded on
    the APPROVED event is the policy, never "owner", by re-running the same
    chain and reading the ledger back -- not by trusting the caller's
    intent."""
    env = _env(tmp_path)
    scaffold_path = audit_review.write_scaffold(AUDIT_ID, inbox=env["inbox"], reviews_dir=env["reviews"])
    scaffold_path.write_text(REVIEW_FILLED, encoding="utf-8")
    audit_review.record_challenge(AUDIT_ID, inbox=env["inbox"], reviews_dir=env["reviews"], ledger_path=env["ledger"])
    audit_decision.decide_auto(AUDIT_ID, inbox=env["inbox"], decisions_dir=env["decisions"], ledger_path=env["ledger"])

    events = audit_ledger.read_events(env["ledger"])
    approved = [e for e in events if e["event"] == "AUDIT_APPROVED"][0]
    assert approved["actor"] == "policy:auto"
    assert approved["actor"] != "owner"


def test_no_human_decision_call_anywhere_in_this_file():
    """Static, mechanical proof this file's own chain never CALLS the human
    accept/reject entry point -- `audit_decision`'s single-word decision
    function, as opposed to its `_auto`-suffixed sibling -- a future edit
    that quietly added one would break this, rather than relying on a
    reviewer noticing. Parses this file's own AST (not a substring search,
    which would also match this docstring's prose) and checks every actual
    Call node's attribute name."""
    import ast

    tree = ast.parse(Path(__file__).read_text(encoding="utf-8"))
    human_path_calls = [
        node for node in ast.walk(tree)
        if isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and node.func.attr == "decide"  # exact match: NOT "decide_auto"
        and isinstance(node.func.value, ast.Name)
        and node.func.value.id == "audit_decision"
    ]
    assert human_path_calls == []
