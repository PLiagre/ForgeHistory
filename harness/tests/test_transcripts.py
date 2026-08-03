"""
Tests for harness/transcripts.py -- the shared transcript reader.

These exist because of a real miss, and they are shaped by it. The budget's
own tests passed while the counter was wrong by a factor of 5.46, because
their fixture wrote ONE record per assistant turn carrying N tool_use
blocks. The real transcript writes **one record per content block**, all
sharing (message.id, requestId). The fixture encoded the assumption instead
of testing it, so the bug was invisible until an external audit counted the
tool_result blocks independently.

Every fixture below therefore uses the real sibling-record shape, and the
tool_use/tool_result cross-check is asserted rather than assumed.
"""
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO_ROOT / "harness"))
import transcripts  # noqa: E402


def sibling_turn(msg_id: str, request_id: str, blocks: list[dict],
                 usages: list[dict]) -> list[str]:
    """One assistant turn as the real transcript writes it: one JSONL record
    per content block, every record repeating the same id/requestId."""
    assert len(blocks) == len(usages)
    lines = []
    for block, usage in zip(blocks, usages):
        lines.append(json.dumps({
            "type": "assistant",
            "requestId": request_id,
            "message": {"id": msg_id, "model": "claude-sonnet-5",
                        "usage": usage, "content": [block]},
        }))
    return lines


def usage(cache_read=1000, out=5):
    return {"input_tokens": 1, "cache_creation_input_tokens": 0,
            "cache_read_input_tokens": cache_read, "output_tokens": out}


def write(tmp_path: Path, lines: list[str]) -> Path:
    path = tmp_path / "agent-x.jsonl"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return path


def test_tool_calls_counted_across_sibling_records(tmp_path: Path):
    """THE regression test. Three tool calls split across three records that
    share one message id must count as three, not one."""
    lines = sibling_turn(
        "msg_1", "req_1",
        [{"type": "thinking"}, {"type": "tool_use"}, {"type": "tool_use"}],
        [usage(out=5), usage(out=5), usage(out=202)],
    )
    path = write(tmp_path, lines)
    tool_use, _ = transcripts.count_tool_calls(path)
    assert tool_use == 2, "sibling records were collapsed -- the 5.46x bug"


def test_tool_use_and_tool_result_agree(tmp_path: Path):
    """The independent cross-check the audit used. A parse that drifts from
    the format breaks the equality even when neither side looks wrong."""
    lines = sibling_turn("msg_1", "req_1",
                         [{"type": "tool_use"}, {"type": "tool_use"}],
                         [usage(), usage()])
    lines.append(json.dumps({"type": "user", "message": {"content": [
        {"type": "tool_result"}, {"type": "tool_result"}]}}))
    tool_use, tool_result = transcripts.count_tool_calls(write(tmp_path, lines))
    assert tool_use == tool_result == 2


def test_one_billed_request_per_message_not_per_record(tmp_path: Path):
    """Summing usage across siblings would double-count the bill."""
    path = write(tmp_path, sibling_turn(
        "msg_1", "req_1",
        [{"type": "thinking"}, {"type": "tool_use"}, {"type": "tool_use"}],
        [usage(cache_read=300_000, out=5), usage(cache_read=300_000, out=5),
         usage(cache_read=300_000, out=202)],
    ))
    per_request = transcripts.usage_by_request(path)
    assert len(per_request) == 1
    assert per_request[0][1]["cache_read_input_tokens"] == 300_000


def test_output_tokens_taken_from_the_last_sibling(tmp_path: Path):
    """output_tokens is cumulative across the group. Taking the first record
    undercounted output ~3x on a real agent (56,524 vs 177,494)."""
    path = write(tmp_path, sibling_turn(
        "msg_1", "req_1",
        [{"type": "thinking"}, {"type": "tool_use"}, {"type": "tool_use"}],
        [usage(out=5), usage(out=5), usage(out=202)],
    ))
    assert transcripts.per_model_usage(path)["claude-sonnet-5"]["out"] == 202


def test_distinct_turns_are_not_collapsed(tmp_path: Path):
    """The dedup must not over-correct: two real requests stay two."""
    lines = sibling_turn("msg_1", "req_1", [{"type": "tool_use"}], [usage(out=10)])
    lines += sibling_turn("msg_2", "req_2", [{"type": "tool_use"}], [usage(out=20)])
    counts = transcripts.per_model_usage(write(tmp_path, lines))["claude-sonnet-5"]
    assert counts["calls"] == 2
    assert counts["out"] == 30


def test_ambiguous_brief_returns_every_candidate(tmp_path: Path):
    """Picking by mtime is what produced OK / tool_calls: 0 on the repo's
    most expensive brief. The reader must hand back all candidates and let
    the caller refuse to guess."""
    root = tmp_path / "t"
    for name, calls in (("sess-a", 1), ("sess-b", 3)):
        subdir = root / name / "subagents"
        subdir.mkdir(parents=True)
        lines = [json.dumps({"type": "user", "message": {
            "content": "run harness/queue/briefs/003-port-unity-game"}})]
        lines += sibling_turn("m", "r", [{"type": "tool_use"}] * calls,
                              [usage()] * calls)
        (subdir / "agent-x.jsonl").write_text("\n".join(lines) + "\n", encoding="utf-8")

    found = transcripts.agent_transcripts_for("003-port-unity-game", root)
    assert len(found) == 2, "a caller cannot refuse to guess if it only sees one"


def test_malformed_lines_are_skipped_not_fatal(tmp_path: Path):
    path = write(tmp_path, ["{not json", "", json.dumps({"type": "user"}),
                            *sibling_turn("m", "r", [{"type": "tool_use"}], [usage()])])
    assert transcripts.count_tool_calls(path)[0] == 1
