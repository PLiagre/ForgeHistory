"""Sentinelle rapide SC9/SC10 : cache DEM et lecture groupée G6.

Les rasters de cette suite sont créés dans un répertoire temporaire. Aucun
octet Copernicus ni téléchargement réseau n'est nécessaire.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import threading
import time
from contextlib import ExitStack
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import numpy as np
import pytest
import rasterio
from rasterio.transform import from_origin
from shapely.geometry import box

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))


def _load(relative_path: str, module_name: str):
    path = ROOT / relative_path
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@pytest.fixture
def cache_policy():
    return _load("tools/dem_cache_policy.py", "dem_cache_policy_sentinel")


@pytest.fixture
def batch_io():
    return _load("tools/dem_batch.py", "dem_batch_sentinel")


@pytest.fixture
def fetch_dem():
    return _load("tools/fetch_dem_tiles.py", "fetch_dem_sentinel")


@pytest.fixture
def relief():
    return _load("steps/06_relief.py", "relief_g6_sentinel")


def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _write_lock(path: Path, tiles: dict[str, bytes]) -> None:
    tile_meta = {
        name: {"bytes": len(data), "sha256": _sha(data)}
        for name, data in sorted(tiles.items())
    }
    collective_payload = "".join(
        f"{name}{tile_meta[name]['sha256']}" for name in sorted(tile_meta)
    ).encode("ascii")
    path.write_text(
        json.dumps(
            {
                "dem": {
                    "collective_sha256": _sha(collective_payload),
                    "tile_count": len(tile_meta),
                    "tiles": tile_meta,
                }
            },
            separators=(",", ":"),
        ),
        encoding="utf-8",
    )


def _tile_path(cache_dir: Path, tile_name: str) -> Path:
    return cache_dir / Path(tile_name).stem / tile_name


def _write_raster(path: Path, west: float, values: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with rasterio.open(
        path,
        "w",
        driver="GTiff",
        width=4,
        height=4,
        count=1,
        dtype="float32",
        crs="EPSG:4326",
        transform=from_origin(west, 43.0, 0.25, 0.25),
        nodata=-9999.0,
    ) as dataset:
        dataset.write(values.astype("float32"), 1)


def test_cache_historique_partage_et_invalidation(tmp_path: Path, cache_policy) -> None:
    lock_path = tmp_path / "sources.lock"
    _write_lock(lock_path, {"tile-a.tif": b"a"})

    historical = cache_policy.resolve_dem_cache_dir(
        geo_root=ROOT, lock_path=lock_path, environ={}
    )
    assert historical == ROOT / "sources" / "dem_cache"

    shared_root = tmp_path / "shared"
    env = {cache_policy.DEM_CACHE_ROOT_ENV: str(shared_root)}
    first = cache_policy.resolve_dem_cache_dir(
        geo_root=ROOT, lock_path=lock_path, environ=env
    )
    first_key = cache_policy.source_lock_sha256(lock_path)
    assert first == shared_root / first_key
    assert len(first_key) == 64

    _write_lock(lock_path, {"tile-a.tif": b"changed"})
    second = cache_policy.resolve_dem_cache_dir(
        geo_root=ROOT, lock_path=lock_path, environ=env
    )
    assert second.parent == shared_root
    assert second != first


def test_cache_refuse_fichier_hors_lock_et_octets_perimes(
    tmp_path: Path, fetch_dem
) -> None:
    tile_name = "Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif"
    expected = b"octets-verifies"
    lock_path = tmp_path / "sources.lock"
    cache_dir = tmp_path / "cache"
    _write_lock(lock_path, {tile_name: expected})

    target = _tile_path(cache_dir, tile_name)
    target.parent.mkdir(parents=True)
    target.write_bytes(expected)
    extra = _tile_path(
        cache_dir, "Copernicus_DSM_COG_30_N42_00_E001_00_DEM.tif"
    )
    extra.parent.mkdir(parents=True)
    extra.write_bytes(b"hors-lock")

    report = fetch_dem.ensure_dem_cache(
        download=False, lock_path=lock_path, cache_dir=cache_dir
    )
    assert report["verified"] == 1
    assert report["unexpected_files"] == [str(extra)]
    assert report["ok"] is False

    extra.unlink()
    target.write_bytes(b"stale")
    stale = fetch_dem.ensure_dem_cache(
        download=False, lock_path=lock_path, cache_dir=cache_dir
    )
    assert stale["verified"] == 0
    assert stale["ok"] is False
    assert stale["failures"]


def test_verrou_exclusif_serialise_deux_telechargements(
    tmp_path: Path, cache_policy
) -> None:
    target = tmp_path / "tile.tif"
    attempted = threading.Event()

    def contender() -> str:
        attempted.set()
        with cache_policy.exclusive_download_lock(target, timeout_s=2.0):
            return "entered"

    with ThreadPoolExecutor(max_workers=1) as pool:
        with cache_policy.exclusive_download_lock(target, timeout_s=2.0):
            future = pool.submit(contender)
            assert attempted.wait(timeout=1.0)
            time.sleep(0.05)
            assert not future.done()
        assert future.result(timeout=1.0) == "entered"


def test_deux_verifications_concurrentes_ne_telechargent_qu_une_fois(
    tmp_path: Path, fetch_dem, monkeypatch
) -> None:
    tile_name = "Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif"
    expected = b"tuile-atomique"
    lock_path = tmp_path / "sources.lock"
    cache_dir = tmp_path / "cache"
    _write_lock(lock_path, {tile_name: expected})
    calls = 0
    calls_guard = threading.Lock()

    def fake_download(_tile_name: str, destination: Path) -> None:
        nonlocal calls
        with calls_guard:
            calls += 1
        time.sleep(0.05)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(expected)

    monkeypatch.setattr(fetch_dem, "download_tile", fake_download)

    def verify() -> dict:
        return fetch_dem.ensure_dem_cache(
            download=True, lock_path=lock_path, cache_dir=cache_dir
        )

    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(verify), pool.submit(verify)]
        reports = [future.result() for future in futures]

    assert calls == 1
    assert all(report["ok"] for report in reports)
    assert sum(report["downloaded"] for report in reports) == 1


def test_sentinelle_reliefs_cote_plaine_degre_nodata_zero(
    tmp_path: Path, relief, batch_io
) -> None:
    """Alpes/Pyrénées/plaine/côte synthétiques, sans cache Europe."""
    west_name = "Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif"
    east_name = "Copernicus_DSM_COG_30_N42_00_E001_00_DEM.tif"
    cache_dir = tmp_path / "cache"
    lock_path = tmp_path / "sources.lock"

    west = np.full((4, 4), 100.0, dtype="float32")
    west[0] = [3000.0, 1500.0, 50.0, 0.0]
    east = np.full((4, 4), 200.0, dtype="float32")
    east[0, 0] = -9999.0
    _write_raster(_tile_path(cache_dir, west_name), 0.0, west)
    _write_raster(_tile_path(cache_dir, east_name), 1.0, east)
    _write_lock(
        lock_path,
        {
            west_name: _tile_path(cache_dir, west_name).read_bytes(),
            east_name: _tile_path(cache_dir, east_name).read_bytes(),
        },
    )

    table = batch_io.MeasurementTable(
        cache_dir, "c" * 64, {"sentinelle": "reliefs-cote-plaine"}
    )
    dem = relief.DemSampler(
        cache_dir, lock_path=lock_path, measurement_table=table
    )
    points = [
        (0.125, 42.875),  # Alpes synthétiques
        (0.375, 42.875),  # Pyrénées synthétiques
        (0.625, 42.875),  # plaine
        (0.875, 42.875),  # côte, vrai zéro mesuré
        (1.125, 42.875),  # nodata déclaré
        (1.0, 42.875),  # frontière de degré, tuile est
    ]
    values = dem.read_many(points, measurement_id="sentinel:reliefs")
    assert values[:4] == [3000.0, 1500.0, 50.0, 0.0]
    assert values[4] is None
    assert values[5] is None
    assert dem.last_batch_metrics["point_count"] == len(points)
    assert dem.last_batch_metrics["tile_count"] == 2
    assert dem.last_batch_metrics["raster_reads"] == 2
    assert dem.last_batch_metrics["raster_reads"] < len(points)
    assert dem.counters.echantillons_valeur_zero_exact == 1
    assert dem.counters.echantillons_nodata_raster == 2
    cold_counters = dem.counters.public_ints()
    table.save()
    dem.close()

    # Une nouvelle passe lit la table figée, pas les pixels.
    replay_table = batch_io.MeasurementTable(
        cache_dir, "c" * 64, {"sentinelle": "reliefs-cote-plaine"}
    )
    replay = relief.DemSampler(
        cache_dir, lock_path=lock_path, measurement_table=replay_table
    )

    def forbidden_dataset(_path):
        raise AssertionError("la seconde passe ne doit pas rouvrir Rasterio")

    replay._dataset = forbidden_dataset
    assert replay.read_many(points, measurement_id="sentinel:reliefs") == values
    assert replay.last_batch_metrics["raster_reads"] == 0
    assert replay.sampling_metrics["measurement_cache_hits"] == len(points)
    assert replay.counters.public_ints() == cold_counters
    replay.close()
    replay_table.close()


def test_compteurs_domaine_zero_nodata_et_bornes(relief) -> None:
    class IdentityProjector:
        @staticmethod
        def project_xy(lon: float, lat: float):
            return lon, lat

        @staticmethod
        def unproject_xy(x: float, y: float):
            return x, y

    class SentinelDem:
        def read_many(self, points, *, measurement_id=None, **_kwargs):
            grid_count = len(points) - 1
            assert measurement_id == "cell:7:grid_and_centroid"
            assert grid_count >= 5
            grid = [0.0, None, -81.0, 4801.0] + [100.0] * (grid_count - 4)
            return grid + [50.0]

    geom = box(0.0, 0.0, 0.04, 0.04)
    cell = {"cell_id": 7, "centroid": {"lon": 0.02, "lat": 0.02}}
    record, excluded = relief.compute_cell_relief(
        cell, geom, IdentityProjector(), SentinelDem()
    )
    grid_count = len(
        relief.grid_points_in_polygon(geom, IdentityProjector())
    )
    assert excluded == 2
    assert record["sample_count"] == grid_count - 3
    assert record["elev_min_m"] == 0.0
    assert record["centroid_elev_m"] == 50.0


def test_lecture_groupee_masked_conserve_zero_et_nodata(
    tmp_path: Path, batch_io
) -> None:
    path = tmp_path / "tiny.tif"
    values = np.arange(16, dtype="float32").reshape(4, 4)
    values[0, 0] = 0.0
    values[0, 1] = -9999.0
    _write_raster(path, 0.0, values)

    with rasterio.open(path) as dataset:
        out, metrics = batch_io.read_grouped_windows(
            {"tiny": [(0, 0, 0), (1, 0, 1), (2, 3, 3)]},
            lambda _key: dataset,
            output_size=3,
            masked=True,
        )

    assert out == [0.0, None, 15.0]
    assert metrics == {
        "point_count": 3,
        "tile_count": 1,
        "raster_reads": 1,
        "pixels_loaded": 16,
    }


def test_dix_mille_points_ne_font_que_deux_lectures_rasterio(
    tmp_path: Path, batch_io
) -> None:
    west_path = tmp_path / "west.tif"
    east_path = tmp_path / "east.tif"
    values = np.arange(16, dtype="float32").reshape(4, 4)
    _write_raster(west_path, 0.0, values)
    _write_raster(east_path, 1.0, values + 100.0)
    grouped = {
        "west": [(index, index % 4, (index // 4) % 4) for index in range(5000)],
        "east": [
            (index, index % 4, (index // 4) % 4)
            for index in range(5000, 10_000)
        ],
    }

    with ExitStack() as stack:
        datasets = {
            "west": stack.enter_context(rasterio.open(west_path)),
            "east": stack.enter_context(rasterio.open(east_path)),
        }
        output, metrics = batch_io.read_grouped_windows(
            grouped,
            datasets.__getitem__,
            output_size=10_000,
            masked=False,
        )

    assert len(output) == 10_000
    assert metrics["point_count"] == 10_000
    assert metrics["tile_count"] == 2
    assert metrics["raster_reads"] == 2
    assert metrics["point_count"] // metrics["raster_reads"] == 5000


def test_cle_table_mesures_invalidee_par_chaque_entree(
    tmp_path: Path, batch_io
) -> None:
    paths = {
        "sources_lock": tmp_path / "sources.lock",
        "cells": tmp_path / "cells_g3.json",
        "adjacency": tmp_path / "adjacency_g5.json",
        "sampling_code": tmp_path / "sampling.py",
    }
    for name, path in paths.items():
        path.write_text(name, encoding="utf-8")

    kwargs = {
        **paths,
        "sample_step": 1.0 / 120.0,
    }
    base_key, base_inputs = batch_io.measurement_table_key(**kwargs)
    assert len(base_key) == 64
    assert set(base_inputs) >= {
        "sources.lock",
        "cells_g3.json",
        "adjacency_g5.json",
        "sampling_code",
        "sample_step",
        "domain_rule",
    }
    extra = tmp_path / "constants.py"
    extra.write_text("G6=1", encoding="utf-8")
    with_extra, _ = batch_io.measurement_table_key(
        **kwargs, extra_files={"constants.py": extra}
    )
    assert with_extra != base_key
    extra.write_text("G6=2", encoding="utf-8")
    changed_extra, _ = batch_io.measurement_table_key(
        **kwargs, extra_files={"constants.py": extra}
    )
    assert changed_extra != with_extra
    other_rule, _ = batch_io.measurement_table_key(
        **kwargs, domain_rule="D19-v2"
    )
    assert other_rule != base_key

    for field, path in paths.items():
        original = path.read_text(encoding="utf-8")
        path.write_text(original + "-changed", encoding="utf-8")
        changed_key, _ = batch_io.measurement_table_key(**kwargs)
        assert changed_key != base_key, field
        path.write_text(original, encoding="utf-8")

    changed_step, _ = batch_io.measurement_table_key(
        **{**paths, "sample_step": 1.0 / 60.0}
    )
    assert changed_step != base_key


def test_table_mesures_persistante_rejoue_none_et_vrai_zero(
    tmp_path: Path, batch_io
) -> None:
    inputs = {"source": "fingerprint", "sample_step": "0.01"}
    first = batch_io.MeasurementTable(tmp_path, "a" * 64, inputs)
    assert first.get("sentinel", 3) is None
    first.put("sentinel", [0.0, None, 3000.0])
    first.save()
    first.close()

    second = batch_io.MeasurementTable(tmp_path, "a" * 64, inputs)
    assert second.get("sentinel", 3) == [0.0, None, 3000.0]
    assert second.get("sentinel", 2) is None
    assert second.cache_hits == 1
    second.put("reprise", [50.0])
    second.save()
    assert second.get("sentinel", 3) == [0.0, None, 3000.0]
    assert second.get("reprise", 1) == [50.0]

    invalidated = batch_io.MeasurementTable(tmp_path, "b" * 64, inputs)
    assert invalidated.get("sentinel", 3) is None

    second.close()
    with first.data_path.open("ab") as handle:
        handle.write(b"corruption-volontaire")
    corrupted = batch_io.MeasurementTable(tmp_path, "a" * 64, inputs)
    assert corrupted.get("sentinel", 3) is None


def test_bornes_ouest_sud_et_latitudes_entieres(relief) -> None:
    west = "Copernicus_DSM_COG_30_N42_00_W001_00_DEM.tif"
    south = "Copernicus_DSM_COG_30_S042_00_E000_00_DEM.tif"
    assert relief._tile_bounds_from_name(west) == (-1.0, 42.0, 0.0, 43.0)
    assert relief._tile_bounds_from_name(south) == (0.0, -42.0, 1.0, -41.0)
    assert relief.lonlat_to_tile_name(12.0, 33.0).endswith("N32_00_E012_00_DEM.tif")
    assert relief.lonlat_to_tile_name_nominal(12.0, 33.0).endswith(
        "N33_00_E012_00_DEM.tif"
    )


def test_registrement_pixel_surface_et_pixel_point(tmp_path: Path, relief) -> None:
    surface = tmp_path / "surface.tif"
    point = tmp_path / "point.tif"
    values = np.arange(16, dtype="float32").reshape(4, 4)
    _write_raster(surface, 0.0, values)
    point.parent.mkdir(parents=True, exist_ok=True)
    with rasterio.open(
        point,
        "w",
        driver="GTiff",
        width=4,
        height=4,
        count=1,
        dtype="float32",
        crs="EPSG:4326",
        transform=from_origin(-0.125, 43.125, 0.25, 0.25),
        nodata=-9999.0,
    ) as dataset:
        dataset.write(values.astype("float32"), 1)
    name_s, *_ = relief.measure_tile_registration(surface)
    name_p, *_ = relief.measure_tile_registration(point)
    assert name_s == "pixel_surface"
    assert name_p == "pixel_point"


def test_hors_domaine_leve_sans_clamp(tmp_path: Path, relief) -> None:
    tile_name = "Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif"
    cache_dir = tmp_path / "cache"
    lock_path = tmp_path / "sources.lock"
    _write_raster(_tile_path(cache_dir, tile_name), 0.0, np.ones((4, 4)))
    _write_lock(lock_path, {tile_name: _tile_path(cache_dir, tile_name).read_bytes()})
    dem = relief.DemSampler(cache_dir, lock_path=lock_path)
    try:
        dem.read_elev(200.0, 50.0)
        raise AssertionError("hors domaine doit lever")
    except RuntimeError as exc:
        assert "hors couverture" in str(exc)
        assert "200.0" in str(exc)
    finally:
        dem.close()


def test_lot_multi_tuile_et_eviction_lru(tmp_path: Path, relief) -> None:
    cache_dir = tmp_path / "cache"
    lock_path = tmp_path / "sources.lock"
    tiles: dict[str, bytes] = {}
    points = []
    for index in range(49):
        lon = float(index)
        name = f"Copernicus_DSM_COG_30_N42_00_E{index:03d}_00_DEM.tif"
        path = _tile_path(cache_dir, name)
        values = np.full((4, 4), float(index + 1), dtype="float32")
        _write_raster(path, lon, values)
        tiles[name] = path.read_bytes()
        points.append((lon + 0.125, 42.875))
    _write_lock(lock_path, tiles)
    dem = relief.DemSampler(cache_dir, lock_path=lock_path)
    try:
        values = dem.read_many(points[:2], measurement_id="multi:2")
        assert values == [1.0, 2.0]
        assert dem.last_batch_metrics["tile_count"] == 2
        assert dem.last_batch_metrics["raster_reads"] == 2
        for lon, lat in points:
            dem.read_elev(lon, lat)
        assert len(dem._datasets) <= relief.DATASET_CACHE_MAX
        assert len(dem._datasets) == relief.DATASET_CACHE_MAX
    finally:
        dem.close()


def test_dix_mille_points_dem_sampler_deux_ouvertures(
    tmp_path: Path, relief
) -> None:
    west_name = "Copernicus_DSM_COG_30_N42_00_E000_00_DEM.tif"
    east_name = "Copernicus_DSM_COG_30_N42_00_E001_00_DEM.tif"
    cache_dir = tmp_path / "cache"
    lock_path = tmp_path / "sources.lock"
    _write_raster(_tile_path(cache_dir, west_name), 0.0, np.full((4, 4), 10.0))
    _write_raster(_tile_path(cache_dir, east_name), 1.0, np.full((4, 4), 20.0))
    _write_lock(
        lock_path,
        {
            west_name: _tile_path(cache_dir, west_name).read_bytes(),
            east_name: _tile_path(cache_dir, east_name).read_bytes(),
        },
    )
    points = [(0.125, 42.875)] * 5000 + [(1.125, 42.875)] * 5000
    dem = relief.DemSampler(cache_dir, lock_path=lock_path)
    try:
        values = dem.read_many(points, measurement_id="lot:10000")
        assert len(values) == 10_000
        assert values[0] == 10.0
        assert values[-1] == 20.0
        assert dem.last_batch_metrics["tile_count"] == 2
        assert dem.last_batch_metrics["raster_reads"] == 2
        assert dem.last_batch_metrics["raster_reads"] < len(points)
    finally:
        dem.close()


def test_aucun_raster_de_sentinelle_n_est_ecrit_dans_le_depot() -> None:
    tracked_fixture_dir = ROOT / "tests" / "fixtures"
    if tracked_fixture_dir.exists():
        assert not list(tracked_fixture_dir.rglob("*.tif"))
        assert not list(tracked_fixture_dir.rglob("*.tiff"))
