#!/usr/bin/env python
"""Pipeline cartographique G1 (fixture) + branchement G2 (littoral réel).

G1 enchaîne : ingest → project → seed → cells → derive → attach → export.
G2 branche Natural Earth sur la même mécanique d'I/O / projection, sans cellules.

Usage (depuis sandbox/geo, avec le venv local) :
  .venv/Scripts/python.exe pipeline.py --source fixture
  .venv/Scripts/python.exe pipeline.py --source natural_earth
  .venv/Scripts/python.exe tests/run_proof.py
  .venv/Scripts/python.exe tests/run_proof_g2.py
"""

from __future__ import annotations

import argparse
import importlib.util
import shutil
import sys
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

from shapely import voronoi_polygons
from shapely.geometry import LineString, MultiPoint, MultiPolygon, Point, Polygon, mapping, shape
from shapely.ops import unary_union

from constants import (
    CELL_ID_BASE,
    FLOAT_DECIMALS,
    FORBIDDEN_GAME_PATH_MARKERS,
    LENGTH_EPS,
    PIPELINE_VERSION,
    SEA_CELL_ID,
    SEED_COUNT_MAX,
    SEED_COUNT_MIN,
)
from io_util import read_json, sha256_file, write_json
from projection import Projector, ProjectionInfo, detect_projection

ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures"
BUILD = ROOT / "build"
ARTIFACTS = ROOT / "artifacts"
LOGS = ROOT / "logs"
CAPTURE = ROOT / "capture"

LAND_PATH = FIXTURES / "sample_land.geojson"
RIVERS_PATH = FIXTURES / "sample_rivers.geojson"
CITIES_PATH = FIXTURES / "sample_cities.geojson"


# ---------------------------------------------------------------------------
# Helpers géométrie / fixtures
# ---------------------------------------------------------------------------


def assert_fixture(doc: dict, path: Path) -> None:
    data_class = doc.get("data_class")
    if data_class != "fixture":
        raise ValueError(
            f"{path} doit déclarer data_class='fixture' (reçu {data_class!r}). "
            "Le socle G1 n'accepte que des fixtures de test."
        )
    comment = str(doc.get("comment") or "")
    if "PAS de géographie réelle" not in comment and "pas de geographie reelle" not in comment.lower():
        # Accepte aussi une variante sans accents si besoin, mais exige un commentaire.
        if "fixture" not in comment.lower():
            raise ValueError(f"{path} : commentaire de non-géographie réelle manquant.")


def refuse_game_export(dest: Path, docs: Sequence[dict]) -> None:
    """Refuse d'exporter une fixture vers un chemin de données de jeu."""
    dest_s = str(dest).replace("\\", "/")
    is_fixture = any(d.get("data_class") == "fixture" for d in docs)
    if not is_fixture:
        return
    for marker in FORBIDDEN_GAME_PATH_MARKERS:
        if marker in dest_s:
            raise PermissionError(
                f"REFUS d'export fixture vers chemin de jeu ({marker} dans {dest_s}). "
                "Les fixtures ne sont pas de la géographie réelle."
            )


def load_land_union(doc: dict) -> Any:
    geoms = [shape(f["geometry"]) for f in doc["features"]]
    union = unary_union(geoms)
    if not union.is_valid:
        union = union.buffer(0)
    return union


def polygon_rings_lonlat(geom: Any) -> List[List[List[float]]]:
    """Extrait les anneaux lon/lat d'un Polygon/MultiPolygon (extérieur seulement pour simplicité)."""
    if geom.geom_type == "Polygon":
        return [list(map(list, geom.exterior.coords))]
    if geom.geom_type == "MultiPolygon":
        rings: List[List[List[float]]] = []
        for poly in sorted(geom.geoms, key=lambda g: (g.centroid.x, g.centroid.y)):
            rings.append(list(map(list, poly.exterior.coords)))
        return rings
    raise TypeError(f"géométrie non supportée : {geom.geom_type}")


def project_geometry(geom: Any, projector: Projector) -> Any:
    """Reprojette une géométrie Shapely (lon/lat → projeté)."""

    def _coords(coords):
        return [projector.project_xy(lon, lat) for lon, lat in coords]

    if geom.geom_type == "Point":
        x, y = projector.project_xy(geom.x, geom.y)
        return Point(x, y)
    if geom.geom_type == "LineString":
        return LineString(_coords(list(geom.coords)))
    if geom.geom_type == "Polygon":
        exterior = _coords(list(geom.exterior.coords))
        holes = [_coords(list(r.coords)) for r in geom.interiors]
        return Polygon(exterior, holes)
    if geom.geom_type == "MultiPolygon":
        parts = [project_geometry(p, projector) for p in geom.geoms]
        # Tri stable des parties.
        parts = sorted(parts, key=lambda g: (round(g.centroid.x, 3), round(g.centroid.y, 3)))
        return MultiPolygon(parts)
    if geom.geom_type == "MultiPoint":
        pts = [project_geometry(p, projector) for p in geom.geoms]
        pts = sorted(pts, key=lambda p: (p.x, p.y))
        return MultiPoint(pts)
    raise TypeError(geom.geom_type)


