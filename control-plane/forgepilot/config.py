from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import tomllib


@dataclass(frozen=True)
class Settings:
    project_id: str
    engine_repository: str
    city_repository: str
    default_base_ref: str
    default_base_branch: str
    grok_binary: str
    cursor_binary: str
    grok_model: str
    cursor_model: str
    timeout_seconds: int


def default_config_path() -> Path:
    return Path(__file__).resolve().parent.parent / "config.toml"


def load_settings(path: Path | str | None = None) -> Settings:
    config_path = Path(path) if path is not None else default_config_path()
    with config_path.open("rb") as stream:
        raw = tomllib.load(stream)

    project = raw["project"]
    tools = raw["tools"]
    return Settings(
        project_id=str(project["id"]),
        engine_repository=str(project["engine_repository"]),
        city_repository=str(project["city_repository"]),
        default_base_ref=str(project["default_base_ref"]),
        default_base_branch=str(project["default_base_branch"]),
        grok_binary=str(tools.get("grok_binary", "grok")),
        cursor_binary=str(tools.get("cursor_binary", "agent")),
        grok_model=str(tools.get("grok_model", "")),
        cursor_model=str(tools.get("cursor_model", "auto")),
        timeout_seconds=int(tools.get("timeout_seconds", 1800)),
    )
