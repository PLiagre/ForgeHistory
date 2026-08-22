#!/usr/bin/env python
"""Test ciblé brief 024 A2 : ensure_dem_cache publie les bornes nom vs raster.

Usage, depuis ``pipeline/geo/`` :
  ../../.venv/bin/python tests/test_ensure_dem_bounds_report.py
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools.fetch_dem_tiles import ensure_dem_cache  # noqa: E402


def main() -> int:
    report = ensure_dem_cache(download=False)
    tile_count = report["tile_count"]
    equal = report.get("tuiles_bornes_nom_vs_raster_egales")
    checked = report.get("tuiles_bornes_verifiees")

    if report["verified"] != tile_count:
        print(
            f"SKIP: cache incomplet ({report['verified']}/{tile_count}), "
            "bornes non exigees"
        )
        return 0

    if equal is None or checked is None:
        print("FAIL: tuiles_bornes_* absentes du rapport ensure_dem_cache")
        return 1

    if checked != tile_count:
        print(f"FAIL: tuiles_bornes_verifiees={checked} attendu={tile_count}")
        return 1

    if equal != tile_count:
        print(
            f"FAIL: tuiles_bornes_nom_vs_raster_egales={equal} "
            f"attendu={tile_count} (divergences dans failures)"
        )
        return 1

    print(
        f"OK: tuiles_bornes_nom_vs_raster_egales={equal}/"
        f"{checked} (tile_count={tile_count})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
