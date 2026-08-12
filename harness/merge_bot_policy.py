#!/usr/bin/env py
"""Lit la frontière d'auto-fusion directement dans merge-bot.yml.

Ce module n'active ni ne modifie aucune règle. Il rend les règles réellement
exécutées mesurables par le test SC12 et par la mesure historique SC14 du
brief 010, lot 010c.
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "merge-bot.yml"

_BRANCH_PREFIX = re.compile(
    r"startsWith\(\s*github\.head_ref\s*,\s*'([^']+)'\s*\)"
)


class MergeBotPolicyError(ValueError):
    """Le workflow ne permet pas d'extraire une frontière complète."""


@dataclass(frozen=True)
class MergeBotPolicy:
    branch_prefixes: tuple[str, ...]
    allowed_path_prefixes: tuple[str, ...]
    allowed_path_regex: str
    denied_path_regex: str

    def refusal_reasons(
        self, head_ref: str, changed_paths: Iterable[str]
    ) -> tuple[str, ...]:
        """Retourne les refus du workflow applicables à une PR observée.

        Il faut un préfixe autorisé, au moins un chemin, aucun chemin interdit
        et tous les chemins dans l'allowlist. Une donnée vide refuse au lieu de
        supposer que la PR serait sûre.
        """
        paths = tuple(changed_paths)
        reasons: list[str] = []
        if not paths:
            reasons.append("aucun chemin renvoyé")
        if not any(head_ref.startswith(prefix) for prefix in self.branch_prefixes):
            reasons.append("préfixe de branche refusé")
        denied = [path for path in paths if re.match(self.denied_path_regex, path)]
        if denied:
            reasons.append("denylist: " + ", ".join(denied))
        outside = [path for path in paths if not re.match(self.allowed_path_regex, path)]
        if outside:
            reasons.append("hors allowlist: " + ", ".join(outside))
        return tuple(reasons)

    def is_automergeable(self, head_ref: str, changed_paths: Iterable[str]) -> bool:
        """Vrai si les règles extraites ne produisent aucun refus."""
        return not self.refusal_reasons(head_ref, changed_paths)


def _grep_pattern(text: str, variable: str, flag: str) -> str:
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped.startswith(f'{variable}="$('):
            continue
        match = re.search(rf"grep\s+{re.escape(flag)}\s+'([^']+)'", stripped)
        if match:
            pattern = match.group(1)
            try:
                re.compile(pattern)
            except re.error as exc:
                raise MergeBotPolicyError(
                    f"regex {variable} invalide dans merge-bot.yml: {exc}"
                ) from exc
            return pattern
    raise MergeBotPolicyError(
        f"commande {variable}=... grep {flag} introuvable dans merge-bot.yml"
    )


def _display_allowed_prefixes(pattern: str) -> tuple[str, ...]:
    if not (pattern.startswith("^(") and pattern.endswith(")")):
        raise MergeBotPolicyError(
            "l'allowlist doit être une alternance ancrée ^(...) pour être mesurable"
        )
    alternatives = pattern[2:-1].split("|")
    if not alternatives or any(not item for item in alternatives):
        raise MergeBotPolicyError("allowlist vide ou alternative vide")

    normalized = tuple(
        item.replace(r"\.", ".").replace(".*", "*") for item in alternatives
    )
    if any(not item.endswith("/") for item in normalized):
        raise MergeBotPolicyError(
            f"forme de chemin autorisé non reconnue: {normalized!r}"
        )
    return normalized


def load_merge_bot_policy(
    workflow_path: Path | str = DEFAULT_WORKFLOW,
) -> MergeBotPolicy:
    """Extrait une politique complète du workflow, sinon refuse."""
    path = Path(workflow_path)
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as exc:
        raise MergeBotPolicyError(f"workflow illisible: {path}: {exc}") from exc
    if not text.strip():
        raise MergeBotPolicyError(f"workflow vide: {path}")

    branch_prefixes = tuple(_BRANCH_PREFIX.findall(text))
    if not branch_prefixes or len(set(branch_prefixes)) != len(branch_prefixes):
        raise MergeBotPolicyError(
            f"préfixes de branche absents ou dupliqués: {branch_prefixes!r}"
        )

    denied_pattern = _grep_pattern(text, "denied", "-E")
    allowed_pattern = _grep_pattern(text, "offending", "-vE")
    allowed_prefixes = _display_allowed_prefixes(allowed_pattern)

    return MergeBotPolicy(
        branch_prefixes=branch_prefixes,
        allowed_path_prefixes=allowed_prefixes,
        allowed_path_regex=allowed_pattern,
        denied_path_regex=denied_pattern,
    )
