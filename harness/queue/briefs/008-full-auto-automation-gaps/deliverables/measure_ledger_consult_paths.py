"""
Measures `ledger_consult_before_transition_paths_count` (brief 008, Lot
008a, Required Counters table) over the WHOLE trigger-resolution entry
point -- `resolve()` (all three branches: --payload, --audit-id, push-diff)
plus `resolve_push()` (the function `resolve()`'s third branch delegates
to) -- not `resolve_push()` alone.

Iteration 1's counter had two defects, both fixed here (brief 008
iteration 2 feedback, BLOCKER-1 and BLOCKER-2):

  BLOCKER-1 -- iteration 1 measured only `resolve_push()`, scoping the
  denominator down to make a pre-existing gap (the two workflow_dispatch
  branches bypassing the ledger entirely) invisible to the counter. This
  script walks `resolve()`'s two `if in_payload:` / `if in_audit_id:`
  branches AND `resolve_push()`'s body -- the whole entry point
  `pipeline-orchestrate.yml` actually calls.

  BLOCKER-2 -- iteration 1's "capable of non-empty event=" test only
  matched `ast.Constant`. `resolve()`'s two workflow_dispatch branches pass
  `event=in_event`, an `ast.Name` -- iteration 1's detector would have
  printed "1 1" even pointed at the fixed `resolve()`, hiding the bug. This
  script treats ANY non-`Constant` `event=` value (`Name`, `Call`,
  `JoinedStr`, ...) as capable of non-empty output -- the conservative
  reading BLOCKER-2 required -- and only a `Constant` with a falsy/empty
  value is excluded.

Method (static, line-order heuristic -- NOT a full control-flow/dominance
analysis; see the documented limitation below): for each of the three
scopes analysed (`if in_payload:` body, `if in_audit_id:` body,
`resolve_push()`'s whole body), collect every AST line number of a call to
`audit_ledger.current_state_for` (the ledger read) and every AST line
number of a `return ResolveOutcome(...)` capable of non-empty `event=`
(per the BLOCKER-2 rule above). A capable return is "gated" if at least
one ledger-read call appears at an EARLIER line number within the same
scope.

Documented limitation (named explicitly here, per BLOCKER-1's own
instruction "say so in the counter's own command string, not only in a
module docstring" -- not hidden in trigger_resolve.py's docstring alone):
the `if in_payload:` branch has exactly ONE literal `return
ResolveOutcome(event=in_event, ...)` statement that is reached by TWO
distinct runtime sub-paths -- (a) the payload names an `audit_id` that is
NOT terminal (the ledger read on the line above DID execute, and this path
is genuinely gated), and (b) the payload names no `audit_id` at all (e.g.
a `gate_reject` payload keyed on `brief_dir`, structurally incapable of
the incident -- there is no `audit_id` to look up, so the ledger read on
the line above did NOT execute for this sub-path). A single AST Return
node cannot carry two different gated/ungated verdicts; this script
counts it once, as gated, because the branch DOES consult the ledger
whenever an `audit_id` is structurally present to consult about -- the
same standard `resolve_push()`'s own candidate loop already meets. Sub-path
(b) is not a silent bypass: it is the SAME exemption
`test_resolve_prioritises_explicit_payload_over_diff` already asserts
(a `gate_reject` payload has no `audit_id`, is not audit-transition
traffic at all, and passes through unchanged, exactly as before this
fix) and is named in `resolve()`'s own inline comment immediately above
that return statement.
"""
from __future__ import annotations

import ast
import pathlib

SRC_PATH = pathlib.Path("harness/pipeline/trigger_resolve.py")


def is_ledger_call(node: ast.AST) -> bool:
    return (
        isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and node.func.attr == "current_state_for"
    )


def is_capable_return(node: ast.AST) -> bool:
    """A return is "capable of non-empty event=" if it returns a
    ResolveOutcome(...) call whose event= keyword is either a truthy
    Constant, or NOT a Constant at all (Name/Call/JoinedStr/... -- the
    BLOCKER-2 conservative rule: we cannot statically prove a Name is
    empty, so treat it as capable)."""
    if not isinstance(node, ast.Return) or not isinstance(node.value, ast.Call):
        return False
    kws = {kw.arg: kw.value for kw in node.value.keywords}
    ev = kws.get("event")
    if ev is None:
        return False
    if isinstance(ev, ast.Constant):
        return bool(ev.value)
    return True


def analyze_scope(stmts: list[ast.stmt]) -> tuple[int, int]:
    """Returns (capable_count, gated_count) for a list of statements
    treated as one scope (ledger reads anywhere in the scope gate every
    capable return at a later line, per this script's own documented
    line-order heuristic -- see module docstring)."""
    ledger_lines: list[int] = []
    capable_lines: list[int] = []
    for s in stmts:
        for n in ast.walk(s):
            if is_ledger_call(n):
                ledger_lines.append(n.lineno)
            if is_capable_return(n):
                capable_lines.append(n.lineno)
    gated = sum(1 for ln in capable_lines if any(l < ln for l in ledger_lines))
    return len(capable_lines), gated


def main() -> None:
    src = SRC_PATH.read_text(encoding="utf-8")
    tree = ast.parse(src)
    funcs = {n.name: n for n in tree.body if isinstance(n, ast.FunctionDef)}
    resolve_fn = funcs["resolve"]
    resolve_push_fn = funcs["resolve_push"]

    if_payload = next(
        s for s in resolve_fn.body if isinstance(s, ast.If) and ast.unparse(s.test) == "in_payload"
    )
    if_audit_id = next(
        s for s in resolve_fn.body if isinstance(s, ast.If) and ast.unparse(s.test) == "in_audit_id"
    )

    total_capable = 0
    total_gated = 0
    for label, stmts in (
        ("resolve()/if in_payload", if_payload.body),
        ("resolve()/if in_audit_id", if_audit_id.body),
        ("resolve_push()", resolve_push_fn.body),
    ):
        capable, gated = analyze_scope(stmts)
        total_capable += capable
        total_gated += gated
        print(f"{label}: capable={capable} gated={gated}")

    print(f"TOTAL: gated={total_gated} capable={total_capable}")
    print(
        "documented exemption: resolve()/if in_payload's one capable return "
        "is reached by a payload-with-no-audit_id sub-path (e.g. gate_reject) "
        "that is structurally incapable of the incident and does not execute "
        "the ledger read -- counted once, as gated, per this script's own "
        "module docstring; not a silent bypass (see "
        "test_resolve_prioritises_explicit_payload_over_diff)."
    )


if __name__ == "__main__":
    main()
