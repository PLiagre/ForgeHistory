"""Demande à forge3d une photographie 3D du MNT. Aucune mécanique de monde."""

from __future__ import annotations

import os
import tempfile
from pathlib import Path

import numpy as np

from visualisateur.raster import HAUTEURS_M, Mnt


class RenduErreur(RuntimeError):
    """Le moteur de rendu a refusé, ou il n'est pas là."""


def _hdr_minimal(path: Path) -> None:
    """Environnement Radiance 2×2, suffisant pour allumer IBL."""
    path.write_bytes(
        b"#?RADIANCE\n"
        b"FORMAT=32-bit_rle_rgbe\n\n"
        b"-Y 2 +X 2\n"
        + bytes([180, 190, 205, 128]) * 4
    )


def rendre_png(mnt: Mnt, destination: Path, *, largeur_px: int = 960, hauteur_px: int = 540) -> Path:
    """Rendu hors écran. L'exagération verticale est un facteur de lecture."""
    try:
        import forge3d as f3d
        from forge3d.terrain_params import make_terrain_params_config
    except ImportError as exc:
        raise RenduErreur(
            "forge3d est absent. Dans le venv du visualisateur : "
            "python3 -m pip install forge3d"
        ) from exc

    if not f3d.has_gpu():
        raise RenduErreur(
            "forge3d ne voit aucun adaptateur GPU (ni logiciel). "
            "Installer mesa-vulkan-drivers, ou une carte."
        )
    manquants = [
        nom
        for nom in ("Session", "TerrainRenderer", "MaterialSet", "IBL", "Colormap1D", "OverlayLayer", "TerrainRenderParams")
        if not hasattr(f3d, nom)
    ]
    if manquants:
        raise RenduErreur("forge3d incomplet : " + ", ".join(manquants))

    h_max = float(max(HAUTEURS_M.values()))
    altitudes = np.clip(mnt.altitudes_m / h_max, 0.0, 1.0).astype(np.float32)
    # Le MNT canonique de forge3d vit dans un monde de largeur 2. On s'y tient :
    # l'étendue kilométrique de la carte ne doit pas écraser le relief.
    terrain_span = 2.0
    fraction_lisible = 0.12
    z_scale = fraction_lisible * terrain_span / max(float(altitudes.max()), 1e-6)

    domaine = (0.0, 1.0)
    colormap = f3d.Colormap1D.from_stops(
        stops=[
            (0.00, "#1a3d6e"),
            (0.02, "#2d6a3a"),
            (0.15, "#6b8f3a"),
            (0.40, "#c4a574"),
            (0.70, "#8a7a6a"),
            (1.00, "#f2f0ea"),
        ],
        domain=domaine,
    )
    overlays = [
        f3d.OverlayLayer.from_colormap1d(
            colormap,
            strength=1.0,
            offset=0.0,
            blend_mode="Alpha",
            domain=domaine,
        )
    ]
    config = make_terrain_params_config(
        size_px=(int(largeur_px), int(hauteur_px)),
        render_scale=1.0,
        terrain_span=terrain_span,
        msaa_samples=1,
        z_scale=float(z_scale),
        exposure=1.15,
        domain=domaine,
        albedo_mode="colormap",
        colormap_strength=1.0,
        ibl_enabled=True,
        sun_intensity=3.0,
        light_azimuth_deg=210.0,
        light_elevation_deg=28.0,
        cam_radius=3.4,
        cam_phi_deg=155.0,
        cam_theta_deg=42.0,
        fov_y_deg=50.0,
        overlays=overlays,
        clip=(0.05, 40.0),
    )
    params = f3d.TerrainRenderParams(config)
    session = f3d.Session(window=False)
    renderer = f3d.TerrainRenderer(session)
    materiaux = f3d.MaterialSet.terrain_default()
    masque_eau = (~mnt.masque_terre).astype(np.float32)

    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        hdr = Path(tmp) / "ciel.hdr"
        _hdr_minimal(hdr)
        ibl = f3d.IBL.from_hdr(str(hdr), intensity=1.0)
        cadre = renderer.render_terrain_pbr_pom(
            material_set=materiaux,
            env_maps=ibl,
            params=params,
            heightmap=altitudes,
            target=None,
            water_mask=masque_eau,
        )
        cadre.save(str(destination))
    if not destination.is_file() or destination.stat().st_size < 32:
        raise RenduErreur(f"png absent ou vide : {destination}")
    return destination
