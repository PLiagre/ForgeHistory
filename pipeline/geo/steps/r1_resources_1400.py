"""R1 — gisements extractifs déclarés de 1400 (v1_081).

Lit les déclarations de data/resources_1400.json et les cellules committées,
rattache chaque gisement par contenance, exporte les artefacts R1.
Aucune quantité, aucun barème — présence, nature et classe qualitative seulement.

Usage :
  ../../.venv/bin/python pipeline.py --source resources_1400
  ../../.venv/bin/python tests/run_proof_r1.py
"""

from __future__ import annotations

import time
from collections import defaultdict
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

from shapely.geometry import Point, shape

ROOT = Path(__file__).resolve().parents[1]

from constants import (  # noqa: E402
    R1_COORDS_CERTAINTY,
    R1_DECLARATIONS_FILE,
    R1_PIPELINE_VERSION,
    R1_PROVENANCE,
    R1_REGISTRY_CREATED,
    R1_VALID_RESOURCE_KINDS,
    R1_VALID_RICHNESS_CLASSES,
    PILOT_WINDOW_LONLAT,
    TARGET_CRS,
)
from io_util import read_json, sha256_file, write_json  # noqa: E402
from projection import Projector, detect_projection  # noqa: E402

ARTIFACTS = ROOT / "artifacts"
CAPTURE = ROOT / "capture"
LOGS = ROOT / "logs"
REGISTRY = ROOT / "registry"
BUILD = ROOT / "build"
DATA = ROOT / "data"


def _in_pilot_window(lon: float, lat: float) -> bool:
    west, south, east, north = PILOT_WINDOW_LONLAT
    return west <= lon <= east and south <= lat <= north


def load_context() -> Dict[str, Any]:
    cells_doc = read_json(ARTIFACTS / "cells_g3.json")
    decl_path = DATA / Path(R1_DECLARATIONS_FILE).name
    declarations = read_json(decl_path)
    cells = cells_doc["cells"]
    cell_geoms = [(int(c["cell_id"]), shape(c["geometry"])) for c in cells]
    return {
        "cells": cells,
        "cell_geoms": cell_geoms,
        "declarations": declarations,
        "declarations_path": decl_path,
        "input_shas": {
            "cells_g3.json": sha256_file(ARTIFACTS / "cells_g3.json"),
            Path(R1_DECLARATIONS_FILE).name: sha256_file(decl_path),
        },
    }


def _find_containing_cell(
    x_m: float, y_m: float, cell_geoms: Sequence[Tuple[int, Any]]
) -> Optional[int]:
    pt = Point(x_m, y_m)
    hits: List[Tuple[int, float]] = []
    for cid, geom in cell_geoms:
        if geom.contains(pt) or geom.covers(pt):
            hits.append((cid, geom.area))
    if not hits:
        return None
    if len(hits) == 1:
        return hits[0][0]
    return max(hits, key=lambda t: t[1])[0]


def derive_resources(
    context: Dict[str, Any],
    *,
    apply_declarations: bool = True,
) -> Dict[str, Any]:
    cells = context["cells"]
    cell_geoms = context["cell_geoms"]
    declarations = context["declarations"]
    projector = Projector(detect_projection())

    deposits_in = declarations.get("deposits") or []
    if not apply_declarations:
        deposits_in = []

    published: List[dict] = []
    outside_window: List[str] = []
    outside_land: List[str] = []
    attached_ids: List[str] = []
    cell_resources: Dict[int, List[str]] = {int(c["cell_id"]): [] for c in cells}

    by_resource: Dict[str, int] = {k: 0 for k in R1_VALID_RESOURCE_KINDS}
    by_certainty: Dict[str, int] = defaultdict(int)
    by_richness: Dict[str, int] = {k: 0 for k in R1_VALID_RICHNESS_CLASSES}

    for dep in sorted(deposits_in, key=lambda d: str(d.get("id", ""))):
        dep_id = str(dep["id"])
        lon = float(dep["lon"])
        lat = float(dep["lat"])
        resource = str(dep["resource"])
        richness = str(dep.get("richness_class", ""))

        if resource in by_resource:
            by_resource[resource] += 1
        by_certainty[str(dep.get("certainty", ""))] += 1
        if richness in by_richness:
            by_richness[richness] += 1

        if not _in_pilot_window(lon, lat):
            outside_window.append(dep_id)
            pub = {k: dep[k] for k in dep}
            pub["cell_id"] = None
            pub["attachment"] = "outside_window"
            published.append(pub)
            continue

        x_m, y_m = projector.project_xy(lon, lat)
        cell_id = _find_containing_cell(x_m, y_m, cell_geoms)
        if cell_id is None:
            outside_land.append(dep_id)
            pub = {k: dep[k] for k in dep}
            pub["cell_id"] = None
            pub["attachment"] = "outside_land"
            published.append(pub)
            continue

        attached_ids.append(dep_id)
        cell_resources[cell_id].append(dep_id)
        pub = {k: dep[k] for k in dep}
        pub["cell_id"] = cell_id
        pub["attachment"] = "contained"
        published.append(pub)

    for cid in cell_resources:
        cell_resources[cid] = sorted(cell_resources[cid])

    multi = sum(1 for ids in cell_resources.values() if len(ids) > 1)
    dotted = sum(1 for ids in cell_resources.values() if ids)

    stats = {
        "pipeline_version": R1_PIPELINE_VERSION,
        "gisements_declares": len(deposits_in),
        "gisements_rattaches": len(attached_ids),
        "gisements_hors_fenetre": outside_window,
        "gisements_hors_terre": outside_land,
        "cellules_dotees": dotted,
        "cellules_totales": len(cells),
        "par_nature": by_resource,
        "par_certitude": dict(sorted(by_certainty.items())),
        "par_classe_de_richesse": by_richness,
        "cellules_a_plusieurs_gisements": multi,
        "apply_declarations": apply_declarations,
    }

    cells_out = [
        {"cell_id": int(c["cell_id"]), "resources": cell_resources[int(c["cell_id"])]}
        for c in sorted(cells, key=lambda x: int(x["cell_id"]))
    ]

    return {
        "published": sorted(published, key=lambda d: str(d["id"])),
        "cells_out": cells_out,
        "stats": stats,
        "projector": projector,
        "attached_ids": attached_ids,
        "containment_checks": [
            (d["id"], int(d["cell_id"]))
            for d in published
            if d.get("attachment") == "contained" and d.get("cell_id") is not None
        ],
    }


