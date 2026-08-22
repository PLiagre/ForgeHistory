#!/usr/bin/env python
"""Téléchargement idempotent et vérification des tuiles Copernicus DEM GLO-90.

Motif S3 : ``<stem>/<stem>.tif`` sur ``copernicus-dem-90m.s3.amazonaws.com``.
Cache local : ``sources/dem_cache/<stem>/<stem>.tif`` (jamais committé).

Usage, depuis ``pipeline/geo/`` :
  ../../.venv/bin/python tools/fetch_dem_tiles.py --probe
  ../../.venv/bin/python tools/fetch_dem_tiles.py --download-required
  ../../.venv/bin/python tools/fetch_dem_tiles.py --regenerate-lock
  ../../.venv/bin/python tools/fetch_dem_tiles.py
"""

from __future__ import annotations

import argparse
import json
import math
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from io_util import read_json, sha256_bytes, sha256_file  # noqa: E402

LOCK_PATH = ROOT / "sources.lock"
CACHE_DIR = ROOT / "sources" / "dem_cache"
REQUIRED_ARTIFACT = ROOT / "artifacts" / "dem_required_tiles_g6.json"
AVAILABILITY_ARTIFACT = ROOT / "artifacts" / "dem_tile_availability_g6.json"
S3_HOST = "copernicus-dem-90m.s3.amazonaws.com"

COLLECTIVE_RECIPE = "sha256_concat_sorted_name_plus_tile_sha256_hex"


def tile_cache_path(tile_name: str) -> Path:
    stem = Path(tile_name).stem
    return CACHE_DIR / stem / tile_name


def tile_s3_url(tile_name: str) -> str:
    stem = Path(tile_name).stem
    return f"https://{S3_HOST}/{stem}/{tile_name}"


def load_dem_spec() -> Tuple[Dict[str, dict], str, int, str | None]:
    lock = read_json(LOCK_PATH)
    dem = lock["dem"]
    recipe = dem.get("collective_recipe")
    return dem["tiles"], dem["collective_sha256"], int(dem["tile_count"]), recipe


def compute_collective_sha256(tile_shas: Dict[str, str]) -> str:
    payload = "".join(f"{name}{tile_shas[name]}" for name in sorted(tile_shas))
    return sha256_bytes(payload.encode("ascii"))


def tile_bounds_from_name(tile_name: str) -> Tuple[float, float, float, float]:
    """Retourne (lon_min, lat_min, lon_max, lat_max) — convention D16."""
    stem = Path(tile_name).stem
    token = stem.replace("Copernicus_DSM_COG_30_", "")
    lat_hem = token[0]
    rest = token[1:]
    lat_deg = int(rest.split("_")[0])
    lon_token = rest.split("_", 2)[2]
    lon_hem = lon_token[0]
    lon_deg = int(lon_token[1:].split("_")[0])
    if lat_hem == "N":
        lat_min, lat_max = float(lat_deg), float(lat_deg + 1)
    else:
        lat_min, lat_max = float(-lat_deg), float(-lat_deg + 1)
    if lon_hem == "E":
        lon_min, lon_max = float(lon_deg), float(lon_deg + 1)
    else:
        lon_min, lon_max = float(-lon_deg), float(-lon_deg + 1)
    return lon_min, lat_min, lon_max, lat_max


def tile_bounds_from_raster(path: Path) -> Tuple[float, float, float, float]:
    import rasterio

    with rasterio.open(path) as ds:
        b = ds.bounds
        return float(b.left), float(b.bottom), float(b.right), float(b.top)


def bounds_match(
    a: Tuple[float, float, float, float],
    b: Tuple[float, float, float, float],
    tol_lon: float,
    tol_lat: float,
) -> bool:
    return (
        math.isclose(a[0], b[0], abs_tol=tol_lon + 1e-9)
        and math.isclose(a[1], b[1], abs_tol=tol_lat + 1e-9)
        and math.isclose(a[2], b[2], abs_tol=tol_lon + 1e-9)
        and math.isclose(a[3], b[3], abs_tol=tol_lat + 1e-9)
    )


