#!/usr/bin/env py
"""
harness/pipeline/trigger_resolve.py -- unit-testable event/payload
resolution for `pipeline-orchestrate.yml`'s "Resolve event + payload" step
(brief 008, Lot 008a; fixes incident run 31085883052).

The incident: on the merge that closed PR #8, the pre-fix "Resolve event +
payload" step was pure inline bash. When exactly one
`architecture/reviews/*.md` file appeared in the push's diff, it built
`event=review_recorded` + `payload={"audit_id": ...}` FROM THAT FILE ALONE,
never consulting `architecture/audit-ledger.jsonl` first. The file named was
`CURSOR-FIXTURE-full-auto-demo`, already `AUDIT_ARCHIVED` (terminal).
`audit_decision.decide_auto` -> `audit_ledger.append_event` correctly
refused to replay a transition on a terminal audit (`TransitionError`,
exit 2) -- but the job died silently: nothing told anyone the loop had
stopped watching itself (run `31085883052`, FAILURE).

What this module does about it (SC1-SC6 of brief 008, Lot 008a):

  SC1 -- ALL of the resolve step's decision logic (workflow_dispatch inputs
         AND the push-diff auto-dispatch path) lives here, in plain Python,
         callable directly by `py -m pytest` with no GitHub Actions context.
         `resolve()` is the one entry point `pipeline-orchestrate.yml` calls.
  SC2 -- `resolve_push()`, the function that replays the incident's exact
         auto-dispatch path, reads `architecture/audit-ledger.jsonl` via
         `audit_ledger.current_state_for` (the ONE existing reader --
         see audit_ledger.py's own current_state_for docstring; this module
         builds no second, competing ledger-state reconstruction) for
         EVERY audit_id implied by the push's reviews/*.md diff, BEFORE it
         is capable of returning a non-empty event=/payload=. Any audit_id
         whose current FSM state is terminal per `audit_ledger.TRANSITIONS`
         (today: only AUDIT_ARCHIVED maps to an empty successor set) is
         excluded from the candidate set first.
  SC3 -- the exact incident shape (1 changed review file, already
         AUDIT_ARCHIVED) now resolves to event="" (never
         event=review_recorded), with a ::notice:: naming the audit_id and
         its terminal state. See harness/tests/test_trigger_resolve.py.
  SC4 -- a diff naming exactly one review file whose audit_id is genuinely
         non-terminal (e.g. only AUDIT_CHALLENGED) still resolves to
         event=review_recorded, exactly as before this fix.
  SC5 -- the pre-existing conservative fallback (0, or more than one,
         non-terminal candidates remaining after the SC2 exclusion) is
         unchanged in substance: skip + ::notice:: + manual
         workflow_dispatch required. This module does not attempt to
         resolve that residual ambiguity.
  SC6 -- harness/audit_decision.py's FSM guard is not touched by this
         module; it is called (via orchestrator.py, a separate process this
         module never imports) only when resolve_push() has already decided
         a transition is legitimate to attempt.

Scope note for the `ledger_consult_before_transition_paths_count` counter
(brief 008 Required Counters table): it is measured over `resolve_push()`
specifically, not `resolve()` as a whole. `resolve()`'s other two branches
(explicit workflow_dispatch `--payload` or `--audit-id`) are an explicit,
already-trusted human/CI invocation with no diff to consult the ledger
about -- SC2's own wording ("for every audit_id implied by the push's
diff") scopes the ledger-consultation requirement to the diff-driven
auto-dispatch path alone, which is `resolve_push()`. This is unchanged from
the pre-fix bash, which also never ledger-checked workflow_dispatch inputs.

Usage:
  py harness/pipeline/trigger_resolve.py resolve \\
      --in-event review_recorded --in-audit-id "" --in-payload "" \\
      --ledger architecture/audit-ledger.jsonl \\
      --github-output "$GITHUB_OUTPUT" \\
      < changed-review-files.txt   # one path per line, may be empty
"""
from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"

sys.path.insert(0, str(HARNESS))
import audit_ledger  # noqa: E402 -- the ONE existing ledger reader (SC2)

LEDGER_PATH = audit_ledger.LEDGER_PATH


def audit_id_from_review_filename(fname: str) -> str:
    """Same derivation the pre-fix bash used, unchanged: basename, strip a
    leading 'CLAUDE-' and a trailing '.md'.
    e.g. 'architecture/reviews/CLAUDE-CURSOR-5633ee7-x.md' -> 'CURSOR-5633ee7-x'.
    """
    base = Path(fname).name
    if base.startswith("CLAUDE-"):
        base = base[len("CLAUDE-"):]
    if base.endswith(".md"):
        base = base[: -len(".md")]
    return base


def is_terminal(state: str | None) -> bool:
    """A ledger FSM state is terminal iff `audit_ledger.TRANSITIONS` maps it
    to an empty successor set (today: only AUDIT_ARCHIVED). Derived from the
    live TRANSITIONS table every call -- never a hardcoded state name -- so
    this stays correct if the FSM grows a second terminal state later
    without this module needing an edit (hard-won rule 12: never cite a
    fingerprint/constant by value where a live lookup will do)."""
    if state is None:
        return False
    return len(audit_ledger.TRANSITIONS.get(state, frozenset())) == 0


