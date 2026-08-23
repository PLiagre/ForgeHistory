"""Viewer mince : refus, classification, SVG déterministe, sources locales."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

from sim.snapshot_export import export_snapshot
from sim.world import World
from viewer.classify import (
    ABSENT,
    INCOMPARABLE,
    NON_CALCULE,
    ZERO,
    classify,
    diff_status,
    numeric_diff,
)
from viewer.snapshot_loader import SnapshotLoadError, load_snapshot
from viewer.svg_proof import render_compare_svg, render_svg

_REPO = Path(__file__).resolve().parents[2]
_VIEWER = _REPO / "viewer"


def test_classify_trois_etats():
    assert classify(0) == ZERO
    assert classify(0.0) == ZERO
    assert classify(None) == ABSENT
    assert classify(-1) == NON_CALCULE
    assert classify(-1.0) == NON_CALCULE
    assert diff_status(-1, 4) == INCOMPARABLE
    assert numeric_diff(-1, 4) is None
    assert numeric_diff(None, 4) is None
    assert numeric_diff(2, 5) == 3.0


def test_schema_inconnu(tmp_path: Path):
    path = tmp_path / "bad.json"
    path.write_text('{"schema_version":"v0a-999","cells":[]}\n', encoding="utf-8")
    try:
        load_snapshot(path)
        raise AssertionError("schema inconnu doit lever")
    except SnapshotLoadError as exc:
        assert "inconnu" in str(exc)
    proc = subprocess.run(
        [sys.executable, "-m", "viewer", "--snapshot", str(path), "--proof-svg", str(tmp_path / "x.svg")],
        cwd=_REPO,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 2


def test_svg_deterministe_et_legend(tmp_path: Path):
    world = World.from_g3(0)
    snap = tmp_path / "a.json"
    export_snapshot(world, 0, 0, snap)
    document = load_snapshot(snap)
    first = render_svg(document, layer="population")
    second = render_svg(document, layer="population")
    assert first == second
    assert first.count("<g id=\"cell-") == document["cell_count"]
    assert all(
        f'id="cell-{int(cell["cell_id"])}"' in first for cell in document["cells"]
    )
    assert "zéro mesuré" in first
    assert "absent" in first
    assert "non calculé" in first
    assert hashlib.sha256(first.encode("utf-8")).hexdigest()
    dest = tmp_path / "carte.svg"
    proc = subprocess.run(
        [
            sys.executable,
            "-m",
            "viewer",
            "--snapshot",
            str(snap),
            "--proof-svg",
            str(dest),
        ],
        cwd=_REPO,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0
    assert dest.is_file()
    missing = subprocess.run(
        [sys.executable, "-m", "viewer", "--proof-svg", str(dest)],
        cwd=_REPO,
        capture_output=True,
        text=True,
    )
    assert missing.returncode == 2


def test_comparaison_incomparable_pas_numerisee(tmp_path: Path):
    world = World.from_g3(0)
    snap_a = tmp_path / "a.json"
    snap_b = tmp_path / "b.json"
    export_snapshot(world, 0, 0, snap_a)
    world_b = World.from_g3(0)
    from sim.engine import tick
    import random
    rng = random.Random(0)
    for _ in range(5):
        tick(world_b, rng)
    export_snapshot(world_b, 0, 5, snap_b)
    svg_a = render_svg(load_snapshot(snap_a))
    svg_cmp = render_compare_svg(load_snapshot(snap_a), load_snapshot(snap_b))
    assert svg_a != svg_cmp
    assert "incomparable" in svg_cmp
    dest = tmp_path / "cmp.svg"
    proc = subprocess.run(
        [
            sys.executable,
            "-m",
            "viewer",
            "--snapshot",
            str(snap_a),
            "--compare",
            str(snap_b),
            "--proof-svg",
            str(dest),
        ],
        cwd=_REPO,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0


def test_sources_sans_pipeline_ni_url():
    hits = []
    for path in _VIEWER.rglob("*"):
        if path.suffix not in {".py", ".js", ".html", ".css"}:
            continue
        if "tests" in path.parts:
            continue
        text = path.read_text(encoding="utf-8")
        if path.suffix in {".html", ".js", ".css"}:
            if "http://" in text or "https://" in text:
                hits.append(str(path))
        if path.suffix in {".py", ".js"} and "pipeline/geo" in text:
            hits.append(str(path))
    assert hits == []


def test_null_reste_absent():
    assert classify(None) == ABSENT
    forged_zero = 0
    assert classify(forged_zero) == ZERO
    assert classify(None) != classify(forged_zero)
