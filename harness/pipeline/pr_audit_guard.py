#!/usr/bin/env py
"""
harness/pipeline/pr_audit_guard.py -- détection des audits non adjugés ciblant une PR.

Module stdlib-only, directement testable par pytest sans contexte GitHub Actions.

Un audit dans architecture/inbox/*.md cible une PR si son frontmatter YAML contient :
  - `target_branch` égal à la branche de tête de la PR, OU
  - `target_commit` dont les 7 premiers caractères correspondent aux 7 premiers
    caractères du SHA du commit de tête de la PR.

Un audit est adjugé si son état dans architecture/audit-ledger.jsonl est l'un de :
  AUDIT_APPROVED, AUDIT_REJECTED, AUDIT_CONVERTED, AUDIT_IMPLEMENTED,
  AUDIT_VERIFIED, AUDIT_ARCHIVED.

Un audit est non adjugé si son état est None (PROPOSED implicite), AUDIT_PROPOSED,
AUDIT_CHALLENGED, ou AUDIT_STALE.

CLI :
  .venv/bin/python harness/pipeline/pr_audit_guard.py check \\
    --head-branch <branche> \\
    --head-commit <sha> \\
    [--inbox architecture/inbox] \\
    [--ledger architecture/audit-ledger.jsonl]

Codes de sortie :
  0 -- aucun audit non adjugé ne cible la PR
  1 -- au moins un audit non adjugé cible la PR
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"

sys.path.insert(0, str(HARNESS))
import audit_ledger  # noqa: E402

DEFAULT_INBOX = REPO_ROOT / "architecture" / "inbox"
DEFAULT_LEDGER = audit_ledger.LEDGER_PATH

# États qui signifient qu'un audit est adjugé (décision prise, boucle fermée).
ADJUDICATED_STATES = frozenset(
    {
        "AUDIT_APPROVED",
        "AUDIT_REJECTED",
        "AUDIT_CONVERTED",
        "AUDIT_IMPLEMENTED",
        "AUDIT_VERIFIED",
        "AUDIT_ARCHIVED",
    }
)


def _parse_frontmatter(text: str) -> dict:
    """Extrait les champs du frontmatter YAML délimité par ---."""
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return {}
    fields: dict = {}
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if ":" in line:
            key, _, value = line.partition(":")
            fields[key.strip()] = value.strip()
    return fields


def _audit_id_from_path(path: Path) -> str:
    """Dérive l'audit_id depuis le nom de fichier (sans extension)."""
    return path.stem


def _targets_pr(frontmatter: dict, head_branch: str, head_commit: str) -> bool:
    """Retourne True si le frontmatter cible la PR par branche ou par commit."""
    target_branch = frontmatter.get("target_branch", "")
    if target_branch and target_branch == head_branch:
        return True
    target_commit = frontmatter.get("target_commit", "")
    if target_commit and head_commit and target_commit[:7] == head_commit[:7]:
        return True
    return False


def _is_adjudicated(state: str | None) -> bool:
    """Retourne True si l'état est adjugé (décision prise)."""
    return state in ADJUDICATED_STATES


def check(
    head_branch: str,
    head_commit: str,
    inbox_path: Path = DEFAULT_INBOX,
    ledger_path: Path = DEFAULT_LEDGER,
) -> int:
    """Détecte les audits non adjugés ciblant la PR.

    Retourne le code de sortie : 0 si aucun audit non adjugé ne cible la PR,
    1 si au moins un audit non adjugé cible la PR.
    """
    inbox_path = Path(inbox_path)
    ledger_path = Path(ledger_path)

    audit_files = sorted(inbox_path.glob("*.md")) if inbox_path.exists() else []
    if not audit_files:
        print("Aucun audit ne cible cette PR — contrôle vert.")
        return 0

    targeting: list[tuple[str, str | None]] = []
    for audit_file in audit_files:
        text = audit_file.read_text(encoding="utf-8")
        fm = _parse_frontmatter(text)
        audit_id = _audit_id_from_path(audit_file)
        if _targets_pr(fm, head_branch, head_commit):
            state = audit_ledger.current_state_for(audit_id, ledger_path)
            targeting.append((audit_id, state))

    if not targeting:
        print("Aucun audit ne cible cette PR — contrôle vert.")
        return 0

    non_adjudicated = [(aid, st) for aid, st in targeting if not _is_adjudicated(st)]
    adjudicated = [(aid, st) for aid, st in targeting if _is_adjudicated(st)]

    if adjudicated:
        print("Audits ciblant cette PR — adjugés :")
        for aid, st in adjudicated:
            print(f"  {aid}: {st}")

    if not non_adjudicated:
        print("Tous les audits ciblant cette PR sont adjugés — contrôle vert.")
        return 0

    print("ERREUR : audits ciblant cette PR, non adjugés :")
    for aid, st in non_adjudicated:
        state_label = st if st is not None else "PROPOSED (aucune ligne au ledger)"
        print(f"  {aid}: {state_label}")
    print(
        f"{len(non_adjudicated)} audit(s) non adjugé(s) cible(nt) cette PR "
        "— la décision doit être prise avant la fusion (contrôle rouge)."
    )
    return 1


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    sub = parser.add_subparsers(dest="cmd", required=True)

    cp = sub.add_parser("check", help="vérifie les audits non adjugés ciblant la PR")
    cp.add_argument("--head-branch", required=True, help="branche de tête de la PR")
    cp.add_argument("--head-commit", required=True, help="SHA du commit de tête de la PR")
    cp.add_argument(
        "--inbox",
        default=str(DEFAULT_INBOX),
        help="répertoire contenant les audits (architecture/inbox par défaut)",
    )
    cp.add_argument(
        "--ledger",
        default=str(DEFAULT_LEDGER),
        help="chemin vers audit-ledger.jsonl",
    )

    args = parser.parse_args(argv)

    if args.cmd != "check":  # pragma: no cover
        return 1

    return check(
        head_branch=args.head_branch,
        head_commit=args.head_commit,
        inbox_path=Path(args.inbox),
        ledger_path=Path(args.ledger),
    )


if __name__ == "__main__":
    raise SystemExit(main())
