#!/usr/bin/env python3
"""Veille quotidienne Hermes, sans agent et silencieuse sur le chemin vert.

Le script ne supprime, ne committe et ne pousse rien. Il mesure l'état local,
écrit le rapport git-ignoré ``DERNIERE-VEILLE.md`` de façon atomique, puis ne
produit aucune sortie si les contrôles sont verts. Une alerte sort sur stderr
avec un code non nul afin que le contrôleur puisse la transmettre à Discord.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Iterable, Mapping, Sequence


CACHE_ENV = "FORGEHISTORY_DEM_CACHE_ROOT"
DEFAULT_REPORT = Path("hermes/propositions/DERNIERE-VEILLE.md")
HISTORICAL_CACHE = Path("tools/map/sources/dem_cache")
SOURCE_LOCK = Path("tools/map/sources.lock")
MIN_FREE_BYTES = 5 * 1024**3
_DASHBOARD_TIMESTAMP_RE = re.compile(
    r"Générée le (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) UTC"
)


class WatchError(RuntimeError):
    """Refus d'exploitation explicite."""


def _run(
    argv: Sequence[str],
    *,
    cwd: Path,
    timeout: int = 600,
) -> dict[str, object]:
    started = time.monotonic()
    try:
        completed = subprocess.run(
            list(argv),
            cwd=cwd,
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
            check=False,
            timeout=timeout,
        )
        return {
            "argv": list(argv),
            "code": completed.returncode,
            "duration_seconds": round(time.monotonic() - started, 6),
            "stdout": completed.stdout,
            "stderr": completed.stderr,
        }
    except (OSError, subprocess.TimeoutExpired) as exc:
        return {
            "argv": list(argv),
            "code": 124 if isinstance(exc, subprocess.TimeoutExpired) else 127,
            "duration_seconds": round(time.monotonic() - started, 6),
            "stdout": "",
            "stderr": str(exc),
        }


def _git_output(
    repo: Path,
    arguments: Sequence[str],
    runner: Callable[..., dict[str, object]],
) -> str:
    result = runner(["git", *arguments], cwd=repo)
    if result["code"] != 0:
        detail = str(result.get("stderr") or result.get("stdout") or "sans détail").strip()
        raise WatchError(f"git {' '.join(arguments)} a échoué : {detail}")
    return str(result["stdout"]).strip()


def parse_worktrees(porcelain: str) -> list[dict[str, object]]:
    """Parse ``git worktree list --porcelain`` sans deviner de champ."""

    records: list[dict[str, object]] = []
    current: dict[str, object] = {}
    for line in [*porcelain.splitlines(), ""]:
        if not line:
            if current:
                if "path" not in current:
                    raise WatchError("sortie git worktree sans chemin")
                records.append(current)
                current = {}
            continue
        key, _, value = line.partition(" ")
        if key == "worktree":
            current["path"] = value
        elif key == "HEAD":
            current["head"] = value
        elif key == "branch":
            current["branch"] = value.removeprefix("refs/heads/")
        elif key in {"bare", "detached", "prunable", "locked"}:
            current[key] = value or True
    return records


def _measure_tree(path: Path, *, now: float | None = None) -> dict[str, object]:
    """Mesure taille et fraîcheur sans suivre de lien ni modifier le cache."""

    if not path.exists():
        return {
            "exists": False,
            "files": 0,
            "bytes": 0,
            "age_seconds": None,
        }
    clock = time.time() if now is None else now
    files = 0
    total = 0
    root_mtime = path.stat(follow_symlinks=False).st_mtime
    newest_file: float | None = None
    stack = [path]
    while stack:
        directory = stack.pop()
        try:
            entries = list(os.scandir(directory))
        except OSError as exc:
            raise WatchError(f"cache illisible {directory} : {exc}") from exc
        for entry in entries:
            try:
                if entry.is_symlink():
                    continue
                stat = entry.stat(follow_symlinks=False)
                if entry.is_dir(follow_symlinks=False):
                    stack.append(Path(entry.path))
                elif entry.is_file(follow_symlinks=False):
                    files += 1
                    total += stat.st_size
                    newest_file = (
                        stat.st_mtime
                        if newest_file is None
                        else max(newest_file, stat.st_mtime)
                    )
            except OSError as exc:
                raise WatchError(f"entrée de cache illisible {entry.path} : {exc}") from exc
    return {
        "exists": True,
        "files": files,
        "bytes": total,
        "age_seconds": max(0, int(clock - (newest_file or root_mtime))),
    }


