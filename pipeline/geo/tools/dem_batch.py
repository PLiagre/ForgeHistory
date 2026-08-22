"""Lecture raster groupée et table de mesures G6 réutilisable."""

from __future__ import annotations

import hashlib
import json
import os
import tempfile
from pathlib import Path
from typing import Any, Callable, Dict, Hashable, Mapping, Optional, Sequence, Tuple

import numpy as np


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def measurement_table_key(
    *,
    sources_lock: Path,
    cells: Path,
    adjacency: Path,
    sampling_code: Path,
    sample_step: float,
) -> Tuple[str, Dict[str, str]]:
    """Clé honnête : sources, maille, adjacence, code et pas d'échantillon."""
    inputs = {
        "sources.lock": _sha256_file(Path(sources_lock)),
        "cells_g3.json": _sha256_file(Path(cells)),
        "adjacency_g5.json": _sha256_file(Path(adjacency)),
        "sampling_code": _sha256_file(Path(sampling_code)),
        "sample_step": format(float(sample_step), ".17g"),
    }
    payload = json.dumps(
        inputs, ensure_ascii=True, sort_keys=True, separators=(",", ":")
    ).encode("ascii")
    return hashlib.sha256(payload).hexdigest(), inputs


def read_grouped_windows(
    grouped: Mapping[Hashable, Sequence[Tuple[int, int, int]]],
    dataset_for_key: Callable[[Hashable], Any],
    *,
    output_size: int,
    masked: bool,
) -> Tuple[list[Optional[float]], Dict[str, int]]:
    """Lit une fenêtre englobante par tuile et restitue l'ordre des requêtes.

    Chaque entrée vaut ``(index_sortie, ligne_raster, colonne_raster)``.
    Les vrais zéros restent ``0.0`` ; avec ``masked=True``, seul un pixel
    masqué devient ``None``.
    """
    if output_size < 0:
        raise ValueError("output_size negatif")
    output: list[Optional[float]] = [None] * output_size
    seen: set[int] = set()
    raster_reads = 0
    pixels_loaded = 0
    point_count = 0

    import numpy.ma as ma
    from rasterio.windows import Window

    for key in sorted(grouped, key=lambda value: str(value)):
        requests = list(grouped[key])
        if not requests:
            continue
        dataset = dataset_for_key(key)
        rows = [int(request[1]) for request in requests]
        cols = [int(request[2]) for request in requests]
        for out_index, row, col in requests:
            if not 0 <= int(out_index) < output_size:
                raise IndexError(f"index sortie hors bornes: {out_index}")
            if int(out_index) in seen:
                raise ValueError(f"index sortie duplique: {out_index}")
            if not (0 <= int(row) < dataset.height and 0 <= int(col) < dataset.width):
                raise IndexError(
                    f"pixel hors bornes pour {key}: row={row} col={col} "
                    f"shape={dataset.height}x{dataset.width}"
                )
            seen.add(int(out_index))

        row_min, row_max = min(rows), max(rows)
        col_min, col_max = min(cols), max(cols)
        width = col_max - col_min + 1
        height = row_max - row_min + 1
        window = Window(col_min, row_min, width, height)
        data = dataset.read(1, window=window, masked=masked)
        raster_reads += 1
        pixels_loaded += width * height
        point_count += len(requests)

        for out_index, row, col in requests:
            value = data[int(row) - row_min, int(col) - col_min]
            if masked and ma.is_masked(value):
                output[int(out_index)] = None
            else:
                output[int(out_index)] = float(value)

    if seen != set(range(output_size)):
        missing = sorted(set(range(output_size)) - seen)
        raise ValueError(f"requete(s) sans tuile: {missing[:8]}")
    metrics = {
        "point_count": point_count,
        "tile_count": sum(1 for requests in grouped.values() if requests),
        "raster_reads": raster_reads,
        "pixels_loaded": pixels_loaded,
    }
    return output, metrics


