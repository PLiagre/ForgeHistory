#!/usr/bin/env py
"""
harness/verdict_audit.py -- Tier-1 mechanical gate. Deterministic, LLM-free.

Usage: py harness/verdict_audit.py <brief_dir>
Exit:  0 ACCEPT | 1 REJECT | 2 INTERNAL ERROR (never treated as a pass)

Operates on a brief directory containing brief.md, eval-rubric.md,
verdict.md, and deliverables/manifest.json. See docs/rules/harness-roles.md
and docs/rules/hard-won-rules.md for the rules each check enforces.
"""
from __future__ import annotations

import datetime
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bare_python

SENTINEL_NOT_COMPUTED = -1

_FRONTMATTER_FIELD = re.compile(r'^\*\*{label}\*\*:\s*(\S+)', re.MULTILINE)
_ISO_TS = re.compile(r'\d{4}-\d{2}-\d{2}T[\d:]+')
_FRONTMATTER_LINE = re.compile(r'^\*\*(Author|Date|Verdict)\*\*:.*$', re.MULTILINE)
_NUMBER = re.compile(r'\b\d{2,}\b')
_CODE_SPAN = re.compile(r'`([^`\n]*)`')


def _mask_code_spans(text: str, *, bare_word_only: str | None = None) -> str:
    """Neutralize inline-code-span content before mechanical scanning.

    Filenames/paths/identifiers are conventionally backtick-quoted in this
    repo's docs (e.g. `docs/adr/0003-....md`), and a spec/rubric/verdict must
    be free to *name* a forbidden word (e.g. "no bare `python`") without that
    mention being read as an actual occurrence. When bare_word_only is set,
    only spans whose content is exactly that word are masked -- a real
    command like `python foo.py` (more than just the bare word) still counts.
    """
    def repl(match: re.Match) -> str:
        content = match.group(1)
        if bare_word_only is not None:
            if re.fullmatch(rf'\s*{re.escape(bare_word_only)}\s*', content, re.IGNORECASE):
                return '`(masked)`'
            return match.group(0)
        return '`' + re.sub(r'\d', '#', content) + '`'
    return _CODE_SPAN.sub(repl, text)


@dataclass
class CheckResult:
    name: str
    passed: bool
    evidence: str
    applicable: bool = True


