#!/usr/bin/env python
"""Téléchargement idempotent et vérification des tuiles Copernicus DEM GLO-90.

Motif S3 : ``<stem>/<stem>.tif`` sur ``copernicus-dem-90m.s3.amazonaws.com``.
Cache historique : ``sources/dem_cache/<stem>/<stem>.tif``. Si
``FORGEHISTORY_DEM_CACHE_ROOT`` est définie, le cache effectif devient
``<racine>/<sha256-sources.lock>/<stem>/<stem>.tif``. Il n'est jamais committé.

Usage, depuis ``pipeline/geo/`` :
  ../../.venv/bin/python tools/fetch_dem_tiles.py
"""

from __future__ import annotations

import os
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path
from typing import Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
TOOLS = Path(__file__).resolve().parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from io_util import read_json, sha256_bytes, sha256_file  # noqa: E402
from dem_cache_policy import (  # noqa: E402
    exclusive_download_lock,
    resolve_dem_cache_dir,
    source_lock_sha256,
    unexpected_tif_files,
)

LOCK_PATH = ROOT / "sources.lock"
HISTORICAL_CACHE_DIR = ROOT / "sources" / "dem_cache"
CACHE_DIR = resolve_dem_cache_dir(geo_root=ROOT, lock_path=LOCK_PATH)
S3_HOST = "copernicus-dem-90m.s3.amazonaws.com"

# Recette collective : SHA256 de la concaténation triée ``<nom_tuile><sha256_tuile>``.
COLLECTIVE_RECIPE = "sha256_concat_sorted_name_plus_tile_sha256_hex"


def tile_cache_path(tile_name: str, cache_dir: Optional[Path] = None) -> Path:
    stem = Path(tile_name).stem
    return Path(cache_dir or CACHE_DIR) / stem / tile_name


def tile_s3_url(tile_name: str) -> str:
    stem = Path(tile_name).stem
    return f"https://{S3_HOST}/{stem}/{tile_name}"


def load_dem_spec(
    lock_path: Path = LOCK_PATH,
) -> Tuple[Dict[str, dict], str, int]:
    lock = read_json(Path(lock_path))
    dem = lock["dem"]
    tiles = dem["tiles"]
    return tiles, dem["collective_sha256"], int(dem["tile_count"])


def compute_collective_sha256(tile_shas: Dict[str, str]) -> str:
    """Empreinte collective : SHA256 de ``nom_tuile + sha256_tuile`` triés par nom."""
    payload = "".join(f"{name}{tile_shas[name]}" for name in sorted(tile_shas))
    return sha256_bytes(payload.encode("ascii"))


def try_collective_recipes(
    tile_shas: Dict[str, str], expected: str, *, cache_dir: Optional[Path] = None
) -> Tuple[str, str]:
    """Teste plusieurs recettes ; retourne (recipe_name, sha) si une correspond."""
    names = sorted(tile_shas)
    candidates = [
        (
            COLLECTIVE_RECIPE,
            compute_collective_sha256(tile_shas),
        ),
        (
            "sha256_concat_sorted_tile_sha256_hex",
            sha256_bytes("".join(tile_shas[n] for n in names).encode("ascii")),
        ),
        (
            "sha256_concat_sorted_tile_bytes",
            sha256_bytes(
                b"".join(
                    tile_cache_path(n, cache_dir=cache_dir).read_bytes()
                    for n in names
                )
            ),
        ),
        (
            "sha256_concat_name_colon_sha_lines",
            sha256_bytes(
                "".join(f"{n}:{tile_shas[n]}\n" for n in names).encode("ascii")
            ),
        ),
    ]
    for recipe, digest in candidates:
        if digest == expected:
            return recipe, digest
    return COLLECTIVE_RECIPE, candidates[0][1]


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
    temp_handle = tempfile.NamedTemporaryFile(
        mode="w+b",
        prefix=f".{dest.name}.",
        suffix=".part",
        dir=dest.parent,
        delete=False,
    )
    temp_path = Path(temp_handle.name)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = resp.read()
        with temp_handle:
            temp_handle.write(data)
            temp_handle.flush()
            os.fsync(temp_handle.fileno())
        os.replace(temp_path, dest)
    except urllib.error.HTTPError as exc:
        temp_handle.close()
        raise RuntimeError(f"HTTP {exc.code} pour {tile_name} ({url})") from exc
    except urllib.error.URLError as exc:
        temp_handle.close()
        raise RuntimeError(f"reseau pour {tile_name} ({url}): {exc}") from exc
    finally:
        if not temp_handle.closed:
            temp_handle.close()
        if temp_path.exists():
            temp_path.unlink()


def ensure_dem_cache(
    *,
    download: bool = True,
    lock_path: Path = LOCK_PATH,
    cache_dir: Optional[Path] = None,
) -> dict:
    """Vérifie les tuiles déclarées et les télécharge si demandé."""
    lock_path = Path(lock_path)
    effective_cache = Path(cache_dir) if cache_dir is not None else resolve_dem_cache_dir(
        geo_root=ROOT, lock_path=lock_path
    )
    tiles_spec, expected_collective, expected_count = load_dem_spec(lock_path)
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
        path = tile_cache_path(tile_name, cache_dir=effective_cache)

        if not verify_tile(path, expected_sha, expected_bytes):
            if download:
                with exclusive_download_lock(path):
                    # Un autre processus a pu terminer pendant notre attente.
                    if not verify_tile(path, expected_sha, expected_bytes):
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
    recipe_used = COLLECTIVE_RECIPE
    if verified == expected_count and not collective_ok:
        recipe_used, collective_try = try_collective_recipes(
            tile_shas, expected_collective, cache_dir=effective_cache
        )
        if collective_try == expected_collective:
            collective = collective_try
            collective_ok = True

    # Recalculé après les téléchargements pour fermer la fenêtre concurrente.
    unexpected = unexpected_tif_files(effective_cache, set(tiles_spec))
    if unexpected:
        failures.append(
            f"cache hors lock: {len(unexpected)} fichier(s) — {unexpected[:3]}"
        )
    ok = verified == expected_count and not failures and collective_ok
    lock_fingerprint = source_lock_sha256(lock_path)
    return {
        "tile_count": expected_count,
        "verified": verified,
        "downloaded": downloaded,
        "collective_sha256": collective,
        "collective_expected": expected_collective,
        "collective_ok": collective_ok,
        "collective_recipe": recipe_used,
        "failures": failures,
        "unexpected_files": unexpected,
        "cache_dir": str(effective_cache),
        "cache_key": lock_fingerprint,
        "source_lock_sha256": lock_fingerprint,
        "shared_cache": effective_cache.resolve()
        != HISTORICAL_CACHE_DIR.resolve(),
        "ok": ok,
    }


def main() -> int:
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
