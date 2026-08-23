from __future__ import annotations

from dataclasses import asdict, dataclass
from fnmatch import fnmatchcase
import hashlib
from pathlib import Path, PurePosixPath
import re
from typing import Iterable
import tomllib

from .process import PilotError


RISK_LEVELS = ("R0", "R1", "R2")
ROLE_NAMES = ("planner", "executor", "reviewer")
TIMEOUT_NAMES = ("planner", "executor", "reviewer", "proof")
TEST_PROFILES = ("fast", "pr", "certify")
BACKENDS = {
    "planner": {"claude", "none"},
    "executor": {"cursor", "none"},
    "reviewer": {"claude", "none"},
}
EFFORT_LEVELS = ("low", "medium", "high", "xhigh", "max")


def _strip_relative_prefix(value: str) -> str:
    while value.startswith("./"):
        value = value[2:]
    return value


@dataclass(frozen=True)
class PolicyRole:
    backend: str
    model: str = ""
    effort: str = ""
    resume: bool = False


@dataclass(frozen=True)
class ControllerPolicy:
    backend: str
    provider: str
    model: str
    can_plan: bool
    can_review: bool
    can_merge: bool


@dataclass(frozen=True)
class RoleTimeouts:
    planner: int
    executor: int
    reviewer: int
    proof: int

    def for_role(self, role: str) -> int:
        if role not in TIMEOUT_NAMES:
            raise PilotError(f"Délai demandé pour un rôle inconnu : {role}")
        return int(getattr(self, role))


@dataclass(frozen=True)
class RiskProfile:
    name: str
    test_profile: str
    roles: dict[str, PolicyRole]
    timeouts: RoleTimeouts


@dataclass(frozen=True)
class WorkflowPolicy:
    path: Path
    version: int
    sha256: str
    review_bundle_max_bytes: int
    controller: ControllerPolicy
    r0_allowlist: tuple[str, ...]
    r2_paths: tuple[str, ...]
    generated_artifacts: tuple[str, ...]
    risks: dict[str, RiskProfile]

    def profile(self, risk: str) -> RiskProfile:
        try:
            return self.risks[risk]
        except KeyError as exc:
            raise PilotError(
                f"Risque inconnu {risk!r} ; niveaux valides : R0, R1, R2."
            ) from exc

    def summary(self) -> dict[str, object]:
        return {
            "path": str(self.path),
            "version": self.version,
            "sha256": self.sha256,
            "review_bundle_max_bytes": self.review_bundle_max_bytes,
            "controller": asdict(self.controller),
            "risks": {
                name: {
                    "test_profile": profile.test_profile,
                    "timeouts": asdict(profile.timeouts),
                    "roles": {
                        role: asdict(value) for role, value in profile.roles.items()
                    },
                }
                for name, profile in self.risks.items()
            },
        }


def default_policy_path() -> Path:
    return Path(__file__).resolve().parent.parent / "workflow-policy.toml"


def _string_list(raw: object, field: str, *, allow_empty: bool = False) -> tuple[str, ...]:
    if not isinstance(raw, list) or (not raw and not allow_empty):
        raise PilotError(f"Politique invalide : {field} doit être une liste non vide.")
    result: list[str] = []
    for item in raw:
        if not isinstance(item, str) or not item.strip():
            raise PilotError(f"Politique invalide : valeur vide dans {field}.")
        value = _strip_relative_prefix(item.replace("\\", "/"))
        pure = PurePosixPath(value)
        if pure.is_absolute() or re.match(r"^[A-Za-z]:/", value) or value.startswith("//") or ".." in pure.parts:
            raise PilotError(f"Politique invalide : motif dangereux {item!r} dans {field}.")
        result.append(value)
    return tuple(result)


def _positive_int(raw: object, field: str) -> int:
    if isinstance(raw, bool) or not isinstance(raw, int) or raw <= 0:
        raise PilotError(f"Politique invalide : {field} doit être un entier positif.")
    return raw


