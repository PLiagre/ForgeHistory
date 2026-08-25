"""Politique de cache DEM partagé et verrou inter-processus.

Le chemin historique reste le repli. Lorsqu'une racine partagée est fournie,
le SHA256 complet de ``sources.lock`` isole les jeux de sources incompatibles.
"""

from __future__ import annotations

import hashlib
import os
import time
from contextlib import contextmanager
from pathlib import Path
from typing import Iterator, Mapping, Optional, Set

DEM_CACHE_ROOT_ENV = "FORGEHISTORY_DEM_CACHE_ROOT"


def source_lock_sha256(lock_path: Path) -> str:
    digest = hashlib.sha256()
    with Path(lock_path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_dem_cache_dir(
    *,
    geo_root: Path,
    lock_path: Path,
    environ: Optional[Mapping[str, str]] = None,
    cache_root: Optional[Path] = None,
) -> Path:
    """Retourne le cache effectif, clé par lock hors du repli historique."""
    env = os.environ if environ is None else environ
    configured = cache_root
    if configured is None:
        raw = str(env.get(DEM_CACHE_ROOT_ENV, "")).strip()
        configured = Path(raw).expanduser() if raw else None
    if configured is None:
        return Path(geo_root) / "sources" / "dem_cache"
    return Path(configured) / source_lock_sha256(Path(lock_path))


def unexpected_tif_files(cache_dir: Path, expected_names: Set[str]) -> list[str]:
    """Liste les rasters dont le chemin exact n'est pas déclaré dans le lock."""
    root = Path(cache_dir)
    if not root.is_dir():
        return []
    expected_paths = {
        (root / Path(name).stem / name).resolve() for name in expected_names
    }
    return sorted(
        str(path)
        for path in root.rglob("*.tif")
        if path.resolve() not in expected_paths
    )


def _try_lock(handle) -> bool:
    handle.seek(0)
    if os.name == "nt":
        import msvcrt

        try:
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            return True
        except OSError:
            return False

    import fcntl

    try:
        fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        return True
    except BlockingIOError:
        return False


def _unlock(handle) -> None:
    handle.seek(0)
    if os.name == "nt":
        import msvcrt

        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
        return

    import fcntl

    fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


@contextmanager
def exclusive_download_lock(
    target: Path, *, timeout_s: float = 300.0, poll_s: float = 0.05
) -> Iterator[Path]:
    """Sérialise un téléchargement sans dépendre d'un lock supprimable.

    Le fichier ``.lock`` peut rester après un crash : seul le verrou du noyau
    fait autorité, donc un fichier de verrou ancien ne bloque ni n'autorise un
    cache périmé.
    """
    destination = Path(target)
    lock_path = destination.with_name(destination.name + ".lock")
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    handle = lock_path.open("a+b")
    acquired = False
    try:
        handle.seek(0, os.SEEK_END)
        if handle.tell() == 0:
            handle.write(b"\0")
            handle.flush()
        deadline = time.monotonic() + timeout_s
        while not acquired:
            acquired = _try_lock(handle)
            if acquired:
                break
            if time.monotonic() >= deadline:
                raise TimeoutError(
                    f"verrou de telechargement indisponible apres {timeout_s:.1f}s: "
                    f"{lock_path}"
                )
            time.sleep(poll_s)
        yield lock_path
    finally:
        if acquired:
            _unlock(handle)
        handle.close()
