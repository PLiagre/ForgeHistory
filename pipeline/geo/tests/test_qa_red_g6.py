"""Cas cassés volontaires G6 — chaque contrôle doit pouvoir devenir ROUGE.

Un contrôle qui ne peut pas rougir ne prouve rien. Six mutations locales sur
des copies en mémoire ; aucune ne modifie `qa/checks.py`.

Quatre cas rouges supplémentaires (amendement 001) pour les gardes D16/D17.

Ce module expose `run_all_red_g6(...)` et `run_amendment_red_g6(...)`,
importés par `tests/run_proof_g6.py`.
"""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Sequence, Tuple

from qa.checks import (
    g6a_dem_fingerprint_verified,
    g6b_all_cells_sampled,
    g6c_elevations_plausible,
    g6d_barrier_above_both_cells,
    g6e_mesh_unchanged,
    q10_determinism,
)


def red_q10(sha_pairs: Dict[str, List[str]]) -> Tuple[str, bool]:
    if not sha_pairs:
        return "aucune paire d empreintes", False
    key = sorted(sha_pairs.keys())[0]
    broken = {k: list(v) for k, v in sha_pairs.items()}
    broken[key] = [broken[key][0], broken[key][0][::-1]]
    result = q10_determinism(broken)
    return f"empreintes_divergentes_forcees_sur_{key}", (not result.passed)


def red_g6a() -> Tuple[str, bool]:
    result = g6a_dem_fingerprint_verified(False, "empreinte_collective_falsifiee")
    return "dem_ok=false avant lecture", (not result.passed)


def red_g6b(cell_relief: Sequence[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(list(cell_relief))
    if not cells:
        return "aucune cellule", False
    cells[0]["sample_count"] = 0
    cells[0]["elev_mean_m"] = None
    result = g6b_all_cells_sampled(cells)
    return f"cellule_{cells[0]['cell_id']}_sample_count_zero", (not result.passed)


def red_g6c(cell_relief: Sequence[dict]) -> Tuple[str, bool]:
    cells = copy.deepcopy(list(cell_relief))
    if not cells:
        return "aucune cellule", False
    cells[0]["elev_mean_m"] = 9999.0
    result = g6c_elevations_plausible(cells)
    return f"cellule_{cells[0]['cell_id']}_altitude_hors_plage", (not result.passed)


def red_g6d(
    adjacency: Sequence[dict], cell_relief: Sequence[dict]
) -> Tuple[str, bool]:
    edges = copy.deepcopy(list(adjacency))
    cells = copy.deepcopy(list(cell_relief))
    target = next((e for e in edges if e.get("relief_barrier")), None)
    if target is None:
        target = next((e for e in edges if e.get("kind") == "land-land"), None)
    if target is None or not cells:
        return "aucune arete barriere", False
    a, b = int(target["a"]), int(target["b"])
    by_id = {int(c["cell_id"]): c for c in cells}
    ca = float(by_id[a].get("centroid_elev_m") or by_id[a]["elev_mean_m"])
    target["relief_barrier"] = True
    target["crossing_elev_m"] = ca - 100.0
    result = g6d_barrier_above_both_cells(edges, cells)
    return f"arete_{a}_{b}_crossing_sous_centroide", (not result.passed)


def red_g6e(
    base_cell_ids: Sequence[int], cell_relief: Sequence[dict]
) -> Tuple[str, bool]:
    relief_ids = [int(c["cell_id"]) for c in cell_relief]
    if len(relief_ids) < 2:
        return "maille trop petite", False
    trimmed = relief_ids[:-1]
    result = g6e_mesh_unchanged(base_cell_ids, trimmed)
    return f"cell_id_{relief_ids[-1]}_retire", (not result.passed)


def red_amend_couverture(required_tiles: Sequence[str]) -> Tuple[str, bool]:
    """Garde D16 : tuile requise retirée du bloc dem avant lecture."""
    if not required_tiles:
        return "aucune tuile requise", False
    removed = required_tiles[0]
    lock_tiles = set(required_tiles[1:])
    try:
        from steps import relief_g6  # type: ignore  # noqa: F401
    except ImportError:
        pass
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    path = root / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6_red", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    try:
        mod.assert_dem_coverage(set(required_tiles), lock_tiles)
        return f"tuile_{removed}_retiree_sans_echec", False
    except RuntimeError as exc:
        msg = str(exc)
        ok = removed in msg or "absente" in msg
        return f"garde_couverture: {msg[:120]}", ok


def red_amend_hors_couverture() -> Tuple[str, bool]:
    """D17 : coordonnée hors tuile — échec explicite, jamais 0,0."""
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    path = root / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6_red2", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    lon, lat = 200.0, 50.0
    try:
        mod.lonlat_to_tile_name(lon, lat)
        mod._tile_bounds_from_name(
            "Copernicus_DSM_COG_30_N50_00_E200_00_DEM.tif"
        )
    except Exception:
        pass
    # DemSampler._tile_for doit lever hors emprise connue
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)
    sampler = mod.DemSampler(fetch.CACHE_DIR, set(), set())
    try:
        sampler._tile_for(lon, lat)
        return f"lon={lon}_lat={lat}_sans_echec", False
    except RuntimeError as exc:
        msg = str(exc)
        ok = "hors couverture" in msg and str(lon) in msg
        return f"hors_couverture: {msg[:120]}", ok
    finally:
        sampler.close()


def red_amend_nodata_counter() -> Tuple[str, bool]:
    """D17 : nodata incrémente le compteur, pas un échantillon valide."""
    ctr_type = None
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    path = root / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6_red3", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red3", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)
    sampler = mod.DemSampler(fetch.CACHE_DIR, set(), set())

    class FakeDS:
        nodata = -9999.0
        nodatavals = (-9999.0,)
        height = 1
        width = 1

        def index(self, lon, lat):
            return 0, 0

        def read(self, *args, **kwargs):
            import numpy as np

            return np.ma.masked_array([[-9999.0]], mask=[[True]])

        def close(self):
            pass

    tile = "Copernicus_DSM_COG_30_N50_00_E005_00_DEM.tif"
    sampler._tile_paths[tile] = (5.0, 50.0, 6.0, 51.0, fetch.CACHE_DIR / "x" / tile)
    sampler.lock_tiles.add(tile)
    sampler._datasets[tile] = FakeDS()
    sampler._nodata_by_tile[tile] = -9999.0
    before = sampler.counters.echantillons_nodata_raster
    val = sampler.read_elev(5.5, 50.5, context="test_nodata", family="grille")
    after = sampler.counters.echantillons_nodata_raster
    sampler.close()
    ok = val is None and after == before + 1
    return f"nodata_compte={after - before}_val={val}", ok