def measure_registration(path: Path) -> Tuple[str, float, float]:
    import rasterio

    with rasterio.open(path) as ds:
        res_x = abs(float(ds.transform.a))
        res_y = abs(float(ds.transform.e))
        half_px_lon = 0.5 * res_x
        half_px_lat = 0.5 * res_y
        w, h = ds.width, ds.height
        extent_lon = w * res_x
        extent_lat = h * res_y
        b = ds.bounds
        on_degrees = (
            abs(b.left - round(b.left)) < 1e-5
            and abs(b.bottom - round(b.bottom)) < 1e-5
            and abs(b.right - round(b.right)) < 1e-5
            and abs(b.top - round(b.top)) < 1e-5
        )
        if (
            on_degrees
            and abs(extent_lon - 1.0) < 1e-4
            and abs(extent_lat - 1.0) < 1e-4
        ):
            return "pixel_surface", half_px_lon, half_px_lat
        if abs(extent_lon - 1.0) < res_x and abs(extent_lat - 1.0) < res_y:
            return "pixel_point", half_px_lon, half_px_lat
        raise RuntimeError(f"registrement inconnu pour {path.name}")


def bounds_tolerance(reg_name: str, half_px_lon: float, half_px_lat: float) -> Tuple[float, float]:
    if reg_name == "pixel_surface":
        return 1e-6, 1e-6
    return half_px_lon, half_px_lat


def verify_tile(path: Path, expected_sha: str, expected_bytes: int | None) -> bool:
    if not path.is_file():
        return False
    if expected_bytes is not None and path.stat().st_size != expected_bytes:
        return False
    return sha256_file(path) == expected_sha