def _load_role(risk: str, name: str, raw: object) -> PolicyRole:
    if not isinstance(raw, dict):
        raise PilotError(f"Politique invalide : [risks.{risk}.roles.{name}] absent.")
    backend = raw.get("backend")
    if not isinstance(backend, str) or backend not in BACKENDS[name]:
        accepted = ", ".join(sorted(BACKENDS[name]))
        raise PilotError(
            f"Backend incompatible pour {risk}.{name} : {backend!r} ; attendu {accepted}."
        )
    model = raw.get("model", "")
    effort = raw.get("effort", "")
    resume = raw.get("resume", False)
    if not isinstance(model, str) or not isinstance(effort, str) or not isinstance(resume, bool):
        raise PilotError(f"Politique invalide : types incorrects pour {risk}.{name}.")
    if effort and effort not in EFFORT_LEVELS:
        raise PilotError(f"Effort invalide {effort!r} pour {risk}.{name}.")
    if name == "executor" and effort:
        raise PilotError("Cursor ne possède pas de drapeau d'effort séparé.")
    if resume and not (name == "executor" and backend == "cursor"):
        raise PilotError(
            f"Reprise incompatible pour {risk}.{name} : seul l'exécuteur Cursor la supporte."
        )
    if backend == "none" and (model or effort or resume):
        raise PilotError(f"Le backend none de {risk}.{name} ne peut définir modèle, effort ou reprise.")
    return PolicyRole(backend=backend, model=model, effort=effort, resume=resume)


def load_policy(path: Path | str | None = None) -> WorkflowPolicy:
    policy_path = Path(path) if path is not None else default_policy_path()
    try:
        with policy_path.open("rb") as stream:
            raw = tomllib.load(stream)
    except FileNotFoundError as exc:
        raise PilotError(f"Politique de workflow introuvable : {policy_path}") from exc
    except tomllib.TOMLDecodeError as exc:
        raise PilotError(f"Politique TOML invalide : {exc}") from exc

    policy_raw = raw.get("policy")
    controller_raw = raw.get("controller")
    classification = raw.get("classification")
    risks_raw = raw.get("risks")
    if (
        not isinstance(policy_raw, dict)
        or not isinstance(controller_raw, dict)
        or not isinstance(classification, dict)
    ):
        raise PilotError(
            "Politique invalide : sections [policy], [controller] et [classification] requises."
        )
    if not isinstance(risks_raw, dict) or set(risks_raw) != set(RISK_LEVELS):
        raise PilotError("Politique invalide : profils R0, R1 et R2 exactement requis.")

    version = _positive_int(policy_raw.get("version"), "policy.version")
    if version != 1:
        raise PilotError(f"Version de politique non supportée : {version}.")
    bundle_limit = _positive_int(
        policy_raw.get("review_bundle_max_bytes"),
        "policy.review_bundle_max_bytes",
    )
    if bundle_limit < 4096:
        raise PilotError("Politique invalide : le bundle de revue doit autoriser au moins 4096 octets.")
    controller_fields = {
        "backend": controller_raw.get("backend"),
        "provider": controller_raw.get("provider"),
        "model": controller_raw.get("model"),
        "can_plan": controller_raw.get("can_plan"),
        "can_review": controller_raw.get("can_review"),
        "can_merge": controller_raw.get("can_merge"),
    }
    if controller_fields["backend"] != "hermes" or controller_fields["provider"] != "nous_portal":
        raise PilotError("Contrôleur incompatible : Hermes via Nous Portal est requis.")
    if not isinstance(controller_fields["model"], str) or not controller_fields["model"]:
        raise PilotError("Politique invalide : controller.model doit être une chaîne non vide.")
    if any(controller_fields[name] is not False for name in ("can_plan", "can_review", "can_merge")):
        raise PilotError(
            "Frontière invalide : Hermes pilote mais ne planifie, ne juge et ne fusionne pas."
        )
    controller = ControllerPolicy(**controller_fields)  # type: ignore[arg-type]

    profiles: dict[str, RiskProfile] = {}
    for risk in RISK_LEVELS:
        profile_raw = risks_raw[risk]
        if not isinstance(profile_raw, dict):
            raise PilotError(f"Politique invalide : profil {risk} incorrect.")
        test_profile = profile_raw.get("test_profile")
        if test_profile not in TEST_PROFILES:
            raise PilotError(
                f"Profil de tests invalide pour {risk} : {test_profile!r}."
            )
        roles_raw = profile_raw.get("roles")
        if not isinstance(roles_raw, dict) or set(roles_raw) != set(ROLE_NAMES):
            raise PilotError(f"Politique invalide : trois rôles exactement requis pour {risk}.")
        roles = {name: _load_role(risk, name, roles_raw[name]) for name in ROLE_NAMES}
        if risk != "R0" and any(value.backend == "none" for value in roles.values()):
            raise PilotError(f"Politique invalide : aucun rôle {risk} ne peut utiliser none.")
        timeouts_raw = profile_raw.get("timeouts")
        if not isinstance(timeouts_raw, dict) or set(timeouts_raw) != set(TIMEOUT_NAMES):
            raise PilotError(f"Politique invalide : quatre délais exactement requis pour {risk}.")
        timeouts = RoleTimeouts(
            **{
                name: _positive_int(timeouts_raw[name], f"risks.{risk}.timeouts.{name}")
                for name in TIMEOUT_NAMES
            }
        )
        profiles[risk] = RiskProfile(risk, test_profile, roles, timeouts)

    return WorkflowPolicy(
        path=policy_path.resolve(),
        version=version,
        sha256=hashlib.sha256(policy_path.read_bytes()).hexdigest(),
        review_bundle_max_bytes=bundle_limit,
        controller=controller,
        r0_allowlist=_string_list(classification.get("r0_allowlist"), "classification.r0_allowlist"),
        r2_paths=_string_list(classification.get("r2_paths"), "classification.r2_paths"),
        generated_artifacts=_string_list(
            classification.get("generated_artifacts", []),
            "classification.generated_artifacts",
            allow_empty=True,
        ),
        risks=profiles,
    )


