"""Surveille la frontière d'auto-fusion réellement appliquée par merge-bot.

Brief 010, lot 010c, SC12. Les règles sont extraites du workflow lui-même ;
les assertions ci-dessous figent la frontière approuvée aujourd'hui. Si le
workflow est élargi, ce test devient rouge et exige une décision explicite.
"""
from __future__ import annotations

from pathlib import Path

import pytest

from harness.merge_bot_policy import MergeBotPolicyError, load_merge_bot_policy


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "merge-bot.yml"


def _assert_current_boundary(policy) -> None:
    assert policy.branch_prefixes == ("cursor/", "forge-bot/")
    assert policy.allowed_path_prefixes == (
        "architecture/inbox/",
        "architecture/reviews/",
        "harness/queue/briefs/*/feedback/",
    )


def test_merge_bot_current_boundary_is_extracted_from_workflow_itself():
    policy = load_merge_bot_policy(WORKFLOW)
    _assert_current_boundary(policy)


@pytest.mark.parametrize("content", ["", "name: merge-bot\njobs:\n"])
def test_empty_or_truncated_workflow_is_refused(tmp_path, content):
    workflow = tmp_path / "merge-bot.yml"
    workflow.write_text(content, encoding="utf-8")
    with pytest.raises(MergeBotPolicyError):
        load_merge_bot_policy(workflow)


def test_adding_a_branch_prefix_makes_the_boundary_assertion_red(tmp_path):
    original = WORKFLOW.read_text(encoding="utf-8")
    widened = original.replace(
        "startsWith(github.head_ref, 'forge-bot/')",
        "startsWith(github.head_ref, 'forge-bot/') || "
        "startsWith(github.head_ref, 'codex/')",
    )
    assert widened != original

    workflow = tmp_path / "merge-bot.yml"
    workflow.write_text(widened, encoding="utf-8")
    with pytest.raises(AssertionError):
        _assert_current_boundary(load_merge_bot_policy(workflow))


def test_adding_an_allowed_path_makes_the_boundary_assertion_red(tmp_path):
    original = WORKFLOW.read_text(encoding="utf-8")
    widened = original.replace(
        "architecture/inbox/|architecture/reviews/|",
        "architecture/inbox/|architecture/reviews/|docs/|",
    )
    assert widened != original

    workflow = tmp_path / "merge-bot.yml"
    workflow.write_text(widened, encoding="utf-8")
    with pytest.raises(AssertionError):
        _assert_current_boundary(load_merge_bot_policy(workflow))


def test_policy_evaluation_uses_extracted_branch_path_and_deny_rules():
    policy = load_merge_bot_policy(WORKFLOW)

    assert policy.is_automergeable(
        "cursor/audit-example",
        ["architecture/inbox/CURSOR-example.md"],
    )
    assert not policy.is_automergeable(
        "codex/code-change",
        ["architecture/inbox/CURSOR-example.md"],
    )
    assert not policy.is_automergeable(
        "cursor/audit-example",
        ["harness/tests/test_example.py"],
    )
    assert not policy.is_automergeable(
        "cursor/audit-example",
        [".github/workflows/merge-bot.yml"],
    )
