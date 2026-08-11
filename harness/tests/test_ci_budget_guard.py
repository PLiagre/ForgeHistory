"""Tests du plafond budgétaire CI — brief 009, lot 009b."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import pytest

from harness.backends import ledger as backend_ledger
from harness.pipeline.ci_budget_guard import (
    BudgetGuardError,
    BudgetExceededError,
    current_month_total_usd,
    precheck_monthly_budget,
    record_invocation,
)


NOW = datetime(2026, 8, 10, 12, 0, tzinfo=timezone.utc)


def _write_ledger(path: Path, entries: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(entry, sort_keys=True) + "\n" for entry in entries),
        encoding="utf-8",
    )


def _write_config(path: Path, *, newline: str = "\n") -> bytes:
    content = newline.join(
        [
            "# commentaire conservé octet pour octet",
            "mode: full_auto_decision_only",
            "max_forge_run_iterations: 3",
            "",
        ]
    ).encode("utf-8")
    path.write_bytes(content)
    return content


def test_monthly_precheck_refuses_at_or_above_cap(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    config_path = tmp_path / "config.yaml"
    _write_config(config_path)
    _write_ledger(
        ledger_path,
        [
            {"timestamp": "2026-08-01T00:00:00Z", "step": "a", "usd": 125.0},
            {"timestamp": "2026-08-09T00:00:00Z", "step": "b", "usd": 75.0},
        ],
    )

    with pytest.raises(BudgetExceededError, match="200"):
        precheck_monthly_budget(
            ledger_path=ledger_path,
            config_path=config_path,
            monthly_cap_usd=200.0,
            now=NOW,
        )


def test_monthly_precheck_proceeds_below_cap(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    config_path = tmp_path / "config.yaml"
    before = _write_config(config_path)
    _write_ledger(
        ledger_path,
        [{"timestamp": "2026-08-01T00:00:00Z", "step": "challenge", "usd": 199.99}],
    )

    total = precheck_monthly_budget(
        ledger_path=ledger_path,
        config_path=config_path,
        monthly_cap_usd=200.0,
        now=NOW,
    )

    assert total == pytest.approx(199.99)
    assert config_path.read_bytes() == before


def test_budget_refusal_changes_only_mode_line_bytes(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    config_path = tmp_path / "config.yaml"
    before = _write_config(config_path, newline="\r\n")
    _write_ledger(
        ledger_path,
        [{"timestamp": "2026-08-10T00:00:00Z", "step": "challenge", "usd": 250.0}],
    )

    with pytest.raises(BudgetExceededError):
        precheck_monthly_budget(
            ledger_path=ledger_path,
            config_path=config_path,
            monthly_cap_usd=200.0,
            now=NOW,
        )

    after = config_path.read_bytes()
    before_lines = before.splitlines(keepends=True)
    after_lines = after.splitlines(keepends=True)
    assert len(before_lines) == len(after_lines)
    changed = [
        index
        for index, (before_line, after_line) in enumerate(zip(before_lines, after_lines))
        if before_line != after_line
    ]
    assert changed == [1]
    assert before_lines[1] == b"mode: full_auto_decision_only\r\n"
    assert after_lines[1] == b"mode: manual\r\n"


def test_record_marks_over_cap_for_challenge_cap(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    usage = {
        "claude-sonnet-5": {
            "calls": 1,
            "in": 0,
            "cache_write": 0,
            "cache_read": 0,
            "out": 400_000,
        }
    }

    entry = record_invocation(
        step_name="challenge",
        usage_by_model=usage,
        per_invocation_cap_usd=5.0,
        ledger_path=ledger_path,
        timestamp=NOW,
    )

    assert entry["usd"] == pytest.approx(backend_ledger.price_of("claude-sonnet-5", usage["claude-sonnet-5"]))
    assert entry["cap_usd"] == 5.0
    assert entry["over_cap"] is True


def test_record_marks_over_cap_for_forge_run_cap(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    usage = {
        "claude-sonnet-5": {
            "calls": 1,
            "in": 0,
            "cache_write": 0,
            "cache_read": 0,
            "out": 4_000_000,
        }
    }

    entry = record_invocation(
        step_name="forge-run",
        usage_by_model=usage,
        per_invocation_cap_usd=50.0,
        ledger_path=ledger_path,
        timestamp=NOW,
    )

    assert entry["cap_usd"] == 50.0
    assert entry["over_cap"] is True


def test_prior_month_entries_do_not_count_toward_current_month(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    _write_ledger(
        ledger_path,
        [
            {"timestamp": "2026-07-31T23:59:59Z", "step": "old", "usd": 199.0},
            {"timestamp": "2026-08-01T00:00:00Z", "step": "current", "usd": 10.0},
        ],
    )

    assert current_month_total_usd(ledger_path, now=NOW) == pytest.approx(10.0)


def test_record_appends_without_rewriting_existing_entries(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    original = b'{"timestamp":"2026-08-01T00:00:00Z","step":"old","usd":1.0}\n'
    ledger_path.write_bytes(original)
    usage = {
        "claude-haiku-4-5": {
            "calls": 1,
            "in": 1_000,
            "cache_write": 0,
            "cache_read": 0,
            "out": 1_000,
        }
    }

    record_invocation(
        step_name="challenge",
        usage_by_model=usage,
        per_invocation_cap_usd=5.0,
        ledger_path=ledger_path,
        timestamp=NOW,
    )

    assert ledger_path.read_bytes().startswith(original)
    assert len(ledger_path.read_text(encoding="utf-8").splitlines()) == 2


def test_record_refuses_unpriced_model_instead_of_assuming_zero(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    usage = {
        "modele-inconnu": {
            "calls": 1,
            "in": 1,
            "cache_write": 0,
            "cache_read": 0,
            "out": 1,
        }
    }

    with pytest.raises(ValueError, match="sans prix publié"):
        record_invocation(
            step_name="challenge",
            usage_by_model=usage,
            per_invocation_cap_usd=5.0,
            ledger_path=ledger_path,
            timestamp=NOW,
        )
    assert not ledger_path.exists()


def test_malformed_ledger_refuses_precheck_instead_of_ignoring_cost(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    config_path = tmp_path / "config.yaml"
    before = _write_config(config_path)
    ledger_path.write_text('{"timestamp":"2026-08-01T00:00:00Z","usd":199}\nnot-json\n', encoding="utf-8")

    with pytest.raises(BudgetGuardError, match="ligne 2"):
        precheck_monthly_budget(
            ledger_path=ledger_path,
            config_path=config_path,
            monthly_cap_usd=200.0,
            now=NOW,
        )
    assert config_path.read_bytes() == before


def test_ambiguous_config_refuses_kill_switch_rewrite(tmp_path):
    ledger_path = tmp_path / "ci-budget-ledger.jsonl"
    config_path = tmp_path / "config.yaml"
    before = b"mode: full_auto_decision_only\nmode: manual\n"
    config_path.write_bytes(before)
    _write_ledger(
        ledger_path,
        [{"timestamp": "2026-08-10T00:00:00Z", "step": "challenge", "usd": 200.0}],
    )

    with pytest.raises(BudgetGuardError, match="2 trouvée"):
        precheck_monthly_budget(
            ledger_path=ledger_path,
            config_path=config_path,
            monthly_cap_usd=200.0,
            now=NOW,
        )
    assert config_path.read_bytes() == before