def geometry_to_game_dict(geom: Any, projector: Projector, lonlat_geom: Any) -> dict:
    """Sérialise une géométrie projetée en dict GeoJSON-like avec coords jeu (int) + lon/lat."""

    def game_ring(coords_xy, coords_ll):
        ring = []
        for (x, y), (lon, lat) in zip(coords_xy, coords_ll):
            gx, gy = projector.to_game(x, y)
            lon_r, lat_r = projector.lonlat_rounded(lon, lat)
            ring.append({"x": gx, "y": gy, "lon": lon_r, "lat": lat_r})
        return ring

    if geom.geom_type == "Polygon" and lonlat_geom.geom_type == "Polygon":
        return {
            "type": "Polygon",
            "coordinates_game": [game_ring(list(geom.exterior.coords), list(lonlat_geom.exterior.coords))],
        }
    # Fallback : mapping shapely standard + game ints séparés
    return mapping(geom)


# ---------------------------------------------------------------------------
# Étapes
# ---------------------------------------------------------------------------


def stage_ingest() -> Dict[str, Any]:
    t0 = time.perf_counter()
    BUILD.mkdir(parents=True, exist_ok=True)
    paths = {
        "land": LAND_PATH,
        "rivers": RIVERS_PATH,
        "cities": CITIES_PATH,
    }
    docs: Dict[str, Any] = {}
    fingerprints: Dict[str, str] = {}
    for key, path in sorted(paths.items()):
        if not path.exists():
            raise FileNotFoundError(path)
        doc = read_json(path)
        assert_fixture(doc, path)
        docs[key] = doc
        fingerprints[key] = sha256_file(path)

    out = {
        "pipeline_version": PIPELINE_VERSION,
        "inputs": {
            key: {
                "path": str(path.relative_to(ROOT)).replace("\\", "/"),
                "sha256": fingerprints[key],
                "data_class": docs[key]["data_class"],
            }
            for key, path in sorted(paths.items())
        },
        "feature_counts": {
            key: len(docs[key]["features"]) for key in sorted(docs.keys())
        },
    }
    write_json(BUILD / "01_ingest.json", out)
    # Copie figée des fixtures dans build (contenu déjà déterministe).
    for key, path in sorted(paths.items()):
        shutil.copyfile(path, BUILD / f"01_{key}.geojson")
    return {
        "docs": docs,
        "fingerprints": fingerprints,
        "elapsed_s": time.perf_counter() - t0,
        "summary": out,
    }


def stage_project(docs: Dict[str, Any], projector: Projector) -> Dict[str, Any]:
    t0 = time.perf_counter()
    land_ll = load_land_union(docs["land"])
    land_xy = project_geometry(land_ll, projector)

    rivers_xy = []
    for feat in docs["rivers"]["features"]:
        g_ll = shape(feat["geometry"])
        g_xy = project_geometry(g_ll, projector)
        rivers_xy.append(
            {
                "name": feat["properties"]["name"],
                "geometry": mapping(g_xy),
                "lonlat": mapping(g_ll),
            }
        )
    rivers_xy = sorted(rivers_xy, key=lambda r: r["name"])

    cities_xy = []
    for feat in docs["cities"]["features"]:
        g_ll = shape(feat["geometry"])
        g_xy = project_geometry(g_ll, projector)
        gx, gy = projector.to_game(g_xy.x, g_xy.y)
        lon_r, lat_r = projector.lonlat_rounded(g_ll.x, g_ll.y)
        cities_xy.append(
            {
                "name": feat["properties"]["name"],
                "role": feat["properties"].get("role", ""),
                "x": gx,
                "y": gy,
                "lon": lon_r,
                "lat": lat_r,
                "x_m": round(g_xy.x, FLOAT_DECIMALS),
                "y_m": round(g_xy.y, FLOAT_DECIMALS),
            }
        )
    cities_xy = sorted(cities_xy, key=lambda c: c["name"])

    payload = {
        "projection": {
            "epsg": projector.info.epsg,
            "fallback": projector.info.fallback,
            "reason": projector.info.reason,
            "source_crs": "EPSG:4326",
        },
        "land_area_m2": round(land_xy.area, FLOAT_DECIMALS),
        "rivers": rivers_xy,
        "cities": cities_xy,
        "land_bounds_m": [round(v, FLOAT_DECIMALS) for v in land_xy.bounds],
    }
    write_json(BUILD / "02_project.json", payload)
    # Géométries binaires non utilisées ; on garde le land projeté en WKT déterministe.
    write_json(
        BUILD / "02_land_meta.json",
        {
            "wkt_sha_placeholder": True,
            "area_m2": round(land_xy.area, FLOAT_DECIMALS),
            "geom_type": land_xy.geom_type,
        },
    )
    return {
        "land_ll": land_ll,
        "land_xy": land_xy,
        "rivers": rivers_xy,
        "cities": cities_xy,
        "elapsed_s": time.perf_counter() - t0,
        "payload": payload,
    }


def _iter_land_parts(land_xy: Any) -> List[Any]:
    if land_xy.geom_type == "Polygon":
        return [land_xy]
    if land_xy.geom_type == "MultiPolygon":
        return sorted(
            list(land_xy.geoms),
            key=lambda g: (round(g.centroid.x, 3), round(g.centroid.y, 3)),
        )
    raise TypeError(land_xy.geom_type)


