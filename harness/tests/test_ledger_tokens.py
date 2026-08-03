"""
Tests for the `tokens` subcommand of harness/backends/ledger.py.

Hard-won rule 4: prove red first. Every claim the report makes gets a
fixture that would break it if the code stopped holding -- in particular
the two claims that make this tool *honest* rather than merely numeric:

  1. An unknown model is counted in tokens and named as unpriced. It is
     never silently priced at zero, because a silently-zero model turns
     the dollar total into a quiet understatement.
  2. A replayed assistant message is counted once. Resumed sessions
     re-write earlier assistant turns into the transcript; counting them
     twice would inflate every figure downstream.

Mixes black-box subprocess runs (the real CLI contract) with direct calls
into the module's pure functions, which have no I/O beyond the file read.
"""
import json
import subprocess
import sys
from pathlib import Path

import pytest

BACKENDS = Path(__file__).resolve().parent.parent / "backends"
SCRIPT = BACKENDS / "ledger.py"

sys.path.insert(0, str(BACKENDS))
import ledger  # noqa: E402


def usage_line(
    model: str,
    *,
    msg_id: str = "msg_1",
    request_id: str = "req_1",
    cache_read: int = 0,
    cache_write: int = 0,
    inp: int = 0,
    out: int = 0,
) -> str:
    return json.dumps(
        {
            "type": "assistant",
            "requestId": request_id,
            "message": {
                "id": msg_id,
                "model": model,
                "usage": {
                    "input_tokens": inp,
                    "cache_creation_input_tokens": cache_write,
                    "cache_read_input_tokens": cache_read,
                    "output_tokens": out,
                },
            },
        }
    )


def write_transcripts(root: Path, agent_lines: list[str], *, brief: str | None,
                      role: str = "forge-generateur") -> None:
    """A minimal but structurally real transcript tree: one session + one agent."""
    session = "sess-0001"
    (root / session / "subagents").mkdir(parents=True)
    (root / f"{session}.jsonl").write_text("", encoding="utf-8")

    prompt = {"type": "user", "message": {"role": "user", "content": "do the thing"}}
    if brief:
        prompt["message"]["content"] = f"Run harness/queue/briefs/{brief} now."
    agent = root / session / "subagents" / "agent-aaa.jsonl"
    agent.write_text(
        "\n".join([json.dumps(prompt), *agent_lines]) + "\n", encoding="utf-8"
    )
    (root / session / "subagents" / "agent-aaa.meta.json").write_text(
        json.dumps({"agentType": role}), encoding="utf-8"
    )


def run_tokens(root: Path, *extra: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "tokens", "--transcripts", str(root), *extra],
        capture_output=True,
        text=True,
    )


# --- token counting -----------------------------------------------------


def test_counts_tokens_per_model(tmp_path: Path):
    root = tmp_path / "t"
    write_transcripts(
        root,
        [usage_line("claude-sonnet-5", cache_read=1_000_000, out=1000)],
        brief="003-port-unity-game",
    )
    per_model = ledger.scan_transcript(root / "sess-0001" / "subagents" / "agent-aaa.jsonl")
    assert per_model["claude-sonnet-5"]["calls"] == 1
    assert per_model["claude-sonnet-5"]["cache_read"] == 1_000_000
    assert per_model["claude-sonnet-5"]["out"] == 1000


def test_replayed_message_counted_once(tmp_path: Path):
    """RED without the (id, requestId) dedup: this would report 2 calls."""
    root = tmp_path / "t"
    duplicate = usage_line("claude-sonnet-5", cache_read=500_000)
    write_transcripts(root, [duplicate, duplicate], brief=None)
    per_model = ledger.scan_transcript(root / "sess-0001" / "subagents" / "agent-aaa.jsonl")
    assert per_model["claude-sonnet-5"]["calls"] == 1
    assert per_model["claude-sonnet-5"]["cache_read"] == 500_000


def test_distinct_messages_both_counted(tmp_path: Path):
    """The dedup must not over-collapse: two real calls stay two."""
    root = tmp_path / "t"
    write_transcripts(
        root,
        [
            usage_line("claude-sonnet-5", msg_id="a", request_id="r1", cache_read=10),
            usage_line("claude-sonnet-5", msg_id="b", request_id="r2", cache_read=20),
        ],
        brief=None,
    )
    per_model = ledger.scan_transcript(root / "sess-0001" / "subagents" / "agent-aaa.jsonl")
    assert per_model["claude-sonnet-5"]["calls"] == 2
    assert per_model["claude-sonnet-5"]["cache_read"] == 30


def test_malformed_lines_are_skipped_not_fatal(tmp_path: Path):
    root = tmp_path / "t"
    write_transcripts(
        root,
        ["{not json", "", json.dumps({"type": "user"}), usage_line("claude-opus-5", out=5)],
        brief=None,
    )
    per_model = ledger.scan_transcript(root / "sess-0001" / "subagents" / "agent-aaa.jsonl")
    assert per_model["claude-opus-5"]["calls"] == 1


