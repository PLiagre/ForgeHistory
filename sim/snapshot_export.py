"""Photographie déterministe du monde déjà simulé (schéma v0a-2).

Ce module ne recalcule aucune mécanique. Il joint ce que porte la carte
figée (géométrie, relief, climat, gisements) à la province dérivée et à
l'état que le moteur fait évoluer.

ADR-0018 : une seule entrée géographique, `data/world-1400.json`, déjà
chargée par le monde. Ce module ne lit plus aucun artefact de pipeline.

Honnêteté des couches : `dans_la_carte` dit que la donnée est là ;
`utilisee_par_le_moteur` dit si le tick s'en sert. Aujourd'hui le tick ne
se sert d'aucune des trois — elles sont exportées, pas encore jouées.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any, Optional

from sim.aggregation import (
    PositionCelluleInconnue,
    agregat_depuis_monde,
    identifiant_de_province_de_cellule,
    nom_de_province_de_cellule,
)
from sim.constants import SNAPSHOT_FLOAT_DECIMALS, SNAPSHOT_SCHEMA_VERSION
from sim.world import CARTE_PATH, CARTE_RELATIVE, World

_HASH_CHUNK_BYTES = 1024 * 1024

# Les trois couches que la carte apporte au-delà de la géométrie. Aucune
# n'est encore utilisée par le tick : le moteur les expose, il ne les joue
# pas. Le jour où le tick en consomme une, on passe son drapeau à True —
# et un test le vérifie.
_COUCHES = {
    "relief": False,
    "climat": False,
    "gisements": False,
}


class SnapshotExportError(RuntimeError):
    """Refus d'export : donnée manquante, jamais inventée."""


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(_HASH_CHUNK_BYTES), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _round_float(value: Any) -> Any:
    if isinstance(value, bool) or value is None:
        return value
    if isinstance(value, int) and not isinstance(value, bool):
        return value
    if isinstance(value, float):
        return round(value, SNAPSHOT_FLOAT_DECIMALS)
    return value


def _round_tree(value: Any) -> Any:
    if isinstance(value, dict):
        return {key: _round_tree(value[key]) for key in value}
    if isinstance(value, list):
        return [_round_tree(item) for item in value]
    return _round_float(value)


def _couches(carte_meta: dict) -> dict:
    """L'état honnête de chaque couche portée par la carte."""
    return {
        nom: {
            "dans_la_carte": True,
            "utilisee_par_le_moteur": utilisee,
        }
        for nom, utilisee in _COUCHES.items()
    }


def build_snapshot_document(world: World, seed: int, tick: int) -> dict:
    if not world.carte:
        raise SnapshotExportError(
            "Le monde n'a pas été chargé depuis la carte figée ; "
            "aucune géométrie à photographier."
        )
    try:
        regroupements = agregat_depuis_monde(world)
    except PositionCelluleInconnue as exc:
        raise SnapshotExportError(str(exc)) from exc

    cells_out = []
    for cell_id, cell in sorted(world.cells.items(), key=lambda item: int(item[0])):
        cid = int(cell_id)
        raw = world.carte.get(cid)
        if raw is None or "geometry" not in raw or "centroid" not in raw:
            raise SnapshotExportError(f"geometrie absente de la carte pour cell_id={cid}")
        centroid_src = raw["centroid"]
        try:
            centroid = {
                "lat": centroid_src["lat"],
                "lon": centroid_src["lon"],
                "x_m": centroid_src["x_m"],
                "y_m": centroid_src["y_m"],
            }
        except KeyError as exc:
            raise SnapshotExportError(
                f"centroide G3 incomplet pour cell_id={cid}"
            ) from exc
        province_id = identifiant_de_province_de_cellule(cid, regroupements)
        province_name = nom_de_province_de_cellule(cid, regroupements)
        if province_id is None or province_name is None:
            raise SnapshotExportError(f"province absente pour cell_id={cid}")
        cells_out.append(
            {
                "area_km2": cell.area_km2,
                "cell_id": cid,
                "centroid": centroid,
                "climat": raw.get("climat"),
                "food_deficit_kg": cell.food_deficit_kg,
                "food_stock_kg": cell.food_stock_kg,
                "geometry": raw["geometry"],
                "gisements": raw.get("gisements", []),
                "hunger_ticks": cell.hunger_ticks,
                "mortality_remainder": cell.mortality_remainder,
                "population": cell.population,
                "province": {"id": int(province_id), "name": province_name},
                "relief": raw.get("relief"),
            }
        )

    document = {
        "cell_count": len(cells_out),
        "cells": cells_out,
        "crs": "EPSG:3035",
        "carte": {
            "path": CARTE_RELATIVE,
            "sha256": _sha256_file(CARTE_PATH),
            "version": world.carte_meta.get("version"),
        },
        "couches": _couches(world.carte_meta),
        "schema_version": SNAPSHOT_SCHEMA_VERSION,
        "seed": int(seed),
        "tick": int(tick),
    }
    return _round_tree(document)


def serialize_snapshot(document: dict) -> bytes:
    payload = json.dumps(
        document,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
        allow_nan=False,
    )
    return (payload + "\n").encode("utf-8")


def export_snapshot(world: World, seed: int, tick: int, path: Path) -> Path:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    payload = serialize_snapshot(build_snapshot_document(world, seed, tick))
    destination.write_bytes(payload)
    return destination