def stage_seed(land_xy: Any) -> Dict[str, Any]:
    """Semis déterministe de 10–20 points à l'intérieur de la terre."""
    t0 = time.perf_counter()
    parts = _iter_land_parts(land_xy)
    minx, miny, maxx, maxy = land_xy.bounds
    width = maxx - minx
    height = maxy - miny

    # Grille fine ; on filtre, on trie, on garde SEED_COUNT_MAX points répartis.
    # Espacement choisi pour obtenir ~15–25 candidats sur la fixture.
    step = min(width, height) / 6.0
    candidates: List[Tuple[float, float]] = []
    # Origine de grille ancrée sur les bornes (déterministe).
    x = minx + step * 0.5
    while x <= maxx:
        y = miny + step * 0.5
        while y <= maxy:
            pt = Point(x, y)
            if land_xy.contains(pt):
                candidates.append((round(x, FLOAT_DECIMALS), round(y, FLOAT_DECIMALS)))
            y += step
        x += step

    # Garantir un point par partie (île / continent).
    for part in parts:
        c = part.representative_point()
        candidates.append((round(c.x, FLOAT_DECIMALS), round(c.y, FLOAT_DECIMALS)))

    # Dédupliquer en conservant l'ordre de tri.
    uniq = sorted(set(candidates), key=lambda p: (p[0], p[1]))

    if len(uniq) < SEED_COUNT_MIN:
        # Densifier.
        step2 = step * 0.5
        x = minx + step2 * 0.5
        while x <= maxx:
            y = miny + step2 * 0.5
            while y <= maxy:
                pt = Point(x, y)
                if land_xy.contains(pt):
                    uniq.append((round(x, FLOAT_DECIMALS), round(y, FLOAT_DECIMALS)))
                y += step2
            x += step2
        uniq = sorted(set(uniq), key=lambda p: (p[0], p[1]))

    # Sous-échantillonner de façon déterministe pour rester dans [10, 20].
    if len(uniq) > SEED_COUNT_MAX:
        # Prendre un sous-ensemble uniforme par index.
        n = SEED_COUNT_MAX
        idxs = [int(round(i * (len(uniq) - 1) / (n - 1))) for i in range(n)]
        # Forcer inclusion des representative_points (premiers de chaque part).
        forced = []
        for part in parts:
            c = part.representative_point()
            key = (round(c.x, FLOAT_DECIMALS), round(c.y, FLOAT_DECIMALS))
            if key in uniq:
                forced.append(uniq.index(key))
        chosen_idx = sorted(set(idxs + forced))[:SEED_COUNT_MAX]
        # Si trop, garder forced + remplir.
        if len(chosen_idx) > SEED_COUNT_MAX:
            chosen_idx = sorted(set(forced))[:SEED_COUNT_MAX]
            for i in idxs:
                if len(chosen_idx) >= SEED_COUNT_MAX:
                    break
                if i not in chosen_idx:
                    chosen_idx.append(i)
            chosen_idx = sorted(chosen_idx)[:SEED_COUNT_MAX]
        seeds = [uniq[i] for i in chosen_idx]
    else:
        seeds = uniq

    if len(seeds) < SEED_COUNT_MIN:
        raise RuntimeError(
            f"semis insuffisant : {len(seeds)} < {SEED_COUNT_MIN}. "
            "Ajuster la grille ou la fixture."
        )
    if len(seeds) > SEED_COUNT_MAX:
        seeds = seeds[:SEED_COUNT_MAX]

    payload = {
        "count": len(seeds),
        "seeds": [{"x_m": s[0], "y_m": s[1], "index": i} for i, s in enumerate(seeds)],
    }
    write_json(BUILD / "03_seeds.json", payload)
    return {"seeds": seeds, "elapsed_s": time.perf_counter() - t0, "payload": payload}


def _as_polygons(geom: Any) -> List[Any]:
    if geom is None or geom.is_empty:
        return []
    if geom.geom_type == "Polygon":
        return [geom]
    if geom.geom_type == "MultiPolygon":
        return list(geom.geoms)
    if geom.geom_type == "GeometryCollection":
        out: List[Any] = []
        for g in geom.geoms:
            out.extend(_as_polygons(g))
        return out
    return []


def _merge_cell(geom: Any, frag: Any) -> Any:
    merged = unary_union([geom, frag])
    if not merged.is_valid:
        merged = merged.buffer(0)
    # Conserver MultiPolygon si besoin — jeter des parties créerait des trous (Q2).
    return merged


def _normalize_cell_geom(geom: Any) -> Any:
    if not geom.is_valid:
        geom = geom.buffer(0)
    polys = _as_polygons(geom)
    if not polys:
        return geom
    if len(polys) == 1:
        return polys[0]
    return MultiPolygon(
        sorted(polys, key=lambda p: (round(p.centroid.x, 3), round(p.centroid.y, 3)))
    )


