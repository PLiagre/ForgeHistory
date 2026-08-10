#!/usr/bin/env py
"""Plafond budgétaire persistant des appels d'agents en CI.

Le contrôle mensuel est préventif : avant un appel, il additionne les coûts
USD du mois civil UTC dans le ledger JSONL append-only. Si le total atteint le
plafond, il remet le ``mode:`` de ``config.yaml`` à ``manual`` et refuse
l'appel.

Le marquage produit par ce module pour une invocation est POST-hoc : il part
du coût réel du transcript. Un dépassement est donc enregistré avec
``over_cap: true`` ; le résultat déjà produit n'est jamais jeté. La commande
``claude --help`` disponible le 2026-08-10 expose aussi
``--max-budget-usd <amount>`` pour les appels ``--print``. Son branchement
appartient au lot qui invoque réellement le CLI, pas à ce module autonome ;
la découverte est conservée dans les livrables du lot 009b au lieu de répéter
l'ancienne hypothèse d'impossibilité. Le calcul importe
``harness.backends.ledger`` et appelle sa fonction ``price_of`` : la table
``PRICES`` et sa date restent une source unique.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Mapping

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from harness.backends import ledger as backend_ledger  # noqa: E402


DEFAULT_LEDGER_PATH = REPO_ROOT / "harness" / "pipeline" / "ci-budget-ledger.jsonl"
DEFAULT_CONFIG_PATH = REPO_ROOT / "harness" / "pipeline" / "config.yaml"
DEFAULT_MONTHLY_CAP_USD = 200.0

_MODE_LINE = re.compile(br"^mode:[^\r\n]*$")


class BudgetGuardError(RuntimeError):
    """Le budget ne peut pas être calculé ou appliqué sans deviner."""


class BudgetExceededError(BudgetGuardError):
    """Le total mensuel a atteint le plafond et l'appel doit être refusé."""


def _as_utc(value: datetime | None) -> datetime:
    value = value or datetime.now(timezone.utc)
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)


def _parse_timestamp(value: object, *, line_number: int) -> datetime:
    if not isinstance(value, str) or not value.strip():
        raise BudgetGuardError(
            f"ledger invalide à la ligne {line_number}: timestamp absent; refus sans estimation."
        )
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise BudgetGuardError(
            f"ledger invalide à la ligne {line_number}: timestamp illisible {value!r}."
        ) from exc
    return _as_utc(parsed)


def _parse_usd(value: object, *, line_number: int) -> float:
    if isinstance(value, bool):
        raise BudgetGuardError(
            f"ledger invalide à la ligne {line_number}: usd booléen; refus sans estimation."
        )
    try:
        usd = float(value)
    except (TypeError, ValueError) as exc:
        raise BudgetGuardError(
            f"ledger invalide à la ligne {line_number}: usd absent ou illisible."
        ) from exc
    if not math.isfinite(usd) or usd < 0:
        raise BudgetGuardError(
            f"ledger invalide à la ligne {line_number}: usd doit être fini et positif ou nul."
        )
    return usd


def load_budget_entries(ledger_path: Path = DEFAULT_LEDGER_PATH) -> list[dict]:
    """Lit toutes les lignes non vides ; une ligne corrompue refuse le calcul."""
    ledger_path = Path(ledger_path)
    if not ledger_path.exists():
        return []

    entries: list[dict] = []
    try:
        lines = ledger_path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeDecodeError) as exc:
        raise BudgetGuardError(f"ledger illisible {ledger_path}: {exc}") from exc
    for line_number, raw in enumerate(lines, start=1):
        if not raw.strip():
            continue
        try:
            entry = json.loads(raw)
        except json.JSONDecodeError as exc:
            raise BudgetGuardError(
                f"ledger JSONL invalide à la ligne {line_number}: {exc.msg}."
            ) from exc
        if not isinstance(entry, dict):
            raise BudgetGuardError(
                f"ledger invalide à la ligne {line_number}: objet JSON attendu."
            )
        entries.append(entry)
    return entries


def current_month_total_usd(
    ledger_path: Path = DEFAULT_LEDGER_PATH,
    *,
    now: datetime | None = None,
) -> float:
    """Somme le mois civil UTC courant ; les mois antérieurs sont exclus."""
    current = _as_utc(now)
    total = 0.0
    for line_number, entry in enumerate(load_budget_entries(ledger_path), start=1):
        timestamp = _parse_timestamp(entry.get("timestamp"), line_number=line_number)
        usd = _parse_usd(entry.get("usd"), line_number=line_number)
        if (timestamp.year, timestamp.month) == (current.year, current.month):
            total += usd
    return round(total, 6)


def _set_mode_manual(config_path: Path) -> None:
    """Réécrit uniquement la ligne top-level ``mode:`` et préserve ses octets voisins."""
    config_path = Path(config_path)
    try:
        before = config_path.read_bytes()
    except OSError as exc:
        raise BudgetGuardError(f"config illisible {config_path}: {exc}") from exc

    lines = before.splitlines(keepends=True)
    indexes: list[int] = []
    for index, line in enumerate(lines):
        body = line.rstrip(b"\r\n")
        if _MODE_LINE.fullmatch(body):
            indexes.append(index)
    if len(indexes) != 1:
        raise BudgetGuardError(
            f"config ambiguë {config_path}: une ligne top-level mode: attendue, "
            f"{len(indexes)} trouvée(s); refus sans réécriture."
        )

    index = indexes[0]
    line = lines[index]
    body = line.rstrip(b"\r\n")
    newline = line[len(body) :]
    hash_index = body.find(b"#")
    if hash_index >= 0:
        prefix = body[:hash_index]
        suffix_start = len(prefix.rstrip(b" \t"))
        suffix = body[suffix_start:]
    else:
        suffix = b""
    replacement = b"mode: manual" + suffix + newline
    if replacement == line:
        return
    lines[index] = replacement
    after = b"".join(lines)
    try:
        config_path.write_bytes(after)
    except OSError as exc:
        raise BudgetGuardError(f"impossible d'activer le mode manuel: {exc}") from exc


