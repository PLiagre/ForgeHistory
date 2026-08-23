from __future__ import annotations

import hashlib
import importlib.util
import os
import time
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "hermes" / "crons" / "veille.py"
SPEC = importlib.util.spec_from_file_location("hermes_veille", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
watch = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(watch)


def test_parse_worktrees_keeps_paths_branches_and_prunable_state():
    records = watch.parse_worktrees(
        "worktree /srv/forge\n"
        "HEAD abc\n"
        "branch refs/heads/master\n\n"
        "worktree /srv/forge-agent\n"
        "HEAD def\n"
        "detached\n"
        "prunable gitdir file points to non-existent location\n"
    )
    assert records == [
        {"path": "/srv/forge", "head": "abc", "branch": "master"},
        {
            "path": "/srv/forge-agent",
            "head": "def",
            "detached": True,
            "prunable": "gitdir file points to non-existent location",
        },
    ]


def test_shared_cache_path_is_keyed_by_sources_lock_and_only_measured(tmp_path):
    repo = tmp_path / "repo"
    repo.mkdir()
    lock = repo / "pipeline" / "geo" / "sources.lock"
    lock.parent.mkdir(parents=True)
    lock.write_bytes(b"sources deterministes")
    digest = hashlib.sha256(lock.read_bytes()).hexdigest()
    shared = tmp_path / "shared-cache"
    effective = shared / digest
    effective.mkdir(parents=True)
    tile = effective / "tile.tif"
    tile.write_bytes(b"dem")
    before = tile.read_bytes()

    metric = watch.cache_metric(repo, {watch.CACHE_ENV: str(shared)})

    assert metric["source"] == "shared"
    assert metric["path"] == str(effective.resolve())
    assert metric["source_lock_sha256"] == digest
    assert metric["files"] == 1
    assert metric["bytes"] == 3
    assert tile.read_bytes() == before


def test_historical_cache_is_the_fallback(tmp_path):
    repo = tmp_path / "repo"
    lock = repo / "pipeline" / "geo" / "sources.lock"
    lock.parent.mkdir(parents=True)
    lock.write_text("lock", encoding="utf-8")
    metric = watch.cache_metric(repo, {})
    assert metric["source"] == "historical"
    assert metric["path"] == str((repo / watch.HISTORICAL_CACHE).resolve())
    assert metric["exists"] is False
    assert metric["age_seconds"] is None


def test_relative_shared_cache_root_is_refused(tmp_path):
    lock = tmp_path / "pipeline" / "geo" / "sources.lock"
    lock.parent.mkdir(parents=True)
    lock.write_text("lock", encoding="utf-8")
    with pytest.raises(watch.WatchError, match="chemin absolu"):
        watch.cache_metric(tmp_path, {watch.CACHE_ENV: "cache/relatif"})


def test_tree_age_comes_from_the_newest_file(tmp_path):
    older = tmp_path / "old.tif"
    newer = tmp_path / "new.tif"
    older.write_bytes(b"1")
    newer.write_bytes(b"22")
    now = time.time()
    os.utime(older, (now - 300, now - 300))
    os.utime(newer, (now - 120, now - 120))
    metric = watch._measure_tree(tmp_path, now=now)
    assert metric["files"] == 2
    assert metric["bytes"] == 3
    assert 119 <= metric["age_seconds"] <= 121


def test_dashboard_age_remains_part_of_the_daily_measure(tmp_path):
    dashboard = tmp_path / "hermes" / "DASHBOARD.md"
    dashboard.parent.mkdir(parents=True)
    dashboard.write_text(
        "# dashboard\n\n> Générée le 2026-08-22 12:00 UTC.\n",
        encoding="utf-8",
    )
    now = watch.datetime(2026, 8, 22, 12, 2, tzinfo=watch.timezone.utc).timestamp()
    metric = watch.dashboard_metric(tmp_path, now=now)
    assert metric["exists"] is True
    assert metric["generated_at"] == "2026-08-22T12:00:00Z"
    assert metric["age_seconds"] == 120


def _report(status="ok"):
    return {
        "schema_version": 1,
        "created_at": "2026-08-22T00:00:00Z",
        "status": status,
        "alerts": [] if status == "ok" else ["test rouge"],
        "git": {
            "branch": "master",
            "head": "a" * 40,
            "changed_paths": 0,
            "worktree_count": 1,
            "worktrees": [{"path": "/repo", "branch": "master"}],
        },
        "disk": {
            "total_bytes": 100,
            "used_bytes": 50,
            "free_bytes": 50,
            "free_percent": 50.0,
        },
        "dashboard": {
            "exists": True,
            "path": "/repo/hermes/DASHBOARD.md",
            "generated_at": "2026-08-22T00:00:00Z",
            "age_seconds": 60,
        },
        "dem_cache": {
            "source": "historical",
            "path": "/repo/cache",
            "source_lock_sha256": "b" * 64,
            "exists": False,
            "files": 0,
            "bytes": 0,
            "age_seconds": None,
        },
        "checks": [],
        "destructive_actions": 0,
    }


def test_main_is_silent_when_everything_is_green(monkeypatch, capsys, tmp_path):
    monkeypatch.setattr(watch, "collect_report", lambda *args, **kwargs: _report())
    monkeypatch.setattr(watch, "write_report", lambda *args, **kwargs: tmp_path / "report")
    assert watch.main(["--repo", str(tmp_path), "--metrics-only"]) == 0
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == ""


def test_main_emits_only_an_alert_on_failure(monkeypatch, capsys, tmp_path):
    monkeypatch.setattr(
        watch, "collect_report", lambda *args, **kwargs: _report("alert")
    )
    monkeypatch.setattr(watch, "write_report", lambda *args, **kwargs: tmp_path / "report")
    assert watch.main(["--repo", str(tmp_path), "--metrics-only"]) == 1
    captured = capsys.readouterr()
    assert captured.out == ""
    assert "test rouge" in captured.err


def test_report_is_confined_to_hermes_propositions(tmp_path):
    (tmp_path / "hermes" / "propositions").mkdir(parents=True)
    with pytest.raises(watch.WatchError, match="hors de hermes/propositions"):
        watch.write_report(tmp_path, Path("outside.md"), _report())


def test_watch_implementation_contains_no_automatic_deletion():
    source = MODULE_PATH.read_text(encoding="utf-8")
    assert "shutil.rmtree" not in source
    assert ".unlink(" not in source
    assert "rm -" not in source
    wrapper = (REPO_ROOT / "hermes" / "crons" / "quotidien.sh").read_text(
        encoding="utf-8"
    )
    assert " rm " not in wrapper
    assert "git push" not in wrapper
    assert "gh pr merge" not in wrapper