def stage_cells(
    land_xy: Any,
    land_ll: Any,
    seeds: Sequence[Tuple[float, float]],
    projector: Projector,
) -> Dict[str, Any]:
    """Découpe en cellules Voronoï contraintes au polygone de terre."""
    t0 = time.perf_counter()
    seed_pts = [Point(x, y) for x, y in seeds]
    mp = MultiPoint(seed_pts)
    pad = max(land_xy.bounds[2] - land_xy.bounds[0], land_xy.bounds[3] - land_xy.bounds[1], 1.0)
    envelope = land_xy.envelope.buffer(pad)
    raw = voronoi_polygons(mp, extend_to=envelope)
    voronoi_list = list(raw.geoms) if hasattr(raw, "geoms") else [raw]

    # cell_map[seed_index] = geometry (Polygon)
    cell_map: Dict[int, Any] = {}
    for i, seed_pt in enumerate(seed_pts):
        matches = [g for g in voronoi_list if g.covers(seed_pt)]
        if not matches:
            matches = sorted(voronoi_list, key=lambda g: g.distance(seed_pt))
        clipped = matches[0].intersection(land_xy)
        polys = _as_polygons(clipped)
        if not polys:
            continue
        containing = [p for p in polys if p.covers(seed_pt)]
        geom = max(containing or polys, key=lambda p: p.area)
        if not geom.is_valid:
            geom = geom.buffer(0)
        cell_map[i] = geom

    # Assurer une cellule par seed même si Voronoï a raté (fallback disque clipé).
    for i, seed_pt in enumerate(seed_pts):
        if i in cell_map:
            continue
        # Partie de terre la plus proche : petit buffer ∩ terre, puis grow.
        guess = seed_pt.buffer(pad * 0.05).intersection(land_xy)
        polys = _as_polygons(guess)
        if polys:
            cell_map[i] = max(polys, key=lambda p: p.area)

    def _assign_remainder(cell_map: Dict[int, Any]) -> Dict[int, Any]:
        """Assigne tout le reste de terre au seed le plus proche, itérativement."""
        for _ in range(8):
            covered = unary_union(list(cell_map.values())) if cell_map else None
            if covered is None:
                break
            remainder = land_xy.difference(covered.buffer(0))
            if remainder.is_empty or remainder.area <= 1.0:
                break
            frags = _as_polygons(remainder)
            if not frags:
                # LineString residues etc.
                remainder = remainder.buffer(0.5).intersection(land_xy)
                frags = _as_polygons(remainder)
            if not frags:
                break
            for frag in sorted(frags, key=lambda g: -g.area):
                if frag.area <= 0:
                    continue
                c = frag.representative_point()
                nearest_i = min(
                    cell_map.keys(),
                    key=lambda i: seed_pts[i].distance(c),
                )
                cell_map[nearest_i] = _merge_cell(cell_map[nearest_i], frag)
        return cell_map

    cell_map = _assign_remainder(cell_map)

    # Clip final strict : aucune cellule ne doit déborder en mer.
    for i in list(cell_map.keys()):
        clipped = cell_map[i].intersection(land_xy)
        cell_map[i] = _normalize_cell_geom(clipped)

    cell_map = _assign_remainder(cell_map)

    # Normalisation finale déterministe.
    for i in list(cell_map.keys()):
        cell_map[i] = _normalize_cell_geom(cell_map[i].intersection(land_xy))
        if cell_map[i].is_empty or cell_map[i].area <= 0:
            del cell_map[i]

    cells_xy: List[Tuple[int, Any]] = [
        (i, cell_map[i]) for i in sorted(cell_map.keys())
    ]

    cells_out: List[dict] = []
    for order, (seed_index, geom) in enumerate(
        sorted(cells_xy, key=lambda t: (round(t[1].centroid.x, 3), round(t[1].centroid.y, 3)))
    ):
        cell_id = CELL_ID_BASE + order
        cx, cy = geom.centroid.x, geom.centroid.y
        gx, gy = projector.to_game(cx, cy)
        lon, lat = _inverse_lonlat(projector, cx, cy)
        cells_out.append(
            {
                "cell_id": cell_id,
                "seed_index": seed_index,
                "area_m2": round(geom.area, FLOAT_DECIMALS),
                "centroid": {
                    "x": gx,
                    "y": gy,
                    "x_m": round(cx, FLOAT_DECIMALS),
                    "y_m": round(cy, FLOAT_DECIMALS),
                    "lon": lon,
                    "lat": lat,
                },
                "geometry": mapping(geom),
                # Placeholder pour factorisation d'arcs partagés (G9).
                "arc_ring_placeholder": True,
            }
        )

    cells_out = sorted(cells_out, key=lambda c: c["cell_id"])
    write_json(BUILD / "04_cells.json", {"cells": cells_out, "count": len(cells_out)})
    return {
        "cells": cells_out,
        "cells_xy": [(c["cell_id"], shape(c["geometry"])) for c in cells_out],
        "elapsed_s": time.perf_counter() - t0,
    }


def _inverse_lonlat(projector: Projector, x: float, y: float) -> Tuple[float, float]:
    if projector._transformer is not None:
        from pyproj import Transformer

        inv = Transformer.from_crs("EPSG:3035", "EPSG:4326", always_xy=True)
        lon, lat = inv.transform(x, y)
        return projector.lonlat_rounded(lon, lat)
    # Repli inverse de l'équirectangulaire.
    cos_lat = __import__("math").cos(__import__("math").radians(47.5))
    lon = x / (cos_lat * 111_320.0)
    lat = y / 111_320.0
    return projector.lonlat_rounded(lon, lat)