def red_amend_west_convention() -> Tuple[str, bool]:
    """D16 : W001 avec bornes fausses [-2,-1) diverge du raster."""
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red4", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)

    tile = "Copernicus_DSM_COG_30_N50_00_W001_00_DEM.tif"
    correct = fetch.tile_bounds_from_name(tile)
    wrong = (-2.0, 50.0, -1.0, 51.0)
    ok_correct = correct == (-1.0, 50.0, 0.0, 51.0)
    ok_wrong_differs = wrong != correct
    return (
        f"W001_correct={correct}_wrong={wrong}",
        ok_correct and ok_wrong_differs,
    )


def red_amend_degree_line() -> Tuple[str, bool]:
    """D19/D23 : latitude entière — ancienne tuile du nord lève hors bornes."""
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    path = root / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6_red_a2a", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red_a2a", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)

    lon, lat = 12.0, 33.0
    wrong_tile = mod.lonlat_to_tile_name_nominal(lon, lat)
    sampler = mod.DemSampler(fetch.CACHE_DIR, {wrong_tile}, {wrong_tile})
    sampler._tile_paths[wrong_tile] = (
        *mod._tile_bounds_from_name(wrong_tile),
        fetch.CACHE_DIR / "x" / wrong_tile,
    )

    class FakeDS:
        height = 3600
        width = 3600
        nodata = None
        nodatavals = (None,)

        def index(self, lon, lat):
            return 3600, 0

        def read(self, *args, **kwargs):
            import numpy as np

            return np.array([[0.0]])

        def close(self):
            pass

    sampler._datasets[wrong_tile] = FakeDS()
    sampler._nodata_by_tile[wrong_tile] = None
    sampler._tile_for = lambda lo, la: mod.lonlat_to_tile_name_nominal(lo, la)  # type: ignore[method-assign]
    try:
        sampler.read_elev(lon, lat, context="test_ligne_degre", family="grille")
        return f"lat={lat}_sans_echec_avec_tuile={wrong_tile}", False
    except RuntimeError as exc:
        msg = str(exc)
        ok = "hors bornes" in msg and "row=" in msg
        return f"ligne_degre: {msg[:120]}", ok
    finally:
        sampler.close()


