#!/usr/bin/env py
"""
harness/pipeline/full_auto_mode_guard.py -- Lot 009a, brief 009 SC1/SC2.

Validates `harness/pipeline/config.yaml`'s `mode:` value against the split
introduced by ADR-0007 (docs/adr/0007-full-auto-mode-split.md): the two
values valid going forward are `manual` (unchanged) and
`full_auto_decision_only` (new, decision-and-fusion loop only). The bare,
unqualified literal `full_auto` is refused FAIL-CLOSED for as long as
`.github/workflows/pipeline-forge-run.yml` still contains the literal
string `TODO(operator` -- i.e. for as long as forge-run itself stays an
unwired stub, `full_auto` would overstate what this repository actually
runs unattended (see brief 009's "World-Terms Requirement").

This is deliberately NOT hardcoded to refuse `full_auto` forever: the check
re-reads `pipeline-forge-run.yml` itself every call rather than caching a
verdict, so once a future lot removes that TODO string for real,
`full_auto` becomes valid again with no code change here -- exercised by
`harness/tests/test_mode_guard.py::test_full_auto_accepted_once_forgerun_
wired` against a FIXTURE copy of the workflow file with the string removed
(never the real repository file, which stays a stub until a separate,
future brief changes it). That fixture proves the conditional branch is
reachable; it does not prove that an agent invocation exists.

This module answers ONE question: "is this `mode:` value legal, given the
repository's current wiring state?" It does NOT gate a running workflow's
invocation step at execution time (a different concern, load-bearing
`mode` for `pipeline-challenge.yml`, tracked in brief 009 Lot 009c SC15 --
out of this lot's scope, not duplicated here).

Fail-closed discipline (brief 009's own instruction, restated as code, not
prose): an unreadable/missing workflow file, or any mode value this module
does not recognise, is refused -- never silently accepted as if it were
`manual`.

Iteration-2 correction (feedback-009a.md blocker B2): iteration 1 accepted
`full_auto` on ANY workflow file that merely lacked the stub marker,
including an empty file, a whitespace-only file, or a file truncated
before the marker's own position -- because "the marker string is absent"
is not the same claim as "this file positively proves forge-run is
wired". Absence of a bad sign is not presence of a good one. The check
below is inverted accordingly: `full_auto` is accepted only when the file
(a) has non-whitespace content, (b) contains, as REAL lines (see
Iteration-3 correction just below -- not merely as a substring anywhere,
including inside a comment), the three structural markers `jobs:`,
`runs-on:` and `steps:` -- a minimal, dependency-free proxy for "this is
not an empty/corrupted/truncated file", chosen instead of a YAML parse
per this brief's own Non-Goal against adding PyYAML to any production
import), AND (c) does not still carry the stub marker. Any one of these
failing is a refusal, in the same "cannot prove forge-run is wired"
family as the pre-existing I/O refusal below. `UnicodeDecodeError` (a
non-UTF-8 workflow file) is now caught alongside `OSError` so every
refusal reaches a caller as the module's own published `ModeGuardError`
contract, never a raw traceback of an unrelated exception type.

Iteration-3 correction (feedback-009a-002.md blocker C3): iteration 2's
own docstring overclaimed what the two-substring check above actually
established. It called the result "positive structural evidence [of] a
real, complete GitHub Actions workflow", said the check "positively
proves forge-run is wired", and claimed "there is no fourth,
silently-permissive outcome". None of that was true: a file containing
only the commented-out lines `# jobs:` / `# runs-on:`, and a file
truncated immediately after a real `runs-on:` line (before any `steps:`
section), were BOTH silently ACCEPTED by iteration 2's check -- reproduced
against the unmodified iteration-2 module and fixed by two changes below
(see `harness/tests/test_mode_guard.py`'s
`test_comment_only_structure_markers_refused_full_auto_fail_closed` and
`test_truncated_after_runs_on_before_steps_refused_full_auto_fail_closed`,
each proved red against the unmodified iteration-2 module first):
(1) each structure marker must now start a real, stripped LINE of the
file, not merely occur as a substring anywhere (closes the comment-only
case); (2) `steps:` was added as a third required marker alongside
`jobs:`/`runs-on:` (closes the truncated-before-steps case), since the
real `pipeline-forge-run.yml` this guard exists to check against always
has one.

This still does NOT make the check semantically prove forge-run is
wired, and this docstring no longer claims it does. A complete,
well-formed workflow with real, uncommented `jobs:`/`runs-on:`/`steps:`
sections whose only step is `run: echo no-agent` -- never invoking any
agent at all -- still passes this check
(`test_structurally_complete_workflow_without_real_invocation_still_
accepted_known_heuristic_limit`, deliberately NOT fixed, deliberately
committed as a documented limit rather than left silently uncovered).
Feedback-009a-002.md offered two ways to close the gap between the
iteration-2 claim and the iteration-2 code: make the check semantically
true, or narrow the claim to match the (weaker) check. This module takes
the FIRST option wherever it is achievable without semantic parsing
(closing the two corruption/truncation shapes above), and the SECOND
option for the one residual case that cannot be closed without proving a
workflow step genuinely invokes an agent -- which is exactly Lot 009c
SC14's own job (the real, headless `claude` CLI invocation), not this
guard's. Building that proof here would pre-empt 009c's own work, which
brief 009's Non-Goals reserve to it ("Lot 009c must not re-implement...
logic already built by 009a" cuts both ways: 009a must not pre-empt
009c's own semantic-wiring proof either). This module's true, narrow
claim, restated without embellishment: it rejects a workflow file that is
missing, unreadable, non-UTF-8, empty, whitespace-only, or that lacks a
real (non-commented) `jobs:`/`runs-on:`/`steps:` line each -- nothing
more. A caller must NOT read `full_auto` being accepted as confirmation
that a real agent invocation exists anywhere in the target workflow.

Usage:
  from harness.pipeline.full_auto_mode_guard import validate_mode, ModeGuardError
  validate_mode("full_auto_decision_only")  # returns None, no raise
  validate_mode("full_auto")                # raises ModeGuardError while
                                             # pipeline-forge-run.yml still
                                             # contains "TODO(operator"
"""
from __future__ import annotations

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
DEFAULT_FORGE_RUN_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "pipeline-forge-run.yml"