def stage_derive(cells_xy: Sequence[Tuple[int, Any]], land_xy: Any) -> Dict[str, Any]:
    """Adjacence dérivée par contiguïté d'arête (terre-terre / terre-mer)."""
    t0 = time.perf_counter()
    edges: List[dict] = []
    items = sorted(cells_xy, key=lambda t: t[0])
    land_boundary = land_xy.boundary

    for i in range(len(items)):
        id_a, ga = items[i]
        for j in range(i + 1, len(items)):
            id_b, gb = items[j]
            inter = ga.boundary.intersection(gb.boundary)
            length = 0.0 if inter.is_empty else inter.length
            if length >= LENGTH_EPS:
                a, b = sorted((id_a, id_b))
                edges.append(
                    {
                        "a": a,
                        "b": b,
                        "kind": "land-land",
                        "shared_length_m": round(length, FLOAT_DECIMALS),
                    }
                )

        # Terre-mer : portion du contour de cellule qui suit le littoral.
        coast = ga.boundary.intersection(land_boundary)
        coast_len = 0.0 if coast.is_empty else coast.length
        if coast_len >= LENGTH_EPS:
            a, b = sorted((id_a, SEA_CELL_ID))
            edges.append(
                {
                    "a": a,
                    "b": b,
                    "kind": "land-sea",
                    "shared_length_m": round(coast_len, FLOAT_DECIMALS),
                }
            )

    edges = sorted(edges, key=lambda e: (e["a"], e["b"], e["kind"]))
    # Dédupliquer (a,b,kind).
    dedup: Dict[Tuple[int, int, str], dict] = {}
    for e in edges:
        dedup[(e["a"], e["b"], e["kind"])] = e
    edges = [dedup[k] for k in sorted(dedup.keys())]

    payload = {"adjacency": edges, "count": len(edges)}
    write_json(BUILD / "05_adjacency.json", payload)
    return {"adjacency": edges, "elapsed_s": time.perf_counter() - t0}


def stage_attach(
    cities: Sequence[dict], cells: Sequence[dict]
) -> Dict[str, Any]:
    t0 = time.perf_counter()
    cell_geoms = [(c["cell_id"], shape(c["geometry"])) for c in cells]
    attached = []
    for city in sorted(cities, key=lambda c: c["name"]):
        pt = Point(city["x_m"], city["y_m"])
        hits = [
            cid
            for cid, g in cell_geoms
            if g.contains(pt) or (g.touches(pt) and g.boundary.distance(pt) < 1e-6)
        ]
        # Si touches multiples, prendre la plus grande aire.
        if len(hits) > 1:
            hits = [
                max(
                    ((cid, g) for cid, g in cell_geoms if cid in hits),
                    key=lambda t: t[1].area,
                )[0]
            ]
        cell_id = hits[0] if len(hits) == 1 else None
        attached.append(
            {
                **city,
                "cell_id": cell_id,
            }
        )
    write_json(BUILD / "06_cities_attached.json", {"cities": attached})
    return {"cities": attached, "elapsed_s": time.perf_counter() - t0}


def stage_export(
    *,
    fingerprints: Dict[str, str],
    projection: ProjectionInfo,
    cells: Sequence[dict],
    adjacency: Sequence[dict],
    cities: Sequence[dict],
    docs: Dict[str, Any],
    timings: Dict[str, float],
) -> Dict[str, str]:
    """Exporte les artefacts déterministes + MANIFEST. Retourne {relpath: sha256}."""
    t0 = time.perf_counter()
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    refuse_game_export(ARTIFACTS, list(docs.values()))

    # Sorties sans horodatage courant (déterminisme).
    cells_out = {
        "pipeline_version": PIPELINE_VERSION,
        "data_class": "fixture",
        "comment": "Artefact de TEST — PAS de géographie réelle.",
        "projection": projection.epsg,
        "cells": [
            {
                "cell_id": c["cell_id"],
                "area_m2": c["area_m2"],
                "centroid": c["centroid"],
                "geometry": c["geometry"],
                "arc_ring_placeholder": True,
            }
            for c in sorted(cells, key=lambda x: x["cell_id"])
        ],
    }
    adj_out = {
        "pipeline_version": PIPELINE_VERSION,
        "data_class": "fixture",
        "adjacency": sorted(adjacency, key=lambda e: (e["a"], e["b"], e["kind"])),
    }
    cities_out = {
        "pipeline_version": PIPELINE_VERSION,
        "data_class": "fixture",
        "cities": sorted(cities, key=lambda c: c["name"]),
    }
    stats = _compute_stats(cells, adjacency)

    shas: Dict[str, str] = {}
    shas["artifacts/cells.json"] = write_json(ARTIFACTS / "cells.json", cells_out)
    shas["artifacts/adjacency.json"] = write_json(ARTIFACTS / "adjacency.json", adj_out)
    shas["artifacts/cities.json"] = write_json(ARTIFACTS / "cities.json", cities_out)
    shas["artifacts/stats.json"] = write_json(ARTIFACTS / "stats.json", stats)

    # MANIFEST : pas d'horodatage ni de timings (non déterministes) — version + empreintes.
    manifest = {
        "pipeline_version": PIPELINE_VERSION,
        "projection": {
            "epsg": projection.epsg,
            "fallback": projection.fallback,
            "reason": projection.reason,
        },
        "inputs": {
            k: fingerprints[k] for k in sorted(fingerprints.keys())
        },
        "outputs": {k: shas[k] for k in sorted(shas.keys())},
        "fixed_timestamp": "1970-01-01T00:00:00Z",
        "data_class": "fixture",
        "comment": "MANIFEST de TEST — PAS de géographie réelle. fixed_timestamp figé ; timings exclus (non déterministes).",
    }
    shas["artifacts/MANIFEST.json"] = write_json(ARTIFACTS / "MANIFEST.json", manifest)
    # Timings uniquement hors artefacts hashés.
    write_json(
        BUILD / "99_timings.json",
        {k: round(v, 6) for k, v in sorted(timings.items())},
    )
    timings["export"] = time.perf_counter() - t0
    return shas