def export_r1(
    context: Dict[str, Any],
    derived: Dict[str, Any],
    *,
    output_dir: Optional[Path] = None,
) -> Dict[str, str]:
    out_root = output_dir or ARTIFACTS
    out_root.mkdir(parents=True, exist_ok=True)
    if output_dir is not None:
        reg_dir = output_dir.parent / "registry"
    else:
        reg_dir = REGISTRY
    reg_dir.mkdir(parents=True, exist_ok=True)

    shas: Dict[str, str] = {}

    resources_doc = {
        "pipeline_version": R1_PIPELINE_VERSION,
        "crs": TARGET_CRS,
        "provenance": R1_PROVENANCE,
        "coords_certainty": R1_COORDS_CERTAINTY,
        "deposits": derived["published"],
    }
    cells_doc = {
        "pipeline_version": R1_PIPELINE_VERSION,
        "crs": TARGET_CRS,
        "cells": derived["cells_out"],
    }
    stats_doc = derived["stats"]

    shas["artifacts/resources_1400_r1.json"] = write_json(
        out_root / "resources_1400_r1.json", resources_doc
    )
    shas["artifacts/cells_resources_r1.json"] = write_json(
        out_root / "cells_resources_r1.json", cells_doc
    )
    shas["artifacts/stats_r1.json"] = write_json(out_root / "stats_r1.json", stats_doc)

    registry_doc = {
        "created": R1_REGISTRY_CREATED,
        "data_class": "resources_r1_registry",
        "pipeline_version": R1_PIPELINE_VERSION,
        "deposit_count": len(
            [d for d in derived["published"] if d.get("attachment") == "contained"]
        ),
        "deposits": [
            {"id": str(d["id"]), "resource": str(d["resource"])}
            for d in derived["published"]
            if d.get("attachment") == "contained"
        ],
    }
    shas["registry/resource_registry.json"] = write_json(
        reg_dir / "resource_registry.json", registry_doc
    )

    manifest = {
        "pipeline_version": R1_PIPELINE_VERSION,
        "crs": TARGET_CRS,
        "inputs": context["input_shas"],
        "outputs": {
            "resources_1400_r1.json": shas["artifacts/resources_1400_r1.json"],
            "cells_resources_r1.json": shas["artifacts/cells_resources_r1.json"],
            "stats_r1.json": shas["artifacts/stats_r1.json"],
            "resource_registry.json": shas["registry/resource_registry.json"],
        },
    }
    shas["artifacts/MANIFEST_r1.json"] = write_json(out_root / "MANIFEST_r1.json", manifest)
    return shas


