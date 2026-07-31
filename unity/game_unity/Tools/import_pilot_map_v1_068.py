#!/usr/bin/env python3
"""v1_068 — importe les artefacts pilote vérifiés vers StreamingAssets/data/map/."""
from __future__ import annotations

import hashlib
import json
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "sandbox" / "geo" / "artifacts"
DST = ROOT / "game_unity" / "Assets" / "StreamingAssets" / "data" / "map"

COPERNICUS = (
    "© DLR e.V. 2010-2014 et © Airbus Defence and Space GmbH 2014-2018 "
    "fournis dans le cadre de COPERNICUS par l'Union européenne et l'ESA"
)

WANTED = [
    "cell_ids_lod0.png",
    "cell_ids_lod1.png",
    "cell_ids_lod2.png",
    "mask_land_sea_lake_lod0.png",
    "mask_land_sea_lake_lod1.png",
    "mask_land_sea_lake_lod2.png",
    "id_color_table_g10.json",
    "cells_lod0.json",
    "cells_lod1.json",
    "cells_lod2.json",
    "cells_g3.json",
    "cells_relief_g6.json",
    "adjacency_g6.json",
    "ownership_1400.json",
    "borders_derived_g8.json",
    "cells_biomes_a12.json",
    "biome_palette_a12.json",
    "appearance_political_lod2.png",
    "appearance_composite_lod2.png",
    "appearance_political_lod1.png",
    "appearance_composite_lod1.png",
    "MANIFEST_g10.json",
    "MANIFEST_g9.json",
    "MANIFEST_g6.json",
    "MANIFEST_g8.json",
    "MANIFEST_g3.json",
    "MANIFEST_a12.json",
]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def write_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def collect_expected() -> dict[str, tuple[str, str]]:
    expected: dict[str, tuple[str, str]] = {}
    for man_name in (
        "MANIFEST_g10.json",
        "MANIFEST_g9.json",
        "MANIFEST_g6.json",
        "MANIFEST_g8.json",
        "MANIFEST_g3.json",
        "MANIFEST_a12.json",
    ):
        data = json.loads((SRC / man_name).read_text(encoding="utf-8"))
        outs = data.get("outputs") or data.get("sha256") or {}
        for key, digest in outs.items():
            key = key.replace("\\", "/")
            if key.startswith("artifacts/"):
                expected[key[len("artifacts/") :]] = (digest, man_name)
            elif "/" not in key and key.endswith((".png", ".json")):
                expected[key] = (digest, man_name)
        for key, digest in (data.get("unchanged_sha256") or {}).items():
            key = key.replace("\\", "/")
            if key.startswith("artifacts/"):
                expected.setdefault(key[len("artifacts/") :], (digest, man_name + "#unchanged"))
    return expected


def main() -> None:
    DST.mkdir(parents=True, exist_ok=True)
    expected = collect_expected()
    imported = []
    skipped_hash = []
    skipped_missing = []
    total_bytes = 0

    for name in WANTED:
        src = SRC / name
        if not src.exists():
            skipped_missing.append(name)
            continue
        digest = sha256(src)
        exp = expected.get(name)
        if exp is not None and exp[0] != digest:
            skipped_hash.append(
                {
                    "file": name,
                    "expected": exp[0],
                    "actual": digest,
                    "manifest": exp[1],
                }
            )
            continue
        dst = DST / name
        shutil.copy2(src, dst)
        write_meta(dst)
        size = dst.stat().st_size
        total_bytes += size
        imported.append(
            {
                "file": name,
                "sha256": digest,
                "bytes": size,
                "manifest": exp[1] if exp else "self",
                "verified": exp is not None,
            }
        )

    all_arts = sorted(p.name for p in SRC.iterdir() if p.is_file())
    wanted_set = set(WANTED)
    left_out = [n for n in all_arts if n not in wanted_set]

    game_manifest = {
        "task_id": "v1_068",
        "data_class": "pilot_map_import",
        "copernicus_attribution": COPERNICUS,
        "copernicus_note": (
            "Obligation contractuelle DEM Copernicus — crédit dans ce MANIFEST "
            "et panneau d'aide HUD (PilotMapProvider)."
        ),
        "pilot_window_lonlat": [-6.5, 42.0, 8.5, 55.5],
        "lod_by_observation": {
            "World": "lod2",
            "Country": "lod1",
            "Province": "lod0",
        },
        "imported": imported,
        "total_bytes": total_bytes,
        "skipped_hash_mismatch": skipped_hash,
        "skipped_missing": skipped_missing,
        "deliberately_left_out_count": len(left_out),
        "deliberately_left_out": left_out,
        "pipeline_artifact_count": len(all_arts),
    }
    man_path = DST / "MANIFEST.json"
    man_path.write_text(json.dumps(game_manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    write_meta(man_path)

    map_meta = Path(str(DST) + ".meta")
    if not map_meta.exists():
        map_meta.write_text(
            "fileFormatVersion: 2\n"
            f"guid: {uuid.uuid4().hex}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
        )

    print(f"imported={len(imported)} total_bytes={total_bytes} ({total_bytes / 1024 / 1024:.2f} MiB)")
    print(f"skipped_hash={len(skipped_hash)} skipped_missing={len(skipped_missing)} left_out={len(left_out)}")
    if skipped_hash:
        print("HASH MISMATCHES:", json.dumps(skipped_hash, indent=2))
    if skipped_missing:
        print("MISSING", skipped_missing)


if __name__ == "__main__":
    main()