def _compute_stats(cells: Sequence[dict], adjacency: Sequence[dict]) -> dict:
    areas = sorted(c["area_m2"] for c in cells)
    neighbor_counts: Dict[int, int] = {c["cell_id"]: 0 for c in cells}
    for e in adjacency:
        if e["kind"] == "land-land":
            neighbor_counts[e["a"]] = neighbor_counts.get(e["a"], 0) + 1
            neighbor_counts[e["b"]] = neighbor_counts.get(e["b"], 0) + 1
        elif e["kind"] == "land-sea":
            land_id = e["a"] if e["a"] != SEA_CELL_ID else e["b"]
            neighbor_counts[land_id] = neighbor_counts.get(land_id, 0) + 1
    counts = sorted(neighbor_counts[cid] for cid in sorted(neighbor_counts.keys()))

    def median(vals: List[float]) -> float:
        if not vals:
            return 0.0
        n = len(vals)
        mid = n // 2
        if n % 2:
            return float(vals[mid])
        return float(vals[mid - 1] + vals[mid]) / 2.0

    land_land = sum(1 for e in adjacency if e["kind"] == "land-land")
    land_sea = sum(1 for e in adjacency if e["kind"] == "land-sea")
    return {
        "cell_count": len(cells),
        "area_m2": {
            "min": areas[0] if areas else 0.0,
            "median": median(areas),
            "max": areas[-1] if areas else 0.0,
        },
        "neighbors": {
            "min": counts[0] if counts else 0,
            "median": median([float(c) for c in counts]),
            "max": counts[-1] if counts else 0,
        },
        "adjacency_count": len(adjacency),
        "adjacency_land_land": land_land,
        "adjacency_land_sea": land_sea,
    }


def write_capture_svg(
    land_xy: Any,
    cells: Sequence[dict],
    adjacency: Sequence[dict],
    cities: Sequence[dict],
) -> Path:
    """Capture de contrôle SVG (aucune vocation esthétique)."""
    CAPTURE.mkdir(parents=True, exist_ok=True)
    minx, miny, maxx, maxy = land_xy.bounds
    pad = (maxx - minx) * 0.05
    minx -= pad
    miny -= pad
    maxx += pad
    maxy += pad
    width = 900
    height = 700

    def sx(x: float) -> float:
        return (x - minx) / (maxx - minx) * width

    def sy(y: float) -> float:
        # SVG y vers le bas.
        return height - (y - miny) / (maxy - miny) * height

    def poly_path(geom: Any) -> List[str]:
        paths: List[str] = []
        for poly in _as_polygons(geom):
            coords = list(poly.exterior.coords)
            parts = [f"{sx(x):.2f},{sy(y):.2f}" for x, y in coords]
            paths.append("M " + " L ".join(parts) + " Z")
        return paths

    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}">',
        "<rect width='100%' height='100%' fill='#dce6f0'/>",
        "<!-- Capture de contrôle G1 — fixture, PAS de géographie réelle -->",
    ]

    # Terre
    for d in poly_path(land_xy):
        lines.append(
            f"<path d='{d}' fill='#c8e6c9' stroke='#2e7d32' stroke-width='2'/>"
        )

    # Cellules
    for c in sorted(cells, key=lambda x: x["cell_id"]):
        g = shape(c["geometry"])
        for d in poly_path(g):
            lines.append(
                f"<path d='{d}' fill='#fff9c4' fill-opacity='0.45' "
                f"stroke='#f57f17' stroke-width='1'/>"
            )
        cx, cy = g.centroid.x, g.centroid.y
        lines.append(
            f"<text x='{sx(cx):.1f}' y='{sy(cy):.1f}' font-size='10' "
            f"text-anchor='middle' fill='#333'>{c['cell_id']}</text>"
        )

    # Adjacences land-land
    by_id = {c["cell_id"]: shape(c["geometry"]) for c in cells}
    for e in adjacency:
        if e["kind"] != "land-land":
            continue
        ga, gb = by_id[e["a"]], by_id[e["b"]]
        a, b = ga.centroid, gb.centroid
        lines.append(
            f"<line x1='{sx(a.x):.1f}' y1='{sy(a.y):.1f}' "
            f"x2='{sx(b.x):.1f}' y2='{sy(b.y):.1f}' "
            f"stroke='#1565c0' stroke-width='1.5' opacity='0.7'/>"
        )

    # Villes
    for city in cities:
        lines.append(
            f"<circle cx='{sx(city['x_m']):.1f}' cy='{sy(city['y_m']):.1f}' "
            f"r='4' fill='#b71c1c'/>"
        )

    lines.append("</svg>")
    # Contenu déterministe (pas d'horodatage).
    text = "\n".join(lines) + "\n"
    out = CAPTURE / "v1_045_control.svg"
    out.write_bytes(text.encode("utf-8"))
    return out