def write_capture(
    context: Dict[str, Any],
    derived: Dict[str, Any],
) -> Path:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.collections import PatchCollection
    from matplotlib.lines import Line2D
    from matplotlib.patches import Polygon as MplPolygon
    from pyproj import Transformer

    CAPTURE.mkdir(parents=True, exist_ok=True)

    inv = Transformer.from_crs(TARGET_CRS, "EPSG:4326", always_xy=True)
    cells = context["cells"]
    cell_res = {int(c["cell_id"]): c["resources"] for c in derived["cells_out"]}
    w, s, e, n = PILOT_WINDOW_LONLAT

    resource_colors = {
        "sel": "#e65100",
        "fer": "#5d4037",
        "cuivre": "#bf360c",
        "argent": "#9e9e9e",
        "etain": "#6a1b9a",
        "plomb": "#455a64",
        "charbon": "#212121",
        "mercure": "#c62828",
        "alun": "#00838f",
        "or": "#f9a825",
    }
    richness_marker = {
        rc: shape
        for rc, shape in zip(R1_VALID_RICHNESS_CLASSES, ("o", "s", "^"))
    }

    def to_ll_ring(geom_xy: Any) -> List[Tuple[float, float]]:
        g = shape(geom_xy)
        poly = g if g.geom_type == "Polygon" else g.geoms[0]
        return [inv.transform(x, y) for x, y in poly.exterior.coords]

    fig, ax = plt.subplots(figsize=(12, 10), dpi=120)
    ax.set_aspect("equal")
    ax.set_facecolor("#e3f2fd")
    ax.set_title("R1 — gisements extractifs déclarés (fenêtre pilote)", fontsize=10)
    ax.set_xlim(w, e)
    ax.set_ylim(s, n)
    ax.grid(True, alpha=0.2)

    empty_patches = []
    dotted_patches = []
    for cell in sorted(cells, key=lambda c: int(c["cell_id"])):
        ring = to_ll_ring(cell["geometry"])
        cid = int(cell["cell_id"])
        if cell_res.get(cid):
            dotted_patches.append(MplPolygon(ring, closed=True))
        else:
            empty_patches.append(MplPolygon(ring, closed=True))

    if empty_patches:
        ax.add_collection(
            PatchCollection(
                empty_patches,
                facecolor="#f5f5f5",
                edgecolor="#bdbdbd",
                linewidth=0.15,
            )
        )
    if dotted_patches:
        ax.add_collection(
            PatchCollection(
                dotted_patches,
                facecolor="#fff9c4",
                edgecolor="#f57f17",
                linewidth=0.2,
            )
        )

    for dep in derived["published"]:
        if dep.get("attachment") != "contained":
            continue
        lon = float(dep["lon"])
        lat = float(dep["lat"])
        res = str(dep["resource"])
        rc = str(dep.get("richness_class", ""))
        color = resource_colors.get(res, "#333333")
        marker = richness_marker.get(rc, "o")
        ax.scatter(
            lon,
            lat,
            c=color,
            marker=marker,
            s=36,
            edgecolors="white",
            linewidths=0.5,
            zorder=5,
        )

    res_handles = [
        Line2D(
            [0],
            [0],
            marker="o",
            color="w",
            markerfacecolor=resource_colors[r],
            markersize=8,
            label=r,
        )
        for r in R1_VALID_RESOURCE_KINDS
        if any(
            d.get("resource") == r and d.get("attachment") == "contained"
            for d in derived["published"]
        )
    ]
    rich_handles = [
        Line2D(
            [0],
            [0],
            marker=richness_marker[rc],
            color="w",
            markerfacecolor="#555555",
            markersize=8,
            label=rc,
        )
        for rc in R1_VALID_RICHNESS_CLASSES
    ]
    ax.legend(handles=res_handles + rich_handles, loc="lower left", fontsize=7, ncol=2)

    out = CAPTURE / "v1_081_resources_window.png"
    fig.savefig(out, format="png", metadata={"Software": None})
    plt.close(fig)
    return out


def format_summary_line(derived: Dict[str, Any], *, apply_declarations: bool) -> str:
    stats = derived["stats"]
    proj = derived["projector"].info
    par = stats.get("par_nature") or {}
    return (
        f"pipeline r1 | projection={proj.epsg} | "
        f"apply_declarations={apply_declarations} | "
        f"gisements_rattaches={stats['gisements_rattaches']} | "
        f"cellules_dotees={stats['cellules_dotees']} | "
        f"par_nature={par}"
    )


def run_resources(
    apply_declarations: bool = True,
    output_dir: Optional[Path] = None,
    *,
    context: Optional[Dict[str, Any]] = None,
    export: bool = True,
    captures: bool = True,
) -> Dict[str, Any]:
    """Dérive et exporte les gisements R1."""
    t_all = time.perf_counter()
    timings: Dict[str, float] = {}
    ctx = context or load_context()

    t = time.perf_counter()
    derived = derive_resources(ctx, apply_declarations=apply_declarations)
    timings["derive"] = time.perf_counter() - t

    shas: Dict[str, str] = {}
    if export:
        t = time.perf_counter()
        shas = export_r1(ctx, derived, output_dir=output_dir)
        timings["export"] = time.perf_counter() - t

    capture_path: Optional[Path] = None
    if captures and export and output_dir is None:
        t = time.perf_counter()
        capture_path = write_capture(ctx, derived)
        timings["capture"] = time.perf_counter() - t

    timings["total"] = time.perf_counter() - t_all
    BUILD.mkdir(parents=True, exist_ok=True)
    write_json(
        BUILD / "99_timings_r1.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )

    return {
        "context": ctx,
        "derived": derived,
        "stats": derived["stats"],
        "published": derived["published"],
        "cells_out": derived["cells_out"],
        "shas": shas,
        "capture": capture_path,
        "projection": derived["projector"].info,
        "timings": timings,
        "containment_checks": derived["containment_checks"],
    }