def cache_metric(
    repo: Path,
    environ: Mapping[str, str] | None = None,
) -> dict[str, object]:
    environ = os.environ if environ is None else environ
    lock_path = repo / SOURCE_LOCK
    try:
        lock_digest = hashlib.sha256(lock_path.read_bytes()).hexdigest()
    except OSError as exc:
        raise WatchError(f"sources.lock illisible : {exc}") from exc

    configured = environ.get(CACHE_ENV, "").strip()
    if configured:
        root = Path(configured).expanduser()
        if not root.is_absolute():
            raise WatchError(f"{CACHE_ENV} doit être un chemin absolu")
        path = root / lock_digest
        source = "shared"
    else:
        path = repo / HISTORICAL_CACHE
        source = "historical"
    metric = _measure_tree(path)
    return {
        "source": source,
        "path": str(path.resolve()),
        "source_lock_sha256": lock_digest,
        **metric,
    }


def disk_metric(repo: Path) -> dict[str, object]:
    usage = shutil.disk_usage(repo)
    free_percent = 100.0 if usage.total == 0 else usage.free * 100.0 / usage.total
    return {
        "total_bytes": usage.total,
        "used_bytes": usage.used,
        "free_bytes": usage.free,
        "free_percent": round(free_percent, 3),
    }


def dashboard_metric(repo: Path, *, now: float | None = None) -> dict[str, object]:
    path = repo / "hermes" / "DASHBOARD.md"
    if not path.is_file():
        return {"exists": False, "path": str(path), "age_seconds": None}
    clock = time.time() if now is None else now
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        raise WatchError(f"dashboard illisible : {exc}") from exc
    match = _DASHBOARD_TIMESTAMP_RE.search(text)
    if match is None:
        return {
            "exists": True,
            "path": str(path),
            "generated_at": None,
            "age_seconds": None,
        }
    generated = datetime.strptime(match.group(1), "%Y-%m-%d %H:%M").replace(
        tzinfo=timezone.utc
    )
    return {
        "exists": True,
        "path": str(path),
        "generated_at": generated.isoformat().replace("+00:00", "Z"),
        "age_seconds": max(0, int(clock - generated.timestamp())),
    }


def collect_report(
    repo: Path | str,
    *,
    run_checks: bool = True,
    environ: Mapping[str, str] | None = None,
    runner: Callable[..., dict[str, object]] = _run,
) -> dict[str, object]:
    repo_path = Path(repo).resolve()
    if not (repo_path / "sim" / "__main__.py").is_file() or not (
        repo_path / "hermes" / "crons"
    ).is_dir():
        raise WatchError(f"racine ForgeHistory refusée : {repo_path}")

    porcelain = _git_output(repo_path, ["status", "--porcelain"], runner)
    worktree_text = _git_output(repo_path, ["worktree", "list", "--porcelain"], runner)
    worktrees = parse_worktrees(worktree_text)
    git = {
        "branch": _git_output(repo_path, ["branch", "--show-current"], runner),
        "head": _git_output(repo_path, ["rev-parse", "HEAD"], runner),
        "changed_paths": len([line for line in porcelain.splitlines() if line]),
        "worktree_count": len(worktrees),
        "worktrees": worktrees,
    }
    disk = disk_metric(repo_path)
    cache = cache_metric(repo_path, environ)
    dashboard = dashboard_metric(repo_path)

    checks: list[dict[str, object]] = []
    if run_checks:
        checks.append(
            {
                "name": "sim-smoke",
                **runner(
                    [sys.executable, "-m", "sim", "--ticks", "0", "--json"],
                    cwd=repo_path,
                    timeout=180,
                ),
            }
        )
        checks.append(
            {
                "name": "sim-tests",
                **runner(
                    [sys.executable, "-m", "pytest", "sim/tests/", "-q", "--tb=no"],
                    cwd=repo_path,
                    timeout=600,
                ),
            }
        )

    alerts: list[str] = []
    if int(disk["free_bytes"]) < MIN_FREE_BYTES:
        alerts.append(
            f"espace disque libre sous {_gib(MIN_FREE_BYTES)} "
            f"({disk['free_percent']} % restant)"
        )
    if any(bool(worktree.get("prunable")) for worktree in worktrees):
        alerts.append("au moins un worktree est déclaré prunable par Git")
    for check in checks:
        if check["code"] != 0:
            alerts.append(f"contrôle {check['name']} en échec (code {check['code']})")

    return {
        "schema_version": 1,
        "created_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": "ok" if not alerts else "alert",
        "alerts": alerts,
        "git": git,
        "disk": disk,
        "dashboard": dashboard,
        "dem_cache": cache,
        "checks": checks,
        "destructive_actions": 0,
    }