def run_pipeline(clean_build: bool = True) -> Dict[str, Any]:
    if clean_build and BUILD.exists():
        shutil.rmtree(BUILD)
    BUILD.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    timings: Dict[str, float] = {}
    projector = Projector(detect_projection())

    ingest = stage_ingest()
    timings["ingest"] = ingest["elapsed_s"]

    projected = stage_project(ingest["docs"], projector)
    timings["project"] = projected["elapsed_s"]

    seeded = stage_seed(projected["land_xy"])
    timings["seed"] = seeded["elapsed_s"]

    celled = stage_cells(
        projected["land_xy"], projected["land_ll"], seeded["seeds"], projector
    )
    timings["cells"] = celled["elapsed_s"]

    derived = stage_derive(celled["cells_xy"], projected["land_xy"])
    timings["derive"] = derived["elapsed_s"]

    attached = stage_attach(projected["cities"], celled["cells"])
    timings["attach"] = attached["elapsed_s"]

    shas = stage_export(
        fingerprints=ingest["fingerprints"],
        projection=projector.info,
        cells=celled["cells"],
        adjacency=derived["adjacency"],
        cities=attached["cities"],
        docs=ingest["docs"],
        timings=timings,
    )

    capture_path = write_capture_svg(
        projected["land_xy"],
        celled["cells"],
        derived["adjacency"],
        attached["cities"],
    )

    return {
        "projection": projector.info,
        "cells": celled["cells"],
        "adjacency": derived["adjacency"],
        "cities": attached["cities"],
        "land_xy": projected["land_xy"],
        "timings": timings,
        "shas": shas,
        "fingerprints": ingest["fingerprints"],
        "capture": capture_path,
        "stats": _compute_stats(celled["cells"], derived["adjacency"]),
    }


