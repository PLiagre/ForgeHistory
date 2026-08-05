#!/usr/bin/env py
"""
harness/audit_convert.py -- turn an APPROVED audit into a brief seed.

This closes the loop: an accepted audit's retained points become a NEW brief
under harness/queue/briefs/, which then flows through the existing harness
(Planificateur -> Générateur -> gate -> Évaluateur) unchanged. Transition
APPROVED -> CONVERTED.

Single source of instruction, preserved on purpose: the audit does NOT
instruct anything. This step writes a brief.md, and from that point the
brief.md is the ONE instruction -- exactly the invariant
test_single_source_of_instruction.py protects. The audit and the decision
are recorded as *provenance* inside the brief, never as a second set of
orders.

What this step deliberately does NOT do: it does not write the spec. It
emits a SEED brief -- provenance plus <<TODO (planificateur)>> placeholders
for the World-Terms Requirement, Success Conditions, Non-Goals, counters and
waivers. Authoring those is the Planificateur's job, before any code exists.
Fabricating them here would put the Générateur's instructions in the mouth of
a mechanical converter that never reasoned about the world.

Fail-closed guards:
  * only an APPROVED audit can be converted;
  * the target brief directory is never clobbered.

Usage:
  py harness/audit_convert.py convert --audit-id ID [--slug custom-slug]
"""
from __future__ import annotations

import argparse
import datetime
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
BRIEFS = REPO_ROOT / "harness" / "queue" / "briefs"

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_ledger  # noqa: E402
import audits as audits_mod  # noqa: E402


class ConvertError(Exception):
    """A guard refused the operation. Message is user-facing."""


def _utc_now_iso() -> str:
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def next_brief_number(briefs_dir: Path = BRIEFS) -> str:
    """The next zero-padded NNN prefix after the existing briefs."""
    briefs_dir = Path(briefs_dir)
    highest = 0
    if briefs_dir.exists():
        for child in briefs_dir.iterdir():
            m = re.match(r"^(\d+)-", child.name)
            if child.is_dir() and m:
                highest = max(highest, int(m.group(1)))
    return f"{highest + 1:03d}"


def slug_from_audit_id(audit_id: str) -> str:
    """Derive a brief slug from an audit id.

    CURSOR-<sha>-<subject> -> <subject>. Falls back to a sanitised form of
    the whole id if it does not match that shape.
    """
    m = re.match(r"^CURSOR-[0-9a-fA-F]+-(.+)$", audit_id)
    raw = m.group(1) if m else audit_id
    slug = re.sub(r"[^a-z0-9]+", "-", raw.lower()).strip("-")
    return slug or "audit-conversion"


def _find_audit(audit_id: str, inbox: Path | None) -> dict | None:
    for audit in audits_mod.load_audits(inbox or audits_mod.INBOX):
        if audit.get("audit_id") == audit_id:
            return audit
    return None


def _approved_retained(audit_id: str, events: list[dict]):
    for event in reversed(events):
        if event.get("audit_id") == audit_id and event.get("event") == "AUDIT_APPROVED":
            return event.get("retained_points")
    return None


def brief_seed_text(audit: dict, number: str, retained, authored: str) -> str:
    audit_id = audit.get("audit_id", "UNKNOWN")
    inbox_path = audit.get("path", f"architecture/inbox/{audit_id}.md")
    retained_str = (
        ", ".join(str(n) for n in retained) if retained else "tous les points"
    )
    return f"""# Brief {number}: <<TODO (planificateur): titre>> (issu de l'audit {audit_id})

**Authored**: {authored}
**Author**: forge-audit-convert (graine — à étendre par le Planificateur)

## Provenance

Ce brief est la conversion des points retenus de l'audit `{audit_id}`.
- Audit source : `{inbox_path}`
- Décision du propriétaire : `architecture/decisions/DECISION-{audit_id}.md`
- Points retenus : {retained_str}

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE
instruction** (voir CLAUDE.md › Single Source of Instruction). L'audit et la
décision ci-dessus sont de la *provenance*, pas des ordres.

## World-Terms Requirement

<<TODO (planificateur): énoncer le besoin en world-terms, causalement — pas
comme une préférence de qualité de code. Traduire chaque point retenu de
l'audit en une conséquence observable dans le monde simulé ou dans la
fiabilité du harness.>>

## Success Conditions

<<TODO (planificateur): conditions de succès numérotées, chacune vérifiable —
mécaniquement quand c'est possible.>>

## Non-Goals

<<TODO (planificateur): ce que ce brief ne doit explicitement PAS faire.>>

## Required Counters

<<TODO (planificateur): table des compteurs (name / sample source /
denominator) que le manifest devra porter.>>

## Acceptable Waivers (if any claim of infeasibility arises)

<<TODO (planificateur): table (claim / required command / required error), ou
« aucun » si aucune dérogation n'est acceptable.>>
"""


