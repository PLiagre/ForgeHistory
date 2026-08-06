#!/usr/bin/env py
"""
harness/pipeline/policy_loader.py -- tiny vendored loader for the two
pipeline YAML files (`auto_policy.yaml`, `config.yaml`).

Why hand-rolled instead of PyYAML: `py -c "import yaml"` fails in this
environment (ModuleNotFoundError -- no PyYAML installed), and the brief for
Lot 006a is explicit that a new pip dependency must never be added silently.
Both files this module reads are deliberately restricted to a tiny YAML
subset (flat `key: value` top-level scalars, plus exactly one top-level
`rules:` block holding a list of flat `- key: value` mappings) so a ~40-line
parser is enough and honest about its limits -- it is NOT a general YAML
parser and must never be asked to be one.

Usage:
  from harness.pipeline.policy_loader import load_auto_policy, load_flat_yaml
  policy = load_auto_policy(Path("harness/pipeline/auto_policy.yaml"))
  config = load_flat_yaml(Path("harness/pipeline/config.yaml"))
"""
from __future__ import annotations

from pathlib import Path


def _coerce_scalar(value: str) -> object:
    value = value.strip()
    if value in ("true", "True"):
        return True
    if value in ("false", "False"):
        return False
    if value and (value.isdigit() or (value[0] == "-" and value[1:].isdigit())):
        return int(value)
    # strip optional surrounding quotes
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
        return value[1:-1]
    return value


def _strip_comment(line: str) -> str:
    # No quoting of '#' is used anywhere in these two files, so a naive
    # split is safe and documented as such.
    return line.split("#", 1)[0].rstrip()


def load_flat_yaml(path: Path) -> dict:
    """Parse top-level `key: value` scalar pairs. List/dict values under a
    top-level key (e.g. `auto_merge_allowlist:` followed by `  - item`) are
    collected as a list of strings. Blank lines and full-line comments are
    skipped."""
    path = Path(path)
    lines = path.read_text(encoding="utf-8").splitlines()
    result: dict = {}
    i = 0
    n = len(lines)
    while i < n:
        raw = _strip_comment(lines[i])
        if not raw.strip():
            i += 1
            continue
        indent = len(raw) - len(raw.lstrip(" "))
        stripped = raw.strip()
        if indent == 0 and stripped.endswith(":") and "-" not in stripped[:1]:
            key = stripped[:-1].strip()
            items: list = []
            i += 1
            while i < n:
                raw2 = _strip_comment(lines[i])
                if not raw2.strip():
                    i += 1
                    continue
                indent2 = len(raw2) - len(raw2.lstrip(" "))
                if indent2 == 0:
                    break
                s2 = raw2.strip()
                if s2.startswith("- "):
                    items.append(_coerce_scalar(s2[2:]))
                i += 1
            result[key] = items
            continue
        if indent == 0 and ":" in stripped:
            key, _, value = stripped.partition(":")
            result[key.strip()] = _coerce_scalar(value)
        i += 1
    return result


def load_auto_policy(path: Path) -> dict:
    """Parse `auto_policy.yaml`: top-level scalars (e.g. `mode:`) plus one
    `rules:` block, a list of flat mappings (`id`, `event`, `condition`,
    `action`, ...). Returns {"mode": ..., "rules": [ {...}, ... ]}."""
    path = Path(path)
    lines = path.read_text(encoding="utf-8").splitlines()
    result: dict = {"rules": []}
    i = 0
    n = len(lines)
    while i < n:
        raw = _strip_comment(lines[i])
        if not raw.strip():
            i += 1
            continue
        indent = len(raw) - len(raw.lstrip(" "))
        stripped = raw.strip()
        if indent == 0 and stripped == "rules:":
            i += 1
            rule: dict | None = None
            while i < n:
                raw2 = _strip_comment(lines[i])
                if not raw2.strip():
                    i += 1
                    continue
                indent2 = len(raw2) - len(raw2.lstrip(" "))
                if indent2 == 0:
                    break  # dedent back to top level: rules block is done
                s2 = raw2.strip()
                if s2.startswith("- "):
                    if rule is not None:
                        result["rules"].append(rule)
                    rule = {}
                    s2 = s2[2:].strip()
                    if ":" in s2:
                        k, _, v = s2.partition(":")
                        rule[k.strip()] = _coerce_scalar(v)
                elif ":" in s2 and rule is not None:
                    k, _, v = s2.partition(":")
                    rule[k.strip()] = _coerce_scalar(v)
                i += 1
            if rule is not None:
                result["rules"].append(rule)
            continue
        if indent == 0 and ":" in stripped:
            key, _, value = stripped.partition(":")
            result[key.strip()] = _coerce_scalar(value)
        i += 1
    return result


def rule_by_id(policy: dict, rule_id: str) -> dict | None:
    for rule in policy.get("rules", []):
        if rule.get("id") == rule_id:
            return rule
    return None
