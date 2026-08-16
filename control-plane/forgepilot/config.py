from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import tomllib

from .process import PilotError


ROLES = ("planner", "reviewer", "executor")
EFFORT_LEVELS = ("low", "medium", "high", "xhigh", "max")

CURSOR_EFFORT_REFUSED = (
    "Cursor cuit l'effort dans le nom du modèle ; "
    "aucun drapeau --effort séparé n'existe pour l'exécutant."
)


def assert_valid_effort(effort: str) -> None:
    """Refuse un niveau hors liste (config.toml ou drapeau --effort)."""
    if effort and effort not in EFFORT_LEVELS:
        raise PilotError(
            f"Niveau d'effort invalide {effort!r} ; "
            "niveaux acceptés : low, medium, high, xhigh, max."
        )


@dataclass(frozen=True)
class RoleSettings:
    model: str = ""
    effort: str = ""


@dataclass(frozen=True)
class Settings:
    project_id: str
    engine_repository: str
    city_repository: str
    default_base_ref: str
    default_base_branch: str
    claude_binary: str
    cursor_binary: str
    claude_model: str
    cursor_model: str
    timeout_seconds: int
    roles: dict[str, RoleSettings] = field(default_factory=dict)


def default_config_path() -> Path:
    return Path(__file__).resolve().parent.parent / "config.toml"


def load_settings(path: Path | str | None = None) -> Settings:
    config_path = Path(path) if path is not None else default_config_path()
    with config_path.open("rb") as stream:
        raw = tomllib.load(stream)

    project = raw["project"]
    tools = raw["tools"]
    roles: dict[str, RoleSettings] = {}
    for name, section in raw.get("roles", {}).items():
        if name not in ROLES:
            raise PilotError(
                f"Rôle inconnu {name!r} ; rôles valides : planner, reviewer, executor."
            )
        if not isinstance(section, dict):
            raise PilotError(f"Section [roles.{name}] invalide.")
        if name == "executor" and "effort" in section:
            raise PilotError(CURSOR_EFFORT_REFUSED)
        effort = str(section.get("effort", "") or "")
        assert_valid_effort(effort)
        roles[name] = RoleSettings(
            model=str(section.get("model", "") or ""),
            effort=effort,
        )
    return Settings(
        project_id=str(project["id"]),
        engine_repository=str(project["engine_repository"]),
        city_repository=str(project["city_repository"]),
        default_base_ref=str(project["default_base_ref"]),
        default_base_branch=str(project["default_base_branch"]),
        claude_binary=str(tools.get("claude_binary", "claude")),
        cursor_binary=str(tools.get("cursor_binary", "agent")),
        claude_model=str(tools.get("claude_model", "")),
        cursor_model=str(tools.get("cursor_model", "auto")),
        timeout_seconds=int(tools.get("timeout_seconds", 1800)),
        roles=roles,
    )