def red_amend_out_of_bounds_read() -> Tuple[str, bool]:
    """D23 : indices hors tableau — lève, ne rend pas 0,0."""
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    path = root / "steps" / "06_relief.py"
    spec = importlib.util.spec_from_file_location("relief_g6_red_a2b", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red_a2b", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)

    lon, lat = 12.0, 33.0
    tile = mod.lonlat_to_tile_name(lon, lat)
    sampler = mod.DemSampler(fetch.CACHE_DIR, {tile}, {tile})

    class FakeDS:
        height = 3600
        width = 3600
        nodata = None
        nodatavals = (None,)

        def index(self, lon, lat):
            return 3600, 0

        def read(self, *args, **kwargs):
            import numpy as np

            return np.ma.masked_array([[0.0]], mask=[[False]])

        def close(self):
            pass

    sampler._tile_paths[tile] = (
        *mod._tile_bounds_from_name(tile),
        fetch.CACHE_DIR / "x" / tile,
    )
    sampler.lock_tiles.add(tile)
    sampler._datasets[tile] = FakeDS()
    sampler._nodata_by_tile[tile] = None
    try:
        val = sampler.read_elev(lon, lat, context="test_hors_bornes", family="grille")
        return f"hors_bornes_rendu={val}", False
    except RuntimeError as exc:
        msg = str(exc)
        ok = "hors bornes" in msg and "row=" in msg
        return f"hors_bornes: {msg[:120]}", ok
    finally:
        sampler.close()


def red_amend_fabricated_tile_probe() -> Tuple[str, bool]:
    """D20 : tuile du cache absente du depot public — sondage nomme la tuile."""
    import importlib.util
    from pathlib import Path

    root = Path(__file__).resolve().parents[1]
    fetch_path = root / "tools" / "fetch_dem_tiles.py"
    fspec = importlib.util.spec_from_file_location("fetch_red_a2c", fetch_path)
    fetch = importlib.util.module_from_spec(fspec)
    assert fspec.loader is not None
    fspec.loader.exec_module(fetch)

    fake = "Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif"
    code = fetch.head_tile(fake)
    ok = code != 200
    return f"sondage_{fake}_code={code}", ok


def run_all_red_g6(
    *,
    cell_relief: Sequence[dict],
    adjacency: Sequence[dict],
    base_cell_ids: Sequence[int],
    sha_pairs: Dict[str, List[str]],
) -> Dict[str, Dict[str, Any]]:
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("Q10", lambda: red_q10(sha_pairs)),
        ("G6-A", lambda: red_g6a()),
        ("G6-B", lambda: red_g6b(cell_relief)),
        ("G6-C", lambda: red_g6c(cell_relief)),
        ("G6-D", lambda: red_g6d(adjacency, cell_relief)),
        ("G6-E", lambda: red_g6e(base_cell_ids, cell_relief)),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs


def run_amendment_red_g6(*, required_tiles: Sequence[str]) -> Dict[str, Dict[str, Any]]:
    proofs: Dict[str, Dict[str, Any]] = {}
    for qid, fn in [
        ("A1-couverture", lambda: red_amend_couverture(required_tiles)),
        ("A1-hors-couverture", lambda: red_amend_hors_couverture()),
        ("A1-nodata", lambda: red_amend_nodata_counter()),
        ("A1-ouest", lambda: red_amend_west_convention()),
        ("A2-ligne-degre", lambda: red_amend_degree_line()),
        ("A2-lecture-hors-bornes", lambda: red_amend_out_of_bounds_read()),
        ("A2-tuile-fabriquee", lambda: red_amend_fabricated_tile_probe()),
    ]:
        case, became_red = fn()
        proofs[qid] = {"case": case, "became_red": became_red}
    return proofs