# --- pricing honesty ----------------------------------------------------


def test_known_model_priced_from_the_published_table():
    counts = {"calls": 1, "in": 0, "cache_write": 0, "cache_read": 1_000_000, "out": 0}
    # Sonnet 5 cache-read is $0.30/Mtok -> exactly $0.30 for 1M tokens.
    assert ledger.price_of("claude-sonnet-5", counts) == pytest.approx(0.30)


def test_unknown_model_is_unpriced_not_zero():
    counts = {"calls": 1, "in": 0, "cache_write": 0, "cache_read": 1_000_000, "out": 0}
    assert ledger.price_of("claude-from-the-future", counts) is None


def test_unpriced_model_is_named_and_excluded_from_usd(tmp_path: Path):
    """RED if an unknown model silently prices at 0: usd would be 0.30 and
    the unpriced list empty, i.e. a total that quietly understates spend."""
    unit = {
        "models": {
            "claude-sonnet-5": {
                "calls": 1, "in": 0, "cache_write": 0, "cache_read": 1_000_000, "out": 0
            },
            "claude-from-the-future": {
                "calls": 1, "in": 0, "cache_write": 0, "cache_read": 9_000_000, "out": 0
            },
        }
    }
    summary = ledger.summarize(unit)
    assert summary["usd"] == pytest.approx(0.30)          # only the priced model
    assert summary["unpriced"] == ["claude-from-the-future"]  # but it is named
    assert summary["cache_read"] == 10_000_000            # and its tokens still counted


def test_zero_token_model_is_not_reported_as_a_pricing_gap():
    """`<synthetic>` bookkeeping entries carry no tokens -- not a real gap."""
    unit = {"models": {"<synthetic>": {"calls": 1, "in": 0, "cache_write": 0,
                                       "cache_read": 0, "out": 0}}}
    assert ledger.summarize(unit)["unpriced"] == []


# --- brief attribution --------------------------------------------------


def test_brief_read_from_the_agents_own_prompt(tmp_path: Path):
    root = tmp_path / "t"
    write_transcripts(root, [usage_line("claude-sonnet-5")], brief="004-polish-visuel")
    agent = root / "sess-0001" / "subagents" / "agent-aaa.jsonl"
    assert ledger.brief_of(agent) == "004-polish-visuel"


def test_no_brief_mention_attributes_to_nothing(tmp_path: Path):
    """Never guess a brief from wall-clock proximity -- report None."""
    root = tmp_path / "t"
    write_transcripts(root, [usage_line("claude-sonnet-5")], brief=None)
    agent = root / "sess-0001" / "subagents" / "agent-aaa.jsonl"
    assert ledger.brief_of(agent) is None


def test_brief_mentioned_only_deep_in_the_log_is_not_attributed(tmp_path: Path):
    """A brief path that shows up mid-run is a file the agent touched, not
    the brief it was spawned for. Only the opening prompt counts."""
    root = tmp_path / "t"
    deep = json.dumps({"type": "user", "message": {
        "content": "see harness/queue/briefs/999-unrelated-brief"}})
    write_transcripts(
        root,
        [usage_line("claude-sonnet-5")] * ledger.BRIEF_SCAN_LINES + [deep],
        brief=None,
    )
    agent = root / "sess-0001" / "subagents" / "agent-aaa.jsonl"
    assert ledger.brief_of(agent) is None


# --- CLI contract -------------------------------------------------------


def test_cli_reports_role_brief_and_cursor_caveat(tmp_path: Path):
    root = tmp_path / "t"
    write_transcripts(
        root,
        [usage_line("claude-sonnet-5", cache_read=2_000_000, out=1000)],
        brief="003-port-unity-game",
    )
    result = run_tokens(root)
    assert result.returncode == 0, result.stderr
    assert "forge-generateur" in result.stdout
    assert "003-port-unity-game" in result.stdout
    assert "NOT observable" in result.stdout  # the Cursor caveat is never dropped


def test_cli_json_is_machine_readable(tmp_path: Path):
    root = tmp_path / "t"
    write_transcripts(
        root, [usage_line("claude-opus-5", cache_read=1_000_000)], brief=None
    )
    result = run_tokens(root, "--json")
    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["total"]["calls"] == 1
    assert payload["prices_as_of"] == ledger.PRICES_AS_OF


def test_cli_missing_directory_says_so_and_exits_nonzero(tmp_path: Path):
    """Absent transcripts must not read as 'zero spend'."""
    result = run_tokens(tmp_path / "does-not-exist")
    assert result.returncode == 1
    assert "No Claude transcripts found" in result.stdout
    assert "$" not in result.stdout  # no fabricated total


def test_report_subcommand_still_works():
    """The token work must not break the original invocation-count report."""
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "report"], capture_output=True, text=True
    )
    assert result.returncode == 0, result.stderr