def _gib(value: object) -> str:
    return f"{int(value) / (1024 ** 3):.2f} Gio"


def _age(value: object) -> str:
    if value is None:
        return "absent"
    seconds = int(value)
    if seconds < 3600:
        return f"{seconds // 60} min"
    if seconds < 86400:
        return f"{seconds / 3600:.1f} h"
    return f"{seconds / 86400:.1f} j"


def render_markdown(report: Mapping[str, object]) -> str:
    git = report["git"]
    disk = report["disk"]
    cache = report["dem_cache"]
    dashboard = report["dashboard"]
    assert (
        isinstance(git, dict)
        and isinstance(disk, dict)
        and isinstance(cache, dict)
        and isinstance(dashboard, dict)
    )
    lines = [
        "---",
        "author: hermes",
        "kind: proposition",
        f"created_at: {report['created_at']}",
        "concerns: projet",
        "status: OPEN",
        "---",
        f"# Veille quotidienne — {report['created_at']}",
        "",
        "Rapport local généré par un script, sans agent, commit, push, fusion ni suppression.",
        "",
        f"- état : `{str(report['status']).upper()}`",
        f"- branche / HEAD : `{git['branch']}` / `{git['head']}`",
        f"- changements locaux : `{git['changed_paths']}`",
        f"- worktrees : `{git['worktree_count']}`",
        f"- disque libre : `{_gib(disk['free_bytes'])}` (`{disk['free_percent']} %`)",
        f"- âge du tableau de bord : `{_age(dashboard['age_seconds'])}`",
        "",
        "## Cache DEM",
        "",
        f"- mode : `{cache['source']}`",
        f"- chemin effectif : `{cache['path']}`",
        f"- empreinte de sources : `{cache['source_lock_sha256']}`",
        f"- présent / fichiers / taille : `{cache['exists']}` / `{cache['files']}` / `{_gib(cache['bytes'])}`",
        f"- âge du fichier le plus récent : `{_age(cache['age_seconds'])}`",
        "",
        "## Worktrees",
        "",
    ]
    worktrees = git["worktrees"]
    assert isinstance(worktrees, list)
    if worktrees:
        for item in worktrees:
            assert isinstance(item, dict)
            flags = ", ".join(
                name for name in ("detached", "locked", "prunable", "bare") if item.get(name)
            ) or "actif"
            lines.append(
                f"- `{item.get('path')}` — `{item.get('branch', 'sans branche')}` — {flags}"
            )
    else:
        lines.append("- aucun worktree mesuré (état anormal à vérifier)")
    lines.extend(["", "## Contrôles", ""])
    checks = report["checks"]
    assert isinstance(checks, list)
    if checks:
        for check in checks:
            assert isinstance(check, dict)
            lines.append(
                f"- `{check['name']}` : code `{check['code']}`, durée `{check['duration_seconds']} s`"
            )
    else:
        lines.append("- contrôles produit non lancés (`--metrics-only`)")
    alerts = report["alerts"]
    assert isinstance(alerts, list)
    if alerts:
        lines.extend(["", "## Alertes", ""])
        lines.extend(f"- {alert}" for alert in alerts)
    lines.extend(
        [
            "",
            "Ce fichier est une mesure locale, jamais une instruction ni un verdict.",
            "",
        ]
    )
    return "\n".join(lines)


def write_report(repo: Path, output: Path, report: Mapping[str, object]) -> Path:
    destination = output if output.is_absolute() else repo / output
    destination = destination.resolve()
    allowed = (repo / "hermes" / "propositions").resolve()
    try:
        destination.relative_to(allowed)
    except ValueError as exc:
        raise WatchError(f"rapport hors de hermes/propositions : {destination}") from exc
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(f".{destination.name}.{os.getpid()}.tmp")
    temporary.write_text(render_markdown(report), encoding="utf-8")
    os.replace(temporary, destination)
    return destination


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, default=DEFAULT_REPORT)
    parser.add_argument(
        "--metrics-only",
        action="store_true",
        help="mesurer sans lancer le smoke ni les tests sim",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="afficher explicitement le rapport JSON (désactive le silence voulu)",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        repo = args.repo.resolve()
        report = collect_report(repo, run_checks=not args.metrics_only)
        write_report(repo, args.output, report)
    except WatchError as exc:
        print(f"veille Hermes refusée : {exc}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(report, ensure_ascii=False, sort_keys=True))
    if report["status"] != "ok":
        alerts = report["alerts"]
        assert isinstance(alerts, list)
        print("veille Hermes en alerte : " + "; ".join(map(str, alerts)), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
