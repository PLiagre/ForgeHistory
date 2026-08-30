"""Lecture et validation d'un snapshot v0a-3. Aucun recalcul métier."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from sim.constants import SNAPSHOT_SCHEMA_VERSION


class SnapshotLoadError(RuntimeError):
    """Fichier absent ou schéma inconnu."""


def proposed_layers(document: dict[str, Any]) -> list[str]:
    """Couches proposées : population puis chaque clé de panier du document."""
    commodities: set[str] = set()
    for cell in document.get("cells") or []:
        stocks = cell.get("stocks")
        if isinstance(stocks, dict):
            commodities.update(stocks.keys())
    return ["population", *sorted(commodities)]


def load_snapshot(path: Path) -> dict[str, Any]:
    destination = Path(path)
    if not destination.is_file():
        raise SnapshotLoadError(f"snapshot absent: {destination}")
    try:
        document = json.loads(destination.read_bytes().decode("utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise SnapshotLoadError(f"snapshot illisible: {destination}") from exc
    version = document.get("schema_version")
    if version != SNAPSHOT_SCHEMA_VERSION:
        raise SnapshotLoadError(
            f"schema_version inconnu: {version} (attendu: {SNAPSHOT_SCHEMA_VERSION})"
        )
    if "cells" not in document:
        raise SnapshotLoadError("cells absentes")
    return document
