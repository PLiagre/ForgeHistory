"""python3 -m visualisateur — photographie → MNT → PNG 3D."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

# wgpu verrouille le backend au premier contexte. Le poser avant forge3d.
os.environ.setdefault("WGPU_BACKENDS", "vulkan")


def _photographier(ticks: int, seed: int, chemin: Path) -> Path:
    from sim.world import World
    from sim.engine import tick
    from sim.snapshot_export import export_snapshot
    import random

    monde = World.charger(rng_seed=seed)
    rng = random.Random(seed)
    for _ in range(int(ticks)):
        tick(monde, rng)
    return export_snapshot(monde, seed, ticks, chemin)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Regard 3D : lit une photographie de sim/, parle à forge3d."
    )
    parser.add_argument("--snapshot", type=Path, help="Photographie JSON déjà écrite.")
    parser.add_argument("--ticks", type=int, help="Photographier après N ticks, puis rendre.")
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--png", type=Path, required=True, help="Image 3D écrite par forge3d.")
    parser.add_argument(
        "--apercu",
        type=Path,
        help="PNG vue du dessus (contrôle du raster, pas un rendu 3D).",
    )
    parser.add_argument("--largeur", type=int, default=640, help="Largeur du MNT rasterisé.")
    parser.add_argument("--largeur-px", type=int, default=1280, help="Largeur de l'image forge3d.")
    parser.add_argument("--hauteur-px", type=int, default=720, help="Hauteur de l'image forge3d.")
    args = parser.parse_args(argv)

    if args.snapshot is None and args.ticks is None:
        print("Il faut --snapshot ou --ticks.", file=sys.stderr)
        return 2

    if args.snapshot is not None:
        snapshot_path = args.snapshot
        if not snapshot_path.is_file():
            print(f"snapshot introuvable : {snapshot_path}", file=sys.stderr)
            return 2
    else:
        snapshot_path = Path("/tmp/visualisateur-monde.json")
        _photographier(args.ticks, args.seed, snapshot_path)

    document = json.loads(snapshot_path.read_text(encoding="utf-8"))

    from visualisateur.raster import RasterErreur, apercu_altitude, rasteriser
    from visualisateur.rendu import RenduErreur, rendre_png
    from PIL import Image

    try:
        mnt = rasteriser(document, largeur=args.largeur)
    except RasterErreur as exc:
        print(str(exc), file=sys.stderr)
        return 2

    if args.apercu is not None:
        Image.fromarray(apercu_altitude(mnt), mode="RGBA").save(args.apercu)

    try:
        rendre_png(
            mnt,
            args.png,
            largeur_px=args.largeur_px,
            hauteur_px=args.hauteur_px,
        )
    except RenduErreur as exc:
        print(str(exc), file=sys.stderr)
        return 2

    print(
        json.dumps(
            {
                "png": str(args.png),
                "apercu": str(args.apercu) if args.apercu else None,
                "cellules": mnt.cellules,
                "tick": mnt.tick,
                "seed": mnt.seed,
                "mnt": list(mnt.altitudes_m.shape),
            },
            ensure_ascii=False,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