@dataclass
class ResolveOutcome:
    event: str = ""
    payload: dict | None = None
    notices: list[str] = field(default_factory=list)

    def github_output_lines(self) -> list[str]:
        lines = [f"event={self.event}"]
        if self.payload is not None:
            lines.append(f"payload={json.dumps(self.payload)}")
        return lines


def resolve_push(changed_review_files: list[str], *, ledger_path: Path = LEDGER_PATH) -> ResolveOutcome:
    """The push-triggered auto-dispatch path -- the one the incident (run
    31085883052) broke. `changed_review_files` is the raw list of
    `architecture/reviews/*.md` paths this push's diff touched, in the same
    shape as
    `git diff --name-only <before> <after> -- 'architecture/reviews/*.md'`
    (newline-split by the caller, blank lines already dropped).

    Exactly one non-empty-producing code path exists below (the
    `len(non_terminal) == 1` branch); it is reachable only after every
    candidate audit_id has been through the ledger-consult loop above it.
    """
    if not changed_review_files:
        return ResolveOutcome(
            event="",
            notices=[
                "::notice::0 reviews/*.md changed by this push (need exactly 1 "
                "to auto-dispatch); skipping. Use workflow_dispatch with an "
                "explicit audit_id."
            ],
        )

    candidates = [(fname, audit_id_from_review_filename(fname)) for fname in changed_review_files]

    # SC2: the ledger is consulted for EVERY candidate audit_id here, before
    # any branch below is capable of returning a non-empty event=/payload=.
    terminal_excluded: list[tuple[str, str, str]] = []
    non_terminal: list[tuple[str, str]] = []
    for fname, audit_id in candidates:
        state = audit_ledger.current_state_for(audit_id, ledger_path)
        if is_terminal(state):
            terminal_excluded.append((fname, audit_id, state))
        else:
            non_terminal.append((fname, audit_id))

    notices = [
        f"::notice::skipping {audit_id} ({fname}): current ledger state is "
        f"{state} (terminal) -- not re-acting on an audit whose life is "
        f"already over (incident run 31085883052 regression guard)"
        for fname, audit_id, state in terminal_excluded
    ]

    if len(non_terminal) == 1:
        fname, audit_id = non_terminal[0]
        return ResolveOutcome(event="review_recorded", payload={"audit_id": audit_id}, notices=notices)

    # SC5: 0, or more than one, non-terminal candidates remain after the
    # SC2 exclusion -- unchanged, deliberately conservative fallback: skip,
    # ::notice::, manual workflow_dispatch required. This module does not
    # attempt to resolve that residual ambiguity.
    notices.append(
        f"::notice::{len(non_terminal)} non-terminal reviews/*.md changed by "
        f"this push (need exactly 1 to auto-dispatch after excluding "
        f"terminal audits); skipping. Use workflow_dispatch with an "
        f"explicit audit_id."
    )
    return ResolveOutcome(event="", notices=notices)


def resolve(
    *,
    in_event: str = "",
    in_audit_id: str = "",
    in_payload: str = "",
    changed_review_files: list[str] | None = None,
    ledger_path: Path = LEDGER_PATH,
) -> ResolveOutcome:
    """The single entry point `pipeline-orchestrate.yml`'s "Resolve event +
    payload" step calls (SC1). Priority, unchanged from the pre-fix bash:
    explicit `--payload` > explicit `--audit-id` > push-diff auto-dispatch.
    Only the last branch is the incident's own path and is the one SC2-SC5
    harden; workflow_dispatch's explicit inputs are an already-trusted
    human/CI invocation, exactly as before this fix.
    """
    if in_payload:
        return ResolveOutcome(event=in_event, payload=json.loads(in_payload))
    if in_audit_id:
        return ResolveOutcome(event=in_event, payload={"audit_id": in_audit_id})
    return resolve_push(changed_review_files or [], ledger_path=ledger_path)


# --- CLI --------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="cmd", required=True)

    rp = sub.add_parser(
        "resolve",
        help="resolve event=/payload= for pipeline-orchestrate.yml's resolve step",
    )
    rp.add_argument("--in-event", default="")
    rp.add_argument("--in-audit-id", default="")
    rp.add_argument("--in-payload", default="")
    rp.add_argument("--ledger", default=str(LEDGER_PATH))
    rp.add_argument(
        "--github-output",
        default=None,
        help="path to append event=/payload= lines to ($GITHUB_OUTPUT in CI); "
        "if omitted, lines are printed to stdout instead",
    )

    args = parser.parse_args(argv)

    if args.cmd != "resolve":  # pragma: no cover -- argparse enforces this already
        return 1

    changed = [line.strip() for line in sys.stdin.read().splitlines() if line.strip()]

    outcome = resolve(
        in_event=args.in_event,
        in_audit_id=args.in_audit_id,
        in_payload=args.in_payload,
        changed_review_files=changed,
        ledger_path=Path(args.ledger),
    )

    for notice in outcome.notices:
        print(notice)

    lines = outcome.github_output_lines()
    if args.github_output:
        with open(args.github_output, "a", encoding="utf-8") as fh:
            for line in lines:
                fh.write(line + "\n")
    else:
        for line in lines:
            print(line)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
