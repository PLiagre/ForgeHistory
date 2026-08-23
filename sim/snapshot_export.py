"""Photographie déterministe du monde déjà simulé (brief 027, schéma v0a-1).

Ce module ne recalcule aucune mécanique. Il joint la géométrie G3, la
province dérivée et les déterminants C1 déjà publiés.
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
from sim.world import World

_REPO_ROOT = Path(__file__).resolve().parent.parent
_G3_RELATIVE = "pipeline/geo/artifacts/cells_g3.json"
_C1_RELATIVE = "pipeline/geo/artifacts/cells_climate_drivers_c1.json"
_G6_RELATIVE = "pipeline/geo/artifacts/cells_relief_g6.json"
_R1_RELATIVE = "pipeline/geo/artifacts/cells_resources_r1.json"
_G3_PATH = _REPO_ROOT / _G3_RELATIVE
_C1_PATH = _REPO_ROOT / _C1_RELATIVE
_G6_PATH = _REPO_ROOT / _G6_RELATIVE
_R1_PATH = _REPO_ROOT / _R1_RELATIVE

_CLIMATE_KEYS = (
    "insolation_annual_mj_m2",
    "daylight_h_summer_solstice",
    "daylight_h_winter_solstice",
    "dist_sea_centroid_m",
    "hops_to_sea",
    "coastal",
)
_HASH_CHUNK_BYTES = 1024 * 1024


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


def _layer_status(path: Path, relative: str, *, consumed: bool) -> dict:
    if not path.is_file():
        return {"status": "absent"}
    if not consumed:
        return {"status": "not_consumed", "path": relative}
    return {
        "status": "present",
        "path": relative,
        "sha256": _sha256_file(path),
    }


def _load_g3_index() -> dict[int, dict]:
    if not _G3_PATH.is_file():
        raise SnapshotExportError("cells_g3.json introuvable")
    doc = json.loads(_G3_PATH.read_text(encoding="utf-8"))
    index = {}
    for raw in doc["cells"]:
        index[int(raw["cell_id"])] = raw
    return index


def _load_c1_index(g3_ids: set[int]) -> tuple[dict, Optional[dict[int, dict]]]:
    if not _C1_PATH.is_file():
        return {"status": "absent"}, None
    doc = json.loads(_C1_PATH.read_text(encoding="utf-8"))
    by_id = {int(raw["cell_id"]): raw for raw in doc["cells"]}
    if set(by_id) != g3_ids:
        return {"status": "not_consumed", "path": _C1_RELATIVE}, None
    return _layer_status(_C1_PATH, _C1_RELATIVE, consumed=True), by_id


def build_snapshot_document(world: World, seed: int, tick: int) -> dict:
    g3_index = _load_g3_index()
    g3_ids = set(g3_index)
    c1_layer, c1_index = _load_c1_index(g3_ids)
    try:
        regroupements = agregat_depuis_monde(world)
    except PositionCelluleInconnue as exc:
        raise SnapshotExportError(str(exc)) from exc

    cells_out = []
    for cell_id, cell in sorted(world.cells.items(), key=lambda item: int(item[0])):
        cid = int(cell_id)
        raw = g3_index.get(cid)
        if raw is None or "geometry" not in raw or "centroid" not in raw:
            raise SnapshotExportError(f"geometrie G3 absente pour cell_id={cid}")
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
        climate = None
        if c1_index is not None and cid in c1_index:
            src = c1_index[cid]
            climate = {key: src[key] for key in _CLIMATE_KEYS}
        cells_out.append(
            {
                "area_km2": cell.area_km2,
                "cell_id": cid,
                "centroid": centroid,
                "climate_drivers": climate,
                "food_deficit_kg": cell.food_deficit_kg,
                "food_stock_kg": cell.food_stock_kg,
                "geometry": raw["geometry"],
                "hunger_ticks": cell.hunger_ticks,
                "mortality_remainder": cell.mortality_remainder,
                "population": cell.population,
                "province": {"id": int(province_id), "name": province_name},
            }
        )

    document = {
        "cell_count": len(cells_out),
        "cells": cells_out,
        "crs": "EPSG:3035",
        "geometry_source": {
            "path": _G3_RELATIVE,
            "sha256": _sha256_file(_G3_PATH),
        },
        "layers": {
            "climate_drivers_c1": c1_layer,
            "relief_g6": _layer_status(_G6_PATH, _G6_RELATIVE, consumed=False)
            if _G6_PATH.is_file()
            else {"status": "absent"},
            "resources_r1": _layer_status(_R1_PATH, _R1_RELATIVE, consumed=False)
            if _R1_PATH.is_file()
            else {"status": "absent"},
        },
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