def sha256_of(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def load_json(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def read_field(p: Path, label: str) -> str | None:
    if not p.exists():
        return None
    m = re.search(_FRONTMATTER_FIELD.pattern.format(label=re.escape(label)), p.read_text(encoding="utf-8"), re.MULTILINE)
    return m.group(1) if m else None


def read_all_fields(p: Path, label: str) -> list[str]:
    """All occurrences of a `**Label**: value` frontmatter field, in document
    order -- unlike read_field (re.search), which only ever sees the first.

    A multi-lot brief appends one signed section per lot to
    deliverables/generator-log.md and to verdict.md; read_field alone can
    only ever see the oldest lot's author (brief 010's SC3b). Callers that
    need every author -- not just the earliest -- use this instead.
    """
    if not p.exists():
        return []
    pattern = re.compile(_FRONTMATTER_FIELD.pattern.format(label=re.escape(label)), re.MULTILINE)
    return [m.group(1) for m in pattern.finditer(p.read_text(encoding="utf-8"))]


def read_ts(p: Path, label: str = "Authored") -> datetime.datetime | None:
    v = read_field(p, label)
    if not v:
        return None
    try:
        ts = datetime.datetime.fromisoformat(v)
    except ValueError:
        return None
    # A brief may legitimately stamp Authored in full ISO 8601 with an offset
    # (e.g. "2026-08-05T10:05:00Z"). Deliverable mtimes are read via
    # datetime.fromtimestamp(), which is naive-local, so an offset-aware brief
    # timestamp would raise TypeError on the ordering comparison and crash the
    # gate into exit 2 (INTERNAL ERROR) -- never ACCEPT. Normalize to naive
    # local so both sides of every < compare in the same frame. This tightens
    # nothing: it only stops the gate mis-firing on a valid timestamp format.
    if ts.tzinfo is not None:
        ts = ts.astimezone().replace(tzinfo=None)
    return ts


def check_files_declared_exist(bd: Path, m: dict) -> CheckResult:
    missing = [f["path"] for f in m.get("files", []) if not (bd / f["path"]).exists()]
    return CheckResult("files_declared_exist", not missing,
                        f"missing: {missing}" if missing else "all declared files present")


def check_declared_files_are_tracked(bd: Path, m: dict) -> CheckResult:
    """Declared proof that git ignores is proof nobody else can re-check.

    Found by running the gate on a fresh clone: brief 003 is ACCEPT in the
    working tree and REJECT on a clone of the same commit, because 14 of its
    54 declared files are gitignored (`*.log`, and unity/game_unity/Logs/).
    The verdict existed in exactly one working tree and could never be
    reproduced -- not on another machine, not on this one after a clean
    checkout.

    Scoped to files inside the brief directory. A path that escapes it (a
    regenerable Unity log under unity/game_unity/Logs/, say) is an external
    reference, not the brief's own evidence; those are named in the evidence
    string so the gap stays visible rather than silently exempted. Briefs
    should declare a committed copy or a hash for those -- now stated in the
    Planificateur's Execution Contract.
    """
    declared = [f["path"] for f in m.get("files", [])]
    if not declared:
        return CheckResult("declared_files_are_tracked", True, "no files declared")

    # Outside a git work tree there is no tracking to verify, so the check is
    # N/A and drops out of the verdict entirely. Deliberately not a PASS: a
    # pass would assert something was checked. N/A says nothing was.
    try:
        inside_repo = subprocess.run(
            ["git", "rev-parse", "--is-inside-work-tree"],
            cwd=bd, capture_output=True, text=True,
        )
    except OSError as exc:
        return CheckResult("declared_files_are_tracked", True,
                           f"git unavailable ({exc}); not checked", applicable=False)
    if inside_repo.returncode != 0 or inside_repo.stdout.strip() != "true":
        return CheckResult("declared_files_are_tracked", True,
                           "brief is not inside a git work tree; not checked",
                           applicable=False)

    inside, outside = [], []
    for rel in declared:
        resolved = (bd / rel).resolve()
        try:
            resolved.relative_to(bd.resolve())
            inside.append(rel)
        except ValueError:
            outside.append(rel)

    untracked = []
    if inside:
        try:
            proc = subprocess.run(
                ["git", "ls-files", "--error-unmatch", "--", *inside],
                cwd=bd, capture_output=True, text=True,
            )
            if proc.returncode != 0:
                # git names each unmatched path on stderr; surface them as-is
                # rather than re-deriving, so the evidence is git's own.
                untracked = [line.split("'")[1] for line in proc.stderr.splitlines()
                             if "did not match" in line and "'" in line]
                if not untracked:
                    untracked = ["(git ls-files failed: " + proc.stderr.strip()[:120] + ")"]
        except (OSError, IndexError) as exc:
            return CheckResult("declared_files_are_tracked", False,
                               f"could not consult git: {exc}")

    note = f"; {len(outside)} declared outside the brief dir, not checked: {outside}" if outside else ""
    return CheckResult(
        "declared_files_are_tracked", not untracked,
        (f"untracked/ignored: {untracked}{note}" if untracked
         else f"all {len(inside)} in-brief declared files are tracked{note}"))


def check_mtime_after_brief(bd: Path, m: dict) -> CheckResult:
    brief_ts = read_ts(bd / "brief.md")
    if brief_ts is None:
        return CheckResult("mtime_after_brief", False, "brief.md missing/unparseable Authored timestamp")
    stale = []
    for f in m.get("files", []):
        p = bd / f["path"]
        if p.exists() and datetime.datetime.fromtimestamp(p.stat().st_mtime) < brief_ts:
            stale.append(f["path"])
    return CheckResult("mtime_after_brief", not stale,
                        f"predate brief.md: {stale}" if stale else "all deliverables postdate the brief")


def _git_blob(bd: Path, ref: str) -> tuple[bytes | None, str]:
    """Read one blob out of git history: ref is git's own `<rev>:<path>`.

    Returns (bytes, "") on success, or (None, reason) — never a silent empty
    blob, which would hash to something and compare "different" by accident.
    """
    try:
        proc = subprocess.run(["git", "show", ref], cwd=bd, capture_output=True)
    except OSError as exc:
        return None, f"git unavailable ({exc})"
    if proc.returncode != 0:
        return None, proc.stderr.decode("utf-8", "replace").strip()[:160] or "git show failed"
    return proc.stdout, ""


def check_captures_differ(bd: Path, m: dict) -> CheckResult:
    """Two artifacts a brief says must differ, actually differ.

    Two forms of reference, same guarantee:

    - `must_differ_from`: a second path inside the brief dir. The pre-state
      has to be committed as its own copy for this to work.
    - `must_differ_from_git`: git's own `<rev>:<path>` (e.g.
      `origin/master:tools/map/constants.py`). Git already stores every
      pre-edit state; committing a `.orig` duplicate of a tracked file next
      to it buys nothing the repository did not already hold. Prefer this
      form for any file git tracks.

    An unresolvable git reference FAILS rather than being skipped. The brief
    made a claim; if the reference cannot be read, nothing checked it, and a
    PASS would assert that something did.
    """
    bad = []
    for f in m.get("files", []):
        p1 = bd / f["path"]

        other = f.get("must_differ_from")
        if other:
            p2 = bd / other
            missing = [str(q) for q, exists in ((f["path"], p1.exists()), (other, p2.exists()))
                       if not exists]
            if missing:
                # Was: `if p1.exists() and p2.exists() and ...` -- a pair whose
                # files the gate cannot find resolved to a silent PASS, and the
                # evidence line still read "all declared pairs differ". Brief
                # 026 shipped three such pairs (its manifest declares paths from
                # the repo root, the gate resolves them from the brief dir): the
                # three committed `.orig` copies proved nothing at all.
                bad.append(f'{f["path"]} vs {other}: not compared, missing {missing}')
            elif sha256_of(p1) == sha256_of(p2):
                bad.append(f'{f["path"]} == {other}')

        ref = f.get("must_differ_from_git")
        if ref:
            if not p1.exists():
                bad.append(f'{f["path"]} vs {ref}: not compared, published file missing')
                continue
            blob, reason = _git_blob(bd, ref)
            if blob is None:
                bad.append(f'{f["path"]} vs {ref}: unresolvable ({reason})')
            elif hashlib.sha256(blob).hexdigest() == sha256_of(p1):
                bad.append(f'{f["path"]} == {ref}')

    return CheckResult("captures_differ_when_should", not bad,
                        f"pairs not established as differing: {bad}" if bad
                        else "all declared pairs compared, and they differ")


def check_waivers(m: dict) -> CheckResult:
    bad = [w.get("claim", "?") for w in m.get("waivers", []) if not w.get("command") or not w.get("error")]
    return CheckResult("waivers_have_command_and_error", not bad,
                        f"missing command+error: {bad}" if bad else "all waivers carry a command and an error")


def check_no_empty_sample(m: dict) -> CheckResult:
    bad = [c["name"] for c in m.get("counters", [])
           if c.get("sample_size", SENTINEL_NOT_COMPUTED) in (0, SENTINEL_NOT_COMPUTED)]
    return CheckResult("no_empty_sample_pass", not bad,
                        f"zero/uncomputed sample_size: {bad}" if bad else "every counter has a real sample_size")


def check_verdict_numbers_traceable(bd: Path, m: dict) -> CheckResult:
    vf = bd / "verdict.md"
    if not vf.exists():
        return CheckResult("verdict_numbers_traceable", False, "verdict.md missing")
    text = vf.read_text(encoding="utf-8")
    text = _ISO_TS.sub('', text)
    text = _FRONTMATTER_LINE.sub('', text)
    text = _mask_code_spans(text)
    cited = set(_NUMBER.findall(text))
    known = {str(c.get("value")) for c in m.get("counters", [])} | \
            {str(c.get("sample_size")) for c in m.get("counters", [])}
    untraceable = sorted(cited - known)
    return CheckResult("verdict_numbers_traceable", not untraceable,
                        f"cited but not in manifest.json: {untraceable}" if untraceable else "all cited numbers trace to manifest.json")


def check_no_bare_python(bd: Path, m: dict) -> CheckResult:
    """Catch a Générateur that ran, or reports having run, the Store stub.

    Uses the same positional matcher as the live PreToolUse hook (see
    harness/bare_python.py): the word counts only where a shell would
    execute it. The previous substring scan flagged any deliverable that
    merely *mentioned* python in prose -- "we rejected python in favour of
    py" would fail the gate -- and any captured log line containing the word,
    which is most logs produced by Python tooling.

    Code-span masking still runs first, and still matters: markdown uses
    backticks for code spans while a shell uses them for command
    substitution, so an unmasked `python` in prose would look like a
    backquoted command. Masking spans whose content is exactly the bare word
    lets a document write "no bare `python`" while `python foo.py` in a span
    is still read as the invocation it is.
    """
    hits = []
    for c in m.get("counters", []) + m.get("waivers", []):
        cmd = c.get("command") or ""
        if bare_python.find_invocation(cmd):
            hits.append(cmd)
    for pattern in ("**/*.log", "**/*.txt", "**/*.md"):
        for lf in bd.glob(pattern):
            t = lf.read_text(encoding="utf-8", errors="ignore")
            t = _mask_code_spans(t, bare_word_only="python")
            if bare_python.find_invocation(t):
                hits.append(str(lf.relative_to(bd)))
    return CheckResult("no_bare_python_alias", not hits,
                        f"bare `python` found in: {hits}" if hits else "no bare `python` invocations found")


def _actor_suffix(author: str, role_prefix: str) -> str | None:
    """The backend name after a role prefix (e.g. 'codex' out of
    'forge-generateur-codex'), or None for the bare/native role string
    ('forge-generateur') carrying no backend suffix at all.

    This is string surgery on the *role* prefix, never a lookup table of
    known backends -- an actor nobody has used yet (brief 010's SC4 proves
    this with a backend name absent from the whole repository) is derived
    exactly the same way as 'codex' or 'cursor', so a third or fourth
    backend needs no change here.
    """
    if author == role_prefix:
        return None
    prefixed = role_prefix + "-"
    return author[len(prefixed):] if author.startswith(prefixed) else None


def _same_actor(gen_author: str, ver_author: str) -> bool:
    """Same *actor* behind a (generator, evaluator) author pair -- the
    question `gen != ver` used to ask by comparing role strings, which broke
    the moment two backends existed (brief 010's World-Terms Requirement):
    'forge-generateur-codex' != 'forge-evaluateur-codex' as strings, even
    though the same actor, Codex, wrote both.
    """
    if gen_author == ver_author:
        return True
    g = _actor_suffix(gen_author, "forge-generateur")
    v = _actor_suffix(ver_author, "forge-evaluateur")
    return g is not None and g == v


def check_verdict_not_self_authored(bd: Path) -> CheckResult:
    """Refuse a lot whose producer also judged it -- by actor, not by role
    string, and across every author pair the two files carry, not only the
    oldest.

    Two angle blind spots closed together (brief 010, iteration 1):

    1. Actor vs. role (SC3/SC4). `gen != ver` on the raw role strings passed
       'forge-generateur-codex' vs. 'forge-evaluateur-codex' -- different
       strings, same actor. `_same_actor` derives the actor from each side's
       backend suffix instead, generically (SC4: an unseen actor name is
       refused with no code change, because nothing here enumerates known
       backends).

    2. First pair only (SC3b). `read_field`'s re.search only ever returns
       the earliest `**Author**:` line, so on a multi-lot brief (each lot
       appends its own signed section) every author pair past the first was
       invisible to this check -- self-authored or not, it was simply never
       looked at. `read_all_fields` collects every occurrence in order.

    Pairing rule, once every author is collected. A brief's generator-log
    and verdict.md are appended to independently and don't carry matching
    counts (verdict.md may hold more than one evaluation pass over the same
    lot -- brief 009 carries an initial REJECT plus a later re-evaluation of
    the very same Lot 009a). The most recent verdict entries are the ones
    that still speak for the brief's current state, so the last `k` authors
    of each file are paired positionally, `k = min(len(gen), len(ver))` --
    older, superseded verdict passes drop off the front rather than being
    force-matched to the wrong lot. Verified against the real brief 009
    (SC6): this pairs Lot 009a's generator (`forge-generateur`) against its
    own later re-evaluation (`forge-evaluateur-codex`, different actor, a
    legitimate cross-actor judgment) and Lot 009b's generator
    (`forge-generateur-codex`) against its own evaluation
    (`forge-evaluateur`, likewise different) -- exactly the two lots that
    exist, no false self-judgment, no lot left unexamined.

    Two more, closed in iteration 2 after the Évaluateur found the k-window
    above had made the check strictly MORE permissive than the single-pair
    code it replaced (brief 010 feedback D1/D2) -- disqualifying under this
    brief's own non-goal 7:

    3. Whole-list identity, regardless of position (D1). The pre-multi-backend
       code compared read_field to read_field -- always the FIRST entry of
       each file -- and a strict string equality there was enough to refuse.
       That invariant must keep holding once there is more than one entry:
       the exact same author string appearing anywhere in generator-log.md
       AND anywhere in verdict.md is definitionally the same actor signing
       both sides, no matter which position the k-window pairs it at.
       Concretely: appending an unjudged, not-yet-reviewed lot to the
       journal shifted the k-window and silenced a plain self-signed verdict
       (producer signs verdict.md with their own generator-log author
       string) -- this check closes that regardless of list length or
       ordering, by testing the two lists as sets first.

    4. Entries the k-window drops are not "superseded", they are
       "unexamined" (D2, SC3b's own "every pair" requirement). Whichever
       side is longer has entries older than the window; those dropped
       entries were never compared against the k-window at all. Each
       dropped author is confronted against every author on the OTHER
       side (not only its positional counterpart) for a same-actor match.
       This is the mirror of point 3 for the case where the two role
       strings differ (e.g. 'forge-generateur-korrigan' dropped from the
       journal vs. 'forge-evaluateur-korrigan' inside the k-window on the
       verdict side) -- same actor, different strings, so the set check in
       point 3 alone would not have caught it.

    Both additions can only refuse cases the pairing rule above used to
    accept; neither can turn an accepted honest pair (SC6, brief 009) into a
    refusal, because both only fire on an actual actor match.
    """
    gen_authors = read_all_fields(bd / "deliverables" / "generator-log.md", "Author")
    ver_authors = read_all_fields(bd / "verdict.md", "Author")
    if not gen_authors or not ver_authors:
        return CheckResult("verdict_is_not_self_authored", False, "Author frontmatter missing on generator-log.md or verdict.md")

    # Point 3: identical raw author string on both sides, any position.
    raw_overlap = set(gen_authors) & set(ver_authors)
    if raw_overlap:
        bad = ", ".join(sorted(raw_overlap))
        return CheckResult("verdict_is_not_self_authored", False,
                            f"identical author string appears in both generator-log.md and verdict.md: {bad}")

    k = min(len(gen_authors), len(ver_authors))
    pairs = list(zip(gen_authors[-k:], ver_authors[-k:]))
    offenders = [(g, v) for g, v in pairs if _same_actor(g, v)]
    examined = "; ".join(f"{g}<->{v}" for g, v in pairs)

    # Point 4: entries dropped by the k-window, confronted against every
    # author on the other file (not only their positional counterpart).
    dropped_gen = gen_authors[:-k] if k < len(gen_authors) else []
    dropped_ver = ver_authors[:-k] if k < len(ver_authors) else []
    cross_offenders = [(g, v) for g in dropped_gen for v in ver_authors if _same_actor(g, v)]
    cross_offenders += [(g, v) for v in dropped_ver for g in gen_authors if _same_actor(g, v)]

    if offenders:
        bad = "; ".join(f"{g}=={v}" for g, v in offenders)
        msg = f"same actor on {len(offenders)}/{len(pairs)} examined pair(s): {bad} (examined: {examined})"
        if cross_offenders:
            bad_cross = "; ".join(f"{g}=={v}" for g, v in cross_offenders)
            msg += f"; plus {len(cross_offenders)} dropped-entry self-judgment(s): {bad_cross}"
        return CheckResult("verdict_is_not_self_authored", False, msg)
    if cross_offenders:
        bad_cross = "; ".join(f"{g}=={v}" for g, v in cross_offenders)
        return CheckResult("verdict_is_not_self_authored", False,
                            f"same actor on {len(cross_offenders)} dropped-entry pair(s) outside the k-window: {bad_cross} "
                            f"(positionally examined: {examined})")
    return CheckResult("verdict_is_not_self_authored", True,
                        f"generator/evaluator actors differ on all {len(pairs)} examined pair(s): {examined}")


def check_rubric_predates(bd: Path, m: dict) -> CheckResult:
    rubric_ts = read_ts(bd / "eval-rubric.md")
    if rubric_ts is None:
        return CheckResult("rubric_predates_deliverables", False, "eval-rubric.md missing/unparseable Authored timestamp")
    mtimes = [datetime.datetime.fromtimestamp((bd / f["path"]).stat().st_mtime)
              for f in m.get("files", []) if (bd / f["path"]).exists()]
    if not mtimes:
        return CheckResult("rubric_predates_deliverables", True, "no deliverables to compare against", applicable=False)
    earliest = min(mtimes)
    return CheckResult("rubric_predates_deliverables", rubric_ts <= earliest,
                        f"rubric ({rubric_ts}) written after earliest deliverable ({earliest})"
                        if rubric_ts > earliest else f"rubric ({rubric_ts}) predates earliest deliverable ({earliest})")


def run_all_checks(bd: Path) -> list[CheckResult]:
    m = load_json(bd / "deliverables" / "manifest.json") or {}
    return [
        check_files_declared_exist(bd, m),
        check_mtime_after_brief(bd, m),
        check_captures_differ(bd, m),
        check_waivers(m),
        check_no_empty_sample(m),
        check_verdict_numbers_traceable(bd, m),
        check_no_bare_python(bd, m),
        check_verdict_not_self_authored(bd),
        check_rubric_predates(bd, m),
        check_declared_files_are_tracked(bd, m),
    ]


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: py verdict_audit.py <brief_dir>", file=sys.stderr)
        return 2
    bd = Path(sys.argv[1])
    if not bd.is_dir():
        print(f"ERROR: {bd} is not a directory", file=sys.stderr)
        return 2

    try:
        checks = run_all_checks(bd)
    except Exception as e:  # noqa: BLE001 -- audit failure must be loud, never silent
        print(f"ERROR: audit itself failed: {e}", file=sys.stderr)
        return 2

    overall = all(c.passed for c in checks if c.applicable)
    print(f"# verdict_audit report for {bd}")
    print(f"# generated_at: {datetime.datetime.now().isoformat()}")
    for c in checks:
        status = "N/A" if not c.applicable else ("PASS" if c.passed else "FAIL")
        print(f"[{status}] {c.name}: {c.evidence}")
    print()
    print("VERDICT: ACCEPT" if overall else "VERDICT: REJECT")
    return 0 if overall else 1


if __name__ == "__main__":
    sys.exit(main())