def precheck_monthly_budget(
    *,
    ledger_path: Path = DEFAULT_LEDGER_PATH,
    config_path: Path = DEFAULT_CONFIG_PATH,
    monthly_cap_usd: float = DEFAULT_MONTHLY_CAP_USD,
    now: datetime | None = None,
) -> float:
    """Retourne le total courant ou refuse et rétablit ``mode: manual``."""
    cap = _parse_usd(monthly_cap_usd, line_number=0)
    total = current_month_total_usd(ledger_path, now=now)
    if total >= cap:
        _set_mode_manual(config_path)
        raise BudgetExceededError(
            f"plafond mensuel atteint: {total:.6f} USD >= {cap:.6f} USD; "
            f"mode remis à manual dans {config_path}."
        )
    return total


def compute_usage_usd(usage_by_model: Mapping[str, Mapping[str, int]]) -> float:
    """Calcule le coût via ``backend_ledger.price_of`` sans recopier les prix."""
    if not usage_by_model:
        raise ValueError("usage vide: coût impossible à mesurer sans transcript.")
    total = 0.0
    for model, counts in usage_by_model.items():
        cost = backend_ledger.price_of(model, dict(counts))
        if cost is None:
            if any(int(counts.get(key, 0)) for key in ("in", "cache_write", "cache_read", "out")):
                raise ValueError(
                    f"modèle {model!r} sans prix publié dans harness/backends/ledger.py; "
                    "refus de supposer un coût nul."
                )
            continue
        total += cost
    return round(total, 6)


def record_invocation(
    *,
    step_name: str,
    usage_by_model: Mapping[str, Mapping[str, int]],
    per_invocation_cap_usd: float,
    ledger_path: Path = DEFAULT_LEDGER_PATH,
    timestamp: datetime | None = None,
) -> dict:
    """Ajoute une mesure post-hoc, avec anomalie explicite en cas de dépassement."""
    if not isinstance(step_name, str) or not step_name.strip():
        raise ValueError("step_name non vide requis.")
    cap = _parse_usd(per_invocation_cap_usd, line_number=0)
    usd = compute_usage_usd(usage_by_model)
    instant = _as_utc(timestamp)
    entry = {
        "timestamp": instant.isoformat().replace("+00:00", "Z"),
        "step": step_name.strip(),
        "usd": usd,
        "cap_usd": cap,
        "over_cap": usd > cap,
        "prices_as_of": backend_ledger.PRICES_AS_OF,
    }
    ledger_path = Path(ledger_path)
    try:
        ledger_path.parent.mkdir(parents=True, exist_ok=True)
        with ledger_path.open("a", encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(entry, ensure_ascii=False, sort_keys=True) + "\n")
    except OSError as exc:
        raise BudgetGuardError(f"impossible d'ajouter au ledger {ledger_path}: {exc}") from exc
    return entry


def record_transcript(
    *,
    step_name: str,
    transcript_path: Path,
    per_invocation_cap_usd: float,
    ledger_path: Path = DEFAULT_LEDGER_PATH,
    timestamp: datetime | None = None,
) -> dict:
    """Scanne le transcript avec le lecteur publié puis appelle ``record_invocation``."""
    usage = backend_ledger.scan_transcript(Path(transcript_path))
    return record_invocation(
        step_name=step_name,
        usage_by_model=usage,
        per_invocation_cap_usd=per_invocation_cap_usd,
        ledger_path=ledger_path,
        timestamp=timestamp,
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    precheck = subparsers.add_parser("precheck")
    precheck.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER_PATH)
    precheck.add_argument("--config", type=Path, default=DEFAULT_CONFIG_PATH)
    precheck.add_argument("--monthly-cap-usd", type=float, default=DEFAULT_MONTHLY_CAP_USD)

    record = subparsers.add_parser("record")
    record.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER_PATH)
    record.add_argument("--step-name", required=True)
    record.add_argument("--transcript", type=Path, required=True)
    record.add_argument("--per-invocation-cap-usd", type=float, required=True)

    args = parser.parse_args(argv)
    if args.command == "precheck":
        try:
            total = precheck_monthly_budget(
                ledger_path=args.ledger,
                config_path=args.config,
                monthly_cap_usd=args.monthly_cap_usd,
            )
        except BudgetGuardError as exc:
            print(f"REFUSED: {exc}", file=sys.stderr)
            return 2
        print(json.dumps({"status": "PROCEED", "month_total_usd": total}, sort_keys=True))
        return 0

    try:
        entry = record_transcript(
            step_name=args.step_name,
            transcript_path=args.transcript,
            per_invocation_cap_usd=args.per_invocation_cap_usd,
            ledger_path=args.ledger,
        )
    except (BudgetGuardError, ValueError) as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(entry, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
