"""Projection EPSG:4326 → cible (EPSG:3035 / repli documenté)."""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Sequence, Tuple

from constants import FLOAT_DECIMALS, GAME_UNIT_SCALE, SOURCE_CRS, TARGET_CRS
from io_util import round_float


Coord = Tuple[float, float]

# Règle du plan §3.2 / §4 : projeté pour l'usage, lon/lat conservées.
CRS_USAGE = TARGET_CRS
CRS_REFERENCE = SOURCE_CRS
CRS_RULE = "projected_for_usage_lonlat_preserved"
CRS_RULE_DOC = (
    "Coordonnées projetées (EPSG:3035) pour l'usage (Unity / planches) ; "
    "lon/lat WGS84 (EPSG:4326) conservées comme référence pour reprojection."
)


@dataclass(frozen=True)
class ProjectionInfo:
    epsg: str
    fallback: bool
    reason: str


def crs_declaration(
    *,
    geometry_crs: str = CRS_USAGE,
    has_geometry_lonlat: bool = True,
) -> Dict[str, Any]:
    """Bloc CRS explicite, lisible sans outil, à publier dans chaque artefact."""
    out: Dict[str, Any] = {
        "geometry": geometry_crs,
        "usage": CRS_USAGE,
        "reference": CRS_REFERENCE,
        "rule": CRS_RULE,
        "rule_doc": CRS_RULE_DOC,
    }
    if has_geometry_lonlat:
        out["geometry_lonlat"] = CRS_REFERENCE
    return out


def detect_crs_from_bounds(
    min_x: float, min_y: float, max_x: float, max_y: float
) -> str:
    """Constate le repère sur la donnée (pas sur le nom de fichier)."""
    # Degrés lon/lat Europe / fenêtre pilote : |x|≲180, |y|≲90.
    if abs(min_x) <= 180.0 and abs(max_x) <= 180.0 and abs(min_y) <= 90.0 and abs(max_y) <= 90.0:
        return CRS_REFERENCE
    # Mètres LAEA Europe : millions.
    if abs(min_x) > 1000.0 or abs(max_x) > 1000.0:
        return CRS_USAGE
    return "UNKNOWN"


def land_lonlat_from_coast_doc(doc: Dict[str, Any]) -> Any:
    """Géométrie lon/lat du littoral — `geometry_lonlat` si présent, sinon legacy."""
    from shapely.geometry import shape

    raw = doc.get("geometry_lonlat")
    if raw is None:
        # Avant v1_064 : geometry était en lon/lat malgré projection=EPSG:3035.
        raw = doc["geometry"]
    geom = shape(raw)
    if not geom.is_valid:
        geom = geom.buffer(0)
    return geom


def land_projected_from_coast_doc(
    doc: Dict[str, Any], projector: Optional["Projector"] = None
) -> Any:
    """Géométrie projetée du littoral — `geometry` si déjà en usage, sinon projection."""
    from shapely.geometry import shape

    projector = projector or Projector(detect_projection())
    geom = shape(doc["geometry"])
    if not geom.is_valid:
        geom = geom.buffer(0)
    b = geom.bounds
    observed = detect_crs_from_bounds(b[0], b[1], b[2], b[3])
    if observed == CRS_USAGE:
        return geom
    if observed == CRS_REFERENCE:
        return project_shapely_ll_to_xy(geom, projector)
    if doc.get("geometry_lonlat") is not None:
        return project_shapely_ll_to_xy(land_lonlat_from_coast_doc(doc), projector)
    return geom


def project_shapely_ll_to_xy(geom: Any, projector: "Projector") -> Any:
    from shapely.ops import transform as shp_transform

    def _xy(x: float, y: float, z: float | None = None):
        px, py = projector.project_xy(float(x), float(y))
        if z is not None:
            return (px, py, z)
        return (px, py)

    out = shp_transform(_xy, geom)
    if not out.is_valid:
        out = out.buffer(0)
    return out


def unproject_shapely_xy_to_ll(geom: Any, projector: "Projector") -> Any:
    from shapely.ops import transform as shp_transform

    def _ll(x: float, y: float, z: float | None = None):
        lon, lat = projector.unproject_xy(float(x), float(y))
        if z is not None:
            return (lon, lat, z)
        return (lon, lat)

    out = shp_transform(_ll, geom)
    if not out.is_valid:
        out = out.buffer(0)
    return out


def _try_pyproj() -> ProjectionInfo | None:
    try:
        from pyproj import CRS, Transformer

        CRS.from_user_input(TARGET_CRS)
        Transformer.from_crs(SOURCE_CRS, TARGET_CRS, always_xy=True)
        return ProjectionInfo(
            epsg=TARGET_CRS,
            fallback=False,
            reason="pyproj + EPSG:3035 disponibles (LAEA Europe, équivalence de surface).",
        )
    except Exception:  # noqa: BLE001 — repli documenté exigé
        return None


def detect_projection() -> ProjectionInfo:
    info = _try_pyproj()
    if info is not None:
        return info
    return ProjectionInfo(
        epsg="FALLBACK:EQUIRECT_COS47_5",
        fallback=True,
        reason=(
            "EPSG:3035 indisponible dans l'environnement ; "
            "repli équirectangulaire x=lon*cos(47.5°), y=lat (mètres fictifs = degrés*111320). "
            "Les lon/lat d'origine restent conservées pour une reprojection ultérieure."
        ),
    )


class Projector:
    def __init__(self, info: ProjectionInfo | None = None) -> None:
        self.info = info or detect_projection()
        self._transformer = None
        self._inverse = None
        if not self.info.fallback:
            from pyproj import Transformer

            self._transformer = Transformer.from_crs(
                SOURCE_CRS, TARGET_CRS, always_xy=True
            )
            self._inverse = Transformer.from_crs(
                TARGET_CRS, SOURCE_CRS, always_xy=True
            )

    def project_xy(self, lon: float, lat: float) -> Coord:
        if self._transformer is not None:
            x, y = self._transformer.transform(lon, lat)
            return (float(x), float(y))
        # Repli : mètres fictifs pour rester dimensionnellement cohérent.
        cos_lat = math.cos(math.radians(47.5))
        x = lon * cos_lat * 111_320.0
        y = lat * 111_320.0
        return (x, y)

    def unproject_xy(self, x: float, y: float) -> Coord:
        if self._inverse is not None:
            lon, lat = self._inverse.transform(x, y)
            return (float(lon), float(lat))
        cos_lat = math.cos(math.radians(47.5))
        lon = x / (cos_lat * 111_320.0)
        lat = y / 111_320.0
        return (lon, lat)

    def project_coords(self, coords: Sequence[Coord]) -> List[Coord]:
        return [self.project_xy(lon, lat) for lon, lat in coords]

    def to_game(self, x: float, y: float) -> Tuple[int, int]:
        return (
            int(round(x * GAME_UNIT_SCALE)),
            int(round(y * GAME_UNIT_SCALE)),
        )

    def lonlat_rounded(self, lon: float, lat: float) -> Coord:
        return (round_float(lon, FLOAT_DECIMALS), round_float(lat, FLOAT_DECIMALS))