def _load_coastline_module():
    """Charge steps/02_coastline.py (nom non importable directement)."""
    path = ROOT / "steps" / "02_coastline.py"
    spec = importlib.util.spec_from_file_location("coastline_g2", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_corrections_module():
    """Charge steps/02b_corrections_1400.py."""
    path = ROOT / "steps" / "02b_corrections_1400.py"
    spec = importlib.util.spec_from_file_location("corrections_g2b", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_cells_module():
    """Charge steps/03_cells.py."""
    path = ROOT / "steps" / "03_cells.py"
    spec = importlib.util.spec_from_file_location("cells_g3", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_adjacency_module():
    """Charge steps/04_adjacency.py."""
    path = ROOT / "steps" / "04_adjacency.py"
    spec = importlib.util.spec_from_file_location("adjacency_g4", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_rivers_module():
    """Charge steps/05_rivers.py."""
    path = ROOT / "steps" / "05_rivers.py"
    spec = importlib.util.spec_from_file_location("rivers_g5", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _load_relief_module():
    """Charge steps/06_relief.py."""
    path = ROOT / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def run_natural_earth_coastline(clean_build: bool = True) -> Dict[str, Any]:
    """G2 : littoral réel — délègue à steps/02_coastline.py sans dupliquer le tuyau."""
    coastline = _load_coastline_module()
    return coastline.run_coastline(clean_build=clean_build)


def run_corrections_1400(
    clean_build: bool = True, apply_corrections: bool = True
) -> Dict[str, Any]:
    """G2-bis : corrections 1400 — délègue à steps/02b_corrections_1400.py."""
    corrections = _load_corrections_module()
    return corrections.run_corrections(
        apply_corrections=apply_corrections, clean_build=clean_build
    )


def run_cells_g3(rebuild_land: bool = False) -> Dict[str, Any]:
    """G3 : cellules sur terre corrigée — délègue à steps/03_cells.py."""
    cells = _load_cells_module()
    return cells.run_cells(rebuild_land=rebuild_land)


def run_adjacency_g4(apply_topology_links: bool = True) -> Dict[str, Any]:
    """G4 : zones maritimes + adjacence typée — délègue à steps/04_adjacency.py."""
    adj = _load_adjacency_module()
    return adj.run_adjacency(apply_topology_links_flag=apply_topology_links)


def run_rivers_g5() -> Dict[str, Any]:
    """G5 : fleuves + enrichissement arêtes — délègue à steps/05_rivers.py."""
    rivers = _load_rivers_module()
    return rivers.run_rivers()


def _load_navigability_module():
    """Charge steps/05b_navigability_1400.py."""
    path = ROOT / "steps" / "05b_navigability_1400.py"
    spec = importlib.util.spec_from_file_location("navigability_g5b", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"impossible de charger {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def run_navigability_g5b(apply_overrides: bool = True) -> Dict[str, Any]:
    """G5-bis : surcharges de navigabilité — délègue à steps/05b_navigability_1400.py."""
    nav = _load_navigability_module()
    return nav.run_navigability(apply_overrides=apply_overrides)


def run_relief_g6() -> Dict[str, Any]:
    """G6 : relief + cols — délègue à steps/06_relief.py."""
    relief = _load_relief_module()
    return relief.run_relief()


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Pipeline cartographique (fixture G1, littoral G2, "
            "corrections G2-bis, cellules G3, adjacence G4, fleuves G5, relief G6)"
        )
    )
    parser.add_argument(
        "--source",
        choices=[
            "fixture",
            "natural_earth",
            "natural_earth_1400",
            "cells",
            "adjacency",
            "rivers",
            "navigability",
            "relief",
        ],
        default="fixture",
        help=(
            "fixture = G1 ; natural_earth = G2 ; "
            "natural_earth_1400 = G2-bis corrections 1400 ; "
            "cells = G3 cellules sur terre 1400 ; "
            "adjacency = G4 zones maritimes + adjacence typée ; "
            "rivers = G5 fleuves + enrichissement arêtes ; "
            "navigability = G5-bis surcharges de navigabilité ; "
            "relief = G6 altitude / pente / cols"
        ),
    )
    parser.add_argument(
        "--no-corrections",
        action="store_true",
        help="Avec natural_earth_1400 : désactive les corrections (réversibilité G2)",
    )
    parser.add_argument(
        "--stage",
        choices=["all", "ingest", "project", "seed", "cells", "derive", "attach", "export"],
        default="all",
    )
    args = parser.parse_args(list(argv) if argv is not None else None)

    if args.source == "natural_earth_1400":
        result = run_corrections_1400(
            apply_corrections=not args.no_corrections
        )
        built = result["result"]
        print(
            f"pipeline g2bis | source=natural_earth_1400 | "
            f"corrections={not args.no_corrections} | "
            f"projection={result['projection'].epsg} | "
            f"land_km2={built['land_area_km2']:.0f} "
            f"(before={result['area_before_km2']:.0f})"
        )
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "cells":
        result = run_cells_g3(rebuild_land=False)
        m = result["metrics"]
        print(
            f"pipeline g3 | source=cells | "
            f"projection={result['projection'].epsg} | "
            f"cells={m['cell_count']} | "
            f"ids={m['id_range']['min']}..{m['id_range']['max']} | "
            f"paris_basin={m['paris_basin']['cell_count']} "
            f"(uniform~{m['paris_basin']['expected_uniform']}) | "
            f"density_ratio={m['density_ratio_basin_vs_emptiest_quartile']}"
        )
        print(f"captures: {result['captures']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "adjacency":
        result = run_adjacency_g4(
            apply_topology_links=not args.no_corrections
        )
        m = result["metrics"]
        print(
            f"pipeline g4 | source=adjacency | "
            f"projection={result['projection'].epsg} | "
            f"sea_zones={m['sea_zone_count']} | "
            f"edges={m['adjacency_count']} {m['by_kind']} | "
            f"coastal={m['coastal_cell_count']} | "
            f"reachability={result['reachability']['all_enclosed_reachable']}"
        )
        print(f"captures: {result['captures']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "rivers":
        result = run_rivers_g5()
        m = result["metrics"]
        print(
            f"pipeline g5 | source=rivers | "
            f"projection={result['projection'].epsg} | "
            f"segments={m['segment_count']} nav={m['navigability_counts']} | "
            f"artery={m['artery_count']} crossing={m['crossing_count']} "
            f"both={m['both_count']} | mouths={m['mouth_count']}"
        )
        print(f"captures: {result['captures']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "navigability":
        result = run_navigability_g5b(apply_overrides=not args.no_corrections)
        m = result["metrics"]
        eff = result["effect"]
        print(
            f"pipeline g5b | source=navigability | "
            f"overrides={result['apply_overrides']} | "
            f"nav={m.get('navigability_counts')} | "
            f"flipped={eff.get('rivers_indeterminate_to_navigable')} | "
            f"new_artery={eff.get('new_fluvial_artery_edges')} | "
            f"new_mouths={eff.get('new_mouths')} | "
            f"ports_ok={result['ports'].get('all_ok')}"
        )
        print(f"captures: {result['captures']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "relief":
        result = run_relief_g6()
        m = result["metrics"]
        print(
            f"pipeline g6 | source=relief | "
            f"projection={result['projection'].epsg} | "
            f"cells={m['cell_count']} | "
            f"elev_med={m['elev_distribution']['median']} | "
            f"barriers={m['barrier_count']} passes={m['pass_count']} | "
            f"below_0_km2={m['below_0_land_km2']}"
        )
        print(f"captures: {result['captures']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.source == "natural_earth":
        result = run_natural_earth_coastline()
        built = result["result"]
        print(
            f"pipeline {PIPELINE_VERSION} | source=natural_earth | "
            f"projection={result['projection'].epsg} | "
            f"land_km2={built['land_area_km2']:.0f} | "
            f"islands_kept={len(built['islands_kept'])} "
            f"dropped={len(built['islands_dropped'])} "
            f"threshold={built['threshold']['threshold_km2']} km²"
        )
        print(f"capture: {result['capture']}")
        for path, digest in sorted(result["shas"].items()):
            print(f"  {path}  {digest}")
        return 0

    if args.stage != "all":
        print(
            f"Les étapes isolées se rejouent via run_pipeline() ; "
            f"--stage={args.stage} lance le pipeline complet (rejouable)."
        )

    result = run_pipeline()
    stats = result["stats"]
    print(
        f"pipeline {PIPELINE_VERSION} | source=fixture | "
        f"projection={result['projection'].epsg} "
        f"| cells={stats['cell_count']} | adj={stats['adjacency_count']} "
        f"(land-land={stats['adjacency_land_land']}, land-sea={stats['adjacency_land_sea']})"
    )
    print(f"capture: {result['capture']}")
    for path, digest in sorted(result["shas"].items()):
        print(f"  {path}  {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