def normalize_repo_path(path: str | Path) -> str:
    value = _strip_relative_prefix(str(path).replace("\\", "/"))
    pure = PurePosixPath(value)
    if (
        not value
        or pure.is_absolute()
        or re.match(r"^[A-Za-z]:/", value)
        or value.startswith("//")
        or ".." in pure.parts
    ):
        raise PilotError(f"Chemin de dépôt invalide : {path!r}")
    return value


def path_matches(path: str | Path, pattern: str) -> bool:
    value = normalize_repo_path(path)
    normalized_pattern = _strip_relative_prefix(pattern.replace("\\", "/"))
    # fnmatch ne donne pas à ** une sémantique spéciale, mais accepte bien les
    # segments multiples. Le second essai couvre `foo/**/bar` sans segment.
    candidates = {
        normalized_pattern,
        normalized_pattern.replace("/**/", "/"),
    }
    if normalized_pattern.startswith("**/"):
        candidates.add(normalized_pattern[3:])
    return any(fnmatchcase(value, candidate) for candidate in candidates)


def derive_risk(policy: WorkflowPolicy, paths: Iterable[str | Path]) -> str:
    normalized = tuple(normalize_repo_path(path) for path in paths)
    if not normalized:
        return "R1"

    def static_prefix(pattern: str) -> str:
        wildcard = min(
            (index for index in (pattern.find("*"), pattern.find("?"), pattern.find("[")) if index >= 0),
            default=len(pattern),
        )
        return pattern[:wildcard].rstrip("/")

    def may_intersect_sensitive(path: str, pattern: str) -> bool:
        if not any(marker in path for marker in ("*", "?", "[")):
            return path_matches(path, pattern)
        left = static_prefix(path)
        right = static_prefix(pattern)
        return not left or not right or left.startswith(right) or right.startswith(left)

    if any(
        any(may_intersect_sensitive(path, pattern) for pattern in policy.r2_paths)
        for path in normalized
    ):
        return "R2"
    if all(any(path_matches(path, pattern) for pattern in policy.r0_allowlist) for path in normalized):
        return "R0"
    return "R1"


def effective_risk(
    policy: WorkflowPolicy,
    requested: str,
    paths: Iterable[str | Path],
) -> tuple[str, str]:
    if requested not in RISK_LEVELS:
        raise PilotError(f"Risque demandé invalide {requested!r}.")
    derived = derive_risk(policy, paths)
    effective = RISK_LEVELS[max(RISK_LEVELS.index(requested), RISK_LEVELS.index(derived))]
    return effective, derived
