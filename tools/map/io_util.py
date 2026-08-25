"""Utilitaires I/O déterministes."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def round_float(value: float, decimals: int) -> float:
    return round(float(value), decimals)


def canonicalize(obj: Any, float_decimals: int = 6) -> Any:
    """Normalise récursivement pour un JSON stable (clés triées, floats arrondis)."""
    if isinstance(obj, dict):
        return {
            str(k): canonicalize(obj[k], float_decimals)
            for k in sorted(obj.keys(), key=lambda x: str(x))
        }
    if isinstance(obj, list):
        return [canonicalize(v, float_decimals) for v in obj]
    if isinstance(obj, float):
        return round_float(obj, float_decimals)
    if isinstance(obj, Path):
        return str(obj).replace("\\", "/")
    return obj


def dumps_deterministic(obj: Any, float_decimals: int = 6) -> str:
    payload = canonicalize(obj, float_decimals)
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":"), sort_keys=True) + "\n"


def write_json(path: Path, obj: Any, float_decimals: int = 6) -> str:
    """Écrit un JSON déterministe (UTF-8, \\n). Retourne le SHA256 du contenu écrit."""
    path.parent.mkdir(parents=True, exist_ok=True)
    text = dumps_deterministic(obj, float_decimals)
    data = text.encode("utf-8")
    path.write_bytes(data)
    return sha256_bytes(data)


def read_json(path: Path) -> Any:
    # Accepte un BOM UTF-8 (défensif) : certains artefacts préexistants peuvent
    # commencer par EF BB BF sans être régénérés.
    return json.loads(path.read_text(encoding="utf-8-sig"))