FORGE_RUN_STUB_MARKER = "TODO(operator"

# Minimal, dependency-free (no PyYAML) corruption/truncation filter: a
# file lacking any of these, AS A REAL LINE (not merely as a substring
# anywhere, including inside a comment -- iteration-3 fix for
# feedback-009a-002.md blocker C3), is refused as not evidently a real,
# complete workflow body. This is NOT semantic proof a workflow step
# invokes an agent -- see the iteration-3 module docstring correction
# above for the residual, deliberately-documented limit this does not
# close.
REQUIRED_WORKFLOW_STRUCTURE_MARKERS = ("jobs:", "runs-on:", "steps:")


def _workflow_has_real_line_starting_with(text: str, marker: str) -> bool:
    """True iff a stripped line starts with a YAML-like ``key:`` token.

    The character after ``marker`` must be whitespace, ``#`` (an inline
    comment), or the end of the line. Thus ``jobs:garbage`` does not count
    merely because it shares a prefix with ``jobs:``. This remains a
    syntax-level heuristic, not semantic validation of a GitHub Actions
    workflow or proof that an agent is invoked.
    """
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped.startswith(marker):
            continue
        suffix = stripped[len(marker) :]
        if not suffix or suffix[0].isspace() or suffix.startswith("#"):
            return True
    return False

VALID_ALWAYS = {"manual", "full_auto_decision_only"}
CONDITIONALLY_VALID = {"full_auto"}


class ModeGuardError(Exception):
    """Raised when `mode:` is not a legal value given current wiring state.
    Fail-closed: every code path below either returns None (accepted) or
    raises this -- there is no third, silently-permissive outcome."""