def rubric_seed_text(number: str, audit_id: str, authored: str) -> str:
    return f"""# Eval Rubric — Brief {number} (issu de l'audit {audit_id})

**Authored**: {authored}

<<TODO (planificateur): écrire la rubrique d'évaluation AVANT tout travail du
Générateur — une ligne par condition de succès du brief, plus les lignes de
gate mécanique. Ne pas réviser après avoir vu les livrables.>>
"""


def convert(
    audit_id: str,
    *,
    slug: str | None = None,
    inbox: Path | None = None,
    briefs_dir: Path = BRIEFS,
    ledger_path: Path | None = None,
) -> dict:
    ledger_path = ledger_path or audit_ledger.LEDGER_PATH
    briefs_dir = Path(briefs_dir)

    audit = _find_audit(audit_id, inbox)
    if audit is None:
        raise ConvertError(f"no audit {audit_id!r} in inbox")

    events = audit_ledger.read_events(ledger_path)
    state = audits_mod.current_state(audit_id, events)
    if state != "AUDIT_APPROVED":
        raise ConvertError(
            f"audit {audit_id!r} is {state}, not AUDIT_APPROVED; only an "
            f"approved audit can be converted (run /forge-audit-accept first)"
        )

    number = next_brief_number(briefs_dir)
    slug = slug or slug_from_audit_id(audit_id)
    brief_dir = briefs_dir / f"{number}-{slug}"
    if brief_dir.exists():
        raise ConvertError(f"{brief_dir.as_posix()} already exists; refusing to clobber")

    authored = _utc_now_iso()
    retained = _approved_retained(audit_id, events)

    (brief_dir / "deliverables").mkdir(parents=True, exist_ok=True)
    (brief_dir / "brief.md").write_text(
        brief_seed_text(audit, number, retained, authored), encoding="utf-8"
    )
    (brief_dir / "eval-rubric.md").write_text(
        rubric_seed_text(number, audit_id, authored), encoding="utf-8"
    )
    (brief_dir / "deliverables" / ".gitkeep").write_text("", encoding="utf-8")

    rel = (
        brief_dir.relative_to(REPO_ROOT).as_posix()
        if _within(brief_dir, REPO_ROOT)
        else brief_dir.as_posix()
    )
    return audit_ledger.append_event(
        audit_id, "AUDIT_CONVERTED", ledger_path=ledger_path, actor="owner", briefs=[rel]
    )


def _within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


# --- CLI ----------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Convert an approved audit into a brief seed.")
    sub = parser.add_subparsers(dest="cmd", required=True)
    cp = sub.add_parser("convert", help="APPROVED -> CONVERTED, seeding a brief")
    cp.add_argument("--audit-id", required=True)
    cp.add_argument("--slug", default=None)
    cp.add_argument("--inbox", default=None)
    cp.add_argument("--briefs", default=str(BRIEFS))
    cp.add_argument("--ledger", default=str(audit_ledger.LEDGER_PATH))
    args = parser.parse_args(argv)

    try:
        record = convert(
            args.audit_id,
            slug=args.slug,
            inbox=Path(args.inbox) if args.inbox else None,
            briefs_dir=Path(args.briefs),
            ledger_path=Path(args.ledger),
        )
    except ConvertError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    print(f"converted {args.audit_id} -> {record['briefs'][0]} (fill the <<TODO>> as Planificateur)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