class MeasurementTable:
    """Table NPZ atomique ; une entrée est un lot ordonné de mesures."""

    FORMAT_VERSION = 1

    def __init__(self, cache_dir: Path, key: str, inputs: Mapping[str, str]) -> None:
        self.key = str(key)
        self.inputs = dict(inputs)
        self.root = Path(cache_dir) / "measurements" / "g6"
        self.data_path = self.root / f"{self.key}.npz"
        self.manifest_path = self.root / f"{self.key}.json"
        self._entries: Dict[str, Dict[str, Any]] = {}
        self._pending: Dict[str, Tuple[np.ndarray, np.ndarray]] = {}
        self._archive = None
        self.cache_hits = 0
        self.cache_misses = 0
        self.writes = 0
        self._load_if_valid()

    def _load_if_valid(self) -> None:
        if not self.data_path.is_file() or not self.manifest_path.is_file():
            return
        try:
            manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
            if manifest.get("format_version") != self.FORMAT_VERSION:
                return
            if manifest.get("key") != self.key or manifest.get("inputs") != self.inputs:
                return
            if manifest.get("data_sha256") != _sha256_file(self.data_path):
                return
            entries = manifest.get("batches")
            if not isinstance(entries, dict):
                return
            archive = np.load(self.data_path, allow_pickle=False)
            for metadata in entries.values():
                entry = str(metadata["entry"])
                if f"{entry}_values" not in archive or f"{entry}_valid" not in archive:
                    archive.close()
                    return
            self._entries = entries
            self._archive = archive
        except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError):
            self._entries = {}
            self._archive = None

    @staticmethod
    def _entry_name(batch_id: str) -> str:
        return hashlib.sha256(batch_id.encode("utf-8")).hexdigest()

    def get(self, batch_id: str, point_count: int) -> Optional[list[Optional[float]]]:
        metadata = self._entries.get(batch_id)
        if metadata is None or int(metadata.get("count", -1)) != int(point_count):
            self.cache_misses += 1
            return None
        entry = str(metadata["entry"])
        arrays = self._pending.get(entry)
        if arrays is None:
            if self._archive is None:
                self.cache_misses += 1
                return None
            try:
                arrays = (
                    np.asarray(self._archive[f"{entry}_values"]),
                    np.asarray(self._archive[f"{entry}_valid"]),
                )
            except KeyError:
                self.cache_misses += 1
                return None
        values, valid = arrays
        if len(values) != point_count or len(valid) != point_count:
            self.cache_misses += 1
            return None
        self.cache_hits += 1
        return [
            float(value) if bool(is_valid) else None
            for value, is_valid in zip(values, valid)
        ]

    def put(self, batch_id: str, values: Sequence[Optional[float]]) -> None:
        entry = self._entry_name(batch_id)
        numeric = np.array(
            [0.0 if value is None else float(value) for value in values],
            dtype=np.float64,
        )
        valid = np.array([value is not None for value in values], dtype=np.bool_)
        self._pending[entry] = (numeric, valid)
        self._entries[batch_id] = {"entry": entry, "count": len(values)}

    def save(self) -> None:
        if not self._pending:
            return
        self.root.mkdir(parents=True, exist_ok=True)
        arrays: Dict[str, np.ndarray] = {}
        if self._archive is not None:
            for metadata in self._entries.values():
                entry = str(metadata["entry"])
                if entry in self._pending:
                    continue
                arrays[f"{entry}_values"] = np.asarray(
                    self._archive[f"{entry}_values"]
                )
                arrays[f"{entry}_valid"] = np.asarray(
                    self._archive[f"{entry}_valid"]
                )
            # Windows refuse de remplacer un NPZ encore ouvert.
            self._archive.close()
            self._archive = None
        for entry, (values, valid) in self._pending.items():
            arrays[f"{entry}_values"] = values
            arrays[f"{entry}_valid"] = valid

        data_temp = tempfile.NamedTemporaryFile(
            mode="w+b",
            prefix=f".{self.key}.",
            suffix=".npz.tmp",
            dir=self.root,
            delete=False,
        )
        data_temp_path = Path(data_temp.name)
        try:
            with data_temp:
                np.savez_compressed(data_temp, **arrays)
                data_temp.flush()
                os.fsync(data_temp.fileno())
            os.replace(data_temp_path, self.data_path)
        finally:
            if data_temp_path.exists():
                data_temp_path.unlink()

        manifest = {
            "format_version": self.FORMAT_VERSION,
            "key": self.key,
            "inputs": self.inputs,
            "data_sha256": _sha256_file(self.data_path),
            "batches": self._entries,
        }
        manifest_temp = tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            prefix=f".{self.key}.",
            suffix=".json.tmp",
            dir=self.root,
            delete=False,
        )
        manifest_temp_path = Path(manifest_temp.name)
        try:
            with manifest_temp:
                json.dump(
                    manifest,
                    manifest_temp,
                    ensure_ascii=False,
                    sort_keys=True,
                    separators=(",", ":"),
                )
                manifest_temp.flush()
                os.fsync(manifest_temp.fileno())
            os.replace(manifest_temp_path, self.manifest_path)
        finally:
            if manifest_temp_path.exists():
                manifest_temp_path.unlink()

        self._archive = np.load(self.data_path, allow_pickle=False)
        self._pending.clear()
        self.writes += 1

    def close(self) -> None:
        if self._archive is not None:
            self._archive.close()
            self._archive = None