def download_tile(tile_name: str, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    url = tile_s3_url(tile_name)
    req = urllib.request.Request(url, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = resp.read()
    except urllib.error.HTTPError as exc:
        raise RuntimeError(f"HTTP {exc.code} pour {tile_name} ({url})") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"reseau pour {tile_name} ({url}): {exc}") from exc
    dest.write_bytes(data)


def head_tile(tile_name: str) -> int:
    url = tile_s3_url(tile_name)
    req = urllib.request.Request(url, method="HEAD")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return int(resp.status)
    except urllib.error.HTTPError as exc:
        return int(exc.code)
    except urllib.error.URLError as exc:
        raise RuntimeError(f"reseau HEAD pour {tile_name}: {exc}") from exc


def load_required_tiles() -> List[str]:
    if not REQUIRED_ARTIFACT.is_file():
        raise RuntimeError(
            f"artefact manquant: {REQUIRED_ARTIFACT} — lancer required_dem_tiles.py d'abord"
        )
    doc = read_json(REQUIRED_ARTIFACT)
    return list(doc["tuiles_requises"])


def probe_required_tiles() -> dict:
    tiles = load_required_tiles()
    availability: Dict[str, int] = {}
    missing: List[str] = []
    for tile_name in tiles:
        code = head_tile(tile_name)
        availability[tile_name] = code
        if code != 200:
            missing.append(tile_name)
    report = {
        "tuiles_sondees": len(tiles),
        "tuiles_disponibles": sum(1 for c in availability.values() if c == 200),
        "tuiles_absentes_du_depot_public": missing,
        "par_tuile": availability,
    }
    AVAILABILITY_ARTIFACT.parent.mkdir(parents=True, exist_ok=True)
    AVAILABILITY_ARTIFACT.write_text(
        json.dumps(report, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    if missing:
        raise RuntimeError(
            f"{len(missing)} tuile(s) requise(s) absente(s) du depot public: "
            f"{missing[:8]}{'...' if len(missing) > 8 else ''}"
        )
    return report


def probe_lock_tiles() -> dict:
    """HEAD de toutes les tuiles du bloc dem publié (D20)."""
    tiles_spec, _, expected_count, _ = load_dem_spec()
    availability: Dict[str, int] = {}
    missing: List[str] = []
    for tile_name in sorted(tiles_spec):
        code = head_tile(tile_name)
        availability[tile_name] = code
        if code != 200:
            missing.append(tile_name)
    report = {
        "tuiles_sondees": len(tiles_spec),
        "tuiles_disponibles": sum(1 for c in availability.values() if c == 200),
        "tuiles_absentes_du_depot_public": missing,
        "par_tuile": availability,
    }
    AVAILABILITY_ARTIFACT.parent.mkdir(parents=True, exist_ok=True)
    AVAILABILITY_ARTIFACT.write_text(
        json.dumps(report, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    if missing:
        raise RuntimeError(
            f"{len(missing)} tuile(s) du bloc dem absente(s) du depot public: "
            f"{missing[:8]}{'...' if len(missing) > 8 else ''}"
        )
    return report


def cache_files_hors_lock() -> List[str]:
    tiles_spec, _, _, _ = load_dem_spec()
    lock_names = set(tiles_spec)
    extra: List[str] = []
    if not CACHE_DIR.is_dir():
        return extra
    for path in CACHE_DIR.rglob("*.tif"):
        if path.name not in lock_names:
            extra.append(str(path))
    return sorted(extra)


def download_required_tiles() -> dict:
    tiles = load_required_tiles()
    downloaded = 0
    present = 0
    for tile_name in tiles:
        path = tile_cache_path(tile_name)
        if path.is_file():
            present += 1
            continue
        download_tile(tile_name, path)
        downloaded += 1
        present += 1
    return {
        "required": len(tiles),
        "present_after": present,
        "downloaded": downloaded,
    }


def verify_bounds_all_cached(tiles: Sequence[str]) -> Tuple[int, int, List[str], str, float]:
    equal = 0
    checked = 0
    failures: List[str] = []
    reg_name: str | None = None
    half_px_sample = 0.0
    for tile_name in tiles:
        path = tile_cache_path(tile_name)
        if not path.is_file():
            continue
        checked += 1
        rname, hp_lon, hp_lat = measure_registration(path)
        if reg_name is None:
            reg_name, half_px_sample = rname, hp_lon
        elif rname != reg_name:
            failures.append(f"{tile_name}: registrement={rname} attendu={reg_name}")
            continue
        tol_lon, tol_lat = bounds_tolerance(rname, hp_lon, hp_lat)
        name_bounds = tile_bounds_from_name(tile_name)
        try:
            raster_bounds = tile_bounds_from_raster(path)
        except Exception as exc:  # noqa: BLE001
            failures.append(f"{tile_name}: lecture raster {exc}")
            continue
        if bounds_match(name_bounds, raster_bounds, tol_lon, tol_lat):
            equal += 1
        else:
            failures.append(
                f"{tile_name}: nom={name_bounds} raster={raster_bounds} "
                f"tol=({tol_lon},{tol_lat})"
            )
    return equal, checked, failures, reg_name or "inconnu", half_px_sample


def regenerate_dem_lock() -> dict:
    tiles = load_required_tiles()
    tile_entries: Dict[str, dict] = {}
    tile_shas: Dict[str, str] = {}
    total_bytes = 0
    for tile_name in sorted(tiles):
        path = tile_cache_path(tile_name)
        if not path.is_file():
            raise RuntimeError(f"tuile manquante dans le cache: {tile_name}")
        digest = sha256_file(path)
        nbytes = path.stat().st_size
        tile_entries[tile_name] = {"bytes": nbytes, "sha256": digest}
        tile_shas[tile_name] = digest
        total_bytes += nbytes

    equal, checked, bound_failures, reg_name, half_px = verify_bounds_all_cached(tiles)
    if bound_failures:
        raise RuntimeError(
            "bornes nom vs raster divergentes: " + "; ".join(bound_failures[:5])
        )

    collective = compute_collective_sha256(tile_shas)
    orig_text = LOCK_PATH.read_text(encoding="utf-8")
    orig = json.loads(orig_text)
    orig_dem_licence = orig["dem"]["licence"]
    preserved = {
        k: orig[k]
        for k in ("files", "geonames_cities500", "layer_coverage", "licence", "source_set")
    }
    new_dem = {
        "collective_recipe": COLLECTIVE_RECIPE,
        "collective_sha256": collective,
        "licence": orig_dem_licence,
        "tile_count": len(tile_entries),
        "tiles": tile_entries,
        "total_bytes": total_bytes,
    }
    new_lock = {"dem": new_dem, **preserved}
    new_text = json.dumps(new_lock, ensure_ascii=False, separators=(",", ":"))
    LOCK_PATH.write_text(new_text, encoding="utf-8")

    for key in preserved:
        if json.loads(new_text)[key] != orig[key]:
            raise RuntimeError(f"objet sources.lock altere hors dem: {key}")
    if json.loads(new_text)["dem"]["licence"] != orig_dem_licence:
        raise RuntimeError("dem.licence alteree")

    return {
        "tile_count": len(tile_entries),
        "total_bytes": total_bytes,
        "collective_sha256": collective,
        "collective_recipe": COLLECTIVE_RECIPE,
        "tuiles_bornes_nom_vs_raster_egales": equal,
        "tuiles_bornes_verifiees": checked,
        "registrement_dem_mesure": reg_name,
        "demi_pixel_deg": half_px,
    }


def ensure_dem_cache(*, download: bool = True) -> dict:
    """Vérifie (et télécharge si besoin) les tuiles déclarées dans sources.lock."""
    tiles_spec, expected_collective, expected_count, expected_recipe = load_dem_spec()
    if len(tiles_spec) != expected_count:
        raise RuntimeError(
            f"tile_count={expected_count} mais {len(tiles_spec)} entrees dans sources.lock"
        )

    verified = 0
    downloaded = 0
    tile_shas: Dict[str, str] = {}
    failures: List[str] = []

    for tile_name in sorted(tiles_spec):
        meta = tiles_spec[tile_name]
        expected_sha = meta["sha256"]
        expected_bytes = meta.get("bytes")
        path = tile_cache_path(tile_name)

        if not verify_tile(path, expected_sha, expected_bytes):
            if download:
                download_tile(tile_name, path)
                downloaded += 1
            if not verify_tile(path, expected_sha, expected_bytes):
                actual = sha256_file(path) if path.is_file() else "missing"
                failures.append(f"{tile_name}: attendu={expected_sha} calcule={actual}")
                continue

        tile_shas[tile_name] = sha256_file(path)
        verified += 1

    collective = compute_collective_sha256(tile_shas) if verified == expected_count else ""
    collective_ok = collective == expected_collective
    recipe_used = expected_recipe or COLLECTIVE_RECIPE

    bound_equal: int | None = None
    bound_checked: int | None = None
    bound_failures: List[str] = []
    if verified == expected_count:
        bound_equal, bound_checked, bound_failures, _, _ = verify_bounds_all_cached(
            sorted(tiles_spec)
        )

    all_failures = failures + bound_failures
    ok = verified == expected_count and not all_failures and collective_ok
    return {
        "tile_count": expected_count,
        "verified": verified,
        "downloaded": downloaded,
        "collective_sha256": collective,
        "collective_expected": expected_collective,
        "collective_ok": collective_ok,
        "collective_recipe": recipe_used,
        "recettes_collectives_essayees": 1,
        "failures": all_failures,
        "ok": ok,
        "tuiles_bornes_nom_vs_raster_egales": bound_equal,
        "tuiles_bornes_verifiees": bound_checked,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Cache Copernicus DEM GLO-90")
    parser.add_argument("--probe", action="store_true", help="HEAD de toutes les tuiles requises")
    parser.add_argument(
        "--download-required",
        action="store_true",
        help="telecharge les tuiles listees dans dem_required_tiles_g6.json",
    )
    parser.add_argument(
        "--regenerate-lock",
        action="store_true",
        help="re-ecrit uniquement le bloc dem de sources.lock depuis le cache",
    )
    parser.add_argument(
        "--probe-lock",
        action="store_true",
        help="HEAD de toutes les tuiles du bloc dem publie",
    )
    args = parser.parse_args()

    if args.probe_lock:
        report = probe_lock_tiles()
        print(
            f"probe-lock: {report['tuiles_disponibles']}/{report['tuiles_sondees']} "
            f"disponibles, absentes={len(report['tuiles_absentes_du_depot_public'])}"
        )
        return 0

    if args.probe:
        report = probe_required_tiles()
        print(
            f"probe: {report['tuiles_disponibles']}/{report['tuiles_sondees']} "
            f"disponibles, absentes={len(report['tuiles_absentes_du_depot_public'])}"
        )
        return 0

    if args.download_required:
        report = download_required_tiles()
        print(
            f"download-required: {report['present_after']}/{report['required']} "
            f"presentes, telechargees={report['downloaded']}"
        )
        return 0

    if args.regenerate_lock:
        report = regenerate_dem_lock()
        print(
            f"regenerate-lock: tile_count={report['tile_count']} "
            f"bytes={report['total_bytes']} collective={report['collective_sha256'][:16]}..."
        )
        return 0

    report = ensure_dem_cache(download=True)
    print(
        f"dem_cache: {report['verified']}/{report['tile_count']} tuiles verifiees, "
        f"telechargees={report['downloaded']}, "
        f"collective_ok={report['collective_ok']} "
        f"(recette={report['collective_recipe']})"
    )
    if report["failures"]:
        for line in report["failures"][:8]:
            print(f"  ECHEC: {line}")
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