def validate_mode(mode: str, forge_run_workflow: Path | str | None = None) -> None:
    """Validate a `mode:` scalar. Raises ModeGuardError on any refusal;
    returns None (no value) when the mode is accepted. Never returns a
    boolean that a caller could accidentally ignore -- refusal is always an
    exception, per brief 009 SC1's "non-zero exit or raised exception"
    requirement."""
    if not isinstance(mode, str) or not mode.strip():
        raise ModeGuardError(f"mode value missing or not a string: {mode!r} -- fail closed.")
    mode = mode.strip()

    if mode in VALID_ALWAYS:
        return

    if mode in CONDITIONALLY_VALID:
        path = Path(forge_run_workflow) if forge_run_workflow is not None else DEFAULT_FORGE_RUN_WORKFLOW
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            # Unreadable/missing/undecodable workflow file: cannot prove
            # forge-run is wired, so refuse -- fail closed, never assume
            # "must be fine". UnicodeDecodeError is caught alongside
            # OSError (iteration-2 fix) so this reaches the caller as the
            # module's own published ModeGuardError contract, not a raw
            # traceback of an unrelated exception type.
            raise ModeGuardError(
                f"mode: full_auto refused -- could not read {path} to verify "
                f"forge-run's wiring state ({exc}); fail-closed."
            ) from exc

        # Corruption/truncation checks, in order, each its own distinct
        # refusal reason -- absence of the stub marker alone proves nothing
        # (an empty file, a whitespace-only file, and a file truncated
        # before the marker's own position ALL "lack the marker" without
        # proving forge-run is wired; iteration-2 fix for feedback-009a.md
        # B2). These checks rule OUT corrupted/truncated/commented-out
        # files; they do NOT rule IN semantic wiring -- see the module
        # docstring's iteration-3 correction (feedback-009a-002.md C3).
        if not text.strip():
            raise ModeGuardError(
                f"mode: full_auto refused -- {path} is empty or has no "
                "content: cannot prove forge-run is wired from a file with "
                "nothing in it; fail-closed."
            )
        missing_structure = [
            marker
            for marker in REQUIRED_WORKFLOW_STRUCTURE_MARKERS
            if not _workflow_has_real_line_starting_with(text, marker)
        ]
        if missing_structure:
            raise ModeGuardError(
                f"mode: full_auto refused -- {path} does not look like a "
                f"complete GitHub Actions workflow (missing, as a real "
                f"line rather than merely a substring, {missing_structure!r}): "
                "cannot prove forge-run is wired "
                "from a file that is not evidently a real, complete "
                "workflow body; fail-closed."
            )
        if FORGE_RUN_STUB_MARKER in text:
            raise ModeGuardError(
                f"mode: full_auto refused -- {path} still contains "
                f"'{FORGE_RUN_STUB_MARKER}': forge-run is not wired yet. "
                "Use 'full_auto_decision_only' instead (docs/adr/"
                "0007-full-auto-mode-split.md)."
            )
        # The narrow structural heuristic passed. This does not establish
        # that an agent invocation exists; that semantic proof belongs to
        # Lot 009c SC14.
        return

    # Any other literal (typo, retired value, empty string already handled
    # above): unknown values are refused, never silently passed through.
    raise ModeGuardError(
        f"mode: {mode!r} is not a recognised value "
        f"({sorted(VALID_ALWAYS | CONDITIONALLY_VALID)}) -- fail closed."
    )


def main(argv: list[str] | None = None) -> int:
    import argparse

    import sys

    HARNESS = Path(__file__).resolve().parent.parent
    sys.path.insert(0, str(HARNESS))
    import policy_loader  # noqa: E402

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=REPO_ROOT / "harness" / "pipeline" / "config.yaml")
    args = parser.parse_args(argv)

    config = policy_loader.load_flat_yaml(args.config)
    mode = config.get("mode")
    try:
        validate_mode(mode)
    except ModeGuardError as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 1
    print(f"OK: mode={mode!r} is valid.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
